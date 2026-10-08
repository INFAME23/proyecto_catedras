# Contratos de integración — Versión 3: personas y carga de asistentes

> ⚠️ **BORRADOR** — ver nota de [2_spec.md](2_spec.md).
>
> **Documento 6 de 8** del spec kit: cómo se hablan las piezas entre sí
> (puertos, hosts, variables, healthchecks). Los endpoints HTTP de la
> API (verbos, formatos exactos) están en [2_spec.md](2_spec.md)
> (RF95-RF118) y se navegan en vivo desde `/swagger`.
>
> **Nota de alcance (v3):** la v3 **no cambia ningún contrato de
> infraestructura** de v1/v2 — mismo mapa de puertos, mismas variables
> de entorno, mismos dos servicios (`api` + `postgres`), mismo
> healthcheck. Lo que crece es (a) el **contrato de la API** (§3), con 7
> recursos más y, por primera vez, rutas sin `/{id}` numérico (ADR D16);
> y (b) los **montajes de migración** (§6), con 2 SP nuevos. Por eso
> este documento enlaza a [version1/6_contracts.md](../version1/6_contracts.md)
> y [version2/6_contracts.md](../version2/6_contracts.md) en vez de
> repetir lo que no cambia.

---

## 1. Mapa de puertos

Sin cambios: [version1/6_contracts.md](../version1/6_contracts.md) §1
(`api` → `8142`, `postgres` → `15542`). La v3 no agrega servicios.

## 2. Variables de entorno

Sin cambios: [version1/6_contracts.md](../version1/6_contracts.md) §2.
Ninguna de las 7 tablas nuevas necesita configuración propia.

## 3. Contrato de la API (lo que crece en v3)

- **`Asistente`** — 6 verbos, molde completo, con dos diferencias sobre
  el molde estándar:
  - `GET /api/Asistente` acepta `?q=` además de `?limite=` (ADR D18):
    filtra por nombre, apellido o `id_asis`, en SQL (`ILIKE`), no en
    memoria. `?q=` sin `?limite=` sigue limitando igual que siempre
    (mismo tope por defecto que el resto de los recursos).
  - **`POST /api/Asistente` acepta detalles iniciales** (Guía IA, Regla
    1). Body:
    ```json
    {
      "nombres": "Ana",
      "apellidos": "García",
      "id_asis": "A00012345",
      "correo_institucional": "ana.garcia@usbmed.edu.co",
      "es_externo": false,
      "fk_lote_carga": 3,
      "documentos": [
        { "fk_tipo_documento": "CC", "numero": "1020304050", "vigente": true }
      ],
      "vinculaciones": [
        { "fk_tipo_vinculacion": "ESTUDIANTE", "fecha_ini": "2026-01-20" }
      ],
      "programas": [
        { "fk_programa": "M0286", "fk_periodo": "2026-1", "estado": "ACTIVO" }
      ],
      "consentimiento": { "version_politica": "v1.0", "canal": "WEB" }
    }
    ```
    Las 4 claves de detalle son **opcionales** (ausentes o `[]`/`null` =
    asistente sin detalles iniciales). Ninguna petición de detalle
    lleva `fk_asistente`: lo fija el servicio con el id que la BD
    genere. Respuesta `200 {estado: "creado", mensaje, id_asistente}`.
    Si cualquier detalle falla (documento duplicado, programa
    inexistente, vinculación repetida) → **500** y **no se crea nada**,
    ni el asistente (`sp_guardar_asistente` revierte todo el `CALL`).
  - `id_asis` nulo con `es_externo: false` (o viceversa) → **500**
    (`chk_asistente_externo`, D9 extendido a un `CHECK` de una sola
    tabla — ver RF101).
- **`DocumentoAsistente`** — 5 verbos, molde completo (a diferencia de
  `AliasPrograma` en v2, que solo tenía 4): `GET` lista (con
  `?fkAsistente=`), `GET/{id}`, `POST`, `PUT/{id}`, `PATCH/{id}`,
  `DELETE/{id}`. Un segundo documento `vigente: true` para el mismo
  asistente → **500** (`ux_documento_vigente`, índice único parcial).
  Dos documentos del mismo `fk_tipo_documento` con el mismo `numero` →
  **500** (`uq_documento_numero`).
