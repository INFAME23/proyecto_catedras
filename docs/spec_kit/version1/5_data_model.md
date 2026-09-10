# Modelo de datos — Versión 1: la BD completa (dada) y las tablas sin FK

> **Versión 1** · La base de datos NO se diseña en esta versión: **viene
> dada** (script SQL provisto). Este documento describe lo que hay y lo
> único que la v1 puede tocar.
>
> **Nota de alcance:** el dominio real de `bdcatedras` es un sistema de
> **asistencia a cátedras universitarias** (facultades, programas,
> sesiones, asistentes, encuestas). Todo el contenido de este documento
> — columnas, reglas, datos de ejemplo — corresponde a las tablas
> **reales** definidas en el script. Documenta las **10 tablas sin
> foreign key saliente**: 4 ya construidas (`sede`, `encuesta`,
> `estado_proceso`, `modalidad`) y 6 **especificadas para el próximo
> commit** (`periodo_academico`, `rol`, `tipo_documento`, `tipo_evento`,
> `tipo_pregunta`, `tipo_vinculacion`) — marcadas explícitamente como
> pendientes.

---

## 1. La base de datos `bdcatedras` (dada, completa)

El script **provisto** `script_bdcatedras.sql` (dialecto PostgreSQL) crea
el esquema **`catedras`** dentro de la base `bdcatedras`. PostgreSQL lo
ejecuta SOLO la PRIMERA vez (está pensado para montarse en
`/docker-entrypoint-initdb.d/` y correr cuando el volumen de datos nace
vacío).

**37 tablas** agrupadas en seis módulos:

```
NÚCLEO ACADÉMICO                        ASISTENTES Y VINCULACIÓN
facultad ── dependencia                 asistente ── documento_asistente
   ↑                                       ↑            ↑
programa_academico ── alias_programa      tipo_documento│
   ↑                                       │        vinculacion_asistente
catedra ── periodo_academico               │            ↑
   ↑            ↑                          │      tipo_vinculacion
modalidad ── sesion ── Sede                programa_asistente
                ↑                          consentimiento_datos
          registro_asistencia

REGISTRO Y ACCESO                        ENCUESTAS
registro_asistencia ── estado_proceso    encuesta ── pregunta ── tipo_pregunta
clave_acceso                                              ↑
enlace_registro                                     opcion_pregunta
                                          respuesta_encuesta ── respuesta_item

CARGA / MIGRACIÓN                        SEGURIDAD Y AUDITORÍA
lote_carga_asistente ── novedad_carga    usuario ── rol_por_usuario ── rol
lote_migracion ── detalle_migracion      bitacora
                                          parametro
OTROS
ponencia
```

Además: **funciones y triggers** (`fn_trg_bitacora`, `fn_trg_sesion_consecutivo`,
`fn_trg_registro_validar`, `fn_trg_clave_derivar`, `fn_trg_enlace_canal`,
`fn_trg_respuesta_item_tipo`, entre otras) y **funciones de consulta/API**
(`fn_resolver_asistente`, `fn_exportar_asis`, `fn_generar_lote_api`, etc.) —
todas esperando a las versiones siguientes.

**Datos de ejemplo:** 3 Sedes (`SAN_BENITO`, `BELLO`, `VIRTUAL`), más
facultades, programas, cátedras, sesiones, asistentes y encuestas de
ejemplo repartidos en las otras 36 tablas.

## 2. Lo que esta versión puede nombrar: las tablas SIN foreign key saliente

La API crece, dentro de esta misma versión, tabla por tabla — pero solo
para las que **no tienen ninguna foreign key saliente** en el esquema
(mismo nivel de complejidad que `sede`: nada de relaciones que resolver).
De las 10 tablas de la BD en esa condición, **4 ya están construidas**
(§2.1-2.4: `sede`, `encuesta`, `estado_proceso`, `modalidad`) y **6 están
especificadas aquí mismo pero su código todavía no existe** (§2.5-2.10:
`periodo_academico`, `rol`, `tipo_documento`, `tipo_evento`,
`tipo_pregunta`, `tipo_vinculacion`) — quedan documentadas para que el
próximo commit de esta versión las construya con la misma guía que ya
sirvió para las 4 primeras.

### 2.1 `sede`

> Comentario del propio esquema (`COMMENT ON TABLE catedras.sede`):
> *"Lugar físico o virtual donde se dicta una sesión [...] LA SEDE ES LO
> QUE DISTINGUE DOS CÁTEDRAS A LA MISMA HORA. Como texto libre en sesión,
> el informe por sede no se puede escribir."*

