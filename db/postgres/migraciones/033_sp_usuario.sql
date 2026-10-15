-- ==========================================================================
--  db/postgres/migraciones/033_sp_usuario.sql
--  Proyecto Cátedras · RETROFIT criterio 3 (VERSION_2.md §4) · Fase 5
--  Molde: 005_sp_sede_ejemplo.sql (ver PLAN_RETROFIT_SP.md).
--
--  'usuario' SÍ tiene 'activo' -> 'sp_eliminar_usuario' es LÓGICO.
--  (Antes era un DELETE físico con CASCADE a rol_por_usuario; ahora la
--  cuenta queda inactiva y sus roles se conservan como historial. El login
--  ya rechaza cuentas inactivas en ServicioUsuario, ADR D27.)
--
--  ⚠️ CLAVE (ADR D23, RNF13, RF138):
--   · Ningún procedimiento de este archivo calcula ni decide un hash. El
--     hash BCrypt lo calcula ServicioUsuario ANTES de llamar al
--     repositorio; aquí solo se guarda la cadena que llega.
--   · 'clave_hash' NUNCA sale en el 'p_resultado' de listar / consultar /
--     crear / actualizar (esos JSON pueden terminar sirviendo una
--     respuesta). La única excepción es 'sp_consultar_usuario_por_login',
--     de uso INTERNO para autenticar: el servicio necesita el hash para
--     BCrypt.Verify, y el modelo C# lo marca [JsonIgnore] para que nunca
--     se serialice hacia afuera.
--
--  AJUSTES AL MOLDE DE 'sede' (ver AJUSTES_RETROFIT_SP_LOTE_FASE5.md):
--   1) Columnas de proyección SIN 'clave_hash' (salvo el login, arriba).
--   2) 'sp_actualizar_usuario' recibe TODAS las columnas, pero
--      p_clave_hash NULL = "conservar el hash guardado". 'clave_hash' es
--      NOT NULL en la tabla, así que NULL nunca es un valor válido y no
--      hay ambigüedad. Hace falta porque el repositorio ya no puede
--      "mezclar" el hash actual (sp_consultar_usuario no lo devuelve), y
--      no queremos que el hash viaje a C# solo para devolverlo igual.
--   3) Dos procedimientos extra, porque IRepositorioUsuario tiene dos
--      métodos fuera de los 5 verbos: ObtenerPorLoginAsync y
--      ActualizarUltimoAccesoAsync. Así el repositorio no conserva NINGÚN
--      SELECT/UPDATE a mano.
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  LISTAR — solo activos (igual que sede), límite opcional. Sin clave_hash.
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
      FROM (SELECT id_usuario, fk_asistente, usuario, activo,
                   ultimo_acceso, creado_en
              FROM usuario
             WHERE activo
             ORDER BY id_usuario
             LIMIT p_limite) u;
END;
$$;

