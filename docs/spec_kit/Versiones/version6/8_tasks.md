# Tareas — Versión 6: encuestas de calidad

> ⚠️ **BORRADOR** — ver nota de [2_spec.md](2_spec.md). Plan, no
> registro de trabajo hecho.
>
> Continúa la numeración de fases de
> [version5/8_tasks.md](../version5/8_tasks.md) (termina en Fase 46).
>
> **Nota de alcance (v6):** 1 migración SQL nueva (`004_...`, la
> primera desde v3) — ver [6_contracts.md](6_contracts.md).

---

## Fase 47 — Migración SQL: `sp_guardar_pregunta`
> Guía IA, Regla 1. Ver §4.1 de [3_plan.md](3_plan.md), ADR D29. Va
> primero porque la Fase 48 depende de que el SP ya exista en la BD.

- [ ] `db/postgres/migraciones/004_sp_guardar_pregunta.sql` —
      `CREATE OR REPLACE PROCEDURE` (re-ejecutable); `INSERT pregunta
      RETURNING id_pregunta` + `INSERT ... SELECT ...
      jsonb_array_elements(p_opciones)` condicional.
- [ ] `docker-compose.yml` — montar como
      `/docker-entrypoint-initdb.d/z004_...sql`.

**Verificación planeada**: aplicar 2 veces sin error; `\df
catedras.sp_guardar_pregunta` muestra la firma esperada; simular el
orden de `initdb` confirma que corre después de
`script_bdcatedras.sql` y de `z001_`-`z003_`.

## Fase 48 — La aplicación (capas de `Pregunta` y `OpcionPregunta`)
> RF159-RF171 de [2_spec.md](2_spec.md), ADR D29, D32, §2.1-2.2 de
> [5_data_model.md](5_data_model.md). Depende de la Fase 47 y de
> `Encuesta`/`TipoPregunta` (v1, ya construidas).

- [ ] `Modelos/Pregunta.cs`, `OpcionPregunta.cs`.
- [ ] `Peticiones/Pregunta{Crear,Reemplazo,Actualizar}.cs` — `Orden`
      con `[Range(1, short.MaxValue)]`; `ValorMin`/`ValorMax` SIN
      cruce validado por anotación (le toca al servicio); `Crear`
      incluye `List<OpcionInicial>? Opciones`.
- [ ] `Peticiones/OpcionInicial.cs` — sin `fk_pregunta` (§4.1 de
      [3_plan.md](3_plan.md), mismo criterio que `AliasInicial` de v2).
- [ ] `Peticiones/OpcionPregunta{Crear,Reemplazo,Actualizar}.cs` —
      molde completo de 3 peticiones (D32: a diferencia de
      `AliasProgramaCrear`, esta entidad SÍ tiene `Reemplazo`/
      `Actualizar`).
- [ ] `Repositorios/IRepositorioPregunta.cs` +
      `RepositorioPreguntaPostgres.cs` — `CrearAsync` serializa
      `Opciones` y hace `CALL catedras.sp_guardar_pregunta(...,
      CAST(@Opciones AS jsonb))`.
- [ ] `Repositorios/IRepositorioOpcionPregunta.cs` +
      `RepositorioOpcionPreguntaPostgres.cs` — CRUD estándar, 5
      métodos.
- [ ] `Servicios/IServicioPregunta.cs` + `ServicioPregunta.cs` —
      `CrearAsync`/`ReemplazarAsync`/`ActualizarAsync` validan
      `valor_min`/`valor_max` (§4.3 de [3_plan.md](3_plan.md)); `CrearAsync`
      arma la lista de `OpcionPregunta` a partir de `Opciones`.
- [ ] `Servicios/IServicioOpcionPregunta.cs` +
      `ServicioOpcionPregunta.cs`.
- [ ] `Controllers/PreguntaController.cs` — 6 endpoints.
- [ ] `Controllers/OpcionPreguntaController.cs` — 6 endpoints.
- [ ] `Program.cs` — registrar las 2 parejas nuevas.

**Verificación planeada** (contra PostgreSQL real): `POST
/api/Pregunta` con `opciones` → 200 y las opciones quedan con el
`fk_pregunta` correcto; repetir con un `orden` de opción duplicado →
500 y nada queda creado; `POST` con `valorMin` sin `valorMax` → 400;
`(fkEncuesta, orden)` de pregunta duplicado → 500.

## Fase 49 — La aplicación (capas de `RespuestaEncuesta` y `RespuestaItem`)
> RF172-RF178 de [2_spec.md](2_spec.md), ADR D30, D31, §2.3-2.4 de
> [5_data_model.md](5_data_model.md). Depende de la Fase 48
> (`fk_pregunta`/`fk_opcion`) y de `RegistroAsistencia` (v5, ya
> construida). **No** depende de ninguna migración nueva — las
> funciones ya existen.

- [ ] `Modelos/RespuestaEncuesta.cs`, `RespuestaItem.cs`.
- [ ] `Peticiones/ItemRespuesta.cs` — `Pregunta` obligatorio;
      `Numero`/`Texto`/`Opcion` opcionales, sin anotación que fuerce
      "exactamente uno" (D9: lo vigila el trigger de la BD al
      responder, no la petición al recibir).
- [ ] `Peticiones/RespuestaEncuestaResponder.cs` — **única** petición
      de esta entidad: `FkRegistro` obligatorio; `Respuestas:
      List<ItemRespuesta>` con `[MinLength(1)]` (primera muralla
      contra una lista vacía, aunque la BD también la rechazaría —
      doble verificación deliberada, igual criterio que
      `RegistroAsistenciaCrear.Origen` en v5).
