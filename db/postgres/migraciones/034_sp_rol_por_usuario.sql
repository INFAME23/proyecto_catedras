-- ==========================================================================
--  db/postgres/migraciones/034_sp_rol_por_usuario.sql
--  Proyecto Cátedras · RETROFIT criterio 3 (VERSION_2.md §4) · Fase 5
--  Molde: 005_sp_sede_ejemplo.sql (ver PLAN_RETROFIT_SP.md).
--
--  'rol_por_usuario' NO tiene 'activo' -> 'sp_eliminar_' es FÍSICO.
--
--  AJUSTES AL MOLDE DE 'sede':
--   1) Llave COMPUESTA (fk_usuario, fk_rol, fecha_ini): consultar,
--      actualizar, cerrar y eliminar reciben las tres columnas.
--   2) La única columna que no es llave es 'fecha_fin', así que
--      'sp_actualizar_' solo reemplaza esa.
--   3) 'sp_cerrar_rol_por_usuario' (extra): es el verbo que de verdad usa
--      la API (PATCH /cerrar). Se deja aparte de 'sp_actualizar_' para
--      conservar la guarda 'AND fecha_fin IS NULL' (no cerrar dos veces el
--      mismo rol aunque lleguen dos peticiones a la vez) — mismo criterio
--      que 'sp_revocar_enlace_registro'.
--   4) 'sp_listar_rol_vigente_usuario' (extra): ObtenerVigentesAsync, que
--      usa el login. Devuelve un arreglo JSON de códigos de rol (texto).
--   5) Fechas como 'date': desde C# llegan como DateOnly -> el manejador
--      de Dapper las manda como timestamp, y un CALL NO aplica casts de
--      asignación, así que el repositorio escribe CAST(@x AS date).
--
--  ⚠️ 'sp_eliminar_rol_por_usuario' existe para cumplir el criterio, pero
--  NO lo expone la API (IRepositorioRolPorUsuario no tiene Eliminar):
--  borrar una fila de rol borra historia. Lo correcto es cerrarla.
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  LISTAR — filtro opcional por usuario, límite opcional. Orden: por
--  usuario y, dentro de cada uno, el más reciente primero (igual que antes).
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_listar_rol_por_usuario(
    IN    p_fk_usuario bigint  DEFAULT NULL,
    IN    p_limite     integer DEFAULT NULL,
    INOUT p_resultado  json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(r), '[]'::json)
      INTO p_resultado
      FROM (SELECT fk_usuario, fk_rol, fecha_ini, fecha_fin
              FROM rol_por_usuario
             WHERE p_fk_usuario IS NULL OR fk_usuario = p_fk_usuario
             ORDER BY fk_usuario, fecha_ini DESC, fk_rol
             LIMIT p_limite) r;
END;
$$;

-- --------------------------------------------------------------------------
--  LISTAR VIGENTES — solo los códigos de rol abiertos (fecha_fin NULL) de
--  un usuario. Siempre un arreglo ('[]' si no tiene ninguno).
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_listar_rol_vigente_usuario(
    IN    p_fk_usuario bigint,
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(fk_rol ORDER BY fk_rol), '[]'::json)
      INTO p_resultado
      FROM rol_por_usuario
     WHERE fk_usuario = p_fk_usuario
       AND fecha_fin IS NULL;
END;
$$;

