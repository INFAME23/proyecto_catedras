-- ==========================================================================
--  db/postgres/migraciones/036_sp_clave_acceso.sql
--  Proyecto Cátedras · RETROFIT criterio 3 (VERSION_2.md §4) · Fase 5
--  Molde: 005_sp_sede_ejemplo.sql (ver PLAN_RETROFIT_SP.md, Grupo C).
--
--  'clave_acceso' ya tenía su escritura en la BD (script provisto):
--     SOLICITAR (crear)     -> fn_solicitar_clave_api -> sp_solicitar_clave
--     VALIDAR  (actualizar) -> fn_validar_clave_api   -> sp_validar_clave_y_registrar
--  NO se tocan ni se reemplazan. Este archivo agrega Listar y Consultar.
--
--  POR QUÉ NO HAY 'sp_actualizar_' NI 'sp_eliminar_' GENÉRICOS: la fila
--  guarda el hash de una clave de un solo uso (RN-34). Editarla a mano
--  (intentos, usado_en, expira_en) desarma el control de intentos y de
--  expiración; borrarla no hace falta porque expira sola.
--
--  AJUSTES AL MOLDE DE 'sede':
--   1) La proyección NO incluye 'clave_hash' (RNF12), igual que el SELECT
--      que tenía C#.
--   2) 'ip_solicitud' es inet: se proyecta con host() para que llegue
--      "10.0.0.1" y no "10.0.0.1/32".
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  LISTAR — filtro opcional por asistente, límite opcional. Sin clave_hash.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_listar_clave_acceso(
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
      FROM (SELECT id_clave, fk_asistente, fk_enlace, enviado_a,
                   es_correo_institucional, generado_en, expira_en,
                   usado_en, intentos, host(ip_solicitud) AS ip_solicitud
              FROM clave_acceso
             WHERE p_fk_asistente IS NULL OR fk_asistente = p_fk_asistente
             ORDER BY id_clave
             LIMIT p_limite) c;
END;
$$;

-- --------------------------------------------------------------------------
--  CONSULTAR — por id. NULL = no existe. Sin clave_hash.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_consultar_clave_acceso(
    IN    p_id_clave  bigint,
    INOUT p_resultado json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(c) INTO p_resultado
      FROM (SELECT id_clave, fk_asistente, fk_enlace, enviado_a,
                   es_correo_institucional, generado_en, expira_en,
                   usado_en, intentos, host(ip_solicitud) AS ip_solicitud
              FROM clave_acceso
             WHERE id_clave = p_id_clave) c;
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_clave_acceso IS
    'Retrofit criterio 3. Filtro opcional por asistente. Nunca incluye clave_hash.';
COMMENT ON PROCEDURE catedras.sp_consultar_clave_acceso IS
    'Retrofit criterio 3. NULL = no existe. Nunca incluye clave_hash.';
