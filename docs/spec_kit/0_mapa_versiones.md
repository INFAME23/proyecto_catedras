# Mapa de versiones del proyecto

> **Documento permanente.** Ordena las 37 tablas de `bdcatedras` en
> versiones incrementales y dice qué se puede hacer al terminar cada una.
> Lo referencia la [constitución](../1_constitution.md); ante conflicto,
> la constitución gana. Cada versión tiene su propio spec kit (documentos
> 2 a 8) en su carpeta: `version1/`, `version2/`, …

---

## 1. Regla que ordena el mapa

La BD se crea **completa** desde la v1 (Artículo 5); lo que crece por
versiones es la **API**. Para que cada versión sea construible y probable
por sí sola, se aplica una sola regla:

> **Una versión solo puede agregar tablas cuyas foreign keys salientes
> apunten a tablas que ya existen en la API** (de versiones anteriores o
> de la propia versión).

Las 37 tablas se agruparon siguiendo el grafo de FK del script
`db/postgres/script_bdcatedras.sql`. Las autorreferencias (`facultad` →
`facultad`, `dependencia` → `dependencia`, que modelan jerarquías) no
afectan el orden.

## 2. Versiones de la API

| Versión | Tablas nuevas | Depende de (FK salientes) | Qué se puede hacer al terminar |
|---|---|---|---|
| **V1** | Las 10 sin FK: `sede`, `encuesta`, `estado_proceso`, `modalidad`, `periodo_academico`, `rol`, `tipo_documento`, `tipo_evento`, `tipo_pregunta`, `tipo_vinculacion` | — | Catálogos base con CRUD completo y la arquitectura en capas validada |
| **V2** (5) | `facultad`, `dependencia`, `programa_academico`, `alias_programa`, `catedra` | `facultad` ↺, `dependencia` ↺ · `programa_academico` → `facultad` · `alias_programa` → `programa_academico` · `catedra` → `dependencia`, `tipo_evento` (V1) | Estructura académica y **catálogo de cátedras** |
| **V3** (7) | `lote_carga_asistente`, `asistente`, `novedad_carga`, `documento_asistente`, `vinculacion_asistente`, `programa_asistente`, `consentimiento_datos` | `asistente` → `lote_carga_asistente` · `novedad_carga` → `lote_carga_asistente` · `documento_asistente` → `asistente`, `tipo_documento` (V1) · `vinculacion_asistente` → `asistente`, `tipo_vinculacion` (V1) · `programa_asistente` → `asistente`, `programa_academico` (V2), `periodo_academico` (V1) · `consentimiento_datos` → `asistente` | Personas y carga de asistentes |
| **V4** (2) | `sesion`, `ponencia` | `sesion` → `catedra` (V2), `encuesta`, `modalidad`, `periodo_academico`, `sede` (V1) · `ponencia` → `sesion`, `asistente` (V3) | Sesiones, calendario y ponentes |
| **V5** (5) | `usuario`, `rol_por_usuario`, `enlace_registro`, `clave_acceso`, `registro_asistencia` | `usuario` → `asistente` (V3) · `rol_por_usuario` → `usuario`, `rol` (V1) · `enlace_registro` → `sesion` (V4), `usuario` · `clave_acceso` → `asistente`, `enlace_registro` · `registro_asistencia` → `asistente`, `sesion`, `clave_acceso`, `enlace_registro`, `programa_academico`, `tipo_vinculacion` | Login real, QR o enlace y registro de asistencia |
| **V6** (4) | `pregunta`, `opcion_pregunta`, `respuesta_encuesta`, `respuesta_item` | `pregunta` → `encuesta`, `tipo_pregunta` (V1) · `opcion_pregunta` → `pregunta` · `respuesta_encuesta` → `encuesta`, `registro_asistencia` (V5) · `respuesta_item` → `respuesta_encuesta`, `pregunta`, `opcion_pregunta` | Encuestas de calidad |
| **V7** (4) | `lote_migracion`, `detalle_migracion`, `bitacora`, `parametro` | `lote_migracion` → `estado_proceso` (V1), `sesion`, `usuario` · `detalle_migracion` → `lote_migracion`, `registro_asistencia` · `bitacora` → `usuario` · `parametro` → `usuario` | Exportar a ASIS, auditoría y parámetros |

Cuenta de control: 10 + 5 + 7 + 2 + 5 + 4 + 4 = **37 tablas**, ninguna
repetida y ninguna omitida.

### Estado de la V1

El código de las 10 tablas ya existe. Para **cerrar** la V1 falta:
actualizar sus documentos (`version1/`) de "parcial" a "completa", marcar
la Fase 12 de su `8_tasks.md` como hecha, y hacer el commit con el tag
`v1` (Artículo 1: solo entonces se escribe la spec siguiente).

## 3. Salvedad: la FK circular con `usuario`

`lote_carga_asistente` (V3) y `enlace_registro` (V5) tienen una FK a
`usuario`, pero `usuario` llega en la **V5**. Además, `usuario` depende de
`asistente` (V3), así que no se puede adelantar.

Solución hasta la V5:

- El campo (`fk_usuario` en `lote_carga_asistente`) se expone en la API
  como un **número opcional**, sin tabla `usuario` detrás.
- Las FK son `ON DELETE SET NULL`, es decir, la columna acepta `NULL`. En
  la práctica, hasta la V5 el campo se envía **vacío**.
- La BD sigue validando la FK: un id que no exista en `usuario` lo
  rechaza el motor, así que no hay que tocar el esquema.
- En la V5, `usuario` existe y el campo pasa a poblarse con el usuario
  autenticado.

## 4. Cómo evoluciona el front

El front (`front_catedras/`, Flask) no puede llegar a su forma final de
golpe: cada pantalla de la plantilla depende de tablas que llegan en
versiones distintas. Crece junto con la API.

| Versión | Front |
|---|---|
| **V1** | CRUD de administración de las 10 tablas base (estado actual) |
| **V2** | Diseño de la plantilla (base, navbar, CSS, footer) sobre `front_catedras/`. Catálogo de cátedras real (`catedras.html`, `detalle_catedra.html`). Se mantiene el CRUD de administración |
| **V3** | Carga de datos (`carga_datos`) y listado de asistentes |
| **V4** | Sesiones, detalle de sesión y calendario del ponente |
| **V5** | Login real (reemplaza el selector de rol de prueba), registro con token y paneles por rol |
| **V6** | Encuestas y dashboards con datos reales |
| **V7** | Exportar ASIS y parámetros |

### Correcciones a la plantilla que se hacen ya en la V2

- Llenar `auth_helpers.py`, que está vacío.
- Arreglar los endpoints del navbar que no existen.
- Cambiar el puerto `5001` por `8142` (Artículo 8).
- Quitar el respaldo automático a datos *mock*: oculta los errores de la
  API y contradice el objetivo didáctico.

## 5. Supuestos del mapa

1. El diseño de la plantilla **reemplaza** el estilo actual del front; el
   CRUD de administración se **conserva**.
2. El rol permanece **simulado** (selector de prueba) hasta la V5.
3. `bitacora` es de **solo lectura** en la API (V7): la llenan los
   triggers de la BD.

## 6. Reglas de uso del mapa

- El mapa se actualiza solo si cambia el grafo de FK o el alcance de una
  versión; no se edita por comodidad de una versión en curso.
- Una spec de versión puede nombrar únicamente las tablas de su fila y las
  de versiones anteriores (Artículos 1 y 5 de la constitución).
- Cada versión termina con sus criterios de aceptación, commit y tag
  (`v1`, `v2`, …) antes de escribir la siguiente.
