# Guía IA — Versión 3: personas y carga de asistentes

> **Qué es este documento.** La guía operativa para construir, revisar o
> verificar la **v3** con un asistente de IA (Claude, Copilot, ChatGPT…),
> siguiendo el spec kit de esta versión. **No crea reglas nuevas**: aterriza
> a la v3 las que ya existen.
>
> **Jerarquía ante un conflicto:**
> [constitución](../../1_constitution.md) › [guía IA general](../../guia_ia.md)
> (sus 3 reglas) › spec kit v3 (`2_spec.md` … `8_tasks.md`) › esta guía.
> Si esta guía contradice al spec kit, gana el spec kit y se corrige esta
> guía.
>
> **Estado de la v3 (2026-10-15):** código completo y verificado en Docker
> (Fases 22-31 marcadas en `8_tasks.md`; prueba de capas registrada en su
> momento: 220 pasadas, 0 fallidas). **Ojo:** el código actual **ya pasó por
> el retrofit de procedimientos almacenados**, lo que cambia varios
> resultados del spec original (borrado lógico, `500` que ahora son `200`;
> ver §5.bis y §7). Esta guía te dice cuándo usar cada foto (§0, modos A
> y B). Además, el ADR **D19 de la v3 comparte número con el D19 de la v4**
> (ver §9).

---

## 0. Cómo usar esta guía

1. **Elige el modo** (es lo primero que debes decirle a la IA):

   | Modo | Cuándo | Qué significa |
   |---|---|---|
   | **A — Reconstruir "como se construyó"** | Ejercicio de estudio: rehacer la v3 desde cero siguiendo el spec kit | SQL a mano en los repositorios (salvo los **2 SP maestro-detalle** de las migraciones `002` y `003`); borrado físico; resultados del spec original |
   | **B — Trabajar sobre el código de hoy** | Revisar, arreglar o ampliar el repositorio actual | Todos los repositorios hacen `CALL catedras.sp_*` (migraciones `002`, `003`, `024`-`030`); borrado lógico en `asistente`; `eliminar` de `vinculacion_asistente`/`programa_asistente` = **cerrar/retirar** |

2. Abre una conversación nueva y pega el **prompt de arranque** (§1).
   Adjunta o da acceso al repositorio.
3. Trabaja **una fase a la vez** (§8). Cada fase trae un prompt, la lista de
   "terminado cuando…" y los errores típicos.
4. No marques una casilla de `8_tasks.md` hasta que la verificación pase **en
   tu máquina** (Artículo 1). La IA escribe código; quien verifica eres tú.
5. Si la IA propone algo de §5 o §9, recházalo citando el ADR.
6. **Dos documentos que se contradicen** (spec vs retrofit, 500 vs 409…): la
   IA debe **reportarlo y preguntarte**, no resolverlo en silencio.

## 1. Prompt de arranque (cópialo tal cual)

```text
Vas a trabajar en el Proyecto Cátedras, VERSIÓN 3 (personas y carga de
asistentes). Es material de estudio: todo en español y con comentarios línea
a línea.

MODO: [ A = reconstruir como se construyó | B = trabajar sobre el código de hoy ]

Antes de escribir código, lee EN ESTE ORDEN y confirma con un resumen de
5 líneas que lo entendiste:
  1. docs/spec_kit/1_constitution.md          (reglas permanentes, ganan siempre)
  2. docs/spec_kit/guia_ia.md                 (Reglas 1-3: SP maestro-detalle, FK=select, BCrypt)
  3. docs/spec_kit/Versiones/0_mapa_versiones.md (qué tablas puede nombrar cada versión)
  4. docs/spec_kit/Versiones/version3/2_spec.md → 8_tasks.md (en orden numérico)
  5. docs/spec_kit/Versiones/version3/guia_ia_v3.md (esta guía)
  6. SOLO en modo B: docs/spec_kit/Versiones/PLAN_RETROFIT_SP.md y
     db/postgres/migraciones/005_sp_sede_ejemplo.sql (el molde de los SP)

Reglas que no se negocian en la v3:
  - Solo puedes nombrar tablas de v1-v3. Las nuevas son lote_carga_asistente,
    asistente, novedad_carga, documento_asistente, vinculacion_asistente,
    programa_asistente y consentimiento_datos. sesion, ponencia, usuario,
    enlace_registro, registro_asistencia, lote_migracion... son de v4+ y NO se
    consultan ni se exponen, aunque la BD ya las tenga (y bloqueen borrados).
  - C# + ASP.NET Core (.NET 10) + Dapper, 3 paquetes NuGet, sin Entity
    Framework. Modo A: SQL a mano con @parametros salvo los 2 SP. Modo B: el
    repositorio solo hace CALL.
  - Capas: Controller -> IServicio -> IRepositorio -> RepositorioXPostgres.
    Solo Program.cs conoce clases concretas.
  - Guía IA Regla 1: POST /api/Asistente guarda maestro + 4 detalles con UN
    SP (migración 002); el cierre del lote usa OTRO SP (migración 003).
    Sin SQL dinámico.
  - Carga parcial (ADR D15): cada fila es su PROPIA petición/transacción; el
    cierre del lote es atómico. Prohibido un SP que reciba todas las filas.
  - Tablas con llave compuesta (ADR D16): vinculacion_asistente y
    programa_asistente NO tienen /{id}, PUT ni DELETE; se abren con POST y se
    cierran con PATCH cerrar / cambiar-estado.
  - Archivo de carga: se interpreta en Python (ADR D17), nunca en C#.
  - Listado de asistentes: ?q= (ILIKE) y ?pagina= en SQL, no en Flask (ADR D18).
  - Toda corrección al borrador está en el ADR D19 de la v3: es ley.
  - Contratos exactos de 6_contracts.md; JSON siempre en snake_case.

Trabajaremos fase por fase (Fases 22-31 de 8_tasks.md). No avances a la
siguiente fase hasta que te confirme que la verificación pasó.
```

## 2. Orden de lectura y qué sacar de cada documento

