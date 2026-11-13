-- ==========================================================================
--  db/postgres/migraciones/066_exportacion_catedras.sql
--  Proyecto Cátedras · SUBVERSIÓN v7.2 · RF-S13 / RF-S14 · D70
-- --------------------------------------------------------------------------
--  Consultas de SOLO LECTURA para la exportación de fin de periodo
--  (/admin/exportar). El front arma los CSV y el ZIP; aquí solo se entregan
--  los datos, ya deduplicados y clasificados:
--
--    fn_exportar_catedras_periodo(periodo)
--        una fila por cátedra del periodo: id, título y cuántos estudiantes,
--        docentes y externos asistieron (más "otros", que no se exportan).
--    fn_exportar_asistentes_periodo(periodo, desde, limite)
--        las mismas cátedras con la lista de quienes asistieron, PAGINADA POR
--        CÁTEDRA (keyset por id_evento_asis) para que el ZIP no haga una
--        petición por cátedra.
--
--  REGLAS (D70):
--   · Clasificación por la instantánea de la VINCULACIÓN del registro
--     (ESTUDIANTE / DOCENTE / EXTERNO), nunca por asistente.es_externo (los
--     docentes nuevos también son es_externo = true, D67). Las asistencias de
--     otras vinculaciones (ADMINISTRATIVO, EGRESADO...) solo se cuentan como
--     "otros".
--   · Una cátedra pertenece a un periodo por sus sesiones: aparece en cada
--     periodo donde tiene al menos una sesión, y ahí solo cuentan los
--     registros de ESAS sesiones.
--   · Una persona aparece UNA vez por cátedra (y vinculación) aunque haya ido a
--     varias sesiones; de sus datos variables (programa, facultad, procedencia)
--     se toma el del registro más reciente.
--   · Cátedras incluidas: las ACTIVAS y las inactivas que tengan asistencias en
--     ese periodo (D59 desactivó miles de cátedras sin asistencias: sin este
--     criterio llenarían la lista de ceros).
--   · 'id_catedra' = catedra.id_evento_asis (9 dígitos, con ceros a la izquierda).
--
--  Idempotente (CREATE OR REPLACE). Si conservas el volumen de PostgreSQL hay
--  que aplicarla a mano.
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  1) Cátedras del periodo con sus conteos
-- --------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION catedras.fn_exportar_catedras_periodo(p_periodo character(6))
    RETURNS json
    LANGUAGE sql
    STABLE
    SET search_path TO 'catedras', 'public'
    AS $$
    SELECT COALESCE(json_agg(t ORDER BY t.id_catedra), '[]'::json)
      FROM (
        SELECT c.id_evento_asis AS id_catedra,
               c.nombre         AS titulo,
               c.activo,
               count(DISTINCT ra.fk_asistente) FILTER (WHERE ra.fk_vinculacion_snapshot = 'ESTUDIANTE') AS estudiantes,
               count(DISTINCT ra.fk_asistente) FILTER (WHERE ra.fk_vinculacion_snapshot = 'DOCENTE')    AS docentes,
               count(DISTINCT ra.fk_asistente) FILTER (WHERE ra.fk_vinculacion_snapshot = 'EXTERNO')    AS externos,
               count(DISTINCT ra.fk_asistente) FILTER (WHERE ra.fk_vinculacion_snapshot
                                                       NOT IN ('ESTUDIANTE', 'DOCENTE', 'EXTERNO'))     AS otros
          FROM catedra c
          JOIN sesion s ON s.fk_catedra = c.id_catedra AND s.fk_periodo = p_periodo
          LEFT JOIN registro_asistencia ra ON ra.fk_sesion = s.id_sesion
         GROUP BY c.id_catedra, c.id_evento_asis, c.nombre, c.activo
        HAVING c.activo OR count(ra.id_registro) > 0
      ) t;
$$;

COMMENT ON FUNCTION catedras.fn_exportar_catedras_periodo IS
    'v7.2 D70. Cátedras con al menos una sesión en el periodo (activas, o inactivas con asistencias) y cuántos estudiantes, docentes y externos asistieron; "otros" = otras vinculaciones, que no se exportan.';

