-- ==========================================================================
--  db/postgres/pruebas/prueba_migraciones_v72.sql
--  Proyecto Cátedras · Subversión v7.2 (migraciones 060-066)
-- --------------------------------------------------------------------------
--  Prueba DIRECTAMENTE contra PostgreSQL (sin la API) lo que construyen las
--  migraciones de la v7.2: coordenadas y radio de la sede, distancia, QR que
--  exige sede con coordenadas, registro público de estudiante / docente /
--  externo (con y sin ubicación), carga idempotente de estudiantes y las
--  consultas de exportación. Casos T01-T31 de docs/spec_kit/Versiones/
--  version7_2/v7_2_subversion.md (sección 8) que se resuelven en la BD.
--
--  NO DEJA RASTRO: todo corre dentro de BEGIN ... ROLLBACK.
--
--  Cómo correrlo (con Docker arriba, desde la raíz del repositorio):
--      docker compose cp db/postgres/pruebas/prueba_migraciones_v72.sql postgres:/tmp/p.sql
--      docker compose exec -T postgres psql -U sa -d bdcatedras_postgres_local -v ON_ERROR_STOP=1 -f /tmp/p.sql
-- ==========================================================================

\set QUIET on
\pset footer off
SET client_min_messages TO warning;
BEGIN;
SET LOCAL search_path TO catedras, public;

-- Las sesiones de prueba se crean "ahora" (el QR solo vale cerca del fin de la
-- sesión): se apagan, SOLO dentro de esta transacción, la separación de una hora
-- y las ventanas de programación, que dependen de los datos que haya en la base.
ALTER TABLE sesion DISABLE TRIGGER trg_sesion_separacion;
ALTER TABLE sesion DISABLE TRIGGER trg_sesion_restriccion;

CREATE TEMP TABLE resultado_prueba (n serial, tabla text, caso text, ok boolean, detalle text);

CREATE FUNCTION pg_temp.ok(p_tabla text, p_caso text, p_cond boolean, p_detalle text DEFAULT NULL)
RETURNS void LANGUAGE sql AS $$
    INSERT INTO resultado_prueba (tabla, caso, ok, detalle)
    VALUES (p_tabla, p_caso, COALESCE(p_cond, false), p_detalle);
$$;

-- El SQL DEBE fallar con ese SQLSTATE (y, si se indica, con ese texto en el mensaje).
CREATE FUNCTION pg_temp.falla(p_tabla text, p_caso text, p_sql text, p_sqlstate text, p_texto text DEFAULT NULL)
RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    EXECUTE p_sql;
    PERFORM pg_temp.ok(p_tabla, p_caso, false, 'no lanzó ningún error');
EXCEPTION WHEN OTHERS THEN
    PERFORM pg_temp.ok(p_tabla, p_caso,
        SQLSTATE = p_sqlstate AND (p_texto IS NULL OR position(p_texto IN SQLERRM) > 0),
        SQLSTATE || ' ' || SQLERRM);
END;
$$;

-- Un CALL con INOUT devuelve una fila; estas funciones la leen como texto "estado|id|distancia|radio".
CREATE FUNCTION pg_temp.reg_est(p_token uuid, p_carnet text, p_lat numeric DEFAULT NULL, p_lon numeric DEFAULT NULL)
RETURNS text LANGUAGE plpgsql AS $$
DECLARE v_estado text; v_id bigint; v_dist numeric; v_rad integer;
BEGIN
    v_estado := NULL; v_id := NULL; v_dist := NULL; v_rad := NULL;
    CALL sp_registrar_asistencia_estudiante(p_token, p_carnet, p_lat, p_lon, '10.0.0.7'::inet, 'prueba/1.0',
                                            v_estado, v_id, v_dist, v_rad);
    RETURN concat_ws('|', v_estado, coalesce(v_id::text, ''), coalesce(v_dist::text, ''), coalesce(v_rad::text, ''));
END; $$;

CREATE FUNCTION pg_temp.reg_doc(p_token uuid, p_ced text, p_nom text, p_cont text, p_fac text,
                                p_lat numeric DEFAULT NULL, p_lon numeric DEFAULT NULL)
RETURNS text LANGUAGE plpgsql AS $$
DECLARE v_estado text; v_id bigint; v_dist numeric; v_rad integer;
BEGIN
    CALL sp_registrar_asistencia_docente(p_token, p_ced, p_nom, p_cont, p_fac, p_lat, p_lon, '10.0.0.8'::inet, 'prueba/1.0',
                                         v_estado, v_id, v_dist, v_rad);
    RETURN concat_ws('|', v_estado, coalesce(v_id::text, ''), coalesce(v_dist::text, ''), coalesce(v_rad::text, ''));
END; $$;

CREATE FUNCTION pg_temp.reg_ext(p_token uuid, p_ced text, p_nom text, p_cont text, p_proc text,
                                p_lat numeric DEFAULT NULL, p_lon numeric DEFAULT NULL)
RETURNS text LANGUAGE plpgsql AS $$
DECLARE v_estado text; v_id bigint; v_dist numeric; v_rad integer;
BEGIN
    CALL sp_registrar_asistencia_externo(p_token, p_ced, p_nom, p_cont, p_proc, p_lat, p_lon, '10.0.0.9'::inet, 'prueba/1.0',
                                         v_estado, v_id, v_dist, v_rad);
    RETURN concat_ws('|', v_estado, coalesce(v_id::text, ''), coalesce(v_dist::text, ''), coalesce(v_rad::text, ''));
END; $$;

-- Primer campo (el estado) de lo que devuelven las funciones de arriba.
CREATE FUNCTION pg_temp.estado(p text) RETURNS text LANGUAGE sql IMMUTABLE AS $$ SELECT split_part(p, '|', 1) $$;

-- Carga un estudiante y devuelve la acción (CREADO / ACTUALIZADO).
CREATE FUNCTION pg_temp.cargar(p_carnet text, p_cc text, p_prog text, p_per text DEFAULT '2026-2') RETURNS text
LANGUAGE plpgsql AS $$
DECLARE v_id bigint; v_acc text;
BEGIN
    v_id := NULL; v_acc := NULL;
    CALL sp_cargar_estudiante(p_carnet, p_cc, p_prog, p_per, NULL, v_id, v_acc);
    RETURN v_acc;
END; $$;

-- Cátedra + sesión de prueba. Devuelve el id de la sesión. 'fin' = dentro de 5 minutos: la
-- ventana del QR ([fin-15 min, fin)) contiene el instante actual.
CREATE FUNCTION pg_temp.sesion_de_prueba(p_catedra bigint, p_modalidad text, p_sede text) RETURNS bigint
LANGUAGE plpgsql AS $$
DECLARE v bigint;
BEGIN
    INSERT INTO sesion (fk_catedra, inicio, fin, fk_modalidad, fk_sede, fk_periodo)
    VALUES (p_catedra, now() - interval '55 minutes', now() + interval '5 minutes', p_modalidad, p_sede, '2026-2')
    RETURNING id_sesion INTO v;
    RETURN v;
