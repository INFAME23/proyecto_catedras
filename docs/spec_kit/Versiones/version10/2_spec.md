# Especificación — Versión 10 del proyecto: calendario público y solicitudes de ponentes

> ⚠️ **BORRADOR · por construir.** Depende de la **v8 fusionada en `main`** y se
> construye **en paralelo con la [v9](../version9/2_spec.md)** (D70). Su único
> punto de contacto con la v9 es enviar el enlace secreto de proyección al
> aprobar (D79).
>
> Rige la [constitución](../../1_constitution.md). Plan:
> [../plan_v8_v11.md](../plan_v8_v11.md). Decisiones: D62, D64 en
> [../decisiones_v8_v11.md](../decisiones_v8_v11.md) y D77–D80 en
> [4_research.md](4_research.md). Guía: [guia_ia_v10.md](guia_ia_v10.md).
>
> | Documento | Contenido |
> |---|---|
> | **2_spec.md** (este) | Qué construir y sus criterios |
> | [3_plan.md](3_plan.md) | Cómo |
> | [4_research.md](4_research.md) | D62, D64, D77–D80 |
> | [5_data_model.md](5_data_model.md) | Migraciones 061–063 |
> | [6_contracts.md](6_contracts.md) | Endpoints |
> | [7_quickstart.md](7_quickstart.md) | Prueba |
> | [8_tasks.md](8_tasks.md) | Fases 74–80 |

---

## 1. Propósito de la v10

Hoy el ponente da sus datos varias veces, y la oficina vuelve a digitarlos para
ASIS, para los certificados y para SNIES (R 14:21–15:34). El cliente pidió que:

- el ponente vea un **calendario** con las franjas libres y ocupadas (R 05:01, R 07:45);
- **pida** su cátedra con sus datos desde ahí, sin cuenta (R 08:33);
- el administrador **la apruebe o la rechace** desde una **bandeja de entrada**
  en el aplicativo, no desde el correo (R 08:40–09:13);
- al aprobarla, todo quede creado (R 14:51–15:11).

```
Admin ── programa ──> franja_disponible (fecha, hora, sede: LIBRE)
Ponente ── /calendario (público) ── pincha una franja LIBRE ──> solicitud_catedra (PENDIENTE)
                                                                 franja: EN_REVISION
Admin ── /admin/solicitudes ──┬─ aprobar ─> asistente + perfil_ponente + catedra + sesion + ponencia
                              │             franja: OCUPADA · correo al ponente (con el enlace de proyección, D79)
                              └─ rechazar ─> franja: LIBRE · correo con el motivo
```

## 2. Alcance

**Incluye:**
- franjas disponibles y su plantilla semanal (RF229–RF233);
- el calendario público (RF234–RF235);
- la solicitud del ponente (RF236–RF239);
- la bandeja del administrador, la aprobación y el rechazo (RF240–RF246);
- las sesiones especiales (RF247).

**No incluye:**
- el certificado en PDF ni el reporte a SNIES: son de la V11 (HU-23);
- el QR dinámico: es de la V9.

## 3. Requisitos funcionales

La numeración arranca en RF229 (la v9 usa RF218–RF228).

### Franjas (D64)

- **RF229 — Crear una franja.** `POST /api/FranjaDisponible` con
  `{fecha, hora_inicio, duracion_minutos?, fk_sede, fk_modalidad}`:
  - la duración es de 60 minutos por defecto;
  - la franja nace `LIBRE`;
  - no puede haber dos franjas de la misma sede que se solapen (EXCLUDE con
    `tstzrange`).
- **RF230 — Listar y obtener franjas.**
  `GET /api/FranjaDisponible?desde=&hasta=&fkSede=&estado=` y `GET …/{id}`.
- **RF231 — Bloquear o liberar.** `PATCH /api/FranjaDisponible/{id}` con
  `{estado: "BLOQUEADA" | "LIBRE"}`. No se puede tocar una franja `OCUPADA` ni
  una `EN_REVISION`.
- **RF232 — Eliminar.** `DELETE /api/FranjaDisponible/{id}`, solo si está `LIBRE`
  o `BLOQUEADA`.
- **RF233 — Plantilla semanal.** `POST /api/FranjaDisponible/plantilla` con
  `{desde, hasta, fk_sede, fk_modalidad, dias: [1..7], horas: ["06:00", "07:00", …]}`:
  - crea las franjas de esos días y esas horas entre las dos fechas;
  - omite las que ya existan o se solapen;
  - responde `{creadas, omitidas}`.

### Calendario público

- **RF234 — Calendario del mes.**
  `GET /api/FranjaDisponible/calendario?anio=&mes=&fkSede=` devuelve por día las
  franjas con `estado` y, si está `OCUPADA`, el nombre de la cátedra. Nunca
  devuelve datos del ponente.
- **RF235 — Vista.** `/calendario` (sin login) muestra el mes en una cuadrícula:
  - **verde** = libre, **rojo** = ocupada, **gris** = en revisión o bloqueada;
  - se puede cambiar de mes y filtrar por sede;
  - al pinchar una franja libre se abre el formulario de solicitud (popup).

### Solicitud del ponente

