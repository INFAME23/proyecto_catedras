# Quickstart — Versión 4: sesiones y ponencias

> ⚠️ **BORRADOR** — ver nota de [2_spec.md](2_spec.md).
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
# Esperado: mi_v1_catedras_api, mi_v1_catedras_postgres corriendo (Up / healthy)
```

1. **http://localhost:8142/swagger** → ahora muestra **24** recursos;
   los 2 nuevos son `Sesion` (6 verbos) y `Ponencia` (4 verbos, con
   rutas de 3 segmentos para `GET` puntual y `DELETE` — ver
   [6_contracts.md](6_contracts.md) §3).
2. **GET /api/Sesion?fkCatedra=3975** → 59 filas, con `numero_reunion`
   de 1 a 59 sin huecos. **GET /api/Sesion/1** →
   `estado: "CERRADA"`, `fk_encuesta: null`.
3. **Numeración automática (ADR D20):** `POST /api/Sesion` con
   ```json
   { "fk_catedra": 5498, "inicio": "2026-10-20T14:00:00-05:00",
     "fin": "2026-10-20T16:00:00-05:00", "fk_modalidad": "PRESENCIAL",
     "fk_sede": "SAN_BENITO", "fk_periodo": "2026-2" }
   ```
   (sin `numero_reunion`, sobre una cátedra de ejemplo SIN sesiones) →
   `200` con `numero_reunion: 1`. Repetir el mismo `POST` (mismo
   `fk_catedra`) → `200` con `numero_reunion: 2`.
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
6. **`RESTRICT` oculto hacia v5/v7 — probar SOLO con sesiones
   seguras:** `DELETE /api/Sesion/1` → `200` (una de las 5 sesiones de
   ejemplo sin `enlace_registro`/`registro_asistencia`/
   `lote_migracion`, ver [5_data_model.md](5_data_model.md) §2.1).
   **No** probar este verbo contra otros `id_sesion` sembrados sin
   verificar antes contra la BD: muchos tienen `registro_asistencia`
   de ejemplo (168 filas en total) y `DELETE` responderá `500`; eso es
   correcto, no un bug.
7. **Reset** (recomendado después de los pasos 3-6, que modifican
   datos): `docker compose down -v && docker compose up -d --build` →
   las 24 tablas vuelven a sus datos originales.
8. **`RESTRICT` hacia `asistente` (RF131):** crear una `Ponencia` nueva
   (paso 5) y luego `DELETE /api/Asistente/{ese fk_asistente}` → `500`
   (`fk_ponencia_asistente`); confirmar con `GET
   /api/DocumentoAsistente?fkAsistente={id}` que sus documentos de
   ejemplo siguen intactos (la cascada de v3 no llegó a ejecutarse).

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

1. **http://localhost:5000/catedras/<id de una cátedra con sesiones,
   p. ej. la de `000023636`>** → el detalle de la cátedra ahora
   incluye su calendario de sesiones (fecha, modalidad, estado).
2. **http://localhost:5000/sesiones/<id_sesion>** (o la ruta que fije
   la implementación) → detalle de la sesión con la lista de ponentes
   y su rol; un formulario para asignar uno nuevo (`<select>` de
   asistentes, Regla 2 de la guía IA).
3. **http://localhost:5000/admin/** → 2 tarjetas nuevas (`Sesion` con
   CRUD completo; `Ponencia` con asignar/quitar, sin "editar").
4. **Datos mock eliminados (igual que v1-v3):** apagar la API
   (`docker compose stop api`) y recargar cualquier pantalla de esta
   versión → error claro, nunca datos inventados.

## 4. Problemas frecuentes

| Síntoma | Diagnóstico |
|---|---|
| `POST /api/Sesion` crea la sesión pero `numero_reunion` "se repite" | Revisar que no se esté comparando contra otra cátedra — el consecutivo es POR `fk_catedra`, no global (confirmar con `GET /api/Sesion?fkCatedra={la misma}`) |
| `DELETE /api/Sesion/{id}` responde 500 en una sesión "de prueba" | Normal si esa sesión ya tiene `registro_asistencia`/`lote_migracion` de ejemplo (`RESTRICT`) — usar `id_sesion` 1-5 o una sesión creada en la propia prueba |
| `GET /api/Ponencia/3` responde 404 | Correcto: `Ponencia` no tiene `/{id}` de una sola columna — usar `GET /api/Ponencia/{fkSesion}/{fkAsistente}/{rol}` (3 segmentos) |
| `POST /api/Ponencia` con el mismo asistente y rol responde 500 | Correcto: es la misma fila (PK duplicada) — si la intención es un rol distinto, cambiar `rol` en el body, no repetir el mismo |
| Todo roto | `docker compose down -v && docker compose up -d --build` (API); para el front, confirmar que `API_BASE_URL` siga en `http://localhost:8142/api` |