END; $$;

-- Emite el QR de una sesión y devuelve su token.
CREATE FUNCTION pg_temp.qr_de(p_sesion bigint) RETURNS uuid LANGUAGE plpgsql AS $$
DECLARE v json;
BEGIN
    CALL sp_emitir_qr_sesion(p_sesion, NULL, false, v);
    RETURN (v ->> 'token')::uuid;
END; $$;

-- Cuántos registros tiene una sesión.
CREATE FUNCTION pg_temp.n_reg(p_sesion bigint) RETURNS bigint LANGUAGE sql AS $$
    SELECT count(*) FROM registro_asistencia WHERE fk_sesion = p_sesion $$;

-- --------------------------------------------------------------------------
--  Datos de prueba (todo se revierte al final)
-- --------------------------------------------------------------------------
CREATE TEMP TABLE ids (k text PRIMARY KEY, v text);

DO $$
DECLARE
    v_cat bigint; v_cat2 bigint; v_tipo text;
BEGIN
    SELECT id_tipo_evento INTO v_tipo FROM tipo_evento ORDER BY 1 LIMIT 1;
    INSERT INTO catedra (id_evento_asis, nombre, fk_tipo_evento) VALUES ('999000001', 'Cátedra v7.2 de prueba', v_tipo)
        RETURNING id_catedra INTO v_cat;
    INSERT INTO catedra (id_evento_asis, nombre, fk_tipo_evento, activo) VALUES ('999000002', 'Cátedra v7.2 inactiva con asistencia', v_tipo, false)
        RETURNING id_catedra INTO v_cat2;
    INSERT INTO ids VALUES ('cat', v_cat::text), ('cat_inactiva', v_cat2::text);

    -- una sede física SIN coordenadas y otra virtual
    INSERT INTO sede (id_sede, nombre, es_virtual) VALUES ('T_SIN_GEO', 'Sede de prueba sin coordenadas', false);
END $$;

-- --------------------------------------------------------------------------
--  1. Sede, parámetro y distancia (060)
-- --------------------------------------------------------------------------
DO $$
DECLARE
    T constant text := 'sede';
    v_json json; v_rad int;
BEGIN
    PERFORM pg_temp.ok('fn_distancia', 'dos puntos iguales -> 0 m', catedras.fn_distancia_metros(6.25, -75.57, 6.25, -75.57) = 0);
    PERFORM pg_temp.ok('fn_distancia', '1 grado de longitud en el ecuador ≈ 111 195 m',
        abs(catedras.fn_distancia_metros(0, 0, 0, 1) - 111195) < 100,
        catedras.fn_distancia_metros(0, 0, 0, 1)::text);
    PERFORM pg_temp.ok('fn_distancia', 'San Benito - Bello ≈ 8,7 km',
        catedras.fn_distancia_metros(6.255095, -75.572969, 6.333129, -75.569174) BETWEEN 8500 AND 8900,
        catedras.fn_distancia_metros(6.255095, -75.572969, 6.333129, -75.569174)::text);
    PERFORM pg_temp.ok('fn_distancia', 'un dato NULL -> NULL', catedras.fn_distancia_metros(1, NULL, 2, 3) IS NULL);
    PERFORM pg_temp.ok('fn_distancia', 'puntos antipodas no rompen asin', catedras.fn_distancia_metros(0, 0, 0, 180) > 20000000);

    PERFORM pg_temp.ok(T, 'BELLO con coordenadas confirmadas',
        (SELECT latitud = 6.333129 AND longitud = -75.569174 FROM sede WHERE id_sede = 'BELLO'));
    PERFORM pg_temp.ok(T, 'SAN_BENITO con coordenadas confirmadas',
        (SELECT latitud = 6.255095 AND longitud = -75.572969 FROM sede WHERE id_sede = 'SAN_BENITO'));
    PERFORM pg_temp.ok(T, 'VIRTUAL sin coordenadas', (SELECT latitud IS NULL AND longitud IS NULL FROM sede WHERE id_sede = 'VIRTUAL'));
    PERFORM pg_temp.ok(T, 'parámetro RADIO_VALIDACION_METROS = 200',
        (SELECT valor FROM parametro WHERE clave = 'RADIO_VALIDACION_METROS') = '200');

    PERFORM pg_temp.falla(T, 'latitud sin longitud -> CHECK', $q$UPDATE sede SET latitud = 6 WHERE id_sede = 'T_SIN_GEO'$q$, '23514');
    PERFORM pg_temp.falla(T, 'latitud fuera de rango -> CHECK', $q$UPDATE sede SET latitud = 91, longitud = 0 WHERE id_sede = 'T_SIN_GEO'$q$, '23514');
    PERFORM pg_temp.falla(T, 'radio menor de 10 -> CHECK', $q$UPDATE sede SET radio_metros = 5 WHERE id_sede = 'BELLO'$q$, '23514');
    PERFORM pg_temp.falla(T, 'sede virtual con coordenadas -> CHECK', $q$UPDATE sede SET latitud = 1, longitud = 1 WHERE id_sede = 'VIRTUAL'$q$, '23514');

    -- radio efectivo en el JSON
    CALL sp_consultar_sede('BELLO', v_json);
    PERFORM pg_temp.ok(T, 'consultar: radio efectivo = parámetro (200) si la sede no tiene radio', (v_json ->> 'radio_efectivo_metros')::int = 200);
    UPDATE sede SET radio_metros = 50 WHERE id_sede = 'BELLO';
    CALL sp_consultar_sede('BELLO', v_json);
    PERFORM pg_temp.ok(T, 'consultar: radio efectivo = el de la sede (50)', (v_json ->> 'radio_efectivo_metros')::int = 50);
    UPDATE sede SET radio_metros = NULL WHERE id_sede = 'BELLO';

    -- compatibilidad: una llamada de 6 argumentos (la de la API anterior) NO borra las coordenadas
    CALL sp_actualizar_sede('BELLO', 'Campus Bello', 'Calle 45 61-40, Bello', false, true, v_json);
    PERFORM pg_temp.ok(T, 'actualizar con la firma anterior conserva las coordenadas',
        (SELECT latitud = 6.333129 FROM sede WHERE id_sede = 'BELLO'));
    CALL sp_actualizar_sede('T_SIN_GEO', 'Sede de prueba sin coordenadas', NULL, false, true, v_json, 6.3, -75.5, 120, true);
    PERFORM pg_temp.ok(T, 'actualizar con p_actualizar_geo = true cambia coordenadas y radio',
        (SELECT latitud = 6.3 AND longitud = -75.5 AND radio_metros = 120 FROM sede WHERE id_sede = 'T_SIN_GEO'));
    CALL sp_actualizar_sede('T_SIN_GEO', 'Sede de prueba sin coordenadas', NULL, false, true, v_json, NULL, NULL, NULL, true);
    PERFORM pg_temp.ok(T, 'actualizar con p_actualizar_geo = true y NULL las borra',
        (SELECT latitud IS NULL AND radio_metros IS NULL FROM sede WHERE id_sede = 'T_SIN_GEO'));
    CALL sp_crear_sede('T_NUEVA', 'Sede nueva de prueba', NULL, false, true, v_json, 6.2, -75.6, 80);
    PERFORM pg_temp.ok(T, 'crear con coordenadas y radio', (SELECT radio_metros = 80 AND latitud = 6.2 FROM sede WHERE id_sede = 'T_NUEVA'));
