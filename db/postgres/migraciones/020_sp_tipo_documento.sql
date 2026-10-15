-- ==========================================================================
--  db/postgres/migraciones/020_sp_tipo_documento.sql
--  Proyecto Cátedras · RETROFIT criterio 3 de VERSION_2.md (Fase 2 de
--  PLAN_RETROFIT_SP.md): los 5 verbos de 'tipo_documento' por procedimiento
--  almacenado, con el MISMO molde de 005_sp_sede_ejemplo.sql.
--
--  'tipo_documento' SÍ tiene columna 'activo' -> borrado LÓGICO y listar
--  solo activos, igual que sede.
--
--  AJUSTE respecto a sede: 'sp_crear_tipo_documento' recibe 'p_activo'
--  (el POST lo admite y el INSERT anterior lo guardaba).
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  LISTAR — solo activos, con límite OPCIONAL (NULL = sin límite).
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_listar_tipo_documento(
    IN    p_limite            integer DEFAULT NULL,
    INOUT p_resultado         json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(t), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_tipo_documento, nombre, activo
              FROM tipo_documento
             WHERE activo
             ORDER BY id_tipo_documento
             LIMIT p_limite) t;
END;
$$;

-- --------------------------------------------------------------------------
--  CONSULTAR — una fila por id. NULL en p_resultado = no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_consultar_tipo_documento(
    IN    p_id_tipo_documento character varying(5),
    INOUT p_resultado         json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(t) INTO p_resultado
      FROM (SELECT id_tipo_documento, nombre, activo
              FROM tipo_documento
             WHERE id_tipo_documento = p_id_tipo_documento) t;
END;
$$;

-- --------------------------------------------------------------------------
--  CREAR — devuelve la fila como quedó guardada. Id repetido o un CHECK
--  violado -> NpgsqlException (el controlador la traduce, criterio 7).
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_crear_tipo_documento(
    IN    p_id_tipo_documento character varying(5),
    IN    p_nombre            character varying(60),
    IN    p_activo            boolean DEFAULT true,
    INOUT p_resultado         json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    INSERT INTO tipo_documento (id_tipo_documento, nombre, activo)
    VALUES (p_id_tipo_documento, p_nombre, p_activo);

    SELECT row_to_json(t) INTO p_resultado
      FROM (SELECT id_tipo_documento, nombre, activo
              FROM tipo_documento
             WHERE id_tipo_documento = p_id_tipo_documento) t;
END;
$$;

-- --------------------------------------------------------------------------
--  ACTUALIZAR — reemplazo COMPLETO (PUT directo; PATCH llega ya mezclado
--  desde el repositorio). Sin FOUND -> p_resultado NULL = no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_tipo_documento(
    IN    p_id_tipo_documento character varying(5),
    IN    p_nombre            character varying(60),
    IN    p_activo            boolean,
    INOUT p_resultado         json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE tipo_documento
       SET nombre = p_nombre,
           activo = p_activo
     WHERE id_tipo_documento = p_id_tipo_documento;

    IF FOUND THEN
        SELECT row_to_json(t) INTO p_resultado
          FROM (SELECT id_tipo_documento, nombre, activo
                  FROM tipo_documento
                 WHERE id_tipo_documento = p_id_tipo_documento) t;
    END IF;
END;
$$;

-- --------------------------------------------------------------------------
--  ELIMINAR — LÓGICO (la tabla tiene 'activo'). Ya inactivo -> no cuenta.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_tipo_documento(
    IN    p_id_tipo_documento character varying(5),
    INOUT p_resultado         json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE tipo_documento
       SET activo = false
     WHERE id_tipo_documento = p_id_tipo_documento
       AND activo;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_tipo_documento IS
    'Retrofit criterio 3 (VERSION_2.md §4). Solo activos, con límite opcional.';
COMMENT ON PROCEDURE catedras.sp_consultar_tipo_documento IS
    'Retrofit criterio 3. NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_tipo_documento IS
    'Retrofit criterio 3. Devuelve la fila como quedó guardada.';
COMMENT ON PROCEDURE catedras.sp_actualizar_tipo_documento IS
    'Retrofit criterio 3. Reemplazo completo; PATCH mezcla antes de llamar.';
COMMENT ON PROCEDURE catedras.sp_eliminar_tipo_documento IS
    'Retrofit criterio 3. Borrado LÓGICO (activo=false): la tabla tiene activo.';
