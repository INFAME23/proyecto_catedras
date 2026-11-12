-- ==========================================================================
--  db/postgres/migraciones/058_solicitud_ponente_modalidad_sede_periodo.sql
--  Proyecto Cátedras · SUBVERSIÓN v7.1 · D58 (el ponente también elige modalidad, sede y periodo)
-- --------------------------------------------------------------------------
--  Además de la fecha y la hora, el ponente que propone una cátedra nueva elige
--  la MODALIDAD, la SEDE y el PERIODO ACADÉMICO. Quedan en la solicitud y la
--  pantalla de decisión del administrador los trae ya elegidos (los puede cambiar).
--
--  Reglas nuevas (P0001 -> 409 en la API):
--    · el periodo debe estar ACTIVO y la fecha de la cátedra debe caer DENTRO de él
--      (las cátedras se dictan en periodos específicos); se revisa al proponer y
--      otra vez al aprobar (el administrador puede cambiar la fecha o el periodo);
--    · la sede debe estar ACTIVA.
--  Las solicitudes anteriores no traen estos datos (columnas null): el administrador
--  los completa al aprobar, como en la 057.
--
--  Cambia la firma de sp_crear_solicitud_ponente (3 parámetros más); se borra la
--  anterior para que un CALL no sea ambiguo. sp_resolver_solicitud_ponente conserva
--  su firma: lo que el administrador no escribe se toma de la solicitud. Idempotente.
--  Depende de la 055, la 056 y la 057.
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  Tabla
-- --------------------------------------------------------------------------
ALTER TABLE catedras.solicitud_ponente
    ADD COLUMN IF NOT EXISTS fk_modalidad character varying(15),
    ADD COLUMN IF NOT EXISTS fk_sede      character varying(15),
    ADD COLUMN IF NOT EXISTS fk_periodo   character(6);

ALTER TABLE catedras.solicitud_ponente DROP CONSTRAINT IF EXISTS fk_solponente_modalidad;
ALTER TABLE catedras.solicitud_ponente ADD CONSTRAINT fk_solponente_modalidad FOREIGN KEY (fk_modalidad)
    REFERENCES catedras.modalidad (id_modalidad) ON UPDATE CASCADE ON DELETE SET NULL;
ALTER TABLE catedras.solicitud_ponente DROP CONSTRAINT IF EXISTS fk_solponente_sede;
ALTER TABLE catedras.solicitud_ponente ADD CONSTRAINT fk_solponente_sede FOREIGN KEY (fk_sede)
    REFERENCES catedras.sede (id_sede) ON UPDATE CASCADE ON DELETE SET NULL;
ALTER TABLE catedras.solicitud_ponente DROP CONSTRAINT IF EXISTS fk_solponente_periodo;
ALTER TABLE catedras.solicitud_ponente ADD CONSTRAINT fk_solponente_periodo FOREIGN KEY (fk_periodo)
    REFERENCES catedras.periodo_academico (id_periodo) ON UPDATE CASCADE ON DELETE SET NULL;

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
       sp.fk_modalidad,
       m.nombre                          AS nombre_modalidad,
       sp.fk_sede,
       se.nombre                         AS nombre_sede,
       sp.fk_periodo,
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
  LEFT JOIN catedras.modalidad m  ON m.id_modalidad = sp.fk_modalidad
  LEFT JOIN catedras.sede      se ON se.id_sede     = sp.fk_sede
  LEFT JOIN catedras.sesion  s ON s.id_sesion  = sp.fk_sesion
  LEFT JOIN catedras.catedra c ON c.id_catedra = coalesce(s.fk_catedra, sp.fk_catedra);

-- --------------------------------------------------------------------------
--  ¿La fecha cabe en el periodo y la sede está activa? (P0001 si no)
-- --------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION catedras.fn_validar_periodo_y_sede(
    p_inicio timestamptz, p_fin timestamptz, p_fk_periodo text, p_fk_sede text)
    RETURNS void
    LANGUAGE plpgsql STABLE
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_per periodo_academico%ROWTYPE;
    v_activa boolean;
BEGIN
    IF p_fk_periodo IS NOT NULL THEN
        SELECT * INTO v_per FROM periodo_academico WHERE id_periodo = p_fk_periodo;
        IF NOT FOUND THEN
            RAISE EXCEPTION 'El periodo académico % no existe.', p_fk_periodo USING ERRCODE = 'P0001';
        END IF;
        IF NOT v_per.activo THEN
            RAISE EXCEPTION 'El periodo académico % no está activo.', btrim(p_fk_periodo) USING ERRCODE = 'P0001';
        END IF;
        IF (p_inicio AT TIME ZONE 'America/Bogota')::date < v_per.fecha_ini
           OR (p_fin AT TIME ZONE 'America/Bogota')::date > v_per.fecha_fin THEN
            RAISE EXCEPTION 'La fecha de la cátedra (% a %, hora de Colombia) no cae dentro del periodo % (del % al %): elige otra fecha u otro periodo.',
                to_char(p_inicio AT TIME ZONE 'America/Bogota', 'YYYY-MM-DD HH24:MI'),
                to_char(p_fin AT TIME ZONE 'America/Bogota', 'YYYY-MM-DD HH24:MI'),
                btrim(p_fk_periodo), to_char(v_per.fecha_ini, 'YYYY-MM-DD'), to_char(v_per.fecha_fin, 'YYYY-MM-DD')
                USING ERRCODE = 'P0001';
        END IF;
    END IF;

    IF p_fk_sede IS NOT NULL THEN
        SELECT activo INTO v_activa FROM sede WHERE id_sede = p_fk_sede;
        IF NOT FOUND THEN
            RAISE EXCEPTION 'La sede % no existe.', p_fk_sede USING ERRCODE = 'P0001';
        END IF;
        IF NOT v_activa THEN
            RAISE EXCEPTION 'La sede % no está activa.', p_fk_sede USING ERRCODE = 'P0001';
        END IF;
    END IF;
