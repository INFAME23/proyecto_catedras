-- ==========================================================================
--  db/postgres/migraciones/063_sp_registro_publico.sql
--  Proyecto Cátedras · SUBVERSIÓN v7.2 · RF-S9 / RF-S10 · D60, D61, D63, D64, D65, D66, D67
-- --------------------------------------------------------------------------
--  Registro público de asistencia SIN clave por correo (D60). Tras escanear el
--  QR, la persona elige ser estudiante, docente o externo:
--
--    fn_consultar_enlace_publico(token)   datos para pintar la página
--    fn_verificar_ubicacion(token,lat,lng) aviso temprano: ¿estás en la sede?
--    sp_registrar_asistencia_estudiante   carnet
--    sp_registrar_asistencia_docente      cédula, nombre, contacto, facultad
--    sp_registrar_asistencia_externo      cédula, nombre, contacto, procedencia
--
--  Los tres procedimientos validan, EN UNA SOLA TRANSACCIÓN (Guía IA, Regla 1):
--  enlace (existe, no revocado, dentro de su ventana) -> ubicación (solo si el
--  canal es QR) -> persona -> vinculación -> duplicado -> INSERT. La BD no
--  confía en la API ni la API en el front.
--
--  ESTADO EN VEZ DE EXCEPCIÓN (D64, mismo criterio que la D41): los casos de
--  negocio devuelven un texto en p_estado y NO insertan nada. Los errores
--  inesperados (FK, CHECK) siguen subiendo como excepción (500).
--
--    OK · ENLACE_NO_EXISTE · ENLACE_REVOCADO · FUERA_DE_VENTANA · SIN_UBICACION
--    SEDE_SIN_COORDENADAS · FUERA_DE_RADIO · ESTUDIANTE_NO_EXISTE
--    SIN_VINCULACION_VIGENTE · PERSONA_INACTIVA · YA_REGISTRADO · DATOS_INVALIDOS
--
--  UBICACIÓN POR CANAL (D61): canal QR -> se exige y se compara con la sede;
--  canal ENLACE -> no se pide, y latitud/longitud/distancia quedan en NULL
--  aunque el navegador las hubiera enviado.
--
--  Quedan 'ip' y 'user_agent' siempre (las columnas ya existían, v5).
--  El disparador trg_registro_validar (RN-15, snapshots, RN-07) sigue activo
--  como SEGUNDA barrera.
--
--  Idempotente (CREATE OR REPLACE). Si conservas el volumen de PostgreSQL hay
--  que aplicarla a mano.
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  0) Pieza común: el enlace y la ubicación.
--     La usan los tres procedimientos y el aviso temprano, para que haya UNA
--     sola regla. Devuelve el estado y, si es OK, lo que hace falta insertar.
--     STABLE: solo lee (y mira now()).
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

    -- Canal ENLACE (virtual/telepresencial): no se pide ni se valida ubicación.
    IF o_canal <> 'QR' THEN
        o_latitud := NULL;  o_longitud := NULL;  o_distancia := NULL;  o_radio := NULL;
        o_estado := 'OK';   RETURN;
    END IF;

    -- Canal QR (presencial): la ubicación es obligatoria y debe caer en la sede.
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
    'v7.2 D61/D64. Valida enlace (existe, no revocado, en ventana) y, si el canal es QR, la ubicación contra la sede. Devuelve el estado y los datos a guardar.';

-- --------------------------------------------------------------------------
--  1) Datos del enlace para pintar la página pública (no incluye las
--     coordenadas de la sede). NULL = el token no existe.
-- --------------------------------------------------------------------------
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
                   (e.canal = 'QR')                       AS requiere_ubicacion,
                   lower(e.ventana)                       AS ventana_desde,
                   upper(e.ventana)                       AS ventana_hasta,
                   CASE WHEN e.revocado_en IS NOT NULL THEN 'revocado'
                        WHEN now() <  lower(e.ventana)    THEN 'pendiente'
                        WHEN e.ventana @> now()           THEN 'abierto'
                        ELSE 'cerrado' END                AS estado_ventana
              FROM enlace_registro e
              JOIN sesion  s ON s.id_sesion   = e.fk_sesion
              JOIN catedra c ON c.id_catedra  = s.fk_catedra
             WHERE e.token = p_token) r;
    RETURN v_res;
END;
$$;

