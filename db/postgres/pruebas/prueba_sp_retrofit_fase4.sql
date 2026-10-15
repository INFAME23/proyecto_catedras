-- ==========================================================================
--  db/postgres/pruebas/prueba_sp_retrofit_fase4.sql
--  Proyecto Cátedras · RETROFIT criterio 3 · Fase 4 (tester: Isaac)
-- --------------------------------------------------------------------------
--  Prueba los procedimientos de las migraciones 024-032 (Fase 4) DIRECTAMENTE contra
--  PostgreSQL (sin la API): los 5 verbos de las 9 tablas, sus casos de
--  "no existe", el borrado lógico/físico/cierre, y que las restricciones de
--  la BD (CHECK, UNIQUE, FK) sigan siendo la última muralla.
--
--  NO DEJA RASTRO: todo corre dentro de BEGIN ... ROLLBACK.
--
--  Cómo correrlo (con Docker arriba, desde la raíz del repositorio; sirve
--  igual en PowerShell y en bash):
--      docker compose cp db/postgres/pruebas/prueba_sp_retrofit_fase4.sql postgres:/tmp/p.sql
--      docker compose exec -T postgres psql -U sa -d bdcatedras_postgres_local -v ON_ERROR_STOP=1 -f /tmp/p.sql
--  (se copia en vez de canalizar con '|' porque PowerShell 5.1 estropea las
--   tildes al pasar texto por la tubería). También lo corre
--  api_catedras/pruebas/correr_todo_fase4.ps1.
--
--  Imprime una línea OK/FALLA por caso y un resumen. Si algo falla, termina
--  con error (código de salida != 0).
-- ==========================================================================

\set QUIET on
\pset footer off
SET client_min_messages TO warning;
BEGIN;
SET LOCAL search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  Instrumentación mínima (todo en pg_temp: desaparece al terminar)
-- --------------------------------------------------------------------------
CREATE TEMP TABLE resultado_prueba (
    n      serial,
    tabla  text,
    caso   text,
    ok     boolean,
    detalle text
);

-- Registra un caso.
CREATE FUNCTION pg_temp.ok(p_tabla text, p_caso text, p_cond boolean, p_detalle text DEFAULT NULL)
RETURNS void LANGUAGE sql AS $$
    INSERT INTO resultado_prueba (tabla, caso, ok, detalle)
    VALUES (p_tabla, p_caso, COALESCE(p_cond, false), p_detalle);
$$;

-- Ejecuta un CALL y devuelve su INOUT p_resultado (json).
CREATE FUNCTION pg_temp.sp(p_sql text) RETURNS json LANGUAGE plpgsql AS $$
DECLARE v json;
BEGIN
    EXECUTE p_sql INTO v;
    RETURN v;
END;
$$;

-- El CALL DEBE fallar con ese SQLSTATE (23505 unique, 23514 check,
-- 23503 FK, 22P02 texto inválido...). Lo que haya hecho se revierte.
CREATE FUNCTION pg_temp.falla(p_tabla text, p_caso text, p_sql text, p_sqlstate text)
RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    EXECUTE p_sql;
    PERFORM pg_temp.ok(p_tabla, p_caso, false, 'no lanzó ningún error');
EXCEPTION WHEN OTHERS THEN
    PERFORM pg_temp.ok(p_tabla, p_caso, SQLSTATE = p_sqlstate, SQLSTATE || ' ' || SQLERRM);
END;
$$;

-- "eliminado" del json de sp_eliminar_*.
CREATE FUNCTION pg_temp.eliminado(j json) RETURNS boolean LANGUAGE sql AS $$
    SELECT (j ->> 'eliminado')::boolean;
$$;

-- --------------------------------------------------------------------------
--  0. Inventario: cada tabla tiene sus 5 verbos (asistente: 4 + guardar)
-- --------------------------------------------------------------------------
DO $$
DECLARE
    t text;
    v text;
BEGIN
    FOREACH t IN ARRAY ARRAY['documento_asistente','vinculacion_asistente','programa_asistente',
                             'consentimiento_datos','lote_carga_asistente','novedad_carga',
                             'lote_migracion','detalle_migracion'] LOOP
        FOREACH v IN ARRAY ARRAY['listar','consultar','crear','actualizar','eliminar'] LOOP
            PERFORM pg_temp.ok('inventario', format('sp_%s_%s existe', v, t),
                EXISTS (SELECT 1 FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
                         WHERE n.nspname = 'catedras' AND p.prokind = 'p'
                           AND p.proname = format('sp_%s_%s', v, t)));
        END LOOP;
    END LOOP;
    FOREACH v IN ARRAY ARRAY['sp_listar_asistente','sp_consultar_asistente','sp_guardar_asistente',
                             'sp_actualizar_asistente','sp_eliminar_asistente',
                             'sp_guardar_lote_carga','sp_generar_lote_migracion'] LOOP
        PERFORM pg_temp.ok('inventario', v || ' existe',
            EXISTS (SELECT 1 FROM pg_proc WHERE proname = v AND prokind = 'p'));
    END LOOP;
    PERFORM pg_temp.ok('inventario', 'NO se creó un sp_crear_asistente paralelo a sp_guardar_asistente',
        NOT EXISTS (SELECT 1 FROM pg_proc WHERE proname = 'sp_crear_asistente'));
    -- Regla 4: p_resultado json INOUT, último parámetro, en TODOS los del lote.
    PERFORM pg_temp.ok('inventario', 'p_resultado es json INOUT y el último parámetro (49 SP del molde)',
        NOT EXISTS (
            SELECT 1 FROM pg_proc p
             WHERE p.prokind = 'p' AND p.pronamespace = 'catedras'::regnamespace
               AND p.proname ~ '^sp_(listar|consultar|crear|actualizar|eliminar)_'
               AND (p.proargnames[array_length(p.proargnames, 1)] <> 'p_resultado'
                    OR p.proargmodes[array_length(p.proargmodes, 1)] <> 'b'
                    OR p.proallargtypes[array_length(p.proallargtypes, 1)] <> 'json'::regtype)));
