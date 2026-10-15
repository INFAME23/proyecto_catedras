# Guía IA — Versión 2: estructura académica y catálogo de cátedras

> **Qué es este documento.** La guía operativa para construir, revisar o
> verificar la **v2** con un asistente de IA (Claude, Copilot, ChatGPT…),
> siguiendo el spec kit de esta versión. **No crea reglas nuevas**: aterriza
> a la v2 las que ya existen.
>
> **Jerarquía ante un conflicto:**
> [constitución](../../1_constitution.md) › [guía IA general](../../guia_ia.md)
> (sus 3 reglas) › spec kit v2 (`2_spec.md` … `8_tasks.md`) › esta guía.
> Si esta guía contradice al spec kit, gana el spec kit y se corrige esta
> guía.
>
> **Estado de la v2 (2026-10-15):** código completo (Fases 14-20b marcadas
> en `8_tasks.md`). **Fase 21 (cierre) sigue sin marcar** — los 11 criterios
> sobre un entorno limpio y el tag `v2` están pendientes en el documento,
> aunque el spec de la v3 ya habla de la v2 como etiquetada. Además, el
> código actual **ya pasó por el retrofit de procedimientos almacenados** y
> **`AliasPrograma` hoy tiene `PUT` y `PATCH`** aunque el ADR D12 dice que
> no (ver §5.bis). Esta guía te dice cuándo usar cada foto (§0, modos A y B).

---

## 0. Cómo usar esta guía

1. **Elige el modo** (es lo primero que debes decirle a la IA):

   | Modo | Cuándo | Qué significa |
   |---|---|---|
   | **A — Reconstruir "como se construyó"** | Ejercicio de estudio: rehacer la v2 desde cero siguiendo el spec kit | SQL a mano en el repositorio con `@parametros`; `AliasPrograma` con 4 verbos (D12); borrado físico; maestro-detalle por la migración `001` (Fase 20b) |
   | **B — Trabajar sobre el código de hoy** | Revisar, arreglar o ampliar el repositorio actual | Repositorios con `CALL catedras.sp_<verbo>_<tabla>(…)` (migraciones 006-010); borrado **lógico** en 4 de las 5 tablas; `AliasPrograma` con 6 verbos |

2. Abre una conversación nueva y pega el **prompt de arranque** (§1).
   Adjunta o da acceso al repositorio.
3. Trabaja **una fase a la vez** (§8). Cada fase trae un prompt, la lista de
   "terminado cuando…" y los errores típicos.
4. No marques una casilla de `8_tasks.md` hasta que la verificación pase **en
   tu máquina** (Artículo 1). La IA escribe código; quien verifica eres tú.
5. Si la IA propone algo de §5 o §9, recházalo citando el ADR.
6. **Dos documentos que se contradicen** (D12 vs el controlador, el criterio 7
   vs el borrado lógico…): la IA debe **reportarlo y preguntarte**, no
   resolverlo en silencio.

## 1. Prompt de arranque (cópialo tal cual)

```text
Vas a trabajar en el Proyecto Cátedras, VERSIÓN 2 (estructura académica y
catálogo de cátedras). Es material de estudio: todo en español y con
comentarios línea a línea.

MODO: [ A = reconstruir como se construyó | B = trabajar sobre el código de hoy ]

Antes de escribir código, lee EN ESTE ORDEN y confirma con un resumen de
5 líneas que lo entendiste:
  1. docs/spec_kit/1_constitution.md          (reglas permanentes, ganan siempre)
  2. docs/spec_kit/guia_ia.md                 (Reglas 1-3: SP maestro-detalle, FK=select, BCrypt)
  3. docs/spec_kit/Versiones/0_mapa_versiones.md (qué tablas puede nombrar cada versión)
  4. docs/spec_kit/Versiones/version2/2_spec.md → 8_tasks.md (en orden numérico)
  5. docs/spec_kit/Versiones/version2/guia_ia_v2.md (esta guía)
  6. SOLO en modo B: docs/spec_kit/Versiones/PLAN_RETROFIT_SP.md y
     db/postgres/migraciones/005_sp_sede_ejemplo.sql (el molde de los SP)

Reglas que no se negocian en la v2:
  - Solo puedes nombrar tablas de v1-v2. Las nuevas son facultad, dependencia,
    programa_academico, alias_programa y catedra. sesion, ponencia, asistente,
    usuario... son de v3+ y NO se consultan ni se exponen, aunque la BD ya las
    tenga (y bloqueen borrados).
  - C# + ASP.NET Core (.NET 10) + Dapper, 3 paquetes NuGet, sin Entity
    Framework. Modo A: SQL a mano con @parametros. Modo B: el repositorio
    solo hace CALL.
  - Capas: Controller -> IServicio -> IRepositorio -> RepositorioXPostgres.
    Solo Program.cs conoce clases concretas.
  - FK hacia tablas existentes: NO se revalida; la BD es la autoridad y el
    error sube como 500 (ADR D9).
  - Autorreferencia (facultad y dependencia): el servicio valida UN nivel,
    padre != id (ADR D10). No hagas recursión.
  - Catedra.nombre_asis es columna GENERADA: la calcula la BD, nunca el C#
    (ADR D11); ninguna petición la declara.
  - Catálogo público: una sola llamada a la API, filtro y paginación en Flask
    (ADR D13). No agregues offset ni endpoints a la API.
  - Maestro-detalle ProgramaAcademico + alias: UN procedimiento almacenado
    explícito (migración 001, ADR D14). Sin SQL dinámico.
  - Contratos exactos de 2_spec.md/6_contracts.md (sobres, códigos
    422/400/404/500).

Trabajaremos fase por fase (Fases 14-21 de 8_tasks.md). No avances a la
siguiente fase hasta que te confirme que la verificación pasó.
```

