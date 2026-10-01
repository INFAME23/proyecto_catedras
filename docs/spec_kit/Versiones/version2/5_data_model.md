# Modelo de datos — Versión 2: estructura académica y catálogo de cátedras

> **Versión 2** · La base de datos sigue sin tocarse (Artículo 5: viene
> dada desde la v1, completa). Este documento describe las **5 tablas**
> que esta versión agrega a la API y, sobre todo, documenta sus
> relaciones **reales** — verificadas contra
> `db/postgres/script_bdcatedras.sql`, no asumidas por nombre de columna.
>
> **Nota de alcance:** la BD ya trae datos de ejemplo para TODO el
> dominio, incluidas tablas de versiones futuras (`sesion`:
> **5714 filas**; `asistente`: **14811**; `programa_asistente`:
> **15349** — ninguna expuesta todavía por la API). Esto importa en v2
> porque algunas de esas tablas futuras ya tienen foreign keys hacia las
> 5 de esta versión, y sus reglas `ON DELETE` producen efectos reales
> desde HOY, aunque esas tablas no tengan controlador propio todavía.
> Se documentan explícitamente en cada sección (§2.1-2.5) para que nadie
> las descubra por accidente borrando una fila de ejemplo.

---

## 1. Las 5 tablas de esta versión y su cadena de FK

```
facultad ↺ (fk_facultad_padre, autorreferencia)
   ↑ SET NULL
programa_academico ──→ facultad
   │ ↑ CASCADE                  ↑ RESTRICT (v3, ya sembrada)
   │ alias_programa      programa_asistente (v3, 15349 filas ya sembradas)
   │                      registro_asistencia (v5, aún sin sembrar)
   ↑ RESTRICT (v3)
(nada en v2 referencia programa_academico salvo lo de arriba)

dependencia ↺ (fk_dependencia_padre, autorreferencia)
   ↑ SET NULL
catedra ──→ tipo_evento (v1, ya existe en la API)
   ↑ CASCADE (v4, 5714 filas ya sembradas)
sesion
```

Las 5 tablas de v2 no tienen ninguna FK **saliente** hacia tablas de
versiones futuras (serían imposibles de construir: esas tablas no
existen en la API todavía). Las flechas marcadas "v3"/"v4"/"v5" son FK
**entrantes**: ya existen en el esquema (Artículo 5, la BD es completa
desde la v1) y, en el caso de `programa_asistente` y `sesion`, ya tienen
filas — así que sus reglas de borrado actúan desde ahora, aunque esas
tablas no tengan API propia hasta su versión.

## 2. Las 5 tablas

### 2.1 `facultad`

Sin comentario propio en el esquema.

| Columna | Tipo (PostgreSQL) | Regla |
|---|---|---|
| `id_facultad` | `VARCHAR(10)` | **PK** — texto de 1 a 10 caracteres |
| `nombre` | `VARCHAR(120)` | No nulo, **único** (`uq_facultad_nombre`) |
| `fk_facultad_padre` | `VARCHAR(10)` | Opcional — FK a `facultad.id_facultad` |
| `activo` | `BOOLEAN` | No nulo, `DEFAULT true` |

Restricción adicional:
- `chk_facultad_padre` — `fk_facultad_padre <> id_facultad`: una
  facultad no puede ser su propio padre (ver §4.2 de
  [3_plan.md](3_plan.md) sobre por qué esto NO alcanza a proteger un
  `POST`, y por qué el servicio valida lo mismo antes de llegar aquí).

```csharp
public class Facultad
{
    public required string IdFacultad { get; set; }
    public required string Nombre { get; set; }
    public string? FkFacultadPadre { get; set; }
    public bool Activo { get; set; } = true;
}
```

**FK saliente:** `fk_facultad_padre → facultad.id_facultad`,
`ON UPDATE CASCADE ON DELETE SET NULL` — **no** `RESTRICT`: borrar una
facultad con hijas no falla, las hijas quedan con `fk_facultad_padre:
null`.

**FK entrante:** `programa_academico.fk_facultad → facultad.id_facultad`,
también `ON DELETE SET NULL` — borrar una facultad usada por programas
tampoco falla; esos programas quedan con `fk_facultad: null`.

