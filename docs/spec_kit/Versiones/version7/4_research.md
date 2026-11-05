# Decisiones de diseño — Versión 7: migración a ASIS, bitácora y parámetros

> ⚠️ **BORRADOR** — ver nota de [2_spec.md](2_spec.md). Revisado contra el
> repositorio y la BD real en la **Fase 54** (2026-10-15): D34 y D36 quedan
> enmendados y D37 registra las decisiones A-F del equipo
> ([guia_ia_v7.md](guia_ia_v7.md) §3).

## D34 — Ninguna operación de v7 necesita un SP *maestro-detalle* nuevo; los verbos sí pasan por SP propios (enmendado en la Fase 54)

**Contexto.** Igual que V6 con `respuesta_encuesta` (ADR D30 de
[version6/4_research.md](../version6/4_research.md)), el plan original
de este proyecto asumía que V7 necesitaba diseñar un SP nuevo para
`lote_migracion`/`detalle_migracion`. La investigación de
`script_bdcatedras.sql` (Artículo 5, obligatoria antes de diseñar
cualquier cosa nueva) mostró que `sp_generar_lote_migracion` —con su
envoltorio `fn_generar_lote_api`, apoyada en `fn_exportar_asis`— **ya
existe desde v1** y ya resuelve el maestro-detalle completo, incluido
un efecto colateral real (`sesion.estado = 'MIGRADA'`) que ninguna
spec habría pedido diseñar desde cero sin arriesgarse a no replicarlo
fielmente.

**Opciones evaluadas:** (a) diseñar un SP nuevo igual que v2/v3/v6,
asumiendo que cada versión con una relación maestro-detalle necesita
uno propio; (b) investigar primero (igual criterio que D30) y
reutilizar lo que la BD ya trae.

