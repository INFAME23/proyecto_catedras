# Plan técnico — Versión 2: Facultad, Dependencia, ProgramaAcademico, AliasPrograma, Catedra + front

> **Versión 2** · CÓMO construir lo especificado en [2_spec.md](2_spec.md).
> El porqué de cada decisión: [4_research.md](4_research.md) · contratos
> de integración: [6_contracts.md](6_contracts.md) · orden:
> [8_tasks.md](8_tasks.md).

---

## 1. Stack

Sin cambios respecto a la v1 ([version1/3_plan.md](../version1/3_plan.md)
§1): C#/ASP.NET Core, Dapper sobre `Npgsql`, PostgreSQL, validación por
petición. La v2 no agrega ningún paquete nuevo (Artículo 2: nada se
agrega sin que una spec lo pida, y esta no lo pide).

## 2. Estructura de carpetas

Mismo patrón de 5 archivos por entidad que ya probó la v1, repetido para
las 5 tablas nuevas — con dos excepciones señaladas abajo.

```
api_catedras/
├── Modelos/
│   ├── Facultad.cs            # 4 propiedades
│   ├── Dependencia.cs         # 4 propiedades
│   ├── ProgramaAcademico.cs   # 5 propiedades
│   ├── AliasPrograma.cs       # 5 propiedades; IdAlias y CreadoEn los pone la BD
│   └── Catedra.cs             # 8 propiedades; IdCatedra y NombreAsis los pone la BD
├── Peticiones/
│   ├── FacultadCrear.cs / FacultadReemplazo.cs / FacultadActualizar.cs
│   ├── DependenciaCrear.cs / DependenciaReemplazo.cs / DependenciaActualizar.cs
│   ├── ProgramaAcademicoCrear.cs / ProgramaAcademicoReemplazo.cs / ProgramaAcademicoActualizar.cs
│   ├── AliasInicial.cs                 # retrofit: elemento de 'alias' en el POST de ProgramaAcademico (§4.8)
│   ├── AliasProgramaCrear.cs           # ⚠️ ÚNICA petición de AliasPrograma — no hay Reemplazo ni Actualizar (§4.6)
│   └── CatedraCrear.cs / CatedraReemplazo.cs / CatedraActualizar.cs   # ninguna de las 3 incluye NombreAsis
├── Controllers/
│   ├── FacultadController.cs
│   ├── DependenciaController.cs
│   ├── ProgramaAcademicoController.cs
│   ├── AliasProgramaController.cs      # solo 4 endpoints: GET lista, GET/{id}, POST, DELETE
│   └── CatedraController.cs
├── Servicios/
│   ├── IServicioFacultad.cs / ServicioFacultad.cs      # valida autorreferencia (§4.2)
│   ├── IServicioDependencia.cs / ServicioDependencia.cs # misma validación
│   ├── IServicioProgramaAcademico.cs / ServicioProgramaAcademico.cs
│   ├── IServicioAliasPrograma.cs / ServicioAliasPrograma.cs
│   └── IServicioCatedra.cs / ServicioCatedra.cs
└── Repositorios/
    ├── IRepositorioFacultad.cs / RepositorioFacultadPostgres.cs
    ├── IRepositorioDependencia.cs / RepositorioDependenciaPostgres.cs
    ├── IRepositorioProgramaAcademico.cs / RepositorioProgramaAcademicoPostgres.cs  # CrearAsync = CALL al SP (§4.8)
    ├── IRepositorioAliasPrograma.cs / RepositorioAliasProgramaPostgres.cs  # sin ActualizarAsync (§4.6)
    └── IRepositorioCatedra.cs / RepositorioCatedraPostgres.cs             # CrearAsync devuelve (id, nombreAsis) (§4.3)
```

No se toca `Excepciones/NoEncontradoExcepcion.cs` ni
`ManejadorTipoDateOnly.cs` (ninguna columna `DATE`/`DateOnly` en estas 5
tablas) — se reutilizan tal cual.

**`front_catedras/`** (fuera del alcance de `ApiCatedras`, mismo
proyecto Flask aparte):

