-- ==========================================================================
--  db/postgres/migraciones/041_sp_respuesta_item.sql
--  Proyecto Cátedras · RETROFIT criterio 3 (VERSION_2.md §4) · Fase 6
--  Molde: 005_sp_sede_ejemplo.sql (ver PLAN_RETROFIT_SP.md).
--
--  'respuesta_item' NO tiene 'activo' -> 'sp_eliminar_' es FÍSICO.
--
--  ⚠️ HECHO AUDITABLE (ADR D31/D32). Hoy la API es de SOLO LECTURA sobre
--  esta tabla: los ítems nacen dentro de sp_responder_encuesta (vía
--  fn_responder_encuesta_api), en el mismo CALL que cierra la respuesta.
--   · Listar y Consultar: los usa la API.
--   · Crear, Actualizar y Eliminar: EXISTEN para cumplir el criterio al
--     pie de la letra, NO están en IRepositorioRespuestaItem ni en el
--     controlador, y se NIEGAN (RAISE) si la respuesta madre ya está
--     cerrada (completa = true) — es decir, siempre, en el flujo normal.
--     trg_respuesta_item_tipo sigue validando tipo, rango y RN-24 en
--     cualquier INSERT/UPDATE.
--
--  AJUSTE AL MOLDE DE 'sede': guarda de "respuesta madre cerrada" en los
--  tres verbos de escritura.
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  LISTAR — filtro opcional por respuesta. Orden: respuesta, pregunta.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_listar_respuesta_item(
    IN    p_fk_respuesta bigint  DEFAULT NULL,
    IN    p_limite       integer DEFAULT NULL,
    INOUT p_resultado    json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(i), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_item, fk_respuesta, fk_pregunta, valor_numerico,
                   valor_texto, fk_opcion
              FROM respuesta_item
             WHERE p_fk_respuesta IS NULL OR fk_respuesta = p_fk_respuesta
             ORDER BY fk_respuesta, fk_pregunta
             LIMIT p_limite) i;
END;
$$;

-- --------------------------------------------------------------------------
--  CONSULTAR — por id. NULL = no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_consultar_respuesta_item(
    IN    p_id_item   bigint,
    INOUT p_resultado json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(i) INTO p_resultado
      FROM (SELECT id_item, fk_respuesta, fk_pregunta, valor_numerico,
                   valor_texto, fk_opcion
              FROM respuesta_item
             WHERE id_item = p_id_item) i;
END;
$$;

-- --------------------------------------------------------------------------
--  CREAR — solo sobre una respuesta ABIERTA. NO expuesto por la API.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_crear_respuesta_item(
    IN    p_fk_respuesta   bigint,
    IN    p_fk_pregunta    bigint,
    IN    p_valor_numerico smallint DEFAULT NULL,
    IN    p_valor_texto    text     DEFAULT NULL,
    IN    p_fk_opcion      bigint   DEFAULT NULL,
    INOUT p_resultado      json     DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_id bigint;
BEGIN
    IF EXISTS (SELECT 1 FROM respuesta_encuesta
                WHERE id_respuesta = p_fk_respuesta AND completa) THEN
        RAISE EXCEPTION
            'D31: la respuesta % ya está cerrada (hecho auditable): no admite ítems nuevos',
            p_fk_respuesta USING ERRCODE = 'check_violation';
    END IF;

    INSERT INTO respuesta_item (fk_respuesta, fk_pregunta, valor_numerico,
                                valor_texto, fk_opcion)
    VALUES (p_fk_respuesta, p_fk_pregunta, p_valor_numerico,
            p_valor_texto, p_fk_opcion)
    RETURNING id_item INTO v_id;

    SELECT row_to_json(i) INTO p_resultado
      FROM (SELECT id_item, fk_respuesta, fk_pregunta, valor_numerico,
                   valor_texto, fk_opcion
              FROM respuesta_item
             WHERE id_item = v_id) i;
END;
$$;

-- --------------------------------------------------------------------------
--  ACTUALIZAR — firma completa (molde). Se niega si la respuesta madre
--  (la actual o la nueva) está cerrada. NO expuesto por la API.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_respuesta_item(
    IN    p_id_item        bigint,
    IN    p_fk_respuesta   bigint,
    IN    p_fk_pregunta    bigint,
    IN    p_valor_numerico smallint,
    IN    p_valor_texto    text,
    IN    p_fk_opcion      bigint,
    INOUT p_resultado      json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_fk_respuesta bigint;
BEGIN
    SELECT fk_respuesta INTO v_fk_respuesta
      FROM respuesta_item
     WHERE id_item = p_id_item
       FOR UPDATE;

    IF NOT FOUND THEN
        RETURN;   -- p_resultado NULL = no existe
    END IF;

    IF EXISTS (SELECT 1 FROM respuesta_encuesta
                WHERE id_respuesta IN (v_fk_respuesta, p_fk_respuesta)
                  AND completa) THEN
        RAISE EXCEPTION
            'D31: el ítem % pertenece a una respuesta cerrada (hecho auditable): no se modifica',
            p_id_item USING ERRCODE = 'check_violation';
    END IF;

    UPDATE respuesta_item
       SET fk_respuesta   = p_fk_respuesta,
           fk_pregunta    = p_fk_pregunta,
           valor_numerico = p_valor_numerico,
           valor_texto    = p_valor_texto,
           fk_opcion      = p_fk_opcion
     WHERE id_item = p_id_item;

    SELECT row_to_json(i) INTO p_resultado
      FROM (SELECT id_item, fk_respuesta, fk_pregunta, valor_numerico,
                   valor_texto, fk_opcion
              FROM respuesta_item
             WHERE id_item = p_id_item) i;
END;
$$;

-- --------------------------------------------------------------------------
--  ELIMINAR — FÍSICO, se niega si la respuesta madre está cerrada.
--  NO expuesto por la API.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_respuesta_item(
    IN    p_id_item   bigint,
    INOUT p_resultado json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    IF EXISTS (SELECT 1
                 FROM respuesta_item i
                 JOIN respuesta_encuesta r ON r.id_respuesta = i.fk_respuesta
                WHERE i.id_item = p_id_item AND r.completa) THEN
        RAISE EXCEPTION
            'D31: el ítem % pertenece a una respuesta cerrada (hecho auditable): no se elimina',
            p_id_item USING ERRCODE = 'check_violation';
    END IF;

    DELETE FROM respuesta_item WHERE id_item = p_id_item;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_respuesta_item IS
    'Retrofit criterio 3. Filtro opcional por respuesta.';
COMMENT ON PROCEDURE catedras.sp_consultar_respuesta_item IS
    'Retrofit criterio 3. NULL = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_respuesta_item IS
    'Retrofit criterio 3. NO expuesto. Se niega si la respuesta madre está cerrada (D31).';
COMMENT ON PROCEDURE catedras.sp_actualizar_respuesta_item IS
    'Retrofit criterio 3. NO expuesto. Se niega si la respuesta madre está cerrada (D31).';
COMMENT ON PROCEDURE catedras.sp_eliminar_respuesta_item IS
    'Retrofit criterio 3. NO expuesto. Se niega si la respuesta madre está cerrada (D31).';
