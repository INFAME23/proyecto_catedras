-- ==========================================================================
--  db/postgres/migraciones/071_lista_personas_con_rol.sql
--  Proyecto Cátedras · Lista de personas (admin)
-- --------------------------------------------------------------------------
--  La lista de personas (GET /api/Asistente) mostraba una etiqueta «Externo»
--  (asistente.es_externo). Ahora muestra el ROL de la persona, que tiene dos
--  orígenes y por eso se devuelven dos campos nuevos (solo lectura):
--
--    roles_cuenta   nombres de los roles VIGENTES de su cuenta de usuario
--                   (rol_por_usuario sin fecha_fin): Administrador, Asistente...
--                   [] si la persona no tiene cuenta.
--    vinculaciones  nombres de sus vinculaciones VIGENTES (vinculacion_asistente
--                   sin fecha_fin): Estudiante USB, Docente USB, Publico externo...
--
--  Se calculan en la misma consulta de la página (máx. 50 filas), sin una petición
--  por persona. Mismo procedimiento y mismos parámetros que en 024: solo se
--  agregan las dos columnas. Idempotente (CREATE OR REPLACE).
-- ==========================================================================

SET search_path TO catedras, public;

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
      FROM (SELECT s.id_asistente, s.id_asis, s.nombres, s.apellidos, s.nombre_completo_asis,
                   s.correo_institucional, s.correo_personal, s.celular,
                   s.es_externo, s.activo, s.creado_en, s.fk_lote_carga,
                   COALESCE((SELECT json_agg(r.nombre ORDER BY r.nombre)
                               FROM usuario u
                               JOIN rol_por_usuario ru ON ru.fk_usuario = u.id_usuario AND ru.fecha_fin IS NULL
                               JOIN rol r ON r.id_rol = ru.fk_rol
                              WHERE u.fk_asistente = s.id_asistente), '[]'::json) AS roles_cuenta,
                   COALESCE((SELECT json_agg(tv.nombre ORDER BY tv.nombre)
                               FROM vinculacion_asistente v
                               JOIN tipo_vinculacion tv ON tv.id_tipo_vinculacion = v.fk_tipo_vinculacion
                              WHERE v.fk_asistente = s.id_asistente AND v.fecha_fin IS NULL), '[]'::json) AS vinculaciones
              FROM asistente s
             WHERE s.activo
               AND (v_patron IS NULL
                    OR (s.nombres || ' ' || s.apellidos) ILIKE v_patron
                    OR s.id_asis ILIKE v_patron)
             ORDER BY s.id_asistente
             LIMIT p_limite OFFSET COALESCE(p_desplazamiento, 0)) a;
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_asistente IS
    'Solo activos; búsqueda ILIKE opcional y paginación (ADR D18). Trae roles_cuenta y vinculaciones vigentes (071).';

RESET search_path;
