-- ==========================================================================
--  db/postgres/migraciones/029_sp_lote_carga_asistente.sql
--  Proyecto Cátedras · RETROFIT criterio 3 (PLAN_RETROFIT_SP.md, Fase 4)
--  Tabla: 'lote_carga_asistente' (maestro de 'novedad_carga').
-- --------------------------------------------------------------------------
--  QUÉ YA EXISTÍA Y NO SE TOCA: 'sp_guardar_lote_carga' (migración 003).
--  OJO: esa migración NO es el "crear" del lote — es su CIERRE (contadores
--  finales + novedades en una transacción, ADR D15). El ABRIR (POST
--  /api/LoteCargaAsistente) era un INSERT suelto en el repositorio; ese es
--  el que pasa ahora a 'sp_crear_lote_carga_asistente'.
--
--  AJUSTES AL MOLDE DE 'sede':
--   1. Listar del más reciente al más antiguo (ORDER BY id DESC), como en
--      la v3; sin filtro y sin "WHERE activo" (no hay columna).
--   2. Clave IDENTITY -> 'v_id' en 'sp_crear_'. Los 3 contadores NO se
--      reciben al crear: nacen en 0 (DEFAULT de la columna).
--   3. 'sp_actualizar_' reemplaza todo menos 'id_lote_carga' y 'cargado_en'.
--      NO lo usa la API: RF117 dice que el lote no se edita; su única
--      escritura posterior es el CIERRE, que sigue siendo sp_guardar_lote_carga.
--   4. 'sp_eliminar_' es DELETE físico (sin 'activo'): sus novedades se van
--      en CASCADE y los asistentes que creó quedan con fk_lote_carga NULL
--      (SET NULL). NO lo usa la API (RF117: el lote es bitácora).
-- ==========================================================================

SET search_path TO catedras, public;

CREATE OR REPLACE PROCEDURE catedras.sp_listar_lote_carga_asistente(
    IN    p_limite    integer DEFAULT NULL,
    INOUT p_resultado json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(l), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_lote_carga, nombre_archivo, cargado_en, fk_usuario,
                   filas_leidas, filas_aceptadas, filas_rechazadas, observaciones
              FROM lote_carga_asistente
             ORDER BY id_lote_carga DESC
             LIMIT p_limite) l;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_consultar_lote_carga_asistente(
    IN    p_id_lote_carga bigint,
    INOUT p_resultado     json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(l) INTO p_resultado
      FROM (SELECT id_lote_carga, nombre_archivo, cargado_en, fk_usuario,
                   filas_leidas, filas_aceptadas, filas_rechazadas, observaciones
              FROM lote_carga_asistente
             WHERE id_lote_carga = p_id_lote_carga) l;
END;
$$;

-- CREAR (abrir) — contadores en 0, 'cargado_en' = now().
CREATE OR REPLACE PROCEDURE catedras.sp_crear_lote_carga_asistente(
    IN    p_nombre_archivo character varying(200),
    IN    p_fk_usuario     bigint DEFAULT NULL,
    INOUT p_resultado      json   DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_id bigint;
BEGIN
    INSERT INTO lote_carga_asistente (nombre_archivo, fk_usuario)
    VALUES (p_nombre_archivo, p_fk_usuario)
    RETURNING id_lote_carga INTO v_id;

    CALL catedras.sp_consultar_lote_carga_asistente(v_id, p_resultado);
END;
$$;

-- ACTUALIZAR — reemplazo completo. NO usado por la API (ver ajuste 3).
-- chk_lote_carga_filas sigue siendo la última muralla.
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_lote_carga_asistente(
    IN    p_id_lote_carga    bigint,
    IN    p_nombre_archivo   character varying(200),
    IN    p_fk_usuario       bigint,
    IN    p_filas_leidas     integer,
    IN    p_filas_aceptadas  integer,
    IN    p_filas_rechazadas integer,
    IN    p_observaciones    text,
    INOUT p_resultado        json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE lote_carga_asistente
       SET nombre_archivo   = p_nombre_archivo,
           fk_usuario       = p_fk_usuario,
           filas_leidas     = p_filas_leidas,
           filas_aceptadas  = p_filas_aceptadas,
           filas_rechazadas = p_filas_rechazadas,
           observaciones    = p_observaciones
     WHERE id_lote_carga = p_id_lote_carga;

    IF FOUND THEN
        CALL catedras.sp_consultar_lote_carga_asistente(p_id_lote_carga, p_resultado);
    END IF;
END;
$$;

-- ELIMINAR — FÍSICO (sin 'activo'). NO usado por la API (ver ajuste 4).
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_lote_carga_asistente(
    IN    p_id_lote_carga bigint,
    INOUT p_resultado     json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    DELETE FROM lote_carga_asistente WHERE id_lote_carga = p_id_lote_carga;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_lote_carga_asistente IS
    'Retrofit criterio 3. Del más reciente al más antiguo.';
COMMENT ON PROCEDURE catedras.sp_consultar_lote_carga_asistente IS
    'Retrofit criterio 3. NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_lote_carga_asistente IS
    'Retrofit criterio 3. ABRE el lote (contadores en 0). El CIERRE es sp_guardar_lote_carga (003).';
COMMENT ON PROCEDURE catedras.sp_actualizar_lote_carga_asistente IS
    'Retrofit criterio 3. Reemplazo completo. NO expuesto por la API (RF117).';
COMMENT ON PROCEDURE catedras.sp_eliminar_lote_carga_asistente IS
    'Retrofit criterio 3. Borrado FÍSICO (novedades en CASCADE). NO expuesto por la API (RF117).';
