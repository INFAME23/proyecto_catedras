# Estado de la v6 y cumplimiento contra `VERSION_2.md`

> Generado por una sesión de IA (Claude) el 2026-10-13, a partir del repositorio
> subido y de `VERSION_2.md` (el documento de requisitos del profesor). Es
> insumo para la sustentación individual — **no reemplaza entenderlo** (§8.1).

---

## 1. Resumen ejecutivo

| | |
|---|---|
| **Avance de esta sesión** | Backend completo de la v6 (Fases 47-49 de `8_tasks.md`): 28 archivos C# nuevos + la migración SQL `004_sp_guardar_pregunta.sql` + registro en `Program.cs` y `docker-compose.yml`. **No compilé ni corrí nada** (sin `dotnet` en este entorno): revísenlo antes de confiar en que corre. |
| **Lo que falta de la v6** | Fase 50 (prueba de capas), Fase 51 (verificación con Docker real), Fase 52 (frontend Flask: responder encuesta + admin de preguntas), Fase 53 (cierre y tag `v6`). |
| **El hallazgo más importante** | Comparé el proyecto contra `VERSION_2.md` y encontré **tres incumplimientos estructurales que atraviesan TODO el proyecto** (no solo la v6): nada pasa por procedimientos almacenados fuera de los 4 maestro-detalle, los errores de la base de datos responden 500 en vez de 409, y las 12 tablas con columna `activo` se borran físicamente en vez de lógicamente. Están en la sección 2 — léanla antes de seguir construyendo sobre el mismo patrón. |
| **Riesgo a vigilar** | La v5 ya construyó login real (BCrypt, roles, `/admin` protegido) — el profesor asigna eso explícitamente a la v3 (§10: "adelantarlos no suma, resta"). |
| **Entregable que falta por completo** | La carpeta de "prompts" que exige el §11 (los que de verdad usaron, con lo que le corrigieron a la IA) no existe en ninguna versión, v1 a v6. |

---

## 2. Los tres hallazgos estructurales (afectan TODO el proyecto, no solo v6)

Los comparé leyendo `VERSION_2.md` §4, §7 y el esquema real (`script_bdcatedras.sql`)
contra el código que ya existe (v1-v5) y contra lo que yo mismo acabo de construir
para la v6 — **repliqué fielmente el patrón que ya tenían**, así que lo nuevo
tiene el mismo problema que lo viejo.

### 2.1 Criterio 3 — "los cinco verbos de TODAS las tablas pasan por procedimientos almacenados"

> *"Se abre el repositorio: no hay SQL escrito a mano."* — §8, criterio 3

Lo que hay hoy: cada `Repositorio*Postgres.cs` (los más de 30 que existen,
incluidos los 4 que acabo de escribir) ejecuta `SELECT`/`INSERT`/`UPDATE`/`DELETE`
de Dapper **a mano**, directamente contra las tablas. Solo hay procedimientos
almacenados para 4 operaciones de creación maestro-detalle
(`sp_guardar_programa_academico`, `sp_guardar_asistente`,
`sp_guardar_lote_carga`, y el `sp_guardar_pregunta` que acabo de agregar).
Ningún `Listar`, `Obtener`, `Actualizar` ni `Eliminar` pasa por un SP, y
ninguno sigue el molde `sp_listar_<tabla>` / `sp_consultar_<tabla>` /
`sp_crear_<tabla>` / `sp_actualizar_<tabla>` / `sp_eliminar_<tabla>` con
`INOUT ... JSON` que pide el documento.

Esto no es una interpretación mía: el propio `.csproj` lo dice en su
comentario sobre Dapper ("corre el SQL que escribimos a mano"), y el
Artículo correspondiente de `1_constitution.md` avala ese enfoque. Es decir:
**la arquitectura del proyecto, desde la v1, fue decidida en un sentido
distinto al que pide este documento.**

### 2.2 Criterio 7 — "409, nunca 500"

> *"Si responde 500, lo que está diciendo es 'me caí'... Un 500 es criterio fallado."* — §7 y §8, criterio 7

Revisé los `catch (NpgsqlException ex)` de todos los controladores (incluidos
los 3 que acabo de escribir: `PreguntaController`, `OpcionPreguntaController`,
`RespuestaEncuestaController`) y **todos** devuelven `500` cuando la base de
datos rechaza una FK inexistente o una pareja repetida. El documento pide
**409** con mensaje en castellano para esos dos casos específicos (padre que
no existe, pareja repetida en tabla puente), reservando el 500 para errores
que de verdad son "me caí".

### 2.3 Lo que falta del §4 — borrado lógico donde hay columna `activo`