| Columna | Tipo (PostgreSQL) | Regla |
|---|---|---|
| `id_sede` | `VARCHAR(15)` | **PK** — texto de 1 a 15 caracteres |
| `nombre` | `VARCHAR(80)` | No nulo, no vacío, **único** (`uq_sede_nombre`) |
| `direccion` | `VARCHAR(200)` | Opcional (nulo permitido) |
| `es_virtual` | `BOOLEAN` | No nulo, `DEFAULT false` |
| `activo` | `BOOLEAN` | No nulo, `DEFAULT true` |

En C#, esa fila viaja como el modelo entidad:

```csharp
public class Sede
{
    public required string IdSede { get; set; }
    public required string Nombre { get; set; }
    public string? Direccion { get; set; }
    public bool EsVirtual { get; set; } = false;
    public bool Activo { get; set; } = true;
}
```

**Relación saliente (fuera del alcance de esta versión, pero presente en
la BD):** `sesion.fk_sede → sede.id_sede`,
`ON UPDATE CASCADE ON DELETE RESTRICT`. Es decir, no se puede borrar una
sede que tenga sesiones asociadas — la BD lo rechaza aunque esta versión
no toque `sesion`.

### 2.2 `encuesta`

> Comentario del propio esquema (`COMMENT ON TABLE catedras.encuesta`):
> *"Cuestionario de calidad en una version concreta. Se versiona porque
> con columnas fijas, cambiar una pregunta rompería la comparabilidad
> histórica: las respuestas viejas quedarían bajo un enunciado que no es
> el que se preguntó."*

| Columna | Tipo (PostgreSQL) | Regla |
|---|---|---|
| `id_encuesta` | `BIGINT` | **PK**, `GENERATED ALWAYS AS IDENTITY` — **la BD lo genera**, el cliente nunca lo manda |
| `nombre` | `VARCHAR(120)` | No nulo |
| `version` | `SMALLINT` | No nulo, `DEFAULT 1`, `CHECK (version >= 1)` |
| `vigente_desde` | `DATE` | No nulo, `DEFAULT CURRENT_DATE` |
| `vigente_hasta` | `DATE` | Opcional (nulo permitido) |
| `activa` | `BOOLEAN` | No nulo, `DEFAULT true` |

Restricciones adicionales:
- `uq_encuesta_ver` — **único** por `(nombre, version)`: la misma
  encuesta puede repetir nombre en otra versión, pero no la misma
  versión dos veces.
- `chk_encuesta_vig` — `vigente_hasta IS NULL OR vigente_hasta >=
  vigente_desde`: no puede vencer antes de empezar.

```csharp
public class Encuesta
{
    public long IdEncuesta { get; set; }      // lo pone la BD, no 'required'
    public required string Nombre { get; set; }
    public short Version { get; set; } = 1;
    public DateOnly VigenteDesde { get; set; }
    public DateOnly? VigenteHasta { get; set; }
    public bool Activa { get; set; } = true;
}
```

**Sin relación saliente ni entrante activa en esta versión:**
`sesion.fk_encuesta` y `pregunta.fk_encuesta` referencian `encuesta`,
pero `sesion`/`pregunta` son de otra versión — hoy nada bloquea un
`DELETE` sobre una encuesta.

### 2.3 `estado_proceso`

> Comentario del propio esquema: *"Los cinco estados del Monitor Procesos
> del ASIS, más GENERADO para el momento en que el archivo existe pero
> todavía no se ha subido."*

| Columna | Tipo (PostgreSQL) | Regla |
|---|---|---|
| `id_estado` | `VARCHAR(12)` | **PK** — texto de 1 a 12 caracteres |
| `nombre` | `VARCHAR(40)` | No nulo |
| `es_final` | `BOOLEAN` | No nulo, `DEFAULT false` |
| `es_exitoso` | `BOOLEAN` | No nulo, `DEFAULT false` |

Restricción adicional:
- `chk_estado_coherente` — `es_exitoso = false OR es_final = true`: no
  puede ser "exitoso" sin ser también "final".

```csharp
public class EstadoProceso
{
    public required string IdEstado { get; set; }
    public required string Nombre { get; set; }
    public bool EsFinal { get; set; } = false;
    public bool EsExitoso { get; set; } = false;
}
```

**Relación entrante (fuera del alcance de esta versión, pero presente en
la BD):** `lote_migracion.fk_estado_proceso → estado_proceso.id_estado`,
`ON UPDATE CASCADE ON DELETE RESTRICT`.

### 2.4 `modalidad`

