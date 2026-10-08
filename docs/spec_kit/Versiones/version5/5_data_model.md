# Modelo de datos — Versión 5: usuarios, login y registro de asistencia

> ⚠️ **BORRADOR** — ver nota de [2_spec.md](2_spec.md).

## 1. Cadena de FK de esta versión

```
asistente (v3) ──< usuario >──< rol_por_usuario >── rol (v1)

sesion (v4) ──< enlace_registro ──< clave_acceso >── asistente (v3)
                     │                    │
                     └────────────────────┴──< registro_asistencia
                                                  ├──> vinculacion_asistente (v3, instantánea)
                                                  ├──> programa_academico (v2, instantánea)
                                                  └──> tipo_vinculacion (v1, instantánea)
```

Referido también DESDE tablas de versiones futuras, ya sembradas:

```
usuario ←── bitacora (v7, SET NULL) · enlace_registro.fk_usuario_crea (esta versión, SET NULL)
          ←── lote_carga_asistente (v3, SET NULL — cierra la salvedad de FK circular de v3)
          ←── lote_migracion (v7, SET NULL) · parametro (v7, SET NULL)
registro_asistencia ←── respuesta_encuesta (v6, CASCADE) · detalle_migracion (v7, RESTRICT)
```

## 2. Tablas

### 2.1 `usuario`

| Columna | Tipo | Nulo | Default | Nota |
|---|---|---|---|---|
| `id_usuario` | `bigint` | NO | identity | PK |
| `fk_asistente` | `bigint` | NO | — | FK → `asistente` (v3), `RESTRICT`; `uq_usuario_persona` — **máximo 1 usuario por asistente** |
| `usuario` | `varchar(40)` | NO | — | `CHECK ~ '^[a-z0-9._-]{4,40}$'`; `uq_usuario_login` |
| `clave_hash` | `varchar(255)` | NO | — | BCrypt (Guía IA Regla 3); `[JsonIgnore]` en el Modelo — nunca se serializa |
| `activo` | `boolean` | NO | `true` | |
| `ultimo_acceso` | `timestamptz` | sí | — | lo pone el servicio en cada login exitoso |
| `creado_en` | `timestamptz` | NO | `now()` | |

**FK salientes:** `fk_asistente` → `RESTRICT` (no se puede borrar un
asistente que ya tiene cuenta de usuario — extiende la lista de
`RESTRICT` que v3/v4 ya documentaron para `Asistente`).

**FK entrantes:** `rol_por_usuario` (`CASCADE`); `bitacora` (v7),
`enlace_registro.fk_usuario_crea` (esta versión), `lote_carga_asistente`
(v3), `lote_migracion` (v7), `parametro` (v7) — todas `SET NULL`.

**Modelo C#:**
```csharp
public class Usuario
{
    public long IdUsuario { get; set; }
    public required long FkAsistente { get; set; }
    [JsonPropertyName("usuario")]
    public required string NombreUsuario { get; set; }
    [JsonIgnore]
    public required string ClaveHash { get; set; }
    public bool Activo { get; set; } = true;
    public DateTimeOffset? UltimoAcceso { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
}
```

### 2.2 `rol_por_usuario`

Igual forma que `vinculacion_asistente`/`programa_asistente` (v3, ADR
D16): sin columna `id`.

| Columna | Tipo | Nulo | Default | Nota |
|---|---|---|---|---|
| `fk_usuario` | `bigint` | NO | — | parte de la PK; FK → `usuario`, `CASCADE` |
| `fk_rol` | `varchar(20)` | NO | — | parte de la PK; FK → `rol` (v1), `RESTRICT` |
| `fecha_ini` | `date` | NO | `CURRENT_DATE` | parte de la PK |
| `fecha_fin` | `date` | sí | — | `NULL` = rol vigente |

