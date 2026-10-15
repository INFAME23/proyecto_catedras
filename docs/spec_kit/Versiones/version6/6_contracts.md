# Contratos de integración — Versión 6: encuestas de calidad

> ⚠️ **BORRADOR** — ver nota de [2_spec.md](2_spec.md).
>
> **Documento 6 de 8** del spec kit. Los endpoints HTTP están en
> [2_spec.md](2_spec.md) (RF159-RF178) y se navegan en vivo desde
> `/swagger`.
>
> **Nota de alcance (v6):** sin cambios de infraestructura — mismo
> mapa de puertos, mismas variables de entorno, mismos dos servicios.
> Crece el **contrato de la API** (§3) y los **montajes de migración**
> (§6, 1 archivo nuevo — la primera migración desde v3).

---

## 1. Mapa de puertos

Sin cambios: [version1/6_contracts.md](../version1/6_contracts.md) §1.

## 2. Variables de entorno

Sin cambios: [version1/6_contracts.md](../version1/6_contracts.md) §2.

## 3. Contrato de la API (lo que crece en v6)

- **`Pregunta`** — 6 verbos, molde completo. `POST` acepta `opciones`
  iniciales opcionales (igual mecánica que `ProgramaAcademico`/`alias`
  en v2):
  ```json
  {
    "fkEncuesta": 1, "enunciado": "¿Recomendaría esta cátedra?",
    "orden": 5, "fkTipoPregunta": "OPCION_UNICA", "obligatoria": true,
    "opciones": [
      { "etiqueta": "Sí", "valor": 1, "orden": 1 },
      { "etiqueta": "No", "valor": 0, "orden": 2 }
    ]
  }
  ```
  Si alguna opción falla (`orden` repetido) → **500** y **nada** queda
  creado. `(fkEncuesta, orden)` repetido entre preguntas → **500**.
- **`OpcionPregunta`** — 6 verbos, molde completo (a diferencia de
  `AliasPrograma`/`ConsentimientoDatos` — D33): `GET/POST
  /api/OpcionPregunta`, `GET/PUT/PATCH/DELETE
  /api/OpcionPregunta/{idOpcion}`.
- **`RespuestaEncuesta` — 3 verbos, uno de ellos de negocio (D31):**
  `GET /api/RespuestaEncuesta?fkEncuesta=`, `GET/{idRespuesta}`,
  `POST /api/RespuestaEncuesta` con body
  ```json
  {
    "fkRegistro": 61,
    "respuestas": [
      { "pregunta": 1, "numero": 5 },
      { "pregunta": 2, "numero": 4 },
      { "pregunta": 3, "numero": 1 }
    ]
  }
  ```
  (`numero`/`texto`/`opcion` — exactamente uno por cada elemento,
  según el tipo de la pregunta correspondiente, D9: la API no lo
  revalida). Devuelve `200 {estado, mensaje, idRespuesta}`. **Sin**
  `PUT`, `PATCH` ni `DELETE` (D32) — esas rutas responden 404 de
  ASP.NET. `respuestas` vacía, el registro sin encuesta asignada, una
  pregunta obligatoria faltante, o el registro ya respondido → **500**
  (D25 de v5: excepciones de negocio de la BD, mismo bucket que
  cualquier otro error de PostgreSQL).
- **`RespuestaItem` — 2 verbos, solo lectura:**
  `GET /api/RespuestaItem?fkRespuesta=`, `GET/{idItem}`. Sin `POST`
  propio (nace solo dentro de `POST /api/RespuestaEncuesta`), sin
  `PUT`/`PATCH`/`DELETE`.
- Errores: mismo sobre que v1-v5 — 422/400/404/500, sin ningún código
  nuevo esta vez (a diferencia de v5 con el 401).

## 4. Contrato API ↔ BD

Sin cambios de mecánica general. Conviven, por primera vez juntas en
una sola versión, las 2 mecánicas de invocación que el proyecto ya
usa por separado: `CALL` con `jsonb` para `sp_guardar_pregunta` (nuevo,
igual que v2/v3) y `SELECT * FROM fn_responder_encuesta_api(...)` para
`RespuestaEncuesta` (función que ya existía, igual que v5).

## 5. Healthchecks y arranque

Sin cambios: [version1/6_contracts.md](../version1/6_contracts.md) §5.

## 6. Contratos de volúmenes y montajes

Las 4 tablas de v6 ya estaban en `script_bdcatedras.sql` desde el
montaje de la v1. Se monta, además, **1 SP nuevo** — el primero desde
`003_sp_guardar_lote_carga.sql` de v3 (v4 y v5 no agregaron ninguno):

| Archivo del repositorio | Ruta en el contenedor `postgres` |
|---|---|
| `db/postgres/migraciones/004_sp_guardar_pregunta.sql` | `/docker-entrypoint-initdb.d/z004_sp_guardar_pregunta.sql` (`:ro`) |

El prefijo `z004_` lo ubica después de `script_bdcatedras.sql` y de
`z001_`-`z003_` de v2/v3 en el orden alfabético de `initdb`.

## 7. Devcontainer

Sin cambios: no existe en v6.

## 8. Credenciales

Sin cambios: [version1/6_contracts.md](../version1/6_contracts.md) §8.

## 9. Contrato del front (`front_catedras/`) con la API — lo que crece en v6

- `detalle_sesion.html` (v4) se extiende con un enlace a "responder
  encuesta" cuando `fk_encuesta` no es nulo y el asistente tiene un
  `registro_asistencia` sin `respuesta_encuesta` previa.
- El formulario de respuesta construye `respuestas` dinámicamente
  según `fk_tipo_pregunta` de cada `Pregunta` (un campo numérico para
  `ESCALA_5`/`SI_NO`, un `<textarea>` para `TEXTO_LIBRE`, un `<select>`
  de `OpcionPregunta` para `OPCION_UNICA` — Regla 2 de la guía IA).
- El front sigue sin estar en `docker-compose.yml`; `API_BASE_URL`
  sigue sin cambios.

## 10. Reset completo

Sin cambios: [version1/6_contracts.md](../version1/6_contracts.md) §9.
`docker compose down -v && docker compose up -d --build` recrea las 37
tablas con sus datos de ejemplo — incluidas las 4 `pregunta`, 62
`respuesta_encuesta` y 248 `respuesta_item` que v6 expone por primera
vez.
