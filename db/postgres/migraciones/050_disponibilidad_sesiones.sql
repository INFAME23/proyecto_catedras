-- ==========================================================================
--  db/postgres/migraciones/050_disponibilidad_sesiones.sql
--  Proyecto Cátedras · SUBVERSIÓN v7.1 · D52 (disponibilidad de horario)
-- --------------------------------------------------------------------------
--  Para que un ponente vea si una fecha/hora está libre ANTES de crear una
--  cátedra, y para que no se crucen dos sesiones. Solo CONSULTAS (no crea ni
--  cambia datos):
--
--    sp_sesiones_en_conflicto(inicio, fin, sede, lugar, ponente, excluir_sesion)
--      Las sesiones que se TRASLAPAN con ese horario (inicio < fin_otra Y
--      fin > inicio_otra: terminar a las 10:00 y empezar a las 10:00 NO cruza)
--      y chocan por alguno de estos motivos:
--        LUGAR   misma sede FÍSICA y el mismo lugar (salón). Si el lugar de
--                alguna de las dos no está escrito, se cuenta como choque: es
--                lo prudente cuando no se sabe en qué salón es. Una sede
--                virtual (sede.es_virtual) no se revisa: no tiene salón.
--        PONENTE la persona indicada ya participa (en cualquier rol) en esa
--                otra sesión: nadie puede estar en dos sesiones a la vez.
--      Sin sede (NULL) solo se revisa el motivo PONENTE; sin ponente, solo
--      LUGAR. 'excluir_sesion' deja fuera una sesión (la que se está asignando).
--
--    sp_agenda_sesiones(desde, hasta, sede)
--      Todas las sesiones que tocan ese rango (p. ej. un día), con el nombre
--      de la cátedra y de la sede, para pintar la ocupación de una fecha.
--
--  No se tocan sp_guardar_catedra/sp_guardar_sesion del repositorio: la
--  regla la aplica la API/el front antes de guardar. Idempotente.
-- ==========================================================================

SET search_path TO catedras, public;

CREATE OR REPLACE PROCEDURE catedras.sp_sesiones_en_conflicto(
    IN    p_inicio         timestamptz,
    IN    p_fin            timestamptz,
    IN    p_fk_sede        text   DEFAULT NULL,
    IN    p_lugar          text   DEFAULT NULL,
    IN    p_fk_asistente   bigint DEFAULT NULL,
    IN    p_excluir_sesion bigint DEFAULT NULL,
    INOUT p_resultado      json   DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_sede_fisica boolean;
    v_lugar       text := lower(btrim(coalesce(p_lugar, '')));
BEGIN
    -- ¿La sede pedida tiene salones? (una sede virtual o desconocida no se revisa por lugar)
    SELECT NOT s.es_virtual INTO v_sede_fisica FROM sede s WHERE s.id_sede = p_fk_sede;
    v_sede_fisica := coalesce(v_sede_fisica, false);

    SELECT COALESCE(json_agg(r ORDER BY r.inicio, r.id_sesion), '[]'::json)
      INTO p_resultado
      FROM (
            SELECT s.id_sesion, s.fk_catedra, c.nombre AS nombre_catedra, s.numero_reunion,
                   s.inicio, s.fin, s.fk_sede, s.lugar, s.fk_modalidad, s.estado,
                   array_remove(ARRAY[
                       CASE WHEN v_sede_fisica
                             AND s.fk_sede = p_fk_sede
                             AND (v_lugar = ''
                                  OR btrim(coalesce(s.lugar, '')) = ''
                                  OR lower(btrim(s.lugar)) = v_lugar)
                            THEN 'LUGAR' END,
                       CASE WHEN p_fk_asistente IS NOT NULL
                             AND EXISTS (SELECT 1 FROM ponencia po
                                          WHERE po.fk_sesion = s.id_sesion AND po.fk_asistente = p_fk_asistente)
                            THEN 'PONENTE' END
                   ], NULL) AS motivos
              FROM sesion s
              JOIN catedra c ON c.id_catedra = s.fk_catedra
             WHERE s.inicio < p_fin
               AND s.fin    > p_inicio
               AND (p_excluir_sesion IS NULL OR s.id_sesion <> p_excluir_sesion)
           ) r
     WHERE cardinality(r.motivos) > 0;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_agenda_sesiones(
    IN    p_desde     timestamptz,
    IN    p_hasta     timestamptz,
    IN    p_fk_sede   text    DEFAULT NULL,
    IN    p_limite    integer DEFAULT 500,
    INOUT p_resultado json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(r), '[]'::json)
      INTO p_resultado
      FROM (SELECT s.id_sesion, s.fk_catedra, c.nombre AS nombre_catedra, s.numero_reunion,
                   s.inicio, s.fin, s.fk_sede, se.nombre AS nombre_sede, s.lugar, s.fk_modalidad, s.estado
              FROM sesion s
              JOIN catedra c  ON c.id_catedra = s.fk_catedra
              JOIN sede    se ON se.id_sede   = s.fk_sede
             WHERE s.inicio < p_hasta
               AND s.fin    > p_desde
               AND (p_fk_sede IS NULL OR s.fk_sede = p_fk_sede)
             ORDER BY s.inicio, s.id_sesion
             LIMIT p_limite) r;
END;
$$;

COMMENT ON PROCEDURE catedras.sp_sesiones_en_conflicto IS
    'v7.1. Sesiones que se traslapan con un horario y chocan por LUGAR (misma sede física y salón) o PONENTE. Solo lectura.';
COMMENT ON PROCEDURE catedras.sp_agenda_sesiones IS
    'v7.1. Sesiones que tocan un rango de fechas (opcionalmente de una sede). Solo lectura.';
