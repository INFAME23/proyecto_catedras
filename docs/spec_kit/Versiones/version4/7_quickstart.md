# Quickstart — Versión 4: sesiones y ponencias

> ✅ **Revisada y construida (2026-10-08)** — ver nota de [2_spec.md](2_spec.md).
>
> **Documento 7 de 8** del spec kit. Validación del sistema ya
> construido en v4, sobre lo que dejaron v1-v3.
>
> **Nota de alcance (v4):** mismo backend que v1-v3, sin montajes
> nuevos (ver [6_contracts.md](6_contracts.md)). Hoy la API expone
> **24** recursos: los 22 de v1-v3 más `Sesion` y `Ponencia`.

---

## 1. Levantar

Sin cambios respecto a v1-v3
([version1/7_quickstart.md](../version1/7_quickstart.md) §1) — y, por
primera vez desde v2, **sin ningún paso extra** para un volumen que ya
existía: v4 no agrega ninguna migración, así que no hay nada que
reaplicar a mano.

```powershell
git clone https://github.com/INFAME23/proyecto_catedras.git
cd proyecto_catedras
docker compose up -d --build
```

## 2. Verificación de la API — 8 pasos

```powershell
docker compose ps
# Esperado: mi_v1_catedras_api, mi_v1_catedras_postgres (healthy) y mi_v1_catedras_front corriendo
```

1. **http://localhost:8142/swagger** → ahora muestra **24** recursos;
   los 2 nuevos son `Sesion` (6 verbos) y `Ponencia` (4 verbos, con
   rutas de 3 segmentos para `GET` puntual y `DELETE` — ver
   [6_contracts.md](6_contracts.md) §3).
2. **GET /api/Sesion?fkCatedra=3975** → 59 filas, con `numero_reunion`
   de 1 a **60**, sin repetidos y **sin la 23** (hueco del dato de
   ejemplo, ver [5_data_model.md](5_data_model.md) §2.1). **GET
   /api/Sesion/1** → `fk_catedra: 1`, `numero_reunion: 33`,
   `estado: "CERRADA"`, `fk_encuesta: null`, `inicio:
   "2026-08-31T13:00:00Z"` (las 08:00 de Colombia, en UTC — ADR D21).
3. **Numeración automática (ADR D20):** `POST /api/Sesion` con
   ```json
   { "fk_catedra": 5498, "inicio": "2026-10-20T14:00:00-05:00",
     "fin": "2026-10-20T16:00:00-05:00", "fk_modalidad": "PRESENCIAL",
     "fk_sede": "SAN_BENITO", "fk_periodo": "2026-2" }
   ```
   (sin `numero_reunion`, sobre una cátedra de ejemplo SIN sesiones) →
   `200` con `numero_reunion: 1`. Repetir el mismo `POST` (mismo
   `fk_catedra`) → `200` con `numero_reunion: 2`. `GET /api/Sesion/{id}`
   de la primera → `inicio: "2026-10-20T19:00:00Z"` (14:00 -05:00 = 19:00
   UTC). Opcional: el mismo `POST` con `"fk_catedra": 3975` →
   `numero_reunion: 61` (el trigger es `MAX + 1`: no rellena el hueco 23).
4. **`fin > inicio` (RF125):** el mismo body del paso 3 con `fin`
   anterior a `inicio` → `400`, sin tocar la BD (confirmar con `GET
   /api/Sesion?fkCatedra=5498` que sigue en 2 filas, no 3).
5. **`Ponencia`, asignar y desasignar:** `POST /api/Ponencia` con
   `{"fk_sesion": <id de la sesión del paso 3>, "fk_asistente": 1, "rol": "PONENTE"}`
   → `200`. El mismo `POST` repetido → `500` (PK duplicada). El mismo
   body con `"rol": "MODERADOR"` → `200` (fila distinta, misma sesión y
   asistente). `GET /api/Ponencia/{idSesion}/1/PONENTE` → `200`.
   `DELETE /api/Ponencia/{idSesion}/1/PONENTE` →
   `filasEliminadas: 1`; repetir el mismo `GET` → `404`.
6. **`RESTRICT` oculto hacia v5/v7:** `DELETE /api/Sesion/5712` →
   `500` (`fk_lotemig_sesion` o `fk_registro_sesion`: esa sesión tiene
   los 168 `registro_asistencia` y los 5 `lote_migracion` de ejemplo), y
   `GET /api/Sesion/5712` → `200` (no se borró nada). `DELETE
   /api/Sesion/1` → `200` (una de las 5 sesiones de ejemplo sin
   `enlace_registro`/`registro_asistencia`/`lote_migracion`, ver
   [5_data_model.md](5_data_model.md) §2.1).
