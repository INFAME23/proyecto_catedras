-- ==========================================================================
--  db/postgres/migraciones/007_sp_dependencia.sql
--  Proyecto Cátedras · RETROFIT criterio 3 de VERSION_2.md · 'dependencia'
-- --------------------------------------------------------------------------
--  Mismo molde que 005_sp_sede_ejemplo.sql / 006_sp_facultad.sql.
--  'dependencia' SÍ tiene 'activo' -> 'sp_eliminar_dependencia' es LÓGICO.
--  'sp_crear_dependencia' recibe 'p_activo' (DEFAULT true), igual que
--  'sp_crear_facultad' (ver la nota de 006).
-- ==========================================================================

SET search_path TO catedras, public;

-- CORRECCIÓN (lote 006-014): el listado SOLO traía las activas y el front no podía
-- ver ni reactivar las inactivas. 'p_incluir_inactivos' (DEFAULT false) conserva el
-- comportamiento del patrón y permite pedir todas con ?incluirInactivos=true. Cambia
-- la firma, así que se elimina la anterior (si no, quedaría una sobrecarga ambigua).
DROP PROCEDURE IF EXISTS catedras.sp_listar_dependencia(integer, json);

CREATE OR REPLACE PROCEDURE catedras.sp_listar_dependencia(
    IN    p_limite    integer DEFAULT NULL,
    IN    p_incluir_inactivos boolean DEFAULT false,
    INOUT p_resultado json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(d), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_dependencia, nombre, fk_dependencia_padre, activo
              FROM dependencia
             WHERE p_incluir_inactivos OR activo
             ORDER BY id_dependencia
             LIMIT p_limite) d;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_consultar_dependencia(
    IN    p_id_dependencia character varying(15),
    INOUT p_resultado      json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(d) INTO p_resultado
      FROM (SELECT id_dependencia, nombre, fk_dependencia_padre, activo
              FROM dependencia
             WHERE id_dependencia = p_id_dependencia) d;
END;
$$;

-- Clave repetida (pk_dependencia), nombre repetido (uq_dependencia_nombre),
-- padre inexistente o igual a sí misma (chk_dependencia_padre) ->
-- NpgsqlException.
CREATE OR REPLACE PROCEDURE catedras.sp_crear_dependencia(
    IN    p_id_dependencia       character varying(15),
    IN    p_nombre               character varying(120),
    IN    p_fk_dependencia_padre character varying(15) DEFAULT NULL,
    IN    p_activo               boolean               DEFAULT true,
    INOUT p_resultado            json                  DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    INSERT INTO dependencia (id_dependencia, nombre, fk_dependencia_padre, activo)
    VALUES (p_id_dependencia, p_nombre, p_fk_dependencia_padre, p_activo);

    SELECT row_to_json(d) INTO p_resultado
      FROM (SELECT id_dependencia, nombre, fk_dependencia_padre, activo
              FROM dependencia
             WHERE id_dependencia = p_id_dependencia) d;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_dependencia(
    IN    p_id_dependencia       character varying(15),
    IN    p_nombre               character varying(120),
    IN    p_fk_dependencia_padre character varying(15),
    IN    p_activo               boolean,
    INOUT p_resultado            json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE dependencia
       SET nombre               = p_nombre,
           fk_dependencia_padre = p_fk_dependencia_padre,
           activo               = p_activo
     WHERE id_dependencia = p_id_dependencia;

    IF FOUND THEN
        SELECT row_to_json(d) INTO p_resultado
          FROM (SELECT id_dependencia, nombre, fk_dependencia_padre, activo
                  FROM dependencia
                 WHERE id_dependencia = p_id_dependencia) d;
    END IF;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_dependencia(
    IN    p_id_dependencia character varying(15),
    INOUT p_resultado      json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE dependencia
       SET activo = false
     WHERE id_dependencia = p_id_dependencia
       AND activo;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_dependencia IS
    'Retrofit criterio 3 (VERSION_2.md §4). Solo activas, con límite opcional.';
COMMENT ON PROCEDURE catedras.sp_consultar_dependencia IS
    'Retrofit criterio 3. NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_dependencia IS
    'Retrofit criterio 3. Devuelve la fila como quedó guardada.';
COMMENT ON PROCEDURE catedras.sp_actualizar_dependencia IS
    'Retrofit criterio 3. Reemplazo completo; PATCH mezcla antes de llamar.';
COMMENT ON PROCEDURE catedras.sp_eliminar_dependencia IS
    'Retrofit criterio 3. Borrado LÓGICO (activo=false): la tabla tiene activo.';
