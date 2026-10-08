# Quickstart — Versión 7: migración a ASIS, bitácora y parámetros

> ⚠️ **BORRADOR** — ver nota de [2_spec.md](2_spec.md).
>
> **Documento 7 de 8** del spec kit. Validación del sistema ya
> construido en v7, sobre lo que dejaron v1-v6.
>
> **Nota de alcance (v7):** mismo backend que v1-v6, **sin ninguna
> migración nueva** (ADR D33). Hoy la API expone las **37** tablas de
> `bdcatedras` por completo: los 33 recursos de v1-v6 más
> `LoteMigracion`, `DetalleMigracion`, `Bitacora` y `Parametro`. Es la
> última versión del mapa.

---

## 1. Levantar

Sin cambios respecto a v1-v6
([version1/7_quickstart.md](../version1/7_quickstart.md) §1) — **y, a
diferencia de v3/v5/v6, no hay ninguna migración que reaplicar**: v7
no agrega ningún archivo a `db/postgres/migraciones/` (segunda versión
del proyecto sin ninguna, junto con v4).

```powershell
docker compose up -d --build
docker compose ps
```

## 2. Verificación de la API — 8 pasos

1. **http://localhost:8142/swagger** → ahora muestra **37** recursos
   — las 37 tablas de `bdcatedras` completas.
