-- ==========================================================================
--  db/postgres/pruebas/prueba_sp_maestro_detalle_formulario.sql
--  Proyecto Cátedras · Maestro-detalle en el formulario (migraciones 042-044)
-- --------------------------------------------------------------------------
--  Prueba DIRECTAMENTE contra PostgreSQL (sin la API) los 3 procedimientos
--  que guardan un maestro JUNTO con sus detalles (Guía IA, Regla 1):
--    · sp_guardar_catedra  -> cátedra + sus sesiones
--    · sp_guardar_encuesta -> encuesta + sus preguntas + las opciones de cada una
--    · sp_guardar_sesion   -> sesión + su primer enlace de registro
--  Lo importante: (1) los detalles quedan colgando del maestro recién
--  creado, (2) el trigger de consecutivo numera 1, 2, 3 dentro del MISMO
--  CALL, y (3) TODO O NADA: si un detalle falla, el maestro no queda.
--
--  NO DEJA RASTRO: todo corre dentro de BEGIN ... ROLLBACK.
--
--  Cómo correrlo (con Docker arriba, desde la raíz del repositorio):
--      docker compose cp db/postgres/pruebas/prueba_sp_maestro_detalle_formulario.sql postgres:/tmp/p.sql
--      docker compose exec -T postgres psql -U sa -d bdcatedras_postgres_local -v ON_ERROR_STOP=1 -f /tmp/p.sql
-- ==========================================================================

\set QUIET on
\pset footer off
SET client_min_messages TO warning;
BEGIN;
SET LOCAL search_path TO catedras, public;

CREATE TEMP TABLE resultado_prueba (n serial, tabla text, caso text, ok boolean, detalle text);

CREATE FUNCTION pg_temp.ok(p_tabla text, p_caso text, p_cond boolean, p_detalle text DEFAULT NULL)
RETURNS void LANGUAGE sql AS $$
    INSERT INTO resultado_prueba (tabla, caso, ok, detalle)
    VALUES (p_tabla, p_caso, COALESCE(p_cond, false), p_detalle);
$$;

-- Ejecuta un CALL y devuelve su único INOUT como texto.
CREATE FUNCTION pg_temp.sp(p_sql text) RETURNS text LANGUAGE plpgsql AS $$
DECLARE v text;
BEGIN
    EXECUTE p_sql INTO v;
    RETURN v;
END;
$$;

-- El CALL DEBE fallar con ese SQLSTATE. Lo que haya hecho se revierte
-- (el bloque EXCEPTION es una subtransacción), que es justo lo que se
-- verifica después: el maestro NO debe existir.
CREATE FUNCTION pg_temp.falla(p_tabla text, p_caso text, p_sql text, p_sqlstate text)
RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    EXECUTE p_sql;
    PERFORM pg_temp.ok(p_tabla, p_caso, false, 'no lanzó ningún error');
EXCEPTION WHEN OTHERS THEN
    PERFORM pg_temp.ok(p_tabla, p_caso, SQLSTATE = p_sqlstate, SQLSTATE || ' ' || SQLERRM);
END;
$$;

-- --------------------------------------------------------------------------
--  1. sp_guardar_catedra (042): cátedra + sesiones
-- --------------------------------------------------------------------------
DO $$
DECLARE
    T constant text := 'catedra+sesiones';
    r json;
    v_id bigint;