END $$;

-- --------------------------------------------------------------------------
--  1. ASISTENTE (activo -> eliminar LÓGICO; crear = sp_guardar_asistente)
-- --------------------------------------------------------------------------
DO $$
DECLARE
    T constant text := 'asistente';
    j json; j2 json; v_id bigint; v_docs_antes int;
BEGIN
    j := pg_temp.sp('CALL catedras.sp_listar_asistente(NULL, 5, 0, NULL)');
    PERFORM pg_temp.ok(T, 'listar con límite 5 devuelve 5', json_array_length(j) = 5);
    PERFORM pg_temp.ok(T, 'listar viene ordenado por id',
        (j -> 0 ->> 'id_asistente')::bigint < (j -> 4 ->> 'id_asistente')::bigint);
    PERFORM pg_temp.ok(T, 'listar trae las 12 columnas', (SELECT count(*) FROM json_object_keys(j -> 0)) = 12);

    j2 := pg_temp.sp('CALL catedras.sp_listar_asistente(''   '', 5, 0, NULL)');
    PERFORM pg_temp.ok(T, 'q en blanco = sin filtro', j2::text = j::text);

    j2 := pg_temp.sp('CALL catedras.sp_listar_asistente(NULL, 5, 5, NULL)');
    PERFORM pg_temp.ok(T, 'desplazamiento 5 empieza después de la página 1',
        (j2 -> 0 ->> 'id_asistente')::bigint > (j -> 4 ->> 'id_asistente')::bigint);

    j2 := pg_temp.sp(format('CALL catedras.sp_listar_asistente(%L, 50, 0, NULL)', upper(j -> 0 ->> 'id_asis')));
    PERFORM pg_temp.ok(T, 'q busca por id_asis sin distinguir mayúsculas',
        json_array_length(j2) >= 1 AND j2::text ILIKE '%' || (j -> 0 ->> 'id_asis') || '%');

    j2 := pg_temp.sp('CALL catedras.sp_listar_asistente(''zzzz_no_existe_zzzz'', 5, 0, NULL)');
    PERFORM pg_temp.ok(T, 'búsqueda sin coincidencias devuelve []', j2::text = '[]');

    -- CREAR por el SP de la v3 (sin tocar) + CONSULTAR por el nuevo
    CALL catedras.sp_guardar_asistente('PRUEBA0001', 'Ana', 'Prueba', NULL, 'ana.prueba@x.edu.co',
        NULL, NULL, false, true, NULL,
        '[{"fk_tipo_documento":"CC","numero":"PRB-0001"}]'::jsonb, NULL, NULL, NULL, v_id);
    j := pg_temp.sp(format('CALL catedras.sp_consultar_asistente(%s, NULL)', v_id));
    PERFORM pg_temp.ok(T, 'sp_guardar_asistente (002) sigue creando y sp_consultar lo lee',
        j ->> 'nombres' = 'Ana' AND (j ->> 'activo')::boolean);
    PERFORM pg_temp.ok(T, 'consultar inexistente -> NULL',
        pg_temp.sp('CALL catedras.sp_consultar_asistente(-1, NULL)') IS NULL);

    -- ACTUALIZAR (conjunto completo)
    j := pg_temp.sp(format($f$CALL catedras.sp_actualizar_asistente(%s, 'PRUEBA0001', 'Ana María',
        'Prueba', NULL, 'ana.prueba@x.edu.co', 'ana@gmail.com', '3001234567', false, true, NULL)$f$, v_id));
    PERFORM pg_temp.ok(T, 'actualizar devuelve la fila nueva',
        j ->> 'nombres' = 'Ana María' AND j ->> 'celular' = '3001234567');
    PERFORM pg_temp.ok(T, 'actualizar NO toca creado_en ni fk_lote_carga',
        j ->> 'creado_en' IS NOT NULL AND j ->> 'fk_lote_carga' IS NULL);
    PERFORM pg_temp.ok(T, 'actualizar inexistente -> NULL',
        pg_temp.sp($f$CALL catedras.sp_actualizar_asistente(-1, NULL, 'X', 'Y', NULL, 'x@y.co',
            NULL, NULL, true, true, NULL)$f$) IS NULL);
    PERFORM pg_temp.falla(T, 'actualizar sin ningún correo -> CHECK chk_asistente_correo',
        format($f$CALL catedras.sp_actualizar_asistente(%s, 'PRUEBA0001', 'A', 'B', NULL, NULL,
            NULL, NULL, false, true, NULL)$f$, v_id), '23514');
    PERFORM pg_temp.falla(T, 'actualizar no externo sin id_asis -> CHECK chk_asistente_externo',
        format($f$CALL catedras.sp_actualizar_asistente(%s, NULL, 'A', 'B', NULL, 'a@b.co',
            NULL, NULL, false, true, NULL)$f$, v_id), '23514');

    -- ELIMINAR lógico
    SELECT count(*) INTO v_docs_antes FROM documento_asistente WHERE fk_asistente = v_id;
    j := pg_temp.sp(format('CALL catedras.sp_eliminar_asistente(%s, NULL)', v_id));
    PERFORM pg_temp.ok(T, 'eliminar -> {"eliminado": true}', pg_temp.eliminado(j));
    PERFORM pg_temp.ok(T, 'eliminar es LÓGICO: la fila sigue, con activo=false',
        (SELECT NOT activo FROM asistente WHERE id_asistente = v_id));
    PERFORM pg_temp.ok(T, 'eliminar lógico NO borra en cascada sus documentos',
        (SELECT count(*) FROM documento_asistente WHERE fk_asistente = v_id) = v_docs_antes AND v_docs_antes = 1);
    PERFORM pg_temp.ok(T, 'eliminar dos veces -> false (ya inactivo)',
        NOT pg_temp.eliminado(pg_temp.sp(format('CALL catedras.sp_eliminar_asistente(%s, NULL)', v_id))));
    PERFORM pg_temp.ok(T, 'eliminar inexistente -> false',
        NOT pg_temp.eliminado(pg_temp.sp('CALL catedras.sp_eliminar_asistente(-1, NULL)')));
    PERFORM pg_temp.ok(T, 'listar ya NO muestra al inactivo',
        pg_temp.sp('CALL catedras.sp_listar_asistente(''PRUEBA0001'', 10, 0, NULL)')::text = '[]');
    PERFORM pg_temp.ok(T, 'consultar SÍ devuelve al inactivo',
        pg_temp.sp(format('CALL catedras.sp_consultar_asistente(%s, NULL)', v_id)) IS NOT NULL);

    -- Un asistente con registro_asistencia (RESTRICT) antes daba 500 al
    -- borrarlo; ahora se puede retirar lógicamente.
    SELECT fk_asistente INTO v_id FROM registro_asistencia LIMIT 1;
    PERFORM pg_temp.ok(T, 'asistente con registros (antes RESTRICT -> 500) ahora se retira',
        pg_temp.eliminado(pg_temp.sp(format('CALL catedras.sp_eliminar_asistente(%s, NULL)', v_id))));
