# Tareas — Versión 4: sesiones y ponencias

> ⚠️ **BORRADOR** — ver nota de [2_spec.md](2_spec.md). Igual que el
> `8_tasks.md` de v3, ninguna casilla está marcada: es un plan, no un
> registro de trabajo ya hecho. Se marcan a medida que cada fase se
> construye y se verifica contra Docker + PostgreSQL reales (Artículo
> 1).
>
> Continúa la numeración de fases de
> [version3/8_tasks.md](../version3/8_tasks.md) (termina en Fase 31).
>
> **Nota de alcance (v4):** sin segundo motor, sin devcontainer, sin
> migraciones SQL nuevas (es la primera versión desde v1 sin ningún
> `CREATE PROCEDURE`) — ver [6_contracts.md](6_contracts.md). La BD ya
> tiene las 2 tablas de esta versión (y sus datos de ejemplo) desde el
> script de la v1 (Artículo 5); aquí solo se construye la API y el
> front que las usan.

---

## Fase 32 — La aplicación (capas de `Sesion`)
> RF119-RF126 de [2_spec.md](2_spec.md), §2.1 de
> [5_data_model.md](5_data_model.md), ADR D20. Depende de `Catedra`
> (v2), `Modalidad`/`Sede`/`PeriodoAcademico`/`Encuesta` (v1), todas ya
> construidas.

- [ ] `Modelos/Sesion.cs` — `IdSesion` y `CreadoEn` sin `required`;
      `NumeroReunion` sin `required` (puede llegar `0`, lo reemplaza la
      BD).
- [ ] `Peticiones/Sesion{Crear,Reemplazo,Actualizar}.cs` — `Cupo` con
      `[Range(1, int.MaxValue)]`; `Estado` con
      `[RegularExpression]` sobre la lista cerrada; **ninguna de las 3**
      valida `fin > inicio` con una anotación (es cruce de dos campos
      del mismo body, le toca al servicio — RF125).
- [ ] `Repositorios/IRepositorioSesion.cs` +
      `RepositorioSesionPostgres.cs` — `ObtenerTodosAsync` recibe
      `long? fkCatedra` y lo usa en un `WHERE` solo si no es `null`;
      `CrearAsync` usa `RETURNING id_sesion, numero_reunion, creado_en`
      (código exacto en §4.1 de [3_plan.md](3_plan.md)) y **no**
      recalcula `numero_reunion` en ningún punto del C#.
- [ ] `Servicios/IServicioSesion.cs` + `ServicioSesion.cs` —
      `CrearAsync`/`ReemplazarAsync`/`ActualizarAsync` validan
      `fin > inicio` (`ArgumentException` si no) ANTES de llamar al
      repositorio.
- [ ] `Controllers/SesionController.cs` — 6 endpoints; `GET` lista
      recibe `[FromQuery] long? fkCatedra` además de `limite`.
- [ ] `Program.cs` — registrar la pareja nueva.

**Verificación planeada** (contra PostgreSQL real): `GET
/api/Sesion?fkCatedra=3975` → 59 filas, `numero_reunion` 1-59 sin
huecos; `POST` sin `numero_reunion` sobre `fk_catedra: 5498` (sin
sesiones de ejemplo) → `numero_reunion: 1`, y un segundo `POST` igual →
`numero_reunion: 2`; `POST` con `fin` anterior a `inicio` → 400, sin
fila nueva en la BD; un `fk_modalidad`/`fk_sede`/`fk_periodo`
inexistente → 500.

## Fase 33 — La aplicación (capas de `Ponencia`)
> RF127-RF131 de [2_spec.md](2_spec.md), ADR D19, §2.2 de
> [5_data_model.md](5_data_model.md). Depende de la Fase 32
> (`fk_sesion`) y de `Asistente` (v3, ya construida). Primera vez desde
> v3 sin el molde de 5/6 verbos, pero con `DELETE` (a diferencia de
> `VinculacionAsistente`/`ProgramaAsistente`) — construir con cuidado
> de no copiar por costumbre el patrón de "cerrar" de v3 donde no
> corresponde (ver ADR D19 antes de empezar esta fase).

- [ ] `Modelos/Ponencia.cs` — SIN propiedad `Id`: la llave primaria son
      sus 3 columnas de negocio.
- [ ] `Peticiones/PonenciaCrear.cs` — **única** petición de esta
      entidad (mismo criterio que `AliasProgramaCrear`/
      `ConsentimientoDatosCrear`): `fk_sesion`, `fk_asistente`
      obligatorios; `rol` opcional con `[RegularExpression]` sobre la
      lista cerrada, por defecto `PONENTE`.
- [ ] `Repositorios/IRepositorioPonencia.cs` +
      `RepositorioPonenciaPostgres.cs` — 4 métodos exactamente (código
      de la interfaz en §4.2 de [3_plan.md](3_plan.md)):
      `ObtenerTodosAsync`, `ObtenerUnoAsync`, `CrearAsync`,
      `EliminarAsync`. **Sin** `ActualizarAsync` en la interfaz (no hay
      nada que actualizar — RF130).
- [ ] `Servicios/IServicioPonencia.cs` + `ServicioPonencia.cs`.
- [ ] `Controllers/PonenciaController.cs` — **4** endpoints:
      `[HttpGet]` lista (con `fkSesion`/`fkAsistente` opcionales),
      `[HttpGet("{fkSesion}/{fkAsistente}/{rol}")]`, `[HttpPost]`,
      `[HttpDelete("{fkSesion}/{fkAsistente}/{rol}")]`. Sin
      `[HttpPut]` ni `[HttpPatch]`.