BEGIN
    -- Sin sesiones: se comporta como sp_crear_catedra.
    r := pg_temp.sp($q$CALL catedras.sp_guardar_catedra('990000001', 'Cátedra sola', 'CAAB', NULL, true, NULL, NULL)$q$)::json;
    PERFORM pg_temp.ok(T, 'sin sesiones: crea la cátedra y devuelve nombre_asis',
        (r ->> 'id_catedra') IS NOT NULL AND r ->> 'nombre_asis' = 'Cátedra sola');
    PERFORM pg_temp.ok(T, 'sin sesiones: "sesiones" sale como lista vacía',
        json_array_length(r -> 'sesiones') = 0);

    -- Con 3 sesiones: el trigger las numera 1, 2, 3 DENTRO del mismo CALL.
    r := pg_temp.sp($q$CALL catedras.sp_guardar_catedra('990000002', 'Cátedra con sesiones', 'CAAB', NULL, true,
        '[{"inicio":"2026-11-03T19:00:00Z","fin":"2026-11-03T21:00:00Z","fk_modalidad":"PRESENCIAL","fk_sede":"SAN_BENITO","fk_periodo":"2026-2","titulo":"Primera","fk_encuesta":1,"cupo":40},
          {"inicio":"2026-11-10T19:00:00Z","fin":"2026-11-10T21:00:00Z","fk_modalidad":"VIRTUAL","fk_sede":"VIRTUAL","fk_periodo":"2026-2"},
          {"inicio":"2026-11-17T19:00:00Z","fin":"2026-11-17T21:00:00Z","fk_modalidad":"PRESENCIAL","fk_sede":"BELLO","fk_periodo":"2026-2","lugar":"Aula 3"}]',
        NULL)$q$)::json;
    v_id := (r ->> 'id_catedra')::bigint;
    PERFORM pg_temp.ok(T, 'con 3 sesiones: las 3 quedan en la cátedra nueva',
        (SELECT count(*) FROM sesion WHERE fk_catedra = v_id) = 3);
    PERFORM pg_temp.ok(T, 'el trigger numera 1, 2, 3 dentro del mismo CALL (una sentencia por sesión)',
        (SELECT array_agg(numero_reunion ORDER BY inicio) FROM sesion WHERE fk_catedra = v_id) = ARRAY[1,2,3]::smallint[]);
    PERFORM pg_temp.ok(T, 'devuelve "sesiones" con id y número, en el orden recibido',
        json_array_length(r -> 'sesiones') = 3 AND (r -> 'sesiones' -> 2 ->> 'numero_reunion')::int = 3);
    PERFORM pg_temp.ok(T, 'los campos opcionales llegan (título, encuesta, cupo, lugar)',
        EXISTS (SELECT 1 FROM sesion WHERE fk_catedra = v_id AND titulo = 'Primera' AND fk_encuesta = 1 AND cupo = 40)
        AND EXISTS (SELECT 1 FROM sesion WHERE fk_catedra = v_id AND lugar = 'Aula 3'));
    PERFORM pg_temp.ok(T, 'estado por defecto PROGRAMADA',
        NOT EXISTS (SELECT 1 FROM sesion WHERE fk_catedra = v_id AND estado <> 'PROGRAMADA'));
    PERFORM pg_temp.ok(T, 'las horas se guardan como el mismo instante (UTC)',
        EXISTS (SELECT 1 FROM sesion WHERE fk_catedra = v_id AND inicio = '2026-11-03 14:00:00-05'::timestamptz));

    -- TODO O NADA: la 2.ª sesión tiene una sede que no existe.
    PERFORM pg_temp.falla(T, 'sede inexistente en una sesión -> 23503 (FK)',
        $q$CALL catedras.sp_guardar_catedra('990000003', 'No debe quedar', 'CAAB', NULL, true,
           '[{"inicio":"2026-11-03T19:00:00Z","fin":"2026-11-03T21:00:00Z","fk_modalidad":"PRESENCIAL","fk_sede":"SAN_BENITO","fk_periodo":"2026-2"},
             {"inicio":"2026-11-10T19:00:00Z","fin":"2026-11-10T21:00:00Z","fk_modalidad":"PRESENCIAL","fk_sede":"NO_EXISTE","fk_periodo":"2026-2"}]',
           NULL)$q$, '23503');
    PERFORM pg_temp.ok(T, '... y NI la cátedra NI la 1.ª sesión quedaron guardadas',
        NOT EXISTS (SELECT 1 FROM catedra WHERE id_evento_asis = '990000003'));

    PERFORM pg_temp.falla(T, 'fin antes de inicio en una sesión -> 23514 (CHECK)',
        $q$CALL catedras.sp_guardar_catedra('990000004', 'No debe quedar', 'CAAB', NULL, true,
           '[{"inicio":"2026-11-03T21:00:00Z","fin":"2026-11-03T19:00:00Z","fk_modalidad":"PRESENCIAL","fk_sede":"SAN_BENITO","fk_periodo":"2026-2"}]',
           NULL)$q$, '23514');
    PERFORM pg_temp.ok(T, '... y la cátedra no quedó',
        NOT EXISTS (SELECT 1 FROM catedra WHERE id_evento_asis = '990000004'));

    PERFORM pg_temp.falla(T, 'dos sesiones con el mismo número explícito -> 23505 (uq_sesion_reunion)',
        $q$CALL catedras.sp_guardar_catedra('990000005', 'No debe quedar', 'CAAB', NULL, true,
           '[{"inicio":"2026-11-03T19:00:00Z","fin":"2026-11-03T21:00:00Z","fk_modalidad":"PRESENCIAL","fk_sede":"SAN_BENITO","fk_periodo":"2026-2","numero_reunion":4},
             {"inicio":"2026-11-10T19:00:00Z","fin":"2026-11-10T21:00:00Z","fk_modalidad":"PRESENCIAL","fk_sede":"SAN_BENITO","fk_periodo":"2026-2","numero_reunion":4}]',
           NULL)$q$, '23505');
    PERFORM pg_temp.ok(T, '... y la cátedra no quedó',
        NOT EXISTS (SELECT 1 FROM catedra WHERE id_evento_asis = '990000005'));

    PERFORM pg_temp.falla(T, 'código ASIS mal formado (el maestro) -> 23514',
        $q$CALL catedras.sp_guardar_catedra('12', 'x', 'CAAB', NULL, true, NULL, NULL)$q$, '23514');
