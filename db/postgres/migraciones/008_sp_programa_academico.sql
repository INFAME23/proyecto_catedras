-- ==========================================================================
--  db/postgres/migraciones/008_sp_programa_academico.sql
--  Proyecto Cátedras · RETROFIT criterio 3 de VERSION_2.md · 'programa_academico'
-- --------------------------------------------------------------------------
--  Agrega Listar / Consultar / Actualizar / Eliminar. NO hay
--  'sp_crear_programa_academico': el Crear (maestro-detalle: programa +
--  sus alias iniciales en una transacción) YA es
--  'sp_guardar_programa_academico' (migración 001) y NO se reemplaza ni se
--  toca. El repositorio sigue llamándolo igual.
--
--  'programa_academico' SÍ tiene 'activo' -> 'sp_eliminar_programa_academico'
--  es LÓGICO. La clave es 'codigo' (varchar(5), clave natural que viaja al
--  archivo plano del ASIS).
-- ==========================================================================

SET search_path TO catedras, public;

-- CORRECCIÓN (lote 006-014): el listado SOLO traía las activas y el front no podía
-- ver ni reactivar las inactivas. 'p_incluir_inactivos' (DEFAULT false) conserva el
-- comportamiento del patrón y permite pedir todas con ?incluirInactivos=true. Cambia
-- la firma, así que se elimina la anterior (si no, quedaría una sobrecarga ambigua).
DROP PROCEDURE IF EXISTS catedras.sp_listar_programa_academico(integer, json);

CREATE OR REPLACE PROCEDURE catedras.sp_listar_programa_academico(
    IN    p_limite    integer DEFAULT NULL,
    IN    p_incluir_inactivos boolean DEFAULT false,
    INOUT p_resultado json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(p), '[]'::json)
      INTO p_resultado
      FROM (SELECT codigo, nombre, fk_facultad, nivel, activo
              FROM programa_academico
             WHERE p_incluir_inactivos OR activo
             ORDER BY codigo
             LIMIT p_limite) p;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_consultar_programa_academico(
    IN    p_codigo    character varying(5),
    INOUT p_resultado json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(p) INTO p_resultado
      FROM (SELECT codigo, nombre, fk_facultad, nivel, activo
              FROM programa_academico
             WHERE codigo = p_codigo) p;
END;
$$;

-- Reemplazo COMPLETO (PUT directo; el PATCH se mezcla en el repositorio).
-- Nivel fuera de la lista -> chk_programa_nivel -> NpgsqlException.
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_programa_academico(
    IN    p_codigo      character varying(5),
    IN    p_nombre      character varying(150),
    IN    p_fk_facultad character varying(10),
    IN    p_nivel       character varying(25),
    IN    p_activo      boolean,
    INOUT p_resultado   json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE programa_academico
       SET nombre      = p_nombre,
           fk_facultad = p_fk_facultad,
           nivel       = p_nivel,
           activo      = p_activo
     WHERE codigo = p_codigo;

    IF FOUND THEN
        SELECT row_to_json(p) INTO p_resultado
          FROM (SELECT codigo, nombre, fk_facultad, nivel, activo
                  FROM programa_academico
                 WHERE codigo = p_codigo) p;
    END IF;
END;
$$;

-- LÓGICO. Los alias NO se tocan: siguen apuntando al programa (que sigue
-- existiendo, solo inactivo) y fn_resolver_programa puede seguir
-- resolviéndolos.
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_programa_academico(
    IN    p_codigo    character varying(5),
    INOUT p_resultado json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE programa_academico
       SET activo = false
     WHERE codigo = p_codigo
       AND activo;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_programa_academico IS
    'Retrofit criterio 3 (VERSION_2.md §4). Solo activos, con límite opcional.';
COMMENT ON PROCEDURE catedras.sp_consultar_programa_academico IS
    'Retrofit criterio 3. NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_actualizar_programa_academico IS
    'Retrofit criterio 3. Reemplazo completo; PATCH mezcla antes de llamar.';
COMMENT ON PROCEDURE catedras.sp_eliminar_programa_academico IS
    'Retrofit criterio 3. Borrado LÓGICO (activo=false). El Crear sigue siendo sp_guardar_programa_academico (migración 001).';
