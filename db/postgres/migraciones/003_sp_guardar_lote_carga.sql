-- ==========================================================================
--  db/postgres/migraciones/003_sp_guardar_lote_carga.sql
--  Proyecto Cátedras · Versión 3 — Guía IA, Regla 1 (maestro-detalle)
-- --------------------------------------------------------------------------
--  Maestro: 'lote_carga_asistente'. Detalle: 'novedad_carga'.
--
--  CIERRA un lote de carga: guarda los 3 contadores finales y, en la MISMA
--  transacción, una fila de 'novedad_carga' por cada fila del archivo que
--  la carga rechazó (con su motivo). Ver ADR D15 (version3/4_research.md):
--  la carga en sí es PARCIAL (cada asistente es su propia transacción),
--  pero el CIERRE es todo-o-nada: los contadores y las novedades son una
--  sola foto del resultado.
--
--  El CHECK chk_lote_carga_filas (aceptadas + rechazadas <= leidas) es la
--  última muralla; el servicio de la API ya lo valida antes (400).
--
--  CÓMO SE APLICA: igual que 002_... (prefijo 'z003_' en initdb, o a mano
--  con psql sobre una BD que ya existe). 'CREATE OR REPLACE': re-ejecutable.
-- ==========================================================================

SET search_path TO catedras, public;

CREATE OR REPLACE PROCEDURE catedras.sp_guardar_lote_carga(
    IN p_id_lote          bigint,
    IN p_filas_leidas     integer,
    IN p_filas_aceptadas  integer,
    IN p_filas_rechazadas integer,
    IN p_observaciones    text  DEFAULT NULL,
    -- Detalle: arreglo JSON, p. ej.
    --   [{"numero_fila":2, "contenido_crudo":"...", "motivo":"correo duplicado"}]
    -- NULL o [] = ninguna fila rechazada.
    IN p_novedades        jsonb DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    -- 1) Maestro: los contadores finales del lote.
    UPDATE lote_carga_asistente
       SET filas_leidas     = p_filas_leidas,
           filas_aceptadas  = p_filas_aceptadas,
           filas_rechazadas = p_filas_rechazadas,
           observaciones    = p_observaciones
     WHERE id_lote_carga = p_id_lote;

    -- 'FOUND' es una variable de PL/pgSQL: false si el UPDATE no tocó
    -- ninguna fila. El servicio ya responde 404 antes de llegar aquí; esto
    -- es solo la defensa del propio SP si alguien lo llama directo.
    IF NOT FOUND THEN
        RAISE EXCEPTION 'No existe el lote de carga %', p_id_lote
            USING ERRCODE = 'no_data_found';
    END IF;

    -- 2) Detalle: una novedad por fila rechazada. 'fk_lote_carga' NO viene
    --    en el JSON: es siempre el lote que se está cerrando.
    IF p_novedades IS NOT NULL AND jsonb_array_length(p_novedades) > 0 THEN
        INSERT INTO novedad_carga (fk_lote_carga, numero_fila, contenido_crudo, motivo)
        SELECT p_id_lote,
               (e ->> 'numero_fila')::integer,
               e ->> 'contenido_crudo',
               e ->> 'motivo'
          FROM jsonb_array_elements(p_novedades) AS e;
    END IF;

    -- Si el paso 2 falla (numero_fila <= 0, motivo de más de 200
    -- caracteres...), PostgreSQL revierte TODO: los contadores del paso 1
    -- tampoco quedan guardados y el lote sigue "abierto".
END;
$$;

COMMENT ON PROCEDURE catedras.sp_guardar_lote_carga IS
    'Guía IA · Regla 1. Cierra un lote de carga: contadores finales + '
    'novedades (filas rechazadas) en una sola transacción (V3, ADR D15).';
