# Contratos de integración — Versión 4: sesiones y ponencias

> ✅ **Revisada y construida (2026-10-08)** — ver nota de [2_spec.md](2_spec.md).
>
> **Documento 6 de 8** del spec kit: cómo se hablan las piezas entre sí
> (puertos, hosts, variables, healthchecks). Los endpoints HTTP de la
> API (verbos, formatos exactos) están en [2_spec.md](2_spec.md)
> (RF119-RF131) y se navegan en vivo desde `/swagger`.
>
> **Nota de alcance (v4):** la v4 **no cambia ningún contrato de
> infraestructura** de v1-v3 — mismo mapa de puertos, mismas variables
> de entorno, mismos dos servicios, mismo healthcheck, y **ningún
> montaje nuevo** en `docker-entrypoint-initdb.d/` (es la primera
> versión desde v1 sin SP nuevo). Lo único que crece es el **contrato
> de la API** (§3): 2 recursos más, uno de ellos con rutas de llave
> compuesta de 3 segmentos (nuevas en el proyecto, aunque de forma
> distinta a las de v3).

---

## 1. Mapa de puertos

Sin cambios: [version1/6_contracts.md](../version1/6_contracts.md) §1.

## 2. Variables de entorno

Sin cambios: [version1/6_contracts.md](../version1/6_contracts.md) §2.

## 3. Contrato de la API (lo que crece en v4)

- **`Sesion`** — 6 verbos, molde completo:
  `GET/POST /api/Sesion` (el `GET` acepta `?fkCatedra=` y `?limite=`),
  `GET/PUT/PATCH/DELETE /api/Sesion/{idSesion}`. `POST`/`PUT`/`PATCH`
  con `fin <= inicio` → **400** (`ArgumentException`, validado en el
  servicio). `numero_reunion` ausente o `0` en `POST` → la BD lo
  calcula; un valor explícito se respeta; en `PUT`/`PATCH` debe ser ≥ 1
  (si no, **422**). Un `fk_modalidad`/`fk_sede`/`fk_periodo`/
  `fk_catedra`/`fk_encuesta` inexistente → **500**.
  - Fechas (ADR D21): se **envían con zona**
    (`"inicio": "2026-10-20T14:00:00-05:00"`) y se **reciben en UTC**
    (`"inicio": "2026-10-20T19:00:00Z"`).
  - Respuesta del `POST`:
    ```json
    { "estado": "creada", "mensaje": "Sesión 1 de la cátedra 5498 creada.",
      "id_sesion": 5715, "numero_reunion": 1, "creado_en": "2026-10-08T16:20:57.588655Z" }
    ```
- **`Ponencia` — rutas de llave compuesta, 4 verbos (ADR D19):**
  - `GET /api/Ponencia?fkSesion=&fkAsistente=` — lista, ambos filtros
    opcionales.
  - `GET /api/Ponencia/{fkSesion}/{fkAsistente}/{rol}` — puntual; 200
    si existe esa combinación exacta, **404** si no.
  - `POST /api/Ponencia` con body
    `{"fk_sesion": 10, "fk_asistente": 500, "rol": "PONENTE"}` (`rol`
    opcional, por defecto `PONENTE`) → **200**
    `{estado, mensaje, ponencia: {fk_sesion, fk_asistente, rol}}`.
    Combinación exacta ya existente → **500** (PK duplicada).
  - `DELETE /api/Ponencia/{fkSesion}/{fkAsistente}/{rol}` —
    `{estado, mensaje, filasEliminadas}`; combinación inexistente →
    **404**.
  - **Sin** `PUT`, `PATCH` ni un `GET`/`DELETE` con un solo `{id}`: esas
    rutas no existen y responden **404** de ASP.NET (el `Controller` no
    las declara) — a diferencia de v3 (D16), donde la ruta inexistente
    era `PUT`/`DELETE` sobre un `/{id}` que tampoco existe pero por una
    razón distinta (ahí la tabla sí tiene listado por `/{id}` lógico
    ausente de otra forma). Aquí simplemente no hay columna `id` ni
    falta hacer "cerrar" nada.
- Errores: mismo sobre que v1-v3 — 422 (petición inválida), 400
  (`ArgumentException`: `fin <= inicio`), 404 (`NoEncontradoExcepcion`,
  incluida la combinación puntual de `Ponencia`), 500
  (`NpgsqlException`: cualquier FK inexistente, `uq_sesion_reunion`,
  PK duplicada de `ponencia`, cualquier `CHECK` de la tabla).