END;
$$;

-- --------------------------------------------------------------------------
--  CREAR — ahora con modalidad, sede y periodo
-- --------------------------------------------------------------------------
DROP PROCEDURE IF EXISTS catedras.sp_crear_solicitud_ponente(text, text, text, text, text, text, text, text, text, text, timestamp with time zone, timestamp with time zone, json);

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
    IN    p_fk_modalidad         text,
    IN    p_fk_sede              text,
    IN    p_fk_periodo           text,
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

    -- El periodo debe estar activo y contener la fecha; la sede debe estar activa.
    PERFORM fn_validar_periodo_y_sede(p_inicio, p_fin, p_fk_periodo, p_fk_sede);

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
                                   titulo_ponencia, resumen_ponencia, inicio_propuesto, fin_propuesto,
                                   fk_modalidad, fk_sede, fk_periodo)
    VALUES (p_nombres, p_apellidos, p_fk_tipo_documento, p_numero_documento,
            p_perfil_profesional, p_direccion, p_telefono, p_correo_institucional,
            p_titulo_ponencia, p_resumen_ponencia, p_inicio, p_fin,
            nullif(btrim(p_fk_modalidad), ''), nullif(btrim(p_fk_sede), ''), nullif(btrim(p_fk_periodo), ''))
    RETURNING id_solicitud INTO v_id;

    SELECT row_to_json(v) INTO p_resultado FROM v_solicitud_ponente v WHERE v.id_solicitud = v_id;
END;
$$;

-- --------------------------------------------------------------------------
--  RESOLVER — igual que la 057, pero modalidad, sede y periodo que el administrador no escribe
--  se toman de la solicitud, y la fecha debe caber en el periodo elegido.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_resolver_solicitud_ponente(
    IN    p_id_solicitud   bigint,
    IN    p_aprobar        boolean,
    IN    p_observacion    text   DEFAULT NULL,
    IN    p_resuelto_por   bigint DEFAULT NULL,
    IN    p_nombre_catedra text   DEFAULT NULL,   -- vacío = el título de la ponencia
    IN    p_fk_tipo_evento text   DEFAULT NULL,   -- vacío = CAAB (cátedra abierta)
    IN    p_id_evento_asis text   DEFAULT NULL,   -- vacío = el siguiente código libre
    IN    p_fk_modalidad   text   DEFAULT NULL,   -- vacío = la que eligió el ponente
    IN    p_fk_sede        text   DEFAULT NULL,   -- vacío = la que eligió el ponente
    IN    p_fk_periodo     text   DEFAULT NULL,   -- vacío = el que eligió el ponente
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
    v_modalidad   text;
    v_sede        text;
    v_periodo     text;
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
            v_inicio    := coalesce(p_inicio, v_sol.inicio_propuesto);
            v_fin       := coalesce(p_fin, v_sol.fin_propuesto);
            v_modalidad := coalesce(nullif(btrim(coalesce(p_fk_modalidad, '')), ''), v_sol.fk_modalidad);
            v_sede      := coalesce(nullif(btrim(coalesce(p_fk_sede, '')), ''), v_sol.fk_sede);
            v_periodo   := coalesce(nullif(btrim(coalesce(p_fk_periodo, '')), ''), btrim(v_sol.fk_periodo));
            IF v_fin <= v_inicio THEN
                RAISE EXCEPTION 'La hora de fin debe ser posterior a la de inicio.' USING ERRCODE = 'P0001';
            END IF;
            IF v_inicio <= now() THEN
                RAISE EXCEPTION 'La fecha propuesta ya pasó: ajusta la fecha y la hora para aprobar la cátedra.' USING ERRCODE = 'P0001';
            END IF;
            IF v_modalidad IS NULL OR v_sede IS NULL OR v_periodo IS NULL THEN
                RAISE EXCEPTION 'Para aprobar hay que indicar la modalidad, la sede y el periodo de la cátedra nueva.'
                    USING ERRCODE = 'P0001';
            END IF;
            PERFORM fn_validar_periodo_y_sede(v_inicio, v_fin, v_periodo, v_sede);
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
                    'fk_modalidad', btrim(v_modalidad),
                    'fk_sede',      btrim(v_sede),
                    'fk_periodo',   btrim(v_periodo),
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
    'v7.1 D58. Registra la postulación (cátedra nueva con fecha, hora, modalidad, sede y periodo) como PENDIENTE. P0001 si la fecha pasó, no cae en el periodo, el periodo o la sede no están activos, no cabe en los periodos de programación o ya hay una pendiente igual.';
COMMENT ON FUNCTION catedras.fn_validar_periodo_y_sede IS
    'v7.1 D58. P0001 si el periodo no existe, no está activo o no contiene la fecha, o si la sede no existe o no está activa.';
