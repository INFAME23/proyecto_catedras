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
