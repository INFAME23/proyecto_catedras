-- ==========================================================================
--  db/postgres/migraciones/006_sp_facultad.sql
--  Proyecto Cátedras · RETROFIT criterio 3 de VERSION_2.md · 'facultad'
-- --------------------------------------------------------------------------
--  Los 5 verbos de 'facultad' pasan por procedimientos almacenados, con el
--  molde exacto de 005_sp_sede_ejemplo.sql (PROCEDURE, INOUT p_resultado
--  JSON como ÚLTIMO parámetro, un procedimiento por verbo y por tabla).
--
--  'facultad' SÍ tiene columna 'activo' -> 'sp_eliminar_facultad' es
--  LÓGICO (UPDATE ... SET activo = false), NO un DELETE (PLAN_RETROFIT_SP.md
--  §1, decisión 2).
--
--  Diferencia con 'sede': 'sp_crear_facultad' RECIBE 'p_activo' (DEFAULT
--  true). El servicio manda 'Activo' (viene de FacultadCrear.Activo ?? true)
--  y, si el procedimiento lo ignorara, una facultad creada con
--  "activo": false quedaría activa sin avisar.
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  LISTAR — solo las activas, con límite OPCIONAL (p_limite NULL = sin
--  límite: así funciona LIMIT en PostgreSQL).
-- --------------------------------------------------------------------------
-- CORRECCIÓN (lote 006-014): el listado SOLO traía las activas y el front no podía
-- ver ni reactivar las inactivas. 'p_incluir_inactivos' (DEFAULT false) conserva el
-- comportamiento del patrón y permite pedir todas con ?incluirInactivos=true. Cambia
-- la firma, así que se elimina la anterior (si no, quedaría una sobrecarga ambigua).
DROP PROCEDURE IF EXISTS catedras.sp_listar_facultad(integer, json);

CREATE OR REPLACE PROCEDURE catedras.sp_listar_facultad(
    IN    p_limite    integer DEFAULT NULL,
    IN    p_incluir_inactivos boolean DEFAULT false,
    INOUT p_resultado json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(f), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_facultad, nombre, fk_facultad_padre, activo
              FROM facultad
             WHERE p_incluir_inactivos OR activo
             ORDER BY id_facultad
             LIMIT p_limite) f;
END;
$$;

-- --------------------------------------------------------------------------
--  CONSULTAR — una facultad por id (activa o no). Si no existe,
--  'p_resultado' queda NULL.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_consultar_facultad(
    IN    p_id_facultad character varying(10),
    INOUT p_resultado   json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(f) INTO p_resultado
      FROM (SELECT id_facultad, nombre, fk_facultad_padre, activo
              FROM facultad
             WHERE id_facultad = p_id_facultad) f;
END;
$$;

-- --------------------------------------------------------------------------
--  CREAR — devuelve la fila COMO QUEDÓ GUARDADA. La clave 'id_facultad' la
--  manda el cliente (clave de negocio), así que no hace falta variable
--  intermedia. Clave o nombre repetido (pk_facultad, uq_facultad_nombre) o
--  padre inexistente (fk_facultad_padre) -> NpgsqlException.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_crear_facultad(
    IN    p_id_facultad       character varying(10),
    IN    p_nombre            character varying(120),
    IN    p_fk_facultad_padre character varying(10) DEFAULT NULL,
    IN    p_activo            boolean               DEFAULT true,
    INOUT p_resultado         json                  DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    INSERT INTO facultad (id_facultad, nombre, fk_facultad_padre, activo)
    VALUES (p_id_facultad, p_nombre, p_fk_facultad_padre, p_activo);

    SELECT row_to_json(f) INTO p_resultado
      FROM (SELECT id_facultad, nombre, fk_facultad_padre, activo
              FROM facultad
             WHERE id_facultad = p_id_facultad) f;
END;
$$;

-- --------------------------------------------------------------------------
--  ACTUALIZAR — reemplazo COMPLETO de las columnas de negocio (sirve para
--  PUT y para PATCH; el PATCH se mezcla en el repositorio antes de llamar).
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_facultad(
    IN    p_id_facultad       character varying(10),
    IN    p_nombre            character varying(120),
    IN    p_fk_facultad_padre character varying(10),
    IN    p_activo            boolean,
    INOUT p_resultado         json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE facultad
       SET nombre            = p_nombre,
           fk_facultad_padre = p_fk_facultad_padre,
           activo            = p_activo
     WHERE id_facultad = p_id_facultad;

    -- Sin FOUND -> no existía -> p_resultado queda NULL.
    IF FOUND THEN
        SELECT row_to_json(f) INTO p_resultado
          FROM (SELECT id_facultad, nombre, fk_facultad_padre, activo
                  FROM facultad
                 WHERE id_facultad = p_id_facultad) f;
    END IF;
END;
$$;

-- --------------------------------------------------------------------------
--  ELIMINAR — LÓGICO: 'facultad' tiene 'activo'. Si ya estaba inactiva
--  cuenta como "no encontrada" (ya no hay nada que retirar).
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_facultad(
    IN    p_id_facultad character varying(10),
    INOUT p_resultado   json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE facultad
       SET activo = false
     WHERE id_facultad = p_id_facultad
       AND activo;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_facultad IS
    'Retrofit criterio 3 (VERSION_2.md §4). Solo activas, con límite opcional.';
COMMENT ON PROCEDURE catedras.sp_consultar_facultad IS
    'Retrofit criterio 3. NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_facultad IS
    'Retrofit criterio 3. Devuelve la fila como quedó guardada.';
COMMENT ON PROCEDURE catedras.sp_actualizar_facultad IS
    'Retrofit criterio 3. Reemplazo completo; PATCH mezcla antes de llamar.';
COMMENT ON PROCEDURE catedras.sp_eliminar_facultad IS
    'Retrofit criterio 3. Borrado LÓGICO (activo=false): la tabla tiene activo.';
