-- ==========================================================================
--  db/postgres/migraciones/059_separacion_entre_sesiones_enlace.sql
--  Proyecto Cátedras · SUBVERSIÓN v7.1 · D59
-- --------------------------------------------------------------------------
--  1. NO PUEDEN HABER DOS SESIONES A LA MISMA HORA, en ninguna modalidad ni sede,
--     y entre una y otra debe haber al menos 60 minutos. Todas las cátedras usan
--     el mismo enlace de ingreso, así que dos sesiones pegadas se pisarían.
--
--       - El tiempo mínimo vive en el parámetro MINUTOS_ENTRE_SESIONES (60 por defecto).
--       - Un disparador sobre 'sesion' (INSERT, o UPDATE que cambie inicio/fin) rechaza la
--         sesión que quede a menos de ese margen de otra (P0001 -> 409 en la API). Cubre
--         todo: formulario de cátedra, sesión suelta, aprobación de un ponente.
--         Sesiones que ya existían NO se tocan; solo cuenta lo que se cree o se mueva.
--       - Terminar a las 10:00 y empezar a las 11:00 es válido (margen exacto de 60 min).
--       - Solo cuentan las sesiones de cátedras ACTIVAS: una cátedra desactivada deja de ocupar el enlace
--         (si no, el histórico desactivado bloquearía horarios que ya nadie usa).
--       - sp_sesiones_en_conflicto suma el motivo 'HORARIO' (otra sesión a menos del
--         margen, sin importar la sede), para que los formularios avisen antes de guardar.
--       - El formulario «Soy ponente» también lo rechaza al proponer (fn_validar_periodo_y_sede).
--
--  2. ENLACE DE INGRESO al crear la cátedra: 'sesion.enlace_virtual' ya existía; ahora
--     sp_resolver_solicitud_ponente lo recibe (p_enlace_virtual) y lo guarda en la sesión
--     que crea al aprobar. Idempotente.
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  Parámetro
-- --------------------------------------------------------------------------
INSERT INTO catedras.parametro (clave, valor, tipo_dato, descripcion)
VALUES ('MINUTOS_ENTRE_SESIONES', '60', 'ENTERO',
        'Minutos mínimos entre el fin de una sesión y el inicio de otra (todas usan el mismo enlace de ingreso).')
ON CONFLICT (clave) DO NOTHING;

CREATE OR REPLACE FUNCTION catedras.fn_minutos_entre_sesiones()
    RETURNS integer
    LANGUAGE sql STABLE
    AS $$
    SELECT greatest(0, coalesce((SELECT p.valor::integer FROM catedras.parametro p
                                  WHERE p.clave = 'MINUTOS_ENTRE_SESIONES'), 60));
$$;

-- --------------------------------------------------------------------------
--  Valida un horario contra TODAS las sesiones (P0001 si no hay margen)
-- --------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION catedras.fn_validar_separacion(
    p_inicio timestamptz, p_fin timestamptz, p_excluir_sesion bigint DEFAULT NULL)
    RETURNS void
    LANGUAGE plpgsql STABLE
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_min integer := fn_minutos_entre_sesiones();
    v_gap interval := make_interval(mins => fn_minutos_entre_sesiones());
    v_otra record;
BEGIN
    SELECT s.numero_reunion, s.inicio, s.fin, c.nombre
      INTO v_otra
      FROM sesion s
      JOIN catedra c ON c.id_catedra = s.fk_catedra
     WHERE c.activo                       -- una cátedra desactivada ya no ocupa el enlace
       AND s.inicio < p_fin + v_gap
       AND s.fin    > p_inicio - v_gap
       AND (p_excluir_sesion IS NULL OR s.id_sesion <> p_excluir_sesion)
     ORDER BY s.inicio, s.id_sesion
     LIMIT 1;

    IF FOUND THEN
        RAISE EXCEPTION 'El horario % a % (hora de Colombia) choca con la cátedra «%» (reunión %, % a %): entre dos sesiones debe haber al menos % minutos, porque todas usan el mismo enlace de ingreso. Elige otra hora.',
            to_char(p_inicio AT TIME ZONE 'America/Bogota', 'YYYY-MM-DD HH24:MI'),
            to_char(p_fin AT TIME ZONE 'America/Bogota', 'HH24:MI'),
            v_otra.nombre, v_otra.numero_reunion,
            to_char(v_otra.inicio AT TIME ZONE 'America/Bogota', 'YYYY-MM-DD HH24:MI'),
            to_char(v_otra.fin AT TIME ZONE 'America/Bogota', 'HH24:MI'),
            v_min
            USING ERRCODE = 'P0001';
    END IF;
