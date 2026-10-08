# Modelo de datos — Versión 7: migración a ASIS, bitácora y parámetros

> ⚠️ **BORRADOR** — ver nota de [2_spec.md](2_spec.md).

## 1. Cadena de FK de esta versión

```
sesion (v4) ──< lote_migracion >──┬── estado_proceso (v1)
                     │             └── usuario (v5, opcional)
                     └──< detalle_migracion >── registro_asistencia (v5)

bitacora >── usuario (v5, opcional) — sin FK saliente a ninguna otra tabla de esta versión

parametro — tabla plana, sin ninguna FK saliente ni entrante
```

Es el grafo de FK más simple de las 7 versiones: ninguna tabla nueva
tiene más de 3 FK salientes, y `parametro` no tiene ninguna.

## 2. Tablas

### 2.1 `lote_migracion`

| Columna | Tipo | Nulo | Default | Nota |
|---|---|---|---|---|
| `id_lote_migracion` | `bigint` | NO | identity | PK |
| `fk_sesion` | `bigint` | NO | — | FK → `sesion` (v4), `RESTRICT` |
| `nombre_proceso` | `varchar(60)` | NO | `'ME_MIGRACION_CATEDRA'` | |
| `nombre_archivo` | `varchar(200)` | NO | — | lo calcula `sp_generar_lote_migracion` (`ME_CATEDRA_<asis>_R<reunión>.xlsx`) |
| `generado_en` | `timestamptz` | NO | `now()` | |
| `fk_usuario` | `bigint` | sí | — | FK → `usuario` (v5), `SET NULL`; sin salvedad de FK circular (v5 ya cerró esa posibilidad) |
| `instancia_proceso_asis` | `varchar(30)` | sí | — | id de ejecución del lado de ASIS, si llega a existir |
| `fk_estado_proceso` | `varchar(12)` | NO | `'GENERADO'` | FK → `estado_proceso` (v1), `RESTRICT` |
| `total_filas` | `int` | NO | `0` | `CHECK >= 0`; lo calcula el SP (`fn_exportar_asis` cuenta las filas migrables) |
| `observaciones` | `text` | sí | — | |

**FK salientes:** `fk_sesion` → `RESTRICT`; `fk_estado_proceso` →
`RESTRICT`; `fk_usuario` → `SET NULL`.

**FK entrantes:** `detalle_migracion` (`CASCADE`).

**Modelo C#:**
```csharp
public class LoteMigracion
{
    public long IdLoteMigracion { get; set; }
    public required long FkSesion { get; set; }
    public string NombreProceso { get; set; } = "ME_MIGRACION_CATEDRA";
    public string NombreArchivo { get; set; } = "";   // sin 'required': lo calcula el SP
    public DateTimeOffset GeneradoEn { get; set; }
    public long? FkUsuario { get; set; }
    public string? InstanciaProcesoAsis { get; set; }
    public string FkEstadoProceso { get; set; } = "GENERADO";
    public int TotalFilas { get; set; }                // sin 'required': lo calcula el SP
    public string? Observaciones { get; set; }
}
```

### 2.2 `detalle_migracion`

| Columna | Tipo | Nulo | Default | Nota |
|---|---|---|---|---|
| `id_detalle` | `bigint` | NO | identity | PK |
| `fk_lote_migracion` | `bigint` | NO | — | FK → `lote_migracion`, `CASCADE`; `uq_detalle_lote_reg` con `fk_registro` |
| `fk_registro` | `bigint` | NO | — | FK → `registro_asistencia` (v5), `RESTRICT`; `ux_detmig_aceptado` parcial |
| `id_asis_enviado` | `varchar(15)` | NO | — | copia de `asistente.id_asis` en el momento de generar (instantánea) |
| `programa_enviado` | `varchar(5)` | sí | — | copia de `registro_asistencia.fk_programa_snapshot` |
| `reunion_enviada` | `smallint` | NO | — | copia de `sesion.numero_reunion` |
| `resultado` | `varchar(12)` | NO | `'PENDIENTE'` | `PENDIENTE`, `ACEPTADO`, `RECHAZADO` |
| `mensaje` | `varchar(300)` | sí | — | motivo, si ASIS rechazó la fila |

**Restricciones:** `uq_detalle_lote_reg` `(fk_lote_migracion,
fk_registro)` — un registro no aparece dos veces en el MISMO lote;
`ux_detmig_aceptado` UNIQUE parcial sobre `fk_registro` donde
`resultado = 'ACEPTADO'` — un registro no queda `ACEPTADO` en más de
un lote, aunque se haya reintentado en varios; `chk_detalle_resultado`.

**Sin FK entrantes:** hoja del grafo.

**Modelo C#:**
```csharp
public class DetalleMigracion
{
    public long IdDetalle { get; set; }
    public required long FkLoteMigracion { get; set; }
    public required long FkRegistro { get; set; }
    public string IdAsisEnviado { get; set; } = "";       // sin 'required': lo copia el SP
    public string? ProgramaEnviado { get; set; }
    public short ReunionEnviada { get; set; }              // sin 'required': lo copia el SP
    public string Resultado { get; set; } = "PENDIENTE";
    public string? Mensaje { get; set; }
}
```

### 2.3 `bitacora`

