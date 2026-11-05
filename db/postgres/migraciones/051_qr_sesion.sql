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
