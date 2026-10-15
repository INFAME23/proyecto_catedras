# Contratos de integración — Versión 7: migración a ASIS, bitácora y parámetros

> ⚠️ **BORRADOR** — ver nota de [2_spec.md](2_spec.md).
>
> **Documento 6 de 8** del spec kit. Los endpoints HTTP están en
> [2_spec.md](2_spec.md) (RF179-RF197) y se navegan en vivo desde
> `/swagger`.
>
> **Nota de alcance (v7):** sin cambios de infraestructura y, por
> segunda vez en el proyecto (junto con v4), **sin ningún montaje
> nuevo** en `docker-entrypoint-initdb.d/` (ADR D34). Es la última
> versión del mapa — al cerrarla, este documento (y los 6 anteriores de
> esta carpeta) describen el contrato final y completo de las 37
> tablas de `bdcatedras`.

---

## 1. Mapa de puertos

Sin cambios: [version1/6_contracts.md](../version1/6_contracts.md) §1.

## 2. Variables de entorno

Sin cambios: [version1/6_contracts.md](../version1/6_contracts.md) §2.

## 3. Contrato de la API (lo que crece en v7)

- **`LoteMigracion` — 4 verbos:**
  `GET /api/LoteMigracion?fkSesion=`, `GET/{idLote}`,
  `POST /api/LoteMigracion` con **LoteMigracionGenerar**
  (`{"fkSesion": 5712, "fkUsuario": null}`) → `200 {estado, mensaje,
  idLote}` — llama `SELECT * FROM fn_generar_lote_api(...)`; sin
  registros migrables en esa sesión → **500**.
  `PATCH /api/LoteMigracion/{idLote}` con **LoteMigracionActualizar**
  (`{"fkEstadoProceso": "EN_CURSO", "observaciones": null}`, ambos
  opcionales) → `200`; `fkEstadoProceso` inexistente → **500**; body
  vacío → **400**. **Sin** `PUT` ni `DELETE` — esas rutas responden
  404 de ASP.NET.
- **`DetalleMigracion` — 3 verbos:**
  `GET /api/DetalleMigracion?fkLoteMigracion=`, `GET/{idDetalle}`,
  `PATCH /api/DetalleMigracion/{idDetalle}` con
  **DetalleMigracionActualizar** (`{"resultado": "ACEPTADO",
  "mensaje": null}`) → `200`; un segundo `ACEPTADO` para el mismo
  `fkRegistro` en cualquier otro lote → **500**
  (`ux_detmig_aceptado`). **Sin** `POST`, `PUT` ni `DELETE`.
- **`Bitacora` — 2 verbos, ningún verbo de escritura (ADR D36):**
  `GET /api/Bitacora?nombreTabla=&fkUsuario=`, `GET/{idBitacora}`.
  `POST`/`PUT`/`PATCH`/`DELETE` sobre `/api/Bitacora` → **404** de
  ASP.NET (el `Controller` no declara ningún método que los acepte).
- **`Parametro` — 6 verbos, llave de texto:**
  `GET/POST /api/Parametro`, `GET/PUT/PATCH/DELETE
  /api/Parametro/{clave}` — mismo molde que `Facultad`/`Sede` desde
  v1/v2 (PK de texto elegida por el cliente, no generada). `clave`
  duplicada → **500**. Ninguna FK entrante bloquea nunca un `DELETE`
  (RF196).
- Errores: mismo sobre que v1-v6 — 422/400/404/500, sin ningún código
  nuevo.

## 4. Contrato API ↔ BD

Sin cambios de mecánica general. Reaparece el patrón
`SELECT * FROM fn_..._api(...)` (v5, v6) para
`LoteMigracion.GenerarAsync`; el resto de los repositorios de esta
versión son `INSERT`/`UPDATE`/`SELECT` directos, sin ningún `CALL`.

## 5. Healthchecks y arranque

Sin cambios: [version1/6_contracts.md](../version1/6_contracts.md) §5.

## 6. Contratos de volúmenes y montajes

**Sin cambios — y sin ningún montaje nuevo**, segunda vez en el
proyecto junto con v4. Las 4 tablas y las 2 funciones que v7 usa ya
estaban en `script_bdcatedras.sql` desde el montaje de la v1.

## 7. Devcontainer

Sin cambios: no existe en v7.

## 8. Credenciales

Sin cambios: [version1/6_contracts.md](../version1/6_contracts.md) §8.

## 9. Contrato del front (`front_catedras/`) con la API — lo que crece en v7

- `detalle_sesion.html` (v4) se extiende con un botón "generar
  migración" (visible solo si la sesión ya pasó y tiene registros)
  que llama `POST /api/LoteMigracion`.
- `routes/migracion.py` (nueva) muestra el detalle de un lote y
  permite actualizar su estado / el resultado de cada fila (dos
  `PATCH` distintos, uno por entidad).
- `routes/bitacora.py` (nueva) — solo lectura, con filtros.
- `routes/parametros.py` (nueva) — CRUD simple, mismo molde que
  `Facultad`/`Sede`.
- El front sigue sin estar en `docker-compose.yml`; `API_BASE_URL`
  sigue sin cambios — **cierra sin ningún cambio de infraestructura
  en las 7 versiones del proyecto**.

## 10. Reset completo

Sin cambios: [version1/6_contracts.md](../version1/6_contracts.md) §9.
`docker compose down -v && docker compose up -d --build` recrea las 37
tablas con sus datos de ejemplo — incluidas las 5 `lote_migracion`, 823
`detalle_migracion`, 168 `bitacora` y 9 `parametro` que v7 expone por
primera vez. **Con esto, el contrato de la API cubre las 37 tablas
completas de `bdcatedras`.**
