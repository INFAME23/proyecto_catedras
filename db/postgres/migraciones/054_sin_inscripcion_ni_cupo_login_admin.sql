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

