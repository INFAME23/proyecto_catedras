-- ==========================================================================
--  db/postgres/migraciones/056_restriccion_programacion.sql
--  Proyecto Cátedras · SUBVERSIÓN v7.1 · D57 (periodos y horarios en que se pueden programar cátedras)
-- --------------------------------------------------------------------------
--  Las cátedras se dictan en periodos de tiempo específicos. El administrador
--  define VENTANAS de programación («Periodo 2026-2: del 3 de agosto al 5 de
--  diciembre, de 06:00 a 21:00, de lunes a sábado») y desde entonces una sesión
--  solo se puede crear o mover a una fecha y hora que quepa en alguna ventana
--  ACTIVA.
--
--    · Sin ventanas activas NO hay restricción (el comportamiento de siempre).
--    · Una sesión cabe en una ventana si: su fecha de inicio y la de fin están
--      entre fecha_desde y fecha_hasta; su hora de inicio y de fin están entre
--      hora_desde y hora_hasta (y empieza y termina el mismo día); y su día de
--      la semana (ISO: 1 = lunes … 7 = domingo) está en dias_semana. hora_* y
--      dias_semana vacíos = sin límite de hora / de día.
--    · Todo se evalúa en hora de Colombia (America/Bogota, UTC-5).
--
--  La regla vive en la BASE DE DATOS (disparador sobre 'sesion'), así vale para
--  cualquier camino: formulario de cátedra, de sesión, API directa. El disparador
--  solo mira las sesiones nuevas o a las que se les CAMBIA inicio o fin: corregir
--  el salón de una sesión vieja no se bloquea. Un rechazo sale como P0001 (la
--  API lo responde 409 con el mensaje).
--
--  Idempotente (se puede volver a aplicar).
-- ==========================================================================

SET search_path TO catedras, public;

CREATE TABLE IF NOT EXISTS catedras.restriccion_programacion (
    id_restriccion bigint GENERATED ALWAYS AS IDENTITY,
    nombre         character varying(100) NOT NULL,
    fecha_desde    date NOT NULL,
    fecha_hasta    date NOT NULL,
    hora_desde     time without time zone,
    hora_hasta     time without time zone,
    dias_semana    smallint[],
    activo         boolean NOT NULL DEFAULT true,
    creado_en      timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT pk_restriccion_programacion PRIMARY KEY (id_restriccion),
    CONSTRAINT chk_restprog_fechas CHECK (fecha_hasta >= fecha_desde),
    CONSTRAINT chk_restprog_horas CHECK (
        (hora_desde IS NULL AND hora_hasta IS NULL)
        OR (hora_desde IS NOT NULL AND hora_hasta IS NOT NULL AND hora_hasta > hora_desde)),
    CONSTRAINT chk_restprog_dias CHECK (
        dias_semana IS NULL
        OR (cardinality(dias_semana) BETWEEN 1 AND 7 AND dias_semana <@ ARRAY[1,2,3,4,5,6,7]::smallint[]))
);

COMMENT ON TABLE catedras.restriccion_programacion IS
    'v7.1 D57. Ventanas (fechas, horas y días) en que se pueden programar sesiones. Sin ventanas activas no hay restricción.';

-- --------------------------------------------------------------------------
--  Texto legible de una ventana: «Periodo 2026-2»: del 2026-08-03 al 2026-12-05, de 06:00 a 21:00, lun, mar
-- --------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION catedras.fn_texto_restriccion(r catedras.restriccion_programacion)
    RETURNS text
    LANGUAGE sql STABLE
    AS $$
    SELECT '«' || r.nombre || '»: del ' || to_char(r.fecha_desde, 'YYYY-MM-DD') || ' al ' || to_char(r.fecha_hasta, 'YYYY-MM-DD')
        || CASE WHEN r.hora_desde IS NULL THEN ''
                ELSE ', de ' || to_char(r.hora_desde, 'HH24:MI') || ' a ' || to_char(r.hora_hasta, 'HH24:MI') END
        || CASE WHEN r.dias_semana IS NULL THEN ''
                ELSE ', ' || array_to_string(ARRAY(
                        SELECT (ARRAY['lun','mar','mié','jue','vie','sáb','dom'])[d]
                          FROM unnest(r.dias_semana) AS d ORDER BY d), ', ') END;
