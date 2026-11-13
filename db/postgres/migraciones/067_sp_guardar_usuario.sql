-- ==========================================================================
--  db/postgres/migraciones/067_sp_guardar_usuario.sql
--  Proyecto Cátedras · Guía IA, Regla 1 (maestro-detalle) · "crear usuario"
-- --------------------------------------------------------------------------
--  Maestro: 'usuario' (con la persona que es su dueña). Detalle: 'rol_por_usuario'.
--
--  CREAR UN USUARIO YA NO ES BUSCAR A UNA PERSONA: el administrador escribe los
--  datos de la persona, los de la cuenta y marca uno o varios roles (ESTUDIANTE,
--  PONENTE, ADMIN...). Este SP guarda las tres cosas en UNA sola transacción:
--
--     1) asistente          la persona (es_externo = true: no viene del ASIS, así
--                           que no lleva id_asis; chk_asistente_externo lo permite)
--     2) usuario            la cuenta, con el hash BCrypt que calculó ServicioUsuario
--     3) rol_por_usuario    una fila por rol del arreglo p_roles (nacen vigentes)
--
--  Si CUALQUIER paso falla (login repetido, correo repetido, rol inexistente,
--  rol repetido...), PostgreSQL revierte TODO el CALL: ni la persona ni la cuenta
--  quedan a medias.
--
--  p_roles  [{"fk_rol":"ESTUDIANTE","fecha_ini":"2026-01-20"}]   (fecha_ini opcional = hoy)
--
--  Idempotente (CREATE OR REPLACE). Si conservas el volumen de PostgreSQL hay que
--  aplicarla a mano con psql.
-- ==========================================================================

SET search_path TO catedras, public;

CREATE OR REPLACE PROCEDURE catedras.sp_guardar_usuario(
    -- La persona (maestro, parte 1).
    IN    p_nombres             character varying(100),
    IN    p_apellidos           character varying(100),
    IN    p_correo_inst         character varying(254),
    IN    p_correo_pers         character varying(254),
    IN    p_celular             character varying(20),
    -- La cuenta (maestro, parte 2). La clave llega YA hasheada (ADR D23).
    IN    p_usuario             character varying(40),
    IN    p_clave_hash          character varying(255),
    IN    p_activo              boolean DEFAULT true,
    IN    p_debe_cambiar_clave  boolean DEFAULT false,
    -- Detalle: los roles.
    IN    p_roles               jsonb   DEFAULT NULL,
    INOUT p_resultado           json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_asistente bigint;
    v_usuario   bigint;
BEGIN
    -- 1) La persona. Correo repetido o sin ningún correo -> excepción del motor (500).
    INSERT INTO asistente (nombres, apellidos, correo_institucional, correo_personal, celular, es_externo, activo)
    VALUES (p_nombres, p_apellidos, p_correo_inst, p_correo_pers, p_celular, true, true)
    RETURNING id_asistente INTO v_asistente;

    -- 2) La cuenta. Login repetido (uq_usuario_login) -> excepción -> se deshace la persona.
    INSERT INTO usuario (fk_asistente, usuario, clave_hash, activo, debe_cambiar_clave)
    VALUES (v_asistente, p_usuario, p_clave_hash, COALESCE(p_activo, true), COALESCE(p_debe_cambiar_clave, false))
    RETURNING id_usuario INTO v_usuario;

    -- 3) Los roles. 'fk_usuario' NO viene en el JSON: es la cuenta recién creada.
    IF p_roles IS NOT NULL AND jsonb_array_length(p_roles) > 0 THEN
        INSERT INTO rol_por_usuario (fk_usuario, fk_rol, fecha_ini)
        SELECT v_usuario,
               e ->> 'fk_rol',
               COALESCE((e ->> 'fecha_ini')::date, CURRENT_DATE)
          FROM jsonb_array_elements(p_roles) AS e;
    END IF;

    SELECT row_to_json(u) INTO p_resultado
      FROM (SELECT id_usuario, fk_asistente, usuario, activo, debe_cambiar_clave, ultimo_acceso, creado_en
              FROM usuario
             WHERE id_usuario = v_usuario) u;
END;
$$;

COMMENT ON PROCEDURE catedras.sp_guardar_usuario IS
    'Guía IA · Regla 1. Crea la persona (asistente), su usuario y sus roles en una sola transacción. '
    'La clave llega ya hasheada por ServicioUsuario. Devuelve el usuario creado (sin clave_hash).';

RESET search_path;
