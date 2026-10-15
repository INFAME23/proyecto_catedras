# Plan técnico — Versión 3: personas y carga de asistentes

> ✅ **Construida y verificada (2026-10-07)** — ver nota de [2_spec.md](2_spec.md); correcciones al borrador en el ADR D19 de [4_research.md](4_research.md).

## 1. Stack

Sin cambios: C#/ASP.NET Core (.NET 10) + Dapper, SIN ORM, SIN paquetes
nuevos (RNF9 de [2_spec.md](2_spec.md) — el archivo de carga se
interpreta en `front_catedras/`, no aquí).

## 2. Estructura de carpetas

```
api_catedras/
├── Modelos/
│   ├── Asistente.cs               # 12 propiedades
│   ├── DocumentoAsistente.cs      # 6 propiedades
│   ├── VinculacionAsistente.cs    # 4 propiedades, SIN id (§4.2)
│   ├── ProgramaAsistente.cs       # 4 propiedades, SIN id (§4.2)
│   ├── ConsentimientoDatos.cs     # 6 propiedades
│   ├── LoteCargaAsistente.cs      # 8 propiedades
│   └── NovedadCarga.cs            # 5 propiedades
├── Peticiones/
│   ├── AsistenteCrear.cs / AsistenteReemplazo.cs / AsistenteActualizar.cs
│   ├── DetalleDocumentoInicial.cs / DetalleVinculacionInicial.cs /     # §4.1 — SIN
│   │   DetalleProgramaInicial.cs / DetalleConsentimientoInicial.cs     # fk_asistente,
│   │                                                                   # lo pone el servicio
│   ├── DocumentoAsistenteCrear.cs / DocumentoAsistenteReemplazo.cs / DocumentoAsistenteActualizar.cs
│   ├── VinculacionAsistenteCrear.cs / VinculacionAsistenteCerrar.cs    # ⚠️ sin Reemplazo/Actualizar (§4.2)
│   ├── ProgramaAsistenteCrear.cs / ProgramaAsistenteCambiarEstado.cs   # ⚠️ idem
│   ├── ConsentimientoDatosCrear.cs                                    # ⚠️ ÚNICA, como AliasPrograma
│   ├── LoteCargaAsistenteCrear.cs / LoteCargaAsistenteCerrar.cs        # ⚠️ sin Reemplazo/Actualizar
│   └── NovedadInicial.cs                                              # elemento de 'novedades' en Cerrar
├── Controllers/
│   ├── AsistenteController.cs            # 6 endpoints (molde completo)
│   ├── DocumentoAsistenteController.cs    # 6 endpoints
│   ├── VinculacionAsistenteController.cs  # 3: GET lista, POST, PATCH /cerrar (§4.2)
│   ├── ProgramaAsistenteController.cs     # 3: GET lista, POST, PATCH /cambiar-estado
│   ├── ConsentimientoDatosController.cs   # 3: GET lista, GET/{id}, POST
│   ├── LoteCargaAsistenteController.cs    # 4: GET lista, GET/{id}, POST, POST/{id}/cerrar
│   └── NovedadCargaController.cs          # 2: GET lista, GET/{id}
├── Servicios/
│   ├── IServicioAsistente.cs / ServicioAsistente.cs        # CrearAsync arma los 4 detalles (§4.1)
│   ├── IServicioDocumentoAsistente.cs / ServicioDocumentoAsistente.cs
│   ├── IServicioVinculacionAsistente.cs / ServicioVinculacionAsistente.cs   # valida fecha_fin ya puesta (§4.2)
│   ├── IServicioProgramaAsistente.cs / ServicioProgramaAsistente.cs
│   ├── IServicioConsentimientoDatos.cs / ServicioConsentimientoDatos.cs
│   ├── IServicioLoteCargaAsistente.cs / ServicioLoteCargaAsistente.cs      # CerrarAsync valida los 3 contadores
│   └── IServicioNovedadCarga.cs / ServicioNovedadCarga.cs
└── Repositorios/
    ├── IRepositorioAsistente.cs / RepositorioAsistentePostgres.cs          # CrearAsync = CALL sp_guardar_asistente
    ├── IRepositorioDocumentoAsistente.cs / RepositorioDocumentoAsistentePostgres.cs
    ├── IRepositorioVinculacionAsistente.cs / RepositorioVinculacionAsistentePostgres.cs  # sin ActualizarAsync
    ├── IRepositorioProgramaAsistente.cs / RepositorioProgramaAsistentePostgres.cs        # idem
    ├── IRepositorioConsentimientoDatos.cs / RepositorioConsentimientoDatosPostgres.cs    # sin ActualizarAsync/EliminarAsync
    ├── IRepositorioLoteCargaAsistente.cs / RepositorioLoteCargaAsistentePostgres.cs       # CerrarAsync = CALL sp_guardar_lote_carga
    └── IRepositorioNovedadCarga.cs / RepositorioNovedadCargaPostgres.cs                   # solo ObtenerTodosAsync/ObtenerPorIdAsync
```