$$;

-- --------------------------------------------------------------------------
--  ¿Cabe este horario en alguna ventana activa? (sin ventanas activas: sí)
-- --------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION catedras.fn_horario_permitido(p_inicio timestamptz, p_fin timestamptz)
    RETURNS boolean
    LANGUAGE sql STABLE
    AS $$
    SELECT NOT EXISTS (SELECT 1 FROM catedras.restriccion_programacion WHERE activo)
        OR EXISTS (
            SELECT 1
              FROM catedras.restriccion_programacion r
             WHERE r.activo
               AND (p_inicio AT TIME ZONE 'America/Bogota')::date >= r.fecha_desde
               AND (p_fin    AT TIME ZONE 'America/Bogota')::date <= r.fecha_hasta
               AND (r.hora_desde IS NULL
                    OR ((p_inicio AT TIME ZONE 'America/Bogota')::time >= r.hora_desde
                        AND (p_fin AT TIME ZONE 'America/Bogota')::time <= r.hora_hasta
                        AND (p_fin AT TIME ZONE 'America/Bogota')::date = (p_inicio AT TIME ZONE 'America/Bogota')::date))
               AND (r.dias_semana IS NULL
                    OR extract(isodow FROM p_inicio AT TIME ZONE 'America/Bogota')::smallint = ANY (r.dias_semana)));
$$;

-- Mensaje con las ventanas activas, para explicar un rechazo.
CREATE OR REPLACE FUNCTION catedras.fn_mensaje_ventanas()
    RETURNS text
    LANGUAGE sql STABLE
    AS $$
    SELECT string_agg(catedras.fn_texto_restriccion(r), '; ' ORDER BY r.fecha_desde, r.id_restriccion)
      FROM catedras.restriccion_programacion r
     WHERE r.activo;
$$;

-- --------------------------------------------------------------------------
--  Disparador sobre 'sesion'
-- --------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION catedras.fn_trg_sesion_restriccion()
    RETURNS trigger
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    -- Corregir otros datos de una sesión existente no se bloquea: solo importa si cambia su horario.
    IF TG_OP = 'UPDATE' AND NEW.inicio = OLD.inicio AND NEW.fin = OLD.fin THEN
        RETURN NEW;
    END IF;

    IF NOT fn_horario_permitido(NEW.inicio, NEW.fin) THEN
        RAISE EXCEPTION 'El horario % a % (hora de Colombia) está fuera de los periodos en que se pueden programar cátedras. Se puede programar en: %.',
            to_char(NEW.inicio AT TIME ZONE 'America/Bogota', 'YYYY-MM-DD HH24:MI'),
            to_char(NEW.fin AT TIME ZONE 'America/Bogota', 'YYYY-MM-DD HH24:MI'),
            fn_mensaje_ventanas()
            USING ERRCODE = 'P0001';
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_sesion_restriccion ON catedras.sesion;
CREATE TRIGGER trg_sesion_restriccion
    BEFORE INSERT OR UPDATE OF inicio, fin ON catedras.sesion
    FOR EACH ROW EXECUTE FUNCTION catedras.fn_trg_sesion_restriccion();