**Decisión: (b), con el mismo razonamiento que D30.** La Guía IA,
Regla 1 no es una cuota por versión ("cada versión agrega al menos un
SP") — es una regla sobre la OPERACIÓN ("toda creación de un maestro
con sus detalles en una sola petición pasa por un SP"), y aplicada con
cuidado, en v7 señala a un procedimiento que ya existe, no a un vacío
que haya que llenar.

**Enmienda (Fase 54).** El borrador concluía además que "V7 no agrega
ningún SP ni migración". Eso dejó de ser cierto con el retrofit del
criterio 3 de `VERSION_2.md` ("todos los verbos por procedimiento",
[PLAN_RETROFIT_SP.md](../PLAN_RETROFIT_SP.md)), que se hizo **antes**
de construir la v7 y que construyó las 4 capas ya con ese molde:

| Migración | Montaje | Procedimientos (`CALL … (…, NULL)` con `INOUT p_resultado json`) |
|---|---|---|
| `013_sp_parametro.sql` | `z013_` | listar · consultar · crear · actualizar · eliminar |
| `014_sp_bitacora.sql` | `z014_` | listar · consultar · crear · actualizar · eliminar |
| `031_sp_lote_migracion.sql` | `z031_` | listar · consultar · crear · actualizar · eliminar |
| `032_sp_detalle_migracion.sql` | `z032_` | listar · consultar · crear · actualizar · eliminar |

Lo que la decisión (b) sigue garantizando es lo importante: **ninguno
de esos SP reimplementa la generación**. `sp_crear_lote_migracion`
recibe la sesión y el usuario, hace `CALL sp_generar_lote_migracion`
(provisto) y devuelve la fila del lote. El conteo de migrables, el
nombre del archivo, los detalles y el `UPDATE sesion SET estado =
'MIGRADA'` siguen viviendo en un solo lugar. `fn_generar_lote_api`
existe en la BD pero la API no la usa (el `CALL` devuelve la fila
completa, que es lo que necesita la respuesta del `POST`).

Que un SP exista no obliga a exponerlo: `sp_eliminar_lote_migracion`,
`sp_crear_detalle_migracion`, `sp_eliminar_detalle_migracion` y los 3
de escritura de `bitacora` (D36, D37-A) se quedan sin llamador, igual
que los de `RespuestaEncuesta`, `RespuestaItem`, `RegistroAsistencia`,
`ConsentimientoDatos` y `NovedadCarga`.

**Consecuencias.** (+) El maestro-detalle de la v7 sigue sin duplicarse.
(+) Las 4 entidades siguen el molde vigente del proyecto (ningún
repositorio con SQL suelto). (−) La afirmación "v7, junto con v4, es la
versión sin migraciones" desaparece de todos los documentos: ya no es
verdad (y v4 también recibió `011`/`012` en el mismo retrofit).

## D35 — `LoteMigracion` sí tiene `PATCH` de estado (a diferencia de `LoteCargaAsistente` en v3)

**Contexto.** `LoteCargaAsistente` (v3) y `LoteMigracion` (v7) se
parecen en forma — ambos son "el registro de una ejecución por
lotes"— pero v3 decidió que, una vez cerrado, un `LoteCargaAsistente`
no cambia más (RF117 de v3). ¿Debería `LoteMigracion` seguir el mismo
criterio?

**Opciones evaluadas:** (a) mismo criterio que v3: `LoteMigracion` se
genera y punto, sin ningún verbo de actualización posterior; (b)
permitir `PATCH` sobre `fk_estado_proceso`/`observaciones`, porque el
estado de un lote de migración SÍ cambia con el tiempo según lo que
responda el sistema ASIS externo.

**Decisión: (b).** La diferencia real no es de forma, es de ciclo de
vida: un `LoteCargaAsistente` describe algo que **ya terminó** por
completo en el momento de cerrarlo (se leyó el archivo, se aceptaron o
rechazaron todas las filas, fin de la historia). Un `LoteMigracion`
describe algo que **apenas empieza** al generarse —
`estado_proceso` incluye estados explícitamente no finales
(`GENERADO`, `EN_COLA`, `EN_CURSO`) antes de llegar a uno final
(`CORRECTO`/`ERROR`/`INCORRECTO`) — y ese avance ocurre después,
típicamente por una confirmación externa que esta versión didáctica
simula con un `PATCH` manual en vez de una integración real con ASIS.
Tratarlo como v3 (sin `PATCH`) habría dejado un lote "generado" sin
forma de reflejar jamás que ASIS lo procesó.

El `PATCH` pasa por `sp_actualizar_lote_migracion` (D34, enmienda): el
repositorio consulta la fila, mezcla los campos enviados y llama al SP
con el conjunto completo (molde de `sede`). El SP también recibe
`instancia_proceso_asis`, pero la API no la expone (D37-F).

**Consecuencias.** (+) El contrato refleja el ciclo de vida real de
`estado_proceso` (una tabla que, desde v1, ya distingue estados
finales de no finales — `es_final`, `es_exitoso`). (+) Coherente con
`DetalleMigracion` (RF188), que por la misma razón también tiene
`PATCH`. (−) Ningún trigger ni proceso automatiza el avance de estado:
en un proyecto real vendría de un *webhook* o un proceso programado que
consulte a ASIS; aquí queda manual, a propósito, documentado como
simplificación didáctica (mismo espíritu que RNF12 de v5 con el correo
simulado).

## D36 — `Bitacora` no tiene ningún verbo de escritura en la API (enmendado en la Fase 54)

**Contexto.** Hasta v6, toda entidad de solo lectura (`NovedadCarga`,
`RespuestaItem`) seguía teniendo, al menos, un `POST` implícito dentro
de otra operación que la "creaba" como detalle. `Bitacora` no tiene
ni eso: ninguna operación de la API inserta una fila ahí a propósito
— lo hace la función de trigger `fn_trg_bitacora` como efecto
colateral de que cambie otra tabla.

**Opciones evaluadas:** (a) agregar algún verbo de escritura "por si
acaso" (p. ej. un `POST` para que un administrador anote manualmente
un evento de auditoría); (b) ningún verbo de escritura, en absoluto.

