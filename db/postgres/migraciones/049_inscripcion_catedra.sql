-- ==========================================================================
--  db/postgres/migraciones/049_inscripcion_catedra.sql
--  Proyecto Cátedras · SUBVERSIÓN v7.1 · D50 (el estudiante se inscribe a una cátedra)
-- --------------------------------------------------------------------------
--  Hasta aquí el estudiante solo podía VER el catálogo; "inscribirse" no
--  existía en el modelo. Esta migración agrega la inscripción a una CÁTEDRA
--  (no a una sesión: el cupo y la asistencia siguen siendo de la sesión).
--
--    · UNA fila por (cátedra, persona). Cancelar no borra: pone estado
--      CANCELADA y fecha; volver a inscribirse la reactiva. Así queda el rastro.
--    · La inscripción NO es requisito para registrar la asistencia (v5): quien
--      ya se registra con su enlace/clave sigue pudiendo hacerlo. Es la lista
--      de "mis cátedras" del estudiante y de quiénes siguen cada cátedra.
--
--  Los procedimientos devuelven un ESTADO (texto) en vez de lanzar excepciones
--  para los casos de negocio (cátedra inactiva, ya inscrito...): la API los
--  traduce a 200/400/404 sin depender de un 500 (como 047).
--
--  Idempotente. Si conservas el volumen de PostgreSQL se aplica a mano.
-- ==========================================================================

SET search_path TO catedras, public;

CREATE TABLE IF NOT EXISTS catedras.inscripcion_catedra (
    id_inscripcion bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    fk_catedra     bigint NOT NULL REFERENCES catedras.catedra (id_catedra),
    fk_asistente   bigint NOT NULL REFERENCES catedras.asistente (id_asistente),
    estado         text   NOT NULL DEFAULT 'ACTIVA',
    inscrita_en    timestamptz NOT NULL DEFAULT now(),
    cancelada_en   timestamptz,
    CONSTRAINT uq_inscripcion_catedra UNIQUE (fk_catedra, fk_asistente),
    CONSTRAINT chk_inscripcion_estado CHECK (estado IN ('ACTIVA', 'CANCELADA')),
    CONSTRAINT chk_inscripcion_fechas CHECK (
        (estado = 'ACTIVA' AND cancelada_en IS NULL) OR (estado = 'CANCELADA' AND cancelada_en IS NOT NULL))
);

CREATE INDEX IF NOT EXISTS ix_inscripcion_asistente ON catedras.inscripcion_catedra (fk_asistente);

COMMENT ON TABLE catedras.inscripcion_catedra IS
    'v7.1. Inscripción de una persona a una cátedra. Cancelar conserva la fila (estado CANCELADA).';

-- --------------------------------------------------------------------------
--  INSCRIBIR  ->  INSCRITA | YA_INSCRITA | CATEDRA_NO_DISPONIBLE | PERSONA_NO_DISPONIBLE
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_inscribir_catedra(
    IN    p_fk_catedra   bigint,
    IN    p_fk_asistente bigint,
    INOUT p_resultado    text DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_estado text;
BEGIN
    IF NOT EXISTS (SELECT 1 FROM catedra WHERE id_catedra = p_fk_catedra AND activo) THEN
        p_resultado := 'CATEDRA_NO_DISPONIBLE';
        RETURN;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM asistente WHERE id_asistente = p_fk_asistente AND activo) THEN
        p_resultado := 'PERSONA_NO_DISPONIBLE';
        RETURN;
    END IF;

    SELECT estado INTO v_estado
      FROM inscripcion_catedra
     WHERE fk_catedra = p_fk_catedra AND fk_asistente = p_fk_asistente
       FOR UPDATE;

    IF NOT FOUND THEN
        INSERT INTO inscripcion_catedra (fk_catedra, fk_asistente) VALUES (p_fk_catedra, p_fk_asistente);
        p_resultado := 'INSCRITA';
    ELSIF v_estado = 'CANCELADA' THEN
        UPDATE inscripcion_catedra
           SET estado = 'ACTIVA', cancelada_en = NULL, inscrita_en = now()
         WHERE fk_catedra = p_fk_catedra AND fk_asistente = p_fk_asistente;
        p_resultado := 'INSCRITA';
    ELSE
        p_resultado := 'YA_INSCRITA';
    END IF;
END;
$$;

-- --------------------------------------------------------------------------
--  CANCELAR  ->  CANCELADA | NO_INSCRITA
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_cancelar_inscripcion_catedra(
    IN    p_fk_catedra   bigint,
    IN    p_fk_asistente bigint,
    INOUT p_resultado    text DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE inscripcion_catedra
       SET estado = 'CANCELADA', cancelada_en = now()
     WHERE fk_catedra = p_fk_catedra AND fk_asistente = p_fk_asistente AND estado = 'ACTIVA';

    p_resultado := CASE WHEN FOUND THEN 'CANCELADA' ELSE 'NO_INSCRITA' END;
END;
$$;

-- --------------------------------------------------------------------------
--  LISTAR  (filtros opcionales; arreglo JSON, '[]' si no hay)
--  Trae el nombre de la cátedra y de la persona para no pedirlos aparte.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_listar_inscripcion_catedra(
    IN    p_fk_asistente bigint  DEFAULT NULL,
    IN    p_fk_catedra   bigint  DEFAULT NULL,
    IN    p_solo_activas boolean DEFAULT true,
    IN    p_limite       integer DEFAULT NULL,
    INOUT p_resultado    json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(r), '[]'::json)
      INTO p_resultado
      FROM (SELECT i.id_inscripcion, i.fk_catedra, c.id_evento_asis, c.nombre AS nombre_catedra,
                   i.fk_asistente, a.id_asis, a.nombres, a.apellidos,
                   i.estado, i.inscrita_en, i.cancelada_en
              FROM inscripcion_catedra i
              JOIN catedra   c ON c.id_catedra   = i.fk_catedra
              JOIN asistente a ON a.id_asistente = i.fk_asistente
             WHERE (p_fk_asistente IS NULL OR i.fk_asistente = p_fk_asistente)
               AND (p_fk_catedra   IS NULL OR i.fk_catedra   = p_fk_catedra)
               AND (NOT COALESCE(p_solo_activas, true) OR i.estado = 'ACTIVA')
             ORDER BY i.inscrita_en DESC, i.id_inscripcion DESC
             LIMIT p_limite) r;
END;
$$;

COMMENT ON PROCEDURE catedras.sp_inscribir_catedra IS
    'v7.1. Inscribe (o reactiva) a una persona en una cátedra activa. Devuelve un estado, no lanza.';
COMMENT ON PROCEDURE catedras.sp_cancelar_inscripcion_catedra IS
    'v7.1. Cancela una inscripción ACTIVA. CANCELADA | NO_INSCRITA.';
COMMENT ON PROCEDURE catedras.sp_listar_inscripcion_catedra IS
    'v7.1. Inscripciones con nombre de cátedra y de persona; por defecto solo las ACTIVAS.';