**Restricciones:** `pk_rol_por_usuario` `(fk_usuario, fk_rol, fecha_ini)`;
`chk_rol_usuario_fechas` `fecha_fin IS NULL OR fecha_fin >= fecha_ini`;
índice parcial `ix_rolusr_vigente` sobre `fk_usuario` donde
`fecha_fin IS NULL` (el mismo que usa `LoginAsync` para traer los
roles vigentes).

**Modelo C#:** igual forma que `VinculacionAsistente` de v3 (sin `Id`).

### 2.3 `enlace_registro`

| Columna | Tipo | Nulo | Default | Nota |
|---|---|---|---|---|
| `id_enlace` | `bigint` | NO | identity | PK |
| `fk_sesion` | `bigint` | NO | — | FK → `sesion` (v4), `CASCADE`; `ux_enlace_vigente` — máximo 1 enlace SIN revocar por sesión |
| `token` | `uuid` | NO | `gen_random_uuid()` | único; lo usa el asistente para pedir su clave |
| `url_publica` | `varchar(300)` | NO | — | la calcula `sp_emitir_enlace` con `parametro.URL_BASE` |
| `ruta_imagen_qr` | `varchar(300)` | sí | — | `CHECK` — nunca una ruta absoluta de archivo |
| `canal` | `varchar(10)` | NO | — | `QR` o `ENLACE`; lo decide la `modalidad` de la sesión (`RN-13`), nunca el cliente |
| `ventana` | `tstzrange` | NO | — | calculada por `sp_emitir_enlace` a partir de `inicio`/`fin` de la sesión ± minutos |
| `usos_maximos` | `int` | sí | — | `CHECK > 0` si no es `NULL`; sin uso activo en v5 (el SP no lo fija) |
| `revocado_en` | `timestamptz` | sí | — | `NULL` = vigente; lo pone RF146 ("revocar") o el propio `sp_emitir_enlace` (`RN-14`) |
| `fk_usuario_crea` | `bigint` | sí | — | FK → `usuario` (esta versión), `SET NULL`; quién lo emitió, opcional |
| `creado_en` | `timestamptz` | NO | `now()` | |

**Restricciones:** `ux_enlace_vigente` UNIQUE parcial sobre
`fk_sesion` donde `revocado_en IS NULL` (refuerza, a nivel de índice,
lo que `RN-14` ya garantiza desde el SP: nunca dos enlaces vigentes
para la misma sesión); `ex_enlace_sin_solape` EXCLUDE (gist) — ni
siquiera dos ventanas que se superpongan para la misma sesión;
`chk_enlace_canal`, `chk_enlace_ventana`, `chk_enlace_usos`.
**Trigger** `trg_enlace_canal` (`RN-13`): si la modalidad de la sesión
no es `AMBOS`, el `canal` debe coincidir con `canal_difusion` de esa
modalidad.

**FK entrantes:** `clave_acceso` (`CASCADE`), `registro_asistencia`
(`SET NULL`).

**Modelo C#:**
```csharp
public class EnlaceRegistro
{
    public long IdEnlace { get; set; }
    public required long FkSesion { get; set; }
    public Guid Token { get; set; }                 // lo genera la BD
    public string UrlPublica { get; set; } = "";     // lo calcula la BD
    public string? RutaImagenQr { get; set; }
    public string Canal { get; set; } = "";          // lo decide la BD (RN-13)
    public required DateTimeOffset VentanaDesde { get; set; }   // 'tstzrange' se expone como 2 columnas planas
    public required DateTimeOffset VentanaHasta { get; set; }   // (ver nota de mapeo en 6_contracts.md)
    public int? UsosMaximos { get; set; }
    public DateTimeOffset? RevocadoEn { get; set; }
    public long? FkUsuarioCrea { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
}
```
> **Nota de mapeo `tstzrange` → C#:** Dapper no tiene un tipo nativo
> para `tstzrange`; el repositorio lo lee con
> `lower(ventana) AS ventana_desde, upper(ventana) AS ventana_hasta`
> en el `SELECT` del `GET`, y el Modelo expone 2 propiedades planas en
> vez de un rango — más simple para el cliente HTTP, que de todos
> modos no necesita operar sobre rangos, solo leerlos.

