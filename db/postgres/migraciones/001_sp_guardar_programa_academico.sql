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
