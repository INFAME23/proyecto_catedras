-- ==========================================================================
--  db/postgres/migraciones/015_sp_encuesta.sql
--  Proyecto Cátedras · RETROFIT criterio 3 de VERSION_2.md (Fase 2 de
--  PLAN_RETROFIT_SP.md): los 5 verbos de 'encuesta' por procedimiento
--  almacenado, con el MISMO molde de 005_sp_sede_ejemplo.sql.
--
--  AJUSTES respecto a sede:
--   · 'encuesta' tiene 'activa' (femenino), NO 'activo'. La regla del
--     retrofit dice "UPDATE activo=false si la tabla tiene columna
--     'activo', DELETE si no" (y PLAN_RETROFIT_SP.md §2 la marca "No") ->
--     'sp_eliminar_encuesta' es un DELETE FÍSICO y 'sp_listar_encuesta'
--     devuelve TODAS las filas. Una encuesta con respuestas
--     (fk_respenc_encuesta, ON DELETE RESTRICT) no se deja borrar.
--   · 'id_encuesta' es GENERATED ALWAYS AS IDENTITY: lo pone la BD, no el
--     cliente. 'sp_crear_encuesta' NO recibe id: lo captura con
--     RETURNING ... INTO v_id y devuelve la fila con él (el repositorio lo
--     lee del JSON, porque 'CrearAsync' devuelve el id nuevo).
--   · 'sp_crear_encuesta' recibe 'p_activa' (el POST la admite).
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  LISTAR — todas las filas (no hay 'activo'), con límite OPCIONAL.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_listar_encuesta(
    IN    p_limite    integer DEFAULT NULL,
    INOUT p_resultado json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(e), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_encuesta, nombre, version, vigente_desde, vigente_hasta, activa
              FROM encuesta
             ORDER BY id_encuesta
             LIMIT p_limite) e;
END;
$$;

-- --------------------------------------------------------------------------
--  CONSULTAR — una encuesta por id. NULL en p_resultado = no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_consultar_encuesta(
    IN    p_id_encuesta bigint,
    INOUT p_resultado   json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(e) INTO p_resultado
      FROM (SELECT id_encuesta, nombre, version, vigente_desde, vigente_hasta, activa
              FROM encuesta
             WHERE id_encuesta = p_id_encuesta) e;
END;
$$;

-- --------------------------------------------------------------------------
--  CREAR — el id lo genera la BD (IDENTITY): se captura en 'v_id' y se
--  devuelve la fila como quedó guardada. vigente_hasta < vigente_desde
--  (chk_encuesta_vig) o version < 1 (chk_encuesta_ver) -> NpgsqlException.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_crear_encuesta(
    IN    p_nombre        character varying(120),
    IN    p_version       smallint DEFAULT 1,
    IN    p_vigente_desde date     DEFAULT CURRENT_DATE,
    IN    p_vigente_hasta date     DEFAULT NULL,
    IN    p_activa        boolean  DEFAULT true,
    INOUT p_resultado     json     DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_id bigint;
BEGIN
    INSERT INTO encuesta (nombre, version, vigente_desde, vigente_hasta, activa)
    VALUES (p_nombre, p_version, p_vigente_desde, p_vigente_hasta, p_activa)
    RETURNING id_encuesta INTO v_id;

    SELECT row_to_json(e) INTO p_resultado
      FROM (SELECT id_encuesta, nombre, version, vigente_desde, vigente_hasta, activa
              FROM encuesta
             WHERE id_encuesta = v_id) e;
END;
$$;

-- --------------------------------------------------------------------------
--  ACTUALIZAR — reemplazo COMPLETO (PUT directo; PATCH llega ya mezclado
--  desde el repositorio). Sin FOUND -> p_resultado NULL = no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_encuesta(
    IN    p_id_encuesta   bigint,
    IN    p_nombre        character varying(120),
    IN    p_version       smallint,
    IN    p_vigente_desde date,
    IN    p_vigente_hasta date,
    IN    p_activa        boolean,
    INOUT p_resultado     json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE encuesta
       SET nombre        = p_nombre,
           version       = p_version,
           vigente_desde = p_vigente_desde,
           vigente_hasta = p_vigente_hasta,
           activa        = p_activa
     WHERE id_encuesta = p_id_encuesta;

    IF FOUND THEN
        SELECT row_to_json(e) INTO p_resultado
          FROM (SELECT id_encuesta, nombre, version, vigente_desde, vigente_hasta, activa
                  FROM encuesta
                 WHERE id_encuesta = p_id_encuesta) e;
    END IF;
END;
$$;

-- --------------------------------------------------------------------------
--  ELIMINAR — FÍSICO (la tabla NO tiene 'activo'; su 'activa' es un dato
--  de negocio que se cambia con PUT/PATCH). FK en uso -> excepción.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_encuesta(
    IN    p_id_encuesta bigint,
    INOUT p_resultado   json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    DELETE FROM encuesta
     WHERE id_encuesta = p_id_encuesta;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_encuesta IS
    'Retrofit criterio 3 (VERSION_2.md §4). Todas las filas, con límite opcional.';
COMMENT ON PROCEDURE catedras.sp_consultar_encuesta IS
    'Retrofit criterio 3. NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_encuesta IS
    'Retrofit criterio 3. id IDENTITY; devuelve la fila como quedó guardada.';
COMMENT ON PROCEDURE catedras.sp_actualizar_encuesta IS
    'Retrofit criterio 3. Reemplazo completo; PATCH mezcla antes de llamar.';
COMMENT ON PROCEDURE catedras.sp_eliminar_encuesta IS
    'Retrofit criterio 3. Borrado FÍSICO (DELETE): la tabla no tiene activo.';