**`front_catedras/`** (ver §5): nueva ruta `carga_datos` (`routes/carga.py`) +
servicio `servicios/carga_asistentes.py` que interpreta el archivo
(`openpyxl`, agregado a `front_catedras/requirements.txt`; la API no
cambia de paquetes).

## 3. Arquitectura en capas

Sin cambios de forma (Artículo 3): `Controller → IServicio → IRepositorio
→ RepositorioXPostgres → BD`. Lo nuevo es que, por primera vez, **dos
repositorios no hacen un simple `INSERT`**: invocan un `CALL` a un SP
con parámetros `jsonb` (mismo patrón que el retrofit de `ProgramaAcademico`
en v2).

## 4. Decisiones de diseño clave

### 4.1 `sp_guardar_asistente`: maestro + 4 detalles, en una transacción

Regla 1 de la [guía operativa](../../guia_ia.md): el `POST
/api/Asistente` acepta 4 listas/objeto opcionales —
`documentos: DetalleDocumentoInicial[]`,
`vinculaciones: DetalleVinculacionInicial[]`,
`programas: DetalleProgramaInicial[]`,
`consentimiento: DetalleConsentimientoInicial?` (uno solo, no lista: un
asistente nuevo acepta como máximo la política vigente en ese momento).
Ninguna de las 4 peticiones de detalle tiene `fk_asistente`: lo fija el
servicio con el id que la BD genere.

```
POST /api/Asistente { nombres, apellidos, ..., documentos: [...], consentimiento: {...} }
  → AsistenteCrear (+ 4 detalles opcionales)             valida forma → 422
  → ServicioAsistente.CrearAsync                         arma Asistente + 4 listas de Modelo
  → RepositorioAsistentePostgres.CrearAsync               serializa cada lista a JSON y hace
       CALL catedras.sp_guardar_asistente(..., CAST(@Documentos AS jsonb), ...)
  → el SP: INSERT asistente (RETURNING id_asistente)
         + INSERT ... SELECT FROM jsonb_array_elements(p_documentos)  -- con ese id_asistente
         + INSERT ... SELECT FROM jsonb_array_elements(p_vinculaciones)
         + INSERT ... SELECT FROM jsonb_array_elements(p_programas)
         + INSERT consentimiento_datos (si p_consentimiento no es NULL)
```

El SP vive en
`db/postgres/migraciones/002_sp_guardar_asistente.sql` (mismo mecanismo
de montaje `z002_...` que el 001 de v2 — ver
[6_contracts.md](6_contracts.md) §3). Si cualquier `INSERT` falla
(documento duplicado, programa inexistente, vinculación repetida), el
`CALL` completo se revierte: **ni el asistente queda creado** — a
diferencia de la carga masiva (§4.3), donde SÍ se quiere que una fila
mala no tumbe las demás. Esta es la diferencia clave entre "una
petición HTTP = un asistente" (todo-o-nada, aquí) y "una carga = miles
de peticiones internas" (parcial, D15).

