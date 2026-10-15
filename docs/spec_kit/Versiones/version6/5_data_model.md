# Modelo de datos — Versión 6: encuestas de calidad

> ⚠️ **BORRADOR** — ver nota de [2_spec.md](2_spec.md).

## 1. Cadena de FK de esta versión

```
encuesta (v1) ──< pregunta >── tipo_pregunta (v1)
                     │
                     └──< opcion_pregunta

registro_asistencia (v5) ──< respuesta_encuesta >── encuesta (v1)
                                   │
                                   └──< respuesta_item >──┬── pregunta (esta versión)
                                                           └── opcion_pregunta (esta versión, opcional)
```

## 2. Tablas

### 2.1 `pregunta`

| Columna | Tipo | Nulo | Default | Nota |
|---|---|---|---|---|
| `id_pregunta` | `bigint` | NO | identity | PK |
| `fk_encuesta` | `bigint` | NO | — | FK → `encuesta` (v1), `CASCADE`; `uq_pregunta_ord` con `orden` |
| `enunciado` | `varchar(300)` | NO | — | |
| `orden` | `smallint` | NO | — | `CHECK >= 1` |
| `fk_tipo_pregunta` | `varchar(15)` | NO | — | FK → `tipo_pregunta` (v1), `RESTRICT` |
| `obligatoria` | `boolean` | NO | `false` | la vigila `sp_cerrar_encuesta` (`RN-26`) al responder |
| `valor_min` | `smallint` | sí | — | `CHECK` ambos o ninguno, `valor_max > valor_min` |
| `valor_max` | `smallint` | sí | — | |

**Restricciones:** `uq_pregunta_ord` `(fk_encuesta, orden)`;
`chk_pregunta_ord`; `chk_pregunta_rango`.

**FK entrantes:** `opcion_pregunta` (`CASCADE`); `respuesta_item`
(`RESTRICT`).

**Modelo C#:**
```csharp
public class Pregunta
{
    public long IdPregunta { get; set; }
    public required long FkEncuesta { get; set; }
    public required string Enunciado { get; set; }
    public required short Orden { get; set; }
    public required string FkTipoPregunta { get; set; }
    public bool Obligatoria { get; set; } = false;
    public short? ValorMin { get; set; }
    public short? ValorMax { get; set; }
}
```

### 2.2 `opcion_pregunta`

| Columna | Tipo | Nulo | Default | Nota |
|---|---|---|---|---|
| `id_opcion` | `bigint` | NO | identity | PK |
| `fk_pregunta` | `bigint` | NO | — | FK → `pregunta`, `CASCADE`; `uq_opcion_orden` con `orden` |
| `etiqueta` | `varchar(150)` | NO | — | |
| `valor` | `smallint` | sí | — | peso numérico opcional (p. ej. para promediar) |
| `orden` | `smallint` | NO | — | |

**Restricciones:** `uq_opcion_orden` `(fk_pregunta, orden)`.

**FK entrantes:** `respuesta_item.fk_opcion` (`RESTRICT`).

**Modelo C#:**
```csharp
public class OpcionPregunta
{
    public long IdOpcion { get; set; }
    public required long FkPregunta { get; set; }
    public required string Etiqueta { get; set; }
    public short? Valor { get; set; }
    public required short Orden { get; set; }
}
```

### 2.3 `respuesta_encuesta`

| Columna | Tipo | Nulo | Default | Nota |
|---|---|---|---|---|
| `id_respuesta` | `bigint` | NO | identity | PK |
| `fk_registro` | `bigint` | NO | — | FK → `registro_asistencia` (v5), `CASCADE`; `uq_respuesta_registro` — **1 respuesta por registro, máximo** |
| `fk_encuesta` | `bigint` | NO | — | FK → `encuesta` (v1), `RESTRICT`; lo fija el SP desde `sesion.fk_encuesta`, nunca lo manda el cliente |
| `respondida_en` | `timestamptz` | NO | `now()` | |
| `completa` | `boolean` | NO | `false` | el SP la deja en `true` siempre que el `CALL` tenga éxito (D31) — un `false` persistido no ocurre en el flujo normal |

