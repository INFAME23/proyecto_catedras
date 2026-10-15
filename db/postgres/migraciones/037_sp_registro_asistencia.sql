-- ==========================================================================
--  db/postgres/migraciones/037_sp_registro_asistencia.sql
--  Proyecto Cátedras · RETROFIT criterio 3 (VERSION_2.md §4) · Fase 5
--  Molde: 005_sp_sede_ejemplo.sql (ver PLAN_RETROFIT_SP.md).
--
--  'registro_asistencia' NO tiene 'activo' -> 'sp_eliminar_' es FÍSICO.
--
--  ⚠️ ES UN HECHO (comentario de la tabla: "Hecho de que un asistente
--  quedó registrado en una sesión, en un instante concreto, con el
--  programa y la vinculación que tenía en ese momento"). Por eso:
--   · Crear, Listar y Consultar: los usa la API (igual que antes).
--   · Actualizar y Eliminar: EXISTEN para cumplir el criterio al pie de la
--     letra, pero NO están en IRepositorioRegistroAsistencia ni en el
--     controlador, y traen GUARDAS para que ni siquiera un CALL a mano
--     rompa el hecho:
--       - sp_actualizar_: recibe todas las columnas (molde), pero rechaza
--         cualquier cambio en las columnas del HECHO (fk_sesion,
--         fk_asistente, registrado_en y las dos instantáneas, que "NUNCA
--         se actualizan"). Solo deja corregir trazabilidad: fk_enlace,
--         fk_clave, origen, ip, user_agent. Además, trg_registro_validar
--         es BEFORE INSERT: un UPDATE libre se saltaría la ventana, el
--         cupo y RN-07/RN-22.
--       - sp_eliminar_: rechaza el borrado si el registro ya tiene una
--         respuesta de encuesta (fk_respenc_registro es CASCADE: borraría
--         en silencio una encuesta cerrada, ADR D31). Si ya se exportó al
--         ASIS, lo frena fk_detmig_registro (RESTRICT). trg_bitacora_registro
--         deja la fila borrada en 'bitacora'.
--
--  AJUSTES AL MOLDE DE 'sede':
--   1) 'ip' es inet: host(ip) en la proyección.
--   2) Crear manda SOLO fk_sesion, fk_asistente, origen; el trigger
--      trg_registro_validar pone las instantáneas. El SP devuelve la fila
--      DESPUÉS del INSERT (ya con las instantáneas), como hacía el
--      RETURNING de C#.
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  LISTAR — filtros opcionales por sesión y por asistente.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_listar_registro_asistencia(
    IN    p_fk_sesion    bigint  DEFAULT NULL,
    IN    p_fk_asistente bigint  DEFAULT NULL,
    IN    p_limite       integer DEFAULT NULL,
    INOUT p_resultado    json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(r), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_registro, fk_sesion, fk_asistente, fk_enlace, fk_clave,
                   registrado_en, fk_programa_snapshot, fk_vinculacion_snapshot,
                   origen, host(ip) AS ip, user_agent
              FROM registro_asistencia
             WHERE (p_fk_sesion    IS NULL OR fk_sesion    = p_fk_sesion)
               AND (p_fk_asistente IS NULL OR fk_asistente = p_fk_asistente)
             ORDER BY id_registro
             LIMIT p_limite) r;
END;
$$;