**Datos de ejemplo (9 filas):** `USB` (raíz, `Universidad de San
Buenaventura`) y 8 facultades hijas de `USB`: `F_ING`, `F_ARTES`,
`F_PSICO`, `F_DERECHO`, `F_EDU`, `F_CIENCIAS`, `F_SALUD`, `F_OTRA`.

### 2.2 `dependencia`

Sin comentario propio en el esquema. Misma forma que `facultad`, pero
sin relación con `programa_academico`.

| Columna | Tipo (PostgreSQL) | Regla |
|---|---|---|
| `id_dependencia` | `VARCHAR(15)` | **PK** — texto de 1 a 15 caracteres |
| `nombre` | `VARCHAR(120)` | No nulo, **único** (`uq_dependencia_nombre`) |
| `fk_dependencia_padre` | `VARCHAR(15)` | Opcional — FK a `dependencia.id_dependencia` |
| `activo` | `BOOLEAN` | No nulo, `DEFAULT true` |

Restricción adicional:
- `chk_dependencia_padre` — `fk_dependencia_padre <> id_dependencia`.

```csharp
public class Dependencia
{
    public required string IdDependencia { get; set; }
    public required string Nombre { get; set; }
    public string? FkDependenciaPadre { get; set; }
    public bool Activo { get; set; } = true;
}
```

**FK saliente:** `fk_dependencia_padre → dependencia.id_dependencia`,
`ON DELETE SET NULL`.

**FK entrante:** `catedra.fk_dependencia → dependencia.id_dependencia`,
también `ON DELETE SET NULL` — borrar una dependencia usada por una
cátedra no falla, la cátedra queda con `fk_dependencia: null`.

**Datos de ejemplo (8 filas):** `VICE_ACAD` y `BIENESTAR` (raíces, sin
padre); hijas de `BIENESTAR`: `PASTORAL`, `DEPORTES`, `CULTURA`,
`SALUD`, `DESA_HUM`; hija de `VICE_ACAD`: `INVESTIG`. De las 5502
cátedras de ejemplo, **solo 1** tiene `fk_dependencia` distinto de
`null`, y es justamente `DESA_HUM`.

### 2.3 `programa_academico`

Sin comentario propio en el esquema.

| Columna | Tipo (PostgreSQL) | Regla |
|---|---|---|
| `codigo` | `VARCHAR(5)` | **PK** — `CHECK (codigo ~ '^[A-Z][0-9A-Z]{3,4}$')` |
| `nombre` | `VARCHAR(150)` | No nulo — sin restricción de unicidad |
| `fk_facultad` | `VARCHAR(10)` | Opcional — FK a `facultad.id_facultad` |
| `nivel` | `VARCHAR(25)` | No nulo, `DEFAULT 'PREGRADO'`, lista cerrada |
| `activo` | `BOOLEAN` | No nulo, `DEFAULT true` |

Restricciones adicionales:
- `chk_programa_codigo` — el formato de arriba (una letra + 3 o 4
  alfanuméricos).
- `chk_programa_nivel` — `nivel IN ('PREGRADO', 'ESPECIALIZACION',
  'MAESTRIA', 'DOCTORADO', 'EDUCACION_CONTINUA', 'OTRO')`.

```csharp
public class ProgramaAcademico
{
    public required string Codigo { get; set; }
    public required string Nombre { get; set; }
    public string? FkFacultad { get; set; }
    public string Nivel { get; set; } = "PREGRADO";
    public bool Activo { get; set; } = true;
}
```

**FK saliente:** `fk_facultad → facultad.id_facultad`, `ON DELETE SET
NULL` (§2.1).

**FK entrantes (3, con reglas distintas — la más importante de esta
versión):**
| Tabla (versión) | FK | Regla |
|---|---|---|
| `alias_programa` (v2, esta misma) | `fk_programa` | `ON DELETE CASCADE` |
| `programa_asistente` (v3, **ya sembrada**: 15349 filas) | `fk_programa` | `ON DELETE RESTRICT` |
| `registro_asistencia` (v5, aún sin datos) | `fk_programa_snapshot` | `ON DELETE RESTRICT` |

