-- ==========================================================================
--  db/postgres/migraciones/010_sp_catedra.sql
--  Proyecto Cátedras · RETROFIT criterio 3 de VERSION_2.md · 'catedra'
-- --------------------------------------------------------------------------
--  Los 5 verbos de 'catedra' + 'sp_contar_catedra' (el 6.º que ya tenía
--  IRepositorioCatedra.ContarAsync; PLAN_RETROFIT_SP.md §2 lo advierte).
--
--  'catedra' SÍ tiene 'activo' -> 'sp_eliminar_catedra' es LÓGICO.
--
--  COLUMNA GENERADA: 'nombre_asis' es GENERATED ALWAYS (los 30 primeros
--  caracteres de 'nombre'). NUNCA aparece en un INSERT ni en un UPDATE: la
--  calcula PostgreSQL. Sí va en el JSON de salida, porque el servicio la
--  devuelve al crear.
--
--  'id_catedra' lo genera la BD (identity): 'sp_crear_catedra' lo recoge con
--  RETURNING y devuelve la fila COMPLETA, con 'id_catedra', 'nombre_asis' y
--  'creado_en' ya puestos.
-- ==========================================================================

SET search_path TO catedras, public;

-- CORRECCIÓN (lote 006-014): el listado SOLO traía las activas y el front no podía
-- ver ni reactivar las inactivas. 'p_incluir_inactivos' (DEFAULT false) conserva el
-- comportamiento del patrón y permite pedir todas con ?incluirInactivos=true. Cambia
-- la firma, así que se elimina la anterior (si no, quedaría una sobrecarga ambigua).
DROP PROCEDURE IF EXISTS catedras.sp_listar_catedra(integer, json);

CREATE OR REPLACE PROCEDURE catedras.sp_listar_catedra(
    IN    p_limite    integer DEFAULT NULL,
    IN    p_incluir_inactivos boolean DEFAULT false,
    INOUT p_resultado json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(c), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_catedra, id_evento_asis, nombre, nombre_asis,
                   fk_tipo_evento, fk_dependencia, activo, creado_en
              FROM catedra
             WHERE p_incluir_inactivos OR activo
             ORDER BY id_catedra
             LIMIT p_limite) c;
END;
$$;

-- CONTAR — el 'total' de la respuesta del listado. Cuenta lo MISMO que lista
-- 'sp_listar_catedra' (por defecto solo activas; con p_incluir_inactivos = true,
-- todas): si no, el 'total' no coincidiría con las filas que de verdad salen.
-- Devuelve {"total": n}.
DROP PROCEDURE IF EXISTS catedras.sp_contar_catedra(json);

CREATE OR REPLACE PROCEDURE catedras.sp_contar_catedra(
    IN    p_incluir_inactivos boolean DEFAULT false,
    INOUT p_resultado         json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT json_build_object('total', count(*))
      INTO p_resultado
      FROM catedra
     WHERE p_incluir_inactivos OR activo;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_consultar_catedra(
    IN    p_id_catedra bigint,
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(c) INTO p_resultado
      FROM (SELECT id_catedra, id_evento_asis, nombre, nombre_asis,
                   fk_tipo_evento, fk_dependencia, activo, creado_en
              FROM catedra
             WHERE id_catedra = p_id_catedra) c;
END;
$$;

-- id_evento_asis fuera de ^[0-9]{9}$ (chk_catedra_asis), tipo de evento o
-- dependencia inexistentes -> NpgsqlException.
CREATE OR REPLACE PROCEDURE catedras.sp_crear_catedra(
    IN    p_id_evento_asis character(9),
    IN    p_nombre         character varying(200),
    IN    p_fk_tipo_evento character(4),
    IN    p_fk_dependencia character varying(15) DEFAULT NULL,
    IN    p_activo         boolean               DEFAULT true,
    INOUT p_resultado      json                  DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_id bigint;
BEGIN
    INSERT INTO catedra (id_evento_asis, nombre, fk_tipo_evento, fk_dependencia, activo)
    VALUES (p_id_evento_asis, p_nombre, p_fk_tipo_evento, p_fk_dependencia, p_activo)
    RETURNING id_catedra INTO v_id;

    SELECT row_to_json(c) INTO p_resultado
      FROM (SELECT id_catedra, id_evento_asis, nombre, nombre_asis,
                   fk_tipo_evento, fk_dependencia, activo, creado_en
              FROM catedra
             WHERE id_catedra = v_id) c;
END;
$$;

-- Reemplazo COMPLETO (sin 'nombre_asis', que es generada, ni 'creado_en').
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_catedra(
    IN    p_id_catedra     bigint,
    IN    p_id_evento_asis character(9),
    IN    p_nombre         character varying(200),
    IN    p_fk_tipo_evento character(4),
    IN    p_fk_dependencia character varying(15),
    IN    p_activo         boolean,
    INOUT p_resultado      json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE catedra
       SET id_evento_asis = p_id_evento_asis,
           nombre         = p_nombre,
           fk_tipo_evento = p_fk_tipo_evento,
           fk_dependencia = p_fk_dependencia,
           activo         = p_activo
     WHERE id_catedra = p_id_catedra;

    IF FOUND THEN
        SELECT row_to_json(c) INTO p_resultado
          FROM (SELECT id_catedra, id_evento_asis, nombre, nombre_asis,
                       fk_tipo_evento, fk_dependencia, activo, creado_en
                  FROM catedra
                 WHERE id_catedra = p_id_catedra) c;
    END IF;
END;
$$;

-- LÓGICO. Las sesiones de la cátedra NO se tocan (con el DELETE de antes
-- caían en cascada por fk_sesion_catedra; ahora conservan su historia).
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_catedra(
    IN    p_id_catedra bigint,
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE catedra
       SET activo = false
     WHERE id_catedra = p_id_catedra
       AND activo;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_catedra IS
    'Retrofit criterio 3 (VERSION_2.md §4). Solo activas, con límite opcional.';
COMMENT ON PROCEDURE catedras.sp_contar_catedra IS
    'Retrofit criterio 3. Cuenta las mismas filas que lista sp_listar_catedra (activas). {"total": n}.';
COMMENT ON PROCEDURE catedras.sp_consultar_catedra IS
    'Retrofit criterio 3. NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_catedra IS
    'Retrofit criterio 3. Devuelve la fila como quedó (id_catedra, nombre_asis y creado_en ya puestos).';
COMMENT ON PROCEDURE catedras.sp_actualizar_catedra IS
    'Retrofit criterio 3. Reemplazo completo (sin la columna generada nombre_asis); PATCH mezcla antes de llamar.';
COMMENT ON PROCEDURE catedras.sp_eliminar_catedra IS
    'Retrofit criterio 3. Borrado LÓGICO (activo=false): la tabla tiene activo.';
