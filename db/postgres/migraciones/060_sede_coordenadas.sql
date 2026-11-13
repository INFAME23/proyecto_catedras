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
