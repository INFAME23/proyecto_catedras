# Tareas — Versión 2: estructura académica y catálogo de cátedras

> **Documento 8 de 8** del spec kit · Orden de construcción. Continúa la
> numeración de fases de [version1/8_tasks.md](../version1/8_tasks.md)
> (termina en Fase 13). Cada fase se verifica contra Docker + PostgreSQL
> reales antes de pasar a la siguiente (Artículo 1: no se avanza sobre
> una fase sin verificar).
>
> **Nota de alcance (v2):** sin segundo motor, sin devcontainer, sin
> cambios de infraestructura — ver [6_contracts.md](6_contracts.md). La
> BD ya tiene las 5 tablas de esta versión (y sus datos de ejemplo)
> desde el script de la v1 (Artículo 5); aquí solo se construye la API y
> el front que las usan.

---

## Fase 14 — La aplicación (capas de `Facultad` y `Dependencia`)
> Especificadas en RF62-RF73 de [2_spec.md](2_spec.md) y §2.1-2.2 de
> [5_data_model.md](5_data_model.md). Primera vez en el proyecto con
> autorreferencia (§4.2 de [3_plan.md](3_plan.md)).

- [x] `Modelos/Facultad.cs`, `Dependencia.cs`.
- [x] `Peticiones/Facultad{Crear,Reemplazo,Actualizar}.cs`,
      `Dependencia{Crear,Reemplazo,Actualizar}.cs` — anotaciones de
      forma únicamente; la autorreferencia NO se valida aquí (es
      cruce de dos campos del mismo body, le toca al servicio).
- [x] `Repositorios/IRepositorioFacultad.cs` +
      `RepositorioFacultadPostgres.cs`,
      `IRepositorioDependencia.cs` + `RepositorioDependenciaPostgres.cs`
      — CRUD estándar, mismo molde que `RepositorioSedePostgres` de v1.
- [x] `Servicios/IServicioFacultad.cs` + `ServicioFacultad.cs`,
      `IServicioDependencia.cs` + `ServicioDependencia.cs` — cada uno
      valida, en `CrearAsync`/`ReemplazarAsync`/`ActualizarAsync`, que
      el padre propuesto no sea la propia fila (código exacto en §4.2
      de [3_plan.md](3_plan.md)).
- [x] `Controllers/FacultadController.cs`,
      `DependenciaController.cs` — 6 endpoints cada uno, mismo
      try/catch → código HTTP que `SedeController`.
- [x] `Program.cs` — registrar las 2 parejas nuevas (`AddScoped`).

**Verificado** (contra PostgreSQL real): `GET /api/Facultad`
devuelve 9 filas con `USB` como raíz; `GET /api/Dependencia` devuelve 8
con `VICE_ACAD` y `BIENESTAR` como raíces; `POST /api/Facultad` con
`fk_facultad_padre` igual al propio `id_facultad` → 400; `POST
/api/Dependencia` con el mismo patrón → 400; un `fk_padre` que no
existe → 500 (FK). **No** ejecutar todavía los `DELETE` de prueba de
§2 de [7_quickstart.md](7_quickstart.md) — eso se deja para la Fase 18,
cuando también exista `ProgramaAcademico` y `Catedra` y se pueda
verificar `SET NULL` de punta a punta.

## Fase 15 — La aplicación (capas de `ProgramaAcademico` y `AliasPrograma`)
> RF74-RF83 de [2_spec.md](2_spec.md), §2.3-2.4 de
> [5_data_model.md](5_data_model.md). Depende de la Fase 14
> (`fk_facultad`).

- [x] `Modelos/ProgramaAcademico.cs`, `AliasPrograma.cs` — en
      `AliasPrograma`, `IdAlias` y `CreadoEn` sin `required` (los pone
      la BD).
- [x] `Peticiones/ProgramaAcademico{Crear,Reemplazo,Actualizar}.cs` —
      `codigo` con `[RegularExpression("^[A-Z][0-9A-Z]{3,4}$")]`,
      `nivel` con `[RegularExpression]` sobre la lista cerrada (mismo
      patrón que `ModalidadCrear.canal_difusion` en v1).
