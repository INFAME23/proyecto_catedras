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