EXCEPTION WHEN OTHERS THEN
    PERFORM pg_temp.ok('sede', 'bloque terminó sin excepción inesperada', false, SQLSTATE || ' ' || SQLERRM);
END $$;

-- --------------------------------------------------------------------------
--  2. Registro_asistencia, DOCENTE y modalidad (061, 062)
-- --------------------------------------------------------------------------
DO $$
BEGIN
    PERFORM pg_temp.ok('061', 'DOCENTE ya no exige programa', (SELECT NOT exige_programa FROM tipo_vinculacion WHERE id_tipo_vinculacion = 'DOCENTE'));
    PERFORM pg_temp.ok('061', 'ESTUDIANTE sigue exigiendo programa', (SELECT exige_programa FROM tipo_vinculacion WHERE id_tipo_vinculacion = 'ESTUDIANTE'));
    PERFORM pg_temp.ok('062', 'modalidad HIBRIDA fuera de uso (desactivada o ya borrada por la 072)', (SELECT count(*) = 0 OR bool_and(NOT activo) FROM modalidad WHERE id_modalidad = 'HIBRIDA'));
    PERFORM pg_temp.ok('062', 'PRESENCIAL sigue activa', (SELECT activo FROM modalidad WHERE id_modalidad = 'PRESENCIAL'));
END $$;

-- --------------------------------------------------------------------------
--  3. QR: exige sede con coordenadas (062) · T10
-- --------------------------------------------------------------------------
DO $$
DECLARE
    T constant text := 'qr';
    v_cat bigint := (SELECT v::bigint FROM ids WHERE k = 'cat');
    v_ses bigint; v_json json; v_tok uuid;
BEGIN
    -- T10 · presencial en una sede sin coordenadas: falla AL GENERAR el QR, nombrando la sede
    v_ses := pg_temp.sesion_de_prueba(v_cat, 'PRESENCIAL', 'T_SIN_GEO');
    PERFORM pg_temp.falla(T, 'T10 presencial en sede sin coordenadas: no se genera el QR',
        format('DO $x$ DECLARE j json; BEGIN CALL sp_emitir_qr_sesion(%s, NULL, false, j); END $x$', v_ses),
        'P0001', 'T_SIN_GEO');
    PERFORM pg_temp.ok(T, 'T10 ... y no quedó ningún enlace',
        NOT EXISTS (SELECT 1 FROM enlace_registro WHERE fk_sesion = v_ses));

    -- presencial en sede virtual (dato inconsistente): también falla, con su propio mensaje
    v_ses := pg_temp.sesion_de_prueba(v_cat, 'PRESENCIAL', 'VIRTUAL');
    PERFORM pg_temp.falla(T, 'presencial en sede virtual: no se genera el QR',
        format('DO $x$ DECLARE j json; BEGIN CALL sp_emitir_qr_sesion(%s, NULL, false, j); END $x$', v_ses),
        'P0001', 'es virtual');

    -- virtual en sede virtual: canal ENLACE, sin comprobación de coordenadas
    v_ses := pg_temp.sesion_de_prueba(v_cat, 'VIRTUAL', 'VIRTUAL');
    CALL sp_emitir_qr_sesion(v_ses, NULL, false, v_json);
    PERFORM pg_temp.ok(T, 'virtual: se genera el enlace con canal ENLACE', v_json ->> 'canal' = 'ENLACE');

    -- presencial en una sede con coordenadas: canal QR
    v_ses := pg_temp.sesion_de_prueba(v_cat, 'PRESENCIAL', 'BELLO');
    CALL sp_emitir_qr_sesion(v_ses, NULL, false, v_json);
    PERFORM pg_temp.ok(T, 'presencial con coordenadas: se genera el QR con canal QR', v_json ->> 'canal' = 'QR');
    CALL sp_emitir_qr_sesion(v_ses, NULL, false, v_json);
    PERFORM pg_temp.ok(T, 'pedirlo otra vez devuelve el mismo (ya_existia)', (v_json ->> 'ya_existia')::boolean);
END $$;

-- --------------------------------------------------------------------------
--  4. Carga de estudiantes (064): idempotencia, CC ajena, P19 · T16, T17, T31
-- --------------------------------------------------------------------------
DO $$
DECLARE
    T constant text := 'cargar';
    v_id bigint; v_acc text;