| # | Documento | Qué debe extraer la IA |
|---|---|---|
| 1 | `1_constitution.md` | Art. 1 (por versiones), Art. 2 (Dapper), Art. 3 (capas), Art. 5 (BD dada: no se crea `id` a las tablas compuestas), Art. 6 (español), Art. 7 (contratos), Art. 8 (códigos) |
| 2 | `guia_ia.md` | **Regla 1 sí** (dos SP maestro-detalle); **Regla 2 sí** (los detalles se crean desde la ficha del asistente: `fk_asistente` nunca se digita); Regla 3 todavía no |
| 3 | `0_mapa_versiones.md` | Fila V3: 7 tablas; front: listado buscable + carga de datos |
| 4 | `2_spec.md` | RF95-RF118, RNF8-RNF9 y los **10 criterios de aceptación** (con las marcas "(corregido)") |
| 5 | `3_plan.md` | §4.1 SP del asistente · §4.2 interfaces de 3 métodos · §4.3 SP del lote · §4.4 carga en Python · §5 pseudocódigo exacto de la pantalla de carga |
| 6 | `4_research.md` | ADR D15 (carga parcial), D16 (llave compuesta), D17 (archivo en Python), D18 (`?q=` en SQL), **D19 (11 correcciones al borrador)** |
| 7 | `5_data_model.md` | Columnas, `CHECK`, índices únicos parciales, FK entrantes y la regla de "lote cerrado" (§2.6) |
| 8 | `6_contracts.md` | Rutas, cuerpos (snake_case), `cerrar`, `cambiar-estado`, filtros |
| 9 | `7_quickstart.md` | Smoke test de la v3 y cómo aplicar `002`/`003` a una BD existente |
| 10 | `8_tasks.md` | Fases 22-31 con el **Resultado** de cada una |

## 3. La v3 en una página

- **Qué agrega:** 7 tablas, 24 RF (RF95-RF118), 2 RNF (RNF8, RNF9), 5 ADR
  (D15-D19), **2 SP** (`sp_guardar_asistente`, `sp_guardar_lote_carga`) y la
  primera pantalla que no es CRUD (carga de datos). La API pasa de 15 a **22**
  recursos; `/admin` a 23 tarjetas (15 + 7 + carga).
- **Qué NO agrega:** ninguna tabla de v4+, ningún paquete NuGet (en Python se
  usa `openpyxl`), ningún servicio nuevo en el compose, ningún login.
- **Lo que enseña:** el **maestro-detalle de verdad** (un `POST` que escribe en
  5 tablas todo-o-nada), el **cierre atómico** de un proceso largo, las
  **tablas de vigencia con llave compuesta** (sin `/{id}`), tablas de
  **solo-anexo/solo-lectura** (3 y 2 verbos), una **búsqueda en SQL**
  (`ILIKE` + `LIMIT/OFFSET`), y cómo **interpretar un archivo** en el front sin
  inventar datos cuando falla la API.

```
lote_carga_asistente ──< asistente >──< documento_asistente   (ux_documento_vigente)
   │ (1 lote, N novedades)     │ └─< vinculacion_asistente  PK (fk_asistente, fk_tipo_vinculacion, fecha_ini)
   └──< novedad_carga          │ └─< programa_asistente     PK (fk_asistente, fk_programa, fk_periodo)
                                └─< consentimiento_datos     (uq_consentimiento_ver)

POST /api/Asistente      ──CALL sp_guardar_asistente (jsonb ×4)──►  maestro + 4 detalles, TODO O NADA
POST /api/LoteCarga/{id}/cerrar ──CALL sp_guardar_lote_carga──►  contadores + novedades, atómico
Carga de datos (front):  abrir lote → N× POST /api/Asistente → cerrar lote   (PARCIAL por fila: D15)
```

## 4. Reglas permanentes aterrizadas a la v3

