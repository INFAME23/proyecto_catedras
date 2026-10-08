# Quickstart — Versión 6: encuestas de calidad

> ⚠️ **BORRADOR** — ver nota de [2_spec.md](2_spec.md).
>
> **Documento 7 de 8** del spec kit. Validación del sistema ya
> construido en v6, sobre lo que dejaron v1-v5.
>
> **Nota de alcance (v6):** mismo backend que v1-v5, con 1 migración
> nueva (`004_sp_guardar_pregunta.sql`). Hoy la API expone **33**
> recursos: los 29 de v1-v5 más `Pregunta`, `OpcionPregunta`,
> `RespuestaEncuesta` y `RespuestaItem`.

---

## 1. Levantar

Sin cambios respecto a v1-v5
([version1/7_quickstart.md](../version1/7_quickstart.md) §1).

**Si tu volumen de PostgreSQL ya existía** (de antes de agregar
`004_sp_guardar_pregunta.sql`): igual que con `001_...`/`002_...`/
`003_...` en v2/v3, hay que reaplicar a mano o resetear:

```powershell
# Opción A: reset completo
docker compose down -v
docker compose up -d --build

# Opción B: aplicar solo la migración nueva
Get-Content db/postgres/migraciones/004_sp_guardar_pregunta.sql | docker compose exec -T postgres psql -U sa -d bdcatedras_postgres_local
```

## 2. Verificación de la API — 7 pasos

```powershell
docker compose ps
```

1. **http://localhost:8142/swagger** → ahora muestra **33** recursos.
2. **GET /api/Pregunta** → 4 filas de ejemplo (2 `ESCALA_5`
   obligatorias, 1 `SI_NO` opcional, 1 `TEXTO_LIBRE` opcional, todas
   de la encuesta `1`).
3. **Maestro-detalle de `Pregunta` (criterio 2):** `POST /api/Pregunta`
   con
   ```json
   { "fkEncuesta": 1, "enunciado": "¿Volvería a esta cátedra?",
     "orden": 5, "fkTipoPregunta": "OPCION_UNICA", "obligatoria": false,
     "opciones": [ {"etiqueta": "Sí", "valor": 1, "orden": 1},
                   {"etiqueta": "No", "valor": 0, "orden": 2} ] }
   ```
   → `200`; `GET /api/OpcionPregunta?fkPregunta={id}` muestra las 2
   opciones. Repetir el mismo `POST` con `orden: 5` (ya existe en la
   encuesta `1`) → `500`, y la pregunta tampoco queda creada
   (`GET /api/Pregunta/{id siguiente}` → `404`).
4. **Responder una encuesta (criterio 3):** usar `id_registro: 61`
   (un registro de ejemplo de la sesión `5712`, con `fk_encuesta: 1` y
   sin respuesta previa — confirmar con `GET
   /api/RespuestaEncuesta?fkEncuesta=1` que no aparece). `POST
   /api/RespuestaEncuesta` con
   ```json
   { "fkRegistro": 61,
     "respuestas": [ {"pregunta": 1, "numero": 5},
                     {"pregunta": 2, "numero": 4},
                     {"pregunta": 3, "numero": 1} ] }
   ```
   (preguntas `1` y `2`, obligatorias, respondidas; `4`, opcional de
   texto, omitida) → `200` con `idRespuesta`; `GET
   /api/RespuestaEncuesta/{idRespuesta}` → `completa: true`. Repetir el
   mismo `POST` → `500` (`uq_respuesta_registro`).
5. **Preguntas obligatorias faltantes (criterio 4):** repetir el paso
   4 con otro `fkRegistro` sin respuesta previa (p. ej. `62`) pero
   omitiendo la pregunta `2` (obligatoria) → `500`
   (`RN-26`); `GET /api/RespuestaEncuesta?fkEncuesta=1` confirma que
   NO quedó ninguna fila nueva para ese registro (todo se revirtió,
   incluido el `INSERT` del maestro).
6. **`RESTRICT` hacia `pregunta` (criterio 5):** `DELETE
   /api/Pregunta/1` (una de las 4 de ejemplo, con respuestas reales) →
   `500`; `GET /api/OpcionPregunta` (si la pregunta tuviera opciones
   de ejemplo) confirmaría que siguen intactas — en este caso la
   pregunta `1` es `ESCALA_5` y no tiene opciones, así que basta con
   confirmar que `GET /api/Pregunta/1` sigue respondiendo `200`.
7. **Reset** (recomendado después de los pasos 3-6):
   `docker compose down -v && docker compose up -d --build`.

**Prueba de capas (criterio de aceptación 6):**
```powershell
cd api_catedras
dotnet run --project pruebas
```
Ahora cubre las **33** entidades con repositorios FALSOS, incluida la
validación de `valor_min`/`valor_max` en `Pregunta` y la confirmación
de que `IRepositorioRespuestaEncuesta` no declara ningún método de
"cerrar". Termina con código 0.

## 3. Frontend (`front_catedras/`, aparte del `docker compose` de arriba)

Arranque sin cambios respecto a v1-v5.

Con la API corriendo (§1-2 de aquí):

1. **http://localhost:5000/sesiones/5712** (o la ruta equivalente) →
   muestra un enlace "responder encuesta" si el asistente tiene un
   registro sin respuesta.
2. El formulario de respuesta renderiza un campo distinto según
   `fkTipoPregunta` de cada pregunta de la encuesta `1`.
3. **http://localhost:5000/admin/** → 4 tarjetas nuevas; `Pregunta`
   permite agregar opciones iniciales al crear (si el tipo es
   `OPCION_UNICA`); `RespuestaEncuesta`/`RespuestaItem` solo listan.
4. **Datos mock eliminados (igual que v1-v5):** apagar la API y
   recargar cualquier pantalla de esta versión → error claro.

## 4. Problemas frecuentes

| Síntoma | Diagnóstico |
|---|---|
| `POST /api/Pregunta` con `opciones` responde 500 "procedure ... does not exist" | Falta aplicar `004_sp_guardar_pregunta.sql` sobre un volumen que ya existía — ver §1, Opción B |
| `POST /api/RespuestaEncuesta` responde 500 "La sesion de ese registro no tiene encuesta asignada" | El `fk_registro` usado corresponde a una sesión sin `fk_encuesta` — usar un registro de una sesión que sí la tenga (p. ej. `id_sesion: 5712`) |
| `POST /api/RespuestaEncuesta` responde 500 aunque "se respondieron todas las preguntas que se le ocurrieron al probador" | Revisar que estén las preguntas marcadas `obligatoria: true` de ESA encuesta, no solo "varias" — `GET /api/Pregunta?fkEncuesta={id}` antes de responder |
| "¿Dónde está el verbo para cerrar la encuesta?" | No existe — "responder" ya cierra internamente (D30); si el `POST` tuvo éxito, `completa` ya es `true` |
| Todo roto | `docker compose down -v && docker compose up -d --build` (API); para el front, confirmar que `API_BASE_URL` siga sin cambios |