-- --------------------------------------------------------------------------
--  CONSULTAR — por la llave completa. NULL = no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_consultar_rol_por_usuario(
    IN    p_fk_usuario bigint,
    IN    p_fk_rol     character varying(20),
    IN    p_fecha_ini  date,
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(r) INTO p_resultado
      FROM (SELECT fk_usuario, fk_rol, fecha_ini, fecha_fin
              FROM rol_por_usuario
             WHERE fk_usuario = p_fk_usuario
               AND fk_rol     = p_fk_rol
               AND fecha_ini  = p_fecha_ini) r;
END;
$$;

-- --------------------------------------------------------------------------
--  CREAR — un rol nace abierto (fecha_fin NULL). p_fecha_ini NULL = hoy
--  (mismo DEFAULT CURRENT_DATE de la tabla).
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_crear_rol_por_usuario(
    IN    p_fk_usuario bigint,
    IN    p_fk_rol     character varying(20),
    IN    p_fecha_ini  date DEFAULT NULL,
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_fecha_ini date := COALESCE(p_fecha_ini, CURRENT_DATE);
BEGIN
    INSERT INTO rol_por_usuario (fk_usuario, fk_rol, fecha_ini)
    VALUES (p_fk_usuario, p_fk_rol, v_fecha_ini);

    SELECT row_to_json(r) INTO p_resultado
      FROM (SELECT fk_usuario, fk_rol, fecha_ini, fecha_fin
              FROM rol_por_usuario
             WHERE fk_usuario = p_fk_usuario
               AND fk_rol     = p_fk_rol
               AND fecha_ini  = v_fecha_ini) r;
END;
$$;

-- --------------------------------------------------------------------------
--  ACTUALIZAR — reemplazo completo de la única columna no-llave.
--  (Para el flujo normal usar 'sp_cerrar_', que no deja cerrar dos veces.)
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_rol_por_usuario(
    IN    p_fk_usuario bigint,
    IN    p_fk_rol     character varying(20),
    IN    p_fecha_ini  date,
    IN    p_fecha_fin  date,
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE rol_por_usuario
       SET fecha_fin = p_fecha_fin
     WHERE fk_usuario = p_fk_usuario
       AND fk_rol     = p_fk_rol
       AND fecha_ini  = p_fecha_ini;

    IF FOUND THEN
        SELECT row_to_json(r) INTO p_resultado
          FROM (SELECT fk_usuario, fk_rol, fecha_ini, fecha_fin
                  FROM rol_por_usuario
                 WHERE fk_usuario = p_fk_usuario
                   AND fk_rol     = p_fk_rol
                   AND fecha_ini  = p_fecha_ini) r;
    END IF;
END;
$$;

-- --------------------------------------------------------------------------
--  CERRAR — fecha_fin solo si el rol sigue abierto. NULL en p_resultado =
--  no existe o ya estaba cerrado (el servicio lo traduce a 400/404 igual
--  que antes con "0 filas"). fecha_fin < fecha_ini -> chk_rol_usuario_fechas.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_cerrar_rol_por_usuario(
    IN    p_fk_usuario bigint,
    IN    p_fk_rol     character varying(20),
    IN    p_fecha_ini  date,
    IN    p_fecha_fin  date,
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE rol_por_usuario
       SET fecha_fin = p_fecha_fin
     WHERE fk_usuario = p_fk_usuario
       AND fk_rol     = p_fk_rol
       AND fecha_ini  = p_fecha_ini
       AND fecha_fin IS NULL;

    IF FOUND THEN
        SELECT row_to_json(r) INTO p_resultado
          FROM (SELECT fk_usuario, fk_rol, fecha_ini, fecha_fin
                  FROM rol_por_usuario
                 WHERE fk_usuario = p_fk_usuario
                   AND fk_rol     = p_fk_rol
                   AND fecha_ini  = p_fecha_ini) r;
    END IF;
END;
$$;

-- --------------------------------------------------------------------------
--  ELIMINAR — FÍSICO (sin 'activo'). No expuesto por la API (ver arriba).
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_rol_por_usuario(
    IN    p_fk_usuario bigint,
    IN    p_fk_rol     character varying(20),
    IN    p_fecha_ini  date,
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    DELETE FROM rol_por_usuario
     WHERE fk_usuario = p_fk_usuario
       AND fk_rol     = p_fk_rol
       AND fecha_ini  = p_fecha_ini;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_rol_por_usuario IS
    'Retrofit criterio 3. Filtro opcional por usuario, límite opcional.';
COMMENT ON PROCEDURE catedras.sp_listar_rol_vigente_usuario IS
    'Retrofit criterio 3. Arreglo JSON con los códigos de rol abiertos (login).';
COMMENT ON PROCEDURE catedras.sp_consultar_rol_por_usuario IS
    'Retrofit criterio 3. Por llave compuesta. NULL = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_rol_por_usuario IS
    'Retrofit criterio 3. El rol nace abierto. fecha_ini NULL = hoy.';
COMMENT ON PROCEDURE catedras.sp_actualizar_rol_por_usuario IS
    'Retrofit criterio 3. Reemplaza fecha_fin (única columna no-llave).';
COMMENT ON PROCEDURE catedras.sp_cerrar_rol_por_usuario IS
    'Retrofit criterio 3. Cierra solo si sigue abierto. NULL = no existe o ya cerrado.';
COMMENT ON PROCEDURE catedras.sp_eliminar_rol_por_usuario IS
    'Retrofit criterio 3. Borrado FÍSICO. NO expuesto: lo correcto es cerrar el rol.';
