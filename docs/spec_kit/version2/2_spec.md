# Especificación — Versión 2 del proyecto: estructura académica y catálogo de cátedras

> **Versión 2** del desarrollo incremental. Rige la constitución del
> proyecto: [../1_constitution.md](../1_constitution.md). Mapa completo
> de versiones: [../versiones/0_mapa_versiones.md](../versiones/0_mapa_versiones.md).
>
> En v2 el sistema ES esto: la API agrega las **5 tablas** cuyas foreign
> keys salientes solo apuntan a tablas que ya existen en la API (las 10
> de la v1) o a sí mismas: `facultad`, `dependencia`,
> `programa_academico`, `alias_programa` y `catedra`. Con esto queda
> expuesto un **catálogo de cátedras real**, que el front consume.
>
> **Frontend:** `front_catedras/` (Flask) recibe en esta versión el
> diseño definitivo de plantilla (base, navbar, CSS, footer) y dos
> páginas públicas nuevas (`catedras.html`, `detalle_catedra.html`); el
> CRUD de administración se extiende a las 5 tablas nuevas, con el mismo
> patrón que ya usa para las 10 de la v1.
>
> | Documento de esta versión | Contenido |
> |---|---|
> | **2_spec.md** (este) | QUÉ construir en v2 y sus criterios de aceptación |
> | [3_plan.md](3_plan.md) | CÓMO: capas de la API y plan de integración del front |
> | [4_research.md](4_research.md) | Decisiones y alternativas *(lectura opcional)* |
> | [5_data_model.md](5_data_model.md) | Las 5 tablas de esta versión, documentadas |
> | [6_contracts.md](6_contracts.md) | Contratos de integración (sin cambios de puertos/healthchecks respecto a v1) |
> | [7_quickstart.md](7_quickstart.md) | Arranque y smoke test de v2 |
> | [8_tasks.md](8_tasks.md) | Orden de construcción |

---

## 1. Propósito de la v2

La v1 dejó el esqueleto arquitectónico validado con 10 tablas sin ninguna
relación entre sí. La v2 agrega la primera **cadena de foreign keys**:

```
facultad (↺ jerarquía propia)
   ↑
programa_academico ── alias_programa

dependencia (↺ jerarquía propia)
   ↑
catedra ──→ tipo_evento (v1, ya existe)
```

Esto obliga a resolver, por primera vez en el proyecto, tres problemas
nuevos que la v1 no tenía:

1. **Autorreferencia** (`facultad`/`dependencia` apuntan a sí mismas):
   el servicio debe impedir que una fila sea su propio padre, además de
   lo que ya vigila el `CHECK` de la BD.
2. **FK hacia otra tabla de la misma versión** (`programa_academico →
   facultad`, `alias_programa → programa_academico`, `catedra →
   dependencia`): un id que no existe en la tabla padre lo rechaza el
   motor (`500`), igual que un duplicado — la API no revalida lo que la
   BD ya garantiza.
3. **Columna generada por la BD** (`catedra.nombre_asis`, `GENERATED
   ALWAYS AS (left(nombre, 30)) STORED`): es de solo lectura; no
   aparece en ninguna petición, solo en la respuesta.

Al terminar la v2, el catálogo de cátedras (5502 filas reales, ya
sembradas por el script de la v1) es consultable por API y navegable
desde el front.

## 2. Alcance

**Incluye:**
- CRUD completo de `Facultad`, `Dependencia`, `ProgramaAcademico`,
  `AliasPrograma` y `Catedra`, mismo patrón de 5 verbos que la v1
  (`GET` lista, `GET/{id}`, `POST`, `PUT`, `PATCH`, `DELETE`).
- Validación de autorreferencia en `Facultad` y `Dependencia` (servicio).
- `Catedra.NombreAsis` expuesto en las respuestas, nunca aceptado en
  una petición.
- Frontend: diseño de plantilla aplicado a `front_catedras/`
  (correcciones del §4) y catálogo público de cátedras
  (`catedras.html` con buscador/filtro simple, `detalle_catedra.html`).
  El CRUD de administración existente se extiende a las 5 tablas nuevas.