### 4.2 Tablas de vigencia con llave compuesta (ADR D16)

`VinculacionAsistente` y `ProgramaAsistente` no tienen columna `id`: su
llave primaria son 3 columnas. El molde de 5 verbos con `/{id}` no
aplica. Se usan 3 operaciones, no 6:

```csharp
public interface IRepositorioVinculacionAsistente
{
    Task<List<VinculacionAsistente>> ObtenerTodosAsync(long? fkAsistente, int limite);
    Task CrearAsync(VinculacionAsistente vinculacion);                 // INSERT; PK duplicada -> 500
    Task<int> CerrarAsync(long fkAsistente, string fkTipoVinculacion,
                          DateOnly fechaIni, DateOnly fechaFin);        // UPDATE ... SET fecha_fin
}
```

`ServicioVinculacionAsistente.CerrarAsync` valida, ANTES de llamar al
repositorio, que la fila exista y que `fecha_fin` sea `null` todavía
(si no, 400 "ya estaba cerrada" — cruce de campos, mismo criterio que
D10 de v2). `ProgramaAsistente` es idéntico, cambiando `fecha_fin` por
`estado` y `CerrarAsync` por `CambiarEstadoAsync`.

### 4.3 `sp_guardar_lote_carga`: cierre parcial, no todo-o-nada (ADR D15)

**Contexto.** La carga de 16.093 filas del archivo real del ASIS tiene
filas con datos inválidos. Si el cierre del lote fuera todo-o-nada
(como el SP de `ProgramaAcademico`), una sola fila mala en 16.093
descartaría las 16.092 buenas — exactamente lo que la tabla
`lote_carga_asistente` (con sus contadores `filas_leidas/aceptadas/
rechazadas`) existe para evitar, según su propio comentario en el
esquema.

**Decisión.** La carga se resuelve en DOS pasos, NO en un solo `CALL`:

1. **Por cada fila del archivo**, el front llama
   `POST /api/Asistente` (§4.1) con `fk_lote_carga` ya fijado al lote
   abierto. Cada llamada es su PROPIA transacción (todo-o-nada *por
   fila*, no por archivo): si esa fila falla, las anteriores ya
   quedaron guardadas. El front cuenta cuántas tuvieron éxito (`200`) y
   cuántas fallaron (`400`/`422`/`500`), y para las que fallaron,
   guarda el motivo (el `mensaje` de la respuesta de error).
2. **Al final**, UNA sola llamada,
   `POST /api/LoteCargaAsistente/{id}/cerrar`, invoca
   `sp_guardar_lote_carga` (maestro = `lote_carga_asistente`
   actualizado con los 3 contadores finales; detalle = `novedad_carga`
   con cada fila rechazada) — esta sí es todo-o-nada, porque cerrar el
   lote es una sola operación de bitácora, no 16.093.

```sql
CALL catedras.sp_guardar_lote_carga(
    @IdLote, @FilasLeidas, @FilasAceptadas, @FilasRechazadas, @Observaciones,
    CAST(@Novedades AS jsonb));   -- [{numero_fila, contenido_crudo, motivo}, ...]
```

El SP hace `UPDATE lote_carga_asistente SET filas_leidas = ..., ... +
INSERT INTO novedad_carga SELECT ... FROM jsonb_array_elements(p_novedades)`,
en una transacción. El `CHECK chk_lote_carga_filas` de la BD
(`aceptadas + rechazadas <= leidas`) es la última muralla si el cliente
manda números que no cuadran — aunque el servicio también lo valida
antes (400, más temprano y con mejor mensaje que un 500 de la BD).

### 4.4 Por qué el archivo se interpreta en `front_catedras/`, no en C# (ADR D17)

