-- ==========================================================================
--  db/postgres/migraciones/028_sp_consentimiento_datos.sql
--  Proyecto Cátedras · RETROFIT criterio 3 (PLAN_RETROFIT_SP.md, Fase 4)
--  Tabla: 'consentimiento_datos' — HECHO LEGAL (Ley 1581 de 2012, RF113).
-- --------------------------------------------------------------------------
--  ⚠ LEER ANTES DE TOCAR LA API:
--  Se construyen los 5 procedimientos para cumplir el criterio 3 al pie de
--  la letra, pero SOLO 3 están conectados: listar, consultar y crear. Ni
--  IRepositorioConsentimientoDatos, ni el servicio, ni el controlador
--  tienen Actualizar/Eliminar, y NO se les agregan: un consentimiento ya
--  registrado es evidencia, y editarlo o borrarlo por API contradice por
--  qué existe la tabla. 'sp_actualizar_' y 'sp_eliminar_' quedan en la BD
--  para un uso administrativo excepcional (p. ej. una orden de supresión
--  del titular), fuera de la API, y su exposición la decide el equipo.
--
--  AJUSTES AL MOLDE DE 'sede':
--   1. 'ip' es INET. Al LEER se devuelve 'host(ip) AS ip' ("10.0.0.1"; un
--      'ip::text' daría "10.0.0.1/32"), igual que el repositorio de la v3.
--      Al ESCRIBIR entra como texto y se convierte con '::inet' dentro del
--      procedimiento: una IP mal escrita -> excepción (500), como antes.
--   2. Clave IDENTITY -> 'v_id' en 'sp_crear_'.
--   3. 'sp_eliminar_' es DELETE físico (sin 'activo').
--   4. 'sp_actualizar_' no cambia 'fk_asistente' ni 'aceptado_en'.
-- ==========================================================================

SET search_path TO catedras, public;

CREATE OR REPLACE PROCEDURE catedras.sp_listar_consentimiento_datos(
    IN    p_fk_asistente bigint  DEFAULT NULL,
    IN    p_limite       integer DEFAULT NULL,
    INOUT p_resultado    json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(c), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_consentimiento, fk_asistente, version_politica, aceptado_en,
                   host(ip) AS ip, canal
              FROM consentimiento_datos
             WHERE p_fk_asistente IS NULL OR fk_asistente = p_fk_asistente
             ORDER BY id_consentimiento
             LIMIT p_limite) c;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_consultar_consentimiento_datos(
    IN    p_id_consentimiento bigint,
    INOUT p_resultado         json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(c) INTO p_resultado
      FROM (SELECT id_consentimiento, fk_asistente, version_politica, aceptado_en,
                   host(ip) AS ip, canal
              FROM consentimiento_datos
             WHERE id_consentimiento = p_id_consentimiento) c;
END;
$$;

-- CREAR — misma versión ya aceptada por ese asistente (uq_consentimiento_ver),
-- asistente inexistente (FK) o IP mal escrita -> excepción del motor (500).
CREATE OR REPLACE PROCEDURE catedras.sp_crear_consentimiento_datos(
    IN    p_fk_asistente     bigint,
    IN    p_version_politica character varying(20),
    IN    p_canal            character varying(20) DEFAULT 'WEB',
    IN    p_ip               character varying(45) DEFAULT NULL,
    INOUT p_resultado        json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_id bigint;
BEGIN
    INSERT INTO consentimiento_datos (fk_asistente, version_politica, canal, ip)
    VALUES (p_fk_asistente, p_version_politica, COALESCE(p_canal, 'WEB'), p_ip::inet)
    RETURNING id_consentimiento INTO v_id;

    CALL catedras.sp_consultar_consentimiento_datos(v_id, p_resultado);
END;
$$;

-- ACTUALIZAR — ⚠ NO conectado a la API (ver cabecera).
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_consentimiento_datos(
    IN    p_id_consentimiento bigint,
    IN    p_version_politica  character varying(20),
    IN    p_canal             character varying(20),
    IN    p_ip                character varying(45),
    INOUT p_resultado         json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE consentimiento_datos
       SET version_politica = p_version_politica,
           canal            = p_canal,
           ip               = p_ip::inet
     WHERE id_consentimiento = p_id_consentimiento;

    IF FOUND THEN
        CALL catedras.sp_consultar_consentimiento_datos(p_id_consentimiento, p_resultado);
    END IF;
END;
$$;

-- ELIMINAR — FÍSICO (sin 'activo'). ⚠ NO conectado a la API (ver cabecera).
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_consentimiento_datos(
    IN    p_id_consentimiento bigint,
    INOUT p_resultado         json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    DELETE FROM consentimiento_datos WHERE id_consentimiento = p_id_consentimiento;

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_consentimiento_datos IS
    'Retrofit criterio 3. Filtro opcional por asistente; ip como texto (host).';
COMMENT ON PROCEDURE catedras.sp_consultar_consentimiento_datos IS
    'Retrofit criterio 3. NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_consentimiento_datos IS
    'Retrofit criterio 3. Registra el consentimiento (Ley 1581).';
COMMENT ON PROCEDURE catedras.sp_actualizar_consentimiento_datos IS
    'Retrofit criterio 3. NO expuesto por la API: el consentimiento es un hecho legal inmutable (RF113).';
COMMENT ON PROCEDURE catedras.sp_eliminar_consentimiento_datos IS
    'Retrofit criterio 3. Borrado FÍSICO. NO expuesto por la API: el consentimiento es un hecho legal inmutable (RF113).';