COMMENT ON FUNCTION catedras.fn_consultar_enlace_publico IS
    'v7.2 RF-S9. Título de la cátedra y la sesión, canal, si requiere ubicación y estado de la ventana. NULL si el token no existe. No devuelve coordenadas.';

-- --------------------------------------------------------------------------
--  2) Aviso temprano de ubicación (no guarda nada).
--     { estado, requiere_ubicacion, dentro, distancia_metros, radio_metros }
-- --------------------------------------------------------------------------
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
    v record;
BEGIN
    SELECT * INTO v FROM catedras.fn_validar_enlace_y_ubicacion(p_token, p_latitud, p_longitud);

    RETURN json_build_object(
        'estado',             v.o_estado,
        'requiere_ubicacion', (v.o_canal = 'QR'),
        'dentro',             CASE WHEN v.o_canal IS DISTINCT FROM 'QR' THEN NULL
                                   WHEN v.o_estado = 'OK'              THEN true
                                   WHEN v.o_estado = 'FUERA_DE_RADIO'  THEN false
                                   ELSE NULL END,
        'distancia_metros',   v.o_distancia,
        'radio_metros',       v.o_radio);
END;
$$;

COMMENT ON FUNCTION catedras.fn_verificar_ubicacion IS
    'v7.2 RF-S10. Aviso temprano: dice si la ubicación cae dentro del radio de la sede de la sesión del token. No guarda nada; el procedimiento de registro la vuelve a verificar.';

-- --------------------------------------------------------------------------
--  3) ESTUDIANTE: identificado por su carnet (asistente.id_asis)
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_registrar_asistencia_estudiante(
    IN    p_token            uuid,
    IN    p_carnet           text,
    IN    p_latitud          numeric DEFAULT NULL,
    IN    p_longitud         numeric DEFAULT NULL,
    IN    p_ip               inet    DEFAULT NULL,
    IN    p_user_agent       text    DEFAULT NULL,
    INOUT p_estado           text    DEFAULT NULL,
    INOUT p_id_registro      bigint  DEFAULT NULL,
    INOUT p_distancia_metros numeric DEFAULT NULL,
    INOUT p_radio_metros     integer DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_carnet    text := btrim(COALESCE(p_carnet, ''));
    v_asistente bigint;
    e           record;
BEGIN
    p_id_registro := NULL;

    -- 0) forma del dato (mismo patrón que chk_asistente_idasis)
    IF v_carnet !~ '^[0-9A-Za-z]{5,15}$' THEN
        p_estado := 'DATOS_INVALIDOS';  RETURN;
    END IF;

    -- 1) enlace y ubicación (una sola regla: fn_validar_enlace_y_ubicacion)
    SELECT * INTO e FROM catedras.fn_validar_enlace_y_ubicacion(p_token, p_latitud, p_longitud);
    p_distancia_metros := e.o_distancia;
    p_radio_metros     := e.o_radio;
    IF e.o_estado <> 'OK' THEN
        p_estado := e.o_estado;  RETURN;
    END IF;

    -- 2) la persona: debe existir como estudiante cargado y estar activa
    SELECT a.id_asistente INTO v_asistente
      FROM asistente a
     WHERE a.id_asis = v_carnet AND a.activo;
    IF v_asistente IS NULL THEN
        p_estado := 'ESTUDIANTE_NO_EXISTE';  RETURN;
    END IF;

    -- 3) con vinculación ESTUDIANTE vigente (sin fecha de fin)
    IF NOT EXISTS (SELECT 1 FROM vinculacion_asistente v
                    WHERE v.fk_asistente = v_asistente
                      AND v.fk_tipo_vinculacion = 'ESTUDIANTE'
                      AND v.fecha_fin IS NULL) THEN
        p_estado := 'SIN_VINCULACION_VIGENTE';  RETURN;
    END IF;

    -- 4) una persona no se registra dos veces en la misma sesión
    IF EXISTS (SELECT 1 FROM registro_asistencia r
                WHERE r.fk_sesion = e.o_id_sesion AND r.fk_asistente = v_asistente) THEN
        p_estado := 'YA_REGISTRADO';  RETURN;
    END IF;

    -- 5) el registro. La vinculación se pasa EXPLÍCITA: si la persona tuviera
    --    varias vigentes, la asistencia queda como lo que eligió ser.
    --    Dos envíos simultáneos: el segundo choca con uq_registro_sesion_asis y
    --    se atrapa aquí (nunca un 500).
    BEGIN
        INSERT INTO registro_asistencia (fk_sesion, fk_asistente, fk_enlace, fk_clave,
                                         fk_vinculacion_snapshot, origen, ip, user_agent,
                                         latitud, longitud, distancia_metros, identificado_por)
        VALUES (e.o_id_sesion, v_asistente, e.o_id_enlace, NULL,
                'ESTUDIANTE', e.o_canal, p_ip, left(p_user_agent, 300),
                e.o_latitud, e.o_longitud, e.o_distancia, 'CARNET')
        RETURNING id_registro INTO p_id_registro;
    EXCEPTION WHEN unique_violation THEN
        p_id_registro := NULL;
        p_estado := 'YA_REGISTRADO';  RETURN;
    END;

    p_estado := 'OK';
