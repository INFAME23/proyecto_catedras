-- ==========================================================================
--  db/postgres/migraciones/070_programas_reales_usb_medellin.sql
--  Proyecto Cátedras · Catálogo de programas académicos
-- --------------------------------------------------------------------------
--  Reemplaza los programas INVENTADOS por los programas REALES de la
--  Universidad de San Buenaventura que se ofrecen en el Valle de Aburrá
--  (sedes Medellín y Bello), según la oferta publicada en usbmed.edu.co
--  (https://usbmed.edu.co/programas/pregrados y /programas/posgrados,
--  consultadas el 2026-11-13).
--
--  QUÉ SE BORRA
--   · Los programas de relleno «Programa M0110», «Programa M0200»...: el CÓDIGO
--     venía del ASIS pero el NOMBRE era inventado.
--   · Los «(mock)» sin equivalente real: INDS (duplicado de M0286), INGC (Ingeniería
--     Civil), COMU (Comunicación Social) y EDUC (Licenciatura en Educación genérica);
--     la USB Medellín no ofrece esos programas.
--   · NO se borra un programa que tenga matrículas (programa_asistente) o registros
--     de asistencia que lo usen (FK RESTRICT): se conserva y queda para revisarlo a mano.
--
--  QUÉ SE DEJA / CREA
--   · Se conservan los códigos ARQU, DERE, PSIC, CONT, ADMO y M0286 y se les pone el
--     nombre real. ADMO queda como «Administración de Negocios» (el programa real).
--   · Los demás llevan un código INTERNO de 4 letras (no son códigos ASIS). Si llega
--     un archivo real con otros códigos, se corrigen con un UPDATE del código o con un
--     alias_programa, sin tocar los nombres.
--   · Sin los 2 programas de Armenia (Psicología – Armenia, Maestría en Educación:
--     Desarrollo Humano – Armenia): no son del Valle de Aburrá.
--   · Los posgrados «– FUCN» son los ofrecidos en convenio, tal como los publica la USB.
--   · La tecnología (Entrenamiento Deportivo) va como PREGRADO: el CHECK de nivel no
--     tiene un valor de tecnología.
--
--  Idempotente: se puede ejecutar varias veces. Con un volumen de PostgreSQL ya
--  creado hay que aplicarla a mano con psql.
-- ==========================================================================

SET search_path TO catedras, public;

-- 1) Fuera los inventados (solo los que nadie usa).
DELETE FROM programa_academico p
 WHERE (p.nombre LIKE 'Programa M%' OR p.codigo IN ('INDS', 'INGC', 'COMU', 'EDUC'))
   AND NOT EXISTS (SELECT 1 FROM programa_asistente   x WHERE x.fk_programa = p.codigo)
   AND NOT EXISTS (SELECT 1 FROM registro_asistencia  r WHERE r.fk_programa_snapshot = p.codigo);