**Decisión: (b).** El propósito de una bitácora de auditoría es
registrar lo que **realmente** pasó en la BD, sin intervención
humana que pueda maquillarla — el Supuesto 3 del mapa de versiones
ya lo anticipaba ("`bitacora` es de solo lectura en la API"). Agregar
un `POST` manual, aunque fuera para "completar el molde", socavaría
exactamente la garantía que hace útil a una bitácora: que nadie pueda
escribir en ella salvo el propio motor de la BD, reaccionando a lo que
de verdad ocurrió.

**Enmienda (Fase 54).**
1. **Hay dos triggers, no uno.** Comprobado contra la BD:
   `trg_bitacora_registro` (`AFTER INSERT OR UPDATE OR DELETE` sobre
   `registro_asistencia`) y `trg_bitacora_asistente` (`AFTER UPDATE OR
   DELETE` sobre `asistente`), ambos con `fn_trg_bitacora`. Como el
   retrofit volvió lógico el borrado de `Asistente`, cada `DELETE
   /api/Asistente/{id}` (y cada `PUT`/`PATCH` de un asistente) agrega una
   fila `asistente / UPDATE`. De partida, las 168 filas son
   `registro_asistencia / INSERT`.
2. **El retrofit abrió la escritura y la v7 la vuelve a cerrar.** La
   segunda ronda del lote de [AJUSTES_PATRON_SEDE_LOTE.md](../AJUSTES_PATRON_SEDE_LOTE.md)
   expuso `POST`/`PUT`/`PATCH`/`DELETE /api/Bitacora` para que los 5 SP
   de `014` "tuvieran llamador", y dejó anotado el riesgo. La decisión
   D37-A lo revierte: los 5 SP se quedan en la BD (el criterio 3 se
   cumple en la base de datos) y solo `sp_listar_bitacora` y
   `sp_consultar_bitacora` tienen llamador. El criterio "si un SP existe,
   que se llame" no aplica a lo que protege un hecho auditable, con el
   mismo precedente de `RespuestaEncuesta` (D31/D32 de v6),
   `ConsentimientoDatos` (Ley 1581) y `RegistroAsistencia`.
3. **Una prueba por reflexión** en `pruebas/Programa.cs` (Fase 58)
   falla si `IRepositorioBitacora` o `IServicioBitacora` vuelven a
   declarar un método de escritura, o si `BitacoraController` vuelve a
   tener algo distinto de `[HttpGet]`.

**Consecuencias.** (+) La bitácora es, de las 37 tablas del proyecto,
la más fiel a su propósito real — ninguna capa de la API se interpone
entre el hecho y su registro. (+) Cierra el proyecto con un ejemplo
claro de que no todo necesita el molde completo de verbos: la pregunta
correcta siempre fue "¿qué necesita esta tabla?", nunca "¿cuántos
verbos le tocan por defecto?". (−) Hay que retirar código que ya
compilaba (Fase 56): 4 métodos del controlador, del servicio y del
repositorio, y las peticiones `BitacoraCrear`, `BitacoraReemplazo` y
`BitacoraActualizar`.

## D37 — Decisiones del equipo al revisar el borrador contra el repositorio (Fase 54)

**Contexto.** El borrador de la v7 se escribió **antes** del retrofit a
procedimientos; las capas de las 4 entidades ya existían cuando se
retomó la versión. La Fase 54 comparó el borrador con el código y con la
BD real (`script_bdcatedras.sql` + migraciones `001`-`041` en
PostgreSQL 16, mismo orden que `docker-entrypoint-initdb.d`) y encontró
16 diferencias ([guia_ia_v7.md](guia_ia_v7.md) §2). Las que no eran un
simple error del documento las decidió el equipo el **2026-10-15**,
todas con la opción recomendada en la guía. La v6 ya está cerrada con
su tag `v6` (Artículo 1).