**No incluye (es de otra versión):**
- `sesion`, `ponencia` y cualquier otra tabla con FK hacia `catedra`
  — v4 (ver mapa de versiones).
- `asistente` y las 6 tablas de carga — v3.
- Importar o depurar los datos de `alias_programa` más allá del CRUD
  simple; la tabla ya trae 3 filas de ejemplo y es, por diseño, una
  tabla viva que se llena con el tiempo (comentario del propio esquema).
- Un árbol interactivo (plegable) para `facultad`/`dependencia` en el
  front; el listado y el formulario muestran el padre, pero la
  navegación jerárquica visual queda para cuando haya una necesidad
  concreta que la pida (Artículo 1, YAGNI).

## 3. Requisitos funcionales — API

> Mismo formato de sobre que la v1 (Artículo 8): lecturas
> `{tabla, limite, total, datos}`; errores `{estado, mensaje, detalle}`
> (+ `errores:[…]` en el 422). `POST`/`PUT` sin un campo obligatorio →
> 422; valor que no existe en la tabla referenciada → 500 (violación de
> FK, igual que un duplicado); `ArgumentException` del servicio → 400;
> no encontrado → 404.

### Facultad

> **Nota de diseño (autorreferencia):** `fk_facultad_padre` es opcional
> (la raíz `USB` no tiene padre). El `CHECK
> (fk_facultad_padre <> id_facultad)` de la BD impide que una fila sea
> su propio padre, pero solo lo nota si el cliente repite el mismo
> `id_facultad` que ya existe — en un `POST` nuevo eso es imposible
> porque el id todavía no existe. El servicio valida explícitamente,
> antes del `INSERT`, que `fk_facultad_padre` (si llega) sea distinto de
> `id_facultad` del mismo body, para dar un `400` claro en vez de
> depender del `CHECK` en un caso donde nunca se dispararía por sí solo.
> Un `fk_facultad_padre` que no exista como facultad → 500 (viola la FK).

- **RF62 — Listar** (`GET /api/Facultad`): 200
  `{tabla, limite, total, datos}`; `limite` opcional; vacía → 204.
- **RF63 — Obtener por código** (`GET /api/Facultad/{idFacultad}`): 200;
  inexistente → 404.
- **RF64 — Crear** (`POST /api/Facultad` con **FacultadCrear**):
  `id_facultad` obligatorio (1–10), `nombre` obligatorio (1–120),
  `fk_facultad_padre` opcional, `activo` opcional (por defecto `true`).
  Autorreferencia → 400. Duplicado (`id` o `uq_facultad_nombre`) → 500.
  `fk_facultad_padre` inexistente → 500.
- **RF65 — Reemplazar** (`PUT /api/Facultad/{idFacultad}` con
  **FacultadReemplazo**): `nombre` y `activo` obligatorios;
  `fk_facultad_padre` se envía siempre, aunque sea `null` (reemplazo
  completo). Autorreferencia → 400; inexistente → 404.
- **RF66 — Actualizar parcialmente** (`PATCH /api/Facultad/{idFacultad}`
  con **FacultadActualizar**): todo opcional. Autorreferencia (si llega
  `fk_facultad_padre`) → 400. Body vacío → 400; inexistente → 404.
- **RF67 — Eliminar** (`DELETE /api/Facultad/{idFacultad}`):
  `filasEliminadas`; inexistente → 404. **No falla por tener
  dependientes**: tanto `fk_facultad_padre` (en facultades hijas) como
  `fk_facultad` (en `ProgramaAcademico`) son `ON DELETE SET NULL` en el
  esquema — al borrar una facultad, sus hijas y los programas que la
  tenían asignada quedan con ese campo en `null`, no se bloquea el
  borrado ni se arrastran filas.

### Dependencia

> Misma forma y misma regla de autorreferencia que `Facultad` —
> `fk_dependencia_padre <> id_dependencia`, validada en el servicio
> antes del `INSERT`/`UPDATE`.

