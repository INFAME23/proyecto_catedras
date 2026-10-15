# Contratos de integración — Versión 2: Facultad, Dependencia, ProgramaAcademico, AliasPrograma, Catedra

> **Documento 6 de 8** del spec kit: cómo se hablan las piezas entre sí
> (puertos, hosts, variables, healthchecks). Los endpoints HTTP de la
> API (verbos, formatos exactos) están en [2_spec.md](2_spec.md)
> (RF62-RF94) y se navegan en vivo desde `/swagger`.
>
> **Nota de alcance (v2):** la v2 **no cambia ningún contrato de
> infraestructura** de la v1 — mismo mapa de puertos, mismas variables de
> entorno, mismos dos servicios (`api` + `postgres`), mismo healthcheck.
> Lo único que crece es el **contrato de la API** (§3): 5 recursos más.
> Por eso este documento es más corto que el de v1: las secciones sin
> cambios enlazan directamente a [version1/6_contracts.md](../version1/6_contracts.md)
> en vez de repetirlas.

---

## 1. Mapa de puertos

Sin cambios: [version1/6_contracts.md](../version1/6_contracts.md) §1
(`api` → `8142`, `postgres` → `15542`). La v2 no agrega servicios.

## 2. Variables de entorno

Sin cambios: [version1/6_contracts.md](../version1/6_contracts.md) §2.
Ninguna de las 5 tablas nuevas necesita configuración propia (no hay
credenciales, URLs externas ni *feature flags* involucrados).

## 3. Contrato de la API (lo que crece en v2)

- CRUD sobre `Facultad`, `Dependencia`, `ProgramaAcademico` y `Catedra`:
  mismo patrón de 5 verbos que v1
  (`GET/POST /api/{Recurso}`, `GET/PUT/PATCH/DELETE /api/{Recurso}/{id}`).
  `{id}` es texto para las tres primeras (`id_facultad`,
  `id_dependencia`, `codigo`) y numérico para `Catedra` (su PK la genera
  la BD, igual que `Encuesta` en v1).
- **Retrofit — `POST /api/ProgramaAcademico` acepta alias iniciales**
  (Guía IA, Regla 1). Body:
  ```json
  {
    "codigo": "Z0001",
    "nombre": "Programa nuevo",
    "fk_facultad": "F_ING",
    "nivel": "PREGRADO",
    "activo": true,
    "alias": [
      { "texto_normalizado": "programa nuevo" },
      { "texto_normalizado": "prog. nuevo", "origen": "CARGA" }
    ]
  }
  ```
  `alias` es **opcional** (ausente o `[]` = igual que antes). Respuesta
  sin cambios: `200 {estado: "creado", mensaje}`. Errores: un alias sin
  `texto_normalizado` o con más de 200 caracteres → **422**
  (`campo: "alias[0].texto_normalizado"`); `alias` que no es una lista →
  **422** (`campo: "alias"`); un `texto_normalizado` que ya existe →
  **500** (`uq_alias_texto`) y **no se guarda nada**, ni el programa.
  Si el cliente manda `fk_programa` dentro de un alias, se ignora.
- `AliasPrograma` expone **4 verbos, no 5**: `GET /api/AliasPrograma`,
  `GET /api/AliasPrograma/{id}`, `POST /api/AliasPrograma`,
  `DELETE /api/AliasPrograma/{id}`. No existen `PUT` ni `PATCH` para
  este recurso — una llamada a cualquiera de los dos responde **404**
  de ASP.NET (ruta inexistente, no un 405 fabricado a mano: el
  `Controller` simplemente no declara esos métodos).
- Errores: mismo sobre que v1 — 422 (petición inválida, antes de la
  BD), 400 (`ArgumentException`, incluida la autorreferencia de
  `Facultad`/`Dependencia`), 404 (`NoEncontradoExcepcion`), 500
  (`NpgsqlException`, incluida cualquier violación de FK —
  `RESTRICT` o un formato que ni siquiera llegó a validarse en la
  petición).
