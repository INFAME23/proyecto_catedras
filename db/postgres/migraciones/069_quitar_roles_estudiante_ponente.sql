-- ==========================================================================
--  db/postgres/migraciones/069_quitar_roles_estudiante_ponente.sql
--  Proyecto Cátedras · Catálogo de roles
-- --------------------------------------------------------------------------
--  Se QUITAN los roles ESTUDIANTE y PONENTE (los sembró la migración 046).
--  Desde la v7.1 · D55 estudiantes y ponentes NO tienen cuenta que entre: la
--  asistencia es pública (QR) y todo lo gestiona el personal. El catálogo
--  queda con ADMIN y ASISTENTE.
--
--  ⚠ NO se tocan otras cosas llamadas igual:
--     · ponencia.rol = 'PONENTE'  (quién da una sesión; chk_ponencia_rol).
--     · vinculacion_asistente.fk_tipo_vinculacion = 'ESTUDIANTE'  (tipo de persona).
--  Aquí solo se borra del catálogo 'rol' (roles de CUENTA).
--
--  ⚠ fk_rolusr_rol es ON DELETE RESTRICT: antes se borran las filas de
--  rol_por_usuario de esos roles (el historial de quien los tuvo).
--  ⚠ sp_provisionar_cuenta_estudiante (052) y la semilla 048 asignan el rol
--  ESTUDIANTE: con el rol borrado, volver a ejecutarlos fallaría por la FK. Ya
--  nada de la API los llama.
--
--  Idempotente. Con un volumen de PostgreSQL ya creado hay que aplicarla a mano.
-- ==========================================================================

SET search_path TO catedras, public;

DELETE FROM rol_por_usuario WHERE fk_rol IN ('ESTUDIANTE', 'PONENTE');
DELETE FROM rol             WHERE id_rol IN ('ESTUDIANTE', 'PONENTE');

RESET search_path;
