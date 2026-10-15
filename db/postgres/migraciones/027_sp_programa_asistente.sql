-- ==========================================================================
--  db/postgres/migraciones/027_sp_programa_asistente.sql
--  Proyecto Cátedras · RETROFIT criterio 3 (PLAN_RETROFIT_SP.md, Fase 4)
--  Tabla: 'programa_asistente' — LLAVE COMPUESTA (ADR D16).
-- --------------------------------------------------------------------------
--  AJUSTES AL MOLDE DE 'sede':
--   1. LLAVE DE 3 COLUMNAS (fk_asistente, fk_programa, fk_periodo).
--      'fk_periodo' es CHAR(6): 'text -> bpchar' SÍ es implícito, así que
--      el string que manda Npgsql encaja sin CAST.
--   2. ACTUALIZAR = CAMBIAR ESTADO. 'estado' es la única columna que no es
--      llave, así que "reemplazo completo" y "cambiar estado" son el mismo
--      UPDATE. 'RepositorioProgramaAsistentePostgres.CambiarEstadoAsync'
--      llama a este procedimiento (consulta + mezcla + llama, igual que
--      ActualizarAsync de sede — aquí la mezcla es trivial).
--   3. ELIMINAR = RETIRO LÓGICO, NO un DELETE. Sin columna 'activo' la
--      regla 3 diría "físico", pero esta tabla, igual que
--      vinculacion_asistente, se cierra en vez de borrarse (ADR D16): el
--      equivalente de "activo = false" es 'estado = RETIRADO' (uno de los 4
--      valores del CHECK). Ya RETIRADO -> eliminado=false.
--      ⚠ DECISIÓN A CONFIRMAR: la API NO lo expone (IRepositorioProgramaAsistente
--      no tiene EliminarAsync y no se le agrega); retirar sigue siendo
--      PATCH /cambiar-estado {"estado":"RETIRADO"}. Este procedimiento
--      existe para cumplir "los 5 verbos" sin inventar un borrado.
-- ==========================================================================

SET search_path TO catedras, public;

CREATE OR REPLACE PROCEDURE catedras.sp_listar_programa_asistente(
    IN    p_fk_asistente bigint  DEFAULT NULL,
    IN    p_limite       integer DEFAULT NULL,
    INOUT p_resultado    json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(p), '[]'::json)
      INTO p_resultado
      FROM (SELECT fk_asistente, fk_programa, fk_periodo, estado
              FROM programa_asistente
             WHERE p_fk_asistente IS NULL OR fk_asistente = p_fk_asistente
             ORDER BY fk_asistente, fk_periodo DESC, fk_programa
             LIMIT p_limite) p;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_consultar_programa_asistente(
    IN    p_fk_asistente bigint,
    IN    p_fk_programa  character varying(5),
    IN    p_fk_periodo   character(6),
    INOUT p_resultado    json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(p) INTO p_resultado
      FROM (SELECT fk_asistente, fk_programa, fk_periodo, estado
              FROM programa_asistente
             WHERE fk_asistente = p_fk_asistente
               AND fk_programa  = p_fk_programa
               AND fk_periodo   = p_fk_periodo) p;
END;
$$;

-- CREAR — sin estado -> 'ACTIVO' (DEFAULT de la columna). Llave repetida
-- (RF111) o programa/periodo/asistente inexistente -> excepción (500).
CREATE OR REPLACE PROCEDURE catedras.sp_crear_programa_asistente(
    IN    p_fk_asistente bigint,
    IN    p_fk_programa  character varying(5),
    IN    p_fk_periodo   character(6),
    IN    p_estado       character varying(15) DEFAULT 'ACTIVO',
    INOUT p_resultado    json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    INSERT INTO programa_asistente (fk_asistente, fk_programa, fk_periodo, estado)
    VALUES (p_fk_asistente, p_fk_programa, p_fk_periodo, COALESCE(p_estado, 'ACTIVO'));

    CALL catedras.sp_consultar_programa_asistente(
        p_fk_asistente, p_fk_programa, p_fk_periodo, p_resultado);
END;
$$;

-- ACTUALIZAR = CAMBIAR ESTADO (ver ajuste 2). Estado fuera de la lista ->
-- CHECK chk_programa_asistente_estado (la petición ya lo filtra con 422).
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_programa_asistente(
    IN    p_fk_asistente bigint,
    IN    p_fk_programa  character varying(5),
    IN    p_fk_periodo   character(6),
    IN    p_estado       character varying(15),
    INOUT p_resultado    json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE programa_asistente
       SET estado = p_estado
     WHERE fk_asistente = p_fk_asistente
       AND fk_programa  = p_fk_programa
       AND fk_periodo   = p_fk_periodo;

    IF FOUND THEN
        CALL catedras.sp_consultar_programa_asistente(
            p_fk_asistente, p_fk_programa, p_fk_periodo, p_resultado);
    END IF;
END;
$$;

-- ELIMINAR = RETIRO LÓGICO (ver ajuste 3). No expuesto por la API.
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_programa_asistente(
    IN    p_fk_asistente bigint,
    IN    p_fk_programa  character varying(5),
    IN    p_fk_periodo   character(6),
    INOUT p_resultado    json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE programa_asistente
       SET estado = 'RETIRADO'
     WHERE fk_asistente = p_fk_asistente
       AND fk_programa  = p_fk_programa
       AND fk_periodo   = p_fk_periodo
       AND estado <> 'RETIRADO';

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_programa_asistente IS
    'Retrofit criterio 3. Filtro opcional por asistente.';
COMMENT ON PROCEDURE catedras.sp_consultar_programa_asistente IS
    'Retrofit criterio 3. Llave compuesta (3 columnas). NULL = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_programa_asistente IS
    'Retrofit criterio 3. Matricula; estado por defecto ACTIVO.';
COMMENT ON PROCEDURE catedras.sp_actualizar_programa_asistente IS
    'Retrofit criterio 3. Reemplaza estado (= PATCH /cambiar-estado).';
COMMENT ON PROCEDURE catedras.sp_eliminar_programa_asistente IS
    'Retrofit criterio 3. ELIMINAR = retiro lógico (estado RETIRADO), nunca DELETE. NO expuesto por la API.';