EXCEPTION WHEN OTHERS THEN
    PERFORM pg_temp.ok('asistente', 'bloque terminó sin excepción inesperada', false, SQLSTATE || ' ' || SQLERRM);
END $$;

-- --------------------------------------------------------------------------
--  2. DOCUMENTO_ASISTENTE (sin activo -> eliminar FÍSICO)
-- --------------------------------------------------------------------------
DO $$
DECLARE
    T constant text := 'documento_asistente';
    j json; v_asis bigint; v_doc bigint;
BEGIN
    CALL catedras.sp_guardar_asistente('PRUEBA0002', 'Beto', 'Prueba', NULL, 'beto.prueba@x.edu.co',
        NULL, NULL, false, true, NULL, NULL, NULL, NULL, NULL, v_asis);

    j := pg_temp.sp(format($f$CALL catedras.sp_crear_documento_asistente(%s, 'CC', 'PRB-0002', NULL, NULL)$f$, v_asis));
    v_doc := (j ->> 'id_documento')::bigint;
    PERFORM pg_temp.ok(T, 'crear devuelve id y registrado_en generados por la BD',
        v_doc IS NOT NULL AND j ->> 'registrado_en' IS NOT NULL);
    PERFORM pg_temp.ok(T, 'crear con vigente NULL -> true (DEFAULT)', (j ->> 'vigente')::boolean);
    PERFORM pg_temp.falla(T, 'segundo vigente del mismo asistente -> ux_documento_vigente',
        format($f$CALL catedras.sp_crear_documento_asistente(%s, 'TI', 'PRB-0003', true, NULL)$f$, v_asis), '23505');
    PERFORM pg_temp.falla(T, 'mismo número y tipo -> uq_documento_numero',
        format($f$CALL catedras.sp_crear_documento_asistente(%s, 'CC', 'PRB-0002', false, NULL)$f$, v_asis), '23505');
    PERFORM pg_temp.falla(T, 'número con formato inválido -> CHECK',
        format($f$CALL catedras.sp_crear_documento_asistente(%s, 'CC', '1', false, NULL)$f$, v_asis), '23514');

    j := pg_temp.sp(format('CALL catedras.sp_listar_documento_asistente(%s, NULL, NULL)', v_asis));
    PERFORM pg_temp.ok(T, 'listar con filtro por asistente', json_array_length(j) = 1
        AND (j -> 0 ->> 'fk_asistente')::bigint = v_asis);
    j := pg_temp.sp('CALL catedras.sp_listar_documento_asistente(NULL, 3, NULL)');
    PERFORM pg_temp.ok(T, 'listar sin filtro respeta el límite', json_array_length(j) = 3);

    j := pg_temp.sp(format($f$CALL catedras.sp_actualizar_documento_asistente(%s, 'CC', 'PRB-0002', false, NULL)$f$, v_doc));
    PERFORM pg_temp.ok(T, 'actualizar (retirar vigente) devuelve la fila', NOT (j ->> 'vigente')::boolean);
    PERFORM pg_temp.ok(T, 'con el anterior no vigente, ya se puede crear otro vigente',
        pg_temp.sp(format($f$CALL catedras.sp_crear_documento_asistente(%s, 'TI', 'PRB-0003', true, NULL)$f$, v_asis)) IS NOT NULL);
    PERFORM pg_temp.ok(T, 'actualizar inexistente -> NULL',
        pg_temp.sp($f$CALL catedras.sp_actualizar_documento_asistente(-1, 'CC', 'PRB-9999', false, NULL)$f$) IS NULL);
    PERFORM pg_temp.ok(T, 'consultar inexistente -> NULL',
        pg_temp.sp('CALL catedras.sp_consultar_documento_asistente(-1, NULL)') IS NULL);

    PERFORM pg_temp.ok(T, 'eliminar -> true',
        pg_temp.eliminado(pg_temp.sp(format('CALL catedras.sp_eliminar_documento_asistente(%s, NULL)', v_doc))));
    PERFORM pg_temp.ok(T, 'eliminar es FÍSICO: la fila ya no está',
        NOT EXISTS (SELECT 1 FROM documento_asistente WHERE id_documento = v_doc));
    PERFORM pg_temp.ok(T, 'eliminar dos veces -> false',
        NOT pg_temp.eliminado(pg_temp.sp(format('CALL catedras.sp_eliminar_documento_asistente(%s, NULL)', v_doc))));
EXCEPTION WHEN OTHERS THEN
    PERFORM pg_temp.ok('documento_asistente', 'bloque terminó sin excepción inesperada', false, SQLSTATE || ' ' || SQLERRM);
END $$;

-- --------------------------------------------------------------------------
--  3. VINCULACION_ASISTENTE (llave compuesta; eliminar = CERRAR)
-- --------------------------------------------------------------------------
DO $$
DECLARE
    T constant text := 'vinculacion_asistente';
    j json; v_asis bigint;