| Regla | En la v3 significa… |
|---|---|
| Art. 1 — no anticipar | Nada de `usuario`, login, QR ni `registro_asistencia`. `fk_lote_carga` se acepta en el `POST` del asistente porque es columna de v3; `fk_usuario` del lote es de v5 |
| Art. 2 — SQL a mano | Fuera de los 2 SP, todo `SELECT/INSERT/UPDATE/DELETE` en el repositorio con `catedras.` y `@parametros`; `ILIKE` con parámetro, jamás concatenado |
| Art. 3 — capas | El cruce `id_asis`/`es_externo` y las reglas del cierre del lote van en el **servicio**; la forma (regex de `id_asis`, `canal`, `estado`, `motivo ≤ 200`), en la **petición**; el `CALL`, en el repositorio |
| Art. 5 — BD dada | Las 7 tablas existen (14.811 asistentes) desde el script. No se agrega `id` a las tablas compuestas (D16). Los SP son **migraciones aparte**, no cambian `script_bdcatedras.sql` |
| Art. 6 — comentarios | Cabecera con la definición real de la tabla y el porqué (D15-D19) |
| Art. 7 — contratos | `POST /api/Asistente` → `{estado, mensaje, id_asistente}`; cuerpo de cierre en snake_case (`filas_leidas`…, D19 #4) |
| Art. 8 — códigos | 422 forma · 400 cruce `id_asis`/`es_externo`, "sin correo", contadores que no cuadran, lote ya cerrado, vinculación ya cerrada, mismo estado · 404 llave o lote inexistente · 500 duplicados, FK, `ux_documento_vigente`, `RESTRICT` |
| Guía IA, Regla 1 | **Aplica dos veces** (asistente y cierre del lote) |
| Guía IA, Regla 2 | Los detalles nacen desde `/admin/asistentes/{id}`; tipos de vinculación/documento, programa, periodo y estado salen de `<select>`; el asistente de un detalle nunca se escribe a mano |
| Guía IA, Regla 3 | No aplica (la contraseña llega con `usuario`, v5) |

## 5. Las decisiones que la IA tiende a "arreglar" mal

### D15 — La carga NO es todo-o-nada; el cierre sí

```csharp
// ✗ MAL: un SP "cargar_archivo(p_filas jsonb)" con las 16.093 filas en una transacción
//   (una fila mala tumba las 16.092 buenas — justo el problema que lote_carga_asistente resuelve).
// ✓ BIEN: 1) POST /api/LoteCargaAsistente  2) por fila POST /api/Asistente (con fk_lote_carga),
//         3) POST /api/LoteCargaAsistente/{id}/cerrar con los 3 contadores y UNA novedad por fila rechazada.
```

Si la API cae a mitad de la carga, el front se detiene ("se detuvo en la fila
N"), deja el lote **ABIERTO** y no inventa datos (RNF8).

### D16 — Llave compuesta: sin `/{id}`, con "cerrar"

```csharp
// ✗ MAL: copiar el molde de 6 verbos por costumbre, o agregar una columna id (viola el Art. 5).
[HttpGet("{id}")] [HttpPut("{id}")] [HttpDelete("{id}")]
Task<int> ActualizarAsync(...);  Task<int> EliminarAsync(...);

// ✓ BIEN: exactamente 3 endpoints y 3 métodos de repositorio.
[HttpGet]                       // ?fkAsistente=
[HttpPost]                      // abre la vigencia
[HttpPatch("cerrar")]           // VinculacionAsistente: llave completa EN EL BODY + fecha_fin
[HttpPatch("cambiar-estado")]   // ProgramaAsistente: llave (fk_asistente, fk_programa, fk_periodo) + estado
// Repositorio: ObtenerTodosAsync, CrearAsync, CerrarAsync / CambiarEstadoAsync
```

El servicio, antes de cerrar, **lee** la fila: inexistente → 404; ya cerrada
(`fecha_fin` no nula) o mismo estado → 400, **sin** llamar al repositorio. Una
`fecha_fin < fecha_ini` → 400 (y `chk_vinculacion_fechas` de última muralla). No
existe `GET /api/VinculacionAsistente/1` (→ 404 del framework).

### Regla 1 — `sp_guardar_asistente`: 4 detalles opcionales en un solo `CALL`

```csharp
// ✗ MAL: crear el asistente y luego 4 llamadas a otros repositorios; o ponerle fk_asistente a los detalles.
// ✓ BIEN: AsistenteCrear lleva 4 listas opcionales SIN fk_asistente; el repositorio las serializa:
//   CALL catedras.sp_guardar_asistente(@IdAsis, @Nombres, @Apellidos, @NombreCompletoAsis, @CorreoInst,
//        @CorreoPers, @Celular, @EsExterno, @Activo, @FkLoteCarga,
//        CAST(@Documentos AS jsonb), CAST(@Vinculaciones AS jsonb),
//        CAST(@Programas AS jsonb), CAST(@Consentimiento AS jsonb), NULL);   -- INOUT: id generado
```

Reglas del SP: el consentimiento es **un objeto** (no un arreglo); los otros
tres, arreglos; `NULL` o `[]` = sin ese detalle; `fk_asistente` es siempre el
id recién generado (nunca viene en el JSON). Cualquier fallo (documento
repetido, programa inexistente, vinculación repetida…) revierte **todo**: ni el
asistente queda. `AsistenteReemplazo`/`Actualizar` **no** llevan detalles
(RF98-99). Los tamaños del SP son idénticos a los de las columnas, y la firma
incluye `p_nombre_completo_asis` (D19 #8).

### D19 #3 — El cruce `id_asis`/`es_externo` lo valida el SERVICIO

```csharp
// ✗ MAL: dejarlo solo al CHECK de la BD (500) — el criterio 8 exige probarlo sin PostgreSQL.
//        Y escribir "son excluyentes": chk_asistente_externo solo exige id_asis SI NO es externo.
// ✓ BIEN (POST y PUT, ServicioAsistente):
//   es_externo = false  y  id_asis nulo      -> 400
//   ningún correo (institucional ni personal)-> 400
// En PATCH queda el CHECK de última muralla (500), costo documentado.
```

`id_asis` no lleva `[Required]` (depende de `es_externo`), pero sí
`[RegularExpression("^[0-9A-Za-z]{5,15}$")]` cuando llega.

### D15/RF114-118 — El cierre del lote

```csharp
// ✗ MAL: exponer los 3 contadores en LoteCargaAsistenteCrear; cerrar sin comprobar; camelCase en el JSON.
// ✓ BIEN (ServicioLoteCargaAsistente.CerrarAsync, ANTES del repositorio):
//   filas_aceptadas + filas_rechazadas <= filas_leidas     -> 400
//   novedades.Count == filas_rechazadas  (una por rechazo) -> 400   (D19 #7)
//   lote ya cerrado (EstaCerrado)                          -> 400   (D19 #5)
//   motivo <= 200 caracteres                               -> 422 en la petición (D19 #6)
```

`EstaCerrado` = `observaciones` no nulo **o** algún contador > 0 (regla de la
spec, expuesta como `cerrado`). `NovedadInicial` va en snake_case
(`numero_fila`, `contenido_crudo`, `motivo`) y sin `fk_lote_carga` (lo fija el
servicio). `IRepositorioNovedadCarga` tiene **solo** `ObtenerTodosAsync` y
`ObtenerPorIdAsync`: las novedades nacen dentro del SP, no por un `INSERT` del
repositorio (`POST /api/NovedadCarga` → 405).

### D17 / D18 — Archivo en Python; búsqueda en SQL

```python
# ✗ MAL: parsear el Excel en C# (nuevo paquete NuGet) o repetir D13 (bajar 14.811 filas y filtrar en Flask).
# ✓ BIEN: servicios/carga_asistentes.py usa openpyxl; routes/admin.py llama
#   GET /api/Asistente?q=...&limite=50&pagina=N  (debounce de 400 ms; 1 petición de 50 filas por búsqueda)
```

```sql
-- Forma real (hoy en sp_listar_asistente, 024; en modo A iría igual en el repositorio):
-- patrón = '%' || NULLIF(btrim(q), '') || '%'; sin q = sin filtro
WHERE (patron IS NULL OR (nombres || ' ' || apellidos) ILIKE patron OR id_asis ILIKE patron)
ORDER BY id_asistente LIMIT @limite OFFSET @desplazamiento;
```

### Otras trampas de forma

- `ConsentimientoDatosCrear` es la **única** petición; `canal` con lista
  cerrada (`WEB` por defecto); la interfaz **sin** `ActualizarAsync`/
  `EliminarAsync`; solo 3 endpoints (`GET`, `GET/{id}`, `POST`). `PUT`/`DELETE`
  sobre `/{id}` → 405.
- `DocumentoAsistente` **sí** lleva las 3 peticiones y 6 verbos (a diferencia
  de `AliasPrograma`). El índice único parcial `ux_documento_vigente` (un solo
  `vigente=true` por asistente) es de la **BD**: no se revalida (D9).
- `IdAsistente`, `IdDocumento`, `IdConsentimiento` y `AceptadoEn` sin
  `required`.

### 5.bis Retrofit de la v3 (solo modo B): qué cambió y qué no

| Spec original (modo A) | Código de hoy (modo B) |
|---|---|
| SQL a mano en 5 repositorios | `CALL sp_{listar,consultar,crear,actualizar,eliminar}_<t>` — migraciones **024** asistente, **025** documento_asistente, **026** vinculacion_asistente, **027** programa_asistente, **028** consentimiento_datos, **029** lote_carga_asistente, **030** novedad_carga |
| `POST /api/Asistente` → `sp_guardar_asistente` | **Sin cambios** (`002`) |
| `CerrarAsync` del lote → `sp_guardar_lote_carga` | **Sin cambios** (`003`); `CrearAsync` del lote llama `sp_crear_lote_carga_asistente(@NombreArchivo, @FkUsuario)`, con `FkUsuario` siempre `null` hasta la v5 (salvedad de la FK circular del mapa) |
| `DELETE /api/Asistente/{id}` físico (cascada a 4 detalles / `RESTRICT`) | **Lógico** (`activo=false`) con `sp_eliminar_asistente`; `?incluirInactivos=true` |
| `ObtenerTodosAsync(q, limite, pagina)` con `ILIKE` en el repositorio | `CALL sp_listar_asistente(@q, @limite, @desplazamiento, NULL)` — el `ILIKE` vive en el SP |
| PATCH = `UPDATE` dinámico | consultar + mezclar + un solo `sp_actualizar_<t>` |
| `sp_eliminar_vinculacion_asistente` | **ELIMINAR = CERRAR** (`fecha_fin = COALESCE(p_fecha_fin, CURRENT_DATE)` solo si seguía abierta; nunca `DELETE`, D16) |
| Duplicado / FK / `CHECK` → 500 | **Igual**; el 409 (criterio 7 de `VERSION_2.md`) **no está implementado** |

Los controladores siguen exponiendo **los mismos verbos** que el spec
(`ProgramaAsistente`: 3; `ConsentimientoDatos`: 3; `NovedadCarga`: 2;
`LoteCargaAsistente`: 4): que existan SP de `eliminar`/`actualizar` para una
tabla **no** autoriza a agregar endpoints. `Asistente` y `DocumentoAsistente`
tienen 6.

## 6. Mapa de archivos de la v3

| Capa | Archivo(s) | Nota |
|---|---|---|
| BD | `db/postgres/migraciones/002_sp_guardar_asistente.sql`, `003_sp_guardar_lote_carga.sql` | Montadas como `z002_`/`z003_` en el compose (orden alfabético después de `script_` y `z001_`); `CREATE OR REPLACE` |
| BD (modo B) | `migraciones/024…030_sp_*.sql` | Molde: `005_sp_sede_ejemplo.sql` |
| Modelo | `Modelos/{LoteCargaAsistente,Asistente,NovedadCarga,DocumentoAsistente,VinculacionAsistente,ProgramaAsistente,ConsentimientoDatos}.cs` | Los dos de llave compuesta **sin `Id`**; `LoteCargaAsistente.EstaCerrado` |
| Petición | `Peticiones/AsistenteCrear.cs` (+ `Detalle{Documento,Vinculacion,Programa,Consentimiento}Inicial.cs`), `AsistenteReemplazo.cs`, `AsistenteActualizar.cs` | 4 listas opcionales sin `fk_asistente` |
| Petición | `Peticiones/DocumentoAsistente{Crear,Reemplazo,Actualizar}.cs` | Molde completo |
| Petición | `Peticiones/VinculacionAsistente{Crear,Cerrar}.cs`, `ProgramaAsistente{Crear,CambiarEstado}.cs` | Sin `Reemplazo`/`Actualizar` |
| Petición | `Peticiones/ConsentimientoDatosCrear.cs`, `LoteCargaAsistente{Crear,Cerrar}.cs`, `NovedadInicial.cs` | `Cerrar` en snake_case |
| Repositorio | `Repositorios/IRepositorio<Entidad>.cs` + `…Postgres.cs` | `IRepositorioVinculacionAsistente` y `IRepositorioProgramaAsistente`: **3** métodos; `IRepositorioConsentimientoDatos`: 3; `IRepositorioNovedadCarga`: 2; `IRepositorioLoteCargaAsistente`: `ObtenerTodos`, `ObtenerPorId`, `Crear`, `Cerrar` |
| Servicio | `Servicios/IServicio<Entidad>.cs` + `Servicio<Entidad>.cs` | `ServicioAsistente` (cruce), `ServicioVinculacionAsistente`/`ServicioProgramaAsistente` (lee antes de cerrar), `ServicioLoteCargaAsistente` (cuadre y novedades) |
| Controlador | `Controllers/{Asistente,DocumentoAsistente}Controller.cs` (6) · `VinculacionAsistente`, `ProgramaAsistente` (3) · `ConsentimientoDatos` (3) · `LoteCargaAsistente` (4) · `NovedadCarga` (2) | `Asistente`: `?q=`, `?limite=`, `?pagina=` |
| Ensamblador | `Program.cs` | 7 parejas nuevas |
| Pruebas | `api_catedras/pruebas/Programa.cs` | Repositorios falsos de las 7 entidades |
| Front | `routes/admin.py`, `templates/admin/{asistente_lista,asistente_detalle,asistente_form,detalle_asistente_form,documento_lista,vinculacion_lista,programa_asistente_lista,consentimiento_lista,novedad_lista,lote_lista,lote_detalle}.html` | Ficha del asistente con sus 4 detalles |
| Front | `routes/carga.py`, `servicios/carga_asistentes.py`, `templates/carga_datos.html` | Ruta `/carga_datos` |

> Verifica cada nombre con `ls` antes de citarlo.

## 7. Datos de ejemplo verificados contra `script_bdcatedras.sql` (2026-10-15)

| Para probar… | Usa | Resultado esperado |
|---|---|---|
| Listado (criterio 2) | `GET /api/Asistente?limite=5` | 5 filas; el universo son **14.811** asistentes (ids 1-14811) |
| Búsqueda en SQL | `?q=1000445` | 1 fila: asistente 2 (`id_asis 1000445`, "Asistente ASIS 1000445"). **`?q=Garcia` → 204**: los nombres sembrados son genéricos (D19 #9) |
| Paginación | `?limite=2&pagina=2` | ids 3 y 4 |
| Asistente 1 | `GET /api/Asistente/1` | `id_asis 1000090`, `correo_institucional asis.1000090@usbmed.edu.co`, `es_externo false`, `fk_lote_carga 1` |
| Asistente 14811 (externo) | `GET /api/Asistente/14811` | `id_asis null`, `es_externo true`, solo `correo_personal` |
| Maestro-detalle (criterio 3) | `POST /api/Asistente` con `documentos`, `vinculaciones`, `programas`, `consentimiento` | 200 y las 4 tablas muestran filas con el mismo `id_asistente` |
| Todo o nada | el mismo `POST` repetido (mismo `id_asis`), o con programa inexistente | 500 y `GET /api/Asistente/{id}` → 404 (ni asistente ni detalles) |
| Cruce externo (D19 #3) | `es_externo:false` sin `id_asis`; sin ningún correo | 400 en cada caso |
| Documento vigente (criterio 4) | 2º `POST /api/DocumentoAsistente` con `vigente:true` para un asistente que ya tiene uno | 500 `ux_documento_vigente`; tras `PATCH` del primero a `vigente:false`, 200 |
| Documentos sembrados | tabla completa | 15.517: 14.808 vigentes y 709 no vigentes; 707 asistentes con más de uno; todos `CC`. Mismo `(tipo, número)` → 500 `uq_documento_numero` |
| Vinculaciones (criterio 5) | `POST` → `PATCH /api/VinculacionAsistente/cerrar` con la misma llave | 200 → 200 con `fecha_fin` → repetir: **400** |
| Estado de las sembradas | 14.811 vinculaciones, todas abiertas | `ESTUDIANTE` 14.808, `ADMINISTRATIVO` 1, `RUTAS_PAZ` 1, `EXTERNO` 1; el asistente 1 tiene `ESTUDIANTE` abierta desde `2026-01-19` |
| Programas | `GET /api/ProgramaAsistente` | 15.349, todas `ACTIVO` en `2026-1`, repartidas en 105 programas (`M0800` 1.738, `M0200` 1.359…) |
| Cambio de estado | `PATCH …/cambiar-estado` | 200; al mismo estado → 400; llave inexistente → 404; estado fuera de lista → 422 (`ACTIVO`, `RETIRADO`, `GRADUADO`, `APLAZADO`…) |
| Consentimientos | `GET /api/ConsentimientoDatos?fkAsistente=14809` | 3 sembrados (asistentes 14809, 14810, 14811; versión `2026.1`, canal `WEB`). Repetir la versión para el mismo asistente → 500 `uq_consentimiento_ver` |
| Verbos que no existen | `PUT /api/ConsentimientoDatos/1`; `POST /api/NovedadCarga`; `GET /api/VinculacionAsistente/1` | 405; 405; 404 |
| Lote (criterio 6) | `GET /api/LoteCargaAsistente` | 1 sembrado: `id 1`, `USBME_EMPLID_CATED_V1_1504983333.xlsx`, leídas 14.808 / aceptadas 14.808 / rechazadas 0, con observaciones → **ya está cerrado** (`POST …/1/cerrar` → 400). Para probar el cierre abre uno nuevo |
| Cierre del lote | lote nuevo: 2 asistentes con su `fk_lote_carga` y `cerrar` con `filas_leidas 3, aceptadas 2, rechazadas 1` + 1 novedad | 200; `GET /api/NovedadCarga?fkLoteCarga={id}` muestra la fila con su motivo; repetir → 400 |
| Cierre mal armado | `aceptadas + rechazadas > leidas`; o faltan novedades; `motivo` > 200 | 400; 400; 422 |
| Novedades sembradas | `GET /api/NovedadCarga` | 0 filas (204) |
| Archivo de prueba | CSV de 3 filas con 1 inválida | 2 aceptadas, 1 rechazada con su motivo, idéntico en `/api/NovedadCarga` |

**Borrado de asistentes (criterio 7, Fase 28 paso 5):**

| Llamada | Modo A (spec original) | Modo B (código de hoy) |
|---|---|---|
| `DELETE /api/Asistente/1` | **500** `fk_registro_asistente` (`RESTRICT`: 165 `registro_asistencia`, v5); su documento y su vinculación **siguen** | 200 (lógico); no toca nada más |
| `DELETE /api/Asistente/14809` | 500 (`usuario` lo referencia, v5) | 200 (lógico) |
| `DELETE` de un asistente creado en la prueba | 200 y se borran sus 4 detalles (`CASCADE`) | 200 (lógico); los detalles no se borran |

> `programa_asistente → asistente` es `CASCADE`, **no** `RESTRICT` (D19 #1): no
> uses esa tabla para justificar el bloqueo.

**Después de cualquier prueba que modifique datos:**
`docker compose down -v && docker compose up -d --build`.

## 8. Fases con prompts listos

> El código de las Fases 22-31 **ya está escrito**. Si reconstruyes la versión
> desde cero (modo A), usa los prompts tal cual; si solo verificas, salta a
> "Terminado cuando…".

### Fase 22 — Los 2 SP maestro-detalle

```text
Fase 22 de docs/spec_kit/Versiones/version3/8_tasks.md (Guía IA, Regla 1; D15 y D16).
Crea db/postgres/migraciones/002_sp_guardar_asistente.sql y 003_sp_guardar_lote_carga.sql
con CREATE OR REPLACE PROCEDURE (re-ejecutables). 002: INSERT del maestro con
RETURNING id_asistente en un parámetro INOUT y cuatro INSERT ... SELECT ...
FROM jsonb_array_elements(...) condicionales; el consentimiento llega como UN
objeto, el resto como arreglos; fk_asistente nunca viene en el JSON; incluye
p_nombre_completo_asis. 003: UPDATE del lote con los 3 contadores y observaciones
+ INSERT de las novedades desde jsonb. Tamaños de parámetros iguales a los de las
columnas. Móntalos en docker-compose.yml como z002_ y z003_. Mismo estilo que
sp_guardar_programa_academico (v2). Sin SQL dinámico.
```

**Terminado cuando:** ambos se aplican **dos veces** sin error; `\df
catedras.sp_guardar_asistente` y `\df catedras.sp_guardar_lote_carga` muestran
las firmas con los `jsonb`; en un PostgreSQL con volumen vacío, initdb corre
`script_` → `z001_` → `z002_` → `z003_` sin errores.

**Errores típicos:** nombre sin el prefijo `z` (corre antes que el script
provisto); `fk_asistente` dentro del JSON; consentimiento como arreglo; olvidar
`nombre_completo_asis`.

### Fase 23 — Capas de `Asistente` y `DocumentoAsistente`

```text
Fase 23 (RF95-RF106, D18, D19 #3). Asistente: AsistenteCrear con 4 listas de
detalle opcionales (DetalleDocumentoInicial, DetalleVinculacionInicial,
DetalleProgramaInicial, DetalleConsentimientoInicial) SIN fk_asistente;
AsistenteReemplazo/Actualizar sin detalles. Repositorio: ObtenerTodosAsync(q,
limite, pagina) con ILIKE en SQL solo si q no es nulo/vacío; CrearAsync hace
CALL sp_guardar_asistente con CAST(... AS jsonb). Servicio: valida en POST y PUT
(1) es_externo=false => id_asis obligatorio y (2) al menos un correo -> 400.
Controlador de 6 endpoints, GET con ?q= y ?pagina=. DocumentoAsistente: molde
completo de 3 peticiones y 6 verbos. IdAsistente/IdDocumento sin 'required'.
Registra las 2 parejas en Program.cs.
```

**Terminado cuando:** `?limite=5` → 5; `?q=1000445` → 1 (el filtro corre en SQL:
míralo en el log de Postgres o en el plan, no solo en el resultado);
`?limite=2&pagina=2` → ids 3 y 4; `POST` con los 4 detalles → 200 en 5 tablas;
repetido → 500 y nada creado; programa inexistente en el detalle → 500 y el
asistente NO queda; 2º documento vigente → 500 (`ux_documento_vigente`);
cruce `id_asis`/`es_externo` y "sin correo" → 400.

**Errores típicos:** concatenar `@q` en el SQL; olvidar `CAST(... AS jsonb)`
(Npgsql manda `text`); dejarle `[Required]` a `id_asis`; leer el consentimiento
como arreglo.

### Fase 24 — Capas de `VinculacionAsistente` y `ProgramaAsistente` (D16)

```text
Fase 24 (RF107-RF111, D16). Lee D16 ANTES de empezar y NO copies el molde de 6
verbos. Modelos SIN Id. Peticiones: VinculacionAsistenteCrear y ...Cerrar (llave
fk_asistente + fk_tipo_vinculacion + fecha_ini + fecha_fin); ProgramaAsistenteCrear
y ...CambiarEstado (llave + estado con [RegularExpression] sobre la lista
cerrada). Repositorios con EXACTAMENTE 3 métodos (ObtenerTodosAsync, CrearAsync,
CerrarAsync / CambiarEstadoAsync). El servicio lee antes de cerrar: inexistente
-> 404, ya cerrada o mismo estado -> 400, fecha_fin < fecha_ini -> 400, sin llamar
al repositorio. Controladores de 3 endpoints: GET (?fkAsistente=), POST, PATCH
"cerrar" / "cambiar-estado". Registra las 2 parejas en Program.cs.
```

**Terminado cuando:** abrir → 200; cerrar → 200 con `fecha_fin`; repetir → 400;
`fecha_fin < fecha_ini` → 400; llave inexistente → 404; misma llave en `POST` →
500 (PK); `GET`/`DELETE /api/VinculacionAsistente/1` → 404; `cambiar-estado` →
200, al mismo estado → 400, estado fuera de lista → 422.

**Errores típicos:** dejar un `[HttpGet("{id}")]` olvidado; mandar la llave en
la ruta en vez de en el body; implementar `ActualizarAsync` "por si acaso".

### Fase 25 — Capas de `ConsentimientoDatos`

```text
Fase 25 (RF112-RF113). Única petición ConsentimientoDatosCrear: fk_asistente y
version_politica obligatorios; canal con [RegularExpression] sobre la lista
cerrada (WEB por defecto); ip opcional. IRepositorioConsentimientoDatos SIN
ActualizarAsync ni EliminarAsync; CrearAsync con RETURNING id_consentimiento,
aceptado_en. Controlador de 3 endpoints (GET lista con ?fkAsistente=, GET/{id},
POST). IdConsentimiento y AceptadoEn sin 'required'.
```

**Terminado cuando:** `POST` → 200 con `aceptado_en` de la BD; misma versión para
el mismo asistente → 500 (`uq_consentimiento_ver`); `PUT`/`DELETE` sobre `/{id}`
→ 405.

### Fase 26 — Capas de `LoteCargaAsistente` y `NovedadCarga`

```text
Fase 26 (RF114-RF118, D15, D19 #4-#7). LoteCargaAsistenteCrear solo con
nombre_archivo (los contadores los pone la BD en 0). LoteCargaAsistenteCerrar con
filas_leidas/filas_aceptadas/filas_rechazadas obligatorios (snake_case),
observaciones opcional y novedades: List<NovedadInicial> (NovedadInicial:
numero_fila, contenido_crudo, motivo <= 200 -> 422; sin fk_lote_carga). El
servicio valida ANTES del repositorio: aceptadas + rechazadas <= leidas, una
novedad por fila rechazada y lote no cerrado (EstaCerrado) -> 400. CerrarAsync
hace CALL sp_guardar_lote_carga con CAST(@Novedades AS jsonb). NovedadCarga es de
solo lectura: IRepositorio con ObtenerTodosAsync (?fkLoteCarga=) y
ObtenerPorIdAsync. Controladores: Lote 4 endpoints (GET, GET/{id}, POST,
POST/{id}/cerrar), Novedad 2.
```

**Terminado cuando:** abrir → 200 con contadores en 0; contadores que no
cuadran → 400 (sin tocar la BD); faltan novedades → 400; `motivo` > 200 → 422;
cerrar → 200; `GET /api/NovedadCarga?fkLoteCarga=` → la novedad; repetir → 400;
lote inexistente → 404; `POST /api/NovedadCarga` → 405.

**Errores típicos:** camelCase en el body (`filasLeidas`); olvidar que una
novedad inválida revierte **todo** el cierre; considerar el lote 1 sembrado como
"abierto".

### Fase 27 — Prueba de capas ampliada (criterio 8)

```text
Fase 27. Agrega a api_catedras/pruebas/Programa.cs un repositorio FALSO y sus
verificaciones para las 7 entidades. RepositorioAsistenteFalso simula el CALL del
SP (id fijo; guarda las 4 listas en memoria) para comprobar que el servicio arma
fk_asistente en cada detalle. Los falsos de Vinculacion y ProgramaAsistente NO
implementan ActualizarAsync; un caso cierra dos veces y confirma el 400 sin que el
repositorio se invoque la segunda vez. El de LoteCarga prueba contadores que no
cuadran: el 400 sale ANTES de llamar al repositorio. Incluye id_asis/es_externo.
Todas las variables top-level comparten ámbito; usa nombres nuevos.
```

**Terminado cuando:** `docker compose exec api dotnet run --project pruebas`
termina con `0 fallidas` y código 0 (la v3 registró **220**; hoy el total es
mayor porque se sumaron v4-v7 — compara con el último total anotado, no con
220). En Windows corre en el contenedor: el Control de aplicaciones puede
bloquear la DLL.

### Fase 28 — Integridad referencial y reglas de negocio (criterios 3-7)

```text
Fase 28. No escribas código: dame la secuencia de llamadas HTTP para los 6 pasos de
8_tasks.md, EN ESE ORDEN, con los datos de §7 de guia_ia_v3.md (asistente 1,
asistente nuevo, lote nuevo) y la respuesta exacta a esperar. Dime también mi
MODO (A o B): el paso 5 cambia (ver la tabla de borrado de §7).
```

**Terminado cuando:** los 6 resultados coinciden con la columna de tu modo y
luego ejecutaste `docker compose down -v && docker compose up -d --build`. Si no
coincide, el error está en el modelo de datos o en la implementación: no
"arregles" simulando en C# una regla que ya es de la BD (Art. 5).

### Fase 29 — Front: listado buscable y administración

```text
Fase 29 (criterios 9-10, D18). routes/admin.py: /admin/asistentes busca con
GET /api/Asistente?q=...&limite=50&pagina=N en cada búsqueda (debounce 400 ms),
NO una llamada sin filtro seguida de filtro en Python (eso es D13). La ficha
/admin/asistentes/<id> muestra y crea los 4 detalles (documento, vinculación,
programa, consentimiento) con fk_asistente fijo por la URL (Regla 2). Vinculación
y programa: "abrir" y "cerrar"/"cambiar estado", NUNCA "editar". ConsentimientoDatos y
NovedadCarga: solo mostrar. Templates con el layout existente.
```

**Terminado cuando:** `/admin/asistentes` busca y pagina sin descargar las 14.811
filas (comprueba las llamadas de red, no solo la pantalla); ningún "editar" en las
tablas de llave compuesta; los errores 400/422/500 de la API se ven en pantalla.

### Fase 30 — Front: pantalla de carga de datos

```text
Fase 30 (RNF8-RNF9, D15, D17, pseudocódigo de §5 de 3_plan.md). servicios/
carga_asistentes.py interpreta el .xlsx/.csv (openpyxl): por fila construye el body
de POST /api/Asistente, resuelve el programa en texto contra el catálogo (nombre o
alias) y, si no puede, lo manda igual y deja que la API lo rechace. routes/carga.py:
(1) leer el archivo COMPLETO antes de abrir ningún lote, (2) abrir el lote,
(3) POST por fila acumulando aceptadas/rechazadas/novedades (motivo recortado a
200), (4) cerrar el lote. Si la API cae a mitad: parar, avisar "se detuvo en la fila
N", lote ABIERTO, sin datos inventados. templates/carga_datos.html con el
formulario y la tabla de resultados.
```

**Terminado cuando:** un CSV de 3 filas con 1 inválida → 2 aceptadas, 1 rechazada
con su motivo, **idéntico** en `GET /api/NovedadCarga?fkLoteCarga=`; un `.xlsx` con
números sin `.0`, programa por nombre y por alias y una fila vacía (saltada) →
correcto; 400 filas en menos de 3 s; con la API detenida a mitad de una carga
grande, error claro y lote abierto.

**Errores típicos:** parsear en C#; leer el archivo por partes (un archivo dañado
dejaría un lote abierto); mostrar contadores inventados; no recortar `motivo`.

### Fase 31 — Cierre de la v3

```text
Fase 31. Con el entorno recién reseteado, acompáñame a correr los 10 criterios de
aceptación de 2_spec.md §5 en orden (dime mi MODO para el 7). Para cada uno dame el
comando, el resultado esperado y qué revisar si no coincide. Al final: total de la
prueba de capas, texto del commit en español y recordatorio del tag v3. No escribas
nada de la v4 (Art. 1).
```

**Terminado cuando:** los 10 criterios pasan, el total queda anotado en
`8_tasks.md`, todas las casillas están marcadas y existe el tag `v3` (`git tag
--list`; **no verificado desde esta guía**).

## 9. Trampas conocidas y diagnóstico

| Síntoma | Causa | Arreglo |
|---|---|---|
| `?q=Garcia` → 204 | Nombres sembrados genéricos | Correcto (D19 #9); usar `?q=1000445` |
| `POST /api/Asistente` falla con "could not determine data type" o `jsonb` | Falta `CAST(@x AS jsonb)` | Cast explícito |
| El consentimiento no se guarda | Se mandó un arreglo en vez de un objeto | Un solo objeto |
| Asistente creado pero sin detalles (o al revés) | `fk_asistente` en el JSON o SP sin migrar | Quitar `fk_asistente`; aplicar `002` |
| `002_…` falla con "relation does not exist" | Sin prefijo `z` en el montaje | `z002_…` / `z003_…` |
| `PATCH …/cerrar` repetido → 400 | Ya cerrada (correcto, D16) | — |
| `GET /api/VinculacionAsistente/1` → 404 | No hay ruta `/{id}` (correcto) | — |
| `PUT /api/ConsentimientoDatos/1` → 405 | La ruta existe solo para `GET` (correcto) | — |
| `POST …/1/cerrar` → 400 | El lote 1 sembrado ya está cerrado | Abrir un lote nuevo |
| El cierre del lote revierte todo | Una novedad con `motivo` > 200, `numero_fila` ≤ 0 o sin cuadre | Recortar en el front / corregir contadores |
| 500 con `registro_asistencia` al borrar el asistente 1 (modo A) | `fk_registro_asistente RESTRICT` (v5) | Correcto; no usar `programa_asistente` como justificación (es `CASCADE`) |
| `DELETE /api/Asistente/1` → 200 (modo B) | Borrado lógico | Correcto |
| `500` con "duplicate key … ux_documento_vigente" | Segundo documento vigente | Poner el primero en `vigente:false` antes |
| El lote 1 sembrado "no existe" en el front | Lista con `limite` bajo o filtro | `GET /api/LoteCargaAsistente` |
| Dos "D19" distintos | La v3 usa D19 para las correcciones al borrador y la v4 para `Ponencia` | Citarlos con versión: "D19 de la v3" / "D19 de la v4" |
| `fk_usuario` en `LoteCargaAsistente` | Columna del modelo; el servicio la deja siempre `null` hasta la v5 (FK circular) | No exponerla en ninguna petición |
| La carga de 4.000 filas se corta | API caída | Esperado: "se detuvo en la fila N", lote ABIERTO |
| Todo roto | — | `docker compose down -v && docker compose up -d --build` |

## 10. Lista de revisión para cualquier cambio de la v3

- [ ] Solo nombra tablas de v1-v3 (Art. 1, 5); ni `usuario` ni `registro_asistencia`.
- [ ] Modo A: SQL a mano con `catedras.` y `@parametros` (salvo los 2 SP).
      Modo B: el repositorio solo hace `CALL`.
- [ ] Los 2 SP: `CREATE OR REPLACE`, `jsonb`, montados como `z002_`/`z003_`,
      sin SQL dinámico; `fk_asistente`/`fk_lote_carga` nunca vienen en el JSON.
- [ ] Carga parcial por fila; cierre atómico (D15).
- [ ] Llave compuesta: 3 endpoints, 3 métodos, sin `Id`, sin `Reemplazo`/
      `Actualizar` (D16).
- [ ] Cruce `id_asis`/`es_externo`, "sin correo", cuadre de contadores,
      novedad por rechazo y lote cerrado: en el **servicio**, antes del
      repositorio.
- [ ] JSON en snake_case; `motivo` ≤ 200 en la petición.
- [ ] `?q=`/`?pagina=` en SQL (D18); archivo interpretado en Python (D17).
- [ ] Forma → petición (422); negocio → servicio (400); no existe → 404;
      motor → 500.
- [ ] Front: detalles desde la ficha del asistente (Regla 2), sin "editar" en
      tablas de vigencia, sin mock, errores de la API visibles.
- [ ] Cada entidad nueva **registrada en `Program.cs`** y con sección en la
      prueba de capas.
- [ ] Comentarios en español con el porqué (Art. 6).
- [ ] `8_tasks.md` actualizado con lo verificado **y dónde**.

## 11. Lo que la v3 le deja a la v4 (no construir todavía)

- La v4 agrega `sesion` y `ponencia` (FK a `catedra` y `asistente`); `ponencia`
  es otra llave compuesta pero **sin vigencia** (el D19 de la v4 la distingue
  de D16).
- El detalle de la cátedra mostrará sus sesiones; el calendario del ponente
  elegirá a la persona por búsqueda (`?q=`) hasta que llegue el login (v5).
- `usuario`, `enlace_registro`, `registro_asistencia` y `rol_por_usuario`
  llegan con la v5 (este último repite el patrón de D16).
- **Pendientes de cierre de la v3** (documentados): verificar el tag `v3`
  (`git tag --list`); el ADR D19 duplicado entre v3 y v4; `FkUsuario` en
  `LoteCargaAsistente`, que sigue `null` hasta la v5. La IA debe
  **reportarlos**, no corregirlos en silencio.
- La spec de la v4 se escribe **después** del tag `v3` (Artículo 1).