2. **Generar un lote de migración (criterio 2):** `GET /api/Sesion/5712`
   confirma el estado de partida (en los datos de ejemplo, ya puede
   decir `MIGRADA` por un lote sembrado de antes — no importa: nada
   impide generar un lote nuevo sobre la misma sesión). `POST
   /api/LoteMigracion` con
   ```json
   { "fkSesion": 5712, "fkUsuario": null }
   ```
   → `200` con `idLote`. `GET
   /api/DetalleMigracion?fkLoteMigracion={idLote}` → 166 filas (los
   registros migrables de esa sesión — ningún asistente externo
   aparece, aunque se haya registrado, por `RN-08`/`RN-28`). `GET
   /api/Sesion/5712` → `estado: "MIGRADA"` (RF184: efecto colateral de
   `sp_generar_lote_migracion`, no de código C#).
3. **Sesión sin nada que migrar:** `POST /api/LoteMigracion` con una
   `fkSesion` de una cátedra recién creada (sin ningún
   `registro_asistencia`, p. ej. una de las cátedras vacías usadas en
   el quickstart de v4) → `500` (la función lanza su propio `RAISE
   EXCEPTION`; D24 de v5).
4. **`PATCH` de estado de lote (criterio 3):** `PATCH
   /api/LoteMigracion/{idLote}` con `{"fkEstadoProceso": "EN_CURSO"}`
   → `200`; `GET /api/LoteMigracion/{idLote}` confirma el cambio.
   Repetir con `{"fkEstadoProceso": "NO_EXISTE"}` → `500` (D9); con
   body `{}` → `400`.
5. **`PATCH` de resultado de detalle y `ux_detmig_aceptado` (criterio
   4):** tomar cualquier `idDetalle` del paso 2. `PATCH
   /api/DetalleMigracion/{idDetalle}` con `{"resultado": "ACEPTADO"}`
   → `200`. Si en los datos de ejemplo el mismo `fkRegistro` ya tiene
   otra fila `ACEPTADA` en un lote distinto (frecuente, porque la
   sesión `5712` viene con un lote sembrado desde antes), repetir el
   mismo `PATCH` sobre esa segunda fila → `500`
   (`ux_detmig_aceptado` — un registro no se migra exitosamente dos
   veces, aunque se haya intentado en lotes distintos).
6. **`Bitacora` sin ningún verbo de escritura (criterio 5):** `GET
   /api/Bitacora?limite=5` → 5 de las 168+ filas de ejemplo (crecen
   solas con cada `registro_asistencia` nuevo desde v5). `POST`, `PUT`,
   `PATCH` y `DELETE` sobre `/api/Bitacora` (o `/api/Bitacora/{id}`) →
   **404** de ASP.NET — no hay ningún método del `Controller` que los
   acepte, no es un `405`.
7. **`Parametro`, CRUD completo con llave de texto (criterio 6):**
   `POST /api/Parametro` con
   ```json
   { "clave": "PRUEBA_QUICKSTART", "valor": "1", "tipoDato": "ENTERO",
     "descripcion": "Parametro de prueba del quickstart de v7" }
   ```
   → `200`. `PATCH /api/Parametro/PRUEBA_QUICKSTART` con
   `{"valor": "2"}` → `200`; `GET /api/Parametro/PRUEBA_QUICKSTART`
   confirma `valor: "2"` y `modificadoEn` más reciente que
   `GET`-antes-del-`PATCH`. `DELETE /api/Parametro/PRUEBA_QUICKSTART` →
   `200`, sin ningún error de integridad referencial (RF196: ninguna
   FK entrante). Repetir el `POST` original (`clave` duplicada) →
   `500`.
8. **Reset** (recomendado después de los pasos 2-7):
   `docker compose down -v && docker compose up -d --build`.

**Prueba de capas (criterio de aceptación 7):**
```powershell
cd api_catedras
dotnet run --project pruebas
```
Ahora cubre las **37** entidades con repositorios FALSOS — número que
coincide, por fin, con las 37 tablas completas de `bdcatedras`.
Termina con código 0.

## 3. Frontend (`front_catedras/`, aparte del `docker compose` de arriba)

Arranque sin cambios respecto a v1-v6.

Con la API corriendo (§1-2 de aquí):

1. **http://localhost:5000/sesiones/5712** (o la ruta equivalente) →
   muestra un botón "generar migración", visible solo si la sesión ya
   pasó y tiene registros.
2. **http://localhost:5000/admin/** → 4 tarjetas nuevas: `LoteMigracion`
   (generar + actualizar estado), `DetalleMigracion` (actualizar
   resultado), `Bitacora` (solo lectura, con filtros por tabla/usuario),
   `Parametro` (CRUD completo, mismo molde visual que `Facultad`/`Sede`).
3. **http://localhost:5000/admin/parametros** → las 9 filas de ejemplo
   (`CLAVE_LONGITUD`, `DOMINIOS_INSTITUCIONALES`, etc.) listadas y
   editables una por una.
4. **Datos mock eliminados (igual que v1-v6):** apagar la API y
   recargar cualquier pantalla de esta versión → error claro.

## 4. Problemas frecuentes

| Síntoma | Diagnóstico |
|---|---|
| `POST /api/LoteMigracion` responde 500 "no hay registros migrables" (o similar) | La `fkSesion` usada no tiene ningún `registro_asistencia` real, o todos son de asistentes externos — usar `5712` u otra sesión con datos de ejemplo |
| `PATCH /api/DetalleMigracion/{id}` con `resultado: "ACEPTADO"` responde 500 inesperadamente | El `fkRegistro` de esa fila ya quedó `ACEPTADO` en otro lote antes — es el comportamiento esperado de `ux_detmig_aceptado`, no un error |
| `POST`/`PATCH`/`DELETE` sobre `/api/Bitacora` responde 404 | Correcto — no es un bug: esta entidad no tiene ningún verbo de escritura a propósito (D35) |
| `GET /api/Bitacora` tarda o devuelve demasiadas filas | Usar `?limite=` bajo — la tabla ya trae 168+ filas de ejemplo y crece con cada `registro_asistencia` |
| `fk_usuario` siempre sale `null` en `Bitacora`/`LoteMigracion` | Esperado — ningún endpoint de la API propaga el usuario autenticado hacia la BD (ver nota en [5_data_model.md](5_data_model.md) §2.3); no hay autorización por rol en este proyecto (D27 de v5) |
| Todo roto | `docker compose down -v && docker compose up -d --build` (API); para el front, confirmar que `API_BASE_URL` siga sin cambios |
