-- ==========================================================================
--  db/postgres/migraciones/072_quitar_modalidad_hibrida.sql
--  Proyecto Cátedras · Catálogo de modalidades
-- --------------------------------------------------------------------------
--  Se BORRA la modalidad HIBRIDA. La 062 ya la había desactivado (P1/P1b: no se
--  usa) pero la dejaba en el catálogo; ahora desaparece del todo.
--
--  Solo se borra si NADIE la usa: 'sesion' y 'solicitud_ponente' tienen FK a
--  modalidad. Si en alguna base hay una sesión o una solicitud con HIBRIDA, la
--  fila se conserva (desactivada) y hay que cambiarlas a mano antes de volver a
--  ejecutar esta migración.
--
--  Idempotente. Con un volumen de PostgreSQL ya creado hay que aplicarla a mano.
-- ==========================================================================

SET search_path TO catedras, public;

DELETE FROM modalidad m
 WHERE m.id_modalidad = 'HIBRIDA'
   AND NOT EXISTS (SELECT 1 FROM sesion            s WHERE s.fk_modalidad = m.id_modalidad)
   AND NOT EXISTS (SELECT 1 FROM solicitud_ponente p WHERE p.fk_modalidad = m.id_modalidad);

-- Si no se pudo borrar porque la usan, al menos que no se ofrezca.
UPDATE modalidad SET activo = false WHERE id_modalidad = 'HIBRIDA' AND activo;

RESET search_path;