## 2. Orden de lectura y qué sacar de cada documento

| # | Documento | Qué debe extraer la IA |
|---|---|---|
| 1 | `1_constitution.md` | Art. 1 (por versiones), Art. 2 (Dapper, SQL a mano), Art. 3 (capas), Art. 5 (BD dada: la integridad referencial es de la BD), Art. 6 (español), Art. 7 (contratos no se amplían a mitad de versión), Art. 8 (códigos) |
| 2 | `guia_ia.md` | **Regla 1 sí** (maestro-detalle programa+alias → SP); **Regla 2 sí** (FK = `<select>`: facultad, dependencia, programa, tipo de evento); Regla 3 todavía no |
| 3 | `0_mapa_versiones.md` | Fila V2: 5 tablas y sus FK; front: catálogo público + administración |
| 4 | `2_spec.md` | RF62-RF94, RNF7 y los **11 criterios de aceptación** (el 11 es el del retrofit) |
| 5 | `3_plan.md` | §4.2 autorreferencia (código exacto) · §4.3 `RETURNING` con columna generada · §4.4 `AliasPrograma` · §4.6 integración de la plantilla · §4.7 paginación en Flask · §4.8 maestro-detalle |
| 6 | `4_research.md` | ADR D9 (FK no se revalida), D10 (un nivel), D11 (columna generada), D12 (alias de solo-anexo), D13 (paginación), D14 (SP maestro-detalle) |
| 7 | `5_data_model.md` | Columnas, `CHECK`, FK entrantes y salientes con su `ON DELETE` y los efectos reales sobre los datos de ejemplo |
| 8 | `6_contracts.md` | Contratos de la API de las 5 tablas y del front |
| 9 | `7_quickstart.md` | Smoke test, cómo aplicar la migración `001` a una BD existente |
| 10 | `8_tasks.md` | Fases 14-21 (con la 20b), qué se verificó y qué falta |

## 3. La v2 en una página

- **Qué agrega:** 5 tablas (`facultad`, `dependencia`, `programa_academico`,
  `alias_programa`, `catedra`), 33 RF (RF62-RF94), 1 RNF (RNF7), 6 ADR
  (D9-D14). La API pasa de 10 a **15** recursos; el panel admin, de 10 a 15
  tarjetas.
- **Qué NO agrega:** ninguna tabla de v3+, ningún paquete NuGet ni de Python,
  ningún servicio nuevo en `docker-compose.yml`, ningún endpoint de búsqueda
  ni paginación en la API.
- **Lo que enseña:** la **primera FK** (qué pasa con un padre inexistente, y
  qué efecto tiene `SET NULL`/`CASCADE`/`RESTRICT` en el `DELETE`), la
  **primera autorreferencia**, una **columna generada** devuelta con
  `RETURNING`, una tabla de **solo-anexo** (D12), el **primer maestro-detalle**
  con SP y el **primer catálogo paginado** en el front.

```
tipo_evento (v1) ──< catedra >── dependencia ─┐ (autorreferencia: fk_dependencia_padre)
                        │                      └──< dependencia
                        └ nombre_asis = LEFT(nombre, 30)  (GENERADA)

facultad ──< facultad            (autorreferencia: fk_facultad_padre)
   │
   └──< programa_academico ──< alias_programa
              (PK: codigo ^[A-Z][0-9A-Z]{3,4}$)    (maestro-detalle: SP de la Fase 20b)
```

## 4. Reglas permanentes aterrizadas a la v2

