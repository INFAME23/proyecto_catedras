-- ==========================================================================
--  db/postgres/migraciones/046_roles_estudiante_ponente.sql
--  Proyecto Cátedras · SUBVERSIÓN v7.1 · RF-S3 (cuentas de estudiante y ponente)
-- --------------------------------------------------------------------------
--  El script provisto solo siembra tres roles: ADMIN, COORDINADOR y CONSULTA.
--  Para que un estudiante o un ponente pueda iniciar sesión con su propio
--  panel hacen falta dos códigos más en el catálogo 'rol'. Una cuenta
--  (usuario) queda ligada a su asistente; el rol se le asigna con una fila
--  de rol_por_usuario, igual que hoy se hace con ADMIN desde
--  /admin/rol-por-usuario.
--
--  Idempotente: ON CONFLICT DO NOTHING.
-- ==========================================================================

SET search_path TO catedras, public;

INSERT INTO catedras.rol (id_rol, nombre, descripcion) VALUES
    ('ESTUDIANTE', 'Estudiante', 'Ve sus sesiones y su información en el panel de estudiante'),
    ('PONENTE',    'Ponente',    'Ve su calendario de sesiones en el panel de ponente')
ON CONFLICT DO NOTHING;
