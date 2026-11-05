-- ==========================================================================
--  db/postgres/migraciones/047_codigo_recuperacion.sql
--  Proyecto Cátedras · SUBVERSIÓN v7.1 · RF-S5 / RF-S6 (recuperar la clave)
-- --------------------------------------------------------------------------
--  FLUJO "OLVIDÉ MI CLAVE" (el de "cambiar la clave sabiendo la actual" no
--  usa nada de esto: la API verifica la clave actual con BCrypt).
--
--    1) sp_solicitar_recuperacion(identificador, ip)
--         identificador = nombre de usuario O correo de la persona.
--         Genera un código de un solo uso, guarda SOLO su hash y devuelve el
--         código EN CLARO para que el front lo envíe por correo (RNF12: es la
--         única vez que existe). Si no hay una cuenta activa devuelve todo en
--         NULL y la API responde igual que si la hubiera (no se revela qué
--         usuarios existen, ADR D27).
--    2) sp_restablecer_clave(id_codigo, codigo, clave_hash)
--         Valida el código (no usado, no vencido, intentos) y cambia
--         usuario.clave_hash. El hash BCrypt lo calcula ServicioRecuperacionClave
--         ANTES de llamar (Guía IA, Regla 3): aquí llega como cadena opaca.
--
--  ¿POR QUÉ UNA TABLA NUEVA Y NO 'clave_acceso'? clave_acceso es del
--  autorregistro (v5): su 'fk_enlace' es NOT NULL (el código nace de un
--  enlace de una sesión) y sp_validar_clave_y_registrar inserta un
--  registro_asistencia. Un código de recuperación no tiene enlace ni sesión;
--  forzarlo ahí exigiría relajar una tabla del script provisto. Se copia el
--  PATRÓN (hash con sal, vigencia, intentos, usado_en) y los parámetros
--  CLAVE_VIGENCIA_MINUTOS / CLAVE_MAX_INTENTOS / CLAVE_LONGITUD, no la tabla.
--  Es una tabla de apoyo (como clave_acceso): la API no la expone como CRUD,
--  por eso el mapa sigue en 37 recursos.
--
--  Idempotente. Si conservas el volumen de PostgreSQL se aplica a mano.
-- ==========================================================================

SET search_path TO catedras, public;

CREATE TABLE IF NOT EXISTS catedras.codigo_recuperacion (
    id_codigo    bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    fk_usuario   bigint NOT NULL
                 REFERENCES catedras.usuario (id_usuario) ON DELETE CASCADE,
    codigo_hash  bytea  NOT NULL,
    enviado_a    public.citext NOT NULL,
    generado_en  timestamptz NOT NULL DEFAULT now(),
    expira_en    timestamptz NOT NULL,
    usado_en     timestamptz,
    intentos     smallint NOT NULL DEFAULT 0,
    ip_solicitud inet,
    CONSTRAINT chk_codrec_correo   CHECK (enviado_a OPERATOR(public.~~) '%@%.%'::public.citext),
    CONSTRAINT chk_codrec_expira   CHECK (expira_en > generado_en),
    CONSTRAINT chk_codrec_intentos CHECK (intentos >= 0 AND intentos <= 10)
);

CREATE INDEX IF NOT EXISTS ix_codrec_usuario ON catedras.codigo_recuperacion (fk_usuario);

COMMENT ON TABLE catedras.codigo_recuperacion IS
    'v7.1. Códigos de un solo uso para recuperar una clave olvidada. Guarda el hash (con sal), nunca el código.';

