-- ==========================================================================
--  db/postgres/migraciones/061_registro_publico_columnas.sql
--  Proyecto Cátedras · SUBVERSIÓN v7.2 · RF-S8 / RF-S9 · D65, D66, D67
-- --------------------------------------------------------------------------
--  Prepara las tablas para el registro público SIN clave por correo (D60):
--
--   1) registro_asistencia: cómo se identificó quien registró
--      (identificado_por), su facultad (docentes), su procedencia (externos)
--      y la distancia a la sede (auditoría de la ubicación);
--   2) chk_registro_clave admite "identificado por carnet o declarado" además
--      de "con clave" y "MANUAL/IMPORTADO";
--   3) asistente.contacto_sin_correo: permite una persona sin correo SOLO
--      cuando lo marca un procedimiento de la v7.2 (estudiantes cargados por
--      archivo; docentes/externos que dan solo su celular);
--   4) DOCENTE deja de exigir programa (si no, RN-07 rechazaría a todo docente);
--   5) listar / consultar / crear de registro_asistencia devuelven las columnas
--      nuevas (misma firma que la 045).
--
--  'origen' NO se toca: sigue diciendo por qué canal entró (QR o ENLACE).
--
--  Idempotente (IF NOT EXISTS / DROP CONSTRAINT IF EXISTS / CREATE OR REPLACE).
--  Si conservas el volumen de PostgreSQL hay que aplicarla a mano.
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  1) registro_asistencia: columnas nuevas
--     Son HECHOS del día del registro (como fk_programa_snapshot): nunca se
--     actualizan después. Las filas anteriores quedan con 'CLAVE' (el flujo de
--     la v5/v7.1) y el resto en NULL.
-- --------------------------------------------------------------------------
ALTER TABLE catedras.registro_asistencia
    ADD COLUMN IF NOT EXISTS identificado_por     varchar(12) NOT NULL DEFAULT 'CLAVE',
    ADD COLUMN IF NOT EXISTS fk_facultad_snapshot varchar(10),
    ADD COLUMN IF NOT EXISTS procedencia          varchar(150),
    ADD COLUMN IF NOT EXISTS distancia_metros     numeric(9,1);

-- La facultad es una referencia al catálogo (Regla 2). Se agrega aparte porque
-- ADD COLUMN IF NOT EXISTS ... REFERENCES no repite la FK en una segunda corrida.
ALTER TABLE catedras.registro_asistencia
    DROP CONSTRAINT IF EXISTS fk_registro_facultad_snap;
ALTER TABLE catedras.registro_asistencia
    ADD CONSTRAINT fk_registro_facultad_snap FOREIGN KEY (fk_facultad_snapshot)
        REFERENCES catedras.facultad (id_facultad) ON UPDATE CASCADE ON DELETE RESTRICT;

ALTER TABLE catedras.registro_asistencia
    DROP CONSTRAINT IF EXISTS chk_registro_identificado,
    DROP CONSTRAINT IF EXISTS chk_registro_facultad_docente,
    DROP CONSTRAINT IF EXISTS chk_registro_procedencia_externo,
    DROP CONSTRAINT IF EXISTS chk_registro_distancia,
    DROP CONSTRAINT IF EXISTS chk_registro_clave;

ALTER TABLE catedras.registro_asistencia
    -- CLAVE = flujo de la v5/v7.1 · CARNET = estudiante por código · DECLARADO = docente/externo por formulario
    ADD CONSTRAINT chk_registro_identificado CHECK (identificado_por IN ('CLAVE', 'CARNET', 'DECLARADO')),
    -- la facultad solo la guardan los docentes; la procedencia, los externos
    ADD CONSTRAINT chk_registro_facultad_docente CHECK (fk_facultad_snapshot IS NULL OR fk_vinculacion_snapshot = 'DOCENTE'),
    ADD CONSTRAINT chk_registro_procedencia_externo CHECK (procedencia IS NULL OR fk_vinculacion_snapshot = 'EXTERNO'),
    -- la distancia solo existe si hubo ubicación, y no es negativa
    ADD CONSTRAINT chk_registro_distancia CHECK (distancia_metros IS NULL OR (latitud IS NOT NULL AND distancia_metros >= 0)),
    -- Reemplaza al de la v5: sin clave solo se admite si es MANUAL/IMPORTADO o
    -- si la persona se identificó por carnet / por formulario (v7.2).
    ADD CONSTRAINT chk_registro_clave CHECK (
        fk_clave IS NOT NULL
        OR origen IN ('MANUAL', 'IMPORTADO')
        OR identificado_por IN ('CARNET', 'DECLARADO'));

COMMENT ON COLUMN catedras.registro_asistencia.identificado_por IS
    'v7.2 D65. CLAVE = clave de un solo uso por correo (v5/v7.1, y filas anteriores); CARNET = estudiante por código; DECLARADO = docente o externo por formulario. No reemplaza a origen (que dice el canal: QR o ENLACE).';