| | Pregunta | Decisión | Qué cambia |
|---|---|---|---|
| **A** | ¿La API puede escribir en la bitácora? | **No: solo lectura** (`GET` lista y `GET/{id}`) | Fase 56 retira la escritura (ver D36, enmienda) |
| **B** | ¿camelCase o snake_case en el JSON? | **snake_case** en cuerpos y respuestas con `[JsonPropertyName]` (`fk_sesion`, `fk_estado_proceso`, `tipo_dato`), igual que los 33 recursos de v1-v6; parámetros de consulta en camelCase (`?fkSesion=`, `?fkLoteMigracion=`, `?nombreTabla=`) | Solo documentos. La respuesta de generar queda `{estado, mensaje, id_lote_migracion, lote}` |
| **C** | ¿Filtro de bitácora por usuario? | **No.** `fk_usuario` es `NULL` en todas las filas porque ningún endpoint propaga el usuario autenticado a la conexión de PostgreSQL: el filtro siempre devolvería vacío (YAGNI, Artículo 1) | `GET /api/Bitacora?nombreTabla=&limite=`; el front filtra por tabla, no por usuario |
| **D** | Validación de `Parametro` | `clave` con `[RegularExpression("^[A-Z][A-Z0-9_]*$")]` (cumplen las 9 claves sembradas). `tipo_dato` **opcional** en `POST` y `PATCH` (la BD pone `TEXTO`), obligatorio en `PUT` (reemplazo completo). **Sin** validar coherencia `valor`/`tipo_dato` | Fase 57 agrega la expresión regular a `ParametroCrear.Clave` → 422 |
| **E** | ¿Dónde vive el front de la v7? | **Dentro de `routes/admin.py`**, para heredar la guardia `before_request` de `admin_bp` (rol `ADMIN`, D28). "Generar migración" es `POST /admin/sesiones/<id>/migrar`; el botón de `detalle_sesion.html` solo se pinta para un usuario `ADMIN` | Fase 60. Sin blueprints `migracion.py`/`bitacora.py`/`parametros.py` |
| **F** | ¿`instancia_proceso_asis` en el `PATCH` del lote? | **No se expone.** El SP la recibe y el repositorio conserva el valor actual al mezclar | Ninguno (Artículo 1: nadie la pidió) |

**Correcciones de hecho (no requerían decisión)** que se aplicaron a los
documentos en la misma fase:

- `total_filas` = `count(*)` de `fn_exportar_asis`, que **excluye**
  externos y a quien no tiene `id_asis` o programa. En 5712: 168
  registros, 1 externo, 166 migrables = `total_filas` = filas de detalle
  (corrige RF185).
- `modificado_en = now()` lo pone `sp_actualizar_parametro`, no el
  repositorio; sigue sin existir un trigger (corrige RF197).
- La sesión 5712 ya está `MIGRADA` en los datos sembrados: para que el
  criterio 2 pruebe algo hay que ponerla en `CERRADA` antes de generar.
- El lote 1 sembrado (`CORRECTO`) ya tiene 164 de los 166 registros
  migrables en `ACEPTADO`: solo los registros **167** y **169** admiten
  un `ACEPTADO` nuevo; cualquier otro choca con `ux_detmig_aceptado` al
  primer intento.
- Un verbo no declarado sobre una ruta que existe con otro verbo
  (`PUT /api/LoteMigracion/1`, `POST /api/Bitacora`…) lo responde
  ASP.NET Core con **405 Method Not Allowed**, no 404 (comprobado en la
  Fase 59 en los 9 casos: 4 de `Bitacora`, 2 de `LoteMigracion` y 3 de
  `DetalleMigracion`).
- Sin 409 en esta versión: cambiar 500 → 409 para FK y `UNIQUE` es una
  decisión de **todo** el proyecto (criterio 7 de `VERSION_2.md`,
  pendiente en `ESTADO_V6_Y_CUMPLIMIENTO.md`), no de la v7 sola.

**Consecuencias.** (+) Ningún documento de `version7/` contradice ya al
código ni a la BD. (+) El trabajo de las Fases 55-57 pasa de "construir"
a "auditar y alinear", con solo dos cambios de código decididos (A y D).
(−) La v7 deja de ser "la versión sin migraciones": es la que demuestra
que reutilizar un SP provisto y tener SP propios por verbo son cosas
compatibles.