| Regla | En la v2 significa… |
|---|---|
| Art. 1 — no anticipar | Nada de `sesion`, `ponencia`, `asistente`. El detalle de la cátedra **no** muestra sesiones todavía (llegan en la v4) |
| Art. 2 — SQL a mano | (Modo A) todo en el repositorio, `catedras.` + `@parametros`, `RETURNING` en el `INSERT`. (Modo B) el SQL vive en las migraciones y el repositorio hace `CALL` |
| Art. 3 — capas | La regla `padre ≠ id` va en el **servicio**; el `RETURNING`, en el repositorio; la expresión regular de `codigo`/`nivel`/`id_evento_asis`, en la **petición** |
| Art. 5 — BD dada | Las 5 tablas (y 5502 cátedras de ejemplo) existen desde el script de la v1. Los `ON DELETE` son de la BD; **no se "arreglan" con código en la API** |
| Art. 6 — comentarios | Cada archivo nuevo con cabecera: definición real de la tabla y el porqué (D9-D14) |
| Art. 7 — contratos | `POST /api/Catedra` → `{estado, mensaje, id_catedra, nombre_asis}`; `POST /api/AliasPrograma` → `id_alias`; el contrato del catálogo no cambia |
| Art. 8 — códigos | 422 forma (`codigo` en minúscula, `nivel` fuera de lista, `id_evento_asis` ≠ 9 dígitos) · 400 autorreferencia, límite ≤ 0, PATCH vacío · 404 no existe · 500 FK inexistente, duplicado, `RESTRICT` |
| Guía IA, Regla 1 | **Aplica**: `ProgramaAcademicoCrear` acepta `alias[]` y todo ocurre en una transacción |
| Guía IA, Regla 2 | `fk_facultad`, `fk_facultad_padre`, `fk_dependencia`, `fk_dependencia_padre`, `fk_programa`, `fk_tipo_evento` salen de un `<select>` llenado con una llamada previa a la API |
| Guía IA, Regla 3 | No aplica |

## 5. Las decisiones que la IA tiende a "arreglar" mal

### D9 — Una FK inexistente es un 500, no un 400

```csharp
// ✗ MAL: SELECT previo al padre "para dar un mensaje amable" (2 viajes, condición de carrera).
if (await _repoFacultad.ObtenerPorIdAsync(peticion.FkFacultad!) is null)
    throw new ArgumentException("La facultad no existe.");

// ✓ BIEN: el servicio no verifica nada que la FK ya verifica; sube la NpgsqlException → 500.
await _repositorio.CrearAsync(programa);
```

El mensaje del 500 es el texto crudo de Npgsql (`violates foreign key
constraint …`): costo aceptado (igual que un duplicado en la v1).

### D10 — Autorreferencia: un nivel, solo en el servicio

```csharp
// ✗ MAL: WITH RECURSIVE para detectar ciclos largos; o confiar solo en el CHECK de la BD
//   (el CHECK no da un 400 claro al crear una fila que se apunta a sí misma).
// ✓ BIEN (ServicioFacultad / ServicioDependencia):
private static void ValidarNoEsSuPropioPadre(string id, string? padre)
{
    if (padre is not null && string.Equals(padre, id, StringComparison.Ordinal))
        throw new ArgumentException("Una facultad no puede ser su propio padre ('fk_facultad_padre').");
}
```

Se llama en `CrearAsync`, `ReemplazarAsync` y `ActualizarAsync`. En PATCH, el
`id` sale de la **ruta**, así que la comparación es posible. Un ciclo de 2+
niveles creado editando el padre **no se detecta** — riesgo documentado, no un
olvido.

### D11 — La columna generada la calcula la BD

```csharp
// ✗ MAL: nombre.Length > 30 ? nombre[..30] : nombre   (duplica la regla y se desincroniza en silencio)
//        o declarar NombreAsis en CatedraCrear/Reemplazo/Actualizar.
// ✓ BIEN:
Task<(long IdCatedra, string NombreAsis)> CrearAsync(Catedra catedra);
// Modo A:  INSERT INTO catedras.catedra (...) VALUES (...) RETURNING id_catedra, nombre_asis;
// Modo B:  sp_crear_catedra devuelve la fila COMPLETA (id_catedra, nombre_asis, creado_en)
```

`IdCatedra`, `NombreAsis` y `CreadoEn` en el modelo van **sin `required`**. Si
el cliente manda `nombre_asis`, se ignora (la petición no tiene la propiedad).

### D12 — `AliasPrograma` de solo-anexo (¡y cuidado con el estado real!)

El ADR dice: 4 endpoints (`GET` lista, `GET/{id}`, `POST`, `DELETE`), sin
`PUT`/`PATCH`, sin `AliasProgramaReemplazo`/`Actualizar`, y
`IRepositorioAliasPrograma` **sin** `ActualizarAsync`. Un `PUT` debe dar
**405** del framework (la ruta existe, el verbo no).

```csharp
// ✗ MAL (modo A): "por consistencia" agregar [HttpPut]/[HttpPatch] y ActualizarAsync.
// ✓ BIEN (modo A): 4 endpoints. La ausencia ES la especificación.
```

> ⚠ **Conflicto doc ↔ código (reportar, no resolver).** El retrofit de
> `PLAN_RETROFIT_SP.md` convirtió esta tabla a 6 verbos
> (`AliasProgramaController` hoy declara `HttpPut` y `HttpPatch`, y hay
> `Peticiones/AliasProgramaReemplazo.cs` y `AliasProgramaActualizar.cs`). En
> **modo A** sigue D12; en **modo B** no borres ni "limpies" esos verbos sin
> preguntar: existen por el criterio 3 de `VERSION_2.md`. Si D12 debe
> reescribirse, es decisión del equipo.

### D13 — Paginación en Flask, no en la API

