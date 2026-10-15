-- ==========================================================================
--  db/postgres/migraciones/039_sp_opcion_pregunta.sql
--  Proyecto Cátedras · RETROFIT criterio 3 (VERSION_2.md §4) · Fase 6
--  Molde: 005_sp_sede_ejemplo.sql (ver PLAN_RETROFIT_SP.md).
--
--  'opcion_pregunta' NO tiene 'activo' -> 'sp_eliminar_' es FÍSICO.
--  CRUD estándar, el más parecido a 'sede' de este lote.
--
--  AJUSTES AL MOLDE DE 'sede':
--   1) id_opcion lo genera la BD (IDENTITY): crear usa un v_id intermedio
--      (RETURNING) y devuelve la fila con ese id.
--   2) Orden de parámetros de crear: los obligatorios primero
--      (fk_pregunta, etiqueta, orden) y 'valor' al final con DEFAULT NULL
--      (PostgreSQL exige que los parámetros con DEFAULT vayan al final).
--   3) 'sp_actualizar_' NO cambia 'fk_pregunta' (tampoco lo dejaba la
--      lista blanca del repositorio): mover una opción a otra pregunta
--      rompería RN-24 en respuestas ya dadas.
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  LISTAR — filtro opcional por pregunta. Orden: pregunta, orden.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_listar_opcion_pregunta(
    IN    p_fk_pregunta bigint  DEFAULT NULL,
    IN    p_limite      integer DEFAULT NULL,
    INOUT p_resultado   json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(o), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_opcion, fk_pregunta, etiqueta, valor, orden
              FROM opcion_pregunta
             WHERE p_fk_pregunta IS NULL OR fk_pregunta = p_fk_pregunta
             ORDER BY fk_pregunta, orden
             LIMIT p_limite) o;
END;
$$;

-- --------------------------------------------------------------------------
--  CONSULTAR — por id. NULL = no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_consultar_opcion_pregunta(
    IN    p_id_opcion bigint,
    INOUT p_resultado json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(o) INTO p_resultado
      FROM (SELECT id_opcion, fk_pregunta, etiqueta, valor, orden
              FROM opcion_pregunta
             WHERE id_opcion = p_id_opcion) o;
END;
$$;

-- --------------------------------------------------------------------------
--  CREAR — (fk_pregunta, orden) duplicado (uq_opcion_orden) o pregunta
--  inexistente -> NpgsqlException.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_crear_opcion_pregunta(
    IN    p_fk_pregunta bigint,
    IN    p_etiqueta    character varying(150),
    IN    p_orden       smallint,
    IN    p_valor       smallint DEFAULT NULL,
    INOUT p_resultado   json     DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_id bigint;
BEGIN
    INSERT INTO opcion_pregunta (fk_pregunta, etiqueta, valor, orden)
    VALUES (p_fk_pregunta, p_etiqueta, p_valor, p_orden)
    RETURNING id_opcion INTO v_id;

    SELECT row_to_json(o) INTO p_resultado
      FROM (SELECT id_opcion, fk_pregunta, etiqueta, valor, orden
              FROM opcion_pregunta
             WHERE id_opcion = v_id) o;
END;
$$;

-- --------------------------------------------------------------------------
--  ACTUALIZAR — reemplazo completo de etiqueta, valor y orden.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_opcion_pregunta(
    IN    p_id_opcion bigint,
    IN    p_etiqueta  character varying(150),
    IN    p_valor     smallint,
    IN    p_orden     smallint,
    INOUT p_resultado json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE opcion_pregunta
       SET etiqueta = p_etiqueta,
           valor    = p_valor,
           orden    = p_orden
     WHERE id_opcion = p_id_opcion;

    IF FOUND THEN
        SELECT row_to_json(o) INTO p_resultado
          FROM (SELECT id_opcion, fk_pregunta, etiqueta, valor, orden
                  FROM opcion_pregunta
                 WHERE id_opcion = p_id_opcion) o;
    END IF;
END;
$$;

-- --------------------------------------------------------------------------
--  ELIMINAR — FÍSICO. Una opción ya elegida en alguna respuesta no se
--  puede borrar (respuesta_item.fk_opcion es RESTRICT).
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_opcion_pregunta(
    IN    p_id_opcion bigint,
    INOUT p_resultado json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    DELETE FROM opcion_pregunta WHERE id_opcion = p_id_opcion;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_opcion_pregunta IS
    'Retrofit criterio 3. Filtro opcional por pregunta.';
COMMENT ON PROCEDURE catedras.sp_consultar_opcion_pregunta IS
    'Retrofit criterio 3. NULL = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_opcion_pregunta IS
    'Retrofit criterio 3. Devuelve la fila como quedó guardada (con id_opcion).';
COMMENT ON PROCEDURE catedras.sp_actualizar_opcion_pregunta IS
    'Retrofit criterio 3. Reemplazo completo (sin fk_pregunta); PATCH mezcla antes de llamar.';
COMMENT ON PROCEDURE catedras.sp_eliminar_opcion_pregunta IS
    'Retrofit criterio 3. Borrado FÍSICO; RESTRICT si ya fue elegida en una respuesta.';
