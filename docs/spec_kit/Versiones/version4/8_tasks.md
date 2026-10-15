# Tareas — Versión 4: sesiones y ponencias

> ✅ **Construida el 2026-10-08** — ver nota de [2_spec.md](2_spec.md).
> Las casillas marcadas `[x]` son código escrito. Cada fase dice además
> **qué se verificó y dónde**: el SQL de los repositorios se ejecutó
> contra la BD real (PostgreSQL 16 con `script_bdcatedras.sql` y las 3
> migraciones) y el front se probó de punta a punta contra esa BD; lo que
> exige el SDK de .NET (`dotnet run`, Swagger, smoke test en Docker)
> queda marcado como **pendiente** y es lo primero que se corre en la
> Fase 37 (Artículo 1: la versión se cierra solo en Docker).
>
> ✅ **Verificada en Docker el 2026-10-08:** las notas "Pendiente en
> Docker" de las Fases 32-36 se resolvieron en la Fase 37; cada una dice
> ahora qué se comprobó por HTTP y qué no se repitió.
>
> Guía para hacerlo con un asistente de IA: [guia_ia_v4.md](guia_ia_v4.md).
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

- [x] `Modelos/Sesion.cs` — `IdSesion` y `CreadoEn` sin `required`;
      `NumeroReunion` sin `required` (puede llegar `0`, lo reemplaza la
      BD).
- [x] `Peticiones/Sesion{Crear,Reemplazo,Actualizar}.cs` — `Cupo` con
      `[Range(1, int.MaxValue)]`; `Estado` con
      `[RegularExpression]` sobre la lista cerrada; **ninguna de las 3**
      valida `fin > inicio` con una anotación (es cruce de dos campos
      del mismo body, le toca al servicio — RF125).
- [x] `Repositorios/IRepositorioSesion.cs` +
      `RepositorioSesionPostgres.cs` — `ObtenerTodosAsync` recibe
      `long? fkCatedra` y lo usa en un `WHERE` solo si no es `null`;
      `CrearAsync` usa `RETURNING id_sesion, numero_reunion, creado_en`
      (código exacto en §4.1 de [3_plan.md](3_plan.md)) y **no**
      recalcula `numero_reunion` en ningún punto del C#.
- [x] `Servicios/IServicioSesion.cs` + `ServicioSesion.cs` —
      `CrearAsync`/`ReemplazarAsync`/`ActualizarAsync` validan
      `fin > inicio` (`ArgumentException` si no) ANTES de llamar al
      repositorio.
- [x] `Controllers/SesionController.cs` — 6 endpoints; `GET` lista
      recibe `[FromQuery] long? fkCatedra` además de `limite`.
- [x] `Program.cs` — registrar la pareja nueva.

**Verificado (2026-10-08, SQL de `RepositorioSesionPostgres` como
sentencias preparadas con los tipos que manda Npgsql):** `?fkCatedra=3975`
→ 59 filas, `numero_reunion` 1-60 **con hueco en la 23** (spec
corregida); `INSERT` con `numero_reunion = 0` sobre 5498 → 1 y luego 2;
sobre 3975 → 61; número explícito repetido → `uq_sesion_reunion`;
`UPDATE` de `estado`/`fin` con `timestamptz` OK.
**Verificado en Docker (2026-10-08, por HTTP en Swagger):** 59 filas en la
cátedra 3975; `POST` sin número → 1 y luego 2 en la 5498; `fin` anterior a
`inicio` → 400 y el total sigue en 2. **No se repitió por HTTP** el
`fk_modalidad` inexistente → 500 (ya lo rechaza la FK a nivel SQL).

## Fase 33 — La aplicación (capas de `Ponencia`)
> RF127-RF131 de [2_spec.md](2_spec.md), ADR D19, §2.2 de
> [5_data_model.md](5_data_model.md). Depende de la Fase 32
> (`fk_sesion`) y de `Asistente` (v3, ya construida). Primera vez desde
> v3 sin el molde de 5/6 verbos, pero con `DELETE` (a diferencia de
> `VinculacionAsistente`/`ProgramaAsistente`) — construir con cuidado
> de no copiar por costumbre el patrón de "cerrar" de v3 donde no
> corresponde (ver ADR D19 antes de empezar esta fase).

- [x] `Modelos/Ponencia.cs` — SIN propiedad `Id`: la llave primaria son
      sus 3 columnas de negocio.