```python
# ✗ MAL: agregar ?offset= a GET /api/Catedra (cambia el contrato por una sola pantalla) o JavaScript nuevo.
# ✓ BIEN (front_catedras/routes/catedras.py): una llamada a /api/Catedra, filtrar con ?q=
#   en una comprensión de lista y cortar con slicing por ?pagina=.
POR_PAGINA = 24
```

Cada visita descarga las 5502 filas (~800 KB): costo aceptado.

### D14 / Regla 1 — Maestro-detalle con un SP explícito

```csharp
// ✗ MAL: crear el programa y luego N peticiones de alias (estado a medias); o un SP genérico con SQL dinámico.
// ✓ BIEN: ProgramaAcademicoCrear.alias[]  ->  CALL catedras.sp_guardar_programa_academico(
//          @Codigo, @Nombre, @FkFacultad, @Nivel, @Activo, CAST(@Alias AS jsonb));
```

`fk_programa` **no** viene en el JSON: el SP usa siempre el código del maestro
recién insertado. Un alias repetido (`uq_alias_texto`) revierte **todo**: el
programa no queda creado. Un `POST` sin `alias` (o `[]`) se comporta como
antes. En `Program.cs`, `LimpiarNombreCampo` deja el error como
`alias[0].texto_normalizado`. La migración `001` se monta en el compose con el
prefijo `z001_` para correr **después** del script provisto (orden
alfabético); una BD ya creada la necesita aplicada a mano una vez.

### 5.bis Retrofit de la v2 (solo modo B): qué cambió y qué no

| Spec original (modo A) | Código de hoy (modo B) |
|---|---|
| SQL a mano por repositorio | `CALL sp_{listar,consultar,crear,actualizar,eliminar}_<t>` (migraciones **006** facultad, **007** dependencia, **008** programa_academico, **009** alias_programa, **010** catedra) |
| `DELETE` físico → `SET NULL`/`CASCADE`/`RESTRICT` visibles | **Lógico** (`UPDATE … SET activo=false`) en `facultad`, `dependencia`, `programa_academico` y `catedra`; **físico** solo en `alias_programa` (sin `activo`) |
| Lista = todas las filas | Solo `activo = true`; `?incluirInactivos=true` trae todas |
| PATCH = `UPDATE` dinámico | consultar + mezclar + un solo `sp_actualizar_<t>` |
| `AliasPrograma` 4 verbos | 6 verbos (ver D12) |
| Maestro-detalle por `001` | Sigue siendo `001` (`sp_guardar_programa_academico`): no se reemplaza por `008` en el `POST` |
| Duplicado / FK → 500 | **Igual**. El 409 (criterio 7 de `VERSION_2.md`) **no está implementado**; no lo "arregles" aquí |

Consecuencia directa para las pruebas de borrado (§7): en modo B los `DELETE`
del criterio 7 devuelven 200 **sin** los efectos `SET NULL`/`500` del spec
original.

## 6. Mapa de archivos de la v2

| Capa | Archivo(s) | Nota |
|---|---|---|
| Modelo | `Modelos/{Facultad,Dependencia,ProgramaAcademico,AliasPrograma,Catedra}.cs` | `AliasPrograma.IdAlias`/`CreadoEn` y `Catedra.IdCatedra`/`NombreAsis`/`CreadoEn` sin `required` |
| Petición | `Peticiones/{Facultad,Dependencia,ProgramaAcademico,Catedra}{Crear,Reemplazo,Actualizar}.cs` | `ProgramaAcademicoCrear`: `codigo` `^[A-Z][0-9A-Z]{3,4}$`, `nivel` lista cerrada, `alias` opcional. `CatedraCrear`: `id_evento_asis` `^[0-9]{9}$`; sin `nombre_asis` |
| Petición | `Peticiones/AliasProgramaCrear.cs` (+ `AliasInicial.cs`) | Modo A: **única** petición del alias. Hoy también existen `AliasProgramaReemplazo`/`Actualizar` |
| Repositorio | `Repositorios/IRepositorio<Entidad>.cs` + `Repositorio<Entidad>Postgres.cs` | `CatedraCrear` → tupla; `ProgramaAcademico.CrearAsync(programa, aliasIniciales)` |
| Servicio | `Servicios/IServicio<Entidad>.cs` + `Servicio<Entidad>.cs` | `ValidarNoEsSuPropioPadre` en Facultad/Dependencia; `ServicioProgramaAcademico` convierte `AliasInicial` → `AliasPrograma` |
| Controlador | `Controllers/{Facultad,Dependencia,ProgramaAcademico,AliasPrograma,Catedra}Controller.cs` | 6 endpoints (4 en `AliasPrograma` en modo A) |
| Ensamblador | `Program.cs` | 5 parejas nuevas; `LimpiarNombreCampo` para `alias[0].texto_normalizado` |
| BD | `db/postgres/migraciones/001_sp_guardar_programa_academico.sql` (+ `006`…`010` en modo B) | Montada como `z001_…` en el compose |
| Pruebas | `api_catedras/pruebas/Programa.cs` | Repositorios falsos de las 5 entidades; casos de autorreferencia, `Catedra` con `(id, nombre[..30])` y maestro-detalle |
| Front | `routes/catedras.py`, `templates/catedras.html`, `detalle_catedra.html` | Catálogo `/catedras`: `?q=`, `?pagina=`, `POR_PAGINA = 24` |
| Front | `routes/admin.py`, `templates/admin/{facultad,dependencia,programa,alias,catedra}_{lista,form}.html` | Los formularios de FK arman el `<select>` con una llamada previa a la API; `programa_form.html` con `<textarea>` "Alias iniciales" |
| Front | `auth_helpers.py` | Siempre "sin sesión" (el login llega con la v5) |

