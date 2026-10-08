# Modelo de datos — Versión 3: personas y carga de asistentes

> ✅ **Construida y verificada (2026-10-07)** — ver nota de [2_spec.md](2_spec.md); correcciones al borrador en el ADR D19 de [4_research.md](4_research.md).

## 1. Las 7 tablas de esta versión y su cadena de FK

```
lote_carga_asistente (↺ fk_usuario -> usuario, v5 — SIEMPRE NULL en v3, §4)
   ├──< asistente (fk_lote_carga, ON DELETE SET NULL)
   └──< novedad_carga (fk_lote_carga, ON DELETE CASCADE)

asistente
   ├──< documento_asistente (ON DELETE CASCADE) ──> tipo_documento (v1, RESTRICT)
   ├──< vinculacion_asistente (ON DELETE CASCADE) ──> tipo_vinculacion (v1, RESTRICT)
   ├──< programa_asistente (ON DELETE CASCADE) ──> programa_academico (v2, RESTRICT)
   │                                           ──> periodo_academico (v1, RESTRICT)
   └──< consentimiento_datos (ON DELETE CASCADE)

-- FK ENTRANTES de versiones futuras, YA SEMBRADAS (relevantes para DELETE):
asistente <── usuario (v5, RESTRICT) · ponencia (v4, RESTRICT) · registro_asistencia (v5, RESTRICT)
```

## 2. Las 7 tablas

### 2.1 `lote_carga_asistente`

> Comentario del esquema: cada ejecución de la carga del archivo de
> personas del ASIS; los 3 contadores son la evidencia de qué se
> aceptó y qué no.

| Columna | Tipo | Regla |
|---|---|---|
| `id_lote_carga` | `BIGINT` | **PK**, `IDENTITY` |
| `nombre_archivo` | `VARCHAR(200)` | No nulo |
| `cargado_en` | `TIMESTAMPTZ` | No nulo, `DEFAULT now()` — de solo lectura |
| `fk_usuario` | `BIGINT` | Opcional — FK a `usuario.id_usuario` (v5). **Siempre `NULL` en v3** (§4 de este documento) |
| `filas_leidas` / `filas_aceptadas` / `filas_rechazadas` | `INTEGER` | No nulos, `DEFAULT 0` |
| `observaciones` | `TEXT` | Opcional |

Restricción: `chk_lote_carga_filas` —
`filas_leidas >= 0 AND filas_aceptadas >= 0 AND filas_rechazadas >= 0
AND (filas_aceptadas + filas_rechazadas) <= filas_leidas`.

```csharp
public class LoteCargaAsistente
{
    public long IdLoteCarga { get; set; }             // lo pone la BD
    public required string NombreArchivo { get; set; }
    public DateTime CargadoEn { get; set; }            // lo pone la BD
    public long? FkUsuario { get; set; }                // siempre null hasta v5
    public int FilasLeidas { get; set; }
    public int FilasAceptadas { get; set; }
    public int FilasRechazadas { get; set; }
    public string? Observaciones { get; set; }
    public bool EstaCerrado =>                          // calculada, no es columna
        FilasLeidas > 0 || FilasAceptadas > 0 || FilasRechazadas > 0 || Observaciones is not null;
}
```

**"Ya cerrado" no es una columna explícita:** se infiere por
convención — un lote recién abierto tiene los 3 contadores en 0 y
`observaciones` nulo; el servicio (`ServicioLoteCargaAsistente.CerrarAsync`)
rechaza (400) cerrar un lote con algún contador mayor que 0 u
`observaciones` con valor (propiedad calculada `EstaCerrado` del modelo,
que la API devuelve como `"cerrado"`). **(corregido: el borrador solo
miraba `filas_leidas`; ver ADR D19.)**

### 2.2 `asistente`

> Comentario del esquema: persona que asiste a las cátedras abiertas —
> estudiante, docente, administrativo, egresado, de rutas de formación
> o público externo.

| Columna | Tipo | Regla |
|---|---|---|
| `id_asistente` | `BIGINT` | **PK**, `IDENTITY` |
| `id_asis` | `VARCHAR(15)` | Opcional, **único** — `CHECK ~ '^[0-9A-Za-z]{5,15}$'` |
| `nombres` / `apellidos` | `VARCHAR(100)` | No nulos |
| `nombre_completo_asis` | `VARCHAR(200)` | Opcional — el nombre tal como llegó del ASIS, sin transformar (permite rehacer la partición en nombres/apellidos si resultó mal hecha) |
| `correo_institucional` / `correo_personal` | `CITEXT` | Opcionales — al menos uno de los dos (`chk_asistente_correo`); formato `%@%.%` |
| `celular` | `VARCHAR(20)` | Opcional |
| `es_externo` | `BOOLEAN` | No nulo, `DEFAULT false` |
| `activo` | `BOOLEAN` | No nulo, `DEFAULT true` |
| `creado_en` | `TIMESTAMPTZ` | No nulo, `DEFAULT now()` — de solo lectura |
| `fk_lote_carga` | `BIGINT` | Opcional — FK a `lote_carga_asistente.id_lote_carga`, `ON DELETE SET NULL` |