BEGIN
    CALL catedras.sp_guardar_asistente('PRUEBA0003', 'Caro', 'Prueba', NULL, 'caro.prueba@x.edu.co',
        NULL, NULL, false, true, NULL, NULL, NULL, NULL, NULL, v_asis);

    j := pg_temp.sp(format($f$CALL catedras.sp_crear_vinculacion_asistente(%s, 'ESTUDIANTE', '2026-01-20', NULL)$f$, v_asis));
    PERFORM pg_temp.ok(T, 'crear (abrir) devuelve la fila con fecha_fin NULL',
        j ->> 'fecha_ini' = '2026-01-20' AND j ->> 'fecha_fin' IS NULL);
    j := pg_temp.sp(format($f$CALL catedras.sp_crear_vinculacion_asistente(%s, 'DOCENTE', NULL, NULL)$f$, v_asis));
    PERFORM pg_temp.ok(T, 'crear sin fecha_ini -> hoy', (j ->> 'fecha_ini')::date = CURRENT_DATE);
    PERFORM pg_temp.falla(T, 'misma llave -> PK duplicada',
        format($f$CALL catedras.sp_crear_vinculacion_asistente(%s, 'ESTUDIANTE', '2026-01-20', NULL)$f$, v_asis), '23505');
    PERFORM pg_temp.falla(T, 'tipo inexistente -> FK',
        format($f$CALL catedras.sp_crear_vinculacion_asistente(%s, 'NO_EXISTE', '2026-01-20', NULL)$f$, v_asis), '23503');

    j := pg_temp.sp(format('CALL catedras.sp_listar_vinculacion_asistente(%s, NULL, NULL)', v_asis));
    PERFORM pg_temp.ok(T, 'listar por asistente: 2 filas, la más reciente primero',
        json_array_length(j) = 2 AND (j -> 0 ->> 'fecha_ini')::date >= (j -> 1 ->> 'fecha_ini')::date);

    j := pg_temp.sp(format($f$CALL catedras.sp_consultar_vinculacion_asistente(%s, 'ESTUDIANTE', '2026-01-20', NULL)$f$, v_asis));
    PERFORM pg_temp.ok(T, 'consultar por la llave de 3 columnas', j ->> 'fk_tipo_vinculacion' = 'ESTUDIANTE');
    PERFORM pg_temp.ok(T, 'consultar llave inexistente -> NULL',
        pg_temp.sp(format($f$CALL catedras.sp_consultar_vinculacion_asistente(%s, 'ESTUDIANTE', '1999-01-01', NULL)$f$, v_asis)) IS NULL);

    PERFORM pg_temp.falla(T, 'cerrar con fecha_fin < fecha_ini -> CHECK chk_vinculacion_fechas',
        format($f$CALL catedras.sp_eliminar_vinculacion_asistente(%s, 'ESTUDIANTE', '2026-01-20', '2025-01-01', NULL)$f$, v_asis), '23514');
    j := pg_temp.sp(format($f$CALL catedras.sp_eliminar_vinculacion_asistente(%s, 'ESTUDIANTE', '2026-01-20', '2026-06-30', NULL)$f$, v_asis));
    PERFORM pg_temp.ok(T, 'eliminar = CERRAR -> true', pg_temp.eliminado(j));
    PERFORM pg_temp.ok(T, 'cerrar NO borra: la fila sigue, con fecha_fin puesta',
        (SELECT fecha_fin FROM vinculacion_asistente WHERE fk_asistente = v_asis
            AND fk_tipo_vinculacion = 'ESTUDIANTE' AND fecha_ini = '2026-01-20') = '2026-06-30');
    PERFORM pg_temp.ok(T, 'cerrar una ya cerrada -> false',
        NOT pg_temp.eliminado(pg_temp.sp(format($f$CALL catedras.sp_eliminar_vinculacion_asistente(%s, 'ESTUDIANTE', '2026-01-20', NULL, NULL)$f$, v_asis))));
    PERFORM pg_temp.ok(T, 'cerrar llave inexistente -> false',
        NOT pg_temp.eliminado(pg_temp.sp('CALL catedras.sp_eliminar_vinculacion_asistente(-1, ''ESTUDIANTE'', ''2026-01-20'', NULL, NULL)')));
    j := pg_temp.sp(format($f$CALL catedras.sp_eliminar_vinculacion_asistente(%s, 'DOCENTE', CURRENT_DATE, NULL, NULL)$f$, v_asis));
    PERFORM pg_temp.ok(T, 'cerrar sin fecha_fin -> hoy',
        pg_temp.eliminado(j) AND (SELECT fecha_fin FROM vinculacion_asistente WHERE fk_asistente = v_asis
            AND fk_tipo_vinculacion = 'DOCENTE') = CURRENT_DATE);

    j := pg_temp.sp(format($f$CALL catedras.sp_actualizar_vinculacion_asistente(%s, 'ESTUDIANTE', '2026-01-20', NULL, NULL)$f$, v_asis));
    PERFORM pg_temp.ok(T, 'actualizar reemplaza fecha_fin (NULL = reabrir)', j IS NOT NULL AND j ->> 'fecha_fin' IS NULL);
    PERFORM pg_temp.ok(T, 'actualizar llave inexistente -> NULL',
        pg_temp.sp('CALL catedras.sp_actualizar_vinculacion_asistente(-1, ''ESTUDIANTE'', ''2026-01-20'', NULL, NULL)') IS NULL);
EXCEPTION WHEN OTHERS THEN
    PERFORM pg_temp.ok('vinculacion_asistente', 'bloque terminó sin excepción inesperada', false, SQLSTATE || ' ' || SQLERRM);
END $$;

-- --------------------------------------------------------------------------
--  4. PROGRAMA_ASISTENTE (llave compuesta; actualizar = cambiar estado;
--     eliminar = retiro lógico)
-- --------------------------------------------------------------------------
DO $$
DECLARE
    T constant text := 'programa_asistente';
    j json; v_asis bigint; v_prog text;