-- --------------------------------------------------------------------------
--  CONSULTAR — por id. NULL = no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_consultar_registro_asistencia(
    IN    p_id_registro bigint,
    INOUT p_resultado   json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(r) INTO p_resultado
      FROM (SELECT id_registro, fk_sesion, fk_asistente, fk_enlace, fk_clave,
                   registrado_en, fk_programa_snapshot, fk_vinculacion_snapshot,
                   origen, host(ip) AS ip, user_agent
              FROM registro_asistencia
             WHERE id_registro = p_id_registro) r;
END;
$$;

-- --------------------------------------------------------------------------
--  CREAR — el trigger calcula las instantáneas y valida ventana, cupo,
--  vinculación (RN-15, RN-22, RN-07, RN-38). Si algo no cumple -> excepción.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_crear_registro_asistencia(
    IN    p_fk_sesion    bigint,
    IN    p_fk_asistente bigint,
    IN    p_origen       character varying(15) DEFAULT 'QR',
    INOUT p_resultado    json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_id bigint;
BEGIN
    INSERT INTO registro_asistencia (fk_sesion, fk_asistente, origen)
    VALUES (p_fk_sesion, p_fk_asistente, COALESCE(p_origen, 'QR'))
    RETURNING id_registro INTO v_id;

    SELECT row_to_json(r) INTO p_resultado
      FROM (SELECT id_registro, fk_sesion, fk_asistente, fk_enlace, fk_clave,
                   registrado_en, fk_programa_snapshot, fk_vinculacion_snapshot,
                   origen, host(ip) AS ip, user_agent
              FROM registro_asistencia
             WHERE id_registro = v_id) r;
END;
$$;

-- --------------------------------------------------------------------------
--  ACTUALIZAR — firma completa (molde), pero el HECHO es inmutable.
--  NO expuesto por la API.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_registro_asistencia(
    IN    p_id_registro             bigint,
    IN    p_fk_sesion               bigint,
    IN    p_fk_asistente            bigint,
    IN    p_fk_enlace               bigint,
    IN    p_fk_clave                bigint,
    IN    p_registrado_en           timestamp with time zone,
    IN    p_fk_programa_snapshot    character varying(5),
    IN    p_fk_vinculacion_snapshot character varying(15),
    IN    p_origen                  character varying(15),
    IN    p_ip                      inet,
    IN    p_user_agent              character varying(300),
    INOUT p_resultado               json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_actual registro_asistencia%ROWTYPE;
BEGIN
    SELECT * INTO v_actual
      FROM registro_asistencia
     WHERE id_registro = p_id_registro
       FOR UPDATE;

    IF NOT FOUND THEN
        RETURN;   -- p_resultado NULL = no existe
    END IF;

    IF v_actual.fk_sesion               IS DISTINCT FROM p_fk_sesion
    OR v_actual.fk_asistente            IS DISTINCT FROM p_fk_asistente
    OR v_actual.registrado_en           IS DISTINCT FROM p_registrado_en
    OR v_actual.fk_programa_snapshot    IS DISTINCT FROM p_fk_programa_snapshot
    OR v_actual.fk_vinculacion_snapshot IS DISTINCT FROM p_fk_vinculacion_snapshot
    THEN
        RAISE EXCEPTION
            'El registro % es un hecho de asistencia: sesión, asistente, instante e instantáneas no se modifican',
            p_id_registro USING ERRCODE = 'check_violation';
    END IF;

    UPDATE registro_asistencia
       SET fk_enlace  = p_fk_enlace,
           fk_clave   = p_fk_clave,
           origen     = p_origen,
           ip         = p_ip,
           user_agent = p_user_agent
     WHERE id_registro = p_id_registro;

    SELECT row_to_json(r) INTO p_resultado
      FROM (SELECT id_registro, fk_sesion, fk_asistente, fk_enlace, fk_clave,
                   registrado_en, fk_programa_snapshot, fk_vinculacion_snapshot,
                   origen, host(ip) AS ip, user_agent
              FROM registro_asistencia
             WHERE id_registro = p_id_registro) r;
END;
$$;

-- --------------------------------------------------------------------------
--  ELIMINAR — FÍSICO (sin 'activo'), con guarda. NO expuesto por la API.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_registro_asistencia(
    IN    p_id_registro bigint,
    INOUT p_resultado   json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    IF EXISTS (SELECT 1 FROM respuesta_encuesta WHERE fk_registro = p_id_registro) THEN
        RAISE EXCEPTION
            'El registro % ya tiene una respuesta de encuesta (hecho auditable, ADR D31): no se elimina',
            p_id_registro USING ERRCODE = 'restrict_violation';
    END IF;

    DELETE FROM registro_asistencia WHERE id_registro = p_id_registro;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_registro_asistencia IS
    'Retrofit criterio 3. Filtros opcionales por sesión y asistente.';
COMMENT ON PROCEDURE catedras.sp_consultar_registro_asistencia IS
    'Retrofit criterio 3. NULL = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_registro_asistencia IS
    'Retrofit criterio 3. El trigger pone las instantáneas; devuelve la fila final.';
COMMENT ON PROCEDURE catedras.sp_actualizar_registro_asistencia IS
    'Retrofit criterio 3. NO expuesto. El hecho es inmutable: solo corrige trazabilidad.';
COMMENT ON PROCEDURE catedras.sp_eliminar_registro_asistencia IS
    'Retrofit criterio 3. NO expuesto. FÍSICO; se niega si ya hay respuesta de encuesta.';
