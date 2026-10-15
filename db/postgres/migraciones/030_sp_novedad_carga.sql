-- ==========================================================================
--  db/postgres/migraciones/030_sp_novedad_carga.sql
--  Proyecto Cátedras · RETROFIT criterio 3 (PLAN_RETROFIT_SP.md, Fase 4)
--  Tabla: 'novedad_carga' (detalle de 'lote_carga_asistente').
-- --------------------------------------------------------------------------
--  En la API esta tabla es de SOLO LECTURA (RF118): una novedad nace
--  únicamente dentro de 'sp_guardar_lote_carga' (003) al cerrar un lote.
--  Se construyen los 5 procedimientos (criterio 3), pero el repositorio
--  solo llama a listar y consultar; crear/actualizar/eliminar existen en la
--  BD y NO se exponen (IRepositorioNovedadCarga no los tiene).
--  'sp_guardar_lote_carga' NO se reescribe para llamar a
--  'sp_crear_novedad_carga': su INSERT ... SELECT de un arreglo jsonb es
--  una sola sentencia y no conviene partirlo en N CALL.
--
--  AJUSTES AL MOLDE DE 'sede':
--   1. Filtro opcional 'p_fk_lote_carga' en listar; orden (lote, fila).
--   2. Clave IDENTITY -> 'v_id' en 'sp_crear_'.
--   3. 'sp_eliminar_' es DELETE físico (sin 'activo').
--   4. 'sp_actualizar_' no cambia 'fk_lote_carga' (la novedad pertenece
--      siempre al lote que la produjo).
-- ==========================================================================

SET search_path TO catedras, public;

CREATE OR REPLACE PROCEDURE catedras.sp_listar_novedad_carga(
    IN    p_fk_lote_carga bigint  DEFAULT NULL,
    IN    p_limite        integer DEFAULT NULL,
    INOUT p_resultado     json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(n), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_novedad, fk_lote_carga, numero_fila, contenido_crudo, motivo
              FROM novedad_carga
             WHERE p_fk_lote_carga IS NULL OR fk_lote_carga = p_fk_lote_carga
             ORDER BY fk_lote_carga, numero_fila
             LIMIT p_limite) n;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_consultar_novedad_carga(
    IN    p_id_novedad bigint,
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(n) INTO p_resultado
      FROM (SELECT id_novedad, fk_lote_carga, numero_fila, contenido_crudo, motivo
              FROM novedad_carga
             WHERE id_novedad = p_id_novedad) n;
END;
$$;

-- CREAR — NO expuesto por la API (RF118). Fila <= 0 o motivo de más de
-- 200 caracteres -> CHECK / tipo de la columna (500).
CREATE OR REPLACE PROCEDURE catedras.sp_crear_novedad_carga(
    IN    p_fk_lote_carga   bigint,
    IN    p_numero_fila     integer,
    IN    p_contenido_crudo text,
    IN    p_motivo          character varying(200),
    INOUT p_resultado       json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_id bigint;
BEGIN
    INSERT INTO novedad_carga (fk_lote_carga, numero_fila, contenido_crudo, motivo)
    VALUES (p_fk_lote_carga, p_numero_fila, p_contenido_crudo, p_motivo)
    RETURNING id_novedad INTO v_id;

    CALL catedras.sp_consultar_novedad_carga(v_id, p_resultado);
END;
$$;

-- ACTUALIZAR — NO expuesto por la API (RF118).
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_novedad_carga(
    IN    p_id_novedad      bigint,
    IN    p_numero_fila     integer,
    IN    p_contenido_crudo text,
    IN    p_motivo          character varying(200),
    INOUT p_resultado       json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE novedad_carga
       SET numero_fila     = p_numero_fila,
           contenido_crudo = p_contenido_crudo,
           motivo          = p_motivo
     WHERE id_novedad = p_id_novedad;

    IF FOUND THEN
        CALL catedras.sp_consultar_novedad_carga(p_id_novedad, p_resultado);
    END IF;
END;
$$;

-- ELIMINAR — FÍSICO (sin 'activo'). NO expuesto por la API (RF118).
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_novedad_carga(
    IN    p_id_novedad bigint,
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    DELETE FROM novedad_carga WHERE id_novedad = p_id_novedad;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_novedad_carga IS
    'Retrofit criterio 3. Filtro opcional por lote.';
COMMENT ON PROCEDURE catedras.sp_consultar_novedad_carga IS
    'Retrofit criterio 3. NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_novedad_carga IS
    'Retrofit criterio 3. NO expuesto por la API: las novedades nacen en sp_guardar_lote_carga (RF118).';
COMMENT ON PROCEDURE catedras.sp_actualizar_novedad_carga IS
    'Retrofit criterio 3. NO expuesto por la API (RF118).';
COMMENT ON PROCEDURE catedras.sp_eliminar_novedad_carga IS
    'Retrofit criterio 3. Borrado FÍSICO. NO expuesto por la API (RF118).';
