-- ==========================================================================
--  db/postgres/migraciones/013_sp_tipo_pregunta.sql
--  Proyecto Cátedras · RETROFIT criterio 3 de VERSION_2.md (Fase 2 de
--  PLAN_RETROFIT_SP.md): los 5 verbos de 'tipo_pregunta' por procedimiento
--  almacenado, con el MISMO molde de 005_sp_sede_ejemplo.sql.
--
--  'tipo_pregunta' NO tiene columna 'activo' -> 'sp_eliminar_tipo_pregunta'
--  es un DELETE físico y 'sp_listar_tipo_pregunta' devuelve TODAS las
--  filas. Un tipo usado por una pregunta hace fallar el DELETE por FK.
--  chk_tipo_pregunta_destino (exactamente un 'guarda_*' en true) lo sigue
--  vigilando la tabla; el servicio ya lo valida antes con 400.
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  LISTAR — todas las filas (no hay 'activo'), con límite OPCIONAL.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_listar_tipo_pregunta(
    IN    p_limite           integer DEFAULT NULL,
    INOUT p_resultado        json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(t), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_tipo_pregunta, nombre, guarda_numero, guarda_texto, guarda_opcion
              FROM tipo_pregunta
             ORDER BY id_tipo_pregunta
             LIMIT p_limite) t;
END;
$$;

-- --------------------------------------------------------------------------
--  CONSULTAR — una fila por id. NULL en p_resultado = no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_consultar_tipo_pregunta(
    IN    p_id_tipo_pregunta character varying(15),
    INOUT p_resultado        json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(t) INTO p_resultado
      FROM (SELECT id_tipo_pregunta, nombre, guarda_numero, guarda_texto, guarda_opcion
              FROM tipo_pregunta
             WHERE id_tipo_pregunta = p_id_tipo_pregunta) t;
END;
$$;

-- --------------------------------------------------------------------------
--  CREAR — devuelve la fila como quedó guardada. Id repetido o un CHECK
--  violado -> NpgsqlException (el controlador la traduce, criterio 7).
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_crear_tipo_pregunta(
    IN    p_id_tipo_pregunta character varying(15),
    IN    p_nombre           character varying(60),
    IN    p_guarda_numero    boolean DEFAULT false,
    IN    p_guarda_texto     boolean DEFAULT false,
    IN    p_guarda_opcion    boolean DEFAULT false,
    INOUT p_resultado        json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    INSERT INTO tipo_pregunta (id_tipo_pregunta, nombre, guarda_numero, guarda_texto, guarda_opcion)
    VALUES (p_id_tipo_pregunta, p_nombre, p_guarda_numero, p_guarda_texto, p_guarda_opcion);

    SELECT row_to_json(t) INTO p_resultado
      FROM (SELECT id_tipo_pregunta, nombre, guarda_numero, guarda_texto, guarda_opcion
              FROM tipo_pregunta
             WHERE id_tipo_pregunta = p_id_tipo_pregunta) t;
END;
$$;

-- --------------------------------------------------------------------------
--  ACTUALIZAR — reemplazo COMPLETO (PUT directo; PATCH llega ya mezclado
--  desde el repositorio). Sin FOUND -> p_resultado NULL = no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_tipo_pregunta(
    IN    p_id_tipo_pregunta character varying(15),
    IN    p_nombre           character varying(60),
    IN    p_guarda_numero    boolean,
    IN    p_guarda_texto     boolean,
    IN    p_guarda_opcion    boolean,
    INOUT p_resultado        json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE tipo_pregunta
       SET nombre        = p_nombre,
           guarda_numero = p_guarda_numero,
           guarda_texto  = p_guarda_texto,
           guarda_opcion = p_guarda_opcion
     WHERE id_tipo_pregunta = p_id_tipo_pregunta;

    IF FOUND THEN
        SELECT row_to_json(t) INTO p_resultado
          FROM (SELECT id_tipo_pregunta, nombre, guarda_numero, guarda_texto, guarda_opcion
                  FROM tipo_pregunta
                 WHERE id_tipo_pregunta = p_id_tipo_pregunta) t;
    END IF;
END;
$$;

-- --------------------------------------------------------------------------
--  ELIMINAR — FÍSICO (la tabla NO tiene 'activo'). FK en uso -> excepción.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_tipo_pregunta(
    IN    p_id_tipo_pregunta character varying(15),
    INOUT p_resultado        json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    DELETE FROM tipo_pregunta
     WHERE id_tipo_pregunta = p_id_tipo_pregunta;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_tipo_pregunta IS
    'Retrofit criterio 3 (VERSION_2.md §4). Todas las filas, con límite opcional.';
COMMENT ON PROCEDURE catedras.sp_consultar_tipo_pregunta IS
    'Retrofit criterio 3. NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_tipo_pregunta IS
    'Retrofit criterio 3. Devuelve la fila como quedó guardada.';
COMMENT ON PROCEDURE catedras.sp_actualizar_tipo_pregunta IS
    'Retrofit criterio 3. Reemplazo completo; PATCH mezcla antes de llamar.';
COMMENT ON PROCEDURE catedras.sp_eliminar_tipo_pregunta IS
    'Retrofit criterio 3. Borrado FÍSICO (DELETE): la tabla no tiene activo.';