```
front_catedras/
├── auth_helpers.py            # NUEVO — llega con la plantilla, se completa (§4.7)
├── routes/
│   ├── catedras.py            # deja de ser placeholder: catálogo + detalle (RF90-91)
│   └── admin.py                # +5 bloques CRUD (mismo patrón que las 10 de v1)
├── templates/
│   ├── base.html, partials/navbar.html, partials/footer.html   # reemplazados por la plantilla (§4.7)
│   ├── catedras.html           # NUEVO
│   ├── detalle_catedra.html    # NUEVO
│   └── admin/
│       ├── facultad_form.html / facultad_lista.html
│       ├── dependencia_form.html / dependencia_lista.html
│       ├── programa_form.html / programa_lista.html
│       ├── alias_form.html / alias_lista.html       # sin editar, solo crear/eliminar
│       └── catedra_form.html / catedra_lista.html
└── static/
    └── (CSS/activos de la plantilla, reemplazan a estilo.css)
```

## 3. Arquitectura en capas

Sin cambios respecto a la v1 ([version1/3_plan.md](../version1/3_plan.md)
§3): controller → `IServicio` → `IRepositorio` → `RepositorioPostgres`.
Las 5 tablas nuevas entran por el mismo molde; lo único que cambia entre
ellas y `Sede` son las reglas de negocio concretas (§4 de aquí) y, en
`AliasPrograma`, que el molde se usa incompleto a propósito.

## 4. Decisiones de diseño clave

### 4.1 FK hacia otra tabla ya expuesta: no se revalida, se deja fallar

`programa_academico.fk_facultad`, `alias_programa.fk_programa` y
`catedra.fk_tipo_evento`/`fk_dependencia` apuntan a tablas que la API ya
expone (de esta misma versión o de la v1). El servicio **no** hace un
`SELECT` previo para confirmar que el padre existe — sería una segunda
fuente de verdad que puede desincronizarse. Se deja que el `INSERT` lo
intente y, si el motor rechaza la FK, el repositorio deja subir la
`NpgsqlException` tal cual; el controller la traduce a 500 con el
mensaje del motor en `detalle` (mismo camino que ya existe para un
`id_sede` duplicado en v1 — no es un camino nuevo, es el mismo aplicado
a una FK en vez de a un `UNIQUE`).

### 4.2 Autorreferencia (`Facultad`, `Dependencia`): se valida en el servicio

El `CHECK` de la BD (`fk_facultad_padre <> id_facultad`) no alcanza a
proteger un `POST`: en el momento de validar, la fila todavía no existe,
así que compararla contra sí misma en la BD no tiene con qué comparar
hasta DESPUÉS del `INSERT` — y para entonces ya sería tarde. El servicio
hace la comparación en memoria, con los dos valores que ya tiene en la
petición, antes de llamar al repositorio:

```csharp
// ServicioFacultad.CrearAsync
if (peticion.FkFacultadPadre == peticion.IdFacultad)
    throw new ArgumentException(
        "Una facultad no puede ser su propio padre ('fk_facultad_padre').");

// ServicioFacultad.ReemplazarAsync / ActualizarAsync — el id viene de la URL,
// no del body, así que se compara contra el parámetro de ruta:
if (peticion.FkFacultadPadre == idFacultad)
    throw new ArgumentException(
        "Una facultad no puede ser su propio padre ('fk_facultad_padre').");
```

`ServicioDependencia` repite exactamente el mismo patrón sobre
`fk_dependencia_padre`/`id_dependencia`. El `CHECK` de la BD queda como
**última muralla** para el caso que el servicio no cubre (un `PATCH` a
un hijo que termine apuntándose a sí mismo por una fila ya existente que
cambió de id — no aplica aquí porque los PK de estas tablas no se
actualizan, pero se documenta por qué el `CHECK` sigue ahí: no estorba y
es la red de seguridad final).

### 4.3 `Catedra`: dos valores que pone la BD en el mismo `INSERT`

`id_catedra` es `IDENTITY` (igual que `encuesta.id_encuesta` en v1,
§4.7 de [version1/3_plan.md](../version1/3_plan.md)) y `nombre_asis` es
una columna **generada** (`GENERATED ALWAYS AS (left(nombre, 30))
STORED`). PostgreSQL permite devolver ambas con un solo `RETURNING`:

```sql
INSERT INTO catedras.catedra
    (id_evento_asis, nombre, fk_tipo_evento, fk_dependencia, activo)
VALUES (@IdEventoAsis, @Nombre, @FkTipoEvento, @FkDependencia, @Activo)
RETURNING id_catedra, nombre_asis;
```