- [ ] `Repositorios/IRepositorioRespuestaEncuesta.cs` +
      `RepositorioRespuestaEncuestaPostgres.cs` — `ObtenerTodosAsync`,
      `ObtenerPorIdAsync`, `ResponderAsync` (`SELECT * FROM
      fn_responder_encuesta_api(...)`, código exacto en §4.2 de
      [3_plan.md](3_plan.md)). **Sin** `CerrarAsync` en la interfaz
      (D30).
- [ ] `Repositorios/IRepositorioRespuestaItem.cs` +
      `RepositorioRespuestaItemPostgres.cs` — solo
      `ObtenerTodosAsync`/`ObtenerPorIdAsync` (sin `CrearAsync`: nace
      dentro del SP).
- [ ] `Servicios/IServicioRespuestaEncuesta.cs` +
      `ServicioRespuestaEncuesta.cs` — sin método de "cerrar".
- [ ] `Servicios/IServicioRespuestaItem.cs` +
      `ServicioRespuestaItem.cs` — de solo lectura.
- [ ] `Controllers/RespuestaEncuestaController.cs` — `GET` lista,
      `GET/{id}`, `[HttpPost]` (= responder). Sin `PUT`/`PATCH`/
      `DELETE`.
- [ ] `Controllers/RespuestaItemController.cs` — `GET` lista, `GET/{id}`.
- [ ] `Program.cs` — registrar las 2 parejas nuevas.

**Verificación planeada** (contra PostgreSQL real, usando un
`fk_registro` de ejemplo con `fk_encuesta` asignado y sin respuesta
previa): `POST /api/RespuestaEncuesta` con todas las preguntas
obligatorias respondidas → 200, `completa: true`; omitiendo una
obligatoria → 500, sin ninguna fila nueva (ni maestro ni detalle);
repetir sobre el mismo `fkRegistro` → 500 (`uq_respuesta_registro`).

## Fase 50 — Prueba de capas ampliada (criterio de aceptación 6)
> Depende de las Fases 48-49 (las 4 entidades deben existir).

- [ ] Agregar a `pruebas/Programa.cs` un repositorio FALSO y sus
      verificaciones para las 4 entidades. Puntos especiales:
      - `RepositorioPreguntaFalso` simula el `CALL` guardando las
        opciones en memoria con el `fk_pregunta` correcto, para
        verificar sin PostgreSQL que el servicio arma bien la lista.
      - Un caso de prueba confirma, por inspección del tipo de
        `IRepositorioRespuestaEncuesta`, que **no existe** ningún
        método `CerrarAsync` en la interfaz (verificación de diseño,
        no solo de comportamiento — asegura que D30 no se revierta
        por accidente en una refactorización futura).
      - `RepositorioRespuestaEncuestaFalso` incluye un caso con una
        pregunta obligatoria faltante, confirmando el 500 simulado
        (`RN-26`) y que ninguna fila quedó en el repositorio falso.

**Verificación planeada**: `dotnet run --project pruebas` → cubre las
**33** entidades, código de salida 0.

## Fase 51 — Verificación de integridad referencial y reglas de negocio (criterios de aceptación 2, 4-5)
> Depende de las Fases 48-49.

- [ ] Ejecutar, contra Docker + PostgreSQL reales y EN ESTE ORDEN:
  1. `POST /api/Pregunta` con opciones → 200; con `orden` de opción
     repetido → 500, nada creado.
  2. `POST /api/RespuestaEncuesta` con todas las obligatorias → 200;
     repetir → 500.
  3. `POST /api/RespuestaEncuesta` (otro registro) omitiendo una
     obligatoria → 500, sin fila nueva.
  4. `DELETE /api/Pregunta/{id con respuestas de ejemplo}` → 500.
- [ ] `docker compose down -v && docker compose up -d --build`.

**Verificación planeada**: los 4 resultados coinciden con
[5_data_model.md](5_data_model.md) y los criterios de aceptación 2, 4-5
de [2_spec.md](2_spec.md).

## Fase 52 — Frontend: responder encuesta y administrar preguntas
> Criterios 7-8 de [2_spec.md](2_spec.md), §5 de [3_plan.md](3_plan.md).
> Depende de las Fases 48-49.

- [ ] `templates/detalle_sesion.html` (v4) — agregar el enlace
      "responder encuesta" condicionado a `fk_encuesta` + registro sin
      respuesta.
- [ ] `routes/encuestas.py` + `templates/encuesta_form.html` —
      formulario dinámico según `fkTipoPregunta` de cada pregunta.
- [ ] `routes/admin.py` — 4 bloques nuevos; `Pregunta` con opciones
      iniciales al crear; `RespuestaEncuesta`/`RespuestaItem` solo
      listan.
- [ ] `templates/admin/` — templates nuevos siguiendo el layout
      existente.

**Verificación planeada**: responder la encuesta de ejemplo de punta a
punta contra Docker/PostgreSQL reales, incluido el caso de una
pregunta obligatoria sin responder (el formulario debe impedir el
envío o mostrar el error de la API con claridad).

## Fase 53 — Cierre de la v6
> Última fase. Solo entonces se escribe la spec de la v7 (Artículo 1).

- [ ] Confirmar que los 8 criterios de aceptación de
      [2_spec.md](2_spec.md) §5 pasan, en orden, sobre un entorno
      recién reseteado.
- [ ] `dotnet run --project pruebas` → anotar el total final de
      verificaciones pasadas (Fase 50).
- [ ] Revisar este spec kit completo contra lo realmente construido —
      quitar el banner de "BORRADOR".
- [ ] Commit y tag: `git tag v6`.