- **`VinculacionAsistente` y `ProgramaAsistente` — SIN `/{id}` (ADR
  D16).** Primera asimetría de rutas del proyecto:
  - `VinculacionAsistente`: **3** verbos —
    `GET /api/VinculacionAsistente?fkAsistente=` (lista, sin `/{id}`
    individual: la llave es compuesta, no hay una sola columna que la
    identifique en la URL), `POST /api/VinculacionAsistente` (abre),
    `PATCH /api/VinculacionAsistente/cerrar` (cierra; body = las 3
    columnas de la llave + `fecha_fin`). **No existen** `GET/{id}`,
    `PUT`, `PATCH/{id}` ni `DELETE` — cualquier llamada a esas rutas
    responde **404** de ASP.NET (ruta inexistente, el `Controller` no
    la declara). Cerrar una combinación inexistente → 404 (de
    negocio, lanzado por el servicio); cerrar una ya cerrada → 400.
  - `ProgramaAsistente`: mismo patrón, **3** verbos —
    `GET /api/ProgramaAsistente?fkAsistente=`, `POST`,
    `PATCH /api/ProgramaAsistente/cambiar-estado` (body = llave
    completa + `estado` nuevo, lista cerrada `ACTIVO|RETIRADO|
    GRADUADO|APLAZADO`).
  - Un `POST` con la combinación de llave exacta ya existente en
    cualquiera de las dos → **500** (PK duplicada, D9).
- **`ConsentimientoDatos`** — **3** verbos: `GET` lista, `GET/{id}`,
  `POST`. Sin `PUT`/`PATCH`/`DELETE` (más estricto que `AliasPrograma`
  en v2, que sí tenía `DELETE`) — Ley 1581: el consentimiento es un
  hecho legal, no una fila editable ni borrable. Un asistente que ya
  aceptó esa `version_politica` → **500** (`uq_consentimiento_ver`).
- **`LoteCargaAsistente`** — **4** verbos: `GET` lista, `GET/{id}`,
  `POST` (abre: solo `nombre_archivo`; `fk_usuario` siempre `null` en
  v3 — ver [2_spec.md](2_spec.md) §2), `POST /{id}/cerrar` (cierra: body
  = los 3 contadores + `observaciones` opcional + `novedades[]`). Sin
  `PUT`/`PATCH` genérico ni `DELETE`. `filasAceptadas + filasRechazadas
  > filasLeidas` → **400** (validado en el servicio, antes de llegar al
  `CHECK chk_lote_carga_filas` de la BD); un lote ya cerrado → **400**.
- **`NovedadCarga`** — **2** verbos: `GET` lista (con
  `?fkLoteCarga=`), `GET/{id}`. Sin `POST` propio — nace únicamente
  dentro de `POST /api/LoteCargaAsistente/{id}/cerrar`. Sin
  `PUT`/`PATCH`/`DELETE`.
- Errores: mismo sobre que v1/v2 — 422 (petición inválida, antes de la
  BD), 400 (`ArgumentException`: cruces de campos del mismo body —
  vinculación ya cerrada, contadores de lote que no cuadran, llave
  compuesta ya cerrada), 404 (`NoEncontradoExcepcion`: incluida una
  combinación de llave compuesta inexistente en `cerrar`/
  `cambiar-estado`), 500 (`NpgsqlException`, incluidos los `CHECK`
  propios de v3: `chk_asistente_externo`, `ux_documento_vigente`,
  `uq_documento_numero`, PK compuesta duplicada, `uq_consentimiento_ver`).
- **Nuevo en v3 — una operación compuesta por naturaleza (`POST
  /api/Asistente` con detalles) que puede fallar por una fila de un
  detalle que el cliente mandó bien, no por el asistente mismo:** a
  diferencia de v2 (un solo detalle, `alias`), aquí son 4 listas/objeto
  independientes; el mensaje de error del 500 identifica cuál violó
  qué restricción (el mismo que devolvería la BD si se hiciera el
  `INSERT` suelto), pero el cliente debe revisar los 4 bloques del body
  para saber cuál corregir — no hay, en v3, un campo que señale "el
  detalle número 2 de la lista `documentos`" (limitación documentada,
  no un contrato roto: ninguna RF de v3 pide ese nivel de detalle en el
  error).

