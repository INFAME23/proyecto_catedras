# Decisiones de diseño — Versión 10: calendario público y solicitudes de ponentes

> ⚠️ **BORRADOR · por construir** — ver nota de [2_spec.md](2_spec.md).

## Decisiones del equipo que aplican

| ADR | Decisión |
|---|---|
| D58 | `/ponente/catedras` de la v7.1 queda apagado; esta versión lo reemplaza |
| D62 | Al aprobar, el ponente recibe el enlace secreto de proyección |
| D64 | Franjas de 1 hora por fecha y sede, con una cátedra por franja y sede, y plantilla semanal |

## D77 — Los datos del ponente viven en `perfil_ponente`

**Contexto.** Los datos que pide ASIS (R 03:19, notas del compañero) incluyen:
- **perfil profesional y dirección:** no existen en ninguna tabla;
- **teléfono:** va en `asistente.celular`;
- **correo institucional:** va en `asistente.correo_institucional`;
- **cédula y tipo de documento:** van en `documento_asistente`.

**Decisión.** Una tabla nueva, `perfil_ponente`, con:
- `fk_asistente` (PK, FK a `asistente`);
- `perfil_profesional`, `direccion` y `telefono`;
- `es_externo_usb` (bool);
- `actualizado_en`.

Se llena (o se actualiza) al aprobar. La solicitud guarda una **copia** de lo
que el ponente escribió, para el historial.

**Alternativa descartada:** agregar las columnas a `asistente`, la tabla de todas
las personas. Sobrarían en el 99 % de las filas.

## D78 — El `id_evento_asis` se digita al aprobar

**Contexto.** `catedra.id_evento_asis` es `NOT NULL`, tiene 9 dígitos
(`chk_catedra_asis`) y es único (`uq_catedra_asis`). Es el código del evento
creado en ASIS (R 14:33).

**Decisión.** El formulario de aprobación pide el `id_evento_asis`. El
procedimiento lo valida, y si choca, el error sale de la BD (500 con el detalle).

**Alternativa:** aprobar sobre una cátedra que ya existe (`fk_catedra`, RF243),
cuando el evento ya está creado.

**Descartado:** inventar un código provisional, que rompería el archivo de ASIS (V11).

## D79 — Integración con la v9 (enlace de proyección)

La v9 y la v10 se construyen en paralelo.

- **Si la v9 ya está en `main` cuando se fusiona la v10:** después de aprobar, el
  front llama a `POST /api/QrSesion/{id_sesion}/enlace-proyeccion` y mete la URL
  en el correo de aprobación.
- **Si la v9 todavía no está:** el correo sale sin el enlace, y la v9, al
  fusionarse, agrega esa llamada.

La rama que llegue segunda hace la integración. Queda anotado como tarea en la
Fase 80.

## D80 — Periodo académico de la sesión

`sesion.fk_periodo` es `NOT NULL`. Al aprobar, se toma el `periodo_academico`
cuya `[fecha_ini, fecha_fin]` contiene la fecha de la franja. Si ninguno la
contiene → 500 con «V10: no hay periodo académico para la fecha …; créalo en
/admin/periodos».

## Sobre la concurrencia (RNF-V10-1)

`sp_crear_solicitud` hace `SELECT … FROM franja_disponible WHERE id = … FOR
UPDATE` antes de mirar el estado. Dos peticiones simultáneas se serializan: la
segunda ve `EN_REVISION` y falla. Es el mismo patrón que `fn_trg_sesion_consecutivo`.