- **RF68 — Listar** (`GET /api/Dependencia`): 200; vacía → 204.
- **RF69 — Obtener por código** (`GET /api/Dependencia/{idDependencia}`):
  200; inexistente → 404.
- **RF70 — Crear** (`POST /api/Dependencia` con **DependenciaCrear**):
  `id_dependencia` obligatorio (1–15), `nombre` obligatorio (1–120),
  `fk_dependencia_padre` opcional, `activo` opcional (por defecto
  `true`). Autorreferencia → 400. Duplicado → 500. Padre inexistente →
  500.
- **RF71 — Reemplazar** (`PUT .../{idDependencia}` con
  **DependenciaReemplazo**): `nombre` y `activo` obligatorios;
  `fk_dependencia_padre` siempre presente (o `null`). Autorreferencia →
  400; inexistente → 404.
- **RF72 — Actualizar parcialmente** (`PATCH .../{idDependencia}` con
  **DependenciaActualizar**): todo opcional. Body vacío → 400;
  inexistente → 404.
- **RF73 — Eliminar** (`DELETE .../{idDependencia}`): `filasEliminadas`;
  inexistente → 404. Mismo criterio que `Facultad`: `fk_dependencia_padre`
  y `Catedra.fk_dependencia` son `ON DELETE SET NULL` — borrar una
  dependencia con hijas o con cátedras asociadas no falla, las deja con
  ese campo en `null`.

### ProgramaAcademico

> **Nota de diseño (formato del código):** `codigo` es `VARCHAR(5)` con
> `CHECK (codigo ~ '^[A-Z][0-9A-Z]{3,4}$')` — una letra mayúscula
> seguida de 3 o 4 alfanuméricos (p. ej. `M0286`). Es una comprobación de
> **forma**, igual que `id_periodo` de `PeriodoAcademico` (v1): se valida
> en la PETICIÓN con `[RegularExpression]`, no en el servicio.
>
> **Nota de diseño (lista cerrada):** `nivel` acepta solo `PREGRADO`,
> `ESPECIALIZACION`, `MAESTRIA`, `DOCTORADO`, `EDUCACION_CONTINUA` u
> `OTRO` (`CHECK` del esquema) — mismo patrón que `canal_difusion` de
> `Modalidad` (v1): la petición valida con `[RegularExpression]` sobre
> la lista exacta, no con un `enum` de C# (la BD no usa un tipo `ENUM`,
> así que el valor viaja como texto).

- **RF74 — Listar** (`GET /api/ProgramaAcademico`): 200; vacía → 204.
- **RF75 — Obtener por código** (`GET /api/ProgramaAcademico/{codigo}`):
  200; inexistente → 404.
- **RF76 — Crear** (`POST /api/ProgramaAcademico` con
  **ProgramaAcademicoCrear**): `codigo` obligatorio (formato validado),
  `nombre` obligatorio (1–150), `fk_facultad` opcional, `nivel` opcional
  (por defecto `PREGRADO`, validado contra la lista si llega), `activo`
  opcional (por defecto `true`). `codigo` con formato inválido → 422;
  `nivel` fuera de la lista → 422; duplicado → 500; `fk_facultad`
  inexistente → 500.
- **RF77 — Reemplazar** (`PUT .../{codigo}` con
  **ProgramaAcademicoReemplazo**): `nombre`, `nivel` y `activo`
  obligatorios; `fk_facultad` siempre presente (o `null`). Inexistente →
  404.
- **RF78 — Actualizar parcialmente** (`PATCH .../{codigo}` con
  **ProgramaAcademicoActualizar**): todo opcional. Body vacío → 400;
  inexistente → 404.
- **RF79 — Eliminar** (`DELETE .../{codigo}`): `filasEliminadas`;
  inexistente → 404. **Dos FK entrantes con reglas distintas:**
  `alias_programa.fk_programa` es `ON DELETE CASCADE` (borrar el
  programa borraría también sus alias, sin aviso), pero
  `programa_asistente.fk_programa` —de la v3, aunque la tabla ya viene
  sembrada por el script de la v1— es `ON DELETE RESTRICT`. Postgres
  evalúa ambas FK en la misma sentencia: si el programa tiene algún
  `programa_asistente`, el `DELETE` completo falla con 500 **antes** de
  que la cascada hacia `alias_programa` llegue a ejecutarse. En la
  práctica, con los datos de ejemplo, casi ningún `ProgramaAcademico` se
  puede borrar hoy por esta razón — ver el criterio de aceptación 7.