| Columna | Tipo (PostgreSQL) | Regla |
|---|---|---|
| `id_modalidad` | `VARCHAR(15)` | **PK** — texto de 1 a 15 caracteres |
| `nombre` | `VARCHAR(60)` | No nulo |
| `canal_difusion` | `VARCHAR(10)` | No nulo, **solo** `QR`, `ENLACE` o `AMBOS` |
| `activo` | `BOOLEAN` | No nulo, `DEFAULT true` |

Restricción adicional:
- `chk_modalidad_canal` — `canal_difusion IN ('QR', 'ENLACE', 'AMBOS')`.
- Comentario de la columna `canal_difusion`: *"QR si es presencial, ENLACE
  si es virtual [...] cuando es virtual enviamos el enlace; si es
  presencial, el QR."*

```csharp
public class Modalidad
{
    public required string IdModalidad { get; set; }
    public required string Nombre { get; set; }
    public required string CanalDifusion { get; set; }  // "QR" | "ENLACE" | "AMBOS"
    public bool Activo { get; set; } = true;
}
```

**Relación entrante (fuera del alcance de esta versión, pero presente en
la BD):** `sesion.fk_modalidad → modalidad.id_modalidad`,
`ON UPDATE CASCADE ON DELETE RESTRICT`.

---

> ## ⏳ Pendientes de implementación (próximo commit de v1)
>
> Las 6 tablas de §2.5 a §2.10 completan las 10 tablas sin FK saliente.
> Están documentadas con el mismo nivel de detalle que las 4 anteriores
> —columnas, restricciones, comentario del esquema, modelo C# propuesto—
> pero **su código (Modelos/Peticiones/Repositorios/Servicios/Controllers)
> todavía no existe**. Los requisitos funcionales exactos (RF26-RF61)
> están en [2_spec.md](2_spec.md).

### 2.5 `periodo_academico` ⏳

> Comentario del propio esquema: *"Periodo academico: 2026-1, 2026-2. Lo
> pidió el profesor Hugo Nelson por su nombre — 'cuántas personas
> externas han ingresado en 2026-2'."*

| Columna | Tipo (PostgreSQL) | Regla |
|---|---|---|
| `id_periodo` | `CHAR(6)` | **PK** — formato fijo `AAAA-N` (N∈{1,2,3}), `CHECK (id_periodo ~ '^[0-9]{4}-[1-3]$')` |
| `fecha_ini` | `DATE` | No nulo (sin `DEFAULT`) |
| `fecha_fin` | `DATE` | No nulo (sin `DEFAULT`) |
| `activo` | `BOOLEAN` | No nulo, `DEFAULT true` |

Restricciones adicionales:
- `chk_periodo_formato` — el `CHECK` de formato de arriba.
- `chk_periodo_fechas` — `fecha_fin > fecha_ini`.

```csharp
public class PeriodoAcademico
{
    public required string IdPeriodo { get; set; }
    public DateOnly FechaIni { get; set; }
    public DateOnly FechaFin { get; set; }
    public bool Activo { get; set; } = true;
}
```

**Relaciones entrantes (fuera del alcance de esta versión, pero
presentes en la BD):** `sesion.fk_periodo` y
`programa_asistente.fk_periodo` → `periodo_academico.id_periodo`,
ambas `ON UPDATE CASCADE ON DELETE RESTRICT`.

**Datos de ejemplo (4 filas):** `2025-1`, `2025-2`, `2026-1`, `2026-2`.

### 2.6 `rol` ⏳

Sin comentario propio en el esquema. Tabla simple, sin `CHECK` de
negocio.

| Columna | Tipo (PostgreSQL) | Regla |
|---|---|---|
| `id_rol` | `VARCHAR(20)` | **PK** — texto de 1 a 20 caracteres |
| `nombre` | `VARCHAR(60)` | No nulo, **único** (`uq_rol_nombre`) |
| `descripcion` | `VARCHAR(200)` | Opcional (nulo permitido) |

```csharp
public class Rol
{
    public required string IdRol { get; set; }
    public required string Nombre { get; set; }
    public string? Descripcion { get; set; }
}
```

**Relación entrante (fuera del alcance de esta versión, pero presente en
la BD):** `rol_por_usuario.fk_rol → rol.id_rol`,
`ON UPDATE CASCADE ON DELETE RESTRICT`.

**Datos de ejemplo (3 filas):** `ADMIN` (Administrador), `CONSULTA`
(Consulta), `COORDINADOR` (Coordinador de dependencia).

### 2.7 `tipo_documento` ⏳