-- --------------------------------------------------------------------------
--  2) Asistentes por cátedra, paginado por cátedra.
--     p_desde: último id_catedra (id_evento_asis) ya recibido; NULL = desde el
--     principio. p_limite: cuántas cátedras por página (tope 500).
--     Cada elemento de 'asistentes' trae TODOS los campos de los tres CSV; el
--     front elige las columnas según el tipo:
--       ESTUDIANTE -> cc, carnet, programa
--       DOCENTE    -> cc, nombre, contacto, facultad
--       EXTERNO    -> cc, nombre, contacto, procedencia
-- --------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION catedras.fn_exportar_asistentes_periodo(
    p_periodo character(6),
    p_desde   text    DEFAULT NULL,
    p_limite  integer DEFAULT 100
) RETURNS json
    LANGUAGE sql
    STABLE
    SET search_path TO 'catedras', 'public'
    AS $$
    WITH cats AS (
        SELECT c.id_catedra, c.id_evento_asis, c.nombre
          FROM catedra c
         WHERE (p_desde IS NULL OR c.id_evento_asis > p_desde)
           AND EXISTS (SELECT 1 FROM sesion s
                        WHERE s.fk_catedra = c.id_catedra AND s.fk_periodo = p_periodo)
           AND (c.activo
                OR EXISTS (SELECT 1 FROM sesion s
                             JOIN registro_asistencia ra ON ra.fk_sesion = s.id_sesion
                            WHERE s.fk_catedra = c.id_catedra AND s.fk_periodo = p_periodo))
         ORDER BY c.id_evento_asis
         LIMIT LEAST(GREATEST(COALESCE(p_limite, 100), 1), 500)
    ),
    filas AS (
        -- una fila por (cátedra, persona, vinculación): la del registro más reciente
        SELECT DISTINCT ON (cs.id_catedra, ra.fk_asistente, ra.fk_vinculacion_snapshot)
               cs.id_catedra,
               ra.fk_vinculacion_snapshot AS tipo,
               ra.fk_asistente,
               ra.fk_programa_snapshot,
               ra.fk_facultad_snapshot,
               ra.procedencia
          FROM cats cs
          JOIN sesion s ON s.fk_catedra = cs.id_catedra AND s.fk_periodo = p_periodo
          JOIN registro_asistencia ra ON ra.fk_sesion = s.id_sesion
               AND ra.fk_vinculacion_snapshot IN ('ESTUDIANTE', 'DOCENTE', 'EXTERNO')
         ORDER BY cs.id_catedra, ra.fk_asistente, ra.fk_vinculacion_snapshot, ra.registrado_en DESC
    )
    SELECT COALESCE(json_agg(
               json_build_object(
                   'id_catedra', cs.id_evento_asis,
                   'titulo',     cs.nombre,
                   'asistentes', COALESCE((
                       SELECT json_agg(
                                  json_build_object(
                                      'tipo',        f.tipo,
                                      'cc',          d.numero,
                                      'carnet',      a.id_asis,
                                      'nombre',      COALESCE(a.nombre_completo_asis,
                                                              btrim(a.nombres || ' ' || a.apellidos)),
                                      'contacto',    COALESCE(a.correo_institucional::text,
                                                              a.correo_personal::text,
                                                              a.celular),
                                      'programa',    f.fk_programa_snapshot,
                                      'facultad',    fa.nombre,
                                      'procedencia', f.procedencia)
                                  ORDER BY f.tipo, a.id_asis NULLS LAST, d.numero)
                         FROM filas f
                         JOIN asistente a ON a.id_asistente = f.fk_asistente
                         LEFT JOIN documento_asistente d ON d.fk_asistente = a.id_asistente AND d.vigente
                         LEFT JOIN facultad fa ON fa.id_facultad = f.fk_facultad_snapshot
                        WHERE f.id_catedra = cs.id_catedra), '[]'::json)
               ) ORDER BY cs.id_evento_asis), '[]'::json)
      FROM cats cs;
$$;

COMMENT ON FUNCTION catedras.fn_exportar_asistentes_periodo IS
    'v7.2 D70. Asistentes por cátedra del periodo, sin repetir persona (por cátedra y vinculación), paginado por id_evento_asis (p_desde, p_limite). Clasifica por la vinculación del registro.';
