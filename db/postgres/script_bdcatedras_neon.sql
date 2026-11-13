--
-- ==========================================================================
--  script_bdcatedras_neon.sql  ·  BASE VACIA PARA NEON (PostgreSQL 16/17)
-- --------------------------------------------------------------------------
--  Genera la base COMPLETA (tablas, restricciones, indices, triggers y TODOS
--  los procedimientos/funciones) pero SIN datos de negocio.
--
--  Que contiene:
--    1) Esquema 'catedras' + extensiones (btree_gist, citext, pg_trgm,
--       pgcrypto, unaccent: todas disponibles en Neon).
--    2) Las tablas y rutinas del script provisto (db/postgres/script_bdcatedras.sql)
--       SIN sus INSERT de datos (asistentes, catedras, sesiones, registros...).
--    3) Las migraciones 001 a 073 (procedimientos almacenados, subversiones v7.1 y
--       v7.2, roles, programas reales, modalidades y ubicacion por modalidad) en orden, ya unidas. Se omiten
--       la 048 (siembra un estudiante) y la 065 (programas de prueba "(mock)": la 070
--       los reemplaza por los programas reales de la USB en el Valle de Aburra).
--    4) Catalogos minimos que el sistema necesita para funcionar:
--       sede, estado_proceso, modalidad, periodo_academico, rol,
--       tipo_documento, tipo_evento, tipo_pregunta, tipo_vinculacion,
--       parametro y facultad. Las migraciones 068 a 072 los dejan asi: roles ADMIN
--       y ASISTENTE; modalidades PRESENCIAL, VIRTUAL y TELEPRESENCIAL; 39 programas
--       academicos reales (pregrado y posgrado).
--       Despues de las migraciones hay un bloque «ajustes de catalogo» con los valores de la
--       base local (modalidades VIRTUAL y TELEPRESENCIAL con canal QR; tipo de documento LC
--       inactivo). Esta comentado en el propio bloque como revertirlo.
--    5) Un administrador (usuario 'admin.catedras') para poder iniciar sesion.
--       Es el bloque 'ADMINISTRADOR INICIAL' del final; borralo si no lo quieres.
--
--  Todo va en UNA transaccion: si algo falla, no queda nada a medias.
--
--  Como ejecutarlo (conexion DIRECTA de Neon, no la del pooler '-pooler'):
--    psql "postgresql://USUARIO:CLAVE@HOST/BASE?sslmode=require" \
--         -v ON_ERROR_STOP=1 -f script_bdcatedras_neon.sql
--
--  Requiere que el esquema 'catedras' NO exista (base nueva o vacia). Para
--  repetir la prueba:  DROP SCHEMA catedras CASCADE;
--  (las extensiones quedan en 'public' y no estorban).
--
--  Este archivo se GENERA a partir de los originales: no los modifica.
-- ==========================================================================

-- ##########  PARTE 1: script provisto, sin datos  ##########
--
-- PostgreSQL database dump
--
-- Generado originalmente con pg_dump 17.6.
-- Adaptado para ejecutarse sin cambios en PostgreSQL 16, 17 y superiores.
--
--   Uso recomendado:
--     psql -v ON_ERROR_STOP=1 -d <base_de_datos> -f script_bdcatedras.sql
--
-- El archivo no contiene meta-comandos de psql (\restrict, \set, \connect),
-- por lo que tambien se puede ejecutar tal cual desde pgAdmin, DBeaver,
-- IntelliJ o docker-entrypoint-initdb.d.
--
-- Todo el contenido va dentro de una sola transaccion: si algo falla, no
-- queda nada a medias en la base.
--

-- Dumped from database version 17.6
-- Dumped by pg_dump version 17.6

-- Started on 2026-08-27 16:18:08


--
-- Verificaciones previas: version del servidor y esquema destino libre.
--

DO $pg_compat$
BEGIN
    IF current_setting('server_version_num')::int < 160000 THEN
        RAISE EXCEPTION
            'Se requiere PostgreSQL 16 o superior. Version detectada: % (%).',
            current_setting('server_version'),
            current_setting('server_version_num');
    END IF;

    IF EXISTS (SELECT 1 FROM pg_catalog.pg_namespace WHERE nspname = 'catedras') THEN
        RAISE EXCEPTION
            'El esquema "catedras" ya existe en la base "%". Ejecute DROP SCHEMA catedras CASCADE; antes de restaurar.',
            current_database();
    END IF;
END
$pg_compat$;


BEGIN;

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;

--
-- transaction_timeout solo existe a partir de PostgreSQL 17. Se aplica unicamente
-- si el servidor lo reconoce; en PostgreSQL 16 un SET directo abortaria el script.
--

DO $pg_compat$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_catalog.pg_settings WHERE name = 'transaction_timeout') THEN
        PERFORM pg_catalog.set_config('transaction_timeout', '0', false);
    END IF;
END
$pg_compat$;

SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- TOC entry 11 (class 2615 OID 86748)
-- Name: catedras; Type: SCHEMA; Schema: -; Owner: -
--

CREATE SCHEMA catedras;


--
-- TOC entry 5936 (class 0 OID 0)
-- Dependencies: 11
-- Name: SCHEMA catedras; Type: COMMENT; Schema: -; Owner: -
--

COMMENT ON SCHEMA catedras IS 'Todos los objetos del sistema. Se usa un esquema propio y no public para poder revocar permisos sobre public sin afectar al proyecto.';


--
-- TOC entry 2 (class 3079 OID 86749)
-- Name: btree_gist; Type: EXTENSION; Schema: -; Owner: -
--

CREATE EXTENSION IF NOT EXISTS btree_gist WITH SCHEMA public;


--
-- TOC entry 5937 (class 0 OID 0)
-- Dependencies: 2
-- Name: EXTENSION btree_gist; Type: COMMENT; Schema: -; Owner: -
--

COMMENT ON EXTENSION btree_gist IS 'support for indexing common datatypes in GiST';


--
-- TOC entry 3 (class 3079 OID 87399)
-- Name: citext; Type: EXTENSION; Schema: -; Owner: -
--

CREATE EXTENSION IF NOT EXISTS citext WITH SCHEMA public;


--
-- TOC entry 5938 (class 0 OID 0)
-- Dependencies: 3
-- Name: EXTENSION citext; Type: COMMENT; Schema: -; Owner: -
--

COMMENT ON EXTENSION citext IS 'data type for case-insensitive character strings';


--
-- TOC entry 4 (class 3079 OID 87504)
-- Name: pg_trgm; Type: EXTENSION; Schema: -; Owner: -
--

CREATE EXTENSION IF NOT EXISTS pg_trgm WITH SCHEMA public;


--
-- TOC entry 5939 (class 0 OID 0)
-- Dependencies: 4
-- Name: EXTENSION pg_trgm; Type: COMMENT; Schema: -; Owner: -
--

COMMENT ON EXTENSION pg_trgm IS 'text similarity measurement and index searching based on trigrams';


--
-- TOC entry 5 (class 3079 OID 87585)
-- Name: pgcrypto; Type: EXTENSION; Schema: -; Owner: -
--

CREATE EXTENSION IF NOT EXISTS pgcrypto WITH SCHEMA public;


--
-- TOC entry 5940 (class 0 OID 0)
-- Dependencies: 5
-- Name: EXTENSION pgcrypto; Type: COMMENT; Schema: -; Owner: -
--

COMMENT ON EXTENSION pgcrypto IS 'cryptographic functions';


--
-- TOC entry 6 (class 3079 OID 87622)
-- Name: unaccent; Type: EXTENSION; Schema: -; Owner: -
--

CREATE EXTENSION IF NOT EXISTS unaccent WITH SCHEMA public;


--
-- TOC entry 5941 (class 0 OID 0)
-- Dependencies: 6
-- Name: EXTENSION unaccent; Type: COMMENT; Schema: -; Owner: -
--

COMMENT ON EXTENSION unaccent IS 'text search dictionary that removes accents';


--
-- TOC entry 291 (class 1255 OID 87629)
-- Name: fn_emitir_enlace_api(bigint, integer, integer, bigint); Type: FUNCTION; Schema: catedras; Owner: -
--

CREATE FUNCTION catedras.fn_emitir_enlace_api(p_id_sesion bigint, p_minutos_antes integer DEFAULT NULL::integer, p_minutos_despues integer DEFAULT NULL::integer, p_usuario bigint DEFAULT NULL::bigint) RETURNS bigint
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_enlace bigint;
BEGIN
    CALL catedras.sp_emitir_enlace(p_id_sesion, p_minutos_antes, p_minutos_despues, p_usuario, v_enlace);
    RETURN v_enlace;
END;
$$;


--
-- TOC entry 408 (class 1255 OID 87630)
-- Name: fn_exportar_asis(bigint); Type: FUNCTION; Schema: catedras; Owner: -
--

CREATE FUNCTION catedras.fn_exportar_asis(p_id_sesion bigint) RETURNS TABLE(reunion smallint, id_asis character varying, programa character varying)
    LANGUAGE sql STABLE
    SET search_path TO 'catedras', 'public'
    AS $$
    SELECT s.numero_reunion, a.id_asis, r.fk_programa_snapshot
      FROM registro_asistencia r
      JOIN sesion    s ON s.id_sesion    = r.fk_sesion
      JOIN asistente a ON a.id_asistente = r.fk_asistente
     WHERE r.fk_sesion = p_id_sesion
       -- RN-28 · solo se migra quien tiene ID y programa
       AND a.id_asis IS NOT NULL
       AND r.fk_programa_snapshot IS NOT NULL
       -- RN-08 · el externo se cuenta, pero NUNCA entra al archivo
       AND a.es_externo = false
     ORDER BY a.id_asis;
$$;


--
-- TOC entry 5942 (class 0 OID 0)
-- Dependencies: 408
-- Name: FUNCTION fn_exportar_asis(p_id_sesion bigint); Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON FUNCTION catedras.fn_exportar_asis(p_id_sesion bigint) IS 'RN-27. TRES columnas exactas: reunion, ID y codigo de programa - "cuando tenemos el archivo plano, son 3" (32:34). Ni una mas. El manual insiste: "unicamente haya informacion en las tres columnas correspondientes".';


--
-- TOC entry 517 (class 1255 OID 87631)
-- Name: fn_generar_lote_api(bigint, bigint); Type: FUNCTION; Schema: catedras; Owner: -
--

CREATE FUNCTION catedras.fn_generar_lote_api(p_id_sesion bigint, p_usuario bigint DEFAULT NULL::bigint) RETURNS bigint
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_lote bigint;
BEGIN
    CALL catedras.sp_generar_lote_migracion(p_id_sesion, p_usuario, v_lote);
    RETURN v_lote;
END;
$$;


--
-- TOC entry 570 (class 1255 OID 87632)
-- Name: fn_normalizar(text); Type: FUNCTION; Schema: catedras; Owner: -
--

CREATE FUNCTION catedras.fn_normalizar(p_texto text) RETURNS text
    LANGUAGE sql IMMUTABLE PARALLEL SAFE
    SET search_path TO 'catedras', 'public'
    AS $$
    SELECT lower(trim(regexp_replace(
               public.unaccent('public.unaccent', coalesce(p_texto, '')),
               '\s+', ' ', 'g')));
$$;


--
-- TOC entry 5943 (class 0 OID 0)
-- Dependencies: 570
-- Name: FUNCTION fn_normalizar(p_texto text); Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON FUNCTION catedras.fn_normalizar(p_texto text) IS 'Baja a minusculas, quita tildes y colapsa espacios. Es la funcion con que se compara el texto libre de los formularios contra alias_programa. Es IMMUTABLE a proposito: si no, no se puede construir el indice GIN.';


--
-- TOC entry 546 (class 1255 OID 87633)
-- Name: fn_resolver_asistente(text); Type: FUNCTION; Schema: catedras; Owner: -
--

CREATE FUNCTION catedras.fn_resolver_asistente(p_identificador text) RETURNS bigint
    LANGUAGE plpgsql STABLE
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_id  bigint;
    v_txt text := trim(p_identificador);
BEGIN
    IF v_txt IS NULL OR v_txt = '' THEN
        RETURN NULL;
    END IF;

    -- El formulario actual trae valores como 'ID:30000141979'
    v_txt := regexp_replace(v_txt, '^\s*(ID|Id|id)\s*[:.-]\s*', '');

    -- 1 · por codigo ASIS
    SELECT a.id_asistente INTO v_id
      FROM asistente a
     WHERE a.id_asis = v_txt AND a.activo;
    IF v_id IS NOT NULL THEN RETURN v_id; END IF;

    -- 2 · por correo, institucional o personal
    IF v_txt LIKE '%@%' THEN
        SELECT a.id_asistente INTO v_id
          FROM asistente a
         WHERE (a.correo_institucional = v_txt::citext
             OR a.correo_personal      = v_txt::citext)
           AND a.activo
         LIMIT 1;
        IF v_id IS NOT NULL THEN RETURN v_id; END IF;
    END IF;

    -- 3 · por numero de documento, VIGENTE O NO
    SELECT d.fk_asistente INTO v_id
      FROM documento_asistente d
      JOIN asistente a ON a.id_asistente = d.fk_asistente
     WHERE d.numero = v_txt AND a.activo
     ORDER BY d.vigente DESC
     LIMIT 1;

    RETURN v_id;
END;
$$;


--
-- TOC entry 5944 (class 0 OID 0)
-- Dependencies: 546
-- Name: FUNCTION fn_resolver_asistente(p_identificador text); Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON FUNCTION catedras.fn_resolver_asistente(p_identificador text) IS 'RN-06. Busca por las tres vias y devuelve el asistente, o NULL. Busca en documentos VIGENTES Y NO VIGENTES a proposito: los 707 casos de personas con varios documentos son justamente quienes digitan el antiguo. Elimina el trabajo manual que describio el profesor Hugo Nelson en 42:01.';


--
-- TOC entry 382 (class 1255 OID 87634)
-- Name: fn_resolver_programa(text, real); Type: FUNCTION; Schema: catedras; Owner: -
--

CREATE FUNCTION catedras.fn_resolver_programa(p_texto text, p_umbral real DEFAULT 0.45) RETURNS character varying
    LANGUAGE plpgsql STABLE
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_norm   text := fn_normalizar(p_texto);
    v_codigo varchar(5);
BEGIN
    IF v_norm IS NULL OR v_norm = '' THEN RETURN NULL; END IF;

    -- 1 · el codigo ya viene bien escrito
    SELECT codigo INTO v_codigo FROM programa_academico
     WHERE upper(trim(p_texto)) = codigo;
    IF v_codigo IS NOT NULL THEN RETURN v_codigo; END IF;

    -- 2 · alias exacto ya conocido
    SELECT fk_programa INTO v_codigo FROM alias_programa
     WHERE texto_normalizado = v_norm;
    IF v_codigo IS NOT NULL THEN RETURN v_codigo; END IF;

    -- 3 · nombre exacto del programa
    SELECT codigo INTO v_codigo FROM programa_academico
     WHERE fn_normalizar(nombre) = v_norm;
    IF v_codigo IS NOT NULL THEN RETURN v_codigo; END IF;

    -- 4 · similitud por trigramas, con umbral
    SELECT codigo INTO v_codigo
      FROM programa_academico
     WHERE similarity(fn_normalizar(nombre), v_norm) >= p_umbral
     ORDER BY similarity(fn_normalizar(nombre), v_norm) DESC
     LIMIT 1;

    RETURN v_codigo;
END;
$$;


--
-- TOC entry 5945 (class 0 OID 0)
-- Dependencies: 382
-- Name: FUNCTION fn_resolver_programa(p_texto text, p_umbral real); Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON FUNCTION catedras.fn_resolver_programa(p_texto text, p_umbral real) IS 'Resuelve el texto libre del formulario contra el catalogo. Cuatro pasos, del mas seguro al mas arriesgado. El paso 4 puede equivocarse: por eso devuelve NULL antes que adivinar por debajo del umbral, y lo que quede sin resolver se cuenta y se revisa a mano.';


--
-- TOC entry 332 (class 1255 OID 87635)
-- Name: fn_responder_encuesta_api(bigint, jsonb); Type: FUNCTION; Schema: catedras; Owner: -
--

CREATE FUNCTION catedras.fn_responder_encuesta_api(p_id_registro bigint, p_respuestas jsonb) RETURNS bigint
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_respuesta bigint;
BEGIN
    CALL catedras.sp_responder_encuesta(p_id_registro, p_respuestas, v_respuesta);
    RETURN v_respuesta;
END;
$$;


--
-- TOC entry 489 (class 1255 OID 87636)
-- Name: fn_siguiente_reunion(character); Type: FUNCTION; Schema: catedras; Owner: -
--

CREATE FUNCTION catedras.fn_siguiente_reunion(p_id_evento_asis character) RETURNS smallint
    LANGUAGE sql STABLE
    SET search_path TO 'catedras', 'public'
    AS $$
    SELECT coalesce(max(s.numero_reunion), 0)::smallint + 1
      FROM catedra c
      LEFT JOIN sesion s ON s.fk_catedra = c.id_catedra
     WHERE c.id_evento_asis = p_id_evento_asis;
$$;


--
-- TOC entry 5946 (class 0 OID 0)
-- Dependencies: 489
-- Name: FUNCTION fn_siguiente_reunion(p_id_evento_asis character); Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON FUNCTION catedras.fn_siguiente_reunion(p_id_evento_asis character) IS 'I8. Sustituye el paso 2 del manual de migracion: la busqueda manual de la ultima reunion en un listado de 5.711 filas.';


--
-- TOC entry 442 (class 1255 OID 87637)
-- Name: fn_solicitar_clave_api(text, uuid, inet); Type: FUNCTION; Schema: catedras; Owner: -
--

CREATE FUNCTION catedras.fn_solicitar_clave_api(p_identificador text, p_token uuid, p_ip inet DEFAULT NULL::inet) RETURNS TABLE(id_clave bigint, clave text, enviado_a text)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_id     bigint;
    v_clave  text;
    v_correo text;
BEGIN
    CALL catedras.sp_solicitar_clave(p_identificador, p_token, p_ip, v_id, v_clave, v_correo);
    RETURN QUERY SELECT v_id, v_clave, v_correo;
END;
$$;


--
-- TOC entry 5947 (class 0 OID 0)
-- Dependencies: 442
-- Name: FUNCTION fn_solicitar_clave_api(p_identificador text, p_token uuid, p_ip inet); Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON FUNCTION catedras.fn_solicitar_clave_api(p_identificador text, p_token uuid, p_ip inet) IS 'Envoltorio de sp_solicitar_clave para la API. RN-17.';


--
-- TOC entry 454 (class 1255 OID 87638)
-- Name: fn_trg_bitacora(); Type: FUNCTION; Schema: catedras; Owner: -
--

CREATE FUNCTION catedras.fn_trg_bitacora() RETURNS trigger
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_antes   jsonb;
    v_despues jsonb;
    v_llave   text;
BEGIN
    -- Se convierte a jsonb PRIMERO y la llave se extrae de ahi.
    -- No se usa CASE sobre NEW.<columna> porque PL/pgSQL planifica la
    -- expresion COMPLETA contra el tipo de registro que dispara: una rama
    -- que nombre una columna de otra tabla revienta aunque no se ejecute.
    IF TG_OP IN ('UPDATE', 'DELETE') THEN v_antes   := to_jsonb(OLD); END IF;
    IF TG_OP IN ('INSERT', 'UPDATE') THEN v_despues := to_jsonb(NEW); END IF;

    v_llave := coalesce(v_despues, v_antes) ->> ('id_' || replace(TG_TABLE_NAME, '_asistencia', ''));

    INSERT INTO bitacora (nombre_tabla, operacion, llave, datos_antes, datos_despues)
    VALUES (TG_TABLE_NAME, TG_OP, v_llave, v_antes, v_despues);

    RETURN CASE WHEN TG_OP = 'DELETE' THEN OLD ELSE NEW END;
END;
$$;


--
-- TOC entry 544 (class 1255 OID 87639)
-- Name: fn_trg_clave_derivar(); Type: FUNCTION; Schema: catedras; Owner: -
--

CREATE FUNCTION catedras.fn_trg_clave_derivar() RETURNS trigger
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_dominios text;
BEGIN
    SELECT valor INTO v_dominios
      FROM parametro WHERE clave = 'DOMINIOS_INSTITUCIONALES';

    v_dominios := coalesce(v_dominios, 'usbmed.edu.co,usb.edu.co');

    NEW.es_correo_institucional := EXISTS (
        SELECT 1
          FROM unnest(string_to_array(v_dominios, ',')) AS d(dom)
         WHERE lower(NEW.enviado_a::text) LIKE '%@' || lower(trim(d.dom))
    );
    RETURN NEW;
END;
$$;


--
-- TOC entry 302 (class 1255 OID 87640)
-- Name: fn_trg_enlace_canal(); Type: FUNCTION; Schema: catedras; Owner: -
--

CREATE FUNCTION catedras.fn_trg_enlace_canal() RETURNS trigger
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_canal_esperado varchar(10);
BEGIN
    SELECT m.canal_difusion
      INTO v_canal_esperado
      FROM sesion s
      JOIN modalidad m ON m.id_modalidad = s.fk_modalidad
     WHERE s.id_sesion = NEW.fk_sesion;

    IF v_canal_esperado IS DISTINCT FROM 'AMBOS'
       AND NEW.canal IS DISTINCT FROM v_canal_esperado THEN
        RAISE EXCEPTION
            'RN-13: la sesion % es de modalidad que se difunde por %, no por %',
            NEW.fk_sesion, v_canal_esperado, NEW.canal
            USING ERRCODE = 'check_violation';
    END IF;
    RETURN NEW;
END;
$$;


--
-- TOC entry 490 (class 1255 OID 87641)
-- Name: fn_trg_registro_validar(); Type: FUNCTION; Schema: catedras; Owner: -
--

CREATE FUNCTION catedras.fn_trg_registro_validar() RETURNS trigger
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_ventana        tstzrange;
    v_revocado       timestamptz;
    v_cupo           integer;
    v_registrados    integer;
    v_exige_programa boolean;
BEGIN
    ------------------------------------------------------------------ RN-15 --
    IF NEW.fk_enlace IS NOT NULL AND NEW.origen NOT IN ('MANUAL', 'IMPORTADO') THEN
        SELECT e.ventana, e.revocado_en
          INTO v_ventana, v_revocado
          FROM enlace_registro e
         WHERE e.id_enlace = NEW.fk_enlace;

        IF v_revocado IS NOT NULL THEN
            RAISE EXCEPTION 'RN-15: el enlace % esta revocado', NEW.fk_enlace
                USING ERRCODE = 'check_violation';
        END IF;

        IF NOT (v_ventana @> NEW.registrado_en) THEN
            RAISE EXCEPTION
                'RN-15: % esta fuera de la ventana % del enlace %',
                NEW.registrado_en, v_ventana, NEW.fk_enlace
                USING ERRCODE = 'check_violation';
        END IF;
    END IF;

    ------------------------------------------------------------------ RN-22 --
    -- Instantanea de la VINCULACION vigente
    IF NEW.fk_vinculacion_snapshot IS NULL THEN
        SELECT v.fk_tipo_vinculacion
          INTO NEW.fk_vinculacion_snapshot
          FROM vinculacion_asistente v
         WHERE v.fk_asistente = NEW.fk_asistente
           AND v.fecha_fin IS NULL
         ORDER BY v.fecha_ini DESC
         LIMIT 1;

        IF NEW.fk_vinculacion_snapshot IS NULL THEN
            RAISE EXCEPTION
                'El asistente % no tiene ninguna vinculacion vigente',
                NEW.fk_asistente USING ERRCODE = 'check_violation';
        END IF;
    END IF;

    SELECT tv.exige_programa INTO v_exige_programa
      FROM tipo_vinculacion tv
     WHERE tv.id_tipo_vinculacion = NEW.fk_vinculacion_snapshot;

    -- Instantanea del PROGRAMA vigente, solo si la vinculacion lo exige
    IF NEW.fk_programa_snapshot IS NULL AND v_exige_programa THEN
        SELECT pa.fk_programa
          INTO NEW.fk_programa_snapshot
          FROM programa_asistente pa
          JOIN periodo_academico p ON p.id_periodo = pa.fk_periodo
         WHERE pa.fk_asistente = NEW.fk_asistente
           AND pa.estado = 'ACTIVO'
         ORDER BY p.fecha_ini DESC
         LIMIT 1;
    END IF;

    ------------------------------------------------------------------ RN-07 --
    IF v_exige_programa AND NEW.fk_programa_snapshot IS NULL THEN
        RAISE EXCEPTION
            'RN-07: la vinculacion % exige programa academico y el asistente % no tiene ninguno activo',
            NEW.fk_vinculacion_snapshot, NEW.fk_asistente
            USING ERRCODE = 'check_violation';
    END IF;

    IF NOT v_exige_programa THEN
        -- Rutas de Paz y similares entran SIN programa, a proposito
        NEW.fk_programa_snapshot := NULL;
    END IF;

    ------------------------------------------------------------------ RN-38 --
    SELECT s.cupo INTO v_cupo FROM sesion s WHERE s.id_sesion = NEW.fk_sesion FOR UPDATE;

    IF v_cupo IS NOT NULL THEN
        SELECT count(*) INTO v_registrados
          FROM registro_asistencia r WHERE r.fk_sesion = NEW.fk_sesion;

        IF v_registrados >= v_cupo THEN
            RAISE EXCEPTION 'RN-38: la sesion % alcanzo su cupo de %',
                NEW.fk_sesion, v_cupo USING ERRCODE = 'check_violation';
        END IF;
    END IF;

    RETURN NEW;
END;
$$;


--
-- TOC entry 5948 (class 0 OID 0)
-- Dependencies: 490
-- Name: FUNCTION fn_trg_registro_validar(); Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON FUNCTION catedras.fn_trg_registro_validar() IS 'Cuatro reglas en un disparador porque las cuatro se comprueban en el mismo instante y sobre la misma fila. El FOR UPDATE sobre la sesion serializa el control de cupo: sin el, dos registros simultaneos podrian pasarse.';


--
-- TOC entry 616 (class 1255 OID 87642)
-- Name: fn_trg_respuesta_item_tipo(); Type: FUNCTION; Schema: catedras; Owner: -
--

CREATE FUNCTION catedras.fn_trg_respuesta_item_tipo() RETURNS trigger
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    t record;
BEGIN
    SELECT tp.id_tipo_pregunta, tp.guarda_numero, tp.guarda_texto, tp.guarda_opcion,
           p.valor_min, p.valor_max
      INTO t
      FROM pregunta p
      JOIN tipo_pregunta tp ON tp.id_tipo_pregunta = p.fk_tipo_pregunta
     WHERE p.id_pregunta = NEW.fk_pregunta;

    IF t.guarda_numero AND NEW.valor_numerico IS NULL THEN
        RAISE EXCEPTION 'RN-25: la pregunta % es de tipo % y espera valor_numerico',
            NEW.fk_pregunta, t.id_tipo_pregunta USING ERRCODE = 'check_violation';
    ELSIF t.guarda_texto AND NEW.valor_texto IS NULL THEN
        RAISE EXCEPTION 'RN-25: la pregunta % es de tipo % y espera valor_texto',
            NEW.fk_pregunta, t.id_tipo_pregunta USING ERRCODE = 'check_violation';
    ELSIF t.guarda_opcion AND NEW.fk_opcion IS NULL THEN
        RAISE EXCEPTION 'RN-25: la pregunta % es de tipo % y espera fk_opcion',
            NEW.fk_pregunta, t.id_tipo_pregunta USING ERRCODE = 'check_violation';
    END IF;

    -- El rango declarado de la pregunta
    IF NEW.valor_numerico IS NOT NULL AND t.valor_min IS NOT NULL
       AND NEW.valor_numerico NOT BETWEEN t.valor_min AND t.valor_max THEN
        RAISE EXCEPTION 'RN-25: % esta fuera del rango [%, %] de la pregunta %',
            NEW.valor_numerico, t.valor_min, t.valor_max, NEW.fk_pregunta
            USING ERRCODE = 'check_violation';
    END IF;

    -- RN-24 · la opcion elegida pertenece a ESA pregunta
    IF NEW.fk_opcion IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM opcion_pregunta o
                        WHERE o.id_opcion = NEW.fk_opcion
                          AND o.fk_pregunta = NEW.fk_pregunta) THEN
        RAISE EXCEPTION 'RN-24: la opcion % no pertenece a la pregunta %',
            NEW.fk_opcion, NEW.fk_pregunta USING ERRCODE = 'check_violation';
    END IF;

    RETURN NEW;
END;
$$;


--
-- TOC entry 469 (class 1255 OID 87643)
-- Name: fn_trg_sesion_consecutivo(); Type: FUNCTION; Schema: catedras; Owner: -
--

CREATE FUNCTION catedras.fn_trg_sesion_consecutivo() RETURNS trigger
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    IF NEW.numero_reunion IS NULL OR NEW.numero_reunion = 0 THEN
        -- El bloqueo de la catedra serializa dos altas simultaneas
        PERFORM 1 FROM catedra WHERE id_catedra = NEW.fk_catedra FOR UPDATE;

        SELECT coalesce(max(s.numero_reunion), 0) + 1
          INTO NEW.numero_reunion
          FROM sesion s
         WHERE s.fk_catedra = NEW.fk_catedra;
    END IF;
    RETURN NEW;
END;
$$;


--
-- TOC entry 5949 (class 0 OID 0)
-- Dependencies: 469
-- Name: FUNCTION fn_trg_sesion_consecutivo(); Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON FUNCTION catedras.fn_trg_sesion_consecutivo() IS 'RN-10. PROPONE el siguiente consecutivo de la catedra cuando no se indica. El FOR UPDATE serializa dos altas simultaneas: sin el, uq_sesion_reunion rechazaria una de las dos con un error feo en vez de ordenarlas. NO sustituye al numero del ASIS: para las catedras que ya existen alli, la autoridad es numero_reunion_asis. Ver el comentario de arriba y §8.3.';


--
-- TOC entry 497 (class 1255 OID 87644)
-- Name: fn_validar_clave_api(bigint, text, inet, text); Type: FUNCTION; Schema: catedras; Owner: -
--

CREATE FUNCTION catedras.fn_validar_clave_api(p_id_clave bigint, p_clave text, p_ip inet DEFAULT NULL::inet, p_user_agent text DEFAULT NULL::text) RETURNS bigint
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_registro bigint;
BEGIN
    CALL catedras.sp_validar_clave_y_registrar(p_id_clave, p_clave, p_ip, p_user_agent, v_registro);
    RETURN v_registro;
END;
$$;


--
-- TOC entry 320 (class 1255 OID 87645)
-- Name: sp_cerrar_encuesta(bigint); Type: PROCEDURE; Schema: catedras; Owner: -
--

CREATE PROCEDURE catedras.sp_cerrar_encuesta(IN p_id_respuesta bigint)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_faltan text;
BEGIN
    SELECT string_agg(p.orden::text, ', ' ORDER BY p.orden)
      INTO v_faltan
      FROM respuesta_encuesta re
      JOIN pregunta p ON p.fk_encuesta = re.fk_encuesta AND p.obligatoria
     WHERE re.id_respuesta = p_id_respuesta
       AND NOT EXISTS (SELECT 1 FROM respuesta_item ri
                        WHERE ri.fk_respuesta = re.id_respuesta
                          AND ri.fk_pregunta  = p.id_pregunta);

    IF v_faltan IS NOT NULL THEN
        RAISE EXCEPTION 'RN-26: faltan por responder las preguntas obligatorias %', v_faltan
            USING ERRCODE = 'check_violation';
    END IF;

    UPDATE respuesta_encuesta SET completa = true WHERE id_respuesta = p_id_respuesta;
END;
$$;


--
-- TOC entry 5950 (class 0 OID 0)
-- Dependencies: 320
-- Name: PROCEDURE sp_cerrar_encuesta(IN p_id_respuesta bigint); Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON PROCEDURE catedras.sp_cerrar_encuesta(IN p_id_respuesta bigint) IS 'RN-26. No es "al menos uno": es "uno por cada pregunta marcada obligatoria", y eso depende del contenido de otra tabla que ademas cambia con cada version de la encuesta. Ninguna restriccion declarativa puede expresarlo.';


--
-- TOC entry 500 (class 1255 OID 87646)
-- Name: sp_emitir_enlace(bigint, integer, integer, bigint, bigint); Type: PROCEDURE; Schema: catedras; Owner: -
--

CREATE PROCEDURE catedras.sp_emitir_enlace(IN p_id_sesion bigint, IN p_minutos_antes integer DEFAULT NULL::integer, IN p_minutos_despues integer DEFAULT NULL::integer, IN p_usuario bigint DEFAULT NULL::bigint, INOUT p_id_enlace bigint DEFAULT NULL::bigint)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_ini    timestamptz;
    v_fin    timestamptz;
    v_canal  varchar(10);
    v_antes  integer;
    v_desp   integer;
    v_token  uuid := gen_random_uuid();
    v_base   text;
BEGIN
    SELECT s.inicio, s.fin, m.canal_difusion
      INTO v_ini, v_fin, v_canal
      FROM sesion s
      JOIN modalidad m ON m.id_modalidad = s.fk_modalidad
     WHERE s.id_sesion = p_id_sesion;

    IF v_ini IS NULL THEN
        RAISE EXCEPTION 'No existe la sesion %', p_id_sesion;
    END IF;

    v_antes := coalesce(p_minutos_antes,
                        (SELECT valor::int FROM parametro WHERE clave = 'VENTANA_MINUTOS_ANTES'), 30);
    v_desp  := coalesce(p_minutos_despues,
                        (SELECT valor::int FROM parametro WHERE clave = 'VENTANA_MINUTOS_DESPUES'), 30);
    v_base  := coalesce((SELECT valor FROM parametro WHERE clave = 'URL_BASE'),
                        'https://catedras.usbmed.edu.co/r/');

    IF v_canal = 'AMBOS' THEN v_canal := 'QR'; END IF;

    -- RN-14 · se revoca el enlace vigente anterior, si lo hay
    UPDATE enlace_registro
       SET revocado_en = now()
     WHERE fk_sesion = p_id_sesion AND revocado_en IS NULL;

    INSERT INTO enlace_registro (fk_sesion, token, url_publica, ruta_imagen_qr,
                                 canal, ventana, fk_usuario_crea)
    VALUES (p_id_sesion, v_token, v_base || v_token::text,
            'qr/' || v_token::text || '.png', v_canal,
            tstzrange(v_ini - make_interval(mins => v_antes),
                      v_fin + make_interval(mins => v_desp), '[)'),
            p_usuario)
    RETURNING id_enlace INTO p_id_enlace;
END;
$$;


--
-- TOC entry 5951 (class 0 OID 0)
-- Dependencies: 500
-- Name: PROCEDURE sp_emitir_enlace(IN p_id_sesion bigint, IN p_minutos_antes integer, IN p_minutos_despues integer, IN p_usuario bigint, INOUT p_id_enlace bigint); Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON PROCEDURE catedras.sp_emitir_enlace(IN p_id_sesion bigint, IN p_minutos_antes integer, IN p_minutos_despues integer, IN p_usuario bigint, INOUT p_id_enlace bigint) IS 'El boton del .docx: "el administrador toca un boton y genera el QR y URL del registro de la catedra especifica". La ventana sale de los parametros, que es lo que pide "el administrador configura hora de inicio y hora de fin".';


--
-- TOC entry 468 (class 1255 OID 87647)
-- Name: sp_generar_lote_migracion(bigint, bigint, bigint); Type: PROCEDURE; Schema: catedras; Owner: -
--

CREATE PROCEDURE catedras.sp_generar_lote_migracion(IN p_id_sesion bigint, IN p_usuario bigint DEFAULT NULL::bigint, INOUT p_id_lote bigint DEFAULT NULL::bigint)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_filas integer;
    v_nombre text;
BEGIN
    SELECT count(*) INTO v_filas FROM fn_exportar_asis(p_id_sesion);

    IF v_filas = 0 THEN
        RAISE EXCEPTION
            'No hay registros migrables en la sesion %. Revise I10: asistentes sin ID o sin programa',
            p_id_sesion USING ERRCODE = 'check_violation';
    END IF;

    SELECT 'ME_CATEDRA_' || c.id_evento_asis || '_R' || s.numero_reunion || '.xlsx'
      INTO v_nombre
      FROM sesion s JOIN catedra c ON c.id_catedra = s.fk_catedra
     WHERE s.id_sesion = p_id_sesion;

    INSERT INTO lote_migracion (fk_sesion, nombre_archivo, fk_usuario,
                                fk_estado_proceso, total_filas)
    VALUES (p_id_sesion, v_nombre, p_usuario, 'GENERADO', v_filas)
    RETURNING id_lote_migracion INTO p_id_lote;

    INSERT INTO detalle_migracion (fk_lote_migracion, fk_registro, id_asis_enviado,
                                   programa_enviado, reunion_enviada)
    SELECT p_id_lote, r.id_registro, a.id_asis, r.fk_programa_snapshot, s.numero_reunion
      FROM registro_asistencia r
      JOIN sesion    s ON s.id_sesion    = r.fk_sesion
      JOIN asistente a ON a.id_asistente = r.fk_asistente
     WHERE r.fk_sesion = p_id_sesion
       AND a.id_asis IS NOT NULL
       AND r.fk_programa_snapshot IS NOT NULL
       AND a.es_externo = false;

    UPDATE sesion SET estado = 'MIGRADA' WHERE id_sesion = p_id_sesion;
END;
$$;


--
-- TOC entry 465 (class 1255 OID 87648)
-- Name: sp_responder_encuesta(bigint, jsonb, bigint); Type: PROCEDURE; Schema: catedras; Owner: -
--

CREATE PROCEDURE catedras.sp_responder_encuesta(IN p_id_registro bigint, IN p_respuestas jsonb, INOUT p_id_respuesta bigint DEFAULT NULL::bigint)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_encuesta bigint;
    v_items    integer;
BEGIN
    SELECT s.fk_encuesta INTO v_encuesta
      FROM registro_asistencia r
      JOIN sesion s ON s.id_sesion = r.fk_sesion
     WHERE r.id_registro = p_id_registro;

    IF v_encuesta IS NULL THEN
        RAISE EXCEPTION 'La sesion de ese registro no tiene encuesta asignada'
            USING ERRCODE = 'check_violation';
    END IF;

    IF jsonb_array_length(p_respuestas) = 0 THEN
        RAISE EXCEPTION 'R29: una encuesta respondida debe tener al menos un item'
            USING ERRCODE = 'check_violation';
    END IF;

    INSERT INTO respuesta_encuesta (fk_registro, fk_encuesta)
    VALUES (p_id_registro, v_encuesta)
    RETURNING id_respuesta INTO p_id_respuesta;

    INSERT INTO respuesta_item (fk_respuesta, fk_pregunta, valor_numerico, valor_texto, fk_opcion)
    SELECT p_id_respuesta,
           (e ->> 'pregunta')::bigint,
           (e ->> 'numero')::smallint,
            e ->> 'texto',
           (e ->> 'opcion')::bigint
      FROM jsonb_array_elements(p_respuestas) AS e;

    GET DIAGNOSTICS v_items = ROW_COUNT;

    CALL sp_cerrar_encuesta(p_id_respuesta);
END;
$$;


--
-- TOC entry 606 (class 1255 OID 87649)
-- Name: sp_solicitar_clave(text, uuid, inet, bigint, text, text); Type: PROCEDURE; Schema: catedras; Owner: -
--

CREATE PROCEDURE catedras.sp_solicitar_clave(IN p_identificador text, IN p_token uuid, IN p_ip inet DEFAULT NULL::inet, INOUT p_id_clave bigint DEFAULT NULL::bigint, INOUT p_clave text DEFAULT NULL::text, INOUT p_enviado_a text DEFAULT NULL::text)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_asistente bigint;
    v_enlace    bigint;
    v_vigencia  integer;
    v_longitud  integer;
BEGIN
    -- RN-01 y RN-06 · solo entra quien esta en la tabla
    v_asistente := fn_resolver_asistente(p_identificador);
    IF v_asistente IS NULL THEN
        RAISE EXCEPTION
            'RN-01: no hay ningun asistente registrado con el identificador %', p_identificador
            USING ERRCODE = 'no_data_found';
    END IF;

    SELECT id_enlace INTO v_enlace
      FROM enlace_registro
     WHERE token = p_token AND revocado_en IS NULL AND ventana @> now();

    IF v_enlace IS NULL THEN
        RAISE EXCEPTION 'RN-15: el enlace no existe, esta revocado o esta fuera de su ventana'
            USING ERRCODE = 'check_violation';
    END IF;

    v_vigencia := coalesce((SELECT valor::int FROM parametro WHERE clave = 'CLAVE_VIGENCIA_MINUTOS'), 10);
    v_longitud := coalesce((SELECT valor::int FROM parametro WHERE clave = 'CLAVE_LONGITUD'), 6);

    -- La clave en claro se devuelve para enviarla por correo y NO se guarda
    p_clave := lpad((floor(random() * (10::numeric ^ v_longitud)))::bigint::text, v_longitud, '0');

    -- RN-19 · a que correo: institucional si existe, si no el personal
    SELECT coalesce(correo_institucional, correo_personal)::text
      INTO p_enviado_a
      FROM asistente WHERE id_asistente = v_asistente;

    INSERT INTO clave_acceso (fk_asistente, fk_enlace, clave_hash, enviado_a,
                              es_correo_institucional, expira_en, ip_solicitud)
    VALUES (v_asistente, v_enlace,
            digest(p_clave || p_enviado_a, 'sha256'),   -- RN-34, con sal
            p_enviado_a::citext,
            false,                                       -- lo deriva el disparador
            now() + make_interval(mins => v_vigencia),
            p_ip)
    RETURNING id_clave INTO p_id_clave;
END;
$$;


--
-- TOC entry 5952 (class 0 OID 0)
-- Dependencies: 606
-- Name: PROCEDURE sp_solicitar_clave(IN p_identificador text, IN p_token uuid, IN p_ip inet, INOUT p_id_clave bigint, INOUT p_clave text, INOUT p_enviado_a text); Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON PROCEDURE catedras.sp_solicitar_clave(IN p_identificador text, IN p_token uuid, IN p_ip inet, INOUT p_id_clave bigint, INOUT p_clave text, INOUT p_enviado_a text) IS 'RN-17. La razon de existir de esta clave no es autenticar, es impedir que uno registre a otro: "yo me puedo registrar por cualquier persona, por eso tiene que ser con el correo" (1:04:51). La clave en claro se DEVUELVE para que la aplicacion la envie, y nunca se almacena.';


--
-- TOC entry 436 (class 1255 OID 87650)
-- Name: sp_validar_clave_y_registrar(bigint, text, inet, text, bigint); Type: PROCEDURE; Schema: catedras; Owner: -
--

CREATE PROCEDURE catedras.sp_validar_clave_y_registrar(IN p_id_clave bigint, IN p_clave text, IN p_ip inet DEFAULT NULL::inet, IN p_user_agent text DEFAULT NULL::text, INOUT p_id_registro bigint DEFAULT NULL::bigint)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    c              record;
    v_max_intentos integer;
    v_sesion       bigint;
    v_canal        varchar(10);
BEGIN
    SELECT * INTO c FROM clave_acceso WHERE id_clave = p_id_clave FOR UPDATE;

    IF c IS NULL THEN
        RAISE EXCEPTION 'No existe la clave %', p_id_clave USING ERRCODE = 'no_data_found';
    END IF;

    -- RN-20 · una clave usada no se reutiliza
    IF c.usado_en IS NOT NULL THEN
        RAISE EXCEPTION 'RN-20: esa clave ya se uso el %', c.usado_en
            USING ERRCODE = 'check_violation';
    END IF;

    IF now() > c.expira_en THEN
        RAISE EXCEPTION 'RN-18: la clave expiro el %', c.expira_en
            USING ERRCODE = 'check_violation';
    END IF;

    v_max_intentos := coalesce((SELECT valor::int FROM parametro WHERE clave = 'CLAVE_MAX_INTENTOS'), 3);

    IF c.clave_hash IS DISTINCT FROM digest(p_clave || c.enviado_a::text, 'sha256') THEN
        UPDATE clave_acceso SET intentos = intentos + 1 WHERE id_clave = p_id_clave;
        IF c.intentos + 1 >= v_max_intentos THEN
            UPDATE clave_acceso SET expira_en = now() WHERE id_clave = p_id_clave;
            RAISE EXCEPTION 'RN-18: se agotaron los % intentos; la clave queda anulada', v_max_intentos
                USING ERRCODE = 'check_violation';
        END IF;
        RAISE EXCEPTION 'Clave incorrecta. Intento % de %', c.intentos + 1, v_max_intentos
            USING ERRCODE = 'check_violation';
    END IF;

    SELECT e.fk_sesion, e.canal INTO v_sesion, v_canal
      FROM enlace_registro e WHERE e.id_enlace = c.fk_enlace;

    -- El disparador de registro hace el resto: ventana, instantaneas y cupo
    INSERT INTO registro_asistencia (fk_sesion, fk_asistente, fk_enlace, fk_clave,
                                     fk_vinculacion_snapshot, origen, ip, user_agent)
    VALUES (v_sesion, c.fk_asistente, c.fk_enlace, p_id_clave,
            NULL, v_canal, p_ip, left(p_user_agent, 300))
    RETURNING id_registro INTO p_id_registro;

    UPDATE clave_acceso SET usado_en = now() WHERE id_clave = p_id_clave;
END;
$$;


SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- TOC entry 223 (class 1259 OID 87651)
-- Name: alias_programa; Type: TABLE; Schema: catedras; Owner: -
--

CREATE TABLE catedras.alias_programa (
    id_alias bigint NOT NULL,
    texto_normalizado character varying(200) NOT NULL,
    fk_programa character varying(5) NOT NULL,
    origen character varying(30) DEFAULT 'FORMULARIO'::character varying NOT NULL,
    creado_en timestamp with time zone DEFAULT now() NOT NULL
);


--
-- TOC entry 5953 (class 0 OID 0)
-- Dependencies: 223
-- Name: TABLE alias_programa; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON TABLE catedras.alias_programa IS 'Texto libre con que se ha escrito un programa, y el codigo al que corresponde. Nace de la calidad real del formulario actual, que trae "Psicologica", "Entrenamiento Deportivo.", "Ingenieria de datos y software" y una celda con la facultad y el programa en dos lineas. Es una tabla VIVA porque los formularios en texto libre van a seguir llegando durante toda la transicion.';


--
-- TOC entry 224 (class 1259 OID 87656)
-- Name: alias_programa_id_alias_seq; Type: SEQUENCE; Schema: catedras; Owner: -
--

ALTER TABLE catedras.alias_programa ALTER COLUMN id_alias ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME catedras.alias_programa_id_alias_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- TOC entry 225 (class 1259 OID 87657)
-- Name: asistente; Type: TABLE; Schema: catedras; Owner: -
--

CREATE TABLE catedras.asistente (
    id_asistente bigint NOT NULL,
    id_asis character varying(15),
    nombres character varying(100) NOT NULL,
    apellidos character varying(100) NOT NULL,
    nombre_completo_asis character varying(200),
    correo_institucional public.citext,
    correo_personal public.citext,
    celular character varying(20),
    es_externo boolean DEFAULT false NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    creado_en timestamp with time zone DEFAULT now() NOT NULL,
    fk_lote_carga bigint,
    CONSTRAINT chk_asistente_cor_inst_fmt CHECK (((correo_institucional IS NULL) OR (correo_institucional OPERATOR(public.~~) '%@%.%'::public.citext))),
    CONSTRAINT chk_asistente_cor_pers_fmt CHECK (((correo_personal IS NULL) OR (correo_personal OPERATOR(public.~~) '%@%.%'::public.citext))),
    CONSTRAINT chk_asistente_correo CHECK (((correo_institucional IS NOT NULL) OR (correo_personal IS NOT NULL))),
    CONSTRAINT chk_asistente_externo CHECK ((((es_externo = false) AND (id_asis IS NOT NULL)) OR (es_externo = true))),
    CONSTRAINT chk_asistente_idasis CHECK (((id_asis IS NULL) OR ((id_asis)::text ~ '^[0-9A-Za-z]{5,15}$'::text)))
);


--
-- TOC entry 5954 (class 0 OID 0)
-- Dependencies: 225
-- Name: TABLE asistente; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON TABLE catedras.asistente IS 'Persona que asiste a las catedras abiertas, de forma presencial o virtual. Puede ser estudiante, docente, administrativo, egresado, participante de rutas de formacion o publico externo.';


--
-- TOC entry 5955 (class 0 OID 0)
-- Dependencies: 225
-- Name: COLUMN asistente.id_asis; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON COLUMN catedras.asistente.id_asis IS 'Codigo de identificacion en el sistema ASIS. CORREGIDO respecto del archivo original: es ALFANUMERICO de hasta 15 caracteres, no 10 digitos. El maestro real trae 9.553 filas de 11 caracteres y valores como 1000513C. NULO significa que es un externo que no esta en el ASIS - decision REVISABLE, ver §3.1 del plan.';


--
-- TOC entry 5956 (class 0 OID 0)
-- Dependencies: 225
-- Name: COLUMN asistente.nombre_completo_asis; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON COLUMN catedras.asistente.nombre_completo_asis IS 'El nombre tal como llego del ASIS, sin transformar. Permite rehacer la particion en nombres y apellidos si resulta equivocada.';


--
-- TOC entry 5957 (class 0 OID 0)
-- Dependencies: 225
-- Name: COLUMN asistente.correo_personal; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON COLUMN catedras.asistente.correo_personal IS 'No es un caso raro: los participantes de Rutas de Paz NO tienen correo institucional, y es su unica via para recibir la clave.';


--
-- TOC entry 226 (class 1259 OID 87670)
-- Name: asistente_id_asistente_seq; Type: SEQUENCE; Schema: catedras; Owner: -
--

ALTER TABLE catedras.asistente ALTER COLUMN id_asistente ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME catedras.asistente_id_asistente_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- TOC entry 227 (class 1259 OID 87671)
-- Name: bitacora; Type: TABLE; Schema: catedras; Owner: -
--

CREATE TABLE catedras.bitacora (
    id_bitacora bigint NOT NULL,
    ocurrido_en timestamp with time zone DEFAULT now() NOT NULL,
    usuario_bd name DEFAULT CURRENT_USER NOT NULL,
    fk_usuario bigint,
    nombre_tabla character varying(63) NOT NULL,
    operacion character varying(10) NOT NULL,
    llave character varying(100),
    datos_antes jsonb,
    datos_despues jsonb,
    CONSTRAINT chk_bitacora_operacion CHECK (((operacion)::text = ANY (ARRAY[('INSERT'::character varying)::text, ('UPDATE'::character varying)::text, ('DELETE'::character varying)::text])))
);


--
-- TOC entry 5958 (class 0 OID 0)
-- Dependencies: 227
-- Name: TABLE bitacora; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON TABLE catedras.bitacora IS 'RN-35 · Registro de toda modificacion sobre datos sensibles, con el antes y el despues en jsonb. La alternativa - una tabla espejo por cada tabla auditada - costaria treinta y siete tablas mas.';


--
-- TOC entry 228 (class 1259 OID 87679)
-- Name: bitacora_id_bitacora_seq; Type: SEQUENCE; Schema: catedras; Owner: -
--

ALTER TABLE catedras.bitacora ALTER COLUMN id_bitacora ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME catedras.bitacora_id_bitacora_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- TOC entry 229 (class 1259 OID 87680)
-- Name: catedra; Type: TABLE; Schema: catedras; Owner: -
--

CREATE TABLE catedras.catedra (
    id_catedra bigint NOT NULL,
    id_evento_asis character(9) NOT NULL,
    nombre character varying(200) NOT NULL,
    nombre_asis character varying(30) GENERATED ALWAYS AS ("left"((nombre)::text, 30)) STORED,
    fk_tipo_evento character(4) NOT NULL,
    fk_dependencia character varying(15),
    activo boolean DEFAULT true NOT NULL,
    creado_en timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT chk_catedra_asis CHECK ((id_evento_asis ~ '^[0-9]{9}$'::text))
);


--
-- TOC entry 5959 (class 0 OID 0)
-- Dependencies: 229
-- Name: COLUMN catedra.id_evento_asis; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON COLUMN catedras.catedra.id_evento_asis IS 'Identificador del evento en el ASIS. char(9) CON CEROS A LA IZQUIERDA: 000035392. Como entero se convierte en 35392 y deja de servir para la carga.';


--
-- TOC entry 5960 (class 0 OID 0)
-- Dependencies: 229
-- Name: COLUMN catedra.nombre_asis; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON COLUMN catedras.catedra.nombre_asis IS 'DERIVADO: los 30 primeros caracteres, que es lo que acepta el ASIS. El listado real trae nombres cortados a mitad de palabra - "REMOCION DE METALES PESADOS DE", "CURSO FORMATIVO LOGICO MATEMAT". Guardando el largo y derivando el corto, no se pierde informacion. Al reves, si.';


--
-- TOC entry 230 (class 1259 OID 87687)
-- Name: catedra_id_catedra_seq; Type: SEQUENCE; Schema: catedras; Owner: -
--

ALTER TABLE catedras.catedra ALTER COLUMN id_catedra ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME catedras.catedra_id_catedra_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- TOC entry 231 (class 1259 OID 87688)
-- Name: clave_acceso; Type: TABLE; Schema: catedras; Owner: -
--

CREATE TABLE catedras.clave_acceso (
    id_clave bigint NOT NULL,
    fk_asistente bigint NOT NULL,
    fk_enlace bigint NOT NULL,
    clave_hash bytea NOT NULL,
    enviado_a public.citext NOT NULL,
    es_correo_institucional boolean NOT NULL,
    generado_en timestamp with time zone DEFAULT now() NOT NULL,
    expira_en timestamp with time zone NOT NULL,
    usado_en timestamp with time zone,
    intentos smallint DEFAULT 0 NOT NULL,
    ip_solicitud inet,
    CONSTRAINT chk_clave_correo CHECK ((enviado_a OPERATOR(public.~~) '%@%.%'::public.citext)),
    CONSTRAINT chk_clave_expira CHECK ((expira_en > generado_en)),
    CONSTRAINT chk_clave_intentos CHECK (((intentos >= 0) AND (intentos <= 10))),
    CONSTRAINT chk_clave_usada CHECK (((usado_en IS NULL) OR ((usado_en >= generado_en) AND (usado_en <= expira_en))))
);


--
-- TOC entry 5961 (class 0 OID 0)
-- Dependencies: 231
-- Name: COLUMN clave_acceso.clave_hash; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON COLUMN catedras.clave_acceso.clave_hash IS 'RN-34 · La clave CIFRADA. Nunca se guarda en claro. Se calcula con digest() de pgcrypto en el procedimiento sp_solicitar_clave.';


--
-- TOC entry 5962 (class 0 OID 0)
-- Dependencies: 231
-- Name: COLUMN clave_acceso.es_correo_institucional; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON COLUMN catedras.clave_acceso.es_correo_institucional IS 'DERIVADO del dominio de enviado_a por el disparador del script 10. Es la columna que responde al asunto que el profesor Carlos Castro dejo abierto en 55:32: "a mi no me gusta el correo que no sea institucional; dejame, yo lo resuelvo". El modelo deja el dato listo y medible (I18) para que la politica se decida con la evidencia a la vista.';


--
-- TOC entry 232 (class 1259 OID 87699)
-- Name: clave_acceso_id_clave_seq; Type: SEQUENCE; Schema: catedras; Owner: -
--

ALTER TABLE catedras.clave_acceso ALTER COLUMN id_clave ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME catedras.clave_acceso_id_clave_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- TOC entry 233 (class 1259 OID 87700)
-- Name: consentimiento_datos; Type: TABLE; Schema: catedras; Owner: -
--

CREATE TABLE catedras.consentimiento_datos (
    id_consentimiento bigint NOT NULL,
    fk_asistente bigint NOT NULL,
    version_politica character varying(20) NOT NULL,
    aceptado_en timestamp with time zone DEFAULT now() NOT NULL,
    ip inet,
    canal character varying(20) DEFAULT 'WEB'::character varying NOT NULL,
    CONSTRAINT chk_consentimiento_canal CHECK (((canal)::text = ANY (ARRAY[('WEB'::character varying)::text, ('PRESENCIAL'::character varying)::text, ('CORREO'::character varying)::text, ('IMPORTADO'::character varying)::text])))
);


--
-- TOC entry 5963 (class 0 OID 0)
-- Dependencies: 233
-- Name: TABLE consentimiento_datos; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON TABLE catedras.consentimiento_datos IS 'Ley 1581 de 2012. El sistema guarda documentos, nombres y correos personales de externos: el consentimiento es un HECHO que se registra, no un aviso en la pantalla.';


--
-- TOC entry 234 (class 1259 OID 87708)
-- Name: consentimiento_datos_id_consentimiento_seq; Type: SEQUENCE; Schema: catedras; Owner: -
--

ALTER TABLE catedras.consentimiento_datos ALTER COLUMN id_consentimiento ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME catedras.consentimiento_datos_id_consentimiento_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- TOC entry 235 (class 1259 OID 87709)
-- Name: dependencia; Type: TABLE; Schema: catedras; Owner: -
--

CREATE TABLE catedras.dependencia (
    id_dependencia character varying(15) NOT NULL,
    nombre character varying(120) NOT NULL,
    fk_dependencia_padre character varying(15),
    activo boolean DEFAULT true NOT NULL,
    CONSTRAINT chk_dependencia_padre CHECK (((fk_dependencia_padre)::text <> (id_dependencia)::text))
);


--
-- TOC entry 236 (class 1259 OID 87714)
-- Name: detalle_migracion; Type: TABLE; Schema: catedras; Owner: -
--

CREATE TABLE catedras.detalle_migracion (
    id_detalle bigint NOT NULL,
    fk_lote_migracion bigint NOT NULL,
    fk_registro bigint NOT NULL,
    id_asis_enviado character varying(15) NOT NULL,
    programa_enviado character varying(5),
    reunion_enviada smallint NOT NULL,
    resultado character varying(12) DEFAULT 'PENDIENTE'::character varying NOT NULL,
    mensaje character varying(300),
    CONSTRAINT chk_detalle_resultado CHECK (((resultado)::text = ANY (ARRAY[('PENDIENTE'::character varying)::text, ('ACEPTADO'::character varying)::text, ('RECHAZADO'::character varying)::text])))
);


--
-- TOC entry 5964 (class 0 OID 0)
-- Dependencies: 236
-- Name: TABLE detalle_migracion; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON TABLE catedras.detalle_migracion IS 'Cada fila enviada en un lote, TAL COMO SE ENVIO, y el resultado que dio.';


--
-- TOC entry 5965 (class 0 OID 0)
-- Dependencies: 236
-- Name: COLUMN detalle_migracion.id_asis_enviado; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON COLUMN catedras.detalle_migracion.id_asis_enviado IS 'COPIA LITERAL del ID que salio en el archivo. Se repite a proposito una columna que ya esta en asistente: NO SON LO MISMO. Si el dato de origen cambia despues del envio, la tabla dira una cosa y el ASIS tendra otra, y sin esta copia un rechazo es indepurable seis meses despues.';


--
-- TOC entry 5966 (class 0 OID 0)
-- Dependencies: 236
-- Name: COLUMN detalle_migracion.programa_enviado; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON COLUMN catedras.detalle_migracion.programa_enviado IS 'COPIA LITERAL del codigo de programa. NULO solo es legitimo si la vinculacion no exigia programa - y en ese caso el registro no deberia haber entrado al lote (RN-28).';


--
-- TOC entry 237 (class 1259 OID 87719)
-- Name: detalle_migracion_id_detalle_seq; Type: SEQUENCE; Schema: catedras; Owner: -
--

ALTER TABLE catedras.detalle_migracion ALTER COLUMN id_detalle ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME catedras.detalle_migracion_id_detalle_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- TOC entry 238 (class 1259 OID 87720)
-- Name: documento_asistente; Type: TABLE; Schema: catedras; Owner: -
--

CREATE TABLE catedras.documento_asistente (
    id_documento bigint NOT NULL,
    fk_asistente bigint NOT NULL,
    fk_tipo_documento character varying(5) NOT NULL,
    numero character varying(20) NOT NULL,
    vigente boolean DEFAULT true NOT NULL,
    registrado_en timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT chk_documento_numero CHECK (((numero)::text ~ '^[0-9A-Za-z-]{4,20}$'::text))
);


--
-- TOC entry 5967 (class 0 OID 0)
-- Dependencies: 238
-- Name: TABLE documento_asistente; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON TABLE catedras.documento_asistente IS 'Numero de documento asociado a un asistente. Una persona puede tener varios a lo largo del tiempo; solo uno esta vigente (RN-05, indice parcial en el script 09). Sirve para resolver el ID cuando la persona no lo recuerda y digita la cedula.';


--
-- TOC entry 239 (class 1259 OID 87726)
-- Name: documento_asistente_id_documento_seq; Type: SEQUENCE; Schema: catedras; Owner: -
--

ALTER TABLE catedras.documento_asistente ALTER COLUMN id_documento ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME catedras.documento_asistente_id_documento_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- TOC entry 240 (class 1259 OID 87727)
-- Name: encuesta; Type: TABLE; Schema: catedras; Owner: -
--

CREATE TABLE catedras.encuesta (
    id_encuesta bigint NOT NULL,
    nombre character varying(120) NOT NULL,
    version smallint DEFAULT 1 NOT NULL,
    vigente_desde date DEFAULT CURRENT_DATE NOT NULL,
    vigente_hasta date,
    activa boolean DEFAULT true NOT NULL,
    CONSTRAINT chk_encuesta_ver CHECK ((version >= 1)),
    CONSTRAINT chk_encuesta_vig CHECK (((vigente_hasta IS NULL) OR (vigente_hasta >= vigente_desde)))
);


--
-- TOC entry 5968 (class 0 OID 0)
-- Dependencies: 240
-- Name: TABLE encuesta; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON TABLE catedras.encuesta IS 'Cuestionario de calidad en una version concreta. Se versiona porque con columnas fijas, cambiar una pregunta romperia la comparabilidad historica: las respuestas viejas quedarian bajo un enunciado que no es el que se pregunto.';


--
-- TOC entry 241 (class 1259 OID 87735)
-- Name: encuesta_id_encuesta_seq; Type: SEQUENCE; Schema: catedras; Owner: -
--

ALTER TABLE catedras.encuesta ALTER COLUMN id_encuesta ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME catedras.encuesta_id_encuesta_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- TOC entry 242 (class 1259 OID 87736)
-- Name: enlace_registro; Type: TABLE; Schema: catedras; Owner: -
--

CREATE TABLE catedras.enlace_registro (
    id_enlace bigint NOT NULL,
    fk_sesion bigint NOT NULL,
    token uuid DEFAULT gen_random_uuid() NOT NULL,
    url_publica character varying(300) NOT NULL,
    ruta_imagen_qr character varying(300),
    canal character varying(10) NOT NULL,
    ventana tstzrange NOT NULL,
    usos_maximos integer,
    revocado_en timestamp with time zone,
    fk_usuario_crea bigint,
    creado_en timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT chk_enlace_canal CHECK (((canal)::text = ANY (ARRAY[('QR'::character varying)::text, ('ENLACE'::character varying)::text]))),
    CONSTRAINT chk_enlace_ruta_qr CHECK (((ruta_imagen_qr IS NULL) OR ((ruta_imagen_qr)::text !~ '^([A-Za-z]:|/|\\\\)'::text))),
    CONSTRAINT chk_enlace_usos CHECK (((usos_maximos IS NULL) OR (usos_maximos > 0))),
    CONSTRAINT chk_enlace_ventana CHECK (((NOT isempty(ventana)) AND (lower(ventana) IS NOT NULL) AND (upper(ventana) IS NOT NULL)))
);


--
-- TOC entry 5969 (class 0 OID 0)
-- Dependencies: 242
-- Name: TABLE enlace_registro; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON TABLE catedras.enlace_registro IS 'Punto de entrada al registro de una sesion, materializado como QR o como URL. El .docx exige que "el QR y URL no pueden ser reutilizables" y que tengan "hora de inicio y hora de cierre" configurables.';


--
-- TOC entry 5970 (class 0 OID 0)
-- Dependencies: 242
-- Name: COLUMN enlace_registro.canal; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON COLUMN catedras.enlace_registro.canal IS 'QR o ENLACE. Debe concordar con la modalidad de la sesion (RN-13).';


--
-- TOC entry 5971 (class 0 OID 0)
-- Dependencies: 242
-- Name: COLUMN enlace_registro.ventana; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON COLUMN catedras.enlace_registro.ventana IS 'Apertura y cierre EN UN SOLO ATRIBUTO de rango. Con dos columnas sueltas, ni el EXCLUDE de RN-36 ni la contencion de RN-15 serian declarativos: los dos serian codigo que alguien tendria que mantener.';


--
-- TOC entry 243 (class 1259 OID 87747)
-- Name: enlace_registro_id_enlace_seq; Type: SEQUENCE; Schema: catedras; Owner: -
--

ALTER TABLE catedras.enlace_registro ALTER COLUMN id_enlace ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME catedras.enlace_registro_id_enlace_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- TOC entry 244 (class 1259 OID 87748)
-- Name: estado_proceso; Type: TABLE; Schema: catedras; Owner: -
--

CREATE TABLE catedras.estado_proceso (
    id_estado character varying(12) NOT NULL,
    nombre character varying(40) NOT NULL,
    es_final boolean DEFAULT false NOT NULL,
    es_exitoso boolean DEFAULT false NOT NULL,
    CONSTRAINT chk_estado_coherente CHECK (((es_exitoso = false) OR (es_final = true)))
);


--
-- TOC entry 5972 (class 0 OID 0)
-- Dependencies: 244
-- Name: TABLE estado_proceso; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON TABLE catedras.estado_proceso IS 'Los cinco estados del Monitor Procesos del ASIS, mas GENERADO para el momento en que el archivo existe pero todavia no se ha subido.';


--
-- TOC entry 245 (class 1259 OID 87754)
-- Name: facultad; Type: TABLE; Schema: catedras; Owner: -
--

CREATE TABLE catedras.facultad (
    id_facultad character varying(10) NOT NULL,
    nombre character varying(120) NOT NULL,
    fk_facultad_padre character varying(10),
    activo boolean DEFAULT true NOT NULL,
    CONSTRAINT chk_facultad_padre CHECK (((fk_facultad_padre)::text <> (id_facultad)::text))
);


--
-- TOC entry 5973 (class 0 OID 0)
-- Dependencies: 245
-- Name: TABLE facultad; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON TABLE catedras.facultad IS 'Unidad academica que agrupa programas. La referencia reflexiva produce un ARBOL - cada nodo tiene un solo padre -, no un grafo. Por eso se resuelve con una columna en la misma tabla y se consulta con WITH RECURSIVE.';


--
-- TOC entry 246 (class 1259 OID 87759)
-- Name: lote_carga_asistente; Type: TABLE; Schema: catedras; Owner: -
--

CREATE TABLE catedras.lote_carga_asistente (
    id_lote_carga bigint NOT NULL,
    nombre_archivo character varying(200) NOT NULL,
    cargado_en timestamp with time zone DEFAULT now() NOT NULL,
    fk_usuario bigint,
    filas_leidas integer DEFAULT 0 NOT NULL,
    filas_aceptadas integer DEFAULT 0 NOT NULL,
    filas_rechazadas integer DEFAULT 0 NOT NULL,
    observaciones text,
    CONSTRAINT chk_lote_carga_filas CHECK (((filas_leidas >= 0) AND (filas_aceptadas >= 0) AND (filas_rechazadas >= 0) AND ((filas_aceptadas + filas_rechazadas) <= filas_leidas)))
);


--
-- TOC entry 5974 (class 0 OID 0)
-- Dependencies: 246
-- Name: TABLE lote_carga_asistente; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON TABLE catedras.lote_carga_asistente IS 'Cada ejecucion de la carga del archivo de personas del ASIS. "Ese archivo lo cargamos en la base de datos como una tabla" (52:57). Una carga de 16.093 filas necesita EVIDENCIA, no un mensaje en pantalla: sin estos contadores, "cargo menos filas de las que tenia" es indepurable.';


--
-- TOC entry 247 (class 1259 OID 87769)
-- Name: lote_carga_asistente_id_lote_carga_seq; Type: SEQUENCE; Schema: catedras; Owner: -
--

ALTER TABLE catedras.lote_carga_asistente ALTER COLUMN id_lote_carga ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME catedras.lote_carga_asistente_id_lote_carga_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- TOC entry 248 (class 1259 OID 87770)
-- Name: lote_migracion; Type: TABLE; Schema: catedras; Owner: -
--

CREATE TABLE catedras.lote_migracion (
    id_lote_migracion bigint NOT NULL,
    fk_sesion bigint NOT NULL,
    nombre_proceso character varying(60) DEFAULT 'ME_MIGRACION_CATEDRA'::character varying NOT NULL,
    nombre_archivo character varying(200) NOT NULL,
    generado_en timestamp with time zone DEFAULT now() NOT NULL,
    fk_usuario bigint,
    instancia_proceso_asis character varying(30),
    fk_estado_proceso character varying(12) DEFAULT 'GENERADO'::character varying NOT NULL,
    total_filas integer DEFAULT 0 NOT NULL,
    observaciones text,
    CONSTRAINT chk_lote_mig_filas CHECK ((total_filas >= 0))
);


--
-- TOC entry 5975 (class 0 OID 0)
-- Dependencies: 248
-- Name: COLUMN lote_migracion.instancia_proceso_asis; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON COLUMN catedras.lote_migracion.instancia_proceso_asis IS 'El numero de instancia que devuelve el ASIS al ejecutar el proceso en PSUNX01. Es lo que permite cerrar el ciclo contra el Monitor Procesos.';


--
-- TOC entry 249 (class 1259 OID 87780)
-- Name: lote_migracion_id_lote_migracion_seq; Type: SEQUENCE; Schema: catedras; Owner: -
--

ALTER TABLE catedras.lote_migracion ALTER COLUMN id_lote_migracion ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME catedras.lote_migracion_id_lote_migracion_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- TOC entry 250 (class 1259 OID 87781)
-- Name: modalidad; Type: TABLE; Schema: catedras; Owner: -
--

CREATE TABLE catedras.modalidad (
    id_modalidad character varying(15) NOT NULL,
    nombre character varying(60) NOT NULL,
    canal_difusion character varying(10) NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    CONSTRAINT chk_modalidad_canal CHECK (((canal_difusion)::text = ANY (ARRAY[('QR'::character varying)::text, ('ENLACE'::character varying)::text, ('AMBOS'::character varying)::text])))
);


--
-- TOC entry 5976 (class 0 OID 0)
-- Dependencies: 250
-- Name: COLUMN modalidad.canal_difusion; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON COLUMN catedras.modalidad.canal_difusion IS 'QR si es presencial, ENLACE si es virtual. Traduce la regla del profesor Hugo Nelson en 1:06:53: "cuando es virtual enviamos el enlace; si es presencial, el QR". Se hace cumplir en el script 10 (RN-13).';


--
-- TOC entry 251 (class 1259 OID 87786)
-- Name: novedad_carga; Type: TABLE; Schema: catedras; Owner: -
--

CREATE TABLE catedras.novedad_carga (
    id_novedad bigint NOT NULL,
    fk_lote_carga bigint NOT NULL,
    numero_fila integer NOT NULL,
    contenido_crudo text NOT NULL,
    motivo character varying(200) NOT NULL,
    CONSTRAINT chk_novedad_fila CHECK ((numero_fila > 0))
);


--
-- TOC entry 5977 (class 0 OID 0)
-- Dependencies: 251
-- Name: COLUMN novedad_carga.contenido_crudo; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON COLUMN catedras.novedad_carga.contenido_crudo IS 'La fila del archivo TAL COMO VENIA, sin interpretar. Es lo unico que permite depurar un rechazo por formato: si se guardara ya convertida, se perderia justamente el dato que causo el problema.';


--
-- TOC entry 252 (class 1259 OID 87792)
-- Name: novedad_carga_id_novedad_seq; Type: SEQUENCE; Schema: catedras; Owner: -
--

ALTER TABLE catedras.novedad_carga ALTER COLUMN id_novedad ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME catedras.novedad_carga_id_novedad_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- TOC entry 253 (class 1259 OID 87793)
-- Name: opcion_pregunta; Type: TABLE; Schema: catedras; Owner: -
--

CREATE TABLE catedras.opcion_pregunta (
    id_opcion bigint NOT NULL,
    fk_pregunta bigint NOT NULL,
    etiqueta character varying(150) NOT NULL,
    valor smallint,
    orden smallint NOT NULL
);


--
-- TOC entry 254 (class 1259 OID 87796)
-- Name: opcion_pregunta_id_opcion_seq; Type: SEQUENCE; Schema: catedras; Owner: -
--

ALTER TABLE catedras.opcion_pregunta ALTER COLUMN id_opcion ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME catedras.opcion_pregunta_id_opcion_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- TOC entry 255 (class 1259 OID 87797)
-- Name: parametro; Type: TABLE; Schema: catedras; Owner: -
--

CREATE TABLE catedras.parametro (
    clave character varying(60) NOT NULL,
    valor character varying(200) NOT NULL,
    tipo_dato character varying(15) DEFAULT 'TEXTO'::character varying NOT NULL,
    descripcion character varying(300) NOT NULL,
    fk_usuario_modifica bigint,
    modificado_en timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT chk_parametro_tipo CHECK (((tipo_dato)::text = ANY (ARRAY[('TEXTO'::character varying)::text, ('ENTERO'::character varying)::text, ('BOOLEANO'::character varying)::text, ('FECHA'::character varying)::text, ('DECIMAL'::character varying)::text])))
);


--
-- TOC entry 5978 (class 0 OID 0)
-- Dependencies: 255
-- Name: TABLE parametro; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON TABLE catedras.parametro IS 'Configuracion que el administrador cambia sin tocar el codigo. Del .docx: "el administrador configura o parametriza los valores que considere".';


--
-- TOC entry 256 (class 1259 OID 87805)
-- Name: periodo_academico; Type: TABLE; Schema: catedras; Owner: -
--

CREATE TABLE catedras.periodo_academico (
    id_periodo character(6) NOT NULL,
    fecha_ini date NOT NULL,
    fecha_fin date NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    CONSTRAINT chk_periodo_fechas CHECK ((fecha_fin > fecha_ini)),
    CONSTRAINT chk_periodo_formato CHECK ((id_periodo ~ '^[0-9]{4}-[1-3]$'::text))
);


--
-- TOC entry 5979 (class 0 OID 0)
-- Dependencies: 256
-- Name: TABLE periodo_academico; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON TABLE catedras.periodo_academico IS 'Periodo academico: 2026-1, 2026-2. Lo pidio el profesor Hugo Nelson por su nombre - "cuantas personas externas han ingresado en 2026-2" (56:16).';


--
-- TOC entry 257 (class 1259 OID 87811)
-- Name: ponencia; Type: TABLE; Schema: catedras; Owner: -
--

CREATE TABLE catedras.ponencia (
    fk_sesion bigint NOT NULL,
    fk_asistente bigint NOT NULL,
    rol character varying(15) DEFAULT 'PONENTE'::character varying NOT NULL,
    CONSTRAINT chk_ponencia_rol CHECK (((rol)::text = ANY (ARRAY[('PONENTE'::character varying)::text, ('MODERADOR'::character varying)::text, ('INVITADO'::character varying)::text, ('ORGANIZADOR'::character varying)::text])))
);


--
-- TOC entry 258 (class 1259 OID 87816)
-- Name: pregunta; Type: TABLE; Schema: catedras; Owner: -
--

CREATE TABLE catedras.pregunta (
    id_pregunta bigint NOT NULL,
    fk_encuesta bigint NOT NULL,
    enunciado character varying(300) NOT NULL,
    orden smallint NOT NULL,
    fk_tipo_pregunta character varying(15) NOT NULL,
    obligatoria boolean DEFAULT false NOT NULL,
    valor_min smallint,
    valor_max smallint,
    CONSTRAINT chk_pregunta_ord CHECK ((orden >= 1)),
    CONSTRAINT chk_pregunta_rango CHECK ((((valor_min IS NULL) AND (valor_max IS NULL)) OR ((valor_min IS NOT NULL) AND (valor_max IS NOT NULL) AND (valor_max > valor_min))))
);


--
-- TOC entry 259 (class 1259 OID 87822)
-- Name: pregunta_id_pregunta_seq; Type: SEQUENCE; Schema: catedras; Owner: -
--

ALTER TABLE catedras.pregunta ALTER COLUMN id_pregunta ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME catedras.pregunta_id_pregunta_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- TOC entry 260 (class 1259 OID 87823)
-- Name: programa_academico; Type: TABLE; Schema: catedras; Owner: -
--

CREATE TABLE catedras.programa_academico (
    codigo character varying(5) NOT NULL,
    nombre character varying(150) NOT NULL,
    fk_facultad character varying(10),
    nivel character varying(25) DEFAULT 'PREGRADO'::character varying NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    CONSTRAINT chk_programa_codigo CHECK (((codigo)::text ~ '^[A-Z][0-9A-Z]{3,4}$'::text)),
    CONSTRAINT chk_programa_nivel CHECK (((nivel)::text = ANY (ARRAY[('PREGRADO'::character varying)::text, ('ESPECIALIZACION'::character varying)::text, ('MAESTRIA'::character varying)::text, ('DOCTORADO'::character varying)::text, ('EDUCACION_CONTINUA'::character varying)::text, ('OTRO'::character varying)::text])))
);


--
-- TOC entry 5980 (class 0 OID 0)
-- Dependencies: 260
-- Name: TABLE programa_academico; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON TABLE catedras.programa_academico IS 'Programa academico. 105 codigos distintos en el maestro real: 103 con la forma M + 4 digitos, mas MCDER de 5 caracteres y MCCP de 4.';


--
-- TOC entry 5981 (class 0 OID 0)
-- Dependencies: 260
-- Name: COLUMN programa_academico.codigo; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON COLUMN catedras.programa_academico.codigo IS 'CLAVE NATURAL a proposito: es el codigo que viaja al archivo plano del ASIS. Un identificador sustituto obligaria a un JOIN en la consulta mas critica del sistema, que es justamente la exportacion.';


--
-- TOC entry 261 (class 1259 OID 87830)
-- Name: programa_asistente; Type: TABLE; Schema: catedras; Owner: -
--

CREATE TABLE catedras.programa_asistente (
    fk_asistente bigint NOT NULL,
    fk_programa character varying(5) NOT NULL,
    fk_periodo character(6) NOT NULL,
    estado character varying(15) DEFAULT 'ACTIVO'::character varying NOT NULL,
    CONSTRAINT chk_programa_asistente_estado CHECK (((estado)::text = ANY (ARRAY[('ACTIVO'::character varying)::text, ('RETIRADO'::character varying)::text, ('GRADUADO'::character varying)::text, ('APLAZADO'::character varying)::text])))
);


--
-- TOC entry 5982 (class 0 OID 0)
-- Dependencies: 261
-- Name: TABLE programa_asistente; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON TABLE catedras.programa_asistente IS 'Matricula de un asistente en un programa, por periodo. Es N:M por dato comprobado: en el maestro real el mismo ID aparece con varios codigos de programa. La clave de tres columnas absorbe los pares duplicados que trae el archivo del ASIS - hay pares repetidos hasta tres veces.';


--
-- TOC entry 262 (class 1259 OID 87835)
-- Name: registro_asistencia; Type: TABLE; Schema: catedras; Owner: -
--

CREATE TABLE catedras.registro_asistencia (
    id_registro bigint NOT NULL,
    fk_sesion bigint NOT NULL,
    fk_asistente bigint NOT NULL,
    fk_enlace bigint,
    fk_clave bigint,
    registrado_en timestamp with time zone DEFAULT now() NOT NULL,
    fk_programa_snapshot character varying(5),
    fk_vinculacion_snapshot character varying(15) NOT NULL,
    origen character varying(15) DEFAULT 'QR'::character varying NOT NULL,
    ip inet,
    user_agent character varying(300),
    CONSTRAINT chk_registro_clave CHECK ((((origen)::text = ANY (ARRAY[('MANUAL'::character varying)::text, ('IMPORTADO'::character varying)::text])) OR (fk_clave IS NOT NULL))),
    CONSTRAINT chk_registro_origen CHECK (((origen)::text = ANY (ARRAY[('QR'::character varying)::text, ('ENLACE'::character varying)::text, ('MANUAL'::character varying)::text, ('IMPORTADO'::character varying)::text])))
);


--
-- TOC entry 5983 (class 0 OID 0)
-- Dependencies: 262
-- Name: TABLE registro_asistencia; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON TABLE catedras.registro_asistencia IS 'Hecho de que un asistente quedo registrado en una sesion, en un instante concreto, con el programa y la vinculacion que tenia en ese momento. Es la tabla central del sistema.';


--
-- TOC entry 5984 (class 0 OID 0)
-- Dependencies: 262
-- Name: COLUMN registro_asistencia.fk_programa_snapshot; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON COLUMN catedras.registro_asistencia.fk_programa_snapshot IS 'INSTANTANEA, no referencia viva. El programa que el asistente tenia EL DIA de la catedra. NUNCA se actualiza. Razon: "el programa es importante porque despues eso le da informacion a Registro de que programas hizo el estudiante" (50:14) - es una afirmacion sobre el pasado. Si se leyera del vigente, un estudiante que cambia de carrera reescribiria su propio historial y el archivo enviado al ASIS dejaria de ser reproducible. NULO cuando la vinculacion no exige programa (Rutas de Paz, RN-07).';


--
-- TOC entry 5985 (class 0 OID 0)
-- Dependencies: 262
-- Name: COLUMN registro_asistencia.fk_vinculacion_snapshot; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON COLUMN catedras.registro_asistencia.fk_vinculacion_snapshot IS 'INSTANTANEA de la vinculacion. Un egresado que asistio siendo estudiante debe seguir contando como estudiante en los informes de aquel periodo.';


--
-- TOC entry 263 (class 1259 OID 87844)
-- Name: registro_asistencia_id_registro_seq; Type: SEQUENCE; Schema: catedras; Owner: -
--

ALTER TABLE catedras.registro_asistencia ALTER COLUMN id_registro ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME catedras.registro_asistencia_id_registro_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- TOC entry 264 (class 1259 OID 87845)
-- Name: respuesta_encuesta; Type: TABLE; Schema: catedras; Owner: -
--

CREATE TABLE catedras.respuesta_encuesta (
    id_respuesta bigint NOT NULL,
    fk_registro bigint NOT NULL,
    fk_encuesta bigint NOT NULL,
    respondida_en timestamp with time zone DEFAULT now() NOT NULL,
    completa boolean DEFAULT false NOT NULL
);


--
-- TOC entry 5986 (class 0 OID 0)
-- Dependencies: 264
-- Name: COLUMN respuesta_encuesta.completa; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON COLUMN catedras.respuesta_encuesta.completa IS 'Solo lo marca sp_cerrar_encuesta, tras comprobar que todas las preguntas obligatorias tienen respuesta (RN-26). Es una regla NO DECLARATIVA: ninguna clave foranea puede exigir "al menos uno".';


--
-- TOC entry 265 (class 1259 OID 87850)
-- Name: respuesta_encuesta_id_respuesta_seq; Type: SEQUENCE; Schema: catedras; Owner: -
--

ALTER TABLE catedras.respuesta_encuesta ALTER COLUMN id_respuesta ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME catedras.respuesta_encuesta_id_respuesta_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- TOC entry 266 (class 1259 OID 87851)
-- Name: respuesta_item; Type: TABLE; Schema: catedras; Owner: -
--

CREATE TABLE catedras.respuesta_item (
    id_item bigint NOT NULL,
    fk_respuesta bigint NOT NULL,
    fk_pregunta bigint NOT NULL,
    valor_numerico smallint,
    valor_texto text,
    fk_opcion bigint,
    CONSTRAINT chk_respuesta_item_un_valor CHECK ((((((valor_numerico IS NOT NULL))::integer + ((valor_texto IS NOT NULL))::integer) + ((fk_opcion IS NOT NULL))::integer) = 1))
);


--
-- TOC entry 5987 (class 0 OID 0)
-- Dependencies: 266
-- Name: TABLE respuesta_item; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON TABLE catedras.respuesta_item IS 'Respuesta a una pregunta concreta. La alternativa ortodoxa es una especializacion supertipo/subtipo con una tabla por tipo de respuesta: es correcta y se acepta, pero cuesta tres tablas y una union en cada consulta de evaluacion.';


--
-- TOC entry 5988 (class 0 OID 0)
-- Dependencies: 266
-- Name: CONSTRAINT chk_respuesta_item_un_valor ON respuesta_item; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON CONSTRAINT chk_respuesta_item_un_valor ON catedras.respuesta_item IS 'RN-25. Impide la corrupcion silenciosa: sin esta restriccion, un item con las tres columnas vacias se colaria y los promedios del informe de evaluacion saldrian mal SIN AVISAR. La concordancia entre la columna usada y el tipo de la pregunta la verifica el disparador del script 10.';


--
-- TOC entry 267 (class 1259 OID 87857)
-- Name: respuesta_item_id_item_seq; Type: SEQUENCE; Schema: catedras; Owner: -
--

ALTER TABLE catedras.respuesta_item ALTER COLUMN id_item ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME catedras.respuesta_item_id_item_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- TOC entry 268 (class 1259 OID 87858)
-- Name: rol; Type: TABLE; Schema: catedras; Owner: -
--

CREATE TABLE catedras.rol (
    id_rol character varying(20) NOT NULL,
    nombre character varying(60) NOT NULL,
    descripcion character varying(200)
);


--
-- TOC entry 269 (class 1259 OID 87861)
-- Name: rol_por_usuario; Type: TABLE; Schema: catedras; Owner: -
--

CREATE TABLE catedras.rol_por_usuario (
    fk_usuario bigint NOT NULL,
    fk_rol character varying(20) NOT NULL,
    fecha_ini date DEFAULT CURRENT_DATE NOT NULL,
    fecha_fin date,
    CONSTRAINT chk_rol_usuario_fechas CHECK (((fecha_fin IS NULL) OR (fecha_fin >= fecha_ini)))
);


--
-- TOC entry 270 (class 1259 OID 87866)
-- Name: sede; Type: TABLE; Schema: catedras; Owner: -
--

CREATE TABLE catedras.sede (
    id_sede character varying(15) NOT NULL,
    nombre character varying(80) NOT NULL,
    direccion character varying(200),
    es_virtual boolean DEFAULT false NOT NULL,
    activo boolean DEFAULT true NOT NULL
);


--
-- TOC entry 5989 (class 0 OID 0)
-- Dependencies: 270
-- Name: TABLE sede; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON TABLE catedras.sede IS 'Lugar fisico o virtual donde se dicta una sesion. Aparece porque "puede que una catedra este en San Benito, otra este virtual y otra este en Bello" (1:03:33): LA SEDE ES LO QUE DISTINGUE DOS CATEDRAS A LA MISMA HORA. Como texto libre en sesion, el informe por sede no se puede escribir.';


--
-- TOC entry 271 (class 1259 OID 87871)
-- Name: sesion; Type: TABLE; Schema: catedras; Owner: -
--

CREATE TABLE catedras.sesion (
    id_sesion bigint NOT NULL,
    fk_catedra bigint NOT NULL,
    numero_reunion smallint NOT NULL,
    numero_reunion_asis smallint,
    titulo character varying(200),
    inicio timestamp with time zone NOT NULL,
    fin timestamp with time zone NOT NULL,
    fk_modalidad character varying(15) NOT NULL,
    fk_sede character varying(15) NOT NULL,
    fk_periodo character(6) NOT NULL,
    fk_encuesta bigint,
    lugar character varying(200),
    enlace_virtual character varying(300),
    cupo integer,
    estado character varying(15) DEFAULT 'PROGRAMADA'::character varying NOT NULL,
    creado_en timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT chk_sesion_cupo CHECK (((cupo IS NULL) OR (cupo > 0))),
    CONSTRAINT chk_sesion_estado CHECK (((estado)::text = ANY (ARRAY[('PROGRAMADA'::character varying)::text, ('ABIERTA'::character varying)::text, ('CERRADA'::character varying)::text, ('MIGRADA'::character varying)::text]))),
    CONSTRAINT chk_sesion_fechas CHECK ((fin > inicio)),
    CONSTRAINT chk_sesion_reunion CHECK ((numero_reunion >= 1))
);


--
-- TOC entry 5990 (class 0 OID 0)
-- Dependencies: 271
-- Name: TABLE sesion; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON TABLE catedras.sesion IS 'Cada una de las reuniones en que se dicta una catedra. ENTIDAD DEBIL: la "reunion 3" no significa nada suelta, es la reunion 3 DE UN EVENTO.';


--
-- TOC entry 5991 (class 0 OID 0)
-- Dependencies: 271
-- Name: COLUMN sesion.numero_reunion; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON COLUMN catedras.sesion.numero_reunion IS 'El numero de reunion con que la sesion viaja al ASIS. Si no se indica, el disparador del script 10 PROPONE el maximo de la catedra mas uno - y eso elimina el paso 2 del manual para las catedras nuevas. Para las que ya existen en el ASIS, el valor bueno es numero_reunion_asis: el dato real demuestra que el ASIS no numera por evento desde 1.';


--
-- TOC entry 5992 (class 0 OID 0)
-- Dependencies: 271
-- Name: COLUMN sesion.numero_reunion_asis; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON COLUMN catedras.sesion.numero_reunion_asis IS 'El numero de reunion segun el ASIS. DECISION RESUELTA POR EL DATO: el ASIS lleva un contador propio - de 5.481 eventos con una sola reunion, solo 588 tienen el numero 1, y los demas llegan hasta 4348. Cuando esta columna tiene valor, MANDA sobre numero_reunion.';


--
-- TOC entry 272 (class 1259 OID 87882)
-- Name: sesion_id_sesion_seq; Type: SEQUENCE; Schema: catedras; Owner: -
--

ALTER TABLE catedras.sesion ALTER COLUMN id_sesion ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME catedras.sesion_id_sesion_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- TOC entry 273 (class 1259 OID 87883)
-- Name: tipo_documento; Type: TABLE; Schema: catedras; Owner: -
--

CREATE TABLE catedras.tipo_documento (
    id_tipo_documento character varying(5) NOT NULL,
    nombre character varying(60) NOT NULL,
    activo boolean DEFAULT true NOT NULL
);


--
-- TOC entry 5993 (class 0 OID 0)
-- Dependencies: 273
-- Name: TABLE tipo_documento; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON TABLE catedras.tipo_documento IS 'Clase de documento de identidad. Catalogo cerrado que elimina las mas de diez formas de escribir "cedula" que trae el formulario actual.';


--
-- TOC entry 274 (class 1259 OID 87887)
-- Name: tipo_evento; Type: TABLE; Schema: catedras; Owner: -
--

CREATE TABLE catedras.tipo_evento (
    id_tipo_evento character(4) NOT NULL,
    nombre character varying(80) NOT NULL,
    activo boolean DEFAULT true NOT NULL
);


--
-- TOC entry 5994 (class 0 OID 0)
-- Dependencies: 274
-- Name: TABLE tipo_evento; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON TABLE catedras.tipo_evento IS 'Clasificacion del evento en el ASIS. Ocho valores observados en las 5.711 filas reales: CAAB (5.449), DLLH (188), DEPR (44), SLDI (16), ARCU (5), MTG (5), DIIB (3), EIIC (1).';


--
-- TOC entry 275 (class 1259 OID 87891)
-- Name: tipo_pregunta; Type: TABLE; Schema: catedras; Owner: -
--

CREATE TABLE catedras.tipo_pregunta (
    id_tipo_pregunta character varying(15) NOT NULL,
    nombre character varying(60) NOT NULL,
    guarda_numero boolean DEFAULT false NOT NULL,
    guarda_texto boolean DEFAULT false NOT NULL,
    guarda_opcion boolean DEFAULT false NOT NULL,
    CONSTRAINT chk_tipo_pregunta_destino CHECK (((((guarda_numero)::integer + (guarda_texto)::integer) + (guarda_opcion)::integer) = 1))
);


--
-- TOC entry 5995 (class 0 OID 0)
-- Dependencies: 275
-- Name: TABLE tipo_pregunta; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON TABLE catedras.tipo_pregunta IS 'Clase de pregunta. Las tres columnas booleanas dicen en cual de las tres columnas de valor de respuesta_item se guarda la respuesta. Es lo que permite que la regla condicional (RN-25) sea DATO y no codigo.';


--
-- TOC entry 276 (class 1259 OID 87898)
-- Name: tipo_vinculacion; Type: TABLE; Schema: catedras; Owner: -
--

CREATE TABLE catedras.tipo_vinculacion (
    id_tipo_vinculacion character varying(15) NOT NULL,
    nombre character varying(60) NOT NULL,
    es_interno boolean NOT NULL,
    exige_programa boolean DEFAULT true NOT NULL,
    activo boolean DEFAULT true NOT NULL
);


--
-- TOC entry 5996 (class 0 OID 0)
-- Dependencies: 276
-- Name: TABLE tipo_vinculacion; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON TABLE catedras.tipo_vinculacion IS 'RUTAS_PAZ tiene exige_programa = false por la excepcion que planteo el profesor Hugo Nelson en 41:16: "ellos entrarian sin programa, pero solo ellos". ADMINISTRATIVO tambien, porque no esta matriculado en nada.';


--
-- TOC entry 5997 (class 0 OID 0)
-- Dependencies: 276
-- Name: COLUMN tipo_vinculacion.es_interno; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON COLUMN catedras.tipo_vinculacion.es_interno IS 'Resuelve el informe 2 del enunciado - internos contra externos - con una agrupacion, en vez de un CASE de veinte lineas.';


--
-- TOC entry 5998 (class 0 OID 0)
-- Dependencies: 276
-- Name: COLUMN tipo_vinculacion.exige_programa; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON COLUMN catedras.tipo_vinculacion.exige_programa IS 'FALSO en Rutas de Paz. Traduce la frase "ellos entrarian sin programa, pero solo ellos" (41:16) en un DATO. Habilitar una nueva excepcion es un UPDATE de una fila, no un ALTER TABLE en produccion.';


--
-- TOC entry 277 (class 1259 OID 87903)
-- Name: usuario; Type: TABLE; Schema: catedras; Owner: -
--

CREATE TABLE catedras.usuario (
    id_usuario bigint NOT NULL,
    fk_asistente bigint NOT NULL,
    usuario character varying(40) NOT NULL,
    clave_hash character varying(255) NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    ultimo_acceso timestamp with time zone,
    creado_en timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT chk_usuario_login CHECK (((usuario)::text ~ '^[a-z0-9._-]{4,40}$'::text))
);


--
-- TOC entry 5999 (class 0 OID 0)
-- Dependencies: 277
-- Name: COLUMN usuario.clave_hash; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON COLUMN catedras.usuario.clave_hash IS 'RN-34 · Nunca la clave en claro. Aqui se guarda un hash con sal; el algoritmo lo decide la aplicacion (bcrypt o argon2 preferiblemente).';


--
-- TOC entry 278 (class 1259 OID 87909)
-- Name: usuario_id_usuario_seq; Type: SEQUENCE; Schema: catedras; Owner: -
--

ALTER TABLE catedras.usuario ALTER COLUMN id_usuario ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME catedras.usuario_id_usuario_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- TOC entry 279 (class 1259 OID 87910)
-- Name: v_asistencia_completa; Type: VIEW; Schema: catedras; Owner: -
--

CREATE VIEW catedras.v_asistencia_completa AS
 SELECT r.id_registro,
    r.registrado_en,
    r.origen,
    c.id_evento_asis,
    c.nombre AS catedra,
    s.id_sesion,
    s.numero_reunion,
    s.titulo AS sesion,
    s.inicio,
    s.fk_periodo AS periodo,
    se.nombre AS sede,
    m.nombre AS modalidad,
    a.id_asistente,
    a.id_asis,
    (((a.nombres)::text || ' '::text) || (a.apellidos)::text) AS asistente,
    a.es_externo,
    r.fk_programa_snapshot AS programa,
    p.nombre AS programa_nombre,
    p.fk_facultad AS facultad,
    r.fk_vinculacion_snapshot AS vinculacion,
    tv.es_interno,
    (re.id_respuesta IS NOT NULL) AS respondio_encuesta
   FROM ((((((((catedras.registro_asistencia r
     JOIN catedras.sesion s ON ((s.id_sesion = r.fk_sesion)))
     JOIN catedras.catedra c ON ((c.id_catedra = s.fk_catedra)))
     JOIN catedras.sede se ON (((se.id_sede)::text = (s.fk_sede)::text)))
     JOIN catedras.modalidad m ON (((m.id_modalidad)::text = (s.fk_modalidad)::text)))
     JOIN catedras.asistente a ON ((a.id_asistente = r.fk_asistente)))
     JOIN catedras.tipo_vinculacion tv ON (((tv.id_tipo_vinculacion)::text = (r.fk_vinculacion_snapshot)::text)))
     LEFT JOIN catedras.programa_academico p ON (((p.codigo)::text = (r.fk_programa_snapshot)::text)))
     LEFT JOIN catedras.respuesta_encuesta re ON ((re.fk_registro = r.id_registro)));


--
-- TOC entry 6000 (class 0 OID 0)
-- Dependencies: 279
-- Name: VIEW v_asistencia_completa; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON VIEW catedras.v_asistencia_completa IS 'Base de los informes I1 a I5 y I14. Usa las INSTANTANEAS del registro, no el programa ni la vinculacion vigentes: es lo que hace que un informe de un periodo cerrado siga dando lo mismo dentro de un ano.';


--
-- TOC entry 280 (class 1259 OID 87915)
-- Name: vinculacion_asistente; Type: TABLE; Schema: catedras; Owner: -
--

CREATE TABLE catedras.vinculacion_asistente (
    fk_asistente bigint NOT NULL,
    fk_tipo_vinculacion character varying(15) NOT NULL,
    fecha_ini date NOT NULL,
    fecha_fin date,
    CONSTRAINT chk_vinculacion_fechas CHECK (((fecha_fin IS NULL) OR (fecha_fin >= fecha_ini)))
);


--
-- TOC entry 6001 (class 0 OID 0)
-- Dependencies: 280
-- Name: TABLE vinculacion_asistente; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON TABLE catedras.vinculacion_asistente IS 'Vinculacion institucional de una persona a lo largo del tiempo.';


--
-- TOC entry 281 (class 1259 OID 87919)
-- Name: v_asistente_vigente; Type: VIEW; Schema: catedras; Owner: -
--

CREATE VIEW catedras.v_asistente_vigente AS
 SELECT a.id_asistente,
    a.id_asis,
    (((a.nombres)::text || ' '::text) || (a.apellidos)::text) AS nombre_completo,
    a.es_externo,
    COALESCE(a.correo_institucional, a.correo_personal) AS correo_contacto,
    (a.correo_institucional IS NOT NULL) AS tiene_correo_institucional,
    d.numero AS documento_vigente,
    d.fk_tipo_documento AS tipo_documento,
    v.fk_tipo_vinculacion AS vinculacion,
    tv.nombre AS vinculacion_nombre,
    tv.es_interno,
    tv.exige_programa,
    a.activo
   FROM (((catedras.asistente a
     LEFT JOIN catedras.documento_asistente d ON (((d.fk_asistente = a.id_asistente) AND d.vigente)))
     LEFT JOIN LATERAL ( SELECT va.fk_tipo_vinculacion
           FROM catedras.vinculacion_asistente va
          WHERE ((va.fk_asistente = a.id_asistente) AND (va.fecha_fin IS NULL))
          ORDER BY va.fecha_ini DESC
         LIMIT 1) v ON (true))
     LEFT JOIN catedras.tipo_vinculacion tv ON (((tv.id_tipo_vinculacion)::text = (v.fk_tipo_vinculacion)::text)));


--
-- TOC entry 6002 (class 0 OID 0)
-- Dependencies: 281
-- Name: VIEW v_asistente_vigente; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON VIEW catedras.v_asistente_vigente IS 'La foto ACTUAL de una persona: su documento vigente y su vinculacion vigente. Ojo: para los informes de asistencia NO se usa esta vista, sino las instantaneas del registro. Aqui esta el hoy; alli, el dia de la catedra.';


--
-- TOC entry 282 (class 1259 OID 87924)
-- Name: v_control_ventana; Type: VIEW; Schema: catedras; Owner: -
--

CREATE VIEW catedras.v_control_ventana AS
 SELECT r.id_registro,
    r.registrado_en,
    e.ventana,
    r.origen
   FROM (catedras.registro_asistencia r
     JOIN catedras.enlace_registro e ON ((e.id_enlace = r.fk_enlace)))
  WHERE (((r.origen)::text <> ALL (ARRAY[('MANUAL'::character varying)::text, ('IMPORTADO'::character varying)::text])) AND (NOT (e.ventana @> r.registrado_en)));


--
-- TOC entry 6003 (class 0 OID 0)
-- Dependencies: 282
-- Name: VIEW v_control_ventana; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON VIEW catedras.v_control_ventana IS 'CONTROL de RN-15. Debe devolver CERO filas siempre. Si alguna vez devuelve algo, es que se desactivo el disparador o que alguien inserto por detras.';


--
-- TOC entry 283 (class 1259 OID 87929)
-- Name: v_embudo_registro; Type: VIEW; Schema: catedras; Owner: -
--

CREATE VIEW catedras.v_embudo_registro AS
 SELECT s.id_sesion,
    c.nombre AS catedra,
    s.numero_reunion,
    ( SELECT count(*) AS count
           FROM catedras.enlace_registro e
          WHERE (e.fk_sesion = s.id_sesion)) AS enlaces,
    ( SELECT count(*) AS count
           FROM (catedras.clave_acceso k
             JOIN catedras.enlace_registro e ON ((e.id_enlace = k.fk_enlace)))
          WHERE (e.fk_sesion = s.id_sesion)) AS claves_enviadas,
    ( SELECT count(*) AS count
           FROM (catedras.clave_acceso k
             JOIN catedras.enlace_registro e ON ((e.id_enlace = k.fk_enlace)))
          WHERE ((e.fk_sesion = s.id_sesion) AND (k.usado_en IS NOT NULL))) AS claves_usadas,
    ( SELECT count(*) AS count
           FROM catedras.registro_asistencia r
          WHERE (r.fk_sesion = s.id_sesion)) AS registros,
    ( SELECT count(*) AS count
           FROM (catedras.respuesta_encuesta re
             JOIN catedras.registro_asistencia r ON ((r.id_registro = re.fk_registro)))
          WHERE (r.fk_sesion = s.id_sesion)) AS encuestas
   FROM (catedras.sesion s
     JOIN catedras.catedra c ON ((c.id_catedra = s.fk_catedra)));


--
-- TOC entry 284 (class 1259 OID 87934)
-- Name: v_evaluacion_sesion; Type: VIEW; Schema: catedras; Owner: -
--

CREATE VIEW catedras.v_evaluacion_sesion AS
 SELECT s.id_sesion,
    c.nombre AS catedra,
    s.numero_reunion,
    p.orden,
    p.enunciado,
    tp.id_tipo_pregunta AS tipo,
    count(ri.id_item) AS respuestas,
    round(avg(ri.valor_numerico), 2) AS promedio,
    min(ri.valor_numerico) AS minimo,
    max(ri.valor_numerico) AS maximo
   FROM ((((((catedras.respuesta_item ri
     JOIN catedras.respuesta_encuesta re ON ((re.id_respuesta = ri.fk_respuesta)))
     JOIN catedras.registro_asistencia r ON ((r.id_registro = re.fk_registro)))
     JOIN catedras.sesion s ON ((s.id_sesion = r.fk_sesion)))
     JOIN catedras.catedra c ON ((c.id_catedra = s.fk_catedra)))
     JOIN catedras.pregunta p ON ((p.id_pregunta = ri.fk_pregunta)))
     JOIN catedras.tipo_pregunta tp ON (((tp.id_tipo_pregunta)::text = (p.fk_tipo_pregunta)::text)))
  WHERE tp.guarda_numero
  GROUP BY s.id_sesion, c.nombre, s.numero_reunion, p.orden, p.enunciado, tp.id_tipo_pregunta;


--
-- TOC entry 285 (class 1259 OID 87939)
-- Name: v_pendiente_migracion; Type: VIEW; Schema: catedras; Owner: -
--

CREATE VIEW catedras.v_pendiente_migracion AS
 SELECT s.id_sesion,
    c.id_evento_asis,
    c.nombre AS catedra,
    s.numero_reunion,
    s.inicio,
    count(*) AS registros,
    count(*) FILTER (WHERE (a.id_asis IS NULL)) AS sin_id_asis,
    count(*) FILTER (WHERE (r.fk_programa_snapshot IS NULL)) AS sin_programa,
    count(*) FILTER (WHERE a.es_externo) AS externos,
    count(*) FILTER (WHERE ((a.id_asis IS NOT NULL) AND (r.fk_programa_snapshot IS NOT NULL) AND (NOT a.es_externo))) AS migrables
   FROM (((catedras.registro_asistencia r
     JOIN catedras.sesion s ON ((s.id_sesion = r.fk_sesion)))
     JOIN catedras.catedra c ON ((c.id_catedra = s.fk_catedra)))
     JOIN catedras.asistente a ON ((a.id_asistente = r.fk_asistente)))
  WHERE (NOT (EXISTS ( SELECT 1
           FROM catedras.lote_migracion lm
          WHERE ((lm.fk_sesion = s.id_sesion) AND ((lm.fk_estado_proceso)::text = 'CORRECTO'::text)))))
  GROUP BY s.id_sesion, c.id_evento_asis, c.nombre, s.numero_reunion, s.inicio;


--
-- TOC entry 5874 (class 0 OID 87651)
-- Dependencies: 223
-- Data for Name: alias_programa; Type: TABLE DATA; Schema: catedras; Owner: -
--



--
-- TOC entry 5876 (class 0 OID 87657)
-- Dependencies: 225
-- Data for Name: asistente; Type: TABLE DATA; Schema: catedras; Owner: -
--



--
-- TOC entry 5878 (class 0 OID 87671)
-- Dependencies: 227
-- Data for Name: bitacora; Type: TABLE DATA; Schema: catedras; Owner: -
--



--
-- TOC entry 5880 (class 0 OID 87680)
-- Dependencies: 229
-- Data for Name: catedra; Type: TABLE DATA; Schema: catedras; Owner: -
--



--
-- TOC entry 5882 (class 0 OID 87688)
-- Dependencies: 231
-- Data for Name: clave_acceso; Type: TABLE DATA; Schema: catedras; Owner: -
--



--
-- TOC entry 5884 (class 0 OID 87700)
-- Dependencies: 233
-- Data for Name: consentimiento_datos; Type: TABLE DATA; Schema: catedras; Owner: -
--



--
-- TOC entry 5886 (class 0 OID 87709)
-- Dependencies: 235
-- Data for Name: dependencia; Type: TABLE DATA; Schema: catedras; Owner: -
--



--
-- TOC entry 5887 (class 0 OID 87714)
-- Dependencies: 236
-- Data for Name: detalle_migracion; Type: TABLE DATA; Schema: catedras; Owner: -
--



--
-- TOC entry 5889 (class 0 OID 87720)
-- Dependencies: 238
-- Data for Name: documento_asistente; Type: TABLE DATA; Schema: catedras; Owner: -
--



--
-- TOC entry 5891 (class 0 OID 87727)
-- Dependencies: 240
-- Data for Name: encuesta; Type: TABLE DATA; Schema: catedras; Owner: -
--



--
-- TOC entry 5893 (class 0 OID 87736)
-- Dependencies: 242
-- Data for Name: enlace_registro; Type: TABLE DATA; Schema: catedras; Owner: -
--



--
-- TOC entry 5895 (class 0 OID 87748)
-- Dependencies: 244
-- Data for Name: estado_proceso; Type: TABLE DATA; Schema: catedras; Owner: -
--

INSERT INTO catedras.estado_proceso VALUES ('GENERADO', 'Generado, sin enviar', false, false);
INSERT INTO catedras.estado_proceso VALUES ('EN_COLA', 'En cola', false, false);
INSERT INTO catedras.estado_proceso VALUES ('EN_CURSO', 'En curso', false, false);
INSERT INTO catedras.estado_proceso VALUES ('ERROR', 'Error', true, false);
INSERT INTO catedras.estado_proceso VALUES ('CORRECTO', 'Correcto', true, true);
INSERT INTO catedras.estado_proceso VALUES ('INCORRECTO', 'Incorrecto', true, false);


--
-- TOC entry 5896 (class 0 OID 87754)
-- Dependencies: 245
-- Data for Name: facultad; Type: TABLE DATA; Schema: catedras; Owner: -
--



--
-- TOC entry 5897 (class 0 OID 87759)
-- Dependencies: 246
-- Data for Name: lote_carga_asistente; Type: TABLE DATA; Schema: catedras; Owner: -
--



--
-- TOC entry 5899 (class 0 OID 87770)
-- Dependencies: 248
-- Data for Name: lote_migracion; Type: TABLE DATA; Schema: catedras; Owner: -
--



--
-- TOC entry 5901 (class 0 OID 87781)
-- Dependencies: 250
-- Data for Name: modalidad; Type: TABLE DATA; Schema: catedras; Owner: -
--

INSERT INTO catedras.modalidad VALUES ('PRESENCIAL', 'Presencial', 'QR', true);
INSERT INTO catedras.modalidad VALUES ('VIRTUAL', 'Virtual', 'ENLACE', true);
INSERT INTO catedras.modalidad VALUES ('TELEPRESENCIAL', 'Telepresencial', 'ENLACE', true);
INSERT INTO catedras.modalidad VALUES ('HIBRIDA', 'Hibrida', 'AMBOS', true);


--
-- TOC entry 5902 (class 0 OID 87786)
-- Dependencies: 251
-- Data for Name: novedad_carga; Type: TABLE DATA; Schema: catedras; Owner: -
--



--
-- TOC entry 5904 (class 0 OID 87793)
-- Dependencies: 253
-- Data for Name: opcion_pregunta; Type: TABLE DATA; Schema: catedras; Owner: -
--



--
-- TOC entry 5906 (class 0 OID 87797)
-- Dependencies: 255
-- Data for Name: parametro; Type: TABLE DATA; Schema: catedras; Owner: -
--

INSERT INTO catedras.parametro VALUES ('VENTANA_MINUTOS_ANTES', '30', 'ENTERO', 'Minutos antes del inicio en que se abre el registro', NULL, '2026-08-10 15:00:30.07467-05');
INSERT INTO catedras.parametro VALUES ('VENTANA_MINUTOS_DESPUES', '30', 'ENTERO', 'Minutos despues del fin en que se cierra el registro', NULL, '2026-08-10 15:00:30.07467-05');
INSERT INTO catedras.parametro VALUES ('CLAVE_VIGENCIA_MINUTOS', '10', 'ENTERO', 'Minutos que vive la clave enviada al correo', NULL, '2026-08-10 15:00:30.07467-05');
INSERT INTO catedras.parametro VALUES ('CLAVE_LONGITUD', '6', 'ENTERO', 'Digitos de la clave de un solo uso', NULL, '2026-08-10 15:00:30.07467-05');
INSERT INTO catedras.parametro VALUES ('CLAVE_MAX_INTENTOS', '3', 'ENTERO', 'Intentos antes de anular la clave', NULL, '2026-08-10 15:00:30.07467-05');
INSERT INTO catedras.parametro VALUES ('DOMINIOS_INSTITUCIONALES', 'usbmed.edu.co,usb.edu.co', 'TEXTO', 'Dominios que cuentan como correo institucional. Alimenta RN-19 e I18', NULL, '2026-08-10 15:00:30.07467-05');
INSERT INTO catedras.parametro VALUES ('URL_BASE', 'https://catedras.usbmed.edu.co/r/', 'TEXTO', 'Prefijo de la URL publica de registro', NULL, '2026-08-10 15:00:30.07467-05');
INSERT INTO catedras.parametro VALUES ('VERSION_POLITICA_DATOS', '2026.1', 'TEXTO', 'Version vigente de la politica de tratamiento de datos, Ley 1581 de 2012', NULL, '2026-08-10 15:00:30.07467-05');
INSERT INTO catedras.parametro VALUES ('PERMITE_EXTERNOS', 'true', 'BOOLEANO', 'DECISION ABIERTA §3.1: si es false, solo entra quien tenga ID en el ASIS', NULL, '2026-08-10 15:00:30.07467-05');


--
-- TOC entry 5907 (class 0 OID 87805)
-- Dependencies: 256
-- Data for Name: periodo_academico; Type: TABLE DATA; Schema: catedras; Owner: -
--

INSERT INTO catedras.periodo_academico VALUES ('2025-1', '2025-01-20', '2025-06-07', true);
INSERT INTO catedras.periodo_academico VALUES ('2025-2', '2025-07-21', '2025-12-06', true);
INSERT INTO catedras.periodo_academico VALUES ('2026-1', '2026-01-19', '2026-06-06', true);
INSERT INTO catedras.periodo_academico VALUES ('2026-2', '2026-07-20', '2026-12-05', true);


--
-- TOC entry 5908 (class 0 OID 87811)
-- Dependencies: 257
-- Data for Name: ponencia; Type: TABLE DATA; Schema: catedras; Owner: -
--



--
-- TOC entry 5909 (class 0 OID 87816)
-- Dependencies: 258
-- Data for Name: pregunta; Type: TABLE DATA; Schema: catedras; Owner: -
--



--
-- TOC entry 5911 (class 0 OID 87823)
-- Dependencies: 260
-- Data for Name: programa_academico; Type: TABLE DATA; Schema: catedras; Owner: -
--



--
-- TOC entry 5912 (class 0 OID 87830)
-- Dependencies: 261
-- Data for Name: programa_asistente; Type: TABLE DATA; Schema: catedras; Owner: -
--



--
-- TOC entry 5913 (class 0 OID 87835)
-- Dependencies: 262
-- Data for Name: registro_asistencia; Type: TABLE DATA; Schema: catedras; Owner: -
--



--
-- TOC entry 5915 (class 0 OID 87845)
-- Dependencies: 264
-- Data for Name: respuesta_encuesta; Type: TABLE DATA; Schema: catedras; Owner: -
--



--
-- TOC entry 5917 (class 0 OID 87851)
-- Dependencies: 266
-- Data for Name: respuesta_item; Type: TABLE DATA; Schema: catedras; Owner: -
--



--
-- TOC entry 5919 (class 0 OID 87858)
-- Dependencies: 268
-- Data for Name: rol; Type: TABLE DATA; Schema: catedras; Owner: -
--

INSERT INTO catedras.rol VALUES ('ADMIN', 'Administrador', 'Gestiona catedras, enlaces, cargas y migraciones');
INSERT INTO catedras.rol VALUES ('COORDINADOR', 'Coordinador de dependencia', 'Ve y gestiona lo de su dependencia');
INSERT INTO catedras.rol VALUES ('CONSULTA', 'Consulta', 'Solo lectura de informes');


--
-- TOC entry 5920 (class 0 OID 87861)
-- Dependencies: 269
-- Data for Name: rol_por_usuario; Type: TABLE DATA; Schema: catedras; Owner: -
--



--
-- TOC entry 5921 (class 0 OID 87866)
-- Dependencies: 270
-- Data for Name: sede; Type: TABLE DATA; Schema: catedras; Owner: -
--

INSERT INTO catedras.sede VALUES ('SAN_BENITO', 'Campus San Benito', 'Carrera 56C 51-110, Medellin', false, true);
INSERT INTO catedras.sede VALUES ('BELLO', 'Campus Bello', 'Calle 45 61-40, Bello', false, true);
INSERT INTO catedras.sede VALUES ('VIRTUAL', 'Virtual', NULL, true, true);


--
-- TOC entry 5922 (class 0 OID 87871)
-- Dependencies: 271
-- Data for Name: sesion; Type: TABLE DATA; Schema: catedras; Owner: -
--



--
-- TOC entry 5924 (class 0 OID 87883)
-- Dependencies: 273
-- Data for Name: tipo_documento; Type: TABLE DATA; Schema: catedras; Owner: -
--

INSERT INTO catedras.tipo_documento VALUES ('CC', 'Cedula de ciudadania', true);
INSERT INTO catedras.tipo_documento VALUES ('TI', 'Tarjeta de identidad', true);
INSERT INTO catedras.tipo_documento VALUES ('CE', 'Cedula de extranjeria', true);
INSERT INTO catedras.tipo_documento VALUES ('PA', 'Pasaporte', true);
INSERT INTO catedras.tipo_documento VALUES ('PEP', 'Permiso especial de permanencia', true);
INSERT INTO catedras.tipo_documento VALUES ('NIT', 'Numero de identificacion tributaria', true);


--
-- TOC entry 5925 (class 0 OID 87887)
-- Dependencies: 274
-- Data for Name: tipo_evento; Type: TABLE DATA; Schema: catedras; Owner: -
--

INSERT INTO catedras.tipo_evento VALUES ('CAAB', 'Catedra abierta', true);
INSERT INTO catedras.tipo_evento VALUES ('DLLH', 'Desarrollo humano', true);
INSERT INTO catedras.tipo_evento VALUES ('DEPR', 'Deporte y recreacion', true);
INSERT INTO catedras.tipo_evento VALUES ('SLDI', 'Salud integral', true);
INSERT INTO catedras.tipo_evento VALUES ('ARCU', 'Arte y cultura', true);
INSERT INTO catedras.tipo_evento VALUES ('MTG ', 'Mesa de trabajo o grupo', true);
INSERT INTO catedras.tipo_evento VALUES ('DIIB', 'Difusion institucional', true);
INSERT INTO catedras.tipo_evento VALUES ('EIIC', 'Encuentro de investigacion', true);


--
-- TOC entry 5926 (class 0 OID 87891)
-- Dependencies: 275
-- Data for Name: tipo_pregunta; Type: TABLE DATA; Schema: catedras; Owner: -
--

INSERT INTO catedras.tipo_pregunta VALUES ('ESCALA_5', 'Escala de 1 a 5', true, false, false);
INSERT INTO catedras.tipo_pregunta VALUES ('SI_NO', 'Si o no', true, false, false);
INSERT INTO catedras.tipo_pregunta VALUES ('TEXTO_LIBRE', 'Texto libre', false, true, false);
INSERT INTO catedras.tipo_pregunta VALUES ('OPCION_UNICA', 'Opcion unica', false, false, true);


--
-- TOC entry 5927 (class 0 OID 87898)
-- Dependencies: 276
-- Data for Name: tipo_vinculacion; Type: TABLE DATA; Schema: catedras; Owner: -
--

INSERT INTO catedras.tipo_vinculacion VALUES ('ESTUDIANTE', 'Estudiante USB', true, true, true);
INSERT INTO catedras.tipo_vinculacion VALUES ('DOCENTE', 'Docente USB', true, true, true);
INSERT INTO catedras.tipo_vinculacion VALUES ('ADMINISTRATIVO', 'Administrativo USB', true, false, true);
INSERT INTO catedras.tipo_vinculacion VALUES ('EGRESADO', 'Egresado USB', true, true, true);
INSERT INTO catedras.tipo_vinculacion VALUES ('RUTAS_PAZ', 'Rutas de paz y formacion', true, false, true);
INSERT INTO catedras.tipo_vinculacion VALUES ('COLEGIO', 'Colegio invitado', false, false, true);
INSERT INTO catedras.tipo_vinculacion VALUES ('EXTERNO', 'Publico externo', false, false, true);


--
-- TOC entry 5928 (class 0 OID 87903)
-- Dependencies: 277
-- Data for Name: usuario; Type: TABLE DATA; Schema: catedras; Owner: -
--



--
-- TOC entry 5930 (class 0 OID 87915)
-- Dependencies: 280
-- Data for Name: vinculacion_asistente; Type: TABLE DATA; Schema: catedras; Owner: -
--



--
-- TOC entry 6004 (class 0 OID 0)
-- Dependencies: 224
-- Name: alias_programa_id_alias_seq; Type: SEQUENCE SET; Schema: catedras; Owner: -
--



--
-- TOC entry 6005 (class 0 OID 0)
-- Dependencies: 226
-- Name: asistente_id_asistente_seq; Type: SEQUENCE SET; Schema: catedras; Owner: -
--



--
-- TOC entry 6006 (class 0 OID 0)
-- Dependencies: 228
-- Name: bitacora_id_bitacora_seq; Type: SEQUENCE SET; Schema: catedras; Owner: -
--



--
-- TOC entry 6007 (class 0 OID 0)
-- Dependencies: 230
-- Name: catedra_id_catedra_seq; Type: SEQUENCE SET; Schema: catedras; Owner: -
--



--
-- TOC entry 6008 (class 0 OID 0)
-- Dependencies: 232
-- Name: clave_acceso_id_clave_seq; Type: SEQUENCE SET; Schema: catedras; Owner: -
--



--
-- TOC entry 6009 (class 0 OID 0)
-- Dependencies: 234
-- Name: consentimiento_datos_id_consentimiento_seq; Type: SEQUENCE SET; Schema: catedras; Owner: -
--



--
-- TOC entry 6010 (class 0 OID 0)
-- Dependencies: 237
-- Name: detalle_migracion_id_detalle_seq; Type: SEQUENCE SET; Schema: catedras; Owner: -
--



--
-- TOC entry 6011 (class 0 OID 0)
-- Dependencies: 239
-- Name: documento_asistente_id_documento_seq; Type: SEQUENCE SET; Schema: catedras; Owner: -
--



--
-- TOC entry 6012 (class 0 OID 0)
-- Dependencies: 241
-- Name: encuesta_id_encuesta_seq; Type: SEQUENCE SET; Schema: catedras; Owner: -
--



--
-- TOC entry 6013 (class 0 OID 0)
-- Dependencies: 243
-- Name: enlace_registro_id_enlace_seq; Type: SEQUENCE SET; Schema: catedras; Owner: -
--



--
-- TOC entry 6014 (class 0 OID 0)
-- Dependencies: 247
-- Name: lote_carga_asistente_id_lote_carga_seq; Type: SEQUENCE SET; Schema: catedras; Owner: -
--



--
-- TOC entry 6015 (class 0 OID 0)
-- Dependencies: 249
-- Name: lote_migracion_id_lote_migracion_seq; Type: SEQUENCE SET; Schema: catedras; Owner: -
--



--
-- TOC entry 6016 (class 0 OID 0)
-- Dependencies: 252
-- Name: novedad_carga_id_novedad_seq; Type: SEQUENCE SET; Schema: catedras; Owner: -
--



--
-- TOC entry 6017 (class 0 OID 0)
-- Dependencies: 254
-- Name: opcion_pregunta_id_opcion_seq; Type: SEQUENCE SET; Schema: catedras; Owner: -
--



--
-- TOC entry 6018 (class 0 OID 0)
-- Dependencies: 259
-- Name: pregunta_id_pregunta_seq; Type: SEQUENCE SET; Schema: catedras; Owner: -
--



--
-- TOC entry 6019 (class 0 OID 0)
-- Dependencies: 263
-- Name: registro_asistencia_id_registro_seq; Type: SEQUENCE SET; Schema: catedras; Owner: -
--



--
-- TOC entry 6020 (class 0 OID 0)
-- Dependencies: 265
-- Name: respuesta_encuesta_id_respuesta_seq; Type: SEQUENCE SET; Schema: catedras; Owner: -
--



--
-- TOC entry 6021 (class 0 OID 0)
-- Dependencies: 267
-- Name: respuesta_item_id_item_seq; Type: SEQUENCE SET; Schema: catedras; Owner: -
--



--
-- TOC entry 6022 (class 0 OID 0)
-- Dependencies: 272
-- Name: sesion_id_sesion_seq; Type: SEQUENCE SET; Schema: catedras; Owner: -
--



--
-- TOC entry 6023 (class 0 OID 0)
-- Dependencies: 278
-- Name: usuario_id_usuario_seq; Type: SEQUENCE SET; Schema: catedras; Owner: -
--



--
-- TOC entry 5560 (class 2606 OID 87945)
-- Name: enlace_registro ex_enlace_sin_solape; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.enlace_registro
    ADD CONSTRAINT ex_enlace_sin_solape EXCLUDE USING gist (fk_sesion WITH =, ventana WITH &&) WHERE ((revocado_en IS NULL));


--
-- TOC entry 6024 (class 0 OID 0)
-- Dependencies: 5560
-- Name: CONSTRAINT ex_enlace_sin_solape ON enlace_registro; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON CONSTRAINT ex_enlace_sin_solape ON catedras.enlace_registro IS 'RN-36. El WHERE hace que los enlaces revocados no estorben: se puede revocar uno y emitir otro para la misma franja.';


--
-- TOC entry 5509 (class 2606 OID 87947)
-- Name: alias_programa pk_alias_programa; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.alias_programa
    ADD CONSTRAINT pk_alias_programa PRIMARY KEY (id_alias);


--
-- TOC entry 5514 (class 2606 OID 87949)
-- Name: asistente pk_asistente; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.asistente
    ADD CONSTRAINT pk_asistente PRIMARY KEY (id_asistente);


--
-- TOC entry 5522 (class 2606 OID 87951)
-- Name: bitacora pk_bitacora; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.bitacora
    ADD CONSTRAINT pk_bitacora PRIMARY KEY (id_bitacora);


--
-- TOC entry 5526 (class 2606 OID 87953)
-- Name: catedra pk_catedra; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.catedra
    ADD CONSTRAINT pk_catedra PRIMARY KEY (id_catedra);


--
-- TOC entry 5534 (class 2606 OID 87955)
-- Name: clave_acceso pk_clave_acceso; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.clave_acceso
    ADD CONSTRAINT pk_clave_acceso PRIMARY KEY (id_clave);


--
-- TOC entry 5536 (class 2606 OID 87957)
-- Name: consentimiento_datos pk_consentimiento; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.consentimiento_datos
    ADD CONSTRAINT pk_consentimiento PRIMARY KEY (id_consentimiento);


--
-- TOC entry 5540 (class 2606 OID 87959)
-- Name: dependencia pk_dependencia; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.dependencia
    ADD CONSTRAINT pk_dependencia PRIMARY KEY (id_dependencia);


--
-- TOC entry 5545 (class 2606 OID 87961)
-- Name: detalle_migracion pk_detalle_migracion; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.detalle_migracion
    ADD CONSTRAINT pk_detalle_migracion PRIMARY KEY (id_detalle);


--
-- TOC entry 5551 (class 2606 OID 87963)
-- Name: documento_asistente pk_documento_asistente; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.documento_asistente
    ADD CONSTRAINT pk_documento_asistente PRIMARY KEY (id_documento);


--
-- TOC entry 5556 (class 2606 OID 87965)
-- Name: encuesta pk_encuesta; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.encuesta
    ADD CONSTRAINT pk_encuesta PRIMARY KEY (id_encuesta);


--
-- TOC entry 5563 (class 2606 OID 87967)
-- Name: enlace_registro pk_enlace_registro; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.enlace_registro
    ADD CONSTRAINT pk_enlace_registro PRIMARY KEY (id_enlace);


--
-- TOC entry 5568 (class 2606 OID 87969)
-- Name: estado_proceso pk_estado_proceso; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.estado_proceso
    ADD CONSTRAINT pk_estado_proceso PRIMARY KEY (id_estado);


--
-- TOC entry 5570 (class 2606 OID 87971)
-- Name: facultad pk_facultad; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.facultad
    ADD CONSTRAINT pk_facultad PRIMARY KEY (id_facultad);


--
-- TOC entry 5574 (class 2606 OID 87973)
-- Name: lote_carga_asistente pk_lote_carga; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.lote_carga_asistente
    ADD CONSTRAINT pk_lote_carga PRIMARY KEY (id_lote_carga);


--
-- TOC entry 5577 (class 2606 OID 87975)
-- Name: lote_migracion pk_lote_migracion; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.lote_migracion
    ADD CONSTRAINT pk_lote_migracion PRIMARY KEY (id_lote_migracion);


--
-- TOC entry 5579 (class 2606 OID 87977)
-- Name: modalidad pk_modalidad; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.modalidad
    ADD CONSTRAINT pk_modalidad PRIMARY KEY (id_modalidad);


--
-- TOC entry 5582 (class 2606 OID 87979)
-- Name: novedad_carga pk_novedad_carga; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.novedad_carga
    ADD CONSTRAINT pk_novedad_carga PRIMARY KEY (id_novedad);


--
-- TOC entry 5584 (class 2606 OID 87981)
-- Name: opcion_pregunta pk_opcion_pregunta; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.opcion_pregunta
    ADD CONSTRAINT pk_opcion_pregunta PRIMARY KEY (id_opcion);


--
-- TOC entry 5588 (class 2606 OID 87983)
-- Name: parametro pk_parametro; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.parametro
    ADD CONSTRAINT pk_parametro PRIMARY KEY (clave);


--
-- TOC entry 5590 (class 2606 OID 87985)
-- Name: periodo_academico pk_periodo_academico; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.periodo_academico
    ADD CONSTRAINT pk_periodo_academico PRIMARY KEY (id_periodo);


--
-- TOC entry 5592 (class 2606 OID 87987)
-- Name: ponencia pk_ponencia; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.ponencia
    ADD CONSTRAINT pk_ponencia PRIMARY KEY (fk_sesion, fk_asistente, rol);


--
-- TOC entry 5595 (class 2606 OID 87989)
-- Name: pregunta pk_pregunta; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.pregunta
    ADD CONSTRAINT pk_pregunta PRIMARY KEY (id_pregunta);


--
-- TOC entry 5600 (class 2606 OID 87991)
-- Name: programa_academico pk_programa_academico; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.programa_academico
    ADD CONSTRAINT pk_programa_academico PRIMARY KEY (codigo);


--
-- TOC entry 5604 (class 2606 OID 87993)
-- Name: programa_asistente pk_programa_asistente; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.programa_asistente
    ADD CONSTRAINT pk_programa_asistente PRIMARY KEY (fk_asistente, fk_programa, fk_periodo);


--
-- TOC entry 5611 (class 2606 OID 87995)
-- Name: registro_asistencia pk_registro_asistencia; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.registro_asistencia
    ADD CONSTRAINT pk_registro_asistencia PRIMARY KEY (id_registro);


--
-- TOC entry 5616 (class 2606 OID 87997)
-- Name: respuesta_encuesta pk_respuesta_encuesta; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.respuesta_encuesta
    ADD CONSTRAINT pk_respuesta_encuesta PRIMARY KEY (id_respuesta);


--
-- TOC entry 5621 (class 2606 OID 87999)
-- Name: respuesta_item pk_respuesta_item; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.respuesta_item
    ADD CONSTRAINT pk_respuesta_item PRIMARY KEY (id_item);


--
-- TOC entry 5625 (class 2606 OID 88001)
-- Name: rol pk_rol; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.rol
    ADD CONSTRAINT pk_rol PRIMARY KEY (id_rol);


--
-- TOC entry 5630 (class 2606 OID 88003)
-- Name: rol_por_usuario pk_rol_por_usuario; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.rol_por_usuario
    ADD CONSTRAINT pk_rol_por_usuario PRIMARY KEY (fk_usuario, fk_rol, fecha_ini);


--
-- TOC entry 6025 (class 0 OID 0)
-- Dependencies: 5630
-- Name: CONSTRAINT pk_rol_por_usuario ON rol_por_usuario; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON CONSTRAINT pk_rol_por_usuario ON catedras.rol_por_usuario IS 'RN-31 · fecha_ini EN LA CLAVE PRIMARIA. Sin ella, quien fue administrador, dejo de serlo y vuelve, quedaria bloqueado para siempre. Es el mismo error que en vinculacion_asistente, y falla igual de silenciosamente.';


--
-- TOC entry 5632 (class 2606 OID 88005)
-- Name: sede pk_sede; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.sede
    ADD CONSTRAINT pk_sede PRIMARY KEY (id_sede);


--
-- TOC entry 5640 (class 2606 OID 88007)
-- Name: sesion pk_sesion; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.sesion
    ADD CONSTRAINT pk_sesion PRIMARY KEY (id_sesion);


--
-- TOC entry 5644 (class 2606 OID 88009)
-- Name: tipo_documento pk_tipo_documento; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.tipo_documento
    ADD CONSTRAINT pk_tipo_documento PRIMARY KEY (id_tipo_documento);


--
-- TOC entry 5648 (class 2606 OID 88011)
-- Name: tipo_evento pk_tipo_evento; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.tipo_evento
    ADD CONSTRAINT pk_tipo_evento PRIMARY KEY (id_tipo_evento);


--
-- TOC entry 5650 (class 2606 OID 88013)
-- Name: tipo_pregunta pk_tipo_pregunta; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.tipo_pregunta
    ADD CONSTRAINT pk_tipo_pregunta PRIMARY KEY (id_tipo_pregunta);


--
-- TOC entry 5652 (class 2606 OID 88015)
-- Name: tipo_vinculacion pk_tipo_vinculacion; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.tipo_vinculacion
    ADD CONSTRAINT pk_tipo_vinculacion PRIMARY KEY (id_tipo_vinculacion);


--
-- TOC entry 5656 (class 2606 OID 88017)
-- Name: usuario pk_usuario; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.usuario
    ADD CONSTRAINT pk_usuario PRIMARY KEY (id_usuario);


--
-- TOC entry 5663 (class 2606 OID 88019)
-- Name: vinculacion_asistente pk_vinculacion_asistente; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.vinculacion_asistente
    ADD CONSTRAINT pk_vinculacion_asistente PRIMARY KEY (fk_asistente, fk_tipo_vinculacion, fecha_ini);


--
-- TOC entry 6026 (class 0 OID 0)
-- Dependencies: 5663
-- Name: CONSTRAINT pk_vinculacion_asistente ON vinculacion_asistente; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON CONSTRAINT pk_vinculacion_asistente ON catedras.vinculacion_asistente IS 'fecha_ini ENTRA EN LA CLAVE PRIMARIA. Sin ella, un egresado que se matricula en una maestria no podria volver a ser estudiante: el INSERT se rechazaria y el historico quedaria incompleto en silencio.';


--
-- TOC entry 5511 (class 2606 OID 88021)
-- Name: alias_programa uq_alias_texto; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.alias_programa
    ADD CONSTRAINT uq_alias_texto UNIQUE (texto_normalizado);


--
-- TOC entry 5516 (class 2606 OID 88023)
-- Name: asistente uq_asistente_asis; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.asistente
    ADD CONSTRAINT uq_asistente_asis UNIQUE (id_asis);


--
-- TOC entry 5518 (class 2606 OID 88025)
-- Name: asistente uq_asistente_corinst; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.asistente
    ADD CONSTRAINT uq_asistente_corinst UNIQUE (correo_institucional);


--
-- TOC entry 5528 (class 2606 OID 88027)
-- Name: catedra uq_catedra_asis; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.catedra
    ADD CONSTRAINT uq_catedra_asis UNIQUE (id_evento_asis);


--
-- TOC entry 5538 (class 2606 OID 88029)
-- Name: consentimiento_datos uq_consentimiento_ver; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.consentimiento_datos
    ADD CONSTRAINT uq_consentimiento_ver UNIQUE (fk_asistente, version_politica);


--
-- TOC entry 5542 (class 2606 OID 88031)
-- Name: dependencia uq_dependencia_nombre; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.dependencia
    ADD CONSTRAINT uq_dependencia_nombre UNIQUE (nombre);


--
-- TOC entry 5547 (class 2606 OID 88033)
-- Name: detalle_migracion uq_detalle_lote_reg; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.detalle_migracion
    ADD CONSTRAINT uq_detalle_lote_reg UNIQUE (fk_lote_migracion, fk_registro);


--
-- TOC entry 5553 (class 2606 OID 88035)
-- Name: documento_asistente uq_documento_numero; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.documento_asistente
    ADD CONSTRAINT uq_documento_numero UNIQUE (fk_tipo_documento, numero);


--
-- TOC entry 5558 (class 2606 OID 88037)
-- Name: encuesta uq_encuesta_ver; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.encuesta
    ADD CONSTRAINT uq_encuesta_ver UNIQUE (nombre, version);


--
-- TOC entry 5565 (class 2606 OID 88039)
-- Name: enlace_registro uq_enlace_token; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.enlace_registro
    ADD CONSTRAINT uq_enlace_token UNIQUE (token);


--
-- TOC entry 5572 (class 2606 OID 88041)
-- Name: facultad uq_facultad_nombre; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.facultad
    ADD CONSTRAINT uq_facultad_nombre UNIQUE (nombre);


--
-- TOC entry 5586 (class 2606 OID 88043)
-- Name: opcion_pregunta uq_opcion_orden; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.opcion_pregunta
    ADD CONSTRAINT uq_opcion_orden UNIQUE (fk_pregunta, orden);


--
-- TOC entry 5597 (class 2606 OID 88045)
-- Name: pregunta uq_pregunta_ord; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.pregunta
    ADD CONSTRAINT uq_pregunta_ord UNIQUE (fk_encuesta, orden);


--
-- TOC entry 5613 (class 2606 OID 88047)
-- Name: registro_asistencia uq_registro_sesion_asis; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.registro_asistencia
    ADD CONSTRAINT uq_registro_sesion_asis UNIQUE (fk_sesion, fk_asistente);


--
-- TOC entry 5623 (class 2606 OID 88049)
-- Name: respuesta_item uq_respuesta_item; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.respuesta_item
    ADD CONSTRAINT uq_respuesta_item UNIQUE (fk_respuesta, fk_pregunta);


--
-- TOC entry 5618 (class 2606 OID 88051)
-- Name: respuesta_encuesta uq_respuesta_registro; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.respuesta_encuesta
    ADD CONSTRAINT uq_respuesta_registro UNIQUE (fk_registro);


--
-- TOC entry 5627 (class 2606 OID 88053)
-- Name: rol uq_rol_nombre; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.rol
    ADD CONSTRAINT uq_rol_nombre UNIQUE (nombre);


--
-- TOC entry 5634 (class 2606 OID 88055)
-- Name: sede uq_sede_nombre; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.sede
    ADD CONSTRAINT uq_sede_nombre UNIQUE (nombre);


--
-- TOC entry 5642 (class 2606 OID 88057)
-- Name: sesion uq_sesion_reunion; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.sesion
    ADD CONSTRAINT uq_sesion_reunion UNIQUE (fk_catedra, numero_reunion);


--
-- TOC entry 5646 (class 2606 OID 88059)
-- Name: tipo_documento uq_tipo_documento_nom; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.tipo_documento
    ADD CONSTRAINT uq_tipo_documento_nom UNIQUE (nombre);


--
-- TOC entry 5654 (class 2606 OID 88061)
-- Name: tipo_vinculacion uq_tipo_vinculacion_nom; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.tipo_vinculacion
    ADD CONSTRAINT uq_tipo_vinculacion_nom UNIQUE (nombre);


--
-- TOC entry 5658 (class 2606 OID 88063)
-- Name: usuario uq_usuario_login; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.usuario
    ADD CONSTRAINT uq_usuario_login UNIQUE (usuario);


--
-- TOC entry 5660 (class 2606 OID 88065)
-- Name: usuario uq_usuario_persona; Type: CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.usuario
    ADD CONSTRAINT uq_usuario_persona UNIQUE (fk_asistente);


--
-- TOC entry 5507 (class 1259 OID 88066)
-- Name: ix_alias_trgm; Type: INDEX; Schema: catedras; Owner: -
--

CREATE INDEX ix_alias_trgm ON catedras.alias_programa USING gin (texto_normalizado public.gin_trgm_ops);


--
-- TOC entry 6027 (class 0 OID 0)
-- Dependencies: 5507
-- Name: INDEX ix_alias_trgm; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON INDEX catedras.ix_alias_trgm IS 'Indice de trigramas. Permite resolver "Psicologica" -> PSICOLOGIA y "Ingenieria de datos y software" -> M0286 por similitud, no por igualdad. En otro motor esto exigiria una tabla de equivalencias exactas o trabajo manual sobre cada fila.';


--
-- TOC entry 5512 (class 1259 OID 88067)
-- Name: ix_asistente_correo_personal; Type: INDEX; Schema: catedras; Owner: -
--

CREATE INDEX ix_asistente_correo_personal ON catedras.asistente USING btree (correo_personal) WHERE (correo_personal IS NOT NULL);


--
-- TOC entry 5519 (class 1259 OID 88068)
-- Name: ix_bitacora_fecha; Type: INDEX; Schema: catedras; Owner: -
--

CREATE INDEX ix_bitacora_fecha ON catedras.bitacora USING btree (ocurrido_en DESC);


--
-- TOC entry 5520 (class 1259 OID 88069)
-- Name: ix_bitacora_tabla; Type: INDEX; Schema: catedras; Owner: -
--

CREATE INDEX ix_bitacora_tabla ON catedras.bitacora USING btree (nombre_tabla, ocurrido_en DESC);


--
-- TOC entry 5523 (class 1259 OID 88070)
-- Name: ix_catedra_depend; Type: INDEX; Schema: catedras; Owner: -
--

CREATE INDEX ix_catedra_depend ON catedras.catedra USING btree (fk_dependencia) WHERE (fk_dependencia IS NOT NULL);


--
-- TOC entry 5524 (class 1259 OID 88071)
-- Name: ix_catedra_tipo; Type: INDEX; Schema: catedras; Owner: -
--

CREATE INDEX ix_catedra_tipo ON catedras.catedra USING btree (fk_tipo_evento);


--
-- TOC entry 5529 (class 1259 OID 88072)
-- Name: ix_clave_asistente; Type: INDEX; Schema: catedras; Owner: -
--

CREATE INDEX ix_clave_asistente ON catedras.clave_acceso USING btree (fk_asistente, generado_en DESC);


--
-- TOC entry 5530 (class 1259 OID 88073)
-- Name: ix_clave_enlace; Type: INDEX; Schema: catedras; Owner: -
--

CREATE INDEX ix_clave_enlace ON catedras.clave_acceso USING btree (fk_enlace);


--
-- TOC entry 5531 (class 1259 OID 88074)
-- Name: ix_clave_intentos; Type: INDEX; Schema: catedras; Owner: -
--

CREATE INDEX ix_clave_intentos ON catedras.clave_acceso USING btree (fk_asistente, intentos) WHERE (intentos > 0);


--
-- TOC entry 5532 (class 1259 OID 88075)
-- Name: ix_clave_no_institucional; Type: INDEX; Schema: catedras; Owner: -
--

CREATE INDEX ix_clave_no_institucional ON catedras.clave_acceso USING btree (generado_en DESC) WHERE (NOT es_correo_institucional);


--
-- TOC entry 6028 (class 0 OID 0)
-- Dependencies: 5532
-- Name: INDEX ix_clave_no_institucional; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON INDEX catedras.ix_clave_no_institucional IS 'I18. Alimenta la decision que el profesor Carlos Castro dejo pendiente en 55:32 sobre los correos no institucionales.';


--
-- TOC entry 5543 (class 1259 OID 88076)
-- Name: ix_detmig_registro; Type: INDEX; Schema: catedras; Owner: -
--

CREATE INDEX ix_detmig_registro ON catedras.detalle_migracion USING btree (fk_registro);


--
-- TOC entry 5549 (class 1259 OID 88077)
-- Name: ix_documento_numero; Type: INDEX; Schema: catedras; Owner: -
--

CREATE INDEX ix_documento_numero ON catedras.documento_asistente USING btree (numero);


--
-- TOC entry 6029 (class 0 OID 0)
-- Dependencies: 5549
-- Name: INDEX ix_documento_numero; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON INDEX catedras.ix_documento_numero IS 'La persona digita su cedula sin decir que tipo de documento es. Sin este indice, uq_documento_numero no sirve porque su primera columna es el tipo.';


--
-- TOC entry 5561 (class 1259 OID 88078)
-- Name: ix_enlace_ventana; Type: INDEX; Schema: catedras; Owner: -
--

CREATE INDEX ix_enlace_ventana ON catedras.enlace_registro USING gist (ventana);


--
-- TOC entry 5575 (class 1259 OID 88079)
-- Name: ix_lotemig_sesion; Type: INDEX; Schema: catedras; Owner: -
--

CREATE INDEX ix_lotemig_sesion ON catedras.lote_migracion USING btree (fk_sesion, generado_en DESC);


--
-- TOC entry 5580 (class 1259 OID 88080)
-- Name: ix_novedad_lote; Type: INDEX; Schema: catedras; Owner: -
--

CREATE INDEX ix_novedad_lote ON catedras.novedad_carga USING btree (fk_lote_carga);


--
-- TOC entry 5593 (class 1259 OID 88081)
-- Name: ix_pregunta_encuesta; Type: INDEX; Schema: catedras; Owner: -
--

CREATE INDEX ix_pregunta_encuesta ON catedras.pregunta USING btree (fk_encuesta, orden);


--
-- TOC entry 5601 (class 1259 OID 88082)
-- Name: ix_progasi_periodo; Type: INDEX; Schema: catedras; Owner: -
--

CREATE INDEX ix_progasi_periodo ON catedras.programa_asistente USING btree (fk_periodo);


--
-- TOC entry 5602 (class 1259 OID 88083)
-- Name: ix_progasi_programa; Type: INDEX; Schema: catedras; Owner: -
--

CREATE INDEX ix_progasi_programa ON catedras.programa_asistente USING btree (fk_programa);


--
-- TOC entry 5598 (class 1259 OID 88084)
-- Name: ix_programa_facultad; Type: INDEX; Schema: catedras; Owner: -
--

CREATE INDEX ix_programa_facultad ON catedras.programa_academico USING btree (fk_facultad);


--
-- TOC entry 5605 (class 1259 OID 88085)
-- Name: ix_registro_asistente; Type: INDEX; Schema: catedras; Owner: -
--

CREATE INDEX ix_registro_asistente ON catedras.registro_asistencia USING btree (fk_asistente);


--
-- TOC entry 5606 (class 1259 OID 88086)
-- Name: ix_registro_fecha; Type: INDEX; Schema: catedras; Owner: -
--

CREATE INDEX ix_registro_fecha ON catedras.registro_asistencia USING btree (registrado_en DESC);


--
-- TOC entry 5607 (class 1259 OID 88087)
-- Name: ix_registro_programa_snap; Type: INDEX; Schema: catedras; Owner: -
--

CREATE INDEX ix_registro_programa_snap ON catedras.registro_asistencia USING btree (fk_programa_snapshot) WHERE (fk_programa_snapshot IS NOT NULL);


--
-- TOC entry 5608 (class 1259 OID 88088)
-- Name: ix_registro_sesion; Type: INDEX; Schema: catedras; Owner: -
--

CREATE INDEX ix_registro_sesion ON catedras.registro_asistencia USING btree (fk_sesion, registrado_en);


--
-- TOC entry 5609 (class 1259 OID 88089)
-- Name: ix_registro_vinc_snap; Type: INDEX; Schema: catedras; Owner: -
--

CREATE INDEX ix_registro_vinc_snap ON catedras.registro_asistencia USING btree (fk_vinculacion_snapshot);


--
-- TOC entry 5614 (class 1259 OID 88090)
-- Name: ix_respenc_encuesta; Type: INDEX; Schema: catedras; Owner: -
--

CREATE INDEX ix_respenc_encuesta ON catedras.respuesta_encuesta USING btree (fk_encuesta);


--
-- TOC entry 5619 (class 1259 OID 88091)
-- Name: ix_respitem_pregunta; Type: INDEX; Schema: catedras; Owner: -
--

CREATE INDEX ix_respitem_pregunta ON catedras.respuesta_item USING btree (fk_pregunta);


--
-- TOC entry 5628 (class 1259 OID 88092)
-- Name: ix_rolusr_vigente; Type: INDEX; Schema: catedras; Owner: -
--

CREATE INDEX ix_rolusr_vigente ON catedras.rol_por_usuario USING btree (fk_usuario) WHERE (fecha_fin IS NULL);


--
-- TOC entry 5635 (class 1259 OID 88093)
-- Name: ix_sesion_inicio; Type: INDEX; Schema: catedras; Owner: -
--

CREATE INDEX ix_sesion_inicio ON catedras.sesion USING btree (inicio DESC);


--
-- TOC entry 5636 (class 1259 OID 88094)
-- Name: ix_sesion_modalidad; Type: INDEX; Schema: catedras; Owner: -
--

CREATE INDEX ix_sesion_modalidad ON catedras.sesion USING btree (fk_modalidad);


--
-- TOC entry 5637 (class 1259 OID 88095)
-- Name: ix_sesion_periodo; Type: INDEX; Schema: catedras; Owner: -
--

CREATE INDEX ix_sesion_periodo ON catedras.sesion USING btree (fk_periodo);


--
-- TOC entry 5638 (class 1259 OID 88096)
-- Name: ix_sesion_sede; Type: INDEX; Schema: catedras; Owner: -
--

CREATE INDEX ix_sesion_sede ON catedras.sesion USING btree (fk_sede);


--
-- TOC entry 5661 (class 1259 OID 88097)
-- Name: ix_vinculacion_vigente; Type: INDEX; Schema: catedras; Owner: -
--

CREATE INDEX ix_vinculacion_vigente ON catedras.vinculacion_asistente USING btree (fk_asistente, fecha_ini DESC) WHERE (fecha_fin IS NULL);


--
-- TOC entry 5548 (class 1259 OID 88098)
-- Name: ux_detmig_aceptado; Type: INDEX; Schema: catedras; Owner: -
--

CREATE UNIQUE INDEX ux_detmig_aceptado ON catedras.detalle_migracion USING btree (fk_registro) WHERE ((resultado)::text = 'ACEPTADO'::text);


--
-- TOC entry 6030 (class 0 OID 0)
-- Dependencies: 5548
-- Name: INDEX ux_detmig_aceptado; Type: COMMENT; Schema: catedras; Owner: -
--

COMMENT ON INDEX catedras.ux_detmig_aceptado IS 'RN-29. Permite reintentar cuantas veces haga falta y garantiza que la asistencia no se cargue dos veces en el ASIS.';


--
-- TOC entry 5554 (class 1259 OID 88099)
-- Name: ux_documento_vigente; Type: INDEX; Schema: catedras; Owner: -
--

CREATE UNIQUE INDEX ux_documento_vigente ON catedras.documento_asistente USING btree (fk_asistente) WHERE vigente;


--
-- TOC entry 5566 (class 1259 OID 88100)
-- Name: ux_enlace_vigente; Type: INDEX; Schema: catedras; Owner: -
--

CREATE UNIQUE INDEX ux_enlace_vigente ON catedras.enlace_registro USING btree (fk_sesion) WHERE (revocado_en IS NULL);


--
-- TOC entry 5716 (class 2620 OID 88101)
-- Name: asistente trg_bitacora_asistente; Type: TRIGGER; Schema: catedras; Owner: -
--

CREATE TRIGGER trg_bitacora_asistente AFTER DELETE OR UPDATE ON catedras.asistente FOR EACH ROW EXECUTE FUNCTION catedras.fn_trg_bitacora();


--
-- TOC entry 5719 (class 2620 OID 88102)
-- Name: registro_asistencia trg_bitacora_registro; Type: TRIGGER; Schema: catedras; Owner: -
--

CREATE TRIGGER trg_bitacora_registro AFTER INSERT OR DELETE OR UPDATE ON catedras.registro_asistencia FOR EACH ROW EXECUTE FUNCTION catedras.fn_trg_bitacora();


--
-- TOC entry 5717 (class 2620 OID 88103)
-- Name: clave_acceso trg_clave_derivar; Type: TRIGGER; Schema: catedras; Owner: -
--

CREATE TRIGGER trg_clave_derivar BEFORE INSERT OR UPDATE OF enviado_a ON catedras.clave_acceso FOR EACH ROW EXECUTE FUNCTION catedras.fn_trg_clave_derivar();


--
-- TOC entry 5718 (class 2620 OID 88104)
-- Name: enlace_registro trg_enlace_canal; Type: TRIGGER; Schema: catedras; Owner: -
--

CREATE TRIGGER trg_enlace_canal BEFORE INSERT OR UPDATE OF canal, fk_sesion ON catedras.enlace_registro FOR EACH ROW EXECUTE FUNCTION catedras.fn_trg_enlace_canal();


--
-- TOC entry 5720 (class 2620 OID 88105)
-- Name: registro_asistencia trg_registro_validar; Type: TRIGGER; Schema: catedras; Owner: -
--

CREATE TRIGGER trg_registro_validar BEFORE INSERT ON catedras.registro_asistencia FOR EACH ROW EXECUTE FUNCTION catedras.fn_trg_registro_validar();


--
-- TOC entry 5721 (class 2620 OID 88106)
-- Name: respuesta_item trg_respuesta_item_tipo; Type: TRIGGER; Schema: catedras; Owner: -
--

CREATE TRIGGER trg_respuesta_item_tipo BEFORE INSERT OR UPDATE ON catedras.respuesta_item FOR EACH ROW EXECUTE FUNCTION catedras.fn_trg_respuesta_item_tipo();


--
-- TOC entry 5722 (class 2620 OID 88107)
-- Name: sesion trg_sesion_consecutivo; Type: TRIGGER; Schema: catedras; Owner: -
--

CREATE TRIGGER trg_sesion_consecutivo BEFORE INSERT ON catedras.sesion FOR EACH ROW EXECUTE FUNCTION catedras.fn_trg_sesion_consecutivo();


--
-- TOC entry 5664 (class 2606 OID 88108)
-- Name: alias_programa fk_alias_programa; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.alias_programa
    ADD CONSTRAINT fk_alias_programa FOREIGN KEY (fk_programa) REFERENCES catedras.programa_academico(codigo) ON UPDATE CASCADE ON DELETE CASCADE;


--
-- TOC entry 5665 (class 2606 OID 88113)
-- Name: asistente fk_asistente_lote; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.asistente
    ADD CONSTRAINT fk_asistente_lote FOREIGN KEY (fk_lote_carga) REFERENCES catedras.lote_carga_asistente(id_lote_carga) ON UPDATE CASCADE ON DELETE SET NULL;


--
-- TOC entry 5666 (class 2606 OID 88118)
-- Name: bitacora fk_bitacora_usuario; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.bitacora
    ADD CONSTRAINT fk_bitacora_usuario FOREIGN KEY (fk_usuario) REFERENCES catedras.usuario(id_usuario) ON UPDATE CASCADE ON DELETE SET NULL;


--
-- TOC entry 5667 (class 2606 OID 88123)
-- Name: catedra fk_catedra_dependencia; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.catedra
    ADD CONSTRAINT fk_catedra_dependencia FOREIGN KEY (fk_dependencia) REFERENCES catedras.dependencia(id_dependencia) ON UPDATE CASCADE ON DELETE SET NULL;


--
-- TOC entry 5668 (class 2606 OID 88128)
-- Name: catedra fk_catedra_tipo_evento; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.catedra
    ADD CONSTRAINT fk_catedra_tipo_evento FOREIGN KEY (fk_tipo_evento) REFERENCES catedras.tipo_evento(id_tipo_evento) ON UPDATE CASCADE ON DELETE RESTRICT;


--
-- TOC entry 5669 (class 2606 OID 88133)
-- Name: clave_acceso fk_clave_asistente; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.clave_acceso
    ADD CONSTRAINT fk_clave_asistente FOREIGN KEY (fk_asistente) REFERENCES catedras.asistente(id_asistente) ON UPDATE CASCADE ON DELETE CASCADE;


--
-- TOC entry 5670 (class 2606 OID 88138)
-- Name: clave_acceso fk_clave_enlace; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.clave_acceso
    ADD CONSTRAINT fk_clave_enlace FOREIGN KEY (fk_enlace) REFERENCES catedras.enlace_registro(id_enlace) ON UPDATE CASCADE ON DELETE CASCADE;


--
-- TOC entry 5671 (class 2606 OID 88143)
-- Name: consentimiento_datos fk_consentimiento_asistente; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.consentimiento_datos
    ADD CONSTRAINT fk_consentimiento_asistente FOREIGN KEY (fk_asistente) REFERENCES catedras.asistente(id_asistente) ON UPDATE CASCADE ON DELETE CASCADE;


--
-- TOC entry 5672 (class 2606 OID 88148)
-- Name: dependencia fk_dependencia_padre; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.dependencia
    ADD CONSTRAINT fk_dependencia_padre FOREIGN KEY (fk_dependencia_padre) REFERENCES catedras.dependencia(id_dependencia) ON UPDATE CASCADE ON DELETE SET NULL;


--
-- TOC entry 5673 (class 2606 OID 88153)
-- Name: detalle_migracion fk_detmig_lote; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.detalle_migracion
    ADD CONSTRAINT fk_detmig_lote FOREIGN KEY (fk_lote_migracion) REFERENCES catedras.lote_migracion(id_lote_migracion) ON UPDATE CASCADE ON DELETE CASCADE;


--
-- TOC entry 5674 (class 2606 OID 88158)
-- Name: detalle_migracion fk_detmig_registro; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.detalle_migracion
    ADD CONSTRAINT fk_detmig_registro FOREIGN KEY (fk_registro) REFERENCES catedras.registro_asistencia(id_registro) ON UPDATE CASCADE ON DELETE RESTRICT;


--
-- TOC entry 5675 (class 2606 OID 88163)
-- Name: documento_asistente fk_documento_asistente; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.documento_asistente
    ADD CONSTRAINT fk_documento_asistente FOREIGN KEY (fk_asistente) REFERENCES catedras.asistente(id_asistente) ON UPDATE CASCADE ON DELETE CASCADE;


--
-- TOC entry 5676 (class 2606 OID 88168)
-- Name: documento_asistente fk_documento_tipo; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.documento_asistente
    ADD CONSTRAINT fk_documento_tipo FOREIGN KEY (fk_tipo_documento) REFERENCES catedras.tipo_documento(id_tipo_documento) ON UPDATE CASCADE ON DELETE RESTRICT;


--
-- TOC entry 5677 (class 2606 OID 88173)
-- Name: enlace_registro fk_enlace_sesion; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.enlace_registro
    ADD CONSTRAINT fk_enlace_sesion FOREIGN KEY (fk_sesion) REFERENCES catedras.sesion(id_sesion) ON UPDATE CASCADE ON DELETE CASCADE;


--
-- TOC entry 5678 (class 2606 OID 88178)
-- Name: enlace_registro fk_enlace_usuario; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.enlace_registro
    ADD CONSTRAINT fk_enlace_usuario FOREIGN KEY (fk_usuario_crea) REFERENCES catedras.usuario(id_usuario) ON UPDATE CASCADE ON DELETE SET NULL;


--
-- TOC entry 5679 (class 2606 OID 88183)
-- Name: facultad fk_facultad_padre; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.facultad
    ADD CONSTRAINT fk_facultad_padre FOREIGN KEY (fk_facultad_padre) REFERENCES catedras.facultad(id_facultad) ON UPDATE CASCADE ON DELETE SET NULL;


--
-- TOC entry 5680 (class 2606 OID 88188)
-- Name: lote_carga_asistente fk_lotecarga_usuario; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.lote_carga_asistente
    ADD CONSTRAINT fk_lotecarga_usuario FOREIGN KEY (fk_usuario) REFERENCES catedras.usuario(id_usuario) ON UPDATE CASCADE ON DELETE SET NULL;


--
-- TOC entry 5681 (class 2606 OID 88193)
-- Name: lote_migracion fk_lotemig_estado; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.lote_migracion
    ADD CONSTRAINT fk_lotemig_estado FOREIGN KEY (fk_estado_proceso) REFERENCES catedras.estado_proceso(id_estado) ON UPDATE CASCADE ON DELETE RESTRICT;


--
-- TOC entry 5682 (class 2606 OID 88198)
-- Name: lote_migracion fk_lotemig_sesion; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.lote_migracion
    ADD CONSTRAINT fk_lotemig_sesion FOREIGN KEY (fk_sesion) REFERENCES catedras.sesion(id_sesion) ON UPDATE CASCADE ON DELETE RESTRICT;


--
-- TOC entry 5683 (class 2606 OID 88203)
-- Name: lote_migracion fk_lotemig_usuario; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.lote_migracion
    ADD CONSTRAINT fk_lotemig_usuario FOREIGN KEY (fk_usuario) REFERENCES catedras.usuario(id_usuario) ON UPDATE CASCADE ON DELETE SET NULL;


--
-- TOC entry 5684 (class 2606 OID 88208)
-- Name: novedad_carga fk_novedad_lote; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.novedad_carga
    ADD CONSTRAINT fk_novedad_lote FOREIGN KEY (fk_lote_carga) REFERENCES catedras.lote_carga_asistente(id_lote_carga) ON UPDATE CASCADE ON DELETE CASCADE;


--
-- TOC entry 5685 (class 2606 OID 88213)
-- Name: opcion_pregunta fk_opcion_pregunta; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.opcion_pregunta
    ADD CONSTRAINT fk_opcion_pregunta FOREIGN KEY (fk_pregunta) REFERENCES catedras.pregunta(id_pregunta) ON UPDATE CASCADE ON DELETE CASCADE;


--
-- TOC entry 5686 (class 2606 OID 88218)
-- Name: parametro fk_parametro_usuario; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.parametro
    ADD CONSTRAINT fk_parametro_usuario FOREIGN KEY (fk_usuario_modifica) REFERENCES catedras.usuario(id_usuario) ON UPDATE CASCADE ON DELETE SET NULL;


--
-- TOC entry 5687 (class 2606 OID 88223)
-- Name: ponencia fk_ponencia_asistente; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.ponencia
    ADD CONSTRAINT fk_ponencia_asistente FOREIGN KEY (fk_asistente) REFERENCES catedras.asistente(id_asistente) ON UPDATE CASCADE ON DELETE RESTRICT;


--
-- TOC entry 5688 (class 2606 OID 88228)
-- Name: ponencia fk_ponencia_sesion; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.ponencia
    ADD CONSTRAINT fk_ponencia_sesion FOREIGN KEY (fk_sesion) REFERENCES catedras.sesion(id_sesion) ON UPDATE CASCADE ON DELETE CASCADE;


--
-- TOC entry 5689 (class 2606 OID 88233)
-- Name: pregunta fk_pregunta_encuesta; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.pregunta
    ADD CONSTRAINT fk_pregunta_encuesta FOREIGN KEY (fk_encuesta) REFERENCES catedras.encuesta(id_encuesta) ON UPDATE CASCADE ON DELETE CASCADE;


--
-- TOC entry 5690 (class 2606 OID 88238)
-- Name: pregunta fk_pregunta_tipo; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.pregunta
    ADD CONSTRAINT fk_pregunta_tipo FOREIGN KEY (fk_tipo_pregunta) REFERENCES catedras.tipo_pregunta(id_tipo_pregunta) ON UPDATE CASCADE ON DELETE RESTRICT;


--
-- TOC entry 5692 (class 2606 OID 88243)
-- Name: programa_asistente fk_progasi_asistente; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.programa_asistente
    ADD CONSTRAINT fk_progasi_asistente FOREIGN KEY (fk_asistente) REFERENCES catedras.asistente(id_asistente) ON UPDATE CASCADE ON DELETE CASCADE;


--
-- TOC entry 5693 (class 2606 OID 88248)
-- Name: programa_asistente fk_progasi_periodo; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.programa_asistente
    ADD CONSTRAINT fk_progasi_periodo FOREIGN KEY (fk_periodo) REFERENCES catedras.periodo_academico(id_periodo) ON UPDATE CASCADE ON DELETE RESTRICT;


--
-- TOC entry 5694 (class 2606 OID 88253)
-- Name: programa_asistente fk_progasi_programa; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.programa_asistente
    ADD CONSTRAINT fk_progasi_programa FOREIGN KEY (fk_programa) REFERENCES catedras.programa_academico(codigo) ON UPDATE CASCADE ON DELETE RESTRICT;


--
-- TOC entry 5691 (class 2606 OID 88258)
-- Name: programa_academico fk_programa_facultad; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.programa_academico
    ADD CONSTRAINT fk_programa_facultad FOREIGN KEY (fk_facultad) REFERENCES catedras.facultad(id_facultad) ON UPDATE CASCADE ON DELETE SET NULL;


--
-- TOC entry 5695 (class 2606 OID 88263)
-- Name: registro_asistencia fk_registro_asistente; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.registro_asistencia
    ADD CONSTRAINT fk_registro_asistente FOREIGN KEY (fk_asistente) REFERENCES catedras.asistente(id_asistente) ON UPDATE CASCADE ON DELETE RESTRICT;


--
-- TOC entry 5696 (class 2606 OID 88268)
-- Name: registro_asistencia fk_registro_clave; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.registro_asistencia
    ADD CONSTRAINT fk_registro_clave FOREIGN KEY (fk_clave) REFERENCES catedras.clave_acceso(id_clave) ON UPDATE CASCADE ON DELETE SET NULL;


--
-- TOC entry 5697 (class 2606 OID 88273)
-- Name: registro_asistencia fk_registro_enlace; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.registro_asistencia
    ADD CONSTRAINT fk_registro_enlace FOREIGN KEY (fk_enlace) REFERENCES catedras.enlace_registro(id_enlace) ON UPDATE CASCADE ON DELETE SET NULL;


--
-- TOC entry 5698 (class 2606 OID 88278)
-- Name: registro_asistencia fk_registro_programa_snap; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.registro_asistencia
    ADD CONSTRAINT fk_registro_programa_snap FOREIGN KEY (fk_programa_snapshot) REFERENCES catedras.programa_academico(codigo) ON UPDATE CASCADE ON DELETE RESTRICT;


--
-- TOC entry 5699 (class 2606 OID 88283)
-- Name: registro_asistencia fk_registro_sesion; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.registro_asistencia
    ADD CONSTRAINT fk_registro_sesion FOREIGN KEY (fk_sesion) REFERENCES catedras.sesion(id_sesion) ON UPDATE CASCADE ON DELETE RESTRICT;


--
-- TOC entry 5700 (class 2606 OID 88288)
-- Name: registro_asistencia fk_registro_vinculacion_snap; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.registro_asistencia
    ADD CONSTRAINT fk_registro_vinculacion_snap FOREIGN KEY (fk_vinculacion_snapshot) REFERENCES catedras.tipo_vinculacion(id_tipo_vinculacion) ON UPDATE CASCADE ON DELETE RESTRICT;


--
-- TOC entry 5701 (class 2606 OID 88293)
-- Name: respuesta_encuesta fk_respenc_encuesta; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.respuesta_encuesta
    ADD CONSTRAINT fk_respenc_encuesta FOREIGN KEY (fk_encuesta) REFERENCES catedras.encuesta(id_encuesta) ON UPDATE CASCADE ON DELETE RESTRICT;


--
-- TOC entry 5702 (class 2606 OID 88298)
-- Name: respuesta_encuesta fk_respenc_registro; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.respuesta_encuesta
    ADD CONSTRAINT fk_respenc_registro FOREIGN KEY (fk_registro) REFERENCES catedras.registro_asistencia(id_registro) ON UPDATE CASCADE ON DELETE CASCADE;


--
-- TOC entry 5703 (class 2606 OID 88303)
-- Name: respuesta_item fk_respitem_opcion; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.respuesta_item
    ADD CONSTRAINT fk_respitem_opcion FOREIGN KEY (fk_opcion) REFERENCES catedras.opcion_pregunta(id_opcion) ON UPDATE CASCADE ON DELETE RESTRICT;


--
-- TOC entry 5704 (class 2606 OID 88308)
-- Name: respuesta_item fk_respitem_pregunta; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.respuesta_item
    ADD CONSTRAINT fk_respitem_pregunta FOREIGN KEY (fk_pregunta) REFERENCES catedras.pregunta(id_pregunta) ON UPDATE CASCADE ON DELETE RESTRICT;


--
-- TOC entry 5705 (class 2606 OID 88313)
-- Name: respuesta_item fk_respitem_respuesta; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.respuesta_item
    ADD CONSTRAINT fk_respitem_respuesta FOREIGN KEY (fk_respuesta) REFERENCES catedras.respuesta_encuesta(id_respuesta) ON UPDATE CASCADE ON DELETE CASCADE;


--
-- TOC entry 5706 (class 2606 OID 88318)
-- Name: rol_por_usuario fk_rolusr_rol; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.rol_por_usuario
    ADD CONSTRAINT fk_rolusr_rol FOREIGN KEY (fk_rol) REFERENCES catedras.rol(id_rol) ON UPDATE CASCADE ON DELETE RESTRICT;


--
-- TOC entry 5707 (class 2606 OID 88323)
-- Name: rol_por_usuario fk_rolusr_usuario; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.rol_por_usuario
    ADD CONSTRAINT fk_rolusr_usuario FOREIGN KEY (fk_usuario) REFERENCES catedras.usuario(id_usuario) ON UPDATE CASCADE ON DELETE CASCADE;


--
-- TOC entry 5708 (class 2606 OID 88328)
-- Name: sesion fk_sesion_catedra; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.sesion
    ADD CONSTRAINT fk_sesion_catedra FOREIGN KEY (fk_catedra) REFERENCES catedras.catedra(id_catedra) ON UPDATE CASCADE ON DELETE CASCADE;


--
-- TOC entry 5709 (class 2606 OID 88333)
-- Name: sesion fk_sesion_encuesta; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.sesion
    ADD CONSTRAINT fk_sesion_encuesta FOREIGN KEY (fk_encuesta) REFERENCES catedras.encuesta(id_encuesta) ON UPDATE CASCADE ON DELETE SET NULL;


--
-- TOC entry 5710 (class 2606 OID 88338)
-- Name: sesion fk_sesion_modalidad; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.sesion
    ADD CONSTRAINT fk_sesion_modalidad FOREIGN KEY (fk_modalidad) REFERENCES catedras.modalidad(id_modalidad) ON UPDATE CASCADE ON DELETE RESTRICT;


--
-- TOC entry 5711 (class 2606 OID 88343)
-- Name: sesion fk_sesion_periodo; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.sesion
    ADD CONSTRAINT fk_sesion_periodo FOREIGN KEY (fk_periodo) REFERENCES catedras.periodo_academico(id_periodo) ON UPDATE CASCADE ON DELETE RESTRICT;


--
-- TOC entry 5712 (class 2606 OID 88348)
-- Name: sesion fk_sesion_sede; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.sesion
    ADD CONSTRAINT fk_sesion_sede FOREIGN KEY (fk_sede) REFERENCES catedras.sede(id_sede) ON UPDATE CASCADE ON DELETE RESTRICT;


--
-- TOC entry 5713 (class 2606 OID 88353)
-- Name: usuario fk_usuario_asistente; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.usuario
    ADD CONSTRAINT fk_usuario_asistente FOREIGN KEY (fk_asistente) REFERENCES catedras.asistente(id_asistente) ON UPDATE CASCADE ON DELETE RESTRICT;


--
-- TOC entry 5714 (class 2606 OID 88358)
-- Name: vinculacion_asistente fk_vinculacion_asistente; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.vinculacion_asistente
    ADD CONSTRAINT fk_vinculacion_asistente FOREIGN KEY (fk_asistente) REFERENCES catedras.asistente(id_asistente) ON UPDATE CASCADE ON DELETE CASCADE;


--
-- TOC entry 5715 (class 2606 OID 88363)
-- Name: vinculacion_asistente fk_vinculacion_tipo; Type: FK CONSTRAINT; Schema: catedras; Owner: -
--

ALTER TABLE ONLY catedras.vinculacion_asistente
    ADD CONSTRAINT fk_vinculacion_tipo FOREIGN KEY (fk_tipo_vinculacion) REFERENCES catedras.tipo_vinculacion(id_tipo_vinculacion) ON UPDATE CASCADE ON DELETE RESTRICT;


-- Completed on 2026-08-27 16:18:09

--
-- PostgreSQL database dump complete
--

-- (el COMMIT se ejecuta al final de este archivo, despues de las migraciones)

-- ##########  PARTE 2: migraciones 001-073 (048 y 065 omitidas)  ##########

-- ----- migracion 001_sp_guardar_programa_academico.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/001_sp_guardar_programa_academico.sql
--  Proyecto Cátedras · Retrofit V2 — Guía IA, Regla 1 (maestro-detalle)
-- --------------------------------------------------------------------------
--  ADR D14 (version2/4_research.md). Complementa a D12, que sigue vigente:
--  'alias_programa' continúa siendo de solo-anexo (POST /api/AliasPrograma
--  no cambia, sigue sin PUT/PATCH). Lo nuevo es que el programa y su primer
--  lote de alias pueden nacer en LA MISMA transacción, vía este SP, en vez
--  de crear el programa primero y cada alias en una petición aparte.
--
--  Estilo: igual al de sp_responder_encuesta (parámetro jsonb), que ya trae
--  la base de datos.
--
--  CÓMO SE APLICA (ver 7_quickstart.md de la v2):
--   · BD nueva: docker-compose.yml monta este archivo en
--     /docker-entrypoint-initdb.d/ con el prefijo 'z001_', para que
--     PostgreSQL lo ejecute DESPUÉS de 'script_bdcatedras.sql' (los
--     scripts de esa carpeta corren en orden ALFABÉTICO; sin el prefijo,
--     '001_...' correría ANTES y fallaría porque las tablas aún no existen).
--   · BD que ya existe (volumen con datos): initdb NO vuelve a correr, así
--     que se aplica a mano con psql (el comando está en el quickstart).
--     Por eso es 'CREATE OR REPLACE': se puede ejecutar varias veces.
-- ==========================================================================

SET search_path TO catedras, public;

CREATE OR REPLACE PROCEDURE catedras.sp_guardar_programa_academico(
    -- Los tamaños son IGUALES a los de las columnas de 'programa_academico'.
    IN p_codigo      character varying(5),
    IN p_nombre      character varying(150),
    IN p_fk_facultad character varying(10)  DEFAULT NULL,
    IN p_nivel       character varying(25)  DEFAULT 'PREGRADO',
    IN p_activo      boolean                DEFAULT true,
    -- Detalles: arreglo JSON, p. ej.
    --   [{"texto_normalizado":"ing. datos", "origen":"FORMULARIO"}, ...]
    -- NULL o [] = programa sin alias iniciales.
    IN p_alias       jsonb                  DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    -- 1) Maestro. Código duplicado o facultad inexistente -> la excepción
    --    del motor sube tal cual (el repositorio la deja pasar y el
    --    controlador responde 500, igual que con el INSERT suelto de antes).
    INSERT INTO programa_academico (codigo, nombre, fk_facultad, nivel, activo)
    VALUES (p_codigo, p_nombre, p_fk_facultad, p_nivel, p_activo);

    -- 2) Detalle: 0 o más alias iniciales, en el MISMO COMMIT que el
    --    maestro. 'fk_programa' NO viene en el JSON: es siempre el código
    --    del maestro que se acaba de insertar (no puede apuntar a otro).
    IF p_alias IS NOT NULL AND jsonb_array_length(p_alias) > 0 THEN
        INSERT INTO alias_programa (texto_normalizado, fk_programa, origen)
        SELECT e ->> 'texto_normalizado',
               p_codigo,
               COALESCE(e ->> 'origen', 'FORMULARIO')   -- DEFAULT de la columna
          FROM jsonb_array_elements(p_alias) AS e;
    END IF;

    -- Si algo falla en el paso 2 (un 'texto_normalizado' que ya existe ->
    -- uq_alias_texto), PostgreSQL revierte TODO el CALL: el programa del
    -- paso 1 tampoco queda creado (ACID · A: todo o nada).
END;
$$;

COMMENT ON PROCEDURE catedras.sp_guardar_programa_academico IS
    'Guía IA · Regla 1. Crea un programa académico y, opcionalmente, su '
    'primer lote de alias, en una sola transacción. Retrofit de V2 '
    '(ADR D14, complementa D12). Agregar un alias DESPUÉS de creado el programa sigue siendo '
    'POST /api/AliasPrograma (sin cambios, sigue sin PUT/PATCH).';


-- ----- migracion 002_sp_guardar_asistente.sql -----
RESET search_path;
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


-- ----- migracion 003_sp_guardar_lote_carga.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/003_sp_guardar_lote_carga.sql
--  Proyecto Cátedras · Versión 3 — Guía IA, Regla 1 (maestro-detalle)
-- --------------------------------------------------------------------------
--  Maestro: 'lote_carga_asistente'. Detalle: 'novedad_carga'.
--
--  CIERRA un lote de carga: guarda los 3 contadores finales y, en la MISMA
--  transacción, una fila de 'novedad_carga' por cada fila del archivo que
--  la carga rechazó (con su motivo). Ver ADR D15 (version3/4_research.md):
--  la carga en sí es PARCIAL (cada asistente es su propia transacción),
--  pero el CIERRE es todo-o-nada: los contadores y las novedades son una
--  sola foto del resultado.
--
--  El CHECK chk_lote_carga_filas (aceptadas + rechazadas <= leidas) es la
--  última muralla; el servicio de la API ya lo valida antes (400).
--
--  CÓMO SE APLICA: igual que 002_... (prefijo 'z003_' en initdb, o a mano
--  con psql sobre una BD que ya existe). 'CREATE OR REPLACE': re-ejecutable.
-- ==========================================================================

SET search_path TO catedras, public;

CREATE OR REPLACE PROCEDURE catedras.sp_guardar_lote_carga(
    IN p_id_lote          bigint,
    IN p_filas_leidas     integer,
    IN p_filas_aceptadas  integer,
    IN p_filas_rechazadas integer,
    IN p_observaciones    text  DEFAULT NULL,
    -- Detalle: arreglo JSON, p. ej.
    --   [{"numero_fila":2, "contenido_crudo":"...", "motivo":"correo duplicado"}]
    -- NULL o [] = ninguna fila rechazada.
    IN p_novedades        jsonb DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    -- 1) Maestro: los contadores finales del lote.
    UPDATE lote_carga_asistente
       SET filas_leidas     = p_filas_leidas,
           filas_aceptadas  = p_filas_aceptadas,
           filas_rechazadas = p_filas_rechazadas,
           observaciones    = p_observaciones
     WHERE id_lote_carga = p_id_lote;

    -- 'FOUND' es una variable de PL/pgSQL: false si el UPDATE no tocó
    -- ninguna fila. El servicio ya responde 404 antes de llegar aquí; esto
    -- es solo la defensa del propio SP si alguien lo llama directo.
    IF NOT FOUND THEN
        RAISE EXCEPTION 'No existe el lote de carga %', p_id_lote
            USING ERRCODE = 'no_data_found';
    END IF;

    -- 2) Detalle: una novedad por fila rechazada. 'fk_lote_carga' NO viene
    --    en el JSON: es siempre el lote que se está cerrando.
    IF p_novedades IS NOT NULL AND jsonb_array_length(p_novedades) > 0 THEN
        INSERT INTO novedad_carga (fk_lote_carga, numero_fila, contenido_crudo, motivo)
        SELECT p_id_lote,
               (e ->> 'numero_fila')::integer,
               e ->> 'contenido_crudo',
               e ->> 'motivo'
          FROM jsonb_array_elements(p_novedades) AS e;
    END IF;

    -- Si el paso 2 falla (numero_fila <= 0, motivo de más de 200
    -- caracteres...), PostgreSQL revierte TODO: los contadores del paso 1
    -- tampoco quedan guardados y el lote sigue "abierto".
END;
$$;

COMMENT ON PROCEDURE catedras.sp_guardar_lote_carga IS
    'Guía IA · Regla 1. Cierra un lote de carga: contadores finales + '
    'novedades (filas rechazadas) en una sola transacción (V3, ADR D15).';


-- ----- migracion 004_sp_guardar_pregunta.sql -----
RESET search_path;
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


-- ----- migracion 005_sp_sede_ejemplo.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/005_sp_sede_ejemplo.sql
--  Proyecto Cátedras · RETROFIT criterio 3 de VERSION_2.md (PRUEBA DE
--  CONCEPTO, solo 'sede'): los 5 verbos pasan por procedimientos
--  almacenados, con el mismo molde exacto del ejemplo del profesor
--  (PROCEDURE, INOUT p_resultado JSON, un procedimiento por verbo y por
--  tabla: sp_listar_/sp_consultar_/sp_crear_/sp_actualizar_/sp_eliminar_).
--
--  POR QUÉ 'sede' PRIMERO: es la tabla más simple del esquema (sin FK
--  salientes, sin maestro-detalle) — sirve para probar el molde antes de
--  aplicarlo a las otras 32. Convertir el resto es PLAN_RETROFIT_SP.md.
--
--  DECISIÓN QUE TOMA ESTE RETROFIT (y que aplica a las otras 19 tablas con
--  Reemplazo+Actualizar, ver PLAN_RETROFIT_SP.md §2): UN SOLO
--  'sp_actualizar_<tabla>' que reemplaza TODAS las columnas (como el PUT
--  de hoy). El PATCH parcial YA NO arma un UPDATE dinámico en SQL: el
--  repositorio trae la fila actual, la mezcla con lo que llegó, y llama a
--  este MISMO procedimiento con el conjunto completo — así hay un solo
--  procedimiento por tabla, tal como pide el documento ("un nombre por
--  operación y por tabla"), y el repositorio queda siendo "la llamada al
--  procedimiento, con sus parámetros" (§4).
--
--  'sede' SÍ tiene columna 'activo' -> 'sp_eliminar_sede' es LÓGICO
--  (UPDATE ... SET activo = false), NO un DELETE (§4: "si sus tablas
--  tienen una columna activo, el borrado es lógico").
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  LISTAR — solo las activas (igual que el ejemplo del profesor), con un
--  límite OPCIONAL: 'LIMIT p_limite' con p_limite NULL = sin límite (así
--  funciona LIMIT en PostgreSQL, no hace falta COALESCE).
-- --------------------------------------------------------------------------
-- CORRECCIÓN (lote 006-014): el listado SOLO traía las activas y el front no podía
-- ver ni reactivar las inactivas. 'p_incluir_inactivos' (DEFAULT false) conserva el
-- comportamiento del patrón y permite pedir todas con ?incluirInactivos=true. Cambia
-- la firma, así que se elimina la anterior (si no, quedaría una sobrecarga ambigua).
DROP PROCEDURE IF EXISTS catedras.sp_listar_sede(integer, json);

CREATE OR REPLACE PROCEDURE catedras.sp_listar_sede(
    IN    p_limite    integer DEFAULT NULL,
    IN    p_incluir_inactivos boolean DEFAULT false,
    INOUT p_resultado json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(s), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_sede, nombre, direccion, es_virtual, activo
              FROM sede
             WHERE p_incluir_inactivos OR activo
             ORDER BY id_sede
             LIMIT p_limite) s;
END;
$$;

-- --------------------------------------------------------------------------
--  CONSULTAR — una sede por id. Si no existe, 'p_resultado' queda NULL
--  (row_to_json de una subconsulta vacía es NULL): el repositorio lo lee
--  como "no encontrada", igual que hoy QuerySingleOrDefaultAsync.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_consultar_sede(
    IN    p_id_sede   character varying(15),
    INOUT p_resultado json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(s) INTO p_resultado
      FROM (SELECT id_sede, nombre, direccion, es_virtual, activo
              FROM sede
             WHERE id_sede = p_id_sede) s;
END;
$$;

-- --------------------------------------------------------------------------
--  CREAR — devuelve la fila COMO QUEDÓ GUARDADA (§4). 'id_sede' no lo
--  genera la BD (lo manda el cliente, es su clave primaria "de negocio"),
--  así que no hace falta un 'v_id' intermedio: ya se conoce.
--  Clave repetida (pk_sede) o nombre repetido (uq_sede_nombre) ->
--  NpgsqlException; el controlador la traduce a 409 (ver PLAN_RETROFIT_SP.md,
--  eso es trabajo del criterio 7, NO de este procedimiento).
-- --------------------------------------------------------------------------
-- CORRECCIÓN (lote 006-014): la versión original no recibía 'activo', así que
-- POST /api/Sede con "activo": false guardaba activo = true. Se agrega
-- 'p_activo' (DEFAULT true). Como cambia la firma, se elimina primero la
-- versión anterior: si no, CREATE OR REPLACE crearía una SOBRECARGA y
-- CALL sp_crear_sede(...) quedaría ambiguo.
DROP PROCEDURE IF EXISTS catedras.sp_crear_sede(varchar, varchar, varchar, boolean, json);

CREATE OR REPLACE PROCEDURE catedras.sp_crear_sede(
    IN    p_id_sede    character varying(15),
    IN    p_nombre     character varying(80),
    IN    p_direccion  character varying(200) DEFAULT NULL,
    IN    p_es_virtual boolean DEFAULT false,
    IN    p_activo     boolean DEFAULT true,
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    INSERT INTO sede (id_sede, nombre, direccion, es_virtual, activo)
    VALUES (p_id_sede, p_nombre, p_direccion, p_es_virtual, COALESCE(p_activo, true));

    SELECT row_to_json(s) INTO p_resultado
      FROM (SELECT id_sede, nombre, direccion, es_virtual, activo
              FROM sede
             WHERE id_sede = p_id_sede) s;
END;
$$;

-- --------------------------------------------------------------------------
--  ACTUALIZAR — reemplazo COMPLETO (sirve para PUT directo y para PATCH,
--  que en el repositorio llega ya mezclado con la fila actual: ver
--  RepositorioSedePostgres.cs). Todas las columnas de negocio son
--  obligatorias aquí porque el repositorio SIEMPRE manda el conjunto
--  completo, nunca un subconjunto.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_sede(
    IN    p_id_sede    character varying(15),
    IN    p_nombre     character varying(80),
    IN    p_direccion  character varying(200),
    IN    p_es_virtual boolean,
    IN    p_activo     boolean,
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE sede
       SET nombre     = p_nombre,
           direccion  = p_direccion,
           es_virtual = p_es_virtual,
           activo     = p_activo
     WHERE id_sede = p_id_sede;

    -- Sin FOUND -> no existía -> p_resultado queda NULL (igual criterio
    -- que 'consultar': el repositorio lo lee como "no encontrada").
    IF FOUND THEN
        SELECT row_to_json(s) INTO p_resultado
          FROM (SELECT id_sede, nombre, direccion, es_virtual, activo
                  FROM sede
                 WHERE id_sede = p_id_sede) s;
    END IF;
END;
$$;

-- --------------------------------------------------------------------------
--  ELIMINAR — LÓGICO: 'sede' tiene columna 'activo' (§4). Si ya estaba
--  inactiva, también cuenta como "no encontrada" (ya no hay nada que
--  retirar), igual criterio que un DELETE físico sobre algo que no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_sede(
    IN    p_id_sede   character varying(15),
    INOUT p_resultado json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE sede
       SET activo = false
     WHERE id_sede = p_id_sede
       AND activo;   -- ya inactiva -> no cuenta como retirada otra vez

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_sede IS
    'Retrofit criterio 3 (VERSION_2.md §4). Solo activas, con límite opcional.';
COMMENT ON PROCEDURE catedras.sp_consultar_sede IS
    'Retrofit criterio 3. NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_sede IS
    'Retrofit criterio 3. Devuelve la fila como quedó guardada.';
COMMENT ON PROCEDURE catedras.sp_actualizar_sede IS
    'Retrofit criterio 3. Reemplazo completo; PATCH mezcla antes de llamar.';
COMMENT ON PROCEDURE catedras.sp_eliminar_sede IS
    'Retrofit criterio 3. Borrado LÓGICO (activo=false): la tabla tiene activo.';


-- ----- migracion 006_sp_facultad.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/006_sp_facultad.sql
--  Proyecto Cátedras · RETROFIT criterio 3 de VERSION_2.md · 'facultad'
-- --------------------------------------------------------------------------
--  Los 5 verbos de 'facultad' pasan por procedimientos almacenados, con el
--  molde exacto de 005_sp_sede_ejemplo.sql (PROCEDURE, INOUT p_resultado
--  JSON como ÚLTIMO parámetro, un procedimiento por verbo y por tabla).
--
--  'facultad' SÍ tiene columna 'activo' -> 'sp_eliminar_facultad' es
--  LÓGICO (UPDATE ... SET activo = false), NO un DELETE (PLAN_RETROFIT_SP.md
--  §1, decisión 2).
--
--  Diferencia con 'sede': 'sp_crear_facultad' RECIBE 'p_activo' (DEFAULT
--  true). El servicio manda 'Activo' (viene de FacultadCrear.Activo ?? true)
--  y, si el procedimiento lo ignorara, una facultad creada con
--  "activo": false quedaría activa sin avisar.
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  LISTAR — solo las activas, con límite OPCIONAL (p_limite NULL = sin
--  límite: así funciona LIMIT en PostgreSQL).
-- --------------------------------------------------------------------------
-- CORRECCIÓN (lote 006-014): el listado SOLO traía las activas y el front no podía
-- ver ni reactivar las inactivas. 'p_incluir_inactivos' (DEFAULT false) conserva el
-- comportamiento del patrón y permite pedir todas con ?incluirInactivos=true. Cambia
-- la firma, así que se elimina la anterior (si no, quedaría una sobrecarga ambigua).
DROP PROCEDURE IF EXISTS catedras.sp_listar_facultad(integer, json);

CREATE OR REPLACE PROCEDURE catedras.sp_listar_facultad(
    IN    p_limite    integer DEFAULT NULL,
    IN    p_incluir_inactivos boolean DEFAULT false,
    INOUT p_resultado json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(f), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_facultad, nombre, fk_facultad_padre, activo
              FROM facultad
             WHERE p_incluir_inactivos OR activo
             ORDER BY id_facultad
             LIMIT p_limite) f;
END;
$$;

-- --------------------------------------------------------------------------
--  CONSULTAR — una facultad por id (activa o no). Si no existe,
--  'p_resultado' queda NULL.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_consultar_facultad(
    IN    p_id_facultad character varying(10),
    INOUT p_resultado   json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(f) INTO p_resultado
      FROM (SELECT id_facultad, nombre, fk_facultad_padre, activo
              FROM facultad
             WHERE id_facultad = p_id_facultad) f;
END;
$$;

-- --------------------------------------------------------------------------
--  CREAR — devuelve la fila COMO QUEDÓ GUARDADA. La clave 'id_facultad' la
--  manda el cliente (clave de negocio), así que no hace falta variable
--  intermedia. Clave o nombre repetido (pk_facultad, uq_facultad_nombre) o
--  padre inexistente (fk_facultad_padre) -> NpgsqlException.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_crear_facultad(
    IN    p_id_facultad       character varying(10),
    IN    p_nombre            character varying(120),
    IN    p_fk_facultad_padre character varying(10) DEFAULT NULL,
    IN    p_activo            boolean               DEFAULT true,
    INOUT p_resultado         json                  DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    INSERT INTO facultad (id_facultad, nombre, fk_facultad_padre, activo)
    VALUES (p_id_facultad, p_nombre, p_fk_facultad_padre, p_activo);

    SELECT row_to_json(f) INTO p_resultado
      FROM (SELECT id_facultad, nombre, fk_facultad_padre, activo
              FROM facultad
             WHERE id_facultad = p_id_facultad) f;
END;
$$;

-- --------------------------------------------------------------------------
--  ACTUALIZAR — reemplazo COMPLETO de las columnas de negocio (sirve para
--  PUT y para PATCH; el PATCH se mezcla en el repositorio antes de llamar).
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_facultad(
    IN    p_id_facultad       character varying(10),
    IN    p_nombre            character varying(120),
    IN    p_fk_facultad_padre character varying(10),
    IN    p_activo            boolean,
    INOUT p_resultado         json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE facultad
       SET nombre            = p_nombre,
           fk_facultad_padre = p_fk_facultad_padre,
           activo            = p_activo
     WHERE id_facultad = p_id_facultad;

    -- Sin FOUND -> no existía -> p_resultado queda NULL.
    IF FOUND THEN
        SELECT row_to_json(f) INTO p_resultado
          FROM (SELECT id_facultad, nombre, fk_facultad_padre, activo
                  FROM facultad
                 WHERE id_facultad = p_id_facultad) f;
    END IF;
END;
$$;

-- --------------------------------------------------------------------------
--  ELIMINAR — LÓGICO: 'facultad' tiene 'activo'. Si ya estaba inactiva
--  cuenta como "no encontrada" (ya no hay nada que retirar).
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_facultad(
    IN    p_id_facultad character varying(10),
    INOUT p_resultado   json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE facultad
       SET activo = false
     WHERE id_facultad = p_id_facultad
       AND activo;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_facultad IS
    'Retrofit criterio 3 (VERSION_2.md §4). Solo activas, con límite opcional.';
COMMENT ON PROCEDURE catedras.sp_consultar_facultad IS
    'Retrofit criterio 3. NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_facultad IS
    'Retrofit criterio 3. Devuelve la fila como quedó guardada.';
COMMENT ON PROCEDURE catedras.sp_actualizar_facultad IS
    'Retrofit criterio 3. Reemplazo completo; PATCH mezcla antes de llamar.';
COMMENT ON PROCEDURE catedras.sp_eliminar_facultad IS
    'Retrofit criterio 3. Borrado LÓGICO (activo=false): la tabla tiene activo.';


-- ----- migracion 007_sp_dependencia.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/007_sp_dependencia.sql
--  Proyecto Cátedras · RETROFIT criterio 3 de VERSION_2.md · 'dependencia'
-- --------------------------------------------------------------------------
--  Mismo molde que 005_sp_sede_ejemplo.sql / 006_sp_facultad.sql.
--  'dependencia' SÍ tiene 'activo' -> 'sp_eliminar_dependencia' es LÓGICO.
--  'sp_crear_dependencia' recibe 'p_activo' (DEFAULT true), igual que
--  'sp_crear_facultad' (ver la nota de 006).
-- ==========================================================================

SET search_path TO catedras, public;

-- CORRECCIÓN (lote 006-014): el listado SOLO traía las activas y el front no podía
-- ver ni reactivar las inactivas. 'p_incluir_inactivos' (DEFAULT false) conserva el
-- comportamiento del patrón y permite pedir todas con ?incluirInactivos=true. Cambia
-- la firma, así que se elimina la anterior (si no, quedaría una sobrecarga ambigua).
DROP PROCEDURE IF EXISTS catedras.sp_listar_dependencia(integer, json);

CREATE OR REPLACE PROCEDURE catedras.sp_listar_dependencia(
    IN    p_limite    integer DEFAULT NULL,
    IN    p_incluir_inactivos boolean DEFAULT false,
    INOUT p_resultado json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(d), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_dependencia, nombre, fk_dependencia_padre, activo
              FROM dependencia
             WHERE p_incluir_inactivos OR activo
             ORDER BY id_dependencia
             LIMIT p_limite) d;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_consultar_dependencia(
    IN    p_id_dependencia character varying(15),
    INOUT p_resultado      json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(d) INTO p_resultado
      FROM (SELECT id_dependencia, nombre, fk_dependencia_padre, activo
              FROM dependencia
             WHERE id_dependencia = p_id_dependencia) d;
END;
$$;

-- Clave repetida (pk_dependencia), nombre repetido (uq_dependencia_nombre),
-- padre inexistente o igual a sí misma (chk_dependencia_padre) ->
-- NpgsqlException.
CREATE OR REPLACE PROCEDURE catedras.sp_crear_dependencia(
    IN    p_id_dependencia       character varying(15),
    IN    p_nombre               character varying(120),
    IN    p_fk_dependencia_padre character varying(15) DEFAULT NULL,
    IN    p_activo               boolean               DEFAULT true,
    INOUT p_resultado            json                  DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    INSERT INTO dependencia (id_dependencia, nombre, fk_dependencia_padre, activo)
    VALUES (p_id_dependencia, p_nombre, p_fk_dependencia_padre, p_activo);

    SELECT row_to_json(d) INTO p_resultado
      FROM (SELECT id_dependencia, nombre, fk_dependencia_padre, activo
              FROM dependencia
             WHERE id_dependencia = p_id_dependencia) d;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_dependencia(
    IN    p_id_dependencia       character varying(15),
    IN    p_nombre               character varying(120),
    IN    p_fk_dependencia_padre character varying(15),
    IN    p_activo               boolean,
    INOUT p_resultado            json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE dependencia
       SET nombre               = p_nombre,
           fk_dependencia_padre = p_fk_dependencia_padre,
           activo               = p_activo
     WHERE id_dependencia = p_id_dependencia;

    IF FOUND THEN
        SELECT row_to_json(d) INTO p_resultado
          FROM (SELECT id_dependencia, nombre, fk_dependencia_padre, activo
                  FROM dependencia
                 WHERE id_dependencia = p_id_dependencia) d;
    END IF;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_dependencia(
    IN    p_id_dependencia character varying(15),
    INOUT p_resultado      json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE dependencia
       SET activo = false
     WHERE id_dependencia = p_id_dependencia
       AND activo;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_dependencia IS
    'Retrofit criterio 3 (VERSION_2.md §4). Solo activas, con límite opcional.';
COMMENT ON PROCEDURE catedras.sp_consultar_dependencia IS
    'Retrofit criterio 3. NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_dependencia IS
    'Retrofit criterio 3. Devuelve la fila como quedó guardada.';
COMMENT ON PROCEDURE catedras.sp_actualizar_dependencia IS
    'Retrofit criterio 3. Reemplazo completo; PATCH mezcla antes de llamar.';
COMMENT ON PROCEDURE catedras.sp_eliminar_dependencia IS
    'Retrofit criterio 3. Borrado LÓGICO (activo=false): la tabla tiene activo.';


-- ----- migracion 008_sp_programa_academico.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/008_sp_programa_academico.sql
--  Proyecto Cátedras · RETROFIT criterio 3 de VERSION_2.md · 'programa_academico'
-- --------------------------------------------------------------------------
--  Agrega Listar / Consultar / Actualizar / Eliminar. NO hay
--  'sp_crear_programa_academico': el Crear (maestro-detalle: programa +
--  sus alias iniciales en una transacción) YA es
--  'sp_guardar_programa_academico' (migración 001) y NO se reemplaza ni se
--  toca. El repositorio sigue llamándolo igual.
--
--  'programa_academico' SÍ tiene 'activo' -> 'sp_eliminar_programa_academico'
--  es LÓGICO. La clave es 'codigo' (varchar(5), clave natural que viaja al
--  archivo plano del ASIS).
-- ==========================================================================

SET search_path TO catedras, public;

-- CORRECCIÓN (lote 006-014): el listado SOLO traía las activas y el front no podía
-- ver ni reactivar las inactivas. 'p_incluir_inactivos' (DEFAULT false) conserva el
-- comportamiento del patrón y permite pedir todas con ?incluirInactivos=true. Cambia
-- la firma, así que se elimina la anterior (si no, quedaría una sobrecarga ambigua).
DROP PROCEDURE IF EXISTS catedras.sp_listar_programa_academico(integer, json);

CREATE OR REPLACE PROCEDURE catedras.sp_listar_programa_academico(
    IN    p_limite    integer DEFAULT NULL,
    IN    p_incluir_inactivos boolean DEFAULT false,
    INOUT p_resultado json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(p), '[]'::json)
      INTO p_resultado
      FROM (SELECT codigo, nombre, fk_facultad, nivel, activo
              FROM programa_academico
             WHERE p_incluir_inactivos OR activo
             ORDER BY codigo
             LIMIT p_limite) p;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_consultar_programa_academico(
    IN    p_codigo    character varying(5),
    INOUT p_resultado json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(p) INTO p_resultado
      FROM (SELECT codigo, nombre, fk_facultad, nivel, activo
              FROM programa_academico
             WHERE codigo = p_codigo) p;
END;
$$;

-- Reemplazo COMPLETO (PUT directo; el PATCH se mezcla en el repositorio).
-- Nivel fuera de la lista -> chk_programa_nivel -> NpgsqlException.
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_programa_academico(
    IN    p_codigo      character varying(5),
    IN    p_nombre      character varying(150),
    IN    p_fk_facultad character varying(10),
    IN    p_nivel       character varying(25),
    IN    p_activo      boolean,
    INOUT p_resultado   json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE programa_academico
       SET nombre      = p_nombre,
           fk_facultad = p_fk_facultad,
           nivel       = p_nivel,
           activo      = p_activo
     WHERE codigo = p_codigo;

    IF FOUND THEN
        SELECT row_to_json(p) INTO p_resultado
          FROM (SELECT codigo, nombre, fk_facultad, nivel, activo
                  FROM programa_academico
                 WHERE codigo = p_codigo) p;
    END IF;
END;
$$;

-- LÓGICO. Los alias NO se tocan: siguen apuntando al programa (que sigue
-- existiendo, solo inactivo) y fn_resolver_programa puede seguir
-- resolviéndolos.
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_programa_academico(
    IN    p_codigo    character varying(5),
    INOUT p_resultado json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE programa_academico
       SET activo = false
     WHERE codigo = p_codigo
       AND activo;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_programa_academico IS
    'Retrofit criterio 3 (VERSION_2.md §4). Solo activos, con límite opcional.';
COMMENT ON PROCEDURE catedras.sp_consultar_programa_academico IS
    'Retrofit criterio 3. NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_actualizar_programa_academico IS
    'Retrofit criterio 3. Reemplazo completo; PATCH mezcla antes de llamar.';
COMMENT ON PROCEDURE catedras.sp_eliminar_programa_academico IS
    'Retrofit criterio 3. Borrado LÓGICO (activo=false). El Crear sigue siendo sp_guardar_programa_academico (migración 001).';


-- ----- migracion 009_sp_alias_programa.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/009_sp_alias_programa.sql
--  Proyecto Cátedras · RETROFIT criterio 3 de VERSION_2.md · 'alias_programa'
-- --------------------------------------------------------------------------
--  'sp_guardar_programa_academico' (migración 001) sigue siendo el Crear
--  MAESTRO-DETALLE (programa + alias iniciales) y NO se toca. Aquí se
--  agregan los 5 verbos de 'alias_programa' PARA EL ALTA SUELTA
--  (POST /api/AliasPrograma, un alias a un programa que ya existe), que
--  hoy no pasa por ningún procedimiento (PLAN_RETROFIT_SP.md §2, Grupo B).
--
--  'alias_programa' NO tiene columna 'activo' -> 'sp_eliminar_alias_programa'
--  es FÍSICO (DELETE). Por eso 'sp_listar_alias_programa' devuelve TODAS las
--  filas (no hay nada que filtrar).
--
--  'sp_actualizar_alias_programa' existe por el criterio de los 5 verbos,
--  pero IRepositorioAliasPrograma no tiene ActualizarAsync (el alias es
--  "sin PUT/PATCH" desde la v2): ninguna capa C# lo llama.
-- ==========================================================================

SET search_path TO catedras, public;

CREATE OR REPLACE PROCEDURE catedras.sp_listar_alias_programa(
    IN    p_limite    integer DEFAULT NULL,
    INOUT p_resultado json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(a), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_alias, texto_normalizado, fk_programa, origen, creado_en
              FROM alias_programa
             ORDER BY id_alias
             LIMIT p_limite) a;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_consultar_alias_programa(
    IN    p_id_alias  bigint,
    INOUT p_resultado json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(a) INTO p_resultado
      FROM (SELECT id_alias, texto_normalizado, fk_programa, origen, creado_en
              FROM alias_programa
             WHERE id_alias = p_id_alias) a;
END;
$$;

-- CREAR — 'id_alias' y 'creado_en' los pone la BD (identity y DEFAULT now()):
-- el procedimiento devuelve la fila tal como quedó, con ambos ya puestos
-- (el servicio los necesita para responder). Texto repetido (uq_alias_texto)
-- o programa inexistente (fk_alias_programa) -> NpgsqlException.
CREATE OR REPLACE PROCEDURE catedras.sp_crear_alias_programa(
    IN    p_texto_normalizado character varying(200),
    IN    p_fk_programa       character varying(5),
    IN    p_origen            character varying(30) DEFAULT 'FORMULARIO',
    INOUT p_resultado         json                  DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_id bigint;
BEGIN
    INSERT INTO alias_programa (texto_normalizado, fk_programa, origen)
    VALUES (p_texto_normalizado, p_fk_programa, COALESCE(p_origen, 'FORMULARIO'))
    RETURNING id_alias INTO v_id;

    SELECT row_to_json(a) INTO p_resultado
      FROM (SELECT id_alias, texto_normalizado, fk_programa, origen, creado_en
              FROM alias_programa
             WHERE id_alias = v_id) a;
END;
$$;

-- Reemplazo COMPLETO de las columnas de negocio. 'creado_en' NO cambia
-- (es el momento del alta, no de la última edición).
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_alias_programa(
    IN    p_id_alias          bigint,
    IN    p_texto_normalizado character varying(200),
    IN    p_fk_programa       character varying(5),
    IN    p_origen            character varying(30),
    INOUT p_resultado         json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE alias_programa
       SET texto_normalizado = p_texto_normalizado,
           fk_programa       = p_fk_programa,
           origen            = p_origen
     WHERE id_alias = p_id_alias;

    IF FOUND THEN
        SELECT row_to_json(a) INTO p_resultado
          FROM (SELECT id_alias, texto_normalizado, fk_programa, origen, creado_en
                  FROM alias_programa
                 WHERE id_alias = p_id_alias) a;
    END IF;
END;
$$;

-- FÍSICO: la tabla no tiene 'activo'.
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_alias_programa(
    IN    p_id_alias  bigint,
    INOUT p_resultado json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    DELETE FROM alias_programa
     WHERE id_alias = p_id_alias;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_alias_programa IS
    'Retrofit criterio 3 (VERSION_2.md §4). Todas las filas (sin activo), con límite opcional.';
COMMENT ON PROCEDURE catedras.sp_consultar_alias_programa IS
    'Retrofit criterio 3. NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_alias_programa IS
    'Retrofit criterio 3. Alta SUELTA de un alias; el alta junto al programa sigue siendo sp_guardar_programa_academico.';
COMMENT ON PROCEDURE catedras.sp_actualizar_alias_programa IS
    'Retrofit criterio 3. Reemplazo completo. Sin llamador en C# (el alias no tiene PUT/PATCH).';
COMMENT ON PROCEDURE catedras.sp_eliminar_alias_programa IS
    'Retrofit criterio 3. Borrado FÍSICO: la tabla no tiene activo.';


-- ----- migracion 010_sp_catedra.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/010_sp_catedra.sql
--  Proyecto Cátedras · RETROFIT criterio 3 de VERSION_2.md · 'catedra'
-- --------------------------------------------------------------------------
--  Los 5 verbos de 'catedra' + 'sp_contar_catedra' (el 6.º que ya tenía
--  IRepositorioCatedra.ContarAsync; PLAN_RETROFIT_SP.md §2 lo advierte).
--
--  'catedra' SÍ tiene 'activo' -> 'sp_eliminar_catedra' es LÓGICO.
--
--  COLUMNA GENERADA: 'nombre_asis' es GENERATED ALWAYS (los 30 primeros
--  caracteres de 'nombre'). NUNCA aparece en un INSERT ni en un UPDATE: la
--  calcula PostgreSQL. Sí va en el JSON de salida, porque el servicio la
--  devuelve al crear.
--
--  'id_catedra' lo genera la BD (identity): 'sp_crear_catedra' lo recoge con
--  RETURNING y devuelve la fila COMPLETA, con 'id_catedra', 'nombre_asis' y
--  'creado_en' ya puestos.
-- ==========================================================================

SET search_path TO catedras, public;

-- CORRECCIÓN (lote 006-014): el listado SOLO traía las activas y el front no podía
-- ver ni reactivar las inactivas. 'p_incluir_inactivos' (DEFAULT false) conserva el
-- comportamiento del patrón y permite pedir todas con ?incluirInactivos=true. Cambia
-- la firma, así que se elimina la anterior (si no, quedaría una sobrecarga ambigua).
DROP PROCEDURE IF EXISTS catedras.sp_listar_catedra(integer, json);

CREATE OR REPLACE PROCEDURE catedras.sp_listar_catedra(
    IN    p_limite    integer DEFAULT NULL,
    IN    p_incluir_inactivos boolean DEFAULT false,
    INOUT p_resultado json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(c), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_catedra, id_evento_asis, nombre, nombre_asis,
                   fk_tipo_evento, fk_dependencia, activo, creado_en
              FROM catedra
             WHERE p_incluir_inactivos OR activo
             ORDER BY id_catedra
             LIMIT p_limite) c;
END;
$$;

-- CONTAR — el 'total' de la respuesta del listado. Cuenta lo MISMO que lista
-- 'sp_listar_catedra' (por defecto solo activas; con p_incluir_inactivos = true,
-- todas): si no, el 'total' no coincidiría con las filas que de verdad salen.
-- Devuelve {"total": n}.
DROP PROCEDURE IF EXISTS catedras.sp_contar_catedra(json);

CREATE OR REPLACE PROCEDURE catedras.sp_contar_catedra(
    IN    p_incluir_inactivos boolean DEFAULT false,
    INOUT p_resultado         json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT json_build_object('total', count(*))
      INTO p_resultado
      FROM catedra
     WHERE p_incluir_inactivos OR activo;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_consultar_catedra(
    IN    p_id_catedra bigint,
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(c) INTO p_resultado
      FROM (SELECT id_catedra, id_evento_asis, nombre, nombre_asis,
                   fk_tipo_evento, fk_dependencia, activo, creado_en
              FROM catedra
             WHERE id_catedra = p_id_catedra) c;
END;
$$;

-- id_evento_asis fuera de ^[0-9]{9}$ (chk_catedra_asis), tipo de evento o
-- dependencia inexistentes -> NpgsqlException.
CREATE OR REPLACE PROCEDURE catedras.sp_crear_catedra(
    IN    p_id_evento_asis character(9),
    IN    p_nombre         character varying(200),
    IN    p_fk_tipo_evento character(4),
    IN    p_fk_dependencia character varying(15) DEFAULT NULL,
    IN    p_activo         boolean               DEFAULT true,
    INOUT p_resultado      json                  DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_id bigint;
BEGIN
    INSERT INTO catedra (id_evento_asis, nombre, fk_tipo_evento, fk_dependencia, activo)
    VALUES (p_id_evento_asis, p_nombre, p_fk_tipo_evento, p_fk_dependencia, p_activo)
    RETURNING id_catedra INTO v_id;

    SELECT row_to_json(c) INTO p_resultado
      FROM (SELECT id_catedra, id_evento_asis, nombre, nombre_asis,
                   fk_tipo_evento, fk_dependencia, activo, creado_en
              FROM catedra
             WHERE id_catedra = v_id) c;
END;
$$;

-- Reemplazo COMPLETO (sin 'nombre_asis', que es generada, ni 'creado_en').
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_catedra(
    IN    p_id_catedra     bigint,
    IN    p_id_evento_asis character(9),
    IN    p_nombre         character varying(200),
    IN    p_fk_tipo_evento character(4),
    IN    p_fk_dependencia character varying(15),
    IN    p_activo         boolean,
    INOUT p_resultado      json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE catedra
       SET id_evento_asis = p_id_evento_asis,
           nombre         = p_nombre,
           fk_tipo_evento = p_fk_tipo_evento,
           fk_dependencia = p_fk_dependencia,
           activo         = p_activo
     WHERE id_catedra = p_id_catedra;

    IF FOUND THEN
        SELECT row_to_json(c) INTO p_resultado
          FROM (SELECT id_catedra, id_evento_asis, nombre, nombre_asis,
                       fk_tipo_evento, fk_dependencia, activo, creado_en
                  FROM catedra
                 WHERE id_catedra = p_id_catedra) c;
    END IF;
END;
$$;

-- LÓGICO. Las sesiones de la cátedra NO se tocan (con el DELETE de antes
-- caían en cascada por fk_sesion_catedra; ahora conservan su historia).
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_catedra(
    IN    p_id_catedra bigint,
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE catedra
       SET activo = false
     WHERE id_catedra = p_id_catedra
       AND activo;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_catedra IS
    'Retrofit criterio 3 (VERSION_2.md §4). Solo activas, con límite opcional.';
COMMENT ON PROCEDURE catedras.sp_contar_catedra IS
    'Retrofit criterio 3. Cuenta las mismas filas que lista sp_listar_catedra (activas). {"total": n}.';
COMMENT ON PROCEDURE catedras.sp_consultar_catedra IS
    'Retrofit criterio 3. NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_catedra IS
    'Retrofit criterio 3. Devuelve la fila como quedó (id_catedra, nombre_asis y creado_en ya puestos).';
COMMENT ON PROCEDURE catedras.sp_actualizar_catedra IS
    'Retrofit criterio 3. Reemplazo completo (sin la columna generada nombre_asis); PATCH mezcla antes de llamar.';
COMMENT ON PROCEDURE catedras.sp_eliminar_catedra IS
    'Retrofit criterio 3. Borrado LÓGICO (activo=false): la tabla tiene activo.';


-- ----- migracion 011_sp_sesion.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/011_sp_sesion.sql
--  Proyecto Cátedras · RETROFIT criterio 3 de VERSION_2.md · 'sesion'
-- --------------------------------------------------------------------------
--  'sesion' NO tiene columna 'activo' -> 'sp_eliminar_sesion' es FÍSICO
--  (DELETE). Por eso 'sp_listar_sesion' devuelve todas las filas.
--
--  TRIGGER trg_sesion_consecutivo (BEFORE INSERT): si 'numero_reunion' llega
--  NULL o 0, el disparador PROPONE el máximo de la cátedra + 1. Por eso
--  'sp_crear_sesion':
--    · acepta 'p_numero_reunion' NULL (o 0, que es lo que manda el servicio
--      cuando el cliente no lo indicó) y lo pasa tal cual al INSERT;
--    · recoge 'id_sesion' con RETURNING y devuelve la fila RELEÍDA después
--      del INSERT, nunca armada con los parámetros: así el JSON trae el
--      'numero_reunion' que calculó el trigger y el 'creado_en' de la BD.
--
--  ORDEN DE LOS PARÁMETROS de 'sp_crear_sesion': PostgreSQL exige que todo
--  parámetro IN después de uno con DEFAULT también lo tenga, así que van
--  primero los 6 obligatorios y después los opcionales (con DEFAULT).
-- ==========================================================================

SET search_path TO catedras, public;

-- LISTAR — todas, con filtro OPCIONAL por cátedra (NULL = todas) y límite
-- OPCIONAL. Mismo orden de siempre: por cátedra y número de reunión.
CREATE OR REPLACE PROCEDURE catedras.sp_listar_sesion(
    IN    p_fk_catedra bigint  DEFAULT NULL,
    IN    p_limite     integer DEFAULT NULL,
    INOUT p_resultado  json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(s), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_sesion, fk_catedra, numero_reunion, numero_reunion_asis,
                   titulo, inicio, fin, fk_modalidad, fk_sede, fk_periodo,
                   fk_encuesta, lugar, enlace_virtual, cupo, estado, creado_en
              FROM sesion
             WHERE p_fk_catedra IS NULL OR fk_catedra = p_fk_catedra
             ORDER BY fk_catedra, numero_reunion
             LIMIT p_limite) s;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_consultar_sesion(
    IN    p_id_sesion bigint,
    INOUT p_resultado json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(s) INTO p_resultado
      FROM (SELECT id_sesion, fk_catedra, numero_reunion, numero_reunion_asis,
                   titulo, inicio, fin, fk_modalidad, fk_sede, fk_periodo,
                   fk_encuesta, lugar, enlace_virtual, cupo, estado, creado_en
              FROM sesion
             WHERE id_sesion = p_id_sesion) s;
END;
$$;

-- Rangos inválidos (chk_sesion_fechas, chk_sesion_cupo, chk_sesion_estado),
-- reunión repetida en la cátedra (uq_sesion_reunion) o FK inexistente ->
-- NpgsqlException.
CREATE OR REPLACE PROCEDURE catedras.sp_crear_sesion(
    IN    p_fk_catedra          bigint,
    IN    p_inicio              timestamp with time zone,
    IN    p_fin                 timestamp with time zone,
    IN    p_fk_modalidad        character varying(15),
    IN    p_fk_sede             character varying(15),
    IN    p_fk_periodo          character(6),
    IN    p_numero_reunion      smallint                 DEFAULT NULL,
    IN    p_numero_reunion_asis smallint                 DEFAULT NULL,
    IN    p_titulo              character varying(200)   DEFAULT NULL,
    IN    p_fk_encuesta         bigint                   DEFAULT NULL,
    IN    p_lugar               character varying(200)   DEFAULT NULL,
    IN    p_enlace_virtual      character varying(300)   DEFAULT NULL,
    IN    p_cupo                integer                  DEFAULT NULL,
    IN    p_estado              character varying(15)    DEFAULT 'PROGRAMADA',
    INOUT p_resultado           json                     DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_id bigint;
BEGIN
    INSERT INTO sesion (fk_catedra, numero_reunion, numero_reunion_asis, titulo,
                        inicio, fin, fk_modalidad, fk_sede, fk_periodo,
                        fk_encuesta, lugar, enlace_virtual, cupo, estado)
    VALUES (p_fk_catedra, p_numero_reunion, p_numero_reunion_asis, p_titulo,
            p_inicio, p_fin, p_fk_modalidad, p_fk_sede, p_fk_periodo,
            p_fk_encuesta, p_lugar, p_enlace_virtual, p_cupo, p_estado)
    RETURNING id_sesion INTO v_id;

    -- Relectura DESPUÉS del INSERT (y de su trigger): trae el
    -- numero_reunion calculado y el creado_en.
    SELECT row_to_json(s) INTO p_resultado
      FROM (SELECT id_sesion, fk_catedra, numero_reunion, numero_reunion_asis,
                   titulo, inicio, fin, fk_modalidad, fk_sede, fk_periodo,
                   fk_encuesta, lugar, enlace_virtual, cupo, estado, creado_en
              FROM sesion
             WHERE id_sesion = v_id) s;
END;
$$;

-- Reemplazo COMPLETO. Aquí 'numero_reunion' es obligatorio y NO se
-- recalcula: el trigger es BEFORE INSERT, no actúa en el UPDATE.
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_sesion(
    IN    p_id_sesion           bigint,
    IN    p_fk_catedra          bigint,
    IN    p_numero_reunion      smallint,
    IN    p_numero_reunion_asis smallint,
    IN    p_titulo              character varying(200),
    IN    p_inicio              timestamp with time zone,
    IN    p_fin                 timestamp with time zone,
    IN    p_fk_modalidad        character varying(15),
    IN    p_fk_sede             character varying(15),
    IN    p_fk_periodo          character(6),
    IN    p_fk_encuesta         bigint,
    IN    p_lugar               character varying(200),
    IN    p_enlace_virtual      character varying(300),
    IN    p_cupo                integer,
    IN    p_estado              character varying(15),
    INOUT p_resultado           json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE sesion
       SET fk_catedra          = p_fk_catedra,
           numero_reunion      = p_numero_reunion,
           numero_reunion_asis = p_numero_reunion_asis,
           titulo              = p_titulo,
           inicio              = p_inicio,
           fin                 = p_fin,
           fk_modalidad        = p_fk_modalidad,
           fk_sede             = p_fk_sede,
           fk_periodo          = p_fk_periodo,
           fk_encuesta         = p_fk_encuesta,
           lugar               = p_lugar,
           enlace_virtual      = p_enlace_virtual,
           cupo                = p_cupo,
           estado              = p_estado
     WHERE id_sesion = p_id_sesion;

    IF FOUND THEN
        SELECT row_to_json(s) INTO p_resultado
          FROM (SELECT id_sesion, fk_catedra, numero_reunion, numero_reunion_asis,
                       titulo, inicio, fin, fk_modalidad, fk_sede, fk_periodo,
                       fk_encuesta, lugar, enlace_virtual, cupo, estado, creado_en
                  FROM sesion
                 WHERE id_sesion = p_id_sesion) s;
    END IF;
END;
$$;

-- FÍSICO: la tabla no tiene 'activo'. Las ponencias y los enlaces de
-- registro de la sesión caen en cascada (fk_ponencia_sesion,
-- fk_enlace_sesion); los registros de asistencia y los lotes de migración la
-- bloquean (fk_registro_sesion, fk_lotemig_sesion, ON DELETE RESTRICT) y
-- PostgreSQL lanza NpgsqlException, igual que con el DELETE de antes.
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_sesion(
    IN    p_id_sesion bigint,
    INOUT p_resultado json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    DELETE FROM sesion
     WHERE id_sesion = p_id_sesion;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_sesion IS
    'Retrofit criterio 3 (VERSION_2.md §4). Todas (sin activo), filtro opcional por cátedra y límite opcional.';
COMMENT ON PROCEDURE catedras.sp_consultar_sesion IS
    'Retrofit criterio 3. NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_sesion IS
    'Retrofit criterio 3. Devuelve la fila RELEÍDA tras el INSERT: trae el numero_reunion que calcula trg_sesion_consecutivo.';
COMMENT ON PROCEDURE catedras.sp_actualizar_sesion IS
    'Retrofit criterio 3. Reemplazo completo; PATCH mezcla antes de llamar.';
COMMENT ON PROCEDURE catedras.sp_eliminar_sesion IS
    'Retrofit criterio 3. Borrado FÍSICO: la tabla no tiene activo.';


-- ----- migracion 012_sp_ponencia.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/012_sp_ponencia.sql
--  Proyecto Cátedras · RETROFIT criterio 3 de VERSION_2.md · 'ponencia'
-- --------------------------------------------------------------------------
--  LLAVE COMPUESTA: la clave primaria es (fk_sesion, fk_asistente, rol) y
--  la tabla NO tiene id propio. Por eso 'sp_consultar_ponencia',
--  'sp_actualizar_ponencia' y 'sp_eliminar_ponencia' reciben los TRES
--  valores, no un solo id.
--
--  'ponencia' NO tiene columna 'activo' -> 'sp_eliminar_ponencia' es FÍSICO
--  (DELETE) y 'sp_listar_ponencia' devuelve todas las filas.
--
--  'sp_actualizar_ponencia': las tres columnas de la tabla SON la llave, así
--  que "reemplazar todas las columnas" con los mismos tres valores no
--  cambiaría nada. Lo único que tiene sentido cambiar en una ponencia
--  existente es el ROL (p. ej. de PONENTE a MODERADOR); por eso, además de
--  los 3 valores de la llave, recibe 'p_rol_nuevo'. Mover una ponencia a
--  otra sesión o a otro asistente es eliminar + crear. Este procedimiento
--  NO tiene llamador en C#: IRepositorioPonencia no tiene ActualizarAsync
--  (PLAN_RETROFIT_SP.md, Grupo B: "Sin Actualizar").
-- ==========================================================================

SET search_path TO catedras, public;

-- LISTAR — todas, con filtros OPCIONALES por sesión y por asistente
-- (NULL = sin filtro) y límite OPCIONAL.
CREATE OR REPLACE PROCEDURE catedras.sp_listar_ponencia(
    IN    p_fk_sesion    bigint  DEFAULT NULL,
    IN    p_fk_asistente bigint  DEFAULT NULL,
    IN    p_limite       integer DEFAULT NULL,
    INOUT p_resultado    json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(p), '[]'::json)
      INTO p_resultado
      FROM (SELECT fk_sesion, fk_asistente, rol
              FROM ponencia
             WHERE (p_fk_sesion    IS NULL OR fk_sesion    = p_fk_sesion)
               AND (p_fk_asistente IS NULL OR fk_asistente = p_fk_asistente)
             ORDER BY fk_sesion, fk_asistente, rol
             LIMIT p_limite) p;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_consultar_ponencia(
    IN    p_fk_sesion    bigint,
    IN    p_fk_asistente bigint,
    IN    p_rol          character varying(15),
    INOUT p_resultado    json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(p) INTO p_resultado
      FROM (SELECT fk_sesion, fk_asistente, rol
              FROM ponencia
             WHERE fk_sesion    = p_fk_sesion
               AND fk_asistente = p_fk_asistente
               AND rol          = p_rol) p;
END;
$$;

-- Llave repetida (pk_ponencia), rol fuera de la lista (chk_ponencia_rol),
-- sesión o asistente inexistentes -> NpgsqlException.
CREATE OR REPLACE PROCEDURE catedras.sp_crear_ponencia(
    IN    p_fk_sesion    bigint,
    IN    p_fk_asistente bigint,
    IN    p_rol          character varying(15) DEFAULT 'PONENTE',
    INOUT p_resultado    json                  DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    INSERT INTO ponencia (fk_sesion, fk_asistente, rol)
    VALUES (p_fk_sesion, p_fk_asistente, COALESCE(p_rol, 'PONENTE'));

    SELECT row_to_json(p) INTO p_resultado
      FROM (SELECT fk_sesion, fk_asistente, rol
              FROM ponencia
             WHERE fk_sesion    = p_fk_sesion
               AND fk_asistente = p_fk_asistente
               AND rol          = COALESCE(p_rol, 'PONENTE')) p;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_ponencia(
    IN    p_fk_sesion    bigint,
    IN    p_fk_asistente bigint,
    IN    p_rol          character varying(15),
    IN    p_rol_nuevo    character varying(15),
    INOUT p_resultado    json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE ponencia
       SET rol = p_rol_nuevo
     WHERE fk_sesion    = p_fk_sesion
       AND fk_asistente = p_fk_asistente
       AND rol          = p_rol;

    IF FOUND THEN
        SELECT row_to_json(p) INTO p_resultado
          FROM (SELECT fk_sesion, fk_asistente, rol
                  FROM ponencia
                 WHERE fk_sesion    = p_fk_sesion
                   AND fk_asistente = p_fk_asistente
                   AND rol          = p_rol_nuevo) p;
    END IF;
END;
$$;

-- FÍSICO: la tabla no tiene 'activo'.
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_ponencia(
    IN    p_fk_sesion    bigint,
    IN    p_fk_asistente bigint,
    IN    p_rol          character varying(15),
    INOUT p_resultado    json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    DELETE FROM ponencia
     WHERE fk_sesion    = p_fk_sesion
       AND fk_asistente = p_fk_asistente
       AND rol          = p_rol;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_ponencia IS
    'Retrofit criterio 3 (VERSION_2.md §4). Todas (sin activo), filtros opcionales por sesión/asistente y límite opcional.';
COMMENT ON PROCEDURE catedras.sp_consultar_ponencia IS
    'Retrofit criterio 3. Llave compuesta (sesión, asistente, rol). NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_ponencia IS
    'Retrofit criterio 3. Devuelve la fila como quedó guardada.';
COMMENT ON PROCEDURE catedras.sp_actualizar_ponencia IS
    'Retrofit criterio 3. Llave (sesión, asistente, rol) + rol nuevo: lo único que se puede cambiar sin cambiar la llave de otra forma. Sin llamador en C#.';
COMMENT ON PROCEDURE catedras.sp_eliminar_ponencia IS
    'Retrofit criterio 3. Borrado FÍSICO: la tabla no tiene activo. Llave compuesta.';


-- ----- migracion 013_sp_parametro.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/013_sp_parametro.sql
--  Proyecto Cátedras · RETROFIT criterio 3 de VERSION_2.md · 'parametro'
-- --------------------------------------------------------------------------
--  'parametro' es la configuración que el administrador cambia sin tocar el
--  código. Clave primaria: 'clave' (varchar(60)). NO tiene columna 'activo'
--  -> 'sp_eliminar_parametro' es FÍSICO (DELETE) y 'sp_listar_parametro'
--  devuelve todas las filas.
--
--  Esta tabla NO tenía código en la API (ni modelo, ni repositorio, ni
--  servicio, ni controlador): su capa C# se construye junto con esta
--  migración.
--
--  'modificado_en' se refresca en cada 'sp_actualizar_parametro'; en el
--  alta lo pone el DEFAULT now() de la columna. 'fk_usuario_modifica' es
--  OPCIONAL (la API aún no tiene sesión de usuario que lo llene sola).
-- ==========================================================================

SET search_path TO catedras, public;

CREATE OR REPLACE PROCEDURE catedras.sp_listar_parametro(
    IN    p_limite    integer DEFAULT NULL,
    INOUT p_resultado json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(p), '[]'::json)
      INTO p_resultado
      FROM (SELECT clave, valor, tipo_dato, descripcion,
                   fk_usuario_modifica, modificado_en
              FROM parametro
             ORDER BY clave
             LIMIT p_limite) p;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_consultar_parametro(
    IN    p_clave     character varying(60),
    INOUT p_resultado json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(p) INTO p_resultado
      FROM (SELECT clave, valor, tipo_dato, descripcion,
                   fk_usuario_modifica, modificado_en
              FROM parametro
             WHERE clave = p_clave) p;
END;
$$;

-- Clave repetida (pk_parametro), tipo_dato fuera de la lista
-- (chk_parametro_tipo) o usuario inexistente (fk_parametro_usuario) ->
-- NpgsqlException.
CREATE OR REPLACE PROCEDURE catedras.sp_crear_parametro(
    IN    p_clave               character varying(60),
    IN    p_valor               character varying(200),
    IN    p_descripcion         character varying(300),
    IN    p_tipo_dato           character varying(15) DEFAULT 'TEXTO',
    IN    p_fk_usuario_modifica bigint                DEFAULT NULL,
    INOUT p_resultado           json                  DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    INSERT INTO parametro (clave, valor, tipo_dato, descripcion, fk_usuario_modifica)
    VALUES (p_clave, p_valor, COALESCE(p_tipo_dato, 'TEXTO'), p_descripcion,
            p_fk_usuario_modifica);

    SELECT row_to_json(p) INTO p_resultado
      FROM (SELECT clave, valor, tipo_dato, descripcion,
                   fk_usuario_modifica, modificado_en
              FROM parametro
             WHERE clave = p_clave) p;
END;
$$;

-- Reemplazo COMPLETO de las columnas de negocio ('clave' es la llave y no
-- cambia). Refresca 'modificado_en'.
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_parametro(
    IN    p_clave               character varying(60),
    IN    p_valor               character varying(200),
    IN    p_tipo_dato           character varying(15),
    IN    p_descripcion         character varying(300),
    IN    p_fk_usuario_modifica bigint,
    INOUT p_resultado           json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE parametro
       SET valor               = p_valor,
           tipo_dato           = p_tipo_dato,
           descripcion         = p_descripcion,
           fk_usuario_modifica = p_fk_usuario_modifica,
           modificado_en       = now()
     WHERE clave = p_clave;

    IF FOUND THEN
        SELECT row_to_json(p) INTO p_resultado
          FROM (SELECT clave, valor, tipo_dato, descripcion,
                       fk_usuario_modifica, modificado_en
                  FROM parametro
                 WHERE clave = p_clave) p;
    END IF;
END;
$$;

-- FÍSICO: la tabla no tiene 'activo'.
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_parametro(
    IN    p_clave     character varying(60),
    INOUT p_resultado json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    DELETE FROM parametro
     WHERE clave = p_clave;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_parametro IS
    'Retrofit criterio 3 (VERSION_2.md §4). Todas (sin activo), con límite opcional.';
COMMENT ON PROCEDURE catedras.sp_consultar_parametro IS
    'Retrofit criterio 3. NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_parametro IS
    'Retrofit criterio 3. Devuelve la fila como quedó (modificado_en ya puesto).';
COMMENT ON PROCEDURE catedras.sp_actualizar_parametro IS
    'Retrofit criterio 3. Reemplazo completo; refresca modificado_en. PATCH mezcla antes de llamar.';
COMMENT ON PROCEDURE catedras.sp_eliminar_parametro IS
    'Retrofit criterio 3. Borrado FÍSICO: la tabla no tiene activo.';


-- ----- migracion 014_sp_bitacora.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/014_sp_bitacora.sql
--  Proyecto Cátedras · RETROFIT criterio 3 de VERSION_2.md · 'bitacora'
-- --------------------------------------------------------------------------
--  ⚠️  'bitacora' es el LOG DE AUDITORÍA (RN-35). Sus columnas se llenan
--  SOLAS: 'ocurrido_en' (DEFAULT now()) y 'usuario_bd' (DEFAULT
--  CURRENT_USER) las pone la BD, y los triggers trg_bitacora_asistente /
--  trg_bitacora_registro (fn_trg_bitacora) son quienes escriben las filas.
--
--  Para cumplir el criterio de los 5 verbos AL PIE DE LA LETRA existen los
--  5 procedimientos, PERO:
--    · LA API SÍ EXPONE Crear / Actualizar / Eliminar (POST/PUT/PATCH/
--      DELETE /api/Bitacora), a pedido del equipo para que los 5
--      procedimientos tengan llamador. RIESGO: permite reescribir o borrar
--      el log de auditoría por HTTP; conviene protegerlo con autorización.
--    · 'sp_crear_bitacora' NO recibe 'ocurrido_en' ni 'usuario_bd': los
--      pone siempre la BD (así nadie puede falsear cuándo o quién).
--    · 'sp_actualizar_bitacora' tampoco toca esas dos columnas: reemplaza
--      las demás columnas de negocio (fk_usuario, nombre_tabla, operacion,
--      llave, datos_antes, datos_despues).
--
--  'bitacora' NO tiene columna 'activo' -> 'sp_eliminar_bitacora' sería
--  FÍSICO (DELETE). 'sp_listar_bitacora' devuelve todas las filas, las más
--  recientes primero.
--
--  Esta tabla NO tenía código en la API: su capa C# se construye junto con
--  esta migración.
-- ==========================================================================

SET search_path TO catedras, public;

-- LISTAR — más recientes primero (ix_bitacora_fecha / ix_bitacora_tabla),
-- con filtro OPCIONAL por tabla auditada (NULL = todas) y límite OPCIONAL.
CREATE OR REPLACE PROCEDURE catedras.sp_listar_bitacora(
    IN    p_nombre_tabla character varying(63) DEFAULT NULL,
    IN    p_limite       integer               DEFAULT NULL,
    INOUT p_resultado    json                  DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(b), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_bitacora, ocurrido_en, usuario_bd, fk_usuario,
                   nombre_tabla, operacion, llave, datos_antes, datos_despues
              FROM bitacora
             WHERE p_nombre_tabla IS NULL OR nombre_tabla = p_nombre_tabla
             ORDER BY ocurrido_en DESC, id_bitacora DESC
             LIMIT p_limite) b;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_consultar_bitacora(
    IN    p_id_bitacora bigint,
    INOUT p_resultado   json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(b) INTO p_resultado
      FROM (SELECT id_bitacora, ocurrido_en, usuario_bd, fk_usuario,
                   nombre_tabla, operacion, llave, datos_antes, datos_despues
              FROM bitacora
             WHERE id_bitacora = p_id_bitacora) b;
END;
$$;

-- Operación fuera de INSERT/UPDATE/DELETE (chk_bitacora_operacion) o usuario
-- inexistente (fk_bitacora_usuario) -> NpgsqlException.
CREATE OR REPLACE PROCEDURE catedras.sp_crear_bitacora(
    IN    p_nombre_tabla  character varying(63),
    IN    p_operacion     character varying(10),
    IN    p_fk_usuario    bigint                 DEFAULT NULL,
    IN    p_llave         character varying(100) DEFAULT NULL,
    IN    p_datos_antes   jsonb                  DEFAULT NULL,
    IN    p_datos_despues jsonb                  DEFAULT NULL,
    INOUT p_resultado     json                   DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_id bigint;
BEGIN
    INSERT INTO bitacora (fk_usuario, nombre_tabla, operacion, llave,
                          datos_antes, datos_despues)
    VALUES (p_fk_usuario, p_nombre_tabla, p_operacion, p_llave,
            p_datos_antes, p_datos_despues)
    RETURNING id_bitacora INTO v_id;

    SELECT row_to_json(b) INTO p_resultado
      FROM (SELECT id_bitacora, ocurrido_en, usuario_bd, fk_usuario,
                   nombre_tabla, operacion, llave, datos_antes, datos_despues
              FROM bitacora
             WHERE id_bitacora = v_id) b;
END;
$$;

-- Reemplazo de las columnas de negocio; NO toca 'ocurrido_en' ni
-- 'usuario_bd' (se llenan solas al insertar y no se reescriben).
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_bitacora(
    IN    p_id_bitacora   bigint,
    IN    p_fk_usuario    bigint,
    IN    p_nombre_tabla  character varying(63),
    IN    p_operacion     character varying(10),
    IN    p_llave         character varying(100),
    IN    p_datos_antes   jsonb,
    IN    p_datos_despues jsonb,
    INOUT p_resultado     json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE bitacora
       SET fk_usuario    = p_fk_usuario,
           nombre_tabla  = p_nombre_tabla,
           operacion     = p_operacion,
           llave         = p_llave,
           datos_antes   = p_datos_antes,
           datos_despues = p_datos_despues
     WHERE id_bitacora = p_id_bitacora;

    IF FOUND THEN
        SELECT row_to_json(b) INTO p_resultado
          FROM (SELECT id_bitacora, ocurrido_en, usuario_bd, fk_usuario,
                       nombre_tabla, operacion, llave, datos_antes, datos_despues
                  FROM bitacora
                 WHERE id_bitacora = p_id_bitacora) b;
    END IF;
END;
$$;

-- FÍSICO: la tabla no tiene 'activo'.
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_bitacora(
    IN    p_id_bitacora bigint,
    INOUT p_resultado   json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    DELETE FROM bitacora
     WHERE id_bitacora = p_id_bitacora;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_bitacora IS
    'Retrofit criterio 3 (VERSION_2.md §4). Más recientes primero; filtro opcional por tabla y límite opcional.';
COMMENT ON PROCEDURE catedras.sp_consultar_bitacora IS
    'Retrofit criterio 3. NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_bitacora IS
    'Retrofit criterio 3. POST /api/Bitacora. ocurrido_en y usuario_bd los pone siempre la BD.';
COMMENT ON PROCEDURE catedras.sp_actualizar_bitacora IS
    'Retrofit criterio 3. PUT/PATCH /api/Bitacora. No toca ocurrido_en ni usuario_bd.';
COMMENT ON PROCEDURE catedras.sp_eliminar_bitacora IS
    'Retrofit criterio 3. DELETE /api/Bitacora. Borrado FÍSICO: la tabla no tiene activo.';


-- ----- migracion 015_sp_encuesta.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/015_sp_encuesta.sql
--  Proyecto Cátedras · RETROFIT criterio 3 de VERSION_2.md (Fase 2 de
--  PLAN_RETROFIT_SP.md): los 5 verbos de 'encuesta' por procedimiento
--  almacenado, con el MISMO molde de 005_sp_sede_ejemplo.sql.
--
--  AJUSTES respecto a sede:
--   · 'encuesta' tiene 'activa' (femenino), NO 'activo'. La regla del
--     retrofit dice "UPDATE activo=false si la tabla tiene columna
--     'activo', DELETE si no" (y PLAN_RETROFIT_SP.md §2 la marca "No") ->
--     'sp_eliminar_encuesta' es un DELETE FÍSICO y 'sp_listar_encuesta'
--     devuelve TODAS las filas. Una encuesta con respuestas
--     (fk_respenc_encuesta, ON DELETE RESTRICT) no se deja borrar.
--   · 'id_encuesta' es GENERATED ALWAYS AS IDENTITY: lo pone la BD, no el
--     cliente. 'sp_crear_encuesta' NO recibe id: lo captura con
--     RETURNING ... INTO v_id y devuelve la fila con él (el repositorio lo
--     lee del JSON, porque 'CrearAsync' devuelve el id nuevo).
--   · 'sp_crear_encuesta' recibe 'p_activa' (el POST la admite).
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  LISTAR — todas las filas (no hay 'activo'), con límite OPCIONAL.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_listar_encuesta(
    IN    p_limite    integer DEFAULT NULL,
    INOUT p_resultado json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(e), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_encuesta, nombre, version, vigente_desde, vigente_hasta, activa
              FROM encuesta
             ORDER BY id_encuesta
             LIMIT p_limite) e;
END;
$$;

-- --------------------------------------------------------------------------
--  CONSULTAR — una encuesta por id. NULL en p_resultado = no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_consultar_encuesta(
    IN    p_id_encuesta bigint,
    INOUT p_resultado   json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(e) INTO p_resultado
      FROM (SELECT id_encuesta, nombre, version, vigente_desde, vigente_hasta, activa
              FROM encuesta
             WHERE id_encuesta = p_id_encuesta) e;
END;
$$;

-- --------------------------------------------------------------------------
--  CREAR — el id lo genera la BD (IDENTITY): se captura en 'v_id' y se
--  devuelve la fila como quedó guardada. vigente_hasta < vigente_desde
--  (chk_encuesta_vig) o version < 1 (chk_encuesta_ver) -> NpgsqlException.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_crear_encuesta(
    IN    p_nombre        character varying(120),
    IN    p_version       smallint DEFAULT 1,
    IN    p_vigente_desde date     DEFAULT CURRENT_DATE,
    IN    p_vigente_hasta date     DEFAULT NULL,
    IN    p_activa        boolean  DEFAULT true,
    INOUT p_resultado     json     DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_id bigint;
BEGIN
    INSERT INTO encuesta (nombre, version, vigente_desde, vigente_hasta, activa)
    VALUES (p_nombre, p_version, p_vigente_desde, p_vigente_hasta, p_activa)
    RETURNING id_encuesta INTO v_id;

    SELECT row_to_json(e) INTO p_resultado
      FROM (SELECT id_encuesta, nombre, version, vigente_desde, vigente_hasta, activa
              FROM encuesta
             WHERE id_encuesta = v_id) e;
END;
$$;

-- --------------------------------------------------------------------------
--  ACTUALIZAR — reemplazo COMPLETO (PUT directo; PATCH llega ya mezclado
--  desde el repositorio). Sin FOUND -> p_resultado NULL = no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_encuesta(
    IN    p_id_encuesta   bigint,
    IN    p_nombre        character varying(120),
    IN    p_version       smallint,
    IN    p_vigente_desde date,
    IN    p_vigente_hasta date,
    IN    p_activa        boolean,
    INOUT p_resultado     json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE encuesta
       SET nombre        = p_nombre,
           version       = p_version,
           vigente_desde = p_vigente_desde,
           vigente_hasta = p_vigente_hasta,
           activa        = p_activa
     WHERE id_encuesta = p_id_encuesta;

    IF FOUND THEN
        SELECT row_to_json(e) INTO p_resultado
          FROM (SELECT id_encuesta, nombre, version, vigente_desde, vigente_hasta, activa
                  FROM encuesta
                 WHERE id_encuesta = p_id_encuesta) e;
    END IF;
END;
$$;

-- --------------------------------------------------------------------------
--  ELIMINAR — FÍSICO (la tabla NO tiene 'activo'; su 'activa' es un dato
--  de negocio que se cambia con PUT/PATCH). FK en uso -> excepción.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_encuesta(
    IN    p_id_encuesta bigint,
    INOUT p_resultado   json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    DELETE FROM encuesta
     WHERE id_encuesta = p_id_encuesta;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_encuesta IS
    'Retrofit criterio 3 (VERSION_2.md §4). Todas las filas, con límite opcional.';
COMMENT ON PROCEDURE catedras.sp_consultar_encuesta IS
    'Retrofit criterio 3. NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_encuesta IS
    'Retrofit criterio 3. id IDENTITY; devuelve la fila como quedó guardada.';
COMMENT ON PROCEDURE catedras.sp_actualizar_encuesta IS
    'Retrofit criterio 3. Reemplazo completo; PATCH mezcla antes de llamar.';
COMMENT ON PROCEDURE catedras.sp_eliminar_encuesta IS
    'Retrofit criterio 3. Borrado FÍSICO (DELETE): la tabla no tiene activo.';


-- ----- migracion 016_sp_estado_proceso.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/016_sp_estado_proceso.sql
--  Proyecto Cátedras · RETROFIT criterio 3 de VERSION_2.md (Fase 2 de
--  PLAN_RETROFIT_SP.md): los 5 verbos de 'estado_proceso' por procedimiento
--  almacenado, con el MISMO molde de 005_sp_sede_ejemplo.sql.
--
--  'estado_proceso' NO tiene columna 'activo' -> 'sp_eliminar_estado_proceso'
--  es un DELETE físico y 'sp_listar_estado_proceso' devuelve TODAS las
--  filas (no hay nada que filtrar). Un estado en uso por un lote de
--  migración (fk_lotemig_estado) hace fallar el DELETE -> NpgsqlException.
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  LISTAR — todas las filas (no hay 'activo'), con límite OPCIONAL.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_listar_estado_proceso(
    IN    p_limite     integer DEFAULT NULL,
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(e), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_estado, nombre, es_final, es_exitoso
              FROM estado_proceso
             ORDER BY id_estado
             LIMIT p_limite) e;
END;
$$;

-- --------------------------------------------------------------------------
--  CONSULTAR — una fila por id. NULL en p_resultado = no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_consultar_estado_proceso(
    IN    p_id_estado  character varying(12),
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(e) INTO p_resultado
      FROM (SELECT id_estado, nombre, es_final, es_exitoso
              FROM estado_proceso
             WHERE id_estado = p_id_estado) e;
END;
$$;

-- --------------------------------------------------------------------------
--  CREAR — devuelve la fila como quedó guardada. Id repetido o un CHECK
--  violado -> NpgsqlException (el controlador la traduce, criterio 7).
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_crear_estado_proceso(
    IN    p_id_estado  character varying(12),
    IN    p_nombre     character varying(40),
    IN    p_es_final   boolean DEFAULT false,
    IN    p_es_exitoso boolean DEFAULT false,
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    INSERT INTO estado_proceso (id_estado, nombre, es_final, es_exitoso)
    VALUES (p_id_estado, p_nombre, p_es_final, p_es_exitoso);

    SELECT row_to_json(e) INTO p_resultado
      FROM (SELECT id_estado, nombre, es_final, es_exitoso
              FROM estado_proceso
             WHERE id_estado = p_id_estado) e;
END;
$$;

-- --------------------------------------------------------------------------
--  ACTUALIZAR — reemplazo COMPLETO (PUT directo; PATCH llega ya mezclado
--  desde el repositorio). Sin FOUND -> p_resultado NULL = no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_estado_proceso(
    IN    p_id_estado  character varying(12),
    IN    p_nombre     character varying(40),
    IN    p_es_final   boolean,
    IN    p_es_exitoso boolean,
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE estado_proceso
       SET nombre     = p_nombre,
           es_final   = p_es_final,
           es_exitoso = p_es_exitoso
     WHERE id_estado = p_id_estado;

    IF FOUND THEN
        SELECT row_to_json(e) INTO p_resultado
          FROM (SELECT id_estado, nombre, es_final, es_exitoso
                  FROM estado_proceso
                 WHERE id_estado = p_id_estado) e;
    END IF;
END;
$$;

-- --------------------------------------------------------------------------
--  ELIMINAR — FÍSICO (la tabla NO tiene 'activo'). FK en uso -> excepción.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_estado_proceso(
    IN    p_id_estado  character varying(12),
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    DELETE FROM estado_proceso
     WHERE id_estado = p_id_estado;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_estado_proceso IS
    'Retrofit criterio 3 (VERSION_2.md §4). Todas las filas, con límite opcional.';
COMMENT ON PROCEDURE catedras.sp_consultar_estado_proceso IS
    'Retrofit criterio 3. NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_estado_proceso IS
    'Retrofit criterio 3. Devuelve la fila como quedó guardada.';
COMMENT ON PROCEDURE catedras.sp_actualizar_estado_proceso IS
    'Retrofit criterio 3. Reemplazo completo; PATCH mezcla antes de llamar.';
COMMENT ON PROCEDURE catedras.sp_eliminar_estado_proceso IS
    'Retrofit criterio 3. Borrado FÍSICO (DELETE): la tabla no tiene activo.';


-- ----- migracion 017_sp_modalidad.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/017_sp_modalidad.sql
--  Proyecto Cátedras · RETROFIT criterio 3 de VERSION_2.md (Fase 2 de
--  PLAN_RETROFIT_SP.md): los 5 verbos de 'modalidad' por procedimiento
--  almacenado, con el MISMO molde de 005_sp_sede_ejemplo.sql.
--
--  'modalidad' SÍ tiene columna 'activo' -> 'sp_eliminar_modalidad' es
--  LÓGICO (UPDATE ... SET activo = false) y 'sp_listar_modalidad' devuelve
--  solo las activas, igual que sede.
--
--  AJUSTE respecto a sede: 'sp_crear_modalidad' recibe 'p_activo', porque
--  el POST de modalidad SÍ admite 'activo' y el INSERT anterior lo guardaba.
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  LISTAR — solo activas, con límite OPCIONAL (NULL = sin límite).
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_listar_modalidad(
    IN    p_limite    integer DEFAULT NULL,
    INOUT p_resultado json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(m), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_modalidad, nombre, canal_difusion, activo
              FROM modalidad
             WHERE activo
             ORDER BY id_modalidad
             LIMIT p_limite) m;
END;
$$;

-- --------------------------------------------------------------------------
--  CONSULTAR — una modalidad por id. NULL en p_resultado = no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_consultar_modalidad(
    IN    p_id_modalidad character varying(15),
    INOUT p_resultado    json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(m) INTO p_resultado
      FROM (SELECT id_modalidad, nombre, canal_difusion, activo
              FROM modalidad
             WHERE id_modalidad = p_id_modalidad) m;
END;
$$;

-- --------------------------------------------------------------------------
--  CREAR — devuelve la fila como quedó guardada. Canal fuera de
--  QR/ENLACE/AMBOS (chk_modalidad_canal) o id repetido -> NpgsqlException.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_crear_modalidad(
    IN    p_id_modalidad   character varying(15),
    IN    p_nombre         character varying(60),
    IN    p_canal_difusion character varying(10),
    IN    p_activo         boolean DEFAULT true,
    INOUT p_resultado      json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    INSERT INTO modalidad (id_modalidad, nombre, canal_difusion, activo)
    VALUES (p_id_modalidad, p_nombre, p_canal_difusion, p_activo);

    SELECT row_to_json(m) INTO p_resultado
      FROM (SELECT id_modalidad, nombre, canal_difusion, activo
              FROM modalidad
             WHERE id_modalidad = p_id_modalidad) m;
END;
$$;

-- --------------------------------------------------------------------------
--  ACTUALIZAR — reemplazo COMPLETO (PUT directo; PATCH llega ya mezclado
--  desde el repositorio). Sin FOUND -> p_resultado NULL = no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_modalidad(
    IN    p_id_modalidad   character varying(15),
    IN    p_nombre         character varying(60),
    IN    p_canal_difusion character varying(10),
    IN    p_activo         boolean,
    INOUT p_resultado      json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE modalidad
       SET nombre         = p_nombre,
           canal_difusion = p_canal_difusion,
           activo         = p_activo
     WHERE id_modalidad = p_id_modalidad;

    IF FOUND THEN
        SELECT row_to_json(m) INTO p_resultado
          FROM (SELECT id_modalidad, nombre, canal_difusion, activo
                  FROM modalidad
                 WHERE id_modalidad = p_id_modalidad) m;
    END IF;
END;
$$;

-- --------------------------------------------------------------------------
--  ELIMINAR — LÓGICO (la tabla tiene 'activo'). Ya inactiva -> no cuenta.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_modalidad(
    IN    p_id_modalidad character varying(15),
    INOUT p_resultado    json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE modalidad
       SET activo = false
     WHERE id_modalidad = p_id_modalidad
       AND activo;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_modalidad IS
    'Retrofit criterio 3 (VERSION_2.md §4). Solo activas, con límite opcional.';
COMMENT ON PROCEDURE catedras.sp_consultar_modalidad IS
    'Retrofit criterio 3. NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_modalidad IS
    'Retrofit criterio 3. Devuelve la fila como quedó guardada.';
COMMENT ON PROCEDURE catedras.sp_actualizar_modalidad IS
    'Retrofit criterio 3. Reemplazo completo; PATCH mezcla antes de llamar.';
COMMENT ON PROCEDURE catedras.sp_eliminar_modalidad IS
    'Retrofit criterio 3. Borrado LÓGICO (activo=false): la tabla tiene activo.';


-- ----- migracion 018_sp_periodo_academico.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/018_sp_periodo_academico.sql
--  Proyecto Cátedras · RETROFIT criterio 3 de VERSION_2.md (Fase 2 de
--  PLAN_RETROFIT_SP.md): los 5 verbos de 'periodo_academico' por procedimiento
--  almacenado, con el MISMO molde de 005_sp_sede_ejemplo.sql.
--
--  'periodo_academico' SÍ tiene columna 'activo' -> borrado LÓGICO y listar
--  solo activos, igual que sede.
--
--  AJUSTES respecto a sede: (1) 'sp_crear_periodo_academico' recibe
--  'p_activo' (el POST lo admite); (2) la tabla NO tiene 'nombre': sus
--  columnas de negocio son fecha_ini/fecha_fin; (3) la pk es character(6)
--  con formato AAAA-S (chk_periodo_formato).
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  LISTAR — solo activos, con límite OPCIONAL (NULL = sin límite).
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_listar_periodo_academico(
    IN    p_limite     integer DEFAULT NULL,
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(p), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_periodo, fecha_ini, fecha_fin, activo
              FROM periodo_academico
             WHERE activo
             ORDER BY id_periodo
             LIMIT p_limite) p;
END;
$$;

-- --------------------------------------------------------------------------
--  CONSULTAR — una fila por id. NULL en p_resultado = no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_consultar_periodo_academico(
    IN    p_id_periodo character(6),
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(p) INTO p_resultado
      FROM (SELECT id_periodo, fecha_ini, fecha_fin, activo
              FROM periodo_academico
             WHERE id_periodo = p_id_periodo) p;
END;
$$;

-- --------------------------------------------------------------------------
--  CREAR — devuelve la fila como quedó guardada. Id repetido o un CHECK
--  violado -> NpgsqlException (el controlador la traduce, criterio 7).
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_crear_periodo_academico(
    IN    p_id_periodo character(6),
    IN    p_fecha_ini  date,
    IN    p_fecha_fin  date,
    IN    p_activo     boolean DEFAULT true,
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    INSERT INTO periodo_academico (id_periodo, fecha_ini, fecha_fin, activo)
    VALUES (p_id_periodo, p_fecha_ini, p_fecha_fin, p_activo);

    SELECT row_to_json(p) INTO p_resultado
      FROM (SELECT id_periodo, fecha_ini, fecha_fin, activo
              FROM periodo_academico
             WHERE id_periodo = p_id_periodo) p;
END;
$$;

-- --------------------------------------------------------------------------
--  ACTUALIZAR — reemplazo COMPLETO (PUT directo; PATCH llega ya mezclado
--  desde el repositorio). Sin FOUND -> p_resultado NULL = no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_periodo_academico(
    IN    p_id_periodo character(6),
    IN    p_fecha_ini  date,
    IN    p_fecha_fin  date,
    IN    p_activo     boolean,
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE periodo_academico
       SET fecha_ini = p_fecha_ini,
           fecha_fin = p_fecha_fin,
           activo    = p_activo
     WHERE id_periodo = p_id_periodo;

    IF FOUND THEN
        SELECT row_to_json(p) INTO p_resultado
          FROM (SELECT id_periodo, fecha_ini, fecha_fin, activo
                  FROM periodo_academico
                 WHERE id_periodo = p_id_periodo) p;
    END IF;
END;
$$;

-- --------------------------------------------------------------------------
--  ELIMINAR — LÓGICO (la tabla tiene 'activo'). Ya inactivo -> no cuenta.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_periodo_academico(
    IN    p_id_periodo character(6),
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE periodo_academico
       SET activo = false
     WHERE id_periodo = p_id_periodo
       AND activo;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_periodo_academico IS
    'Retrofit criterio 3 (VERSION_2.md §4). Solo activos, con límite opcional.';
COMMENT ON PROCEDURE catedras.sp_consultar_periodo_academico IS
    'Retrofit criterio 3. NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_periodo_academico IS
    'Retrofit criterio 3. Devuelve la fila como quedó guardada.';
COMMENT ON PROCEDURE catedras.sp_actualizar_periodo_academico IS
    'Retrofit criterio 3. Reemplazo completo; PATCH mezcla antes de llamar.';
COMMENT ON PROCEDURE catedras.sp_eliminar_periodo_academico IS
    'Retrofit criterio 3. Borrado LÓGICO (activo=false): la tabla tiene activo.';


-- ----- migracion 019_sp_rol.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/019_sp_rol.sql
--  Proyecto Cátedras · RETROFIT criterio 3 de VERSION_2.md (Fase 2 de
--  PLAN_RETROFIT_SP.md): los 5 verbos de 'rol' por procedimiento
--  almacenado, con el MISMO molde de 005_sp_sede_ejemplo.sql.
--
--  'rol' NO tiene columna 'activo' -> 'sp_eliminar_rol' es un DELETE
--  físico y 'sp_listar_rol' devuelve TODAS las filas. Un rol asignado en
--  rol_por_usuario hace fallar el DELETE por FK, igual que antes.
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  LISTAR — todas las filas (no hay 'activo'), con límite OPCIONAL.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_listar_rol(
    IN    p_limite      integer DEFAULT NULL,
    INOUT p_resultado   json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(r), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_rol, nombre, descripcion
              FROM rol
             ORDER BY id_rol
             LIMIT p_limite) r;
END;
$$;

-- --------------------------------------------------------------------------
--  CONSULTAR — una fila por id. NULL en p_resultado = no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_consultar_rol(
    IN    p_id_rol      character varying(20),
    INOUT p_resultado   json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(r) INTO p_resultado
      FROM (SELECT id_rol, nombre, descripcion
              FROM rol
             WHERE id_rol = p_id_rol) r;
END;
$$;

-- --------------------------------------------------------------------------
--  CREAR — devuelve la fila como quedó guardada. Id repetido o un CHECK
--  violado -> NpgsqlException (el controlador la traduce, criterio 7).
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_crear_rol(
    IN    p_id_rol      character varying(20),
    IN    p_nombre      character varying(60),
    IN    p_descripcion character varying(200) DEFAULT NULL,
    INOUT p_resultado   json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    INSERT INTO rol (id_rol, nombre, descripcion)
    VALUES (p_id_rol, p_nombre, p_descripcion);

    SELECT row_to_json(r) INTO p_resultado
      FROM (SELECT id_rol, nombre, descripcion
              FROM rol
             WHERE id_rol = p_id_rol) r;
END;
$$;

-- --------------------------------------------------------------------------
--  ACTUALIZAR — reemplazo COMPLETO (PUT directo; PATCH llega ya mezclado
--  desde el repositorio). Sin FOUND -> p_resultado NULL = no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_rol(
    IN    p_id_rol      character varying(20),
    IN    p_nombre      character varying(60),
    IN    p_descripcion character varying(200),
    INOUT p_resultado   json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE rol
       SET nombre      = p_nombre,
           descripcion = p_descripcion
     WHERE id_rol = p_id_rol;

    IF FOUND THEN
        SELECT row_to_json(r) INTO p_resultado
          FROM (SELECT id_rol, nombre, descripcion
                  FROM rol
                 WHERE id_rol = p_id_rol) r;
    END IF;
END;
$$;

-- --------------------------------------------------------------------------
--  ELIMINAR — FÍSICO (la tabla NO tiene 'activo'). FK en uso -> excepción.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_rol(
    IN    p_id_rol      character varying(20),
    INOUT p_resultado   json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    DELETE FROM rol
     WHERE id_rol = p_id_rol;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_rol IS
    'Retrofit criterio 3 (VERSION_2.md §4). Todas las filas, con límite opcional.';
COMMENT ON PROCEDURE catedras.sp_consultar_rol IS
    'Retrofit criterio 3. NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_rol IS
    'Retrofit criterio 3. Devuelve la fila como quedó guardada.';
COMMENT ON PROCEDURE catedras.sp_actualizar_rol IS
    'Retrofit criterio 3. Reemplazo completo; PATCH mezcla antes de llamar.';
COMMENT ON PROCEDURE catedras.sp_eliminar_rol IS
    'Retrofit criterio 3. Borrado FÍSICO (DELETE): la tabla no tiene activo.';


-- ----- migracion 020_sp_tipo_documento.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/020_sp_tipo_documento.sql
--  Proyecto Cátedras · RETROFIT criterio 3 de VERSION_2.md (Fase 2 de
--  PLAN_RETROFIT_SP.md): los 5 verbos de 'tipo_documento' por procedimiento
--  almacenado, con el MISMO molde de 005_sp_sede_ejemplo.sql.
--
--  'tipo_documento' SÍ tiene columna 'activo' -> borrado LÓGICO y listar
--  solo activos, igual que sede.
--
--  AJUSTE respecto a sede: 'sp_crear_tipo_documento' recibe 'p_activo'
--  (el POST lo admite y el INSERT anterior lo guardaba).
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  LISTAR — solo activos, con límite OPCIONAL (NULL = sin límite).
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_listar_tipo_documento(
    IN    p_limite            integer DEFAULT NULL,
    INOUT p_resultado         json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(t), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_tipo_documento, nombre, activo
              FROM tipo_documento
             WHERE activo
             ORDER BY id_tipo_documento
             LIMIT p_limite) t;
END;
$$;

-- --------------------------------------------------------------------------
--  CONSULTAR — una fila por id. NULL en p_resultado = no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_consultar_tipo_documento(
    IN    p_id_tipo_documento character varying(5),
    INOUT p_resultado         json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(t) INTO p_resultado
      FROM (SELECT id_tipo_documento, nombre, activo
              FROM tipo_documento
             WHERE id_tipo_documento = p_id_tipo_documento) t;
END;
$$;

-- --------------------------------------------------------------------------
--  CREAR — devuelve la fila como quedó guardada. Id repetido o un CHECK
--  violado -> NpgsqlException (el controlador la traduce, criterio 7).
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_crear_tipo_documento(
    IN    p_id_tipo_documento character varying(5),
    IN    p_nombre            character varying(60),
    IN    p_activo            boolean DEFAULT true,
    INOUT p_resultado         json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    INSERT INTO tipo_documento (id_tipo_documento, nombre, activo)
    VALUES (p_id_tipo_documento, p_nombre, p_activo);

    SELECT row_to_json(t) INTO p_resultado
      FROM (SELECT id_tipo_documento, nombre, activo
              FROM tipo_documento
             WHERE id_tipo_documento = p_id_tipo_documento) t;
END;
$$;

-- --------------------------------------------------------------------------
--  ACTUALIZAR — reemplazo COMPLETO (PUT directo; PATCH llega ya mezclado
--  desde el repositorio). Sin FOUND -> p_resultado NULL = no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_tipo_documento(
    IN    p_id_tipo_documento character varying(5),
    IN    p_nombre            character varying(60),
    IN    p_activo            boolean,
    INOUT p_resultado         json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE tipo_documento
       SET nombre = p_nombre,
           activo = p_activo
     WHERE id_tipo_documento = p_id_tipo_documento;

    IF FOUND THEN
        SELECT row_to_json(t) INTO p_resultado
          FROM (SELECT id_tipo_documento, nombre, activo
                  FROM tipo_documento
                 WHERE id_tipo_documento = p_id_tipo_documento) t;
    END IF;
END;
$$;

-- --------------------------------------------------------------------------
--  ELIMINAR — LÓGICO (la tabla tiene 'activo'). Ya inactivo -> no cuenta.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_tipo_documento(
    IN    p_id_tipo_documento character varying(5),
    INOUT p_resultado         json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE tipo_documento
       SET activo = false
     WHERE id_tipo_documento = p_id_tipo_documento
       AND activo;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_tipo_documento IS
    'Retrofit criterio 3 (VERSION_2.md §4). Solo activos, con límite opcional.';
COMMENT ON PROCEDURE catedras.sp_consultar_tipo_documento IS
    'Retrofit criterio 3. NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_tipo_documento IS
    'Retrofit criterio 3. Devuelve la fila como quedó guardada.';
COMMENT ON PROCEDURE catedras.sp_actualizar_tipo_documento IS
    'Retrofit criterio 3. Reemplazo completo; PATCH mezcla antes de llamar.';
COMMENT ON PROCEDURE catedras.sp_eliminar_tipo_documento IS
    'Retrofit criterio 3. Borrado LÓGICO (activo=false): la tabla tiene activo.';


-- ----- migracion 021_sp_tipo_evento.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/021_sp_tipo_evento.sql
--  Proyecto Cátedras · RETROFIT criterio 3 de VERSION_2.md (Fase 2 de
--  PLAN_RETROFIT_SP.md): los 5 verbos de 'tipo_evento' por procedimiento
--  almacenado, con el MISMO molde de 005_sp_sede_ejemplo.sql.
--
--  'tipo_evento' SÍ tiene columna 'activo' -> borrado LÓGICO y listar
--  solo activos, igual que sede.
--
--  AJUSTES respecto a sede: 'sp_crear_tipo_evento' recibe 'p_activo'
--  (el POST lo admite); la pk es character(4), no varchar.
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  LISTAR — solo activos, con límite OPCIONAL (NULL = sin límite).
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_listar_tipo_evento(
    IN    p_limite         integer DEFAULT NULL,
    INOUT p_resultado      json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(t), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_tipo_evento, nombre, activo
              FROM tipo_evento
             WHERE activo
             ORDER BY id_tipo_evento
             LIMIT p_limite) t;
END;
$$;

-- --------------------------------------------------------------------------
--  CONSULTAR — una fila por id. NULL en p_resultado = no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_consultar_tipo_evento(
    IN    p_id_tipo_evento character(4),
    INOUT p_resultado      json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(t) INTO p_resultado
      FROM (SELECT id_tipo_evento, nombre, activo
              FROM tipo_evento
             WHERE id_tipo_evento = p_id_tipo_evento) t;
END;
$$;

-- --------------------------------------------------------------------------
--  CREAR — devuelve la fila como quedó guardada. Id repetido o un CHECK
--  violado -> NpgsqlException (el controlador la traduce, criterio 7).
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_crear_tipo_evento(
    IN    p_id_tipo_evento character(4),
    IN    p_nombre         character varying(80),
    IN    p_activo         boolean DEFAULT true,
    INOUT p_resultado      json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    INSERT INTO tipo_evento (id_tipo_evento, nombre, activo)
    VALUES (p_id_tipo_evento, p_nombre, p_activo);

    SELECT row_to_json(t) INTO p_resultado
      FROM (SELECT id_tipo_evento, nombre, activo
              FROM tipo_evento
             WHERE id_tipo_evento = p_id_tipo_evento) t;
END;
$$;

-- --------------------------------------------------------------------------
--  ACTUALIZAR — reemplazo COMPLETO (PUT directo; PATCH llega ya mezclado
--  desde el repositorio). Sin FOUND -> p_resultado NULL = no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_tipo_evento(
    IN    p_id_tipo_evento character(4),
    IN    p_nombre         character varying(80),
    IN    p_activo         boolean,
    INOUT p_resultado      json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE tipo_evento
       SET nombre = p_nombre,
           activo = p_activo
     WHERE id_tipo_evento = p_id_tipo_evento;

    IF FOUND THEN
        SELECT row_to_json(t) INTO p_resultado
          FROM (SELECT id_tipo_evento, nombre, activo
                  FROM tipo_evento
                 WHERE id_tipo_evento = p_id_tipo_evento) t;
    END IF;
END;
$$;

-- --------------------------------------------------------------------------
--  ELIMINAR — LÓGICO (la tabla tiene 'activo'). Ya inactivo -> no cuenta.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_tipo_evento(
    IN    p_id_tipo_evento character(4),
    INOUT p_resultado      json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE tipo_evento
       SET activo = false
     WHERE id_tipo_evento = p_id_tipo_evento
       AND activo;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_tipo_evento IS
    'Retrofit criterio 3 (VERSION_2.md §4). Solo activos, con límite opcional.';
COMMENT ON PROCEDURE catedras.sp_consultar_tipo_evento IS
    'Retrofit criterio 3. NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_tipo_evento IS
    'Retrofit criterio 3. Devuelve la fila como quedó guardada.';
COMMENT ON PROCEDURE catedras.sp_actualizar_tipo_evento IS
    'Retrofit criterio 3. Reemplazo completo; PATCH mezcla antes de llamar.';
COMMENT ON PROCEDURE catedras.sp_eliminar_tipo_evento IS
    'Retrofit criterio 3. Borrado LÓGICO (activo=false): la tabla tiene activo.';


-- ----- migracion 022_sp_tipo_pregunta.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/022_sp_tipo_pregunta.sql
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


-- ----- migracion 023_sp_tipo_vinculacion.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/023_sp_tipo_vinculacion.sql
--  Proyecto Cátedras · RETROFIT criterio 3 de VERSION_2.md (Fase 2 de
--  PLAN_RETROFIT_SP.md): los 5 verbos de 'tipo_vinculacion' por procedimiento
--  almacenado, con el MISMO molde de 005_sp_sede_ejemplo.sql.
--
--  'tipo_vinculacion' SÍ tiene columna 'activo' -> borrado LÓGICO y listar
--  solo activos, igual que sede.
--
--  AJUSTE respecto a sede: 'sp_crear_tipo_vinculacion' recibe 'p_activo'
--  (el POST lo admite y el INSERT anterior lo guardaba).
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  LISTAR — solo activos, con límite OPCIONAL (NULL = sin límite).
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_listar_tipo_vinculacion(
    IN    p_limite              integer DEFAULT NULL,
    INOUT p_resultado           json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(t), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_tipo_vinculacion, nombre, es_interno, exige_programa, activo
              FROM tipo_vinculacion
             WHERE activo
             ORDER BY id_tipo_vinculacion
             LIMIT p_limite) t;
END;
$$;

-- --------------------------------------------------------------------------
--  CONSULTAR — una fila por id. NULL en p_resultado = no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_consultar_tipo_vinculacion(
    IN    p_id_tipo_vinculacion character varying(15),
    INOUT p_resultado           json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(t) INTO p_resultado
      FROM (SELECT id_tipo_vinculacion, nombre, es_interno, exige_programa, activo
              FROM tipo_vinculacion
             WHERE id_tipo_vinculacion = p_id_tipo_vinculacion) t;
END;
$$;

-- --------------------------------------------------------------------------
--  CREAR — devuelve la fila como quedó guardada. Id repetido o un CHECK
--  violado -> NpgsqlException (el controlador la traduce, criterio 7).
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_crear_tipo_vinculacion(
    IN    p_id_tipo_vinculacion character varying(15),
    IN    p_nombre              character varying(60),
    IN    p_es_interno          boolean,
    IN    p_exige_programa      boolean DEFAULT true,
    IN    p_activo              boolean DEFAULT true,
    INOUT p_resultado           json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    INSERT INTO tipo_vinculacion (id_tipo_vinculacion, nombre, es_interno, exige_programa, activo)
    VALUES (p_id_tipo_vinculacion, p_nombre, p_es_interno, p_exige_programa, p_activo);

    SELECT row_to_json(t) INTO p_resultado
      FROM (SELECT id_tipo_vinculacion, nombre, es_interno, exige_programa, activo
              FROM tipo_vinculacion
             WHERE id_tipo_vinculacion = p_id_tipo_vinculacion) t;
END;
$$;

-- --------------------------------------------------------------------------
--  ACTUALIZAR — reemplazo COMPLETO (PUT directo; PATCH llega ya mezclado
--  desde el repositorio). Sin FOUND -> p_resultado NULL = no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_tipo_vinculacion(
    IN    p_id_tipo_vinculacion character varying(15),
    IN    p_nombre              character varying(60),
    IN    p_es_interno          boolean,
    IN    p_exige_programa      boolean,
    IN    p_activo              boolean,
    INOUT p_resultado           json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE tipo_vinculacion
       SET nombre         = p_nombre,
           es_interno     = p_es_interno,
           exige_programa = p_exige_programa,
           activo         = p_activo
     WHERE id_tipo_vinculacion = p_id_tipo_vinculacion;

    IF FOUND THEN
        SELECT row_to_json(t) INTO p_resultado
          FROM (SELECT id_tipo_vinculacion, nombre, es_interno, exige_programa, activo
                  FROM tipo_vinculacion
                 WHERE id_tipo_vinculacion = p_id_tipo_vinculacion) t;
    END IF;
END;
$$;

-- --------------------------------------------------------------------------
--  ELIMINAR — LÓGICO (la tabla tiene 'activo'). Ya inactivo -> no cuenta.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_tipo_vinculacion(
    IN    p_id_tipo_vinculacion character varying(15),
    INOUT p_resultado           json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE tipo_vinculacion
       SET activo = false
     WHERE id_tipo_vinculacion = p_id_tipo_vinculacion
       AND activo;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_tipo_vinculacion IS
    'Retrofit criterio 3 (VERSION_2.md §4). Solo activos, con límite opcional.';
COMMENT ON PROCEDURE catedras.sp_consultar_tipo_vinculacion IS
    'Retrofit criterio 3. NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_tipo_vinculacion IS
    'Retrofit criterio 3. Devuelve la fila como quedó guardada.';
COMMENT ON PROCEDURE catedras.sp_actualizar_tipo_vinculacion IS
    'Retrofit criterio 3. Reemplazo completo; PATCH mezcla antes de llamar.';
COMMENT ON PROCEDURE catedras.sp_eliminar_tipo_vinculacion IS
    'Retrofit criterio 3. Borrado LÓGICO (activo=false): la tabla tiene activo.';


-- ----- migracion 024_sp_asistente.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/024_sp_asistente.sql
--  Proyecto Cátedras · RETROFIT criterio 3 (PLAN_RETROFIT_SP.md, Fase 4)
--  Tabla: 'asistente'. Molde: 005_sp_sede_ejemplo.sql.
-- --------------------------------------------------------------------------
--  AJUSTES AL MOLDE DE 'sede' (y por qué):
--   1. NO hay 'sp_crear_asistente': el CREAR ya es 'sp_guardar_asistente'
--      (migración 002, maestro + 4 detalles en una transacción). No se
--      reemplaza ni se duplica — un segundo procedimiento de alta sería
--      una puerta para crear asistentes SIN sus detalles por otro camino.
--   2. 'sp_listar_asistente' recibe además la búsqueda 'p_q' y el
--      desplazamiento (paginación), porque así lo pide el contrato de la v3
--      (ADR D18): el filtro corre en SQL, no en memoria. El patrón ILIKE
--      ('%texto%') ahora lo arma el procedimiento, no el repositorio.
--   3. Listar devuelve SOLO activos (igual que 'sede'): ahora que eliminar
--      es lógico, un asistente "eliminado" no debe volver a aparecer en la
--      lista. Consultar por id SÍ lo devuelve aunque esté inactivo.
--   4. Los correos entran como 'character varying(254)' (igual que en
--      'sp_guardar_asistente'): 'text -> citext' no es un cast implícito y
--      Npgsql manda los string como 'text'; al asignarlos a la columna
--      CITEXT, PostgreSQL hace la conversión de asignación sin problema.
--   5. ELIMINAR pasa de DELETE físico (con RESTRICT de usuario/ponencia/
--      registro_asistencia -> 500 y CASCADE de los 4 detalles) a LÓGICO
--      (activo = false): la tabla tiene 'activo' (regla 3 del retrofit).
--      Consecuencia: ya no se borran en cascada sus documentos,
--      vinculaciones, programas ni consentimientos — quedan como historia.
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  LISTAR — activos, con búsqueda opcional y paginación.
--  p_q NULL o en blanco = sin filtro. p_limite NULL = sin límite.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_listar_asistente(
    IN    p_q              text    DEFAULT NULL,
    IN    p_limite         integer DEFAULT NULL,
    IN    p_desplazamiento integer DEFAULT 0,
    INOUT p_resultado      json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    -- NULLIF(btrim(...), '') convierte '' y '   ' en NULL: "sin búsqueda".
    v_patron text := '%' || NULLIF(btrim(p_q), '') || '%';
BEGIN
    SELECT COALESCE(json_agg(a), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_asistente, id_asis, nombres, apellidos, nombre_completo_asis,
                   correo_institucional, correo_personal, celular,
                   es_externo, activo, creado_en, fk_lote_carga
              FROM asistente
             WHERE activo
               AND (v_patron IS NULL
                    OR (nombres || ' ' || apellidos) ILIKE v_patron
                    OR id_asis ILIKE v_patron)
             ORDER BY id_asistente
             LIMIT p_limite OFFSET COALESCE(p_desplazamiento, 0)) a;
END;
$$;

-- --------------------------------------------------------------------------
--  CONSULTAR — por id. NULL en p_resultado = no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_consultar_asistente(
    IN    p_id_asistente bigint,
    INOUT p_resultado    json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(a) INTO p_resultado
      FROM (SELECT id_asistente, id_asis, nombres, apellidos, nombre_completo_asis,
                   correo_institucional, correo_personal, celular,
                   es_externo, activo, creado_en, fk_lote_carga
              FROM asistente
             WHERE id_asistente = p_id_asistente) a;
END;
$$;

-- --------------------------------------------------------------------------
--  CREAR — no se define aquí: es 'sp_guardar_asistente' (migración 002).
-- --------------------------------------------------------------------------

-- --------------------------------------------------------------------------
--  ACTUALIZAR — reemplazo COMPLETO de las 9 columnas de negocio. NO toca
--  'id_asistente', 'creado_en' ni 'fk_lote_carga' (la historia de cómo
--  nació la fila — misma lista blanca que tenía el repositorio en la v3).
--  Los CHECK (chk_asistente_externo, chk_asistente_correo...) siguen
--  siendo la última muralla: ahora se evalúan sobre la fila YA mezclada.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_asistente(
    IN    p_id_asistente         bigint,
    IN    p_id_asis              character varying(15),
    IN    p_nombres              character varying(100),
    IN    p_apellidos            character varying(100),
    IN    p_nombre_completo_asis character varying(200),
    IN    p_correo_inst          character varying(254),
    IN    p_correo_pers          character varying(254),
    IN    p_celular              character varying(20),
    IN    p_es_externo           boolean,
    IN    p_activo               boolean,
    INOUT p_resultado            json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE asistente
       SET id_asis              = p_id_asis,
           nombres              = p_nombres,
           apellidos            = p_apellidos,
           nombre_completo_asis = p_nombre_completo_asis,
           correo_institucional = p_correo_inst,
           correo_personal      = p_correo_pers,
           celular              = p_celular,
           es_externo           = p_es_externo,
           activo               = p_activo
     WHERE id_asistente = p_id_asistente;

    IF FOUND THEN
        CALL catedras.sp_consultar_asistente(p_id_asistente, p_resultado);
    END IF;
END;
$$;

-- --------------------------------------------------------------------------
--  ELIMINAR — LÓGICO (la tabla tiene 'activo'). Ya inactivo = "no
--  encontrado" (no hay nada que retirar), igual criterio que 'sede'.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_asistente(
    IN    p_id_asistente bigint,
    INOUT p_resultado    json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE asistente
       SET activo = false
     WHERE id_asistente = p_id_asistente
       AND activo;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_asistente IS
    'Retrofit criterio 3. Solo activos; búsqueda ILIKE opcional y paginación (ADR D18).';
COMMENT ON PROCEDURE catedras.sp_consultar_asistente IS
    'Retrofit criterio 3. NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_actualizar_asistente IS
    'Retrofit criterio 3. Reemplazo completo; PATCH mezcla antes de llamar. El CREAR es sp_guardar_asistente (002).';
COMMENT ON PROCEDURE catedras.sp_eliminar_asistente IS
    'Retrofit criterio 3. Borrado LÓGICO (activo=false).';


-- ----- migracion 025_sp_documento_asistente.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/025_sp_documento_asistente.sql
--  Proyecto Cátedras · RETROFIT criterio 3 (PLAN_RETROFIT_SP.md, Fase 4)
--  Tabla: 'documento_asistente'. Molde: 005_sp_sede_ejemplo.sql.
-- --------------------------------------------------------------------------
--  AJUSTES AL MOLDE DE 'sede':
--   1. 'sp_listar_' recibe el filtro opcional 'p_fk_asistente' (contrato v3:
--      GET /api/DocumentoAsistente?fkAsistente=). Sin filtro = todos.
--      Sin "WHERE activo": la tabla no tiene esa columna.
--   2. La clave la genera la BD (IDENTITY), no el cliente: 'sp_crear_'
--      guarda el id en 'v_id' (RETURNING ... INTO) para devolver la fila.
--   3. 'sp_eliminar_' es DELETE FÍSICO: la tabla NO tiene 'activo'.
--      'vigente' NO es un 'activo' disfrazado: es "cuál de sus documentos
--      es el actual" (índice único parcial ux_documento_vigente), y se
--      cambia con PATCH {"vigente": false}, como hasta hoy.
--   4. 'sp_actualizar_' no recibe 'fk_asistente': un documento no cambia
--      de dueño (misma lista blanca que tenía el repositorio en la v3).
-- ==========================================================================

SET search_path TO catedras, public;

CREATE OR REPLACE PROCEDURE catedras.sp_listar_documento_asistente(
    IN    p_fk_asistente bigint  DEFAULT NULL,
    IN    p_limite       integer DEFAULT NULL,
    INOUT p_resultado    json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(d), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_documento, fk_asistente, fk_tipo_documento, numero, vigente, registrado_en
              FROM documento_asistente
             WHERE p_fk_asistente IS NULL OR fk_asistente = p_fk_asistente
             ORDER BY id_documento
             LIMIT p_limite) d;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_consultar_documento_asistente(
    IN    p_id_documento bigint,
    INOUT p_resultado    json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(d) INTO p_resultado
      FROM (SELECT id_documento, fk_asistente, fk_tipo_documento, numero, vigente, registrado_en
              FROM documento_asistente
             WHERE id_documento = p_id_documento) d;
END;
$$;

-- CREAR — devuelve la fila con 'id_documento' y 'registrado_en' que puso la
-- BD. Segundo vigente (ux_documento_vigente), número repetido por tipo
-- (uq_documento_numero) o FK inexistente -> excepción del motor (500).
CREATE OR REPLACE PROCEDURE catedras.sp_crear_documento_asistente(
    IN    p_fk_asistente      bigint,
    IN    p_fk_tipo_documento character varying(5),
    IN    p_numero            character varying(20),
    IN    p_vigente           boolean DEFAULT true,
    INOUT p_resultado         json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_id bigint;
BEGIN
    INSERT INTO documento_asistente (fk_asistente, fk_tipo_documento, numero, vigente)
    VALUES (p_fk_asistente, p_fk_tipo_documento, p_numero, COALESCE(p_vigente, true))
    RETURNING id_documento INTO v_id;

    CALL catedras.sp_consultar_documento_asistente(v_id, p_resultado);
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_documento_asistente(
    IN    p_id_documento      bigint,
    IN    p_fk_tipo_documento character varying(5),
    IN    p_numero            character varying(20),
    IN    p_vigente           boolean,
    INOUT p_resultado         json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE documento_asistente
       SET fk_tipo_documento = p_fk_tipo_documento,
           numero            = p_numero,
           vigente           = p_vigente
     WHERE id_documento = p_id_documento;

    IF FOUND THEN
        CALL catedras.sp_consultar_documento_asistente(p_id_documento, p_resultado);
    END IF;
END;
$$;

-- ELIMINAR — FÍSICO (sin 'activo'). Ninguna tabla referencia a
-- documento_asistente, así que nunca lo bloquea una FK (RF105).
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_documento_asistente(
    IN    p_id_documento bigint,
    INOUT p_resultado    json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    DELETE FROM documento_asistente WHERE id_documento = p_id_documento;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_documento_asistente IS
    'Retrofit criterio 3. Filtro opcional por asistente; sin columna activo.';
COMMENT ON PROCEDURE catedras.sp_consultar_documento_asistente IS
    'Retrofit criterio 3. NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_documento_asistente IS
    'Retrofit criterio 3. Devuelve la fila con el id y la fecha que puso la BD.';
COMMENT ON PROCEDURE catedras.sp_actualizar_documento_asistente IS
    'Retrofit criterio 3. Reemplazo completo (sin fk_asistente); PATCH mezcla antes de llamar.';
COMMENT ON PROCEDURE catedras.sp_eliminar_documento_asistente IS
    'Retrofit criterio 3. Borrado FÍSICO: la tabla no tiene activo.';


-- ----- migracion 026_sp_vinculacion_asistente.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/026_sp_vinculacion_asistente.sql
--  Proyecto Cátedras · RETROFIT criterio 3 (PLAN_RETROFIT_SP.md, Fase 4)
--  Tabla: 'vinculacion_asistente' — LLAVE COMPUESTA (ADR D16).
-- --------------------------------------------------------------------------
--  AJUSTES AL MOLDE DE 'sede':
--   1. LLAVE DE 3 COLUMNAS (fk_asistente, fk_tipo_vinculacion, fecha_ini):
--      consultar/actualizar/eliminar reciben las 3 en vez de un 'p_id'.
--   2. ELIMINAR = CERRAR. La tabla no tiene 'activo', así que la regla 3
--      del retrofit diría DELETE físico — pero esta tabla es una HISTORIA:
--      desde la v3 una vinculación se abre y se CIERRA (fecha_fin), nunca
--      se borra (ADR D16, RF109). 'fecha_fin IS NULL' cumple aquí el papel
--      de 'activo': 'sp_eliminar_' pone 'fecha_fin' (por defecto hoy) solo
--      si seguía abierta. Es exactamente el UPDATE que hacía
--      'RepositorioVinculacionAsistentePostgres.CerrarAsync', y es lo que
--      ese método llama ahora. No existe (ni se inventa) un DELETE.
--      Por eso 'sp_eliminar_' recibe un 4.º parámetro, 'p_fecha_fin'.
--   3. 'sp_actualizar_' reemplaza la ÚNICA columna que no es llave
--      ('fecha_fin', NULL incluido = reabrir). Existe para cumplir "los 5
--      verbos", pero la API NO lo expone: IRepositorioVinculacionAsistente
--      no tiene ActualizarAsync y no se le agrega (ADR D16 lo prohíbe).
--   4. 'fecha_ini'/'fecha_fin' son 'date'. Npgsql manda los DateOnly como
--      'timestamp' (ver ManejadorTipoDateOnly), y 'timestamp -> date' NO es
--      un cast implícito: un CALL con ese argumento no encontraría el
--      procedimiento. El repositorio escribe 'CAST(@fechaIni AS date)'.
-- ==========================================================================

SET search_path TO catedras, public;

CREATE OR REPLACE PROCEDURE catedras.sp_listar_vinculacion_asistente(
    IN    p_fk_asistente bigint  DEFAULT NULL,
    IN    p_limite       integer DEFAULT NULL,
    INOUT p_resultado    json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    -- Orden de la v3: por asistente y, dentro de cada uno, la más reciente primero.
    SELECT COALESCE(json_agg(v), '[]'::json)
      INTO p_resultado
      FROM (SELECT fk_asistente, fk_tipo_vinculacion, fecha_ini, fecha_fin
              FROM vinculacion_asistente
             WHERE p_fk_asistente IS NULL OR fk_asistente = p_fk_asistente
             ORDER BY fk_asistente, fecha_ini DESC, fk_tipo_vinculacion
             LIMIT p_limite) v;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_consultar_vinculacion_asistente(
    IN    p_fk_asistente        bigint,
    IN    p_fk_tipo_vinculacion character varying(15),
    IN    p_fecha_ini           date,
    INOUT p_resultado           json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(v) INTO p_resultado
      FROM (SELECT fk_asistente, fk_tipo_vinculacion, fecha_ini, fecha_fin
              FROM vinculacion_asistente
             WHERE fk_asistente        = p_fk_asistente
               AND fk_tipo_vinculacion = p_fk_tipo_vinculacion
               AND fecha_ini           = p_fecha_ini) v;
END;
$$;

-- CREAR (abrir) — nace sin 'fecha_fin'. Sin fecha_ini -> hoy (igual que
-- sp_guardar_asistente). Llave repetida / FK inexistente -> excepción (500).
CREATE OR REPLACE PROCEDURE catedras.sp_crear_vinculacion_asistente(
    IN    p_fk_asistente        bigint,
    IN    p_fk_tipo_vinculacion character varying(15),
    IN    p_fecha_ini           date DEFAULT NULL,
    INOUT p_resultado           json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_fecha_ini date := COALESCE(p_fecha_ini, CURRENT_DATE);
BEGIN
    INSERT INTO vinculacion_asistente (fk_asistente, fk_tipo_vinculacion, fecha_ini)
    VALUES (p_fk_asistente, p_fk_tipo_vinculacion, v_fecha_ini);

    CALL catedras.sp_consultar_vinculacion_asistente(
        p_fk_asistente, p_fk_tipo_vinculacion, v_fecha_ini, p_resultado);
END;
$$;

-- ACTUALIZAR — reemplaza 'fecha_fin' (la única columna que no es llave).
-- No expuesto por la API (ADR D16). chk_vinculacion_fechas sigue mandando.
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_vinculacion_asistente(
    IN    p_fk_asistente        bigint,
    IN    p_fk_tipo_vinculacion character varying(15),
    IN    p_fecha_ini           date,
    IN    p_fecha_fin           date,
    INOUT p_resultado           json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE vinculacion_asistente
       SET fecha_fin = p_fecha_fin
     WHERE fk_asistente        = p_fk_asistente
       AND fk_tipo_vinculacion = p_fk_tipo_vinculacion
       AND fecha_ini           = p_fecha_ini;

    IF FOUND THEN
        CALL catedras.sp_consultar_vinculacion_asistente(
            p_fk_asistente, p_fk_tipo_vinculacion, p_fecha_ini, p_resultado);
    END IF;
END;
$$;

-- ELIMINAR = CERRAR (ver ajuste 2). 'AND fecha_fin IS NULL' es la defensa
-- del motor: una vinculación ya cerrada no se "re-cierra" (eliminado=false).
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_vinculacion_asistente(
    IN    p_fk_asistente        bigint,
    IN    p_fk_tipo_vinculacion character varying(15),
    IN    p_fecha_ini           date,
    IN    p_fecha_fin           date DEFAULT NULL,
    INOUT p_resultado           json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE vinculacion_asistente
       SET fecha_fin = COALESCE(p_fecha_fin, CURRENT_DATE)
     WHERE fk_asistente        = p_fk_asistente
       AND fk_tipo_vinculacion = p_fk_tipo_vinculacion
       AND fecha_ini           = p_fecha_ini
       AND fecha_fin IS NULL;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_vinculacion_asistente IS
    'Retrofit criterio 3. Filtro opcional por asistente.';
COMMENT ON PROCEDURE catedras.sp_consultar_vinculacion_asistente IS
    'Retrofit criterio 3. Llave compuesta (3 columnas). NULL = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_vinculacion_asistente IS
    'Retrofit criterio 3. Abre una vinculación (fecha_fin NULL).';
COMMENT ON PROCEDURE catedras.sp_actualizar_vinculacion_asistente IS
    'Retrofit criterio 3. Reemplaza fecha_fin. NO expuesto por la API (ADR D16).';
COMMENT ON PROCEDURE catedras.sp_eliminar_vinculacion_asistente IS
    'Retrofit criterio 3. ELIMINAR = CERRAR (fecha_fin), nunca DELETE: la tabla es una historia (ADR D16).';


-- ----- migracion 027_sp_programa_asistente.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/027_sp_programa_asistente.sql
--  Proyecto Cátedras · RETROFIT criterio 3 (PLAN_RETROFIT_SP.md, Fase 4)
--  Tabla: 'programa_asistente' — LLAVE COMPUESTA (ADR D16).
-- --------------------------------------------------------------------------
--  AJUSTES AL MOLDE DE 'sede':
--   1. LLAVE DE 3 COLUMNAS (fk_asistente, fk_programa, fk_periodo).
--      'fk_periodo' es CHAR(6): 'text -> bpchar' SÍ es implícito, así que
--      el string que manda Npgsql encaja sin CAST.
--   2. ACTUALIZAR = CAMBIAR ESTADO. 'estado' es la única columna que no es
--      llave, así que "reemplazo completo" y "cambiar estado" son el mismo
--      UPDATE. 'RepositorioProgramaAsistentePostgres.CambiarEstadoAsync'
--      llama a este procedimiento (consulta + mezcla + llama, igual que
--      ActualizarAsync de sede — aquí la mezcla es trivial).
--   3. ELIMINAR = RETIRO LÓGICO, NO un DELETE. Sin columna 'activo' la
--      regla 3 diría "físico", pero esta tabla, igual que
--      vinculacion_asistente, se cierra en vez de borrarse (ADR D16): el
--      equivalente de "activo = false" es 'estado = RETIRADO' (uno de los 4
--      valores del CHECK). Ya RETIRADO -> eliminado=false.
--      ⚠ DECISIÓN A CONFIRMAR: la API NO lo expone (IRepositorioProgramaAsistente
--      no tiene EliminarAsync y no se le agrega); retirar sigue siendo
--      PATCH /cambiar-estado {"estado":"RETIRADO"}. Este procedimiento
--      existe para cumplir "los 5 verbos" sin inventar un borrado.
-- ==========================================================================

SET search_path TO catedras, public;

CREATE OR REPLACE PROCEDURE catedras.sp_listar_programa_asistente(
    IN    p_fk_asistente bigint  DEFAULT NULL,
    IN    p_limite       integer DEFAULT NULL,
    INOUT p_resultado    json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(p), '[]'::json)
      INTO p_resultado
      FROM (SELECT fk_asistente, fk_programa, fk_periodo, estado
              FROM programa_asistente
             WHERE p_fk_asistente IS NULL OR fk_asistente = p_fk_asistente
             ORDER BY fk_asistente, fk_periodo DESC, fk_programa
             LIMIT p_limite) p;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_consultar_programa_asistente(
    IN    p_fk_asistente bigint,
    IN    p_fk_programa  character varying(5),
    IN    p_fk_periodo   character(6),
    INOUT p_resultado    json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(p) INTO p_resultado
      FROM (SELECT fk_asistente, fk_programa, fk_periodo, estado
              FROM programa_asistente
             WHERE fk_asistente = p_fk_asistente
               AND fk_programa  = p_fk_programa
               AND fk_periodo   = p_fk_periodo) p;
END;
$$;

-- CREAR — sin estado -> 'ACTIVO' (DEFAULT de la columna). Llave repetida
-- (RF111) o programa/periodo/asistente inexistente -> excepción (500).
CREATE OR REPLACE PROCEDURE catedras.sp_crear_programa_asistente(
    IN    p_fk_asistente bigint,
    IN    p_fk_programa  character varying(5),
    IN    p_fk_periodo   character(6),
    IN    p_estado       character varying(15) DEFAULT 'ACTIVO',
    INOUT p_resultado    json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    INSERT INTO programa_asistente (fk_asistente, fk_programa, fk_periodo, estado)
    VALUES (p_fk_asistente, p_fk_programa, p_fk_periodo, COALESCE(p_estado, 'ACTIVO'));

    CALL catedras.sp_consultar_programa_asistente(
        p_fk_asistente, p_fk_programa, p_fk_periodo, p_resultado);
END;
$$;

-- ACTUALIZAR = CAMBIAR ESTADO (ver ajuste 2). Estado fuera de la lista ->
-- CHECK chk_programa_asistente_estado (la petición ya lo filtra con 422).
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_programa_asistente(
    IN    p_fk_asistente bigint,
    IN    p_fk_programa  character varying(5),
    IN    p_fk_periodo   character(6),
    IN    p_estado       character varying(15),
    INOUT p_resultado    json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE programa_asistente
       SET estado = p_estado
     WHERE fk_asistente = p_fk_asistente
       AND fk_programa  = p_fk_programa
       AND fk_periodo   = p_fk_periodo;

    IF FOUND THEN
        CALL catedras.sp_consultar_programa_asistente(
            p_fk_asistente, p_fk_programa, p_fk_periodo, p_resultado);
    END IF;
END;
$$;

-- ELIMINAR = RETIRO LÓGICO (ver ajuste 3). No expuesto por la API.
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_programa_asistente(
    IN    p_fk_asistente bigint,
    IN    p_fk_programa  character varying(5),
    IN    p_fk_periodo   character(6),
    INOUT p_resultado    json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE programa_asistente
       SET estado = 'RETIRADO'
     WHERE fk_asistente = p_fk_asistente
       AND fk_programa  = p_fk_programa
       AND fk_periodo   = p_fk_periodo
       AND estado <> 'RETIRADO';

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_programa_asistente IS
    'Retrofit criterio 3. Filtro opcional por asistente.';
COMMENT ON PROCEDURE catedras.sp_consultar_programa_asistente IS
    'Retrofit criterio 3. Llave compuesta (3 columnas). NULL = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_programa_asistente IS
    'Retrofit criterio 3. Matricula; estado por defecto ACTIVO.';
COMMENT ON PROCEDURE catedras.sp_actualizar_programa_asistente IS
    'Retrofit criterio 3. Reemplaza estado (= PATCH /cambiar-estado).';
COMMENT ON PROCEDURE catedras.sp_eliminar_programa_asistente IS
    'Retrofit criterio 3. ELIMINAR = retiro lógico (estado RETIRADO), nunca DELETE. NO expuesto por la API.';


-- ----- migracion 028_sp_consentimiento_datos.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/028_sp_consentimiento_datos.sql
--  Proyecto Cátedras · RETROFIT criterio 3 (PLAN_RETROFIT_SP.md, Fase 4)
--  Tabla: 'consentimiento_datos' — HECHO LEGAL (Ley 1581 de 2012, RF113).
-- --------------------------------------------------------------------------
--  ⚠ LEER ANTES DE TOCAR LA API:
--  Se construyen los 5 procedimientos para cumplir el criterio 3 al pie de
--  la letra, pero SOLO 3 están conectados: listar, consultar y crear. Ni
--  IRepositorioConsentimientoDatos, ni el servicio, ni el controlador
--  tienen Actualizar/Eliminar, y NO se les agregan: un consentimiento ya
--  registrado es evidencia, y editarlo o borrarlo por API contradice por
--  qué existe la tabla. 'sp_actualizar_' y 'sp_eliminar_' quedan en la BD
--  para un uso administrativo excepcional (p. ej. una orden de supresión
--  del titular), fuera de la API, y su exposición la decide el equipo.
--
--  AJUSTES AL MOLDE DE 'sede':
--   1. 'ip' es INET. Al LEER se devuelve 'host(ip) AS ip' ("10.0.0.1"; un
--      'ip::text' daría "10.0.0.1/32"), igual que el repositorio de la v3.
--      Al ESCRIBIR entra como texto y se convierte con '::inet' dentro del
--      procedimiento: una IP mal escrita -> excepción (500), como antes.
--   2. Clave IDENTITY -> 'v_id' en 'sp_crear_'.
--   3. 'sp_eliminar_' es DELETE físico (sin 'activo').
--   4. 'sp_actualizar_' no cambia 'fk_asistente' ni 'aceptado_en'.
-- ==========================================================================

SET search_path TO catedras, public;

CREATE OR REPLACE PROCEDURE catedras.sp_listar_consentimiento_datos(
    IN    p_fk_asistente bigint  DEFAULT NULL,
    IN    p_limite       integer DEFAULT NULL,
    INOUT p_resultado    json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(c), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_consentimiento, fk_asistente, version_politica, aceptado_en,
                   host(ip) AS ip, canal
              FROM consentimiento_datos
             WHERE p_fk_asistente IS NULL OR fk_asistente = p_fk_asistente
             ORDER BY id_consentimiento
             LIMIT p_limite) c;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_consultar_consentimiento_datos(
    IN    p_id_consentimiento bigint,
    INOUT p_resultado         json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(c) INTO p_resultado
      FROM (SELECT id_consentimiento, fk_asistente, version_politica, aceptado_en,
                   host(ip) AS ip, canal
              FROM consentimiento_datos
             WHERE id_consentimiento = p_id_consentimiento) c;
END;
$$;

-- CREAR — misma versión ya aceptada por ese asistente (uq_consentimiento_ver),
-- asistente inexistente (FK) o IP mal escrita -> excepción del motor (500).
CREATE OR REPLACE PROCEDURE catedras.sp_crear_consentimiento_datos(
    IN    p_fk_asistente     bigint,
    IN    p_version_politica character varying(20),
    IN    p_canal            character varying(20) DEFAULT 'WEB',
    IN    p_ip               character varying(45) DEFAULT NULL,
    INOUT p_resultado        json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_id bigint;
BEGIN
    INSERT INTO consentimiento_datos (fk_asistente, version_politica, canal, ip)
    VALUES (p_fk_asistente, p_version_politica, COALESCE(p_canal, 'WEB'), p_ip::inet)
    RETURNING id_consentimiento INTO v_id;

    CALL catedras.sp_consultar_consentimiento_datos(v_id, p_resultado);
END;
$$;

-- ACTUALIZAR — ⚠ NO conectado a la API (ver cabecera).
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_consentimiento_datos(
    IN    p_id_consentimiento bigint,
    IN    p_version_politica  character varying(20),
    IN    p_canal             character varying(20),
    IN    p_ip                character varying(45),
    INOUT p_resultado         json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE consentimiento_datos
       SET version_politica = p_version_politica,
           canal            = p_canal,
           ip               = p_ip::inet
     WHERE id_consentimiento = p_id_consentimiento;

    IF FOUND THEN
        CALL catedras.sp_consultar_consentimiento_datos(p_id_consentimiento, p_resultado);
    END IF;
END;
$$;

-- ELIMINAR — FÍSICO (sin 'activo'). ⚠ NO conectado a la API (ver cabecera).
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_consentimiento_datos(
    IN    p_id_consentimiento bigint,
    INOUT p_resultado         json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    DELETE FROM consentimiento_datos WHERE id_consentimiento = p_id_consentimiento;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_consentimiento_datos IS
    'Retrofit criterio 3. Filtro opcional por asistente; ip como texto (host).';
COMMENT ON PROCEDURE catedras.sp_consultar_consentimiento_datos IS
    'Retrofit criterio 3. NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_consentimiento_datos IS
    'Retrofit criterio 3. Registra el consentimiento (Ley 1581).';
COMMENT ON PROCEDURE catedras.sp_actualizar_consentimiento_datos IS
    'Retrofit criterio 3. NO expuesto por la API: el consentimiento es un hecho legal inmutable (RF113).';
COMMENT ON PROCEDURE catedras.sp_eliminar_consentimiento_datos IS
    'Retrofit criterio 3. Borrado FÍSICO. NO expuesto por la API: el consentimiento es un hecho legal inmutable (RF113).';


-- ----- migracion 029_sp_lote_carga_asistente.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/029_sp_lote_carga_asistente.sql
--  Proyecto Cátedras · RETROFIT criterio 3 (PLAN_RETROFIT_SP.md, Fase 4)
--  Tabla: 'lote_carga_asistente' (maestro de 'novedad_carga').
-- --------------------------------------------------------------------------
--  QUÉ YA EXISTÍA Y NO SE TOCA: 'sp_guardar_lote_carga' (migración 003).
--  OJO: esa migración NO es el "crear" del lote — es su CIERRE (contadores
--  finales + novedades en una transacción, ADR D15). El ABRIR (POST
--  /api/LoteCargaAsistente) era un INSERT suelto en el repositorio; ese es
--  el que pasa ahora a 'sp_crear_lote_carga_asistente'.
--
--  AJUSTES AL MOLDE DE 'sede':
--   1. Listar del más reciente al más antiguo (ORDER BY id DESC), como en
--      la v3; sin filtro y sin "WHERE activo" (no hay columna).
--   2. Clave IDENTITY -> 'v_id' en 'sp_crear_'. Los 3 contadores NO se
--      reciben al crear: nacen en 0 (DEFAULT de la columna).
--   3. 'sp_actualizar_' reemplaza todo menos 'id_lote_carga' y 'cargado_en'.
--      NO lo usa la API: RF117 dice que el lote no se edita; su única
--      escritura posterior es el CIERRE, que sigue siendo sp_guardar_lote_carga.
--   4. 'sp_eliminar_' es DELETE físico (sin 'activo'): sus novedades se van
--      en CASCADE y los asistentes que creó quedan con fk_lote_carga NULL
--      (SET NULL). NO lo usa la API (RF117: el lote es bitácora).
-- ==========================================================================

SET search_path TO catedras, public;

CREATE OR REPLACE PROCEDURE catedras.sp_listar_lote_carga_asistente(
    IN    p_limite    integer DEFAULT NULL,
    INOUT p_resultado json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(l), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_lote_carga, nombre_archivo, cargado_en, fk_usuario,
                   filas_leidas, filas_aceptadas, filas_rechazadas, observaciones
              FROM lote_carga_asistente
             ORDER BY id_lote_carga DESC
             LIMIT p_limite) l;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_consultar_lote_carga_asistente(
    IN    p_id_lote_carga bigint,
    INOUT p_resultado     json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(l) INTO p_resultado
      FROM (SELECT id_lote_carga, nombre_archivo, cargado_en, fk_usuario,
                   filas_leidas, filas_aceptadas, filas_rechazadas, observaciones
              FROM lote_carga_asistente
             WHERE id_lote_carga = p_id_lote_carga) l;
END;
$$;

-- CREAR (abrir) — contadores en 0, 'cargado_en' = now().
CREATE OR REPLACE PROCEDURE catedras.sp_crear_lote_carga_asistente(
    IN    p_nombre_archivo character varying(200),
    IN    p_fk_usuario     bigint DEFAULT NULL,
    INOUT p_resultado      json   DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_id bigint;
BEGIN
    INSERT INTO lote_carga_asistente (nombre_archivo, fk_usuario)
    VALUES (p_nombre_archivo, p_fk_usuario)
    RETURNING id_lote_carga INTO v_id;

    CALL catedras.sp_consultar_lote_carga_asistente(v_id, p_resultado);
END;
$$;

-- ACTUALIZAR — reemplazo completo. NO usado por la API (ver ajuste 3).
-- chk_lote_carga_filas sigue siendo la última muralla.
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_lote_carga_asistente(
    IN    p_id_lote_carga    bigint,
    IN    p_nombre_archivo   character varying(200),
    IN    p_fk_usuario       bigint,
    IN    p_filas_leidas     integer,
    IN    p_filas_aceptadas  integer,
    IN    p_filas_rechazadas integer,
    IN    p_observaciones    text,
    INOUT p_resultado        json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE lote_carga_asistente
       SET nombre_archivo   = p_nombre_archivo,
           fk_usuario       = p_fk_usuario,
           filas_leidas     = p_filas_leidas,
           filas_aceptadas  = p_filas_aceptadas,
           filas_rechazadas = p_filas_rechazadas,
           observaciones    = p_observaciones
     WHERE id_lote_carga = p_id_lote_carga;

    IF FOUND THEN
        CALL catedras.sp_consultar_lote_carga_asistente(p_id_lote_carga, p_resultado);
    END IF;
END;
$$;

-- ELIMINAR — FÍSICO (sin 'activo'). NO usado por la API (ver ajuste 4).
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_lote_carga_asistente(
    IN    p_id_lote_carga bigint,
    INOUT p_resultado     json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    DELETE FROM lote_carga_asistente WHERE id_lote_carga = p_id_lote_carga;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_lote_carga_asistente IS
    'Retrofit criterio 3. Del más reciente al más antiguo.';
COMMENT ON PROCEDURE catedras.sp_consultar_lote_carga_asistente IS
    'Retrofit criterio 3. NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_lote_carga_asistente IS
    'Retrofit criterio 3. ABRE el lote (contadores en 0). El CIERRE es sp_guardar_lote_carga (003).';
COMMENT ON PROCEDURE catedras.sp_actualizar_lote_carga_asistente IS
    'Retrofit criterio 3. Reemplazo completo. NO expuesto por la API (RF117).';
COMMENT ON PROCEDURE catedras.sp_eliminar_lote_carga_asistente IS
    'Retrofit criterio 3. Borrado FÍSICO (novedades en CASCADE). NO expuesto por la API (RF117).';


-- ----- migracion 030_sp_novedad_carga.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/030_sp_novedad_carga.sql
--  Proyecto Cátedras · RETROFIT criterio 3 (PLAN_RETROFIT_SP.md, Fase 4)
--  Tabla: 'novedad_carga' (detalle de 'lote_carga_asistente').
-- --------------------------------------------------------------------------
--  En la API esta tabla es de SOLO LECTURA (RF118): una novedad nace
--  únicamente dentro de 'sp_guardar_lote_carga' (003) al cerrar un lote.
--  Se construyen los 5 procedimientos (criterio 3), pero el repositorio
--  solo llama a listar y consultar; crear/actualizar/eliminar existen en la
--  BD y NO se exponen (IRepositorioNovedadCarga no los tiene).
--  'sp_guardar_lote_carga' NO se reescribe para llamar a
--  'sp_crear_novedad_carga': su INSERT ... SELECT de un arreglo jsonb es
--  una sola sentencia y no conviene partirlo en N CALL.
--
--  AJUSTES AL MOLDE DE 'sede':
--   1. Filtro opcional 'p_fk_lote_carga' en listar; orden (lote, fila).
--   2. Clave IDENTITY -> 'v_id' en 'sp_crear_'.
--   3. 'sp_eliminar_' es DELETE físico (sin 'activo').
--   4. 'sp_actualizar_' no cambia 'fk_lote_carga' (la novedad pertenece
--      siempre al lote que la produjo).
-- ==========================================================================

SET search_path TO catedras, public;

CREATE OR REPLACE PROCEDURE catedras.sp_listar_novedad_carga(
    IN    p_fk_lote_carga bigint  DEFAULT NULL,
    IN    p_limite        integer DEFAULT NULL,
    INOUT p_resultado     json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(n), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_novedad, fk_lote_carga, numero_fila, contenido_crudo, motivo
              FROM novedad_carga
             WHERE p_fk_lote_carga IS NULL OR fk_lote_carga = p_fk_lote_carga
             ORDER BY fk_lote_carga, numero_fila
             LIMIT p_limite) n;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_consultar_novedad_carga(
    IN    p_id_novedad bigint,
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(n) INTO p_resultado
      FROM (SELECT id_novedad, fk_lote_carga, numero_fila, contenido_crudo, motivo
              FROM novedad_carga
             WHERE id_novedad = p_id_novedad) n;
END;
$$;

-- CREAR — NO expuesto por la API (RF118). Fila <= 0 o motivo de más de
-- 200 caracteres -> CHECK / tipo de la columna (500).
CREATE OR REPLACE PROCEDURE catedras.sp_crear_novedad_carga(
    IN    p_fk_lote_carga   bigint,
    IN    p_numero_fila     integer,
    IN    p_contenido_crudo text,
    IN    p_motivo          character varying(200),
    INOUT p_resultado       json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_id bigint;
BEGIN
    INSERT INTO novedad_carga (fk_lote_carga, numero_fila, contenido_crudo, motivo)
    VALUES (p_fk_lote_carga, p_numero_fila, p_contenido_crudo, p_motivo)
    RETURNING id_novedad INTO v_id;

    CALL catedras.sp_consultar_novedad_carga(v_id, p_resultado);
END;
$$;

-- ACTUALIZAR — NO expuesto por la API (RF118).
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_novedad_carga(
    IN    p_id_novedad      bigint,
    IN    p_numero_fila     integer,
    IN    p_contenido_crudo text,
    IN    p_motivo          character varying(200),
    INOUT p_resultado       json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE novedad_carga
       SET numero_fila     = p_numero_fila,
           contenido_crudo = p_contenido_crudo,
           motivo          = p_motivo
     WHERE id_novedad = p_id_novedad;

    IF FOUND THEN
        CALL catedras.sp_consultar_novedad_carga(p_id_novedad, p_resultado);
    END IF;
END;
$$;

-- ELIMINAR — FÍSICO (sin 'activo'). NO expuesto por la API (RF118).
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_novedad_carga(
    IN    p_id_novedad bigint,
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    DELETE FROM novedad_carga WHERE id_novedad = p_id_novedad;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_novedad_carga IS
    'Retrofit criterio 3. Filtro opcional por lote.';
COMMENT ON PROCEDURE catedras.sp_consultar_novedad_carga IS
    'Retrofit criterio 3. NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_novedad_carga IS
    'Retrofit criterio 3. NO expuesto por la API: las novedades nacen en sp_guardar_lote_carga (RF118).';
COMMENT ON PROCEDURE catedras.sp_actualizar_novedad_carga IS
    'Retrofit criterio 3. NO expuesto por la API (RF118).';
COMMENT ON PROCEDURE catedras.sp_eliminar_novedad_carga IS
    'Retrofit criterio 3. Borrado FÍSICO. NO expuesto por la API (RF118).';


-- ----- migracion 031_sp_lote_migracion.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/031_sp_lote_migracion.sql
--  Proyecto Cátedras · RETROFIT criterio 3 (PLAN_RETROFIT_SP.md, Fase 4)
--  Tabla: 'lote_migracion' (maestro de 'detalle_migracion'). Contrato de la
--  API: version7/2_spec.md RF179-RF185 (borrador).
-- --------------------------------------------------------------------------
--  QUÉ YA EXISTÍA Y NO SE TOCA: 'sp_generar_lote_migracion' (script
--  provisto, desde la v1): crea el lote + una fila de detalle por cada
--  registro migrable + pone la sesión en 'MIGRADA', todo en una
--  transacción (ADR D34 de la v7). 'sp_crear_lote_migracion' NO lo
--  reimplementa: lo LLAMA y devuelve la fila como quedó. Así el "crear"
--  pasa por un procedimiento con el nombre del molde sin duplicar la
--  lógica del maestro-detalle (ni arriesgarse a no replicar su efecto
--  colateral sobre 'sesion').
--  Nota: el borrador de la v7 decía "SELECT * FROM fn_generar_lote_api";
--  esa función es solo un envoltorio del mismo procedimiento.
--
--  AJUSTES AL MOLDE DE 'sede':
--   1. Filtro opcional 'p_fk_sesion' en listar; del más reciente al más
--      antiguo (ORDER BY id DESC), igual que lote_carga_asistente.
--   2. 'sp_crear_' recibe la SESIÓN, no las columnas: nombre_archivo y
--      total_filas los calcula sp_generar_lote_migracion. Sesión sin
--      registros migrables -> su RAISE EXCEPTION (check_violation) sube
--      tal cual (500, RF181).
--   3. 'sp_actualizar_' reemplaza solo las 3 columnas que cambian con el
--      tiempo según responde ASIS: instancia_proceso_asis,
--      fk_estado_proceso y observaciones (RF182, ADR D35). fk_sesion,
--      nombre_*, generado_en, fk_usuario y total_filas son la foto de la
--      generación y no se reemplazan.
--   4. 'sp_eliminar_' es DELETE físico (sin 'activo'); los detalles se van
--      en CASCADE. NO lo usa la API: RF185 — borrar un lote borraría la
--      evidencia de un intento de migración real.
-- ==========================================================================

SET search_path TO catedras, public;

CREATE OR REPLACE PROCEDURE catedras.sp_listar_lote_migracion(
    IN    p_fk_sesion bigint  DEFAULT NULL,
    IN    p_limite    integer DEFAULT NULL,
    INOUT p_resultado json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(l), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_lote_migracion, fk_sesion, nombre_proceso, nombre_archivo,
                   generado_en, fk_usuario, instancia_proceso_asis,
                   fk_estado_proceso, total_filas, observaciones
              FROM lote_migracion
             WHERE p_fk_sesion IS NULL OR fk_sesion = p_fk_sesion
             ORDER BY id_lote_migracion DESC
             LIMIT p_limite) l;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_consultar_lote_migracion(
    IN    p_id_lote_migracion bigint,
    INOUT p_resultado         json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(l) INTO p_resultado
      FROM (SELECT id_lote_migracion, fk_sesion, nombre_proceso, nombre_archivo,
                   generado_en, fk_usuario, instancia_proceso_asis,
                   fk_estado_proceso, total_filas, observaciones
              FROM lote_migracion
             WHERE id_lote_migracion = p_id_lote_migracion) l;
END;
$$;

-- CREAR (generar) — ver ajuste 2. Todo lo que hace sp_generar_lote_migracion
-- (maestro + detalles + sesion MIGRADA) queda en la MISMA transacción del CALL.
CREATE OR REPLACE PROCEDURE catedras.sp_crear_lote_migracion(
    IN    p_fk_sesion  bigint,
    IN    p_fk_usuario bigint DEFAULT NULL,
    INOUT p_resultado  json   DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_id bigint;
BEGIN
    CALL catedras.sp_generar_lote_migracion(p_fk_sesion, p_fk_usuario, v_id);

    CALL catedras.sp_consultar_lote_migracion(v_id, p_resultado);
END;
$$;

-- ACTUALIZAR — ver ajuste 3. fk_estado_proceso inexistente -> FK (500).
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_lote_migracion(
    IN    p_id_lote_migracion      bigint,
    IN    p_instancia_proceso_asis character varying(30),
    IN    p_fk_estado_proceso      character varying(12),
    IN    p_observaciones          text,
    INOUT p_resultado              json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE lote_migracion
       SET instancia_proceso_asis = p_instancia_proceso_asis,
           fk_estado_proceso      = p_fk_estado_proceso,
           observaciones          = p_observaciones
     WHERE id_lote_migracion = p_id_lote_migracion;

    IF FOUND THEN
        CALL catedras.sp_consultar_lote_migracion(p_id_lote_migracion, p_resultado);
    END IF;
END;
$$;

-- ELIMINAR — FÍSICO (sin 'activo'). NO usado por la API (RF185).
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_lote_migracion(
    IN    p_id_lote_migracion bigint,
    INOUT p_resultado         json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    DELETE FROM lote_migracion WHERE id_lote_migracion = p_id_lote_migracion;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_lote_migracion IS
    'Retrofit criterio 3. Filtro opcional por sesión; más reciente primero.';
COMMENT ON PROCEDURE catedras.sp_consultar_lote_migracion IS
    'Retrofit criterio 3. NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_lote_migracion IS
    'Retrofit criterio 3. Genera el lote llamando a sp_generar_lote_migracion (maestro+detalle+sesión MIGRADA).';
COMMENT ON PROCEDURE catedras.sp_actualizar_lote_migracion IS
    'Retrofit criterio 3. Reemplaza instancia_proceso_asis, fk_estado_proceso y observaciones (RF182).';
COMMENT ON PROCEDURE catedras.sp_eliminar_lote_migracion IS
    'Retrofit criterio 3. Borrado FÍSICO (detalles en CASCADE). NO expuesto por la API (RF185).';


-- ----- migracion 032_sp_detalle_migracion.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/032_sp_detalle_migracion.sql
--  Proyecto Cátedras · RETROFIT criterio 3 (PLAN_RETROFIT_SP.md, Fase 4)
--  Tabla: 'detalle_migracion' (detalle de 'lote_migracion'). Contrato de la
--  API: version7/2_spec.md RF186-RF189 (borrador).
-- --------------------------------------------------------------------------
--  En la API: listar, consultar y ACTUALIZAR RESULTADO (PATCH). Sin POST:
--  un detalle nace solo dentro de sp_generar_lote_migracion (RF189), igual
--  que novedad_carga nace dentro del cierre de su lote. Sin DELETE.
--  'sp_crear_' y 'sp_eliminar_' existen (criterio 3) y NO se exponen.
--
--  AJUSTES AL MOLDE DE 'sede':
--   1. CHOQUE DE NOMBRES: la tabla tiene una columna 'resultado', y la
--      regla 4 del retrofit reserva 'p_resultado' para el INOUT json. El
--      parámetro de la COLUMNA se llama 'p_resultado_migracion'.
--   2. 'reunion_enviada' es SMALLINT, pero entra como 'integer': C# manda
--      'short' o 'int' según el modelo, e 'int4 -> int2' NO es un cast
--      implícito (un CALL con integer no encontraría el procedimiento).
--      Dentro, la asignación a la columna hace la conversión.
--   3. 'sp_actualizar_' reemplaza SOLO 'resultado' y 'mensaje'. Las 3
--      columnas '*_enviado(a)' son COPIA LITERAL de lo que salió en el
--      archivo (ver COMMENT ON COLUMN del script): no se reescriben nunca.
--      Marcar ACEPTADO una fila cuyo registro ya está ACEPTADO en otro lote
--      -> ux_detmig_aceptado (500, RF188).
--   4. Filtro opcional 'p_fk_lote_migracion' en listar.
--   5. 'sp_eliminar_' es DELETE físico (sin 'activo').
-- ==========================================================================

SET search_path TO catedras, public;

CREATE OR REPLACE PROCEDURE catedras.sp_listar_detalle_migracion(
    IN    p_fk_lote_migracion bigint  DEFAULT NULL,
    IN    p_limite            integer DEFAULT NULL,
    INOUT p_resultado         json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(d), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_detalle, fk_lote_migracion, fk_registro, id_asis_enviado,
                   programa_enviado, reunion_enviada, resultado, mensaje
              FROM detalle_migracion
             WHERE p_fk_lote_migracion IS NULL OR fk_lote_migracion = p_fk_lote_migracion
             ORDER BY fk_lote_migracion, id_detalle
             LIMIT p_limite) d;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_consultar_detalle_migracion(
    IN    p_id_detalle bigint,
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(d) INTO p_resultado
      FROM (SELECT id_detalle, fk_lote_migracion, fk_registro, id_asis_enviado,
                   programa_enviado, reunion_enviada, resultado, mensaje
              FROM detalle_migracion
             WHERE id_detalle = p_id_detalle) d;
END;
$$;

-- CREAR — NO expuesto por la API (RF189). Nace siempre 'PENDIENTE'.
-- Registro repetido en el mismo lote -> uq_detalle_lote_reg (500).
CREATE OR REPLACE PROCEDURE catedras.sp_crear_detalle_migracion(
    IN    p_fk_lote_migracion bigint,
    IN    p_fk_registro       bigint,
    IN    p_id_asis_enviado   character varying(15),
    IN    p_reunion_enviada   integer,
    IN    p_programa_enviado  character varying(5) DEFAULT NULL,
    INOUT p_resultado         json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_id bigint;
BEGIN
    INSERT INTO detalle_migracion (fk_lote_migracion, fk_registro, id_asis_enviado,
                                   programa_enviado, reunion_enviada)
    VALUES (p_fk_lote_migracion, p_fk_registro, p_id_asis_enviado,
            p_programa_enviado, p_reunion_enviada)
    RETURNING id_detalle INTO v_id;

    CALL catedras.sp_consultar_detalle_migracion(v_id, p_resultado);
END;
$$;

-- ACTUALIZAR — resultado + mensaje (ver ajustes 1 y 3).
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_detalle_migracion(
    IN    p_id_detalle           bigint,
    IN    p_resultado_migracion  character varying(12),
    IN    p_mensaje              character varying(300),
    INOUT p_resultado            json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE detalle_migracion
       SET resultado = p_resultado_migracion,
           mensaje   = p_mensaje
     WHERE id_detalle = p_id_detalle;

    IF FOUND THEN
        CALL catedras.sp_consultar_detalle_migracion(p_id_detalle, p_resultado);
    END IF;
END;
$$;

-- ELIMINAR — FÍSICO (sin 'activo'). NO expuesto por la API (RF189).
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_detalle_migracion(
    IN    p_id_detalle bigint,
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    DELETE FROM detalle_migracion WHERE id_detalle = p_id_detalle;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_detalle_migracion IS
    'Retrofit criterio 3. Filtro opcional por lote.';
COMMENT ON PROCEDURE catedras.sp_consultar_detalle_migracion IS
    'Retrofit criterio 3. NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_detalle_migracion IS
    'Retrofit criterio 3. NO expuesto por la API: los detalles nacen en sp_generar_lote_migracion (RF189).';
COMMENT ON PROCEDURE catedras.sp_actualizar_detalle_migracion IS
    'Retrofit criterio 3. Reemplaza resultado (p_resultado_migracion) y mensaje (RF188).';
COMMENT ON PROCEDURE catedras.sp_eliminar_detalle_migracion IS
    'Retrofit criterio 3. Borrado FÍSICO. NO expuesto por la API (RF189).';


-- ----- migracion 033_sp_usuario.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/033_sp_usuario.sql
--  Proyecto Cátedras · RETROFIT criterio 3 (VERSION_2.md §4) · Fase 5
--  Molde: 005_sp_sede_ejemplo.sql (ver PLAN_RETROFIT_SP.md).
--
--  'usuario' SÍ tiene 'activo' -> 'sp_eliminar_usuario' es LÓGICO.
--  (Antes era un DELETE físico con CASCADE a rol_por_usuario; ahora la
--  cuenta queda inactiva y sus roles se conservan como historial. El login
--  ya rechaza cuentas inactivas en ServicioUsuario, ADR D27.)
--
--  ⚠️ CLAVE (ADR D23, RNF13, RF138):
--   · Ningún procedimiento de este archivo calcula ni decide un hash. El
--     hash BCrypt lo calcula ServicioUsuario ANTES de llamar al
--     repositorio; aquí solo se guarda la cadena que llega.
--   · 'clave_hash' NUNCA sale en el 'p_resultado' de listar / consultar /
--     crear / actualizar (esos JSON pueden terminar sirviendo una
--     respuesta). La única excepción es 'sp_consultar_usuario_por_login',
--     de uso INTERNO para autenticar: el servicio necesita el hash para
--     BCrypt.Verify, y el modelo C# lo marca [JsonIgnore] para que nunca
--     se serialice hacia afuera.
--
--  AJUSTES AL MOLDE DE 'sede' (ver AJUSTES_RETROFIT_SP_LOTE_FASE5.md):
--   1) Columnas de proyección SIN 'clave_hash' (salvo el login, arriba).
--   2) 'sp_actualizar_usuario' recibe TODAS las columnas, pero
--      p_clave_hash NULL = "conservar el hash guardado". 'clave_hash' es
--      NOT NULL en la tabla, así que NULL nunca es un valor válido y no
--      hay ambigüedad. Hace falta porque el repositorio ya no puede
--      "mezclar" el hash actual (sp_consultar_usuario no lo devuelve), y
--      no queremos que el hash viaje a C# solo para devolverlo igual.
--   3) Dos procedimientos extra, porque IRepositorioUsuario tiene dos
--      métodos fuera de los 5 verbos: ObtenerPorLoginAsync y
--      ActualizarUltimoAccesoAsync. Así el repositorio no conserva NINGÚN
--      SELECT/UPDATE a mano.
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  LISTAR — solo activos (igual que sede), límite opcional. Sin clave_hash.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_listar_usuario(
    IN    p_limite    integer DEFAULT NULL,
    INOUT p_resultado json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(u), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_usuario, fk_asistente, usuario, activo,
                   ultimo_acceso, creado_en
              FROM usuario
             WHERE activo
             ORDER BY id_usuario
             LIMIT p_limite) u;
END;
$$;

-- --------------------------------------------------------------------------
--  CONSULTAR — por id (activo o no, igual que sede). Sin clave_hash.
--  NULL en p_resultado = no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_consultar_usuario(
    IN    p_id_usuario bigint,
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(u) INTO p_resultado
      FROM (SELECT id_usuario, fk_asistente, usuario, activo,
                   ultimo_acceso, creado_en
              FROM usuario
             WHERE id_usuario = p_id_usuario) u;
END;
$$;

-- --------------------------------------------------------------------------
--  CONSULTAR POR LOGIN — USO INTERNO (autenticación). ÚNICO procedimiento
--  que devuelve 'clave_hash'. uq_usuario_login garantiza 0 o 1 fila.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_consultar_usuario_por_login(
    IN    p_usuario   character varying(40),
    INOUT p_resultado json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(u) INTO p_resultado
      FROM (SELECT id_usuario, fk_asistente, usuario, clave_hash, activo,
                   ultimo_acceso, creado_en
              FROM usuario
             WHERE usuario = p_usuario) u;
END;
$$;

-- --------------------------------------------------------------------------
--  CREAR — recibe el hash YA calculado. id_usuario y creado_en los pone la
--  BD; se devuelven en la fila (sin clave_hash) para que el repositorio
--  conserve su contrato (IdUsuario, CreadoEn).
--  usuario repetido (uq_usuario_login), persona con cuenta
--  (uq_usuario_persona) o asistente inexistente -> NpgsqlException.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_crear_usuario(
    IN    p_fk_asistente bigint,
    IN    p_usuario      character varying(40),
    IN    p_clave_hash   character varying(255),
    IN    p_activo       boolean DEFAULT true,
    INOUT p_resultado    json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_id bigint;
BEGIN
    INSERT INTO usuario (fk_asistente, usuario, clave_hash, activo)
    VALUES (p_fk_asistente, p_usuario, p_clave_hash, COALESCE(p_activo, true))
    RETURNING id_usuario INTO v_id;

    SELECT row_to_json(u) INTO p_resultado
      FROM (SELECT id_usuario, fk_asistente, usuario, activo,
                   ultimo_acceso, creado_en
              FROM usuario
             WHERE id_usuario = v_id) u;
END;
$$;

-- --------------------------------------------------------------------------
--  ACTUALIZAR — reemplazo completo de las columnas de negocio
--  (fk_asistente, usuario, clave_hash, activo). NO se tocan: id_usuario
--  (PK), creado_en (BD) ni ultimo_acceso (tiene su propio procedimiento).
--  p_clave_hash NULL = conservar el hash actual (ver ajuste 2 arriba).
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_usuario(
    IN    p_id_usuario   bigint,
    IN    p_fk_asistente bigint,
    IN    p_usuario      character varying(40),
    IN    p_clave_hash   character varying(255),
    IN    p_activo       boolean,
    INOUT p_resultado    json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE usuario
       SET fk_asistente = p_fk_asistente,
           usuario      = p_usuario,
           clave_hash   = COALESCE(p_clave_hash, clave_hash),
           activo       = p_activo
     WHERE id_usuario = p_id_usuario;

    IF FOUND THEN
        SELECT row_to_json(u) INTO p_resultado
          FROM (SELECT id_usuario, fk_asistente, usuario, activo,
                       ultimo_acceso, creado_en
                  FROM usuario
                 WHERE id_usuario = p_id_usuario) u;
    END IF;
END;
$$;

-- --------------------------------------------------------------------------
--  REGISTRAR ACCESO — 'ultimo_acceso = now()' tras un login correcto.
--  now() lo evalúa PostgreSQL: la hora es la del servidor de la BD.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_registrar_acceso_usuario(
    IN    p_id_usuario bigint,
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE usuario
       SET ultimo_acceso = now()
     WHERE id_usuario = p_id_usuario;

    p_resultado := json_build_object('registrado', FOUND);
END;
$$;

-- --------------------------------------------------------------------------
--  ELIMINAR — LÓGICO (la tabla tiene 'activo'). Ya inactivo = no cuenta.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_usuario(
    IN    p_id_usuario bigint,
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE usuario
       SET activo = false
     WHERE id_usuario = p_id_usuario
       AND activo;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_usuario IS
    'Retrofit criterio 3. Solo activos, límite opcional. Nunca incluye clave_hash.';
COMMENT ON PROCEDURE catedras.sp_consultar_usuario IS
    'Retrofit criterio 3. NULL = no existe. Nunca incluye clave_hash.';
COMMENT ON PROCEDURE catedras.sp_consultar_usuario_por_login IS
    'Retrofit criterio 3. USO INTERNO (login): único SP que devuelve clave_hash.';
COMMENT ON PROCEDURE catedras.sp_crear_usuario IS
    'Retrofit criterio 3. Recibe el hash ya calculado por ServicioUsuario (BCrypt).';
COMMENT ON PROCEDURE catedras.sp_actualizar_usuario IS
    'Retrofit criterio 3. Reemplazo completo; p_clave_hash NULL = conservar el hash.';
COMMENT ON PROCEDURE catedras.sp_registrar_acceso_usuario IS
    'Retrofit criterio 3. ultimo_acceso = now() tras un login correcto.';
COMMENT ON PROCEDURE catedras.sp_eliminar_usuario IS
    'Retrofit criterio 3. Borrado LÓGICO (activo=false).';


-- ----- migracion 034_sp_rol_por_usuario.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/034_sp_rol_por_usuario.sql
--  Proyecto Cátedras · RETROFIT criterio 3 (VERSION_2.md §4) · Fase 5
--  Molde: 005_sp_sede_ejemplo.sql (ver PLAN_RETROFIT_SP.md).
--
--  'rol_por_usuario' NO tiene 'activo' -> 'sp_eliminar_' es FÍSICO.
--
--  AJUSTES AL MOLDE DE 'sede':
--   1) Llave COMPUESTA (fk_usuario, fk_rol, fecha_ini): consultar,
--      actualizar, cerrar y eliminar reciben las tres columnas.
--   2) La única columna que no es llave es 'fecha_fin', así que
--      'sp_actualizar_' solo reemplaza esa.
--   3) 'sp_cerrar_rol_por_usuario' (extra): es el verbo que de verdad usa
--      la API (PATCH /cerrar). Se deja aparte de 'sp_actualizar_' para
--      conservar la guarda 'AND fecha_fin IS NULL' (no cerrar dos veces el
--      mismo rol aunque lleguen dos peticiones a la vez) — mismo criterio
--      que 'sp_revocar_enlace_registro'.
--   4) 'sp_listar_rol_vigente_usuario' (extra): ObtenerVigentesAsync, que
--      usa el login. Devuelve un arreglo JSON de códigos de rol (texto).
--   5) Fechas como 'date': desde C# llegan como DateOnly -> el manejador
--      de Dapper las manda como timestamp, y un CALL NO aplica casts de
--      asignación, así que el repositorio escribe CAST(@x AS date).
--
--  ⚠️ 'sp_eliminar_rol_por_usuario' existe para cumplir el criterio, pero
--  NO lo expone la API (IRepositorioRolPorUsuario no tiene Eliminar):
--  borrar una fila de rol borra historia. Lo correcto es cerrarla.
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  LISTAR — filtro opcional por usuario, límite opcional. Orden: por
--  usuario y, dentro de cada uno, el más reciente primero (igual que antes).
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_listar_rol_por_usuario(
    IN    p_fk_usuario bigint  DEFAULT NULL,
    IN    p_limite     integer DEFAULT NULL,
    INOUT p_resultado  json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(r), '[]'::json)
      INTO p_resultado
      FROM (SELECT fk_usuario, fk_rol, fecha_ini, fecha_fin
              FROM rol_por_usuario
             WHERE p_fk_usuario IS NULL OR fk_usuario = p_fk_usuario
             ORDER BY fk_usuario, fecha_ini DESC, fk_rol
             LIMIT p_limite) r;
END;
$$;

-- --------------------------------------------------------------------------
--  LISTAR VIGENTES — solo los códigos de rol abiertos (fecha_fin NULL) de
--  un usuario. Siempre un arreglo ('[]' si no tiene ninguno).
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_listar_rol_vigente_usuario(
    IN    p_fk_usuario bigint,
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(fk_rol ORDER BY fk_rol), '[]'::json)
      INTO p_resultado
      FROM rol_por_usuario
     WHERE fk_usuario = p_fk_usuario
       AND fecha_fin IS NULL;
END;
$$;

-- --------------------------------------------------------------------------
--  CONSULTAR — por la llave completa. NULL = no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_consultar_rol_por_usuario(
    IN    p_fk_usuario bigint,
    IN    p_fk_rol     character varying(20),
    IN    p_fecha_ini  date,
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(r) INTO p_resultado
      FROM (SELECT fk_usuario, fk_rol, fecha_ini, fecha_fin
              FROM rol_por_usuario
             WHERE fk_usuario = p_fk_usuario
               AND fk_rol     = p_fk_rol
               AND fecha_ini  = p_fecha_ini) r;
END;
$$;

-- --------------------------------------------------------------------------
--  CREAR — un rol nace abierto (fecha_fin NULL). p_fecha_ini NULL = hoy
--  (mismo DEFAULT CURRENT_DATE de la tabla).
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_crear_rol_por_usuario(
    IN    p_fk_usuario bigint,
    IN    p_fk_rol     character varying(20),
    IN    p_fecha_ini  date DEFAULT NULL,
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_fecha_ini date := COALESCE(p_fecha_ini, CURRENT_DATE);
BEGIN
    INSERT INTO rol_por_usuario (fk_usuario, fk_rol, fecha_ini)
    VALUES (p_fk_usuario, p_fk_rol, v_fecha_ini);

    SELECT row_to_json(r) INTO p_resultado
      FROM (SELECT fk_usuario, fk_rol, fecha_ini, fecha_fin
              FROM rol_por_usuario
             WHERE fk_usuario = p_fk_usuario
               AND fk_rol     = p_fk_rol
               AND fecha_ini  = v_fecha_ini) r;
END;
$$;

-- --------------------------------------------------------------------------
--  ACTUALIZAR — reemplazo completo de la única columna no-llave.
--  (Para el flujo normal usar 'sp_cerrar_', que no deja cerrar dos veces.)
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_rol_por_usuario(
    IN    p_fk_usuario bigint,
    IN    p_fk_rol     character varying(20),
    IN    p_fecha_ini  date,
    IN    p_fecha_fin  date,
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE rol_por_usuario
       SET fecha_fin = p_fecha_fin
     WHERE fk_usuario = p_fk_usuario
       AND fk_rol     = p_fk_rol
       AND fecha_ini  = p_fecha_ini;

    IF FOUND THEN
        SELECT row_to_json(r) INTO p_resultado
          FROM (SELECT fk_usuario, fk_rol, fecha_ini, fecha_fin
                  FROM rol_por_usuario
                 WHERE fk_usuario = p_fk_usuario
                   AND fk_rol     = p_fk_rol
                   AND fecha_ini  = p_fecha_ini) r;
    END IF;
END;
$$;

-- --------------------------------------------------------------------------
--  CERRAR — fecha_fin solo si el rol sigue abierto. NULL en p_resultado =
--  no existe o ya estaba cerrado (el servicio lo traduce a 400/404 igual
--  que antes con "0 filas"). fecha_fin < fecha_ini -> chk_rol_usuario_fechas.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_cerrar_rol_por_usuario(
    IN    p_fk_usuario bigint,
    IN    p_fk_rol     character varying(20),
    IN    p_fecha_ini  date,
    IN    p_fecha_fin  date,
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE rol_por_usuario
       SET fecha_fin = p_fecha_fin
     WHERE fk_usuario = p_fk_usuario
       AND fk_rol     = p_fk_rol
       AND fecha_ini  = p_fecha_ini
       AND fecha_fin IS NULL;

    IF FOUND THEN
        SELECT row_to_json(r) INTO p_resultado
          FROM (SELECT fk_usuario, fk_rol, fecha_ini, fecha_fin
                  FROM rol_por_usuario
                 WHERE fk_usuario = p_fk_usuario
                   AND fk_rol     = p_fk_rol
                   AND fecha_ini  = p_fecha_ini) r;
    END IF;
END;
$$;

-- --------------------------------------------------------------------------
--  ELIMINAR — FÍSICO (sin 'activo'). No expuesto por la API (ver arriba).
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_rol_por_usuario(
    IN    p_fk_usuario bigint,
    IN    p_fk_rol     character varying(20),
    IN    p_fecha_ini  date,
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    DELETE FROM rol_por_usuario
     WHERE fk_usuario = p_fk_usuario
       AND fk_rol     = p_fk_rol
       AND fecha_ini  = p_fecha_ini;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_rol_por_usuario IS
    'Retrofit criterio 3. Filtro opcional por usuario, límite opcional.';
COMMENT ON PROCEDURE catedras.sp_listar_rol_vigente_usuario IS
    'Retrofit criterio 3. Arreglo JSON con los códigos de rol abiertos (login).';
COMMENT ON PROCEDURE catedras.sp_consultar_rol_por_usuario IS
    'Retrofit criterio 3. Por llave compuesta. NULL = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_rol_por_usuario IS
    'Retrofit criterio 3. El rol nace abierto. fecha_ini NULL = hoy.';
COMMENT ON PROCEDURE catedras.sp_actualizar_rol_por_usuario IS
    'Retrofit criterio 3. Reemplaza fecha_fin (única columna no-llave).';
COMMENT ON PROCEDURE catedras.sp_cerrar_rol_por_usuario IS
    'Retrofit criterio 3. Cierra solo si sigue abierto. NULL = no existe o ya cerrado.';
COMMENT ON PROCEDURE catedras.sp_eliminar_rol_por_usuario IS
    'Retrofit criterio 3. Borrado FÍSICO. NO expuesto: lo correcto es cerrar el rol.';


-- ----- migracion 035_sp_enlace_registro.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/035_sp_enlace_registro.sql
--  Proyecto Cátedras · RETROFIT criterio 3 (VERSION_2.md §4) · Fase 5
--  Molde: 005_sp_sede_ejemplo.sql (ver PLAN_RETROFIT_SP.md, Grupo C).
--
--  'enlace_registro' ya tenía su escritura principal en la BD:
--     EMITIR (crear) -> fn_emitir_enlace_api -> sp_emitir_enlace (script
--     provisto, RN-14). NO se toca ni se reemplaza.
--  Este archivo agrega lo que faltaba:
--     sp_listar_enlace_registro, sp_consultar_enlace_registro y
--     sp_revocar_enlace_registro (antes era un UPDATE suelto en C#).
--
--  POR QUÉ NO HAY 'sp_actualizar_' NI 'sp_eliminar_' GENÉRICOS: la
--  revocación ES la baja lógica de un enlace ('revocado_en' cumple el
--  papel de 'activo': ex_enlace_sin_solape solo mira los no revocados), y
--  un UPDATE libre de token/url/ventana rompería RN-14/RN-15. El "editar"
--  legítimo de un enlace es emitir uno nuevo (que revoca el anterior).
--
--  AJUSTES AL MOLDE DE 'sede':
--   1) 'ventana' es tstzrange: se parte en ventana_desde/ventana_hasta
--      (lower/upper) dentro del JSON, igual que hacía el SELECT de C#.
--   2) Revocar devuelve la FILA (con revocado_en) en vez de
--      {"eliminado": ...}; NULL = no existe o ya estaba revocado.
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  LISTAR — filtro opcional por sesión, límite opcional. Incluye los
--  revocados (es historial; antes también los listaba).
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_listar_enlace_registro(
    IN    p_fk_sesion bigint  DEFAULT NULL,
    IN    p_limite    integer DEFAULT NULL,
    INOUT p_resultado json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(e), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_enlace, fk_sesion, token, url_publica, ruta_imagen_qr,
                   canal, lower(ventana) AS ventana_desde,
                   upper(ventana) AS ventana_hasta, usos_maximos,
                   revocado_en, fk_usuario_crea, creado_en
              FROM enlace_registro
             WHERE p_fk_sesion IS NULL OR fk_sesion = p_fk_sesion
             ORDER BY id_enlace
             LIMIT p_limite) e;
END;
$$;

-- --------------------------------------------------------------------------
--  CONSULTAR — por id. NULL = no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_consultar_enlace_registro(
    IN    p_id_enlace bigint,
    INOUT p_resultado json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(e) INTO p_resultado
      FROM (SELECT id_enlace, fk_sesion, token, url_publica, ruta_imagen_qr,
                   canal, lower(ventana) AS ventana_desde,
                   upper(ventana) AS ventana_hasta, usos_maximos,
                   revocado_en, fk_usuario_crea, creado_en
              FROM enlace_registro
             WHERE id_enlace = p_id_enlace) e;
END;
$$;

-- --------------------------------------------------------------------------
--  REVOCAR — baja lógica del enlace. 'AND revocado_en IS NULL': un segundo
--  intento no cambia nada y deja p_resultado en NULL (el servicio lo
--  traduce a 400, igual que antes con "0 filas").
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_revocar_enlace_registro(
    IN    p_id_enlace bigint,
    INOUT p_resultado json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE enlace_registro
       SET revocado_en = now()
     WHERE id_enlace = p_id_enlace
       AND revocado_en IS NULL;

    IF FOUND THEN
        SELECT row_to_json(e) INTO p_resultado
          FROM (SELECT id_enlace, fk_sesion, token, url_publica, ruta_imagen_qr,
                       canal, lower(ventana) AS ventana_desde,
                       upper(ventana) AS ventana_hasta, usos_maximos,
                       revocado_en, fk_usuario_crea, creado_en
                  FROM enlace_registro
                 WHERE id_enlace = p_id_enlace) e;
    END IF;
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_enlace_registro IS
    'Retrofit criterio 3. Filtro opcional por sesión; ventana partida en desde/hasta.';
COMMENT ON PROCEDURE catedras.sp_consultar_enlace_registro IS
    'Retrofit criterio 3. NULL = no existe.';
COMMENT ON PROCEDURE catedras.sp_revocar_enlace_registro IS
    'Retrofit criterio 3. Baja lógica (revocado_en=now()). NULL = no existe o ya revocado.';


-- ----- migracion 036_sp_clave_acceso.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/036_sp_clave_acceso.sql
--  Proyecto Cátedras · RETROFIT criterio 3 (VERSION_2.md §4) · Fase 5
--  Molde: 005_sp_sede_ejemplo.sql (ver PLAN_RETROFIT_SP.md, Grupo C).
--
--  'clave_acceso' ya tenía su escritura en la BD (script provisto):
--     SOLICITAR (crear)     -> fn_solicitar_clave_api -> sp_solicitar_clave
--     VALIDAR  (actualizar) -> fn_validar_clave_api   -> sp_validar_clave_y_registrar
--  NO se tocan ni se reemplazan. Este archivo agrega Listar y Consultar.
--
--  POR QUÉ NO HAY 'sp_actualizar_' NI 'sp_eliminar_' GENÉRICOS: la fila
--  guarda el hash de una clave de un solo uso (RN-34). Editarla a mano
--  (intentos, usado_en, expira_en) desarma el control de intentos y de
--  expiración; borrarla no hace falta porque expira sola.
--
--  AJUSTES AL MOLDE DE 'sede':
--   1) La proyección NO incluye 'clave_hash' (RNF12), igual que el SELECT
--      que tenía C#.
--   2) 'ip_solicitud' es inet: se proyecta con host() para que llegue
--      "10.0.0.1" y no "10.0.0.1/32".
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  LISTAR — filtro opcional por asistente, límite opcional. Sin clave_hash.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_listar_clave_acceso(
    IN    p_fk_asistente bigint  DEFAULT NULL,
    IN    p_limite       integer DEFAULT NULL,
    INOUT p_resultado    json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(c), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_clave, fk_asistente, fk_enlace, enviado_a,
                   es_correo_institucional, generado_en, expira_en,
                   usado_en, intentos, host(ip_solicitud) AS ip_solicitud
              FROM clave_acceso
             WHERE p_fk_asistente IS NULL OR fk_asistente = p_fk_asistente
             ORDER BY id_clave
             LIMIT p_limite) c;
END;
$$;

-- --------------------------------------------------------------------------
--  CONSULTAR — por id. NULL = no existe. Sin clave_hash.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_consultar_clave_acceso(
    IN    p_id_clave  bigint,
    INOUT p_resultado json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(c) INTO p_resultado
      FROM (SELECT id_clave, fk_asistente, fk_enlace, enviado_a,
                   es_correo_institucional, generado_en, expira_en,
                   usado_en, intentos, host(ip_solicitud) AS ip_solicitud
              FROM clave_acceso
             WHERE id_clave = p_id_clave) c;
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_clave_acceso IS
    'Retrofit criterio 3. Filtro opcional por asistente. Nunca incluye clave_hash.';
COMMENT ON PROCEDURE catedras.sp_consultar_clave_acceso IS
    'Retrofit criterio 3. NULL = no existe. Nunca incluye clave_hash.';


-- ----- migracion 037_sp_registro_asistencia.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/037_sp_registro_asistencia.sql
--  Proyecto Cátedras · RETROFIT criterio 3 (VERSION_2.md §4) · Fase 5
--  Molde: 005_sp_sede_ejemplo.sql (ver PLAN_RETROFIT_SP.md).
--
--  'registro_asistencia' NO tiene 'activo' -> 'sp_eliminar_' es FÍSICO.
--
--  ⚠️ ES UN HECHO (comentario de la tabla: "Hecho de que un asistente
--  quedó registrado en una sesión, en un instante concreto, con el
--  programa y la vinculación que tenía en ese momento"). Por eso:
--   · Crear, Listar y Consultar: los usa la API (igual que antes).
--   · Actualizar y Eliminar: EXISTEN para cumplir el criterio al pie de la
--     letra, pero NO están en IRepositorioRegistroAsistencia ni en el
--     controlador, y traen GUARDAS para que ni siquiera un CALL a mano
--     rompa el hecho:
--       - sp_actualizar_: recibe todas las columnas (molde), pero rechaza
--         cualquier cambio en las columnas del HECHO (fk_sesion,
--         fk_asistente, registrado_en y las dos instantáneas, que "NUNCA
--         se actualizan"). Solo deja corregir trazabilidad: fk_enlace,
--         fk_clave, origen, ip, user_agent. Además, trg_registro_validar
--         es BEFORE INSERT: un UPDATE libre se saltaría la ventana, el
--         cupo y RN-07/RN-22.
--       - sp_eliminar_: rechaza el borrado si el registro ya tiene una
--         respuesta de encuesta (fk_respenc_registro es CASCADE: borraría
--         en silencio una encuesta cerrada, ADR D31). Si ya se exportó al
--         ASIS, lo frena fk_detmig_registro (RESTRICT). trg_bitacora_registro
--         deja la fila borrada en 'bitacora'.
--
--  AJUSTES AL MOLDE DE 'sede':
--   1) 'ip' es inet: host(ip) en la proyección.
--   2) Crear manda SOLO fk_sesion, fk_asistente, origen; el trigger
--      trg_registro_validar pone las instantáneas. El SP devuelve la fila
--      DESPUÉS del INSERT (ya con las instantáneas), como hacía el
--      RETURNING de C#.
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  LISTAR — filtros opcionales por sesión y por asistente.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_listar_registro_asistencia(
    IN    p_fk_sesion    bigint  DEFAULT NULL,
    IN    p_fk_asistente bigint  DEFAULT NULL,
    IN    p_limite       integer DEFAULT NULL,
    INOUT p_resultado    json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(r), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_registro, fk_sesion, fk_asistente, fk_enlace, fk_clave,
                   registrado_en, fk_programa_snapshot, fk_vinculacion_snapshot,
                   origen, host(ip) AS ip, user_agent
              FROM registro_asistencia
             WHERE (p_fk_sesion    IS NULL OR fk_sesion    = p_fk_sesion)
               AND (p_fk_asistente IS NULL OR fk_asistente = p_fk_asistente)
             ORDER BY id_registro
             LIMIT p_limite) r;
END;
$$;

-- --------------------------------------------------------------------------
--  CONSULTAR — por id. NULL = no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_consultar_registro_asistencia(
    IN    p_id_registro bigint,
    INOUT p_resultado   json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(r) INTO p_resultado
      FROM (SELECT id_registro, fk_sesion, fk_asistente, fk_enlace, fk_clave,
                   registrado_en, fk_programa_snapshot, fk_vinculacion_snapshot,
                   origen, host(ip) AS ip, user_agent
              FROM registro_asistencia
             WHERE id_registro = p_id_registro) r;
END;
$$;

-- --------------------------------------------------------------------------
--  CREAR — el trigger calcula las instantáneas y valida ventana, cupo,
--  vinculación (RN-15, RN-22, RN-07, RN-38). Si algo no cumple -> excepción.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_crear_registro_asistencia(
    IN    p_fk_sesion    bigint,
    IN    p_fk_asistente bigint,
    IN    p_origen       character varying(15) DEFAULT 'QR',
    INOUT p_resultado    json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_id bigint;
BEGIN
    INSERT INTO registro_asistencia (fk_sesion, fk_asistente, origen)
    VALUES (p_fk_sesion, p_fk_asistente, COALESCE(p_origen, 'QR'))
    RETURNING id_registro INTO v_id;

    SELECT row_to_json(r) INTO p_resultado
      FROM (SELECT id_registro, fk_sesion, fk_asistente, fk_enlace, fk_clave,
                   registrado_en, fk_programa_snapshot, fk_vinculacion_snapshot,
                   origen, host(ip) AS ip, user_agent
              FROM registro_asistencia
             WHERE id_registro = v_id) r;
END;
$$;

-- --------------------------------------------------------------------------
--  ACTUALIZAR — firma completa (molde), pero el HECHO es inmutable.
--  NO expuesto por la API.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_registro_asistencia(
    IN    p_id_registro             bigint,
    IN    p_fk_sesion               bigint,
    IN    p_fk_asistente            bigint,
    IN    p_fk_enlace               bigint,
    IN    p_fk_clave                bigint,
    IN    p_registrado_en           timestamp with time zone,
    IN    p_fk_programa_snapshot    character varying(5),
    IN    p_fk_vinculacion_snapshot character varying(15),
    IN    p_origen                  character varying(15),
    IN    p_ip                      inet,
    IN    p_user_agent              character varying(300),
    INOUT p_resultado               json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_actual registro_asistencia%ROWTYPE;
BEGIN
    SELECT * INTO v_actual
      FROM registro_asistencia
     WHERE id_registro = p_id_registro
       FOR UPDATE;

    IF NOT FOUND THEN
        RETURN;   -- p_resultado NULL = no existe
    END IF;

    IF v_actual.fk_sesion               IS DISTINCT FROM p_fk_sesion
    OR v_actual.fk_asistente            IS DISTINCT FROM p_fk_asistente
    OR v_actual.registrado_en           IS DISTINCT FROM p_registrado_en
    OR v_actual.fk_programa_snapshot    IS DISTINCT FROM p_fk_programa_snapshot
    OR v_actual.fk_vinculacion_snapshot IS DISTINCT FROM p_fk_vinculacion_snapshot
    THEN
        RAISE EXCEPTION
            'El registro % es un hecho de asistencia: sesión, asistente, instante e instantáneas no se modifican',
            p_id_registro USING ERRCODE = 'check_violation';
    END IF;

    UPDATE registro_asistencia
       SET fk_enlace  = p_fk_enlace,
           fk_clave   = p_fk_clave,
           origen     = p_origen,
           ip         = p_ip,
           user_agent = p_user_agent
     WHERE id_registro = p_id_registro;

    SELECT row_to_json(r) INTO p_resultado
      FROM (SELECT id_registro, fk_sesion, fk_asistente, fk_enlace, fk_clave,
                   registrado_en, fk_programa_snapshot, fk_vinculacion_snapshot,
                   origen, host(ip) AS ip, user_agent
              FROM registro_asistencia
             WHERE id_registro = p_id_registro) r;
END;
$$;

-- --------------------------------------------------------------------------
--  ELIMINAR — FÍSICO (sin 'activo'), con guarda. NO expuesto por la API.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_registro_asistencia(
    IN    p_id_registro bigint,
    INOUT p_resultado   json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    IF EXISTS (SELECT 1 FROM respuesta_encuesta WHERE fk_registro = p_id_registro) THEN
        RAISE EXCEPTION
            'El registro % ya tiene una respuesta de encuesta (hecho auditable, ADR D31): no se elimina',
            p_id_registro USING ERRCODE = 'restrict_violation';
    END IF;

    DELETE FROM registro_asistencia WHERE id_registro = p_id_registro;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_registro_asistencia IS
    'Retrofit criterio 3. Filtros opcionales por sesión y asistente.';
COMMENT ON PROCEDURE catedras.sp_consultar_registro_asistencia IS
    'Retrofit criterio 3. NULL = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_registro_asistencia IS
    'Retrofit criterio 3. El trigger pone las instantáneas; devuelve la fila final.';
COMMENT ON PROCEDURE catedras.sp_actualizar_registro_asistencia IS
    'Retrofit criterio 3. NO expuesto. El hecho es inmutable: solo corrige trazabilidad.';
COMMENT ON PROCEDURE catedras.sp_eliminar_registro_asistencia IS
    'Retrofit criterio 3. NO expuesto. FÍSICO; se niega si ya hay respuesta de encuesta.';


-- ----- migracion 038_sp_pregunta.sql -----
RESET search_path;
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


-- ----- migracion 039_sp_opcion_pregunta.sql -----
RESET search_path;
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


-- ----- migracion 040_sp_respuesta_encuesta.sql -----
RESET search_path;
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


-- ----- migracion 041_sp_respuesta_item.sql -----
RESET search_path;
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


-- ----- migracion 042_sp_guardar_catedra.sql -----
RESET search_path;
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


-- ----- migracion 043_sp_guardar_encuesta.sql -----
RESET search_path;
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


-- ----- migracion 044_sp_guardar_sesion.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/044_sp_guardar_sesion.sql
--  Proyecto Cátedras · Maestro-detalle en el formulario — Guía IA, Regla 1
-- --------------------------------------------------------------------------
--  Maestro: 'sesion'. Detalle (opcional): su primer 'enlace_registro'
--  (#2 de la Regla 4).
--
--  POST /api/Sesion puede traer, además de la sesión, un objeto 'enlace'
--  ({"minutos_antes":30, "minutos_despues":30, "fk_usuario":1}) para
--  emitir de una vez el enlace / QR de registro. Sesión y enlace se
--  guardan en UNA sola transacción: si emitir falla, la sesión tampoco
--  queda creada.
--
--  REUTILIZA, no reescribe: la sesión se crea con 'sp_crear_sesion'
--  (migración 011, con su trigger de consecutivo) y el enlace con
--  'sp_emitir_enlace' (PROVISTO por el script de la BD: arma el token, la
--  URL, el canal según la modalidad y la ventana según los parámetros).
--
--  Los registros de asistencia (#3) NO van aquí: nacen cuando la sesión ya
--  ocurrió, cada uno en su momento (caso "No aplica" de la Regla 1).
--
--  CÓMO SE APLICA: docker-compose.yml lo monta como 'z044_' (después de
--  'z011_'). Con una BD ya creada, a mano con psql.
-- ==========================================================================

SET search_path TO catedras, public;

CREATE OR REPLACE PROCEDURE catedras.sp_guardar_sesion(
    -- Columnas propias del maestro (mismo orden y tipos que sp_crear_sesion).
    IN    p_fk_catedra          bigint,
    IN    p_inicio              timestamp with time zone,
    IN    p_fin                 timestamp with time zone,
    IN    p_fk_modalidad        character varying(15),
    IN    p_fk_sede             character varying(15),
    IN    p_fk_periodo          character(6),
    IN    p_numero_reunion      smallint                 DEFAULT NULL,
    IN    p_numero_reunion_asis smallint                 DEFAULT NULL,
    IN    p_titulo              character varying(200)   DEFAULT NULL,
    IN    p_fk_encuesta         bigint                   DEFAULT NULL,
    IN    p_lugar               character varying(200)   DEFAULT NULL,
    IN    p_enlace_virtual      character varying(300)   DEFAULT NULL,
    IN    p_cupo                integer                  DEFAULT NULL,
    IN    p_estado              character varying(15)    DEFAULT 'PROGRAMADA',
    -- Detalle: objeto JSON opcional. NULL = no se emite enlace.
    IN    p_enlace              jsonb                    DEFAULT NULL,
    -- Salida: la sesión como quedó (releída) + 'id_enlace' (null si no se emitió).
    INOUT p_resultado           json                     DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_sesion    json;
    v_id_enlace bigint;
BEGIN
    -- 1) Maestro (reutiliza sp_crear_sesion: el trigger pone el consecutivo).
    CALL catedras.sp_crear_sesion(p_fk_catedra, p_inicio, p_fin, p_fk_modalidad, p_fk_sede,
                                  p_fk_periodo, p_numero_reunion, p_numero_reunion_asis,
                                  p_titulo, p_fk_encuesta, p_lugar, p_enlace_virtual,
                                  p_cupo, COALESCE(p_estado, 'PROGRAMADA'), v_sesion);

    -- 2) Detalle: el primer enlace de registro, en el MISMO COMMIT.
    IF p_enlace IS NOT NULL AND jsonb_typeof(p_enlace) = 'object' THEN
        CALL catedras.sp_emitir_enlace(
            (v_sesion ->> 'id_sesion')::bigint,
            (p_enlace ->> 'minutos_antes')::integer,     -- NULL = el del parámetro
            (p_enlace ->> 'minutos_despues')::integer,
            (p_enlace ->> 'fk_usuario')::bigint,
            v_id_enlace);
    END IF;

    p_resultado := (v_sesion::jsonb || jsonb_build_object('id_enlace', v_id_enlace))::json;
END;
$$;

COMMENT ON PROCEDURE catedras.sp_guardar_sesion IS
    'Guía IA · Regla 1. Crea una sesión y, opcionalmente, emite su primer enlace de '
    'registro, en una sola transacción. Reutiliza sp_crear_sesion y sp_emitir_enlace. '
    'Devuelve la sesión releída + id_enlace.';


-- ----- migracion 045_geolocalizacion_registro.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/045_geolocalizacion_registro.sql
--  Proyecto Cátedras · SUBVERSIÓN v7.1 · RF-S1 (geolocalización)
-- --------------------------------------------------------------------------
--  Al autorregistrar la asistencia (paso 2 de /registro/<token>) el
--  navegador entrega la ubicación del asistente. Esta migración:
--
--   1) agrega 'latitud' y 'longitud' a registro_asistencia, con su rango
--      (-90..90 / -180..180) y la regla "las dos o ninguna";
--   2) agrega 'sp_validar_clave_y_registrar_geo' y 'fn_validar_clave_geo_api',
--      las versiones con ubicación de las dos rutinas que trae el script
--      provisto. NO se modifican las originales (el script provisto se monta,
--      no se edita): quedan para quien no necesite ubicación;
--   3) reemplaza listar / consultar / crear de la migración 037 para que el
--      JSON de la fila incluya 'latitud' y 'longitud'.
--
--  DECISIÓN (RF-S1): la ubicación es OBLIGATORIA en el camino del
--  autorregistro. Se exige en tres sitios, de afuera hacia adentro:
--    · el front no deja enviar el formulario sin coordenadas;
--    · la API rechaza la petición si faltan (422);
--    · sp_validar_clave_y_registrar_geo lanza excepción si llegan nulas
--      (la API no confía en el front, ni la BD en la API).
--  Las columnas siguen siendo NULABLES porque los registros MANUAL e
--  IMPORTADO (los crea el administrador, sin navegador) no tienen ubicación,
--  y porque ya existen filas anteriores a esta migración.
--
--  'numeric(9,6)': seis decimales son ~0,11 m, más precisión de la que
--  entrega cualquier GPS de teléfono.
--
--  Idempotente (IF NOT EXISTS / CREATE OR REPLACE): se puede ejecutar varias
--  veces. Si conservas el volumen de PostgreSQL hay que aplicarla a mano
--  (ver README, "Subversión v7.1").
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  1) Columnas y restricciones
-- --------------------------------------------------------------------------
ALTER TABLE catedras.registro_asistencia
    ADD COLUMN IF NOT EXISTS latitud  numeric(9,6),
    ADD COLUMN IF NOT EXISTS longitud numeric(9,6);

ALTER TABLE catedras.registro_asistencia
    DROP CONSTRAINT IF EXISTS chk_registro_latitud,
    DROP CONSTRAINT IF EXISTS chk_registro_longitud,
    DROP CONSTRAINT IF EXISTS chk_registro_geo_par;

ALTER TABLE catedras.registro_asistencia
    ADD CONSTRAINT chk_registro_latitud  CHECK (latitud  IS NULL OR latitud  BETWEEN  -90 AND  90),
    ADD CONSTRAINT chk_registro_longitud CHECK (longitud IS NULL OR longitud BETWEEN -180 AND 180),
    ADD CONSTRAINT chk_registro_geo_par  CHECK ((latitud IS NULL) = (longitud IS NULL));

COMMENT ON COLUMN catedras.registro_asistencia.latitud IS
    'RF-S1 (v7.1). Latitud en grados que entregó el navegador al autorregistrarse. NULL en registros MANUAL / IMPORTADO y anteriores a la v7.1. Va siempre junto a longitud.';
COMMENT ON COLUMN catedras.registro_asistencia.longitud IS
    'RF-S1 (v7.1). Longitud en grados que entregó el navegador al autorregistrarse. NULL en registros MANUAL / IMPORTADO y anteriores a la v7.1.';

-- --------------------------------------------------------------------------
--  2) Validar la clave y registrar, CON ubicación
--     Mismo cuerpo que sp_validar_clave_y_registrar (script provisto) más la
--     guarda de la ubicación y las dos columnas en el INSERT. La guarda va
--     PRIMERO: una petición sin ubicación no gasta un intento de la clave.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_validar_clave_y_registrar_geo(
    IN    p_id_clave    bigint,
    IN    p_clave       text,
    IN    p_latitud     numeric,
    IN    p_longitud    numeric,
    IN    p_ip          inet DEFAULT NULL,
    IN    p_user_agent  text DEFAULT NULL,
    INOUT p_id_registro bigint DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    c              record;
    v_max_intentos integer;
    v_sesion       bigint;
    v_canal        varchar(10);
BEGIN
    -- RF-S1 · sin ubicación no hay autorregistro
    IF p_latitud IS NULL OR p_longitud IS NULL THEN
        RAISE EXCEPTION 'RF-S1: la ubicación (latitud y longitud) es obligatoria para registrar la asistencia'
            USING ERRCODE = 'check_violation';
    END IF;
    IF p_latitud NOT BETWEEN -90 AND 90 OR p_longitud NOT BETWEEN -180 AND 180 THEN
        RAISE EXCEPTION 'RF-S1: la ubicación está fuera de rango (latitud -90..90, longitud -180..180)'
            USING ERRCODE = 'check_violation';
    END IF;

    SELECT * INTO c FROM clave_acceso WHERE id_clave = p_id_clave FOR UPDATE;

    IF c IS NULL THEN
        RAISE EXCEPTION 'No existe la clave %', p_id_clave USING ERRCODE = 'no_data_found';
    END IF;

    -- RN-20 · una clave usada no se reutiliza
    IF c.usado_en IS NOT NULL THEN
        RAISE EXCEPTION 'RN-20: esa clave ya se uso el %', c.usado_en
            USING ERRCODE = 'check_violation';
    END IF;

    IF now() > c.expira_en THEN
        RAISE EXCEPTION 'RN-18: la clave expiro el %', c.expira_en
            USING ERRCODE = 'check_violation';
    END IF;

    v_max_intentos := coalesce((SELECT valor::int FROM parametro WHERE clave = 'CLAVE_MAX_INTENTOS'), 3);

    IF c.clave_hash IS DISTINCT FROM digest(p_clave || c.enviado_a::text, 'sha256') THEN
        UPDATE clave_acceso SET intentos = intentos + 1 WHERE id_clave = p_id_clave;
        IF c.intentos + 1 >= v_max_intentos THEN
            UPDATE clave_acceso SET expira_en = now() WHERE id_clave = p_id_clave;
            RAISE EXCEPTION 'RN-18: se agotaron los % intentos; la clave queda anulada', v_max_intentos
                USING ERRCODE = 'check_violation';
        END IF;
        RAISE EXCEPTION 'Clave incorrecta. Intento % de %', c.intentos + 1, v_max_intentos
            USING ERRCODE = 'check_violation';
    END IF;

    SELECT e.fk_sesion, e.canal INTO v_sesion, v_canal
      FROM enlace_registro e WHERE e.id_enlace = c.fk_enlace;

    -- El disparador de registro hace el resto: ventana, instantaneas y cupo
    INSERT INTO registro_asistencia (fk_sesion, fk_asistente, fk_enlace, fk_clave,
                                     fk_vinculacion_snapshot, origen, ip, user_agent,
                                     latitud, longitud)
    VALUES (v_sesion, c.fk_asistente, c.fk_enlace, p_id_clave,
            NULL, v_canal, p_ip, left(p_user_agent, 300),
            p_latitud, p_longitud)
    RETURNING id_registro INTO p_id_registro;

    UPDATE clave_acceso SET usado_en = now() WHERE id_clave = p_id_clave;
END;
$$;

CREATE OR REPLACE FUNCTION catedras.fn_validar_clave_geo_api(
    p_id_clave   bigint,
    p_clave      text,
    p_latitud    numeric,
    p_longitud   numeric,
    p_ip         inet DEFAULT NULL,
    p_user_agent text DEFAULT NULL
) RETURNS bigint
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_registro bigint;
BEGIN
    CALL catedras.sp_validar_clave_y_registrar_geo(
        p_id_clave, p_clave, p_latitud, p_longitud, p_ip, p_user_agent, v_registro);
    RETURN v_registro;
END;
$$;

COMMENT ON PROCEDURE catedras.sp_validar_clave_y_registrar_geo IS
    'v7.1 RF-S1. Como sp_validar_clave_y_registrar, pero exige y guarda latitud/longitud. Es el que usa la API.';
COMMENT ON FUNCTION catedras.fn_validar_clave_geo_api IS
    'v7.1 RF-S1. Envoltorio de sp_validar_clave_y_registrar_geo que devuelve el id_registro.';

-- --------------------------------------------------------------------------
--  3) Listar / consultar / crear de la migración 037, ahora con ubicación
--     (misma firma: CREATE OR REPLACE los sustituye sin tocar la API).
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_listar_registro_asistencia(
    IN    p_fk_sesion    bigint  DEFAULT NULL,
    IN    p_fk_asistente bigint  DEFAULT NULL,
    IN    p_limite       integer DEFAULT NULL,
    INOUT p_resultado    json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(r), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_registro, fk_sesion, fk_asistente, fk_enlace, fk_clave,
                   registrado_en, fk_programa_snapshot, fk_vinculacion_snapshot,
                   origen, host(ip) AS ip, user_agent, latitud, longitud
              FROM registro_asistencia
             WHERE (p_fk_sesion    IS NULL OR fk_sesion    = p_fk_sesion)
               AND (p_fk_asistente IS NULL OR fk_asistente = p_fk_asistente)
             ORDER BY id_registro
             LIMIT p_limite) r;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_consultar_registro_asistencia(
    IN    p_id_registro bigint,
    INOUT p_resultado   json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(r) INTO p_resultado
      FROM (SELECT id_registro, fk_sesion, fk_asistente, fk_enlace, fk_clave,
                   registrado_en, fk_programa_snapshot, fk_vinculacion_snapshot,
                   origen, host(ip) AS ip, user_agent, latitud, longitud
              FROM registro_asistencia
             WHERE id_registro = p_id_registro) r;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_crear_registro_asistencia(
    IN    p_fk_sesion    bigint,
    IN    p_fk_asistente bigint,
    IN    p_origen       character varying(15) DEFAULT 'QR',
    INOUT p_resultado    json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_id bigint;
BEGIN
    INSERT INTO registro_asistencia (fk_sesion, fk_asistente, origen)
    VALUES (p_fk_sesion, p_fk_asistente, COALESCE(p_origen, 'QR'))
    RETURNING id_registro INTO v_id;

    SELECT row_to_json(r) INTO p_resultado
      FROM (SELECT id_registro, fk_sesion, fk_asistente, fk_enlace, fk_clave,
                   registrado_en, fk_programa_snapshot, fk_vinculacion_snapshot,
                   origen, host(ip) AS ip, user_agent, latitud, longitud
              FROM registro_asistencia
             WHERE id_registro = v_id) r;
END;
$$;

-- sp_actualizar_registro_asistencia (037) NO cambia: la ubicación es parte
-- del HECHO y, como las instantáneas, no se corrige después. Su RETURNING
-- JSON no trae latitud/longitud, pero la API no lo expone (037).


-- ----- migracion 046_roles_estudiante_ponente.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/046_roles_estudiante_ponente.sql
--  Proyecto Cátedras · SUBVERSIÓN v7.1 · RF-S3 (cuentas de estudiante y ponente)
-- --------------------------------------------------------------------------
--  El script provisto solo siembra tres roles: ADMIN, COORDINADOR y CONSULTA.
--  Para que un estudiante o un ponente pueda iniciar sesión con su propio
--  panel hacen falta dos códigos más en el catálogo 'rol'. Una cuenta
--  (usuario) queda ligada a su asistente; el rol se le asigna con una fila
--  de rol_por_usuario, igual que hoy se hace con ADMIN desde
--  /admin/rol-por-usuario.
--
--  Idempotente: ON CONFLICT DO NOTHING.
-- ==========================================================================

SET search_path TO catedras, public;

INSERT INTO catedras.rol (id_rol, nombre, descripcion) VALUES
    ('ESTUDIANTE', 'Estudiante', 'Ve sus sesiones y su información en el panel de estudiante'),
    ('PONENTE',    'Ponente',    'Ve su calendario de sesiones en el panel de ponente')
ON CONFLICT DO NOTHING;


-- ----- migracion 047_codigo_recuperacion.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/047_codigo_recuperacion.sql
--  Proyecto Cátedras · SUBVERSIÓN v7.1 · RF-S5 / RF-S6 (recuperar la clave)
-- --------------------------------------------------------------------------
--  FLUJO "OLVIDÉ MI CLAVE" (el de "cambiar la clave sabiendo la actual" no
--  usa nada de esto: la API verifica la clave actual con BCrypt).
--
--    1) sp_solicitar_recuperacion(identificador, ip)
--         identificador = nombre de usuario O correo de la persona.
--         Genera un código de un solo uso, guarda SOLO su hash y devuelve el
--         código EN CLARO para que el front lo envíe por correo (RNF12: es la
--         única vez que existe). Si no hay una cuenta activa devuelve todo en
--         NULL y la API responde igual que si la hubiera (no se revela qué
--         usuarios existen, ADR D27).
--    2) sp_restablecer_clave(id_codigo, codigo, clave_hash)
--         Valida el código (no usado, no vencido, intentos) y cambia
--         usuario.clave_hash. El hash BCrypt lo calcula ServicioRecuperacionClave
--         ANTES de llamar (Guía IA, Regla 3): aquí llega como cadena opaca.
--
--  ¿POR QUÉ UNA TABLA NUEVA Y NO 'clave_acceso'? clave_acceso es del
--  autorregistro (v5): su 'fk_enlace' es NOT NULL (el código nace de un
--  enlace de una sesión) y sp_validar_clave_y_registrar inserta un
--  registro_asistencia. Un código de recuperación no tiene enlace ni sesión;
--  forzarlo ahí exigiría relajar una tabla del script provisto. Se copia el
--  PATRÓN (hash con sal, vigencia, intentos, usado_en) y los parámetros
--  CLAVE_VIGENCIA_MINUTOS / CLAVE_MAX_INTENTOS / CLAVE_LONGITUD, no la tabla.
--  Es una tabla de apoyo (como clave_acceso): la API no la expone como CRUD,
--  por eso el mapa sigue en 37 recursos.
--
--  Idempotente. Si conservas el volumen de PostgreSQL se aplica a mano.
-- ==========================================================================

SET search_path TO catedras, public;

CREATE TABLE IF NOT EXISTS catedras.codigo_recuperacion (
    id_codigo    bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    fk_usuario   bigint NOT NULL
                 REFERENCES catedras.usuario (id_usuario) ON DELETE CASCADE,
    codigo_hash  bytea  NOT NULL,
    enviado_a    public.citext NOT NULL,
    generado_en  timestamptz NOT NULL DEFAULT now(),
    expira_en    timestamptz NOT NULL,
    usado_en     timestamptz,
    intentos     smallint NOT NULL DEFAULT 0,
    ip_solicitud inet,
    CONSTRAINT chk_codrec_correo   CHECK (enviado_a OPERATOR(public.~~) '%@%.%'::public.citext),
    CONSTRAINT chk_codrec_expira   CHECK (expira_en > generado_en),
    CONSTRAINT chk_codrec_intentos CHECK (intentos >= 0 AND intentos <= 10)
);

CREATE INDEX IF NOT EXISTS ix_codrec_usuario ON catedras.codigo_recuperacion (fk_usuario);

COMMENT ON TABLE catedras.codigo_recuperacion IS
    'v7.1. Códigos de un solo uso para recuperar una clave olvidada. Guarda el hash (con sal), nunca el código.';

-- --------------------------------------------------------------------------
--  SOLICITAR
--  Resuelve la cuenta por nombre de usuario o por correo (institucional o
--  personal) y anula los códigos anteriores todavía vivos de esa cuenta.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_solicitar_recuperacion(
    IN    p_identificador text,
    IN    p_ip            inet DEFAULT NULL,
    INOUT p_id_codigo     bigint DEFAULT NULL,
    INOUT p_codigo        text   DEFAULT NULL,
    INOUT p_enviado_a     text   DEFAULT NULL,
    INOUT p_usuario       text   DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_id_usuario bigint;
    v_vigencia   integer;
    v_longitud   integer;
    v_azar       bytea;
BEGIN
    -- Sin cuenta activa: todo queda en NULL (la API responde igual).
    SELECT u.id_usuario, u.usuario,
           COALESCE(a.correo_institucional, a.correo_personal)::text
      INTO v_id_usuario, p_usuario, p_enviado_a
      FROM usuario u
      JOIN asistente a ON a.id_asistente = u.fk_asistente
     WHERE u.activo
       AND (u.usuario = lower(btrim(p_identificador))
            OR a.correo_institucional = btrim(p_identificador)::citext
            OR a.correo_personal      = btrim(p_identificador)::citext)
     ORDER BY (u.usuario = lower(btrim(p_identificador))) DESC, u.id_usuario
     LIMIT 1;

    IF v_id_usuario IS NULL THEN
        p_id_codigo := NULL;
        p_codigo    := NULL;
        p_enviado_a := NULL;
        p_usuario   := NULL;
        RETURN;
    END IF;

    v_vigencia := coalesce((SELECT valor::int FROM parametro WHERE clave = 'CLAVE_VIGENCIA_MINUTOS'), 10);
    v_longitud := coalesce((SELECT valor::int FROM parametro WHERE clave = 'CLAVE_LONGITUD'), 6);

    -- Un solo código vivo por cuenta: pedir otro anula el anterior.
    -- clock_timestamp() y no now(): now() es el inicio de la transacción y
    -- chk_codrec_expira exige expira_en > generado_en.
    UPDATE codigo_recuperacion
       SET expira_en = clock_timestamp()
     WHERE fk_usuario = v_id_usuario AND usado_en IS NULL AND expira_en > now();

    -- gen_random_bytes (pgcrypto) en vez de random(): es un secreto. Se
    -- toman 4 bytes (0..2^32-1) y se reducen a 'v_longitud' dígitos; el sesgo
    -- del módulo es despreciable para un código que vence en minutos.
    v_azar   := gen_random_bytes(4);
    p_codigo := lpad(
        ((get_byte(v_azar, 0)::bigint * 16777216 + get_byte(v_azar, 1)::bigint * 65536
          + get_byte(v_azar, 2)::bigint * 256 + get_byte(v_azar, 3)::bigint)
         % (10::numeric ^ v_longitud)::bigint)::text,
        v_longitud, '0');

    INSERT INTO codigo_recuperacion (fk_usuario, codigo_hash, enviado_a, expira_en, ip_solicitud)
    VALUES (v_id_usuario,
            digest(p_codigo || p_enviado_a, 'sha256'),     -- con sal, como RN-34
            p_enviado_a::citext,
            now() + make_interval(mins => v_vigencia),
            p_ip)
    RETURNING id_codigo INTO p_id_codigo;
END;
$$;

-- --------------------------------------------------------------------------
--  RESTABLECER
--  Devuelve un ESTADO en 'p_resultado' en vez de lanzar una excepción:
--    OK · NO_EXISTE · USADO · EXPIRADO · INCORRECTO · AGOTADO
--
--  ⚠️ POR QUÉ NO SE USA RAISE EXCEPTION AQUÍ (a diferencia de
--  sp_validar_clave_y_registrar, v5). Una excepción deshace TODA la
--  transacción del CALL, incluido el 'intentos + 1' que acaba de escribirse:
--  los intentos fallidos nunca se acumulan y el tope de CLAVE_MAX_INTENTOS
--  no se aplica (es el "límite conocido" RF153 de la v5). En un registro de
--  asistencia es tolerable; en una RECUPERACIÓN DE CLAVE no: con un código de
--  6 dígitos y reintentos ilimitados durante la vigencia se puede adivinar.
--  Devolviendo el estado, el CALL termina bien, el contador SE GUARDA y la
--  API traduce el estado a un 400.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_restablecer_clave(
    IN    p_id_codigo  bigint,
    IN    p_codigo     text,
    IN    p_clave_hash character varying(255),
    INOUT p_resultado  text DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    c              record;
    v_max_intentos integer;
BEGIN
    SELECT * INTO c FROM codigo_recuperacion WHERE id_codigo = p_id_codigo FOR UPDATE;

    IF c IS NULL THEN
        p_resultado := 'NO_EXISTE';
        RETURN;
    END IF;

    IF c.usado_en IS NOT NULL THEN
        p_resultado := 'USADO';
        RETURN;
    END IF;

    -- clock_timestamp(): un código anulado en esta misma transacción ya cuenta como vencido.
    IF clock_timestamp() > c.expira_en THEN
        p_resultado := 'EXPIRADO';
        RETURN;
    END IF;

    v_max_intentos := coalesce((SELECT valor::int FROM parametro WHERE clave = 'CLAVE_MAX_INTENTOS'), 3);

    IF c.codigo_hash IS DISTINCT FROM digest(p_codigo || c.enviado_a::text, 'sha256') THEN
        UPDATE codigo_recuperacion SET intentos = intentos + 1 WHERE id_codigo = p_id_codigo;
        IF c.intentos + 1 >= v_max_intentos THEN
            UPDATE codigo_recuperacion SET expira_en = clock_timestamp() WHERE id_codigo = p_id_codigo;
            p_resultado := 'AGOTADO';
        ELSE
            p_resultado := 'INCORRECTO';
        END IF;
        RETURN;
    END IF;

    UPDATE usuario SET clave_hash = p_clave_hash WHERE id_usuario = c.fk_usuario;
    UPDATE codigo_recuperacion SET usado_en = now() WHERE id_codigo = p_id_codigo;
    p_resultado := 'OK';
END;
$$;

COMMENT ON PROCEDURE catedras.sp_solicitar_recuperacion IS
    'v7.1. Genera un código de un solo uso para recuperar la clave. Todo NULL = no hay cuenta activa.';
COMMENT ON PROCEDURE catedras.sp_restablecer_clave IS
    'v7.1. Valida el código y cambia usuario.clave_hash (el hash BCrypt llega calculado). Devuelve un estado, no lanza excepción, para que los intentos fallidos SE GUARDEN.';


-- ----- migracion 049_inscripcion_catedra.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/049_inscripcion_catedra.sql
--  Proyecto Cátedras · SUBVERSIÓN v7.1 · D50 (el estudiante se inscribe a una cátedra)
-- --------------------------------------------------------------------------
--  Hasta aquí el estudiante solo podía VER el catálogo; "inscribirse" no
--  existía en el modelo. Esta migración agrega la inscripción a una CÁTEDRA
--  (no a una sesión: el cupo y la asistencia siguen siendo de la sesión).
--
--    · UNA fila por (cátedra, persona). Cancelar no borra: pone estado
--      CANCELADA y fecha; volver a inscribirse la reactiva. Así queda el rastro.
--    · La inscripción NO es requisito para registrar la asistencia (v5): quien
--      ya se registra con su enlace/clave sigue pudiendo hacerlo. Es la lista
--      de "mis cátedras" del estudiante y de quiénes siguen cada cátedra.
--
--  Los procedimientos devuelven un ESTADO (texto) en vez de lanzar excepciones
--  para los casos de negocio (cátedra inactiva, ya inscrito...): la API los
--  traduce a 200/400/404 sin depender de un 500 (como 047).
--
--  Idempotente. Si conservas el volumen de PostgreSQL se aplica a mano.
-- ==========================================================================

SET search_path TO catedras, public;

CREATE TABLE IF NOT EXISTS catedras.inscripcion_catedra (
    id_inscripcion bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    fk_catedra     bigint NOT NULL REFERENCES catedras.catedra (id_catedra),
    fk_asistente   bigint NOT NULL REFERENCES catedras.asistente (id_asistente),
    estado         text   NOT NULL DEFAULT 'ACTIVA',
    inscrita_en    timestamptz NOT NULL DEFAULT now(),
    cancelada_en   timestamptz,
    CONSTRAINT uq_inscripcion_catedra UNIQUE (fk_catedra, fk_asistente),
    CONSTRAINT chk_inscripcion_estado CHECK (estado IN ('ACTIVA', 'CANCELADA')),
    CONSTRAINT chk_inscripcion_fechas CHECK (
        (estado = 'ACTIVA' AND cancelada_en IS NULL) OR (estado = 'CANCELADA' AND cancelada_en IS NOT NULL))
);

CREATE INDEX IF NOT EXISTS ix_inscripcion_asistente ON catedras.inscripcion_catedra (fk_asistente);

COMMENT ON TABLE catedras.inscripcion_catedra IS
    'v7.1. Inscripción de una persona a una cátedra. Cancelar conserva la fila (estado CANCELADA).';

-- --------------------------------------------------------------------------
--  INSCRIBIR  ->  INSCRITA | YA_INSCRITA | CATEDRA_NO_DISPONIBLE | PERSONA_NO_DISPONIBLE
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_inscribir_catedra(
    IN    p_fk_catedra   bigint,
    IN    p_fk_asistente bigint,
    INOUT p_resultado    text DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_estado text;
BEGIN
    IF NOT EXISTS (SELECT 1 FROM catedra WHERE id_catedra = p_fk_catedra AND activo) THEN
        p_resultado := 'CATEDRA_NO_DISPONIBLE';
        RETURN;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM asistente WHERE id_asistente = p_fk_asistente AND activo) THEN
        p_resultado := 'PERSONA_NO_DISPONIBLE';
        RETURN;
    END IF;

    SELECT estado INTO v_estado
      FROM inscripcion_catedra
     WHERE fk_catedra = p_fk_catedra AND fk_asistente = p_fk_asistente
       FOR UPDATE;

    IF NOT FOUND THEN
        INSERT INTO inscripcion_catedra (fk_catedra, fk_asistente) VALUES (p_fk_catedra, p_fk_asistente);
        p_resultado := 'INSCRITA';
    ELSIF v_estado = 'CANCELADA' THEN
        UPDATE inscripcion_catedra
           SET estado = 'ACTIVA', cancelada_en = NULL, inscrita_en = now()
         WHERE fk_catedra = p_fk_catedra AND fk_asistente = p_fk_asistente;
        p_resultado := 'INSCRITA';
    ELSE
        p_resultado := 'YA_INSCRITA';
    END IF;
END;
$$;

-- --------------------------------------------------------------------------
--  CANCELAR  ->  CANCELADA | NO_INSCRITA
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_cancelar_inscripcion_catedra(
    IN    p_fk_catedra   bigint,
    IN    p_fk_asistente bigint,
    INOUT p_resultado    text DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE inscripcion_catedra
       SET estado = 'CANCELADA', cancelada_en = now()
     WHERE fk_catedra = p_fk_catedra AND fk_asistente = p_fk_asistente AND estado = 'ACTIVA';

    p_resultado := CASE WHEN FOUND THEN 'CANCELADA' ELSE 'NO_INSCRITA' END;
END;
$$;

-- --------------------------------------------------------------------------
--  LISTAR  (filtros opcionales; arreglo JSON, '[]' si no hay)
--  Trae el nombre de la cátedra y de la persona para no pedirlos aparte.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_listar_inscripcion_catedra(
    IN    p_fk_asistente bigint  DEFAULT NULL,
    IN    p_fk_catedra   bigint  DEFAULT NULL,
    IN    p_solo_activas boolean DEFAULT true,
    IN    p_limite       integer DEFAULT NULL,
    INOUT p_resultado    json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(r), '[]'::json)
      INTO p_resultado
      FROM (SELECT i.id_inscripcion, i.fk_catedra, c.id_evento_asis, c.nombre AS nombre_catedra,
                   i.fk_asistente, a.id_asis, a.nombres, a.apellidos,
                   i.estado, i.inscrita_en, i.cancelada_en
              FROM inscripcion_catedra i
              JOIN catedra   c ON c.id_catedra   = i.fk_catedra
              JOIN asistente a ON a.id_asistente = i.fk_asistente
             WHERE (p_fk_asistente IS NULL OR i.fk_asistente = p_fk_asistente)
               AND (p_fk_catedra   IS NULL OR i.fk_catedra   = p_fk_catedra)
               AND (NOT COALESCE(p_solo_activas, true) OR i.estado = 'ACTIVA')
             ORDER BY i.inscrita_en DESC, i.id_inscripcion DESC
             LIMIT p_limite) r;
END;
$$;

COMMENT ON PROCEDURE catedras.sp_inscribir_catedra IS
    'v7.1. Inscribe (o reactiva) a una persona en una cátedra activa. Devuelve un estado, no lanza.';
COMMENT ON PROCEDURE catedras.sp_cancelar_inscripcion_catedra IS
    'v7.1. Cancela una inscripción ACTIVA. CANCELADA | NO_INSCRITA.';
COMMENT ON PROCEDURE catedras.sp_listar_inscripcion_catedra IS
    'v7.1. Inscripciones con nombre de cátedra y de persona; por defecto solo las ACTIVAS.';


-- ----- migracion 050_disponibilidad_sesiones.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/050_disponibilidad_sesiones.sql
--  Proyecto Cátedras · SUBVERSIÓN v7.1 · D52 (disponibilidad de horario)
-- --------------------------------------------------------------------------
--  Para que un ponente vea si una fecha/hora está libre ANTES de crear una
--  cátedra, y para que no se crucen dos sesiones. Solo CONSULTAS (no crea ni
--  cambia datos):
--
--    sp_sesiones_en_conflicto(inicio, fin, sede, lugar, ponente, excluir_sesion)
--      Las sesiones que se TRASLAPAN con ese horario (inicio < fin_otra Y
--      fin > inicio_otra: terminar a las 10:00 y empezar a las 10:00 NO cruza)
--      y chocan por alguno de estos motivos:
--        LUGAR   misma sede FÍSICA y el mismo lugar (salón). Si el lugar de
--                alguna de las dos no está escrito, se cuenta como choque: es
--                lo prudente cuando no se sabe en qué salón es. Una sede
--                virtual (sede.es_virtual) no se revisa: no tiene salón.
--        PONENTE la persona indicada ya participa (en cualquier rol) en esa
--                otra sesión: nadie puede estar en dos sesiones a la vez.
--      Sin sede (NULL) solo se revisa el motivo PONENTE; sin ponente, solo
--      LUGAR. 'excluir_sesion' deja fuera una sesión (la que se está asignando).
--
--    sp_agenda_sesiones(desde, hasta, sede)
--      Todas las sesiones que tocan ese rango (p. ej. un día), con el nombre
--      de la cátedra y de la sede, para pintar la ocupación de una fecha.
--
--  No se tocan sp_guardar_catedra/sp_guardar_sesion del repositorio: la
--  regla la aplica la API/el front antes de guardar. Idempotente.
-- ==========================================================================

SET search_path TO catedras, public;

CREATE OR REPLACE PROCEDURE catedras.sp_sesiones_en_conflicto(
    IN    p_inicio         timestamptz,
    IN    p_fin            timestamptz,
    IN    p_fk_sede        text   DEFAULT NULL,
    IN    p_lugar          text   DEFAULT NULL,
    IN    p_fk_asistente   bigint DEFAULT NULL,
    IN    p_excluir_sesion bigint DEFAULT NULL,
    INOUT p_resultado      json   DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_sede_fisica boolean;
    v_lugar       text := lower(btrim(coalesce(p_lugar, '')));
BEGIN
    -- ¿La sede pedida tiene salones? (una sede virtual o desconocida no se revisa por lugar)
    SELECT NOT s.es_virtual INTO v_sede_fisica FROM sede s WHERE s.id_sede = p_fk_sede;
    v_sede_fisica := coalesce(v_sede_fisica, false);

    SELECT COALESCE(json_agg(r ORDER BY r.inicio, r.id_sesion), '[]'::json)
      INTO p_resultado
      FROM (
            SELECT s.id_sesion, s.fk_catedra, c.nombre AS nombre_catedra, s.numero_reunion,
                   s.inicio, s.fin, s.fk_sede, s.lugar, s.fk_modalidad, s.estado,
                   array_remove(ARRAY[
                       CASE WHEN v_sede_fisica
                             AND s.fk_sede = p_fk_sede
                             AND (v_lugar = ''
                                  OR btrim(coalesce(s.lugar, '')) = ''
                                  OR lower(btrim(s.lugar)) = v_lugar)
                            THEN 'LUGAR' END,
                       CASE WHEN p_fk_asistente IS NOT NULL
                             AND EXISTS (SELECT 1 FROM ponencia po
                                          WHERE po.fk_sesion = s.id_sesion AND po.fk_asistente = p_fk_asistente)
                            THEN 'PONENTE' END
                   ], NULL) AS motivos
              FROM sesion s
              JOIN catedra c ON c.id_catedra = s.fk_catedra
             WHERE s.inicio < p_fin
               AND s.fin    > p_inicio
               AND (p_excluir_sesion IS NULL OR s.id_sesion <> p_excluir_sesion)
           ) r
     WHERE cardinality(r.motivos) > 0;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_agenda_sesiones(
    IN    p_desde     timestamptz,
    IN    p_hasta     timestamptz,
    IN    p_fk_sede   text    DEFAULT NULL,
    IN    p_limite    integer DEFAULT 500,
    INOUT p_resultado json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(r), '[]'::json)
      INTO p_resultado
      FROM (SELECT s.id_sesion, s.fk_catedra, c.nombre AS nombre_catedra, s.numero_reunion,
                   s.inicio, s.fin, s.fk_sede, se.nombre AS nombre_sede, s.lugar, s.fk_modalidad, s.estado
              FROM sesion s
              JOIN catedra c  ON c.id_catedra = s.fk_catedra
              JOIN sede    se ON se.id_sede   = s.fk_sede
             WHERE s.inicio < p_hasta
               AND s.fin    > p_desde
               AND (p_fk_sede IS NULL OR s.fk_sede = p_fk_sede)
             ORDER BY s.inicio, s.id_sesion
             LIMIT p_limite) r;
END;
$$;

COMMENT ON PROCEDURE catedras.sp_sesiones_en_conflicto IS
    'v7.1. Sesiones que se traslapan con un horario y chocan por LUGAR (misma sede física y salón) o PONENTE. Solo lectura.';
COMMENT ON PROCEDURE catedras.sp_agenda_sesiones IS
    'v7.1. Sesiones que tocan un rango de fechas (opcionalmente de una sede). Solo lectura.';


-- ----- migracion 051_qr_sesion.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/051_qr_sesion.sql
--  Proyecto Cátedras · SUBVERSIÓN v7.1 · D53 (QR de cierre de sesión)
-- --------------------------------------------------------------------------
--  Cada sesión tiene un QR que el ponente comparte con los estudiantes AL FINAL.
--  El QR es el enlace de registro de la v5 (enlace_registro), con tres reglas
--  propias:
--
--    · VENTANA: se abre unos minutos ANTES de que termine la sesión y se cierra
--      cuando termina (parámetros QR_MINUTOS_ANTES_FIN = 15 y
--      QR_MINUTOS_DESPUES_FIN = 0; si se quiere un margen después del fin, se sube
--      el segundo). Fuera de ahí el enlace no sirve (RN-15, ya existente).
--    · INSCRIPCIÓN: enlace_registro.requiere_inscripcion = true -> solo se registra
--      quien está INSCRITO (inscripción ACTIVA) en la cátedra de la sesión. Se
--      comprueba al pedir la clave y, otra vez, al guardar el registro (disparador).
--      Los enlaces anteriores a esta migración quedan en false: siguen como antes.
--    · UBICACIÓN: la sigue exigiendo el registro (RF-S1, migración 045).
--
--  Procedimientos (los usa la API 'QrSesion'):
--    sp_emitir_qr_sesion      crea el QR de una sesión (o devuelve el vigente; con
--                             regenerar = true revoca el anterior y crea otro, RN-14)
--    sp_consultar_qr_sesion   el QR vigente de una sesión (o NULL)
--    sp_asistencia_sesion     inscritos de la cátedra y quién asistió a ESA sesión
--
--  No se edita el script provisto: la comprobación de inscripción de "pedir la
--  clave" va en una función envoltorio (fn_solicitar_clave_insc_api) que llama a
--  la original, y la de "guardar el registro" en un disparador nuevo.
--  Idempotente. Si conservas el volumen de PostgreSQL se aplica a mano.
-- ==========================================================================

SET search_path TO catedras, public;

ALTER TABLE catedras.enlace_registro
    ADD COLUMN IF NOT EXISTS requiere_inscripcion boolean NOT NULL DEFAULT false;

COMMENT ON COLUMN catedras.enlace_registro.requiere_inscripcion IS
    'v7.1. true = solo registra asistencia quien tenga inscripción ACTIVA en la cátedra de la sesión (QR de cierre).';

INSERT INTO catedras.parametro (clave, valor, tipo_dato, descripcion)
VALUES ('QR_MINUTOS_ANTES_FIN', '15', 'ENTERO',
        'v7.1. Minutos antes del FIN de la sesión en que se abre el QR de cierre de asistencia'),
       ('QR_MINUTOS_DESPUES_FIN', '0', 'ENTERO',
        'v7.1. Minutos después del FIN de la sesión en que se cierra el QR (0 = cierra cuando termina)')
ON CONFLICT (clave) DO NOTHING;

-- --------------------------------------------------------------------------
--  1) Inscripción obligatoria al GUARDAR el registro (segunda barrera).
--     Se llama 'trg_registro_inscripcion' para correr ANTES de 'trg_registro_validar'.
-- --------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION catedras.fn_trg_registro_inscripcion() RETURNS trigger
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_requiere boolean;
    v_catedra  bigint;
BEGIN
    IF NEW.fk_enlace IS NULL THEN
        RETURN NEW;                       -- registros MANUAL / IMPORTADO: no pasan por un enlace
    END IF;

    SELECT e.requiere_inscripcion INTO v_requiere FROM enlace_registro e WHERE e.id_enlace = NEW.fk_enlace;

    IF coalesce(v_requiere, false) THEN
        SELECT s.fk_catedra INTO v_catedra FROM sesion s WHERE s.id_sesion = NEW.fk_sesion;

        IF NOT EXISTS (SELECT 1 FROM inscripcion_catedra i
                        WHERE i.fk_catedra = v_catedra AND i.fk_asistente = NEW.fk_asistente
                          AND i.estado = 'ACTIVA') THEN
            RAISE EXCEPTION 'D53: debes estar inscrito en la cátedra para registrar tu asistencia con este QR'
                USING ERRCODE = 'check_violation';
        END IF;
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_registro_inscripcion ON catedras.registro_asistencia;
CREATE TRIGGER trg_registro_inscripcion BEFORE INSERT ON catedras.registro_asistencia
    FOR EACH ROW EXECUTE FUNCTION catedras.fn_trg_registro_inscripcion();

-- --------------------------------------------------------------------------
--  2) Inscripción obligatoria al PEDIR la clave (primera barrera: se avisa antes
--     de mandar un correo). Mismo resultado que fn_solicitar_clave_api.
-- --------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION catedras.fn_solicitar_clave_insc_api(
    p_identificador text,
    p_token         uuid,
    p_ip            inet DEFAULT NULL
) RETURNS TABLE(id_clave bigint, clave text, enviado_a text)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_asistente bigint;
    v_requiere  boolean;
    v_catedra   bigint;
BEGIN
    v_asistente := fn_resolver_asistente(p_identificador);

    -- Si no hay persona o enlace vigente, que lo diga la rutina original (mismos mensajes que siempre).
    IF v_asistente IS NOT NULL THEN
        SELECT e.requiere_inscripcion, s.fk_catedra
          INTO v_requiere, v_catedra
          FROM enlace_registro e
          JOIN sesion s ON s.id_sesion = e.fk_sesion
         WHERE e.token = p_token AND e.revocado_en IS NULL AND e.ventana @> now();

        IF coalesce(v_requiere, false)
           AND NOT EXISTS (SELECT 1 FROM inscripcion_catedra i
                            WHERE i.fk_catedra = v_catedra AND i.fk_asistente = v_asistente
                              AND i.estado = 'ACTIVA') THEN
            RAISE EXCEPTION 'D53: no estás inscrito en esta cátedra. Inscríbete desde el catálogo y vuelve a escanear el QR'
                USING ERRCODE = 'check_violation';
        END IF;
    END IF;

    RETURN QUERY SELECT * FROM catedras.fn_solicitar_clave_api(p_identificador, p_token, p_ip);
END;
$$;

-- --------------------------------------------------------------------------
--  3) EMITIR el QR de una sesión.  Devuelve el enlace (JSON) o NULL si la sesión no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_emitir_qr_sesion(
    IN    p_fk_sesion  bigint,
    IN    p_fk_usuario bigint  DEFAULT NULL,
    IN    p_regenerar  boolean DEFAULT false,
    INOUT p_resultado  json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_fin     timestamptz;
    v_canal   varchar(10);
    v_id      bigint;
    v_antes   integer;
    v_desp    integer;
    v_base    text;
    v_token   uuid := gen_random_uuid();
    v_existia boolean := false;
BEGIN
    SELECT s.fin, m.canal_difusion INTO v_fin, v_canal
      FROM sesion s JOIN modalidad m ON m.id_modalidad = s.fk_modalidad
     WHERE s.id_sesion = p_fk_sesion;
    IF v_fin IS NULL THEN
        p_resultado := NULL;
        RETURN;
    END IF;

    -- RN-13 (script provisto): el canal del enlace debe concordar con la modalidad de la sesión:
    -- presencial -> QR, virtual y telepresencial -> ENLACE, híbrida (AMBOS) -> QR. Se deriva igual que
    -- sp_emitir_enlace. A TODAS les corresponde el mismo código QR en pantalla (es la imagen del enlace);
    -- lo que cambia es el CANAL que se registra y el 'origen' de la asistencia (QR o ENLACE).
    v_canal := CASE WHEN v_canal = 'ENLACE' THEN 'ENLACE' ELSE 'QR' END;

    IF NOT coalesce(p_regenerar, false) THEN
        SELECT e.id_enlace INTO v_id
          FROM enlace_registro e
         WHERE e.fk_sesion = p_fk_sesion AND e.revocado_en IS NULL AND e.requiere_inscripcion
         ORDER BY e.id_enlace DESC LIMIT 1;
        v_existia := v_id IS NOT NULL;
    END IF;

    IF v_id IS NULL THEN
        v_antes := greatest(coalesce((SELECT valor::int FROM parametro WHERE clave = 'QR_MINUTOS_ANTES_FIN'), 15), 1);
        v_desp  := greatest(coalesce((SELECT valor::int FROM parametro WHERE clave = 'QR_MINUTOS_DESPUES_FIN'), 0), 0);
        v_base  := coalesce((SELECT valor FROM parametro WHERE clave = 'URL_BASE'), 'https://catedras.usbmed.edu.co/r/');

        -- RN-14 · un solo enlace vigente por sesión
        UPDATE enlace_registro SET revocado_en = now()
         WHERE fk_sesion = p_fk_sesion AND revocado_en IS NULL;

        INSERT INTO enlace_registro (fk_sesion, token, url_publica, ruta_imagen_qr, canal, ventana,
                                     fk_usuario_crea, requiere_inscripcion)
        VALUES (p_fk_sesion, v_token, v_base || v_token::text, 'qr/' || v_token::text || '.png', v_canal,
                tstzrange(v_fin - make_interval(mins => v_antes), v_fin + make_interval(mins => v_desp), '[)'),
                p_fk_usuario, true)
        RETURNING id_enlace INTO v_id;
    END IF;

    SELECT row_to_json(r) INTO p_resultado
      FROM (SELECT e.id_enlace, e.fk_sesion, e.token, e.canal, lower(e.ventana) AS ventana_desde,
                   upper(e.ventana) AS ventana_hasta, e.revocado_en, e.requiere_inscripcion,
                   v_existia AS ya_existia
              FROM enlace_registro e WHERE e.id_enlace = v_id) r;
END;
$$;

-- --------------------------------------------------------------------------
--  4) CONSULTAR el QR vigente de una sesión (NULL si no tiene).
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_consultar_qr_sesion(
    IN    p_fk_sesion bigint,
    INOUT p_resultado json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(r) INTO p_resultado
      FROM (SELECT e.id_enlace, e.fk_sesion, e.token, e.canal, lower(e.ventana) AS ventana_desde,
                   upper(e.ventana) AS ventana_hasta, e.revocado_en, e.requiere_inscripcion,
                   true AS ya_existia
              FROM enlace_registro e
             WHERE e.fk_sesion = p_fk_sesion AND e.revocado_en IS NULL AND e.requiere_inscripcion
             ORDER BY e.id_enlace DESC LIMIT 1) r;
END;
$$;

-- --------------------------------------------------------------------------
--  5) ASISTENCIA de una sesión: los inscritos en su cátedra y quién asistió a ESA sesión
--     (con la hora y la ubicación del registro). Una persona que asistió sin estar
--     inscrita (p. ej. con un enlace anterior) también sale, con inscrito = false.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_asistencia_sesion(
    IN    p_fk_sesion bigint,
    INOUT p_resultado json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_catedra bigint;
BEGIN
    SELECT s.fk_catedra INTO v_catedra FROM sesion s WHERE s.id_sesion = p_fk_sesion;

    SELECT COALESCE(json_agg(r), '[]'::json) INTO p_resultado
      FROM (SELECT a.id_asistente AS fk_asistente, a.id_asis, a.nombres, a.apellidos,
                   (i.id_inscripcion IS NOT NULL) AS inscrito,
                   (g.id_registro IS NOT NULL)    AS asistio,
                   g.registrado_en, g.latitud, g.longitud, g.origen
              FROM (SELECT ic.fk_asistente FROM inscripcion_catedra ic
                     WHERE ic.fk_catedra = v_catedra AND ic.estado = 'ACTIVA'
                    UNION
                    SELECT ra.fk_asistente FROM registro_asistencia ra WHERE ra.fk_sesion = p_fk_sesion) x
              JOIN asistente a ON a.id_asistente = x.fk_asistente
              LEFT JOIN inscripcion_catedra i
                     ON i.fk_asistente = a.id_asistente AND i.fk_catedra = v_catedra AND i.estado = 'ACTIVA'
              LEFT JOIN LATERAL (SELECT ra.id_registro, ra.registrado_en, ra.latitud, ra.longitud, ra.origen
                                   FROM registro_asistencia ra
                                  WHERE ra.fk_sesion = p_fk_sesion AND ra.fk_asistente = a.id_asistente
                                  ORDER BY ra.registrado_en LIMIT 1) g ON true
             ORDER BY (g.id_registro IS NULL), a.apellidos, a.nombres) r;
END;
$$;

COMMENT ON PROCEDURE catedras.sp_emitir_qr_sesion IS
    'v7.1. QR de cierre: ventana [fin - QR_MINUTOS_ANTES_FIN, fin + QR_MINUTOS_DESPUES_FIN), exige inscripción. NULL = la sesión no existe.';
COMMENT ON PROCEDURE catedras.sp_consultar_qr_sesion IS
    'v7.1. El QR vigente de la sesión, o NULL.';
COMMENT ON PROCEDURE catedras.sp_asistencia_sesion IS
    'v7.1. Inscritos de la cátedra y quién asistió a la sesión (con hora y ubicación).';
COMMENT ON FUNCTION catedras.fn_solicitar_clave_insc_api IS
    'v7.1. fn_solicitar_clave_api + comprobación de inscripción cuando el enlace la exige.';


-- ----- migracion 052_primer_ingreso_estudiante.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/052_primer_ingreso_estudiante.sql
--  Proyecto Cátedras · SUBVERSIÓN v7.1 · D54 (cuentas de estudiante y primer ingreso)
-- --------------------------------------------------------------------------
--  Reglas nuevas para los ESTUDIANTES:
--    · El usuario es su CORREO INSTITUCIONAL. La columna usuario.usuario sigue
--      siendo un nombre corto (su CHECK no admite '@'); el login acepta una de
--      dos formas: ese nombre, o el correo institucional de la persona.
--    · La clave inicial es su CÓDIGO de estudiante (asistente.id_asis).
--    · El PRIMER ingreso obliga a cambiarla: usuario.debe_cambiar_clave.
--
--  Las cuentas NO se crean todas de antemano (serían miles de hashes BCrypt):
--  se crean en el PRIMER ingreso. Si alguien entra con el correo institucional de
--  un estudiante vigente (vinculación ESTUDIANTE sin fecha de fin) que aún no tiene
--  cuenta, y su clave es exactamente su código, la API calcula el hash (BCrypt solo vive
--  en ServicioUsuario) y estos procedimientos crean la cuenta con el rol ESTUDIANTE y
--  debe_cambiar_clave = true. Cualquier otro intento sigue dando el mismo rechazo (D27).
--
--    sp_buscar_estudiante_sin_cuenta   candidato (id_asistente, id_asis) por correo, o NULL
--    sp_provisionar_cuenta_estudiante  crea usuario + rol ESTUDIANTE con el hash que le llega
--    sp_fijar_debe_cambiar_clave       el administrador/servicio marca o desmarca la exigencia
--    trg_usuario_clave_cambiada        al cambiar la clave (cambiar, recuperar, PUT) la
--                                      exigencia se apaga sola
--
--  Y la cuenta semilla (048) pasa a esta regla mientras no haya ingresado nunca.
--  Idempotente. Si conservas el volumen de PostgreSQL se aplica a mano.
-- ==========================================================================

SET search_path TO catedras, public;

ALTER TABLE catedras.usuario
    ADD COLUMN IF NOT EXISTS debe_cambiar_clave boolean NOT NULL DEFAULT false;

COMMENT ON COLUMN catedras.usuario.debe_cambiar_clave IS
    'v7.1. true = en el próximo ingreso debe cambiar su clave (clave inicial = código de estudiante). Se apaga al cambiarla.';

-- --------------------------------------------------------------------------
--  1) Cambiar la clave apaga la exigencia (salvo que la misma sentencia la fije).
-- --------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION catedras.fn_trg_usuario_clave_cambiada() RETURNS trigger
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    IF NEW.clave_hash IS DISTINCT FROM OLD.clave_hash
       AND NEW.debe_cambiar_clave IS NOT DISTINCT FROM OLD.debe_cambiar_clave THEN
        NEW.debe_cambiar_clave := false;
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_usuario_clave_cambiada ON catedras.usuario;
CREATE TRIGGER trg_usuario_clave_cambiada BEFORE UPDATE OF clave_hash ON catedras.usuario
    FOR EACH ROW EXECUTE FUNCTION catedras.fn_trg_usuario_clave_cambiada();

-- --------------------------------------------------------------------------
--  2) Listar / consultar / consultar por login (misma firma que la 033) con la
--     exigencia en el JSON. El login acepta el nombre de usuario O el correo
--     institucional de la persona (citext: sin importar mayúsculas).
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_listar_usuario(
    IN    p_limite    integer DEFAULT NULL,
    INOUT p_resultado json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(u), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_usuario, fk_asistente, usuario, activo, debe_cambiar_clave,
                   ultimo_acceso, creado_en
              FROM usuario
             WHERE activo
             ORDER BY id_usuario
             LIMIT p_limite) u;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_consultar_usuario(
    IN    p_id_usuario bigint,
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(u) INTO p_resultado
      FROM (SELECT id_usuario, fk_asistente, usuario, activo, debe_cambiar_clave,
                   ultimo_acceso, creado_en
              FROM usuario
             WHERE id_usuario = p_id_usuario) u;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_consultar_usuario_por_login(
    IN    p_usuario   text,
    INOUT p_resultado json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(u) INTO p_resultado
      FROM (SELECT x.id_usuario, x.fk_asistente, x.usuario, x.clave_hash, x.activo, x.debe_cambiar_clave,
                   x.ultimo_acceso, x.creado_en
              FROM usuario x
              LEFT JOIN asistente a ON a.id_asistente = x.fk_asistente
             WHERE x.usuario = p_usuario
                OR (position('@' in p_usuario) > 0 AND a.correo_institucional = p_usuario::citext)
             ORDER BY (x.usuario = p_usuario) DESC, x.id_usuario
             LIMIT 1) u;
END;
$$;

-- --------------------------------------------------------------------------
--  3) Primer ingreso de un estudiante sin cuenta.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_buscar_estudiante_sin_cuenta(
    IN    p_correo    text,
    INOUT p_resultado json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(r) INTO p_resultado
      FROM (SELECT a.id_asistente, a.id_asis
              FROM asistente a
             WHERE a.correo_institucional = btrim(p_correo)::citext
               AND a.activo
               AND a.id_asis IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM usuario u WHERE u.fk_asistente = a.id_asistente)
               AND EXISTS (SELECT 1 FROM vinculacion_asistente v
                            WHERE v.fk_asistente = a.id_asistente
                              AND v.fk_tipo_vinculacion = 'ESTUDIANTE'
                              AND v.fecha_fin IS NULL)
             ORDER BY a.id_asistente
             LIMIT 1) r;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_provisionar_cuenta_estudiante(
    IN    p_correo     text,
    IN    p_clave_hash character varying(255),
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_asistente bigint;
    v_base      text;
    v_nombre    text;
    v_n         integer := 1;
    v_id        bigint;
BEGIN
    -- La misma condición del buscador: el rechazo de "no corresponde" es NULL, sin excepción.
    SELECT a.id_asistente INTO v_asistente
      FROM asistente a
     WHERE a.correo_institucional = btrim(p_correo)::citext
       AND a.activo AND a.id_asis IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM usuario u WHERE u.fk_asistente = a.id_asistente)
       AND EXISTS (SELECT 1 FROM vinculacion_asistente v
                    WHERE v.fk_asistente = a.id_asistente AND v.fk_tipo_vinculacion = 'ESTUDIANTE' AND v.fecha_fin IS NULL)
     ORDER BY a.id_asistente
     LIMIT 1;

    IF v_asistente IS NULL THEN
        p_resultado := NULL;
        RETURN;
    END IF;

    -- Nombre corto = lo que va antes de la @, ajustado al CHECK ^[a-z0-9._-]{4,40}$ (y sin repetirse).
    v_base := regexp_replace(lower(split_part(btrim(p_correo), '@', 1)), '[^a-z0-9._-]', '.', 'g');
    -- OJO: rpad TRUNCA lo que ya es más largo que el largo pedido, por eso solo se rellena si es menor de 4.
    IF length(v_base) < 4 THEN
        v_base := rpad(v_base, 4, '0');
    END IF;
    v_base := left(v_base, 36);
    v_nombre := v_base;
    WHILE EXISTS (SELECT 1 FROM usuario WHERE usuario = v_nombre) LOOP
        v_n := v_n + 1;
        v_nombre := v_base || v_n::text;
    END LOOP;

    INSERT INTO usuario (fk_asistente, usuario, clave_hash, activo, debe_cambiar_clave)
    VALUES (v_asistente, v_nombre, p_clave_hash, true, true)
    RETURNING id_usuario INTO v_id;

    INSERT INTO rol_por_usuario (fk_usuario, fk_rol, fecha_ini)
    VALUES (v_id, 'ESTUDIANTE', current_date)
    ON CONFLICT DO NOTHING;

    SELECT row_to_json(u) INTO p_resultado
      FROM (SELECT id_usuario, fk_asistente, usuario, activo, debe_cambiar_clave, ultimo_acceso, creado_en
              FROM usuario WHERE id_usuario = v_id) u;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_fijar_debe_cambiar_clave(
    IN p_id_usuario bigint,
    IN p_valor      boolean
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE usuario SET debe_cambiar_clave = coalesce(p_valor, false) WHERE id_usuario = p_id_usuario;
END;
$$;

-- --------------------------------------------------------------------------
--  4) La cuenta semilla (048) pasa a la regla nueva si nunca ha ingresado:
--     su clave inicial es su código de estudiante y debe cambiarla al entrar.
--     (048 sigue sembrando 'Cambiar-123'; esto la reemplaza al aplicar la 052.)
-- --------------------------------------------------------------------------
UPDATE catedras.usuario u
   SET clave_hash = crypt(a.id_asis, gen_salt('bf', 10)),
       debe_cambiar_clave = true
  FROM catedras.asistente a
 WHERE a.id_asistente = u.fk_asistente
   AND u.usuario = 'alejandro.zuleta'
   AND u.ultimo_acceso IS NULL
   AND a.id_asis IS NOT NULL
   AND NOT u.debe_cambiar_clave;

COMMENT ON PROCEDURE catedras.sp_buscar_estudiante_sin_cuenta IS
    'v7.1. Estudiante vigente con ese correo institucional y sin cuenta (id_asistente, id_asis), o NULL.';
COMMENT ON PROCEDURE catedras.sp_provisionar_cuenta_estudiante IS
    'v7.1. Crea la cuenta (nombre corto del correo) con rol ESTUDIANTE y debe_cambiar_clave = true. El hash lo calcula la API.';
COMMENT ON PROCEDURE catedras.sp_fijar_debe_cambiar_clave IS
    'v7.1. Marca o desmarca la exigencia de cambiar la clave en el próximo ingreso.';


-- ----- migracion 053_qr_canal_por_modalidad.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/053_qr_canal_por_modalidad.sql
--  Proyecto Cátedras · SUBVERSIÓN v7.1 · D53 (corrección)
-- --------------------------------------------------------------------------
--  La 051 emitía TODOS los QR con canal 'QR'. El script provisto (RN-13) exige que el canal
--  del enlace concuerde con la modalidad de la sesión: presencial -> QR; virtual y telepresencial
--  -> ENLACE; híbrida (AMBOS) -> QR. Con una sesión virtual la base de datos respondía:
--     23514: RN-13: la sesion N es de modalidad que se difunde por ENLACE, no por QR
--  Ahora el canal se deriva de la modalidad (igual que sp_emitir_enlace). Todas las sesiones
--  siguen teniendo su código QR en pantalla (es la imagen del enlace); solo cambia el canal que
--  se registra y el 'origen' de la asistencia (QR o ENLACE). Además el JSON del QR trae 'canal'.
--
--  Solo reemplaza dos procedimientos (CREATE OR REPLACE): no toca datos. La 051 ya trae esta
--  misma versión para volúmenes nuevos; esta migración es para los que aplicaron la 051 antes.
-- ==========================================================================

SET search_path TO catedras, public;

CREATE OR REPLACE PROCEDURE catedras.sp_emitir_qr_sesion(
    IN    p_fk_sesion  bigint,
    IN    p_fk_usuario bigint  DEFAULT NULL,
    IN    p_regenerar  boolean DEFAULT false,
    INOUT p_resultado  json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_fin     timestamptz;
    v_canal   varchar(10);
    v_id      bigint;
    v_antes   integer;
    v_desp    integer;
    v_base    text;
    v_token   uuid := gen_random_uuid();
    v_existia boolean := false;
BEGIN
    SELECT s.fin, m.canal_difusion INTO v_fin, v_canal
      FROM sesion s JOIN modalidad m ON m.id_modalidad = s.fk_modalidad
     WHERE s.id_sesion = p_fk_sesion;
    IF v_fin IS NULL THEN
        p_resultado := NULL;
        RETURN;
    END IF;

    -- RN-13 (script provisto): el canal del enlace debe concordar con la modalidad de la sesión:
    -- presencial -> QR, virtual y telepresencial -> ENLACE, híbrida (AMBOS) -> QR. Se deriva igual que
    -- sp_emitir_enlace. A TODAS les corresponde el mismo código QR en pantalla (es la imagen del enlace);
    -- lo que cambia es el CANAL que se registra y el 'origen' de la asistencia (QR o ENLACE).
    v_canal := CASE WHEN v_canal = 'ENLACE' THEN 'ENLACE' ELSE 'QR' END;

    IF NOT coalesce(p_regenerar, false) THEN
        SELECT e.id_enlace INTO v_id
          FROM enlace_registro e
         WHERE e.fk_sesion = p_fk_sesion AND e.revocado_en IS NULL AND e.requiere_inscripcion
         ORDER BY e.id_enlace DESC LIMIT 1;
        v_existia := v_id IS NOT NULL;
    END IF;

    IF v_id IS NULL THEN
        v_antes := greatest(coalesce((SELECT valor::int FROM parametro WHERE clave = 'QR_MINUTOS_ANTES_FIN'), 15), 1);
        v_desp  := greatest(coalesce((SELECT valor::int FROM parametro WHERE clave = 'QR_MINUTOS_DESPUES_FIN'), 0), 0);
        v_base  := coalesce((SELECT valor FROM parametro WHERE clave = 'URL_BASE'), 'https://catedras.usbmed.edu.co/r/');

        -- RN-14 · un solo enlace vigente por sesión
        UPDATE enlace_registro SET revocado_en = now()
         WHERE fk_sesion = p_fk_sesion AND revocado_en IS NULL;

        INSERT INTO enlace_registro (fk_sesion, token, url_publica, ruta_imagen_qr, canal, ventana,
                                     fk_usuario_crea, requiere_inscripcion)
        VALUES (p_fk_sesion, v_token, v_base || v_token::text, 'qr/' || v_token::text || '.png', v_canal,
                tstzrange(v_fin - make_interval(mins => v_antes), v_fin + make_interval(mins => v_desp), '[)'),
                p_fk_usuario, true)
        RETURNING id_enlace INTO v_id;
    END IF;

    SELECT row_to_json(r) INTO p_resultado
      FROM (SELECT e.id_enlace, e.fk_sesion, e.token, e.canal, lower(e.ventana) AS ventana_desde,
                   upper(e.ventana) AS ventana_hasta, e.revocado_en, e.requiere_inscripcion,
                   v_existia AS ya_existia
              FROM enlace_registro e WHERE e.id_enlace = v_id) r;
END;
$$;

-- --------------------------------------------------------------------------
--  4) CONSULTAR el QR vigente de una sesión (NULL si no tiene).
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_consultar_qr_sesion(
    IN    p_fk_sesion bigint,
    INOUT p_resultado json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(r) INTO p_resultado
      FROM (SELECT e.id_enlace, e.fk_sesion, e.token, e.canal, lower(e.ventana) AS ventana_desde,
                   upper(e.ventana) AS ventana_hasta, e.revocado_en, e.requiere_inscripcion,
                   true AS ya_existia
              FROM enlace_registro e
             WHERE e.fk_sesion = p_fk_sesion AND e.revocado_en IS NULL AND e.requiere_inscripcion
             ORDER BY e.id_enlace DESC LIMIT 1) r;
END;
$$;


-- ----- migracion 054_sin_inscripcion_ni_cupo_login_admin.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/054_sin_inscripcion_ni_cupo_login_admin.sql
--  Proyecto Cátedras · SUBVERSIÓN v7.1 · D55
-- --------------------------------------------------------------------------
--  Cambio de alcance: (1) SOLO el administrador inicia sesión (estudiantes y ponentes
--  ya no tienen cuenta ni panel: la asistencia es pública, por el QR); (2) SE QUITA la
--  inscripción a las cátedras; (3) SE QUITA el cupo de las sesiones.
--
--  Qué hace esta migración (todo con CREATE OR REPLACE / IF EXISTS, es repetible):
--    cupo        fn_trg_registro_validar sin la regla RN-38 (el cupo ya no limita registros).
--                La columna sesion.cupo NO se borra: la usan los procedimientos del script
--                provisto; queda dormida y la aplicación ya no la lee ni la escribe.
--    inscripción el QR de cierre ya no exige estar inscrito: se quita el disparador y la
--                comprobación al pedir la clave; el marcador del QR de cierre pasa a
--                enlace_registro.qr_cierre; sp_asistencia_sesion lista solo a quien asistió;
--                se borran los procedimientos de inscripción y la tabla inscripcion_catedra
--                SOLO si está vacía (si tiene filas se conserva y se avisa).
--    cuentas     se borran los procedimientos que creaban la cuenta del estudiante en su
--                primer ingreso y el login vuelve a ser solo por nombre de usuario.
--  Las columnas debe_cambiar_clave y el procedimiento de «exigir cambio» siguen (sirven a
--  cualquier cuenta, p. ej. un administrador nuevo con clave temporal).
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  1) CUPO: el disparador de registro sin RN-38 (el resto, igual que el del script provisto)
-- --------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION catedras.fn_trg_registro_validar() RETURNS trigger
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_ventana        tstzrange;
    v_revocado       timestamptz;
    v_exige_programa boolean;
BEGIN
    ------------------------------------------------------------------ RN-15 --
    IF NEW.fk_enlace IS NOT NULL AND NEW.origen NOT IN ('MANUAL', 'IMPORTADO') THEN
        SELECT e.ventana, e.revocado_en
          INTO v_ventana, v_revocado
          FROM enlace_registro e
         WHERE e.id_enlace = NEW.fk_enlace;

        IF v_revocado IS NOT NULL THEN
            RAISE EXCEPTION 'RN-15: el enlace % esta revocado', NEW.fk_enlace
                USING ERRCODE = 'check_violation';
        END IF;

        IF NOT (v_ventana @> NEW.registrado_en) THEN
            RAISE EXCEPTION
                'RN-15: % esta fuera de la ventana % del enlace %',
                NEW.registrado_en, v_ventana, NEW.fk_enlace
                USING ERRCODE = 'check_violation';
        END IF;
    END IF;

    ------------------------------------------------------------------ RN-22 --
    -- Instantanea de la VINCULACION vigente
    IF NEW.fk_vinculacion_snapshot IS NULL THEN
        SELECT v.fk_tipo_vinculacion
          INTO NEW.fk_vinculacion_snapshot
          FROM vinculacion_asistente v
         WHERE v.fk_asistente = NEW.fk_asistente
           AND v.fecha_fin IS NULL
         ORDER BY v.fecha_ini DESC
         LIMIT 1;

        IF NEW.fk_vinculacion_snapshot IS NULL THEN
            RAISE EXCEPTION
                'El asistente % no tiene ninguna vinculacion vigente',
                NEW.fk_asistente USING ERRCODE = 'check_violation';
        END IF;
    END IF;

    SELECT tv.exige_programa INTO v_exige_programa
      FROM tipo_vinculacion tv
     WHERE tv.id_tipo_vinculacion = NEW.fk_vinculacion_snapshot;

    -- Instantanea del PROGRAMA vigente, solo si la vinculacion lo exige
    IF NEW.fk_programa_snapshot IS NULL AND v_exige_programa THEN
        SELECT pa.fk_programa
          INTO NEW.fk_programa_snapshot
          FROM programa_asistente pa
          JOIN periodo_academico p ON p.id_periodo = pa.fk_periodo
         WHERE pa.fk_asistente = NEW.fk_asistente
           AND pa.estado = 'ACTIVO'
         ORDER BY p.fecha_ini DESC
         LIMIT 1;
    END IF;

    ------------------------------------------------------------------ RN-07 --
    IF v_exige_programa AND NEW.fk_programa_snapshot IS NULL THEN
        RAISE EXCEPTION
            'RN-07: la vinculacion % exige programa academico y el asistente % no tiene ninguno activo',
            NEW.fk_vinculacion_snapshot, NEW.fk_asistente
            USING ERRCODE = 'check_violation';
    END IF;

    IF NOT v_exige_programa THEN
        -- Rutas de Paz y similares entran SIN programa, a proposito
        NEW.fk_programa_snapshot := NULL;
    END IF;

    RETURN NEW;
END;
$$;

COMMENT ON FUNCTION catedras.fn_trg_registro_validar() IS
    'Tres reglas en un disparador (RN-15 ventana, RN-22 vinculación, RN-07 programa). v7.1: se quitó RN-38 (cupo).';

-- --------------------------------------------------------------------------
--  2) INSCRIPCIÓN: fuera de la asistencia por QR
-- --------------------------------------------------------------------------
ALTER TABLE catedras.enlace_registro
    ADD COLUMN IF NOT EXISTS qr_cierre boolean NOT NULL DEFAULT false;
COMMENT ON COLUMN catedras.enlace_registro.qr_cierre IS
    'v7.1. true = es el QR de cierre de la sesión (se emite al crear la cátedra). Antes se marcaba con requiere_inscripcion.';

-- El marcador se traslada y deja de exigirse la inscripción.
UPDATE catedras.enlace_registro SET qr_cierre = true WHERE requiere_inscripcion AND NOT qr_cierre;
UPDATE catedras.enlace_registro SET requiere_inscripcion = false WHERE requiere_inscripcion;

DROP TRIGGER IF EXISTS trg_registro_inscripcion ON catedras.registro_asistencia;
DROP FUNCTION IF EXISTS catedras.fn_trg_registro_inscripcion();

-- Se conserva el nombre (una API anterior aún lo llama) pero ya no comprueba nada: solo delega.
CREATE OR REPLACE FUNCTION catedras.fn_solicitar_clave_insc_api(
    p_identificador text,
    p_token         uuid,
    p_ip            inet DEFAULT NULL
) RETURNS TABLE(id_clave bigint, clave text, enviado_a text)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    RETURN QUERY SELECT * FROM catedras.fn_solicitar_clave_api(p_identificador, p_token, p_ip);
END;
$$;

-- --------------------------------------------------------------------------
--  El QR de cierre (sin exigir inscripción; el marcador es qr_cierre)
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_emitir_qr_sesion(
    IN    p_fk_sesion  bigint,
    IN    p_fk_usuario bigint  DEFAULT NULL,
    IN    p_regenerar  boolean DEFAULT false,
    INOUT p_resultado  json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_fin     timestamptz;
    v_canal   varchar(10);
    v_id      bigint;
    v_antes   integer;
    v_desp    integer;
    v_base    text;
    v_token   uuid := gen_random_uuid();
    v_existia boolean := false;
BEGIN
    SELECT s.fin, m.canal_difusion INTO v_fin, v_canal
      FROM sesion s JOIN modalidad m ON m.id_modalidad = s.fk_modalidad
     WHERE s.id_sesion = p_fk_sesion;
    IF v_fin IS NULL THEN
        p_resultado := NULL;
        RETURN;
    END IF;

    -- RN-13 (script provisto): el canal del enlace debe concordar con la modalidad de la sesión:
    -- presencial -> QR, virtual y telepresencial -> ENLACE, híbrida (AMBOS) -> QR. Se deriva igual que
    -- sp_emitir_enlace. A TODAS les corresponde el mismo código QR en pantalla (es la imagen del enlace);
    -- lo que cambia es el CANAL que se registra y el 'origen' de la asistencia (QR o ENLACE).
    v_canal := CASE WHEN v_canal = 'ENLACE' THEN 'ENLACE' ELSE 'QR' END;

    IF NOT coalesce(p_regenerar, false) THEN
        SELECT e.id_enlace INTO v_id
          FROM enlace_registro e
         WHERE e.fk_sesion = p_fk_sesion AND e.revocado_en IS NULL AND e.qr_cierre
         ORDER BY e.id_enlace DESC LIMIT 1;
        v_existia := v_id IS NOT NULL;
    END IF;

    IF v_id IS NULL THEN
        v_antes := greatest(coalesce((SELECT valor::int FROM parametro WHERE clave = 'QR_MINUTOS_ANTES_FIN'), 15), 1);
        v_desp  := greatest(coalesce((SELECT valor::int FROM parametro WHERE clave = 'QR_MINUTOS_DESPUES_FIN'), 0), 0);
        v_base  := coalesce((SELECT valor FROM parametro WHERE clave = 'URL_BASE'), 'https://catedras.usbmed.edu.co/r/');

        -- RN-14 · un solo enlace vigente por sesión
        UPDATE enlace_registro SET revocado_en = now()
         WHERE fk_sesion = p_fk_sesion AND revocado_en IS NULL;

        INSERT INTO enlace_registro (fk_sesion, token, url_publica, ruta_imagen_qr, canal, ventana,
                                     fk_usuario_crea, requiere_inscripcion, qr_cierre)
        VALUES (p_fk_sesion, v_token, v_base || v_token::text, 'qr/' || v_token::text || '.png', v_canal,
                tstzrange(v_fin - make_interval(mins => v_antes), v_fin + make_interval(mins => v_desp), '[)'),
                p_fk_usuario, false, true)
        RETURNING id_enlace INTO v_id;
    END IF;

    SELECT row_to_json(r) INTO p_resultado
      FROM (SELECT e.id_enlace, e.fk_sesion, e.token, e.canal, lower(e.ventana) AS ventana_desde,
                   upper(e.ventana) AS ventana_hasta, e.revocado_en,
                   v_existia AS ya_existia
              FROM enlace_registro e WHERE e.id_enlace = v_id) r;
END;
$$;

-- --------------------------------------------------------------------------
--  4) CONSULTAR el QR vigente de una sesión (NULL si no tiene).
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_consultar_qr_sesion(
    IN    p_fk_sesion bigint,
    INOUT p_resultado json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(r) INTO p_resultado
      FROM (SELECT e.id_enlace, e.fk_sesion, e.token, e.canal, lower(e.ventana) AS ventana_desde,
                   upper(e.ventana) AS ventana_hasta, e.revocado_en,
                   true AS ya_existia
              FROM enlace_registro e
             WHERE e.fk_sesion = p_fk_sesion AND e.revocado_en IS NULL AND e.qr_cierre
             ORDER BY e.id_enlace DESC LIMIT 1) r;
END;
$$;

-- --------------------------------------------------------------------------
--  Asistencia de una sesión: quién registró (con hora y ubicación). Sin inscritos.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_asistencia_sesion(
    IN    p_fk_sesion bigint,
    INOUT p_resultado json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(r), '[]'::json) INTO p_resultado
      FROM (SELECT a.id_asistente AS fk_asistente, a.id_asis, a.nombres, a.apellidos,
                   ra.registrado_en, ra.latitud, ra.longitud, ra.origen
              FROM registro_asistencia ra
              JOIN asistente a ON a.id_asistente = ra.fk_asistente
             WHERE ra.fk_sesion = p_fk_sesion
             ORDER BY ra.registrado_en, a.apellidos, a.nombres) r;
END;
$$;

-- Procedimientos de la inscripción (049) y la tabla, solo si está vacía.
DROP PROCEDURE IF EXISTS catedras.sp_inscribir_catedra(bigint, bigint, text);
DROP PROCEDURE IF EXISTS catedras.sp_cancelar_inscripcion_catedra(bigint, bigint, text);
DROP PROCEDURE IF EXISTS catedras.sp_listar_inscripcion_catedra(bigint, bigint, boolean, integer, json);

DO $$
BEGIN
    IF to_regclass('catedras.inscripcion_catedra') IS NOT NULL THEN
        IF NOT EXISTS (SELECT 1 FROM catedras.inscripcion_catedra) THEN
            DROP TABLE catedras.inscripcion_catedra;
        ELSE
            RAISE NOTICE 'catedras.inscripcion_catedra tiene filas: se CONSERVA (la aplicación ya no la usa). Bórrala a mano si no las necesitas.';
        END IF;
    END IF;
END;
$$;

-- --------------------------------------------------------------------------
--  3) CUENTAS: solo el administrador entra; fuera la creación de cuentas de estudiante.
--     El login vuelve a ser solo por nombre de usuario.
-- --------------------------------------------------------------------------
DROP PROCEDURE IF EXISTS catedras.sp_buscar_estudiante_sin_cuenta(text, json);
DROP PROCEDURE IF EXISTS catedras.sp_provisionar_cuenta_estudiante(text, character varying, json);

CREATE OR REPLACE PROCEDURE catedras.sp_consultar_usuario_por_login(
    IN    p_usuario   text,
    INOUT p_resultado json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(u) INTO p_resultado
      FROM (SELECT id_usuario, fk_asistente, usuario, clave_hash, activo, debe_cambiar_clave,
                   ultimo_acceso, creado_en
              FROM usuario
             WHERE usuario = p_usuario) u;
END;
$$;



-- ----- migracion 055_solicitud_ponente.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/055_solicitud_ponente.sql
--  Proyecto Cátedras · SUBVERSIÓN v7.1 · D56 (calendario público + «Soy ponente»)
-- --------------------------------------------------------------------------
--  Una persona que quiere dictar una ponencia llena el formulario «Soy ponente»
--  del calendario (sin cuenta: el único usuario es el ADMIN, D55). Lo que
--  escribe NO toca todavía a 'asistente' ni a 'ponencia': queda en esta tabla
--  como SOLICITUD y espera en la bandeja del administrador, que la aprueba o
--  la rechaza.
--
--    PENDIENTE  -> APROBADA   (sp_resolver_solicitud_ponente, p_aprobar = true)
--               -> RECHAZADA  (p_aprobar = false; el motivo es obligatorio)
--
--  Al APROBAR, en la misma transacción:
--    1. Se busca a la persona por su documento (tipo + número) y, si no está,
--       por su correo institucional (único por persona). Si ya existe como
--       asistente, se reutiliza; si no, se crea 'asistente' (externa) y su
--       'documento_asistente' vigente.
--    2. Si la solicitud eligió una sesión del calendario, se asigna la persona
--       a esa sesión como PONENTE ('ponencia'). Si no eligió ninguna (propone
--       una cátedra nueva) la ponencia queda aprobada y el administrador crea
--       la cátedra desde /admin/catedras/nueva.
--
--  Una solicitud ya resuelta no se vuelve a resolver (P0001 -> 409 en la API).
--
--  También agrega al catálogo 'tipo_documento' los dos tipos que el formulario
--  ofrece y el script provisto no trae: licencia de conducción y código
--  institucional.
--
--  Idempotente (se puede volver a aplicar).
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  Catálogo: tipos de documento que pide el formulario
-- --------------------------------------------------------------------------
INSERT INTO catedras.tipo_documento (id_tipo_documento, nombre, activo) VALUES
    ('LC', 'Licencia de conduccion', true),
    ('CI', 'Codigo institucional',   true)
ON CONFLICT DO NOTHING;

-- --------------------------------------------------------------------------
--  Tabla
-- --------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS catedras.solicitud_ponente (
    id_solicitud       bigint GENERATED ALWAYS AS IDENTITY,
    nombres            character varying(100)  NOT NULL,
    apellidos          character varying(100)  NOT NULL,
    fk_tipo_documento  character varying(5)    NOT NULL,
    numero_documento   character varying(20)   NOT NULL,
    perfil_profesional character varying(200)  NOT NULL,
    direccion          character varying(200)  NOT NULL,
    telefono           character varying(20)   NOT NULL,
    correo_institucional public.citext         NOT NULL,
    titulo_ponencia    character varying(200)  NOT NULL,
    resumen_ponencia   character varying(2000) NOT NULL,
    fk_sesion          bigint,
    estado             character varying(10)   NOT NULL DEFAULT 'PENDIENTE',
    observacion_admin  character varying(500),
    fk_asistente       bigint,
    resuelto_por       bigint,
    resuelto_en        timestamp with time zone,
    creado_en          timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT pk_solicitud_ponente PRIMARY KEY (id_solicitud),
    CONSTRAINT fk_solponente_tipodoc FOREIGN KEY (fk_tipo_documento)
        REFERENCES catedras.tipo_documento (id_tipo_documento) ON UPDATE CASCADE ON DELETE RESTRICT,
    CONSTRAINT fk_solponente_sesion FOREIGN KEY (fk_sesion)
        REFERENCES catedras.sesion (id_sesion) ON UPDATE CASCADE ON DELETE SET NULL,
    CONSTRAINT fk_solponente_asistente FOREIGN KEY (fk_asistente)
        REFERENCES catedras.asistente (id_asistente) ON UPDATE CASCADE ON DELETE SET NULL,
    CONSTRAINT fk_solponente_usuario FOREIGN KEY (resuelto_por)
        REFERENCES catedras.usuario (id_usuario) ON UPDATE CASCADE ON DELETE SET NULL,
    CONSTRAINT chk_solponente_estado CHECK (estado IN ('PENDIENTE', 'APROBADA', 'RECHAZADA')),
    CONSTRAINT chk_solponente_numero CHECK (numero_documento ~ '^[0-9A-Za-z-]{4,20}$'),
    CONSTRAINT chk_solponente_correo CHECK (correo_institucional OPERATOR(public.~~) '%@%.%'::public.citext),
    CONSTRAINT chk_solponente_resuelta CHECK (
        (estado = 'PENDIENTE' AND resuelto_en IS NULL)
        OR (estado <> 'PENDIENTE' AND resuelto_en IS NOT NULL)),
    CONSTRAINT chk_solponente_motivo CHECK (
        estado <> 'RECHAZADA' OR length(btrim(coalesce(observacion_admin, ''))) > 0)
);

-- La bandeja se lee por estado, lo más reciente primero.
CREATE INDEX IF NOT EXISTS ix_solponente_estado ON catedras.solicitud_ponente (estado, creado_en DESC);

-- Una misma persona no puede tener dos solicitudes PENDIENTES con la misma ponencia
-- (doble clic, reenvío del formulario).
CREATE UNIQUE INDEX IF NOT EXISTS ux_solponente_pendiente
    ON catedras.solicitud_ponente (fk_tipo_documento, numero_documento, lower(titulo_ponencia))
    WHERE estado = 'PENDIENTE';

COMMENT ON TABLE catedras.solicitud_ponente IS
    'v7.1 D56. Postulación de un ponente desde el calendario público. El ADMIN la aprueba (crea asistente + documento + ponencia) o la rechaza con un motivo.';

-- --------------------------------------------------------------------------
--  Vista de lectura: la solicitud con los nombres que la pantalla necesita
-- --------------------------------------------------------------------------
CREATE OR REPLACE VIEW catedras.v_solicitud_ponente AS
SELECT sp.id_solicitud,
       sp.nombres,
       sp.apellidos,
       sp.nombres || ' ' || sp.apellidos AS nombre_completo,
       sp.fk_tipo_documento,
       td.nombre                         AS nombre_tipo_documento,
       sp.numero_documento,
       sp.perfil_profesional,
       sp.direccion,
       sp.telefono,
       sp.correo_institucional::text     AS correo_institucional,
       -- Misma regla que fn_trg_clave_derivar: dominios del parámetro DOMINIOS_INSTITUCIONALES.
       EXISTS (SELECT 1
                 FROM unnest(string_to_array(
                          coalesce((SELECT p.valor FROM catedras.parametro p WHERE p.clave = 'DOMINIOS_INSTITUCIONALES'),
                                   'usbmed.edu.co,usb.edu.co'), ',')) AS d(dom)
                WHERE lower(sp.correo_institucional::text) LIKE '%@' || lower(trim(d.dom))
              )                          AS correo_es_institucional,
       sp.titulo_ponencia,
       sp.resumen_ponencia,
       sp.fk_sesion,
       c.nombre                          AS nombre_catedra,
       s.numero_reunion,
       s.inicio                          AS inicio_sesion,
       sp.estado,
       sp.observacion_admin,
       sp.fk_asistente,
       sp.resuelto_por,
       sp.resuelto_en,
       sp.creado_en
  FROM catedras.solicitud_ponente sp
  JOIN catedras.tipo_documento td ON td.id_tipo_documento = sp.fk_tipo_documento
  LEFT JOIN catedras.sesion  s ON s.id_sesion  = sp.fk_sesion
  LEFT JOIN catedras.catedra c ON c.id_catedra = s.fk_catedra;

-- --------------------------------------------------------------------------
--  LISTAR — filtro opcional por estado (NULL = todas), las más recientes primero
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_listar_solicitud_ponente(
    IN    p_estado    text    DEFAULT NULL,
    IN    p_limite    integer DEFAULT 200,
    INOUT p_resultado json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(r), '[]'::json)
      INTO p_resultado
      FROM (SELECT *
              FROM v_solicitud_ponente
             WHERE p_estado IS NULL OR estado = upper(p_estado)
             ORDER BY (estado = 'PENDIENTE') DESC, creado_en DESC, id_solicitud DESC
             LIMIT p_limite) r;
END;
$$;

-- --------------------------------------------------------------------------
--  CONSULTAR — NULL en p_resultado = no existe
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_consultar_solicitud_ponente(
    IN    p_id_solicitud bigint,
    INOUT p_resultado    json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(v) INTO p_resultado
      FROM v_solicitud_ponente v
     WHERE v.id_solicitud = p_id_solicitud;
END;
$$;

-- --------------------------------------------------------------------------
--  CREAR — la postulación entra como PENDIENTE
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_crear_solicitud_ponente(
    IN    p_nombres              text,
    IN    p_apellidos            text,
    IN    p_fk_tipo_documento    text,
    IN    p_numero_documento     text,
    IN    p_perfil_profesional   text,
    IN    p_direccion            text,
    IN    p_telefono             text,
    IN    p_correo_institucional text,
    IN    p_titulo_ponencia      text,
    IN    p_resumen_ponencia     text,
    IN    p_fk_sesion            bigint DEFAULT NULL,
    INOUT p_resultado            json   DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_id bigint;
BEGIN
    IF EXISTS (SELECT 1 FROM solicitud_ponente
                WHERE estado = 'PENDIENTE'
                  AND fk_tipo_documento = p_fk_tipo_documento
                  AND numero_documento  = p_numero_documento
                  AND lower(titulo_ponencia) = lower(p_titulo_ponencia)) THEN
        RAISE EXCEPTION 'Ya hay una solicitud pendiente de este documento con la misma ponencia.'
            USING ERRCODE = 'P0001';
    END IF;

    INSERT INTO solicitud_ponente (nombres, apellidos, fk_tipo_documento, numero_documento,
                                   perfil_profesional, direccion, telefono, correo_institucional,
                                   titulo_ponencia, resumen_ponencia, fk_sesion)
    VALUES (p_nombres, p_apellidos, p_fk_tipo_documento, p_numero_documento,
            p_perfil_profesional, p_direccion, p_telefono, p_correo_institucional,
            p_titulo_ponencia, p_resumen_ponencia, p_fk_sesion)
    RETURNING id_solicitud INTO v_id;

    SELECT row_to_json(v) INTO p_resultado FROM v_solicitud_ponente v WHERE v.id_solicitud = v_id;
END;
$$;

-- --------------------------------------------------------------------------
--  RESOLVER — aprobar o rechazar (una sola vez), todo en una transacción
--  NULL en p_resultado = la solicitud no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_resolver_solicitud_ponente(
    IN    p_id_solicitud bigint,
    IN    p_aprobar      boolean,
    IN    p_observacion  text   DEFAULT NULL,
    IN    p_resuelto_por bigint DEFAULT NULL,
    INOUT p_resultado    json   DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_sol        solicitud_ponente%ROWTYPE;
    v_asistente  bigint;
    v_observacion text := nullif(btrim(coalesce(p_observacion, '')), '');
BEGIN
    SELECT * INTO v_sol FROM solicitud_ponente WHERE id_solicitud = p_id_solicitud FOR UPDATE;
    IF NOT FOUND THEN
        RETURN;
    END IF;

    IF v_sol.estado <> 'PENDIENTE' THEN
        RAISE EXCEPTION 'La solicitud % ya fue resuelta (%).', p_id_solicitud, v_sol.estado
            USING ERRCODE = 'P0001';
    END IF;

    IF p_aprobar THEN
        -- 1) La persona: por su documento; si no existe, se crea como externa.
        SELECT d.fk_asistente INTO v_asistente
          FROM documento_asistente d
         WHERE d.fk_tipo_documento = v_sol.fk_tipo_documento
           AND d.numero            = v_sol.numero_documento
         ORDER BY d.vigente DESC, d.registrado_en DESC
         LIMIT 1;

        -- Mismo correo institucional (único por persona: uq_asistente_corinst) = la misma persona que ya se
        -- registró con otro documento. Se reutiliza; solo se le agrega este documento si no tiene uno vigente.
        IF v_asistente IS NULL THEN
            SELECT a.id_asistente INTO v_asistente
              FROM asistente a
             WHERE a.correo_institucional = v_sol.correo_institucional;

            IF v_asistente IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM documento_asistente d WHERE d.fk_asistente = v_asistente AND d.vigente) THEN
                INSERT INTO documento_asistente (fk_asistente, fk_tipo_documento, numero)
                VALUES (v_asistente, v_sol.fk_tipo_documento, v_sol.numero_documento);
            END IF;
        END IF;

        IF v_asistente IS NULL THEN
            INSERT INTO asistente (nombres, apellidos, nombre_completo_asis,
                                   correo_institucional, celular, es_externo)
            VALUES (v_sol.nombres, v_sol.apellidos,
                    left(v_sol.nombres || ' ' || v_sol.apellidos, 200),
                    v_sol.correo_institucional, v_sol.telefono, true)
            RETURNING id_asistente INTO v_asistente;

            INSERT INTO documento_asistente (fk_asistente, fk_tipo_documento, numero)
            VALUES (v_asistente, v_sol.fk_tipo_documento, v_sol.numero_documento);
        END IF;

        -- 2) La participación en la sesión elegida (si eligió alguna).
        IF v_sol.fk_sesion IS NOT NULL THEN
            INSERT INTO ponencia (fk_sesion, fk_asistente, rol)
            VALUES (v_sol.fk_sesion, v_asistente, 'PONENTE')
            ON CONFLICT DO NOTHING;
        END IF;

        UPDATE solicitud_ponente
           SET estado = 'APROBADA', observacion_admin = v_observacion, fk_asistente = v_asistente,
               resuelto_por = p_resuelto_por, resuelto_en = now()
         WHERE id_solicitud = p_id_solicitud;
    ELSE
        IF v_observacion IS NULL THEN
            RAISE EXCEPTION 'Para rechazar la solicitud hay que escribir el motivo.'
                USING ERRCODE = 'P0001';
        END IF;

        UPDATE solicitud_ponente
           SET estado = 'RECHAZADA', observacion_admin = v_observacion,
               resuelto_por = p_resuelto_por, resuelto_en = now()
         WHERE id_solicitud = p_id_solicitud;
    END IF;

    SELECT row_to_json(v) INTO p_resultado FROM v_solicitud_ponente v WHERE v.id_solicitud = p_id_solicitud;
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_solicitud_ponente IS
    'v7.1 D56. Solicitudes de ponente (filtro opcional por estado), las pendientes primero.';
COMMENT ON PROCEDURE catedras.sp_consultar_solicitud_ponente IS
    'v7.1 D56. Una solicitud con los nombres de tipo de documento, cátedra y sesión. NULL = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_solicitud_ponente IS
    'v7.1 D56. Registra la postulación como PENDIENTE. P0001 si ya hay una pendiente igual.';
COMMENT ON PROCEDURE catedras.sp_resolver_solicitud_ponente IS
    'v7.1 D56. Aprueba (crea/reutiliza asistente y documento, asigna PONENTE a la sesión) o rechaza (motivo obligatorio). P0001 si ya estaba resuelta.';


-- ----- migracion 056_restriccion_programacion.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/056_restriccion_programacion.sql
--  Proyecto Cátedras · SUBVERSIÓN v7.1 · D57 (periodos y horarios en que se pueden programar cátedras)
-- --------------------------------------------------------------------------
--  Las cátedras se dictan en periodos de tiempo específicos. El administrador
--  define VENTANAS de programación («Periodo 2026-2: del 3 de agosto al 5 de
--  diciembre, de 06:00 a 21:00, de lunes a sábado») y desde entonces una sesión
--  solo se puede crear o mover a una fecha y hora que quepa en alguna ventana
--  ACTIVA.
--
--    · Sin ventanas activas NO hay restricción (el comportamiento de siempre).
--    · Una sesión cabe en una ventana si: su fecha de inicio y la de fin están
--      entre fecha_desde y fecha_hasta; su hora de inicio y de fin están entre
--      hora_desde y hora_hasta (y empieza y termina el mismo día); y su día de
--      la semana (ISO: 1 = lunes … 7 = domingo) está en dias_semana. hora_* y
--      dias_semana vacíos = sin límite de hora / de día.
--    · Todo se evalúa en hora de Colombia (America/Bogota, UTC-5).
--
--  La regla vive en la BASE DE DATOS (disparador sobre 'sesion'), así vale para
--  cualquier camino: formulario de cátedra, de sesión, API directa. El disparador
--  solo mira las sesiones nuevas o a las que se les CAMBIA inicio o fin: corregir
--  el salón de una sesión vieja no se bloquea. Un rechazo sale como P0001 (la
--  API lo responde 409 con el mensaje).
--
--  Idempotente (se puede volver a aplicar).
-- ==========================================================================

SET search_path TO catedras, public;

CREATE TABLE IF NOT EXISTS catedras.restriccion_programacion (
    id_restriccion bigint GENERATED ALWAYS AS IDENTITY,
    nombre         character varying(100) NOT NULL,
    fecha_desde    date NOT NULL,
    fecha_hasta    date NOT NULL,
    hora_desde     time without time zone,
    hora_hasta     time without time zone,
    dias_semana    smallint[],
    activo         boolean NOT NULL DEFAULT true,
    creado_en      timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT pk_restriccion_programacion PRIMARY KEY (id_restriccion),
    CONSTRAINT chk_restprog_fechas CHECK (fecha_hasta >= fecha_desde),
    CONSTRAINT chk_restprog_horas CHECK (
        (hora_desde IS NULL AND hora_hasta IS NULL)
        OR (hora_desde IS NOT NULL AND hora_hasta IS NOT NULL AND hora_hasta > hora_desde)),
    CONSTRAINT chk_restprog_dias CHECK (
        dias_semana IS NULL
        OR (cardinality(dias_semana) BETWEEN 1 AND 7 AND dias_semana <@ ARRAY[1,2,3,4,5,6,7]::smallint[]))
);

COMMENT ON TABLE catedras.restriccion_programacion IS
    'v7.1 D57. Ventanas (fechas, horas y días) en que se pueden programar sesiones. Sin ventanas activas no hay restricción.';

-- --------------------------------------------------------------------------
--  Texto legible de una ventana: «Periodo 2026-2»: del 2026-08-03 al 2026-12-05, de 06:00 a 21:00, lun, mar
-- --------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION catedras.fn_texto_restriccion(r catedras.restriccion_programacion)
    RETURNS text
    LANGUAGE sql STABLE
    AS $$
    SELECT '«' || r.nombre || '»: del ' || to_char(r.fecha_desde, 'YYYY-MM-DD') || ' al ' || to_char(r.fecha_hasta, 'YYYY-MM-DD')
        || CASE WHEN r.hora_desde IS NULL THEN ''
                ELSE ', de ' || to_char(r.hora_desde, 'HH24:MI') || ' a ' || to_char(r.hora_hasta, 'HH24:MI') END
        || CASE WHEN r.dias_semana IS NULL THEN ''
                ELSE ', ' || array_to_string(ARRAY(
                        SELECT (ARRAY['lun','mar','mié','jue','vie','sáb','dom'])[d]
                          FROM unnest(r.dias_semana) AS d ORDER BY d), ', ') END;
$$;

-- --------------------------------------------------------------------------
--  ¿Cabe este horario en alguna ventana activa? (sin ventanas activas: sí)
-- --------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION catedras.fn_horario_permitido(p_inicio timestamptz, p_fin timestamptz)
    RETURNS boolean
    LANGUAGE sql STABLE
    AS $$
    SELECT NOT EXISTS (SELECT 1 FROM catedras.restriccion_programacion WHERE activo)
        OR EXISTS (
            SELECT 1
              FROM catedras.restriccion_programacion r
             WHERE r.activo
               AND (p_inicio AT TIME ZONE 'America/Bogota')::date >= r.fecha_desde
               AND (p_fin    AT TIME ZONE 'America/Bogota')::date <= r.fecha_hasta
               AND (r.hora_desde IS NULL
                    OR ((p_inicio AT TIME ZONE 'America/Bogota')::time >= r.hora_desde
                        AND (p_fin AT TIME ZONE 'America/Bogota')::time <= r.hora_hasta
                        AND (p_fin AT TIME ZONE 'America/Bogota')::date = (p_inicio AT TIME ZONE 'America/Bogota')::date))
               AND (r.dias_semana IS NULL
                    OR extract(isodow FROM p_inicio AT TIME ZONE 'America/Bogota')::smallint = ANY (r.dias_semana)));
$$;

-- Mensaje con las ventanas activas, para explicar un rechazo.
CREATE OR REPLACE FUNCTION catedras.fn_mensaje_ventanas()
    RETURNS text
    LANGUAGE sql STABLE
    AS $$
    SELECT string_agg(catedras.fn_texto_restriccion(r), '; ' ORDER BY r.fecha_desde, r.id_restriccion)
      FROM catedras.restriccion_programacion r
     WHERE r.activo;
$$;

-- --------------------------------------------------------------------------
--  Disparador sobre 'sesion'
-- --------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION catedras.fn_trg_sesion_restriccion()
    RETURNS trigger
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    -- Corregir otros datos de una sesión existente no se bloquea: solo importa si cambia su horario.
    IF TG_OP = 'UPDATE' AND NEW.inicio = OLD.inicio AND NEW.fin = OLD.fin THEN
        RETURN NEW;
    END IF;

    IF NOT fn_horario_permitido(NEW.inicio, NEW.fin) THEN
        RAISE EXCEPTION 'El horario % a % (hora de Colombia) está fuera de los periodos en que se pueden programar cátedras. Se puede programar en: %.',
            to_char(NEW.inicio AT TIME ZONE 'America/Bogota', 'YYYY-MM-DD HH24:MI'),
            to_char(NEW.fin AT TIME ZONE 'America/Bogota', 'YYYY-MM-DD HH24:MI'),
            fn_mensaje_ventanas()
            USING ERRCODE = 'P0001';
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_sesion_restriccion ON catedras.sesion;
CREATE TRIGGER trg_sesion_restriccion
    BEFORE INSERT OR UPDATE OF inicio, fin ON catedras.sesion
    FOR EACH ROW EXECUTE FUNCTION catedras.fn_trg_sesion_restriccion();

-- --------------------------------------------------------------------------
--  Procedimientos
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_listar_restriccion_programacion(
    IN    p_solo_activas boolean DEFAULT false,
    IN    p_limite       integer DEFAULT 500,
    INOUT p_resultado    json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(x), '[]'::json)
      INTO p_resultado
      FROM (SELECT r.*, fn_texto_restriccion(r) AS texto
              FROM restriccion_programacion r
             WHERE NOT p_solo_activas OR r.activo
             ORDER BY r.activo DESC, r.fecha_desde DESC, r.id_restriccion DESC
             LIMIT p_limite) x;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_consultar_restriccion_programacion(
    IN    p_id_restriccion bigint,
    INOUT p_resultado      json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(x) INTO p_resultado
      FROM (SELECT r.*, fn_texto_restriccion(r) AS texto
              FROM restriccion_programacion r
             WHERE r.id_restriccion = p_id_restriccion) x;
END;
$$;

-- Horas como texto 'HH:MI' y días como texto '1,2,3' (vacío = sin límite): se evita depender de cómo
-- el cliente arma arreglos y horas.
CREATE OR REPLACE PROCEDURE catedras.sp_crear_restriccion_programacion(
    IN    p_nombre      text,
    IN    p_fecha_desde date,
    IN    p_fecha_hasta date,
    IN    p_hora_desde  text,
    IN    p_hora_hasta  text,
    IN    p_dias_semana text,
    IN    p_activo      boolean DEFAULT true,
    INOUT p_resultado   json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_id bigint;
BEGIN
    INSERT INTO restriccion_programacion (nombre, fecha_desde, fecha_hasta, hora_desde, hora_hasta, dias_semana, activo)
    VALUES (p_nombre, p_fecha_desde, p_fecha_hasta,
            nullif(btrim(p_hora_desde), '')::time, nullif(btrim(p_hora_hasta), '')::time,
            string_to_array(nullif(btrim(p_dias_semana), ''), ',')::smallint[],
            coalesce(p_activo, true))
    RETURNING id_restriccion INTO v_id;

    CALL sp_consultar_restriccion_programacion(v_id, p_resultado);
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_restriccion_programacion(
    IN    p_id_restriccion bigint,
    IN    p_nombre         text,
    IN    p_fecha_desde    date,
    IN    p_fecha_hasta    date,
    IN    p_hora_desde     text,
    IN    p_hora_hasta     text,
    IN    p_dias_semana    text,
    IN    p_activo         boolean,
    INOUT p_resultado      json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE restriccion_programacion
       SET nombre = p_nombre, fecha_desde = p_fecha_desde, fecha_hasta = p_fecha_hasta,
           hora_desde = nullif(btrim(p_hora_desde), '')::time, hora_hasta = nullif(btrim(p_hora_hasta), '')::time,
           dias_semana = string_to_array(nullif(btrim(p_dias_semana), ''), ',')::smallint[],
           activo = coalesce(p_activo, true)
     WHERE id_restriccion = p_id_restriccion;

    IF FOUND THEN
        CALL sp_consultar_restriccion_programacion(p_id_restriccion, p_resultado);
    END IF;
END;
$$;

-- Borrado FÍSICO: una ventana es configuración, no historia (para pausarla se desactiva).
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_restriccion_programacion(
    IN    p_id_restriccion bigint,
    INOUT p_resultado      json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    DELETE FROM restriccion_programacion WHERE id_restriccion = p_id_restriccion;
    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

-- ¿Cabe este horario? Devuelve { permitido, hay_restricciones, mensaje, ventanas:[{id_restriccion, nombre, texto}] }.
CREATE OR REPLACE PROCEDURE catedras.sp_validar_horario_programacion(
    IN    p_inicio    timestamptz,
    IN    p_fin       timestamptz,
    INOUT p_resultado json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_ventanas json;
    v_hay      boolean;
    v_ok       boolean := fn_horario_permitido(p_inicio, p_fin);
BEGIN
    SELECT COALESCE(json_agg(json_build_object('id_restriccion', r.id_restriccion, 'nombre', r.nombre,
                                               'texto', fn_texto_restriccion(r))
                             ORDER BY r.fecha_desde, r.id_restriccion), '[]'::json),
           count(*) > 0
      INTO v_ventanas, v_hay
      FROM restriccion_programacion r
     WHERE r.activo;

    p_resultado := json_build_object(
        'permitido', v_ok,
        'hay_restricciones', v_hay,
        'mensaje', CASE WHEN v_ok THEN NULL
                        ELSE 'Ese horario está fuera de los periodos en que se pueden programar cátedras. Se puede programar en: '
                             || fn_mensaje_ventanas() || '.' END,
        'ventanas', v_ventanas);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_validar_horario_programacion IS
    'v7.1 D57. ¿Cabe el horario en alguna ventana activa? (sin ventanas activas, sí). Solo lectura.';
COMMENT ON FUNCTION catedras.fn_trg_sesion_restriccion IS
    'v7.1 D57. Rechaza (P0001) una sesión nueva o con inicio/fin cambiado que no cabe en ninguna ventana activa.';


-- ----- migracion 057_solicitud_ponente_catedra_nueva.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/057_solicitud_ponente_catedra_nueva.sql
--  Proyecto Cátedras · SUBVERSIÓN v7.1 · D58 («Soy ponente» = cátedra nueva con fecha y hora)
-- --------------------------------------------------------------------------
--  El ponente ya no se postula a una sesión que existe: propone una CÁTEDRA NUEVA
--  y elige la fecha y la hora en que quiere dictarla. Al APROBAR la solicitud, la
--  cátedra se crea sola, en la misma transacción que la persona y la ponencia:
--
--    1. persona: por documento, luego por correo institucional; si no existe, se
--       crea como externa (asistente + documento_asistente)  [igual que en la 055];
--    2. cátedra + su única sesión, con sp_guardar_catedra (042): el nombre es el
--       título de la ponencia; el código ASIS se asigna solo (siguiente libre) si el
--       administrador no escribe uno; la sesión queda en la fecha y hora propuestas
--       (el administrador puede ajustarlas al aprobar);
--    3. ponencia: la persona queda como PONENTE de esa sesión.
--
--  Lo que el ponente NO elige y la cátedra necesita —tipo de evento, modalidad, sede,
--  periodo y salón— lo escoge el administrador al aprobar (la pantalla trae valores
--  sugeridos). Las reglas de siempre siguen valiendo al crear: los periodos y horarios
--  de la 056 (disparador sobre sesion) y las FK. Si algo falla, no se crea nada.
--
--  Las solicitudes que ya existían (con 'fk_sesion', de la 055) se resuelven como antes.
--
--  Cambia dos firmas: sp_crear_solicitud_ponente (sin fk_sesion, con inicio y fin) y
--  sp_resolver_solicitud_ponente (con los datos de la cátedra). Se borran las viejas
--  para que un CALL no sea ambiguo. Idempotente.
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  Tabla: fecha y hora propuestas y la cátedra creada
-- --------------------------------------------------------------------------
ALTER TABLE catedras.solicitud_ponente
    ADD COLUMN IF NOT EXISTS inicio_propuesto timestamp with time zone,
    ADD COLUMN IF NOT EXISTS fin_propuesto    timestamp with time zone,
    ADD COLUMN IF NOT EXISTS fk_catedra       bigint;

ALTER TABLE catedras.solicitud_ponente DROP CONSTRAINT IF EXISTS fk_solponente_catedra;
ALTER TABLE catedras.solicitud_ponente
    ADD CONSTRAINT fk_solponente_catedra FOREIGN KEY (fk_catedra)
        REFERENCES catedras.catedra (id_catedra) ON UPDATE CASCADE ON DELETE SET NULL;

ALTER TABLE catedras.solicitud_ponente DROP CONSTRAINT IF EXISTS chk_solponente_horario;
ALTER TABLE catedras.solicitud_ponente
    ADD CONSTRAINT chk_solponente_horario CHECK (
        (inicio_propuesto IS NULL AND fin_propuesto IS NULL)
        OR (inicio_propuesto IS NOT NULL AND fin_propuesto > inicio_propuesto));

COMMENT ON COLUMN catedras.solicitud_ponente.inicio_propuesto IS
    'v7.1 D58. Fecha y hora de inicio que el ponente propone para su cátedra nueva (null en las solicitudes anteriores a la 057).';
COMMENT ON COLUMN catedras.solicitud_ponente.fk_catedra IS
    'v7.1 D58. La cátedra que se creó al aprobar la solicitud.';

-- --------------------------------------------------------------------------
--  Vista (se recrea: las columnas nuevas van en medio)
-- --------------------------------------------------------------------------
DROP VIEW IF EXISTS catedras.v_solicitud_ponente;
CREATE VIEW catedras.v_solicitud_ponente AS
SELECT sp.id_solicitud,
       sp.nombres,
       sp.apellidos,
       sp.nombres || ' ' || sp.apellidos AS nombre_completo,
       sp.fk_tipo_documento,
       td.nombre                         AS nombre_tipo_documento,
       sp.numero_documento,
       sp.perfil_profesional,
       sp.direccion,
       sp.telefono,
       sp.correo_institucional::text     AS correo_institucional,
       EXISTS (SELECT 1
                 FROM unnest(string_to_array(
                          coalesce((SELECT p.valor FROM catedras.parametro p WHERE p.clave = 'DOMINIOS_INSTITUCIONALES'),
                                   'usbmed.edu.co,usb.edu.co'), ',')) AS d(dom)
                WHERE lower(sp.correo_institucional::text) LIKE '%@' || lower(trim(d.dom))
              )                          AS correo_es_institucional,
       sp.titulo_ponencia,
       sp.resumen_ponencia,
       sp.inicio_propuesto,
       sp.fin_propuesto,
       sp.fk_sesion,
       c.nombre                          AS nombre_catedra,
       s.numero_reunion,
       s.inicio                          AS inicio_sesion,
       sp.fk_catedra,
       sp.estado,
       sp.observacion_admin,
       sp.fk_asistente,
       sp.resuelto_por,
       sp.resuelto_en,
       sp.creado_en
  FROM catedras.solicitud_ponente sp
  JOIN catedras.tipo_documento td ON td.id_tipo_documento = sp.fk_tipo_documento
  LEFT JOIN catedras.sesion  s ON s.id_sesion  = sp.fk_sesion
  LEFT JOIN catedras.catedra c ON c.id_catedra = coalesce(s.fk_catedra, sp.fk_catedra);

-- --------------------------------------------------------------------------
--  CREAR — la postulación (cátedra nueva con fecha y hora) entra como PENDIENTE
-- --------------------------------------------------------------------------
DROP PROCEDURE IF EXISTS catedras.sp_crear_solicitud_ponente(text, text, text, text, text, text, text, text, text, text, bigint, json);

CREATE OR REPLACE PROCEDURE catedras.sp_crear_solicitud_ponente(
    IN    p_nombres              text,
    IN    p_apellidos            text,
    IN    p_fk_tipo_documento    text,
    IN    p_numero_documento     text,
    IN    p_perfil_profesional   text,
    IN    p_direccion            text,
    IN    p_telefono             text,
    IN    p_correo_institucional text,
    IN    p_titulo_ponencia      text,
    IN    p_resumen_ponencia     text,
    IN    p_inicio               timestamp with time zone,
    IN    p_fin                  timestamp with time zone,
    INOUT p_resultado            json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_id bigint;
BEGIN
    IF p_inicio IS NULL OR p_fin IS NULL OR p_fin <= p_inicio THEN
        RAISE EXCEPTION 'La hora de fin debe ser posterior a la de inicio.' USING ERRCODE = 'P0001';
    END IF;
    IF p_inicio <= now() THEN
        RAISE EXCEPTION 'La fecha y la hora que propones ya pasaron: elige una futura.' USING ERRCODE = 'P0001';
    END IF;
    -- Periodos y horarios de programación (056): si el administrador los definió, la propuesta debe caber.
    IF NOT fn_horario_permitido(p_inicio, p_fin) THEN
        RAISE EXCEPTION 'El horario % a % (hora de Colombia) está fuera de los periodos en que se pueden programar cátedras. Se puede programar en: %.',
            to_char(p_inicio AT TIME ZONE 'America/Bogota', 'YYYY-MM-DD HH24:MI'),
            to_char(p_fin AT TIME ZONE 'America/Bogota', 'YYYY-MM-DD HH24:MI'),
            fn_mensaje_ventanas()
            USING ERRCODE = 'P0001';
    END IF;

    IF EXISTS (SELECT 1 FROM solicitud_ponente
                WHERE estado = 'PENDIENTE'
                  AND fk_tipo_documento = p_fk_tipo_documento
                  AND numero_documento  = p_numero_documento
                  AND lower(titulo_ponencia) = lower(p_titulo_ponencia)) THEN
        RAISE EXCEPTION 'Ya hay una solicitud pendiente de este documento con la misma ponencia.'
            USING ERRCODE = 'P0001';
    END IF;

    INSERT INTO solicitud_ponente (nombres, apellidos, fk_tipo_documento, numero_documento,
                                   perfil_profesional, direccion, telefono, correo_institucional,
                                   titulo_ponencia, resumen_ponencia, inicio_propuesto, fin_propuesto)
    VALUES (p_nombres, p_apellidos, p_fk_tipo_documento, p_numero_documento,
            p_perfil_profesional, p_direccion, p_telefono, p_correo_institucional,
            p_titulo_ponencia, p_resumen_ponencia, p_inicio, p_fin)
    RETURNING id_solicitud INTO v_id;

    SELECT row_to_json(v) INTO p_resultado FROM v_solicitud_ponente v WHERE v.id_solicitud = v_id;
END;
$$;

-- --------------------------------------------------------------------------
--  RESOLVER — aprobar (crea persona, cátedra y ponencia) o rechazar, una sola vez
--  NULL en p_resultado = la solicitud no existe.
-- --------------------------------------------------------------------------
DROP PROCEDURE IF EXISTS catedras.sp_resolver_solicitud_ponente(bigint, boolean, text, bigint, json);

CREATE OR REPLACE PROCEDURE catedras.sp_resolver_solicitud_ponente(
    IN    p_id_solicitud   bigint,
    IN    p_aprobar        boolean,
    IN    p_observacion    text   DEFAULT NULL,
    IN    p_resuelto_por   bigint DEFAULT NULL,
    -- Datos de la cátedra nueva (solo se usan al APROBAR una solicitud con fecha propuesta):
    IN    p_nombre_catedra text   DEFAULT NULL,   -- vacío = el título de la ponencia
    IN    p_fk_tipo_evento text   DEFAULT NULL,   -- vacío = CAAB (cátedra abierta)
    IN    p_id_evento_asis text   DEFAULT NULL,   -- vacío = el siguiente código libre
    IN    p_fk_modalidad   text   DEFAULT NULL,
    IN    p_fk_sede        text   DEFAULT NULL,
    IN    p_fk_periodo     text   DEFAULT NULL,
    IN    p_lugar          text   DEFAULT NULL,
    IN    p_inicio         timestamp with time zone DEFAULT NULL,   -- vacío = el propuesto
    IN    p_fin            timestamp with time zone DEFAULT NULL,
    INOUT p_resultado      json   DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_sol         solicitud_ponente%ROWTYPE;
    v_asistente   bigint;
    v_observacion text := nullif(btrim(coalesce(p_observacion, '')), '');
    v_inicio      timestamptz;
    v_fin         timestamptz;
    v_codigo      text;
    v_creada      json;
    v_id_catedra  bigint;
    v_id_sesion   bigint;
BEGIN
    SELECT * INTO v_sol FROM solicitud_ponente WHERE id_solicitud = p_id_solicitud FOR UPDATE;
    IF NOT FOUND THEN
        RETURN;
    END IF;

    IF v_sol.estado <> 'PENDIENTE' THEN
        RAISE EXCEPTION 'La solicitud % ya fue resuelta (%).', p_id_solicitud, v_sol.estado
            USING ERRCODE = 'P0001';
    END IF;

    IF p_aprobar THEN
        -- Antes de tocar nada: lo que la cátedra nueva necesita.
        IF v_sol.inicio_propuesto IS NOT NULL THEN
            v_inicio := coalesce(p_inicio, v_sol.inicio_propuesto);
            v_fin    := coalesce(p_fin, v_sol.fin_propuesto);
            IF v_fin <= v_inicio THEN
                RAISE EXCEPTION 'La hora de fin debe ser posterior a la de inicio.' USING ERRCODE = 'P0001';
            END IF;
            IF v_inicio <= now() THEN
                RAISE EXCEPTION 'La fecha propuesta ya pasó: ajusta la fecha y la hora para aprobar la cátedra.' USING ERRCODE = 'P0001';
            END IF;
            IF nullif(btrim(coalesce(p_fk_modalidad, '')), '') IS NULL
               OR nullif(btrim(coalesce(p_fk_sede, '')), '') IS NULL
               OR nullif(btrim(coalesce(p_fk_periodo, '')), '') IS NULL THEN
                RAISE EXCEPTION 'Para aprobar hay que indicar la modalidad, la sede y el periodo de la cátedra nueva.'
                    USING ERRCODE = 'P0001';
            END IF;
            v_codigo := nullif(btrim(coalesce(p_id_evento_asis, '')), '');
            IF v_codigo IS NOT NULL AND v_codigo !~ '^[0-9]{9}$' THEN
                RAISE EXCEPTION 'El código ASIS debe tener 9 dígitos (con ceros a la izquierda).' USING ERRCODE = 'P0001';
            END IF;
        END IF;

        -- 1) La persona: por documento; si no, por correo institucional; si no existe, se crea como externa.
        SELECT d.fk_asistente INTO v_asistente
          FROM documento_asistente d
         WHERE d.fk_tipo_documento = v_sol.fk_tipo_documento
           AND d.numero            = v_sol.numero_documento
         ORDER BY d.vigente DESC, d.registrado_en DESC
         LIMIT 1;

        IF v_asistente IS NULL THEN
            SELECT a.id_asistente INTO v_asistente
              FROM asistente a
             WHERE a.correo_institucional = v_sol.correo_institucional;

            IF v_asistente IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM documento_asistente d WHERE d.fk_asistente = v_asistente AND d.vigente) THEN
                INSERT INTO documento_asistente (fk_asistente, fk_tipo_documento, numero)
                VALUES (v_asistente, v_sol.fk_tipo_documento, v_sol.numero_documento);
            END IF;
        END IF;

        IF v_asistente IS NULL THEN
            INSERT INTO asistente (nombres, apellidos, nombre_completo_asis,
                                   correo_institucional, celular, es_externo)
            VALUES (v_sol.nombres, v_sol.apellidos,
                    left(v_sol.nombres || ' ' || v_sol.apellidos, 200),
                    v_sol.correo_institucional, v_sol.telefono, true)
            RETURNING id_asistente INTO v_asistente;

            INSERT INTO documento_asistente (fk_asistente, fk_tipo_documento, numero)
            VALUES (v_asistente, v_sol.fk_tipo_documento, v_sol.numero_documento);
        END IF;

        -- 2) La cátedra nueva con su sesión (sp_guardar_catedra: todo o nada; el disparador de la 056 revisa periodos y horarios).
        IF v_sol.inicio_propuesto IS NOT NULL THEN
            IF v_codigo IS NULL THEN
                SELECT lpad((coalesce(max(c.id_evento_asis::bigint), 0) + 1)::text, 9, '0') INTO v_codigo FROM catedra c;
            END IF;

            CALL catedras.sp_guardar_catedra(
                v_codigo::character(9),
                left(coalesce(nullif(btrim(coalesce(p_nombre_catedra, '')), ''), v_sol.titulo_ponencia), 200)::character varying(200),
                coalesce(nullif(btrim(coalesce(p_fk_tipo_evento, '')), ''), 'CAAB')::character(4),
                NULL::character varying(15),
                true,
                jsonb_build_array(jsonb_build_object(
                    'inicio',       v_inicio,
                    'fin',          v_fin,
                    'fk_modalidad', btrim(p_fk_modalidad),
                    'fk_sede',      btrim(p_fk_sede),
                    'fk_periodo',   btrim(p_fk_periodo),
                    'titulo',       left(v_sol.titulo_ponencia, 200),
                    'lugar',        nullif(btrim(coalesce(p_lugar, '')), ''))),
                v_creada);
            v_id_catedra := (v_creada ->> 'id_catedra')::bigint;
            v_id_sesion  := (v_creada -> 'sesiones' -> 0 ->> 'id_sesion')::bigint;

            UPDATE solicitud_ponente SET fk_catedra = v_id_catedra, fk_sesion = v_id_sesion
             WHERE id_solicitud = p_id_solicitud;
            v_sol.fk_sesion := v_id_sesion;
        END IF;

        -- 3) La participación en la sesión (la nueva, o la elegida en una solicitud anterior a la 057).
        IF v_sol.fk_sesion IS NOT NULL THEN
            INSERT INTO ponencia (fk_sesion, fk_asistente, rol)
            VALUES (v_sol.fk_sesion, v_asistente, 'PONENTE')
            ON CONFLICT DO NOTHING;
        END IF;

        UPDATE solicitud_ponente
           SET estado = 'APROBADA', observacion_admin = v_observacion, fk_asistente = v_asistente,
               resuelto_por = p_resuelto_por, resuelto_en = now()
         WHERE id_solicitud = p_id_solicitud;
    ELSE
        IF v_observacion IS NULL THEN
            RAISE EXCEPTION 'Para rechazar la solicitud hay que escribir el motivo.'
                USING ERRCODE = 'P0001';
        END IF;

        UPDATE solicitud_ponente
           SET estado = 'RECHAZADA', observacion_admin = v_observacion,
               resuelto_por = p_resuelto_por, resuelto_en = now()
         WHERE id_solicitud = p_id_solicitud;
    END IF;

    SELECT row_to_json(v) INTO p_resultado FROM v_solicitud_ponente v WHERE v.id_solicitud = p_id_solicitud;
END;
$$;

COMMENT ON PROCEDURE catedras.sp_crear_solicitud_ponente IS
    'v7.1 D58. Registra la postulación (cátedra nueva con fecha y hora propuestas) como PENDIENTE. P0001 si la fecha pasó, no cabe en los periodos o ya hay una pendiente igual.';
COMMENT ON PROCEDURE catedras.sp_resolver_solicitud_ponente IS
    'v7.1 D58. Aprueba (crea/reutiliza la persona, crea la cátedra con su sesión y asigna PONENTE) o rechaza (motivo obligatorio). P0001 si ya estaba resuelta o faltan datos.';


-- ----- migracion 058_solicitud_ponente_modalidad_sede_periodo.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/058_solicitud_ponente_modalidad_sede_periodo.sql
--  Proyecto Cátedras · SUBVERSIÓN v7.1 · D58 (el ponente también elige modalidad, sede y periodo)
-- --------------------------------------------------------------------------
--  Además de la fecha y la hora, el ponente que propone una cátedra nueva elige
--  la MODALIDAD, la SEDE y el PERIODO ACADÉMICO. Quedan en la solicitud y la
--  pantalla de decisión del administrador los trae ya elegidos (los puede cambiar).
--
--  Reglas nuevas (P0001 -> 409 en la API):
--    · el periodo debe estar ACTIVO y la fecha de la cátedra debe caer DENTRO de él
--      (las cátedras se dictan en periodos específicos); se revisa al proponer y
--      otra vez al aprobar (el administrador puede cambiar la fecha o el periodo);
--    · la sede debe estar ACTIVA.
--  Las solicitudes anteriores no traen estos datos (columnas null): el administrador
--  los completa al aprobar, como en la 057.
--
--  Cambia la firma de sp_crear_solicitud_ponente (3 parámetros más); se borra la
--  anterior para que un CALL no sea ambiguo. sp_resolver_solicitud_ponente conserva
--  su firma: lo que el administrador no escribe se toma de la solicitud. Idempotente.
--  Depende de la 055, la 056 y la 057.
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  Tabla
-- --------------------------------------------------------------------------
ALTER TABLE catedras.solicitud_ponente
    ADD COLUMN IF NOT EXISTS fk_modalidad character varying(15),
    ADD COLUMN IF NOT EXISTS fk_sede      character varying(15),
    ADD COLUMN IF NOT EXISTS fk_periodo   character(6);

ALTER TABLE catedras.solicitud_ponente DROP CONSTRAINT IF EXISTS fk_solponente_modalidad;
ALTER TABLE catedras.solicitud_ponente ADD CONSTRAINT fk_solponente_modalidad FOREIGN KEY (fk_modalidad)
    REFERENCES catedras.modalidad (id_modalidad) ON UPDATE CASCADE ON DELETE SET NULL;
ALTER TABLE catedras.solicitud_ponente DROP CONSTRAINT IF EXISTS fk_solponente_sede;
ALTER TABLE catedras.solicitud_ponente ADD CONSTRAINT fk_solponente_sede FOREIGN KEY (fk_sede)
    REFERENCES catedras.sede (id_sede) ON UPDATE CASCADE ON DELETE SET NULL;
ALTER TABLE catedras.solicitud_ponente DROP CONSTRAINT IF EXISTS fk_solponente_periodo;
ALTER TABLE catedras.solicitud_ponente ADD CONSTRAINT fk_solponente_periodo FOREIGN KEY (fk_periodo)
    REFERENCES catedras.periodo_academico (id_periodo) ON UPDATE CASCADE ON DELETE SET NULL;

-- --------------------------------------------------------------------------
--  Vista (se recrea: las columnas nuevas van en medio)
-- --------------------------------------------------------------------------
DROP VIEW IF EXISTS catedras.v_solicitud_ponente;
CREATE VIEW catedras.v_solicitud_ponente AS
SELECT sp.id_solicitud,
       sp.nombres,
       sp.apellidos,
       sp.nombres || ' ' || sp.apellidos AS nombre_completo,
       sp.fk_tipo_documento,
       td.nombre                         AS nombre_tipo_documento,
       sp.numero_documento,
       sp.perfil_profesional,
       sp.direccion,
       sp.telefono,
       sp.correo_institucional::text     AS correo_institucional,
       EXISTS (SELECT 1
                 FROM unnest(string_to_array(
                          coalesce((SELECT p.valor FROM catedras.parametro p WHERE p.clave = 'DOMINIOS_INSTITUCIONALES'),
                                   'usbmed.edu.co,usb.edu.co'), ',')) AS d(dom)
                WHERE lower(sp.correo_institucional::text) LIKE '%@' || lower(trim(d.dom))
              )                          AS correo_es_institucional,
       sp.titulo_ponencia,
       sp.resumen_ponencia,
       sp.inicio_propuesto,
       sp.fin_propuesto,
       sp.fk_modalidad,
       m.nombre                          AS nombre_modalidad,
       sp.fk_sede,
       se.nombre                         AS nombre_sede,
       sp.fk_periodo,
       sp.fk_sesion,
       c.nombre                          AS nombre_catedra,
       s.numero_reunion,
       s.inicio                          AS inicio_sesion,
       sp.fk_catedra,
       sp.estado,
       sp.observacion_admin,
       sp.fk_asistente,
       sp.resuelto_por,
       sp.resuelto_en,
       sp.creado_en
  FROM catedras.solicitud_ponente sp
  JOIN catedras.tipo_documento td ON td.id_tipo_documento = sp.fk_tipo_documento
  LEFT JOIN catedras.modalidad m  ON m.id_modalidad = sp.fk_modalidad
  LEFT JOIN catedras.sede      se ON se.id_sede     = sp.fk_sede
  LEFT JOIN catedras.sesion  s ON s.id_sesion  = sp.fk_sesion
  LEFT JOIN catedras.catedra c ON c.id_catedra = coalesce(s.fk_catedra, sp.fk_catedra);

-- --------------------------------------------------------------------------
--  ¿La fecha cabe en el periodo y la sede está activa? (P0001 si no)
-- --------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION catedras.fn_validar_periodo_y_sede(
    p_inicio timestamptz, p_fin timestamptz, p_fk_periodo text, p_fk_sede text)
    RETURNS void
    LANGUAGE plpgsql STABLE
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_per periodo_academico%ROWTYPE;
    v_activa boolean;
BEGIN
    IF p_fk_periodo IS NOT NULL THEN
        SELECT * INTO v_per FROM periodo_academico WHERE id_periodo = p_fk_periodo;
        IF NOT FOUND THEN
            RAISE EXCEPTION 'El periodo académico % no existe.', p_fk_periodo USING ERRCODE = 'P0001';
        END IF;
        IF NOT v_per.activo THEN
            RAISE EXCEPTION 'El periodo académico % no está activo.', btrim(p_fk_periodo) USING ERRCODE = 'P0001';
        END IF;
        IF (p_inicio AT TIME ZONE 'America/Bogota')::date < v_per.fecha_ini
           OR (p_fin AT TIME ZONE 'America/Bogota')::date > v_per.fecha_fin THEN
            RAISE EXCEPTION 'La fecha de la cátedra (% a %, hora de Colombia) no cae dentro del periodo % (del % al %): elige otra fecha u otro periodo.',
                to_char(p_inicio AT TIME ZONE 'America/Bogota', 'YYYY-MM-DD HH24:MI'),
                to_char(p_fin AT TIME ZONE 'America/Bogota', 'YYYY-MM-DD HH24:MI'),
                btrim(p_fk_periodo), to_char(v_per.fecha_ini, 'YYYY-MM-DD'), to_char(v_per.fecha_fin, 'YYYY-MM-DD')
                USING ERRCODE = 'P0001';
        END IF;
    END IF;

    IF p_fk_sede IS NOT NULL THEN
        SELECT activo INTO v_activa FROM sede WHERE id_sede = p_fk_sede;
        IF NOT FOUND THEN
            RAISE EXCEPTION 'La sede % no existe.', p_fk_sede USING ERRCODE = 'P0001';
        END IF;
        IF NOT v_activa THEN
            RAISE EXCEPTION 'La sede % no está activa.', p_fk_sede USING ERRCODE = 'P0001';
        END IF;
    END IF;
END;
$$;

-- --------------------------------------------------------------------------
--  CREAR — ahora con modalidad, sede y periodo
-- --------------------------------------------------------------------------
DROP PROCEDURE IF EXISTS catedras.sp_crear_solicitud_ponente(text, text, text, text, text, text, text, text, text, text, timestamp with time zone, timestamp with time zone, json);

CREATE OR REPLACE PROCEDURE catedras.sp_crear_solicitud_ponente(
    IN    p_nombres              text,
    IN    p_apellidos            text,
    IN    p_fk_tipo_documento    text,
    IN    p_numero_documento     text,
    IN    p_perfil_profesional   text,
    IN    p_direccion            text,
    IN    p_telefono             text,
    IN    p_correo_institucional text,
    IN    p_titulo_ponencia      text,
    IN    p_resumen_ponencia     text,
    IN    p_inicio               timestamp with time zone,
    IN    p_fin                  timestamp with time zone,
    IN    p_fk_modalidad         text,
    IN    p_fk_sede              text,
    IN    p_fk_periodo           text,
    INOUT p_resultado            json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_id bigint;
BEGIN
    IF p_inicio IS NULL OR p_fin IS NULL OR p_fin <= p_inicio THEN
        RAISE EXCEPTION 'La hora de fin debe ser posterior a la de inicio.' USING ERRCODE = 'P0001';
    END IF;
    IF p_inicio <= now() THEN
        RAISE EXCEPTION 'La fecha y la hora que propones ya pasaron: elige una futura.' USING ERRCODE = 'P0001';
    END IF;

    -- El periodo debe estar activo y contener la fecha; la sede debe estar activa.
    PERFORM fn_validar_periodo_y_sede(p_inicio, p_fin, p_fk_periodo, p_fk_sede);

    -- Periodos y horarios de programación (056): si el administrador los definió, la propuesta debe caber.
    IF NOT fn_horario_permitido(p_inicio, p_fin) THEN
        RAISE EXCEPTION 'El horario % a % (hora de Colombia) está fuera de los periodos en que se pueden programar cátedras. Se puede programar en: %.',
            to_char(p_inicio AT TIME ZONE 'America/Bogota', 'YYYY-MM-DD HH24:MI'),
            to_char(p_fin AT TIME ZONE 'America/Bogota', 'YYYY-MM-DD HH24:MI'),
            fn_mensaje_ventanas()
            USING ERRCODE = 'P0001';
    END IF;

    IF EXISTS (SELECT 1 FROM solicitud_ponente
                WHERE estado = 'PENDIENTE'
                  AND fk_tipo_documento = p_fk_tipo_documento
                  AND numero_documento  = p_numero_documento
                  AND lower(titulo_ponencia) = lower(p_titulo_ponencia)) THEN
        RAISE EXCEPTION 'Ya hay una solicitud pendiente de este documento con la misma ponencia.'
            USING ERRCODE = 'P0001';
    END IF;

    INSERT INTO solicitud_ponente (nombres, apellidos, fk_tipo_documento, numero_documento,
                                   perfil_profesional, direccion, telefono, correo_institucional,
                                   titulo_ponencia, resumen_ponencia, inicio_propuesto, fin_propuesto,
                                   fk_modalidad, fk_sede, fk_periodo)
    VALUES (p_nombres, p_apellidos, p_fk_tipo_documento, p_numero_documento,
            p_perfil_profesional, p_direccion, p_telefono, p_correo_institucional,
            p_titulo_ponencia, p_resumen_ponencia, p_inicio, p_fin,
            nullif(btrim(p_fk_modalidad), ''), nullif(btrim(p_fk_sede), ''), nullif(btrim(p_fk_periodo), ''))
    RETURNING id_solicitud INTO v_id;

    SELECT row_to_json(v) INTO p_resultado FROM v_solicitud_ponente v WHERE v.id_solicitud = v_id;
END;
$$;

-- --------------------------------------------------------------------------
--  RESOLVER — igual que la 057, pero modalidad, sede y periodo que el administrador no escribe
--  se toman de la solicitud, y la fecha debe caber en el periodo elegido.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_resolver_solicitud_ponente(
    IN    p_id_solicitud   bigint,
    IN    p_aprobar        boolean,
    IN    p_observacion    text   DEFAULT NULL,
    IN    p_resuelto_por   bigint DEFAULT NULL,
    IN    p_nombre_catedra text   DEFAULT NULL,   -- vacío = el título de la ponencia
    IN    p_fk_tipo_evento text   DEFAULT NULL,   -- vacío = CAAB (cátedra abierta)
    IN    p_id_evento_asis text   DEFAULT NULL,   -- vacío = el siguiente código libre
    IN    p_fk_modalidad   text   DEFAULT NULL,   -- vacío = la que eligió el ponente
    IN    p_fk_sede        text   DEFAULT NULL,   -- vacío = la que eligió el ponente
    IN    p_fk_periodo     text   DEFAULT NULL,   -- vacío = el que eligió el ponente
    IN    p_lugar          text   DEFAULT NULL,
    IN    p_inicio         timestamp with time zone DEFAULT NULL,   -- vacío = el propuesto
    IN    p_fin            timestamp with time zone DEFAULT NULL,
    INOUT p_resultado      json   DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_sol         solicitud_ponente%ROWTYPE;
    v_asistente   bigint;
    v_observacion text := nullif(btrim(coalesce(p_observacion, '')), '');
    v_inicio      timestamptz;
    v_fin         timestamptz;
    v_modalidad   text;
    v_sede        text;
    v_periodo     text;
    v_codigo      text;
    v_creada      json;
    v_id_catedra  bigint;
    v_id_sesion   bigint;
BEGIN
    SELECT * INTO v_sol FROM solicitud_ponente WHERE id_solicitud = p_id_solicitud FOR UPDATE;
    IF NOT FOUND THEN
        RETURN;
    END IF;

    IF v_sol.estado <> 'PENDIENTE' THEN
        RAISE EXCEPTION 'La solicitud % ya fue resuelta (%).', p_id_solicitud, v_sol.estado
            USING ERRCODE = 'P0001';
    END IF;

    IF p_aprobar THEN
        -- Antes de tocar nada: lo que la cátedra nueva necesita.
        IF v_sol.inicio_propuesto IS NOT NULL THEN
            v_inicio    := coalesce(p_inicio, v_sol.inicio_propuesto);
            v_fin       := coalesce(p_fin, v_sol.fin_propuesto);
            v_modalidad := coalesce(nullif(btrim(coalesce(p_fk_modalidad, '')), ''), v_sol.fk_modalidad);
            v_sede      := coalesce(nullif(btrim(coalesce(p_fk_sede, '')), ''), v_sol.fk_sede);
            v_periodo   := coalesce(nullif(btrim(coalesce(p_fk_periodo, '')), ''), btrim(v_sol.fk_periodo));
            IF v_fin <= v_inicio THEN
                RAISE EXCEPTION 'La hora de fin debe ser posterior a la de inicio.' USING ERRCODE = 'P0001';
            END IF;
            IF v_inicio <= now() THEN
                RAISE EXCEPTION 'La fecha propuesta ya pasó: ajusta la fecha y la hora para aprobar la cátedra.' USING ERRCODE = 'P0001';
            END IF;
            IF v_modalidad IS NULL OR v_sede IS NULL OR v_periodo IS NULL THEN
                RAISE EXCEPTION 'Para aprobar hay que indicar la modalidad, la sede y el periodo de la cátedra nueva.'
                    USING ERRCODE = 'P0001';
            END IF;
            PERFORM fn_validar_periodo_y_sede(v_inicio, v_fin, v_periodo, v_sede);
            v_codigo := nullif(btrim(coalesce(p_id_evento_asis, '')), '');
            IF v_codigo IS NOT NULL AND v_codigo !~ '^[0-9]{9}$' THEN
                RAISE EXCEPTION 'El código ASIS debe tener 9 dígitos (con ceros a la izquierda).' USING ERRCODE = 'P0001';
            END IF;
        END IF;

        -- 1) La persona: por documento; si no, por correo institucional; si no existe, se crea como externa.
        SELECT d.fk_asistente INTO v_asistente
          FROM documento_asistente d
         WHERE d.fk_tipo_documento = v_sol.fk_tipo_documento
           AND d.numero            = v_sol.numero_documento
         ORDER BY d.vigente DESC, d.registrado_en DESC
         LIMIT 1;

        IF v_asistente IS NULL THEN
            SELECT a.id_asistente INTO v_asistente
              FROM asistente a
             WHERE a.correo_institucional = v_sol.correo_institucional;

            IF v_asistente IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM documento_asistente d WHERE d.fk_asistente = v_asistente AND d.vigente) THEN
                INSERT INTO documento_asistente (fk_asistente, fk_tipo_documento, numero)
                VALUES (v_asistente, v_sol.fk_tipo_documento, v_sol.numero_documento);
            END IF;
        END IF;

        IF v_asistente IS NULL THEN
            INSERT INTO asistente (nombres, apellidos, nombre_completo_asis,
                                   correo_institucional, celular, es_externo)
            VALUES (v_sol.nombres, v_sol.apellidos,
                    left(v_sol.nombres || ' ' || v_sol.apellidos, 200),
                    v_sol.correo_institucional, v_sol.telefono, true)
            RETURNING id_asistente INTO v_asistente;

            INSERT INTO documento_asistente (fk_asistente, fk_tipo_documento, numero)
            VALUES (v_asistente, v_sol.fk_tipo_documento, v_sol.numero_documento);
        END IF;

        -- 2) La cátedra nueva con su sesión (sp_guardar_catedra: todo o nada; el disparador de la 056 revisa periodos y horarios).
        IF v_sol.inicio_propuesto IS NOT NULL THEN
            IF v_codigo IS NULL THEN
                SELECT lpad((coalesce(max(c.id_evento_asis::bigint), 0) + 1)::text, 9, '0') INTO v_codigo FROM catedra c;
            END IF;

            CALL catedras.sp_guardar_catedra(
                v_codigo::character(9),
                left(coalesce(nullif(btrim(coalesce(p_nombre_catedra, '')), ''), v_sol.titulo_ponencia), 200)::character varying(200),
                coalesce(nullif(btrim(coalesce(p_fk_tipo_evento, '')), ''), 'CAAB')::character(4),
                NULL::character varying(15),
                true,
                jsonb_build_array(jsonb_build_object(
                    'inicio',       v_inicio,
                    'fin',          v_fin,
                    'fk_modalidad', btrim(v_modalidad),
                    'fk_sede',      btrim(v_sede),
                    'fk_periodo',   btrim(v_periodo),
                    'titulo',       left(v_sol.titulo_ponencia, 200),
                    'lugar',        nullif(btrim(coalesce(p_lugar, '')), ''))),
                v_creada);
            v_id_catedra := (v_creada ->> 'id_catedra')::bigint;
            v_id_sesion  := (v_creada -> 'sesiones' -> 0 ->> 'id_sesion')::bigint;

            UPDATE solicitud_ponente SET fk_catedra = v_id_catedra, fk_sesion = v_id_sesion
             WHERE id_solicitud = p_id_solicitud;
            v_sol.fk_sesion := v_id_sesion;
        END IF;

        -- 3) La participación en la sesión (la nueva, o la elegida en una solicitud anterior a la 057).
        IF v_sol.fk_sesion IS NOT NULL THEN
            INSERT INTO ponencia (fk_sesion, fk_asistente, rol)
            VALUES (v_sol.fk_sesion, v_asistente, 'PONENTE')
            ON CONFLICT DO NOTHING;
        END IF;

        UPDATE solicitud_ponente
           SET estado = 'APROBADA', observacion_admin = v_observacion, fk_asistente = v_asistente,
               resuelto_por = p_resuelto_por, resuelto_en = now()
         WHERE id_solicitud = p_id_solicitud;
    ELSE
        IF v_observacion IS NULL THEN
            RAISE EXCEPTION 'Para rechazar la solicitud hay que escribir el motivo.'
                USING ERRCODE = 'P0001';
        END IF;

        UPDATE solicitud_ponente
           SET estado = 'RECHAZADA', observacion_admin = v_observacion,
               resuelto_por = p_resuelto_por, resuelto_en = now()
         WHERE id_solicitud = p_id_solicitud;
    END IF;

    SELECT row_to_json(v) INTO p_resultado FROM v_solicitud_ponente v WHERE v.id_solicitud = p_id_solicitud;
END;
$$;

COMMENT ON PROCEDURE catedras.sp_crear_solicitud_ponente IS
    'v7.1 D58. Registra la postulación (cátedra nueva con fecha, hora, modalidad, sede y periodo) como PENDIENTE. P0001 si la fecha pasó, no cae en el periodo, el periodo o la sede no están activos, no cabe en los periodos de programación o ya hay una pendiente igual.';
COMMENT ON FUNCTION catedras.fn_validar_periodo_y_sede IS
    'v7.1 D58. P0001 si el periodo no existe, no está activo o no contiene la fecha, o si la sede no existe o no está activa.';


-- ----- migracion 059_separacion_entre_sesiones_enlace.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/059_separacion_entre_sesiones_enlace.sql
--  Proyecto Cátedras · SUBVERSIÓN v7.1 · D59
-- --------------------------------------------------------------------------
--  1. NO PUEDEN HABER DOS SESIONES A LA MISMA HORA, en ninguna modalidad ni sede,
--     y entre una y otra debe haber al menos 60 minutos. Todas las cátedras usan
--     el mismo enlace de ingreso, así que dos sesiones pegadas se pisarían.
--
--       - El tiempo mínimo vive en el parámetro MINUTOS_ENTRE_SESIONES (60 por defecto).
--       - Un disparador sobre 'sesion' (INSERT, o UPDATE que cambie inicio/fin) rechaza la
--         sesión que quede a menos de ese margen de otra (P0001 -> 409 en la API). Cubre
--         todo: formulario de cátedra, sesión suelta, aprobación de un ponente.
--         Sesiones que ya existían NO se tocan; solo cuenta lo que se cree o se mueva.
--       - Terminar a las 10:00 y empezar a las 11:00 es válido (margen exacto de 60 min).
--       - Solo cuentan las sesiones de cátedras ACTIVAS: una cátedra desactivada deja de ocupar el enlace
--         (si no, el histórico desactivado bloquearía horarios que ya nadie usa).
--       - sp_sesiones_en_conflicto suma el motivo 'HORARIO' (otra sesión a menos del
--         margen, sin importar la sede), para que los formularios avisen antes de guardar.
--       - El formulario «Soy ponente» también lo rechaza al proponer (fn_validar_periodo_y_sede).
--
--  2. ENLACE DE INGRESO al crear la cátedra: 'sesion.enlace_virtual' ya existía; ahora
--     sp_resolver_solicitud_ponente lo recibe (p_enlace_virtual) y lo guarda en la sesión
--     que crea al aprobar. Idempotente.
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  Parámetro
-- --------------------------------------------------------------------------
INSERT INTO catedras.parametro (clave, valor, tipo_dato, descripcion)
VALUES ('MINUTOS_ENTRE_SESIONES', '60', 'ENTERO',
        'Minutos mínimos entre el fin de una sesión y el inicio de otra (todas usan el mismo enlace de ingreso).')
ON CONFLICT (clave) DO NOTHING;

CREATE OR REPLACE FUNCTION catedras.fn_minutos_entre_sesiones()
    RETURNS integer
    LANGUAGE sql STABLE
    AS $$
    SELECT greatest(0, coalesce((SELECT p.valor::integer FROM catedras.parametro p
                                  WHERE p.clave = 'MINUTOS_ENTRE_SESIONES'), 60));
$$;

-- --------------------------------------------------------------------------
--  Valida un horario contra TODAS las sesiones (P0001 si no hay margen)
-- --------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION catedras.fn_validar_separacion(
    p_inicio timestamptz, p_fin timestamptz, p_excluir_sesion bigint DEFAULT NULL)
    RETURNS void
    LANGUAGE plpgsql STABLE
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_min integer := fn_minutos_entre_sesiones();
    v_gap interval := make_interval(mins => fn_minutos_entre_sesiones());
    v_otra record;
BEGIN
    SELECT s.numero_reunion, s.inicio, s.fin, c.nombre
      INTO v_otra
      FROM sesion s
      JOIN catedra c ON c.id_catedra = s.fk_catedra
     WHERE c.activo                       -- una cátedra desactivada ya no ocupa el enlace
       AND s.inicio < p_fin + v_gap
       AND s.fin    > p_inicio - v_gap
       AND (p_excluir_sesion IS NULL OR s.id_sesion <> p_excluir_sesion)
     ORDER BY s.inicio, s.id_sesion
     LIMIT 1;

    IF FOUND THEN
        RAISE EXCEPTION 'El horario % a % (hora de Colombia) choca con la cátedra «%» (reunión %, % a %): entre dos sesiones debe haber al menos % minutos, porque todas usan el mismo enlace de ingreso. Elige otra hora.',
            to_char(p_inicio AT TIME ZONE 'America/Bogota', 'YYYY-MM-DD HH24:MI'),
            to_char(p_fin AT TIME ZONE 'America/Bogota', 'HH24:MI'),
            v_otra.nombre, v_otra.numero_reunion,
            to_char(v_otra.inicio AT TIME ZONE 'America/Bogota', 'YYYY-MM-DD HH24:MI'),
            to_char(v_otra.fin AT TIME ZONE 'America/Bogota', 'HH24:MI'),
            v_min
            USING ERRCODE = 'P0001';
    END IF;
END;
$$;

-- --------------------------------------------------------------------------
--  Disparador sobre 'sesion'
-- --------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION catedras.fn_trg_sesion_separacion()
    RETURNS trigger
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    -- Corregir otros datos de una sesión existente no se bloquea: solo importa si cambia su horario.
    IF TG_OP = 'UPDATE' AND NEW.inicio = OLD.inicio AND NEW.fin = OLD.fin THEN
        RETURN NEW;
    END IF;

    -- Una a la vez: dos transacciones que crean sesiones juntas no se ven entre sí hasta confirmar.
    PERFORM pg_advisory_xact_lock(hashtext('catedras.sesion.separacion'));

    PERFORM fn_validar_separacion(NEW.inicio, NEW.fin, NEW.id_sesion);
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_sesion_separacion ON catedras.sesion;
CREATE TRIGGER trg_sesion_separacion
    BEFORE INSERT OR UPDATE OF inicio, fin ON catedras.sesion
    FOR EACH ROW EXECUTE FUNCTION catedras.fn_trg_sesion_separacion();

-- --------------------------------------------------------------------------
--  Consulta de conflictos: ahora también 'HORARIO' (a menos del margen de otra sesión)
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_sesiones_en_conflicto(
    IN    p_inicio         timestamptz,
    IN    p_fin            timestamptz,
    IN    p_fk_sede        text   DEFAULT NULL,
    IN    p_lugar          text   DEFAULT NULL,
    IN    p_fk_asistente   bigint DEFAULT NULL,
    IN    p_excluir_sesion bigint DEFAULT NULL,
    INOUT p_resultado      json   DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_sede_fisica boolean;
    v_lugar       text     := lower(btrim(coalesce(p_lugar, '')));
    v_gap         interval := make_interval(mins => fn_minutos_entre_sesiones());
BEGIN
    SELECT NOT s.es_virtual INTO v_sede_fisica FROM sede s WHERE s.id_sede = p_fk_sede;
    v_sede_fisica := coalesce(v_sede_fisica, false);

    SELECT COALESCE(json_agg(r ORDER BY r.inicio, r.id_sesion), '[]'::json)
      INTO p_resultado
      FROM (
            SELECT x.id_sesion, x.fk_catedra, x.nombre_catedra, x.numero_reunion,
                   x.inicio, x.fin, x.fk_sede, x.lugar, x.fk_modalidad, x.estado, x.solapa,
                   array_remove(ARRAY[
                       CASE WHEN x.solapa
                             AND v_sede_fisica
                             AND x.fk_sede = p_fk_sede
                             AND (v_lugar = ''
                                  OR btrim(coalesce(x.lugar, '')) = ''
                                  OR lower(btrim(x.lugar)) = v_lugar)
                            THEN 'LUGAR' END,
                       CASE WHEN x.solapa
                             AND p_fk_asistente IS NOT NULL
                             AND EXISTS (SELECT 1 FROM ponencia po
                                          WHERE po.fk_sesion = x.id_sesion AND po.fk_asistente = p_fk_asistente)
                            THEN 'PONENTE' END,
                       -- v7.1 D59: cualquier sesión a menos del margen, de cualquier sede o modalidad
                       -- (las sesiones de una cátedra desactivada no cuentan)
                       CASE WHEN x.catedra_activa THEN 'HORARIO' END
                   ], NULL) AS motivos
              FROM (SELECT s.id_sesion, s.fk_catedra, c.nombre AS nombre_catedra, s.numero_reunion,
                           s.inicio, s.fin, s.fk_sede, s.lugar, s.fk_modalidad, s.estado,
                           c.activo AS catedra_activa,
                           (s.inicio < p_fin AND s.fin > p_inicio) AS solapa
                      FROM sesion s
                      JOIN catedra c ON c.id_catedra = s.fk_catedra
                     WHERE s.inicio < p_fin + v_gap
                       AND s.fin    > p_inicio - v_gap
                       AND (p_excluir_sesion IS NULL OR s.id_sesion <> p_excluir_sesion)) x
           ) r
     WHERE cardinality(r.motivos) > 0;
END;
$$;

COMMENT ON PROCEDURE catedras.sp_sesiones_en_conflicto IS
    'v7.1 (D52, D59). Sesiones que chocan con un horario: LUGAR (se traslapan en la misma sede física y salón), PONENTE (ya participa) u HORARIO (a menos de MINUTOS_ENTRE_SESIONES de otra sesión, en cualquier sede). "solapa" dice si además se traslapan. Solo lectura.';

-- --------------------------------------------------------------------------
--  El formulario «Soy ponente» y la aprobación también revisan el margen
-- --------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION catedras.fn_validar_periodo_y_sede(
    p_inicio timestamptz, p_fin timestamptz, p_fk_periodo text, p_fk_sede text)
    RETURNS void
    LANGUAGE plpgsql STABLE
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_per periodo_academico%ROWTYPE;
    v_activa boolean;
BEGIN
    IF p_fk_periodo IS NOT NULL THEN
        SELECT * INTO v_per FROM periodo_academico WHERE id_periodo = p_fk_periodo;
        IF NOT FOUND THEN
            RAISE EXCEPTION 'El periodo académico % no existe.', p_fk_periodo USING ERRCODE = 'P0001';
        END IF;
        IF NOT v_per.activo THEN
            RAISE EXCEPTION 'El periodo académico % no está activo.', btrim(p_fk_periodo) USING ERRCODE = 'P0001';
        END IF;
        IF (p_inicio AT TIME ZONE 'America/Bogota')::date < v_per.fecha_ini
           OR (p_fin AT TIME ZONE 'America/Bogota')::date > v_per.fecha_fin THEN
            RAISE EXCEPTION 'La fecha de la cátedra (% a %, hora de Colombia) no cae dentro del periodo % (del % al %): elige otra fecha u otro periodo.',
                to_char(p_inicio AT TIME ZONE 'America/Bogota', 'YYYY-MM-DD HH24:MI'),
                to_char(p_fin AT TIME ZONE 'America/Bogota', 'YYYY-MM-DD HH24:MI'),
                btrim(p_fk_periodo), to_char(v_per.fecha_ini, 'YYYY-MM-DD'), to_char(v_per.fecha_fin, 'YYYY-MM-DD')
                USING ERRCODE = 'P0001';
        END IF;
    END IF;

    IF p_fk_sede IS NOT NULL THEN
        SELECT activo INTO v_activa FROM sede WHERE id_sede = p_fk_sede;
        IF NOT FOUND THEN
            RAISE EXCEPTION 'La sede % no existe.', p_fk_sede USING ERRCODE = 'P0001';
        END IF;
        IF NOT v_activa THEN
            RAISE EXCEPTION 'La sede % no está activa.', p_fk_sede USING ERRCODE = 'P0001';
        END IF;
    END IF;

    -- v7.1 D59: ninguna otra sesión a menos del margen (cualquier sede o modalidad).
    PERFORM fn_validar_separacion(p_inicio, p_fin);
END;
$$;

COMMENT ON FUNCTION catedras.fn_validar_periodo_y_sede IS
    'v7.1 D58/D59. P0001 si el periodo no existe, no está activo o no contiene la fecha, si la sede no existe o no está activa, o si otra sesión queda a menos de MINUTOS_ENTRE_SESIONES.';
COMMENT ON FUNCTION catedras.fn_validar_separacion IS
    'v7.1 D59. P0001 si el horario queda a menos de MINUTOS_ENTRE_SESIONES de cualquier otra sesión (todas usan el mismo enlace).';

-- --------------------------------------------------------------------------
--  RESOLVER — igual que la 058, más el enlace de ingreso de la cátedra que se crea
-- --------------------------------------------------------------------------
DROP PROCEDURE IF EXISTS catedras.sp_resolver_solicitud_ponente(bigint, boolean, text, bigint, text, text, text, text, text, text, text, timestamp with time zone, timestamp with time zone, json);

CREATE OR REPLACE PROCEDURE catedras.sp_resolver_solicitud_ponente(
    IN    p_id_solicitud   bigint,
    IN    p_aprobar        boolean,
    IN    p_observacion    text   DEFAULT NULL,
    IN    p_resuelto_por   bigint DEFAULT NULL,
    IN    p_nombre_catedra text   DEFAULT NULL,   -- vacío = el título de la ponencia
    IN    p_fk_tipo_evento text   DEFAULT NULL,   -- vacío = CAAB (cátedra abierta)
    IN    p_id_evento_asis text   DEFAULT NULL,   -- vacío = el siguiente código libre
    IN    p_fk_modalidad   text   DEFAULT NULL,   -- vacío = la que eligió el ponente
    IN    p_fk_sede        text   DEFAULT NULL,   -- vacío = la que eligió el ponente
    IN    p_fk_periodo     text   DEFAULT NULL,   -- vacío = el que eligió el ponente
    IN    p_lugar          text   DEFAULT NULL,
    IN    p_inicio         timestamp with time zone DEFAULT NULL,   -- vacío = el propuesto
    IN    p_fin            timestamp with time zone DEFAULT NULL,
    IN    p_enlace_virtual text   DEFAULT NULL,   -- enlace de ingreso (http/https); vacío = sin enlace
    INOUT p_resultado      json   DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_sol         solicitud_ponente%ROWTYPE;
    v_asistente   bigint;
    v_observacion text := nullif(btrim(coalesce(p_observacion, '')), '');
    v_enlace      text := nullif(btrim(coalesce(p_enlace_virtual, '')), '');
    v_inicio      timestamptz;
    v_fin         timestamptz;
    v_modalidad   text;
    v_sede        text;
    v_periodo     text;
    v_codigo      text;
    v_creada      json;
    v_id_catedra  bigint;
    v_id_sesion   bigint;
BEGIN
    SELECT * INTO v_sol FROM solicitud_ponente WHERE id_solicitud = p_id_solicitud FOR UPDATE;
    IF NOT FOUND THEN
        RETURN;
    END IF;

    IF v_sol.estado <> 'PENDIENTE' THEN
        RAISE EXCEPTION 'La solicitud % ya fue resuelta (%).', p_id_solicitud, v_sol.estado
            USING ERRCODE = 'P0001';
    END IF;

    IF p_aprobar THEN
        -- Antes de tocar nada: lo que la cátedra nueva necesita.
        IF v_sol.inicio_propuesto IS NOT NULL THEN
            v_inicio    := coalesce(p_inicio, v_sol.inicio_propuesto);
            v_fin       := coalesce(p_fin, v_sol.fin_propuesto);
            v_modalidad := coalesce(nullif(btrim(coalesce(p_fk_modalidad, '')), ''), v_sol.fk_modalidad);
            v_sede      := coalesce(nullif(btrim(coalesce(p_fk_sede, '')), ''), v_sol.fk_sede);
            v_periodo   := coalesce(nullif(btrim(coalesce(p_fk_periodo, '')), ''), btrim(v_sol.fk_periodo));
            IF v_fin <= v_inicio THEN
                RAISE EXCEPTION 'La hora de fin debe ser posterior a la de inicio.' USING ERRCODE = 'P0001';
            END IF;
            IF v_inicio <= now() THEN
                RAISE EXCEPTION 'La fecha propuesta ya pasó: ajusta la fecha y la hora para aprobar la cátedra.' USING ERRCODE = 'P0001';
            END IF;
            IF v_modalidad IS NULL OR v_sede IS NULL OR v_periodo IS NULL THEN
                RAISE EXCEPTION 'Para aprobar hay que indicar la modalidad, la sede y el periodo de la cátedra nueva.'
                    USING ERRCODE = 'P0001';
            END IF;
            IF v_enlace IS NOT NULL AND (v_enlace !~* '^https?://[^[:space:]]+$' OR length(v_enlace) > 300) THEN
                RAISE EXCEPTION 'El enlace de ingreso debe empezar por http:// o https:// y tener hasta 300 caracteres.'
                    USING ERRCODE = 'P0001';
            END IF;
            PERFORM fn_validar_periodo_y_sede(v_inicio, v_fin, v_periodo, v_sede);
            v_codigo := nullif(btrim(coalesce(p_id_evento_asis, '')), '');
            IF v_codigo IS NOT NULL AND v_codigo !~ '^[0-9]{9}$' THEN
                RAISE EXCEPTION 'El código ASIS debe tener 9 dígitos (con ceros a la izquierda).' USING ERRCODE = 'P0001';
            END IF;
        END IF;

        -- 1) La persona: por documento; si no, por correo institucional; si no existe, se crea como externa.
        SELECT d.fk_asistente INTO v_asistente
          FROM documento_asistente d
         WHERE d.fk_tipo_documento = v_sol.fk_tipo_documento
           AND d.numero            = v_sol.numero_documento
         ORDER BY d.vigente DESC, d.registrado_en DESC
         LIMIT 1;

        IF v_asistente IS NULL THEN
            SELECT a.id_asistente INTO v_asistente
              FROM asistente a
             WHERE a.correo_institucional = v_sol.correo_institucional;

            IF v_asistente IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM documento_asistente d WHERE d.fk_asistente = v_asistente AND d.vigente) THEN
                INSERT INTO documento_asistente (fk_asistente, fk_tipo_documento, numero)
                VALUES (v_asistente, v_sol.fk_tipo_documento, v_sol.numero_documento);
            END IF;
        END IF;

        IF v_asistente IS NULL THEN
            INSERT INTO asistente (nombres, apellidos, nombre_completo_asis,
                                   correo_institucional, celular, es_externo)
            VALUES (v_sol.nombres, v_sol.apellidos,
                    left(v_sol.nombres || ' ' || v_sol.apellidos, 200),
                    v_sol.correo_institucional, v_sol.telefono, true)
            RETURNING id_asistente INTO v_asistente;

            INSERT INTO documento_asistente (fk_asistente, fk_tipo_documento, numero)
            VALUES (v_asistente, v_sol.fk_tipo_documento, v_sol.numero_documento);
        END IF;

        -- 2) La cátedra nueva con su sesión (sp_guardar_catedra: todo o nada; los disparadores de la 056 y la 059
        --    revisan periodos/horarios y el margen entre sesiones).
        IF v_sol.inicio_propuesto IS NOT NULL THEN
            IF v_codigo IS NULL THEN
                SELECT lpad((coalesce(max(c.id_evento_asis::bigint), 0) + 1)::text, 9, '0') INTO v_codigo FROM catedra c;
            END IF;

            CALL catedras.sp_guardar_catedra(
                v_codigo::character(9),
                left(coalesce(nullif(btrim(coalesce(p_nombre_catedra, '')), ''), v_sol.titulo_ponencia), 200)::character varying(200),
                coalesce(nullif(btrim(coalesce(p_fk_tipo_evento, '')), ''), 'CAAB')::character(4),
                NULL::character varying(15),
                true,
                jsonb_build_array(jsonb_build_object(
                    'inicio',         v_inicio,
                    'fin',            v_fin,
                    'fk_modalidad',   btrim(v_modalidad),
                    'fk_sede',        btrim(v_sede),
                    'fk_periodo',     btrim(v_periodo),
                    'titulo',         left(v_sol.titulo_ponencia, 200),
                    'lugar',          nullif(btrim(coalesce(p_lugar, '')), ''),
                    'enlace_virtual', v_enlace)),
                v_creada);
            v_id_catedra := (v_creada ->> 'id_catedra')::bigint;
            v_id_sesion  := (v_creada -> 'sesiones' -> 0 ->> 'id_sesion')::bigint;

            UPDATE solicitud_ponente SET fk_catedra = v_id_catedra, fk_sesion = v_id_sesion
             WHERE id_solicitud = p_id_solicitud;
            v_sol.fk_sesion := v_id_sesion;
        END IF;

        -- 3) La participación en la sesión (la nueva, o la elegida en una solicitud anterior a la 057).
        IF v_sol.fk_sesion IS NOT NULL THEN
            INSERT INTO ponencia (fk_sesion, fk_asistente, rol)
            VALUES (v_sol.fk_sesion, v_asistente, 'PONENTE')
            ON CONFLICT DO NOTHING;
        END IF;

        UPDATE solicitud_ponente
           SET estado = 'APROBADA', observacion_admin = v_observacion, fk_asistente = v_asistente,
               resuelto_por = p_resuelto_por, resuelto_en = now()
         WHERE id_solicitud = p_id_solicitud;
    ELSE
        IF v_observacion IS NULL THEN
            RAISE EXCEPTION 'Para rechazar la solicitud hay que escribir el motivo.'
                USING ERRCODE = 'P0001';
        END IF;

        UPDATE solicitud_ponente
           SET estado = 'RECHAZADA', observacion_admin = v_observacion,
               resuelto_por = p_resuelto_por, resuelto_en = now()
         WHERE id_solicitud = p_id_solicitud;
    END IF;

    SELECT row_to_json(v) INTO p_resultado FROM v_solicitud_ponente v WHERE v.id_solicitud = p_id_solicitud;
END;
$$;

COMMENT ON PROCEDURE catedras.sp_resolver_solicitud_ponente IS
    'v7.1 D56-D59. Aprueba (crea la cátedra nueva con su sesión, enlace de ingreso incluido, y registra al ponente) o rechaza con motivo. P0001 si la solicitud ya se resolvió, falta un dato, el horario/periodo/sede no valen, el enlace no es http(s) o la sesión queda a menos de MINUTOS_ENTRE_SESIONES de otra.';


-- ----- migracion 060_sede_coordenadas.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/060_sede_coordenadas.sql
--  Proyecto Cátedras · SUBVERSIÓN v7.2 · RF-S10 / RF-S11 · D62, D63
-- --------------------------------------------------------------------------
--  Hasta la v7.1 la ubicación del navegador se GUARDABA pero no se comparaba
--  con nada: 'sede' solo tiene 'direccion' (texto). Esta migración deja a la
--  base de datos en condiciones de responder "¿esta persona está en la sede?":
--
--   1) 'sede' gana latitud, longitud y radio_metros (nulable);
--   2) parámetro RADIO_VALIDACION_METROS (radio global, 200 m);
--   3) coordenadas de BELLO y SAN_BENITO (confirmadas por el equipo el
--      2026-11-12; VIRTUAL no lleva);
--   4) fn_distancia_metros: distancia entre dos puntos (Haversine, sin
--      PostGIS: la constitución no admite extensiones nuevas);
--   5) los procedimientos de 'sede' devuelven y reciben las columnas nuevas.
--
--  COMPATIBILIDAD HASTA QUE SE ACTUALICE LA API (parte 2 de la entrega):
--  los parámetros nuevos de sp_crear_sede / sp_actualizar_sede van DESPUÉS de
--  p_resultado y tienen valor por defecto, así que la API actual (que llama con
--  los 6 argumentos de siempre) sigue funcionando. Para que un PUT/PATCH viejo
--  NO borre las coordenadas, sp_actualizar_sede solo las cambia si
--  p_actualizar_geo = true.
--
--  Idempotente (IF NOT EXISTS / CREATE OR REPLACE / ON CONFLICT): se puede
--  ejecutar varias veces. Si conservas el volumen de PostgreSQL hay que
--  aplicarla a mano (ver README, "Subversión v7.2").
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  1) Columnas y restricciones
--     'numeric(9,6)': seis decimales son ~0,11 m, más que cualquier GPS de
--     teléfono (mismo tipo que registro_asistencia, migración 045).
-- --------------------------------------------------------------------------
ALTER TABLE catedras.sede
    ADD COLUMN IF NOT EXISTS latitud       numeric(9,6),
    ADD COLUMN IF NOT EXISTS longitud      numeric(9,6),
    ADD COLUMN IF NOT EXISTS radio_metros  integer;

ALTER TABLE catedras.sede
    DROP CONSTRAINT IF EXISTS chk_sede_latitud,
    DROP CONSTRAINT IF EXISTS chk_sede_longitud,
    DROP CONSTRAINT IF EXISTS chk_sede_geo_par,
    DROP CONSTRAINT IF EXISTS chk_sede_radio,
    DROP CONSTRAINT IF EXISTS chk_sede_virtual_sin_geo;

ALTER TABLE catedras.sede
    ADD CONSTRAINT chk_sede_latitud   CHECK (latitud  IS NULL OR latitud  BETWEEN  -90 AND  90),
    ADD CONSTRAINT chk_sede_longitud  CHECK (longitud IS NULL OR longitud BETWEEN -180 AND 180),
    -- "las dos o ninguna": una coordenada sola no ubica nada (igual que la 045)
    ADD CONSTRAINT chk_sede_geo_par   CHECK ((latitud IS NULL) = (longitud IS NULL)),
    -- menos de 10 m es menor que el error normal de un GPS; más de 5 km ya no es "estar en la sede"
    ADD CONSTRAINT chk_sede_radio     CHECK (radio_metros IS NULL OR radio_metros BETWEEN 10 AND 5000),
    -- una sede virtual no está en ningún lugar
    ADD CONSTRAINT chk_sede_virtual_sin_geo CHECK (NOT es_virtual OR latitud IS NULL);

COMMENT ON COLUMN catedras.sede.latitud IS
    'v7.2 D62. Latitud (grados) del punto de la sede contra el que se compara la ubicación de quien registra asistencia en una sesión presencial. NULL en sedes virtuales. Va junto a longitud.';
COMMENT ON COLUMN catedras.sede.longitud IS
    'v7.2 D62. Longitud (grados) del punto de la sede. Va junto a latitud.';
COMMENT ON COLUMN catedras.sede.radio_metros IS
    'v7.2 D62. Radio propio de la sede, en metros (10 a 5000). NULL = usar el parámetro RADIO_VALIDACION_METROS.';

-- --------------------------------------------------------------------------
--  2) Parámetro global del radio
--     200 m: un GPS de teléfono al aire libre falla 5-20 m, pero dentro de un
--     edificio puede fallar 50-100 m, y un campus mide cientos de metros.
-- --------------------------------------------------------------------------
INSERT INTO catedras.parametro (clave, valor, tipo_dato, descripcion)
VALUES ('RADIO_VALIDACION_METROS', '200', 'ENTERO',
        'v7.2. Radio en metros alrededor de la sede dentro del cual se acepta la ubicación de quien registra asistencia en una sesión presencial. Cada sede puede tener el suyo (sede.radio_metros)')
ON CONFLICT (clave) DO NOTHING;

-- --------------------------------------------------------------------------
--  3) Coordenadas de las sedes (dato confirmado por el equipo, 2026-11-12).
--     'WHERE latitud IS NULL': si el administrador ya las corrigió desde la
--     pantalla de sedes, volver a ejecutar esta migración NO las pisa.
-- --------------------------------------------------------------------------
UPDATE catedras.sede SET latitud = 6.333129, longitud = -75.569174
 WHERE id_sede = 'BELLO' AND latitud IS NULL AND NOT es_virtual;

UPDATE catedras.sede SET latitud = 6.255095, longitud = -75.572969
 WHERE id_sede = 'SAN_BENITO' AND latitud IS NULL AND NOT es_virtual;

-- --------------------------------------------------------------------------
--  4) Distancia entre dos puntos, en metros (fórmula de Haversine)
--       a = sin²(Δlat/2) + cos(lat1)·cos(lat2)·sin²(Δlon/2)
--       d = 2·R·asin(√a)          con R = 6 371 008,8 m (radio medio terrestre)
--     'least(1, …)' evita que un error de redondeo deje √a en 1,0000000002 y
--     'asin' falle. Devuelve NULL si falta algún dato.
--     Es IMMUTABLE: mismo resultado para los mismos argumentos.
-- --------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION catedras.fn_distancia_metros(
    p_lat1 double precision,
    p_lon1 double precision,
    p_lat2 double precision,
    p_lon2 double precision
) RETURNS double precision
    LANGUAGE sql
    IMMUTABLE
    AS $$
    SELECT CASE
        WHEN p_lat1 IS NULL OR p_lon1 IS NULL OR p_lat2 IS NULL OR p_lon2 IS NULL THEN NULL
        ELSE 2 * 6371008.8 * asin(least(1.0, sqrt(
                 power(sin(radians(p_lat2 - p_lat1) / 2), 2)
               + cos(radians(p_lat1)) * cos(radians(p_lat2))
                 * power(sin(radians(p_lon2 - p_lon1) / 2), 2))))
    END;
$$;

COMMENT ON FUNCTION catedras.fn_distancia_metros IS
    'v7.2 D63. Distancia en metros entre (lat1,lon1) y (lat2,lon2) por Haversine, sin PostGIS. NULL si falta algún dato.';

-- --------------------------------------------------------------------------
--  5) Procedimientos de 'sede' con las columnas nuevas
-- --------------------------------------------------------------------------

-- LISTAR / CONSULTAR: misma firma que la 005; solo cambia el JSON, que ahora
-- trae las coordenadas, el radio propio y el radio EFECTIVO (el que de verdad
-- se aplicará: el de la sede o, si no tiene, el parámetro global).
CREATE OR REPLACE PROCEDURE catedras.sp_listar_sede(
    IN    p_limite            integer DEFAULT NULL,
    IN    p_incluir_inactivos boolean DEFAULT false,
    INOUT p_resultado         json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(s), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_sede, nombre, direccion, es_virtual, activo,
                   latitud, longitud, radio_metros,
                   COALESCE(radio_metros,
                            (SELECT valor::int FROM parametro WHERE clave = 'RADIO_VALIDACION_METROS'),
                            200) AS radio_efectivo_metros
              FROM sede
             WHERE p_incluir_inactivos OR activo
             ORDER BY id_sede
             LIMIT p_limite) s;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_consultar_sede(
    IN    p_id_sede   character varying(15),
    INOUT p_resultado json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(s) INTO p_resultado
      FROM (SELECT id_sede, nombre, direccion, es_virtual, activo,
                   latitud, longitud, radio_metros,
                   COALESCE(radio_metros,
                            (SELECT valor::int FROM parametro WHERE clave = 'RADIO_VALIDACION_METROS'),
                            200) AS radio_efectivo_metros
              FROM sede
             WHERE id_sede = p_id_sede) s;
END;
$$;

-- CREAR: cambia la firma (parámetros nuevos), así que se elimina la anterior;
-- si no, CREATE OR REPLACE dejaría una SOBRECARGA y 'CALL sp_crear_sede(...)'
-- quedaría ambiguo (mismo motivo que en la 005).
DROP PROCEDURE IF EXISTS catedras.sp_crear_sede(varchar, varchar, varchar, boolean, boolean, json);

CREATE OR REPLACE PROCEDURE catedras.sp_crear_sede(
    IN    p_id_sede      character varying(15),
    IN    p_nombre       character varying(80),
    IN    p_direccion    character varying(200) DEFAULT NULL,
    IN    p_es_virtual   boolean DEFAULT false,
    IN    p_activo       boolean DEFAULT true,
    INOUT p_resultado    json    DEFAULT NULL,
    -- v7.2: van DESPUÉS de p_resultado para no romper las llamadas anteriores
    IN    p_latitud      numeric DEFAULT NULL,
    IN    p_longitud     numeric DEFAULT NULL,
    IN    p_radio_metros integer DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    -- Las reglas (rango, "las dos o ninguna", radio 10-5000, virtual sin
    -- coordenadas) las hacen cumplir los CHECK de la tabla: si no se cumplen,
    -- la excepción del motor sube tal cual.
    INSERT INTO sede (id_sede, nombre, direccion, es_virtual, activo, latitud, longitud, radio_metros)
    VALUES (p_id_sede, p_nombre, p_direccion, p_es_virtual, COALESCE(p_activo, true),
            p_latitud, p_longitud, p_radio_metros);

    CALL catedras.sp_consultar_sede(p_id_sede, p_resultado);
END;
$$;

-- ACTUALIZAR: reemplazo completo de las columnas de siempre. Las coordenadas y
-- el radio SOLO se cambian si p_actualizar_geo = true (la API nueva lo manda);
-- una llamada anterior a la v7.2 las deja como están.
DROP PROCEDURE IF EXISTS catedras.sp_actualizar_sede(varchar, varchar, varchar, boolean, boolean, json);

CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_sede(
    IN    p_id_sede         character varying(15),
    IN    p_nombre          character varying(80),
    IN    p_direccion       character varying(200),
    IN    p_es_virtual      boolean,
    IN    p_activo          boolean,
    INOUT p_resultado       json DEFAULT NULL,
    IN    p_latitud         numeric DEFAULT NULL,
    IN    p_longitud        numeric DEFAULT NULL,
    IN    p_radio_metros    integer DEFAULT NULL,
    IN    p_actualizar_geo  boolean DEFAULT false
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE sede
       SET nombre       = p_nombre,
           direccion    = p_direccion,
           es_virtual   = p_es_virtual,
           activo       = p_activo,
           latitud      = CASE WHEN COALESCE(p_actualizar_geo, false) THEN p_latitud      ELSE latitud      END,
           longitud     = CASE WHEN COALESCE(p_actualizar_geo, false) THEN p_longitud     ELSE longitud     END,
           radio_metros = CASE WHEN COALESCE(p_actualizar_geo, false) THEN p_radio_metros ELSE radio_metros END
     WHERE id_sede = p_id_sede;

    -- Sin FOUND -> no existía -> p_resultado queda NULL (criterio de siempre).
    IF FOUND THEN
        CALL catedras.sp_consultar_sede(p_id_sede, p_resultado);
    END IF;
END;
$$;

COMMENT ON PROCEDURE catedras.sp_crear_sede IS
    'Retrofit criterio 3 + v7.2: devuelve la fila como quedó guardada; acepta coordenadas y radio (después de p_resultado, con valor por defecto).';
COMMENT ON PROCEDURE catedras.sp_actualizar_sede IS
    'Retrofit criterio 3 + v7.2: reemplazo completo; coordenadas y radio solo cambian con p_actualizar_geo = true.';


-- ----- migracion 061_registro_publico_columnas.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/061_registro_publico_columnas.sql
--  Proyecto Cátedras · SUBVERSIÓN v7.2 · RF-S8 / RF-S9 · D65, D66, D67
-- --------------------------------------------------------------------------
--  Prepara las tablas para el registro público SIN clave por correo (D60):
--
--   1) registro_asistencia: cómo se identificó quien registró
--      (identificado_por), su facultad (docentes), su procedencia (externos)
--      y la distancia a la sede (auditoría de la ubicación);
--   2) chk_registro_clave admite "identificado por carnet o declarado" además
--      de "con clave" y "MANUAL/IMPORTADO";
--   3) asistente.contacto_sin_correo: permite una persona sin correo SOLO
--      cuando lo marca un procedimiento de la v7.2 (estudiantes cargados por
--      archivo; docentes/externos que dan solo su celular);
--   4) DOCENTE deja de exigir programa (si no, RN-07 rechazaría a todo docente);
--   5) listar / consultar / crear de registro_asistencia devuelven las columnas
--      nuevas (misma firma que la 045).
--
--  'origen' NO se toca: sigue diciendo por qué canal entró (QR o ENLACE).
--
--  Idempotente (IF NOT EXISTS / DROP CONSTRAINT IF EXISTS / CREATE OR REPLACE).
--  Si conservas el volumen de PostgreSQL hay que aplicarla a mano.
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  1) registro_asistencia: columnas nuevas
--     Son HECHOS del día del registro (como fk_programa_snapshot): nunca se
--     actualizan después. Las filas anteriores quedan con 'CLAVE' (el flujo de
--     la v5/v7.1) y el resto en NULL.
-- --------------------------------------------------------------------------
ALTER TABLE catedras.registro_asistencia
    ADD COLUMN IF NOT EXISTS identificado_por     varchar(12) NOT NULL DEFAULT 'CLAVE',
    ADD COLUMN IF NOT EXISTS fk_facultad_snapshot varchar(10),
    ADD COLUMN IF NOT EXISTS procedencia          varchar(150),
    ADD COLUMN IF NOT EXISTS distancia_metros     numeric(9,1);

-- La facultad es una referencia al catálogo (Regla 2). Se agrega aparte porque
-- ADD COLUMN IF NOT EXISTS ... REFERENCES no repite la FK en una segunda corrida.
ALTER TABLE catedras.registro_asistencia
    DROP CONSTRAINT IF EXISTS fk_registro_facultad_snap;
ALTER TABLE catedras.registro_asistencia
    ADD CONSTRAINT fk_registro_facultad_snap FOREIGN KEY (fk_facultad_snapshot)
        REFERENCES catedras.facultad (id_facultad) ON UPDATE CASCADE ON DELETE RESTRICT;

ALTER TABLE catedras.registro_asistencia
    DROP CONSTRAINT IF EXISTS chk_registro_identificado,
    DROP CONSTRAINT IF EXISTS chk_registro_facultad_docente,
    DROP CONSTRAINT IF EXISTS chk_registro_procedencia_externo,
    DROP CONSTRAINT IF EXISTS chk_registro_distancia,
    DROP CONSTRAINT IF EXISTS chk_registro_clave;

ALTER TABLE catedras.registro_asistencia
    -- CLAVE = flujo de la v5/v7.1 · CARNET = estudiante por código · DECLARADO = docente/externo por formulario
    ADD CONSTRAINT chk_registro_identificado CHECK (identificado_por IN ('CLAVE', 'CARNET', 'DECLARADO')),
    -- la facultad solo la guardan los docentes; la procedencia, los externos
    ADD CONSTRAINT chk_registro_facultad_docente CHECK (fk_facultad_snapshot IS NULL OR fk_vinculacion_snapshot = 'DOCENTE'),
    ADD CONSTRAINT chk_registro_procedencia_externo CHECK (procedencia IS NULL OR fk_vinculacion_snapshot = 'EXTERNO'),
    -- la distancia solo existe si hubo ubicación, y no es negativa
    ADD CONSTRAINT chk_registro_distancia CHECK (distancia_metros IS NULL OR (latitud IS NOT NULL AND distancia_metros >= 0)),
    -- Reemplaza al de la v5: sin clave solo se admite si es MANUAL/IMPORTADO o
    -- si la persona se identificó por carnet / por formulario (v7.2).
    ADD CONSTRAINT chk_registro_clave CHECK (
        fk_clave IS NOT NULL
        OR origen IN ('MANUAL', 'IMPORTADO')
        OR identificado_por IN ('CARNET', 'DECLARADO'));

COMMENT ON COLUMN catedras.registro_asistencia.identificado_por IS
    'v7.2 D65. CLAVE = clave de un solo uso por correo (v5/v7.1, y filas anteriores); CARNET = estudiante por código; DECLARADO = docente o externo por formulario. No reemplaza a origen (que dice el canal: QR o ENLACE).';
COMMENT ON COLUMN catedras.registro_asistencia.fk_facultad_snapshot IS
    'v7.2 D66. INSTANTÁNEA: la facultad que declaró un DOCENTE el día de la cátedra. NULL para los demás. Nunca se actualiza.';
COMMENT ON COLUMN catedras.registro_asistencia.procedencia IS
    'v7.2 D66. INSTANTÁNEA: de dónde dijo venir un EXTERNO (texto libre). NULL para los demás. Nunca se actualiza.';
COMMENT ON COLUMN catedras.registro_asistencia.distancia_metros IS
    'v7.2 D66. Distancia en metros entre la ubicación enviada y la sede de la sesión, calculada por la BD (fn_distancia_metros). Solo en registros de canal QR; NULL en el resto.';

-- --------------------------------------------------------------------------
--  3) asistente.contacto_sin_correo (D67)
--     chk_asistente_correo exigía correo institucional o personal. El celular
--     no cuenta, así que un docente o externo que dé solo su celular lo
--     violaría. La marca la ponen SOLO los procedimientos de la v7.2: la API de
--     Asistente y la carga antigua no la tocan y siguen exigiendo un correo.
-- --------------------------------------------------------------------------
ALTER TABLE catedras.asistente
    ADD COLUMN IF NOT EXISTS contacto_sin_correo boolean NOT NULL DEFAULT false;

ALTER TABLE catedras.asistente
    DROP CONSTRAINT IF EXISTS chk_asistente_correo;

ALTER TABLE catedras.asistente
    ADD CONSTRAINT chk_asistente_correo CHECK (
        correo_institucional IS NOT NULL
        OR correo_personal IS NOT NULL
        OR contacto_sin_correo);

COMMENT ON COLUMN catedras.asistente.contacto_sin_correo IS
    'v7.2 D67. true = la persona se creó sin correo (estudiante cargado por archivo, o docente/externo que dio solo celular). Solo la ponen los procedimientos de la v7.2; la API de Asistente no la usa.';

-- --------------------------------------------------------------------------
--  4) DOCENTE no exige programa (D67, decidido el 2026-11-12)
--     El script provisto lo dejó en true; RN-07 (fn_trg_registro_validar)
--     rechaza el registro si la vinculación exige programa y no hay uno
--     activo. Un docente declara su FACULTAD, no un programa. Es el mecanismo
--     que documenta el propio script: "habilitar una excepción es un UPDATE".
--     Solo afecta registros NUEVOS (la instantánea de los anteriores no cambia).
-- --------------------------------------------------------------------------
UPDATE catedras.tipo_vinculacion
   SET exige_programa = false
 WHERE id_tipo_vinculacion = 'DOCENTE' AND exige_programa;

-- --------------------------------------------------------------------------
--  5) Listar / consultar / crear de registro_asistencia, con las columnas
--     nuevas (misma firma que la 045: CREATE OR REPLACE sin tocar la API).
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_listar_registro_asistencia(
    IN    p_fk_sesion    bigint  DEFAULT NULL,
    IN    p_fk_asistente bigint  DEFAULT NULL,
    IN    p_limite       integer DEFAULT NULL,
    INOUT p_resultado    json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(r), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_registro, fk_sesion, fk_asistente, fk_enlace, fk_clave,
                   registrado_en, fk_programa_snapshot, fk_vinculacion_snapshot,
                   origen, host(ip) AS ip, user_agent, latitud, longitud,
                   identificado_por, fk_facultad_snapshot, procedencia, distancia_metros
              FROM registro_asistencia
             WHERE (p_fk_sesion    IS NULL OR fk_sesion    = p_fk_sesion)
               AND (p_fk_asistente IS NULL OR fk_asistente = p_fk_asistente)
             ORDER BY id_registro
             LIMIT p_limite) r;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_consultar_registro_asistencia(
    IN    p_id_registro bigint,
    INOUT p_resultado   json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(r) INTO p_resultado
      FROM (SELECT id_registro, fk_sesion, fk_asistente, fk_enlace, fk_clave,
                   registrado_en, fk_programa_snapshot, fk_vinculacion_snapshot,
                   origen, host(ip) AS ip, user_agent, latitud, longitud,
                   identificado_por, fk_facultad_snapshot, procedencia, distancia_metros
              FROM registro_asistencia
             WHERE id_registro = p_id_registro) r;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_crear_registro_asistencia(
    IN    p_fk_sesion    bigint,
    IN    p_fk_asistente bigint,
    IN    p_origen       character varying(15) DEFAULT 'QR',
    INOUT p_resultado    json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_id bigint;
BEGIN
    INSERT INTO registro_asistencia (fk_sesion, fk_asistente, origen)
    VALUES (p_fk_sesion, p_fk_asistente, COALESCE(p_origen, 'QR'))
    RETURNING id_registro INTO v_id;

    CALL catedras.sp_consultar_registro_asistencia(v_id, p_resultado);
END;
$$;

-- sp_actualizar_registro_asistencia (037) NO cambia: las columnas nuevas son
-- parte del HECHO y no se corrigen después.


-- ----- migracion 062_qr_exige_sede_con_coordenadas.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/062_qr_exige_sede_con_coordenadas.sql
--  Proyecto Cátedras · SUBVERSIÓN v7.2 · RF-S12 · D61, D72
-- --------------------------------------------------------------------------
--  1) El QR de una sesión de canal QR (presencial) NO se genera si su sede no
--     tiene coordenadas: falla AL GENERAR el QR, con un mensaje que dice qué
--     sede completar, y no el día del evento cuando alguien intenta registrarse.
--     (Es lo único que cambia de sp_emitir_qr_sesion respecto de la 054.)
--  2) La modalidad HIBRIDA se desactiva (decidido el 2026-11-12, P1/P1b): ya
--     no se programan sesiones híbridas. Las que existan siguen existiendo y,
--     como su canal es QR, se comportan como presenciales (D61).
--
--  El error es 'P0001' (raise_exception): la API lo devuelve como 409, igual
--  que las reglas de negocio de las migraciones 055-059.
--
--  Idempotente (CREATE OR REPLACE / UPDATE condicionado).
-- ==========================================================================

SET search_path TO catedras, public;

CREATE OR REPLACE PROCEDURE catedras.sp_emitir_qr_sesion(
    IN    p_fk_sesion  bigint,
    IN    p_fk_usuario bigint  DEFAULT NULL,
    IN    p_regenerar  boolean DEFAULT false,
    INOUT p_resultado  json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_fin        timestamptz;
    v_canal      varchar(10);
    v_id_sede    varchar(15);
    v_nom_sede   varchar(80);
    v_sede_virt  boolean;
    v_sede_lat   numeric;
    v_id         bigint;
    v_antes      integer;
    v_desp       integer;
    v_base       text;
    v_token      uuid := gen_random_uuid();
    v_existia    boolean := false;
BEGIN
    SELECT s.fin, m.canal_difusion, sd.id_sede, sd.nombre, sd.es_virtual, sd.latitud
      INTO v_fin, v_canal, v_id_sede, v_nom_sede, v_sede_virt, v_sede_lat
      FROM sesion s
      JOIN modalidad m ON m.id_modalidad = s.fk_modalidad
      JOIN sede sd     ON sd.id_sede     = s.fk_sede
     WHERE s.id_sesion = p_fk_sesion;
    IF v_fin IS NULL THEN
        p_resultado := NULL;
        RETURN;
    END IF;

    -- RN-13 (script provisto): el canal del enlace debe concordar con la modalidad:
    -- presencial -> QR, virtual y telepresencial -> ENLACE, híbrida (AMBOS) -> QR.
    v_canal := CASE WHEN v_canal = 'ENLACE' THEN 'ENLACE' ELSE 'QR' END;

    -- v7.2 D72 · una sesión de canal QR se valida contra su sede; sin sede
    -- física con coordenadas no hay con qué comparar, así que no se emite el QR.
    IF v_canal = 'QR' THEN
        IF v_sede_virt THEN
            RAISE EXCEPTION 'La sesión % se difunde por QR (presencial) pero su sede "%" (%) es virtual: cambia la sede o la modalidad antes de generar el QR',
                p_fk_sesion, v_nom_sede, v_id_sede
                USING ERRCODE = 'P0001';
        END IF;
        IF v_sede_lat IS NULL THEN
            RAISE EXCEPTION 'La sede "%" (%) no tiene coordenadas: complétalas en Administración > Sedes antes de generar el QR de la sesión %',
                v_nom_sede, v_id_sede, p_fk_sesion
                USING ERRCODE = 'P0001';
        END IF;
    END IF;

    IF NOT coalesce(p_regenerar, false) THEN
        SELECT e.id_enlace INTO v_id
          FROM enlace_registro e
         WHERE e.fk_sesion = p_fk_sesion AND e.revocado_en IS NULL AND e.qr_cierre
         ORDER BY e.id_enlace DESC LIMIT 1;
        v_existia := v_id IS NOT NULL;
    END IF;

    IF v_id IS NULL THEN
        v_antes := greatest(coalesce((SELECT valor::int FROM parametro WHERE clave = 'QR_MINUTOS_ANTES_FIN'), 15), 1);
        v_desp  := greatest(coalesce((SELECT valor::int FROM parametro WHERE clave = 'QR_MINUTOS_DESPUES_FIN'), 0), 0);
        v_base  := coalesce((SELECT valor FROM parametro WHERE clave = 'URL_BASE'), 'https://catedras.usbmed.edu.co/r/');

        -- RN-14 · un solo enlace vigente por sesión
        UPDATE enlace_registro SET revocado_en = now()
         WHERE fk_sesion = p_fk_sesion AND revocado_en IS NULL;

        INSERT INTO enlace_registro (fk_sesion, token, url_publica, ruta_imagen_qr, canal, ventana,
                                     fk_usuario_crea, requiere_inscripcion, qr_cierre)
        VALUES (p_fk_sesion, v_token, v_base || v_token::text, 'qr/' || v_token::text || '.png', v_canal,
                tstzrange(v_fin - make_interval(mins => v_antes), v_fin + make_interval(mins => v_desp), '[)'),
                p_fk_usuario, false, true)
        RETURNING id_enlace INTO v_id;
    END IF;

    SELECT row_to_json(r) INTO p_resultado
      FROM (SELECT e.id_enlace, e.fk_sesion, e.token, e.canal, lower(e.ventana) AS ventana_desde,
                   upper(e.ventana) AS ventana_hasta, e.revocado_en,
                   v_existia AS ya_existia
              FROM enlace_registro e WHERE e.id_enlace = v_id) r;
END;
$$;

COMMENT ON PROCEDURE catedras.sp_emitir_qr_sesion IS
    'v7.1 D53/D54 + v7.2 D72. QR de cierre: ventana [fin - QR_MINUTOS_ANTES_FIN, fin + QR_MINUTOS_DESPUES_FIN). Si el canal es QR exige una sede física con coordenadas (P0001 si no). NULL = la sesión no existe.';

-- --------------------------------------------------------------------------
--  Modalidad HIBRIDA fuera de uso (P1b). No se borra: hay FK desde 'sesion'.
-- --------------------------------------------------------------------------
UPDATE catedras.modalidad SET activo = false
 WHERE id_modalidad = 'HIBRIDA' AND activo;


-- ----- migracion 063_sp_registro_publico.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/063_sp_registro_publico.sql
--  Proyecto Cátedras · SUBVERSIÓN v7.2 · RF-S9 / RF-S10 · D60, D61, D63, D64, D65, D66, D67
-- --------------------------------------------------------------------------
--  Registro público de asistencia SIN clave por correo (D60). Tras escanear el
--  QR, la persona elige ser estudiante, docente o externo:
--
--    fn_consultar_enlace_publico(token)   datos para pintar la página
--    fn_verificar_ubicacion(token,lat,lng) aviso temprano: ¿estás en la sede?
--    sp_registrar_asistencia_estudiante   carnet
--    sp_registrar_asistencia_docente      cédula, nombre, contacto, facultad
--    sp_registrar_asistencia_externo      cédula, nombre, contacto, procedencia
--
--  Los tres procedimientos validan, EN UNA SOLA TRANSACCIÓN (Guía IA, Regla 1):
--  enlace (existe, no revocado, dentro de su ventana) -> ubicación (solo si el
--  canal es QR) -> persona -> vinculación -> duplicado -> INSERT. La BD no
--  confía en la API ni la API en el front.
--
--  ESTADO EN VEZ DE EXCEPCIÓN (D64, mismo criterio que la D41): los casos de
--  negocio devuelven un texto en p_estado y NO insertan nada. Los errores
--  inesperados (FK, CHECK) siguen subiendo como excepción (500).
--
--    OK · ENLACE_NO_EXISTE · ENLACE_REVOCADO · FUERA_DE_VENTANA · SIN_UBICACION
--    SEDE_SIN_COORDENADAS · FUERA_DE_RADIO · ESTUDIANTE_NO_EXISTE
--    SIN_VINCULACION_VIGENTE · PERSONA_INACTIVA · YA_REGISTRADO · DATOS_INVALIDOS
--
--  UBICACIÓN POR CANAL (D61): canal QR -> se exige y se compara con la sede;
--  canal ENLACE -> no se pide, y latitud/longitud/distancia quedan en NULL
--  aunque el navegador las hubiera enviado.
--
--  Quedan 'ip' y 'user_agent' siempre (las columnas ya existían, v5).
--  El disparador trg_registro_validar (RN-15, snapshots, RN-07) sigue activo
--  como SEGUNDA barrera.
--
--  Idempotente (CREATE OR REPLACE). Si conservas el volumen de PostgreSQL hay
--  que aplicarla a mano.
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  0) Pieza común: el enlace y la ubicación.
--     La usan los tres procedimientos y el aviso temprano, para que haya UNA
--     sola regla. Devuelve el estado y, si es OK, lo que hace falta insertar.
--     STABLE: solo lee (y mira now()).
-- --------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION catedras.fn_validar_enlace_y_ubicacion(
    IN  p_token     uuid,
    IN  p_latitud   numeric,
    IN  p_longitud  numeric,
    OUT o_estado    text,
    OUT o_id_enlace bigint,
    OUT o_id_sesion bigint,
    OUT o_canal     text,
    OUT o_latitud   numeric,     -- lo que se guardará (NULL si el canal es ENLACE)
    OUT o_longitud  numeric,
    OUT o_distancia numeric,     -- metros, redondeada a 0,1; NULL si el canal es ENLACE
    OUT o_radio     integer      -- radio que se aplicó; NULL si el canal es ENLACE
)
    LANGUAGE plpgsql
    STABLE
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_ventana   tstzrange;
    v_revocado  timestamptz;
    v_sede_virt boolean;
    v_sede_lat  numeric;
    v_sede_lon  numeric;
    v_sede_rad  integer;
    v_dist      double precision;
BEGIN
    SELECT e.id_enlace, e.fk_sesion, e.canal, e.ventana, e.revocado_en
      INTO o_id_enlace, o_id_sesion, o_canal, v_ventana, v_revocado
      FROM enlace_registro e
     WHERE e.token = p_token;

    IF NOT FOUND THEN
        o_estado := 'ENLACE_NO_EXISTE';  RETURN;
    END IF;
    IF v_revocado IS NOT NULL THEN
        o_estado := 'ENLACE_REVOCADO';   RETURN;
    END IF;
    IF NOT (v_ventana @> now()) THEN
        o_estado := 'FUERA_DE_VENTANA';  RETURN;
    END IF;

    -- Canal ENLACE (virtual/telepresencial): no se pide ni se valida ubicación.
    IF o_canal <> 'QR' THEN
        o_latitud := NULL;  o_longitud := NULL;  o_distancia := NULL;  o_radio := NULL;
        o_estado := 'OK';   RETURN;
    END IF;

    -- Canal QR (presencial): la ubicación es obligatoria y debe caer en la sede.
    IF p_latitud IS NULL OR p_longitud IS NULL THEN
        o_estado := 'SIN_UBICACION';     RETURN;
    END IF;
    IF p_latitud NOT BETWEEN -90 AND 90 OR p_longitud NOT BETWEEN -180 AND 180 THEN
        o_estado := 'DATOS_INVALIDOS';   RETURN;
    END IF;

    SELECT sd.es_virtual, sd.latitud, sd.longitud, sd.radio_metros
      INTO v_sede_virt, v_sede_lat, v_sede_lon, v_sede_rad
      FROM sesion s JOIN sede sd ON sd.id_sede = s.fk_sede
     WHERE s.id_sesion = o_id_sesion;

    -- Sin coordenadas de sede NO se acepta la asistencia sin comprobar el lugar.
    IF v_sede_virt OR v_sede_lat IS NULL OR v_sede_lon IS NULL THEN
        o_estado := 'SEDE_SIN_COORDENADAS';  RETURN;
    END IF;

    -- Radio efectivo: el de la sede, o el parámetro global, o 200 por si falta.
    o_radio := COALESCE(v_sede_rad,
                        (SELECT valor::int FROM parametro WHERE clave = 'RADIO_VALIDACION_METROS'),
                        200);
    v_dist  := catedras.fn_distancia_metros(p_latitud::double precision, p_longitud::double precision,
                                            v_sede_lat::double precision, v_sede_lon::double precision);
    o_distancia := round(v_dist::numeric, 1);
    o_latitud   := p_latitud;
    o_longitud  := p_longitud;

    IF v_dist > o_radio THEN
        o_estado := 'FUERA_DE_RADIO';    RETURN;
    END IF;

    o_estado := 'OK';
END;
$$;

COMMENT ON FUNCTION catedras.fn_validar_enlace_y_ubicacion IS
    'v7.2 D61/D64. Valida enlace (existe, no revocado, en ventana) y, si el canal es QR, la ubicación contra la sede. Devuelve el estado y los datos a guardar.';

-- --------------------------------------------------------------------------
--  1) Datos del enlace para pintar la página pública (no incluye las
--     coordenadas de la sede). NULL = el token no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION catedras.fn_consultar_enlace_publico(p_token uuid)
    RETURNS json
    LANGUAGE plpgsql
    STABLE
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_res json;
BEGIN
    SELECT row_to_json(r) INTO v_res
      FROM (SELECT e.id_enlace,
                   s.id_sesion,
                   s.titulo                               AS titulo_sesion,
                   s.numero_reunion,
                   c.id_evento_asis                       AS id_catedra,
                   c.nombre                               AS titulo_catedra,
                   s.inicio, s.fin,
                   s.fk_modalidad                         AS modalidad,
                   e.canal,
                   (e.canal = 'QR')                       AS requiere_ubicacion,
                   lower(e.ventana)                       AS ventana_desde,
                   upper(e.ventana)                       AS ventana_hasta,
                   CASE WHEN e.revocado_en IS NOT NULL THEN 'revocado'
                        WHEN now() <  lower(e.ventana)    THEN 'pendiente'
                        WHEN e.ventana @> now()           THEN 'abierto'
                        ELSE 'cerrado' END                AS estado_ventana
              FROM enlace_registro e
              JOIN sesion  s ON s.id_sesion   = e.fk_sesion
              JOIN catedra c ON c.id_catedra  = s.fk_catedra
             WHERE e.token = p_token) r;
    RETURN v_res;
END;
$$;

COMMENT ON FUNCTION catedras.fn_consultar_enlace_publico IS
    'v7.2 RF-S9. Título de la cátedra y la sesión, canal, si requiere ubicación y estado de la ventana. NULL si el token no existe. No devuelve coordenadas.';

-- --------------------------------------------------------------------------
--  2) Aviso temprano de ubicación (no guarda nada).
--     { estado, requiere_ubicacion, dentro, distancia_metros, radio_metros }
-- --------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION catedras.fn_verificar_ubicacion(
    p_token    uuid,
    p_latitud  numeric,
    p_longitud numeric
) RETURNS json
    LANGUAGE plpgsql
    STABLE
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v record;
BEGIN
    SELECT * INTO v FROM catedras.fn_validar_enlace_y_ubicacion(p_token, p_latitud, p_longitud);

    RETURN json_build_object(
        'estado',             v.o_estado,
        'requiere_ubicacion', (v.o_canal = 'QR'),
        'dentro',             CASE WHEN v.o_canal IS DISTINCT FROM 'QR' THEN NULL
                                   WHEN v.o_estado = 'OK'              THEN true
                                   WHEN v.o_estado = 'FUERA_DE_RADIO'  THEN false
                                   ELSE NULL END,
        'distancia_metros',   v.o_distancia,
        'radio_metros',       v.o_radio);
END;
$$;

COMMENT ON FUNCTION catedras.fn_verificar_ubicacion IS
    'v7.2 RF-S10. Aviso temprano: dice si la ubicación cae dentro del radio de la sede de la sesión del token. No guarda nada; el procedimiento de registro la vuelve a verificar.';

-- --------------------------------------------------------------------------
--  3) ESTUDIANTE: identificado por su carnet (asistente.id_asis)
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_registrar_asistencia_estudiante(
    IN    p_token            uuid,
    IN    p_carnet           text,
    IN    p_latitud          numeric DEFAULT NULL,
    IN    p_longitud         numeric DEFAULT NULL,
    IN    p_ip               inet    DEFAULT NULL,
    IN    p_user_agent       text    DEFAULT NULL,
    INOUT p_estado           text    DEFAULT NULL,
    INOUT p_id_registro      bigint  DEFAULT NULL,
    INOUT p_distancia_metros numeric DEFAULT NULL,
    INOUT p_radio_metros     integer DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_carnet    text := btrim(COALESCE(p_carnet, ''));
    v_asistente bigint;
    e           record;
BEGIN
    p_id_registro := NULL;

    -- 0) forma del dato (mismo patrón que chk_asistente_idasis)
    IF v_carnet !~ '^[0-9A-Za-z]{5,15}$' THEN
        p_estado := 'DATOS_INVALIDOS';  RETURN;
    END IF;

    -- 1) enlace y ubicación (una sola regla: fn_validar_enlace_y_ubicacion)
    SELECT * INTO e FROM catedras.fn_validar_enlace_y_ubicacion(p_token, p_latitud, p_longitud);
    p_distancia_metros := e.o_distancia;
    p_radio_metros     := e.o_radio;
    IF e.o_estado <> 'OK' THEN
        p_estado := e.o_estado;  RETURN;
    END IF;

    -- 2) la persona: debe existir como estudiante cargado y estar activa
    SELECT a.id_asistente INTO v_asistente
      FROM asistente a
     WHERE a.id_asis = v_carnet AND a.activo;
    IF v_asistente IS NULL THEN
        p_estado := 'ESTUDIANTE_NO_EXISTE';  RETURN;
    END IF;

    -- 3) con vinculación ESTUDIANTE vigente (sin fecha de fin)
    IF NOT EXISTS (SELECT 1 FROM vinculacion_asistente v
                    WHERE v.fk_asistente = v_asistente
                      AND v.fk_tipo_vinculacion = 'ESTUDIANTE'
                      AND v.fecha_fin IS NULL) THEN
        p_estado := 'SIN_VINCULACION_VIGENTE';  RETURN;
    END IF;

    -- 4) una persona no se registra dos veces en la misma sesión
    IF EXISTS (SELECT 1 FROM registro_asistencia r
                WHERE r.fk_sesion = e.o_id_sesion AND r.fk_asistente = v_asistente) THEN
        p_estado := 'YA_REGISTRADO';  RETURN;
    END IF;

    -- 5) el registro. La vinculación se pasa EXPLÍCITA: si la persona tuviera
    --    varias vigentes, la asistencia queda como lo que eligió ser.
    --    Dos envíos simultáneos: el segundo choca con uq_registro_sesion_asis y
    --    se atrapa aquí (nunca un 500).
    BEGIN
        INSERT INTO registro_asistencia (fk_sesion, fk_asistente, fk_enlace, fk_clave,
                                         fk_vinculacion_snapshot, origen, ip, user_agent,
                                         latitud, longitud, distancia_metros, identificado_por)
        VALUES (e.o_id_sesion, v_asistente, e.o_id_enlace, NULL,
                'ESTUDIANTE', e.o_canal, p_ip, left(p_user_agent, 300),
                e.o_latitud, e.o_longitud, e.o_distancia, 'CARNET')
        RETURNING id_registro INTO p_id_registro;
    EXCEPTION WHEN unique_violation THEN
        p_id_registro := NULL;
        p_estado := 'YA_REGISTRADO';  RETURN;
    END;

    p_estado := 'OK';
END;
$$;

-- --------------------------------------------------------------------------
--  Pieza común de DOCENTE y EXTERNO: "contacto" es UN campo. Con '@' es un
--  correo (correo_personal); si no, un celular. Devuelve NULL en los dos si
--  el contacto no sirve.
-- --------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION catedras.fn_interpretar_contacto(
    IN  p_contacto text,
    OUT o_correo   text,
    OUT o_celular  text,
    OUT o_valido   boolean
)
    LANGUAGE plpgsql
    IMMUTABLE
    AS $$
DECLARE
    v text := btrim(COALESCE(p_contacto, ''));
BEGIN
    o_correo := NULL;  o_celular := NULL;  o_valido := false;

    IF position('@' IN v) > 0 THEN
        -- mismo formato que la API de Asistente y chk_asistente_cor_pers_fmt
        IF length(v) <= 254 AND v ~ '^[^@[:space:]]+@[^@[:space:]]+\.[^@[:space:]]+$' THEN
            o_correo := v;  o_valido := true;
        END IF;
    ELSE
        -- celular: hasta 20 caracteres (columna), con al menos 7 dígitos
        IF length(v) <= 20 AND v ~ '^[0-9+() -]+$'
           AND length(regexp_replace(v, '[^0-9]', '', 'g')) >= 7 THEN
            o_celular := v;  o_valido := true;
        END IF;
    END IF;
END;
$$;

-- --------------------------------------------------------------------------
--  Pieza común de DOCENTE y EXTERNO: busca a la persona por cédula (CC) y, si
--  no existe, la crea (es_externo = true: no tiene código ASIS, D67). Si ya
--  existe NO se pisa nada: solo se rellenan correo y celular vacíos.
--  Devuelve NULL si la persona existe pero está inactiva.
-- --------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION catedras.fn_persona_por_cedula(
    p_cedula   text,
    p_nombre   text,
    p_correo   text,
    p_celular  text
) RETURNS bigint
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_id     bigint;
    v_activo boolean;
BEGIN
    SELECT a.id_asistente, a.activo INTO v_id, v_activo
      FROM documento_asistente d
      JOIN asistente a ON a.id_asistente = d.fk_asistente
     WHERE d.fk_tipo_documento = 'CC' AND d.numero = p_cedula;

    IF v_id IS NOT NULL THEN
        IF NOT v_activo THEN
            RETURN NULL;
        END IF;
        -- Se reutiliza la persona. Solo se completan datos vacíos.
        UPDATE asistente
           SET correo_personal = COALESCE(correo_personal, p_correo),
               celular         = COALESCE(celular, p_celular)
         WHERE id_asistente = v_id
           AND ((correo_personal IS NULL AND p_correo IS NOT NULL)
             OR (celular IS NULL AND p_celular IS NOT NULL));
        RETURN v_id;
    END IF;

    -- Persona nueva. 'nombres' es NOT NULL y mide 100: el nombre completo que
    -- escribió va entero en nombre_completo_asis (200) y 'nombres' lleva su
    -- principio; 'apellidos' no existe como dato aparte ('-'), D67.
    INSERT INTO asistente (nombres, apellidos, nombre_completo_asis, correo_personal, celular,
                           es_externo, contacto_sin_correo)
    VALUES (left(p_nombre, 100), '-', left(p_nombre, 200), p_correo, p_celular,
            true, p_correo IS NULL)
    RETURNING id_asistente INTO v_id;

    INSERT INTO documento_asistente (fk_asistente, fk_tipo_documento, numero, vigente)
    VALUES (v_id, 'CC', p_cedula, true);

    RETURN v_id;
END;
$$;

-- --------------------------------------------------------------------------
--  4) DOCENTE: cédula + nombre + contacto + facultad
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_registrar_asistencia_docente(
    IN    p_token            uuid,
    IN    p_cedula           text,
    IN    p_nombre           text,
    IN    p_contacto         text,
    IN    p_fk_facultad      text,
    IN    p_latitud          numeric DEFAULT NULL,
    IN    p_longitud         numeric DEFAULT NULL,
    IN    p_ip               inet    DEFAULT NULL,
    IN    p_user_agent       text    DEFAULT NULL,
    INOUT p_estado           text    DEFAULT NULL,
    INOUT p_id_registro      bigint  DEFAULT NULL,
    INOUT p_distancia_metros numeric DEFAULT NULL,
    INOUT p_radio_metros     integer DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_cedula    text := btrim(COALESCE(p_cedula, ''));
    v_nombre    text := btrim(COALESCE(p_nombre, ''));
    v_facultad  text := btrim(COALESCE(p_fk_facultad, ''));
    v_asistente bigint;
    c           record;
    e           record;
BEGIN
    p_id_registro := NULL;

    -- 0) forma de los datos (la facultad debe existir y estar activa: Regla 2)
    SELECT * INTO c FROM catedras.fn_interpretar_contacto(p_contacto);
    IF v_cedula !~ '^[0-9A-Za-z-]{4,20}$'
       OR length(v_nombre) NOT BETWEEN 1 AND 200
       OR NOT c.o_valido
       OR NOT EXISTS (SELECT 1 FROM facultad f WHERE f.id_facultad = v_facultad AND f.activo) THEN
        p_estado := 'DATOS_INVALIDOS';  RETURN;
    END IF;

    -- 1) enlace y ubicación
    SELECT * INTO e FROM catedras.fn_validar_enlace_y_ubicacion(p_token, p_latitud, p_longitud);
    p_distancia_metros := e.o_distancia;
    p_radio_metros     := e.o_radio;
    IF e.o_estado <> 'OK' THEN
        p_estado := e.o_estado;  RETURN;
    END IF;

    -- 2) la persona: se reutiliza por cédula o se crea
    v_asistente := catedras.fn_persona_por_cedula(v_cedula, v_nombre, c.o_correo, c.o_celular);
    IF v_asistente IS NULL THEN
        p_estado := 'PERSONA_INACTIVA';  RETURN;
    END IF;

    -- 3) vinculación DOCENTE vigente: si no la tiene, se le crea
    IF NOT EXISTS (SELECT 1 FROM vinculacion_asistente v
                    WHERE v.fk_asistente = v_asistente
                      AND v.fk_tipo_vinculacion = 'DOCENTE' AND v.fecha_fin IS NULL) THEN
        INSERT INTO vinculacion_asistente (fk_asistente, fk_tipo_vinculacion, fecha_ini)
        VALUES (v_asistente, 'DOCENTE', CURRENT_DATE)
        ON CONFLICT (fk_asistente, fk_tipo_vinculacion, fecha_ini) DO UPDATE SET fecha_fin = NULL;
    END IF;

    -- 4) duplicado
    IF EXISTS (SELECT 1 FROM registro_asistencia r
                WHERE r.fk_sesion = e.o_id_sesion AND r.fk_asistente = v_asistente) THEN
        p_estado := 'YA_REGISTRADO';  RETURN;
    END IF;

    -- 5) el registro (con la facultad que declaró, como instantánea)
    BEGIN
        INSERT INTO registro_asistencia (fk_sesion, fk_asistente, fk_enlace, fk_clave,
                                         fk_vinculacion_snapshot, fk_facultad_snapshot,
                                         origen, ip, user_agent,
                                         latitud, longitud, distancia_metros, identificado_por)
        VALUES (e.o_id_sesion, v_asistente, e.o_id_enlace, NULL,
                'DOCENTE', v_facultad,
                e.o_canal, p_ip, left(p_user_agent, 300),
                e.o_latitud, e.o_longitud, e.o_distancia, 'DECLARADO')
        RETURNING id_registro INTO p_id_registro;
    EXCEPTION WHEN unique_violation THEN
        p_id_registro := NULL;
        p_estado := 'YA_REGISTRADO';  RETURN;
    END;

    p_estado := 'OK';
END;
$$;

-- --------------------------------------------------------------------------
--  5) EXTERNO: cédula + nombre + contacto + de dónde viene
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_registrar_asistencia_externo(
    IN    p_token            uuid,
    IN    p_cedula           text,
    IN    p_nombre           text,
    IN    p_contacto         text,
    IN    p_procedencia      text,
    IN    p_latitud          numeric DEFAULT NULL,
    IN    p_longitud         numeric DEFAULT NULL,
    IN    p_ip               inet    DEFAULT NULL,
    IN    p_user_agent       text    DEFAULT NULL,
    INOUT p_estado           text    DEFAULT NULL,
    INOUT p_id_registro      bigint  DEFAULT NULL,
    INOUT p_distancia_metros numeric DEFAULT NULL,
    INOUT p_radio_metros     integer DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_cedula      text := btrim(COALESCE(p_cedula, ''));
    v_nombre      text := btrim(COALESCE(p_nombre, ''));
    v_procedencia text := btrim(COALESCE(p_procedencia, ''));
    v_asistente   bigint;
    c             record;
    e             record;
BEGIN
    p_id_registro := NULL;

    SELECT * INTO c FROM catedras.fn_interpretar_contacto(p_contacto);
    IF v_cedula !~ '^[0-9A-Za-z-]{4,20}$'
       OR length(v_nombre) NOT BETWEEN 1 AND 200
       OR NOT c.o_valido
       OR length(v_procedencia) NOT BETWEEN 1 AND 150 THEN
        p_estado := 'DATOS_INVALIDOS';  RETURN;
    END IF;

    SELECT * INTO e FROM catedras.fn_validar_enlace_y_ubicacion(p_token, p_latitud, p_longitud);
    p_distancia_metros := e.o_distancia;
    p_radio_metros     := e.o_radio;
    IF e.o_estado <> 'OK' THEN
        p_estado := e.o_estado;  RETURN;
    END IF;

    v_asistente := catedras.fn_persona_por_cedula(v_cedula, v_nombre, c.o_correo, c.o_celular);
    IF v_asistente IS NULL THEN
        p_estado := 'PERSONA_INACTIVA';  RETURN;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM vinculacion_asistente v
                    WHERE v.fk_asistente = v_asistente
                      AND v.fk_tipo_vinculacion = 'EXTERNO' AND v.fecha_fin IS NULL) THEN
        INSERT INTO vinculacion_asistente (fk_asistente, fk_tipo_vinculacion, fecha_ini)
        VALUES (v_asistente, 'EXTERNO', CURRENT_DATE)
        ON CONFLICT (fk_asistente, fk_tipo_vinculacion, fecha_ini) DO UPDATE SET fecha_fin = NULL;
    END IF;

    IF EXISTS (SELECT 1 FROM registro_asistencia r
                WHERE r.fk_sesion = e.o_id_sesion AND r.fk_asistente = v_asistente) THEN
        p_estado := 'YA_REGISTRADO';  RETURN;
    END IF;

    BEGIN
        INSERT INTO registro_asistencia (fk_sesion, fk_asistente, fk_enlace, fk_clave,
                                         fk_vinculacion_snapshot, procedencia,
                                         origen, ip, user_agent,
                                         latitud, longitud, distancia_metros, identificado_por)
        VALUES (e.o_id_sesion, v_asistente, e.o_id_enlace, NULL,
                'EXTERNO', v_procedencia,
                e.o_canal, p_ip, left(p_user_agent, 300),
                e.o_latitud, e.o_longitud, e.o_distancia, 'DECLARADO')
        RETURNING id_registro INTO p_id_registro;
    EXCEPTION WHEN unique_violation THEN
        p_id_registro := NULL;
        p_estado := 'YA_REGISTRADO';  RETURN;
    END;

    p_estado := 'OK';
END;
$$;

COMMENT ON PROCEDURE catedras.sp_registrar_asistencia_estudiante IS
    'v7.2 D64. Registra la asistencia de un estudiante por carnet. Devuelve p_estado (OK u otro) y, si OK, p_id_registro. No lanza excepción para los casos de negocio.';
COMMENT ON PROCEDURE catedras.sp_registrar_asistencia_docente IS
    'v7.2 D64. Registra la asistencia de un docente (cédula, nombre, contacto, facultad). Crea o reutiliza la persona por cédula.';
COMMENT ON PROCEDURE catedras.sp_registrar_asistencia_externo IS
    'v7.2 D64. Registra la asistencia de un externo (cédula, nombre, contacto, procedencia). Crea o reutiliza la persona por cédula.';


-- ----- migracion 064_sp_cargar_estudiante.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/064_sp_cargar_estudiante.sql
--  Proyecto Cátedras · SUBVERSIÓN v7.2 · RF-S8 · D67, D68
-- --------------------------------------------------------------------------
--  Carga de UN estudiante del archivo (CC, Carnet_estudiante, Programa) +
--  el periodo elegido en la pantalla. Es un UPSERT por carnet: volver a subir
--  el mismo archivo actualiza y NO duplica. 'sp_guardar_asistente' (002) es un
--  INSERT puro: repetir un id_asis lanzaba uq_asistente_asis.
--
--  Por fila, en una sola transacción:
--    1) persona: se busca por id_asis (= carnet); si no existe, se crea con
--       nombre provisional ('Estudiante <carnet>'), sin correo
--       (contacto_sin_correo = true) y no externa (D67). Si ya existía NO se
--       toca su nombre; si estaba inactiva, el archivo la reactiva.
--    2) documento CC: si ese número ya es de OTRA persona se rechaza la fila.
--       Si la persona tenía otro documento vigente, ese pasa a vigente = false
--       (no se borra: RN-05) y el nuevo queda vigente.
--    3) programa (P19): los OTROS programas ACTIVO de esa persona en ese mismo
--       periodo pasan a RETIRADO; el del archivo queda ACTIVO. Así hay uno solo
--       activo por persona y periodo y el 'snapshot' del registro de asistencia
--       (fn_trg_registro_validar) es determinista. Otros periodos no se tocan.
--    4) vinculación ESTUDIANTE: si no tiene una vigente, se crea con
--       fecha_ini = hoy; si la tiene, se conserva su fecha.
--
--  Responde p_accion = 'CREADO' o 'ACTUALIZADO' y el id de la persona.
--  Los rechazos (formato, programa o periodo inexistente, CC de otra persona)
--  son excepciones: el front guarda su texto como motivo de la novedad de esa
--  fila y sigue con las demás (carga parcial, D15).
--
--  Idempotente (CREATE OR REPLACE). Si conservas el volumen de PostgreSQL hay
--  que aplicarla a mano.
-- ==========================================================================

SET search_path TO catedras, public;

CREATE OR REPLACE PROCEDURE catedras.sp_cargar_estudiante(
    IN    p_id_asis        character varying(15),
    IN    p_documento      character varying(20),
    IN    p_programa       character varying(5),
    IN    p_periodo        character(6),
    IN    p_fk_lote_carga  bigint DEFAULT NULL,
    INOUT p_id_asistente   bigint DEFAULT NULL,
    INOUT p_accion         text   DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_dueno_doc bigint;
    v_doc_vigente boolean;
BEGIN
    -- 0) forma de los datos (mismos patrones que los CHECK de las tablas, con un mensaje claro)
    IF p_id_asis IS NULL OR p_id_asis !~ '^[0-9A-Za-z]{5,15}$' THEN
        RAISE EXCEPTION 'El carnet "%" no es válido: debe tener entre 5 y 15 letras o dígitos', p_id_asis
            USING ERRCODE = 'check_violation';
    END IF;
    IF p_documento IS NULL OR p_documento !~ '^[0-9A-Za-z-]{4,20}$' THEN
        RAISE EXCEPTION 'La CC "%" no es válida: debe tener entre 4 y 20 letras, dígitos o guiones', p_documento
            USING ERRCODE = 'check_violation';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM programa_academico WHERE codigo = p_programa) THEN
        RAISE EXCEPTION 'El programa "%" no existe en el catálogo', p_programa
            USING ERRCODE = 'foreign_key_violation';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM periodo_academico WHERE id_periodo = p_periodo) THEN
        RAISE EXCEPTION 'El periodo "%" no existe', p_periodo
            USING ERRCODE = 'foreign_key_violation';
    END IF;

    -- 1) la persona. FOR UPDATE: dos cargas simultáneas del mismo carnet se turnan.
    SELECT a.id_asistente INTO p_id_asistente
      FROM asistente a WHERE a.id_asis = p_id_asis
       FOR UPDATE;

    IF p_id_asistente IS NULL THEN
        INSERT INTO asistente (id_asis, nombres, apellidos, es_externo, contacto_sin_correo, fk_lote_carga)
        VALUES (p_id_asis, 'Estudiante ' || p_id_asis, '-', false, true, p_fk_lote_carga)
        RETURNING id_asistente INTO p_id_asistente;
        p_accion := 'CREADO';
    ELSE
        -- el archivo es la fuente de verdad: una persona inactiva que aparece vuelve a estar activa
        UPDATE asistente SET activo = true WHERE id_asistente = p_id_asistente AND NOT activo;
        p_accion := 'ACTUALIZADO';
    END IF;

    -- 2) documento CC
    SELECT d.fk_asistente, d.vigente INTO v_dueno_doc, v_doc_vigente
      FROM documento_asistente d
     WHERE d.fk_tipo_documento = 'CC' AND d.numero = p_documento;

    IF v_dueno_doc IS NOT NULL AND v_dueno_doc <> p_id_asistente THEN
        RAISE EXCEPTION 'La CC % ya pertenece a otra persona; no se carga el carnet %', p_documento, p_id_asis
            USING ERRCODE = 'unique_violation';
    END IF;

    IF v_dueno_doc IS NULL OR NOT v_doc_vigente THEN
        -- ux_documento_vigente: solo un documento vigente por persona. El actual se retira primero.
        UPDATE documento_asistente SET vigente = false
         WHERE fk_asistente = p_id_asistente AND vigente;

        IF v_dueno_doc IS NULL THEN
            INSERT INTO documento_asistente (fk_asistente, fk_tipo_documento, numero, vigente)
            VALUES (p_id_asistente, 'CC', p_documento, true);
        ELSE
            -- tenía ese mismo número pero ya no vigente: vuelve a serlo
            UPDATE documento_asistente SET vigente = true
             WHERE fk_asistente = p_id_asistente AND fk_tipo_documento = 'CC' AND numero = p_documento;
        END IF;
    END IF;

    -- 3) programa del periodo (P19): uno solo ACTIVO por persona y periodo
    UPDATE programa_asistente SET estado = 'RETIRADO'
     WHERE fk_asistente = p_id_asistente AND fk_periodo = p_periodo
       AND estado = 'ACTIVO' AND fk_programa <> p_programa;

    INSERT INTO programa_asistente (fk_asistente, fk_programa, fk_periodo, estado)
    VALUES (p_id_asistente, p_programa, p_periodo, 'ACTIVO')
    ON CONFLICT (fk_asistente, fk_programa, fk_periodo) DO UPDATE SET estado = 'ACTIVO';

    -- 4) vinculación ESTUDIANTE vigente
    IF NOT EXISTS (SELECT 1 FROM vinculacion_asistente v
                    WHERE v.fk_asistente = p_id_asistente
                      AND v.fk_tipo_vinculacion = 'ESTUDIANTE' AND v.fecha_fin IS NULL) THEN
        INSERT INTO vinculacion_asistente (fk_asistente, fk_tipo_vinculacion, fecha_ini)
        VALUES (p_id_asistente, 'ESTUDIANTE', CURRENT_DATE)
        ON CONFLICT (fk_asistente, fk_tipo_vinculacion, fecha_ini) DO UPDATE SET fecha_fin = NULL;
    END IF;
END;
$$;

COMMENT ON PROCEDURE catedras.sp_cargar_estudiante IS
    'v7.2 D68. Upsert de un estudiante por carnet (CC + programa + periodo). Idempotente. Retira los otros programas ACTIVO del mismo periodo (P19). Devuelve p_id_asistente y p_accion (CREADO / ACTUALIZADO).';


-- ----- migracion 066_exportacion_catedras.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/066_exportacion_catedras.sql
--  Proyecto Cátedras · SUBVERSIÓN v7.2 · RF-S13 / RF-S14 · D70
-- --------------------------------------------------------------------------
--  Consultas de SOLO LECTURA para la exportación de fin de periodo
--  (/admin/exportar). El front arma los CSV y el ZIP; aquí solo se entregan
--  los datos, ya deduplicados y clasificados:
--
--    fn_exportar_catedras_periodo(periodo)
--        una fila por cátedra del periodo: id, título y cuántos estudiantes,
--        docentes y externos asistieron (más "otros", que no se exportan).
--    fn_exportar_asistentes_periodo(periodo, desde, limite)
--        las mismas cátedras con la lista de quienes asistieron, PAGINADA POR
--        CÁTEDRA (keyset por id_evento_asis) para que el ZIP no haga una
--        petición por cátedra.
--
--  REGLAS (D70):
--   · Clasificación por la instantánea de la VINCULACIÓN del registro
--     (ESTUDIANTE / DOCENTE / EXTERNO), nunca por asistente.es_externo (los
--     docentes nuevos también son es_externo = true, D67). Las asistencias de
--     otras vinculaciones (ADMINISTRATIVO, EGRESADO...) solo se cuentan como
--     "otros".
--   · Una cátedra pertenece a un periodo por sus sesiones: aparece en cada
--     periodo donde tiene al menos una sesión, y ahí solo cuentan los
--     registros de ESAS sesiones.
--   · Una persona aparece UNA vez por cátedra (y vinculación) aunque haya ido a
--     varias sesiones; de sus datos variables (programa, facultad, procedencia)
--     se toma el del registro más reciente.
--   · Cátedras incluidas: las ACTIVAS y las inactivas que tengan asistencias en
--     ese periodo (D59 desactivó miles de cátedras sin asistencias: sin este
--     criterio llenarían la lista de ceros).
--   · 'id_catedra' = catedra.id_evento_asis (9 dígitos, con ceros a la izquierda).
--
--  Idempotente (CREATE OR REPLACE). Si conservas el volumen de PostgreSQL hay
--  que aplicarla a mano.
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  1) Cátedras del periodo con sus conteos
-- --------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION catedras.fn_exportar_catedras_periodo(p_periodo character(6))
    RETURNS json
    LANGUAGE sql
    STABLE
    SET search_path TO 'catedras', 'public'
    AS $$
    SELECT COALESCE(json_agg(t ORDER BY t.id_catedra), '[]'::json)
      FROM (
        SELECT c.id_evento_asis AS id_catedra,
               c.nombre         AS titulo,
               c.activo,
               count(DISTINCT ra.fk_asistente) FILTER (WHERE ra.fk_vinculacion_snapshot = 'ESTUDIANTE') AS estudiantes,
               count(DISTINCT ra.fk_asistente) FILTER (WHERE ra.fk_vinculacion_snapshot = 'DOCENTE')    AS docentes,
               count(DISTINCT ra.fk_asistente) FILTER (WHERE ra.fk_vinculacion_snapshot = 'EXTERNO')    AS externos,
               count(DISTINCT ra.fk_asistente) FILTER (WHERE ra.fk_vinculacion_snapshot
                                                       NOT IN ('ESTUDIANTE', 'DOCENTE', 'EXTERNO'))     AS otros
          FROM catedra c
          JOIN sesion s ON s.fk_catedra = c.id_catedra AND s.fk_periodo = p_periodo
          LEFT JOIN registro_asistencia ra ON ra.fk_sesion = s.id_sesion
         GROUP BY c.id_catedra, c.id_evento_asis, c.nombre, c.activo
        HAVING c.activo OR count(ra.id_registro) > 0
      ) t;
$$;

COMMENT ON FUNCTION catedras.fn_exportar_catedras_periodo IS
    'v7.2 D70. Cátedras con al menos una sesión en el periodo (activas, o inactivas con asistencias) y cuántos estudiantes, docentes y externos asistieron; "otros" = otras vinculaciones, que no se exportan.';

-- --------------------------------------------------------------------------
--  2) Asistentes por cátedra, paginado por cátedra.
--     p_desde: último id_catedra (id_evento_asis) ya recibido; NULL = desde el
--     principio. p_limite: cuántas cátedras por página (tope 500).
--     Cada elemento de 'asistentes' trae TODOS los campos de los tres CSV; el
--     front elige las columnas según el tipo:
--       ESTUDIANTE -> cc, carnet, programa
--       DOCENTE    -> cc, nombre, contacto, facultad
--       EXTERNO    -> cc, nombre, contacto, procedencia
-- --------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION catedras.fn_exportar_asistentes_periodo(
    p_periodo character(6),
    p_desde   text    DEFAULT NULL,
    p_limite  integer DEFAULT 100
) RETURNS json
    LANGUAGE sql
    STABLE
    SET search_path TO 'catedras', 'public'
    AS $$
    WITH cats AS (
        SELECT c.id_catedra, c.id_evento_asis, c.nombre
          FROM catedra c
         WHERE (p_desde IS NULL OR c.id_evento_asis > p_desde)
           AND EXISTS (SELECT 1 FROM sesion s
                        WHERE s.fk_catedra = c.id_catedra AND s.fk_periodo = p_periodo)
           AND (c.activo
                OR EXISTS (SELECT 1 FROM sesion s
                             JOIN registro_asistencia ra ON ra.fk_sesion = s.id_sesion
                            WHERE s.fk_catedra = c.id_catedra AND s.fk_periodo = p_periodo))
         ORDER BY c.id_evento_asis
         LIMIT LEAST(GREATEST(COALESCE(p_limite, 100), 1), 500)
    ),
    filas AS (
        -- una fila por (cátedra, persona, vinculación): la del registro más reciente
        SELECT DISTINCT ON (cs.id_catedra, ra.fk_asistente, ra.fk_vinculacion_snapshot)
               cs.id_catedra,
               ra.fk_vinculacion_snapshot AS tipo,
               ra.fk_asistente,
               ra.fk_programa_snapshot,
               ra.fk_facultad_snapshot,
               ra.procedencia
          FROM cats cs
          JOIN sesion s ON s.fk_catedra = cs.id_catedra AND s.fk_periodo = p_periodo
          JOIN registro_asistencia ra ON ra.fk_sesion = s.id_sesion
               AND ra.fk_vinculacion_snapshot IN ('ESTUDIANTE', 'DOCENTE', 'EXTERNO')
         ORDER BY cs.id_catedra, ra.fk_asistente, ra.fk_vinculacion_snapshot, ra.registrado_en DESC
    )
    SELECT COALESCE(json_agg(
               json_build_object(
                   'id_catedra', cs.id_evento_asis,
                   'titulo',     cs.nombre,
                   'asistentes', COALESCE((
                       SELECT json_agg(
                                  json_build_object(
                                      'tipo',        f.tipo,
                                      'cc',          d.numero,
                                      'carnet',      a.id_asis,
                                      'nombre',      COALESCE(a.nombre_completo_asis,
                                                              btrim(a.nombres || ' ' || a.apellidos)),
                                      'contacto',    COALESCE(a.correo_institucional::text,
                                                              a.correo_personal::text,
                                                              a.celular),
                                      'programa',    f.fk_programa_snapshot,
                                      'facultad',    fa.nombre,
                                      'procedencia', f.procedencia)
                                  ORDER BY f.tipo, a.id_asis NULLS LAST, d.numero)
                         FROM filas f
                         JOIN asistente a ON a.id_asistente = f.fk_asistente
                         LEFT JOIN documento_asistente d ON d.fk_asistente = a.id_asistente AND d.vigente
                         LEFT JOIN facultad fa ON fa.id_facultad = f.fk_facultad_snapshot
                        WHERE f.id_catedra = cs.id_catedra), '[]'::json)
               ) ORDER BY cs.id_evento_asis), '[]'::json)
      FROM cats cs;
$$;

COMMENT ON FUNCTION catedras.fn_exportar_asistentes_periodo IS
    'v7.2 D70. Asistentes por cátedra del periodo, sin repetir persona (por cátedra y vinculación), paginado por id_evento_asis (p_desde, p_limite). Clasifica por la vinculación del registro.';


-- ----- migracion 067_sp_guardar_usuario.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/067_sp_guardar_usuario.sql
--  Proyecto Cátedras · Guía IA, Regla 1 (maestro-detalle) · "crear usuario"
-- --------------------------------------------------------------------------
--  Maestro: 'usuario' (con la persona que es su dueña). Detalle: 'rol_por_usuario'.
--
--  CREAR UN USUARIO YA NO ES BUSCAR A UNA PERSONA: el administrador escribe los
--  datos de la persona, los de la cuenta y marca uno o varios roles (ESTUDIANTE,
--  PONENTE, ADMIN...). Este SP guarda las tres cosas en UNA sola transacción:
--
--     1) asistente          la persona (es_externo = true: no viene del ASIS, así
--                           que no lleva id_asis; chk_asistente_externo lo permite)
--     2) usuario            la cuenta, con el hash BCrypt que calculó ServicioUsuario
--     3) rol_por_usuario    una fila por rol del arreglo p_roles (nacen vigentes)
--
--  Si CUALQUIER paso falla (login repetido, correo repetido, rol inexistente,
--  rol repetido...), PostgreSQL revierte TODO el CALL: ni la persona ni la cuenta
--  quedan a medias.
--
--  p_roles  [{"fk_rol":"ESTUDIANTE","fecha_ini":"2026-01-20"}]   (fecha_ini opcional = hoy)
--
--  Idempotente (CREATE OR REPLACE). Si conservas el volumen de PostgreSQL hay que
--  aplicarla a mano con psql.
-- ==========================================================================

SET search_path TO catedras, public;

CREATE OR REPLACE PROCEDURE catedras.sp_guardar_usuario(
    -- La persona (maestro, parte 1).
    IN    p_nombres             character varying(100),
    IN    p_apellidos           character varying(100),
    IN    p_correo_inst         character varying(254),
    IN    p_correo_pers         character varying(254),
    IN    p_celular             character varying(20),
    -- La cuenta (maestro, parte 2). La clave llega YA hasheada (ADR D23).
    IN    p_usuario             character varying(40),
    IN    p_clave_hash          character varying(255),
    IN    p_activo              boolean DEFAULT true,
    IN    p_debe_cambiar_clave  boolean DEFAULT false,
    -- Detalle: los roles.
    IN    p_roles               jsonb   DEFAULT NULL,
    INOUT p_resultado           json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_asistente bigint;
    v_usuario   bigint;
BEGIN
    -- 1) La persona. Correo repetido o sin ningún correo -> excepción del motor (500).
    INSERT INTO asistente (nombres, apellidos, correo_institucional, correo_personal, celular, es_externo, activo)
    VALUES (p_nombres, p_apellidos, p_correo_inst, p_correo_pers, p_celular, true, true)
    RETURNING id_asistente INTO v_asistente;

    -- 2) La cuenta. Login repetido (uq_usuario_login) -> excepción -> se deshace la persona.
    INSERT INTO usuario (fk_asistente, usuario, clave_hash, activo, debe_cambiar_clave)
    VALUES (v_asistente, p_usuario, p_clave_hash, COALESCE(p_activo, true), COALESCE(p_debe_cambiar_clave, false))
    RETURNING id_usuario INTO v_usuario;

    -- 3) Los roles. 'fk_usuario' NO viene en el JSON: es la cuenta recién creada.
    IF p_roles IS NOT NULL AND jsonb_array_length(p_roles) > 0 THEN
        INSERT INTO rol_por_usuario (fk_usuario, fk_rol, fecha_ini)
        SELECT v_usuario,
               e ->> 'fk_rol',
               COALESCE((e ->> 'fecha_ini')::date, CURRENT_DATE)
          FROM jsonb_array_elements(p_roles) AS e;
    END IF;

    SELECT row_to_json(u) INTO p_resultado
      FROM (SELECT id_usuario, fk_asistente, usuario, activo, debe_cambiar_clave, ultimo_acceso, creado_en
              FROM usuario
             WHERE id_usuario = v_usuario) u;
END;
$$;

COMMENT ON PROCEDURE catedras.sp_guardar_usuario IS
    'Guía IA · Regla 1. Crea la persona (asistente), su usuario y sus roles en una sola transacción. '
    'La clave llega ya hasheada por ServicioUsuario. Devuelve el usuario creado (sin clave_hash).';

RESET search_path;


-- ----- migracion 068_rol_asistente_sin_coordinador_consulta.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/068_rol_asistente_sin_coordinador_consulta.sql
--  Proyecto Cátedras · Catálogo de roles
-- --------------------------------------------------------------------------
--  1) Se QUITAN los roles COORDINADOR y CONSULTA. Nunca tuvieron una sola
--     pantalla ni permiso asociado (el front solo conoce ADMIN, ESTUDIANTE y
--     PONENTE) y el script base los sembraba solo como ejemplo.
--     ⚠ fk_rolusr_rol es ON DELETE RESTRICT: para poder borrar el rol hay que
--     borrar ANTES sus filas de rol_por_usuario (el historial de quien los tuvo).
--  2) Se CREA el rol ASISTENTE: el administrador lo asigna a una cuenta y esa
--     persona puede iniciar sesión y gestionar SOLO cátedras (agregar, editar
--     y borrar). Lo que puede hacer lo decide el front (la API no autoriza por
--     rol, ADR D28) y el login de la API ahora acepta ADMIN o ASISTENTE.
--
--  Idempotente: se puede ejecutar varias veces. Con un volumen de PostgreSQL ya
--  creado hay que aplicarla a mano con psql.
-- ==========================================================================

SET search_path TO catedras, public;

DELETE FROM rol_por_usuario WHERE fk_rol IN ('COORDINADOR', 'CONSULTA');
DELETE FROM rol             WHERE id_rol IN ('COORDINADOR', 'CONSULTA');

INSERT INTO rol (id_rol, nombre, descripcion) VALUES
    ('ASISTENTE', 'Asistente', 'Inicia sesión y gestiona cátedras: agregar, editar y borrar')
ON CONFLICT DO NOTHING;

RESET search_path;


-- ----- migracion 069_quitar_roles_estudiante_ponente.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/069_quitar_roles_estudiante_ponente.sql
--  Proyecto Cátedras · Catálogo de roles
-- --------------------------------------------------------------------------
--  Se QUITAN los roles ESTUDIANTE y PONENTE (los sembró la migración 046).
--  Desde la v7.1 · D55 estudiantes y ponentes NO tienen cuenta que entre: la
--  asistencia es pública (QR) y todo lo gestiona el personal. El catálogo
--  queda con ADMIN y ASISTENTE.
--
--  ⚠ NO se tocan otras cosas llamadas igual:
--     · ponencia.rol = 'PONENTE'  (quién da una sesión; chk_ponencia_rol).
--     · vinculacion_asistente.fk_tipo_vinculacion = 'ESTUDIANTE'  (tipo de persona).
--  Aquí solo se borra del catálogo 'rol' (roles de CUENTA).
--
--  ⚠ fk_rolusr_rol es ON DELETE RESTRICT: antes se borran las filas de
--  rol_por_usuario de esos roles (el historial de quien los tuvo).
--  ⚠ sp_provisionar_cuenta_estudiante (052) y la semilla 048 asignan el rol
--  ESTUDIANTE: con el rol borrado, volver a ejecutarlos fallaría por la FK. Ya
--  nada de la API los llama.
--
--  Idempotente. Con un volumen de PostgreSQL ya creado hay que aplicarla a mano.
-- ==========================================================================

SET search_path TO catedras, public;

DELETE FROM rol_por_usuario WHERE fk_rol IN ('ESTUDIANTE', 'PONENTE');
DELETE FROM rol             WHERE id_rol IN ('ESTUDIANTE', 'PONENTE');

RESET search_path;


-- ----- catalogo facultad (lo necesita la migracion 070: programa_academico.fk_facultad) -----
RESET search_path;
SET search_path TO catedras, public;

--  Las mismas 9 filas del script provisto (script_bdcatedras.sql). La USB es la raiz; las demas cuelgan de ella.
INSERT INTO catedras.facultad VALUES ('USB', 'Universidad de San Buenaventura', NULL, true)           ON CONFLICT DO NOTHING;
INSERT INTO catedras.facultad VALUES ('F_ING', 'Facultad de Ingenierias', 'USB', true)                 ON CONFLICT DO NOTHING;
INSERT INTO catedras.facultad VALUES ('F_ARTES', 'Facultad de Artes Integradas', 'USB', true)          ON CONFLICT DO NOTHING;
INSERT INTO catedras.facultad VALUES ('F_PSICO', 'Facultad de Psicologia', 'USB', true)                ON CONFLICT DO NOTHING;
INSERT INTO catedras.facultad VALUES ('F_DERECHO', 'Facultad de Derecho y Ciencias Politicas', 'USB', true) ON CONFLICT DO NOTHING;
INSERT INTO catedras.facultad VALUES ('F_EDU', 'Facultad de Educacion', 'USB', true)                   ON CONFLICT DO NOTHING;
INSERT INTO catedras.facultad VALUES ('F_CIENCIAS', 'Facultad de Ciencias Empresariales', 'USB', true) ON CONFLICT DO NOTHING;
INSERT INTO catedras.facultad VALUES ('F_SALUD', 'Facultad de Ciencias de la Salud', 'USB', true)      ON CONFLICT DO NOTHING;
INSERT INTO catedras.facultad VALUES ('F_OTRA', 'Otras unidades academicas', 'USB', true)              ON CONFLICT DO NOTHING;


-- ----- migracion 070_programas_reales_usb_medellin.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/070_programas_reales_usb_medellin.sql
--  Proyecto Cátedras · Catálogo de programas académicos
-- --------------------------------------------------------------------------
--  Reemplaza los programas INVENTADOS por los programas REALES de la
--  Universidad de San Buenaventura que se ofrecen en el Valle de Aburrá
--  (sedes Medellín y Bello), según la oferta publicada en usbmed.edu.co
--  (https://usbmed.edu.co/programas/pregrados y /programas/posgrados,
--  consultadas el 2026-11-13).
--
--  QUÉ SE BORRA
--   · Los programas de relleno «Programa M0110», «Programa M0200»...: el CÓDIGO
--     venía del ASIS pero el NOMBRE era inventado.
--   · Los «(mock)» sin equivalente real: INDS (duplicado de M0286), INGC (Ingeniería
--     Civil), COMU (Comunicación Social) y EDUC (Licenciatura en Educación genérica);
--     la USB Medellín no ofrece esos programas.
--   · NO se borra un programa que tenga matrículas (programa_asistente) o registros
--     de asistencia que lo usen (FK RESTRICT): se conserva y queda para revisarlo a mano.
--
--  QUÉ SE DEJA / CREA
--   · Se conservan los códigos ARQU, DERE, PSIC, CONT, ADMO y M0286 y se les pone el
--     nombre real. ADMO queda como «Administración de Negocios» (el programa real).
--   · Los demás llevan un código INTERNO de 4 letras (no son códigos ASIS). Si llega
--     un archivo real con otros códigos, se corrigen con un UPDATE del código o con un
--     alias_programa, sin tocar los nombres.
--   · Sin los 2 programas de Armenia (Psicología – Armenia, Maestría en Educación:
--     Desarrollo Humano – Armenia): no son del Valle de Aburrá.
--   · Los posgrados «– FUCN» son los ofrecidos en convenio, tal como los publica la USB.
--   · La tecnología (Entrenamiento Deportivo) va como PREGRADO: el CHECK de nivel no
--     tiene un valor de tecnología.
--
--  Idempotente: se puede ejecutar varias veces. Con un volumen de PostgreSQL ya
--  creado hay que aplicarla a mano con psql.
-- ==========================================================================

SET search_path TO catedras, public;

-- 1) Fuera los inventados (solo los que nadie usa).
DELETE FROM programa_academico p
 WHERE (p.nombre LIKE 'Programa M%' OR p.codigo IN ('INDS', 'INGC', 'COMU', 'EDUC'))
   AND NOT EXISTS (SELECT 1 FROM programa_asistente   x WHERE x.fk_programa = p.codigo)
   AND NOT EXISTS (SELECT 1 FROM registro_asistencia  r WHERE r.fk_programa_snapshot = p.codigo);

-- 2) Los programas reales (si el código ya existe, se corrige nombre, facultad y nivel).
INSERT INTO programa_academico (codigo, nombre, fk_facultad, nivel, activo) VALUES
    -- Pregrado · Ciencias Empresariales
    ('ADMO', 'Administración de Negocios',                     'F_CIENCIAS', 'PREGRADO', true),
    ('CONT', 'Contaduría Pública',                             'F_CIENCIAS', 'PREGRADO', true),
    ('NEGI', 'Negocios Internacionales',                       'F_CIENCIAS', 'PREGRADO', true),
    -- Pregrado · Artes Integradas
    ('ARQU', 'Arquitectura',                                   'F_ARTES',    'PREGRADO', true),
    ('DMOD', 'Diseño de Modas',                                'F_ARTES',    'PREGRADO', true),
    ('DIND', 'Diseño Industrial',                              'F_ARTES',    'PREGRADO', true),
    -- Pregrado · Ingenierías
    ('CCDI', 'Creación de Contenidos Digitales',               'F_ING',      'PREGRADO', true),
    ('IAMB', 'Ingeniería Ambiental',                           'F_ING',      'PREGRADO', true),
    ('M0286', 'Ingeniería de Datos y Software',                'F_ING',      'PREGRADO', true),
    ('ISCI', 'Ingeniería de Sistemas Cibernéticos',            'F_ING',      'PREGRADO', true),
    ('ISON', 'Ingeniería de Sonido',                           'F_ING',      'PREGRADO', true),
    ('IIND', 'Ingeniería Industrial',                          'F_ING',      'PREGRADO', true),
    ('IMUL', 'Ingeniería Multimedia',                          'F_ING',      'PREGRADO', true),
    -- Pregrado · Derecho
    ('DERE', 'Derecho',                                        'F_DERECHO',  'PREGRADO', true),
    -- Pregrado · Psicología
    ('PSIC', 'Psicología',                                     'F_PSICO',    'PREGRADO', true),
    -- Pregrado · Educación
    ('LEFD', 'Licenciatura en Educación Física y Deporte',     'F_EDU',      'PREGRADO', true),
    ('LEIN', 'Licenciatura en Educación Infantil',             'F_EDU',      'PREGRADO', true),
    ('TEDE', 'Tecnología en Entrenamiento Deportivo',          'F_EDU',      'PREGRADO', true),
    -- Especializaciones
    ('EDPP', 'Especialización en Derecho Penal y Procesal Penal',                          'F_DERECHO',  'ESPECIALIZACION', true),
    ('ERSE', 'Especialización en Responsabilidad y Seguros',                               'F_DERECHO',  'ESPECIALIZACION', true),
    ('EDGE', 'Especialización en Dirección y Gestión Educativa',                           'F_EDU',      'ESPECIALIZACION', true),
    ('EFWE', 'Especialización en Fitness y Wellness',                                      'F_EDU',      'ESPECIALIZACION', true),
    ('EGEF', 'Especialización en Gerencia Educativa – FUCN',                               'F_EDU',      'ESPECIALIZACION', true),
    ('EDVI', 'Especialización en Diseño y Validación de Instrumentos de Medición',         'F_PSICO',    'ESPECIALIZACION', true),
    ('EPST', 'Especialización en Psicología de la Salud y la Seguridad en el Trabajo',     'F_PSICO',    'ESPECIALIZACION', true),
    ('EPOT', 'Especialización en Psicología de las Organizaciones y del Trabajo',          'F_PSICO',    'ESPECIALIZACION', true),
    ('EPOF', 'Especialización en Psicología de las Organizaciones y del Trabajo – FUCN',   'F_PSICO',    'ESPECIALIZACION', true),
    ('EPCP', 'Especialización en Psicología de los Cuidados Paliativos',                   'F_PSICO',    'ESPECIALIZACION', true),
    ('EGDT', 'Especialización en Gestión de Destinos Turísticos Innovadores',              'F_CIENCIAS', 'ESPECIALIZACION', true),
    ('EISP', 'Especialización en Interventoría y Supervisión de Proyectos y Obras Civiles','F_ARTES',    'ESPECIALIZACION', true),
    ('ESIG', 'Especialización en Sistemas de Información Geográfica',                      'F_ING',      'ESPECIALIZACION', true),
    -- Maestrías
    ('MBIO', 'Maestría en Bioclimática',                       'F_ARTES',    'MAESTRIA', true),
    ('MCRE', 'Maestría en Creatividad',                        'F_ARTES',    'MAESTRIA', true),
    ('MCED', 'Maestría en Ciencias de la Educación',           'F_EDU',      'MAESTRIA', true),
    ('MGEO', 'Maestría en Geoinformática',                     'F_ING',      'MAESTRIA', true),
    ('MNEU', 'Maestría en Neuropsicología',                    'F_PSICO',    'MAESTRIA', true),
    ('MPCL', 'Maestría en Psicología Clínica',                 'F_PSICO',    'MAESTRIA', true),
    -- Doctorados
    ('DCED', 'Doctorado en Ciencias de la Educación',          'F_EDU',      'DOCTORADO', true),
    ('DPSI', 'Doctorado en Psicología',                        'F_PSICO',    'DOCTORADO', true)
ON CONFLICT (codigo) DO UPDATE
   SET nombre = EXCLUDED.nombre, fk_facultad = EXCLUDED.fk_facultad, nivel = EXCLUDED.nivel, activo = true;

RESET search_path;


-- ----- migracion 071_lista_personas_con_rol.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/071_lista_personas_con_rol.sql
--  Proyecto Cátedras · Lista de personas (admin)
-- --------------------------------------------------------------------------
--  La lista de personas (GET /api/Asistente) mostraba una etiqueta «Externo»
--  (asistente.es_externo). Ahora muestra el ROL de la persona, que tiene dos
--  orígenes y por eso se devuelven dos campos nuevos (solo lectura):
--
--    roles_cuenta   nombres de los roles VIGENTES de su cuenta de usuario
--                   (rol_por_usuario sin fecha_fin): Administrador, Asistente...
--                   [] si la persona no tiene cuenta.
--    vinculaciones  nombres de sus vinculaciones VIGENTES (vinculacion_asistente
--                   sin fecha_fin): Estudiante USB, Docente USB, Publico externo...
--
--  Se calculan en la misma consulta de la página (máx. 50 filas), sin una petición
--  por persona. Mismo procedimiento y mismos parámetros que en 024: solo se
--  agregan las dos columnas. Idempotente (CREATE OR REPLACE).
-- ==========================================================================

SET search_path TO catedras, public;

CREATE OR REPLACE PROCEDURE catedras.sp_listar_asistente(
    IN    p_q              text    DEFAULT NULL,
    IN    p_limite         integer DEFAULT NULL,
    IN    p_desplazamiento integer DEFAULT 0,
    INOUT p_resultado      json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    -- NULLIF(btrim(...), '') convierte '' y '   ' en NULL: "sin búsqueda".
    v_patron text := '%' || NULLIF(btrim(p_q), '') || '%';
BEGIN
    SELECT COALESCE(json_agg(a), '[]'::json)
      INTO p_resultado
      FROM (SELECT s.id_asistente, s.id_asis, s.nombres, s.apellidos, s.nombre_completo_asis,
                   s.correo_institucional, s.correo_personal, s.celular,
                   s.es_externo, s.activo, s.creado_en, s.fk_lote_carga,
                   COALESCE((SELECT json_agg(r.nombre ORDER BY r.nombre)
                               FROM usuario u
                               JOIN rol_por_usuario ru ON ru.fk_usuario = u.id_usuario AND ru.fecha_fin IS NULL
                               JOIN rol r ON r.id_rol = ru.fk_rol
                              WHERE u.fk_asistente = s.id_asistente), '[]'::json) AS roles_cuenta,
                   COALESCE((SELECT json_agg(tv.nombre ORDER BY tv.nombre)
                               FROM vinculacion_asistente v
                               JOIN tipo_vinculacion tv ON tv.id_tipo_vinculacion = v.fk_tipo_vinculacion
                              WHERE v.fk_asistente = s.id_asistente AND v.fecha_fin IS NULL), '[]'::json) AS vinculaciones
              FROM asistente s
             WHERE s.activo
               AND (v_patron IS NULL
                    OR (s.nombres || ' ' || s.apellidos) ILIKE v_patron
                    OR s.id_asis ILIKE v_patron)
             ORDER BY s.id_asistente
             LIMIT p_limite OFFSET COALESCE(p_desplazamiento, 0)) a;
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_asistente IS
    'Solo activos; búsqueda ILIKE opcional y paginación (ADR D18). Trae roles_cuenta y vinculaciones vigentes (071).';

RESET search_path;


-- ----- migracion 072_quitar_modalidad_hibrida.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/072_quitar_modalidad_hibrida.sql
--  Proyecto Cátedras · Catálogo de modalidades
-- --------------------------------------------------------------------------
--  Se BORRA la modalidad HIBRIDA. La 062 ya la había desactivado (P1/P1b: no se
--  usa) pero la dejaba en el catálogo; ahora desaparece del todo.
--
--  Solo se borra si NADIE la usa: 'sesion' y 'solicitud_ponente' tienen FK a
--  modalidad. Si en alguna base hay una sesión o una solicitud con HIBRIDA, la
--  fila se conserva (desactivada) y hay que cambiarlas a mano antes de volver a
--  ejecutar esta migración.
--
--  Idempotente. Con un volumen de PostgreSQL ya creado hay que aplicarla a mano.
-- ==========================================================================

SET search_path TO catedras, public;

DELETE FROM modalidad m
 WHERE m.id_modalidad = 'HIBRIDA'
   AND NOT EXISTS (SELECT 1 FROM sesion            s WHERE s.fk_modalidad = m.id_modalidad)
   AND NOT EXISTS (SELECT 1 FROM solicitud_ponente p WHERE p.fk_modalidad = m.id_modalidad);

-- Si no se pudo borrar porque la usan, al menos que no se ofrezca.
UPDATE modalidad SET activo = false WHERE id_modalidad = 'HIBRIDA' AND activo;

RESET search_path;


-- ----- migracion 073_ubicacion_por_modalidad.sql -----
RESET search_path;
-- ==========================================================================
--  db/postgres/migraciones/073_ubicacion_por_modalidad.sql
--  Proyecto Cátedras · La UBICACIÓN la decide la MODALIDAD, no el canal
-- --------------------------------------------------------------------------
--  Hasta ahora "pide ubicación" era lo mismo que "el enlace es de canal QR". Con las
--  modalidades VIRTUAL y TELEPRESENCIAL en canal QR (como se decidió), el QR de una sesión
--  virtual no se podía generar: exigía una sede física con coordenadas, y la sede VIRTUAL
--  no las tiene. Pedir ubicación a una sesión virtual no tiene sentido.
--
--  AHORA
--   · modalidad.requiere_ubicacion (boolean, por defecto true): PRESENCIAL la pide;
--     VIRTUAL y TELEPRESENCIAL no.
--   · Se pide ubicación solo si el canal del enlace es QR Y la modalidad la exige. Es la
--     misma regla en las 4 piezas que la usaban:
--       sp_emitir_qr_sesion             coordenadas de la sede solo si la modalidad pide ubicación
--       fn_validar_enlace_y_ubicacion   la regla única del registro (los 3 procedimientos la usan)
--       fn_consultar_enlace_publico     requiere_ubicacion que ve la página pública
--       fn_verificar_ubicacion          aviso temprano
--   · Una sesión virtual genera su QR sin coordenadas y quien se registra NO recibe el
--     pedido de ubicación; latitud, longitud y distancia quedan en NULL (igual que con ENLACE).
--
--  El valor se cambia con:  UPDATE catedras.modalidad SET requiere_ubicacion = ... WHERE id_modalidad = ...;
--  Las funciones se recrean a partir de las de la 062 y la 063 cambiando SOLO lo descrito.
--  Idempotente (ADD COLUMN IF NOT EXISTS + CREATE OR REPLACE). Con un volumen de PostgreSQL
--  ya creado hay que aplicarla a mano con psql.
-- ==========================================================================

SET search_path TO catedras, public;

ALTER TABLE catedras.modalidad ADD COLUMN IF NOT EXISTS requiere_ubicacion boolean NOT NULL DEFAULT true;

COMMENT ON COLUMN catedras.modalidad.requiere_ubicacion IS
    '073. ¿Quien registra su asistencia (y el QR de la sesión) debe estar en la sede? true = sí (presencial); false = no se pide ubicación ni coordenadas (virtual, telepresencial).';

UPDATE catedras.modalidad SET requiere_ubicacion = false WHERE id_modalidad IN ('VIRTUAL', 'TELEPRESENCIAL');

-- --------------------------------------------------------------------------
--  1) Emisión del QR
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_emitir_qr_sesion(
    IN    p_fk_sesion  bigint,
    IN    p_fk_usuario bigint  DEFAULT NULL,
    IN    p_regenerar  boolean DEFAULT false,
    INOUT p_resultado  json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_fin        timestamptz;
    v_canal      varchar(10);
    v_requiere   boolean;      -- 073: ¿la modalidad pide ubicación?
    v_id_sede    varchar(15);
    v_nom_sede   varchar(80);
    v_sede_virt  boolean;
    v_sede_lat   numeric;
    v_id         bigint;
    v_antes      integer;
    v_desp       integer;
    v_base       text;
    v_token      uuid := gen_random_uuid();
    v_existia    boolean := false;
BEGIN
    SELECT s.fin, m.canal_difusion, m.requiere_ubicacion, sd.id_sede, sd.nombre, sd.es_virtual, sd.latitud
      INTO v_fin, v_canal, v_requiere, v_id_sede, v_nom_sede, v_sede_virt, v_sede_lat
      FROM sesion s
      JOIN modalidad m ON m.id_modalidad = s.fk_modalidad
      JOIN sede sd     ON sd.id_sede     = s.fk_sede
     WHERE s.id_sesion = p_fk_sesion;
    IF v_fin IS NULL THEN
        p_resultado := NULL;
        RETURN;
    END IF;

    -- RN-13 (script provisto): el canal del enlace debe concordar con la modalidad:
    -- presencial -> QR, virtual y telepresencial -> ENLACE, híbrida (AMBOS) -> QR.
    v_canal := CASE WHEN v_canal = 'ENLACE' THEN 'ENLACE' ELSE 'QR' END;

    -- v7.2 D72 · una sesión que PIDE ubicación (canal QR y modalidad con requiere_ubicacion) se valida contra
    -- su sede; sin sede física con coordenadas no hay con qué comparar, así que no se emite el QR.
    -- 073: una modalidad que no pide ubicación (virtual, telepresencial) genera su QR sin coordenadas.
    IF v_canal = 'QR' AND v_requiere THEN
        IF v_sede_virt THEN
            RAISE EXCEPTION 'La sesión % se difunde por QR (presencial) pero su sede "%" (%) es virtual: cambia la sede o la modalidad antes de generar el QR',
                p_fk_sesion, v_nom_sede, v_id_sede
                USING ERRCODE = 'P0001';
        END IF;
        IF v_sede_lat IS NULL THEN
            RAISE EXCEPTION 'La sede "%" (%) no tiene coordenadas: complétalas en Administración > Sedes antes de generar el QR de la sesión %',
                v_nom_sede, v_id_sede, p_fk_sesion
                USING ERRCODE = 'P0001';
        END IF;
    END IF;

    IF NOT coalesce(p_regenerar, false) THEN
        SELECT e.id_enlace INTO v_id
          FROM enlace_registro e
         WHERE e.fk_sesion = p_fk_sesion AND e.revocado_en IS NULL AND e.qr_cierre
         ORDER BY e.id_enlace DESC LIMIT 1;
        v_existia := v_id IS NOT NULL;
    END IF;

    IF v_id IS NULL THEN
        v_antes := greatest(coalesce((SELECT valor::int FROM parametro WHERE clave = 'QR_MINUTOS_ANTES_FIN'), 15), 1);
        v_desp  := greatest(coalesce((SELECT valor::int FROM parametro WHERE clave = 'QR_MINUTOS_DESPUES_FIN'), 0), 0);
        v_base  := coalesce((SELECT valor FROM parametro WHERE clave = 'URL_BASE'), 'https://catedras.usbmed.edu.co/r/');

        -- RN-14 · un solo enlace vigente por sesión
        UPDATE enlace_registro SET revocado_en = now()
         WHERE fk_sesion = p_fk_sesion AND revocado_en IS NULL;

        INSERT INTO enlace_registro (fk_sesion, token, url_publica, ruta_imagen_qr, canal, ventana,
                                     fk_usuario_crea, requiere_inscripcion, qr_cierre)
        VALUES (p_fk_sesion, v_token, v_base || v_token::text, 'qr/' || v_token::text || '.png', v_canal,
                tstzrange(v_fin - make_interval(mins => v_antes), v_fin + make_interval(mins => v_desp), '[)'),
                p_fk_usuario, false, true)
        RETURNING id_enlace INTO v_id;
    END IF;

    SELECT row_to_json(r) INTO p_resultado
      FROM (SELECT e.id_enlace, e.fk_sesion, e.token, e.canal, lower(e.ventana) AS ventana_desde,
                   upper(e.ventana) AS ventana_hasta, e.revocado_en,
                   v_existia AS ya_existia
              FROM enlace_registro e WHERE e.id_enlace = v_id) r;
END;
$$;
COMMENT ON PROCEDURE catedras.sp_emitir_qr_sesion IS
    'v7.1 D53/D54 + v7.2 D72 + 073. QR de cierre: ventana [fin - QR_MINUTOS_ANTES_FIN, fin + QR_MINUTOS_DESPUES_FIN). Si el canal es QR y la modalidad pide ubicación, exige una sede física con coordenadas (P0001 si no). NULL = la sesión no existe.';

-- --------------------------------------------------------------------------
--  2) Registro público: la regla única de ubicación
-- --------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION catedras.fn_validar_enlace_y_ubicacion(
    IN  p_token     uuid,
    IN  p_latitud   numeric,
    IN  p_longitud  numeric,
    OUT o_estado    text,
    OUT o_id_enlace bigint,
    OUT o_id_sesion bigint,
    OUT o_canal     text,
    OUT o_latitud   numeric,     -- lo que se guardará (NULL si el canal es ENLACE)
    OUT o_longitud  numeric,
    OUT o_distancia numeric,     -- metros, redondeada a 0,1; NULL si el canal es ENLACE
    OUT o_radio     integer      -- radio que se aplicó; NULL si el canal es ENLACE
)
    LANGUAGE plpgsql
    STABLE
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_ventana   tstzrange;
    v_revocado  timestamptz;
    v_requiere  boolean;     -- 073: ¿la modalidad de la sesión pide ubicación?
    v_sede_virt boolean;
    v_sede_lat  numeric;
    v_sede_lon  numeric;
    v_sede_rad  integer;
    v_dist      double precision;
BEGIN
    SELECT e.id_enlace, e.fk_sesion, e.canal, e.ventana, e.revocado_en
      INTO o_id_enlace, o_id_sesion, o_canal, v_ventana, v_revocado
      FROM enlace_registro e
     WHERE e.token = p_token;

    IF NOT FOUND THEN
        o_estado := 'ENLACE_NO_EXISTE';  RETURN;
    END IF;
    IF v_revocado IS NOT NULL THEN
        o_estado := 'ENLACE_REVOCADO';   RETURN;
    END IF;
    IF NOT (v_ventana @> now()) THEN
        o_estado := 'FUERA_DE_VENTANA';  RETURN;
    END IF;

    -- 073: la ubicación se pide solo si el canal es QR Y la modalidad de la sesión la exige
    -- (modalidad.requiere_ubicacion). Una sesión virtual o telepresencial no la pide, aunque se difunda por QR.
    SELECT m.requiere_ubicacion INTO v_requiere
      FROM sesion s JOIN modalidad m ON m.id_modalidad = s.fk_modalidad
     WHERE s.id_sesion = o_id_sesion;

    IF o_canal <> 'QR' OR NOT coalesce(v_requiere, true) THEN
        o_latitud := NULL;  o_longitud := NULL;  o_distancia := NULL;  o_radio := NULL;
        o_estado := 'OK';   RETURN;
    END IF;

    -- Sesión que pide ubicación (presencial): es obligatoria y debe caer en la sede.
    IF p_latitud IS NULL OR p_longitud IS NULL THEN
        o_estado := 'SIN_UBICACION';     RETURN;
    END IF;
    IF p_latitud NOT BETWEEN -90 AND 90 OR p_longitud NOT BETWEEN -180 AND 180 THEN
        o_estado := 'DATOS_INVALIDOS';   RETURN;
    END IF;

    SELECT sd.es_virtual, sd.latitud, sd.longitud, sd.radio_metros
      INTO v_sede_virt, v_sede_lat, v_sede_lon, v_sede_rad
      FROM sesion s JOIN sede sd ON sd.id_sede = s.fk_sede
     WHERE s.id_sesion = o_id_sesion;

    -- Sin coordenadas de sede NO se acepta la asistencia sin comprobar el lugar.
    IF v_sede_virt OR v_sede_lat IS NULL OR v_sede_lon IS NULL THEN
        o_estado := 'SEDE_SIN_COORDENADAS';  RETURN;
    END IF;

    -- Radio efectivo: el de la sede, o el parámetro global, o 200 por si falta.
    o_radio := COALESCE(v_sede_rad,
                        (SELECT valor::int FROM parametro WHERE clave = 'RADIO_VALIDACION_METROS'),
                        200);
    v_dist  := catedras.fn_distancia_metros(p_latitud::double precision, p_longitud::double precision,
                                            v_sede_lat::double precision, v_sede_lon::double precision);
    o_distancia := round(v_dist::numeric, 1);
    o_latitud   := p_latitud;
    o_longitud  := p_longitud;

    IF v_dist > o_radio THEN
        o_estado := 'FUERA_DE_RADIO';    RETURN;
    END IF;

    o_estado := 'OK';
END;
$$;
COMMENT ON FUNCTION catedras.fn_validar_enlace_y_ubicacion IS
    'v7.2 D61/D64 + 073. Valida enlace (existe, no revocado, en ventana) y, si el canal es QR y la modalidad pide ubicación, la ubicación contra la sede. Devuelve el estado y los datos a guardar.';

CREATE OR REPLACE FUNCTION catedras.fn_consultar_enlace_publico(p_token uuid)
    RETURNS json
    LANGUAGE plpgsql
    STABLE
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_res json;
BEGIN
    SELECT row_to_json(r) INTO v_res
      FROM (SELECT e.id_enlace,
                   s.id_sesion,
                   s.titulo                               AS titulo_sesion,
                   s.numero_reunion,
                   c.id_evento_asis                       AS id_catedra,
                   c.nombre                               AS titulo_catedra,
                   s.inicio, s.fin,
                   s.fk_modalidad                         AS modalidad,
                   e.canal,
                   (e.canal = 'QR' AND m.requiere_ubicacion) AS requiere_ubicacion,
                   lower(e.ventana)                       AS ventana_desde,
                   upper(e.ventana)                       AS ventana_hasta,
                   CASE WHEN e.revocado_en IS NOT NULL THEN 'revocado'
                        WHEN now() <  lower(e.ventana)    THEN 'pendiente'
                        WHEN e.ventana @> now()           THEN 'abierto'
                        ELSE 'cerrado' END                AS estado_ventana
              FROM enlace_registro e
              JOIN sesion  s ON s.id_sesion   = e.fk_sesion
              JOIN modalidad m ON m.id_modalidad = s.fk_modalidad
              JOIN catedra c ON c.id_catedra  = s.fk_catedra
             WHERE e.token = p_token) r;
    RETURN v_res;
END;
$$;
COMMENT ON FUNCTION catedras.fn_consultar_enlace_publico IS
    'v7.2 RF-S9 + 073. Título de la cátedra y la sesión, canal, si requiere ubicación (canal QR y modalidad que la exige) y estado de la ventana. NULL si el token no existe. No devuelve coordenadas.';

CREATE OR REPLACE FUNCTION catedras.fn_verificar_ubicacion(
    p_token    uuid,
    p_latitud  numeric,
    p_longitud numeric
) RETURNS json
    LANGUAGE plpgsql
    STABLE
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v     record;
    v_req boolean;     -- 073: ¿este enlace pide ubicación? (canal QR y modalidad que la exige)
BEGIN
    SELECT * INTO v FROM catedras.fn_validar_enlace_y_ubicacion(p_token, p_latitud, p_longitud);

    SELECT (e.canal = 'QR' AND m.requiere_ubicacion) INTO v_req
      FROM enlace_registro e
      JOIN sesion    s ON s.id_sesion    = e.fk_sesion
      JOIN modalidad m ON m.id_modalidad = s.fk_modalidad
     WHERE e.token = p_token;

    RETURN json_build_object(
        'estado',             v.o_estado,
        'requiere_ubicacion', coalesce(v_req, false),
        'dentro',             CASE WHEN NOT coalesce(v_req, false)        THEN NULL
                                   WHEN v.o_estado = 'OK'              THEN true
                                   WHEN v.o_estado = 'FUERA_DE_RADIO'  THEN false
                                   ELSE NULL END,
        'distancia_metros',   v.o_distancia,
        'radio_metros',       v.o_radio);
END;
$$;
COMMENT ON FUNCTION catedras.fn_verificar_ubicacion IS
    'v7.2 RF-S10 + 073. Aviso temprano: dice si la ubicación cae dentro del radio de la sede (solo si el enlace la pide). No guarda nada; el procedimiento de registro la vuelve a verificar.';

RESET search_path;

-- ----- ajustes de catalogo: los mismos valores de la base local -----
RESET search_path;
SET search_path TO catedras, public;

--  AJUSTES DE CATALOGO (iguales a la base local de desarrollo; el script provisto traia otros valores).
--  Si no los quieres, borra este bloque:
--
--  1) Modalidades VIRTUAL y TELEPRESENCIAL con canal 'QR' (el provisto: 'ENLACE').
--     La ubicacion NO se pide por esto: la decide modalidad.requiere_ubicacion (migracion 073: PRESENCIAL si; VIRTUAL y
--     TELEPRESENCIAL no), asi que una sesion virtual genera su QR sin coordenadas aunque el canal sea QR.
--     Para volver al canal original:
--       UPDATE catedras.modalidad SET canal_difusion = 'ENLACE' WHERE id_modalidad IN ('VIRTUAL', 'TELEPRESENCIAL');
UPDATE catedras.modalidad SET canal_difusion = 'QR' WHERE id_modalidad IN ('VIRTUAL', 'TELEPRESENCIAL');

--  2) Tipo de documento «LC» (licencia de conduccion) inactivo (el provisto: activo).
UPDATE catedras.tipo_documento SET activo = false WHERE id_tipo_documento = 'LC';

-- ##########  PARTE 3: ADMINISTRADOR INICIAL (opcional)  ##########
--  Sin al menos un administrador nadie puede entrar a /admin. Es la misma cuenta
--  del script provisto ('admin.catedras', con su clave actual): se recrea sin
--  documento de identidad. Borra este bloque si prefieres crear el tuyo.
RESET search_path;
SET search_path TO catedras, public;

WITH a AS (
    INSERT INTO asistente (id_asis, nombres, apellidos, correo_institucional, es_externo)
    VALUES ('30016947', 'Administrador', 'Catedras', 'admin.catedras@usbmed.edu.co', false)
    RETURNING id_asistente
), v AS (
    INSERT INTO vinculacion_asistente (fk_asistente, fk_tipo_vinculacion, fecha_ini)
    SELECT id_asistente, 'ADMINISTRATIVO', DATE '2026-01-01' FROM a
), u AS (
    INSERT INTO usuario (fk_asistente, usuario, clave_hash, activo)
    SELECT id_asistente, 'admin.catedras',
           '$2b$12$1hxs9gOETaNm5DV5pGEEGOpHVJwGoCP0Q9unvLt9oVyd5mbh6XLUm', true FROM a
    RETURNING id_usuario
)
INSERT INTO rol_por_usuario (fk_usuario, fk_rol, fecha_ini)
SELECT id_usuario, 'ADMIN', DATE '2026-01-01' FROM u;

-- Resumen para comprobar la instalacion
SELECT (SELECT count(*) FROM information_schema.tables WHERE table_schema='catedras' AND table_type='BASE TABLE') AS tablas,
       (SELECT count(*) FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace WHERE n.nspname='catedras') AS rutinas,
       (SELECT count(*) FROM catedras.usuario) AS usuarios,
       (SELECT count(*) FROM catedras.asistente) AS asistentes,
       (SELECT count(*) FROM catedras.catedra) AS catedras,
       (SELECT string_agg(id_rol, ', ' ORDER BY id_rol) FROM catedras.rol) AS roles,
       (SELECT string_agg(id_modalidad, ', ' ORDER BY id_modalidad) FROM catedras.modalidad) AS modalidades,
       (SELECT count(*) FROM catedras.programa_academico) AS programas;

COMMIT;