> Comentario del propio esquema: *"Clase de documento de identidad.
> Catálogo cerrado que elimina las más de diez formas de escribir
> 'cédula' que trae el formulario actual."*

| Columna | Tipo (PostgreSQL) | Regla |
|---|---|---|
| `id_tipo_documento` | `VARCHAR(5)` | **PK** — texto de 1 a 5 caracteres |
| `nombre` | `VARCHAR(60)` | No nulo, **único** (`uq_tipo_documento_nom`) |
| `activo` | `BOOLEAN` | No nulo, `DEFAULT true` |

```csharp
public class TipoDocumento
{
    public required string IdTipoDocumento { get; set; }
    public required string Nombre { get; set; }
    public bool Activo { get; set; } = true;
}
```

**Relación entrante (fuera del alcance de esta versión, pero presente en
la BD):** `documento_asistente.fk_tipo_documento → tipo_documento.id_tipo_documento`,
`ON UPDATE CASCADE ON DELETE RESTRICT`.

**Datos de ejemplo (6 filas):** `CC`, `CE`, `NIT`, `PA`, `PEP`, `TI`.

### 2.8 `tipo_evento` ⏳

> Comentario del propio esquema: *"Clasificación del evento en el ASIS.
> Ocho valores observados en las 5.711 filas reales: CAAB (5.449), DLLH
> (188), DEPR (44)…"*

| Columna | Tipo (PostgreSQL) | Regla |
|---|---|---|
| `id_tipo_evento` | `CHAR(4)` | **PK** — longitud **fija** de 4 caracteres |
| `nombre` | `VARCHAR(80)` | No nulo — **sin** restricción de unicidad |
| `activo` | `BOOLEAN` | No nulo, `DEFAULT true` |

Sin `CHECK` propio. Nota: a diferencia de `sede`/`rol`/`tipo_documento`,
aquí **no existe** un `uq_tipo_evento_nom` — el nombre se puede repetir.

```csharp
public class TipoEvento
{
    public required string IdTipoEvento { get; set; }  // exactamente 4 caracteres
    public required string Nombre { get; set; }
    public bool Activo { get; set; } = true;
}
```

**Relación entrante (fuera del alcance de esta versión, pero presente en
la BD):** `catedra.fk_tipo_evento → tipo_evento.id_tipo_evento`,
`ON UPDATE CASCADE ON DELETE RESTRICT`.

**Datos de ejemplo (8 filas):** `ARCU`, `CAAB`, `DEPR`, `DIIB`, `DLLH`,
`EIIC`, `MTG`, `SLDI`.

### 2.9 `tipo_pregunta` ⏳

> Comentario del propio esquema: *"Clase de pregunta. Las tres columnas
> booleanas dicen en cuál de las tres columnas de valor de
> respuesta_item se guarda la respuesta. Es lo que permite que la regla
> condicional (RN-25) sea DATO y no código."*

| Columna | Tipo (PostgreSQL) | Regla |
|---|---|---|
| `id_tipo_pregunta` | `VARCHAR(15)` | **PK** — texto de 1 a 15 caracteres |
| `nombre` | `VARCHAR(60)` | No nulo — sin restricción de unicidad |
| `guarda_numero` | `BOOLEAN` | No nulo, `DEFAULT false` |
| `guarda_texto` | `BOOLEAN` | No nulo, `DEFAULT false` |
| `guarda_opcion` | `BOOLEAN` | No nulo, `DEFAULT false` |

Restricción adicional:
- `chk_tipo_pregunta_destino` — `guarda_numero::int + guarda_texto::int +
  guarda_opcion::int = 1`: **exactamente una** de las tres debe ser
  `true` (regla de coherencia de TRES campos, ver la nota de diseño en
  [2_spec.md](2_spec.md)).

```csharp
public class TipoPregunta
{
    public required string IdTipoPregunta { get; set; }
    public required string Nombre { get; set; }
    public bool GuardaNumero { get; set; } = false;
    public bool GuardaTexto { get; set; } = false;
    public bool GuardaOpcion { get; set; } = false;
}
```

**Relación entrante (fuera del alcance de esta versión, pero presente en
la BD):** `pregunta.fk_tipo_pregunta → tipo_pregunta.id_tipo_pregunta`,
`ON UPDATE CASCADE ON DELETE RESTRICT`.

**Datos de ejemplo (4 filas):** `ESCALA_5` (guarda_numero),
`OPCION_UNICA` (guarda_opcion), `SI_NO` (guarda_numero), `TEXTO_LIBRE`
(guarda_texto).

### 2.10 `tipo_vinculacion` ⏳