BEGIN
    SELECT codigo INTO v_prog FROM programa_academico ORDER BY codigo LIMIT 1;
    CALL catedras.sp_guardar_asistente('PRUEBA0004', 'Dani', 'Prueba', NULL, 'dani.prueba@x.edu.co',
        NULL, NULL, false, true, NULL, NULL, NULL, NULL, NULL, v_asis);

    j := pg_temp.sp(format($f$CALL catedras.sp_crear_programa_asistente(%s, %L, '2026-1', NULL, NULL)$f$, v_asis, v_prog));
    PERFORM pg_temp.ok(T, 'crear sin estado -> ACTIVO', j ->> 'estado' = 'ACTIVO' AND j ->> 'fk_periodo' = '2026-1');
    PERFORM pg_temp.falla(T, 'misma llave -> PK duplicada (RF111)',
        format($f$CALL catedras.sp_crear_programa_asistente(%s, %L, '2026-1', 'ACTIVO', NULL)$f$, v_asis, v_prog), '23505');
    PERFORM pg_temp.falla(T, 'periodo inexistente -> FK',
        format($f$CALL catedras.sp_crear_programa_asistente(%s, %L, '1990-1', 'ACTIVO', NULL)$f$, v_asis, v_prog), '23503');

    j := pg_temp.sp(format('CALL catedras.sp_listar_programa_asistente(%s, NULL, NULL)', v_asis));
    PERFORM pg_temp.ok(T, 'listar por asistente', json_array_length(j) = 1);
    j := pg_temp.sp(format($f$CALL catedras.sp_consultar_programa_asistente(%s, %L, '2026-1', NULL)$f$, v_asis, v_prog));
    PERFORM pg_temp.ok(T, 'consultar por la llave de 3 columnas (fk_periodo CHAR(6))', j ->> 'estado' = 'ACTIVO');

    j := pg_temp.sp(format($f$CALL catedras.sp_actualizar_programa_asistente(%s, %L, '2026-1', 'GRADUADO', NULL)$f$, v_asis, v_prog));
    PERFORM pg_temp.ok(T, 'actualizar = cambiar estado', j ->> 'estado' = 'GRADUADO');
    PERFORM pg_temp.falla(T, 'estado fuera de la lista -> CHECK',
        format($f$CALL catedras.sp_actualizar_programa_asistente(%s, %L, '2026-1', 'EXPULSADO', NULL)$f$, v_asis, v_prog), '23514');
    PERFORM pg_temp.ok(T, 'actualizar llave inexistente -> NULL',
        pg_temp.sp(format($f$CALL catedras.sp_actualizar_programa_asistente(-1, %L, '2026-1', 'ACTIVO', NULL)$f$, v_prog)) IS NULL);

    j := pg_temp.sp(format($f$CALL catedras.sp_eliminar_programa_asistente(%s, %L, '2026-1', NULL)$f$, v_asis, v_prog));
    PERFORM pg_temp.ok(T, 'eliminar = retiro lógico -> true', pg_temp.eliminado(j));
    PERFORM pg_temp.ok(T, 'eliminar NO borra: queda estado RETIRADO',
        (SELECT estado FROM programa_asistente WHERE fk_asistente = v_asis) = 'RETIRADO');
    PERFORM pg_temp.ok(T, 'eliminar uno ya RETIRADO -> false',
        NOT pg_temp.eliminado(pg_temp.sp(format($f$CALL catedras.sp_eliminar_programa_asistente(%s, %L, '2026-1', NULL)$f$, v_asis, v_prog))));
EXCEPTION WHEN OTHERS THEN
    PERFORM pg_temp.ok('programa_asistente', 'bloque terminó sin excepción inesperada', false, SQLSTATE || ' ' || SQLERRM);
END $$;

-- --------------------------------------------------------------------------
--  5. CONSENTIMIENTO_DATOS (hecho legal: la API solo usa 3 de los 5)
-- --------------------------------------------------------------------------
DO $$
DECLARE
    T constant text := 'consentimiento_datos';
    j json; v_asis bigint; v_id bigint;
BEGIN
    CALL catedras.sp_guardar_asistente('PRUEBA0005', 'Eva', 'Prueba', NULL, 'eva.prueba@x.edu.co',
        NULL, NULL, false, true, NULL, NULL, NULL, NULL, NULL, v_asis);

    j := pg_temp.sp(format($f$CALL catedras.sp_crear_consentimiento_datos(%s, 'v1.0', NULL, '10.0.0.1', NULL)$f$, v_asis));
    v_id := (j ->> 'id_consentimiento')::bigint;
    PERFORM pg_temp.ok(T, 'crear devuelve id y aceptado_en', v_id IS NOT NULL AND j ->> 'aceptado_en' IS NOT NULL);
    PERFORM pg_temp.ok(T, 'canal NULL -> WEB (DEFAULT)', j ->> 'canal' = 'WEB');
    PERFORM pg_temp.ok(T, 'ip vuelve como texto sin máscara (host)', j ->> 'ip' = '10.0.0.1');
    PERFORM pg_temp.falla(T, 'misma versión para el mismo asistente -> uq_consentimiento_ver',
        format($f$CALL catedras.sp_crear_consentimiento_datos(%s, 'v1.0', 'WEB', NULL, NULL)$f$, v_asis), '23505');
    PERFORM pg_temp.falla(T, 'IP mal escrita -> error de tipo INET',
        format($f$CALL catedras.sp_crear_consentimiento_datos(%s, 'v2.0', 'WEB', '999.1.1', NULL)$f$, v_asis), '22P02');
    PERFORM pg_temp.falla(T, 'canal fuera de la lista -> CHECK',
        format($f$CALL catedras.sp_crear_consentimiento_datos(%s, 'v3.0', 'FAX', NULL, NULL)$f$, v_asis), '23514');

    j := pg_temp.sp(format('CALL catedras.sp_listar_consentimiento_datos(%s, NULL, NULL)', v_asis));
    PERFORM pg_temp.ok(T, 'listar por asistente', json_array_length(j) = 1);
    PERFORM pg_temp.ok(T, 'consultar existente / inexistente',
        pg_temp.sp(format('CALL catedras.sp_consultar_consentimiento_datos(%s, NULL)', v_id)) IS NOT NULL
        AND pg_temp.sp('CALL catedras.sp_consultar_consentimiento_datos(-1, NULL)') IS NULL);

    -- Los 2 que NO usa la API: existen y funcionan a nivel de BD.
    j := pg_temp.sp(format($f$CALL catedras.sp_actualizar_consentimiento_datos(%s, 'v1.0', 'PRESENCIAL', NULL, NULL)$f$, v_id));
    PERFORM pg_temp.ok(T, '[solo BD] actualizar funciona', j ->> 'canal' = 'PRESENCIAL' AND j ->> 'ip' IS NULL);
    PERFORM pg_temp.ok(T, '[solo BD] eliminar es físico -> true y luego false',
        pg_temp.eliminado(pg_temp.sp(format('CALL catedras.sp_eliminar_consentimiento_datos(%s, NULL)', v_id)))
        AND NOT pg_temp.eliminado(pg_temp.sp(format('CALL catedras.sp_eliminar_consentimiento_datos(%s, NULL)', v_id))));