-- --------------------------------------------------------------------------
--  CONSULTAR — por id (activo o no, igual que sede). Sin clave_hash.
--  NULL en p_resultado = no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_consultar_usuario(
    IN    p_id_usuario bigint,
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(u) INTO p_resultado
      FROM (SELECT id_usuario, fk_asistente, usuario, activo,
                   ultimo_acceso, creado_en
              FROM usuario
             WHERE id_usuario = p_id_usuario) u;
END;
$$;

-- --------------------------------------------------------------------------
--  CONSULTAR POR LOGIN — USO INTERNO (autenticación). ÚNICO procedimiento
--  que devuelve 'clave_hash'. uq_usuario_login garantiza 0 o 1 fila.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_consultar_usuario_por_login(
    IN    p_usuario   character varying(40),
    INOUT p_resultado json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(u) INTO p_resultado
      FROM (SELECT id_usuario, fk_asistente, usuario, clave_hash, activo,
                   ultimo_acceso, creado_en
              FROM usuario
             WHERE usuario = p_usuario) u;
END;
$$;

-- --------------------------------------------------------------------------
--  CREAR — recibe el hash YA calculado. id_usuario y creado_en los pone la
--  BD; se devuelven en la fila (sin clave_hash) para que el repositorio
--  conserve su contrato (IdUsuario, CreadoEn).
--  usuario repetido (uq_usuario_login), persona con cuenta
--  (uq_usuario_persona) o asistente inexistente -> NpgsqlException.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_crear_usuario(
    IN    p_fk_asistente bigint,
    IN    p_usuario      character varying(40),
    IN    p_clave_hash   character varying(255),
    IN    p_activo       boolean DEFAULT true,
    INOUT p_resultado    json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_id bigint;
BEGIN
    INSERT INTO usuario (fk_asistente, usuario, clave_hash, activo)
    VALUES (p_fk_asistente, p_usuario, p_clave_hash, COALESCE(p_activo, true))
    RETURNING id_usuario INTO v_id;

    SELECT row_to_json(u) INTO p_resultado
      FROM (SELECT id_usuario, fk_asistente, usuario, activo,
                   ultimo_acceso, creado_en
              FROM usuario
             WHERE id_usuario = v_id) u;
END;
$$;

-- --------------------------------------------------------------------------
--  ACTUALIZAR — reemplazo completo de las columnas de negocio
--  (fk_asistente, usuario, clave_hash, activo). NO se tocan: id_usuario
--  (PK), creado_en (BD) ni ultimo_acceso (tiene su propio procedimiento).
--  p_clave_hash NULL = conservar el hash actual (ver ajuste 2 arriba).
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_usuario(
    IN    p_id_usuario   bigint,
    IN    p_fk_asistente bigint,
    IN    p_usuario      character varying(40),
    IN    p_clave_hash   character varying(255),
    IN    p_activo       boolean,
    INOUT p_resultado    json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE usuario
       SET fk_asistente = p_fk_asistente,
           usuario      = p_usuario,
           clave_hash   = COALESCE(p_clave_hash, clave_hash),
           activo       = p_activo
     WHERE id_usuario = p_id_usuario;

    IF FOUND THEN
        SELECT row_to_json(u) INTO p_resultado
          FROM (SELECT id_usuario, fk_asistente, usuario, activo,
                       ultimo_acceso, creado_en
                  FROM usuario
                 WHERE id_usuario = p_id_usuario) u;
    END IF;
END;
$$;

-- --------------------------------------------------------------------------
--  REGISTRAR ACCESO — 'ultimo_acceso = now()' tras un login correcto.
--  now() lo evalúa PostgreSQL: la hora es la del servidor de la BD.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_registrar_acceso_usuario(
    IN    p_id_usuario bigint,
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE usuario
       SET ultimo_acceso = now()
     WHERE id_usuario = p_id_usuario;

    p_resultado := json_build_object('registrado', FOUND);
END;
$$;

-- --------------------------------------------------------------------------
--  ELIMINAR — LÓGICO (la tabla tiene 'activo'). Ya inactivo = no cuenta.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_usuario(
    IN    p_id_usuario bigint,
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE usuario
       SET activo = false
     WHERE id_usuario = p_id_usuario
       AND activo;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_usuario IS
    'Retrofit criterio 3. Solo activos, límite opcional. Nunca incluye clave_hash.';
COMMENT ON PROCEDURE catedras.sp_consultar_usuario IS
    'Retrofit criterio 3. NULL = no existe. Nunca incluye clave_hash.';
COMMENT ON PROCEDURE catedras.sp_consultar_usuario_por_login IS
    'Retrofit criterio 3. USO INTERNO (login): único SP que devuelve clave_hash.';
COMMENT ON PROCEDURE catedras.sp_crear_usuario IS
    'Retrofit criterio 3. Recibe el hash ya calculado por ServicioUsuario (BCrypt).';
COMMENT ON PROCEDURE catedras.sp_actualizar_usuario IS
    'Retrofit criterio 3. Reemplazo completo; p_clave_hash NULL = conservar el hash.';
COMMENT ON PROCEDURE catedras.sp_registrar_acceso_usuario IS
    'Retrofit criterio 3. ultimo_acceso = now() tras un login correcto.';
COMMENT ON PROCEDURE catedras.sp_eliminar_usuario IS
    'Retrofit criterio 3. Borrado LÓGICO (activo=false).';