### AliasPrograma

> **Nota de diseño (tabla de solo-anexo):** `creado_en` es
> `TIMESTAMPTZ DEFAULT now()` — la BD la asigna; **no se acepta en
> ninguna petición**, ni siquiera opcional (igual criterio que
> `id_alias`, que es `IDENTITY`). Esta tabla es, por el comentario del
> propio esquema, un registro histórico de texto libre: no tiene verbo
> `PUT`/`PATCH` de actualización de contenido en v2 — si un alias quedó
> mal escrito, se borra y se crea de nuevo (mantiene `creado_en`
> honesto). Esto es una restricción deliberada, no un olvido.

- **RF80 — Listar** (`GET /api/AliasPrograma`): 200; vacía → 204.
- **RF81 — Obtener por id** (`GET /api/AliasPrograma/{idAlias}`): 200;
  inexistente → 404.
- **RF82 — Crear** (`POST /api/AliasPrograma` con
  **AliasProgramaCrear**): `texto_normalizado` obligatorio (1–200,
  único), `fk_programa` obligatorio, `origen` opcional (por defecto
  `FORMULARIO`). Duplicado de `texto_normalizado` → 500; `fk_programa`
  inexistente → 500. Responde el `id_alias` generado por la BD.
- **RF83 — Eliminar** (`DELETE /api/AliasPrograma/{idAlias}`):
  `filasEliminadas`; inexistente → 404.

### Catedra

> **Nota de diseño (columna generada):** `nombre_asis` es `GENERATED
> ALWAYS AS (left(nombre, 30)) STORED` — la BD la calcula a partir de
> `nombre` en cada `INSERT`/`UPDATE`. No existe en **ninguna** de las 3
> peticiones (`CatedraCrear`, `CatedraReemplazo`, `CatedraActualizar`);
> sí viaja en el modelo de respuesta. Enviarla en un body no es un error
> (el `[JsonPropertyName]` simplemente no está declarado en la
> petición: el campo, si llega, se ignora al deserializar).
>
> **Nota de diseño (formato ASIS):** `id_evento_asis` es `CHAR(9)` con
> `CHECK (id_evento_asis ~ '^[0-9]{9}$')` — 9 dígitos con ceros a la
> izquierda (comentario del propio esquema: como entero pierde los
> ceros y deja de servir para la carga). Se valida en la petición con
> `[RegularExpression]`, nunca como `int`.
>
> **Nota de diseño (FK a dos tablas, una de otra versión):**
> `fk_tipo_evento` referencia `tipo_evento`, que ya existe en la API
> desde la v1 — un código que no exista ahí → 500, igual que cualquier
> otra FK. `fk_dependencia` es **opcional** (en los datos reales, 5501
> de las 5502 cátedras no tienen dependencia asignada todavía).

- **RF84 — Listar** (`GET /api/Catedra`): 200 `{tabla, limite, total,
  datos}`; `limite` opcional — con 5502 filas, el front SIEMPRE pagina
  con `limite` en el catálogo público; vacía → 204.
- **RF85 — Obtener por id** (`GET /api/Catedra/{idCatedra}`): 200;
  inexistente → 404.
- **RF86 — Crear** (`POST /api/Catedra` con **CatedraCrear**):
  `id_evento_asis` obligatorio (formato validado, único), `nombre`
  obligatorio (1–200), `fk_tipo_evento` obligatorio, `fk_dependencia`
  opcional, `activo` opcional (por defecto `true`). Formato de
  `id_evento_asis` inválido → 422; duplicado → 500; `fk_tipo_evento` o
  `fk_dependencia` inexistente → 500. Responde el `id_catedra` generado
  por la BD y el `nombre_asis` ya calculado.