EXCEPTION WHEN OTHERS THEN
    PERFORM pg_temp.ok('consentimiento_datos', 'bloque terminó sin excepción inesperada', false, SQLSTATE || ' ' || SQLERRM);
END $$;

-- --------------------------------------------------------------------------
--  6. LOTE_CARGA_ASISTENTE + 7. NOVEDAD_CARGA (maestro-detalle de la v3)
-- --------------------------------------------------------------------------
DO $$
DECLARE
    T constant text := 'lote_carga_asistente';
    N constant text := 'novedad_carga';
    j json; v_lote bigint; v_nov bigint;
BEGIN
    j := pg_temp.sp($f$CALL catedras.sp_crear_lote_carga_asistente('personas_prueba.xlsx', NULL, NULL)$f$);
    v_lote := (j ->> 'id_lote_carga')::bigint;
    PERFORM pg_temp.ok(T, 'crear (abrir) devuelve id, cargado_en y contadores en 0',
        v_lote IS NOT NULL AND j ->> 'cargado_en' IS NOT NULL
        AND (j ->> 'filas_leidas')::int = 0 AND (j ->> 'filas_aceptadas')::int = 0 AND (j ->> 'filas_rechazadas')::int = 0);
    j := pg_temp.sp('CALL catedras.sp_listar_lote_carga_asistente(NULL, NULL)');
    PERFORM pg_temp.ok(T, 'listar: el más reciente primero', (j -> 0 ->> 'id_lote_carga')::bigint = v_lote);
    PERFORM pg_temp.ok(T, 'consultar inexistente -> NULL',
        pg_temp.sp('CALL catedras.sp_consultar_lote_carga_asistente(-1, NULL)') IS NULL);

    -- El CIERRE sigue siendo sp_guardar_lote_carga (003), sin tocar.
    CALL catedras.sp_guardar_lote_carga(v_lote, 3, 1, 2, 'cierre de prueba',
        '[{"numero_fila":2,"contenido_crudo":"a;b","motivo":"correo duplicado"},
          {"numero_fila":3,"contenido_crudo":"c;d","motivo":"sin id"}]'::jsonb);
    j := pg_temp.sp(format('CALL catedras.sp_consultar_lote_carga_asistente(%s, NULL)', v_lote));
    PERFORM pg_temp.ok(T, 'sp_guardar_lote_carga (003) sigue cerrando y sp_consultar lo lee',
        (j ->> 'filas_leidas')::int = 3 AND j ->> 'observaciones' = 'cierre de prueba');

    j := pg_temp.sp(format('CALL catedras.sp_listar_novedad_carga(%s, NULL, NULL)', v_lote));
    PERFORM pg_temp.ok(N, 'listar por lote: las 2 novedades del cierre, por número de fila',
        json_array_length(j) = 2 AND (j -> 0 ->> 'numero_fila')::int = 2 AND (j -> 1 ->> 'numero_fila')::int = 3);
    v_nov := (j -> 0 ->> 'id_novedad')::bigint;
    PERFORM pg_temp.ok(N, 'consultar existente / inexistente',
        pg_temp.sp(format('CALL catedras.sp_consultar_novedad_carga(%s, NULL)', v_nov)) ->> 'motivo' = 'correo duplicado'
        AND pg_temp.sp('CALL catedras.sp_consultar_novedad_carga(-1, NULL)') IS NULL);
    j := pg_temp.sp(format($f$CALL catedras.sp_crear_novedad_carga(%s, 4, 'e;f', 'otra', NULL)$f$, v_lote));
    PERFORM pg_temp.ok(N, '[solo BD] crear funciona', (j ->> 'numero_fila')::int = 4);
    PERFORM pg_temp.falla(N, '[solo BD] crear con numero_fila 0 -> CHECK',
        format($f$CALL catedras.sp_crear_novedad_carga(%s, 0, 'x', 'y', NULL)$f$, v_lote), '23514');
    j := pg_temp.sp(format($f$CALL catedras.sp_actualizar_novedad_carga(%s, 2, 'a;b', 'motivo corregido', NULL)$f$, v_nov));
    PERFORM pg_temp.ok(N, '[solo BD] actualizar funciona', j ->> 'motivo' = 'motivo corregido');
    PERFORM pg_temp.ok(N, '[solo BD] eliminar físico -> true',
        pg_temp.eliminado(pg_temp.sp(format('CALL catedras.sp_eliminar_novedad_carga(%s, NULL)', v_nov))));

    PERFORM pg_temp.falla(T, '[solo BD] actualizar con contadores que no cuadran -> CHECK',
        format($f$CALL catedras.sp_actualizar_lote_carga_asistente(%s, 'x.xlsx', NULL, 1, 1, 1, NULL, NULL)$f$, v_lote), '23514');
    j := pg_temp.sp(format($f$CALL catedras.sp_actualizar_lote_carga_asistente(%s, 'renombrado.xlsx', NULL, 3, 1, 2, NULL, NULL)$f$, v_lote));
    PERFORM pg_temp.ok(T, '[solo BD] actualizar completo funciona', j ->> 'nombre_archivo' = 'renombrado.xlsx' AND j ->> 'observaciones' IS NULL);
    -- (El NOT EXISTS va en una sentencia APARTE: dentro de la misma
    --  sentencia vería la foto de ANTES del DELETE.)
    PERFORM pg_temp.ok(T, '[solo BD] eliminar físico -> true',
        pg_temp.eliminado(pg_temp.sp(format('CALL catedras.sp_eliminar_lote_carga_asistente(%s, NULL)', v_lote))));
    PERFORM pg_temp.ok(T, '[solo BD] ... y sus novedades se fueron en CASCADE',
        NOT EXISTS (SELECT 1 FROM novedad_carga WHERE fk_lote_carga = v_lote));