END;
$$;

-- --------------------------------------------------------------------------
--  Disparador sobre 'sesion'
-- --------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION catedras.fn_trg_sesion_separacion()
    RETURNS trigger
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    -- Corregir otros datos de una sesión existente no se bloquea: solo importa si cambia su horario.
    IF TG_OP = 'UPDATE' AND NEW.inicio = OLD.inicio AND NEW.fin = OLD.fin THEN
        RETURN NEW;
    END IF;

    -- Una a la vez: dos transacciones que crean sesiones juntas no se ven entre sí hasta confirmar.
    PERFORM pg_advisory_xact_lock(hashtext('catedras.sesion.separacion'));

    PERFORM fn_validar_separacion(NEW.inicio, NEW.fin, NEW.id_sesion);
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_sesion_separacion ON catedras.sesion;
CREATE TRIGGER trg_sesion_separacion
    BEFORE INSERT OR UPDATE OF inicio, fin ON catedras.sesion
    FOR EACH ROW EXECUTE FUNCTION catedras.fn_trg_sesion_separacion();

-- --------------------------------------------------------------------------
--  Consulta de conflictos: ahora también 'HORARIO' (a menos del margen de otra sesión)
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_sesiones_en_conflicto(
    IN    p_inicio         timestamptz,
    IN    p_fin            timestamptz,
    IN    p_fk_sede        text   DEFAULT NULL,
    IN    p_lugar          text   DEFAULT NULL,
    IN    p_fk_asistente   bigint DEFAULT NULL,
    IN    p_excluir_sesion bigint DEFAULT NULL,
    INOUT p_resultado      json   DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_sede_fisica boolean;
    v_lugar       text     := lower(btrim(coalesce(p_lugar, '')));
    v_gap         interval := make_interval(mins => fn_minutos_entre_sesiones());
BEGIN
    SELECT NOT s.es_virtual INTO v_sede_fisica FROM sede s WHERE s.id_sede = p_fk_sede;
    v_sede_fisica := coalesce(v_sede_fisica, false);

    SELECT COALESCE(json_agg(r ORDER BY r.inicio, r.id_sesion), '[]'::json)
      INTO p_resultado
      FROM (
            SELECT x.id_sesion, x.fk_catedra, x.nombre_catedra, x.numero_reunion,
                   x.inicio, x.fin, x.fk_sede, x.lugar, x.fk_modalidad, x.estado, x.solapa,
                   array_remove(ARRAY[
                       CASE WHEN x.solapa
                             AND v_sede_fisica
                             AND x.fk_sede = p_fk_sede
                             AND (v_lugar = ''
                                  OR btrim(coalesce(x.lugar, '')) = ''
                                  OR lower(btrim(x.lugar)) = v_lugar)
                            THEN 'LUGAR' END,
                       CASE WHEN x.solapa
                             AND p_fk_asistente IS NOT NULL
                             AND EXISTS (SELECT 1 FROM ponencia po
                                          WHERE po.fk_sesion = x.id_sesion AND po.fk_asistente = p_fk_asistente)
                            THEN 'PONENTE' END,
                       -- v7.1 D59: cualquier sesión a menos del margen, de cualquier sede o modalidad
                       -- (las sesiones de una cátedra desactivada no cuentan)
                       CASE WHEN x.catedra_activa THEN 'HORARIO' END
                   ], NULL) AS motivos
              FROM (SELECT s.id_sesion, s.fk_catedra, c.nombre AS nombre_catedra, s.numero_reunion,
                           s.inicio, s.fin, s.fk_sede, s.lugar, s.fk_modalidad, s.estado,
                           c.activo AS catedra_activa,
                           (s.inicio < p_fin AND s.fin > p_inicio) AS solapa
                      FROM sesion s
                      JOIN catedra c ON c.id_catedra = s.fk_catedra
                     WHERE s.inicio < p_fin + v_gap
                       AND s.fin    > p_inicio - v_gap
                       AND (p_excluir_sesion IS NULL OR s.id_sesion <> p_excluir_sesion)) x
           ) r
     WHERE cardinality(r.motivos) > 0;
END;
$$;

COMMENT ON PROCEDURE catedras.sp_sesiones_en_conflicto IS
    'v7.1 (D52, D59). Sesiones que chocan con un horario: LUGAR (se traslapan en la misma sede física y salón), PONENTE (ya participa) u HORARIO (a menos de MINUTOS_ENTRE_SESIONES de otra sesión, en cualquier sede). "solapa" dice si además se traslapan. Solo lectura.';

-- --------------------------------------------------------------------------
--  El formulario «Soy ponente» y la aprobación también revisan el margen
-- --------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION catedras.fn_validar_periodo_y_sede(
    p_inicio timestamptz, p_fin timestamptz, p_fk_periodo text, p_fk_sede text)
    RETURNS void
    LANGUAGE plpgsql STABLE
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_per periodo_academico%ROWTYPE;
    v_activa boolean;
BEGIN
    IF p_fk_periodo IS NOT NULL THEN
        SELECT * INTO v_per FROM periodo_academico WHERE id_periodo = p_fk_periodo;
        IF NOT FOUND THEN
            RAISE EXCEPTION 'El periodo académico % no existe.', p_fk_periodo USING ERRCODE = 'P0001';
        END IF;
        IF NOT v_per.activo THEN
            RAISE EXCEPTION 'El periodo académico % no está activo.', btrim(p_fk_periodo) USING ERRCODE = 'P0001';
        END IF;
        IF (p_inicio AT TIME ZONE 'America/Bogota')::date < v_per.fecha_ini
           OR (p_fin AT TIME ZONE 'America/Bogota')::date > v_per.fecha_fin THEN
            RAISE EXCEPTION 'La fecha de la cátedra (% a %, hora de Colombia) no cae dentro del periodo % (del % al %): elige otra fecha u otro periodo.',
                to_char(p_inicio AT TIME ZONE 'America/Bogota', 'YYYY-MM-DD HH24:MI'),
                to_char(p_fin AT TIME ZONE 'America/Bogota', 'YYYY-MM-DD HH24:MI'),
                btrim(p_fk_periodo), to_char(v_per.fecha_ini, 'YYYY-MM-DD'), to_char(v_per.fecha_fin, 'YYYY-MM-DD')
                USING ERRCODE = 'P0001';
        END IF;
    END IF;

    IF p_fk_sede IS NOT NULL THEN
        SELECT activo INTO v_activa FROM sede WHERE id_sede = p_fk_sede;
        IF NOT FOUND THEN
            RAISE EXCEPTION 'La sede % no existe.', p_fk_sede USING ERRCODE = 'P0001';
        END IF;
        IF NOT v_activa THEN
            RAISE EXCEPTION 'La sede % no está activa.', p_fk_sede USING ERRCODE = 'P0001';
        END IF;
    END IF;

    -- v7.1 D59: ninguna otra sesión a menos del margen (cualquier sede o modalidad).
    PERFORM fn_validar_separacion(p_inicio, p_fin);
END;
$$;

COMMENT ON FUNCTION catedras.fn_validar_periodo_y_sede IS
    'v7.1 D58/D59. P0001 si el periodo no existe, no está activo o no contiene la fecha, si la sede no existe o no está activa, o si otra sesión queda a menos de MINUTOS_ENTRE_SESIONES.';
COMMENT ON FUNCTION catedras.fn_validar_separacion IS
    'v7.1 D59. P0001 si el horario queda a menos de MINUTOS_ENTRE_SESIONES de cualquier otra sesión (todas usan el mismo enlace).';

-- --------------------------------------------------------------------------
--  RESOLVER — igual que la 058, más el enlace de ingreso de la cátedra que se crea
-- --------------------------------------------------------------------------
DROP PROCEDURE IF EXISTS catedras.sp_resolver_solicitud_ponente(bigint, boolean, text, bigint, text, text, text, text, text, text, text, timestamp with time zone, timestamp with time zone, json);

CREATE OR REPLACE PROCEDURE catedras.sp_resolver_solicitud_ponente(
    IN    p_id_solicitud   bigint,
    IN    p_aprobar        boolean,
    IN    p_observacion    text   DEFAULT NULL,
    IN    p_resuelto_por   bigint DEFAULT NULL,
    IN    p_nombre_catedra text   DEFAULT NULL,   -- vacío = el título de la ponencia
    IN    p_fk_tipo_evento text   DEFAULT NULL,   -- vacío = CAAB (cátedra abierta)
    IN    p_id_evento_asis text   DEFAULT NULL,   -- vacío = el siguiente código libre
    IN    p_fk_modalidad   text   DEFAULT NULL,   -- vacío = la que eligió el ponente
    IN    p_fk_sede        text   DEFAULT NULL,   -- vacío = la que eligió el ponente
    IN    p_fk_periodo     text   DEFAULT NULL,   -- vacío = el que eligió el ponente
    IN    p_lugar          text   DEFAULT NULL,
    IN    p_inicio         timestamp with time zone DEFAULT NULL,   -- vacío = el propuesto
    IN    p_fin            timestamp with time zone DEFAULT NULL,
    IN    p_enlace_virtual text   DEFAULT NULL,   -- enlace de ingreso (http/https); vacío = sin enlace
    INOUT p_resultado      json   DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_sol         solicitud_ponente%ROWTYPE;
    v_asistente   bigint;
    v_observacion text := nullif(btrim(coalesce(p_observacion, '')), '');
    v_enlace      text := nullif(btrim(coalesce(p_enlace_virtual, '')), '');
    v_inicio      timestamptz;
    v_fin         timestamptz;
    v_modalidad   text;
    v_sede        text;
    v_periodo     text;
    v_codigo      text;
    v_creada      json;
    v_id_catedra  bigint;
    v_id_sesion   bigint;
BEGIN
    SELECT * INTO v_sol FROM solicitud_ponente WHERE id_solicitud = p_id_solicitud FOR UPDATE;
    IF NOT FOUND THEN
        RETURN;
    END IF;

    IF v_sol.estado <> 'PENDIENTE' THEN
        RAISE EXCEPTION 'La solicitud % ya fue resuelta (%).', p_id_solicitud, v_sol.estado
            USING ERRCODE = 'P0001';
    END IF;

    IF p_aprobar THEN
        -- Antes de tocar nada: lo que la cátedra nueva necesita.
        IF v_sol.inicio_propuesto IS NOT NULL THEN
            v_inicio    := coalesce(p_inicio, v_sol.inicio_propuesto);
            v_fin       := coalesce(p_fin, v_sol.fin_propuesto);
            v_modalidad := coalesce(nullif(btrim(coalesce(p_fk_modalidad, '')), ''), v_sol.fk_modalidad);
            v_sede      := coalesce(nullif(btrim(coalesce(p_fk_sede, '')), ''), v_sol.fk_sede);
            v_periodo   := coalesce(nullif(btrim(coalesce(p_fk_periodo, '')), ''), btrim(v_sol.fk_periodo));
            IF v_fin <= v_inicio THEN
                RAISE EXCEPTION 'La hora de fin debe ser posterior a la de inicio.' USING ERRCODE = 'P0001';
            END IF;
            IF v_inicio <= now() THEN
                RAISE EXCEPTION 'La fecha propuesta ya pasó: ajusta la fecha y la hora para aprobar la cátedra.' USING ERRCODE = 'P0001';
            END IF;
            IF v_modalidad IS NULL OR v_sede IS NULL OR v_periodo IS NULL THEN
                RAISE EXCEPTION 'Para aprobar hay que indicar la modalidad, la sede y el periodo de la cátedra nueva.'
                    USING ERRCODE = 'P0001';
            END IF;
            IF v_enlace IS NOT NULL AND (v_enlace !~* '^https?://[^[:space:]]+$' OR length(v_enlace) > 300) THEN
                RAISE EXCEPTION 'El enlace de ingreso debe empezar por http:// o https:// y tener hasta 300 caracteres.'
                    USING ERRCODE = 'P0001';
            END IF;
            PERFORM fn_validar_periodo_y_sede(v_inicio, v_fin, v_periodo, v_sede);
            v_codigo := nullif(btrim(coalesce(p_id_evento_asis, '')), '');
            IF v_codigo IS NOT NULL AND v_codigo !~ '^[0-9]{9}$' THEN
                RAISE EXCEPTION 'El código ASIS debe tener 9 dígitos (con ceros a la izquierda).' USING ERRCODE = 'P0001';
            END IF;
        END IF;

        -- 1) La persona: por documento; si no, por correo institucional; si no existe, se crea como externa.
        SELECT d.fk_asistente INTO v_asistente
          FROM documento_asistente d
         WHERE d.fk_tipo_documento = v_sol.fk_tipo_documento
           AND d.numero            = v_sol.numero_documento
         ORDER BY d.vigente DESC, d.registrado_en DESC
         LIMIT 1;

        IF v_asistente IS NULL THEN
            SELECT a.id_asistente INTO v_asistente
              FROM asistente a
             WHERE a.correo_institucional = v_sol.correo_institucional;

            IF v_asistente IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM documento_asistente d WHERE d.fk_asistente = v_asistente AND d.vigente) THEN
                INSERT INTO documento_asistente (fk_asistente, fk_tipo_documento, numero)
                VALUES (v_asistente, v_sol.fk_tipo_documento, v_sol.numero_documento);
            END IF;
        END IF;

        IF v_asistente IS NULL THEN
            INSERT INTO asistente (nombres, apellidos, nombre_completo_asis,
                                   correo_institucional, celular, es_externo)
            VALUES (v_sol.nombres, v_sol.apellidos,
                    left(v_sol.nombres || ' ' || v_sol.apellidos, 200),
                    v_sol.correo_institucional, v_sol.telefono, true)
            RETURNING id_asistente INTO v_asistente;

            INSERT INTO documento_asistente (fk_asistente, fk_tipo_documento, numero)
            VALUES (v_asistente, v_sol.fk_tipo_documento, v_sol.numero_documento);
        END IF;

        -- 2) La cátedra nueva con su sesión (sp_guardar_catedra: todo o nada; los disparadores de la 056 y la 059
        --    revisan periodos/horarios y el margen entre sesiones).
        IF v_sol.inicio_propuesto IS NOT NULL THEN
            IF v_codigo IS NULL THEN
                SELECT lpad((coalesce(max(c.id_evento_asis::bigint), 0) + 1)::text, 9, '0') INTO v_codigo FROM catedra c;
            END IF;

            CALL catedras.sp_guardar_catedra(
                v_codigo::character(9),
                left(coalesce(nullif(btrim(coalesce(p_nombre_catedra, '')), ''), v_sol.titulo_ponencia), 200)::character varying(200),
                coalesce(nullif(btrim(coalesce(p_fk_tipo_evento, '')), ''), 'CAAB')::character(4),
                NULL::character varying(15),
                true,
                jsonb_build_array(jsonb_build_object(
                    'inicio',         v_inicio,
                    'fin',            v_fin,
                    'fk_modalidad',   btrim(v_modalidad),
                    'fk_sede',        btrim(v_sede),
                    'fk_periodo',     btrim(v_periodo),
                    'titulo',         left(v_sol.titulo_ponencia, 200),
                    'lugar',          nullif(btrim(coalesce(p_lugar, '')), ''),
                    'enlace_virtual', v_enlace)),
                v_creada);
            v_id_catedra := (v_creada ->> 'id_catedra')::bigint;
            v_id_sesion  := (v_creada -> 'sesiones' -> 0 ->> 'id_sesion')::bigint;

            UPDATE solicitud_ponente SET fk_catedra = v_id_catedra, fk_sesion = v_id_sesion
             WHERE id_solicitud = p_id_solicitud;
            v_sol.fk_sesion := v_id_sesion;
        END IF;

        -- 3) La participación en la sesión (la nueva, o la elegida en una solicitud anterior a la 057).
        IF v_sol.fk_sesion IS NOT NULL THEN
            INSERT INTO ponencia (fk_sesion, fk_asistente, rol)
            VALUES (v_sol.fk_sesion, v_asistente, 'PONENTE')
            ON CONFLICT DO NOTHING;
        END IF;

        UPDATE solicitud_ponente
           SET estado = 'APROBADA', observacion_admin = v_observacion, fk_asistente = v_asistente,
               resuelto_por = p_resuelto_por, resuelto_en = now()
         WHERE id_solicitud = p_id_solicitud;
    ELSE
        IF v_observacion IS NULL THEN
            RAISE EXCEPTION 'Para rechazar la solicitud hay que escribir el motivo.'
                USING ERRCODE = 'P0001';
        END IF;

        UPDATE solicitud_ponente
           SET estado = 'RECHAZADA', observacion_admin = v_observacion,
               resuelto_por = p_resuelto_por, resuelto_en = now()
         WHERE id_solicitud = p_id_solicitud;
    END IF;

    SELECT row_to_json(v) INTO p_resultado FROM v_solicitud_ponente v WHERE v.id_solicitud = p_id_solicitud;
END;
$$;

COMMENT ON PROCEDURE catedras.sp_resolver_solicitud_ponente IS
    'v7.1 D56-D59. Aprueba (crea la cátedra nueva con su sesión, enlace de ingreso incluido, y registra al ponente) o rechaza con motivo. P0001 si la solicitud ya se resolvió, falta un dato, el horario/periodo/sede no valen, el enlace no es http(s) o la sesión queda a menos de MINUTOS_ENTRE_SESIONES de otra.';
