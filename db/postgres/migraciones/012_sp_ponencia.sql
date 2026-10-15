-- ==========================================================================
--  db/postgres/migraciones/012_sp_ponencia.sql
--  Proyecto Cátedras · RETROFIT criterio 3 de VERSION_2.md · 'ponencia'
-- --------------------------------------------------------------------------
--  LLAVE COMPUESTA: la clave primaria es (fk_sesion, fk_asistente, rol) y
--  la tabla NO tiene id propio. Por eso 'sp_consultar_ponencia',
--  'sp_actualizar_ponencia' y 'sp_eliminar_ponencia' reciben los TRES
--  valores, no un solo id.
--
--  'ponencia' NO tiene columna 'activo' -> 'sp_eliminar_ponencia' es FÍSICO
--  (DELETE) y 'sp_listar_ponencia' devuelve todas las filas.
--
--  'sp_actualizar_ponencia': las tres columnas de la tabla SON la llave, así
--  que "reemplazar todas las columnas" con los mismos tres valores no
--  cambiaría nada. Lo único que tiene sentido cambiar en una ponencia
--  existente es el ROL (p. ej. de PONENTE a MODERADOR); por eso, además de
--  los 3 valores de la llave, recibe 'p_rol_nuevo'. Mover una ponencia a
--  otra sesión o a otro asistente es eliminar + crear. Este procedimiento
--  NO tiene llamador en C#: IRepositorioPonencia no tiene ActualizarAsync
--  (PLAN_RETROFIT_SP.md, Grupo B: "Sin Actualizar").
-- ==========================================================================

SET search_path TO catedras, public;

-- LISTAR — todas, con filtros OPCIONALES por sesión y por asistente
-- (NULL = sin filtro) y límite OPCIONAL.
CREATE OR REPLACE PROCEDURE catedras.sp_listar_ponencia(
    IN    p_fk_sesion    bigint  DEFAULT NULL,
    IN    p_fk_asistente bigint  DEFAULT NULL,
    IN    p_limite       integer DEFAULT NULL,
    INOUT p_resultado    json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(p), '[]'::json)
      INTO p_resultado
      FROM (SELECT fk_sesion, fk_asistente, rol
              FROM ponencia
             WHERE (p_fk_sesion    IS NULL OR fk_sesion    = p_fk_sesion)
               AND (p_fk_asistente IS NULL OR fk_asistente = p_fk_asistente)
             ORDER BY fk_sesion, fk_asistente, rol
             LIMIT p_limite) p;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_consultar_ponencia(
    IN    p_fk_sesion    bigint,
    IN    p_fk_asistente bigint,
    IN    p_rol          character varying(15),
    INOUT p_resultado    json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(p) INTO p_resultado
      FROM (SELECT fk_sesion, fk_asistente, rol
              FROM ponencia
             WHERE fk_sesion    = p_fk_sesion
               AND fk_asistente = p_fk_asistente
               AND rol          = p_rol) p;
END;
$$;

-- Llave repetida (pk_ponencia), rol fuera de la lista (chk_ponencia_rol),
-- sesión o asistente inexistentes -> NpgsqlException.
CREATE OR REPLACE PROCEDURE catedras.sp_crear_ponencia(
    IN    p_fk_sesion    bigint,
    IN    p_fk_asistente bigint,
    IN    p_rol          character varying(15) DEFAULT 'PONENTE',
    INOUT p_resultado    json                  DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    INSERT INTO ponencia (fk_sesion, fk_asistente, rol)
    VALUES (p_fk_sesion, p_fk_asistente, COALESCE(p_rol, 'PONENTE'));

    SELECT row_to_json(p) INTO p_resultado
      FROM (SELECT fk_sesion, fk_asistente, rol
              FROM ponencia
             WHERE fk_sesion    = p_fk_sesion
               AND fk_asistente = p_fk_asistente
               AND rol          = COALESCE(p_rol, 'PONENTE')) p;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_ponencia(
    IN    p_fk_sesion    bigint,
    IN    p_fk_asistente bigint,
    IN    p_rol          character varying(15),
    IN    p_rol_nuevo    character varying(15),
    INOUT p_resultado    json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE ponencia
       SET rol = p_rol_nuevo
     WHERE fk_sesion    = p_fk_sesion
       AND fk_asistente = p_fk_asistente
       AND rol          = p_rol;

    IF FOUND THEN
        SELECT row_to_json(p) INTO p_resultado
          FROM (SELECT fk_sesion, fk_asistente, rol
                  FROM ponencia
                 WHERE fk_sesion    = p_fk_sesion
                   AND fk_asistente = p_fk_asistente
                   AND rol          = p_rol_nuevo) p;
    END IF;
END;
$$;

-- FÍSICO: la tabla no tiene 'activo'.
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_ponencia(
    IN    p_fk_sesion    bigint,
    IN    p_fk_asistente bigint,
    IN    p_rol          character varying(15),
    INOUT p_resultado    json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    DELETE FROM ponencia
     WHERE fk_sesion    = p_fk_sesion
       AND fk_asistente = p_fk_asistente
       AND rol          = p_rol;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_ponencia IS
    'Retrofit criterio 3 (VERSION_2.md §4). Todas (sin activo), filtros opcionales por sesión/asistente y límite opcional.';
COMMENT ON PROCEDURE catedras.sp_consultar_ponencia IS
    'Retrofit criterio 3. Llave compuesta (sesión, asistente, rol). NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_ponencia IS
    'Retrofit criterio 3. Devuelve la fila como quedó guardada.';
COMMENT ON PROCEDURE catedras.sp_actualizar_ponencia IS
    'Retrofit criterio 3. Llave (sesión, asistente, rol) + rol nuevo: lo único que se puede cambiar sin cambiar la llave de otra forma. Sin llamador en C#.';
COMMENT ON PROCEDURE catedras.sp_eliminar_ponencia IS
    'Retrofit criterio 3. Borrado FÍSICO: la tabla no tiene activo. Llave compuesta.';