7. **Reset** (recomendado después de los pasos 3-6, que modifican
   datos): `docker compose down -v && docker compose up -d --build` →
   las 24 tablas vuelven a sus datos originales.
8. **`RESTRICT` hacia `asistente` (RF131):** `POST /api/Ponencia` con
   `{"fk_sesion": 2, "fk_asistente": 167}` y luego `DELETE
   /api/Asistente/167` → `500` (`fk_ponencia_asistente`); confirmar con
   `GET /api/DocumentoAsistente?fkAsistente=167` que su documento de
   ejemplo sigue intacto (la cascada de v3 no llegó a ejecutarse). Se usa
   el **167** y no el 1 porque el 1 ya tiene `registro_asistencia`: con
   él, el `DELETE` fallaría aunque no hubiera ponencia y la prueba no
   demostraría nada.

**Prueba de capas (criterio de aceptación 7):**
```powershell
cd api_catedras
dotnet run --project pruebas
```
Ahora cubre las **24** entidades con repositorios FALSOS en memoria,
incluida la validación de `fin > inicio` y la asignación/desasignación
de `Ponencia`. Termina con código 0 si todo pasa.

## 3. Frontend (`front_catedras/`, aparte del `docker compose` de arriba)

Arranque sin cambios respecto a v1-v3
([version1/7_quickstart.md](../version1/7_quickstart.md) §3).

Con la API corriendo (§1-2 de aquí):

1. **http://localhost:5000/catedras/3975** → el detalle de la cátedra
   ahora incluye su calendario: 59 sesiones con fecha y horario **en
   hora de Colombia**, modalidad, sede y estado. `/catedras/5498` → "aún
   no tiene sesiones".
2. **http://localhost:5000/sesiones/1** → detalle de la sesión con la
   lista de ponentes y su rol. Buscar `1013032` → el `<select>` ofrece al
   asistente 167 (Regla 2: se busca y se elige, no se digita) → Asignar
   como `PONENTE` → aparece en la lista. Asignarlo otra vez con el mismo
   rol → error 500 de la API visible; con `MODERADOR` → segunda fila.
   Quitar → desaparece.
3. **http://localhost:5000/ponente/?asistente=167** → calendario del
   ponente con la sesión del paso 2 y sus roles.
4. **http://localhost:5000/admin/** → sección v4 con las tarjetas
   `Sesión` (CRUD completo, se entra por cátedra: botón
   <i>calendario</i> en la lista de cátedras) y `Ponencia` (asignar/quitar,
   sin "editar"). Crear una sesión en la cátedra 5498 dejando vacío el
   número → el aviso dice "la base de datos le asignó el número de
   reunión 1".
5. **Datos mock eliminados (igual que v1-v3):** apagar la API
   (`docker compose stop api`) y recargar cualquier pantalla de esta
   versión → error claro, nunca datos inventados.

## 4. Problemas frecuentes

| Síntoma | Diagnóstico |
|---|---|
| `POST /api/Sesion` crea la sesión pero `numero_reunion` "se repite" | Revisar que no se esté comparando contra otra cátedra — el consecutivo es POR `fk_catedra`, no global (confirmar con `GET /api/Sesion?fkCatedra={la misma}`) |
| `DELETE /api/Sesion/{id}` responde 500 en una sesión "de prueba" | Normal si esa sesión ya tiene `registro_asistencia`/`lote_migracion` de ejemplo (`RESTRICT`) — usar `id_sesion` 1-5 o una sesión creada en la propia prueba |
| `GET /api/Ponencia/3` responde 404 | Correcto: `Ponencia` no tiene `/{id}` de una sola columna — usar `GET /api/Ponencia/{fkSesion}/{fkAsistente}/{rol}` (3 segmentos) |
| Las horas de la API salen 5 horas "corridas" | Correcto: la API responde en UTC (`...Z`, ADR D21). 19:00Z = 14:00 en Colombia. El front convierte al mostrar |
| `PUT`/`PATCH /api/Sesion/{id}` con `numero_reunion: 0` responde 422 | Correcto: el trigger solo numera en `INSERT`; al editar hay que mandar un número ≥ 1 |
| `GET /api/Sesion?fkCatedra=3975` "salta" de la 22 a la 24 | Correcto: el dato de ejemplo no trae la reunión 23 |
| Error "Cannot write DateTimeOffset with Offset=-05:00:00" en el log de la API | Alguien cambió el modelo a `DateTimeOffset`. Volver a `DateTime` en UTC (ADR D21) |
| `POST /api/Ponencia` con el mismo asistente y rol responde 500 | Correcto: es la misma fila (PK duplicada) — si la intención es un rol distinto, cambiar `rol` en el body, no repetir el mismo |
| Todo roto | `docker compose down -v && docker compose up -d --build` (API); para el front, confirmar que `API_BASE_URL` siga en `http://localhost:8142/api` |
