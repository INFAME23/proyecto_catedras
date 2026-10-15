-- ==========================================================================
--  db/postgres/migraciones/011_sp_sesion.sql
--  Proyecto Cátedras · RETROFIT criterio 3 de VERSION_2.md · 'sesion'
-- --------------------------------------------------------------------------
--  'sesion' NO tiene columna 'activo' -> 'sp_eliminar_sesion' es FÍSICO
--  (DELETE). Por eso 'sp_listar_sesion' devuelve todas las filas.
--
--  TRIGGER trg_sesion_consecutivo (BEFORE INSERT): si 'numero_reunion' llega
--  NULL o 0, el disparador PROPONE el máximo de la cátedra + 1. Por eso
--  'sp_crear_sesion':
--    · acepta 'p_numero_reunion' NULL (o 0, que es lo que manda el servicio
--      cuando el cliente no lo indicó) y lo pasa tal cual al INSERT;
--    · recoge 'id_sesion' con RETURNING y devuelve la fila RELEÍDA después
--      del INSERT, nunca armada con los parámetros: así el JSON trae el
--      'numero_reunion' que calculó el trigger y el 'creado_en' de la BD.
--
--  ORDEN DE LOS PARÁMETROS de 'sp_crear_sesion': PostgreSQL exige que todo
--  parámetro IN después de uno con DEFAULT también lo tenga, así que van
--  primero los 6 obligatorios y después los opcionales (con DEFAULT).
-- ==========================================================================

SET search_path TO catedras, public;

