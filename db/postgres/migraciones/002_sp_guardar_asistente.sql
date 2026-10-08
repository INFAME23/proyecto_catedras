-- ==========================================================================
--  db/postgres/migraciones/002_sp_guardar_asistente.sql
--  Proyecto Cátedras · Versión 3 — Guía IA, Regla 1 (maestro-detalle)
-- --------------------------------------------------------------------------
--  Maestro: 'asistente'. Detalles (los 4 opcionales):
--    documento_asistente · vinculacion_asistente · programa_asistente ·
--    consentimiento_datos
--
--  POST /api/Asistente puede traer el asistente JUNTO con sus detalles
--  iniciales. Este SP los guarda en UNA sola transacción: si cualquier
--  INSERT falla (documento duplicado, programa inexistente, vinculación
--  repetida...), PostgreSQL revierte TODO el CALL y ni el asistente queda
--  creado (ACID · A: todo o nada). Ver version3/3_plan.md §4.1.
--
--  Estilo: el mismo de sp_guardar_programa_academico (v2) y de
--  sp_responder_encuesta (que ya trae la BD): parámetros jsonb para los
--  detalles y un parámetro INOUT que devuelve el id generado.
--
--  CÓMO SE APLICA (ver version3/7_quickstart.md §1):
--   · BD nueva: docker-compose.yml lo monta en /docker-entrypoint-initdb.d/
--     con el prefijo 'z002_', para que corra DESPUÉS de
--     'script_bdcatedras.sql' (initdb ejecuta los archivos en orden
--     ALFABÉTICO).
--   · BD que ya existe: se aplica a mano con psql. Por eso es
--     'CREATE OR REPLACE': se puede ejecutar varias veces.
-- ==========================================================================

SET search_path TO catedras, public;

CREATE OR REPLACE PROCEDURE catedras.sp_guardar_asistente(
    -- Columnas propias del maestro. Los tamaños son IGUALES a los de la
    -- tabla 'asistente'. Los correos llegan como texto: al insertarlos,
    -- PostgreSQL los convierte a 'citext' (el tipo de la columna).
    IN p_id_asis              character varying(15),
    IN p_nombres              character varying(100),
    IN p_apellidos            character varying(100),
    IN p_nombre_completo_asis character varying(200) DEFAULT NULL,
    IN p_correo_inst          character varying(254) DEFAULT NULL,
    IN p_correo_pers          character varying(254) DEFAULT NULL,
    IN p_celular              character varying(20)  DEFAULT NULL,
    IN p_es_externo           boolean                DEFAULT false,
    IN p_activo               boolean                DEFAULT true,
    IN p_fk_lote_carga        bigint                 DEFAULT NULL,
    -- Detalles. NULL o [] = sin ese detalle. Formatos:
    --   p_documentos     [{"fk_tipo_documento":"CC","numero":"1020304050","vigente":true}]
    --   p_vinculaciones  [{"fk_tipo_vinculacion":"ESTUDIANTE","fecha_ini":"2026-01-20"}]
    --   p_programas      [{"fk_programa":"M0286","fk_periodo":"2026-1","estado":"ACTIVO"}]
    --   p_consentimiento {"version_politica":"v1.0","canal":"WEB","ip":"10.0.0.1"}  (UN objeto)
    IN p_documentos           jsonb                  DEFAULT NULL,
    IN p_vinculaciones        jsonb                  DEFAULT NULL,
    IN p_programas            jsonb                  DEFAULT NULL,
    IN p_consentimiento       jsonb                  DEFAULT NULL,
    -- Salida: el id que la BD le asignó al asistente nuevo.
    INOUT p_id_asistente      bigint                 DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    -- 1) Maestro. 'id_asis' o 'correo_institucional' duplicados, o un CHECK
    --    que no se cumple (chk_asistente_externo, chk_asistente_correo...)
    --    -> la excepción del motor sube tal cual (el controlador responde 500).
    --    RETURNING ... INTO guarda el id generado en el parámetro de salida.
    INSERT INTO asistente (id_asis, nombres, apellidos, nombre_completo_asis,
                           correo_institucional, correo_personal, celular,
                           es_externo, activo, fk_lote_carga)
    VALUES (p_id_asis, p_nombres, p_apellidos, p_nombre_completo_asis,
            p_correo_inst, p_correo_pers, p_celular,
            p_es_externo, p_activo, p_fk_lote_carga)
    RETURNING id_asistente INTO p_id_asistente;

    -- 2) Documentos iniciales. 'fk_asistente' NO viene en el JSON: es
    --    siempre el asistente que se acaba de crear.
    --    COALESCE(...) aplica el DEFAULT de la columna si el campo no llegó.
    IF p_documentos IS NOT NULL AND jsonb_array_length(p_documentos) > 0 THEN
        INSERT INTO documento_asistente (fk_asistente, fk_tipo_documento, numero, vigente)
        SELECT p_id_asistente,
               e ->> 'fk_tipo_documento',
               e ->> 'numero',
               COALESCE((e ->> 'vigente')::boolean, true)
          FROM jsonb_array_elements(p_documentos) AS e;
    END IF;

    -- 3) Vinculaciones iniciales (llave compuesta: asistente + tipo + fecha_ini).
    --    Sin fecha_ini -> hoy.
    IF p_vinculaciones IS NOT NULL AND jsonb_array_length(p_vinculaciones) > 0 THEN
        INSERT INTO vinculacion_asistente (fk_asistente, fk_tipo_vinculacion, fecha_ini)
        SELECT p_id_asistente,
               e ->> 'fk_tipo_vinculacion',
               COALESCE((e ->> 'fecha_ini')::date, CURRENT_DATE)
          FROM jsonb_array_elements(p_vinculaciones) AS e;
    END IF;

    -- 4) Programas iniciales (llave compuesta: asistente + programa + periodo).
    --    Sin estado -> 'ACTIVO'. Programa o periodo inexistente -> FK -> 500.
    IF p_programas IS NOT NULL AND jsonb_array_length(p_programas) > 0 THEN
        INSERT INTO programa_asistente (fk_asistente, fk_programa, fk_periodo, estado)
        SELECT p_id_asistente,
               e ->> 'fk_programa',
               e ->> 'fk_periodo',
               COALESCE(e ->> 'estado', 'ACTIVO')
          FROM jsonb_array_elements(p_programas) AS e;
    END IF;

    -- 5) Consentimiento inicial: UN objeto (no un arreglo), porque un
    --    asistente nuevo acepta como máximo la política vigente en ese momento.
    --    'aceptado_en' lo pone la BD (DEFAULT now()). La IP llega como texto
    --    y se convierte a 'inet' (una IP mal escrita -> excepción -> 500).
    IF p_consentimiento IS NOT NULL THEN
        INSERT INTO consentimiento_datos (fk_asistente, version_politica, canal, ip)
        VALUES (p_id_asistente,
                p_consentimiento ->> 'version_politica',
                COALESCE(p_consentimiento ->> 'canal', 'WEB'),
                (p_consentimiento ->> 'ip')::inet);
    END IF;

    -- Si cualquiera de los pasos 2 a 5 falla, PostgreSQL revierte TODO el
    -- CALL: el asistente del paso 1 tampoco queda creado.
END;
$$;

COMMENT ON PROCEDURE catedras.sp_guardar_asistente IS
    'Guía IA · Regla 1. Crea un asistente y, opcionalmente, sus documentos, '
    'vinculaciones, programas y consentimiento iniciales, en una sola '
    'transacción (V3, version3/3_plan.md §4.1). Devuelve el id en p_id_asistente.';
