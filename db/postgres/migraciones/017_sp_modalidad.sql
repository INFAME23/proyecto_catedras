-- ==========================================================================
--  db/postgres/migraciones/017_sp_modalidad.sql
--  Proyecto Cátedras · RETROFIT criterio 3 de VERSION_2.md (Fase 2 de
--  PLAN_RETROFIT_SP.md): los 5 verbos de 'modalidad' por procedimiento
--  almacenado, con el MISMO molde de 005_sp_sede_ejemplo.sql.
--
--  'modalidad' SÍ tiene columna 'activo' -> 'sp_eliminar_modalidad' es
--  LÓGICO (UPDATE ... SET activo = false) y 'sp_listar_modalidad' devuelve
--  solo las activas, igual que sede.
--
--  AJUSTE respecto a sede: 'sp_crear_modalidad' recibe 'p_activo', porque
--  el POST de modalidad SÍ admite 'activo' y el INSERT anterior lo guardaba.
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  LISTAR — solo activas, con límite OPCIONAL (NULL = sin límite).
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_listar_modalidad(
    IN    p_limite    integer DEFAULT NULL,
    INOUT p_resultado json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(m), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_modalidad, nombre, canal_difusion, activo
              FROM modalidad
             WHERE activo
             ORDER BY id_modalidad
             LIMIT p_limite) m;
END;
$$;

-- --------------------------------------------------------------------------
--  CONSULTAR — una modalidad por id. NULL en p_resultado = no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_consultar_modalidad(
    IN    p_id_modalidad character varying(15),
    INOUT p_resultado    json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(m) INTO p_resultado
      FROM (SELECT id_modalidad, nombre, canal_difusion, activo
              FROM modalidad
             WHERE id_modalidad = p_id_modalidad) m;
END;
$$;

-- --------------------------------------------------------------------------
--  CREAR — devuelve la fila como quedó guardada. Canal fuera de
--  QR/ENLACE/AMBOS (chk_modalidad_canal) o id repetido -> NpgsqlException.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_crear_modalidad(
    IN    p_id_modalidad   character varying(15),
    IN    p_nombre         character varying(60),
    IN    p_canal_difusion character varying(10),
    IN    p_activo         boolean DEFAULT true,
    INOUT p_resultado      json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    INSERT INTO modalidad (id_modalidad, nombre, canal_difusion, activo)
    VALUES (p_id_modalidad, p_nombre, p_canal_difusion, p_activo);

    SELECT row_to_json(m) INTO p_resultado
      FROM (SELECT id_modalidad, nombre, canal_difusion, activo
              FROM modalidad
             WHERE id_modalidad = p_id_modalidad) m;
END;
$$;

-- --------------------------------------------------------------------------
--  ACTUALIZAR — reemplazo COMPLETO (PUT directo; PATCH llega ya mezclado
--  desde el repositorio). Sin FOUND -> p_resultado NULL = no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_modalidad(
    IN    p_id_modalidad   character varying(15),
    IN    p_nombre         character varying(60),
    IN    p_canal_difusion character varying(10),
    IN    p_activo         boolean,
    INOUT p_resultado      json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE modalidad
       SET nombre         = p_nombre,
           canal_difusion = p_canal_difusion,
           activo         = p_activo
     WHERE id_modalidad = p_id_modalidad;

    IF FOUND THEN
        SELECT row_to_json(m) INTO p_resultado
          FROM (SELECT id_modalidad, nombre, canal_difusion, activo
                  FROM modalidad
                 WHERE id_modalidad = p_id_modalidad) m;
    END IF;
END;
$$;

-- --------------------------------------------------------------------------
--  ELIMINAR — LÓGICO (la tabla tiene 'activo'). Ya inactiva -> no cuenta.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_modalidad(
    IN    p_id_modalidad character varying(15),
    INOUT p_resultado    json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE modalidad
       SET activo = false
     WHERE id_modalidad = p_id_modalidad
       AND activo;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_modalidad IS
    'Retrofit criterio 3 (VERSION_2.md §4). Solo activas, con límite opcional.';
COMMENT ON PROCEDURE catedras.sp_consultar_modalidad IS
    'Retrofit criterio 3. NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_modalidad IS
    'Retrofit criterio 3. Devuelve la fila como quedó guardada.';
COMMENT ON PROCEDURE catedras.sp_actualizar_modalidad IS
    'Retrofit criterio 3. Reemplazo completo; PATCH mezcla antes de llamar.';
COMMENT ON PROCEDURE catedras.sp_eliminar_modalidad IS
    'Retrofit criterio 3. Borrado LÓGICO (activo=false): la tabla tiene activo.';