EXCEPTION WHEN OTHERS THEN
    PERFORM pg_temp.ok('catedra+sesiones', 'bloque terminó sin excepción inesperada', false, SQLSTATE || ' ' || SQLERRM);
END $$;

-- --------------------------------------------------------------------------
--  2. sp_guardar_encuesta (043): encuesta + preguntas + opciones
-- --------------------------------------------------------------------------
DO $$
DECLARE
    T constant text := 'encuesta+preguntas';
    v_id bigint;
BEGIN
    v_id := pg_temp.sp($q$CALL catedras.sp_guardar_encuesta('Encuesta sola', 1::smallint, '2026-01-01', NULL, true, NULL, NULL)$q$)::bigint;
    PERFORM pg_temp.ok(T, 'sin preguntas: crea la encuesta y devuelve su id',
        EXISTS (SELECT 1 FROM encuesta WHERE id_encuesta = v_id AND nombre = 'Encuesta sola'));

    v_id := pg_temp.sp($q$CALL catedras.sp_guardar_encuesta('Encuesta completa', 1::smallint, '2026-01-01', NULL, true,
        '[{"enunciado":"¿Qué tan útil fue?","orden":1,"fk_tipo_pregunta":"ESCALA_5","obligatoria":true,"valor_min":1,"valor_max":5},
          {"enunciado":"¿Por dónde se enteró?","orden":2,"fk_tipo_pregunta":"OPCION_UNICA",
           "opciones":[{"etiqueta":"Correo","valor":1,"orden":1},{"etiqueta":"Cartelera","orden":2},{"etiqueta":"Amigos","orden":3}]},
          {"enunciado":"Comentarios","orden":3,"fk_tipo_pregunta":"TEXTO_LIBRE","opciones":null}]',
        NULL)$q$)::bigint;
    PERFORM pg_temp.ok(T, 'con 3 preguntas: las 3 quedan en la encuesta nueva, en su orden',
        (SELECT array_agg(orden ORDER BY orden) FROM pregunta WHERE fk_encuesta = v_id) = ARRAY[1,2,3]::smallint[]);
    PERFORM pg_temp.ok(T, 'obligatoria y rango llegan; obligatoria por defecto false',
        EXISTS (SELECT 1 FROM pregunta WHERE fk_encuesta = v_id AND orden = 1 AND obligatoria AND valor_min = 1 AND valor_max = 5)
        AND EXISTS (SELECT 1 FROM pregunta WHERE fk_encuesta = v_id AND orden = 2 AND NOT obligatoria));
    PERFORM pg_temp.ok(T, 'segundo nivel: la pregunta de opción única trae sus 3 opciones',
        (SELECT count(*) FROM opcion_pregunta o JOIN pregunta p ON p.id_pregunta = o.fk_pregunta
          WHERE p.fk_encuesta = v_id AND p.orden = 2) = 3);
    PERFORM pg_temp.ok(T, '"opciones": null en el JSON = pregunta sin opciones (no error)',
        NOT EXISTS (SELECT 1 FROM opcion_pregunta o JOIN pregunta p ON p.id_pregunta = o.fk_pregunta
                     WHERE p.fk_encuesta = v_id AND p.orden = 3));

    PERFORM pg_temp.falla(T, 'dos preguntas con el mismo orden -> 23505 (uq_pregunta_ord)',
        $q$CALL catedras.sp_guardar_encuesta('No debe quedar A', 1::smallint, '2026-01-01', NULL, true,
           '[{"enunciado":"a","orden":1,"fk_tipo_pregunta":"ESCALA_5"},{"enunciado":"b","orden":1,"fk_tipo_pregunta":"ESCALA_5"}]', NULL)$q$,
        '23505');
    PERFORM pg_temp.ok(T, '... y la encuesta no quedó', NOT EXISTS (SELECT 1 FROM encuesta WHERE nombre = 'No debe quedar A'));

    PERFORM pg_temp.falla(T, 'opción con orden repetido (2.º nivel) -> 23505 (uq_opcion_orden)',
        $q$CALL catedras.sp_guardar_encuesta('No debe quedar B', 1::smallint, '2026-01-01', NULL, true,
           '[{"enunciado":"a","orden":1,"fk_tipo_pregunta":"OPCION_UNICA","opciones":[{"etiqueta":"x","orden":1},{"etiqueta":"y","orden":1}]}]', NULL)$q$,
        '23505');
    PERFORM pg_temp.ok(T, '... y ni la encuesta ni la pregunta quedaron',
        NOT EXISTS (SELECT 1 FROM encuesta WHERE nombre = 'No debe quedar B'));

    PERFORM pg_temp.falla(T, 'tipo de pregunta inexistente -> 23503 (FK)',
        $q$CALL catedras.sp_guardar_encuesta('No debe quedar C', 1::smallint, '2026-01-01', NULL, true,
           '[{"enunciado":"a","orden":1,"fk_tipo_pregunta":"NO_EXISTE"}]', NULL)$q$,
        '23503');
    PERFORM pg_temp.ok(T, '... y la encuesta no quedó', NOT EXISTS (SELECT 1 FROM encuesta WHERE nombre = 'No debe quedar C'));