EXCEPTION WHEN OTHERS THEN
    PERFORM pg_temp.ok('lote_carga_asistente', 'bloque terminó sin excepción inesperada', false, SQLSTATE || ' ' || SQLERRM);
END $$;

-- --------------------------------------------------------------------------
--  8. LOTE_MIGRACION + 9. DETALLE_MIGRACION (v7, construidas en este lote)
-- --------------------------------------------------------------------------
DO $$
DECLARE
    T constant text := 'lote_migracion';
    D constant text := 'detalle_migracion';
    j json; v_lote bigint; v_lotes_antes int; v_sin bigint; v_det bigint; v_det2 bigint;
BEGIN
    j := pg_temp.sp('CALL catedras.sp_listar_lote_migracion(5712, NULL, NULL)');
    PERFORM pg_temp.ok(T, 'listar por sesión 5712: los 5 lotes de ejemplo, el más reciente primero',
        json_array_length(j) = 5 AND (j -> 0 ->> 'id_lote_migracion')::bigint = 5);
    j := pg_temp.sp('CALL catedras.sp_consultar_lote_migracion(1, NULL)');
    PERFORM pg_temp.ok(T, 'consultar lote 1 (CORRECTO, instancia 9284471)',
        j ->> 'fk_estado_proceso' = 'CORRECTO' AND j ->> 'instancia_proceso_asis' = '9284471');
    PERFORM pg_temp.ok(T, 'consultar inexistente -> NULL',
        pg_temp.sp('CALL catedras.sp_consultar_lote_migracion(-1, NULL)') IS NULL);

    -- CREAR = generar (reusa sp_generar_lote_migracion)
    UPDATE sesion SET estado = 'CERRADA' WHERE id_sesion = 5712 AND estado = 'MIGRADA';
    j := pg_temp.sp('CALL catedras.sp_crear_lote_migracion(5712, NULL, NULL)');
    v_lote := (j ->> 'id_lote_migracion')::bigint;
    PERFORM pg_temp.ok(T, 'crear (generar) devuelve el lote GENERADO con nombre y total calculados',
        j ->> 'fk_estado_proceso' = 'GENERADO' AND j ->> 'nombre_archivo' LIKE 'ME_CATEDRA_%_R%.xlsx'
        AND (j ->> 'total_filas')::int = (SELECT count(*) FROM fn_exportar_asis(5712)));
    PERFORM pg_temp.ok(T, 'crear genera una fila de detalle por cada fila migrable',
        (SELECT count(*) FROM detalle_migracion WHERE fk_lote_migracion = v_lote) = (j ->> 'total_filas')::int);
    PERFORM pg_temp.ok(T, 'crear pone la sesión en MIGRADA (efecto colateral RF184)',
        (SELECT estado FROM sesion WHERE id_sesion = 5712) = 'MIGRADA');

    SELECT id_sesion INTO v_sin FROM sesion s
     WHERE NOT EXISTS (SELECT 1 FROM registro_asistencia r WHERE r.fk_sesion = s.id_sesion)
     ORDER BY id_sesion LIMIT 1;
    SELECT count(*) INTO v_lotes_antes FROM lote_migracion;
    PERFORM pg_temp.falla(T, 'sesión sin registros migrables -> excepción de la BD (RF181)',
        format('CALL catedras.sp_crear_lote_migracion(%s, NULL, NULL)', v_sin), '23514');
    PERFORM pg_temp.ok(T, '... y no queda ningún lote a medias',
        (SELECT count(*) FROM lote_migracion) = v_lotes_antes);

    j := pg_temp.sp(format($f$CALL catedras.sp_actualizar_lote_migracion(%s, '777001', 'EN_CURSO', 'enviado', NULL)$f$, v_lote));
    PERFORM pg_temp.ok(T, 'actualizar estado / instancia / observaciones',
        j ->> 'fk_estado_proceso' = 'EN_CURSO' AND j ->> 'instancia_proceso_asis' = '777001' AND j ->> 'observaciones' = 'enviado');
    PERFORM pg_temp.ok(T, 'actualizar NO toca la foto de la generación (total_filas, nombre_archivo)',
        (j ->> 'total_filas')::int > 0 AND j ->> 'nombre_archivo' LIKE 'ME_CATEDRA_%');
    PERFORM pg_temp.falla(T, 'estado inexistente -> FK fk_lotemig_estado',
        format($f$CALL catedras.sp_actualizar_lote_migracion(%s, NULL, 'NO_EXISTE', NULL, NULL)$f$, v_lote), '23503');
    PERFORM pg_temp.ok(T, 'actualizar inexistente -> NULL',
        pg_temp.sp($f$CALL catedras.sp_actualizar_lote_migracion(-1, NULL, 'EN_CURSO', NULL, NULL)$f$) IS NULL);

    -- DETALLE
    j := pg_temp.sp('CALL catedras.sp_listar_detalle_migracion(2, NULL, NULL)');
    PERFORM pg_temp.ok(D, 'listar por lote 2: sus 164 filas, ordenadas por id',
        json_array_length(j) = 164 AND (j -> 0 ->> 'id_detalle')::bigint < (j -> 163 ->> 'id_detalle')::bigint);
    PERFORM pg_temp.ok(D, 'listar respeta el límite',
        json_array_length(pg_temp.sp('CALL catedras.sp_listar_detalle_migracion(NULL, 10, NULL)')) = 10);
    PERFORM pg_temp.ok(D, 'consultar inexistente -> NULL',
        pg_temp.sp('CALL catedras.sp_consultar_detalle_migracion(-1, NULL)') IS NULL);

    -- Una fila del lote 2 cuyo registro ya está ACEPTADO en el lote 1.
    SELECT d2.id_detalle INTO v_det
      FROM detalle_migracion d2
      JOIN detalle_migracion d1 ON d1.fk_registro = d2.fk_registro AND d1.resultado = 'ACEPTADO'
     WHERE d2.fk_lote_migracion = 2 ORDER BY d2.id_detalle LIMIT 1;
    PERFORM pg_temp.falla(D, 'segundo ACEPTADO del mismo registro -> ux_detmig_aceptado (RF188)',
        format($f$CALL catedras.sp_actualizar_detalle_migracion(%s, 'ACEPTADO', NULL, NULL)$f$, v_det), '23505');
    j := pg_temp.sp(format($f$CALL catedras.sp_actualizar_detalle_migracion(%s, 'RECHAZADO', 'ID no existe en ASIS', NULL)$f$, v_det));
    PERFORM pg_temp.ok(D, 'actualizar resultado + mensaje (parámetro p_resultado_migracion)',
        j ->> 'resultado' = 'RECHAZADO' AND j ->> 'mensaje' = 'ID no existe en ASIS');
    PERFORM pg_temp.ok(D, 'actualizar NO toca las columnas *_enviado(a)',
        j ->> 'id_asis_enviado' = (SELECT id_asis_enviado FROM detalle_migracion WHERE id_detalle = v_det));
    PERFORM pg_temp.falla(D, 'resultado fuera de la lista -> CHECK',
        format($f$CALL catedras.sp_actualizar_detalle_migracion(%s, 'QUIZAS', NULL, NULL)$f$, v_det), '23514');
    PERFORM pg_temp.ok(D, 'actualizar inexistente -> NULL',
        pg_temp.sp($f$CALL catedras.sp_actualizar_detalle_migracion(-1, 'ACEPTADO', NULL, NULL)$f$) IS NULL);

    -- Los que NO usa la API (RF189): existen y funcionan a nivel de BD.
    PERFORM pg_temp.falla(D, '[solo BD] crear el mismo registro en el mismo lote -> uq_detalle_lote_reg',
        format($f$CALL catedras.sp_crear_detalle_migracion(2, %s, 'X12345', 1, NULL, NULL)$f$,
               (SELECT fk_registro FROM detalle_migracion WHERE id_detalle = v_det)), '23505');
    -- Un registro de la sesión que NO entró al lote (externo o sin programa).
    j := pg_temp.sp(format($f$CALL catedras.sp_crear_detalle_migracion(%s, %s, 'X12345', 1, 'M0200', NULL)$f$,
               v_lote, (SELECT r.id_registro FROM registro_asistencia r
                         WHERE r.fk_sesion = 5712
                           AND NOT EXISTS (SELECT 1 FROM detalle_migracion d
                                            WHERE d.fk_lote_migracion = v_lote AND d.fk_registro = r.id_registro)
                         ORDER BY r.id_registro LIMIT 1)));
    v_det2 := (j ->> 'id_detalle')::bigint;
    PERFORM pg_temp.ok(D, '[solo BD] crear nace PENDIENTE; reunion_enviada entra como integer',
        j ->> 'resultado' = 'PENDIENTE' AND (j ->> 'reunion_enviada')::int = 1);
    PERFORM pg_temp.ok(D, '[solo BD] eliminar físico -> true',
        pg_temp.eliminado(pg_temp.sp(format('CALL catedras.sp_eliminar_detalle_migracion(%s, NULL)', v_det2))));

    PERFORM pg_temp.ok(T, '[solo BD] eliminar lote físico -> true',
        pg_temp.eliminado(pg_temp.sp(format('CALL catedras.sp_eliminar_lote_migracion(%s, NULL)', v_lote))));
    PERFORM pg_temp.ok(T, '[solo BD] ... y sus detalles se fueron en CASCADE',
        NOT EXISTS (SELECT 1 FROM detalle_migracion WHERE fk_lote_migracion = v_lote));
EXCEPTION WHEN OTHERS THEN
    PERFORM pg_temp.ok('lote_migracion', 'bloque terminó sin excepción inesperada', false, SQLSTATE || ' ' || SQLERRM);
END $$;

-- --------------------------------------------------------------------------
--  Resultado
-- --------------------------------------------------------------------------
\pset format unaligned
\pset tuples_only on
SELECT CASE WHEN ok THEN '  OK    ' ELSE '  FALLA ' END || rpad(tabla, 22) || caso
       || CASE WHEN NOT ok AND detalle IS NOT NULL THEN E'\n          -> ' || detalle ELSE '' END
  FROM resultado_prueba ORDER BY n;
SELECT format(E'\n  %s casos: %s OK, %s FALLA', count(*), count(*) FILTER (WHERE ok), count(*) FILTER (WHERE NOT ok))
  FROM resultado_prueba;

-- Si algo falló, termina con error (código de salida != 0).
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM resultado_prueba WHERE NOT ok) THEN
        RAISE EXCEPTION 'Hay casos en FALLA (ver arriba).';
    END IF;
END $$;

ROLLBACK;