Restricción adicional: `chk_asistente_externo` —
`(es_externo = false AND id_asis IS NOT NULL) OR es_externo = true`.
Un externo PUEDE tener `id_asis` (no son excluyentes). **(corregido)** En
`POST`/`PUT` el servicio valida este cruce y "al menos un correo" (→ 400,
RF101, ADR D19); el `CHECK` queda como última muralla (p. ej. en `PATCH`).

```csharp
public class Asistente
{
    public long IdAsistente { get; set; }               // lo pone la BD
    public string? IdAsis { get; set; }
    public required string Nombres { get; set; }
    public required string Apellidos { get; set; }
    public string? NombreCompletoAsis { get; set; }
    public string? CorreoInstitucional { get; set; }
    public string? CorreoPersonal { get; set; }
    public string? Celular { get; set; }
    public bool EsExterno { get; set; } = false;
    public bool Activo { get; set; } = true;
    public DateTime CreadoEn { get; set; }               // lo pone la BD
    public long? FkLoteCarga { get; set; }
}
```

**FK entrantes relevantes para `DELETE` (RF100):**
| Tabla (versión) | Regla |
|---|---|
| `documento_asistente`, `vinculacion_asistente`, `programa_asistente`, `consentimiento_datos` (v3, estas mismas) | `ON DELETE CASCADE` |
| `clave_acceso` (v5, ya sembrada: 167 filas) | `ON DELETE CASCADE` |
| `usuario` (v5, ya sembrada: 1 fila) | `ON DELETE RESTRICT` |
| `ponencia` (v4, aún sin datos) | `ON DELETE RESTRICT` |
| `registro_asistencia` (v5, **ya sembrada: 168 filas** — corregido) | `ON DELETE RESTRICT` |

Como en v2, `RESTRICT` gana sobre `CASCADE` en la misma sentencia: si
el asistente tiene `usuario` o `registro_asistencia` (p. ej. el asistente
1), su `DELETE` falla con 500 **antes** de que la cascada hacia sus 4
detalles llegue a ejecutarse.

#### 2.2.1 Maestro-detalle con 4 tablas (retrofit del mismo día, Guía IA · Regla 1)

`asistente` es el maestro; `documento_asistente`, `vinculacion_asistente`,
`programa_asistente` y `consentimiento_datos` son sus 4 detalles. El
`CALL` recibe hasta 4 `jsonb` opcionales:

```sql
-- db/postgres/migraciones/002_sp_guardar_asistente.sql
CREATE OR REPLACE PROCEDURE catedras.sp_guardar_asistente(
    IN p_id_asis       varchar(15),
    IN p_nombres       varchar(100),
    IN p_apellidos     varchar(100),
    IN p_nombre_completo_asis varchar(200) DEFAULT NULL,  -- (agregado al construir, D19)
    IN p_correo_inst   varchar(254) DEFAULT NULL,  -- texto; al insertar se convierte a citext
    IN p_correo_pers   varchar(254) DEFAULT NULL,
    IN p_celular       varchar(20) DEFAULT NULL,
    IN p_es_externo    boolean DEFAULT false,
    IN p_activo        boolean DEFAULT true,
    IN p_fk_lote_carga bigint DEFAULT NULL,
    IN p_documentos    jsonb DEFAULT NULL,   -- [{fk_tipo_documento, numero, vigente}]
    IN p_vinculaciones jsonb DEFAULT NULL,   -- [{fk_tipo_vinculacion, fecha_ini}]
    IN p_programas     jsonb DEFAULT NULL,   -- [{fk_programa, fk_periodo, estado}]
    IN p_consentimiento jsonb DEFAULT NULL,  -- {version_politica, canal, ip} (un objeto, no arreglo)
    INOUT p_id_asistente bigint DEFAULT NULL
)
```

Cualquier fallo en los 4 `INSERT ... SELECT` revierte también el
`INSERT` del asistente (igual criterio que `sp_guardar_programa_academico`
de v2).

### 2.3 `documento_asistente`

> Comentario del esquema: número de documento de un asistente; una
> persona puede tener varios a lo largo del tiempo, solo uno vigente.

