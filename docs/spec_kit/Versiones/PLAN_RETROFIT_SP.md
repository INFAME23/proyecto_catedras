# Plan: todos los verbos por procedimiento almacenado (criterio 3)

> Alcance de este plan: **solo** el criterio 3 de `VERSION_2.md` ("los cinco
> verbos de todas las tablas pasan por procedimientos almacenados"). El 409
> (criterio 7) y el borrado lógico donde falte quedaron fuera a propósito —
> se tocan en un plan aparte para no mezclar dos refactors grandes a la vez.
> Aunque sí: al convertir cada tabla a `sp_eliminar_<tabla>`, el borrado
> lógico queda resuelto de una vez para esa tabla (está dentro del mismo
> procedimiento).

---

## 1. El patrón, probado contra `sede`

Ya está construido y aplicado (no solo diseñado) sobre la tabla más simple
del esquema, para validar el molde antes de repetirlo 32 veces:

- `db/postgres/migraciones/005_sp_sede_ejemplo.sql` — los 5 procedimientos.
- `api_catedras/Repositorios/RepositorioSedePostgres.cs` — reescrito para
  llamarlos.

**Lo importante: `IRepositorioSede.cs`, `ServicioSede.cs` y
`SedeController.cs` NO cambiaron ni una línea.** Esto es a propósito — es
justo lo que permite la Inversión de Dependencias (Artículo 3 de la
constitución): el contrato público del repositorio (qué recibe cada
método, qué devuelve) se mantuvo igual; solo cambió CÓMO llega a la base
de datos por dentro. Eso significa que convertir las otras 32 tablas **no
debería tocar Controllers, Servicios ni Peticiones** — si al hacerlo
alguna de esas capas necesita cambiar, es señal de que algo del molde no
encajó, deténganse y revisen antes de seguir.

### Las 3 decisiones de diseño que valen para las 33 tablas

1. **Un solo `sp_actualizar_<tabla>` por tabla**, que reemplaza TODAS las
   columnas de negocio (sirve para PUT directo). El PATCH parcial ya no
   arma un `UPDATE` dinámico en SQL: el repositorio **consulta la fila
   actual, mezcla encima los campos que llegaron, y llama al mismo
   procedimiento con el conjunto completo** (ver el método `ActualizarAsync`
   de `RepositorioSedePostgres.cs` como plantilla exacta).
2. **`sp_eliminar_<tabla>`:** `UPDATE ... SET activo = false` si la tabla
   tiene columna `activo`; `DELETE` si no la tiene. Siempre devuelve
   `json_build_object('eliminado', FOUND)`.
3. **`p_resultado` es `json`, no `jsonb`** (igual que el ejemplo del
   profesor) — un procedimiento, un nombre, una fila o un arreglo adentro.
   `NULL` en `p_resultado` = "no se encontró" (lo lee el repositorio igual
   que hoy lee un `null` de Dapper).

---

## 2. Inventario completo — 33 repositorios, 1 ya convertido

### Grupo A — CRUD completo (5 verbos), mismo molde exacto que `sede`

| Tabla | ¿Tiene `activo`? | Estado |
|---|---|---|
| **Sede** | Sí | ✅ Hecho (prueba de concepto) |
| Catedra | Sí | Pendiente — tiene además `Contar`, y FK a facultad/dependencia/tipo_evento |
| Dependencia | Sí | Pendiente |
| Facultad | Sí | Pendiente |
| Modalidad | Sí | Pendiente |
| PeriodoAcademico | Sí | Pendiente |
| ProgramaAcademico | Sí | Pendiente — es maestro de `AliasPrograma` (Grupo B) |
| TipoDocumento | Sí | Pendiente |
| TipoEvento | Sí | Pendiente |
| TipoVinculacion | Sí | Pendiente |
| Asistente | Sí | Pendiente — es maestro-detalle en `CrearAsync` (ya usa `sp_guardar_asistente`, eso no cambia; solo Listar/Consultar/Actualizar/Eliminar) |
| Usuario | Sí | Pendiente — ⚠️ no tocar la clave: `ServicioUsuario` sigue siendo el único que ve la clave en texto plano / hashea con BCrypt, el repositorio nunca decide eso |
| DocumentoAsistente | No | Pendiente — `sp_eliminar` queda `DELETE` (no tiene `activo`) |
| Encuesta | No | Pendiente |
| EstadoProceso | No | Pendiente |
| OpcionPregunta | No | Pendiente (v6, construida hoy) |
| Pregunta | No | Pendiente (v6, construida hoy) — su `CrearAsync` ya usa `sp_guardar_pregunta` (maestro-detalle con opciones), eso no cambia |
| Rol | No | Pendiente |
| Sesion | No | Pendiente — ⚠️ tiene el trigger `trg_sesion_consecutivo` que calcula `numero_reunion`: el SP debe devolver la fila COMPLETA después del INSERT (como el ejemplo del profesor), no antes, para que venga con el número ya puesto |
| TipoPregunta | No | Pendiente |

**19 tablas pendientes de este grupo** (20 con `sede` ya hecha).

### Grupo B — CRUD reducido (sin `Actualizar`, o sin `Actualizar` ni `Eliminar`)

Mismo molde, pero sin el procedimiento que no aplica.

| Tabla | Verbos reales | Nota |
|---|---|---|
| AliasPrograma | Listar, Consultar, Crear, Eliminar | `CrearAsync` puede seguir siendo parte de `sp_guardar_programa_academico` (maestro-detalle) O tener su propio `sp_crear_alias_programa` si se agrega suelto — revisar cuál aplica caso por caso |
| ConsentimientoDatos | Listar, Consultar, Crear | Hecho auditable — nunca tendrá `sp_actualizar` ni `sp_eliminar`, y eso está bien |
| Ponencia | Listar, Consultar, Crear, Eliminar | Sin `Actualizar` |

### Grupo C — Acciones especiales (su verbo de escritura YA pasa por función/SP; falta Listar/Consultar)

Buena noticia: a estas **ya les falta menos**. Su operación de escritura
principal ya es una función o procedimiento (`fn_emitir_enlace_api`,
`fn_responder_encuesta_api`, `fn_solicitar_clave_api`,
`fn_validar_clave_api`, `sp_guardar_lote_carga`...) — eso YA cumple el
criterio 3 para ESE verbo. Lo que falta es convertir `Listar`/`Consultar`,
que hoy siguen siendo `SELECT` a mano.

| Tabla | Verbo(s) de escritura (ya en SP/función) | Falta convertir |
|---|---|---|
| ClaveAcceso | Solicitar, Validar | Listar, Consultar |
| EnlaceRegistro | Emitir, Revocar (Revocar es un `UPDATE` suelto hoy — convertir también) | Listar, Consultar, Revocar |
| LoteCargaAsistente | Crear, Cerrar | Listar, Consultar, Cerrar |
| NovedadCarga | (sin escritura: nace del lote) | Listar, Consultar |
| ProgramaAsistente | Crear, CambiarEstado | Listar, CambiarEstado |
| RegistroAsistencia | Crear | Listar, Consultar |
| RespuestaEncuesta | Responder | Listar, Consultar |
| RespuestaItem | (sin escritura: nace de Responder) | Listar, Consultar |
| RolPorUsuario | Crear, Cerrar | Listar, ObtenerVigentes, Cerrar |
| VinculacionAsistente | Crear, Cerrar | Listar, Cerrar |

**10 tablas**, todas con trabajo parcial.

---

## 3. Fases y a quién le queda mejor cada una

No hay una sola forma correcta de repartir esto — es una sugerencia según
los roles que ya tienen en el proyecto. Ajusten según quién tiene tiempo
esta semana.

| Fase | Qué | Tablas | Sugerido |
|---|---|---|---|
| **1** | Prueba de concepto | Sede | ✅ Hecho hoy |
| **2** | Catálogos simples v1 (copiar el molde de `sede` tal cual, sin FK complicadas) | Dependencia, Facultad, Modalidad, PeriodoAcademico, TipoDocumento, TipoEvento, TipoVinculacion, Rol, TipoPregunta, EstadoProceso | **German** (9-10 tablas, mecánico una vez que el molde está probado — buen primer lote para medir cuánto tardan las demás) |
| **3** | v1 con FK + v2 (maestro-detalle) | Catedra, ProgramaAcademico, AliasPrograma, Encuesta | **Jhoan** (coordina porque `ProgramaAcademico`/`AliasPrograma` es el primer maestro-detalle que se toca) |
| **4** | v3 completo | Asistente, DocumentoAsistente, ConsentimientoDatos, LoteCargaAsistente, NovedadCarga, VinculacionAsistente, ProgramaAsistente | **Isaac** (y de paso, como tester, deja escritas/actualizadas las pruebas de cada una) |
| **5** | v4 + v5 | Sesion, Ponencia, TipoPregunta, Usuario, RolPorUsuario, EnlaceRegistro, ClaveAcceso, RegistroAsistencia | **German** (segundo lote, ya con práctica de la Fase 2) — ⚠️ avisar del cuidado con `Sesion` (trigger) y `Usuario` (BCrypt) de la tabla de arriba |
| **6** | v6 (construida hoy) | Pregunta, OpcionPregunta, RespuestaEncuesta, RespuestaItem | **Alex** (quien ya conoce esas 4 clases de hoy) |
| **7** | Verificación completa | — | **Isaac**: correr TODA la colección de pruebas (incluida la regresión de v1, obligatoria por §9) contra Docker real, tabla por tabla |

### Orden sugerido entre fases

**2 → (3 y 4 en paralelo) → 5 → 6 → 7.** La Fase 2 primero porque es la más
mecánica y deja a todo el equipo con el patrón ya practicado antes de
tocar las tablas con FK o maestro-detalle.

---

## 4. Prompt para repartir (Fase 2, el lote más grande)

```
Necesito convertir estos repositorios para que sus 5 verbos pasen por
procedimientos almacenados, EXACTAMENTE con el mismo patrón que ya está
aplicado en 'sede' — ábrelos primero como plantilla:
  db/postgres/migraciones/005_sp_sede_ejemplo.sql
  api_catedras/Repositorios/RepositorioSedePostgres.cs
Tablas de este lote: dependencia, facultad, modalidad, periodo_academico,
tipo_documento, tipo_evento, tipo_vinculacion, rol, tipo_pregunta,
estado_proceso.

Para cada una:
1. Un archivo de migración nuevo (siguiente número disponible en
   db/postgres/migraciones/) con sp_listar_/sp_consultar_/sp_crear_/
   sp_actualizar_/sp_eliminar_<tabla>, mismas columnas que la tabla real
   (revísalas en db/postgres/script_bdcatedras.sql).
2. sp_eliminar: UPDATE activo=false SI la tabla tiene columna 'activo'
   (todas las de este lote la tienen, confírmalo igual), DELETE si no.
3. Monta cada migración en docker-compose.yml con el siguiente prefijo
   z0XX_ disponible.
4. Reescribe Repositorio<Tabla>Postgres.cs para llamar a los 5
   procedimientos (CALL + leer el JSON que vuelve), con el mismo patrón
   de 'ActualizarAsync' de sede.cs (consultar, mezclar, llamar).
5. NO toques IRepositorio<Tabla>.cs, Servicio<Tabla>.cs ni
   <Tabla>Controller.cs — si crees que necesitas tocarlos, para y
   avísame, algo no encajó.
Al final dime qué tuviste que corregirme o ajustar del patrón de sede
para que funcionara en cada tabla — eso lo necesito para el entregable de
prompts (§11 de VERSION_2.md).
```

---

## 5. Lo que todavía no cubre este plan

- **Criterio 7 (409 en vez de 500):** aparte, ver `ESTADO_V6_Y_CUMPLIMIENTO.md`
  parche 4. Tiene sentido hacerlo DESPUÉS de este retrofit, no durante: los
  procedimientos no cambian qué excepción lanza PostgreSQL, así que el
  orden no importa mucho, pero mezclar los dos cambios a la vez hace más
  difícil saber cuál rompió qué si algo falla.
- **Probar contra Docker real:** lo de `sede` está escrito pero no lo pude
  correr en este entorno (sin `dotnet` ni Docker). Antes de replicarlo 32
  veces, alguien debería levantar `docker compose up -d --build` y probar
  los 5 verbos de `sede` por Swagger o la colección de pruebas.
