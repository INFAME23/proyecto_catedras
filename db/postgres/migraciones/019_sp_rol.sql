-- ==========================================================================
--  db/postgres/migraciones/019_sp_rol.sql
--  Proyecto Cátedras · RETROFIT criterio 3 de VERSION_2.md (Fase 2 de
--  PLAN_RETROFIT_SP.md): los 5 verbos de 'rol' por procedimiento
--  almacenado, con el MISMO molde de 005_sp_sede_ejemplo.sql.
--
--  'rol' NO tiene columna 'activo' -> 'sp_eliminar_rol' es un DELETE
--  físico y 'sp_listar_rol' devuelve TODAS las filas. Un rol asignado en
--  rol_por_usuario hace fallar el DELETE por FK, igual que antes.
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  LISTAR — todas las filas (no hay 'activo'), con límite OPCIONAL.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_listar_rol(
    IN    p_limite      integer DEFAULT NULL,
    INOUT p_resultado   json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(r), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_rol, nombre, descripcion
              FROM rol
             ORDER BY id_rol
             LIMIT p_limite) r;
END;
$$;

-- --------------------------------------------------------------------------
--  CONSULTAR — una fila por id. NULL en p_resultado = no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_consultar_rol(
    IN    p_id_rol      character varying(20),
    INOUT p_resultado   json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(r) INTO p_resultado
      FROM (SELECT id_rol, nombre, descripcion
              FROM rol
             WHERE id_rol = p_id_rol) r;
END;
$$;

-- --------------------------------------------------------------------------
--  CREAR — devuelve la fila como quedó guardada. Id repetido o un CHECK
--  violado -> NpgsqlException (el controlador la traduce, criterio 7).
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_crear_rol(
    IN    p_id_rol      character varying(20),
    IN    p_nombre      character varying(60),
    IN    p_descripcion character varying(200) DEFAULT NULL,
    INOUT p_resultado   json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    INSERT INTO rol (id_rol, nombre, descripcion)
    VALUES (p_id_rol, p_nombre, p_descripcion);

    SELECT row_to_json(r) INTO p_resultado
      FROM (SELECT id_rol, nombre, descripcion
              FROM rol
             WHERE id_rol = p_id_rol) r;
END;
$$;

-- --------------------------------------------------------------------------
--  ACTUALIZAR — reemplazo COMPLETO (PUT directo; PATCH llega ya mezclado
--  desde el repositorio). Sin FOUND -> p_resultado NULL = no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_rol(
    IN    p_id_rol      character varying(20),
    IN    p_nombre      character varying(60),
    IN    p_descripcion character varying(200),
    INOUT p_resultado   json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE rol
       SET nombre      = p_nombre,
           descripcion = p_descripcion
     WHERE id_rol = p_id_rol;

    IF FOUND THEN
        SELECT row_to_json(r) INTO p_resultado
          FROM (SELECT id_rol, nombre, descripcion
                  FROM rol
                 WHERE id_rol = p_id_rol) r;
    END IF;
END;
$$;

-- --------------------------------------------------------------------------
--  ELIMINAR — FÍSICO (la tabla NO tiene 'activo'). FK en uso -> excepción.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_rol(
    IN    p_id_rol      character varying(20),
    INOUT p_resultado   json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    DELETE FROM rol
     WHERE id_rol = p_id_rol;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_rol IS
    'Retrofit criterio 3 (VERSION_2.md §4). Todas las filas, con límite opcional.';
COMMENT ON PROCEDURE catedras.sp_consultar_rol IS
    'Retrofit criterio 3. NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_rol IS
    'Retrofit criterio 3. Devuelve la fila como quedó guardada.';
COMMENT ON PROCEDURE catedras.sp_actualizar_rol IS
    'Retrofit criterio 3. Reemplazo completo; PATCH mezcla antes de llamar.';
COMMENT ON PROCEDURE catedras.sp_eliminar_rol IS
    'Retrofit criterio 3. Borrado FÍSICO (DELETE): la tabla no tiene activo.';