- **RF87 — Reemplazar** (`PUT .../{idCatedra}` con
  **CatedraReemplazo**): `id_evento_asis`, `nombre`, `fk_tipo_evento` y
  `activo` obligatorios; `fk_dependencia` siempre presente (o `null`).
  Inexistente → 404.
- **RF88 — Actualizar parcialmente** (`PATCH .../{idCatedra}` con
  **CatedraActualizar**): todo opcional. Body vacío → 400; inexistente →
  404.
- **RF89 — Eliminar** (`DELETE .../{idCatedra}`): `filasEliminadas`;
  inexistente → 404. **Advertencia (dato verificado, no hipotético):**
  `sesion.fk_catedra` es `ON DELETE CASCADE`, y la BD ya trae 5714
  sesiones sembradas desde la v1 (son de la v4, que la API todavía no
  expone). De las 5502 cátedras de ejemplo, **5497 ya tienen al menos
  una sesión**: borrarlas borra en cascada, sin aviso, esas sesiones
  ocultas. Solo las cátedras con `id_catedra` 5498 a 5502 no tienen
  ninguna. El smoke test (`7_quickstart.md`) prueba este verbo contra
  una cátedra creada por el propio test, nunca contra una de las 5502
  sembradas.

## 4. Requisitos funcionales — Frontend

> **Supuesto a confirmar:** el "diseño de plantilla" de este bloque es
> un proyecto externo del estudiante (`Desktop/Front`, mencionado en el
> plan de v2), que **no viene en este repositorio** y que yo no he
> podido inspeccionar. Las tres correcciones de abajo (RF91) las doy
> por ciertas porque las describiste tú; si al traer la plantilla real
> alguna no aplica, se ajusta `8_tasks.md` sin tocar este documento.
> El front *actual* (`front_catedras/`, ya con Bootstrap 5 + identidad
> USB) sigue siendo la base sobre la que se integra.

- **RF90 — Catálogo público de cátedras** (`GET /catedras`,
  blueprint `catedras`, reemplaza el placeholder de la inicio): lista
  paginada contra `GET /api/Catedra?limite=…`, con buscador de texto
  libre por `nombre` (filtrado en el propio template, sin un endpoint
  nuevo en la API) y filtro por `fk_tipo_evento`.
- **RF91 — Detalle de cátedra** (`GET /catedras/{idCatedra}`,
  `detalle_catedra.html`): consume `GET /api/Catedra/{id}` y, si
  `fk_dependencia` no es nulo, el nombre de la dependencia
  (`GET /api/Dependencia/{id}`).
- **RF92 — Integración de la plantilla** (corrige lo heredado del
  proyecto externo antes de fusionarlo):
  - Completar `auth_helpers.py` (hoy vacío en la plantilla) con, como
    mínimo, el helper que el `base.html` de la plantilla espera para
    saber si hay sesión — en v2 siempre devuelve "sin sesión", porque
    el login real es de la v5 (mapa de versiones).
  - Corregir en el navbar de la plantilla los `endpoint` que no existen
    en este proyecto, apuntándolos a los blueprints reales
    (`catedras.inicio`, `admin.panel`, etc.).
  - Puerto: la plantilla trae `5001` cableado; se reemplaza por el
    puerto del front de este proyecto (hoy `5000` en `run.py`, Artículo
    8 reserva `8142` para la API).
- **RF93 — Quitar el respaldo a datos mock**: cualquier `try/except` en
  `servicios/api_catedras.py` o en las rutas que, ante un error de la
  API, sirva datos de ejemplo en vez de propagar el error — se elimina;
  un error de la API se muestra como error en pantalla (mismo criterio
  de "la validación es la frontera" que ya rige en el backend, RF5 de
  la v1).
- **RF94 — CRUD de administración de las 5 tablas nuevas**: mismo
  patrón que las 10 de la v1 (`admin_bp`, formularios HTML que traducen
  a PUT/DELETE). Para `Facultad` y `Dependencia`, el formulario incluye
  un `<select>` con las filas existentes como posible padre (y la
  opción "ninguno"). `AliasPrograma` solo tiene listar/crear/eliminar
  (RF80-83: no hay verbo de actualización).

