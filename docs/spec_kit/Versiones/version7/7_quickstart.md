# Quickstart — Versión 7: migración a ASIS, bitácora y parámetros

> ⚠️ **BORRADOR** — ver nota de [2_spec.md](2_spec.md). Datos verificados
> contra la BD real en la Fase 54; los códigos HTTP se confirman contra
> Docker con `pruebas/correr_todo_v7.ps1` (Fase 61).
>
> **Documento 7 de 8** del spec kit. Validación del sistema ya
> construido en v7, sobre lo que dejaron v1-v6.
>
> **Nota de alcance (v7):** mismo backend que v1-v6. Las migraciones de
> procedimientos de la v7 (`013`, `014`, `031`, `032`) ya vienen
> montadas en `docker-compose.yml`; no hay ninguna nueva que reaplicar.
> La API expone las **37** tablas de `bdcatedras`: los 33 recursos de
> v1-v6 más `LoteMigracion`, `DetalleMigracion`, `Bitacora` y
> `Parametro`. Es la última versión del mapa.

---

## 1. Levantar (BD recién creada)

```powershell
docker compose down -v
docker compose up -d --build
docker compose ps
```

Los ids de los pasos siguientes (lote **6**, etc.) suponen una BD recién
creada. Los cuerpos JSON van en **snake_case** (`fk_sesion`, no
`fkSesion`); los filtros de consulta, en camelCase (`?fkSesion=`).

## 2. Verificación de la API — 8 pasos

> **Automatizado:** `pruebas/verificar_v7.ps1` corre los pasos 1-8 (71
> verificaciones) y escribe al final el código real de cada verbo que no
> existe. `pruebas/correr_todo_v7.ps1` hace TODO en un comando: BD nueva,
> procedimientos (SQL), prueba de capas, regresión v5 y retrofit, BD nueva
> otra vez, `verificar_v7.ps1`, y deja una BD nueva con la clave del
> administrador fijada para el §3.
>
> ```powershell
> powershell -ExecutionPolicy Bypass -File .\api_catedras\pruebas\correr_todo_v7.ps1
> ```

1. **http://localhost:8142/swagger** → **37** recursos — las 37 tablas
   de `bdcatedras` completas. `GET /` → `version: "v7"`.
