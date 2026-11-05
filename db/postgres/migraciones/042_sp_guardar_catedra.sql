-- ==========================================================================
--  db/postgres/migraciones/042_sp_guardar_catedra.sql
--  Proyecto Cátedras · Maestro-detalle en el formulario — Guía IA, Regla 1
-- --------------------------------------------------------------------------
--  Maestro: 'catedra'. Detalle (opcional): 'sesion' (#1 de la Regla 4).
--
--  POST /api/Catedra puede traer la cátedra JUNTO con sus primeras
--  sesiones (lista 'sesiones'). Este SP las guarda en UNA sola transacción:
--  si cualquier sesión falla (fin <= inicio, modalidad/sede/periodo que no
--  existen, número de reunión repetido...), PostgreSQL revierte TODO el
--  CALL y ni la cátedra queda creada (ACID · A: todo o nada) — mismo
--  criterio que sp_guardar_programa_academico (v2) y sp_guardar_pregunta (v6).
--
--  REUTILIZA, no reescribe: la cátedra se crea con 'sp_crear_catedra' y
--  cada sesión con 'sp_crear_sesion' (migraciones 010 y 011). Así las
--  reglas de cada INSERT viven en un solo lugar.
--
--  ¿Por qué un LOOP y no un 'INSERT ... SELECT FROM jsonb_array_elements'?
--  Por el trigger 'trg_sesion_consecutivo' (ADR D20): calcula el número de
--  reunión con 'max(numero_reunion) + 1'. Dentro de UN solo INSERT, las
--  filas que ese mismo INSERT ya agregó NO son visibles para el SELECT del
--  trigger, así que todas recibirían el mismo número (y chocarían con
--  uq_sesion_reunion). Con una sentencia por sesión, cada una ve a las
--  anteriores: 1, 2, 3...
--
--  Formato de 'p_sesiones' (los nombres son los del JSON de la API):
--    [{"inicio":"2026-10-20T19:00:00Z", "fin":"2026-10-20T21:00:00Z",
--      "fk_modalidad":"PRESENCIAL", "fk_sede":"SAN_BENITO", "fk_periodo":"2026-2",
--      "titulo":"...", "fk_encuesta":1, "lugar":"...", "cupo":40,
--      "numero_reunion":null, "estado":"PROGRAMADA"}, ...]
--  'fk_catedra' NO viene en el JSON: es siempre la cátedra recién creada.
--
--  CÓMO SE APLICA: docker-compose.yml lo monta como 'z042_' (corre después
--  de 'z010_' y 'z011_', de los que depende). Con una BD ya creada, se
--  aplica a mano con psql: es 'CREATE OR REPLACE'.
-- ==========================================================================

SET search_path TO catedras, public;

CREATE OR REPLACE PROCEDURE catedras.sp_guardar_catedra(
    -- Columnas propias del maestro (mismos tipos que sp_crear_catedra).
    IN    p_id_evento_asis character(9),
    IN    p_nombre         character varying(200),
    IN    p_fk_tipo_evento character(4),
    IN    p_fk_dependencia character varying(15) DEFAULT NULL,
    IN    p_activo         boolean               DEFAULT true,
    -- Detalle: arreglo JSON opcional. NULL o '[]' = cátedra sin sesiones.
    IN    p_sesiones       jsonb                 DEFAULT NULL,
    -- Salida: la cátedra como quedó (id, nombre_asis, creado_en) y
    -- 'sesiones': [{id_sesion, numero_reunion}, ...] en el orden recibido.
    INOUT p_resultado      json                  DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_catedra  json;
    v_id       bigint;
    v_sesion   jsonb;
    v_creada   json;
    v_creadas  jsonb := '[]'::jsonb;
BEGIN
    -- 1) Maestro (reutiliza sp_crear_catedra, migración 010).
    CALL catedras.sp_crear_catedra(p_id_evento_asis, p_nombre, p_fk_tipo_evento,
                                   p_fk_dependencia, p_activo, v_catedra);
    v_id := (v_catedra ->> 'id_catedra')::bigint;

    -- 2) Detalle: una sesión por elemento, EN ORDEN (WITH ORDINALITY) y una
    --    sentencia por sesión (ver la nota del trigger arriba).
    IF p_sesiones IS NOT NULL AND jsonb_typeof(p_sesiones) = 'array' THEN
        FOR v_sesion IN
            SELECT e FROM jsonb_array_elements(p_sesiones) WITH ORDINALITY AS t(e, n) ORDER BY n
        LOOP
            v_creada := NULL;
            CALL catedras.sp_crear_sesion(
                v_id,
                (v_sesion ->> 'inicio')::timestamptz,
                (v_sesion ->> 'fin')::timestamptz,
                v_sesion ->> 'fk_modalidad',
                v_sesion ->> 'fk_sede',
                v_sesion ->> 'fk_periodo',
                COALESCE((v_sesion ->> 'numero_reunion')::smallint, 0::smallint),   -- 0 = lo pone el trigger
                -- (el '::smallint' importa: un CALL elige el procedimiento por
                --  TIPOS, y un 0 suelto es 'integer' -> 42883 'does not exist')
                (v_sesion ->> 'numero_reunion_asis')::smallint,
                v_sesion ->> 'titulo',
                (v_sesion ->> 'fk_encuesta')::bigint,
                v_sesion ->> 'lugar',
                v_sesion ->> 'enlace_virtual',
                (v_sesion ->> 'cupo')::integer,
                COALESCE(v_sesion ->> 'estado', 'PROGRAMADA'),
                v_creada);

            v_creadas := v_creadas || jsonb_build_array(jsonb_build_object(
                'id_sesion',      (v_creada ->> 'id_sesion')::bigint,
                'numero_reunion', (v_creada ->> 'numero_reunion')::smallint));
        END LOOP;
    END IF;

    -- Si cualquier sesión falló, la excepción ya abortó el CALL y nada de
    -- lo anterior (ni la cátedra) quedó guardado.
    p_resultado := (v_catedra::jsonb || jsonb_build_object('sesiones', v_creadas))::json;
END;
$$;

COMMENT ON PROCEDURE catedras.sp_guardar_catedra IS
    'Guía IA · Regla 1. Crea una cátedra y, opcionalmente, sus primeras sesiones, '
    'en una sola transacción. Reutiliza sp_crear_catedra y sp_crear_sesion. '
    'Devuelve la cátedra y "sesiones": [{id_sesion, numero_reunion}].';
