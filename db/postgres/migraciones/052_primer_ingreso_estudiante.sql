-- ==========================================================================
--  db/postgres/migraciones/052_primer_ingreso_estudiante.sql
--  Proyecto Cátedras · SUBVERSIÓN v7.1 · D54 (cuentas de estudiante y primer ingreso)
-- --------------------------------------------------------------------------
--  Reglas nuevas para los ESTUDIANTES:
--    · El usuario es su CORREO INSTITUCIONAL. La columna usuario.usuario sigue
--      siendo un nombre corto (su CHECK no admite '@'); el login acepta una de
--      dos formas: ese nombre, o el correo institucional de la persona.
--    · La clave inicial es su CÓDIGO de estudiante (asistente.id_asis).
--    · El PRIMER ingreso obliga a cambiarla: usuario.debe_cambiar_clave.
--
--  Las cuentas NO se crean todas de antemano (serían miles de hashes BCrypt):
--  se crean en el PRIMER ingreso. Si alguien entra con el correo institucional de
--  un estudiante vigente (vinculación ESTUDIANTE sin fecha de fin) que aún no tiene
--  cuenta, y su clave es exactamente su código, la API calcula el hash (BCrypt solo vive
--  en ServicioUsuario) y estos procedimientos crean la cuenta con el rol ESTUDIANTE y
--  debe_cambiar_clave = true. Cualquier otro intento sigue dando el mismo rechazo (D27).
--
--    sp_buscar_estudiante_sin_cuenta   candidato (id_asistente, id_asis) por correo, o NULL
--    sp_provisionar_cuenta_estudiante  crea usuario + rol ESTUDIANTE con el hash que le llega
--    sp_fijar_debe_cambiar_clave       el administrador/servicio marca o desmarca la exigencia
--    trg_usuario_clave_cambiada        al cambiar la clave (cambiar, recuperar, PUT) la
--                                      exigencia se apaga sola
--
--  Y la cuenta semilla (048) pasa a esta regla mientras no haya ingresado nunca.
--  Idempotente. Si conservas el volumen de PostgreSQL se aplica a mano.
-- ==========================================================================

SET search_path TO catedras, public;

ALTER TABLE catedras.usuario
    ADD COLUMN IF NOT EXISTS debe_cambiar_clave boolean NOT NULL DEFAULT false;

COMMENT ON COLUMN catedras.usuario.debe_cambiar_clave IS
    'v7.1. true = en el próximo ingreso debe cambiar su clave (clave inicial = código de estudiante). Se apaga al cambiarla.';

-- --------------------------------------------------------------------------
--  1) Cambiar la clave apaga la exigencia (salvo que la misma sentencia la fije).
-- --------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION catedras.fn_trg_usuario_clave_cambiada() RETURNS trigger
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    IF NEW.clave_hash IS DISTINCT FROM OLD.clave_hash
       AND NEW.debe_cambiar_clave IS NOT DISTINCT FROM OLD.debe_cambiar_clave THEN
        NEW.debe_cambiar_clave := false;
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_usuario_clave_cambiada ON catedras.usuario;
CREATE TRIGGER trg_usuario_clave_cambiada BEFORE UPDATE OF clave_hash ON catedras.usuario
    FOR EACH ROW EXECUTE FUNCTION catedras.fn_trg_usuario_clave_cambiada();