-- --------------------------------------------------------------------------
--  Procedimientos
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_listar_restriccion_programacion(
    IN    p_solo_activas boolean DEFAULT false,
    IN    p_limite       integer DEFAULT 500,
    INOUT p_resultado    json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(x), '[]'::json)
      INTO p_resultado
      FROM (SELECT r.*, fn_texto_restriccion(r) AS texto
              FROM restriccion_programacion r
             WHERE NOT p_solo_activas OR r.activo
             ORDER BY r.activo DESC, r.fecha_desde DESC, r.id_restriccion DESC
             LIMIT p_limite) x;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_consultar_restriccion_programacion(
    IN    p_id_restriccion bigint,
    INOUT p_resultado      json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(x) INTO p_resultado
      FROM (SELECT r.*, fn_texto_restriccion(r) AS texto
              FROM restriccion_programacion r
             WHERE r.id_restriccion = p_id_restriccion) x;
END;
$$;

-- Horas como texto 'HH:MI' y días como texto '1,2,3' (vacío = sin límite): se evita depender de cómo
-- el cliente arma arreglos y horas.
CREATE OR REPLACE PROCEDURE catedras.sp_crear_restriccion_programacion(
    IN    p_nombre      text,
    IN    p_fecha_desde date,
    IN    p_fecha_hasta date,
    IN    p_hora_desde  text,
    IN    p_hora_hasta  text,
    IN    p_dias_semana text,
    IN    p_activo      boolean DEFAULT true,
    INOUT p_resultado   json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_id bigint;
BEGIN
    INSERT INTO restriccion_programacion (nombre, fecha_desde, fecha_hasta, hora_desde, hora_hasta, dias_semana, activo)
    VALUES (p_nombre, p_fecha_desde, p_fecha_hasta,
            nullif(btrim(p_hora_desde), '')::time, nullif(btrim(p_hora_hasta), '')::time,
            string_to_array(nullif(btrim(p_dias_semana), ''), ',')::smallint[],
            coalesce(p_activo, true))
    RETURNING id_restriccion INTO v_id;

    CALL sp_consultar_restriccion_programacion(v_id, p_resultado);
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_restriccion_programacion(
    IN    p_id_restriccion bigint,
    IN    p_nombre         text,
    IN    p_fecha_desde    date,
    IN    p_fecha_hasta    date,
    IN    p_hora_desde     text,
    IN    p_hora_hasta     text,
    IN    p_dias_semana    text,
    IN    p_activo         boolean,
    INOUT p_resultado      json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE restriccion_programacion
       SET nombre = p_nombre, fecha_desde = p_fecha_desde, fecha_hasta = p_fecha_hasta,
           hora_desde = nullif(btrim(p_hora_desde), '')::time, hora_hasta = nullif(btrim(p_hora_hasta), '')::time,
           dias_semana = string_to_array(nullif(btrim(p_dias_semana), ''), ',')::smallint[],
           activo = coalesce(p_activo, true)
     WHERE id_restriccion = p_id_restriccion;

    IF FOUND THEN
        CALL sp_consultar_restriccion_programacion(p_id_restriccion, p_resultado);
    END IF;
END;
$$;

-- Borrado FÍSICO: una ventana es configuración, no historia (para pausarla se desactiva).
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_restriccion_programacion(
    IN    p_id_restriccion bigint,
    INOUT p_resultado      json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    DELETE FROM restriccion_programacion WHERE id_restriccion = p_id_restriccion;
    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

-- ¿Cabe este horario? Devuelve { permitido, hay_restricciones, mensaje, ventanas:[{id_restriccion, nombre, texto}] }.
CREATE OR REPLACE PROCEDURE catedras.sp_validar_horario_programacion(
    IN    p_inicio    timestamptz,
    IN    p_fin       timestamptz,
    INOUT p_resultado json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_ventanas json;
    v_hay      boolean;
    v_ok       boolean := fn_horario_permitido(p_inicio, p_fin);
BEGIN
    SELECT COALESCE(json_agg(json_build_object('id_restriccion', r.id_restriccion, 'nombre', r.nombre,
                                               'texto', fn_texto_restriccion(r))
                             ORDER BY r.fecha_desde, r.id_restriccion), '[]'::json),
           count(*) > 0
      INTO v_ventanas, v_hay
      FROM restriccion_programacion r
     WHERE r.activo;

    p_resultado := json_build_object(
        'permitido', v_ok,
        'hay_restricciones', v_hay,
        'mensaje', CASE WHEN v_ok THEN NULL
                        ELSE 'Ese horario está fuera de los periodos en que se pueden programar cátedras. Se puede programar en: '
                             || fn_mensaje_ventanas() || '.' END,
        'ventanas', v_ventanas);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_validar_horario_programacion IS
    'v7.1 D57. ¿Cabe el horario en alguna ventana activa? (sin ventanas activas, sí). Solo lectura.';
COMMENT ON FUNCTION catedras.fn_trg_sesion_restriccion IS
    'v7.1 D57. Rechaza (P0001) una sesión nueva o con inicio/fin cambiado que no cabe en ninguna ventana activa.';