- **Nuevo en v4 — un `DELETE` exitoso puede, por primera vez, estar
  bloqueado por filas de una tabla que la API todavía no expone en
  absoluto (no solo que no haya construido su CRUD, como en v2 con
  `sesion`, sino que su spec ni siquiera se ha escrito):**
  `DELETE /api/Sesion/{id}` puede responder 500 por un
  `registro_asistencia` o `lote_migracion` de ejemplo (v5 y v7, ya
  sembrados) que esta versión no puede nombrar ni consultar
  directamente (Artículo 1: una spec solo nombra tablas de su versión o
  anteriores). El smoke test (§2 de
  [7_quickstart.md](7_quickstart.md)) evita esas sesiones a propósito,
  usando `id_sesion` 1-5 para las pruebas de `DELETE` limpio.

## 4. Contrato API ↔ BD

Sin cambios de mecánica: [version1/6_contracts.md](../version1/6_contracts.md)
§4. Es la primera versión desde v1 donde **ningún** repositorio nuevo
usa `CALL`: los dos (`RepositorioSesionPostgres`,
`RepositorioPonenciaPostgres`) son `INSERT`/`UPDATE`/`DELETE`/`SELECT`
directos, con Dapper como simple ejecutor (sin ORM, Artículo 2). La
única mecánica nueva es que `RepositorioSesionPostgres.CrearAsync` usa
`RETURNING numero_reunion` para leer de vuelta un valor que **la BD
decidió**, no el C# (mismo patrón que `NombreAsis` en `Catedra`, v2).

## 5. Healthchecks y arranque

Sin cambios: [version1/6_contracts.md](../version1/6_contracts.md) §5.

## 6. Contratos de volúmenes y montajes

**Sin cambios — y sin ningún montaje nuevo.** Las 2 tablas de v4 ya
estaban en `script_bdcatedras.sql` desde el montaje de la v1; a
diferencia de v2 (`z001_...`) y v3 (`z002_...`, `z003_...`), v4 no
agrega ningún archivo a `db/postgres/migraciones/` ni a
`docker-compose.yml` (§1 de [3_plan.md](3_plan.md): ninguna operación
de esta versión es maestro-detalle).

## 7. Devcontainer

Sin cambios: no existe en v4, igual que en v1-v3.

## 8. Credenciales

Sin cambios: [version1/6_contracts.md](../version1/6_contracts.md) §8.

## 9. Contrato del front (`front_catedras/`) con la API — lo que crece en v4

- `detalle_catedra.html` (v2) se extiende con una llamada a
  `GET /api/Sesion?fkCatedra={id}` para mostrar el calendario de esa
  cátedra — no cambia el contrato de `Catedra` en sí, solo agrega una
  llamada nueva desde una plantilla existente.
- `detalle_sesion.html` (nueva, ruta `/sesiones/{id}`) llama
  `GET /api/Ponencia?fkSesion={id}` para la lista de ponentes (y
  `GET /api/Asistente/{id}` por cada uno, para el nombre), y
  `POST /api/Ponencia` / `DELETE /api/Ponencia/{s}/{a}/{rol}` desde un
  formulario (seleccionar un asistente existente — `<select>` poblado de
  `GET /api/Asistente?q=...`, Regla 2 de la guía IA — y un rol de la
  lista cerrada).
- `/ponente/` (calendario del ponente) usa `GET /api/Ponencia?fkAsistente=`.
- El front envía las horas con `-05:00` y muestra las que recibe (UTC)
  en hora de Colombia (`servicios/fechas.py`).
- El front **sí** está en `docker-compose.yml` desde la v1 (servicio
  `front`, puerto 5000, `API_BASE_URL=http://api:8042/api` dentro de la
  red de Docker); fuera de Docker, `API_BASE_URL` por defecto es
  `http://localhost:8142/api`. Sin cambios en v4.

## 10. Reset completo

Sin cambios: [version1/6_contracts.md](../version1/6_contracts.md) §9.
`docker compose down -v && docker compose up -d --build` recrea las 37
tablas con sus datos de ejemplo — incluidas las 5714 filas de `sesion`
que v4 expone por primera vez (y las 9/168/5 de `enlace_registro`/
`registro_asistencia`/`lote_migracion` que v4 solo siente a través de
`RESTRICT`/`CASCADE`, sin poder consultarlas todavía).
