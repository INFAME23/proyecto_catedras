-- ==========================================================================
--  db/postgres/migraciones/044_sp_guardar_sesion.sql
--  Proyecto Cátedras · Maestro-detalle en el formulario — Guía IA, Regla 1
-- --------------------------------------------------------------------------
--  Maestro: 'sesion'. Detalle (opcional): su primer 'enlace_registro'
--  (#2 de la Regla 4).
--
--  POST /api/Sesion puede traer, además de la sesión, un objeto 'enlace'
--  ({"minutos_antes":30, "minutos_despues":30, "fk_usuario":1}) para
--  emitir de una vez el enlace / QR de registro. Sesión y enlace se
--  guardan en UNA sola transacción: si emitir falla, la sesión tampoco
--  queda creada.
--
--  REUTILIZA, no reescribe: la sesión se crea con 'sp_crear_sesion'
--  (migración 011, con su trigger de consecutivo) y el enlace con
--  'sp_emitir_enlace' (PROVISTO por el script de la BD: arma el token, la
--  URL, el canal según la modalidad y la ventana según los parámetros).
--
--  Los registros de asistencia (#3) NO van aquí: nacen cuando la sesión ya
--  ocurrió, cada uno en su momento (caso "No aplica" de la Regla 1).
--
--  CÓMO SE APLICA: docker-compose.yml lo monta como 'z044_' (después de
--  'z011_'). Con una BD ya creada, a mano con psql.
-- ==========================================================================

SET search_path TO catedras, public;

CREATE OR REPLACE PROCEDURE catedras.sp_guardar_sesion(
    -- Columnas propias del maestro (mismo orden y tipos que sp_crear_sesion).
    IN    p_fk_catedra          bigint,
    IN    p_inicio              timestamp with time zone,
    IN    p_fin                 timestamp with time zone,
    IN    p_fk_modalidad        character varying(15),
    IN    p_fk_sede             character varying(15),
    IN    p_fk_periodo          character(6),
    IN    p_numero_reunion      smallint                 DEFAULT NULL,
    IN    p_numero_reunion_asis smallint                 DEFAULT NULL,
    IN    p_titulo              character varying(200)   DEFAULT NULL,
    IN    p_fk_encuesta         bigint                   DEFAULT NULL,
    IN    p_lugar               character varying(200)   DEFAULT NULL,
    IN    p_enlace_virtual      character varying(300)   DEFAULT NULL,
    IN    p_cupo                integer                  DEFAULT NULL,
    IN    p_estado              character varying(15)    DEFAULT 'PROGRAMADA',
    -- Detalle: objeto JSON opcional. NULL = no se emite enlace.
    IN    p_enlace              jsonb                    DEFAULT NULL,
    -- Salida: la sesión como quedó (releída) + 'id_enlace' (null si no se emitió).
    INOUT p_resultado           json                     DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_sesion    json;
    v_id_enlace bigint;
BEGIN
    -- 1) Maestro (reutiliza sp_crear_sesion: el trigger pone el consecutivo).
    CALL catedras.sp_crear_sesion(p_fk_catedra, p_inicio, p_fin, p_fk_modalidad, p_fk_sede,
                                  p_fk_periodo, p_numero_reunion, p_numero_reunion_asis,
                                  p_titulo, p_fk_encuesta, p_lugar, p_enlace_virtual,
                                  p_cupo, COALESCE(p_estado, 'PROGRAMADA'), v_sesion);

    -- 2) Detalle: el primer enlace de registro, en el MISMO COMMIT.
    IF p_enlace IS NOT NULL AND jsonb_typeof(p_enlace) = 'object' THEN
        CALL catedras.sp_emitir_enlace(
            (v_sesion ->> 'id_sesion')::bigint,
            (p_enlace ->> 'minutos_antes')::integer,     -- NULL = el del parámetro
            (p_enlace ->> 'minutos_despues')::integer,
            (p_enlace ->> 'fk_usuario')::bigint,
            v_id_enlace);
    END IF;

    p_resultado := (v_sesion::jsonb || jsonb_build_object('id_enlace', v_id_enlace))::json;
END;
$$;

COMMENT ON PROCEDURE catedras.sp_guardar_sesion IS
    'Guía IA · Regla 1. Crea una sesión y, opcionalmente, emite su primer enlace de '
    'registro, en una sola transacción. Reutiliza sp_crear_sesion y sp_emitir_enlace. '
    'Devuelve la sesión releída + id_enlace.';