- [x] `Peticiones/AliasProgramaCrear.cs` — **única** petición de esta
      entidad; `texto_normalizado` y `fk_programa` obligatorios,
      `origen` opcional. No crear `AliasProgramaReemplazo` ni
      `AliasProgramaActualizar` (§4.4 de [3_plan.md](3_plan.md): la
      ausencia es la especificación, no queda nada pendiente aquí).
- [x] `Repositorios/IRepositorioProgramaAcademico.cs` +
      `RepositorioProgramaAcademicoPostgres.cs` — CRUD estándar.
- [x] `Repositorios/IRepositorioAliasPrograma.cs` +
      `RepositorioAliasProgramaPostgres.cs` — **sin**
      `ActualizarAsync` en la interfaz (§4.4 de [3_plan.md](3_plan.md));
      `CrearAsync` usa `RETURNING id_alias, creado_en`.
- [x] `Servicios/IServicioProgramaAcademico.cs` +
      `ServicioProgramaAcademico.cs`.
- [x] `Servicios/IServicioAliasPrograma.cs` + `ServicioAliasPrograma.cs`
      — sin método de actualizar, mismo criterio que el repositorio.
- [x] `Controllers/ProgramaAcademicoController.cs` — 6 endpoints.
- [x] `Controllers/AliasProgramaController.cs` — **4** endpoints
      (`GET` lista, `GET/{id}`, `POST`, `DELETE`); sin `[HttpPut]` ni
      `[HttpPatch]`.
- [x] `Program.cs` — registrar las 2 parejas nuevas.

**Verificado** (contra PostgreSQL real): `GET /api/ProgramaAcademico` → 105 filas; `GET
/api/ProgramaAcademico/M0286` → `fk_facultad: "F_ING"`; `POST` con
`codigo: "m0001"` → 422; con `nivel: "TECNICO"` → 422. `GET
/api/AliasPrograma` → 3 filas, las tres con `fk_programa: "M0286"`; un
`POST` exitoso devuelve `id_alias` e ignora cualquier `creado_en` que
llegue en el body; `PUT /api/AliasPrograma/1` → **405** de ASP.NET (no 404: la RUTA sí
existe para GET/DELETE, lo que no existe es el VERBO; en ambos casos lo
responde el framework, sin código propio — confirma que el verbo de
verdad no está declarado).

## Fase 16 — La aplicación (capas de `Catedra`)
> RF84-RF89 de [2_spec.md](2_spec.md), §2.5 de
> [5_data_model.md](5_data_model.md). Depende de la Fase 14
> (`fk_dependencia`) y de `TipoEvento`, ya construido en v1
> (`fk_tipo_evento`).

- [x] `Modelos/Catedra.cs` — `IdCatedra`, `NombreAsis` y `CreadoEn` sin
      `required` (los pone la BD; `NombreAsis` además nunca se asigna
      a mano, solo la rellena el repositorio con lo que devuelve el
      `RETURNING`).
- [x] `Peticiones/Catedra{Crear,Reemplazo,Actualizar}.cs` —
      `id_evento_asis` con
      `[RegularExpression("^[0-9]{9}$")]` y `[StringLength(9)]`;
      **ninguna de las 3** declara `nombre_asis` (§4.3 de
      [3_plan.md](3_plan.md): si el cliente lo manda, Dapper/ASP.NET
      lo ignora al no existir la propiedad).
- [x] `Repositorios/IRepositorioCatedra.cs` +
      `RepositorioCatedraPostgres.cs` — `CrearAsync` devuelve
      `Task<(long IdCatedra, string NombreAsis)>` con
      `INSERT ... RETURNING id_catedra, nombre_asis` (código exacto en
      §4.3 de [3_plan.md](3_plan.md)).
- [x] `Servicios/IServicioCatedra.cs` + `ServicioCatedra.cs`.
- [x] `Controllers/CatedraController.cs` — 6 endpoints; el de `POST`
      arma la respuesta con la tupla `(IdCatedra, NombreAsis)` que
      devuelve el servicio.
- [x] `Program.cs` — registrar la pareja nueva.

