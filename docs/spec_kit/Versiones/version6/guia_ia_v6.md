# Guía IA — Versión 6: encuestas de calidad

> **Propósito:** guía práctica para reconstruir, completar, revisar y verificar la v6 del Proyecto Cátedras con ayuda de una IA. No reemplaza la constitución ni el spec kit; traduce sus reglas a un flujo de trabajo verificable.
>
> **Estado inicial del ZIP (2026-10-14):** la implementación de backend de las fases 47–49 aparece escrita, pero la documentación de estado indica que no se compiló ni ejecutó en el entorno donde se generó. Siguen pendientes la prueba de capas (fase 50), la verificación en Docker (fase 51), el frontend (fase 52) y el cierre/tag (fase 53). **No consideres la v6 terminada hasta ejecutar y documentar estas comprobaciones.**
>
> **Jerarquía:** `docs/spec_kit/1_constitution.md` → `docs/spec_kit/guia_ia.md` → `docs/spec_kit/Versiones/0_mapa_versiones.md` → documentos `2_spec.md` a `8_tasks.md` de `version6/` → esta guía. Si hay contradicción, prevalecen los documentos de mayor jerarquía.

## 0. Cómo trabajar con esta guía

1. Abre una conversación con la IA y adjunta el repositorio o dale acceso a él.
2. Pega el prompt de arranque de §1.
3. Trabaja **una fase por turno**. Pide primero inspección y plan; después implementación; al final pruebas.
4. Ejecuta las comprobaciones en tu propia máquina, con Docker y PostgreSQL reales. No aceptes “pasa” si solo se leyó el código.
5. Actualiza `8_tasks.md` únicamente cuando hayas comprobado el resultado. Guarda los comandos ejecutados, su salida y cualquier limitación.
6. Si la IA encuentra contradicciones entre los requisitos del profesor (`VERSION_2.md`), la constitución y el patrón histórico del proyecto, debe detenerse y describirlas; no debe resolverlas silenciosamente.

## 1. Prompt de arranque (copiar y pegar)

```text
Trabajaremos en el Proyecto Cátedras, VERSIÓN 6: encuestas de calidad.
Es un proyecto de estudio. Responde en español y explica las decisiones
con comentarios claros. No inventes resultados de pruebas.

Antes de modificar archivos, lee en este orden:
1. docs/spec_kit/1_constitution.md
2. docs/spec_kit/guia_ia.md
3. docs/spec_kit/Versiones/0_mapa_versiones.md
4. docs/spec_kit/Versiones/version6/2_spec.md
5. docs/spec_kit/Versiones/version6/3_plan.md
6. docs/spec_kit/Versiones/version6/4_research.md
7. docs/spec_kit/Versiones/version6/5_data_model.md
8. docs/spec_kit/Versiones/version6/6_contracts.md
9. docs/spec_kit/Versiones/version6/7_quickstart.md
10. docs/spec_kit/Versiones/version6/8_tasks.md
11. docs/spec_kit/Versiones/version6/ESTADO_V6_Y_CUMPLIMIENTO.md
12. docs/spec_kit/Versiones/version6/guia_ia_v6.md

Después, resume en 8-10 puntos: alcance de v6, entidades, endpoints,
reglas de negocio, migración, pruebas pendientes y contradicciones
detectadas. No escribas código hasta que te confirme.

Reglas del alcance:
- La v6 agrega solo Pregunta, OpcionPregunta, RespuestaEncuesta y
  RespuestaItem a la API (33 recursos en total: 29 de v1-v5 + 4).
- No exponer ni implementar entidades de v7: LoteMigracion,
  DetalleMigracion, Bitacora y Parametro.
- Mantener C# / ASP.NET Core (.NET 10) / Dapper / PostgreSQL y la
  arquitectura Controller -> IServicio -> IRepositorio ->
  RepositorioXPostgres. No introducir Entity Framework ni paquetes nuevos
  sin autorización explícita en los documentos.
- Los nombres JSON de los cuerpos siguen los contratos exactos de
  6_contracts.md. No cambiar camelCase/snake_case por preferencia personal.
- Pregunta puede crearse con opciones iniciales en una sola transacción
  mediante catedras.sp_guardar_pregunta (migración 004).
- Responder una encuesta es una operación de negocio: POST
  /api/RespuestaEncuesta llama a fn_responder_encuesta_api. No crear
  CRUD de escritura para RespuestaEncuesta ni endpoints de escritura
  para RespuestaItem. No agregar una operación Cerrar separada.
- Validar valor_min/valor_max en ServicioPregunta: deben venir ambos o
  ninguno y valor_max debe ser mayor que valor_min.
- No dar por aprobada ninguna fase sin ejecutar su verificación.
- Si VERSION_2.md contradice el patrón histórico o la constitución,
  documenta el conflicto y solicita decisión; no refactorices todo el
  proyecto como parte de v6 sin autorización.
Trabajaremos fase por fase (47-53). No avances sin mi confirmación.
```

