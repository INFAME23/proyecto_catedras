# Modelo de datos — Versión 7: migración a ASIS, bitácora y parámetros

> ⚠️ **BORRADOR** — ver nota de [2_spec.md](2_spec.md). Comprobado contra
> la BD real (script + migraciones `001`-`041`) en la Fase 54.

## 1. Cadena de FK de esta versión

```
sesion (v4) ──< lote_migracion >──┬── estado_proceso (v1)
                     │             └── usuario (v5, opcional)
                     └──< detalle_migracion >── registro_asistencia (v5)

bitacora >── usuario (v5, opcional) — la llenan trg_bitacora_registro
                                       y trg_bitacora_asistente

parametro >── usuario (v5, opcional, fk_usuario_modifica) — sin FK entrantes
```

Es el grafo de FK más simple de las 7 versiones: ninguna tabla nueva
tiene más de 3 FK salientes, y `bitacora` y `parametro` solo apuntan,
opcionalmente, a `usuario`.

## 2. Tablas

### 2.1 `lote_migracion`

| Columna | Tipo | Nulo | Default | Nota |
|---|---|---|---|---|
| `id_lote_migracion` | `bigint` | NO | identity | PK |
| `fk_sesion` | `bigint` | NO | — | FK → `sesion` (v4), `RESTRICT` |
| `nombre_proceso` | `varchar(60)` | NO | `'ME_MIGRACION_CATEDRA'` | |
| `nombre_archivo` | `varchar(200)` | NO | — | lo calcula `sp_generar_lote_migracion` (`ME_CATEDRA_<id_evento_asis>_R<reunión>.xlsx`) |
| `generado_en` | `timestamptz` | NO | `now()` | |
| `fk_usuario` | `bigint` | sí | — | FK → `usuario` (v5), `SET NULL`; sin salvedad de FK circular (v5 ya cerró esa posibilidad) |
| `instancia_proceso_asis` | `varchar(30)` | sí | — | id de ejecución del lado de ASIS, si llega a existir. El SP de actualizar la recibe; la API no la expone (D37-F) |
| `fk_estado_proceso` | `varchar(12)` | NO | `'GENERADO'` | FK → `estado_proceso` (v1), `RESTRICT` (`fk_lotemig_estado`) |
| `total_filas` | `int` | NO | `0` | `CHECK >= 0`; `count(*)` de `fn_exportar_asis`: **solo migrables** (excluye externos y a quien no tiene `id_asis` o programa) |
| `observaciones` | `text` | sí | — | |

**FK salientes:** `fk_sesion` → `RESTRICT`; `fk_estado_proceso` →
`RESTRICT`; `fk_usuario` → `SET NULL`.

**FK entrantes:** `detalle_migracion` (`CASCADE`).

**Datos de partida:** 5 lotes, todos de la sesión 5712, archivo
`ME_CATEDRA_000099001_R1.xlsx`. Lote 1 `CORRECTO` (164 detalles
`ACEPTADO`); lotes 2-5 `GENERADO` (164, 164, 165 y 166 detalles
`PENDIENTE`).

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
(Cada propiedad lleva `[JsonPropertyName]` en snake_case: `id_lote_migracion`,
`fk_sesion`, … — D37-B.)

### 2.2 `detalle_migracion`

| Columna | Tipo | Nulo | Default | Nota |
|---|---|---|---|---|
| `id_detalle` | `bigint` | NO | identity | PK |
| `fk_lote_migracion` | `bigint` | NO | — | FK → `lote_migracion`, `CASCADE`; `uq_detalle_lote_reg` con `fk_registro` |
| `fk_registro` | `bigint` | NO | — | FK → `registro_asistencia` (v5), `RESTRICT`; `ux_detmig_aceptado` parcial |
| `id_asis_enviado` | `varchar(15)` | NO | — | copia de `asistente.id_asis` en el momento de generar (instantánea) |
| `programa_enviado` | `varchar(5)` | sí | — | copia de `registro_asistencia.fk_programa_snapshot` |
| `reunion_enviada` | `smallint` | NO | — | copia de `sesion.numero_reunion` (en los `CALL` entra como `integer`: `int4 → int2` no es implícito) |
| `resultado` | `varchar(12)` | NO | `'PENDIENTE'` | `PENDIENTE`, `ACEPTADO`, `RECHAZADO` (en el SP, parámetro `p_resultado_migracion`) |
| `mensaje` | `varchar(300)` | sí | — | motivo, si ASIS rechazó la fila |

