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
