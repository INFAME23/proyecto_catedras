-- ==========================================================================
--  db/postgres/migraciones/024_sp_asistente.sql
--  Proyecto Cátedras · RETROFIT criterio 3 (PLAN_RETROFIT_SP.md, Fase 4)
--  Tabla: 'asistente'. Molde: 005_sp_sede_ejemplo.sql.
-- --------------------------------------------------------------------------
--  AJUSTES AL MOLDE DE 'sede' (y por qué):
--   1. NO hay 'sp_crear_asistente': el CREAR ya es 'sp_guardar_asistente'
--      (migración 002, maestro + 4 detalles en una transacción). No se
--      reemplaza ni se duplica — un segundo procedimiento de alta sería
--      una puerta para crear asistentes SIN sus detalles por otro camino.
--   2. 'sp_listar_asistente' recibe además la búsqueda 'p_q' y el
--      desplazamiento (paginación), porque así lo pide el contrato de la v3
--      (ADR D18): el filtro corre en SQL, no en memoria. El patrón ILIKE
--      ('%texto%') ahora lo arma el procedimiento, no el repositorio.
--   3. Listar devuelve SOLO activos (igual que 'sede'): ahora que eliminar
--      es lógico, un asistente "eliminado" no debe volver a aparecer en la
--      lista. Consultar por id SÍ lo devuelve aunque esté inactivo.
--   4. Los correos entran como 'character varying(254)' (igual que en
--      'sp_guardar_asistente'): 'text -> citext' no es un cast implícito y
--      Npgsql manda los string como 'text'; al asignarlos a la columna
--      CITEXT, PostgreSQL hace la conversión de asignación sin problema.
--   5. ELIMINAR pasa de DELETE físico (con RESTRICT de usuario/ponencia/
--      registro_asistencia -> 500 y CASCADE de los 4 detalles) a LÓGICO
--      (activo = false): la tabla tiene 'activo' (regla 3 del retrofit).
--      Consecuencia: ya no se borran en cascada sus documentos,
--      vinculaciones, programas ni consentimientos — quedan como historia.
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  LISTAR — activos, con búsqueda opcional y paginación.
--  p_q NULL o en blanco = sin filtro. p_limite NULL = sin límite.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_listar_asistente(
    IN    p_q              text    DEFAULT NULL,
    IN    p_limite         integer DEFAULT NULL,
    IN    p_desplazamiento integer DEFAULT 0,
    INOUT p_resultado      json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    -- NULLIF(btrim(...), '') convierte '' y '   ' en NULL: "sin búsqueda".
    v_patron text := '%' || NULLIF(btrim(p_q), '') || '%';
BEGIN
    SELECT COALESCE(json_agg(a), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_asistente, id_asis, nombres, apellidos, nombre_completo_asis,
                   correo_institucional, correo_personal, celular,
                   es_externo, activo, creado_en, fk_lote_carga
              FROM asistente
             WHERE activo
               AND (v_patron IS NULL
                    OR (nombres || ' ' || apellidos) ILIKE v_patron
                    OR id_asis ILIKE v_patron)
             ORDER BY id_asistente
             LIMIT p_limite OFFSET COALESCE(p_desplazamiento, 0)) a;
END;
$$;

-- --------------------------------------------------------------------------
--  CONSULTAR — por id. NULL en p_resultado = no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_consultar_asistente(
    IN    p_id_asistente bigint,
    INOUT p_resultado    json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(a) INTO p_resultado
      FROM (SELECT id_asistente, id_asis, nombres, apellidos, nombre_completo_asis,
                   correo_institucional, correo_personal, celular,
                   es_externo, activo, creado_en, fk_lote_carga
              FROM asistente
             WHERE id_asistente = p_id_asistente) a;
END;
$$;

-- --------------------------------------------------------------------------
--  CREAR — no se define aquí: es 'sp_guardar_asistente' (migración 002).
-- --------------------------------------------------------------------------

-- --------------------------------------------------------------------------
--  ACTUALIZAR — reemplazo COMPLETO de las 9 columnas de negocio. NO toca
--  'id_asistente', 'creado_en' ni 'fk_lote_carga' (la historia de cómo
--  nació la fila — misma lista blanca que tenía el repositorio en la v3).
--  Los CHECK (chk_asistente_externo, chk_asistente_correo...) siguen
--  siendo la última muralla: ahora se evalúan sobre la fila YA mezclada.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_asistente(
    IN    p_id_asistente         bigint,
    IN    p_id_asis              character varying(15),
    IN    p_nombres              character varying(100),
    IN    p_apellidos            character varying(100),
    IN    p_nombre_completo_asis character varying(200),
    IN    p_correo_inst          character varying(254),
    IN    p_correo_pers          character varying(254),
    IN    p_celular              character varying(20),
    IN    p_es_externo           boolean,
    IN    p_activo               boolean,
    INOUT p_resultado            json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE asistente
       SET id_asis              = p_id_asis,
           nombres              = p_nombres,
           apellidos            = p_apellidos,
           nombre_completo_asis = p_nombre_completo_asis,
           correo_institucional = p_correo_inst,
           correo_personal      = p_correo_pers,
           celular              = p_celular,
           es_externo           = p_es_externo,
           activo               = p_activo
     WHERE id_asistente = p_id_asistente;

    IF FOUND THEN
        CALL catedras.sp_consultar_asistente(p_id_asistente, p_resultado);
    END IF;
END;
$$;

-- --------------------------------------------------------------------------
--  ELIMINAR — LÓGICO (la tabla tiene 'activo'). Ya inactivo = "no
--  encontrado" (no hay nada que retirar), igual criterio que 'sede'.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_asistente(
    IN    p_id_asistente bigint,
    INOUT p_resultado    json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE asistente
       SET activo = false
     WHERE id_asistente = p_id_asistente
       AND activo;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_asistente IS
    'Retrofit criterio 3. Solo activos; búsqueda ILIKE opcional y paginación (ADR D18).';
COMMENT ON PROCEDURE catedras.sp_consultar_asistente IS
    'Retrofit criterio 3. NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_actualizar_asistente IS
    'Retrofit criterio 3. Reemplazo completo; PATCH mezcla antes de llamar. El CREAR es sp_guardar_asistente (002).';
COMMENT ON PROCEDURE catedras.sp_eliminar_asistente IS
    'Retrofit criterio 3. Borrado LÓGICO (activo=false).';