> Verifica cada nombre con `ls` antes de citarlo.

## 7. Datos de ejemplo verificados contra `script_bdcatedras.sql` (2026-10-15)

| Para probar… | Usa | Resultado esperado |
|---|---|---|
| Facultades (criterio 2) | `GET /api/Facultad` | 9: raíz `USB` (padre `null`) y 8 hijas (`F_ING`, `F_ARTES`, `F_PSICO`, `F_DERECHO`, `F_EDU`, `F_CIENCIAS`, `F_SALUD`, `F_OTRA`) |
| Autorreferencia | `POST` con `fk_facultad_padre` = su propio `id_facultad` | 400 (y lo mismo en `Dependencia`) |
| Padre inexistente | `fk_facultad_padre: "NO_EXISTE"` | 500 (D9) |
| Dependencias (criterio 3) | `GET /api/Dependencia` | 8; raíces `VICE_ACAD` y `BIENESTAR`; hijas de `BIENESTAR`: `PASTORAL`, `DEPORTES`, `CULTURA`, `SALUD`, `DESA_HUM`; hija de `VICE_ACAD`: `INVESTIG` |
| Programas (criterio 4) | `GET /api/ProgramaAcademico` | 105: 104 en `F_OTRA` ("Programa M0110"…) y **1** en `F_ING` (`M0286` "Ingenieria de Datos y Software"); `PREGRADO` 58 y `OTRO` 47 |
| Forma de `codigo` | `"m0286"` (minúscula) | 422 |
| Lista cerrada de `nivel` | `"TECNICO"` | 422 (válidos: `PREGRADO`, `ESPECIALIZACION`, `MAESTRIA`, `DOCTORADO`, `EDUCACION_CONTINUA`, `OTRO`) |
| Alias (criterio 5) | `GET /api/AliasPrograma` | 3, todos de `M0286`, **con ids 1, 3 y 4** (no hay id 2): `ingenieria de datos y software`, `ingenieria de datos`, `ing. datos y software` |
| `POST` de alias | sin `creado_en` en el body | Devuelve `id_alias`; si manda `creado_en`, se ignora |
| Cátedras (criterio 6) | `GET /api/Catedra?limite=10` | 10 filas, `total: 5502` |
| Columna generada | `GET /api/Catedra/1` | `id_evento_asis 000023636`, `nombre_asis "PELICULA:LA MIRADA DEL AMOR"` (30 caracteres, calculado por la BD) |
| Forma de `id_evento_asis` | `"123"` / `"12345"` | 422 |
| FK a `tipo_evento` (v1) | `fk_tipo_evento` inexistente | 500 |
| Maestro-detalle (criterio 11) | `POST /api/ProgramaAcademico` con `alias:[{texto_normalizado:"nuevo uno"},{texto_normalizado:"nuevo dos"}]` | 200 y 2 alias con `fk_programa` = el código nuevo y `origen` `FORMULARIO` |
| Atomicidad | mismo `POST` con el alias `"ingenieria de datos"` (ya existe) | 500 **y** `GET /api/ProgramaAcademico/{codigo}` → 404 |
| Alias sin texto / `alias:"hola"` | — | 422 con `alias[0].texto_normalizado` / 422 |

**Integridad referencial (criterio 7, Fase 18) — orden obligatorio, sobre BD
recién reseteada:**

| # | Llamada | Modo A (spec original) | Modo B (código de hoy) |
|---|---|---|---|
| 1 | `DELETE /api/Facultad/F_ING` | 200; `M0286` queda con `fk_facultad: null` (`SET NULL`) | 200 (lógico); `M0286` **conserva** `F_ING` |
| 2 | `DELETE /api/Dependencia/DESA_HUM` | 200; la cátedra **5497** queda con `fk_dependencia: null` | 200 (lógico); la cátedra 5497 conserva `DESA_HUM` |
| 3 | `DELETE /api/ProgramaAcademico/M0286` | **500**: 58 filas de `programa_asistente` (v3) la bloquean con `RESTRICT`; los 3 alias siguen | 200 (lógico); los 3 alias siguen |
| 4 | `DELETE /api/Catedra/5502` | 200 sin efectos (sin sesiones) | 200 (lógico) |

Cátedras **sin sesiones** (seguras para borrar en modo A): **5498, 5500, 5502,
5503, 5504** (los ids no son consecutivos: hay 5502 filas y el último id es
5504). Las demás tienen sesiones sembradas de la v4 (p. ej. la cátedra 1 tiene
604) y el `DELETE` físico las borraría en cascada (`fk_sesion_catedra ON DELETE
CASCADE`): **no lo ejecutes en modo A**.