- [x] `Peticiones/PonenciaCrear.cs` — **única** petición de esta
      entidad (mismo criterio que `AliasProgramaCrear`/
      `ConsentimientoDatosCrear`): `fk_sesion`, `fk_asistente`
      obligatorios; `rol` opcional con `[RegularExpression]` sobre la
      lista cerrada, por defecto `PONENTE`.
- [x] `Repositorios/IRepositorioPonencia.cs` +
      `RepositorioPonenciaPostgres.cs` — 4 métodos exactamente (código
      de la interfaz en §4.2 de [3_plan.md](3_plan.md)):
      `ObtenerTodosAsync`, `ObtenerUnoAsync`, `CrearAsync`,
      `EliminarAsync`. **Sin** `ActualizarAsync` en la interfaz (no hay
      nada que actualizar — RF130).
- [x] `Servicios/IServicioPonencia.cs` + `ServicioPonencia.cs`.
- [x] `Controllers/PonenciaController.cs` — **4** endpoints:
      `[HttpGet]` lista (con `fkSesion`/`fkAsistente` opcionales),
      `[HttpGet("{fkSesion}/{fkAsistente}/{rol}")]`, `[HttpPost]`,
      `[HttpDelete("{fkSesion}/{fkAsistente}/{rol}")]`. Sin
      `[HttpPut]` ni `[HttpPatch]`.
- [x] `Program.cs` — registrar la pareja nueva.

**Verificado (2026-10-08, SQL de `RepositorioPonenciaPostgres`):**
`INSERT` → OK; misma llave → `pk_ponencia`; otro rol → fila nueva; los 2
filtros del listado solos y combinados; `DELETE` por la llave → 1 fila y
luego 0; borrar la sesión arrastra la ponencia (CASCADE).
**Verificado en Docker (2026-10-08, por HTTP en Swagger):** asignar → 200;
misma llave → 500; otro rol → 200; `GET` de la llave de 3 segmentos → 200;
`DELETE` → `filasEliminadas: 1`; `GET` posterior → 404. **No se repitió**
`GET /api/Ponencia/3` → 404 de ASP.NET (no hay ruta de un segmento).

## Fase 34 — Prueba de capas ampliada (criterio de aceptación 7)
> Depende de las Fases 32-33 (las 2 entidades deben existir).

- [x] Agregar a `pruebas/Programa.cs` un repositorio FALSO y sus
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

**Construida:** 2 secciones nuevas en `pruebas/Programa.cs` (Sesion: 21
verificaciones; Ponencia: 15) y los repositorios falsos
`RepositorioSesionFalso` (simula IDENTITY, `now()`, el trigger y
`uq_sesion_reunion`) y `RepositorioPonenciaFalso`. Sintaxis de los 214
`.cs` revisada con un parser de C#.
**Verificado en Docker (2026-10-08):** `docker compose exec api dotnet run
--project pruebas` → **256 pasadas, 0 fallidas** (v3 tenía 220).

## Fase 35 — Verificación de integridad referencial y reglas de negocio (criterios de aceptación 4-6)
> Depende de las Fases 32-33. Como en v2 (Fase 18) y v3 (Fase 28), es
> la fase dedicada exclusivamente a probar lo que la BD hace sola, sin
> código nuevo.

- [x] Ejecutar, contra Docker + PostgreSQL reales y EN ESTE ORDEN
      (a nivel SQL ya se comprobaron los 6 — ver Fases 32-33; aquí se
      repiten por HTTP):
  1. `POST /api/Sesion` con `fin` anterior a `inicio` → 400, sin fila
     nueva.
  2. `POST /api/Sesion` sin `numero_reunion` dos veces sobre la misma
     cátedra sin sesiones de ejemplo → `numero_reunion: 1` y luego `2`.
  3. `POST /api/Ponencia` → 200; repetir la misma llave → 500; con
     `rol` distinto → 200.
  4. `DELETE /api/Ponencia/{fkSesion}/{fkAsistente}/{rol}` → 200;
     repetir → 404.
  5. `DELETE /api/Sesion/1` (segura, sin filas de v5/v7) → 200;
     `DELETE /api/Sesion/5712` → 500 y la sesión sigue ahí.
  6. `DELETE /api/Asistente/167` (usar el **167** en el paso 3: no tiene
     `registro_asistencia` ni `usuario`) → 500
     (`fk_ponencia_asistente`); confirmar que su documento de v3 sigue
     intacto.
- [x] `docker compose down -v && docker compose up -d --build` —
      revertir los efectos de arriba antes de seguir con cualquier otra
      prueba manual.

