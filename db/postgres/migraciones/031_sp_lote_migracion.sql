-- ==========================================================================
--  db/postgres/migraciones/031_sp_lote_migracion.sql
--  Proyecto Cátedras · RETROFIT criterio 3 (PLAN_RETROFIT_SP.md, Fase 4)
--  Tabla: 'lote_migracion' (maestro de 'detalle_migracion'). Contrato de la
--  API: version7/2_spec.md RF179-RF185 (borrador).
-- --------------------------------------------------------------------------
--  QUÉ YA EXISTÍA Y NO SE TOCA: 'sp_generar_lote_migracion' (script
--  provisto, desde la v1): crea el lote + una fila de detalle por cada
--  registro migrable + pone la sesión en 'MIGRADA', todo en una
--  transacción (ADR D34 de la v7). 'sp_crear_lote_migracion' NO lo
--  reimplementa: lo LLAMA y devuelve la fila como quedó. Así el "crear"
--  pasa por un procedimiento con el nombre del molde sin duplicar la
--  lógica del maestro-detalle (ni arriesgarse a no replicar su efecto
--  colateral sobre 'sesion').
--  Nota: el borrador de la v7 decía "SELECT * FROM fn_generar_lote_api";
--  esa función es solo un envoltorio del mismo procedimiento.
--
--  AJUSTES AL MOLDE DE 'sede':
--   1. Filtro opcional 'p_fk_sesion' en listar; del más reciente al más
--      antiguo (ORDER BY id DESC), igual que lote_carga_asistente.
--   2. 'sp_crear_' recibe la SESIÓN, no las columnas: nombre_archivo y
--      total_filas los calcula sp_generar_lote_migracion. Sesión sin
--      registros migrables -> su RAISE EXCEPTION (check_violation) sube
--      tal cual (500, RF181).
--   3. 'sp_actualizar_' reemplaza solo las 3 columnas que cambian con el
--      tiempo según responde ASIS: instancia_proceso_asis,
--      fk_estado_proceso y observaciones (RF182, ADR D35). fk_sesion,
--      nombre_*, generado_en, fk_usuario y total_filas son la foto de la
--      generación y no se reemplazan.
--   4. 'sp_eliminar_' es DELETE físico (sin 'activo'); los detalles se van
--      en CASCADE. NO lo usa la API: RF185 — borrar un lote borraría la
--      evidencia de un intento de migración real.
-- ==========================================================================

SET search_path TO catedras, public;

CREATE OR REPLACE PROCEDURE catedras.sp_listar_lote_migracion(
    IN    p_fk_sesion bigint  DEFAULT NULL,
    IN    p_limite    integer DEFAULT NULL,
    INOUT p_resultado json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(l), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_lote_migracion, fk_sesion, nombre_proceso, nombre_archivo,
                   generado_en, fk_usuario, instancia_proceso_asis,
                   fk_estado_proceso, total_filas, observaciones
              FROM lote_migracion
             WHERE p_fk_sesion IS NULL OR fk_sesion = p_fk_sesion
             ORDER BY id_lote_migracion DESC
             LIMIT p_limite) l;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_consultar_lote_migracion(
    IN    p_id_lote_migracion bigint,
    INOUT p_resultado         json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(l) INTO p_resultado
      FROM (SELECT id_lote_migracion, fk_sesion, nombre_proceso, nombre_archivo,
                   generado_en, fk_usuario, instancia_proceso_asis,
                   fk_estado_proceso, total_filas, observaciones
              FROM lote_migracion
             WHERE id_lote_migracion = p_id_lote_migracion) l;
END;
$$;

-- CREAR (generar) — ver ajuste 2. Todo lo que hace sp_generar_lote_migracion
-- (maestro + detalles + sesion MIGRADA) queda en la MISMA transacción del CALL.
CREATE OR REPLACE PROCEDURE catedras.sp_crear_lote_migracion(
    IN    p_fk_sesion  bigint,
    IN    p_fk_usuario bigint DEFAULT NULL,
    INOUT p_resultado  json   DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_id bigint;
BEGIN
    CALL catedras.sp_generar_lote_migracion(p_fk_sesion, p_fk_usuario, v_id);

    CALL catedras.sp_consultar_lote_migracion(v_id, p_resultado);
END;
$$;

-- ACTUALIZAR — ver ajuste 3. fk_estado_proceso inexistente -> FK (500).
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_lote_migracion(
    IN    p_id_lote_migracion      bigint,
    IN    p_instancia_proceso_asis character varying(30),
    IN    p_fk_estado_proceso      character varying(12),
    IN    p_observaciones          text,
    INOUT p_resultado              json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE lote_migracion
       SET instancia_proceso_asis = p_instancia_proceso_asis,
           fk_estado_proceso      = p_fk_estado_proceso,
           observaciones          = p_observaciones
     WHERE id_lote_migracion = p_id_lote_migracion;

    IF FOUND THEN
        CALL catedras.sp_consultar_lote_migracion(p_id_lote_migracion, p_resultado);
    END IF;
END;
$$;

-- ELIMINAR — FÍSICO (sin 'activo'). NO usado por la API (RF185).
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_lote_migracion(
    IN    p_id_lote_migracion bigint,
    INOUT p_resultado         json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    DELETE FROM lote_migracion WHERE id_lote_migracion = p_id_lote_migracion;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_lote_migracion IS
    'Retrofit criterio 3. Filtro opcional por sesión; más reciente primero.';
COMMENT ON PROCEDURE catedras.sp_consultar_lote_migracion IS
    'Retrofit criterio 3. NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_lote_migracion IS
    'Retrofit criterio 3. Genera el lote llamando a sp_generar_lote_migracion (maestro+detalle+sesión MIGRADA).';
COMMENT ON PROCEDURE catedras.sp_actualizar_lote_migracion IS
    'Retrofit criterio 3. Reemplaza instancia_proceso_asis, fk_estado_proceso y observaciones (RF182).';
COMMENT ON PROCEDURE catedras.sp_eliminar_lote_migracion IS
    'Retrofit criterio 3. Borrado FÍSICO (detalles en CASCADE). NO expuesto por la API (RF185).';
