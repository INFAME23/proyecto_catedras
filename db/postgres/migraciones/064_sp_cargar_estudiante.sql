-- ==========================================================================
--  db/postgres/migraciones/064_sp_cargar_estudiante.sql
--  Proyecto Cátedras · SUBVERSIÓN v7.2 · RF-S8 · D67, D68
-- --------------------------------------------------------------------------
--  Carga de UN estudiante del archivo (CC, Carnet_estudiante, Programa) +
--  el periodo elegido en la pantalla. Es un UPSERT por carnet: volver a subir
--  el mismo archivo actualiza y NO duplica. 'sp_guardar_asistente' (002) es un
--  INSERT puro: repetir un id_asis lanzaba uq_asistente_asis.
--
--  Por fila, en una sola transacción:
--    1) persona: se busca por id_asis (= carnet); si no existe, se crea con
--       nombre provisional ('Estudiante <carnet>'), sin correo
--       (contacto_sin_correo = true) y no externa (D67). Si ya existía NO se
--       toca su nombre; si estaba inactiva, el archivo la reactiva.
--    2) documento CC: si ese número ya es de OTRA persona se rechaza la fila.
--       Si la persona tenía otro documento vigente, ese pasa a vigente = false
--       (no se borra: RN-05) y el nuevo queda vigente.
--    3) programa (P19): los OTROS programas ACTIVO de esa persona en ese mismo
--       periodo pasan a RETIRADO; el del archivo queda ACTIVO. Así hay uno solo
--       activo por persona y periodo y el 'snapshot' del registro de asistencia
--       (fn_trg_registro_validar) es determinista. Otros periodos no se tocan.
--    4) vinculación ESTUDIANTE: si no tiene una vigente, se crea con
--       fecha_ini = hoy; si la tiene, se conserva su fecha.
--
--  Responde p_accion = 'CREADO' o 'ACTUALIZADO' y el id de la persona.
--  Los rechazos (formato, programa o periodo inexistente, CC de otra persona)
--  son excepciones: el front guarda su texto como motivo de la novedad de esa
--  fila y sigue con las demás (carga parcial, D15).
--
--  Idempotente (CREATE OR REPLACE). Si conservas el volumen de PostgreSQL hay
--  que aplicarla a mano.
-- ==========================================================================

SET search_path TO catedras, public;