**Después de cualquier prueba que modifique datos:**
`docker compose down -v && docker compose up -d --build`.

## 8. Fases con prompts listos

> El código de las Fases 14-20b **ya está escrito**. Si reconstruyes la
> versión desde cero (modo A), usa los prompts tal cual; si solo verificas,
> salta a "Terminado cuando…". La Fase 21 es la única **abierta**.

### Fase 14 — Capas de `Facultad` y `Dependencia`

```text
Fase 14 de docs/spec_kit/Versiones/version2/8_tasks.md (RF62-RF73). Lee D9 y D10
ANTES de empezar. Crea Modelos, Peticiones {Crear,Reemplazo,Actualizar},
IRepositorio/Repositorio...Postgres, IServicio/Servicio y el controlador (6
endpoints) de Facultad y de Dependencia, y regístralos en Program.cs.
Molde: copia la FORMA de Sede (v1). Autorreferencia: ValidarNoEsSuPropioPadre en
el SERVICIO (Crear, Reemplazar y Actualizar), nunca en la petición. NO verifiques
que el padre exista (D9) y NO hagas recursión (D10). Comentarios en español,
línea a línea, con la definición real de la tabla en la cabecera del modelo.
```

**Terminado cuando:** `GET /api/Facultad` → 9 (raíz `USB`); `GET
/api/Dependencia` → 8 (raíces `VICE_ACAD`, `BIENESTAR`); autorreferencia → 400
en ambas; padre inexistente → 500. **No ejecutes aún los `DELETE`** (van en la
Fase 18).

**Errores típicos:** validar el padre con un `SELECT` previo (D9); poner
`padre ≠ id` en la petición (es cruce de dos campos del mismo body); olvidar
registrar las parejas en `Program.cs` (500 "Unable to resolve service").

### Fase 15 — Capas de `ProgramaAcademico` y `AliasPrograma`

```text
Fase 15 (RF74-RF83, D12). ProgramaAcademico: PK codigo con
[RegularExpression("^[A-Z][0-9A-Z]{3,4}$")] y nivel con [RegularExpression] sobre la
lista cerrada (mismo patrón que canal_difusion de la v1). AliasPrograma
(MODO A): única petición AliasProgramaCrear (texto_normalizado y fk_programa
obligatorios, origen opcional), IRepositorioAliasPrograma SIN ActualizarAsync,
CrearAsync con RETURNING id_alias, creado_en, y controlador con 4 endpoints (sin
HttpPut ni HttpPatch). IdAlias y CreadoEn sin 'required' en el modelo.
```

**Terminado cuando:** `GET /api/ProgramaAcademico` → 105; `M0286` →
`fk_facultad: "F_ING"`; `codigo: "m0001"` → 422; `nivel: "TECNICO"` → 422;
`GET /api/AliasPrograma` → 3 de `M0286`; `POST` devuelve `id_alias` e ignora
`creado_en`; en modo A, `PUT /api/AliasPrograma/1` → **405** (no 404).

**Errores típicos:** crear `AliasProgramaReemplazo`/`Actualizar` "por si acaso"
(modo A); aceptar `creado_en` en la petición; olvidar que `uq_alias_texto` hace
que el texto del alias sea único en toda la tabla.

### Fase 16 — Capas de `Catedra`

```text
Fase 16 (RF84-RF89, D11). Catedra.nombre_asis es GENERATED ALWAYS (LEFT(nombre,30)):
NINGUNA de las 3 peticiones lo declara. IRepositorioCatedra.CrearAsync devuelve
Task<(long IdCatedra, string NombreAsis)> con INSERT ... RETURNING id_catedra,
nombre_asis (un solo viaje). id_evento_asis con [RegularExpression("^[0-9]{9}$")] y
[StringLength(9)]. El controlador arma la respuesta del POST con la tupla.
IdCatedra, NombreAsis y CreadoEn sin 'required' en el modelo.
```

**Terminado cuando:** `GET /api/Catedra?limite=5` → 5 filas y `total: 5502`;
`GET /api/Catedra/1` → `nombre_asis` idéntico a `LEFT(nombre, 30)` **calculado
por la BD**; `id_evento_asis: "12345"` → 422; `fk_tipo_evento` inexistente →
500. **No hagas `DELETE` de cátedras con sesiones** (modo A).

**Errores típicos:** recortar el nombre en C#; hacer un `SELECT` extra después
del `INSERT`; declarar `NombreAsis` en una petición.

### Fase 17 — Prueba de capas extendida (criterio 8)

```text
Fase 17. Extiende api_catedras/pruebas/Programa.cs con un repositorio FALSO y
verificaciones para Facultad, Dependencia, ProgramaAcademico, AliasPrograma y
Catedra (mismo patrón que la Fase 11 de la v1). Los falsos de Facultad y
Dependencia fuerzan la autorreferencia y confirman el 400. El de AliasPrograma
NO implementa ActualizarAsync (modo A). El de Catedra simula el RETURNING
devolviendo (id, nombre[..30]). Cuidado: todas las variables top-level comparten
ámbito; usa nombres nuevos.
```