BEGIN
    PERFORM pg_temp.ok(T, '1ª carga: CREADO', pg_temp.cargar('T7200001', '9990000001', 'INDS') = 'CREADO');
    PERFORM pg_temp.ok(T, '2ª carga del mismo archivo: ACTUALIZADO', pg_temp.cargar('T7200001', '9990000001', 'INDS') = 'ACTUALIZADO');

    PERFORM pg_temp.ok(T, 'T16 una sola persona', (SELECT count(*) FROM asistente WHERE id_asis = 'T7200001') = 1);
    PERFORM pg_temp.ok(T, 'T16 un solo documento', (SELECT count(*) FROM documento_asistente d JOIN asistente a ON a.id_asistente = d.fk_asistente WHERE a.id_asis = 'T7200001') = 1);
    PERFORM pg_temp.ok(T, 'T16 un solo programa', (SELECT count(*) FROM programa_asistente p JOIN asistente a ON a.id_asistente = p.fk_asistente WHERE a.id_asis = 'T7200001') = 1);
    PERFORM pg_temp.ok(T, 'T16 una sola vinculación ESTUDIANTE vigente',
        (SELECT count(*) FROM vinculacion_asistente v JOIN asistente a ON a.id_asistente = v.fk_asistente
          WHERE a.id_asis = 'T7200001' AND v.fk_tipo_vinculacion = 'ESTUDIANTE' AND v.fecha_fin IS NULL) = 1);
    PERFORM pg_temp.ok(T, 'persona nueva: nombre provisional, no externa, sin correo marcado',
        (SELECT nombres = 'Estudiante T7200001' AND NOT es_externo AND contacto_sin_correo FROM asistente WHERE id_asis = 'T7200001'));

    -- T17 · la CC ya es de otra persona
    PERFORM pg_temp.falla(T, 'T17 CC de otra persona: se rechaza la fila',
        $q$SELECT pg_temp.cargar('T7200009', '9990000001', 'INDS')$q$, '23505', 'ya pertenece a otra persona');
    PERFORM pg_temp.ok(T, 'T17 ... y no quedó la persona a medias', NOT EXISTS (SELECT 1 FROM asistente WHERE id_asis = 'T7200009'));

    -- formato y catálogos
    PERFORM pg_temp.falla(T, 'carnet inválido', $q$SELECT pg_temp.cargar('ab', '9990000004', 'INDS')$q$, '23514');
    PERFORM pg_temp.falla(T, 'programa inexistente', $q$SELECT pg_temp.cargar('T7200010', '9990000005', 'ZZZZ')$q$, '23503');
    PERFORM pg_temp.falla(T, 'periodo inexistente', $q$SELECT pg_temp.cargar('T7200010', '9990000005', 'INDS', '1999-1')$q$, '23503');

    -- T31 · carnet que ya existía con otra CC y otro programa en el mismo periodo (P19)
    INSERT INTO asistente (id_asis, nombres, apellidos, correo_personal, es_externo)
        VALUES ('T7200002', 'Relleno', 'ASIS', 'relleno@prueba.co', false) RETURNING id_asistente INTO v_id;
    INSERT INTO documento_asistente (fk_asistente, fk_tipo_documento, numero, vigente) VALUES (v_id, 'CC', '9990000002', true);
    INSERT INTO programa_asistente (fk_asistente, fk_programa, fk_periodo, estado) VALUES (v_id, 'M0801', '2026-2', 'ACTIVO');
    INSERT INTO programa_asistente (fk_asistente, fk_programa, fk_periodo, estado) VALUES (v_id, 'M0226', '2026-1', 'ACTIVO');
    INSERT INTO vinculacion_asistente (fk_asistente, fk_tipo_vinculacion, fecha_ini) VALUES (v_id, 'ESTUDIANTE', CURRENT_DATE - 30);

    PERFORM pg_temp.ok(T, 'T31 carnet existente: ACTUALIZADO', pg_temp.cargar('T7200002', '9990000003', 'ADMO') = 'ACTUALIZADO');
    PERFORM pg_temp.ok(T, 'T31 la CC vieja queda no vigente',
        (SELECT NOT vigente FROM documento_asistente WHERE numero = '9990000002'));
    PERFORM pg_temp.ok(T, 'T31 la CC nueva queda vigente',
        (SELECT vigente FROM documento_asistente WHERE numero = '9990000003' AND fk_asistente = v_id));
    PERFORM pg_temp.ok(T, 'T31 el programa viejo del mismo periodo pasa a RETIRADO',
        (SELECT estado FROM programa_asistente WHERE fk_asistente = v_id AND fk_programa = 'M0801' AND fk_periodo = '2026-2') = 'RETIRADO');
    PERFORM pg_temp.ok(T, 'T31 el programa nuevo queda ACTIVO',
        (SELECT estado FROM programa_asistente WHERE fk_asistente = v_id AND fk_programa = 'ADMO' AND fk_periodo = '2026-2') = 'ACTIVO');
    PERFORM pg_temp.ok(T, 'T31 el programa de OTRO periodo no se toca',
        (SELECT estado FROM programa_asistente WHERE fk_asistente = v_id AND fk_programa = 'M0226' AND fk_periodo = '2026-1') = 'ACTIVO');
    PERFORM pg_temp.ok(T, 'T31 la vinculación vigente conserva su fecha',
        (SELECT fecha_ini FROM vinculacion_asistente WHERE fk_asistente = v_id AND fk_tipo_vinculacion = 'ESTUDIANTE') = CURRENT_DATE - 30);
    PERFORM pg_temp.ok(T, 'T31 el nombre real no se pisa', (SELECT nombres FROM asistente WHERE id_asistente = v_id) = 'Relleno');

    -- carnets del archivo de ejemplo que se usan abajo
    PERFORM pg_temp.cargar('T7200003', '9990000013', 'PSIC');
    PERFORM pg_temp.cargar('T7200004', '9990000014', 'DERE');
    PERFORM pg_temp.cargar('T7200005', '9990000015', 'ARQU');
    -- uno sin vinculación vigente
    PERFORM pg_temp.cargar('T7200006', '9990000016', 'EDUC');
    UPDATE vinculacion_asistente SET fecha_fin = CURRENT_DATE
     WHERE fk_asistente = (SELECT id_asistente FROM asistente WHERE id_asis = 'T7200006');
EXCEPTION WHEN OTHERS THEN
    PERFORM pg_temp.ok('cargar', 'bloque terminó sin excepción inesperada', false, SQLSTATE || ' ' || SQLERRM);
END $$;

-- --------------------------------------------------------------------------
--  5. Registro público de ESTUDIANTE (063) · T01-T08, T11, T15
-- --------------------------------------------------------------------------
DO $$
DECLARE
    T constant text := 'estudiante';
    v_cat bigint := (SELECT v::bigint FROM ids WHERE k = 'cat');
    v_pres bigint; v_virt bigint; v_tok_p uuid; v_tok_v uuid; v_r text; v_id bigint;
    -- BELLO = (6.333129, -75.569174). 0.00045 grados de latitud ≈ 50 m.
    c_lat constant numeric := 6.333129;  c_lon constant numeric := -75.569174;
