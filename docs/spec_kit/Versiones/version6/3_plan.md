# Plan técnico — Versión 6: encuestas de calidad

> ⚠️ **BORRADOR** — ver nota de [2_spec.md](2_spec.md).

## 1. Stack

Sin cambios: C#/ASP.NET Core (.NET 10) + Dapper, sin ORM, sin
paquetes nuevos. **Un SP nuevo** (`sp_guardar_pregunta`, a diferencia
de lo que el plan original asumía — ver ADR D29).

## 2. Estructura de carpetas

```
api_catedras/
├── Modelos/
│   ├── Pregunta.cs             # 8 propiedades
│   ├── OpcionPregunta.cs       # 5 propiedades
│   ├── RespuestaEncuesta.cs    # 5 propiedades
│   └── RespuestaItem.cs        # 6 propiedades
├── Peticiones/
│   ├── Pregunta{Crear,Reemplazo,Actualizar}.cs
│   ├── OpcionInicial.cs                                    # §4.1 — SIN fk_pregunta, lo pone el servicio
│   ├── OpcionPregunta{Crear,Reemplazo,Actualizar}.cs
│   ├── ItemRespuesta.cs                                    # elemento de 'respuestas' en Responder — SIN fk_respuesta
│   └── RespuestaEncuestaResponder.cs                        # ⚠️ ÚNICA — sin Crear/Reemplazo/Actualizar genéricos
├── Controllers/
│   ├── PreguntaController.cs            # 6 endpoints
│   ├── OpcionPreguntaController.cs      # 6 endpoints
│   ├── RespuestaEncuestaController.cs   # GET lista, GET/{id}, POST (=responder) — 3
│   └── RespuestaItemController.cs       # GET lista, GET/{id} — 2
├── Servicios/
│   ├── IServicioPregunta.cs / ServicioPregunta.cs            # CrearAsync valida valor_min/valor_max y arma las opciones
│   ├── IServicioOpcionPregunta.cs / ServicioOpcionPregunta.cs
│   ├── IServicioRespuestaEncuesta.cs / ServicioRespuestaEncuesta.cs
│   └── IServicioRespuestaItem.cs / ServicioRespuestaItem.cs
└── Repositorios/
    ├── IRepositorioPregunta.cs / RepositorioPreguntaPostgres.cs          # CrearAsync = CALL sp_guardar_pregunta
    ├── IRepositorioOpcionPregunta.cs / RepositorioOpcionPreguntaPostgres.cs
    ├── IRepositorioRespuestaEncuesta.cs / RepositorioRespuestaEncuestaPostgres.cs  # ResponderAsync = SELECT * FROM fn_responder_encuesta_api(...); SIN CerrarAsync
    └── IRepositorioRespuestaItem.cs / RepositorioRespuestaItemPostgres.cs          # solo ObtenerTodosAsync/ObtenerPorIdAsync
```

## 3. Arquitectura en capas

Sin cambios de forma (Artículo 3). Dos mecánicas distintas conviven en
esta versión, cada una igual a un patrón ya usado antes:
`RepositorioPreguntaPostgres.CrearAsync` hace `CALL` a un `PROCEDURE`
nuevo con un parámetro `jsonb` (igual que v2/v3);
`RepositorioRespuestaEncuestaPostgres.ResponderAsync` hace
`SELECT * FROM` sobre una `FUNCTION` que ya existía desde v1 (igual que
v5 con `EnlaceRegistro`/`ClaveAcceso`).

## 4. Decisiones de diseño clave

### 4.1 `sp_guardar_pregunta`: maestro + opciones iniciales, en una transacción (Guía IA, Regla 1)

A diferencia de `respuesta_encuesta`/`respuesta_item` (que ya tenían su
SP desde v1 — ADR D29), **esta** pareja maestro-detalle SÍ necesita un
SP nuevo, porque ninguna versión anterior lo escribió:

```sql
-- db/postgres/migraciones/004_sp_guardar_pregunta.sql
CREATE OR REPLACE PROCEDURE catedras.sp_guardar_pregunta(
    IN p_fk_encuesta      bigint,
    IN p_enunciado        character varying(300),
    IN p_orden            smallint,
    IN p_fk_tipo_pregunta character varying(15),
    IN p_obligatoria      boolean  DEFAULT false,
    IN p_valor_min        smallint DEFAULT NULL,
    IN p_valor_max        smallint DEFAULT NULL,
    -- Detalle: arreglo JSON, p. ej. [{"etiqueta":"Si","valor":1,"orden":1}, ...]
    IN p_opciones         jsonb    DEFAULT NULL,
    INOUT p_id_pregunta   bigint   DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    INSERT INTO pregunta (fk_encuesta, enunciado, orden, fk_tipo_pregunta,
                          obligatoria, valor_min, valor_max)
    VALUES (p_fk_encuesta, p_enunciado, p_orden, p_fk_tipo_pregunta,
            p_obligatoria, p_valor_min, p_valor_max)
    RETURNING id_pregunta INTO p_id_pregunta;

    IF p_opciones IS NOT NULL AND jsonb_array_length(p_opciones) > 0 THEN
        INSERT INTO opcion_pregunta (fk_pregunta, etiqueta, valor, orden)
        SELECT p_id_pregunta,
               e ->> 'etiqueta',
               (e ->> 'valor')::smallint,
               (e ->> 'orden')::smallint
          FROM jsonb_array_elements(p_opciones) AS e;
    END IF;
END;
$$;
```