| Columna | Tipo | Nulo | Default | Nota |
|---|---|---|---|---|
| `id_bitacora` | `bigint` | NO | identity | PK |
| `ocurrido_en` | `timestamptz` | NO | `now()` | |
| `usuario_bd` | `name` | NO | `CURRENT_USER` | el rol de PostgreSQL que ejecutó la operación (`sa`, en este proyecto) — no confundir con `fk_usuario` |
| `fk_usuario` | `bigint` | sí | — | FK → `usuario` (v5), `SET NULL`; `NULL` en toda la API actual, porque ningún endpoint pasa el usuario autenticado hacia el trigger (ver nota abajo) |
| `nombre_tabla` | `varchar(63)` | NO | — | hoy, siempre `registro_asistencia` (el único trigger de bitácora que existe) |
| `operacion` | `varchar(10)` | NO | — | `INSERT`, `UPDATE`, `DELETE` |
| `llave` | `varchar(100)` | sí | — | el id de la fila afectada |
| `datos_antes` | `jsonb` | sí | — | `NULL` en un `INSERT` |
| `datos_despues` | `jsonb` | sí | — | `NULL` en un `DELETE` |

**Sin FK entrantes ni salientes relevantes para esta versión** (solo
`fk_usuario`, opcional).

> **Nota — `fk_usuario` queda siempre `NULL` en la práctica:**
> `trg_bitacora_registro` no tiene forma de saber qué usuario
> autenticado hizo la petición HTTP que llegó hasta el `INSERT`/
> `UPDATE`/`DELETE` de `registro_asistencia` — esa información vive en
> la sesión de Flask (v5), no en la conexión de PostgreSQL. No es un
> error de v7: completar ese dato exigiría que la API mandara el
> usuario actual en cada operación (p. ej. con `SET
> LOCAL app.usuario_actual = ...` antes de cada `INSERT`), algo que
> ninguna spec ha pedido hasta ahora y que v7 no agrega por su cuenta
> (Artículo 7).

**Modelo C#:**
```csharp
public class Bitacora
{
    public long IdBitacora { get; set; }
    public DateTimeOffset OcurridoEn { get; set; }
    public string UsuarioBd { get; set; } = "";
    public long? FkUsuario { get; set; }
    public string NombreTabla { get; set; } = "";
    public string Operacion { get; set; } = "";
    public string? Llave { get; set; }
    public JsonDocument? DatosAntes { get; set; }
    public JsonDocument? DatosDespues { get; set; }
}
```

### 2.4 `parametro`

| Columna | Tipo | Nulo | Default | Nota |
|---|---|---|---|---|
| `clave` | `varchar(60)` | NO | — | **PK** — llave de texto, la elige el cliente al crear |
| `valor` | `varchar(200)` | NO | — | siempre texto; `tipo_dato` documenta cómo interpretarlo |
| `tipo_dato` | `varchar(15)` | NO | `'TEXTO'` | `TEXTO`, `ENTERO`, `BOOLEANO`, `FECHA`, `DECIMAL` |
| `descripcion` | `varchar(300)` | NO | — | |
| `fk_usuario_modifica` | `bigint` | sí | — | FK → `usuario` (v5), `SET NULL` |
| `modificado_en` | `timestamptz` | NO | `now()` | **sin trigger** — lo fija el repositorio en cada `UPDATE` (RF197) |

**Sin FK entrantes:** hoja del grafo, y sin ninguna FK saliente más
que `fk_usuario_modifica`.

**9 parámetros de ejemplo**, todos ya leídos con `COALESCE` por
funciones de v5/v6 si faltaran: `CLAVE_LONGITUD`, `CLAVE_MAX_INTENTOS`,
`CLAVE_VIGENCIA_MINUTOS`, `DOMINIOS_INSTITUCIONALES`,
`PERMITE_EXTERNOS`, `URL_BASE`, `VENTANA_MINUTOS_ANTES`,
`VENTANA_MINUTOS_DESPUES`, `VERSION_POLITICA_DATOS`.

**Modelo C#:**
```csharp
public class Parametro
{
    public required string Clave { get; set; }
    public required string Valor { get; set; }
    public string TipoDato { get; set; } = "TEXTO";
    public required string Descripcion { get; set; }
    public long? FkUsuarioModifica { get; set; }
    public DateTimeOffset ModificadoEn { get; set; }
}
```

## 3. Las dos murallas de validación

- **Petición (forma, 422):** `[RegularExpression]` en
  `Resultado`/`TipoDato`; `[Required]` en las FK/columnas obligatorias.
- **Servicio (reglas de negocio que cruzan campos, 400):** ninguna
  nueva en esta versión — v7 es, junto con v1, la versión con menos
  reglas de cruce de campos del proyecto.
- **BD (última muralla, 500):** cualquier FK inexistente (D9);
  `ux_detmig_aceptado`; `uq_detalle_lote_reg`; las excepciones de
  negocio de `sp_generar_lote_migracion`/`fn_exportar_asis` (RN-08,
  RN-28 — D24 de v5, sin cambios).

## 4. Reglas de esta versión

- La BD y los datos de las 4 tablas no cambian respecto al script
  provisto (Artículo 5); **v7 no agrega ninguna migración** — segunda
  versión del proyecto sin SP nuevo, junto con v4 (ver ADR D33).
- `docker compose down -v && docker compose up -d --build` resetea las
  37 tablas de siempre, con 5 `lote_migracion`, 823 `detalle_migracion`,
  168 `bitacora` y 9 `parametro` de ejemplo de vuelta a su estado
  original.
- Al cerrar esta versión, las **37 tablas** de `bdcatedras` quedan
  expuestas por la API — cuenta de control del mapa de versiones
  (§1 de [0_mapa_versiones.md](../0_mapa_versiones.md)): 10+5+7+2+5+4+4
  = 37, confirmado.