BEGIN
    v_pres := pg_temp.sesion_de_prueba(v_cat, 'PRESENCIAL', 'BELLO');
    v_tok_p := pg_temp.qr_de(v_pres);
    v_virt := pg_temp.sesion_de_prueba(v_cat, 'VIRTUAL', 'VIRTUAL');
    v_tok_v := pg_temp.qr_de(v_virt);
    INSERT INTO ids VALUES ('pres', v_pres::text), ('virt', v_virt::text), ('tok_p', v_tok_p::text), ('tok_v', v_tok_v::text);

    -- T05 · presencial, dentro del radio
    v_r := pg_temp.reg_est(v_tok_p, 'T7200001', c_lat, c_lon);
    PERFORM pg_temp.ok(T, 'T05 presencial dentro del radio: OK', pg_temp.estado(v_r) = 'OK', v_r);
    PERFORM pg_temp.ok(T, 'T05 guarda ubicación, distancia, carnet, canal e identificación',
        (SELECT latitud = c_lat AND longitud = c_lon AND distancia_metros = 0 AND identificado_por = 'CARNET'
                AND origen = 'QR' AND fk_vinculacion_snapshot = 'ESTUDIANTE' AND fk_programa_snapshot = 'INDS'
                AND ip = '10.0.0.7'::inet AND user_agent = 'prueba/1.0' AND fk_clave IS NULL
           FROM registro_asistencia WHERE fk_sesion = v_pres AND fk_asistente = (SELECT id_asistente FROM asistente WHERE id_asis = 'T7200001')));

    -- T02 · repetido en la misma sesión
    v_r := pg_temp.reg_est(v_tok_p, 'T7200001', c_lat, c_lon);
    PERFORM pg_temp.ok(T, 'T02 repetido en la misma sesión: YA_REGISTRADO', pg_temp.estado(v_r) = 'YA_REGISTRADO', v_r);
    PERFORM pg_temp.ok(T, 'T02 ... y sigue habiendo un solo registro', pg_temp.n_reg(v_pres) = 1);

    -- T01 · carnet inexistente
    v_r := pg_temp.reg_est(v_tok_p, 'NOEXISTE9', c_lat, c_lon);
    PERFORM pg_temp.ok(T, 'T01 carnet inexistente: ESTUDIANTE_NO_EXISTE', pg_temp.estado(v_r) = 'ESTUDIANTE_NO_EXISTE', v_r);
    v_r := pg_temp.reg_est(v_tok_p, 'ab', c_lat, c_lon);
    PERFORM pg_temp.ok(T, 'carnet con formato inválido: DATOS_INVALIDOS', pg_temp.estado(v_r) = 'DATOS_INVALIDOS', v_r);

    -- T04 · presencial sin ubicación
    v_r := pg_temp.reg_est(v_tok_p, 'T7200003');
    PERFORM pg_temp.ok(T, 'T04 presencial sin ubicación: SIN_UBICACION', pg_temp.estado(v_r) = 'SIN_UBICACION', v_r);
    PERFORM pg_temp.ok(T, 'T04 ... y no quedó registro', NOT EXISTS (SELECT 1 FROM registro_asistencia WHERE fk_sesion = v_pres AND fk_asistente = (SELECT id_asistente FROM asistente WHERE id_asis = 'T7200003')));

    -- T06 · presencial, fuera del radio (con las coordenadas de la otra sede, a ~8,7 km)
    v_r := pg_temp.reg_est(v_tok_p, 'T7200003', 6.255095, -75.572969);
    PERFORM pg_temp.ok(T, 'T06 fuera del radio: FUERA_DE_RADIO', pg_temp.estado(v_r) = 'FUERA_DE_RADIO', v_r);
    PERFORM pg_temp.ok(T, 'T06 ... informa distancia (≈8,7 km) y radio (200)',
        split_part(v_r, '|', 3)::numeric BETWEEN 8500 AND 8900 AND split_part(v_r, '|', 4)::int = 200, v_r);
    PERFORM pg_temp.ok(T, 'T06 ... y NO queda registro', NOT EXISTS (SELECT 1 FROM registro_asistencia WHERE fk_sesion = v_pres AND fk_asistente = (SELECT id_asistente FROM asistente WHERE id_asis = 'T7200003')));
    v_r := pg_temp.reg_est(v_tok_p, 'T7200003', 91, 0);
    PERFORM pg_temp.ok(T, 'ubicación fuera de rango: DATOS_INVALIDOS', pg_temp.estado(v_r) = 'DATOS_INVALIDOS', v_r);

    -- T11 · el radio de la sede sobrescribe al global (a ~50 m del punto)
    v_r := pg_temp.reg_est(v_tok_p, 'T7200003', c_lat + 0.00045, c_lon);
    PERFORM pg_temp.ok(T, 'T11 a ~50 m con el radio global (200): OK', pg_temp.estado(v_r) = 'OK', v_r);
    PERFORM pg_temp.ok(T, 'T12 la distancia guardada coincide con fn_distancia_metros (±0,1 m)',
        (SELECT abs(distancia_metros - round(catedras.fn_distancia_metros(c_lat + 0.00045, c_lon, 6.333129, -75.569174)::numeric, 1)) <= 0.1
           FROM registro_asistencia WHERE fk_sesion = v_pres AND fk_asistente = (SELECT id_asistente FROM asistente WHERE id_asis = 'T7200003')));
    UPDATE sede SET radio_metros = 10 WHERE id_sede = 'BELLO';
    v_r := pg_temp.reg_est(v_tok_p, 'T7200004', c_lat + 0.00045, c_lon);
    PERFORM pg_temp.ok(T, 'T11 a ~50 m con radio propio de 10 m: FUERA_DE_RADIO', pg_temp.estado(v_r) = 'FUERA_DE_RADIO' AND split_part(v_r, '|', 4) = '10', v_r);
    UPDATE sede SET radio_metros = NULL WHERE id_sede = 'BELLO';

    -- T15 · sin vinculación vigente
    v_r := pg_temp.reg_est(v_tok_p, 'T7200006', c_lat, c_lon);
    PERFORM pg_temp.ok(T, 'T15 sin vinculación vigente: SIN_VINCULACION_VIGENTE', pg_temp.estado(v_r) = 'SIN_VINCULACION_VIGENTE', v_r);

    -- T07 / T08 · virtual: no se pide ni se guarda ubicación
    v_r := pg_temp.reg_est(v_tok_v, 'T7200001');
    PERFORM pg_temp.ok(T, 'T07 virtual sin ubicación: OK', pg_temp.estado(v_r) = 'OK', v_r);
    PERFORM pg_temp.ok(T, 'T07 ... latitud, longitud y distancia en NULL; origen ENLACE',
        (SELECT latitud IS NULL AND longitud IS NULL AND distancia_metros IS NULL AND origen = 'ENLACE'
           FROM registro_asistencia WHERE fk_sesion = v_virt AND fk_asistente = (SELECT id_asistente FROM asistente WHERE id_asis = 'T7200001')));
    v_r := pg_temp.reg_est(v_tok_v, 'T7200003', 6.255095, -75.572969);
    PERFORM pg_temp.ok(T, 'T08 virtual con ubicación enviada: OK (aunque esté lejos)', pg_temp.estado(v_r) = 'OK', v_r);
    PERFORM pg_temp.ok(T, 'T08 ... las coordenadas se descartan (NULL)',
        (SELECT latitud IS NULL AND longitud IS NULL AND distancia_metros IS NULL
           FROM registro_asistencia WHERE fk_sesion = v_virt AND fk_asistente = (SELECT id_asistente FROM asistente WHERE id_asis = 'T7200003')));

    -- T03 · enlace inexistente, revocado, fuera de ventana
    v_r := pg_temp.reg_est(gen_random_uuid(), 'T7200004', c_lat, c_lon);
    PERFORM pg_temp.ok(T, 'enlace inexistente: ENLACE_NO_EXISTE', pg_temp.estado(v_r) = 'ENLACE_NO_EXISTE', v_r);
    UPDATE enlace_registro SET ventana = tstzrange(now() - interval '2 hours', now() - interval '1 hour', '[)') WHERE token = v_tok_p;
    v_r := pg_temp.reg_est(v_tok_p, 'T7200004', c_lat, c_lon);
    PERFORM pg_temp.ok(T, 'T03 fuera de ventana: FUERA_DE_VENTANA', pg_temp.estado(v_r) = 'FUERA_DE_VENTANA', v_r);
    UPDATE enlace_registro SET ventana = tstzrange(now() - interval '10 minutes', now() + interval '5 minutes', '[)'), revocado_en = now() WHERE token = v_tok_p;
    v_r := pg_temp.reg_est(v_tok_p, 'T7200004', c_lat, c_lon);
    PERFORM pg_temp.ok(T, 'T03 revocado: ENLACE_REVOCADO', pg_temp.estado(v_r) = 'ENLACE_REVOCADO', v_r);
    UPDATE enlace_registro SET revocado_en = NULL WHERE token = v_tok_p;
