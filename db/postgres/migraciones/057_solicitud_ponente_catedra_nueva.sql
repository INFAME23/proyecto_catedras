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