**Restricciones:** `uq_detalle_lote_reg` `(fk_lote_migracion,
fk_registro)` — un registro no aparece dos veces en el MISMO lote;
`ux_detmig_aceptado` UNIQUE parcial sobre `fk_registro` donde
`resultado = 'ACEPTADO'` — un registro no queda `ACEPTADO` en más de
un lote, aunque se haya reintentado en varios; `chk_detalle_resultado`.

**Consecuencia práctica de los datos sembrados:** el lote 1 ya tiene
`ACEPTADO` 164 de los 166 registros migrables de la sesión 5712. En un
lote nuevo, solo las filas de los registros **167** y **169** admiten
pasar a `ACEPTADO`; cualquier otra choca con `ux_detmig_aceptado`.

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
| `fk_usuario` | `bigint` | sí | — | FK → `usuario` (v5), `SET NULL`; `NULL` en todas las filas, porque ningún endpoint pasa el usuario autenticado hacia el trigger (ver nota abajo) |
| `nombre_tabla` | `varchar(63)` | NO | — | `registro_asistencia` o `asistente` (las dos tablas con trigger de bitácora) |
| `operacion` | `varchar(10)` | NO | — | `INSERT`, `UPDATE`, `DELETE` |
| `llave` | `varchar(100)` | sí | — | el id de la fila afectada (`id_registro` o `id_asistente`) |
| `datos_antes` | `jsonb` | sí | — | `NULL` en un `INSERT` |
| `datos_despues` | `jsonb` | sí | — | `NULL` en un `DELETE` |

**Quién escribe:** solo `fn_trg_bitacora`, a través de dos triggers:

| Trigger | Tabla | Cuándo |
|---|---|---|
| `trg_bitacora_registro` | `registro_asistencia` | `AFTER INSERT OR UPDATE OR DELETE` |
| `trg_bitacora_asistente` | `asistente` | `AFTER UPDATE OR DELETE` |

Como el borrado de `Asistente` es lógico desde el retrofit, cada `PUT`,
`PATCH` o `DELETE` de `/api/Asistente/{id}` agrega una fila `asistente /
UPDATE`. **Datos de partida:** 168 filas, todas `registro_asistencia /
INSERT`.

Los SP `sp_crear_`/`sp_actualizar_`/`sp_eliminar_bitacora` (`014`)
existen y **no tienen llamador** en la API (D36, D37-A).

**Sin FK entrantes ni salientes relevantes para esta versión** (solo
`fk_usuario`, opcional).

> **Nota — `fk_usuario` queda siempre `NULL` en la práctica:**
> `fn_trg_bitacora` no tiene forma de saber qué usuario autenticado hizo
> la petición HTTP que llegó hasta el `INSERT`/`UPDATE`/`DELETE` —
> esa información vive en la sesión de Flask (v5), no en la conexión de
> PostgreSQL. No es un error de v7: completar ese dato exigiría que la
> API mandara el usuario actual en cada operación (p. ej. con `SET
> LOCAL app.usuario_actual = ...` antes de cada escritura), algo que
> ninguna spec ha pedido y que v7 no agrega por su cuenta (Artículo 1).
> Por lo mismo, la API no ofrece filtro por usuario (D37-C).

**Modelo C#:**
```csharp
public class Bitacora
{
    public long IdBitacora { get; set; }
    public DateTime OcurridoEn { get; set; }          // normalizada a UTC en el repositorio
    public required string UsuarioBd { get; set; }
    public long? FkUsuario { get; set; }
    public required string NombreTabla { get; set; }
    public required string Operacion { get; set; }
    public string? Llave { get; set; }
    public JsonElement? DatosAntes { get; set; }      // jsonb tal cual
    public JsonElement? DatosDespues { get; set; }
}
```

### 2.4 `parametro`