### 2.4 `clave_acceso`

| Columna | Tipo | Nulo | Default | Nota |
|---|---|---|---|---|
| `id_clave` | `bigint` | NO | identity | PK |
| `fk_asistente` | `bigint` | NO | — | FK → `asistente` (v3), `CASCADE`; lo resuelve `fn_resolver_asistente` |
| `fk_enlace` | `bigint` | NO | — | FK → `enlace_registro`, `CASCADE` |
| `clave_hash` | `bytea` | NO | — | `sha256(clave + enviado_a)` (`RN-34`); `[JsonIgnore]` — nunca se serializa |
| `enviado_a` | `citext` | NO | — | institucional si existe, si no personal (`RN-19`) |
| `es_correo_institucional` | `boolean` | NO | — | lo deriva el trigger `trg_clave_derivar` desde `parametro.DOMINIOS_INSTITUCIONALES` |
| `generado_en` | `timestamptz` | NO | `now()` | |
| `expira_en` | `timestamptz` | NO | — | `generado_en + parametro.CLAVE_VIGENCIA_MINUTOS` (10 min. por defecto) |
| `usado_en` | `timestamptz` | sí | — | `NULL` hasta que RF150 ("validar") la consuma |
| `intentos` | `smallint` | NO | `0` | `CHECK 0-10`; se anula (`expira_en = now()`) al llegar a `parametro.CLAVE_MAX_INTENTOS` (3 por defecto) |
| `ip_solicitud` | `inet` | sí | — | |

**Restricciones:** `chk_clave_correo`, `chk_clave_expira`
(`expira_en > generado_en`), `chk_clave_intentos`, `chk_clave_usada`.
**Trigger** `trg_clave_derivar` (`BEFORE INSERT/UPDATE OF enviado_a`):
calcula `es_correo_institucional`.

**FK entrantes:** `registro_asistencia` (`SET NULL`).

**Modelo C#:**
```csharp
public class ClaveAcceso
{
    public long IdClave { get; set; }
    public required long FkAsistente { get; set; }
    public required long FkEnlace { get; set; }
    [JsonIgnore]
    public byte[] ClaveHash { get; set; } = [];
    public required string EnviadoA { get; set; }
    public bool EsCorreoInstitucional { get; set; }
    public DateTimeOffset GeneradoEn { get; set; }
    public DateTimeOffset ExpiraEn { get; set; }
    public DateTimeOffset? UsadoEn { get; set; }
    public short Intentos { get; set; }
    public string? IpSolicitud { get; set; }
}
```

### 2.5 `registro_asistencia`

| Columna | Tipo | Nulo | Default | Nota |
|---|---|---|---|---|
| `id_registro` | `bigint` | NO | identity | PK |
| `fk_sesion` | `bigint` | NO | — | FK → `sesion` (v4), `RESTRICT`; `uq_registro_sesion_asis` — un asistente no se registra dos veces en la misma sesión |
| `fk_asistente` | `bigint` | NO | — | FK → `asistente` (v3), `RESTRICT` |
| `fk_enlace` | `bigint` | sí | — | FK → `enlace_registro`, `SET NULL`; `NULL` si `origen` es `MANUAL`/`IMPORTADO` |
| `fk_clave` | `bigint` | sí | — | FK → `clave_acceso`, `SET NULL`; `NULL` si `origen` es `MANUAL`/`IMPORTADO` |
| `registrado_en` | `timestamptz` | NO | `now()` | |
| `fk_programa_snapshot` | `varchar(5)` | sí | — | FK → `programa_academico` (v2), `RESTRICT`; instantánea, la llena el trigger si la vinculación lo exige |
| `fk_vinculacion_snapshot` | `varchar(15)` | NO | — | FK → `tipo_vinculacion` (v1), `RESTRICT`; instantánea, la llena el trigger (`RN-22`) |
| `origen` | `varchar(15)` | NO | `'QR'` | `QR`, `ENLACE`, `MANUAL`, `IMPORTADO` |
| `ip` | `inet` | sí | — | |
| `user_agent` | `varchar(300)` | sí | — | |