Como Postgres aplica todas las FK de una tabla en la misma sentencia,
`RESTRICT` gana: si un programa tiene asistentes vinculados (lo normal,
con 15349 filas repartidas en 105 programas), su `DELETE` falla con 500
**antes** de que la cascada hacia `alias_programa` llegue a ejecutarse.
La cascada solo se vería en un programa sin ningún
`programa_asistente` — raro en los datos reales, pero posible en uno
creado nuevo durante una prueba.

**Datos de ejemplo (105 filas):** 104 con `fk_facultad: 'F_OTRA'` y 1
(`M0286`, "Ingenieria de Datos y Software") con `fk_facultad: 'F_ING'`
— el único caso de ejemplo con una facultad real asignada.

### 2.4 `alias_programa`

> Comentario del propio esquema (`COMMENT ON TABLE`): texto libre con
> que se ha escrito un programa, y el código al que corresponde. Nace
> de la calidad real del formulario actual, que trae variantes como
> "Psicologica" o "Ingenieria de datos y software" sin un código
> asociado directo. Es una tabla **viva**: los formularios en texto
> libre seguirán llegando durante toda la transición.

| Columna | Tipo (PostgreSQL) | Regla |
|---|---|---|
| `id_alias` | `BIGINT` | **PK**, respaldado por secuencia (`alias_programa_id_alias_seq`) — la BD lo genera, el cliente nunca lo manda |
| `texto_normalizado` | `VARCHAR(200)` | No nulo, **único** (`uq_alias_texto`) |
| `fk_programa` | `VARCHAR(5)` | No nulo — FK a `programa_academico.codigo` |
| `origen` | `VARCHAR(30)` | No nulo, `DEFAULT 'FORMULARIO'` |
| `creado_en` | `TIMESTAMPTZ` | No nulo, `DEFAULT now()` — **de solo lectura en la API**, ninguna petición la acepta (§4.4 de [3_plan.md](3_plan.md)) |

Sin `CHECK` de lista cerrada sobre `origen` (es texto libre con un
valor por defecto, no un catálogo).

```csharp
public class AliasPrograma
{
    public long IdAlias { get; set; }                 // lo pone la BD
    public required string TextoNormalizado { get; set; }
    public required string FkPrograma { get; set; }
    public string Origen { get; set; } = "FORMULARIO";
    public DateTimeOffset CreadoEn { get; set; }       // lo pone la BD
}
```

**Sin FK saliente propia distinta de `fk_programa` (ya descrita en
§2.3) y sin ninguna FK entrante:** ninguna tabla del esquema referencia
`alias_programa`. Es, en ese sentido, una tabla "hoja" — y por eso su
`DELETE` (RF83) nunca puede fallar por integridad referencial, solo por
no encontrarse (404).

**Datos de ejemplo (3 filas),** todas con `fk_programa: 'M0286'`:
`"ingenieria de datos y software"`, `"ingenieria de datos"`, `"ing.
datos y software"`.

### 2.5 `catedra`

> Comentario del propio esquema sobre `id_evento_asis`: identificador
> del evento en el ASIS, `CHAR(9)` **con ceros a la izquierda** — como
> entero perdería los ceros y dejaría de servir para la carga.

| Columna | Tipo (PostgreSQL) | Regla |
|---|---|---|
| `id_catedra` | `BIGINT` | **PK**, respaldado por secuencia (`catedra_id_catedra_seq`) — la BD lo genera |
| `id_evento_asis` | `CHAR(9)` | No nulo, **único** (`uq_catedra_asis`), `CHECK (id_evento_asis ~ '^[0-9]{9}$')` |
| `nombre` | `VARCHAR(200)` | No nulo |
| `nombre_asis` | `VARCHAR(30)` | **Columna generada:** `GENERATED ALWAYS AS (left(nombre, 30)) STORED` — nunca en una petición, solo en la respuesta |
| `fk_tipo_evento` | `CHAR(4)` | No nulo — FK a `tipo_evento.id_tipo_evento` (v1) |
| `fk_dependencia` | `VARCHAR(15)` | Opcional — FK a `dependencia.id_dependencia` |
| `activo` | `BOOLEAN` | No nulo, `DEFAULT true` |
| `creado_en` | `TIMESTAMPTZ` | No nulo, `DEFAULT now()` — de solo lectura, mismo criterio que en `alias_programa` |

