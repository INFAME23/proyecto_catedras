# Modelo de datos — Versión 1: la BD completa (dada) y la tabla Sede

> **Versión 1** · La base de datos NO se diseña en esta versión: **viene
> dada** (script SQL provisto). Este documento describe lo que hay y lo
> único que la v1 puede tocar.
>
> **Nota de alcance:** el dominio real de `bdcatedras` es un sistema de
> **asistencia a cátedras universitarias** (facultades, programas,
> sesiones, asistentes, encuestas), no un sistema de facturación. Este
> documento sigue el mismo formato de la plantilla de referencia del
> proyecto, pero todo el contenido — columnas, reglas, datos de ejemplo —
> corresponde a la tabla `sede` **real** definida en el script.

---

## 1. La base de datos `bdcatedras` (dada, completa)

El script **provisto** `script_bdcatedras.sql` (dialecto PostgreSQL) crea
el esquema **`catedras`** dentro de la base `bdcatedras`. PostgreSQL lo
ejecuta SOLO la PRIMERA vez (está pensado para montarse en
`/docker-entrypoint-initdb.d/` y correr cuando el volumen de datos nace
vacío).

**37 tablas** agrupadas en seis módulos:

```
NÚCLEO ACADÉMICO                        ASISTENTES Y VINCULACIÓN
facultad ── dependencia                 asistente ── documento_asistente
   ↑                                       ↑            ↑
programa_academico ── alias_programa      tipo_documento│
   ↑                                       │        vinculacion_asistente
catedra ── periodo_academico               │            ↑
   ↑            ↑                          │      tipo_vinculacion
modalidad ── sesion ── Sede                programa_asistente
                ↑                          consentimiento_datos
          registro_asistencia

REGISTRO Y ACCESO                        ENCUESTAS
registro_asistencia ── estado_proceso    encuesta ── pregunta ── tipo_pregunta
clave_acceso                                              ↑
enlace_registro                                     opcion_pregunta
                                          respuesta_encuesta ── respuesta_item

CARGA / MIGRACIÓN                        SEGURIDAD Y AUDITORÍA
lote_carga_asistente ── novedad_carga    usuario ── rol_por_usuario ── rol
lote_migracion ── detalle_migracion      bitacora
                                          parametro
OTROS
ponencia
```

Además: **funciones y triggers** (`fn_trg_bitacora`, `fn_trg_sesion_consecutivo`,
`fn_trg_registro_validar`, `fn_trg_clave_derivar`, `fn_trg_enlace_canal`,
`fn_trg_respuesta_item_tipo`, entre otras) y **funciones de consulta/API**
(`fn_resolver_asistente`, `fn_exportar_asis`, `fn_generar_lote_api`, etc.) —
todas esperando a las versiones siguientes.

**Datos de ejemplo:** 3 Sedes (`SAN_BENITO`, `BELLO`, `VIRTUAL`), más
facultades, programas, cátedras, sesiones, asistentes y encuestas de
ejemplo repartidos en las otras 36 tablas.

## 2. Lo ÚNICO que la v1 puede nombrar: la tabla `sede`

> Comentario del propio esquema (`COMMENT ON TABLE catedras.sede`):
> *"Lugar físico o virtual donde se dicta una sesión [...] LA SEDE ES LO
> QUE DISTINGUE DOS CÁTEDRAS A LA MISMA HORA. Como texto libre en sesión,
> el informe por sede no se puede escribir."*

| Columna | Tipo (PostgreSQL) | Regla |
|---|---|---|
| `id_sede` | `VARCHAR(15)` | **PK** — texto de 1 a 15 caracteres |
| `nombre` | `VARCHAR(80)` | No nulo, no vacío, **único** (`uq_sede_nombre`) |
| `direccion` | `VARCHAR(200)` | Opcional (nulo permitido) |
| `es_virtual` | `BOOLEAN` | No nulo, `DEFAULT false` |
| `activo` | `BOOLEAN` | No nulo, `DEFAULT true` |

En C#, esa fila viaja como el modelo entidad:

```csharp
public class Sede
{
    public required string IdSede { get; set; }
    public required string Nombre { get; set; }
    public string? Direccion { get; set; }
    public bool EsVirtual { get; set; } = false;
    public bool Activo { get; set; } = true;
}
```

**Relación saliente (fuera del alcance de v1, pero presente en la BD):**
`sesion.fk_sede → sede.id_sede`, `ON UPDATE CASCADE ON DELETE RESTRICT`.
Es decir, no se puede borrar una sede que tenga sesiones asociadas — la
BD lo rechaza aunque la v1 no toque `sesion`.

## 3. Las dos murallas de validación

1. **La API** (las peticiones por verbo con anotaciones): forma, tipos y
   rangos → 422 con lista de errores, ANTES de tocar la BD.
2. **La BD** (PK, NOT NULL, UNIQUE y FK): la última línea de defensa —
   un `id_sede` duplicado viola la PK, un `nombre` repetido viola
   `uq_sede_nombre`, y el motor los rechaza aunque la API tuviera un bug
   (la API lo reporta como 500 con el error del motor). Igualmente, si en
   una versión futura se intenta borrar una sede en uso, `fk_sesion_sede`
   la bloquea con `RESTRICT`.

## 4. Reglas de esta versión

- El código de la v1 **solo puede nombrar `sede`** — las otras 36 tablas
  existen pero son territorio de versiones posteriores.
- La BD **no se modifica**: ni columnas nuevas, ni índices, ni datos
  semilla distintos. Si algo parece faltar, es de otra versión.
- El reset completo es de Docker, no de SQL:
  `docker compose down -v && docker compose up -d` (borra el volumen y el
  inicializador vuelve a crear todo).