12 tablas tienen `activo boolean DEFAULT true NOT NULL`: `asistente`,
`catedra`, `dependencia`, `facultad`, `modalidad`, `periodo_academico`,
`programa_academico`, `sede`, `tipo_documento`, `tipo_evento`,
`tipo_vinculacion`, `usuario`. El documento es explícito: si existe `activo`,
`Eliminar` debe ser un `UPDATE ... SET activo = false`, no un `DELETE`, y
**todos los listados deben filtrar los inactivos**. Confirmé que las 12
usan `DELETE FROM` sin condición — esto también pesa en el criterio 10
("si el borrado es lógico, los listados filtran").

(Las 4 tablas nuevas de la v6 — `pregunta`, `opcion_pregunta`,
`respuesta_encuesta`, `respuesta_item` — **no tienen** columna `activo` en
el esquema real, así que el `DELETE` físico que les puse sí es correcto
para ellas. El problema es con las 12 que si la tienen.)

### Lo que SÍ está bien — no es todo malo

- **Criterio 4 (disparador):** ya hay **7** funcionando en el esquema real
  (`trg_bitacora_asistente`, `trg_bitacora_registro`, `trg_clave_derivar`,
  `trg_enlace_canal`, `trg_registro_validar`, `trg_respuesta_item_tipo`,
  `trg_sesion_consecutivo`) — superan de sobra el mínimo de uno, e incluyen
  una bitácora real (Propuesta C del §5) y validaciones cruzadas entre
  tablas (Propuesta D). Esto no hay que construirlo, solo saberlo explicar
  en la sustentación (§8.1 pregunta exactamente "cuénteme este disparador").
- **Criterios 5 y 6 (FK por desplegable, "ninguna" → null):** revisé varias
  plantillas de `front_catedras/templates/admin/` y el patrón ya está:
  `<select>` cargado desde lo que devuelve la API, mostrando nombre,
  mandando código, con opción "— Sin documento —" para las FK opcionales.

### Qué hacer con esto

Este es un cambio de arquitectura grande (30+ tablas, 5 versiones ya
cerradas) — **no es algo que deba decidir yo ni resolver en un parche
silencioso.** Les recomiendo, en orden:

1. **Discútanlo como equipo** esta semana, con tiempo, no la noche antes de
   entregar.
2. Si `VERSION_2.md` es realmente la versión vigente de los requisitos (no
   una versión vieja o de otro grupo), **pregúntenle al profesor** si la v1
   y la v2 ya cerradas se pueden dejar como están (con esta limitación
   documentada y explicada) o si hay que retrofitarlas — el costo no es
   igual.
3. El criterio 7 (409) y el borrado lógico de las 12 tablas con `activo`
   son arreglables sin tocar el modelo de datos ni reescribir las 30+
   tablas en procedimientos — son los más baratos de corregir primero.
   El criterio 3 (SP para todo) es el cambio grande.

---

## 3. Lo demás que encontré (menor, pero vale la pena arreglarlo)

| # | Qué | Dónde |
|---|---|---|
| 1 | **Riesgo:** la v5 ya construyó login real / roles / `/admin` protegido — el profesor asigna eso a la v3 ("adelantarlos no suma, resta", §10 de `VERSION_2.md`). El equipo tiene una justificación técnica real (la dependencia circular de FK con `usuario`, documentada en `0_mapa_versiones.md` §3 y los ADR D28-D29) — tenerla lista para la sustentación, porque vale 4/20 puntos de la versión. | `docs/spec_kit/Versiones/0_mapa_versiones.md` |
| 2 | **Falta la carpeta de "prompts"** que exige el §11 en los dos formatos (chat e IDE agéntico), con lo que hubo que corregirle a la IA. No existe en ninguna versión, v1 a v6. | `docs/spec_kit/Versiones/version*/` |
| 3 | **La v1 no está formalmente cerrada**: falta actualizar sus documentos, marcar la Fase 12 y el tag `v1`. El §9 de `VERSION_2.md` exige que la regresión de la v1 pase completa — conviene cerrarla antes de seguir. | `docs/spec_kit/Versiones/version1/` |
| 4 | **La v5 solo le falta el commit con el tag `v5`** (según `0_mapa_versiones.md`, estado a 2026-10-11). | — |
| 5 | **`guia_ia.md` es un solo archivo global**, pero el §11 pide "los mismos nueve documentos... y la guía de IA" **por carpeta de versión**. Confirmen con el profesor si vale la guía única o si hay que copiarla/adaptarla en cada `version*/`. | `docs/spec_kit/guia_ia.md` |

---

## 4. Plan de parches — prompts listos para repartir

