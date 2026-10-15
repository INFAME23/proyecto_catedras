-- ==========================================================================
--  db/postgres/migraciones/004_sp_guardar_pregunta.sql
--  Proyecto Cátedras · Versión 6 — Guía IA, Regla 1 (maestro-detalle)
-- --------------------------------------------------------------------------
--  Maestro: 'pregunta'. Detalle (opcional): 'opcion_pregunta'.
--
--  POST /api/Pregunta puede traer la pregunta JUNTO con sus opciones
--  iniciales (si es de tipo OPCION_UNICA). Este SP las guarda en UNA sola
--  transacción: si cualquier INSERT de 'opcion_pregunta' falla (orden
--  repetido para la misma pregunta -> uq_opcion_orden), PostgreSQL revierte
--  TODO el CALL y ni la pregunta queda creada (ACID · A: todo o nada) —
--  mismo criterio que sp_guardar_programa_academico (v2) y
--  sp_guardar_asistente (v3). Ver version6/3_plan.md §4.1 y ADR D30.
--
--  A diferencia de 'respuesta_encuesta'/'respuesta_item' (que ya tenían su
--  función desde la v1: fn_responder_encuesta_api), esta pareja SÍ necesita
--  un SP nuevo: ninguna versión anterior lo había escrito.
--
--  Estilo: el mismo de sp_guardar_asistente (v3): parámetro jsonb para el
--  detalle y un parámetro INOUT que devuelve el id generado.
--
--  CÓMO SE APLICA (ver version6/7_quickstart.md):
--   · BD nueva: docker-compose.yml lo monta en /docker-entrypoint-initdb.d/
--     con el prefijo 'z004_', para que corra DESPUÉS de
--     'script_bdcatedras.sql' y de 'z001_'-'z003_' (initdb ejecuta los
--     archivos en orden ALFABÉTICO).
--   · BD que ya existe: se aplica a mano con psql. Por eso es
--     'CREATE OR REPLACE': se puede ejecutar varias veces sin error.
-- ==========================================================================

SET search_path TO catedras, public;

CREATE OR REPLACE PROCEDURE catedras.sp_guardar_pregunta(
    -- Columnas propias del maestro 'pregunta'. Los tamaños son IGUALES a
    -- los de la tabla (version6/5_data_model.md §2.1).
    IN p_fk_encuesta      bigint,
    IN p_enunciado        character varying(300),
    IN p_orden            smallint,
    IN p_fk_tipo_pregunta character varying(15),
    IN p_obligatoria      boolean  DEFAULT false,
    IN p_valor_min        smallint DEFAULT NULL,
    IN p_valor_max        smallint DEFAULT NULL,
    -- Detalle: arreglo JSON opcional con las opciones iniciales.
    -- NULL o '[]' = pregunta sin opciones (p. ej. ESCALA_5 o TEXTO_LIBRE).
    -- Formato: [{"etiqueta":"Si","valor":1,"orden":1}, {"etiqueta":"No","valor":0,"orden":2}]
    IN p_opciones         jsonb    DEFAULT NULL,
    -- Salida: el id que la BD le asignó a la pregunta nueva.
    INOUT p_id_pregunta   bigint   DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    -- 1) Maestro. '(fk_encuesta, orden)' duplicado (uq_pregunta_ord) o un
    --    CHECK que no se cumple (chk_pregunta_ord, chk_pregunta_rango) ->
    --    la excepción del motor sube tal cual (el controlador responde
    --    500). RETURNING ... INTO guarda el id generado en el parámetro
    --    de salida, igual que sp_guardar_asistente (v3).
    INSERT INTO pregunta (fk_encuesta, enunciado, orden, fk_tipo_pregunta,
                          obligatoria, valor_min, valor_max)
    VALUES (p_fk_encuesta, p_enunciado, p_orden, p_fk_tipo_pregunta,
            p_obligatoria, p_valor_min, p_valor_max)
    RETURNING id_pregunta INTO p_id_pregunta;

    -- 2) Opciones iniciales. 'fk_pregunta' NO viene en el JSON: es siempre
    --    la pregunta que se acaba de crear en el paso 1 (igual criterio
    --    que 'fk_programa' en sp_guardar_programa_academico, v2).
    --    Un 'orden' repetido entre dos opciones de la misma pregunta
    --    (uq_opcion_orden) revierte el CALL completo: ni la pregunta del
    --    paso 1 queda creada.
    IF p_opciones IS NOT NULL AND jsonb_array_length(p_opciones) > 0 THEN
        INSERT INTO opcion_pregunta (fk_pregunta, etiqueta, valor, orden)
        SELECT p_id_pregunta,
               e ->> 'etiqueta',
               (e ->> 'valor')::smallint,
               (e ->> 'orden')::smallint
          FROM jsonb_array_elements(p_opciones) AS e;
    END IF;

    -- Si el paso 2 falla, PostgreSQL revierte TODO el CALL: la pregunta
    -- del paso 1 tampoco queda creada.
END;
$$;

COMMENT ON PROCEDURE catedras.sp_guardar_pregunta IS
    'Guía IA · Regla 1. Crea una pregunta y, opcionalmente, sus opciones '
    'de respuesta iniciales, en una sola transacción (V6, '
    'version6/3_plan.md §4.1). Devuelve el id en p_id_pregunta.';