END;
$$;

-- --------------------------------------------------------------------------
--  Pieza común de DOCENTE y EXTERNO: "contacto" es UN campo. Con '@' es un
--  correo (correo_personal); si no, un celular. Devuelve NULL en los dos si
--  el contacto no sirve.
-- --------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION catedras.fn_interpretar_contacto(
    IN  p_contacto text,
    OUT o_correo   text,
    OUT o_celular  text,
    OUT o_valido   boolean
)
    LANGUAGE plpgsql
    IMMUTABLE
    AS $$
DECLARE
    v text := btrim(COALESCE(p_contacto, ''));
BEGIN
    o_correo := NULL;  o_celular := NULL;  o_valido := false;

    IF position('@' IN v) > 0 THEN
        -- mismo formato que la API de Asistente y chk_asistente_cor_pers_fmt
        IF length(v) <= 254 AND v ~ '^[^@[:space:]]+@[^@[:space:]]+\.[^@[:space:]]+$' THEN
            o_correo := v;  o_valido := true;
        END IF;
    ELSE
        -- celular: hasta 20 caracteres (columna), con al menos 7 dígitos
        IF length(v) <= 20 AND v ~ '^[0-9+() -]+$'
           AND length(regexp_replace(v, '[^0-9]', '', 'g')) >= 7 THEN
            o_celular := v;  o_valido := true;
        END IF;
    END IF;
END;
$$;

-- --------------------------------------------------------------------------
--  Pieza común de DOCENTE y EXTERNO: busca a la persona por cédula (CC) y, si
--  no existe, la crea (es_externo = true: no tiene código ASIS, D67). Si ya
--  existe NO se pisa nada: solo se rellenan correo y celular vacíos.
--  Devuelve NULL si la persona existe pero está inactiva.
-- --------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION catedras.fn_persona_por_cedula(
    p_cedula   text,
    p_nombre   text,
    p_correo   text,
    p_celular  text
) RETURNS bigint
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_id     bigint;
    v_activo boolean;
BEGIN
    SELECT a.id_asistente, a.activo INTO v_id, v_activo
      FROM documento_asistente d
      JOIN asistente a ON a.id_asistente = d.fk_asistente
     WHERE d.fk_tipo_documento = 'CC' AND d.numero = p_cedula;

    IF v_id IS NOT NULL THEN
        IF NOT v_activo THEN
            RETURN NULL;
        END IF;
        -- Se reutiliza la persona. Solo se completan datos vacíos.
        UPDATE asistente
           SET correo_personal = COALESCE(correo_personal, p_correo),
               celular         = COALESCE(celular, p_celular)
         WHERE id_asistente = v_id
           AND ((correo_personal IS NULL AND p_correo IS NOT NULL)
             OR (celular IS NULL AND p_celular IS NOT NULL));
        RETURN v_id;
    END IF;

    -- Persona nueva. 'nombres' es NOT NULL y mide 100: el nombre completo que
    -- escribió va entero en nombre_completo_asis (200) y 'nombres' lleva su
    -- principio; 'apellidos' no existe como dato aparte ('-'), D67.
    INSERT INTO asistente (nombres, apellidos, nombre_completo_asis, correo_personal, celular,
                           es_externo, contacto_sin_correo)
    VALUES (left(p_nombre, 100), '-', left(p_nombre, 200), p_correo, p_celular,
            true, p_correo IS NULL)
    RETURNING id_asistente INTO v_id;

    INSERT INTO documento_asistente (fk_asistente, fk_tipo_documento, numero, vigente)
    VALUES (v_id, 'CC', p_cedula, true);

    RETURN v_id;
END;
$$;

