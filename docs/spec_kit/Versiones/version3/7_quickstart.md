# Quickstart — Versión 3: personas y carga de asistentes

> ✅ **Construida y verificada (2026-10-07)** — ver nota de [2_spec.md](2_spec.md); correcciones al borrador en el ADR D19 de [4_research.md](4_research.md).
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
`procedure catedras.sp_guardar_asistente(...) does not exist` (o
`sp_guardar_lote_carga`).

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
   /api/Asistente?q=1000445** → solo el asistente con ese texto en
   nombre, apellido o `id_asis` (filtrado en SQL). Los nombres sembrados
   son genéricos ("Asistente ASIS 1000445"): buscar un apellido real como
   "Garcia" responde 204. **GET /api/Asistente?limite=2&pagina=2** →
   los asistentes 3 y 4.
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
   `{"fk_asistente": {id}, "fk_tipo_documento": "TI", "numero": "11112222", "vigente": true}`
   (**corregido**: con `"111"` respondería 422 — el número exige 4 a 20 caracteres)
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
   `{"filas_leidas": 3, "filas_aceptadas": 2, "filas_rechazadas": 1, "novedades": [{"numero_fila": 2, "contenido_crudo": "...", "motivo": "correo duplicado"}]}`
   → `200`; `GET /api/NovedadCarga?fkLoteCarga={id}` muestra esa fila.
   Repetir el mismo `POST .../cerrar` → `400` (ya cerrado).
7. **Cruce `id_asis`/`es_externo` (RF101 — corregido, ADR D19):** `POST
   /api/Asistente` con `{"nombres": "X", "apellidos": "Y", "es_externo": false, "correo_personal": "x@y.co"}`
   (sin `id_asis`) → `400` del servicio; sin ningún correo → `400`. Un
   externo CON `id_asis` (`{"es_externo": true, "id_asis": "EXT00001", ...}`)
   → `200`: el `CHECK chk_asistente_externo` no los hace excluyentes.
8. **`RESTRICT`, verificado (corregido):** `DELETE /api/Asistente/1`
   (tiene `registro_asistencia` sembrado) → `500`; confirmar con
   `GET /api/DocumentoAsistente?fkAsistente=1` que su documento de
   ejemplo SIGUE ahí (la cascada no llegó a ejecutarse). Ojo:
   `programa_asistente` es `CASCADE` y no bloquea; bloquean
   `registro_asistencia` (168 filas), `usuario` (1) y `ponencia`.
   **No** usar el asistente creado en el paso 3 para esta prueba — ese
   sí se puede borrar limpio (ver limpieza, abajo).
9. **Consentimiento, solo lectura tras crear (RF113):** sobre el
   consentimiento creado en el paso 3, intentar
   `PUT /api/ConsentimientoDatos/{id}` → `405` de ASP.NET (la ruta
   `/{id}` existe solo para `GET`; ni `PUT` ni `PATCH` ni `DELETE` están
   declarados).
10. **Reset** (recomendado después de las pruebas anteriores, que
    agregan filas de prueba): `docker compose down -v && docker compose
    up -d --build` → las 22 tablas vuelven a sus datos originales. Si
    se prefiere no resetear: `DELETE /api/Asistente/{id del paso 3}`
    borra en cascada sus 4 detalles de prueba (documento, vinculación,
    programa, consentimiento) sin tocar los 14.810 reales.

**Prueba de capas (criterio de aceptación 8):**
```powershell
docker compose exec api dotnet run --project pruebas
```
Se corre dentro del contenedor: en Windows, el Control de aplicaciones
puede bloquear la DLL recién compilada si se corre en el equipo. Cubre
las **22** entidades con repositorios FALSOS en memoria, incluida la
validación de `id_asis`/`es_externo` y los 3 contadores del cierre de
lote. Termina con código 0 si todo pasa (al construir la v3: **220
pasadas, 0 fallidas**).

## 3. Frontend (`front_catedras/`)

**(corregido)** El front es el servicio `front` del mismo
`docker-compose.yml` (puerto 5000). La v3 agrega `openpyxl` a su
`requirements.txt`, así que la primera vez hay que reconstruir su imagen:

```powershell
docker compose up -d --build front
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
| `DELETE /api/Asistente/{id}` responde 500 en un asistente que "debería poder borrarse" | Normal si tiene `usuario`/`ponencia`/`registro_asistencia` (`RESTRICT`; p. ej. el asistente 1) — `programa_asistente` NO bloquea, es `CASCADE`. Usar un asistente creado en la propia prueba |
| El buscador de `/admin/asistentes` tarda igual que el catálogo de `Catedra` | Revisar que el front esté mandando `?q=` a la API en cada búsqueda, no descargando todo y filtrando en Flask (eso sería repetir D13 en vez de D18) |
| La carga de datos "se queda a medias" en un archivo grande | Esperado si el archivo es grande: son N peticiones HTTP secuenciales, una por fila (D15) — no es un error, es el costo aceptado de la carga parcial; la pantalla muestra "Cargando fila por fila..." mientras procesa (como referencia, 4.000 filas tardan del orden de medio minuto) |
| La carga dice "la API dejó de responder" | La API se cayó a mitad de la carga: las filas ya aceptadas quedaron guardadas y el lote quedó ABIERTO (sin contadores). Levantar la API y volver a cargar; las filas ya guardadas se rechazarán por `id_asis` duplicado |
| Todo roto | `docker compose down -v && docker compose up -d --build`; dentro de Docker el front usa `API_BASE_URL=http://api:8042/api` (fuera de Docker, `http://localhost:8142/api`) |