Monta con prefijo `z004_` (después de `script_bdcatedras.sql` y de
`z001_`-`z003_` de v2/v3 — ver [6_contracts.md](6_contracts.md) §3). Si
alguna opción falla (`orden` repetido para la misma pregunta), el
`CALL` completo se revierte: **ni la pregunta queda creada** — mismo
criterio todo-o-nada que `sp_guardar_programa_academico` (v2) y
`sp_guardar_asistente` (v3), no el parcial de `sp_guardar_lote_carga`
(v3): aquí no hay miles de filas independientes, hay una pregunta con
sus propias opciones, que o se crean juntas o no se crean.

### 4.2 `RespuestaEncuesta.ResponderAsync`: una función que ya hace todo (ADR D30)

```csharp
// RepositorioRespuestaEncuestaPostgres.cs
public async Task<long> ResponderAsync(long fkRegistro, IReadOnlyList<ItemRespuesta> items)
{
    const string sql = @"SELECT * FROM catedras.fn_responder_encuesta_api(
        @FkRegistro, CAST(@Respuestas AS jsonb));";
    string respuestasJson = JsonSerializer.Serialize(items.Select(i => new {
        pregunta = i.Pregunta, numero = i.Numero, texto = i.Texto, opcion = i.Opcion }));
    return await conexion.ExecuteScalarAsync<long>(sql,
        new { FkRegistro = fkRegistro, Respuestas = respuestasJson });
}
```

`IRepositorioRespuestaEncuesta` **no** tiene ningún método
`CerrarAsync`: no hace falta, porque `fn_responder_encuesta_api` ya
llama a `sp_cerrar_encuesta` internamente, dentro de la misma
transacción SQL que el `INSERT` de los detalles. Si faltan preguntas
obligatorias, la excepción de `sp_cerrar_encuesta` revierte TODO el
`CALL` — incluido el `INSERT` del maestro que ya había ocurrido unas
líneas antes dentro del mismo procedimiento.

### 4.3 `valor_min`/`valor_max`: cruce de campos, mismo criterio que `Sesion`/`PeriodoAcademico`

`ServicioPregunta.CrearAsync`/`ReemplazarAsync`/`ActualizarAsync`
validan, antes de llamar al repositorio: si alguno de los dos viene, el
otro también debe venir, y `valor_max > valor_min` — `ArgumentException`
(400) si no, igual mecánica que `Sesion.fin > inicio` (v4) y
`PeriodoAcademico.fecha_fin > fecha_ini` (v1). El `CHECK
chk_pregunta_rango` de la BD es la última muralla.

### 4.4 FK hacia tablas que ya existen: no se revalida (D9, sin cambios)

`pregunta.fk_encuesta`/`fk_tipo_pregunta`,
`opcion_pregunta.fk_pregunta`, `respuesta_item.fk_pregunta`/`fk_opcion`
— igual que siempre, un id inexistente lo rechaza el motor (500), la
API no revalida. El trigger `fn_trg_respuesta_item_tipo` (RN-24/RN-25)
tampoco se revalida en C#: es un caso limpio de D9 (a diferencia de
`chk_registro_clave` en v5, ADR D25, este `CHECK`/trigger SÍ refleja
fielmente toda la regla, sin ningún hueco que la API necesite cerrar).

## 5. Frontend: responder encuesta y administrar preguntas

```python
# routes/encuestas.py (forma, no código final)
# GET /sesiones/<id>/encuesta -> si la sesion tiene fk_encuesta Y el asistente
# autenticado (o resuelto por identificador, igual que v5) tiene un
# registro_asistencia sin respuesta todavia, muestra el formulario.
# POST /sesiones/<id>/encuesta -> arma 'respuestas' a partir del formulario
# (una entrada por pregunta, el campo que corresponda segun fk_tipo_pregunta)
# y llama POST /api/RespuestaEncuesta.
```

## 6. Docker: una migración más

Se agrega `004_sp_guardar_pregunta.sql`, montada con prefijo `z004_`
(después de `script_bdcatedras.sql` y de `z001_`-`z003_` de v2/v3).
