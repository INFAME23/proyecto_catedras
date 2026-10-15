-- ==========================================================================
--  db/postgres/migraciones/026_sp_vinculacion_asistente.sql
--  Proyecto Cátedras · RETROFIT criterio 3 (PLAN_RETROFIT_SP.md, Fase 4)
--  Tabla: 'vinculacion_asistente' — LLAVE COMPUESTA (ADR D16).
-- --------------------------------------------------------------------------
--  AJUSTES AL MOLDE DE 'sede':
--   1. LLAVE DE 3 COLUMNAS (fk_asistente, fk_tipo_vinculacion, fecha_ini):
--      consultar/actualizar/eliminar reciben las 3 en vez de un 'p_id'.
--   2. ELIMINAR = CERRAR. La tabla no tiene 'activo', así que la regla 3
--      del retrofit diría DELETE físico — pero esta tabla es una HISTORIA:
--      desde la v3 una vinculación se abre y se CIERRA (fecha_fin), nunca
--      se borra (ADR D16, RF109). 'fecha_fin IS NULL' cumple aquí el papel
--      de 'activo': 'sp_eliminar_' pone 'fecha_fin' (por defecto hoy) solo
--      si seguía abierta. Es exactamente el UPDATE que hacía
--      'RepositorioVinculacionAsistentePostgres.CerrarAsync', y es lo que
--      ese método llama ahora. No existe (ni se inventa) un DELETE.
--      Por eso 'sp_eliminar_' recibe un 4.º parámetro, 'p_fecha_fin'.
--   3. 'sp_actualizar_' reemplaza la ÚNICA columna que no es llave
--      ('fecha_fin', NULL incluido = reabrir). Existe para cumplir "los 5
--      verbos", pero la API NO lo expone: IRepositorioVinculacionAsistente
--      no tiene ActualizarAsync y no se le agrega (ADR D16 lo prohíbe).
--   4. 'fecha_ini'/'fecha_fin' son 'date'. Npgsql manda los DateOnly como
--      'timestamp' (ver ManejadorTipoDateOnly), y 'timestamp -> date' NO es
--      un cast implícito: un CALL con ese argumento no encontraría el
--      procedimiento. El repositorio escribe 'CAST(@fechaIni AS date)'.
-- ==========================================================================

SET search_path TO catedras, public;

CREATE OR REPLACE PROCEDURE catedras.sp_listar_vinculacion_asistente(
    IN    p_fk_asistente bigint  DEFAULT NULL,
    IN    p_limite       integer DEFAULT NULL,
    INOUT p_resultado    json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    -- Orden de la v3: por asistente y, dentro de cada uno, la más reciente primero.
    SELECT COALESCE(json_agg(v), '[]'::json)
      INTO p_resultado
      FROM (SELECT fk_asistente, fk_tipo_vinculacion, fecha_ini, fecha_fin
              FROM vinculacion_asistente
             WHERE p_fk_asistente IS NULL OR fk_asistente = p_fk_asistente
             ORDER BY fk_asistente, fecha_ini DESC, fk_tipo_vinculacion
             LIMIT p_limite) v;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_consultar_vinculacion_asistente(
    IN    p_fk_asistente        bigint,
    IN    p_fk_tipo_vinculacion character varying(15),
    IN    p_fecha_ini           date,
    INOUT p_resultado           json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(v) INTO p_resultado
      FROM (SELECT fk_asistente, fk_tipo_vinculacion, fecha_ini, fecha_fin
              FROM vinculacion_asistente
             WHERE fk_asistente        = p_fk_asistente
               AND fk_tipo_vinculacion = p_fk_tipo_vinculacion
               AND fecha_ini           = p_fecha_ini) v;
END;
$$;

-- CREAR (abrir) — nace sin 'fecha_fin'. Sin fecha_ini -> hoy (igual que
-- sp_guardar_asistente). Llave repetida / FK inexistente -> excepción (500).
CREATE OR REPLACE PROCEDURE catedras.sp_crear_vinculacion_asistente(
    IN    p_fk_asistente        bigint,
    IN    p_fk_tipo_vinculacion character varying(15),
    IN    p_fecha_ini           date DEFAULT NULL,
    INOUT p_resultado           json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_fecha_ini date := COALESCE(p_fecha_ini, CURRENT_DATE);
BEGIN
    INSERT INTO vinculacion_asistente (fk_asistente, fk_tipo_vinculacion, fecha_ini)
    VALUES (p_fk_asistente, p_fk_tipo_vinculacion, v_fecha_ini);

    CALL catedras.sp_consultar_vinculacion_asistente(
        p_fk_asistente, p_fk_tipo_vinculacion, v_fecha_ini, p_resultado);
END;
$$;

-- ACTUALIZAR — reemplaza 'fecha_fin' (la única columna que no es llave).
-- No expuesto por la API (ADR D16). chk_vinculacion_fechas sigue mandando.
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_vinculacion_asistente(
    IN    p_fk_asistente        bigint,
    IN    p_fk_tipo_vinculacion character varying(15),
    IN    p_fecha_ini           date,
    IN    p_fecha_fin           date,
    INOUT p_resultado           json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE vinculacion_asistente
       SET fecha_fin = p_fecha_fin
     WHERE fk_asistente        = p_fk_asistente
       AND fk_tipo_vinculacion = p_fk_tipo_vinculacion
       AND fecha_ini           = p_fecha_ini;

    IF FOUND THEN
        CALL catedras.sp_consultar_vinculacion_asistente(
            p_fk_asistente, p_fk_tipo_vinculacion, p_fecha_ini, p_resultado);
    END IF;
END;
$$;

-- ELIMINAR = CERRAR (ver ajuste 2). 'AND fecha_fin IS NULL' es la defensa
-- del motor: una vinculación ya cerrada no se "re-cierra" (eliminado=false).
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_vinculacion_asistente(
    IN    p_fk_asistente        bigint,
    IN    p_fk_tipo_vinculacion character varying(15),
    IN    p_fecha_ini           date,
    IN    p_fecha_fin           date DEFAULT NULL,
    INOUT p_resultado           json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE vinculacion_asistente
       SET fecha_fin = COALESCE(p_fecha_fin, CURRENT_DATE)
     WHERE fk_asistente        = p_fk_asistente
       AND fk_tipo_vinculacion = p_fk_tipo_vinculacion
       AND fecha_ini           = p_fecha_ini
       AND fecha_fin IS NULL;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_vinculacion_asistente IS
    'Retrofit criterio 3. Filtro opcional por asistente.';
COMMENT ON PROCEDURE catedras.sp_consultar_vinculacion_asistente IS
    'Retrofit criterio 3. Llave compuesta (3 columnas). NULL = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_vinculacion_asistente IS
    'Retrofit criterio 3. Abre una vinculación (fecha_fin NULL).';
COMMENT ON PROCEDURE catedras.sp_actualizar_vinculacion_asistente IS
    'Retrofit criterio 3. Reemplaza fecha_fin. NO expuesto por la API (ADR D16).';
COMMENT ON PROCEDURE catedras.sp_eliminar_vinculacion_asistente IS
    'Retrofit criterio 3. ELIMINAR = CERRAR (fecha_fin), nunca DELETE: la tabla es una historia (ADR D16).';