-- --------------------------------------------------------------------------
--  2) Listar / consultar / consultar por login (misma firma que la 033) con la
--     exigencia en el JSON. El login acepta el nombre de usuario O el correo
--     institucional de la persona (citext: sin importar mayúsculas).
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_listar_usuario(
    IN    p_limite    integer DEFAULT NULL,
    INOUT p_resultado json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(u), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_usuario, fk_asistente, usuario, activo, debe_cambiar_clave,
                   ultimo_acceso, creado_en
              FROM usuario
             WHERE activo
             ORDER BY id_usuario
             LIMIT p_limite) u;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_consultar_usuario(
    IN    p_id_usuario bigint,
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(u) INTO p_resultado
      FROM (SELECT id_usuario, fk_asistente, usuario, activo, debe_cambiar_clave,
                   ultimo_acceso, creado_en
              FROM usuario
             WHERE id_usuario = p_id_usuario) u;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_consultar_usuario_por_login(
    IN    p_usuario   text,
    INOUT p_resultado json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(u) INTO p_resultado
      FROM (SELECT x.id_usuario, x.fk_asistente, x.usuario, x.clave_hash, x.activo, x.debe_cambiar_clave,
                   x.ultimo_acceso, x.creado_en
              FROM usuario x
              LEFT JOIN asistente a ON a.id_asistente = x.fk_asistente
             WHERE x.usuario = p_usuario
                OR (position('@' in p_usuario) > 0 AND a.correo_institucional = p_usuario::citext)
             ORDER BY (x.usuario = p_usuario) DESC, x.id_usuario
             LIMIT 1) u;
END;
$$;

-- --------------------------------------------------------------------------
--  3) Primer ingreso de un estudiante sin cuenta.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_buscar_estudiante_sin_cuenta(
    IN    p_correo    text,
    INOUT p_resultado json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(r) INTO p_resultado
      FROM (SELECT a.id_asistente, a.id_asis
              FROM asistente a
             WHERE a.correo_institucional = btrim(p_correo)::citext
               AND a.activo
               AND a.id_asis IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM usuario u WHERE u.fk_asistente = a.id_asistente)
               AND EXISTS (SELECT 1 FROM vinculacion_asistente v
                            WHERE v.fk_asistente = a.id_asistente
                              AND v.fk_tipo_vinculacion = 'ESTUDIANTE'
                              AND v.fecha_fin IS NULL)
             ORDER BY a.id_asistente
             LIMIT 1) r;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_provisionar_cuenta_estudiante(
    IN    p_correo     text,
    IN    p_clave_hash character varying(255),
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_asistente bigint;
    v_base      text;
    v_nombre    text;
    v_n         integer := 1;
    v_id        bigint;
BEGIN
    -- La misma condición del buscador: el rechazo de "no corresponde" es NULL, sin excepción.
    SELECT a.id_asistente INTO v_asistente
      FROM asistente a
     WHERE a.correo_institucional = btrim(p_correo)::citext
       AND a.activo AND a.id_asis IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM usuario u WHERE u.fk_asistente = a.id_asistente)
       AND EXISTS (SELECT 1 FROM vinculacion_asistente v
                    WHERE v.fk_asistente = a.id_asistente AND v.fk_tipo_vinculacion = 'ESTUDIANTE' AND v.fecha_fin IS NULL)
     ORDER BY a.id_asistente
     LIMIT 1;

    IF v_asistente IS NULL THEN
        p_resultado := NULL;
        RETURN;
    END IF;

    -- Nombre corto = lo que va antes de la @, ajustado al CHECK ^[a-z0-9._-]{4,40}$ (y sin repetirse).
    v_base := regexp_replace(lower(split_part(btrim(p_correo), '@', 1)), '[^a-z0-9._-]', '.', 'g');
    -- OJO: rpad TRUNCA lo que ya es más largo que el largo pedido, por eso solo se rellena si es menor de 4.
    IF length(v_base) < 4 THEN
        v_base := rpad(v_base, 4, '0');
    END IF;
    v_base := left(v_base, 36);
    v_nombre := v_base;
    WHILE EXISTS (SELECT 1 FROM usuario WHERE usuario = v_nombre) LOOP
        v_n := v_n + 1;
        v_nombre := v_base || v_n::text;
    END LOOP;

    INSERT INTO usuario (fk_asistente, usuario, clave_hash, activo, debe_cambiar_clave)
    VALUES (v_asistente, v_nombre, p_clave_hash, true, true)
    RETURNING id_usuario INTO v_id;

    INSERT INTO rol_por_usuario (fk_usuario, fk_rol, fecha_ini)
    VALUES (v_id, 'ESTUDIANTE', current_date)
    ON CONFLICT DO NOTHING;

    SELECT row_to_json(u) INTO p_resultado
      FROM (SELECT id_usuario, fk_asistente, usuario, activo, debe_cambiar_clave, ultimo_acceso, creado_en
              FROM usuario WHERE id_usuario = v_id) u;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_fijar_debe_cambiar_clave(
    IN p_id_usuario bigint,
    IN p_valor      boolean
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE usuario SET debe_cambiar_clave = coalesce(p_valor, false) WHERE id_usuario = p_id_usuario;
END;
$$;

-- --------------------------------------------------------------------------
--  4) La cuenta semilla (048) pasa a la regla nueva si nunca ha ingresado:
--     su clave inicial es su código de estudiante y debe cambiarla al entrar.
--     (048 sigue sembrando 'Cambiar-123'; esto la reemplaza al aplicar la 052.)
-- --------------------------------------------------------------------------
UPDATE catedras.usuario u
   SET clave_hash = crypt(a.id_asis, gen_salt('bf', 10)),
       debe_cambiar_clave = true
  FROM catedras.asistente a
 WHERE a.id_asistente = u.fk_asistente
   AND u.usuario = 'alejandro.zuleta'
   AND u.ultimo_acceso IS NULL
   AND a.id_asis IS NOT NULL
   AND NOT u.debe_cambiar_clave;

COMMENT ON PROCEDURE catedras.sp_buscar_estudiante_sin_cuenta IS
    'v7.1. Estudiante vigente con ese correo institucional y sin cuenta (id_asistente, id_asis), o NULL.';
COMMENT ON PROCEDURE catedras.sp_provisionar_cuenta_estudiante IS
    'v7.1. Crea la cuenta (nombre corto del correo) con rol ESTUDIANTE y debe_cambiar_clave = true. El hash lo calcula la API.';
COMMENT ON PROCEDURE catedras.sp_fijar_debe_cambiar_clave IS
    'v7.1. Marca o desmarca la exigencia de cambiar la clave en el próximo ingreso.';