## 4. Contrato API ↔ BD

Sin cambios de mecánica: [version1/6_contracts.md](../version1/6_contracts.md)
§4. Lo que se repite, por tercera vez en el proyecto (tras el retrofit
de `ProgramaAcademico` en v2), es el patrón `CALL` con parámetro
`jsonb` — ahora con **4 parámetros `jsonb` en el mismo `CALL`**
(`sp_guardar_asistente`: documentos, vinculaciones, programas,
consentimiento) en vez de uno solo. El repositorio serializa cada lista
por separado con `System.Text.Json.JsonSerializer.Serialize`; ninguna
lista vacía se manda como `jsonb` vacío — se manda `NULL` (mismo
criterio que v2, para que el SP distinga "no venía el detalle" de "venía
una lista vacía", aunque el SP trate ambos casos igual).

## 5. Healthchecks y arranque

Sin cambios: [version1/6_contracts.md](../version1/6_contracts.md) §5.

## 6. Contratos de volúmenes y montajes

Las 7 tablas de v3 ya estaban en `script_bdcatedras.sql` desde el
montaje de la v1 (Artículo 5). Se montan, además, los **2 SP
maestro-detalle** de esta versión, con el mismo mecanismo de prefijo
alfabético que el `001_...` de v2:

| Archivo del repositorio | Ruta en el contenedor `postgres` |
|---|---|
| `db/postgres/migraciones/002_sp_guardar_asistente.sql` | `/docker-entrypoint-initdb.d/z002_sp_guardar_asistente.sql` (`:ro`) |
| `db/postgres/migraciones/003_sp_guardar_lote_carga.sql` | `/docker-entrypoint-initdb.d/z003_sp_guardar_lote_carga.sql` (`:ro`) |

El prefijo `z00N_` hace que ambos corran **después** de
`script_bdcatedras.sql` y después de `z001_...` de v2 (orden
alfabético: `z001_` < `z002_` < `z003_`, sin que el orden entre ellos
importe — no dependen uno del otro). Igual que `001_...`, solo corren
con el volumen vacío; sobre un volumen ya existente se aplican a mano
con `psql` (ver [7_quickstart.md](7_quickstart.md) §1).

## 7. Devcontainer

Sin cambios: no existe en v3, igual que en v1/v2.

## 8. Credenciales

Sin cambios: [version1/6_contracts.md](../version1/6_contracts.md) §8.

## 9. Contrato del front (`front_catedras/`) con la API — lo que crece en v3

- **Pantalla nueva de carga de datos** (ADR D17): el front interpreta el
  archivo subido en Python (`openpyxl`/`pandas`, fuera de la API) y
  llama, por cada fila, `POST /api/Asistente` con `fk_lote_carga` ya
  fijado al lote que abrió con `POST /api/LoteCargaAsistente` al
  comenzar. Al terminar todas las filas, UNA llamada a
  `POST /api/LoteCargaAsistente/{id}/cerrar` con los contadores y las
  novedades acumuladas (ver §5 de [3_plan.md](3_plan.md)). El contrato
  de la API no sabe ni necesita saber que esas N peticiones vienen de
  un mismo archivo — cada `POST /api/Asistente` es, para la API, una
  petición independiente como cualquier otra.
- **Listado de `Asistente` con `?q=` (ADR D18):** a diferencia de
  `GET /api/Catedra` en v2 (que el front pide completo y pagina en
  Flask), el front de `Asistente` manda `?q=` y `?limite=`/`?pagina=`
  en cada tecla del buscador — el filtro vive en la API/BD, no en
  Flask. Este es el primer endpoint del proyecto donde el front NO
  descarga la tabla completa.
- El front sigue sin estar en `docker-compose.yml`, arrancado aparte
  con `python run.py`; `API_BASE_URL` sigue en
  `http://localhost:8142/api`, sin cambios.

## 10. Reset completo

Sin cambios: [version1/6_contracts.md](../version1/6_contracts.md) §9.
`docker compose down -v && docker compose up -d --build` recrea las 37
tablas con sus datos de ejemplo — incluidas las **14.811** filas de
`asistente` y las **15.349** de `programa_asistente` que v3 expone por
primera vez, aunque la BD ya las traía sembradas desde el script
inicial.