-- --------------------------------------------------------------------------
--  SOLICITAR
--  Resuelve la cuenta por nombre de usuario o por correo (institucional o
--  personal) y anula los códigos anteriores todavía vivos de esa cuenta.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_solicitar_recuperacion(
    IN    p_identificador text,
    IN    p_ip            inet DEFAULT NULL,
    INOUT p_id_codigo     bigint DEFAULT NULL,
    INOUT p_codigo        text   DEFAULT NULL,
    INOUT p_enviado_a     text   DEFAULT NULL,
    INOUT p_usuario       text   DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_id_usuario bigint;
    v_vigencia   integer;
    v_longitud   integer;
    v_azar       bytea;
BEGIN
    -- Sin cuenta activa: todo queda en NULL (la API responde igual).
    SELECT u.id_usuario, u.usuario,
           COALESCE(a.correo_institucional, a.correo_personal)::text
      INTO v_id_usuario, p_usuario, p_enviado_a
      FROM usuario u
      JOIN asistente a ON a.id_asistente = u.fk_asistente
     WHERE u.activo
       AND (u.usuario = lower(btrim(p_identificador))
            OR a.correo_institucional = btrim(p_identificador)::citext
            OR a.correo_personal      = btrim(p_identificador)::citext)
     ORDER BY (u.usuario = lower(btrim(p_identificador))) DESC, u.id_usuario
     LIMIT 1;

    IF v_id_usuario IS NULL THEN
        p_id_codigo := NULL;
        p_codigo    := NULL;
        p_enviado_a := NULL;
        p_usuario   := NULL;
        RETURN;
    END IF;

    v_vigencia := coalesce((SELECT valor::int FROM parametro WHERE clave = 'CLAVE_VIGENCIA_MINUTOS'), 10);
    v_longitud := coalesce((SELECT valor::int FROM parametro WHERE clave = 'CLAVE_LONGITUD'), 6);

    -- Un solo código vivo por cuenta: pedir otro anula el anterior.
    -- clock_timestamp() y no now(): now() es el inicio de la transacción y
    -- chk_codrec_expira exige expira_en > generado_en.
    UPDATE codigo_recuperacion
       SET expira_en = clock_timestamp()
     WHERE fk_usuario = v_id_usuario AND usado_en IS NULL AND expira_en > now();

    -- gen_random_bytes (pgcrypto) en vez de random(): es un secreto. Se
    -- toman 4 bytes (0..2^32-1) y se reducen a 'v_longitud' dígitos; el sesgo
    -- del módulo es despreciable para un código que vence en minutos.
    v_azar   := gen_random_bytes(4);
    p_codigo := lpad(
        ((get_byte(v_azar, 0)::bigint * 16777216 + get_byte(v_azar, 1)::bigint * 65536
          + get_byte(v_azar, 2)::bigint * 256 + get_byte(v_azar, 3)::bigint)
         % (10::numeric ^ v_longitud)::bigint)::text,
        v_longitud, '0');

    INSERT INTO codigo_recuperacion (fk_usuario, codigo_hash, enviado_a, expira_en, ip_solicitud)
    VALUES (v_id_usuario,
            digest(p_codigo || p_enviado_a, 'sha256'),     -- con sal, como RN-34
            p_enviado_a::citext,
            now() + make_interval(mins => v_vigencia),
            p_ip)
    RETURNING id_codigo INTO p_id_codigo;
END;
$$;

-- --------------------------------------------------------------------------
--  RESTABLECER
--  Devuelve un ESTADO en 'p_resultado' en vez de lanzar una excepción:
--    OK · NO_EXISTE · USADO · EXPIRADO · INCORRECTO · AGOTADO
--
--  ⚠️ POR QUÉ NO SE USA RAISE EXCEPTION AQUÍ (a diferencia de
--  sp_validar_clave_y_registrar, v5). Una excepción deshace TODA la
--  transacción del CALL, incluido el 'intentos + 1' que acaba de escribirse:
--  los intentos fallidos nunca se acumulan y el tope de CLAVE_MAX_INTENTOS
--  no se aplica (es el "límite conocido" RF153 de la v5). En un registro de
--  asistencia es tolerable; en una RECUPERACIÓN DE CLAVE no: con un código de
--  6 dígitos y reintentos ilimitados durante la vigencia se puede adivinar.
--  Devolviendo el estado, el CALL termina bien, el contador SE GUARDA y la
--  API traduce el estado a un 400.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_restablecer_clave(
    IN    p_id_codigo  bigint,
    IN    p_codigo     text,
    IN    p_clave_hash character varying(255),
    INOUT p_resultado  text DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    c              record;
    v_max_intentos integer;
BEGIN
    SELECT * INTO c FROM codigo_recuperacion WHERE id_codigo = p_id_codigo FOR UPDATE;

    IF c IS NULL THEN
        p_resultado := 'NO_EXISTE';
        RETURN;
    END IF;

    IF c.usado_en IS NOT NULL THEN
        p_resultado := 'USADO';
        RETURN;
    END IF;

    -- clock_timestamp(): un código anulado en esta misma transacción ya cuenta como vencido.
    IF clock_timestamp() > c.expira_en THEN
        p_resultado := 'EXPIRADO';
        RETURN;
    END IF;

    v_max_intentos := coalesce((SELECT valor::int FROM parametro WHERE clave = 'CLAVE_MAX_INTENTOS'), 3);

    IF c.codigo_hash IS DISTINCT FROM digest(p_codigo || c.enviado_a::text, 'sha256') THEN
        UPDATE codigo_recuperacion SET intentos = intentos + 1 WHERE id_codigo = p_id_codigo;
        IF c.intentos + 1 >= v_max_intentos THEN
            UPDATE codigo_recuperacion SET expira_en = clock_timestamp() WHERE id_codigo = p_id_codigo;
            p_resultado := 'AGOTADO';
        ELSE
            p_resultado := 'INCORRECTO';
        END IF;
        RETURN;
    END IF;

    UPDATE usuario SET clave_hash = p_clave_hash WHERE id_usuario = c.fk_usuario;
    UPDATE codigo_recuperacion SET usado_en = now() WHERE id_codigo = p_id_codigo;
    p_resultado := 'OK';
END;
$$;

COMMENT ON PROCEDURE catedras.sp_solicitar_recuperacion IS
    'v7.1. Genera un código de un solo uso para recuperar la clave. Todo NULL = no hay cuenta activa.';
COMMENT ON PROCEDURE catedras.sp_restablecer_clave IS
    'v7.1. Valida el código y cambia usuario.clave_hash (el hash BCrypt llega calculado). Devuelve un estado, no lanza excepción, para que los intentos fallidos SE GUARDEN.';
