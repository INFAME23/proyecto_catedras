# Decisiones de diseño — Versión 7: migración a ASIS, bitácora y parámetros

> ⚠️ **BORRADOR** — ver nota de [2_spec.md](2_spec.md).

## D33 — Ninguna operación de v7 necesita un SP nuevo (segunda corrección del plan original)

**Contexto.** Igual que V6 con `respuesta_encuesta` (ADR D29 de
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
uno propio; (b) investigar primero (igual criterio que D29) y
confirmar que, de las 7 versiones del proyecto, exactamente 3 (v2, v3,
v6) necesitaron un SP nuevo — las otras 4 (v1, v4, v5, v7) no, porque
o no tienen maestro-detalle real (v1, v4) o porque la BD ya lo resolvía
desde el principio (v5, v7).

**Decisión: (b), con el mismo razonamiento que D29.** La Guía IA,
Regla 1 no es una cuota por versión ("cada versión agrega al menos un
SP") — es una regla sobre la OPERACIÓN ("toda creación de un maestro
con sus detalles en una sola petición pasa por un SP"), y aplicada con
cuidado, en v7 señala a una función que ya existe, no a un vacío que
haya que llenar. **Esta es la segunda vez que el mismo tipo de
suposición resulta incorrecta** — un patrón que vale la pena nombrar
explícitamente: cuando un plan de versiones se escribe sin haber leído
primero el script completo de la BD, es más probable de lo que
parece que asuma trabajo que la BD ya hizo.

**Consecuencias.** (+) V7 termina siendo, junto con v4, la versión con
menos código nuevo del proyecto — apropiado para la última fila del
mapa, cuyo trabajo real es exponer, no diseñar. (+) Deja un
precedente explícito (aquí y en D29) de que "investigar antes de
construir" no es un paso ceremonial del Artículo 5: cambió el
resultado real de 2 versiones distintas. (−) Ninguna: el "costo" fue
solo el tiempo de verificar, mínimo comparado con el de haber
construido y luego descubierto la duplicación.

## D34 — `LoteMigracion` sí tiene `PATCH` de estado (a diferencia de `LoteCargaAsistente` en v3)

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

**Consecuencias.** (+) El contrato refleja el ciclo de vida real de
`estado_proceso` (una tabla que, desde v1, ya distingue estados
finales de no finales — `es_final`, `es_exitoso`). (+) Coherente con
`DetalleMigracion` (RF188), que por la misma razón también tiene
`PATCH`. (−) Es la primera tabla del proyecto que nace con un
`PATCH` pero sin ningún SP ni trigger que automatice su avance de
estado — en un proyecto real, esa actualización vendría de un *webhook*
o un proceso programado que consulte a ASIS; aquí queda manual, a
propósito, documentado como simplificación didáctica (mismo espíritu
que RNF11 de v5 con el correo simulado).

## D35 — `Bitacora` es la única entidad del proyecto sin ningún verbo de escritura

**Contexto.** Hasta v6, toda entidad de solo lectura (`NovedadCarga`,
`RespuestaItem`) seguía teniendo, al menos, un `POST` implícito dentro
de otra operación que la "creaba" como detalle. `Bitacora` no tiene
ni eso: ninguna operación de la API inserta una fila ahí a propósito
— lo hace un trigger (`trg_bitacora_registro`) como efecto colateral
de que `registro_asistencia` cambie.

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

**Consecuencias.** (+) La bitácora es, de las 37 tablas del proyecto,
la más fiel a su propósito real — ninguna capa de la API se interpone
entre el hecho y su registro. (+) Cierra el proyecto con un ejemplo
claro de que no todo necesita el molde completo de verbos: la pregunta
correcta siempre fue "¿qué necesita esta tabla?", nunca "¿cuántos
verbos le tocan por defecto?". (−) Ninguna: es exactamente el
comportamiento que el propósito de la tabla exige.