**Terminado cuando:** `docker compose exec api dotnet run --project pruebas`
termina con `0 fallidas` y código 0 (el spec registró **153** en esta fase y
**158** tras el retrofit; hoy el total es mayor porque se sumaron v3-v7:
compara con el último total anotado, no con 153/158).

### Fase 18 — Integridad referencial (sin código nuevo)

```text
Fase 18. No escribas código: dame la secuencia de llamadas HTTP (curl o
PowerShell Invoke-RestMethod) para los 4 pasos de 8_tasks.md, EN ESE ORDEN, con
los datos de §7 de guia_ia_v2.md (F_ING, DESA_HUM, M0286, 5502) y la respuesta
exacta que debo esperar. Dime también cuál es mi MODO (A o B), porque los
resultados cambian (ver la tabla de §7).
```

**Terminado cuando:** los 4 resultados coinciden con la columna de tu modo y,
al final, ejecutaste `docker compose down -v && docker compose up -d --build`.
Si no coincide, el modelo de datos del spec tiene un error: corrígelo **ahí**,
no "arregles" el comportamiento con código en la API (Art. 5).

### Fase 19 — Front: integración de la plantilla

```text
Fase 19 (RF92-RF93, §4.6 de 3_plan.md). Integra base.html, partials/navbar.html y
partials/footer.html con los estáticos; crea auth_helpers.py en la raíz de
front_catedras/ (siempre 'sin sesión', registrado como context_processor);
corrige cada url_for roto del navbar hacia blueprints reales; verifica que no haya
un puerto 5001 cableado (hoy 5000); elimina cualquier try/except que sirva datos
MOCK cuando la API falla: el error se muestra en pantalla (RF93).
```

**Terminado cuando:** con `docker compose stop api`, `http://localhost:5000`
muestra un error, nunca datos inventados; con la API prendida el navbar no
produce `werkzeug.routing.BuildError`.

### Fase 20 — Front: catálogo público y administración

```text
Fase 20 (RF90-RF91, RF94, D13). routes/catedras.py: GET /catedras llama UNA vez
a /api/Catedra (sin limite), filtra por ?q= y pagina por ?pagina= en Python
(POR_PAGINA = 24), sin JavaScript nuevo. GET /catedras/<id> con detalle y, si
fk_dependencia no es nulo, una segunda llamada a /api/Dependencia/<id> para el
nombre. routes/admin.py: 5 bloques CRUD con el mismo patrón de la v1; los de
Facultad/Dependencia arman el <select> de padres con una llamada previa (Regla 2);
Alias (modo A) solo crear y eliminar. Completa también las pantallas de
TipoPregunta y TipoVinculacion si faltan.
```

**Terminado cuando:** `/catedras` pagina las 5502 y el buscador `"amor"`
encuentra `000023636` sin importar la página; `/catedras/1` muestra el detalle;
`/admin/` tiene **15** tarjetas con CRUD de punta a punta.

**Errores típicos:** pedir la lista por páginas a la API (cambia el contrato);
un campo de texto libre donde debía ir un `<select>` (Regla 2); mostrar datos
de ejemplo si la API falla.

### Fase 20b — Retrofit maestro-detalle (Regla 1)

```text
Fase 20b (RF76, RF94, criterio 11, D14). Crea
db/postgres/migraciones/001_sp_guardar_programa_academico.sql con CREATE OR
REPLACE PROCEDURE sp_guardar_programa_academico(p_codigo varchar(5), p_nombre
varchar(150), p_fk_facultad, p_nivel varchar(25), p_activo, p_alias jsonb) que
inserta el maestro y luego los alias desde el jsonb, con fk_programa = p_codigo
(nunca del JSON). Móntala en docker-compose.yml como z001_... Agrega
Peticiones/AliasInicial.cs y 'alias' (opcional) a ProgramaAcademicoCrear;
CrearAsync(programa, aliasIniciales) hace el CALL con CAST(@Alias AS jsonb);
ServicioProgramaAcademico convierte los alias con origen por defecto. Arregla
LimpiarNombreCampo en Program.cs. En el front, un <textarea> "Alias iniciales"
(uno por línea) solo al crear. Agrega 5 verificaciones a la prueba de capas.
```

**Terminado cuando:** el SP se aplica 2 veces sin error; `POST` sin `alias`, con
`[]` y con 2 alias → 200; alias repetido → 500 **y** el programa no existe (404);
alias sin texto → 422; `alias: "hola"` → 422; el formulario Flask crea programa +
alias y, ante un alias repetido, no crea nada y conserva lo escrito.

**Errores típicos:** nombre `001_…` sin el prefijo `z` (corre antes que el script
provisto y falla); tamaños del SP distintos a los de la columna; meter
`fk_programa` en el JSON; SQL dinámico.

### Fase 21 — Cierre de la v2 (ABIERTA)