> Comentario del propio esquema: *"RUTAS_PAZ tiene exige_programa = false
> por la excepción que planteó el profesor Hugo Nelson: 'ellos entrarían
> sin programa, pero solo ellos'. ADMINISTRATIVO también, porque no está
> matriculado en nada."*

| Columna | Tipo (PostgreSQL) | Regla |
|---|---|---|
| `id_tipo_vinculacion` | `VARCHAR(15)` | **PK** — texto de 1 a 15 caracteres |
| `nombre` | `VARCHAR(60)` | No nulo, **único** (`uq_tipo_vinculacion_nom`) |
| `es_interno` | `BOOLEAN` | No nulo, **sin `DEFAULT`** — obligatorio en el POST |
| `exige_programa` | `BOOLEAN` | No nulo, `DEFAULT true` |
| `activo` | `BOOLEAN` | No nulo, `DEFAULT true` |

```csharp
public class TipoVinculacion
{
    public required string IdTipoVinculacion { get; set; }
    public required string Nombre { get; set; }
    public bool EsInterno { get; set; }           // sin '= false': no tiene default en la BD
    public bool ExigePrograma { get; set; } = true;
    public bool Activo { get; set; } = true;
}
```

**Relaciones entrantes (fuera del alcance de esta versión, pero
presentes en la BD):** `registro_asistencia.fk_vinculacion_snapshot` y
`vinculacion_asistente.fk_tipo_vinculacion` →
`tipo_vinculacion.id_tipo_vinculacion`, ambas
`ON UPDATE CASCADE ON DELETE RESTRICT`.

**Datos de ejemplo (7 filas):** `ADMINISTRATIVO`, `COLEGIO`, `DOCENTE`,
`EGRESADO`, `ESTUDIANTE`, `EXTERNO`, `RUTAS_PAZ`.

## 3. Las dos murallas de validación

1. **La API**:
   - Las peticiones por verbo con anotaciones: forma, tipos y rangos →
     422 con lista de errores, ANTES de tocar la BD (`id_sede` 1-15
     caracteres, `canal_difusion` en una lista cerrada, etc.).
   - El **servicio**, para reglas de negocio que cruzan dos campos y que
     una anotación de un solo campo no puede expresar (`ArgumentException`
     → 400): `Encuesta.vigente_hasta >= vigente_desde`,
     `EstadoProceso.es_exitoso` implica `es_final`. Se validan ahí cuando
     el servicio tiene ambos valores en la mano (POST, PUT, y PATCH si
     llegan los dos campos juntos).
2. **La BD** (PK, NOT NULL, UNIQUE, CHECK y FK): la última línea de
   defensa — un `id_sede` duplicado viola la PK, un `nombre` repetido
   viola `uq_sede_nombre`, un PATCH que solo cambia una fecha de
   `encuesta` y deja la combinación inválida viola `chk_encuesta_vig` —
   y el motor los rechaza aunque la API tuviera un bug o no duplicara la
   regla (la API lo reporta como 500 con el error del motor). Igualmente,
   si en una versión futura se intenta borrar una sede en uso,
   `fk_sesion_sede` la bloquea con `RESTRICT`.

## 4. Reglas de esta versión

- El **código** de esta versión (hoy) **solo nombra `sede`, `encuesta`,
  `estado_proceso` y `modalidad`**. `periodo_academico`, `rol`,
  `tipo_documento`, `tipo_evento`, `tipo_pregunta` y `tipo_vinculacion`
  ya están **documentadas** (§2.5-2.10) para el próximo commit, pero
  todavía no tienen Modelo/Petición/Repositorio/Servicio/Controller. Las
  27 tablas restantes (con FK saliente) siguen siendo territorio de
  versiones posteriores.
- La BD **no se modifica**: ni columnas nuevas, ni índices, ni datos
  semilla distintos. Si algo parece faltar, es de otra versión.
- Datos de ejemplo de las tablas construidas: `encuesta` trae **1** fila
  ("Calidad de la catedra abierta", versión 1); `estado_proceso` trae
  **6** (`GENERADO`, `EN_COLA`, `EN_CURSO`, `ERROR`, `CORRECTO`,
  `INCORRECTO`); `modalidad` trae **4** (`PRESENCIAL`, `VIRTUAL`,
  `TELEPRESENCIAL`, `HIBRIDA`). Los datos de ejemplo de las 6 tablas
  pendientes están en §2.5-2.10.
- El reset completo es de Docker, no de SQL:
  `docker compose down -v && docker compose up -d --build` (borra el
  volumen y el inicializador vuelve a crear todo).