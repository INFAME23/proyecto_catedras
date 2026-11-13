-- ==========================================================================
--  db/postgres/migraciones/068_rol_asistente_sin_coordinador_consulta.sql
--  Proyecto Cátedras · Catálogo de roles
-- --------------------------------------------------------------------------
--  1) Se QUITAN los roles COORDINADOR y CONSULTA. Nunca tuvieron una sola
--     pantalla ni permiso asociado (el front solo conoce ADMIN, ESTUDIANTE y
--     PONENTE) y el script base los sembraba solo como ejemplo.
--     ⚠ fk_rolusr_rol es ON DELETE RESTRICT: para poder borrar el rol hay que
--     borrar ANTES sus filas de rol_por_usuario (el historial de quien los tuvo).
--  2) Se CREA el rol ASISTENTE: el administrador lo asigna a una cuenta y esa
--     persona puede iniciar sesión y gestionar SOLO cátedras (agregar, editar
--     y borrar). Lo que puede hacer lo decide el front (la API no autoriza por
--     rol, ADR D28) y el login de la API ahora acepta ADMIN o ASISTENTE.
--
--  Idempotente: se puede ejecutar varias veces. Con un volumen de PostgreSQL ya
--  creado hay que aplicarla a mano con psql.
-- ==========================================================================

SET search_path TO catedras, public;

DELETE FROM rol_por_usuario WHERE fk_rol IN ('COORDINADOR', 'CONSULTA');
DELETE FROM rol             WHERE id_rol IN ('COORDINADOR', 'CONSULTA');

INSERT INTO rol (id_rol, nombre, descripcion) VALUES
    ('ASISTENTE', 'Asistente', 'Inicia sesión y gestiona cátedras: agregar, editar y borrar')
ON CONFLICT DO NOTHING;

RESET search_path;