-- --------------------------------------------------------------------------
--  4) DOCENTE: cédula + nombre + contacto + facultad
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_registrar_asistencia_docente(
    IN    p_token            uuid,
    IN    p_cedula           text,
    IN    p_nombre           text,
    IN    p_contacto         text,
    IN    p_fk_facultad      text,
    IN    p_latitud          numeric DEFAULT NULL,
    IN    p_longitud         numeric DEFAULT NULL,
    IN    p_ip               inet    DEFAULT NULL,
    IN    p_user_agent       text    DEFAULT NULL,
    INOUT p_estado           text    DEFAULT NULL,
    INOUT p_id_registro      bigint  DEFAULT NULL,
    INOUT p_distancia_metros numeric DEFAULT NULL,
    INOUT p_radio_metros     integer DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_cedula    text := btrim(COALESCE(p_cedula, ''));
    v_nombre    text := btrim(COALESCE(p_nombre, ''));
    v_facultad  text := btrim(COALESCE(p_fk_facultad, ''));
    v_asistente bigint;
    c           record;
    e           record;
BEGIN
    p_id_registro := NULL;

    -- 0) forma de los datos (la facultad debe existir y estar activa: Regla 2)
    SELECT * INTO c FROM catedras.fn_interpretar_contacto(p_contacto);
    IF v_cedula !~ '^[0-9A-Za-z-]{4,20}$'
       OR length(v_nombre) NOT BETWEEN 1 AND 200
       OR NOT c.o_valido
       OR NOT EXISTS (SELECT 1 FROM facultad f WHERE f.id_facultad = v_facultad AND f.activo) THEN
        p_estado := 'DATOS_INVALIDOS';  RETURN;
    END IF;

    -- 1) enlace y ubicación
    SELECT * INTO e FROM catedras.fn_validar_enlace_y_ubicacion(p_token, p_latitud, p_longitud);
    p_distancia_metros := e.o_distancia;
    p_radio_metros     := e.o_radio;
    IF e.o_estado <> 'OK' THEN
        p_estado := e.o_estado;  RETURN;
    END IF;

    -- 2) la persona: se reutiliza por cédula o se crea
    v_asistente := catedras.fn_persona_por_cedula(v_cedula, v_nombre, c.o_correo, c.o_celular);
    IF v_asistente IS NULL THEN
        p_estado := 'PERSONA_INACTIVA';  RETURN;
    END IF;

    -- 3) vinculación DOCENTE vigente: si no la tiene, se le crea
    IF NOT EXISTS (SELECT 1 FROM vinculacion_asistente v
                    WHERE v.fk_asistente = v_asistente
                      AND v.fk_tipo_vinculacion = 'DOCENTE' AND v.fecha_fin IS NULL) THEN
        INSERT INTO vinculacion_asistente (fk_asistente, fk_tipo_vinculacion, fecha_ini)
        VALUES (v_asistente, 'DOCENTE', CURRENT_DATE)
        ON CONFLICT (fk_asistente, fk_tipo_vinculacion, fecha_ini) DO UPDATE SET fecha_fin = NULL;
    END IF;

    -- 4) duplicado
    IF EXISTS (SELECT 1 FROM registro_asistencia r
                WHERE r.fk_sesion = e.o_id_sesion AND r.fk_asistente = v_asistente) THEN
        p_estado := 'YA_REGISTRADO';  RETURN;
    END IF;

    -- 5) el registro (con la facultad que declaró, como instantánea)
    BEGIN
        INSERT INTO registro_asistencia (fk_sesion, fk_asistente, fk_enlace, fk_clave,
                                         fk_vinculacion_snapshot, fk_facultad_snapshot,
                                         origen, ip, user_agent,
                                         latitud, longitud, distancia_metros, identificado_por)
        VALUES (e.o_id_sesion, v_asistente, e.o_id_enlace, NULL,
                'DOCENTE', v_facultad,
                e.o_canal, p_ip, left(p_user_agent, 300),
                e.o_latitud, e.o_longitud, e.o_distancia, 'DECLARADO')
        RETURNING id_registro INTO p_id_registro;
    EXCEPTION WHEN unique_violation THEN
        p_id_registro := NULL;
        p_estado := 'YA_REGISTRADO';  RETURN;
    END;

    p_estado := 'OK';
END;
$$;