CREATE OR REPLACE PROCEDURE catedras.sp_cargar_estudiante(
    IN    p_id_asis        character varying(15),
    IN    p_documento      character varying(20),
    IN    p_programa       character varying(5),
    IN    p_periodo        character(6),
    IN    p_fk_lote_carga  bigint DEFAULT NULL,
    INOUT p_id_asistente   bigint DEFAULT NULL,
    INOUT p_accion         text   DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_dueno_doc bigint;
    v_doc_vigente boolean;
BEGIN
    -- 0) forma de los datos (mismos patrones que los CHECK de las tablas, con un mensaje claro)
    IF p_id_asis IS NULL OR p_id_asis !~ '^[0-9A-Za-z]{5,15}$' THEN
        RAISE EXCEPTION 'El carnet "%" no es válido: debe tener entre 5 y 15 letras o dígitos', p_id_asis
            USING ERRCODE = 'check_violation';
    END IF;
    IF p_documento IS NULL OR p_documento !~ '^[0-9A-Za-z-]{4,20}$' THEN
        RAISE EXCEPTION 'La CC "%" no es válida: debe tener entre 4 y 20 letras, dígitos o guiones', p_documento
            USING ERRCODE = 'check_violation';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM programa_academico WHERE codigo = p_programa) THEN
        RAISE EXCEPTION 'El programa "%" no existe en el catálogo', p_programa
            USING ERRCODE = 'foreign_key_violation';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM periodo_academico WHERE id_periodo = p_periodo) THEN
        RAISE EXCEPTION 'El periodo "%" no existe', p_periodo
            USING ERRCODE = 'foreign_key_violation';
    END IF;

    -- 1) la persona. FOR UPDATE: dos cargas simultáneas del mismo carnet se turnan.
    SELECT a.id_asistente INTO p_id_asistente
      FROM asistente a WHERE a.id_asis = p_id_asis
       FOR UPDATE;

    IF p_id_asistente IS NULL THEN
        INSERT INTO asistente (id_asis, nombres, apellidos, es_externo, contacto_sin_correo, fk_lote_carga)
        VALUES (p_id_asis, 'Estudiante ' || p_id_asis, '-', false, true, p_fk_lote_carga)
        RETURNING id_asistente INTO p_id_asistente;
        p_accion := 'CREADO';
    ELSE
        -- el archivo es la fuente de verdad: una persona inactiva que aparece vuelve a estar activa
        UPDATE asistente SET activo = true WHERE id_asistente = p_id_asistente AND NOT activo;
        p_accion := 'ACTUALIZADO';
    END IF;

    -- 2) documento CC
    SELECT d.fk_asistente, d.vigente INTO v_dueno_doc, v_doc_vigente
      FROM documento_asistente d
     WHERE d.fk_tipo_documento = 'CC' AND d.numero = p_documento;

    IF v_dueno_doc IS NOT NULL AND v_dueno_doc <> p_id_asistente THEN
        RAISE EXCEPTION 'La CC % ya pertenece a otra persona; no se carga el carnet %', p_documento, p_id_asis
            USING ERRCODE = 'unique_violation';
    END IF;

    IF v_dueno_doc IS NULL OR NOT v_doc_vigente THEN
        -- ux_documento_vigente: solo un documento vigente por persona. El actual se retira primero.
        UPDATE documento_asistente SET vigente = false
         WHERE fk_asistente = p_id_asistente AND vigente;

        IF v_dueno_doc IS NULL THEN
            INSERT INTO documento_asistente (fk_asistente, fk_tipo_documento, numero, vigente)
            VALUES (p_id_asistente, 'CC', p_documento, true);
        ELSE
            -- tenía ese mismo número pero ya no vigente: vuelve a serlo
            UPDATE documento_asistente SET vigente = true
             WHERE fk_asistente = p_id_asistente AND fk_tipo_documento = 'CC' AND numero = p_documento;
        END IF;
    END IF;

    -- 3) programa del periodo (P19): uno solo ACTIVO por persona y periodo
    UPDATE programa_asistente SET estado = 'RETIRADO'
     WHERE fk_asistente = p_id_asistente AND fk_periodo = p_periodo
       AND estado = 'ACTIVO' AND fk_programa <> p_programa;

    INSERT INTO programa_asistente (fk_asistente, fk_programa, fk_periodo, estado)
    VALUES (p_id_asistente, p_programa, p_periodo, 'ACTIVO')
    ON CONFLICT (fk_asistente, fk_programa, fk_periodo) DO UPDATE SET estado = 'ACTIVO';

    -- 4) vinculación ESTUDIANTE vigente
    IF NOT EXISTS (SELECT 1 FROM vinculacion_asistente v
                    WHERE v.fk_asistente = p_id_asistente
                      AND v.fk_tipo_vinculacion = 'ESTUDIANTE' AND v.fecha_fin IS NULL) THEN
        INSERT INTO vinculacion_asistente (fk_asistente, fk_tipo_vinculacion, fecha_ini)
        VALUES (p_id_asistente, 'ESTUDIANTE', CURRENT_DATE)
        ON CONFLICT (fk_asistente, fk_tipo_vinculacion, fecha_ini) DO UPDATE SET fecha_fin = NULL;
    END IF;
END;
$$;

COMMENT ON PROCEDURE catedras.sp_cargar_estudiante IS
    'v7.2 D68. Upsert de un estudiante por carnet (CC + programa + periodo). Idempotente. Retira los otros programas ACTIVO del mismo periodo (P19). Devuelve p_id_asistente y p_accion (CREADO / ACTUALIZADO).';
