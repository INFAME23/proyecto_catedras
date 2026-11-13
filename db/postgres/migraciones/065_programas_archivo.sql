-- ==========================================================================
--  db/postgres/migraciones/065_programas_archivo.sql
--  Proyecto Cátedras · SUBVERSIÓN v7.2 · RF-S8 · D69
-- --------------------------------------------------------------------------
--  Los códigos de programa del archivo de estudiantes (INDS, ADMO, PSIC, DERE,
--  ARQU, INGC, CONT, COMU, EDUC) no existían en el catálogo (que usa M0110,
--  M0286...). Se crean con ESE MISMO código para que el 'Programa' exportado
--  salga tal como se cargó (con un alias saldría el M0xxx).
--
--  ⚠ DATOS FICTICIOS: el archivo es de prueba y el equipo pidió (2026-11-12)
--  inventar nombre y facultad al azar. Todos llevan el sufijo "(mock)" para
--  reconocerlos y borrarlos fácil. Si llega el archivo real, se corrigen con
--  un UPDATE de nombre/facultad sin tocar los códigos.
--
--  Las facultades son las que ya existen en el catálogo (Regla 2: nada de
--  claves inventadas). Idempotente: ON CONFLICT (codigo) DO NOTHING.
-- ==========================================================================

SET search_path TO catedras, public;

INSERT INTO catedras.programa_academico (codigo, nombre, fk_facultad, nivel, activo) VALUES
    ('INDS', 'Ingenieria de Datos y Software (mock)', 'F_ING',     'PREGRADO', true),
    ('ADMO', 'Administracion de Empresas (mock)',     'F_CIENCIAS', 'PREGRADO', true),
    ('PSIC', 'Psicologia (mock)',                     'F_PSICO',    'PREGRADO', true),
    ('DERE', 'Derecho (mock)',                        'F_DERECHO',  'PREGRADO', true),
    ('ARQU', 'Arquitectura (mock)',                   'F_ARTES',    'PREGRADO', true),
    ('INGC', 'Ingenieria Civil (mock)',               'F_ING',      'PREGRADO', true),
    ('CONT', 'Contaduria Publica (mock)',             'F_CIENCIAS', 'PREGRADO', true),
    ('COMU', 'Comunicacion Social (mock)',            'F_ARTES',    'PREGRADO', true),
    ('EDUC', 'Licenciatura en Educacion (mock)',      'F_EDU',      'PREGRADO', true)
ON CONFLICT (codigo) DO NOTHING;