- **Nuevo en v2 — un verbo puede tener éxito y, aun así, modificar
  filas que el cliente no pidió tocar:** `DELETE` sobre `Facultad` o
  `Dependencia` puede dejar otras filas con una FK en `null`
  (`ON DELETE SET NULL`); `DELETE` sobre `Catedra` puede además borrar
  en cascada sesiones de una tabla que esta misma API todavía no
  expone (`ON DELETE CASCADE` hacia `sesion`, ver
  [5_data_model.md](5_data_model.md) §2.5). No hay forma de advertirlo
  en la respuesta del `DELETE` (sigue siendo `{estado, mensaje,
  filasEliminadas}`, igual que v1) — queda documentado aquí y en el
  smoke test, no en el contrato HTTP en sí.

## 4. Contrato API ↔ BD

Sin cambios de mecánica: [version1/6_contracts.md](../version1/6_contracts.md)
§4 (una conexión `Npgsql` por operación, `await using`, sin pool
propio). Lo que cambia es que, por primera vez, **la BD participa
activamente en el resultado de un `DELETE`** más allá de aceptarlo o
rechazarlo: las reglas `SET NULL`/`CASCADE`/`RESTRICT` de §3 las aplica
el motor, no la API — el repositorio no las implementa ni las simula,
solo propaga lo que Postgres decida (éxito con filas afectadas en otras
tablas, o `NpgsqlException` → 500).

## 5. Healthchecks y arranque

Sin cambios: [version1/6_contracts.md](../version1/6_contracts.md) §5.

## 6. Contratos de volúmenes y montajes

Sin cambios: [version1/6_contracts.md](../version1/6_contracts.md) §6.
Las 5 tablas de v2 ya estaban en `script_bdcatedras.sql` desde el
montaje de la v1. **Retrofit:** se monta además la migración del SP
maestro-detalle:

| Archivo del repositorio | Ruta en el contenedor `postgres` |
|---|---|
| `db/postgres/migraciones/001_sp_guardar_programa_academico.sql` | `/docker-entrypoint-initdb.d/z001_sp_guardar_programa_academico.sql` (`:ro`) |

El prefijo `z001_` hace que corra **después** de `script_bdcatedras.sql`
(initdb ejecuta en orden alfabético). Igual que el script provisto, solo
corre con el volumen vacío.

## 7. Devcontainer

Sin cambios: no existe en v2, igual que en v1.

## 8. Credenciales

Sin cambios: [version1/6_contracts.md](../version1/6_contracts.md) §8.

## 9. Contrato del front (`front_catedras/`) con la API — nuevo en v2

A diferencia de v1 (donde el front solo hacía CRUD simple), en v2 el
front hace una lectura que el contrato de la API debe soportar sin
cambios:

- `GET /api/Catedra` **sin `limite`** devuelve las 5502 filas en una
  sola respuesta (§4.7 y ADR D13 de [3_plan.md](3_plan.md) /
  [4_research.md](4_research.md)) — el catálogo público depende de que
  este verbo siga aceptando "sin límite" como "todas"; si una versión
  futura cambiara ese comportamiento por defecto, rompería el catálogo
  de v2 sin que ninguna spec de esa versión lo haya pedido
  explícitamente (Artículo 7: los contratos se cumplen al pie de la
  letra).
- El front sigue sin estar en `docker-compose.yml`
  ([version1/6_contracts.md](../version1/6_contracts.md) §3, nota de
  alcance) y se arranca aparte con `python run.py`; `API_BASE_URL`
  sigue apuntando a `http://localhost:8142/api`, sin cambios.

## 10. Reset completo

Sin cambios: [version1/6_contracts.md](../version1/6_contracts.md) §9.
`docker compose down -v && docker compose up -d --build` recrea las 37
tablas con sus datos de ejemplo — incluidas las 5714 filas de `sesion`
y las 15349 de `programa_asistente` que [5_data_model.md](5_data_model.md)
documenta como relevantes para v2 aunque sean de otras versiones.