| Columna | Tipo | Nulo | Default | Nota |
|---|---|---|---|---|
| `clave` | `varchar(60)` | NO | — | **PK** — llave de texto, la elige el cliente al crear; en la API, `^[A-Z][A-Z0-9_]*$` (D37-D) |
| `valor` | `varchar(200)` | NO | — | siempre texto (admite cadena vacía); `tipo_dato` documenta cómo interpretarlo |
| `tipo_dato` | `varchar(15)` | NO | `'TEXTO'` | `TEXTO`, `ENTERO`, `BOOLEANO`, `FECHA`, `DECIMAL` |
| `descripcion` | `varchar(300)` | NO | — | |
| `fk_usuario_modifica` | `bigint` | sí | — | FK → `usuario` (v5), `SET NULL` |
| `modificado_en` | `timestamptz` | NO | `now()` | **sin trigger** — lo fija `sp_actualizar_parametro` en cada actualización (RF197) |

**Sin FK entrantes:** hoja del grafo; la única FK saliente es
`fk_usuario_modifica`.

**9 parámetros de ejemplo**, todos ya leídos con `COALESCE` por
funciones de v5 si faltaran:

| Clave | Valor |
|---|---|
| `CLAVE_LONGITUD` | `6` |
| `CLAVE_MAX_INTENTOS` | `3` |
| `CLAVE_VIGENCIA_MINUTOS` | `10` |
| `DOMINIOS_INSTITUCIONALES` | `usbmed.edu.co,usb.edu.co` |
| `PERMITE_EXTERNOS` | `true` |
| `URL_BASE` | `https://catedras.usbmed.edu.co/r/` |
| `VENTANA_MINUTOS_ANTES` | `30` |
| `VENTANA_MINUTOS_DESPUES` | `30` |
| `VERSION_POLITICA_DATOS` | `2026.1` |

**Modelo C#:**
```csharp
public class Parametro
{
    public required string Clave { get; set; }
    public required string Valor { get; set; }
    public string TipoDato { get; set; } = "TEXTO";
    public required string Descripcion { get; set; }
    public long? FkUsuarioModifica { get; set; }
    public DateTime ModificadoEn { get; set; }        // normalizada a UTC en el repositorio
}
```

## 3. Las tres murallas de validación

- **Petición (forma, 422):** `[Required]` en `fk_sesion` (generar), en
  `clave`/`valor`/`descripcion` (crear parámetro) y en
  `valor`/`tipo_dato`/`descripcion` (`PUT` de parámetro);
  `[RegularExpression]` en `resultado` (lista cerrada), `tipo_dato`
  (lista cerrada) y `clave` (`^[A-Z][A-Z0-9_]*$`, D37-D);
  `[StringLength]` según la columna; `[Range(1, …)]` en las FK numéricas.
- **Servicio (400):** `PATCH` sin ningún campo y `limite` ≤ 0. Ninguna
  regla nueva que cruce campos: no se valida la coherencia
  `valor`/`tipo_dato` (D37-D).
- **BD (última muralla, 500):** cualquier FK inexistente (D9, p. ej.
  `fk_lotemig_estado`); `ux_detmig_aceptado`; `uq_detalle_lote_reg`;
  `pk_parametro` (clave duplicada); el `RAISE` de
  `sp_generar_lote_migracion` ("No hay registros migrables…",
  `check_violation`; D25 de v5, sin cambios).

## 4. Reglas de esta versión

- La BD y los datos de las 4 tablas no cambian respecto al script
  provisto (Artículo 5). Los SP por verbo de la v7 ya existen
  (`013`, `014`, `031`, `032`, D34 enmendado); ninguno reemplaza a
  `sp_generar_lote_migracion`, `fn_exportar_asis` ni `fn_trg_bitacora`.
- `docker compose down -v && docker compose up -d --build` resetea las
  37 tablas, con 5 `lote_migracion`, 823 `detalle_migracion`, 168
  `bitacora` y 9 `parametro` de ejemplo. La sesión 5712 (la única con
  registros: 168, 1 externo, 166 migrables) vuelve a estar `MIGRADA`.
- Al cerrar esta versión, las **37 tablas** de `bdcatedras` quedan
  expuestas por la API — cuenta de control del mapa de versiones
  (§1 de [0_mapa_versiones.md](../0_mapa_versiones.md)): 10+5+7+2+5+4+4
  = 37.