Cuatro tareas. Cada una es un prompt que la persona pega tal cual en su
sesión de IA (y **anota lo que tuvo que corregirle** — eso es justo el
entregable que falta, el #2 de la sección 3). Sugerencia de reparto según
los roles que veo en el proyecto, pero ajústenlo a quién tiene tiempo esta
semana.

### Parche 1 — Prueba de capas de la v6 (Fase 50) · sugerido: Isaac

```
Estoy en la rama con el backend de la v6 ya construido (Pregunta,
OpcionPregunta, RespuestaEncuesta, RespuestaItem). Necesito que completes
la Fase 50 de docs/spec_kit/Versiones/version6/8_tasks.md: agregar a
api_catedras/pruebas/Programa.cs un repositorio FALSO en memoria y sus
verificaciones para estas 4 entidades, siguiendo EXACTAMENTE el mismo
patrón que ya existe ahí para ProgramaAcademico y Sesion (léelo primero
en el archivo). Cubre: crear con opciones iniciales en Pregunta (y que si
una opción falla no quede nada), el "orden" repetido, actualizar parcial,
eliminar bloqueado por RESTRICT, y "responder" una encuesta completa.
Al final corre (o dime cómo correr) `dotnet run --project pruebas` y
dime cuántas verificaciones pasaron. No toques las capas reales, solo
pruebas/Programa.cs.
```

### Parche 2 — Verificación con Docker real + regresión v1 (Fase 51) · sugerido: Jhoan

```
Necesito verificar la v6 contra Docker + PostgreSQL reales, EN ESTE ORDEN
(ver Fase 51 de docs/spec_kit/Versiones/version6/8_tasks.md):
1. docker compose down -v && docker compose up -d --build (para que
   corra la migración 004_sp_guardar_pregunta.sql).
2. Crear una pregunta OPCION_UNICA con 2 opciones en el mismo POST, y
   confirmar que si repito el 'orden' de una opción, NO queda ni la
   pregunta ni ninguna opción (transacción completa).
3. Responder una encuesta con fn_responder_encuesta_api: una respuesta
   por registro, que falte una obligatoria la rechace, que no se pueda
   responder dos veces el mismo registro.
4. Correr TODA la colección de pruebas de la v1 (regresión obligatoria,
   §9 de VERSION_2.md) y confirmar que sigue pasando completa.
Anota cualquier error que encuentres, con el mensaje exacto.
```

### Parche 3 — Frontend Flask de la v6 (Fase 52) · sugerido: Alex (quien escribe este prompt)

```
Tengo el backend de la v6 ya construido y necesito el frontend en
front_catedras (Flask). Sigue la Fase 52 de
docs/spec_kit/Versiones/version6/8_tasks.md:
1. En templates/detalle_sesion.html (v4), agregar el enlace para
   responder la encuesta de esa sesión.
2. routes/encuestas.py + templates/encuesta_form.html — el formulario
   que arma el POST /api/RespuestaEncuesta con la lista 'respuestas'.
3. routes/admin.py — los 4 bloques nuevos (Pregunta con sus opciones
   iniciales en el mismo formulario, igual que ya hacen alias_form.html
   o asistente_form.html con sus detalles iniciales).
4. templates/admin/ — las plantillas nuevas, con el MISMO layout de las
   que ya existen (Bootstrap, breadcrumbs, mensajes flash).
Usa <select> para 'fk_tipo_pregunta', NUNCA un campo de texto (criterio 5
de VERSION_2.md): cárgalo desde GET /api/TipoPregunta. Y en ningún lado
de la interfaz pueden aparecer las palabras "PUT", "PATCH", "422" ni "FK"
(criterio 11).
```

### Parche 4 — Decisión de arquitectura: SP para todo y 409 en vez de 500 · sugerido: todo el equipo, con Jhoan coordinando

```
Necesito evaluar qué tan grande es el trabajo de hacer que el proyecto
cumpla dos criterios de VERSION_2.md que hoy no se cumplen:

(a) Criterio 7 — que un NpgsqlException por clave foránea inexistente o
    pareja duplicada responda 409 en castellano, no 500. Para EMPEZAR,
    hazlo solo en PreguntaController, OpcionPreguntaController y
    RespuestaEncuestaController (los 3 de la v6): distingue, por el
    SqlState de la excepción (23503 = foreign_key_violation, 23505 =
    unique_violation), cuándo devolver 409 en vez de 500, y arma el
    mensaje en español. Después dime cuántos controladores más del
    proyecto (v1-v5) necesitarían el mismo cambio y si conviene un
    middleware/filtro global en vez de repetirlo en cada uno.

(b) Criterio 3 — que los 5 verbos pasen por procedimientos almacenados.
    NO lo hagas todavía: solo dame un estimado de cuántas tablas (listamos
    más de 30) y cuánto trabajo implica, y si tiene sentido hacerlo
    completo o conversarlo primero con el profesor dado que v1-v5 ya
    están cerradas y tageadas.

No toques nada de v1 a v5 todavía — esto es solo para decidir el plan.
```

---

## 5. Archivos de esta sesión

- `v6_backend_completo.patch` — aplicable con `git apply` sobre el commit
  en el que estaba el `.zip` subido.
- `proyecto_catedras_v6_avance.zip` — el proyecto completo con los cambios
  ya aplicados, por si prefieren revisarlo sin aplicar el patch.
