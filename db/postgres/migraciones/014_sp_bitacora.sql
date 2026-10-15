-- ==========================================================================
--  db/postgres/migraciones/014_sp_bitacora.sql
--  Proyecto Cátedras · RETROFIT criterio 3 de VERSION_2.md · 'bitacora'
-- --------------------------------------------------------------------------
--  ⚠️  'bitacora' es el LOG DE AUDITORÍA (RN-35). Sus columnas se llenan
--  SOLAS: 'ocurrido_en' (DEFAULT now()) y 'usuario_bd' (DEFAULT
--  CURRENT_USER) las pone la BD, y los triggers trg_bitacora_asistente /
--  trg_bitacora_registro (fn_trg_bitacora) son quienes escriben las filas.
--
--  Para cumplir el criterio de los 5 verbos AL PIE DE LA LETRA existen los
--  5 procedimientos, PERO:
--    · LA API SÍ EXPONE Crear / Actualizar / Eliminar (POST/PUT/PATCH/
--      DELETE /api/Bitacora), a pedido del equipo para que los 5
--      procedimientos tengan llamador. RIESGO: permite reescribir o borrar
--      el log de auditoría por HTTP; conviene protegerlo con autorización.
--    · 'sp_crear_bitacora' NO recibe 'ocurrido_en' ni 'usuario_bd': los
--      pone siempre la BD (así nadie puede falsear cuándo o quién).
--    · 'sp_actualizar_bitacora' tampoco toca esas dos columnas: reemplaza
--      las demás columnas de negocio (fk_usuario, nombre_tabla, operacion,
--      llave, datos_antes, datos_despues).
--
--  'bitacora' NO tiene columna 'activo' -> 'sp_eliminar_bitacora' sería
--  FÍSICO (DELETE). 'sp_listar_bitacora' devuelve todas las filas, las más
--  recientes primero.
--
--  Esta tabla NO tenía código en la API: su capa C# se construye junto con
--  esta migración.
-- ==========================================================================

SET search_path TO catedras, public;

-- LISTAR — más recientes primero (ix_bitacora_fecha / ix_bitacora_tabla),
-- con filtro OPCIONAL por tabla auditada (NULL = todas) y límite OPCIONAL.
CREATE OR REPLACE PROCEDURE catedras.sp_listar_bitacora(
    IN    p_nombre_tabla character varying(63) DEFAULT NULL,
    IN    p_limite       integer               DEFAULT NULL,
    INOUT p_resultado    json                  DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(b), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_bitacora, ocurrido_en, usuario_bd, fk_usuario,
                   nombre_tabla, operacion, llave, datos_antes, datos_despues
              FROM bitacora
             WHERE p_nombre_tabla IS NULL OR nombre_tabla = p_nombre_tabla
             ORDER BY ocurrido_en DESC, id_bitacora DESC
             LIMIT p_limite) b;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_consultar_bitacora(
    IN    p_id_bitacora bigint,
    INOUT p_resultado   json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(b) INTO p_resultado
      FROM (SELECT id_bitacora, ocurrido_en, usuario_bd, fk_usuario,
                   nombre_tabla, operacion, llave, datos_antes, datos_despues
              FROM bitacora
             WHERE id_bitacora = p_id_bitacora) b;
END;
$$;

-- Operación fuera de INSERT/UPDATE/DELETE (chk_bitacora_operacion) o usuario
-- inexistente (fk_bitacora_usuario) -> NpgsqlException.
CREATE OR REPLACE PROCEDURE catedras.sp_crear_bitacora(
    IN    p_nombre_tabla  character varying(63),
    IN    p_operacion     character varying(10),
    IN    p_fk_usuario    bigint                 DEFAULT NULL,
    IN    p_llave         character varying(100) DEFAULT NULL,
    IN    p_datos_antes   jsonb                  DEFAULT NULL,
    IN    p_datos_despues jsonb                  DEFAULT NULL,
    INOUT p_resultado     json                   DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_id bigint;
BEGIN
    INSERT INTO bitacora (fk_usuario, nombre_tabla, operacion, llave,
                          datos_antes, datos_despues)
    VALUES (p_fk_usuario, p_nombre_tabla, p_operacion, p_llave,
            p_datos_antes, p_datos_despues)
    RETURNING id_bitacora INTO v_id;

    SELECT row_to_json(b) INTO p_resultado
      FROM (SELECT id_bitacora, ocurrido_en, usuario_bd, fk_usuario,
                   nombre_tabla, operacion, llave, datos_antes, datos_despues
              FROM bitacora
             WHERE id_bitacora = v_id) b;
END;
$$;

-- Reemplazo de las columnas de negocio; NO toca 'ocurrido_en' ni
-- 'usuario_bd' (se llenan solas al insertar y no se reescriben).
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_bitacora(
    IN    p_id_bitacora   bigint,
    IN    p_fk_usuario    bigint,
    IN    p_nombre_tabla  character varying(63),
    IN    p_operacion     character varying(10),
    IN    p_llave         character varying(100),
    IN    p_datos_antes   jsonb,
    IN    p_datos_despues jsonb,
    INOUT p_resultado     json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE bitacora
       SET fk_usuario    = p_fk_usuario,
           nombre_tabla  = p_nombre_tabla,
           operacion     = p_operacion,
           llave         = p_llave,
           datos_antes   = p_datos_antes,
           datos_despues = p_datos_despues
     WHERE id_bitacora = p_id_bitacora;

    IF FOUND THEN
        SELECT row_to_json(b) INTO p_resultado
          FROM (SELECT id_bitacora, ocurrido_en, usuario_bd, fk_usuario,
                       nombre_tabla, operacion, llave, datos_antes, datos_despues
                  FROM bitacora
                 WHERE id_bitacora = p_id_bitacora) b;
    END IF;
END;
$$;

-- FÍSICO: la tabla no tiene 'activo'.
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_bitacora(
    IN    p_id_bitacora bigint,
    INOUT p_resultado   json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    DELETE FROM bitacora
     WHERE id_bitacora = p_id_bitacora;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_bitacora IS
    'Retrofit criterio 3 (VERSION_2.md §4). Más recientes primero; filtro opcional por tabla y límite opcional.';
COMMENT ON PROCEDURE catedras.sp_consultar_bitacora IS
    'Retrofit criterio 3. NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_bitacora IS
    'Retrofit criterio 3. POST /api/Bitacora. ocurrido_en y usuario_bd los pone siempre la BD.';
COMMENT ON PROCEDURE catedras.sp_actualizar_bitacora IS
    'Retrofit criterio 3. PUT/PATCH /api/Bitacora. No toca ocurrido_en ni usuario_bd.';
COMMENT ON PROCEDURE catedras.sp_eliminar_bitacora IS
    'Retrofit criterio 3. DELETE /api/Bitacora. Borrado FÍSICO: la tabla no tiene activo.';
