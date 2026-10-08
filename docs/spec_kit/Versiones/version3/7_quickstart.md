# Quickstart — Versión 3: personas y carga de asistentes

> ⚠️ **BORRADOR** — ver nota de [2_spec.md](2_spec.md).
>
> **Documento 7 de 8** del spec kit. Validación del sistema ya
> construido en v3, sobre lo que dejaron v1 y v2.
>
> **Nota de alcance (v3):** mismo backend que v1/v2 (`api` +
> `postgres`, sin cambios de infraestructura — ver
> [6_contracts.md](6_contracts.md)). Hoy la API expone **22** recursos:
> los 15 de v1+v2 más `Asistente`, `DocumentoAsistente`,
> `VinculacionAsistente`, `ProgramaAsistente`, `ConsentimientoDatos`,
> `LoteCargaAsistente` y `NovedadCarga`. El front (`front_catedras/`)
> suma la pantalla de carga de datos y el listado buscable de
> asistentes.

---

## 1. Levantar

Sin cambios respecto a v1/v2
([version1/7_quickstart.md](../version1/7_quickstart.md) §1):

```powershell
git clone https://github.com/INFAME23/proyecto_catedras.git
cd proyecto_catedras
docker compose up -d --build
```

**Si tu volumen de PostgreSQL ya existía** (de antes de agregar
`db/postgres/migraciones/002_...` y `003_...`): initdb no vuelve a
correr, así que los 2 SP de esta versión no existen todavía. Dos
opciones, igual que en v2:

```powershell
# Opción A: reset completo (borra los datos de prueba y resiembra todo)
docker compose down -v
docker compose up -d --build

# Opción B: aplicar solo las migraciones nuevas, conservando los datos
Get-Content db/postgres/migraciones/002_sp_guardar_asistente.sql | docker compose exec -T postgres psql -U sa -d bdcatedras_postgres_local
Get-Content db/postgres/migraciones/003_sp_guardar_lote_carga.sql | docker compose exec -T postgres psql -U sa -d bdcatedras_postgres_local
```

Sin los SP, `POST /api/Asistente` (con detalles) y
`POST /api/LoteCargaAsistente/{id}/cerrar` responden 500 con
`procedure catedras.sp_..._api(...) does not exist`.

## 2. Verificación de la API — 10 pasos

```powershell
docker compose ps
# Esperado: mi_v1_catedras_api, mi_v1_catedras_postgres corriendo (Up / healthy)
```

1. **http://localhost:8142/swagger** → ahora muestra **22** recursos;
   los 7 nuevos son `Asistente`, `DocumentoAsistente`,
   `VinculacionAsistente` y `ProgramaAsistente` (solo 3 verbos cada
   uno — sin `/{id}`, ver [6_contracts.md](6_contracts.md) §3),
   `ConsentimientoDatos` (3 verbos) y `LoteCargaAsistente`/
   `NovedadCarga`.
2. **GET /api/Asistente?limite=5** → 5 de los 14.811 asistentes de
   ejemplo, con `total` reflejando el límite. **GET
   /api/Asistente?q=Garcia** → solo los asistentes con "Garcia" en
   nombre, apellido o `id_asis` (filtrado en SQL, confirmar que la
   respuesta llega rápido incluso sin `?limite=` explícito).
3. **Maestro-detalle completo:** `POST /api/Asistente` con
   ```json
   { "nombres": "Prueba", "apellidos": "Uno", "id_asis": "Z9999999",
     "correo_institucional": "prueba.uno@usbmed.edu.co",
     "documentos": [{"fk_tipo_documento": "CC", "numero": "999999999"}],
     "vinculaciones": [{"fk_tipo_vinculacion": "ESTUDIANTE"}],
     "programas": [{"fk_programa": "M0286", "fk_periodo": "2026-1"}],
     "consentimiento": {"version_politica": "v1.0"} }
   ```
   → `200` con `id_asistente`; `GET /api/DocumentoAsistente?fkAsistente={id}`,
   `.../VinculacionAsistente?fkAsistente={id}`,
   `.../ProgramaAsistente?fkAsistente={id}` y
   `.../ConsentimientoDatos?fkAsistente={id}` muestran cada uno su fila.
   Repetir el mismo `POST` (mismo `id_asis`) → `500`, y
   `GET /api/Asistente/{id}` del segundo intento → `404` (el SP revirtió
   todo, incluidos los 4 detalles).
