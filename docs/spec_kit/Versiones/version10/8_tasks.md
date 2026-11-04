# Tareas — Versión 10: calendario público y solicitudes de ponentes

> ⚠️ **BORRADOR · por construir** — ver nota de [2_spec.md](2_spec.md). Las Fases
> 74–80 se construyen en paralelo con la v9 (Fases 69–73), en otra rama.

## Fase 74 — Preparación
- [ ] La v8 está fusionada en `main` y la rama de la v10 sale de ahí.
- [ ] Confirmar en la BD:
  - [ ] `btree_gist` y `citext` instalados;
  - [ ] los ids reales de `tipo_evento` (`char(4)`) y `dependencia`;
  - [ ] los periodos académicos cargados;
  - [ ] la firma de `sp_guardar_catedra` (042) y de `sp_guardar_asistente` (002).
- [ ] Corregir los documentos de `version10/` donde no coincidan.

## Fase 75 — Migración 061: franjas
- [ ] La tabla, el `EXCLUDE`, los 5 SP, `sp_plantilla_franjas` y `sp_calendario_franjas`.
- [ ] `prueba_sp_franjas_v10.sql`:
  - [ ] el solape se rechaza en la misma sede y se permite en otra;
  - [ ] la plantilla es idempotente;
  - [ ] no se borra una franja `OCUPADA`.

## Fase 76 — Migraciones 062 y 063: solicitudes
- [ ] `solicitud_catedra`, `perfil_ponente` y sus SP.
- [ ] `sp_aprobar_solicitud` y `sp_rechazar_solicitud`.
- [ ] `prueba_sp_solicitud_v10.sql`:
  - [ ] la carrera de dos solicitudes, con dos sesiones de psql;
  - [ ] aprobar con un ponente nuevo y con uno que ya existe;
  - [ ] aprobar con un `id_evento_asis` repetido → nada queda creado (todo o nada);
  - [ ] el rechazo libera la franja;
  - [ ] una solicitud no se resuelve dos veces;
  - [ ] el periodo inexistente (D80).

## Fase 77 — API
- [ ] Las capas de `FranjaDisponible` y de `SolicitudCatedra`, y su registro en `Program.cs`.
- [ ] Prueba de capas: bloque "V10 · Franjas y solicitudes".

## Fase 78 — Front público
- [ ] El blueprint `calendario`, las plantillas y `static/calendario.js`.
- [ ] El correo "solicitud recibida".

## Fase 79 — Front admin
- [ ] `/admin/franjas`, con la plantilla.
- [ ] `/admin/solicitudes`: la bandeja, el detalle y aprobar o rechazar, con sus correos.
- [ ] El aviso de pendientes en el panel.
- [ ] La sesión especial (RF247): ofrecer ocupar las franjas solapadas.

## Fase 80 — Cierre de la v10
- [ ] **D79:** si la v9 ya está en `main`, conectar el enlace de proyección en el
      correo de aprobación; si no, dejar la tarea anotada en la v9.
- [ ] `pruebas/verificar_v10.ps1` y `correr_todo_v10.ps1` en verde.
- [ ] README y `0_mapa_versiones.md` actualizados.
- [ ] Banner fuera, tag `v10` y PR a `main`.
