-- ==========================================================================
--  db/postgres/migraciones/032_sp_detalle_migracion.sql
--  Proyecto Cátedras · RETROFIT criterio 3 (PLAN_RETROFIT_SP.md, Fase 4)
--  Tabla: 'detalle_migracion' (detalle de 'lote_migracion'). Contrato de la
--  API: version7/2_spec.md RF186-RF189 (borrador).
-- --------------------------------------------------------------------------
--  En la API: listar, consultar y ACTUALIZAR RESULTADO (PATCH). Sin POST:
--  un detalle nace solo dentro de sp_generar_lote_migracion (RF189), igual
--  que novedad_carga nace dentro del cierre de su lote. Sin DELETE.
--  'sp_crear_' y 'sp_eliminar_' existen (criterio 3) y NO se exponen.
--
--  AJUSTES AL MOLDE DE 'sede':
--   1. CHOQUE DE NOMBRES: la tabla tiene una columna 'resultado', y la
--      regla 4 del retrofit reserva 'p_resultado' para el INOUT json. El
--      parámetro de la COLUMNA se llama 'p_resultado_migracion'.
--   2. 'reunion_enviada' es SMALLINT, pero entra como 'integer': C# manda
--      'short' o 'int' según el modelo, e 'int4 -> int2' NO es un cast
--      implícito (un CALL con integer no encontraría el procedimiento).
--      Dentro, la asignación a la columna hace la conversión.
--   3. 'sp_actualizar_' reemplaza SOLO 'resultado' y 'mensaje'. Las 3
--      columnas '*_enviado(a)' son COPIA LITERAL de lo que salió en el
--      archivo (ver COMMENT ON COLUMN del script): no se reescriben nunca.
--      Marcar ACEPTADO una fila cuyo registro ya está ACEPTADO en otro lote
--      -> ux_detmig_aceptado (500, RF188).
--   4. Filtro opcional 'p_fk_lote_migracion' en listar.
--   5. 'sp_eliminar_' es DELETE físico (sin 'activo').
-- ==========================================================================

SET search_path TO catedras, public;

CREATE OR REPLACE PROCEDURE catedras.sp_listar_detalle_migracion(
    IN    p_fk_lote_migracion bigint  DEFAULT NULL,
    IN    p_limite            integer DEFAULT NULL,
    INOUT p_resultado         json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(d), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_detalle, fk_lote_migracion, fk_registro, id_asis_enviado,
                   programa_enviado, reunion_enviada, resultado, mensaje
              FROM detalle_migracion
             WHERE p_fk_lote_migracion IS NULL OR fk_lote_migracion = p_fk_lote_migracion
             ORDER BY fk_lote_migracion, id_detalle
             LIMIT p_limite) d;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_consultar_detalle_migracion(
    IN    p_id_detalle bigint,
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(d) INTO p_resultado
      FROM (SELECT id_detalle, fk_lote_migracion, fk_registro, id_asis_enviado,
                   programa_enviado, reunion_enviada, resultado, mensaje
              FROM detalle_migracion
             WHERE id_detalle = p_id_detalle) d;
END;
$$;

-- CREAR — NO expuesto por la API (RF189). Nace siempre 'PENDIENTE'.
-- Registro repetido en el mismo lote -> uq_detalle_lote_reg (500).
CREATE OR REPLACE PROCEDURE catedras.sp_crear_detalle_migracion(
    IN    p_fk_lote_migracion bigint,
    IN    p_fk_registro       bigint,
    IN    p_id_asis_enviado   character varying(15),
    IN    p_reunion_enviada   integer,
    IN    p_programa_enviado  character varying(5) DEFAULT NULL,
    INOUT p_resultado         json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_id bigint;
BEGIN
    INSERT INTO detalle_migracion (fk_lote_migracion, fk_registro, id_asis_enviado,
                                   programa_enviado, reunion_enviada)
    VALUES (p_fk_lote_migracion, p_fk_registro, p_id_asis_enviado,
            p_programa_enviado, p_reunion_enviada)
    RETURNING id_detalle INTO v_id;

    CALL catedras.sp_consultar_detalle_migracion(v_id, p_resultado);
END;
$$;

-- ACTUALIZAR — resultado + mensaje (ver ajustes 1 y 3).
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_detalle_migracion(
    IN    p_id_detalle           bigint,
    IN    p_resultado_migracion  character varying(12),
    IN    p_mensaje              character varying(300),
    INOUT p_resultado            json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE detalle_migracion
       SET resultado = p_resultado_migracion,
           mensaje   = p_mensaje
     WHERE id_detalle = p_id_detalle;

    IF FOUND THEN
        CALL catedras.sp_consultar_detalle_migracion(p_id_detalle, p_resultado);
    END IF;
END;
$$;

-- ELIMINAR — FÍSICO (sin 'activo'). NO expuesto por la API (RF189).
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_detalle_migracion(
    IN    p_id_detalle bigint,
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    DELETE FROM detalle_migracion WHERE id_detalle = p_id_detalle;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_detalle_migracion IS
    'Retrofit criterio 3. Filtro opcional por lote.';
COMMENT ON PROCEDURE catedras.sp_consultar_detalle_migracion IS
    'Retrofit criterio 3. NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_detalle_migracion IS
    'Retrofit criterio 3. NO expuesto por la API: los detalles nacen en sp_generar_lote_migracion (RF189).';
COMMENT ON PROCEDURE catedras.sp_actualizar_detalle_migracion IS
    'Retrofit criterio 3. Reemplaza resultado (p_resultado_migracion) y mensaje (RF188).';
COMMENT ON PROCEDURE catedras.sp_eliminar_detalle_migracion IS
    'Retrofit criterio 3. Borrado FÍSICO. NO expuesto por la API (RF189).';