| Columna | Tipo | Regla |
|---|---|---|
| `id_documento` | `BIGINT` | **PK**, `IDENTITY` |
| `fk_asistente` | `BIGINT` | No nulo — FK a `asistente`, `CASCADE` |
| `fk_tipo_documento` | `VARCHAR(5)` | No nulo — FK a `tipo_documento` (v1), `RESTRICT` |
| `numero` | `VARCHAR(20)` | No nulo — `CHECK ~ '^[0-9A-Za-z-]{4,20}$'`; único junto con `fk_tipo_documento` (`uq_documento_numero`) |
| `vigente` | `BOOLEAN` | No nulo, `DEFAULT true` |
| `registrado_en` | `TIMESTAMPTZ` | No nulo, `DEFAULT now()` |

**Índice único parcial — la regla más importante de esta tabla:**
`ux_documento_vigente` sobre `fk_asistente` **`WHERE vigente`** — un
asistente no puede tener dos documentos `vigente: true` al mismo
tiempo. No hay `CHECK` que lo exprese como restricción de una sola
fila (depende de las demás filas del mismo asistente), así que es un
índice único parcial — Postgres lo aplica igual que una `UNIQUE` normal
(RF103: se deja fallar, D9).

```csharp
public class DocumentoAsistente
{
    public long IdDocumento { get; set; }              // lo pone la BD
    public required long FkAsistente { get; set; }
    public required string FkTipoDocumento { get; set; }
    public required string Numero { get; set; }
    public bool Vigente { get; set; } = true;
    public DateTime RegistradoEn { get; set; }           // lo pone la BD
}
```

### 2.4 `vinculacion_asistente` — llave compuesta, sin `id` (ADR D16)

> Comentario del esquema: vinculación institucional de una persona a
> lo largo del tiempo.

| Columna | Tipo | Regla |
|---|---|---|
| `fk_asistente` | `BIGINT` | **PK (parte 1 de 3)** — FK a `asistente`, `CASCADE` |
| `fk_tipo_vinculacion` | `VARCHAR(15)` | **PK (parte 2 de 3)** — FK a `tipo_vinculacion` (v1), `RESTRICT` |
| `fecha_ini` | `DATE` | **PK (parte 3 de 3)** |
| `fecha_fin` | `DATE` | Opcional — `NULL` = vigente |

Restricción: `chk_vinculacion_fechas` — `fecha_fin IS NULL OR fecha_fin
>= fecha_ini`.

```csharp
public class VinculacionAsistente
{
    public required long FkAsistente { get; set; }
    public required string FkTipoVinculacion { get; set; }
    public required DateOnly FechaIni { get; set; }
    public DateOnly? FechaFin { get; set; }
}
```

Nota de uso (relevante para v4/v5): `tipo_vinculacion.exige_programa`
(columna de v1) decide si, al registrar asistencia, la BD exige una
`programa_asistente` activa — eso lo resuelve un *trigger* de v5
(`fn_trg_registro_validar`), no v3; aquí solo se documenta porque
explica por qué `ADMINISTRATIVO`, `RUTAS_PAZ`, `COLEGIO` y `EXTERNO`
tienen `exige_programa = false` en los datos de ejemplo.

### 2.5 `programa_asistente` — llave compuesta, sin `id` (ADR D16)

> Comentario del esquema: matrícula de un asistente en un programa,
> por periodo — N:M por dato comprobado (el mismo id aparece con
> varios códigos de programa, y el archivo real trae pares repetidos
> hasta 3 veces).

| Columna | Tipo | Regla |
|---|---|---|
| `fk_asistente` | `BIGINT` | **PK (parte 1 de 3)** — FK a `asistente`, `CASCADE` |
| `fk_programa` | `VARCHAR(5)` | **PK (parte 2 de 3)** — FK a `programa_academico` (v2), `RESTRICT` |
| `fk_periodo` | `CHAR(6)` | **PK (parte 3 de 3)** — FK a `periodo_academico` (v1), `RESTRICT` |
| `estado` | `VARCHAR(15)` | No nulo, `DEFAULT 'ACTIVO'`, lista cerrada |

Restricción: `chk_programa_asistente_estado` —
`estado IN ('ACTIVO', 'RETIRADO', 'GRADUADO', 'APLAZADO')`.

```csharp
public class ProgramaAsistente
{
    public required long FkAsistente { get; set; }
    public required string FkPrograma { get; set; }
    public required string FkPeriodo { get; set; }
    public string Estado { get; set; } = "ACTIVO";
}
```

### 2.6 `consentimiento_datos`

