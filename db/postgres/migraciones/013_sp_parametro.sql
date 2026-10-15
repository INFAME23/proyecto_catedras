-- ==========================================================================
--  db/postgres/migraciones/013_sp_parametro.sql
--  Proyecto Cátedras · RETROFIT criterio 3 de VERSION_2.md · 'parametro'
-- --------------------------------------------------------------------------
--  'parametro' es la configuración que el administrador cambia sin tocar el
--  código. Clave primaria: 'clave' (varchar(60)). NO tiene columna 'activo'
--  -> 'sp_eliminar_parametro' es FÍSICO (DELETE) y 'sp_listar_parametro'
--  devuelve todas las filas.
--
--  Esta tabla NO tenía código en la API (ni modelo, ni repositorio, ni
--  servicio, ni controlador): su capa C# se construye junto con esta
--  migración.
--
--  'modificado_en' se refresca en cada 'sp_actualizar_parametro'; en el
--  alta lo pone el DEFAULT now() de la columna. 'fk_usuario_modifica' es
--  OPCIONAL (la API aún no tiene sesión de usuario que lo llene sola).
-- ==========================================================================

SET search_path TO catedras, public;

CREATE OR REPLACE PROCEDURE catedras.sp_listar_parametro(
    IN    p_limite    integer DEFAULT NULL,
    INOUT p_resultado json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(p), '[]'::json)
      INTO p_resultado
      FROM (SELECT clave, valor, tipo_dato, descripcion,
                   fk_usuario_modifica, modificado_en
              FROM parametro
             ORDER BY clave
             LIMIT p_limite) p;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_consultar_parametro(
    IN    p_clave     character varying(60),
    INOUT p_resultado json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(p) INTO p_resultado
      FROM (SELECT clave, valor, tipo_dato, descripcion,
                   fk_usuario_modifica, modificado_en
              FROM parametro
             WHERE clave = p_clave) p;
END;
$$;

-- Clave repetida (pk_parametro), tipo_dato fuera de la lista
-- (chk_parametro_tipo) o usuario inexistente (fk_parametro_usuario) ->
-- NpgsqlException.
CREATE OR REPLACE PROCEDURE catedras.sp_crear_parametro(
    IN    p_clave               character varying(60),
    IN    p_valor               character varying(200),
    IN    p_descripcion         character varying(300),
    IN    p_tipo_dato           character varying(15) DEFAULT 'TEXTO',
    IN    p_fk_usuario_modifica bigint                DEFAULT NULL,
    INOUT p_resultado           json                  DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    INSERT INTO parametro (clave, valor, tipo_dato, descripcion, fk_usuario_modifica)
    VALUES (p_clave, p_valor, COALESCE(p_tipo_dato, 'TEXTO'), p_descripcion,
            p_fk_usuario_modifica);

    SELECT row_to_json(p) INTO p_resultado
      FROM (SELECT clave, valor, tipo_dato, descripcion,
                   fk_usuario_modifica, modificado_en
              FROM parametro
             WHERE clave = p_clave) p;
END;
$$;

-- Reemplazo COMPLETO de las columnas de negocio ('clave' es la llave y no
-- cambia). Refresca 'modificado_en'.
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_parametro(
    IN    p_clave               character varying(60),
    IN    p_valor               character varying(200),
    IN    p_tipo_dato           character varying(15),
    IN    p_descripcion         character varying(300),
    IN    p_fk_usuario_modifica bigint,
    INOUT p_resultado           json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE parametro
       SET valor               = p_valor,
           tipo_dato           = p_tipo_dato,
           descripcion         = p_descripcion,
           fk_usuario_modifica = p_fk_usuario_modifica,
           modificado_en       = now()
     WHERE clave = p_clave;

    IF FOUND THEN
        SELECT row_to_json(p) INTO p_resultado
          FROM (SELECT clave, valor, tipo_dato, descripcion,
                       fk_usuario_modifica, modificado_en
                  FROM parametro
                 WHERE clave = p_clave) p;
    END IF;
END;
$$;

-- FÍSICO: la tabla no tiene 'activo'.
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_parametro(
    IN    p_clave     character varying(60),
    INOUT p_resultado json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    DELETE FROM parametro
     WHERE clave = p_clave;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_parametro IS
    'Retrofit criterio 3 (VERSION_2.md §4). Todas (sin activo), con límite opcional.';
COMMENT ON PROCEDURE catedras.sp_consultar_parametro IS
    'Retrofit criterio 3. NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_parametro IS
    'Retrofit criterio 3. Devuelve la fila como quedó (modificado_en ya puesto).';
COMMENT ON PROCEDURE catedras.sp_actualizar_parametro IS
    'Retrofit criterio 3. Reemplazo completo; refresca modificado_en. PATCH mezcla antes de llamar.';
COMMENT ON PROCEDURE catedras.sp_eliminar_parametro IS
    'Retrofit criterio 3. Borrado FÍSICO: la tabla no tiene activo.';
