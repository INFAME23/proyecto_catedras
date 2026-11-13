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