2. **Generar un lote de migración (criterio 2).** La sesión 5712 es la
   única con registros (168, 1 externo, 166 migrables) y **ya viene
   `MIGRADA`** por los lotes sembrados; para ver el efecto de RF184,
   primero regrésala:
   - `PATCH /api/Sesion/5712` con `{"estado": "CERRADA"}` → 200; `GET
     /api/Sesion/5712` → `estado: "CERRADA"`.
   - `POST /api/LoteMigracion` con
     ```json
     { "fk_sesion": 5712, "fk_usuario": null }
     ```
     → `200` con `id_lote_migracion: 6` y `lote.total_filas: 166`,
     `lote.fk_estado_proceso: "GENERADO"`.
   - `GET /api/DetalleMigracion?fkLoteMigracion=6` → 166 filas
     `PENDIENTE` (el externo no aparece, RN-08/RN-28).
   - `GET /api/Sesion/5712` → `estado: "MIGRADA"` (efecto colateral de
     `sp_generar_lote_migracion`, no de código C#).
3. **Sesión sin nada que migrar:** `POST /api/LoteMigracion` con
   `{"fk_sesion": 1}` (cualquier sesión distinta de 5712) → `500`, con
   el mensaje "No hay registros migrables en la sesion 1. Revise I10…"
   (`check_violation`; D25 de v5). Sin `fk_sesion` → `422`.
4. **`PATCH` de estado de lote (criterio 3):** `PATCH
   /api/LoteMigracion/6` con `{"fk_estado_proceso": "EN_CURSO"}` →
   `200`; `GET /api/LoteMigracion/6` confirma el cambio. Repetir con
   `{"fk_estado_proceso": "NO_EXISTE"}` → `500` (`fk_lotemig_estado`,
   D9); con body `{}` → `400`. Estados válidos: `GET /api/EstadoProceso`
   (no finales `GENERADO`, `EN_COLA`, `EN_CURSO`; finales `ERROR`,
   `CORRECTO`, `INCORRECTO`).
5. **`PATCH` de resultado de detalle y `ux_detmig_aceptado` (criterio
   4).** El lote 1 sembrado (`CORRECTO`) ya tiene `ACEPTADO` 164 de los
   166 registros; solo los registros **167** y **169** admiten un
   `ACEPTADO` nuevo. En la lista del paso 2, busca el `id_detalle` de
   las filas con `fk_registro` 167 y 1 (los ids de detalle dependen de
   la secuencia: tómalos de la lista, no los adivines):
   - `PATCH /api/DetalleMigracion/{id de la fila del registro 167}` con
     `{"resultado": "ACEPTADO"}` → `200`.
   - `PATCH /api/DetalleMigracion/{id de la fila del registro 1}` con
     `{"resultado": "ACEPTADO"}` → `500` (`ux_detmig_aceptado` — un
     registro no se migra exitosamente dos veces).
   - `{"resultado": "OTRO"}` → `422`; `{}` → `400`.
6. **`Bitacora`, solo lectura (criterio 5):**
   - `GET /api/Bitacora?limite=5` → 5 de las 168 filas, todas
     `registro_asistencia` / `INSERT`, `fk_usuario: null`.
   - `DELETE /api/Asistente/2` (borrado lógico) → `GET
     /api/Bitacora?limite=1` muestra una fila nueva `nombre_tabla:
     "asistente"`, `operacion: "UPDATE"` (`trg_bitacora_asistente`).
   - `POST /api/Bitacora`, `PUT`/`PATCH`/`DELETE /api/Bitacora/1` →
     **405** (la ruta existe solo con `GET`).
7. **`Parametro`, CRUD completo con llave de texto (criterio 6):**
   - `GET /api/Parametro` → las 9 claves sembradas.
   - `POST /api/Parametro` con
     ```json
     { "clave": "PRUEBA_QUICKSTART", "valor": "1", "tipo_dato": "ENTERO",
       "descripcion": "Prueba v7" }
     ```
     → `200`. Repetirlo → `500` (`pk_parametro`). Con `"clave":
     "prueba"` → `422` (D37-D; la expresión regular se agrega en la
     Fase 57 — antes de eso, este caso responde 200).
   - `PATCH /api/Parametro/PRUEBA_QUICKSTART` con `{"valor": "2"}` →
     `200`; `GET` confirma `valor: "2"` y un `modificado_en` posterior al
     del `POST`.
   - `DELETE /api/Parametro/PRUEBA_QUICKSTART` → `200`, sin error de
     integridad referencial (RF196); un segundo `DELETE` → `404`.
8. **Verbos que no existen:** `PUT`/`DELETE /api/LoteMigracion/6` y
   `POST /api/DetalleMigracion` → **405**.

**Reset** (después de los pasos 2-8):
`docker compose down -v && docker compose up -d --build`, y volver a
fijar la clave del administrador: `PATCH /api/Usuario/1 {"clave":
"Admin12345"}`.

**Prueba de capas (criterio de aceptación 7):**
```powershell
cd api_catedras
dotnet run --project pruebas
```
Cubre las **37** entidades con repositorios FALSOS, incluida la
verificación por reflexión de que `Bitacora` no tiene métodos de
escritura. Termina con código 0: **452 pasadas** (385 de la v6 + 67 de
la Fase 58).

**Regresión:** `pruebas/verificar_retrofit_fase4.ps1` (80 verificaciones,
incluye `LoteMigracion`/`DetalleMigracion` por HTTP y la regresión de la v1)
y `pruebas/verificar_v5.ps1` (63).

## 3. Frontend (`front_catedras/`, aparte del `docker compose` de arriba)

Arranque sin cambios respecto a v1-v6. Entrar como administrador
(`admin.catedras` / `Admin12345`, después de fijar la clave como en el
paso de reset).

1. **http://localhost:5000/admin/** → 4 tarjetas nuevas: lotes de
   migración (`/admin/lotes-migracion`), detalle de migración
   (`/admin/detalles-migracion`), bitácora (`/admin/bitacora`, solo
   lectura) y parámetros (`/admin/parametros`).
2. **http://localhost:5000/admin/parametros** → las 9 filas de ejemplo,
   editables una por una (la clave no se edita).
3. **http://localhost:5000/sesiones/5712** → botón "Generar migración",
   visible solo para `ADMIN` en una sesión que ya terminó y tiene
   registros (si ya está `MIGRADA`, el aviso lo dice: generar crea otro
   lote, para reintentar). Al usarlo (pide confirmación), redirige al lote
   nuevo — 166 filas — y la sesión aparece como migrada sin que el front
   haya tocado `Sesion`. Sin sesión iniciada o sin rol `ADMIN`, el botón no
   aparece y `/admin/sesiones/5712/migrar` responde 403 (o lleva al login).
   En la sesión 1 no aparece (no tiene registros).
4. **Detalle del lote** → el estado se elige de un `<select>` con el
   catálogo de estados de proceso; cada fila tiene su `<select>` de
   resultado y un mensaje. Marcar `ACEPTADO` la fila del registro 167 →
   aviso de éxito; la del registro 1 → "Ese registro de asistencia ya
   quedó ACEPTADO en otro lote de migración…" (en castellano, sin jerga).
5. **http://localhost:5000/admin/bitacora** → filtro por tabla con
   `<select>`, límite, y `datos_antes`/`datos_despues` legibles ("Ver
   datos"). Sin botones de editar ni borrar.
6. **Datos mock eliminados (igual que v1-v6):** apagar la API y
   recargar cualquier pantalla de esta versión → "No se pudo contactar
   la API de Cátedras. ¿Está levantada?".

## 4. Problemas frecuentes

| Síntoma | Diagnóstico |
|---|---|
| `POST /api/LoteMigracion` → 422 "fk_sesion es obligatorio" aunque se mandó | Cuerpo en camelCase (`fkSesion`): los cuerpos van en snake_case (D37-B) |
| `POST /api/LoteMigracion` → 500 "No hay registros migrables" | La sesión no tiene registros migrables (todas salvo 5712) — usar `5712` |
| `GET /api/Sesion/5712` ya dice `MIGRADA` antes de generar | Datos sembrados: hacer `PATCH` a `CERRADA` antes (paso 2) |
| `PATCH /api/DetalleMigracion/{id}` con `ACEPTADO` → 500 | Ese registro ya está `ACEPTADO` en otro lote (`ux_detmig_aceptado`): pasa con 164 de los 166. Para el 200, usar los registros 167 o 169 |
| `PATCH /api/DetalleMigracion/{id}` → 404 con un id "del lote nuevo" | Los ids de detalle dependen de la secuencia: tomarlos de `GET …?fkLoteMigracion=` |
| `POST`/`PUT`/`PATCH`/`DELETE` sobre `/api/Bitacora` → 405 | Correcto — sin verbos de escritura a propósito (D36) |
| La bitácora tiene filas de `asistente` | `trg_bitacora_asistente` + borrado lógico de `DELETE /api/Asistente`: correcto |
| `GET /api/Bitacora` tarda o devuelve demasiadas filas | Usar `?limite=` bajo |
| `fk_usuario` siempre sale `null` en `Bitacora`/`LoteMigracion` | Esperado — ningún endpoint propaga el usuario autenticado hacia la BD ([5_data_model.md](5_data_model.md) §2.3; D28 de v5) |
| `generado_en` sale con `+00:00` y `modificado_en` con `Z` | `GeneradoEn` es `DateTimeOffset`; mismo instante, distinto texto |
| Todo roto | `docker compose down -v && docker compose up -d --build` (API); para el front, confirmar que `API_BASE_URL` siga sin cambios |