## 2. Qué leer y qué extraer

| Documento | Información que debe guiar la implementación |
|---|---|
| `1_constitution.md` | Reglas permanentes: versiones, capas, base de datos y contratos |
| `guia_ia.md` | Reglas transversales sobre SP, FK y seguridad |
| `Versiones/0_mapa_versiones.md` | Qué tablas pertenecen a v1-v7 y dependencias |
| `2_spec.md` | Requisitos RF159 en adelante, alcance y criterios de aceptación |
| `3_plan.md` | Estructura de archivos, capas y decisiones técnicas |
| `4_research.md` | ADR D30 en adelante y razones de las decisiones |
| `5_data_model.md` | Columnas, tipos, FK, restricciones y datos de prueba |
| `6_contracts.md` | Rutas, parámetros, cuerpos JSON, respuestas y códigos HTTP |
| `7_quickstart.md` | Arranque, smoke test de API y frontend |
| `8_tasks.md` | Fases 47–53 y evidencia exigida |
| `ESTADO_V6_Y_CUMPLIMIENTO.md` | Estado conocido y discrepancias contra los requisitos del curso |

## 3. La v6 en una página

La v6 completa el ciclo de calidad: una sesión ya tiene asistentes registrados (v5), y ahora esos asistentes pueden responder la encuesta asociada.

- **`Pregunta`**: CRUD; al crear, admite `opciones` iniciales opcionales.
- **`OpcionPregunta`**: CRUD completo; se puede corregir la etiqueta y el orden.
- **`RespuestaEncuesta`**: lectura y una operación de escritura `POST` para responder.
- **`RespuestaItem`**: solo lectura; sus filas nacen al responder.
- **Migración nueva:** `db/postgres/migraciones/004_sp_guardar_pregunta.sql`, para insertar la pregunta y sus opciones iniciales en una transacción.
- **Función preexistente:** `fn_responder_encuesta_api`, que encapsula el guardado de la respuesta y sus ítems y el cierre correspondiente.
- **No pertenece a v6:** migración de datos, bitácora y parámetros (v7).

## 4. Reglas y trampas importantes

1. **Maestro-detalle atómico:** al crear una pregunta con opciones, si una opción falla, la pregunta tampoco debe quedar guardada. Verifica esto en PostgreSQL real.
2. **Tipos de respuesta:** la API no debe duplicar las reglas que ya aplica el trigger de la base de datos al insertar `respuesta_item`; consulta los ADR y el spec antes de añadir validaciones.
3. **Preguntas obligatorias:** al responder, deben cumplirse las reglas de obligatoriedad definidas por la base de datos. Un rechazo debe dejar la transacción completa revertida.
4. **Sin edición de respuestas:** no añadir `PUT`, `PATCH` ni `DELETE` a `RespuestaEncuesta`; tampoco escritura directa de `RespuestaItem`.
5. **Rangos:** `valor_min` y `valor_max` se validan en el servicio al crear/reemplazar/actualizar una pregunta. Ambos nulos es válido; solo uno presente o `valor_max <= valor_min` no lo es.
6. **Errores HTTP:** los documentos actuales del proyecto pueden no coincidir con `VERSION_2.md` en códigos de error y borrado lógico. Antes de cambiar el comportamiento, identifica el requisito, cita el documento y pide una decisión explícita. No declares que v6 cumple el documento del profesor mientras haya discrepancias abiertas.
7. **Base de datos existente:** el script principal crea el esquema completo, pero un volumen Docker antiguo no ejecuta automáticamente una migración nueva. Aplica la migración 004 de forma controlada o recrea el volumen de desarrollo si puedes perder los datos.
8. **No confundir código con verificación:** archivos presentes o casillas marcadas en tareas no prueban que compile ni que funcione.

## 5. Flujo de trabajo por fases (47–53)

### Fase 47 — Migración `sp_guardar_pregunta`

Prompt:
```text
Revisa la fase 47 de version6/8_tasks.md y la sección 4.1 de
version6/3_plan.md. Inspecciona la migración 004 y docker-compose.yml.
Comprueba idempotencia, firma del procedimiento, JSONB, atomicidad y
orden de ejecución después del esquema principal y de las migraciones
001-003. No cambies código todavía: entrega hallazgos y comandos de prueba.
```

Verifica que el procedimiento se pueda crear/recrear sin error, que reciba las opciones iniciales esperadas y que una opción inválida revierta toda la operación.

### Fase 48 — `Pregunta` y `OpcionPregunta`

Prompt:
```text
Implementa o revisa solo la fase 48. Compara cada modelo, petición,
interfaz, repositorio, servicio y controlador contra 2_spec.md,
3_plan.md, 5_data_model.md y 6_contracts.md. Comprueba los 6 endpoints
de Pregunta y los 6 de OpcionPregunta, los códigos HTTP, los nombres JSON,
la validación de valor_min/valor_max y el uso del procedimiento 004 al
crear Pregunta. Ejecuta build y pruebas disponibles; informa exactamente
qué comandos ejecutaste y sus resultados.
```