- [ ] `Program.cs` — registrar la pareja nueva.

**Verificación planeada** (contra PostgreSQL real): `POST
/api/Ponencia` → 200; repetir con la misma llave exacta → 500 (PK
duplicada); el mismo body con `rol` distinto → 200 (fila nueva); `GET
/api/Ponencia/{fkSesion}/{fkAsistente}/{rol}` → 200; `DELETE` sobre esa
misma ruta → `filasEliminadas: 1`; repetir el `GET` → 404; un `GET
/api/Ponencia/3` (ruta de un solo segmento) → 404 de ASP.NET.

## Fase 34 — Prueba de capas ampliada (criterio de aceptación 7)
> Depende de las Fases 32-33 (las 2 entidades deben existir).

- [ ] Agregar a `pruebas/Programa.cs` un repositorio FALSO y sus
      verificaciones para `Sesion` y `Ponencia`. Puntos especiales:
      - `RepositorioSesionFalso` simula el `RETURNING`: si recibe
        `numero_reunion: 0`, el repositorio FALSO calcula el consecutivo
        él mismo para la prueba (simulando lo que haría el trigger),
        **solo dentro del repositorio falso** — el servicio sigue sin
        saber nada de ese cálculo, para que la prueba confirme
        exactamente lo que el servicio SÍ hace (pasar el valor tal
        cual) y no lo que no hace.
      - `RepositorioPonenciaFalso` **no** implementa `ActualizarAsync`
        (no existe en la interfaz — Fase 33); un caso de prueba fuerza
        la misma llave dos veces y confirma el 500 simulado por el
        repositorio falso (PK duplicada), y otro caso confirma que un
        `rol` distinto sí se acepta como fila nueva.

**Verificación planeada**: `dotnet run --project pruebas` → cubre las
**24** entidades, código de salida 0. Anotar aquí el total final de
verificaciones pasadas una vez construido.

## Fase 35 — Verificación de integridad referencial y reglas de negocio (criterios de aceptación 4-6)
> Depende de las Fases 32-33. Como en v2 (Fase 18) y v3 (Fase 28), es
> la fase dedicada exclusivamente a probar lo que la BD hace sola, sin
> código nuevo.

- [ ] Ejecutar, contra Docker + PostgreSQL reales y EN ESTE ORDEN:
  1. `POST /api/Sesion` con `fin` anterior a `inicio` → 400, sin fila
     nueva.
  2. `POST /api/Sesion` sin `numero_reunion` dos veces sobre la misma
     cátedra sin sesiones de ejemplo → `numero_reunion: 1` y luego `2`.
  3. `POST /api/Ponencia` → 200; repetir la misma llave → 500; con
     `rol` distinto → 200.
  4. `DELETE /api/Ponencia/{fkSesion}/{fkAsistente}/{rol}` → 200;
     repetir → 404.
  5. `DELETE /api/Sesion/1` (segura, sin filas de v5/v7) → 200.
  6. `DELETE /api/Asistente/{id usado en el paso 3}` (ya es ponente) →
     500 (`fk_ponencia_asistente`); confirmar que sus documentos de v3
     siguen intactos.
- [ ] `docker compose down -v && docker compose up -d --build` —
      revertir los efectos de arriba antes de seguir con cualquier otra
      prueba manual.

**Verificación planeada**: los 6 resultados coinciden con lo
documentado en [5_data_model.md](5_data_model.md) y con los criterios
de aceptación 4-6 de [2_spec.md](2_spec.md).

## Fase 36 — Frontend: calendario de cátedra y detalle de sesión
> Criterios 8-9 de [2_spec.md](2_spec.md), §5 de [3_plan.md](3_plan.md).
> Depende de las Fases 32-33 (API completa).

- [ ] `templates/detalle_catedra.html` (v2) — agregar la sección de
      calendario: tabla de sesiones (`GET /api/Sesion?fkCatedra={id}`)
      con fecha, modalidad y estado.
- [ ] `routes/sesiones.py` (o el nombre que fije la implementación) +
      `templates/detalle_sesion.html` — detalle de una sesión con la
      lista de ponentes (`GET /api/Ponencia?fkSesion={id}`) y un
      formulario para asignar uno nuevo (`<select>` de asistentes
      poblado vía `GET /api/Asistente?q=...`, Regla 2 de la guía IA).
- [ ] `routes/admin.py` — 2 bloques nuevos (`Sesion` con CRUD completo;
      `Ponencia` con asignar/quitar, sin "editar").
- [ ] `templates/admin/` — templates nuevos siguiendo el layout
      existente.

**Verificación planeada**: el detalle de una cátedra con sesiones de
ejemplo muestra su calendario completo; el detalle de una sesión
permite asignar un ponente nuevo y lo refleja sin recargar manualmente
el `<select>`; `/admin/` muestra las 2 tarjetas nuevas con CRUD/
asignación de punta a punta contra Docker/PostgreSQL reales.

## Fase 37 — Cierre de la v4
> Última fase. Solo entonces se escribe la spec de la v5 (Artículo 1).

- [ ] Confirmar que los 9 criterios de aceptación de
      [2_spec.md](2_spec.md) §5 pasan, en orden, sobre un entorno
      recién reseteado (`down -v && up -d --build`).
- [ ] `dotnet run --project pruebas` → anotar el total final de
      verificaciones pasadas (Fase 34).
- [ ] Revisar este spec kit completo (los 7 documentos) contra lo que
      realmente quedó construido — quitar el banner de "BORRADOR" de
      cada archivo una vez confirmado que coincide.
- [ ] Commit y tag: `git tag v4`.