-- 2) Los programas reales (si el código ya existe, se corrige nombre, facultad y nivel).
INSERT INTO programa_academico (codigo, nombre, fk_facultad, nivel, activo) VALUES
    -- Pregrado · Ciencias Empresariales
    ('ADMO', 'Administración de Negocios',                     'F_CIENCIAS', 'PREGRADO', true),
    ('CONT', 'Contaduría Pública',                             'F_CIENCIAS', 'PREGRADO', true),
    ('NEGI', 'Negocios Internacionales',                       'F_CIENCIAS', 'PREGRADO', true),
    -- Pregrado · Artes Integradas
    ('ARQU', 'Arquitectura',                                   'F_ARTES',    'PREGRADO', true),
    ('DMOD', 'Diseño de Modas',                                'F_ARTES',    'PREGRADO', true),
    ('DIND', 'Diseño Industrial',                              'F_ARTES',    'PREGRADO', true),
    -- Pregrado · Ingenierías
    ('CCDI', 'Creación de Contenidos Digitales',               'F_ING',      'PREGRADO', true),
    ('IAMB', 'Ingeniería Ambiental',                           'F_ING',      'PREGRADO', true),
    ('M0286', 'Ingeniería de Datos y Software',                'F_ING',      'PREGRADO', true),
    ('ISCI', 'Ingeniería de Sistemas Cibernéticos',            'F_ING',      'PREGRADO', true),
    ('ISON', 'Ingeniería de Sonido',                           'F_ING',      'PREGRADO', true),
    ('IIND', 'Ingeniería Industrial',                          'F_ING',      'PREGRADO', true),
    ('IMUL', 'Ingeniería Multimedia',                          'F_ING',      'PREGRADO', true),
    -- Pregrado · Derecho
    ('DERE', 'Derecho',                                        'F_DERECHO',  'PREGRADO', true),
    -- Pregrado · Psicología
    ('PSIC', 'Psicología',                                     'F_PSICO',    'PREGRADO', true),
    -- Pregrado · Educación
    ('LEFD', 'Licenciatura en Educación Física y Deporte',     'F_EDU',      'PREGRADO', true),
    ('LEIN', 'Licenciatura en Educación Infantil',             'F_EDU',      'PREGRADO', true),
    ('TEDE', 'Tecnología en Entrenamiento Deportivo',          'F_EDU',      'PREGRADO', true),
    -- Especializaciones
    ('EDPP', 'Especialización en Derecho Penal y Procesal Penal',                          'F_DERECHO',  'ESPECIALIZACION', true),
    ('ERSE', 'Especialización en Responsabilidad y Seguros',                               'F_DERECHO',  'ESPECIALIZACION', true),
    ('EDGE', 'Especialización en Dirección y Gestión Educativa',                           'F_EDU',      'ESPECIALIZACION', true),
    ('EFWE', 'Especialización en Fitness y Wellness',                                      'F_EDU',      'ESPECIALIZACION', true),
    ('EGEF', 'Especialización en Gerencia Educativa – FUCN',                               'F_EDU',      'ESPECIALIZACION', true),
    ('EDVI', 'Especialización en Diseño y Validación de Instrumentos de Medición',         'F_PSICO',    'ESPECIALIZACION', true),
    ('EPST', 'Especialización en Psicología de la Salud y la Seguridad en el Trabajo',     'F_PSICO',    'ESPECIALIZACION', true),
    ('EPOT', 'Especialización en Psicología de las Organizaciones y del Trabajo',          'F_PSICO',    'ESPECIALIZACION', true),
    ('EPOF', 'Especialización en Psicología de las Organizaciones y del Trabajo – FUCN',   'F_PSICO',    'ESPECIALIZACION', true),
    ('EPCP', 'Especialización en Psicología de los Cuidados Paliativos',                   'F_PSICO',    'ESPECIALIZACION', true),
    ('EGDT', 'Especialización en Gestión de Destinos Turísticos Innovadores',              'F_CIENCIAS', 'ESPECIALIZACION', true),
    ('EISP', 'Especialización en Interventoría y Supervisión de Proyectos y Obras Civiles','F_ARTES',    'ESPECIALIZACION', true),
    ('ESIG', 'Especialización en Sistemas de Información Geográfica',                      'F_ING',      'ESPECIALIZACION', true),
    -- Maestrías
    ('MBIO', 'Maestría en Bioclimática',                       'F_ARTES',    'MAESTRIA', true),
    ('MCRE', 'Maestría en Creatividad',                        'F_ARTES',    'MAESTRIA', true),
    ('MCED', 'Maestría en Ciencias de la Educación',           'F_EDU',      'MAESTRIA', true),
    ('MGEO', 'Maestría en Geoinformática',                     'F_ING',      'MAESTRIA', true),
    ('MNEU', 'Maestría en Neuropsicología',                    'F_PSICO',    'MAESTRIA', true),
    ('MPCL', 'Maestría en Psicología Clínica',                 'F_PSICO',    'MAESTRIA', true),
    -- Doctorados
    ('DCED', 'Doctorado en Ciencias de la Educación',          'F_EDU',      'DOCTORADO', true),
    ('DPSI', 'Doctorado en Psicología',                        'F_PSICO',    'DOCTORADO', true)
ON CONFLICT (codigo) DO UPDATE
   SET nombre = EXCLUDED.nombre, fk_facultad = EXCLUDED.fk_facultad, nivel = EXCLUDED.nivel, activo = true;

RESET search_path;