## 5. Requisitos no funcionales

Rigen los mismos RNF1-RNF6 de la v1 ([2_spec.md](../version1/2_spec.md)
§4), sin cambios. Se añade uno nuevo, específico de v2:

- **RNF7 — Columnas generadas, de solo lectura:** una columna
  `GENERATED ALWAYS ... STORED` (hoy solo `catedra.nombre_asis`) nunca
  se declara en una clase de `Peticiones/`; solo en el `Modelo/` de
  respuesta. Si una versión futura agrega otra columna generada, se
  trata igual.

## 6. Criterios de aceptación

1. `docker compose up -d --build` deja los 15 recursos (10 de v1 + 5 de
   v2) respondiendo en `/swagger`.
2. `GET /api/Facultad` devuelve 9 facultades de ejemplo, con `USB` como
   raíz (`fk_facultad_padre: null`); un `POST` con
   `fk_facultad_padre` igual al propio `id_facultad` → 400.
3. `GET /api/Dependencia` devuelve 8 dependencias de ejemplo, con
   `VICE_ACAD` y `BIENESTAR` como raíces.
4. `GET /api/ProgramaAcademico` devuelve 105 programas; un `POST` con
   `codigo: "m0286"` (minúscula) → 422; uno con `nivel: "TECNICO"` →
   422 (no está en la lista cerrada).
5. `GET /api/AliasPrograma` devuelve 3 alias de ejemplo, los tres con
   `fk_programa: "M0286"`; un `POST` exitoso devuelve el `id_alias`
   generado y no acepta `creado_en` en el body (si se manda, se
   ignora).
6. `GET /api/Catedra?limite=10` devuelve 10 de las 5502 cátedras de
   ejemplo con `total: 5502`; `GET /api/Catedra/1` devuelve la cátedra
   `000023636` con `nombre_asis: "PELICULA:LA MIRADA DEL AMOR"[:30]` ya
   calculado por la BD; un `POST` con `id_evento_asis: "123"` → 422
   (no son 9 dígitos).
7. Integridad referencial, con comportamiento verificado contra los
   datos de ejemplo reales (no asumido):
   - `DELETE /api/Facultad/F_ING` → **200** (`filasEliminadas: 1`);
     después, `GET /api/ProgramaAcademico/M0286` muestra
     `fk_facultad: null` (`F_ING` era su única facultad).
   - `DELETE /api/Dependencia/DESA_HUM` → **200**; es a la vez hija de
     `BIENESTAR` y la única dependencia usada por una cátedra de
     ejemplo — ambas quedan con el campo en `null`, no se bloquea nada.
   - `DELETE /api/ProgramaAcademico/M0286` → **500**: tiene 58 filas en
     `programa_asistente` (sembradas desde la v1 aunque esa tabla es de
     la v3), y esa FK es `RESTRICT`. Sus 3 `alias_programa` NO se
     borran (la sentencia completa se revierte).
   - `DELETE /api/Catedra/5502` → **200**, sin efectos secundarios (es
     una de las 5 sin sesiones asociadas). Repetir contra
     `/api/Catedra/1` NO forma parte de este criterio — borraría una
     sesión sembrada de la v4 (ver RF89).
8. **Prueba de capas ampliada:** `dotnet run --project pruebas` cubre
   las 15 entidades sin PostgreSQL (incluye la validación de
   autorreferencia de `Facultad`/`Dependencia` con un repositorio
   FALSO).
9. El catálogo público (`http://localhost:5000/catedras`) muestra el
   listado paginado y el buscador filtra por nombre sin recargar la
   lista completa del servidor; `http://localhost:5000/catedras/1`
   muestra el detalle de la primera cátedra de ejemplo.
10. El panel de administración (`/admin`) muestra las 5 tarjetas
    nuevas, con CRUD funcional de punta a punta contra Docker/PostgreSQL
    reales, igual que las 10 de la v1.