-- LISTAR — todas, con filtro OPCIONAL por cátedra (NULL = todas) y límite
-- OPCIONAL. Mismo orden de siempre: por cátedra y número de reunión.
CREATE OR REPLACE PROCEDURE catedras.sp_listar_sesion(
    IN    p_fk_catedra bigint  DEFAULT NULL,
    IN    p_limite     integer DEFAULT NULL,
    INOUT p_resultado  json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(s), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_sesion, fk_catedra, numero_reunion, numero_reunion_asis,
                   titulo, inicio, fin, fk_modalidad, fk_sede, fk_periodo,
                   fk_encuesta, lugar, enlace_virtual, cupo, estado, creado_en
              FROM sesion
             WHERE p_fk_catedra IS NULL OR fk_catedra = p_fk_catedra
             ORDER BY fk_catedra, numero_reunion
             LIMIT p_limite) s;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_consultar_sesion(
    IN    p_id_sesion bigint,
    INOUT p_resultado json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(s) INTO p_resultado
      FROM (SELECT id_sesion, fk_catedra, numero_reunion, numero_reunion_asis,
                   titulo, inicio, fin, fk_modalidad, fk_sede, fk_periodo,
                   fk_encuesta, lugar, enlace_virtual, cupo, estado, creado_en
              FROM sesion
             WHERE id_sesion = p_id_sesion) s;
END;
$$;

-- Rangos inválidos (chk_sesion_fechas, chk_sesion_cupo, chk_sesion_estado),
-- reunión repetida en la cátedra (uq_sesion_reunion) o FK inexistente ->
-- NpgsqlException.
CREATE OR REPLACE PROCEDURE catedras.sp_crear_sesion(
    IN    p_fk_catedra          bigint,
    IN    p_inicio              timestamp with time zone,
    IN    p_fin                 timestamp with time zone,
    IN    p_fk_modalidad        character varying(15),
    IN    p_fk_sede             character varying(15),
    IN    p_fk_periodo          character(6),
    IN    p_numero_reunion      smallint                 DEFAULT NULL,
    IN    p_numero_reunion_asis smallint                 DEFAULT NULL,
    IN    p_titulo              character varying(200)   DEFAULT NULL,
    IN    p_fk_encuesta         bigint                   DEFAULT NULL,
    IN    p_lugar               character varying(200)   DEFAULT NULL,
    IN    p_enlace_virtual      character varying(300)   DEFAULT NULL,
    IN    p_cupo                integer                  DEFAULT NULL,
    IN    p_estado              character varying(15)    DEFAULT 'PROGRAMADA',
    INOUT p_resultado           json                     DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_id bigint;
BEGIN
    INSERT INTO sesion (fk_catedra, numero_reunion, numero_reunion_asis, titulo,
                        inicio, fin, fk_modalidad, fk_sede, fk_periodo,
                        fk_encuesta, lugar, enlace_virtual, cupo, estado)
    VALUES (p_fk_catedra, p_numero_reunion, p_numero_reunion_asis, p_titulo,
            p_inicio, p_fin, p_fk_modalidad, p_fk_sede, p_fk_periodo,
            p_fk_encuesta, p_lugar, p_enlace_virtual, p_cupo, p_estado)
    RETURNING id_sesion INTO v_id;

    -- Relectura DESPUÉS del INSERT (y de su trigger): trae el
    -- numero_reunion calculado y el creado_en.
    SELECT row_to_json(s) INTO p_resultado
      FROM (SELECT id_sesion, fk_catedra, numero_reunion, numero_reunion_asis,
                   titulo, inicio, fin, fk_modalidad, fk_sede, fk_periodo,
                   fk_encuesta, lugar, enlace_virtual, cupo, estado, creado_en
              FROM sesion
             WHERE id_sesion = v_id) s;
END;
$$;

-- Reemplazo COMPLETO. Aquí 'numero_reunion' es obligatorio y NO se
-- recalcula: el trigger es BEFORE INSERT, no actúa en el UPDATE.
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_sesion(
    IN    p_id_sesion           bigint,
    IN    p_fk_catedra          bigint,
    IN    p_numero_reunion      smallint,
    IN    p_numero_reunion_asis smallint,
    IN    p_titulo              character varying(200),
    IN    p_inicio              timestamp with time zone,
    IN    p_fin                 timestamp with time zone,
    IN    p_fk_modalidad        character varying(15),
    IN    p_fk_sede             character varying(15),
    IN    p_fk_periodo          character(6),
    IN    p_fk_encuesta         bigint,
    IN    p_lugar               character varying(200),
    IN    p_enlace_virtual      character varying(300),
    IN    p_cupo                integer,
    IN    p_estado              character varying(15),
    INOUT p_resultado           json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE sesion
       SET fk_catedra          = p_fk_catedra,
           numero_reunion      = p_numero_reunion,
           numero_reunion_asis = p_numero_reunion_asis,
           titulo              = p_titulo,
           inicio              = p_inicio,
           fin                 = p_fin,
           fk_modalidad        = p_fk_modalidad,
           fk_sede             = p_fk_sede,
           fk_periodo          = p_fk_periodo,
           fk_encuesta         = p_fk_encuesta,
           lugar               = p_lugar,
           enlace_virtual      = p_enlace_virtual,
           cupo                = p_cupo,
           estado              = p_estado
     WHERE id_sesion = p_id_sesion;

    IF FOUND THEN
        SELECT row_to_json(s) INTO p_resultado
          FROM (SELECT id_sesion, fk_catedra, numero_reunion, numero_reunion_asis,
                       titulo, inicio, fin, fk_modalidad, fk_sede, fk_periodo,
                       fk_encuesta, lugar, enlace_virtual, cupo, estado, creado_en
                  FROM sesion
                 WHERE id_sesion = p_id_sesion) s;
    END IF;
END;
$$;

-- FÍSICO: la tabla no tiene 'activo'. Las ponencias y los enlaces de
-- registro de la sesión caen en cascada (fk_ponencia_sesion,
-- fk_enlace_sesion); los registros de asistencia y los lotes de migración la
-- bloquean (fk_registro_sesion, fk_lotemig_sesion, ON DELETE RESTRICT) y
-- PostgreSQL lanza NpgsqlException, igual que con el DELETE de antes.
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_sesion(
    IN    p_id_sesion bigint,
    INOUT p_resultado json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    DELETE FROM sesion
     WHERE id_sesion = p_id_sesion;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_sesion IS
    'Retrofit criterio 3 (VERSION_2.md §4). Todas (sin activo), filtro opcional por cátedra y límite opcional.';
COMMENT ON PROCEDURE catedras.sp_consultar_sesion IS
    'Retrofit criterio 3. NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_sesion IS
    'Retrofit criterio 3. Devuelve la fila RELEÍDA tras el INSERT: trae el numero_reunion que calcula trg_sesion_consecutivo.';
COMMENT ON PROCEDURE catedras.sp_actualizar_sesion IS
    'Retrofit criterio 3. Reemplazo completo; PATCH mezcla antes de llamar.';
COMMENT ON PROCEDURE catedras.sp_eliminar_sesion IS
    'Retrofit criterio 3. Borrado FÍSICO: la tabla no tiene activo.';
