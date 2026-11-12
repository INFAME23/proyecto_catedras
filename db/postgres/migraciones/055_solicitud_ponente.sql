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