**Contexto.** El archivo real (`USBME_EMPLID_CATED_V1_....xlsx`) es
Excel. Leerlo en C# exigiría un paquete nuevo (p. ej.
`ClosedXML`/`EPPlus`), que el Artículo 2 no permite agregar sin que una
spec lo pida explícitamente — y ninguna lo hace.

**Opciones evaluadas:** (a) agregar un paquete de Excel a
`ApiCatedras.csproj`; (b) que el FRONT (Flask/Python) interprete el
archivo con `openpyxl`/`pandas` — ya disponibles en el ecosistema
Python sin fricción — y envíe a la API solo los datos ya parseados,
fila por fila.

**Decisión: (b).** Python interpreta hojas de cálculo de forma más
natural que C# sin un paquete adicional, y la API se mantiene fiel al
Artículo 2 (solo `Npgsql`, `Dapper`, `Swashbuckle`). El front hace, por
cada fila del archivo: `fn_resolver_programa` (vía un endpoint de
`ProgramaAcademico`/`AliasPrograma` o, más simple, resolviendo en
Python contra el catálogo ya cargado) para emparejar el programa en
texto libre con un `codigo`, y luego `POST /api/Asistente` con lo que
logró resolver; lo que no logra resolver, lo manda igual (sin esos
campos) y deja que la API lo rechace con un motivo claro para
`novedad_carga`.

**Consecuencias.** (+) Cero paquetes nuevos en la API. (+) El front ya
tenía, desde el mapa de versiones, la carga de datos asignada a v3.
(−) La lógica de "qué fila se acepta" queda repartida entre Python (el
parseo) y C#/SQL (la validación final) — aceptado porque la VALIDACIÓN
de negocio sigue centralizada en la API/BD (el front nunca decide por
su cuenta que una fila es válida, solo la envía y reporta lo que la API
respondió).

### 4.5 FK hacia tablas que ya existen: no se revalida (D9, sin cambios)

`documento_asistente.fk_tipo_documento`, `vinculacion_asistente.fk_tipo_vinculacion`,
`programa_asistente.fk_programa`/`fk_periodo` — igual que siempre, un id
inexistente lo rechaza el motor (500), la API no revalida.

## 5. Frontend: pantalla de carga de datos

```python
# routes/carga.py (forma; el código final además: lee el archivo COMPLETO antes
# de abrir el lote, detiene la carga si la API deja de responder, y numera
# las filas como en el archivo — el encabezado es la fila 1)
# servicios/carga_asistentes.py interpreta el archivo subido (openpyxl)
# y por cada fila llama a servicios/api_catedras.py (POST /api/Asistente)
lote = crear('LoteCargaAsistente', {'nombre_archivo': archivo.filename})
aceptadas, rechazadas, novedades = 0, 0, []
for numero_fila, fila in enumerate(leer_filas(archivo), start=1):
    datos = construir_asistente(fila, fk_lote_carga=lote['id_lote_carga'])
    try:
        crear('Asistente', datos)
        aceptadas += 1
    except ErrorApi as ex:
        rechazadas += 1
        novedades.append({'numero_fila': numero_fila, 'contenido_crudo': str(fila), 'motivo': ex.mensaje})
crear(f"LoteCargaAsistente/{lote['id_lote_carga']}/cerrar", {
    'filas_leidas': aceptadas + rechazadas, 'filas_aceptadas': aceptadas,
    'filas_rechazadas': rechazadas, 'novedades': novedades})
```

## 6. Docker: sin cambios de infraestructura

Sigue siendo `postgres` + `api`; se agrega una migración más
(`002_sp_guardar_asistente.sql` y `003_sp_guardar_lote_carga.sql`,
montadas con prefijo `z002_`/`z003_` para respetar el orden alfabético
después de `script_bdcatedras.sql` — ver [6_contracts.md](6_contracts.md)
§3).