### Fase 49 — Respuestas de encuesta

Prompt:
```text
Implementa o revisa solo la fase 49: RespuestaEncuesta y RespuestaItem.
RespuestaEncuesta debe ofrecer listado, detalle y POST para responder;
RespuestaItem solo lectura. Confirma que ResponderAsync llama a
fn_responder_encuesta_api y que no existe CerrarAsync ni CRUD de escritura
genérico para las respuestas. Prueba respuesta correcta, duplicada,
pregunta obligatoria omitida y rollback completo según 6_contracts.md
y 7_quickstart.md.
```

### Fase 50 — Prueba de capas

Prompt:
```text
Ejecuta la fase 50 y revisa la prueba de capas. Debe cubrir las 33
entidades y las nuevas validaciones de Pregunta, además de los contratos
de RespuestaEncuesta/RespuestaItem. No sustituyas repositorios reales
por mocks en la prueba de integración HTTP; distingue claramente prueba
unitaria/de capas de prueba contra Docker. Reporta conteos reales, fallos
y comando de salida.
```

### Fase 51 — Verificación con Docker

Prompt:
```text
Sigue version6/7_quickstart.md y ejecuta la fase 51 con PostgreSQL y la
API reales en Docker. Verifica Swagger, GET de preguntas, creación de
Pregunta con opciones, rollback si falla una opción, respuesta válida,
respuesta duplicada, obligatorias faltantes y restricción al borrar una
pregunta respondida. No inventes datos ni resultados: registra solicitud,
respuesta HTTP y resultado esperado/obtenido. Indica si tu entorno tenía
un volumen antiguo y cómo aplicaste 004.
```

### Fase 52 — Frontend Flask

Prompt:
```text
Implementa el frontend de la fase 52 de acuerdo con los contratos de v6.
Agrega el formulario para responder la encuesta desde la sesión y la
administración de preguntas/opciones. Los controles deben variar según
el tipo de pregunta, respetar las obligatorias y enviar el JSON exacto
del contrato. Reutiliza los patrones visuales y de autenticación existentes;
no inventes endpoints. Comprueba manualmente los estados de éxito y error
y documenta rutas y pasos de prueba.
```

### Fase 53 — Cierre

Prompt:
```text
Haz una auditoría final de la fase 53. Revisa criterios de aceptación,
diff, migración 004, README, guía y 8_tasks.md. Comprueba que las fases
47-52 tengan evidencia real, que no se haya incluido alcance de v7 y que
los documentos indiquen con honestidad lo que se verificó. No crees el
tag v6 ni marques la versión como cerrada hasta que yo confirme todas
las pruebas y la revisión de los conflictos contra VERSION_2.md.
```

## 6. Lista de comprobación para el estudiante

- [ ] La IA leyó todos los documentos indicados y resumió los límites de v6.
- [ ] Se revisó `004_sp_guardar_pregunta.sql` y su orden de ejecución.
- [ ] `dotnet build` termina correctamente.
- [ ] La prueba de capas termina con código 0 y se registró su conteo real.
- [ ] Docker arranca con un volumen limpio y también se documentó el caso de volumen previo.
- [ ] Los endpoints y códigos HTTP coinciden con `6_contracts.md`.
- [ ] Una creación pregunta+opciones fallida no deja datos parciales.
- [ ] Una respuesta fallida no deja maestro ni detalles parciales.
- [ ] El frontend permite responder y administrar preguntas/opciones.
- [ ] Se documentaron los conflictos con `VERSION_2.md` y su decisión.
- [ ] `8_tasks.md` y README reflejan el estado real.
- [ ] El tag `v6` solo se creó después de la aprobación humana.

## 7. Conflictos conocidos que deben tratarse explícitamente

El documento `ESTADO_V6_Y_CUMPLIMIENTO.md` señala discrepancias estructurales entre `VERSION_2.md` y el patrón heredado del repositorio, especialmente:

- uso de SQL directo en repositorios en lugar de procedimientos almacenados para todos los verbos;
- respuestas `500` para ciertos errores de integridad donde el documento del curso exige `409`;
- borrado físico en entidades con columna `activo`, en vez de borrado lógico y filtros de inactivos.

No asumas que estos hallazgos están resueltos. Antes de refactorizar, separa: (a) qué exige la evaluación, (b) qué define la constitución actual, (c) qué implicaría cambiar versiones anteriores y (d) qué decisión debe tomar el responsable del proyecto. Registra la resolución en la documentación pertinente y prueba regresión de v1-v5 si se aprueba un cambio transversal.

---

**Regla final:** la IA ayuda a construir y revisar; la evidencia de que funciona debe salir de comandos y pruebas ejecutados sobre el proyecto real.