-- --------------------------------------------------------------------------
--  5) EXTERNO: cédula + nombre + contacto + de dónde viene
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_registrar_asistencia_externo(
    IN    p_token            uuid,
    IN    p_cedula           text,
    IN    p_nombre           text,
    IN    p_contacto         text,
    IN    p_procedencia      text,
    IN    p_latitud          numeric DEFAULT NULL,
    IN    p_longitud         numeric DEFAULT NULL,
    IN    p_ip               inet    DEFAULT NULL,
    IN    p_user_agent       text    DEFAULT NULL,
    INOUT p_estado           text    DEFAULT NULL,
    INOUT p_id_registro      bigint  DEFAULT NULL,
    INOUT p_distancia_metros numeric DEFAULT NULL,
    INOUT p_radio_metros     integer DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_cedula      text := btrim(COALESCE(p_cedula, ''));
    v_nombre      text := btrim(COALESCE(p_nombre, ''));
    v_procedencia text := btrim(COALESCE(p_procedencia, ''));
    v_asistente   bigint;
    c             record;
    e             record;
BEGIN
    p_id_registro := NULL;

    SELECT * INTO c FROM catedras.fn_interpretar_contacto(p_contacto);
    IF v_cedula !~ '^[0-9A-Za-z-]{4,20}$'
       OR length(v_nombre) NOT BETWEEN 1 AND 200
       OR NOT c.o_valido
       OR length(v_procedencia) NOT BETWEEN 1 AND 150 THEN
        p_estado := 'DATOS_INVALIDOS';  RETURN;
    END IF;

    SELECT * INTO e FROM catedras.fn_validar_enlace_y_ubicacion(p_token, p_latitud, p_longitud);
    p_distancia_metros := e.o_distancia;
    p_radio_metros     := e.o_radio;
    IF e.o_estado <> 'OK' THEN
        p_estado := e.o_estado;  RETURN;
    END IF;

    v_asistente := catedras.fn_persona_por_cedula(v_cedula, v_nombre, c.o_correo, c.o_celular);
    IF v_asistente IS NULL THEN
        p_estado := 'PERSONA_INACTIVA';  RETURN;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM vinculacion_asistente v
                    WHERE v.fk_asistente = v_asistente
                      AND v.fk_tipo_vinculacion = 'EXTERNO' AND v.fecha_fin IS NULL) THEN
        INSERT INTO vinculacion_asistente (fk_asistente, fk_tipo_vinculacion, fecha_ini)
        VALUES (v_asistente, 'EXTERNO', CURRENT_DATE)
        ON CONFLICT (fk_asistente, fk_tipo_vinculacion, fecha_ini) DO UPDATE SET fecha_fin = NULL;
    END IF;

    IF EXISTS (SELECT 1 FROM registro_asistencia r
                WHERE r.fk_sesion = e.o_id_sesion AND r.fk_asistente = v_asistente) THEN
        p_estado := 'YA_REGISTRADO';  RETURN;
    END IF;

    BEGIN
        INSERT INTO registro_asistencia (fk_sesion, fk_asistente, fk_enlace, fk_clave,
                                         fk_vinculacion_snapshot, procedencia,
                                         origen, ip, user_agent,
                                         latitud, longitud, distancia_metros, identificado_por)
        VALUES (e.o_id_sesion, v_asistente, e.o_id_enlace, NULL,
                'EXTERNO', v_procedencia,
                e.o_canal, p_ip, left(p_user_agent, 300),
                e.o_latitud, e.o_longitud, e.o_distancia, 'DECLARADO')
        RETURNING id_registro INTO p_id_registro;
    EXCEPTION WHEN unique_violation THEN
        p_id_registro := NULL;
        p_estado := 'YA_REGISTRADO';  RETURN;
    END;

    p_estado := 'OK';
END;
$$;

COMMENT ON PROCEDURE catedras.sp_registrar_asistencia_estudiante IS
    'v7.2 D64. Registra la asistencia de un estudiante por carnet. Devuelve p_estado (OK u otro) y, si OK, p_id_registro. No lanza excepción para los casos de negocio.';
COMMENT ON PROCEDURE catedras.sp_registrar_asistencia_docente IS
    'v7.2 D64. Registra la asistencia de un docente (cédula, nombre, contacto, facultad). Crea o reutiliza la persona por cédula.';
COMMENT ON PROCEDURE catedras.sp_registrar_asistencia_externo IS
    'v7.2 D64. Registra la asistencia de un externo (cédula, nombre, contacto, procedencia). Crea o reutiliza la persona por cédula.';
