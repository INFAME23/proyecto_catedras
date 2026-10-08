# Modelo de datos — Versión 4: sesiones y ponencias

> ⚠️ **BORRADOR** — ver nota de [2_spec.md](2_spec.md).

## 1. Cadena de FK de esta versión

```
catedra (v2) ──┐
modalidad (v1) ─┼──< sesion >──┬── encuesta (v1, opcional)
sede (v1) ──────┤              │
periodo_academico (v1) ┘       └──< ponencia >── asistente (v3)
```

Referido también DESDE tablas de versiones futuras, ya sembradas con
datos de ejemplo (Artículo 5: la BD viene dada completa):

```
sesion ←── enlace_registro (v5, 9 filas, CASCADE)
sesion ←── registro_asistencia (v5, 168 filas, RESTRICT)
sesion ←── lote_migracion (v7, 5 filas, RESTRICT)
asistente ←── ponencia (esta versión, RESTRICT hacia asistente)
```

## 2. Tablas

### 2.1 `sesion`

| Columna | Tipo | Nulo | Default | Nota |
|---|---|---|---|---|
| `id_sesion` | `bigint` | NO | identity | PK |
| `fk_catedra` | `bigint` | NO | — | FK → `catedra` (v2), `CASCADE` |
| `numero_reunion` | `smallint` | NO | — | `CHECK >= 1`; lo calcula el trigger si llega `NULL`/`0` (ADR D20) |
| `numero_reunion_asis` | `smallint` | sí | — | número de reunión del ASIS original, sin FK; de utilidad para v7 (migración), v4 solo lo expone |
| `titulo` | `varchar(200)` | sí | — | |
| `inicio` | `timestamptz` | NO | — | `CHECK fin > inicio` |
| `fin` | `timestamptz` | NO | — | |
| `fk_modalidad` | `varchar(15)` | NO | — | FK → `modalidad` (v1), `RESTRICT` |
| `fk_sede` | `varchar(15)` | NO | — | FK → `sede` (v1), `RESTRICT` |
| `fk_periodo` | `char(6)` | NO | — | FK → `periodo_academico` (v1), `RESTRICT` |
| `fk_encuesta` | `bigint` | sí | — | FK → `encuesta` (v1), `SET NULL` — sin salvedad de FK circular: `encuesta` ya existe desde v1 |
| `lugar` | `varchar(200)` | sí | — | |
| `enlace_virtual` | `varchar(300)` | sí | — | |
| `cupo` | `int` | sí | — | `CHECK cupo IS NULL OR cupo > 0` |
| `estado` | `varchar(15)` | NO | `'PROGRAMADA'` | lista cerrada: `PROGRAMADA`, `ABIERTA`, `CERRADA`, `MIGRADA` |
| `creado_en` | `timestamptz` | NO | `now()` | lo pone la BD |

**Restricciones:**
- `uq_sesion_reunion` UNIQUE `(fk_catedra, numero_reunion)` — la
  última muralla contra dos sesiones con el mismo número en la misma
  cátedra (el trigger de D20 ya lo evita en el caso automático; esta
  restricción cubre también el caso de un `numero_reunion` explícito
  que choque).
- `chk_sesion_fechas` CHECK `fin > inicio` — RF125 lo valida antes en
  el servicio (400), esta es la última muralla (500 si algo se cuela).
- `chk_sesion_cupo`, `chk_sesion_estado`, `chk_sesion_reunion` — formato
  y listas cerradas, todas replicadas en `[Range]`/`[RegularExpression]`
  de la petición (422 antes de llegar aquí).
- **Trigger** `trg_sesion_consecutivo` `BEFORE INSERT` → `numero_reunion`
  automático (ADR D20); bloquea la fila de `catedra` con `FOR UPDATE`
  mientras calcula.

**FK salientes:** `fk_catedra` → `catedra` (`ON DELETE CASCADE`:
borrar una cátedra borra sus sesiones, ya documentado desde v2);
`fk_modalidad`/`fk_sede`/`fk_periodo` → `RESTRICT` (no se puede borrar
una modalidad/sede/periodo que alguna sesión use); `fk_encuesta` →
`SET NULL`.

**FK entrantes (quién se rompe o se arrastra al borrar una `sesion`):**

| Tabla | Versión | Filas de ejemplo | Regla | Efecto de `DELETE /api/Sesion/{id}` |
|---|---|---|---|---|
| `ponencia` | v4 (esta) | 0 (nace con esta versión) | `CASCADE` | se borran junto con la sesión, sin aviso |
| `enlace_registro` | v5 | 9 | `CASCADE` | idem — oculto hasta que v5 lo exponga |
| `registro_asistencia` | v5 | 168 | `RESTRICT` | `DELETE` → 500 si existe al menos una |
| `lote_migracion` | v7 | 5 | `RESTRICT` | idem |