**Verificado** (contra PostgreSQL real): `GET /api/Catedra?limite=5` → 5 filas, `total: 5502`;
`GET /api/Catedra/1` → `nombre_asis: "PELICULA:LA MIRADA DEL AMOR"`
(verificar que coincide con `LEFT(nombre, 30)` calculado por la BD, no
por el C#); `POST` con `id_evento_asis: "12345"` → 422; un
`fk_tipo_evento` que no existe en `tipo_evento` (v1) → 500.
**No ejecutar `DELETE` sobre `id_catedra` 1-5497 todavía** — tienen
sesiones de v4 ya sembradas (`ON DELETE CASCADE`, ver
[5_data_model.md](5_data_model.md) §2.5); esa prueba específica se deja
para la Fase 18, usando `id_catedra` 5498-5502.

## Fase 17 — Prueba de capas extendida (criterio de aceptación 8)
> Depende de las Fases 14-16 (las 5 entidades deben existir).

- [x] Agregar a `pruebas/Programa.cs` un repositorio FALSO y sus
      verificaciones para `Facultad`, `Dependencia`,
      `ProgramaAcademico`, `AliasPrograma` y `Catedra` (mismo patrón que
      v1, Fase 11). `RepositorioFacultadFalso` y
      `RepositorioDependenciaFalso` incluyen un caso que fuerce la
      autorreferencia y confirmen el `400`.
      `RepositorioAliasProgramaFalso` **no** implementa
      `ActualizarAsync` (no existe en la interfaz — ver Fase 15).
      `RepositorioCatedraFalso` simula el `RETURNING` devolviendo
      `(id, nombre[..30])` desde el repositorio falso, para poder
      verificar `NombreAsis` sin PostgreSQL.

**Verificado:** `dotnet run --project pruebas` → cubre las **15**
entidades, **153 pasadas, 0 fallidas**, código de salida 0 (89 que ya
existían en v1 + 64 nuevas: 14 del cierre de `TipoPregunta`/
`TipoVinculacion` y 50 de las 5 tablas de la v2).

## Fase 18 — Verificación de integridad referencial (criterios de aceptación 7 y 9)
> Depende de las Fases 14-16. Es la única fase de v2 dedicada
> exclusivamente a probar lo que la BD hace sola, sin código nuevo —
> por eso va separada: mezclarla con la Fase 14/16 habría significado
> probar `DELETE` antes de que todas las piezas relacionadas existieran.

- [x] Ejecutar, contra Docker + PostgreSQL reales y EN ESTE ORDEN
      (cada paso depende de que el de ejemplo siga intacto hasta
      llegar a él):
  1. `DELETE /api/Facultad/F_ING` → 200; confirmar con `GET
     /api/ProgramaAcademico/M0286` que `fk_facultad` quedó en `null`.
  2. `DELETE /api/Dependencia/DESA_HUM` → 200; confirmar que la
     cátedra de ejemplo que la usaba quedó con `fk_dependencia: null`.
  3. `DELETE /api/ProgramaAcademico/M0286` → 500 (bloqueado por
     `programa_asistente`, RESTRICT); confirmar con `GET
     /api/AliasPrograma` que los 3 alias de ejemplo SIGUEN ahí (la
     cascada no llegó a ejecutarse).
  4. `DELETE /api/Catedra/5502` → 200, sin efectos en otras tablas
     (una de las 5 sin sesiones).
- [x] `docker compose down -v && docker compose up -d --build` —
      revertir los 4 `DELETE` de arriba antes de seguir con cualquier
      otra prueba manual.

**Verificado** (contra PostgreSQL real, en este orden): los 4 resultados de arriba coinciden exactamente con lo
documentado en [5_data_model.md](5_data_model.md) §2.1-2.5 y en el
criterio de aceptación 7 de [2_spec.md](2_spec.md). Si alguno no
coincide, el modelo de datos de este documento tiene un error y hay que
corregirlo ahí antes de seguir — no "arreglar" el comportamiento con
código nuevo en la API (la integridad referencial es de la BD,
Artículo 5).

> **Nota de verificación:** las cátedras de ejemplo SIN sesiones son las
> de `id_catedra` 5498, 5500, 5502, 5503 y 5504 (los ids no son
> consecutivos: hay 5502 filas pero el último id es 5504). La 5502, que es
> la que usa el criterio 7, sí es segura: borrarla dejó `sesion` en 5714.

## Fase 19 — Frontend: integración de la plantilla
> RF92-RF93 de [2_spec.md](2_spec.md), §4.6 de [3_plan.md](3_plan.md).
> No depende de ninguna fase de la API — puede hacerse en paralelo con
> las Fases 14-18, pero el catálogo (Fase 20) sí depende de que esta
> fase termine primero (si no, se construiría dos veces, ver §4.6 de
> [3_plan.md](3_plan.md)).

- [x] Copiar `base.html`, `partials/navbar.html`, `partials/footer.html`
      y los estáticos de la plantilla externa sobre los actuales de
      `front_catedras/`.
- [x] Copiar `auth_helpers.py` a la raíz de `front_catedras/`; completar
      solo el helper que `base.html` necesita, devolviendo siempre "sin
      sesión" en esta versión.
- [x] Corregir en `navbar.html` cada `url_for(...)` roto, mapeándolo a
      uno de los 6 blueprints reales que `app.py` ya registra.
- [x] Reemplazar el puerto `5001` cableado en la plantilla por el que
      usa `run.py` (hoy `5000`).
- [x] Eliminar cualquier `try/except` en `servicios/api_catedras.py` o
      en las rutas que sirva datos mock cuando la API falla (RF93).

> **Hallazgo al implementar:** la plantilla externa (`Desktop/Front`) no
> está en el repositorio. El front actual ya tenía el diseño USB
> (Bootstrap 5 + CSS institucional), así que la fase se aplicó sobre él:
> se creó `auth_helpers.py` (siempre "sin sesión", registrado como
> `context_processor`), el navbar quedó sin `url_for` rotos (más el
> enlace al catálogo), no había ningún `5001` cableado, y
> `servicios/api_catedras.py` no tenía respaldo a datos mock (RF93 ya
> se cumplía: cada error de la API se muestra en pantalla).

**Verificado:** con la API apagada (`docker compose stop api`),
`http://localhost:5000` y cualquier ruta que consuma la API muestran un
error, nunca datos inventados; con la API prendida, el navbar no
produce ningún `werkzeug.routing.BuildError`.

## Fase 20 — Frontend: catálogo público y administración de las 5 tablas
> RF90-RF91, RF94 de [2_spec.md](2_spec.md). Depende de la Fase 19
> (plantilla integrada) y de las Fases 14-16 (API completa).

- [x] `routes/catedras.py` — reemplazar el placeholder: `GET /catedras`
      llama una vez a `GET /api/Catedra` (sin `limite`, ADR D13 de
      [4_research.md](4_research.md)), filtra por `?q=` y pagina con
      `?pagina=` en Python, sin JavaScript nuevo (código de forma en
      §4.7 de [3_plan.md](3_plan.md)).
- [x] `templates/catedras.html` — listado paginado + buscador.
- [x] `GET /catedras/{idCatedra}` + `templates/detalle_catedra.html` —
      si `fk_dependencia` no es nulo, una llamada adicional a
      `GET /api/Dependencia/{id}` para mostrar su nombre.
- [x] `routes/admin.py` — 5 bloques CRUD nuevos (mismo patrón que las
      10 de v1); el de `Facultad`/`Dependencia` arma el `<select>` de
      posibles padres con una llamada previa a `GET /api/Facultad` o
      `GET /api/Dependencia`; el de `AliasPrograma` solo ofrece crear y
      eliminar.
- [x] `templates/admin/` — 10 templates nuevos (`_form.html` +
      `_lista.html` por cada una de las 5 tablas), reutilizando el
      layout ya corregido en la Fase 19.

**Verificado** (contra PostgreSQL real): `http://localhost:5000/catedras` pagina las 5502
cátedras y el buscador `"amor"` encuentra `000023636` sin importar la
página; `http://localhost:5000/catedras/1` muestra el detalle completo;
`http://localhost:5000/admin/` tiene 15 tarjetas en total (10 de v1 +
5 de v2), con CRUD de punta a punta contra Docker/PostgreSQL reales.

> Además se completaron las pantallas de `TipoPregunta` y
> `TipoVinculacion` (plantillas de la v1 que no tenían rutas en
> `admin.py`), para que el panel tenga de verdad las 15 tarjetas.

## Fase 20b — Retrofit: maestro-detalle `ProgramaAcademico` + `AliasPrograma` (Guía IA, Regla 1)
> RF76 y RF94 (texto de retrofit) y criterio 11 de [2_spec.md](2_spec.md);
> §4.8 de [3_plan.md](3_plan.md); ADR D14 de [4_research.md](4_research.md);
> §2.3.1 de [5_data_model.md](5_data_model.md). Regla 2 (FK = `<select>`)
> ya se cumplía; Regla 3 (BCrypt) no aplica hasta la v5.

- [x] `db/postgres/migraciones/001_sp_guardar_programa_academico.sql` —
      `CREATE OR REPLACE PROCEDURE` (re-ejecutable); `p_nivel` con el
      mismo tamaño que la columna (`varchar(25)`).
- [x] `docker-compose.yml` — montar la migración como
      `/docker-entrypoint-initdb.d/z001_...sql` (corre DESPUÉS del script
      provisto: orden alfabético).
- [x] `Peticiones/AliasInicial.cs` (nuevo) + `ProgramaAcademicoCrear.cs`
      → `List<AliasInicial>? Alias`.
- [x] `IRepositorioProgramaAcademico.cs` + `RepositorioProgramaAcademicoPostgres.cs`
      → `CrearAsync(programa, aliasIniciales)` hace `CALL` al SP con
      Dapper (`CAST(@Alias AS jsonb)`); el JSON se arma aquí (detalle del
      motor, Artículo 3).
- [x] `Servicios/ServicioProgramaAcademico.cs` → `CrearAsync` convierte
      los alias de la petición en `AliasPrograma` con `fk_programa` =
      código del maestro y `origen` por defecto.
- [x] `Program.cs` → `LimpiarNombreCampo` deja `alias[0].texto_normalizado`
      (antes salía `alias[0]._texto_normalizado`).
- [x] `pruebas/Programa.cs` → repositorio falso con la nueva firma + 5
      verificaciones del maestro-detalle.
- [x] `front_catedras/routes/admin.py` + `templates/admin/programa_form.html`
      → `<textarea>` "Alias iniciales" (uno por línea), solo al crear.
- [x] Docs: `2_spec.md`, `3_plan.md`, `4_research.md`, `5_data_model.md`,
      `6_contracts.md`, `7_quickstart.md`, este archivo.

**Verificado** (PostgreSQL 16 real con `script_bdcatedras.sql` + la
migración; API compilada con .NET 10): el SP se aplica 2 veces sin
error; POST sin `alias`, con `[]` y con 2 alias → 200 (los 2 alias
quedan con `fk_programa` correcto y `origen` por defecto); POST con un
alias repetido → 500 y el programa NO queda creado (404); alias sin
texto → 422 `alias[0].texto_normalizado`; `alias: "hola"` → 422;
`POST /api/AliasPrograma` standalone sin cambios; prueba de capas
**158 pasadas, 0 fallidas**; el formulario Flask crea programa + alias
y, ante un alias repetido, no crea nada y conserva lo escrito.
**Pendiente:** repetir el criterio 11 con `docker compose` en tu equipo
(Fase 21).

## Fase 21 — Cierre de la v2
> Última fase. Solo entonces se escribe la spec de la v3 (Artículo 1).

- [ ] Confirmar que los 11 criterios de aceptación de
      [2_spec.md](2_spec.md) §6 pasan, en orden, sobre un entorno recién
      reseteado (`down -v && up -d --build`).
- [ ] `dotnet run --project pruebas` → anotar el total final de
      verificaciones pasadas (Fase 17; con el retrofit deben ser 158).
- [ ] Commit y tag: `git tag v2`.