`IRepositorioCatedra.CrearAsync` devuelve una tupla
`Task<(long IdCatedra, string NombreAsis)>` (no un `long` solo, a
diferencia de `IRepositorioEncuesta.CrearAsync`); el servicio asigna
ambos valores al modelo antes de devolverlo. `CatedraReemplazo` y
`CatedraActualizar` tampoco tocan `nombre_asis`: un `UPDATE` sobre
`nombre` hace que la BD la recalcule sola, así que esos dos verbos
devuelven solo `filasEliminadas`/confirmación, igual que el resto —
si el cliente necesita ver el `nombre_asis` recalculado, hace un `GET`
después (mismo criterio que un `PATCH` de `Sede` no devuelve la fila
completa).

### 4.4 `AliasPrograma`: el molde de 5 verbos se usa incompleto, a propósito

Por diseño (RF80-83 de [2_spec.md](2_spec.md)), esta tabla no tiene
`PUT` ni `PATCH`. Eso se refleja en la interfaz, no se simula:

```csharp
public interface IRepositorioAliasPrograma
{
    Task<List<AliasPrograma>> ObtenerTodosAsync(int limite);
    Task<AliasPrograma?> ObtenerPorIdAsync(long idAlias);
    Task<long> CrearAsync(AliasPrograma alias);   // RETURNING id_alias, creado_en
    Task<int> EliminarAsync(long idAlias);
}
```

No existe `ActualizarAsync`: `IServicioAliasPrograma` tampoco lo
declara, y `AliasProgramaController` no tiene métodos `[HttpPut]` ni
`[HttpPatch]`. Esto es deliberado (Artículo 1, YAGNI) y no una omisión:
agregar esos verbos sin que una spec los pida sería anticipar.

`creado_en` sigue el mismo criterio que `id_alias`: la BD la asigna
(`DEFAULT now()`), el repositorio la lee con el mismo `RETURNING
id_alias, creado_en` y el modelo la expone de solo lectura.

### 4.5 Listas cerradas y formato: mismo criterio que v1

`ProgramaAcademico.nivel` (lista cerrada: `PREGRADO`,
`ESPECIALIZACION`, `MAESTRIA`, `DOCTORADO`, `EDUCACION_CONTINUA`,
`OTRO`) y los formatos de `codigo` e `id_evento_asis` se validan en la
PETICIÓN con `[RegularExpression]`, exactamente como
`Modalidad.canal_difusion` en v1 (§4.9 de
[version1/3_plan.md](../version1/3_plan.md)) — no son reglas nuevas,
son la misma regla aplicada a tres campos distintos.

### 4.6 Integración de la plantilla en `front_catedras/`

> Basado en lo que describiste del proyecto externo (`Desktop/Front`),
> que no pude inspeccionar — ver el supuesto en §4 de
> [2_spec.md](2_spec.md).

Pasos, en este orden, para no dejar el front roto a medio camino:

1. Copiar `base.html`, `partials/navbar.html`, `partials/footer.html` y
   los estáticos (CSS) de la plantilla **sobre** los actuales de
   `front_catedras/` (que hoy ya tienen Bootstrap 5 + identidad USB,
   así que es un reemplazo de diseño, no una integración desde cero).
2. Copiar `auth_helpers.py` a la raíz de `front_catedras/` y completar
   SOLO lo que el `base.html` de la plantilla necesita para renderizar
   (típicamente, si hay un usuario en sesión). En v2 esa función
   siempre devuelve "sin sesión" — no hay `usuario` en la API todavía
   (login real es v5, mapa de versiones).
3. Recorrer `navbar.html` y corregir cada `url_for(...)` que apunte a
   un endpoint que no existe en este proyecto, mapeándolo al blueprint
   real más parecido (`catedras.inicio`, `admin.panel`, `auth.login`,
   `ponente.inicio`, `estudiante.inicio`, `registro.inicio` — los 6 que
   `app.py` ya registra).
4. Reemplazar el puerto `5001` cableado en la plantilla por el que usa
   este proyecto (`run.py`, hoy `5000`; no se cambia a `8142` porque ese
   puerto es de `ApiCatedras`, Artículo 8).