> Comentario del esquema: Ley 1581 de 2012 — el sistema guarda
> documentos, nombres y correos personales de externos; el
> consentimiento es un HECHO que se registra, no un aviso en pantalla.

| Columna | Tipo | Regla |
|---|---|---|
| `id_consentimiento` | `BIGINT` | **PK**, `IDENTITY` |
| `fk_asistente` | `BIGINT` | No nulo — FK a `asistente`, `CASCADE` |
| `version_politica` | `VARCHAR(20)` | No nulo — única junto con `fk_asistente` (`uq_consentimiento_ver`) |
| `aceptado_en` | `TIMESTAMPTZ` | No nulo, `DEFAULT now()` — de solo lectura |
| `ip` | `INET` | Opcional |
| `canal` | `VARCHAR(20)` | No nulo, `DEFAULT 'WEB'`, lista cerrada |

Restricción: `chk_consentimiento_canal` —
`canal IN ('WEB', 'PRESENCIAL', 'CORREO', 'IMPORTADO')`.

```csharp
public class ConsentimientoDatos
{
    public long IdConsentimiento { get; set; }          // lo pone la BD
    public required long FkAsistente { get; set; }
    public required string VersionPolitica { get; set; }
    public DateTime AceptadoEn { get; set; }              // lo pone la BD
    public string? Ip { get; set; }
    public string Canal { get; set; } = "WEB";
}
```

**Sin FK entrante:** ninguna tabla del esquema referencia
`consentimiento_datos` — igual que `alias_programa` en v2, su `DELETE`
nunca puede fallar por integridad referencial. **Pero, a diferencia de
`alias_programa`, no tiene ni siquiera `DELETE`** (RF113): es evidencia
legal, no un registro de solo-anexo corregible.

### 2.7 `novedad_carga`

> Comentario del esquema (implícito en `lote_carga_asistente`): el
> detalle de cada fila que una carga rechazó.

| Columna | Tipo | Regla |
|---|---|---|
| `id_novedad` | `BIGINT` | **PK**, `IDENTITY` |
| `fk_lote_carga` | `BIGINT` | No nulo — FK a `lote_carga_asistente`, `CASCADE` |
| `numero_fila` | `INTEGER` | No nulo — `CHECK > 0` |
| `contenido_crudo` | `TEXT` | No nulo — la fila tal como llegó (para poder reprocesarla a mano) |
| `motivo` | `VARCHAR(200)` | No nulo |

```csharp
public class NovedadCarga
{
    public long IdNovedad { get; set; }                 // lo pone la BD
    public required long FkLoteCarga { get; set; }
    public required int NumeroFila { get; set; }
    public required string ContenidoCrudo { get; set; }
    public required string Motivo { get; set; }
}
```

**Nace únicamente dentro de `sp_guardar_lote_carga`** (§2.1, RF116):
no existe `NovedadCargaCrear` ni ningún `POST` propio.

## 3. Las dos murallas de validación (sin cambios de criterio)

Igual que v1/v2: la PETICIÓN detiene errores de forma (422); el
SERVICIO detiene reglas de negocio que cruzan campos (400) — en v3,
"no cerrar un lote ya cerrado" (RF116) y "no cerrar una vinculación ya
cerrada" (RF109); la BD es la última muralla, y en v3 incluye por
primera vez un **índice único parcial** (`ux_documento_vigente`) además
de los `CHECK`/`UNIQUE`/FK ya conocidos.

## 4. Reglas de esta versión

- El código de esta versión solo nombra `lote_carga_asistente`,
  `asistente`, `novedad_carga`, `documento_asistente`,
  `vinculacion_asistente`, `programa_asistente` y `consentimiento_datos`.
  `sesion`, `ponencia`, `usuario` y las demás tablas de v4/v5 no se
  construyen aquí, aunque ya tengan datos de ejemplo.
- **Salvedad de la FK circular (§3 de [0_mapa_versiones.md](../0_mapa_versiones.md)):**
  `lote_carga_asistente.fk_usuario` se expone como `long?` opcional;
  en v3 **siempre viaja `null`** porque no existe ningún `Usuario`
  todavía. La BD sigue validando la FK si algún valor llegara a
  mandarse (rechazaría un id inexistente), pero la API/el front nunca
  envían uno hasta la v5.
- La BD no se modifica en tablas ni datos; se agregan dos
  procedimientos nuevos como migración aparte (§2.2.1 y §4.3 de
  [3_plan.md](3_plan.md)), sin tocar `script_bdcatedras.sql`.
- Reset completo: igual que siempre,
  `docker compose down -v && docker compose up -d --build`.