EXCEPTION WHEN OTHERS THEN
    PERFORM pg_temp.ok('estudiante', 'bloque terminó sin excepción inesperada', false, SQLSTATE || ' ' || SQLERRM);
END $$;

-- --------------------------------------------------------------------------
--  6. Registro público de DOCENTE y EXTERNO (063) · T13, T14
-- --------------------------------------------------------------------------
DO $$
DECLARE
    T constant text := 'docente/externo';
    v_pres bigint := (SELECT v::bigint FROM ids WHERE k = 'pres');
    v_virt bigint := (SELECT v::bigint FROM ids WHERE k = 'virt');
    v_tok_p uuid := (SELECT v::uuid FROM ids WHERE k = 'tok_p');
    v_tok_v uuid := (SELECT v::uuid FROM ids WHERE k = 'tok_v');
    v_cat bigint := (SELECT v::bigint FROM ids WHERE k = 'cat');
    v_r text; v_a bigint;
    c_lat constant numeric := 6.333129;  c_lon constant numeric := -75.569174;
BEGIN
    -- DOCENTE nuevo, con correo
    v_r := pg_temp.reg_doc(v_tok_p, '9990100', 'Docente de Prueba', 'docente@prueba.co', 'F_ING', c_lat, c_lon);
    PERFORM pg_temp.ok(T, 'docente nuevo: OK', pg_temp.estado(v_r) = 'OK', v_r);
    SELECT a.id_asistente INTO v_a FROM documento_asistente d JOIN asistente a ON a.id_asistente = d.fk_asistente
     WHERE d.fk_tipo_documento = 'CC' AND d.numero = '9990100';
    PERFORM pg_temp.ok(T, 'docente: persona creada, es_externo = true (sin código ASIS), correo en correo_personal, nombre completo guardado',
        (SELECT es_externo AND id_asis IS NULL AND correo_personal = 'docente@prueba.co' AND NOT contacto_sin_correo
                AND nombre_completo_asis = 'Docente de Prueba' FROM asistente WHERE id_asistente = v_a));
    PERFORM pg_temp.ok(T, 'docente: vinculación DOCENTE vigente creada',
        EXISTS (SELECT 1 FROM vinculacion_asistente WHERE fk_asistente = v_a AND fk_tipo_vinculacion = 'DOCENTE' AND fecha_fin IS NULL));
    PERFORM pg_temp.ok(T, 'docente: registro con facultad, vinculación, identificación y sin programa (P7)',
        (SELECT fk_vinculacion_snapshot = 'DOCENTE' AND fk_facultad_snapshot = 'F_ING' AND identificado_por = 'DECLARADO'
                AND fk_programa_snapshot IS NULL AND procedencia IS NULL AND distancia_metros = 0
           FROM registro_asistencia WHERE fk_sesion = v_pres AND fk_asistente = v_a));

    -- T13 · cédula repetida en otra sesión: se reutiliza la persona
    v_r := pg_temp.reg_doc(v_tok_v, '9990100', 'Otro Nombre', 'otro@prueba.co', 'F_EDU');
    PERFORM pg_temp.ok(T, 'T13 misma cédula en otra sesión: OK', pg_temp.estado(v_r) = 'OK', v_r);
    PERFORM pg_temp.ok(T, 'T13 ... una sola persona, un solo documento, una sola vinculación DOCENTE',
        (SELECT count(*) FROM documento_asistente WHERE fk_tipo_documento = 'CC' AND numero = '9990100') = 1
        AND (SELECT count(*) FROM vinculacion_asistente WHERE fk_asistente = v_a AND fk_tipo_vinculacion = 'DOCENTE') = 1);
    PERFORM pg_temp.ok(T, 'T13 ... el nombre ya guardado NO se pisa y la facultad del día queda en el registro',
        (SELECT nombre_completo_asis = 'Docente de Prueba' FROM asistente WHERE id_asistente = v_a)
        AND (SELECT fk_facultad_snapshot = 'F_EDU' FROM registro_asistencia WHERE fk_sesion = v_virt AND fk_asistente = v_a));
    v_r := pg_temp.reg_doc(v_tok_v, '9990100', 'Docente de Prueba', 'docente@prueba.co', 'F_ING');
    PERFORM pg_temp.ok(T, 'docente repetido en la misma sesión: YA_REGISTRADO', pg_temp.estado(v_r) = 'YA_REGISTRADO', v_r);

    -- T14 · solo celular
    v_r := pg_temp.reg_doc(v_tok_p, '9990101', 'Docente Celular', '300 123 4567', 'F_ING', c_lat, c_lon);
    PERFORM pg_temp.ok(T, 'T14 docente con solo celular: OK', pg_temp.estado(v_r) = 'OK', v_r);
    PERFORM pg_temp.ok(T, 'T14 ... queda en celular y la persona se marca sin correo',
        (SELECT celular = '300 123 4567' AND correo_personal IS NULL AND contacto_sin_correo
           FROM asistente a JOIN documento_asistente d ON d.fk_asistente = a.id_asistente WHERE d.numero = '9990101'));

    -- datos inválidos y ubicación
    v_r := pg_temp.reg_doc(v_tok_p, '9990102', 'X', 'x@prueba.co', 'F_NOEXISTE', c_lat, c_lon);
    PERFORM pg_temp.ok(T, 'facultad inexistente: DATOS_INVALIDOS', pg_temp.estado(v_r) = 'DATOS_INVALIDOS', v_r);
    v_r := pg_temp.reg_doc(v_tok_p, '9990102', 'X', 'esto no es un contacto', 'F_ING', c_lat, c_lon);
    PERFORM pg_temp.ok(T, 'contacto inválido: DATOS_INVALIDOS', pg_temp.estado(v_r) = 'DATOS_INVALIDOS', v_r);
    v_r := pg_temp.reg_doc(v_tok_p, '9990102', 'X', 'x@prueba.co', 'F_ING');
    PERFORM pg_temp.ok(T, 'docente presencial sin ubicación: SIN_UBICACION', pg_temp.estado(v_r) = 'SIN_UBICACION', v_r);
    v_r := pg_temp.reg_doc(v_tok_p, '9990102', 'X', 'x@prueba.co', 'F_ING', 6.255095, -75.572969);
    PERFORM pg_temp.ok(T, 'docente presencial fuera del radio: FUERA_DE_RADIO', pg_temp.estado(v_r) = 'FUERA_DE_RADIO', v_r);
    PERFORM pg_temp.ok(T, '... y no se creó la persona', NOT EXISTS (SELECT 1 FROM documento_asistente WHERE numero = '9990102'));

    -- EXTERNO
    v_r := pg_temp.reg_ext(v_tok_p, '9990200', 'Externo de Prueba', 'externo@prueba.co', 'Colegio San José', c_lat, c_lon);
    PERFORM pg_temp.ok(T, 'externo nuevo: OK', pg_temp.estado(v_r) = 'OK', v_r);
    PERFORM pg_temp.ok(T, 'externo: registro con procedencia, vinculación EXTERNO y sin facultad ni programa',
        (SELECT fk_vinculacion_snapshot = 'EXTERNO' AND procedencia = 'Colegio San José' AND fk_facultad_snapshot IS NULL
                AND fk_programa_snapshot IS NULL AND identificado_por = 'DECLARADO'
           FROM registro_asistencia r JOIN documento_asistente d ON d.fk_asistente = r.fk_asistente
          WHERE r.fk_sesion = v_pres AND d.numero = '9990200'));
    v_r := pg_temp.reg_ext(v_tok_p, '9990200', 'Externo de Prueba', 'externo@prueba.co', 'Colegio San José', c_lat, c_lon);
    PERFORM pg_temp.ok(T, 'externo repetido: YA_REGISTRADO', pg_temp.estado(v_r) = 'YA_REGISTRADO', v_r);
    v_r := pg_temp.reg_ext(v_tok_v, '9990201', 'Externo Virtual', 'externo2@prueba.co', 'Empresa X');
    PERFORM pg_temp.ok(T, 'externo en sesión virtual sin ubicación: OK', pg_temp.estado(v_r) = 'OK', v_r);
    v_r := pg_temp.reg_ext(v_tok_v, '9990202', 'Sin procedencia', 'externo3@prueba.co', '   ');
    PERFORM pg_temp.ok(T, 'externo sin procedencia: DATOS_INVALIDOS', pg_temp.estado(v_r) = 'DATOS_INVALIDOS', v_r);

    -- CHECK de coherencia por tipo (D66)
    PERFORM pg_temp.falla(T, 'la facultad solo puede ir en un registro DOCENTE',
        format($q$UPDATE registro_asistencia SET fk_facultad_snapshot = 'F_ING' WHERE fk_sesion = %s AND fk_vinculacion_snapshot = 'ESTUDIANTE'$q$, v_pres), '23514');
    PERFORM pg_temp.falla(T, 'la procedencia solo puede ir en un registro EXTERNO',
        format($q$UPDATE registro_asistencia SET procedencia = 'x' WHERE fk_sesion = %s AND fk_vinculacion_snapshot = 'DOCENTE'$q$, v_pres), '23514');
    PERFORM pg_temp.falla(T, 'sin clave y sin carnet/declarado no se admite un registro QR',
        format($q$INSERT INTO registro_asistencia (fk_sesion, fk_asistente, origen, fk_vinculacion_snapshot) VALUES (%s, %s, 'QR', 'EXTERNO')$q$, v_virt, v_a), '23514');