**Sesiones de ejemplo seguras para pruebas de `DELETE` sin tocar datos
reales:** `id_sesion` 1-5 (confirmado contra la BD real: ninguna tiene
fila en `enlace_registro`, `registro_asistencia` ni `lote_migracion`).
**Cátedras de ejemplo sin ninguna sesión todavía** (útiles para probar
`numero_reunion` automático desde 1): `id_catedra` 5498, 5500, 5502,
5503, 5504 — las mismas 5 que v2 ya señalaba como "sin sesiones" al
documentar el `DELETE` de `Catedra`.

**Modelo C#:**
```csharp
public class Sesion
{
    public long IdSesion { get; set; }
    public required long FkCatedra { get; set; }
    public short NumeroReunion { get; set; }          // sin 'required': puede llegar 0 y la BD la reemplaza
    public short? NumeroReunionAsis { get; set; }
    public string? Titulo { get; set; }
    public required DateTimeOffset Inicio { get; set; }
    public required DateTimeOffset Fin { get; set; }
    public required string FkModalidad { get; set; }
    public required string FkSede { get; set; }
    public required string FkPeriodo { get; set; }
    public long? FkEncuesta { get; set; }
    public string? Lugar { get; set; }
    public string? EnlaceVirtual { get; set; }
    public int? Cupo { get; set; }
    public string Estado { get; set; } = "PROGRAMADA";
    public DateTimeOffset CreadoEn { get; set; }        // sin 'required': la pone la BD
}
```

### 2.2 `ponencia`

| Columna | Tipo | Nulo | Default | Nota |
|---|---|---|---|---|
| `fk_sesion` | `bigint` | NO | — | parte de la PK; FK → `sesion`, `CASCADE` |
| `fk_asistente` | `bigint` | NO | — | parte de la PK; FK → `asistente` (v3), `RESTRICT` |
| `rol` | `varchar(15)` | NO | `'PONENTE'` | parte de la PK; lista cerrada |

**Restricciones:**
- `pk_ponencia` PRIMARY KEY `(fk_sesion, fk_asistente, rol)` — **sin
  columna `id`** (ADR D19, distinto de D16 de v3 porque aquí no hay
  vigencia que proteger).
- `chk_ponencia_rol` CHECK: `PONENTE`, `MODERADOR`, `INVITADO`,
  `ORGANIZADOR`.

**FK salientes:** `fk_sesion` → `CASCADE` (se borra con la sesión);
`fk_asistente` → `RESTRICT` (no se puede borrar un asistente que ya es
ponente de alguna sesión — RF131, completa lo que RF100 de v3
anticipaba de forma genérica).

**Sin FK entrantes:** ninguna tabla referencia a `ponencia`; es hoja
del grafo, como `AliasPrograma` en v2 o `NovedadCarga` en v3.

**Modelo C#:**
```csharp
public class Ponencia
{
    // SIN propiedad 'Id': la llave primaria son estas 3 columnas (§4.2 de 3_plan.md)
    public required long FkSesion { get; set; }
    public required long FkAsistente { get; set; }
    public string Rol { get; set; } = "PONENTE";
}
```

## 3. Las dos murallas de validación (sin cambios de criterio)

- **Petición (forma, 422):** `[Range]` en `Cupo`; `[RegularExpression]`
  en `Estado` (`Sesion`) y `Rol` (`Ponencia`); `[Required]` en las FK
  obligatorias de ambas.
- **Servicio (reglas de negocio que cruzan campos, 400):**
  `fin > inicio` en `SesionCrear`/`SesionReemplazo`/`SesionActualizar`
  (RF125) — el único cruce de campos de esta versión.
- **BD (última muralla, 500):** cualquier FK inexistente (D9, sin
  revalidar); `uq_sesion_reunion`; PK duplicada de `ponencia`;
  `chk_sesion_fechas`/`chk_sesion_cupo`/`chk_sesion_estado`/
  `chk_sesion_reunion`/`chk_ponencia_rol` como respaldo de lo que la
  petición ya valida en forma.

## 4. Reglas de esta versión

- La BD y los datos de las 2 tablas **no cambian** respecto al script
  provisto (Artículo 5); v4 no agrega ninguna migración (a diferencia
  de v2 y v3) porque ninguna de sus operaciones es maestro-detalle.
- `fk_encuesta` se expone y funciona end-to-end desde ya (no hay
  salvedad de FK circular como la de `fk_usuario` en v3): simplemente
  ninguna sesión de ejemplo trae una encuesta asociada todavía
  (`fk_encuesta` es `NULL` en las 5714 filas sembradas), porque esa
  asociación es un dato de negocio que v6 empezará a poblar, no una
  limitación técnica de v4.
- `docker compose down -v && docker compose up -d --build` resetea las
  37 tablas de siempre, con las 5714 filas de `sesion` y 0 de
  `ponencia` (nace vacía: ningún ponente de ejemplo viene sembrado) de
  vuelta a su estado original.
