# Tareas — Versión 8: registro de asistencia sin login, por tipo de usuario

> ⚠️ **BORRADOR · por construir** — ver nota de [2_spec.md](2_spec.md). Una
> casilla se marca solo con la verificación ejecutada y anotada (comando y salida
> resumida). La numeración de fases continúa la de la
> [v7](../version7/8_tasks.md), que terminó en la Fase 61.
> Guía con prompts: [guia_ia_v8.md](guia_ia_v8.md).

---

## Fase 62 — Preparación: revisar el borrador contra la BD

- [ ] Tag `v7` creado (o el equipo confirma que se trabaja sin él).
- [ ] Contra la BD (script + 001–053, en el orden de initdb), confirmar:
  - [ ] `chk_registro_clave`, `uq_registro_sesion_asis`, `chk_asistente_correo`,
        `chk_documento_numero` y `chk_asistente_idasis` (D59, D71).
  - [ ] Las columnas reales de `rol_por_usuario` y `vinculacion_asistente`
        (nombres que usa la migración 057).
  - [ ] Que `fn_resolver_asistente` busque por `id_asis` y por documento.
  - [ ] Cuántos asistentes activos no tienen ningún correo (D71).
  - [ ] Los ids de facultad reales para los ejemplos.
- [ ] Corregir los documentos de `version8/` donde no coincidan.

## Fase 63 — Migraciones 054 y 055

- [ ] `054_tipos_registro_v8.sql`: `EXTERNO`, `DOCENTE` sin programa y las columnas de D72.
- [ ] `055_geocerca_sede.sql`: columnas de la sede, el parámetro, `fn_distancia_metros`
      y los SP de sede con las columnas nuevas.
- [ ] Montarlas en `docker-compose.yml` (`z054_`, `z055_`).
- [ ] **Verificación:** `fn_distancia_metros(6.2297,-75.5869, 6.2306,-75.5869)` ≈ 100.

## Fase 64 — Migración 056: procedimientos de registro

- [ ] `sp_contexto_registro` y `sp_validar_ubicacion_registro`.
- [ ] `sp_registrar_estudiante` + `fn_registrar_estudiante_api`.
- [ ] `sp_registrar_docente` + `fn_registrar_docente_api`.
- [ ] `sp_registrar_externo` + `fn_registrar_externo_api`.
- [ ] `db/postgres/pruebas/prueba_sp_registro_v8.sql` con al menos:
  - [ ] un estudiante que se registra y uno con ID inexistente;
  - [ ] el duplicado;
  - [ ] la presencial sin ubicación y la presencial fuera del radio;
  - [ ] la virtual sin ubicación;
  - [ ] un docente nuevo y el mismo docente en otra sesión (se reutiliza);
  - [ ] un externo con pasaporte;
  - [ ] `PERMITE_EXTERNOS = false`;
  - [ ] el todo-o-nada: si falla la geocerca, no queda ningún asistente creado.
- [ ] **Verificación:** la prueba SQL termina sin errores.

## Fase 65 — Migración 057 y apagado en el front (D58)

- [ ] `057_apagado_v71.sql`: roles cerrados y `requiere_inscripcion` en `false` por defecto.
- [ ] `app.py`: sin los blueprints `estudiante`, `ponente` y `ponente_catedras`.
- [ ] `auth_helpers.py` (`endpoint_de_inicio`) y el navbar: solo el administrador.
- [ ] `ServicioUsuario.LoginAsync`: solo `ADMIN`; sin `ProvisionarPrimerIngresoAsync`.
- [ ] Marcar como "apagadas por D58" las pruebas del front de la v7.1 que dependan de lo apagado.

## Fase 66 — API: capas de `Registro`

- [ ] Peticiones `RegistroEstudiante`, `RegistroDocente` y `RegistroExterno`, con validación.
- [ ] `IRepositorioRegistro` / `RepositorioRegistroPostgres`.
- [ ] `IServicioRegistro` / `ServicioRegistro`.
- [ ] `RegistroController` (4 endpoints) y su registro en `Program.cs`.
- [ ] `Sede`: modelo, peticiones y repositorio con las coordenadas.
- [ ] Prueba de capas (`pruebas/Programa.cs`): bloque "V8 · Registro por tipo".
- [ ] **Verificación:** `docker compose exec api dotnet run --project pruebas` en verde.

## Fase 67 — Front: registro por tipo

- [ ] `routes/registro.py`: la pantalla de 3 botones y las 3 rutas con su `POST`.
- [ ] Plantillas `registro_tipo`, `registro_estudiante`, `registro_docente`,
      `registro_externo` y `registro_listo`.
- [ ] `geolocalizacion.js` según `ubicacion`.
- [ ] Botón "Responder la encuesta (opcional)" (RF216).
- [ ] `/admin/sedes`: campos de coordenadas.
- [ ] **Verificación a mano** desde un celular por https (criterios 1–6).

## Fase 68 — Cierre de la v8

- [ ] `pruebas/verificar_v8.ps1` y `pruebas/correr_todo_v8.ps1` en verde.
- [ ] README: sección "Versión 8", con las migraciones que se aplican a mano.
- [ ] `0_mapa_versiones.md`: fila V8.
- [ ] Quitar el banner BORRADOR de los 7 documentos y crear el tag `v8`.
- [ ] PR de `feature/…` a `main`.