**Verificado en Docker (2026-10-08, por HTTP en Swagger):** los 6
resultados coincidieron con [5_data_model.md](5_data_model.md) y con los
criterios 4-6 de [2_spec.md](2_spec.md). Detalle: `DELETE
/api/Sesion/1` → 200; `DELETE /api/Sesion/5712` → 500 y su `GET` sigue en
200; `DELETE /api/Asistente/167` con una ponencia → 500 y `GET
/api/DocumentoAsistente?fkAsistente=167` → 200. Tras el reset,
`GET /api/Sesion/1` volvió a responder 200.

## Fase 36 — Frontend: calendario de cátedra y detalle de sesión
> Criterios 8-9 de [2_spec.md](2_spec.md), §5 de [3_plan.md](3_plan.md).
> Depende de las Fases 32-33 (API completa).

- [x] `templates/detalle_catedra.html` (v2) — agregar la sección de
      calendario: tabla de sesiones (`GET /api/Sesion?fkCatedra={id}`)
      con fecha, modalidad y estado.
- [x] `routes/sesiones.py` + `templates/detalle_sesion.html` — detalle
      de una sesión (`/sesiones/<id>`) con la lista de ponentes
      (`GET /api/Ponencia?fkSesion={id}`) y un formulario para asignar
      uno nuevo (`<select>` de asistentes poblado vía
      `GET /api/Asistente?q=...`, Regla 2 de la guía IA) o quitarlo.
- [x] `routes/ponente.py` + `templates/ponente_calendario.html` —
      calendario del ponente (mapa de versiones §4), con la persona
      elegida por búsqueda hasta que la v5 traiga login.
- [x] `servicios/fechas.py` (UTC ↔ hora de Colombia, ADR D21),
      `servicios/api_catedras.llave()` (ruta de llave compuesta) y
      `templates/partials/macros_sesion.html` (piezas compartidas).
- [x] `routes/admin.py` — 2 bloques nuevos (`Sesion` con CRUD completo;
      `Ponencia` con asignar/quitar, sin "editar").
- [x] `templates/admin/` — templates nuevos siguiendo el layout
      existente.

**Verificado (2026-10-08):** 31 comprobaciones de punta a punta del
front contra un doble HTTP de la API que ejecuta el mismo SQL sobre la
BD real: calendario de 3975 (59 filas, horas en hora de Colombia),
detalle de sesión, buscar → asignar → duplicado (error visible) → otro
rol → quitar, calendario del ponente, CRUD de sesión en el admin
(número automático 1 y 2, 400 por fechas con el formulario conservado,
PUT, DELETE con CASCADE, 5712 bloqueada), y API apagada → error claro en
las 5 pantallas nuevas.
**Verificado en Docker (2026-10-08, contra la API .NET real):**
calendario de la cátedra 3975 (59 sesiones, hora de Colombia); detalle de
la sesión 5715; buscar `1013032` → asignar al 167 → duplicado (aviso rojo
visible **arriba** de la página, fuera de pantalla porque la redirección
baja a `#ponentes`) → otro rol → quitar; calendario del ponente
(`/ponente/?asistente=167`); `/admin/` con las tarjetas `Sesión` y
`Ponencia` (sin "editar"); crear una sesión desde el admin sin número → la
BD asignó el 3 (la cátedra 5498 ya tenía 2); API apagada → "No se pudo
contactar la API" en `/catedras/3975`. **No se repitió** con la API
apagada en las otras 4 pantallas nuevas ni el 400 por fechas en el
formulario del admin.

## Fase 37 — Cierre de la v4
> Última fase. Solo entonces se escribe la spec de la v5 (Artículo 1).

- [x] Confirmar que los 9 criterios de aceptación de
      [2_spec.md](2_spec.md) §5 pasan, en orden, sobre un entorno
      recién reseteado (`down -v && up -d --build`). *(2026-10-08:
      criterios 1-6 por HTTP, 8-9 en el front, 7 con la prueba de capas.)*
- [x] `dotnet run --project pruebas` → anotar el total final de
      verificaciones pasadas (Fase 34). ***256 pasadas, 0 fallidas.***
- [x] Revisar este spec kit completo (los 7 documentos) contra lo que
      realmente quedó construido — quitar el banner de "BORRADOR" de
      cada archivo una vez confirmado que coincide. *(Ningún documento de
      la v4 conserva el banner de borrador.)*
- [ ] Commit y tag: `git tag v4`.
