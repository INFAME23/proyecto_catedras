# Guía IA — Versión 4: sesiones y ponencias

> **Qué es este documento.** La guía operativa para construir, revisar o
> verificar la **v4** con un asistente de IA (Claude, Copilot, ChatGPT…),
> siguiendo el spec kit de esta versión. **No crea reglas nuevas**: aterriza
> a la v4 las que ya existen.
>
> **Jerarquía ante un conflicto:**
> [constitución](../../1_constitution.md) › [guía IA general](../../guia_ia.md)
> (sus 3 reglas) › spec kit v4 (`2_spec.md` … `8_tasks.md`) › esta guía.
> Si esta guía contradice al spec kit, gana el spec kit y se corrige esta
> guía.
>
> **Estado de la v4 (2026-10-08):** código completo; SQL y front
> verificados contra la BD real. Falta la Fase 37: correr todo en Docker y
> poner el tag `v4`.

---

## 0. Cómo usar esta guía

1. Abre una conversación nueva con tu asistente y pega el **prompt de
   arranque** (§1). Adjunta o da acceso al repositorio.
2. Trabaja **una fase a la vez** (§8). Cada fase trae un prompt listo para
   copiar, la lista de "terminado cuando…" y los errores típicos.
3. No marques una casilla de `8_tasks.md` hasta que la verificación de la
   fase pase **en tu máquina** (Artículo 1). La IA puede escribir código;
   quien verifica eres tú.