```text
Fase 21. Con el entorno recién reseteado, acompáñame a correr los 11 criterios de
aceptación de 2_spec.md §6 en orden (dime mi MODO para el criterio 7). Para cada
uno dame el comando, el resultado esperado y qué revisar si no coincide. Al final:
total de la prueba de capas, texto del commit en español y recordatorio del tag v2.
No escribas nada de la v3 todavía (Art. 1).
```

**Terminado cuando:** los 11 criterios pasan, el total de la prueba de capas
queda anotado en `8_tasks.md`, la casilla está marcada con el dónde y existe
el tag `v2` (**no verificado desde esta guía**: `git tag --list`).

## 9. Trampas conocidas y diagnóstico

| Síntoma | Causa | Arreglo |
|---|---|---|
| `POST /api/Facultad` con padre inexistente → 500 | Es el diseño (D9) | Correcto; no agregar `SELECT` previo |
| Un ciclo `A → B → A` pasa | Solo se valida un nivel (D10) | Riesgo documentado |
| `PUT /api/AliasPrograma/1` responde 405 (modo A) | Verbo no declarado (D12) | Correcto |
| `PUT /api/AliasPrograma/1` responde 200 (modo B) | El retrofit agregó los verbos | Conflicto con D12: **reportar**, no borrar |
| `nombre_asis` ignorado en el `POST` | Columna generada | Correcto (D11) |
| `GET /api/Catedra` no trae las cátedras "borradas" (modo B) | Borrado lógico | `?incluirInactivos=true` |
| `DELETE /api/ProgramaAcademico/M0286` → 200 en vez de 500 (modo B) | Lógico: no toca la FK | Correcto; el 500 del spec es de modo A |
| `DELETE /api/Catedra/1` (modo A) borra cientos de sesiones | `fk_sesion_catedra ON DELETE CASCADE` | No ejecutarlo; usar 5498/5500/5502/5503/5504 y `down -v` |
| Alias creado con el programa no aparece | `fk_programa` mal armado o transacción revertida | Mirar el 500 del `POST` y el SP |
| El SP `sp_guardar_programa_academico` no existe en mi BD | Volumen creado antes de la v2 | Aplicar la migración a mano con `psql` (ver `7_quickstart.md`) o `down -v` |
| `001_…` falla con "relation does not exist" | Sin prefijo `z` en el montaje | `z001_…` en el compose |
| `alias[0]._texto_normalizado` en el 422 | Falta `LimpiarNombreCampo` | Corregir en `Program.cs` |
| El catálogo tarda | Descarga las 5502 filas por visita | Aceptado (D13) |
| (Modo B) "could not determine data type of parameter" | `CALL` resuelve por tipos | `CAST(@x AS …)` explícito |
| Todo roto | — | `docker compose down -v && docker compose up -d --build` |

## 10. Lista de revisión para cualquier cambio de la v2

- [ ] Solo nombra tablas de v1-v2 (Art. 1, 5).
- [ ] Modo A: SQL a mano con `catedras.` y `@parametros`. Modo B: el
      repositorio solo hace `CALL`.
- [ ] FK existente: sin `SELECT` previo (D9); error del motor → 500.
- [ ] Autorreferencia: solo en el servicio, un nivel (D10), en Crear,
      Reemplazar y Actualizar.
- [ ] `nombre_asis` sin tocar en C#; `CrearAsync` devuelve la tupla (D11).
- [ ] `AliasPrograma`: 4 verbos en modo A / 6 en modo B, **sin tocar** sin
      preguntar (D12 vs retrofit).
- [ ] Maestro-detalle: un SP explícito, sin SQL dinámico, todo-o-nada (D14).
- [ ] Forma → petición (422); negocio → servicio (400); no existe → 404;
      motor → 500.
- [ ] Catálogo: una llamada, filtro y paginación en Flask (D13).
- [ ] Front: cada FK es un `<select>` lleno desde la API (Regla 2); sin mock.
- [ ] Cada entidad nueva **registrada en `Program.cs`** y con sección en la
      prueba de capas.
- [ ] Comentarios en español con el porqué (Art. 6).
- [ ] `8_tasks.md` actualizado con lo verificado **y dónde**.

## 11. Lo que la v2 le deja a la v3 (no construir todavía)

- La v3 agrega los **asistentes** (`lote_carga_asistente`, `asistente`,
  `novedad_carga`, `documento_asistente`, `vinculacion_asistente`,
  `programa_asistente`, `consentimiento_datos`) y usa `programa_academico` y
  `periodo_academico` como FK: ahí es donde `programa_asistente` hace
  `RESTRICT` sobre `M0286` (58 filas).
- La **carga de archivos** (Excel/CSV) y la **búsqueda** por `?q=` en SQL
  llegan con la v3 (ADR D17/D18); en la v2 la búsqueda del catálogo es solo
  de front.
- El detalle de la cátedra mostrará sus sesiones en la v4.
- **Pendiente de cierre de la v2** (documentado): Fase 21 sin marcar, tag `v2`
  sin verificar, D12 desalineado con el controlador actual. La IA debe
  **reportarlos**, no corregirlos en silencio.
- La spec de la v3 se escribe **después** del tag `v2` (Artículo 1).
