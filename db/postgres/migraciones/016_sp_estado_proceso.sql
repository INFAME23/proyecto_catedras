-- ==========================================================================
--  db/postgres/migraciones/016_sp_estado_proceso.sql
--  Proyecto Cátedras · RETROFIT criterio 3 de VERSION_2.md (Fase 2 de
--  PLAN_RETROFIT_SP.md): los 5 verbos de 'estado_proceso' por procedimiento
--  almacenado, con el MISMO molde de 005_sp_sede_ejemplo.sql.
--
--  'estado_proceso' NO tiene columna 'activo' -> 'sp_eliminar_estado_proceso'
--  es un DELETE físico y 'sp_listar_estado_proceso' devuelve TODAS las
--  filas (no hay nada que filtrar). Un estado en uso por un lote de
--  migración (fk_lotemig_estado) hace fallar el DELETE -> NpgsqlException.
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  LISTAR — todas las filas (no hay 'activo'), con límite OPCIONAL.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_listar_estado_proceso(
    IN    p_limite     integer DEFAULT NULL,
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(e), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_estado, nombre, es_final, es_exitoso
              FROM estado_proceso
             ORDER BY id_estado
             LIMIT p_limite) e;
END;
$$;

-- --------------------------------------------------------------------------
--  CONSULTAR — una fila por id. NULL en p_resultado = no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_consultar_estado_proceso(
    IN    p_id_estado  character varying(12),
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(e) INTO p_resultado
      FROM (SELECT id_estado, nombre, es_final, es_exitoso
              FROM estado_proceso
             WHERE id_estado = p_id_estado) e;
END;
$$;

-- --------------------------------------------------------------------------
--  CREAR — devuelve la fila como quedó guardada. Id repetido o un CHECK
--  violado -> NpgsqlException (el controlador la traduce, criterio 7).
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_crear_estado_proceso(
    IN    p_id_estado  character varying(12),
    IN    p_nombre     character varying(40),
    IN    p_es_final   boolean DEFAULT false,
    IN    p_es_exitoso boolean DEFAULT false,
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    INSERT INTO estado_proceso (id_estado, nombre, es_final, es_exitoso)
    VALUES (p_id_estado, p_nombre, p_es_final, p_es_exitoso);

    SELECT row_to_json(e) INTO p_resultado
      FROM (SELECT id_estado, nombre, es_final, es_exitoso
              FROM estado_proceso
             WHERE id_estado = p_id_estado) e;
END;
$$;

-- --------------------------------------------------------------------------
--  ACTUALIZAR — reemplazo COMPLETO (PUT directo; PATCH llega ya mezclado
--  desde el repositorio). Sin FOUND -> p_resultado NULL = no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_estado_proceso(
    IN    p_id_estado  character varying(12),
    IN    p_nombre     character varying(40),
    IN    p_es_final   boolean,
    IN    p_es_exitoso boolean,
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE estado_proceso
       SET nombre     = p_nombre,
           es_final   = p_es_final,
           es_exitoso = p_es_exitoso
     WHERE id_estado = p_id_estado;

    IF FOUND THEN
        SELECT row_to_json(e) INTO p_resultado
          FROM (SELECT id_estado, nombre, es_final, es_exitoso
                  FROM estado_proceso
                 WHERE id_estado = p_id_estado) e;
    END IF;
END;
$$;

-- --------------------------------------------------------------------------
--  ELIMINAR — FÍSICO (la tabla NO tiene 'activo'). FK en uso -> excepción.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_estado_proceso(
    IN    p_id_estado  character varying(12),
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    DELETE FROM estado_proceso
     WHERE id_estado = p_id_estado;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_estado_proceso IS
    'Retrofit criterio 3 (VERSION_2.md §4). Todas las filas, con límite opcional.';
COMMENT ON PROCEDURE catedras.sp_consultar_estado_proceso IS
    'Retrofit criterio 3. NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_estado_proceso IS
    'Retrofit criterio 3. Devuelve la fila como quedó guardada.';
COMMENT ON PROCEDURE catedras.sp_actualizar_estado_proceso IS
    'Retrofit criterio 3. Reemplazo completo; PATCH mezcla antes de llamar.';
COMMENT ON PROCEDURE catedras.sp_eliminar_estado_proceso IS
    'Retrofit criterio 3. Borrado FÍSICO (DELETE): la tabla no tiene activo.';
