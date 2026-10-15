-- ==========================================================================
--  db/postgres/migraciones/009_sp_alias_programa.sql
--  Proyecto Cátedras · RETROFIT criterio 3 de VERSION_2.md · 'alias_programa'
-- --------------------------------------------------------------------------
--  'sp_guardar_programa_academico' (migración 001) sigue siendo el Crear
--  MAESTRO-DETALLE (programa + alias iniciales) y NO se toca. Aquí se
--  agregan los 5 verbos de 'alias_programa' PARA EL ALTA SUELTA
--  (POST /api/AliasPrograma, un alias a un programa que ya existe), que
--  hoy no pasa por ningún procedimiento (PLAN_RETROFIT_SP.md §2, Grupo B).
--
--  'alias_programa' NO tiene columna 'activo' -> 'sp_eliminar_alias_programa'
--  es FÍSICO (DELETE). Por eso 'sp_listar_alias_programa' devuelve TODAS las
--  filas (no hay nada que filtrar).
--
--  'sp_actualizar_alias_programa' existe por el criterio de los 5 verbos,
--  pero IRepositorioAliasPrograma no tiene ActualizarAsync (el alias es
--  "sin PUT/PATCH" desde la v2): ninguna capa C# lo llama.
-- ==========================================================================

SET search_path TO catedras, public;

CREATE OR REPLACE PROCEDURE catedras.sp_listar_alias_programa(
    IN    p_limite    integer DEFAULT NULL,
    INOUT p_resultado json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(a), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_alias, texto_normalizado, fk_programa, origen, creado_en
              FROM alias_programa
             ORDER BY id_alias
             LIMIT p_limite) a;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_consultar_alias_programa(
    IN    p_id_alias  bigint,
    INOUT p_resultado json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(a) INTO p_resultado
      FROM (SELECT id_alias, texto_normalizado, fk_programa, origen, creado_en
              FROM alias_programa
             WHERE id_alias = p_id_alias) a;
END;
$$;

-- CREAR — 'id_alias' y 'creado_en' los pone la BD (identity y DEFAULT now()):
-- el procedimiento devuelve la fila tal como quedó, con ambos ya puestos
-- (el servicio los necesita para responder). Texto repetido (uq_alias_texto)
-- o programa inexistente (fk_alias_programa) -> NpgsqlException.
CREATE OR REPLACE PROCEDURE catedras.sp_crear_alias_programa(
    IN    p_texto_normalizado character varying(200),
    IN    p_fk_programa       character varying(5),
    IN    p_origen            character varying(30) DEFAULT 'FORMULARIO',
    INOUT p_resultado         json                  DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_id bigint;
BEGIN
    INSERT INTO alias_programa (texto_normalizado, fk_programa, origen)
    VALUES (p_texto_normalizado, p_fk_programa, COALESCE(p_origen, 'FORMULARIO'))
    RETURNING id_alias INTO v_id;

    SELECT row_to_json(a) INTO p_resultado
      FROM (SELECT id_alias, texto_normalizado, fk_programa, origen, creado_en
              FROM alias_programa
             WHERE id_alias = v_id) a;
END;
$$;

-- Reemplazo COMPLETO de las columnas de negocio. 'creado_en' NO cambia
-- (es el momento del alta, no de la última edición).
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_alias_programa(
    IN    p_id_alias          bigint,
    IN    p_texto_normalizado character varying(200),
    IN    p_fk_programa       character varying(5),
    IN    p_origen            character varying(30),
    INOUT p_resultado         json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE alias_programa
       SET texto_normalizado = p_texto_normalizado,
           fk_programa       = p_fk_programa,
           origen            = p_origen
     WHERE id_alias = p_id_alias;

    IF FOUND THEN
        SELECT row_to_json(a) INTO p_resultado
          FROM (SELECT id_alias, texto_normalizado, fk_programa, origen, creado_en
                  FROM alias_programa
                 WHERE id_alias = p_id_alias) a;
    END IF;
END;
$$;

-- FÍSICO: la tabla no tiene 'activo'.
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_alias_programa(
    IN    p_id_alias  bigint,
    INOUT p_resultado json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    DELETE FROM alias_programa
     WHERE id_alias = p_id_alias;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_alias_programa IS
    'Retrofit criterio 3 (VERSION_2.md §4). Todas las filas (sin activo), con límite opcional.';
COMMENT ON PROCEDURE catedras.sp_consultar_alias_programa IS
    'Retrofit criterio 3. NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_alias_programa IS
    'Retrofit criterio 3. Alta SUELTA de un alias; el alta junto al programa sigue siendo sp_guardar_programa_academico.';
COMMENT ON PROCEDURE catedras.sp_actualizar_alias_programa IS
    'Retrofit criterio 3. Reemplazo completo. Sin llamador en C# (el alias no tiene PUT/PATCH).';
COMMENT ON PROCEDURE catedras.sp_eliminar_alias_programa IS
    'Retrofit criterio 3. Borrado FÍSICO: la tabla no tiene activo.';