EXCEPTION WHEN OTHERS THEN
    PERFORM pg_temp.ok('encuesta+preguntas', 'bloque terminó sin excepción inesperada', false, SQLSTATE || ' ' || SQLERRM);
END $$;

-- --------------------------------------------------------------------------
--  3. sp_guardar_sesion (044): sesión + su primer enlace de registro
-- --------------------------------------------------------------------------
DO $$
DECLARE
    T constant text := 'sesion+enlace';
    r json;
    v_id bigint;
    v_siguiente smallint;
BEGIN
    SELECT coalesce(max(numero_reunion), 0) + 1 INTO v_siguiente FROM sesion WHERE fk_catedra = 3975;

    r := pg_temp.sp($q$CALL catedras.sp_guardar_sesion(3975, '2026-11-03T19:00:00Z', '2026-11-03T21:00:00Z',
        'PRESENCIAL', 'SAN_BENITO', '2026-2', 0::smallint, NULL, 'Sin enlace', NULL, NULL, NULL, NULL, NULL, NULL, NULL)$q$)::json;
    PERFORM pg_temp.ok(T, 'sin enlace: crea la sesión con el consecutivo y id_enlace null',
        (r ->> 'numero_reunion')::int = v_siguiente AND (r ->> 'id_enlace') IS NULL
        AND NOT EXISTS (SELECT 1 FROM enlace_registro WHERE fk_sesion = (r ->> 'id_sesion')::bigint));

    r := pg_temp.sp($q$CALL catedras.sp_guardar_sesion(3975, '2026-11-10T19:00:00Z', '2026-11-10T21:00:00Z',
        'PRESENCIAL', 'SAN_BENITO', '2026-2', 0::smallint, NULL, 'Con enlace', NULL, NULL, NULL, NULL, NULL,
        '{"minutos_antes":15,"minutos_despues":45,"fk_usuario":1}', NULL)$q$)::json;
    v_id := (r ->> 'id_sesion')::bigint;
    PERFORM pg_temp.ok(T, 'con enlace: la sesión nace con UN enlace vigente',
        (SELECT count(*) FROM enlace_registro WHERE fk_sesion = v_id AND revocado_en IS NULL) = 1
        AND (r ->> 'id_enlace')::bigint = (SELECT id_enlace FROM enlace_registro WHERE fk_sesion = v_id));
    PERFORM pg_temp.ok(T, 'la ventana usa los minutos pedidos (15 antes, 45 después)',
        EXISTS (SELECT 1 FROM enlace_registro WHERE fk_sesion = v_id
                 AND lower(ventana) = '2026-11-10T18:45:00Z'::timestamptz
                 AND upper(ventana) = '2026-11-10T21:45:00Z'::timestamptz));
    PERFORM pg_temp.ok(T, 'queda quién lo emitió (fk_usuario_crea)',
        EXISTS (SELECT 1 FROM enlace_registro WHERE fk_sesion = v_id AND fk_usuario_crea = 1));

    PERFORM pg_temp.falla(T, 'usuario emisor inexistente -> 23503 (FK del enlace)',
        $q$CALL catedras.sp_guardar_sesion(3975, '2026-11-17T19:00:00Z', '2026-11-17T21:00:00Z',
           'PRESENCIAL', 'SAN_BENITO', '2026-2', 0::smallint, NULL, 'No debe quedar', NULL, NULL, NULL, NULL, NULL,
           '{"fk_usuario":999999}', NULL)$q$, '23503');
    PERFORM pg_temp.ok(T, '... y la sesión tampoco quedó (todo o nada)',
        NOT EXISTS (SELECT 1 FROM sesion WHERE titulo = 'No debe quedar'));
EXCEPTION WHEN OTHERS THEN
    PERFORM pg_temp.ok('sesion+enlace', 'bloque terminó sin excepción inesperada', false, SQLSTATE || ' ' || SQLERRM);
END $$;

-- --------------------------------------------------------------------------
--  Resultado
-- --------------------------------------------------------------------------
\pset format unaligned
\pset tuples_only on
SELECT CASE WHEN ok THEN '  OK    ' ELSE '  FALLA ' END || rpad(tabla, 22) || caso
       || CASE WHEN NOT ok AND detalle IS NOT NULL THEN E'\n          -> ' || detalle ELSE '' END
  FROM resultado_prueba ORDER BY n;
SELECT format(E'\n  %s casos: %s OK, %s FALLA', count(*), count(*) FILTER (WHERE ok), count(*) FILTER (WHERE NOT ok))
  FROM resultado_prueba;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM resultado_prueba WHERE NOT ok) THEN
        RAISE EXCEPTION 'Hay casos en FALLA (ver arriba).';
    END IF;
END $$;

ROLLBACK;