5. Buscar y eliminar cualquier `try/except` en
   `servicios/api_catedras.py` o en las rutas que sirva datos de
   ejemplo cuando la API falla (RF93) — un error de la API debe
   mostrarse como error, no esconderse.
6. Recién con la plantilla integrada y corregida, construir
   `catedras.py` (catálogo + detalle, RF90-91) y las 5 extensiones de
   `admin.py` sobre la base nueva — así no se construye dos veces.

### 4.7 Paginación del catálogo público (5502 filas): una sola llamada, resuelta en Flask

`GET /api/Catedra` sin `limite` sigue devolviendo todas las filas (igual
que en v1 — no se cambia el contrato de la API por conveniencia del
front, Artículo 7). La ruta `catedras.inicio` del front llama a la API
**una sola vez por visita**, sin `limite`, y resuelve ahí mismo, en
Python, las dos cosas que la pantalla necesita:

```python
# routes/catedras.py (forma, no código final)
datos = listar('Catedra')['datos']                     # las 5502, una sola llamada
if texto_busqueda:
    datos = [c for c in datos if texto_busqueda.lower() in c['nombre'].lower()]
total_filtrado = len(datos)
inicio = (pagina - 1) * POR_PAGINA
pagina_actual = datos[inicio:inicio + POR_PAGINA]       # el slicing pagina
```

Así el buscador de texto libre (RF90) filtra sobre el catálogo
**completo**, no solo sobre la página visible, y no hace falta
JavaScript ni un `offset` nuevo en la API — ver el ADR D13 de
[4_research.md](4_research.md) para las alternativas descartadas (una
API con `offset`, o filtrar en JavaScript en el navegador).

### 4.8 Retrofit — maestro-detalle `programa_academico` + `alias_programa` con un SP

Regla 1 de la [guía operativa para la IA](../../guia_ia.md): si una
petición crea un maestro **junto con** sus detalles, se resuelve con UN
procedimiento almacenado que hace todo en una transacción — nunca dos
`INSERT` sueltos desde C#. Ver el ADR D14 de [4_research.md](4_research.md).

```
POST /api/ProgramaAcademico  { ..., "alias": [ {...}, {...} ] }
  → ProgramaAcademicoCrear (+ List<AliasInicial>?)   valida forma → 422
  → ServicioProgramaAcademico.CrearAsync             arma List<AliasPrograma>
                                                      (fk_programa = codigo, origen por defecto)
  → RepositorioProgramaAcademicoPostgres.CrearAsync  serializa a JSON y hace
       CALL catedras.sp_guardar_programa_academico(@Codigo, ..., CAST(@Alias AS jsonb))
  → el SP: INSERT programa + INSERT ... SELECT FROM jsonb_array_elements(p_alias)
```

- **El JSON se arma en el repositorio, no en el servicio:** la forma
  `[{"texto_normalizado", "origen"}]` es el contrato del SP, es decir,
  un detalle del motor; el servicio sigue sin saber qué motor hay
  (Artículo 3). El servicio solo aplica la regla de negocio: cada alias
  inicial pertenece al programa que se crea.
- El SP vive en `db/postgres/migraciones/001_sp_guardar_programa_academico.sql`
  y se monta en `/docker-entrypoint-initdb.d/` con prefijo `z001_` para
  correr **después** del script provisto (orden alfabético).
- `POST /api/AliasPrograma` (agregar un alias DESPUÉS) no cambia: sigue
  siendo un `INSERT` normal, porque ahí no hay "una sola petición" de
  maestro + detalle que unir.

## 5. Docker: sin cambios

La v2 no agrega servicios: sigue siendo `postgres` + `api`
([version1/3_plan.md](../version1/3_plan.md) §5, [6_contracts.md](6_contracts.md)
§1). Las 5 tablas nuevas ya existen en el script de la BD desde la v1
(Artículo 5); lo único que crece es el código de `api_catedras/` montado
en el mismo contenedor `api`. `front_catedras/` sigue fuera de
`docker-compose.yml` (se arranca aparte con `python run.py`), como en
v1 — integrarlo al compose no lo pide ninguna spec todavía.

**Retrofit:** el servicio `postgres` monta además
`db/postgres/migraciones/001_sp_guardar_programa_academico.sql` como
`/docker-entrypoint-initdb.d/z001_sp_guardar_programa_academico.sql`
(§4.8).