4. Si la IA propone algo de la lista de §5 o §9 ("lo que la IA tiende a
   arreglar mal"), recházalo citando el ADR.

## 1. Prompt de arranque (cópialo tal cual)

```text
Vas a trabajar en el Proyecto Cátedras, VERSIÓN 4 (sesiones y ponencias).
Es material de estudio: todo en español y con comentarios línea a línea.

Antes de escribir código, lee EN ESTE ORDEN y confirma con un resumen de
5 líneas que lo entendiste:
  1. docs/spec_kit/1_constitution.md          (reglas permanentes, ganan siempre)
  2. docs/spec_kit/guia_ia.md                 (Reglas 1-3: SP maestro-detalle, FK=select, BCrypt)
  3. docs/spec_kit/Versiones/0_mapa_versiones.md (qué tablas puede nombrar cada versión)
  4. docs/spec_kit/Versiones/version4/2_spec.md → 8_tasks.md (en orden numérico)
  5. docs/spec_kit/Versiones/version4/guia_ia_v4.md (esta guía)

Reglas que no se negocian en la v4:
  - Solo puedes nombrar tablas de v1-v4. sesion y ponencia son las nuevas.
    usuario, enlace_registro, registro_asistencia, lote_migracion... son de
    v5/v7: NO se consultan ni se exponen, aunque la BD ya las tenga.
  - C# + ASP.NET Core (.NET 10) + Dapper, SQL a mano y SIEMPRE con @parametros.
    Sin Entity Framework, sin paquetes NuGet nuevos, sin SP nuevo.
  - Capas: Controller -> IServicio -> IRepositorio -> RepositorioXPostgres.
    Solo Program.cs conoce clases concretas.
  - numero_reunion lo calcula el TRIGGER de la BD (ADR D20). Prohibido
    calcularlo en C#.
  - Ponencia: llave compuesta SIN vigencia (ADR D19): GET lista, GET puntual
    de 3 segmentos, POST y DELETE. Sin PUT, sin PATCH, sin "/cerrar".
  - inicio/fin: la petición acepta DateTimeOffset con zona; el modelo guarda
    DateTime en UTC (ADR D21). Nunca DateTimeOffset en el modelo.
  - Contratos exactos de 6_contracts.md (sobres, códigos 400/404/422/500).

Trabajaremos fase por fase (Fases 32-37 de 8_tasks.md). No avances a la
siguiente fase hasta que te confirme que la verificación pasó.
```

## 2. Orden de lectura y qué sacar de cada documento

| # | Documento | Qué debe extraer la IA |
|---|---|---|
| 1 | `1_constitution.md` | Art. 1 (por versiones, YAGNI), Art. 2 (Dapper, SQL a mano, 3 paquetes), Art. 3 (capas e interfaces), Art. 6 (español, comentarios línea a línea), Art. 7 (contratos exactos), Art. 8 (puertos, sobres, códigos) |
| 2 | `guia_ia.md` | Regla 1 **no aplica** a la v4 (no hay maestro-detalle); Regla 2 **sí** (FK = `<select>`); Regla 3 todavía no (llega con `usuario`, v5) |
| 3 | `0_mapa_versiones.md` | Fila V4: `sesion` → `catedra`(v2), `encuesta`, `modalidad`, `periodo_academico`, `sede`(v1); `ponencia` → `sesion`, `asistente`(v3). Front v4: sesiones, detalle de sesión, calendario del ponente |
| 4 | `2_spec.md` | RF119-RF131, RNF10-RNF11 y los **9 criterios de aceptación** (son la definición de "terminado") |
| 5 | `3_plan.md` | Estructura de archivos, SQL exacto del `INSERT … RETURNING`, interfaz de 4 métodos de `IRepositorioPonencia`, tabla de pantallas del front |
| 6 | `4_research.md` | ADR D19 (Ponencia ≠ D16), D20 (trigger), D21 (UTC) — el **porqué** de lo que no se debe "mejorar" |
| 7 | `5_data_model.md` | Columnas, `CHECK`, FK entrantes/salientes y **los datos de ejemplo útiles para probar** |
| 8 | `6_contracts.md` | Formatos de petición/respuesta, rutas de 3 segmentos, contrato del front |
| 9 | `7_quickstart.md` | Smoke test de 8 pasos + front + problemas frecuentes |
| 10 | `8_tasks.md` | Fases 32-37: qué está hecho, qué se verificó y qué falta |

## 3. La v4 en una página

- **Qué agrega:** 2 tablas (`sesion`, `ponencia`), 13 RF (RF119-RF131),
  2 RNF (RNF10, RNF11), 3 ADR (D19, D20, D21). La API pasa de 22 a **24**
  recursos.
- **Qué NO agrega:** ningún SP ni migración (`db/postgres/migraciones/`
  no cambia), ningún paquete NuGet ni de Python, ningún servicio nuevo en
  `docker-compose.yml`.
- **Lo que enseña:** una regla de negocio que vive en la BD (trigger), la
  primera fecha con hora que la API escribe, una segunda forma de
  resolver "tabla sin `id`" (D19 frente a D16), y una FK `RESTRICT` desde
  tablas que la API ni siquiera puede nombrar todavía.

```
catedra (v2) ──< sesion >── modalidad, sede, periodo_academico, encuesta (v1)
                   │
                   └──< ponencia >── asistente (v3)

ponencia: PK (fk_sesion, fk_asistente, rol)   — sin columna id
sesion:   trigger BEFORE INSERT -> numero_reunion = MAX + 1 por cátedra
```

## 4. Reglas permanentes aterrizadas a la v4

| Regla | En la v4 significa… |
|---|---|
| Art. 1 — no anticipar | Nada de `usuario`, login, QR ni encuestas respondidas. El calendario del ponente **elige** a la persona (búsqueda) porque el login llega en la v5 |
| Art. 2 — SQL a mano | Todo `SELECT/INSERT/UPDATE/DELETE` escrito en el repositorio, con prefijo `catedras.` y `@parametros`. Filtros opcionales con `@x::bigint IS NULL OR col = @x` |
| Art. 3 — capas | La regla `fin > inicio` va en el **servicio**; la conversión a UTC, en el servicio (o en `ACamposEnviados()` del PATCH); el `RETURNING`, en el repositorio |
| Art. 5 — BD dada | `sesion` (5714 filas) y `ponencia` (0 filas) ya existen. No se toca el script ni se crean tablas |
| Art. 6 — comentarios | Cada archivo nuevo lleva cabecera con la definición real de la tabla y comentarios que expliquen el *porqué* (D19/D20/D21) |
| Art. 7 — contratos | `POST /api/Sesion` responde `{estado, mensaje, id_sesion, numero_reunion, creado_en}`; `DELETE /api/Ponencia/…` responde `{estado, mensaje, filasEliminadas}` |
| Art. 8 — códigos | 422 forma de la petición · 400 `fin <= inicio`, body de PATCH vacío, `limite <= 0` · 404 no existe (incluida la llave de 3 columnas) · 500 FK inexistente, PK duplicada, `uq_sesion_reunion`, `RESTRICT` |
| Guía IA, Regla 1 | No aplica: `sesion` y `ponencia` se crean en peticiones distintas |
| Guía IA, Regla 2 | `fk_catedra` lo fija la URL; `fk_asistente` sale de un `<select>` llenado por búsqueda; modalidad/sede/periodo/encuesta, de `<select>` con el catálogo |

## 5. Las tres decisiones que la IA tiende a "arreglar" mal

### D20 — El número de reunión lo pone la BD

```csharp
// ✗ MAL: duplica la regla y tiene condición de carrera (dos altas
//   simultáneas leen el mismo MAX antes de que cualquiera inserte).
var siguiente = await conexion.ExecuteScalarAsync<short>(
    "SELECT COALESCE(MAX(numero_reunion),0)+1 FROM catedras.sesion WHERE fk_catedra=@FkCatedra", sesion);
sesion.NumeroReunion = siguiente;

// ✓ BIEN: el servicio pasa 0 y el trigger (con FOR UPDATE) pone el número.
NumeroReunion = peticion.NumeroReunion ?? 0,
// ...y el repositorio lee el valor DEFINITIVO:
//   INSERT ... RETURNING id_sesion, numero_reunion, creado_en;
```

Consecuencias que la IA debe conocer: el trigger es `BEFORE INSERT`, **no
corre en un `UPDATE`** (por eso PUT/PATCH exigen `numero_reunion >= 1`), y
es `MAX + 1`: **no rellena huecos** (la cátedra 3975 no tiene la 23 y un
alta automática allí recibe el 61).

### D19 — `Ponencia` no es `VinculacionAsistente`

| | `VinculacionAsistente` (v3, D16) | `Ponencia` (v4, D19) |
|---|---|---|
| ¿Tiene `id`? | No | No |
| ¿Modela un intervalo de tiempo? | Sí (`fecha_ini`/`fecha_fin`) | No: existe o no existe |
| ¿Se borra? | **No**: se cierra (`PATCH /cerrar`) | **Sí**: `DELETE /{s}/{a}/{rol}` |
| ¿Dónde va la llave? | En el body del `PATCH` | En la **ruta**, 3 segmentos |
| ¿Se edita? | No | No (cambiar rol = quitar + asignar) |

```csharp
// ✗ MAL: copiar el molde de la v3 por costumbre.
[HttpPatch("retirar")] public Task<IActionResult> Retirar([FromBody] PonenciaRetirar p) ...
[HttpPut("{id}")]      public Task<IActionResult> Reemplazar(...)

// ✓ BIEN: exactamente 4 endpoints.
[HttpGet]                                              // ?fkSesion=&fkAsistente=
[HttpGet("{fkSesion:long}/{fkAsistente:long}/{rol}")]
[HttpPost]
[HttpDelete("{fkSesion:long}/{fkAsistente:long}/{rol}")]
```

### D21 — Zona en la entrada, UTC adentro

```csharp
// ✗ MAL: Npgsql 9 rechaza escribir un DateTimeOffset con desfase ≠ 0 en
//   timestamptz: "Cannot write DateTimeOffset with Offset=-05:00:00 ...
//   only offset 0 (UTC) is supported".
public required DateTimeOffset Inicio { get; set; }   // en Modelos/Sesion.cs

// ✓ BIEN: la PETICIÓN acepta la zona; el SERVICIO convierte; el MODELO es UTC.
public DateTimeOffset? Inicio { get; set; }            // Peticiones/SesionCrear.cs
DateTime inicio = peticion.Inicio!.Value.UtcDateTime;   // Servicios/ServicioSesion.cs
public required DateTime Inicio { get; set; }          // Modelos/Sesion.cs (Kind = Utc)
```

Por qué funciona (verificado en el código fuente de Dapper 2.1.66 y Npgsql
9.0.3): Dapper no fija `DbType` para `DateTime`, y Npgsql infiere
`timestamptz` cuando `Kind = Utc`. El front convierte a hora de Colombia
al mostrar (`servicios/fechas.py`, UTC-5 fijo: Colombia no tiene horario
de verano).

### RNF10 en una línea

Si la BD ya garantiza algo (FK, `CHECK`, trigger), el C# **no** lo
revalida ni lo recalcula; solo se adelanta una regla en el servicio cuando
cruza campos y merece un 400 claro (`fin > inicio`).

## 6. Mapa de archivos de la v4

| Capa | Archivo | Nota |
|---|---|---|
| Modelo | `api_catedras/Modelos/Sesion.cs` | 16 propiedades; `Inicio`/`Fin`/`CreadoEn` `DateTime` UTC |
| Modelo | `api_catedras/Modelos/Ponencia.cs` | 3 propiedades, **sin `Id`** |
| Petición | `Peticiones/SesionCrear.cs` | `numero_reunion` `[Range(0, …)]` (0 = automático) |
| Petición | `Peticiones/SesionReemplazo.cs` / `SesionActualizar.cs` | `numero_reunion` `[Range(1, …)]`; PATCH convierte a UTC en `ACamposEnviados()` |
| Petición | `Peticiones/PonenciaCrear.cs` | **Única** petición de Ponencia |
| Repositorio | `Repositorios/IRepositorioSesion.cs` + `RepositorioSesionPostgres.cs` | `CrearAsync` → tupla `(IdSesion, NumeroReunion, CreadoEn)` |
| Repositorio | `Repositorios/IRepositorioPonencia.cs` + `RepositorioPonenciaPostgres.cs` | 4 métodos: `ObtenerTodosAsync`, `ObtenerUnoAsync`, `CrearAsync`, `EliminarAsync` |
| Servicio | `Servicios/IServicioSesion.cs` + `ServicioSesion.cs` | `ValidarRango` (RF125) en POST/PUT/PATCH |
| Servicio | `Servicios/IServicioPonencia.cs` + `ServicioPonencia.cs` | Rol por defecto `PONENTE` |
| Controlador | `Controllers/SesionController.cs` | 6 endpoints, `?fkCatedra=` |
| Controlador | `Controllers/PonenciaController.cs` | 4 endpoints, rutas de 3 segmentos |
| Ensamblador | `Program.cs` | 2 parejas nuevas; diagnóstico `version = "v4"` |
| Pruebas | `pruebas/Programa.cs` | Secciones v4 (21 + 15 verificaciones) y repos falsos `RepositorioSesionFalso` / `RepositorioPonenciaFalso` |
| Front | `routes/sesiones.py`, `templates/detalle_sesion.html` | `/sesiones/<id>`: datos + ponentes + asignar/quitar |
| Front | `routes/ponente.py`, `templates/ponente_calendario.html` | `/ponente/`: calendario de una persona |
| Front | `routes/catedras.py`, `templates/detalle_catedra.html` | Calendario de la cátedra |
| Front | `routes/admin.py`, `templates/admin/sesion_*.html`, `ponencia_lista.html`, `panel.html`, `catedra_lista.html` | CRUD de Sesión, asignar/quitar Ponencia, botón "Sesiones" por cátedra |
| Front | `servicios/fechas.py`, `servicios/api_catedras.py` (`llave()`), `templates/partials/macros_sesion.html` | Piezas compartidas |

## 7. Datos de ejemplo verificados contra la BD (2026-10-08)

| Para probar… | Usa | Resultado esperado |
|---|---|---|
| Listado filtrado (criterio 2) | `?fkCatedra=3975` | 59 filas, `numero_reunion` 1-60, **sin la 23** |
| Numeración automática (criterio 3) | `fk_catedra` 5498 (también 5500, 5502, 5503, 5504: sin sesiones) | 1, luego 2 |
| `MAX + 1` no rellena huecos | `fk_catedra` 3975 sin número | 61 |
| `DELETE` limpio (criterio 6) | `id_sesion` 1-5 | 200 |
| `DELETE` bloqueado por v5/v7 (criterio 6) | `id_sesion` **5712** (168 `registro_asistencia`, 5 `lote_migracion`) | 500, la sesión sigue |
| `RESTRICT` desde `ponencia` (RF131) | asistente **167** (sin registros ni usuario) | 500 `fk_ponencia_asistente` |
| ⚠ No usar para RF131 | asistente 1 | Falla igual sin ponencia (ya tiene `registro_asistencia`) |
| Buscar persona en el front | texto `1013032` | asistente 167 en el `<select>` |
| Sesiones con encuesta | 5712, 5713, 5714 | `fk_encuesta: 1` (las otras 5711: `null`) |
| Catálogos v1 | modalidad `PRESENCIAL`, sede `SAN_BENITO`, periodo `2026-2` | Existen |

Después de cualquier prueba que modifique datos:
`docker compose down -v && docker compose up -d --build`.

## 8. Fases con prompts listos

> El código de las Fases 32-34 y 36 **ya está escrito**. Si reconstruyes
> la versión desde cero (p. ej. como ejercicio), usa los prompts tal cual;
> si solo verificas, salta a la sección "Terminado cuando…" de cada fase.

### Fase 32 — Capas de `Sesion`

```text
Fase 32 de docs/spec_kit/Versiones/version4/8_tasks.md (RF119-RF126).
Crea Modelos/Sesion.cs, Peticiones/Sesion{Crear,Reemplazo,Actualizar}.cs,
IRepositorioSesion + RepositorioSesionPostgres, IServicioSesion + ServicioSesion,
SesionController y registra la pareja en Program.cs.
Molde: copia la FORMA de Catedra (v2) y el filtro opcional de
VinculacionAsistente (v3). Respeta D20 (sin calcular numero_reunion) y D21
(DateTimeOffset? en peticiones, DateTime UTC en el modelo).
"fin > inicio" en el servicio, también en PATCH combinando con la fila guardada.
Comentarios en español, línea a línea, con la definición real de la tabla
en la cabecera del modelo.
```

**Terminado cuando:** `GET /api/Sesion?fkCatedra=3975` → 59 filas;
`POST` sin número en 5498 → `numero_reunion: 1` y luego `2`; `POST` con
`fin` antes de `inicio` → 400 y `GET ?fkCatedra=5498` sigue en 2; un
`fk_modalidad` inexistente → 500; `PUT` con `numero_reunion: 0` → 422.

**Errores típicos:** usar `DateTimeOffset` en el modelo (500 de Npgsql);
olvidar el prefijo `catedras.`; olvidar `::bigint` en `@fkCatedra IS
NULL` (PostgreSQL no sabe el tipo de un NULL); no registrar la pareja en
`Program.cs` (500 "Unable to resolve service").

### Fase 33 — Capas de `Ponencia`

```text
Fase 33 (RF127-RF131, ADR D19). Lee D19 ANTES de empezar.
Ponencia es llave compuesta SIN vigencia: 4 endpoints (GET lista con
fkSesion/fkAsistente opcionales, GET y DELETE con ruta
{fkSesion:long}/{fkAsistente:long}/{rol}, POST). Sin PUT, PATCH ni /cerrar.
IRepositorioPonencia con EXACTAMENTE 4 métodos (ObtenerTodosAsync,
ObtenerUnoAsync, CrearAsync, EliminarAsync). PonenciaCrear es la única
petición; rol opcional (PONENTE por defecto) con [RegularExpression].
```

**Terminado cuando:** `POST` → 200; misma llave → 500; otro rol → 200;
`GET /api/Ponencia/{s}/{a}/PONENTE` → 200; `DELETE` misma ruta →
`filasEliminadas: 1`; repetir `GET` → 404; `GET /api/Ponencia/3` → 404.

**Errores típicos:** agregar `Id` al modelo; un `ActualizarAsync` "por si
acaso"; mandar la llave en el body; capturar la PK duplicada y devolver
409 (el contrato dice 500, D9).

### Fase 34 — Prueba de capas

```text
Fase 34. Agrega a pruebas/Programa.cs (top-level statements, sin librerías)
las secciones de ServicioSesion y ServicioPonencia con repositorios FALSOS
en memoria. El falso de Sesion simula el trigger (0 -> MAX+1) SOLO dentro
del falso y registra el último numero_reunion recibido, para comprobar que
el servicio le pasó 0. Prueba también: UTC, fin <= inicio sin llamar al
repositorio, PATCH con una sola fecha, y por reflexión que IRepositorioPonencia
no declara ObtenerPorIdAsync/ActualizarAsync pero sí EliminarAsync.
Cuidado: todas las variables top-level comparten ámbito; usa nombres nuevos.
```

**Terminado cuando:** `docker compose exec api dotnet run --project
pruebas` termina con `256 pasadas, 0 fallidas` y código 0.

### Fase 35 — Integridad referencial (sin código nuevo)

```text
Fase 35. No escribas código: dame la secuencia de llamadas HTTP (curl o
PowerShell Invoke-RestMethod) para los 6 pasos de 8_tasks.md, usando los
datos de §7 de guia_ia_v4.md (5498, 5712, 167), y qué respuesta exacta
esperar en cada uno.
```

**Terminado cuando:** los 6 resultados coinciden con
`5_data_model.md`; luego `docker compose down -v && docker compose up -d
--build`.

### Fase 36 — Front

```text
Fase 36 (criterios 8 y 9, §5 de 3_plan.md). Flask + Jinja + Bootstrap,
mismo estilo que las plantillas existentes. Sin datos mock: si la API
falla, se muestra el error. Regla 2: fk_catedra fijo por URL; fk_asistente
por búsqueda (?q=) + <select>; catálogos v1 en <select>. Horas: enviar con
-05:00 y mostrar en hora de Colombia (servicios/fechas.py). Ponencia sin
"editar": asignar y quitar con la llave completa.
```

**Terminado cuando:** `/catedras/3975` muestra 59 sesiones en hora local;
`/sesiones/1` permite buscar → asignar → ver el error del duplicado →
otro rol → quitar; `/ponente/?asistente=167` muestra su calendario;
`/admin/` tiene la sección v4; con `docker compose stop api` cada pantalla
nueva muestra "No se pudo contactar la API".

### Fase 37 — Cierre

```text
Fase 37. Con el entorno recién reseteado, acompáñame a correr los 9
criterios de aceptación de 2_spec.md §5 en orden. Para cada uno dime el
comando, el resultado esperado y qué revisar si no coincide. Al final
genera el texto del commit (en español) y recuérdame el tag v4. No
escribas nada de la v5 todavía (Artículo 1).
```

**Terminado cuando:** los 9 criterios pasan, el total de la prueba de
capas queda anotado en `8_tasks.md`, todas las casillas están marcadas y
existe el tag `v4`.

## 9. Trampas conocidas y diagnóstico

| Síntoma | Causa | Arreglo |
|---|---|---|
| `Cannot write DateTimeOffset with Offset=-05:00:00` | Modelo con `DateTimeOffset` | `DateTime` UTC en el modelo (D21) |
| `Cannot write DateTime with Kind=Unspecified to … timestamp with time zone` | Fecha sin pasar por `.UtcDateTime` | Convertir en el servicio / `ACamposEnviados()` |
| `PUT`/`PATCH` con `numero_reunion: 0` → 500 `chk_sesion_reunion` | El trigger solo corre en `INSERT` | `[Range(1, …)]` en `SesionReemplazo`/`SesionActualizar` → 422 |
| "Falta la reunión 23 de la cátedra 3975" | Dato de ejemplo | Correcto; no "arreglar" |
| `DELETE /api/Sesion/{id}` → 500 | `RESTRICT` desde `registro_asistencia`/`lote_migracion` (v5/v7) | Correcto; usar 1-5 o una sesión creada en la prueba |
| `DELETE /api/Asistente/1` → 500 aunque se borró su ponencia | Tiene `registro_asistencia` | Usar el 167 para RF131 |
| `GET /api/Ponencia/3` → 404 | No hay ruta de 1 segmento | Correcto (D19) |
| Las horas de la API salen 5 h "corridas" | Respuesta en UTC | Correcto; el front convierte |
| El `<select>` de personas sale vacío | No se buscó nada (`?q=`) | Correcto: nunca se descargan las 14.811 personas |
| `operator does not exist: bigint = text` o "could not determine data type of parameter" | Falta `::bigint` en el filtro opcional | `@fkSesion::bigint IS NULL OR …` |
| La prueba de capas no compila: "A local variable named 'x' is already defined" | Nombre repetido en top-level | Renombrar (todas comparten ámbito) |

## 10. Lista de revisión para cualquier cambio de la v4

Pídele a la IA que revise el diff contra esta lista (o revísala tú):

- [ ] Solo nombra tablas de v1-v4 (Art. 1, 5).
- [ ] SQL a mano, con prefijo `catedras.` y `@parametros`; los nombres de
      columna que se interpolan salen de una **lista blanca** (Art. 2).
- [ ] Ningún `SELECT MAX(numero_reunion)` en C# (D20, RNF10).
- [ ] `Ponencia`: 4 endpoints, 4 métodos de repositorio, 1 petición, sin
      `Id` (D19).
- [ ] Fechas: `DateTimeOffset?` solo en peticiones; `DateTime` UTC en
      modelo y repositorio (D21).
- [ ] `fin > inicio` en el servicio, en POST/PUT/PATCH (RF125).
- [ ] Errores: 422 forma · 400 negocio · 404 no existe · 500 motor (Art. 8).
- [ ] Front: FK por URL o `<select>` (Regla 2), sin mock, horas en hora de
      Colombia.
- [ ] Comentarios en español que explican el porqué (Art. 6).
- [ ] `8_tasks.md` actualizado con lo verificado **y dónde**.

## 11. Lo que la v4 le deja a la v5 (no construir todavía)

- `auth_helpers.py` sigue respondiendo "sin sesión"; en la v5 el
  calendario del ponente tomará el asistente del usuario autenticado y la
  búsqueda desaparecerá.
- `enlace_registro` y `registro_asistencia` se colgarán de `sesion`
  (QR/enlace y registro de asistencia), con `estado` de la sesión
  (`ABIERTA`/`CERRADA`) como control.
- La Regla 3 (BCrypt) entra con `usuario.clave`.
- La spec de la v5 se escribe **después** del tag `v4` (Artículo 1).