```csharp
public class Catedra
{
    public long IdCatedra { get; set; }               // lo pone la BD
    public required string IdEventoAsis { get; set; } // CHAR(9), con ceros a la izq.
    public required string Nombre { get; set; }
    public string NombreAsis { get; set; } = "";       // lo pone la BD (generada)
    public required string FkTipoEvento { get; set; }
    public string? FkDependencia { get; set; }
    public bool Activo { get; set; } = true;
    public DateTimeOffset CreadoEn { get; set; }       // lo pone la BD
}
```

**FK salientes:**
- `fk_tipo_evento → tipo_evento.id_tipo_evento` (v1, ya existe en la
  API), `ON DELETE RESTRICT` — borrar un `TipoEvento` en uso por alguna
  cátedra fallaría con 500 (no es un caso que v2 pruebe: el catálogo de
  `TipoEvento` de v1 no se toca en esta versión).
- `fk_dependencia → dependencia.id_dependencia` (§2.2), `ON DELETE SET
  NULL`.

**FK entrante — advertencia (dato verificado, no hipotético):**
`sesion.fk_catedra → catedra.id_catedra` (v4), `ON DELETE CASCADE`, y
`sesion` **ya tiene 5714 filas sembradas** desde el script de la v1.
De las 5502 cátedras de ejemplo, **5497 tienen al menos una sesión**
asociada; borrar cualquiera de esas 5497 borra en cascada, sin aviso,
sesiones de una tabla que la API de v2 ni siquiera expone todavía. Solo
los `id_catedra` **5498 a 5502** no tienen ninguna sesión — son las
únicas cátedras de ejemplo seguras para probar `DELETE` sin arrastrar
nada (ver RF89 de [2_spec.md](2_spec.md) y el smoke test de
[7_quickstart.md](7_quickstart.md)).

**Datos de ejemplo (5502 filas).** Distribución por `fk_tipo_evento`:
`CAAB` 5460 · `DLLH` 14 · `DEPR` 12 · `SLDI` 7 · `ARCU` 5 · `DIIB` 3 ·
`EIIC` 1 (`MTG` de v1 no se usa en ningún ejemplo de `catedra`).
`fk_dependencia` es `null` en 5501 de las 5502; la única excepción
apunta a `DESA_HUM` (§2.2).

## 3. Las dos murallas de validación (sin cambios de criterio respecto a v1)

Igual que en v1 ([version1/5_data_model.md](../version1/5_data_model.md)
§3): la PETICIÓN detiene errores de forma (422) antes de tocar la BD; el
SERVICIO detiene reglas de negocio que cruzan campos (400) — en v2, la
autorreferencia de `Facultad`/`Dependencia`; la BD es la última muralla,
y en v2 esa muralla incluye por primera vez **FK reales entre tablas**,
no solo `PK`/`UNIQUE`/`CHECK` de una sola tabla.

## 4. Reglas de esta versión

- El código de esta versión solo nombra `Facultad`, `Dependencia`,
  `ProgramaAcademico`, `AliasPrograma` y `Catedra`. Ninguna otra tabla
  con FK saliente hacia estas 5 (`sesion`, `programa_asistente`,
  `asistente`, etc.) se construye en v2, aunque algunas ya tengan datos
  de ejemplo cargados — ese dato es relevante para entender el
  comportamiento de `DELETE`, no una invitación a adelantar su API.
- La BD **no se modifica**: cero columnas, índices o datos nuevos. Los
  5714 registros de `sesion` y los 15349 de `programa_asistente` que se
  mencionan aquí **ya existían** desde el script de la v1 (Artículo 5,
  "completa desde el día 1") — v2 no los siembra, solo los descubre al
  documentar las FK entrantes.
- Reset completo: igual que en v1,
  `docker compose down -v && docker compose up -d --build`.