EXCEPTION WHEN OTHERS THEN
    PERFORM pg_temp.ok('docente/externo', 'bloque terminó sin excepción inesperada', false, SQLSTATE || ' ' || SQLERRM);
END $$;

-- --------------------------------------------------------------------------
--  7. Consulta del enlace y aviso temprano (063) · T30
-- --------------------------------------------------------------------------
DO $$
DECLARE
    T constant text := 'enlace público';
    v_tok_p uuid := (SELECT v::uuid FROM ids WHERE k = 'tok_p');
    v_tok_v uuid := (SELECT v::uuid FROM ids WHERE k = 'tok_v');
    j json;
BEGIN
    j := fn_consultar_enlace_publico(v_tok_p);
    PERFORM pg_temp.ok(T, 'trae título de la cátedra, id de 9 dígitos, canal QR y requiere ubicación',
        j ->> 'titulo_catedra' = 'Cátedra v7.2 de prueba' AND j ->> 'id_catedra' = '999000001'
        AND j ->> 'canal' = 'QR' AND (j ->> 'requiere_ubicacion')::boolean AND j ->> 'estado_ventana' = 'abierto', j::text);
    PERFORM pg_temp.ok(T, 'NO trae las coordenadas de la sede', j::text NOT LIKE '%latitud%' AND j::text NOT LIKE '%6.3331%');
    j := fn_consultar_enlace_publico(v_tok_v);
    PERFORM pg_temp.ok(T, 'sesión virtual: canal ENLACE y no requiere ubicación', j ->> 'canal' = 'ENLACE' AND NOT (j ->> 'requiere_ubicacion')::boolean);
    PERFORM pg_temp.ok(T, 'token inexistente: NULL', fn_consultar_enlace_publico(gen_random_uuid()) IS NULL);

    j := fn_verificar_ubicacion(v_tok_p, 6.333129, -75.569174);
    PERFORM pg_temp.ok(T, 'T30 aviso temprano dentro: dentro = true', (j ->> 'dentro')::boolean AND (j ->> 'requiere_ubicacion')::boolean, j::text);
    j := fn_verificar_ubicacion(v_tok_p, 6.255095, -75.572969);
    PERFORM pg_temp.ok(T, 'T30 aviso temprano fuera: dentro = false con distancia', NOT (j ->> 'dentro')::boolean AND (j ->> 'distancia_metros')::numeric > 8000, j::text);
    j := fn_verificar_ubicacion(v_tok_v, 6.255095, -75.572969);
    PERFORM pg_temp.ok(T, 'T30 aviso temprano en canal ENLACE: no requiere ubicación', NOT (j ->> 'requiere_ubicacion')::boolean AND (j ->> 'dentro') IS NULL, j::text);
END $$;

-- --------------------------------------------------------------------------
--  8. Exportación (066) · T18, T19, T21, T24, T25, T27
-- --------------------------------------------------------------------------
DO $$
DECLARE
    T constant text := 'exportacion';
    v_cat bigint := (SELECT v::bigint FROM ids WHERE k = 'cat');
    v_cat2 bigint := (SELECT v::bigint FROM ids WHERE k = 'cat_inactiva');
    v_ses2 bigint; v_tok2 uuid; v_ses_p1 bigint; v_tok_p1 uuid; v_r text; j json; fila json; asist json;
    c_lat constant numeric := 6.333129;  c_lon constant numeric := -75.569174;