4. **Índice único parcial (RF103):** `POST /api/DocumentoAsistente` con
   `{"fk_asistente": {id}, "fk_tipo_documento": "TI", "numero": "111", "vigente": true}`
   sobre el asistente del paso 3 (que ya tiene un documento vigente) →
   `500` (`ux_documento_vigente`). `PATCH` el documento del paso 3 a
   `{"vigente": false}` → `200`; repetir el `POST` de este paso → `200`.
5. **Vigencia con llave compuesta (ADR D16):** `PATCH
   /api/VinculacionAsistente/cerrar` con
   `{"fk_asistente": {id}, "fk_tipo_vinculacion": "ESTUDIANTE", "fecha_ini": "<la que puso el paso 3>", "fecha_fin": "2026-10-07"}`
   → `200`; repetir el mismo `PATCH` → `400` ("ya estaba cerrada").
   `GET /api/VinculacionAsistente/9999999999` (intentar `/{id}`) → `404`
   de ASP.NET (la ruta no existe, no hay columna `id`).
6. **Lote de carga, cierre parcial (ADR D15):** `POST
   /api/LoteCargaAsistente` con `{"nombre_archivo": "prueba.xlsx"}` →
   `200` con `id_lote_carga`; crear 1-2 `Asistente` con ese
   `fk_lote_carga`; luego `POST .../{id}/cerrar` con
   `{"filasLeidas": 3, "filasAceptadas": 2, "filasRechazadas": 1, "novedades": [{"numeroFila": 2, "contenidoCrudo": "...", "motivo": "correo duplicado"}]}`
   → `200`; `GET /api/NovedadCarga?fkLoteCarga={id}` muestra esa fila.
   Repetir el mismo `POST .../cerrar` → `400` (ya cerrado).
7. **`CHECK` de autorreferencia de campos (RF101):** `POST
   /api/Asistente` con `{"nombres": "X", "apellidos": "Y", "es_externo": false}`
   (sin `id_asis`) → `500` (`chk_asistente_externo`); con
   `{"es_externo": true, "id_asis": "ABC"}` (ambos presentes) → también
   `500` (el `CHECK` exige que sean mutuamente excluyentes, no solo
   "uno de los dos").
8. **`RESTRICT`, verificado (si falla):** `DELETE
   /api/Asistente/{id}` sobre uno de los 14.810 asistentes reales con
   `programa_asistente` ya sembrado (15.349 filas) → `500`; confirmar
   con `GET /api/DocumentoAsistente?fkAsistente={id}` que sus
   documentos de ejemplo SIGUEN ahí (la cascada no llegó a ejecutarse).
   **No** usar el asistente creado en el paso 3 para esta prueba — ese
   sí se puede borrar limpio (ver limpieza, abajo).
9. **Consentimiento, solo lectura tras crear (RF113):** sobre el
   consentimiento creado en el paso 3, intentar
   `PUT /api/ConsentimientoDatos/{id}` → `404` de ASP.NET (la ruta no
   existe, ni `PUT` ni `PATCH` ni `DELETE` están declarados).
10. **Reset** (recomendado después de las pruebas anteriores, que
    agregan filas de prueba): `docker compose down -v && docker compose
    up -d --build` → las 22 tablas vuelven a sus datos originales. Si
    se prefiere no resetear: `DELETE /api/Asistente/{id del paso 3}`
    borra en cascada sus 4 detalles de prueba (documento, vinculación,
    programa, consentimiento) sin tocar los 14.810 reales.

**Prueba de capas (criterio de aceptación 8):**
```powershell
cd api_catedras
dotnet run --project pruebas
```
Ahora cubre las **22** entidades con repositorios FALSOS en memoria,
incluida la validación de `id_asis`/`es_externo` y los 3 contadores del
cierre de lote. Termina con código 0 si todo pasa.

