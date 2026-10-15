-- ==========================================================================
--  db/postgres/migraciones/009_sp_periodo_academico.sql
--  Proyecto Cátedras · RETROFIT criterio 3 de VERSION_2.md (Fase 2 de
--  PLAN_RETROFIT_SP.md): los 5 verbos de 'periodo_academico' por procedimiento
--  almacenado, con el MISMO molde de 005_sp_sede_ejemplo.sql.
--
--  'periodo_academico' SÍ tiene columna 'activo' -> borrado LÓGICO y listar
--  solo activos, igual que sede.
--
--  AJUSTES respecto a sede: (1) 'sp_crear_periodo_academico' recibe
--  'p_activo' (el POST lo admite); (2) la tabla NO tiene 'nombre': sus
--  columnas de negocio son fecha_ini/fecha_fin; (3) la pk es character(6)
--  con formato AAAA-S (chk_periodo_formato).
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  LISTAR — solo activos, con límite OPCIONAL (NULL = sin límite).
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_listar_periodo_academico(
    IN    p_limite     integer DEFAULT NULL,
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(p), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_periodo, fecha_ini, fecha_fin, activo
              FROM periodo_academico
             WHERE activo
             ORDER BY id_periodo
             LIMIT p_limite) p;
END;
$$;

-- --------------------------------------------------------------------------
--  CONSULTAR — una fila por id. NULL en p_resultado = no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_consultar_periodo_academico(
    IN    p_id_periodo character(6),
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(p) INTO p_resultado
      FROM (SELECT id_periodo, fecha_ini, fecha_fin, activo
              FROM periodo_academico
             WHERE id_periodo = p_id_periodo) p;
END;
$$;

-- --------------------------------------------------------------------------
--  CREAR — devuelve la fila como quedó guardada. Id repetido o un CHECK
--  violado -> NpgsqlException (el controlador la traduce, criterio 7).
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_crear_periodo_academico(
    IN    p_id_periodo character(6),
    IN    p_fecha_ini  date,
    IN    p_fecha_fin  date,
    IN    p_activo     boolean DEFAULT true,
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    INSERT INTO periodo_academico (id_periodo, fecha_ini, fecha_fin, activo)
    VALUES (p_id_periodo, p_fecha_ini, p_fecha_fin, p_activo);

    SELECT row_to_json(p) INTO p_resultado
      FROM (SELECT id_periodo, fecha_ini, fecha_fin, activo
              FROM periodo_academico
             WHERE id_periodo = p_id_periodo) p;
END;
$$;

-- --------------------------------------------------------------------------
--  ACTUALIZAR — reemplazo COMPLETO (PUT directo; PATCH llega ya mezclado
--  desde el repositorio). Sin FOUND -> p_resultado NULL = no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_periodo_academico(
    IN    p_id_periodo character(6),
    IN    p_fecha_ini  date,
    IN    p_fecha_fin  date,
    IN    p_activo     boolean,
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE periodo_academico
       SET fecha_ini = p_fecha_ini,
           fecha_fin = p_fecha_fin,
           activo    = p_activo
     WHERE id_periodo = p_id_periodo;

    IF FOUND THEN
        SELECT row_to_json(p) INTO p_resultado
          FROM (SELECT id_periodo, fecha_ini, fecha_fin, activo
                  FROM periodo_academico
                 WHERE id_periodo = p_id_periodo) p;
    END IF;
END;
$$;

-- --------------------------------------------------------------------------
--  ELIMINAR — LÓGICO (la tabla tiene 'activo'). Ya inactivo -> no cuenta.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_periodo_academico(
    IN    p_id_periodo character(6),
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE periodo_academico
       SET activo = false
     WHERE id_periodo = p_id_periodo
       AND activo;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_periodo_academico IS
    'Retrofit criterio 3 (VERSION_2.md §4). Solo activos, con límite opcional.';
COMMENT ON PROCEDURE catedras.sp_consultar_periodo_academico IS
    'Retrofit criterio 3. NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_periodo_academico IS
    'Retrofit criterio 3. Devuelve la fila como quedó guardada.';
COMMENT ON PROCEDURE catedras.sp_actualizar_periodo_academico IS
    'Retrofit criterio 3. Reemplazo completo; PATCH mezcla antes de llamar.';
COMMENT ON PROCEDURE catedras.sp_eliminar_periodo_academico IS
    'Retrofit criterio 3. Borrado LÓGICO (activo=false): la tabla tiene activo.';
