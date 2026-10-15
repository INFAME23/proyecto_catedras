-- ==========================================================================
--  db/postgres/migraciones/025_sp_documento_asistente.sql
--  Proyecto Cátedras · RETROFIT criterio 3 (PLAN_RETROFIT_SP.md, Fase 4)
--  Tabla: 'documento_asistente'. Molde: 005_sp_sede_ejemplo.sql.
-- --------------------------------------------------------------------------
--  AJUSTES AL MOLDE DE 'sede':
--   1. 'sp_listar_' recibe el filtro opcional 'p_fk_asistente' (contrato v3:
--      GET /api/DocumentoAsistente?fkAsistente=). Sin filtro = todos.
--      Sin "WHERE activo": la tabla no tiene esa columna.
--   2. La clave la genera la BD (IDENTITY), no el cliente: 'sp_crear_'
--      guarda el id en 'v_id' (RETURNING ... INTO) para devolver la fila.
--   3. 'sp_eliminar_' es DELETE FÍSICO: la tabla NO tiene 'activo'.
--      'vigente' NO es un 'activo' disfrazado: es "cuál de sus documentos
--      es el actual" (índice único parcial ux_documento_vigente), y se
--      cambia con PATCH {"vigente": false}, como hasta hoy.
--   4. 'sp_actualizar_' no recibe 'fk_asistente': un documento no cambia
--      de dueño (misma lista blanca que tenía el repositorio en la v3).
-- ==========================================================================

SET search_path TO catedras, public;

CREATE OR REPLACE PROCEDURE catedras.sp_listar_documento_asistente(
    IN    p_fk_asistente bigint  DEFAULT NULL,
    IN    p_limite       integer DEFAULT NULL,
    INOUT p_resultado    json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(d), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_documento, fk_asistente, fk_tipo_documento, numero, vigente, registrado_en
              FROM documento_asistente
             WHERE p_fk_asistente IS NULL OR fk_asistente = p_fk_asistente
             ORDER BY id_documento
             LIMIT p_limite) d;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_consultar_documento_asistente(
    IN    p_id_documento bigint,
    INOUT p_resultado    json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(d) INTO p_resultado
      FROM (SELECT id_documento, fk_asistente, fk_tipo_documento, numero, vigente, registrado_en
              FROM documento_asistente
             WHERE id_documento = p_id_documento) d;
END;
$$;

-- CREAR — devuelve la fila con 'id_documento' y 'registrado_en' que puso la
-- BD. Segundo vigente (ux_documento_vigente), número repetido por tipo
-- (uq_documento_numero) o FK inexistente -> excepción del motor (500).
CREATE OR REPLACE PROCEDURE catedras.sp_crear_documento_asistente(
    IN    p_fk_asistente      bigint,
    IN    p_fk_tipo_documento character varying(5),
    IN    p_numero            character varying(20),
    IN    p_vigente           boolean DEFAULT true,
    INOUT p_resultado         json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_id bigint;
BEGIN
    INSERT INTO documento_asistente (fk_asistente, fk_tipo_documento, numero, vigente)
    VALUES (p_fk_asistente, p_fk_tipo_documento, p_numero, COALESCE(p_vigente, true))
    RETURNING id_documento INTO v_id;

    CALL catedras.sp_consultar_documento_asistente(v_id, p_resultado);
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_documento_asistente(
    IN    p_id_documento      bigint,
    IN    p_fk_tipo_documento character varying(5),
    IN    p_numero            character varying(20),
    IN    p_vigente           boolean,
    INOUT p_resultado         json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE documento_asistente
       SET fk_tipo_documento = p_fk_tipo_documento,
           numero            = p_numero,
           vigente           = p_vigente
     WHERE id_documento = p_id_documento;

    IF FOUND THEN
        CALL catedras.sp_consultar_documento_asistente(p_id_documento, p_resultado);
    END IF;
END;
$$;

-- ELIMINAR — FÍSICO (sin 'activo'). Ninguna tabla referencia a
-- documento_asistente, así que nunca lo bloquea una FK (RF105).
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_documento_asistente(
    IN    p_id_documento bigint,
    INOUT p_resultado    json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    DELETE FROM documento_asistente WHERE id_documento = p_id_documento;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_documento_asistente IS
    'Retrofit criterio 3. Filtro opcional por asistente; sin columna activo.';
COMMENT ON PROCEDURE catedras.sp_consultar_documento_asistente IS
    'Retrofit criterio 3. NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_documento_asistente IS
    'Retrofit criterio 3. Devuelve la fila con el id y la fecha que puso la BD.';
COMMENT ON PROCEDURE catedras.sp_actualizar_documento_asistente IS
    'Retrofit criterio 3. Reemplazo completo (sin fk_asistente); PATCH mezcla antes de llamar.';
COMMENT ON PROCEDURE catedras.sp_eliminar_documento_asistente IS
    'Retrofit criterio 3. Borrado FÍSICO: la tabla no tiene activo.';