## 3. Frontend (`front_catedras/`, aparte del `docker compose` de arriba)

Arranque sin cambios respecto a v1/v2
([version1/7_quickstart.md](../version1/7_quickstart.md) §3):

```powershell
cd front_catedras
python -m venv venv
venv\Scripts\activate
pip install -r requirements.txt
python run.py
```

Con la API corriendo (§1-2 de aquí):

1. **http://localhost:5000/admin/** → tarjetas de las 7 tablas de v3
   (o 5, si `NovedadCarga` y los detalles de `Asistente` se integran en
   su propia pantalla — decisión de UI libre, ver criterio 10 de
   [2_spec.md](2_spec.md)); `VinculacionAsistente`/`ProgramaAsistente`
   muestran "cerrar"/"cambiar estado" en vez de "editar"/"eliminar".
2. **http://localhost:5000/admin/asistentes** (o la ruta que defina
   `3_plan.md`) → listado paginado y buscable: escribir en el buscador
   dispara `GET /api/Asistente?q=...` en cada tecla (o con un pequeño
   *debounce*), **sin** descargar las 14.811 filas de una vez — a
   diferencia del catálogo de `Catedra` en v2.
3. **http://localhost:5000/carga_datos** (o la ruta equivalente) → subir
   un archivo de prueba (puede ser un `.xlsx`/`.csv` pequeño armado a
   mano con 2-3 filas, una de ellas inválida a propósito — p. ej. sin
   `nombres`); al terminar, la pantalla reporta cuántas filas se
   aceptaron y cuántas se rechazaron, con el motivo de cada rechazo.
   `GET /api/NovedadCarga?fkLoteCarga={id del lote recién cerrado}`
   confirma que el motivo mostrado en pantalla es el mismo que quedó
   guardado en la BD.
4. **Datos mock eliminados (igual que v1/v2):** apagar la API
   (`docker compose stop api`) y recargar `/admin/asistentes` → debe
   mostrar un error claro, nunca una lista inventada por el front.

## 4. Problemas frecuentes

| Síntoma | Diagnóstico |
|---|---|
| `POST /api/Asistente` con detalles responde 500 "procedure ... does not exist" | Falta aplicar `002_sp_guardar_asistente.sql` sobre un volumen que ya existía antes de la migración — ver §1, Opción B |
| `POST .../LoteCargaAsistente/{id}/cerrar` responde 500 "procedure ... does not exist" | Igual que arriba, con `003_sp_guardar_lote_carga.sql` |
| `GET /api/VinculacionAsistente/3` responde 404 | Correcto: esta tabla no tiene `/{id}` (ADR D16) — usar `?fkAsistente=` para listar y `PATCH .../cerrar` con la llave completa para cerrar |
| `PATCH /api/VinculacionAsistente/cerrar` responde 400 aunque la vinculación existe | Revisar que `fecha_fin` no esté ya puesta (una vinculación cerrada no se "re-cierra") |
| `DELETE /api/Asistente/{id}` responde 500 en un asistente que "debería poder borrarse" | Normal si tiene `programa_asistente`/`usuario`/`ponencia`/`registro_asistencia` ya sembrados (`RESTRICT`) — usar un asistente creado en la propia prueba, no uno de los 14.810 reales |
| El buscador de `/admin/asistentes` tarda igual que el catálogo de `Catedra` | Revisar que el front esté mandando `?q=` a la API en cada búsqueda, no descargando todo y filtrando en Flask (eso sería repetir D13 en vez de D18) |
| La carga de datos "se queda a medias" en un archivo grande | Esperado si el archivo es grande: son N peticiones HTTP secuenciales, una por fila (D15) — no es un error, es el costo aceptado de la carga parcial; revisar la barra de progreso o el conteo en pantalla, no asumir que se colgó |
| Todo roto | `docker compose down -v && docker compose up -d --build` (API); para el front, confirmar que `API_BASE_URL` siga en `http://localhost:8142/api` |
