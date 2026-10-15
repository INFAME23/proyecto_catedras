-- ==========================================================================
--  db/postgres/migraciones/040_sp_respuesta_encuesta.sql
--  Proyecto Cátedras · RETROFIT criterio 3 (VERSION_2.md §4) · Fase 6
--  Molde: 005_sp_sede_ejemplo.sql (ver PLAN_RETROFIT_SP.md).
--
--  'respuesta_encuesta' NO tiene 'activo' -> 'sp_eliminar_' es FÍSICO.
--
--  CREAR (Responder) sigue siendo fn_responder_encuesta_api ->
--  sp_responder_encuesta -> sp_cerrar_encuesta (script provisto, ADR D30,
--  D31): maestro + todos los ítems + cierre en una sola transacción. NO
--  se reemplaza ni se duplica.
--
--  ⚠️ HECHO AUDITABLE (ADR D31/D32): una respuesta cerrada
--  (completa = true) es una encuesta ya entregada. En el flujo normal TODA
--  respuesta queda cerrada en el mismo CALL que la crea (5_data_model.md:
--  "un false persistido no ocurre en el flujo normal"). Por eso:
--   · Listar y Consultar: los usa la API.
--   · Actualizar y Eliminar: EXISTEN para cumplir el criterio al pie de la
--     letra, NO están en IRepositorioRespuestaEncuesta ni en el
--     controlador, y se NIEGAN (RAISE) sobre una respuesta cerrada. En la
--     práctica solo podrían tocar una respuesta abierta, que no existe en
--     el flujo normal: la guarda deja la regla "encuesta cerrada" en la BD
--     aunque alguien haga el CALL a mano.
--
--  AJUSTE AL MOLDE DE 'sede': guarda de "cerrada" antes de UPDATE/DELETE.
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  LISTAR — filtro opcional por encuesta.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_listar_respuesta_encuesta(
    IN    p_fk_encuesta bigint  DEFAULT NULL,
    IN    p_limite      integer DEFAULT NULL,
    INOUT p_resultado   json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(r), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_respuesta, fk_registro, fk_encuesta, respondida_en, completa
              FROM respuesta_encuesta
             WHERE p_fk_encuesta IS NULL OR fk_encuesta = p_fk_encuesta
             ORDER BY id_respuesta
             LIMIT p_limite) r;
END;
$$;

-- --------------------------------------------------------------------------
--  CONSULTAR — por id. NULL = no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_consultar_respuesta_encuesta(
    IN    p_id_respuesta bigint,
    INOUT p_resultado    json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(r) INTO p_resultado
      FROM (SELECT id_respuesta, fk_registro, fk_encuesta, respondida_en, completa
              FROM respuesta_encuesta
             WHERE id_respuesta = p_id_respuesta) r;
END;
$$;

-- --------------------------------------------------------------------------
--  ACTUALIZAR — firma completa (molde). Se niega si la respuesta está
--  cerrada. NO expuesto por la API.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_respuesta_encuesta(
    IN    p_id_respuesta  bigint,
    IN    p_fk_registro   bigint,
    IN    p_fk_encuesta   bigint,
    IN    p_respondida_en timestamp with time zone,
    IN    p_completa      boolean,
    INOUT p_resultado     json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_completa boolean;
BEGIN
    SELECT completa INTO v_completa
      FROM respuesta_encuesta
     WHERE id_respuesta = p_id_respuesta
       FOR UPDATE;

    IF NOT FOUND THEN
        RETURN;   -- p_resultado NULL = no existe
    END IF;

    IF v_completa THEN
        RAISE EXCEPTION
            'D31: la respuesta % ya está cerrada (hecho auditable): no se modifica',
            p_id_respuesta USING ERRCODE = 'check_violation';
    END IF;

    UPDATE respuesta_encuesta
       SET fk_registro   = p_fk_registro,
           fk_encuesta   = p_fk_encuesta,
           respondida_en = p_respondida_en,
           completa      = p_completa
     WHERE id_respuesta = p_id_respuesta;

    SELECT row_to_json(r) INTO p_resultado
      FROM (SELECT id_respuesta, fk_registro, fk_encuesta, respondida_en, completa
              FROM respuesta_encuesta
             WHERE id_respuesta = p_id_respuesta) r;
END;
$$;

-- --------------------------------------------------------------------------
--  ELIMINAR — FÍSICO, se niega si está cerrada. NO expuesto por la API.
--  (respuesta_item.fk_respuesta es CASCADE: borraría también sus ítems.)
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_respuesta_encuesta(
    IN    p_id_respuesta bigint,
    INOUT p_resultado    json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    IF EXISTS (SELECT 1 FROM respuesta_encuesta
                WHERE id_respuesta = p_id_respuesta AND completa) THEN
        RAISE EXCEPTION
            'D31: la respuesta % ya está cerrada (hecho auditable): no se elimina',
            p_id_respuesta USING ERRCODE = 'check_violation';
    END IF;

    DELETE FROM respuesta_encuesta WHERE id_respuesta = p_id_respuesta;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_respuesta_encuesta IS
    'Retrofit criterio 3. Filtro opcional por encuesta. Crear = fn_responder_encuesta_api.';
COMMENT ON PROCEDURE catedras.sp_consultar_respuesta_encuesta IS
    'Retrofit criterio 3. NULL = no existe.';
COMMENT ON PROCEDURE catedras.sp_actualizar_respuesta_encuesta IS
    'Retrofit criterio 3. NO expuesto. Se niega sobre una respuesta cerrada (D31).';
COMMENT ON PROCEDURE catedras.sp_eliminar_respuesta_encuesta IS
    'Retrofit criterio 3. NO expuesto. FÍSICO; se niega sobre una respuesta cerrada (D31).';
