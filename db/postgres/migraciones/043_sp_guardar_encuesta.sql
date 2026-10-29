-- ==========================================================================
--  db/postgres/migraciones/043_sp_guardar_encuesta.sql
--  Proyecto Cátedras · Maestro-detalle en el formulario — Guía IA, Regla 1
-- --------------------------------------------------------------------------
--  Maestro: 'encuesta'. Detalle (opcional): 'pregunta' (#6 de la Regla 4),
--  y cada pregunta con sus 'opcion_pregunta' (#7): DOS niveles.
--
--  POST /api/Encuesta puede traer la encuesta JUNTO con sus preguntas, y
--  cada pregunta con sus opciones. Todo se guarda en UNA sola transacción:
--  si cualquier pregunta u opción falla ('orden' repetido -> uq_pregunta_ord
--  o uq_opcion_orden, tipo de pregunta inexistente, rango inválido ->
--  chk_pregunta_rango), se revierte TODO el CALL y ni la encuesta queda
--  creada (ACID · A).
--
--  REUTILIZA, no reescribe: cada pregunta (con sus opciones) se guarda
--  llamando a 'sp_guardar_pregunta' (migración 004, v6) — el maestro-detalle
--  de segundo nivel ya existía. Un procedimiento que llama a otro dentro de
--  la MISMA transacción: si el de adentro falla, falla el de afuera.
--
--  Formato de 'p_preguntas':
--    [{"enunciado":"¿...?", "orden":1, "fk_tipo_pregunta":"OPCION_UNICA",
--      "obligatoria":true, "valor_min":null, "valor_max":null,
--      "opciones":[{"etiqueta":"Sí","valor":1,"orden":1}, ...]}, ...]
--  'fk_encuesta' NO viene en el JSON: es siempre la encuesta recién creada.
--
--  CÓMO SE APLICA: docker-compose.yml lo monta como 'z043_' (después de
--  'z004_' y 'z015_'). Con una BD ya creada, a mano con psql.
-- ==========================================================================

SET search_path TO catedras, public;

CREATE OR REPLACE PROCEDURE catedras.sp_guardar_encuesta(
    -- Columnas propias del maestro (mismos tipos que sp_crear_encuesta).
    IN    p_nombre        character varying(120),
    IN    p_version       smallint DEFAULT 1,
    IN    p_vigente_desde date     DEFAULT CURRENT_DATE,
    IN    p_vigente_hasta date     DEFAULT NULL,
    IN    p_activa        boolean  DEFAULT true,
    -- Detalle: arreglo JSON opcional. NULL o '[]' = encuesta sin preguntas.
    IN    p_preguntas     jsonb    DEFAULT NULL,
    -- Salida: el id que la BD le asignó a la encuesta.
    INOUT p_id_encuesta   bigint   DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_pregunta    jsonb;
    v_opciones    jsonb;
    v_id_pregunta bigint;
BEGIN
    -- 1) Maestro.
    INSERT INTO encuesta (nombre, version, vigente_desde, vigente_hasta, activa)
    VALUES (p_nombre, COALESCE(p_version, 1), COALESCE(p_vigente_desde, CURRENT_DATE),
            p_vigente_hasta, COALESCE(p_activa, true))
    RETURNING id_encuesta INTO p_id_encuesta;

    -- 2) Detalle: cada pregunta, con sus opciones, por sp_guardar_pregunta.
    IF p_preguntas IS NOT NULL AND jsonb_typeof(p_preguntas) = 'array' THEN
        FOR v_pregunta IN
            SELECT e FROM jsonb_array_elements(p_preguntas) WITH ORDINALITY AS t(e, n) ORDER BY n
        LOOP
            -- "opciones": null en el JSON es un jsonb 'null', no un NULL de
            -- SQL: se normaliza para que sp_guardar_pregunta lo trate como
            -- "sin opciones".
            v_opciones := v_pregunta -> 'opciones';
            IF v_opciones IS NOT NULL AND jsonb_typeof(v_opciones) <> 'array' THEN
                v_opciones := NULL;
            END IF;

            v_id_pregunta := NULL;
            CALL catedras.sp_guardar_pregunta(
                p_id_encuesta,
                v_pregunta ->> 'enunciado',
                (v_pregunta ->> 'orden')::smallint,
                v_pregunta ->> 'fk_tipo_pregunta',
                COALESCE((v_pregunta ->> 'obligatoria')::boolean, false),
                (v_pregunta ->> 'valor_min')::smallint,
                (v_pregunta ->> 'valor_max')::smallint,
                v_opciones,
                v_id_pregunta);
        END LOOP;
    END IF;
END;
$$;

COMMENT ON PROCEDURE catedras.sp_guardar_encuesta IS
    'Guía IA · Regla 1. Crea una encuesta y, opcionalmente, sus preguntas (cada una con '
    'sus opciones), en una sola transacción. Reutiliza sp_guardar_pregunta. '
    'Devuelve el id en p_id_encuesta.';