**FK entrantes:** `respuesta_item` (`CASCADE`).

**Modelo C#:**
```csharp
public class RespuestaEncuesta
{
    public long IdRespuesta { get; set; }
    public required long FkRegistro { get; set; }
    public long FkEncuesta { get; set; }       // sin 'required': lo fija el SP, nunca el cliente
    public DateTimeOffset RespondidaEn { get; set; }
    public bool Completa { get; set; }
}
```

### 2.4 `respuesta_item`

| Columna | Tipo | Nulo | Default | Nota |
|---|---|---|---|---|
| `id_item` | `bigint` | NO | identity | PK |
| `fk_respuesta` | `bigint` | NO | — | FK → `respuesta_encuesta`, `CASCADE`; `uq_respuesta_item` con `fk_pregunta` |
| `fk_pregunta` | `bigint` | NO | — | FK → `pregunta`, `RESTRICT` |
| `valor_numerico` | `smallint` | sí | — | exactamente 1 de los 3 (`CHECK chk_respuesta_item_un_valor`) |
| `valor_texto` | `text` | sí | — | |
| `fk_opcion` | `bigint` | sí | — | FK → `opcion_pregunta`, `RESTRICT` |

**Restricciones:** `uq_respuesta_item` `(fk_respuesta, fk_pregunta)` —
no se responde la misma pregunta dos veces dentro de una misma
encuesta respondida; `chk_respuesta_item_un_valor`. **Trigger**
`trg_respuesta_item_tipo` (`RN-24`: la opción elegida debe pertenecer a
la pregunta; `RN-25`: el campo usado y el rango deben coincidir con lo
que `tipo_pregunta`/`pregunta` declaran) — D9, sin revalidar en C#
(ver §4.4 de [3_plan.md](3_plan.md)).

**Sin FK entrantes:** hoja del grafo, como `AliasPrograma` (v2) o
`NovedadCarga` (v3).

**Modelo C#:**
```csharp
public class RespuestaItem
{
    public long IdItem { get; set; }
    public required long FkRespuesta { get; set; }
    public required long FkPregunta { get; set; }
    public short? ValorNumerico { get; set; }
    public string? ValorTexto { get; set; }
    public long? FkOpcion { get; set; }
}
```

## 3. Las dos murallas de validación

- **Petición (forma, 422):** `[Range(1, short.MaxValue)]` en `Orden`
  (`Pregunta`/`OpcionPregunta`); `[Required]` en las FK obligatorias.
- **Servicio (reglas de negocio que cruzan campos, 400):**
  `valor_min`/`valor_max` ambos-o-ninguno y `max > min` en
  `Pregunta` (§4.3 de [3_plan.md](3_plan.md)).
- **BD (última muralla, 500):** `uq_pregunta_ord`, `uq_opcion_orden`,
  `uq_respuesta_registro`, `uq_respuesta_item`,
  `chk_respuesta_item_un_valor`, y las reglas de negocio de
  `RN-24`/`RN-25`/`RN-26`/`R29` dentro de
  `sp_responder_encuesta`/`sp_cerrar_encuesta`/
  `fn_trg_respuesta_item_tipo` — todas caen en el mismo 500 (D25 de
  v5, sin cambios).

## 4. Reglas de esta versión

- La BD y los datos de las 4 tablas no cambian respecto al script
  provisto (Artículo 5); v6 agrega **una** migración nueva
  (`004_sp_guardar_pregunta.sql`) — la primera desde v3.
- `respuesta_encuesta`/`respuesta_item` usan funciones que YA EXISTÍAN
  desde v1 (`fn_responder_encuesta_api`) — v6 no agrega ninguna
  migración para ellas (ver ADR D30).
- `docker compose down -v && docker compose up -d --build` resetea las
  37 tablas de siempre, con 4 `pregunta`, 0 `opcion_pregunta`, 62
  `respuesta_encuesta` y 248 `respuesta_item` de ejemplo de vuelta a
  su estado original.