COMMENT ON COLUMN catedras.registro_asistencia.fk_facultad_snapshot IS
    'v7.2 D66. INSTANTÁNEA: la facultad que declaró un DOCENTE el día de la cátedra. NULL para los demás. Nunca se actualiza.';
COMMENT ON COLUMN catedras.registro_asistencia.procedencia IS
    'v7.2 D66. INSTANTÁNEA: de dónde dijo venir un EXTERNO (texto libre). NULL para los demás. Nunca se actualiza.';
COMMENT ON COLUMN catedras.registro_asistencia.distancia_metros IS
    'v7.2 D66. Distancia en metros entre la ubicación enviada y la sede de la sesión, calculada por la BD (fn_distancia_metros). Solo en registros de canal QR; NULL en el resto.';

-- --------------------------------------------------------------------------
--  3) asistente.contacto_sin_correo (D67)
--     chk_asistente_correo exigía correo institucional o personal. El celular
--     no cuenta, así que un docente o externo que dé solo su celular lo
--     violaría. La marca la ponen SOLO los procedimientos de la v7.2: la API de
--     Asistente y la carga antigua no la tocan y siguen exigiendo un correo.
-- --------------------------------------------------------------------------
ALTER TABLE catedras.asistente
    ADD COLUMN IF NOT EXISTS contacto_sin_correo boolean NOT NULL DEFAULT false;

ALTER TABLE catedras.asistente
    DROP CONSTRAINT IF EXISTS chk_asistente_correo;

ALTER TABLE catedras.asistente
    ADD CONSTRAINT chk_asistente_correo CHECK (
        correo_institucional IS NOT NULL
        OR correo_personal IS NOT NULL
        OR contacto_sin_correo);

COMMENT ON COLUMN catedras.asistente.contacto_sin_correo IS
    'v7.2 D67. true = la persona se creó sin correo (estudiante cargado por archivo, o docente/externo que dio solo celular). Solo la ponen los procedimientos de la v7.2; la API de Asistente no la usa.';

-- --------------------------------------------------------------------------
--  4) DOCENTE no exige programa (D67, decidido el 2026-11-12)
--     El script provisto lo dejó en true; RN-07 (fn_trg_registro_validar)
--     rechaza el registro si la vinculación exige programa y no hay uno
--     activo. Un docente declara su FACULTAD, no un programa. Es el mecanismo
--     que documenta el propio script: "habilitar una excepción es un UPDATE".
--     Solo afecta registros NUEVOS (la instantánea de los anteriores no cambia).
-- --------------------------------------------------------------------------
UPDATE catedras.tipo_vinculacion
   SET exige_programa = false
 WHERE id_tipo_vinculacion = 'DOCENTE' AND exige_programa;

-- --------------------------------------------------------------------------
--  5) Listar / consultar / crear de registro_asistencia, con las columnas
--     nuevas (misma firma que la 045: CREATE OR REPLACE sin tocar la API).
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_listar_registro_asistencia(
    IN    p_fk_sesion    bigint  DEFAULT NULL,
    IN    p_fk_asistente bigint  DEFAULT NULL,
    IN    p_limite       integer DEFAULT NULL,
    INOUT p_resultado    json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(r), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_registro, fk_sesion, fk_asistente, fk_enlace, fk_clave,
                   registrado_en, fk_programa_snapshot, fk_vinculacion_snapshot,
                   origen, host(ip) AS ip, user_agent, latitud, longitud,
                   identificado_por, fk_facultad_snapshot, procedencia, distancia_metros
              FROM registro_asistencia
             WHERE (p_fk_sesion    IS NULL OR fk_sesion    = p_fk_sesion)
               AND (p_fk_asistente IS NULL OR fk_asistente = p_fk_asistente)
             ORDER BY id_registro
             LIMIT p_limite) r;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_consultar_registro_asistencia(
    IN    p_id_registro bigint,
    INOUT p_resultado   json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(r) INTO p_resultado
      FROM (SELECT id_registro, fk_sesion, fk_asistente, fk_enlace, fk_clave,
                   registrado_en, fk_programa_snapshot, fk_vinculacion_snapshot,
                   origen, host(ip) AS ip, user_agent, latitud, longitud,
                   identificado_por, fk_facultad_snapshot, procedencia, distancia_metros
              FROM registro_asistencia
             WHERE id_registro = p_id_registro) r;
END;
$$;

CREATE OR REPLACE PROCEDURE catedras.sp_crear_registro_asistencia(
    IN    p_fk_sesion    bigint,
    IN    p_fk_asistente bigint,
    IN    p_origen       character varying(15) DEFAULT 'QR',
    INOUT p_resultado    json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
DECLARE
    v_id bigint;
BEGIN
    INSERT INTO registro_asistencia (fk_sesion, fk_asistente, origen)
    VALUES (p_fk_sesion, p_fk_asistente, COALESCE(p_origen, 'QR'))
    RETURNING id_registro INTO v_id;

    CALL catedras.sp_consultar_registro_asistencia(v_id, p_resultado);
END;
$$;

-- sp_actualizar_registro_asistencia (037) NO cambia: las columnas nuevas son
-- parte del HECHO y no se corrigen después.