- **RF236 — Enviar una solicitud.** `POST /api/SolicitudCatedra` (pública) con:
  - **la cátedra:** `fk_franja`, `nombre_catedra`, `resumen_ponencia` y
    `fk_tipo_evento` (opcional);
  - **el ponente:** `tipo_documento`, `numero_documento`, `nombres`, `apellidos`,
    `perfil_profesional`, `direccion`, `telefono`, `correo_institucional`,
    `es_externo` y `acepta_datos`.
  - En un `CALL` bloquea la franja (`FOR UPDATE`), comprueba que siga `LIBRE`,
    inserta la solicitud `PENDIENTE` y pone la franja `EN_REVISION`.
  - Si la franja ya no está libre → 500 con «V10: esa franja ya no está disponible».
- **RF237 — Campos del ponente (notas del compañero, R 03:19).**
  - Obligatorios: cédula, tipo de documento, perfil profesional, teléfono,
    correo institucional y resumen de la ponencia.
  - La dirección es obligatoria solo si es nuevo: no tiene documento registrado.
- **RF238 — Consentimiento.** `acepta_datos` debe ser `true` (422). Al aprobar se
  registra en `consentimiento_datos`.
- **RF239 — Confirmación.** La respuesta trae `{id_solicitud, estado:
  "PENDIENTE"}`. El front le envía al ponente un correo de "solicitud recibida".

### Bandeja y aprobación

- **RF240 — Bandeja.** `GET /api/SolicitudCatedra?estado=PENDIENTE` (por defecto,
  de la más antigua a la más reciente) y `GET …/{id}`.
- **RF241 — Contador.** `GET /api/SolicitudCatedra/pendientes` → `{total}`. El
  panel `/admin` lo muestra como un aviso.
- **RF242 — Aprobar (D78).** `POST /api/SolicitudCatedra/{id}/aprobar` con
  `{id_evento_asis, fk_tipo_evento, fk_dependencia?, fk_encuesta?, fk_usuario?}`.
  En un `CALL`:
  1. Comprueba que la solicitud siga `PENDIENTE`.
  2. Crea o reutiliza al ponente: `asistente`, `documento_asistente`,
     `vinculacion_asistente`, `perfil_ponente` (D77) y `consentimiento_datos`.
  3. Crea la `catedra` con el `id_evento_asis` digitado (9 dígitos, único).
  4. Crea la `sesion`, con su periodo deducido de la fecha (D80).
  5. Crea la `ponencia` con `rol = 'PONENTE'`.
  6. Pone la franja `OCUPADA` (con `fk_sesion`) y la solicitud `APROBADA`.

  Responde `{id_catedra, id_sesion, correo_ponente}`.
- **RF243 — Aprobar sobre una cátedra existente.** Si el cuerpo trae
  `fk_catedra` en vez de `id_evento_asis`, la sesión se agrega a esa cátedra (una
  reunión más, numerada por `trg_sesion_consecutivo`).
- **RF244 — Rechazar.** `POST /api/SolicitudCatedra/{id}/rechazar` con
  `{motivo}` (obligatorio): la solicitud queda `RECHAZADA` y la franja vuelve a
  `LIBRE`.
- **RF245 — Correos al ponente.** El front envía, con `servicios/correo.py`:
  - "Solicitud recibida" (RF239);
  - "Tu cátedra fue aprobada": incluye fecha, sede y, si la v9 está, el enlace de
    proyección (D79);
  - "Tu solicitud fue rechazada", con el motivo.

  Sin SMTP configurado, se aplica `CORREO_MODO_CONSOLA`, como en la v7.1.
- **RF246 — Historial.** Una solicitud aprobada o rechazada guarda
  `resuelta_en`, `fk_usuario_resuelve` y `motivo`, y no se puede volver a
  resolver.

### Sesiones especiales

- **RF247 — Sesión especial (R 06:56).** Desde `/admin`, el administrador puede
  crear sesiones de cualquier duración y fuera de las franjas (la pantalla de la
  v4 sigue). Si la sesión cae sobre franjas `LIBRE` de la misma sede, el sistema
  ofrece marcarlas `OCUPADA`.

## 4. Requisitos no funcionales

- **RNF-V10-1:** dos ponentes que piden la misma franja al mismo tiempo: solo una
  solicitud queda; la otra recibe el mensaje de RF236.
- **RNF-V10-2:** el calendario del mes responde en menos de 1 s con 300 franjas.
- **RNF-V10-3:** ningún dato del ponente es visible sin el login de admin.

## 5. Criterios de aceptación

1. Con la plantilla, el administrador crea las franjas de lunes a viernes de
   6:00 a 20:00 en San Benito para un mes.
2. Un ponente sin cuenta pide una franja libre: la franja pasa a gris y el panel
   muestra «1 solicitud pendiente».
3. Al aprobar con un `id_evento_asis` nuevo aparecen la cátedra, la sesión y la
   ponencia; la franja queda en rojo; el ponente recibe el correo.
4. Al rechazar con un motivo, la franja vuelve a verde y el ponente recibe el
   motivo.
5. Dos solicitudes simultáneas a la misma franja: solo una queda.
6. Un `id_evento_asis` repetido o que no tiene 9 dígitos → error claro, y no se
   crea nada.