**Restricciones:** `uq_registro_sesion_asis`; `chk_registro_origen`;
`chk_registro_clave` (`origen IN ('MANUAL','IMPORTADO') OR fk_clave IS
NOT NULL` — ver ADR D25 sobre por qué esto NO basta por sí solo).
**Triggers:** `trg_registro_validar` (`RN-07`, `RN-15`, `RN-22`,
`RN-38` — ventana del enlace, instantáneas de vinculación/programa,
cupo de la sesión); `trg_bitacora_registro` (llena `bitacora`, v7 —
v5 no la toca ni la necesita tocar).

**FK entrantes:** `respuesta_encuesta` (v6, `CASCADE`),
`detalle_migracion` (v7, `RESTRICT`).

**Modelo C#:**
```csharp
public class RegistroAsistencia
{
    public long IdRegistro { get; set; }
    public required long FkSesion { get; set; }
    public required long FkAsistente { get; set; }
    public long? FkEnlace { get; set; }
    public long? FkClave { get; set; }
    public DateTimeOffset RegistradoEn { get; set; }
    public string? FkProgramaSnapshot { get; set; }         // sin 'required': lo pone el trigger, puede quedar NULL
    public string FkVinculacionSnapshot { get; set; } = ""; // sin 'required': lo pone el trigger, nunca llega del cliente
    public string Origen { get; set; } = "QR";
    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
}
```

## 3. Las dos murallas de validación (sin cambios de criterio, con una tercera que aparece por primera vez)

- **Petición (forma, 422):** `[RegularExpression]` en `Usuario.usuario`;
  `[MinLength(8)]` en `Clave`; `[RegularExpression]` en `Origen`
  (`RegistroAsistenciaCrear`).
- **Servicio (reglas de negocio que cruzan campos, 400):**
  `origen` fuera de `{MANUAL, IMPORTADO}` en `RegistroAsistenciaCrear`
  (D25); `RolPorUsuario` ya cerrado (D16, igual que v3).
- **BD (última muralla, 500):** cualquier FK inexistente (D9); todos
  los `CHECK`/`UNIQUE` de las 5 tablas; **y, nuevo en v5, las
  excepciones de negocio que la propia BD lanza con `RAISE EXCEPTION`
  dentro de `sp_solicitar_clave`/`sp_validar_clave_y_registrar`** —
  tratadas igual que cualquier otro error de PostgreSQL (D24), no como
  una cuarta muralla con su propio mapeo.
- **Nueva — autenticación, no muralla de datos (401):** `POST
  /api/Usuario/login` es el único endpoint de todo el proyecto donde
  un fallo no es "dato mal formado", "regla de negocio" ni "error de
  la BD": es "no demostraste ser quien dices ser" (D26).

## 4. Reglas de esta versión

- La BD y los datos de las 5 tablas no cambian respecto al script
  provisto (Artículo 5); v5 no agrega ninguna migración (igual que
  v4): las 3 funciones que usa ya existen desde v1.
- La salvedad de FK circular de `fk_usuario` en `lote_carga_asistente`
  (v3, §3 de [0_mapa_versiones.md](../0_mapa_versiones.md)) queda
  **técnicamente cerrada** desde esta versión (`usuario` ya existe),
  pero v5 no modifica el contrato ya cerrado de v3 (Artículo 7) — si
  una versión futura quiere que la carga masiva registre quién la
  hizo, es una decisión explícita de esa spec, no un efecto colateral
  de que v5 exista.
- `docker compose down -v && docker compose up -d --build` resetea las
  37 tablas de siempre, con 1 `usuario`, 3 `rol_por_usuario`, 9
  `enlace_registro`, 167 `clave_acceso` y 168 `registro_asistencia` de
  ejemplo de vuelta a su estado original.