BEGIN
    -- una segunda sesión PRESENCIAL de la misma cátedra: el estudiante T7200001 asiste a las dos (T19)
    v_ses2 := pg_temp.sesion_de_prueba(v_cat, 'PRESENCIAL', 'BELLO');
    v_tok2 := pg_temp.qr_de(v_ses2);
    v_r := pg_temp.reg_est(v_tok2, 'T7200001', c_lat, c_lon);
    PERFORM pg_temp.ok(T, 'preparación: 2ª sesión de la misma cátedra, estudiante registrado', pg_temp.estado(v_r) = 'OK', v_r);
    v_r := pg_temp.reg_ext(v_tok2, '9990200', 'Externo de Prueba', 'externo@prueba.co', 'Colegio San José', c_lat, c_lon);
    PERFORM pg_temp.ok(T, 'preparación: el mismo externo en la 2ª sesión', pg_temp.estado(v_r) = 'OK', v_r);
    -- el externo de la cátedra 2 (otra cátedra): T7200001 también asiste (T18: 2 cátedras)
    -- una cátedra INACTIVA con asistencia en el periodo
    v_ses_p1 := pg_temp.sesion_de_prueba(v_cat2, 'PRESENCIAL', 'BELLO');
    v_tok_p1 := pg_temp.qr_de(v_ses_p1);
    v_r := pg_temp.reg_est(v_tok_p1, 'T7200001', c_lat, c_lon);
    PERFORM pg_temp.ok(T, 'preparación: cátedra inactiva con asistencia', pg_temp.estado(v_r) = 'OK', v_r);

    -- catálogo del periodo
    j := fn_exportar_catedras_periodo('2026-2');
    SELECT x INTO fila FROM json_array_elements(j) x WHERE x ->> 'id_catedra' = '999000001';
    PERFORM pg_temp.ok(T, 'T24 el catálogo trae la cátedra con su id de 9 dígitos y título', fila ->> 'titulo' = 'Cátedra v7.2 de prueba');
    PERFORM pg_temp.ok(T, 'T19/T21 conteos sin repetir persona (estudiantes 2, docentes 2, externos 2)',
        (fila ->> 'estudiantes')::int = 2 AND (fila ->> 'docentes')::int = 2 AND (fila ->> 'externos')::int = 2, fila::text);
    PERFORM pg_temp.ok(T, 'la cátedra inactiva CON asistencias aparece',
        EXISTS (SELECT 1 FROM json_array_elements(j) x WHERE x ->> 'id_catedra' = '999000002'));
    PERFORM pg_temp.ok(T, 'solo aparecen cátedras con sesión en el periodo (ninguna de otro)',
        NOT EXISTS (SELECT 1 FROM json_array_elements(fn_exportar_catedras_periodo('1999-1')) x));

    -- asistentes por cátedra
    j := fn_exportar_asistentes_periodo('2026-2', '999000000', 1);
    PERFORM pg_temp.ok(T, 'paginación por cátedra: limite 1 desde 999000000 devuelve 1 cátedra', json_array_length(j) = 1, j::text);
    SELECT x INTO fila FROM json_array_elements(fn_exportar_asistentes_periodo('2026-2', '999000000', 10)) x WHERE x ->> 'id_catedra' = '999000001';
    PERFORM pg_temp.ok(T, 'T19 el estudiante de 2 sesiones aparece UNA vez en la cátedra',
        (SELECT count(*) FROM json_array_elements(fila -> 'asistentes') a WHERE a ->> 'carnet' = 'T7200001' AND a ->> 'tipo' = 'ESTUDIANTE') = 1);
    PERFORM pg_temp.ok(T, 'T21 el externo de 2 sesiones aparece UNA vez',
        (SELECT count(*) FROM json_array_elements(fila -> 'asistentes') a WHERE a ->> 'cc' = '9990200' AND a ->> 'tipo' = 'EXTERNO') = 1);
    SELECT a INTO asist FROM json_array_elements(fila -> 'asistentes') a WHERE a ->> 'carnet' = 'T7200001';
    PERFORM pg_temp.ok(T, 'T20 estudiante: trae CC, carnet y programa tal como se cargó (INDS)',
        asist ->> 'cc' = '9990000001' AND asist ->> 'carnet' = 'T7200001' AND asist ->> 'programa' = 'INDS', asist::text);
    SELECT a INTO asist FROM json_array_elements(fila -> 'asistentes') a WHERE a ->> 'cc' = '9990100';
    PERFORM pg_temp.ok(T, 'T22 docente: CC, nombre, contacto y facultad (nombre) tal como se registró',
        asist ->> 'nombre' = 'Docente de Prueba' AND asist ->> 'contacto' = 'docente@prueba.co'
        -- se registró en dos sesiones con facultades distintas y con la misma hora (misma transacción): sale la de alguna de las dos
        AND asist ->> 'facultad' IN (SELECT nombre FROM facultad WHERE id_facultad IN ('F_EDU', 'F_ING')), asist::text);
    SELECT a INTO asist FROM json_array_elements(fila -> 'asistentes') a WHERE a ->> 'cc' = '9990200';
    PERFORM pg_temp.ok(T, 'T22 externo: CC, nombre, contacto y procedencia',
        asist ->> 'nombre' = 'Externo de Prueba' AND asist ->> 'contacto' = 'externo@prueba.co' AND asist ->> 'procedencia' = 'Colegio San José', asist::text);

    -- T18 · el estudiante asistió a 2 cátedras: aparece en 2 listas
    PERFORM pg_temp.ok(T, 'T18 el estudiante aparece en 2 cátedras distintas',
        (SELECT count(*) FROM json_array_elements(fn_exportar_asistentes_periodo('2026-2', '999000000', 10)) c,
                json_array_elements(c -> 'asistentes') a
          WHERE c ->> 'id_catedra' IN ('999000001', '999000002') AND a ->> 'carnet' = 'T7200001') = 2);

    -- T27 · una cátedra con sesiones en dos periodos aparece en ambos, con los registros de cada uno
    INSERT INTO sesion (fk_catedra, inicio, fin, fk_modalidad, fk_sede, fk_periodo)
    VALUES (v_cat, now() + interval '1 day', now() + interval '1 day 1 hour', 'PRESENCIAL', 'BELLO', '2025-2');
    PERFORM pg_temp.ok(T, 'T27 sin asistentes en el otro periodo: aparece con ceros',
        (SELECT (x ->> 'estudiantes')::int = 0 AND (x ->> 'docentes')::int = 0 AND (x ->> 'externos')::int = 0
           FROM json_array_elements(fn_exportar_catedras_periodo('2025-2')) x WHERE x ->> 'id_catedra' = '999000001'));
    PERFORM pg_temp.ok(T, 'T25 cátedra sin asistentes: lista de asistentes vacía',
        (SELECT json_array_length(x -> 'asistentes') = 0
           FROM json_array_elements(fn_exportar_asistentes_periodo('2025-2', '999000000', 10)) x WHERE x ->> 'id_catedra' = '999000001'));
EXCEPTION WHEN OTHERS THEN
    PERFORM pg_temp.ok('exportacion', 'bloque terminó sin excepción inesperada', false, SQLSTATE || ' ' || SQLERRM);
END $$;

-- --------------------------------------------------------------------------
--  Resultado
-- --------------------------------------------------------------------------
\pset format unaligned
\pset tuples_only on
SELECT CASE WHEN ok THEN '  OK    ' ELSE '  FALLA ' END || rpad(tabla, 18) || caso
       || CASE WHEN NOT ok AND detalle IS NOT NULL THEN E'\n          -> ' || detalle ELSE '' END
  FROM resultado_prueba ORDER BY n;
SELECT format(E'\n  %s casos: %s OK, %s FALLA', count(*), count(*) FILTER (WHERE ok), count(*) FILTER (WHERE NOT ok))
  FROM resultado_prueba;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM resultado_prueba WHERE NOT ok) THEN
        RAISE EXCEPTION 'Hay casos en FALLA (ver arriba).';
    END IF;
END $$;

ROLLBACK;
