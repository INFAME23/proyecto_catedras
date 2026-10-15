-- ==========================================================================
--  db/postgres/migraciones/038_sp_pregunta.sql
--  Proyecto Cátedras · RETROFIT criterio 3 (VERSION_2.md §4) · Fase 6
--  Molde: 005_sp_sede_ejemplo.sql (ver PLAN_RETROFIT_SP.md).
--
--  'pregunta' NO tiene 'activo' -> 'sp_eliminar_' es FÍSICO.
--
--  CREAR sigue siendo 'sp_guardar_pregunta' (004): maestro + opciones
--  iniciales en una sola transacción. NO se reemplaza ni se duplica con un
--  'sp_crear_pregunta'; este archivo solo completa Listar, Consultar,
--  Actualizar y Eliminar.
--
--  AJUSTES AL MOLDE DE 'sede':
--   1) 'sp_actualizar_pregunta' reemplaza las columnas que la API deja
--      cambiar (enunciado, orden, fk_tipo_pregunta, obligatoria,
--      valor_min, valor_max). 'fk_encuesta' NO: mover una pregunta de
--      encuesta no estaba permitido antes (lista blanca del repositorio)
--      y se mantiene así.
--   2) Eliminar: respuesta_item.fk_pregunta es RESTRICT y
--      opcion_pregunta.fk_pregunta es CASCADE. Si hay respuestas, el DELETE
--      completo falla y la cascada a las opciones no ocurre (ACID · A).
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  LISTAR — filtro opcional por encuesta. Orden: encuesta, orden.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_listar_pregunta(
    IN    p_fk_encuesta bigint  DEFAULT NULL,
    IN    p_limite      integer DEFAULT NULL,
    INOUT p_resultado   json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(p), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_pregunta, fk_encuesta, enunciado, orden,
                   fk_tipo_pregunta, obligatoria, valor_min, valor_max
              FROM pregunta
             WHERE p_fk_encuesta IS NULL OR fk_encuesta = p_fk_encuesta
             ORDER BY fk_encuesta, orden
             LIMIT p_limite) p;
END;
$$;

-- --------------------------------------------------------------------------
--  CONSULTAR — por id. NULL = no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_consultar_pregunta(
    IN    p_id_pregunta bigint,
    INOUT p_resultado   json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(p) INTO p_resultado
      FROM (SELECT id_pregunta, fk_encuesta, enunciado, orden,
                   fk_tipo_pregunta, obligatoria, valor_min, valor_max
              FROM pregunta
             WHERE id_pregunta = p_id_pregunta) p;
END;
$$;

-- --------------------------------------------------------------------------
--  ACTUALIZAR — reemplazo completo (PUT directo; PATCH llega mezclado).
--  (fk_encuesta, orden) duplicado, tipo inexistente o rango inválido ->
--  NpgsqlException (uq_pregunta_ord, fk_pregunta_tipo, chk_pregunta_rango).
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_pregunta(
    IN    p_id_pregunta      bigint,
    IN    p_enunciado        character varying(300),
    IN    p_orden            smallint,
    IN    p_fk_tipo_pregunta character varying(15),
    IN    p_obligatoria      boolean,
    IN    p_valor_min        smallint,
    IN    p_valor_max        smallint,
    INOUT p_resultado        json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE pregunta
       SET enunciado        = p_enunciado,
           orden            = p_orden,
           fk_tipo_pregunta = p_fk_tipo_pregunta,
           obligatoria      = p_obligatoria,
           valor_min        = p_valor_min,
           valor_max        = p_valor_max
     WHERE id_pregunta = p_id_pregunta;

    IF FOUND THEN
        SELECT row_to_json(p) INTO p_resultado
          FROM (SELECT id_pregunta, fk_encuesta, enunciado, orden,
                       fk_tipo_pregunta, obligatoria, valor_min, valor_max
                  FROM pregunta
                 WHERE id_pregunta = p_id_pregunta) p;
    END IF;
END;
$$;

-- --------------------------------------------------------------------------
--  ELIMINAR — FÍSICO (sin 'activo'). Con respuestas -> falla (RESTRICT).
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_pregunta(
    IN    p_id_pregunta bigint,
    INOUT p_resultado   json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    DELETE FROM pregunta WHERE id_pregunta = p_id_pregunta;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_pregunta IS
    'Retrofit criterio 3. Filtro opcional por encuesta. Crear = sp_guardar_pregunta (004).';
COMMENT ON PROCEDURE catedras.sp_consultar_pregunta IS
    'Retrofit criterio 3. NULL = no existe.';
COMMENT ON PROCEDURE catedras.sp_actualizar_pregunta IS
    'Retrofit criterio 3. Reemplazo completo (sin fk_encuesta); PATCH mezcla antes de llamar.';
COMMENT ON PROCEDURE catedras.sp_eliminar_pregunta IS
    'Retrofit criterio 3. Borrado FÍSICO; RESTRICT si ya tiene respuestas.';
