# Especificación — Versión 8 del proyecto: registro de asistencia sin login, por tipo de usuario

> ⚠️ **BORRADOR · por construir.** Nace de la reunión con el cliente del
> 2026-11-04 y de las decisiones D55–D70 del equipo
> ([../decisiones_v8_v11.md](../decisiones_v8_v11.md)). Antes de construir se
> revisa contra el código y la BD reales (Fase 62) y se corrige donde no
> coincida. El banner se quita cuando pase `pruebas/correr_todo_v8.ps1`; ese
> mismo día se crea el tag `v8`.
>
> Rige la [constitución](../../1_constitution.md) y la
> [guía operativa para la IA](../../guia_ia.md). Plan de las versiones nuevas:
> [../plan_v8_v11.md](../plan_v8_v11.md). Historias de usuario:
> [../historias_usuario_cliente.md](../historias_usuario_cliente.md).
> Guía de esta versión: [guia_ia_v8.md](guia_ia_v8.md).
>
> | Documento de esta versión | Contenido |
> |---|---|
> | **2_spec.md** (este) | QUÉ construir en la v8 y sus criterios de aceptación |
> | [3_plan.md](3_plan.md) | CÓMO: capas, archivos que se tocan y qué se apaga |
> | [4_research.md](4_research.md) | Decisiones y alternativas (D55–D61, D66, D67 y las nuevas D71–D73) |
> | [5_data_model.md](5_data_model.md) | Migraciones 054–057 y procedimientos de registro |
> | [6_contracts.md](6_contracts.md) | Contratos de la API y del front |
> | [7_quickstart.md](7_quickstart.md) | Arranque y prueba manual |
> | [8_tasks.md](8_tasks.md) | Orden de construcción (Fases 62–68) |
> | [guia_ia_v8.md](guia_ia_v8.md) | Guía para trabajar la v8 con un asistente de IA |

---

## 1. Propósito de la v8

Las versiones V1–V7 dejaron las 37 tablas de `bdcatedras` expuestas, y la v7.1
agregó cuentas de estudiante y de ponente, inscripción a cátedras, QR de cierre
y geolocalización. En la reunión del 2026-11-04 el cliente fijó otro modelo:

- **solo el administrador inicia sesión** (R 04:21);
- el catálogo de cátedras es **público** (R 04:03);
- **no hay inscripción ni cupo** que bloquee el registro (R 10:15);
- en el QR la persona **elige su tipo de usuario** y llena solo lo que le toca
  (R 11:46–13:24).

La v8 **cambia el registro de asistencia** a ese modelo y **apaga sin borrar**
(D58) lo que la v7.1 construyó y ya no se usa. Es la versión que el cliente
usará todos los días; las versiones V9, V10 y V11 dependen de ella.

```
QR / enlace (enlace_registro, v5)
   └─ /registro/<token>  ── elige el tipo ──┬─ Estudiante USB   → ID
                                            ├─ Docente/Admin.   → cédula, nombre, contacto, facultad
                                            └─ Externo          → cédula, nombre, contacto, procedencia
        └─ POST /api/Registro/{tipo} → CALL sp_registrar_{tipo} → registro_asistencia
                                            (+ asistente, documento, vinculación y consentimiento si es nuevo)
```

## 2. Alcance

**Incluye:**
- Un registro por cada tipo de usuario: estudiante, docente/administrativo y
  externo (RF198–RF206).
- Ubicación según la modalidad y geocerca por sede (RF207–RF209), adelantada de
  la V9 porque cambia los mismos procedimientos.
- Catálogo público y login solo para el administrador (RF210–RF212).
- Apagado de lo de la v7.1 sin borrar tablas (RF213–RF215).
- Encuesta opcional al terminar el registro y casilla de tratamiento de datos
  (RF216–RF217).

**Convenciones que no cambian:**
- cuerpos y respuestas en snake_case con `[JsonPropertyName]`;
- parámetros de consulta en camelCase;
- códigos HTTP: 422 forma · 400 regla del servicio · 404 no existe · 500 todo
  rechazo de la BD (con `detalle` = mensaje del `RAISE`);
- sin 409 (decisión pendiente de todo el proyecto).

**No incluye:**
- el QR dinámico (V9);
- el calendario y las solicitudes de ponentes (V10);
- los archivos planos (V11);
- borrar tablas, columnas o procedimientos de la v7.1 (D58).

## 3. Requisitos funcionales

La numeración continúa la de la v7, que terminó en RF197. La v7.1 usó `RF-S1`…`RF-S7`.

### Registro por tipo de usuario

- **RF198 — Elegir el tipo de usuario.** `GET /registro/<token>` muestra el nombre
  de la sesión y tres botones: *Estudiante USB*, *Docente / Administrativo* y
  *Externo*. Cada botón lleva a su formulario. Un token que no existe, que está
  revocado o que está fuera de su ventana muestra la pantalla de "enlace no
  disponible" (RN-15, ya existente). No pide login. Para pintar la pantalla, el
  front llama a `GET /api/Registro/contexto?token=`, que responde
  `{id_sesion, catedra, titulo, inicio, fin, modalidad, ubicacion:
  "obligatoria" | "opcional" | "no_aplica", tiene_encuesta, abierto}`.
- **RF199 — Registrar a un estudiante.** `POST /api/Registro/estudiante` con
  `{token, id_asis, latitud?, longitud?, ip?, user_agent?}`:
  - el procedimiento busca al asistente por `id_asis` (D59: alfanumérico de 5 a 15);
  - si no existe → 404 con «No encontramos ese ID. Verifícalo o regístrate como
    docente o externo»;
  - si existe → inserta el `registro_asistencia`. Los disparadores existentes
    toman la foto de la vinculación y del programa (RN-22) y validan la ventana
    (RN-15).
  - Respuesta `200 {estado: "registrado", id_registro, nombre, tiene_encuesta}`.
- **RF200 — El estudiante no da más datos.** El formulario del estudiante tiene
  un solo campo (`id_asis`), con `inputmode="numeric"`. No se pide cédula, correo
  ni clave (D55).
- **RF201 — Registrar a un docente o administrativo.**
  `POST /api/Registro/docente` con:
  - obligatorios: `{token, tipo_documento, numero_documento, nombres,
    apellidos, correo, fk_facultad, es_administrativo, acepta_datos}`;
  - opcionales: `celular`, `latitud`, `longitud`, `ip`, `user_agent`.

  En un solo `CALL` (D57) el procedimiento:
  - busca a la persona por el documento vigente; si no existe, crea el
    `asistente` (`es_externo = false`, sin `id_asis`), su `documento_asistente`
    y su `vinculacion_asistente` (`DOCENTE` o `ADMINISTRATIVO`);
  - guarda la facultad (D56);
  - registra el consentimiento (RF217);
  - inserta el registro.
- **RF202 — El docente no da programa.** `DOCENTE` ya no exige programa (D56).
  Su registro queda con `fk_programa_snapshot = NULL` y la facultad declarada.
- **RF203 — Registrar a un externo.** `POST /api/Registro/externo` con:
  - obligatorios: `{token, tipo_documento, numero_documento, nombres,
    apellidos, correo, procedencia, acepta_datos}`;
  - opcionales: `celular`, `latitud`, `longitud`, `ip`, `user_agent`.

  Hace lo mismo que RF201, pero con `es_externo = true`, la vinculación
  `EXTERNO` y la `procedencia` (barrio, zona o institución, en texto libre de
  hasta 150 caracteres). Si el parámetro `PERMITE_EXTERNOS` es `false` → 500 con
  «El registro de externos está deshabilitado».
- **RF204 — Persona que vuelve.** Si el documento ya existe (`documento_asistente`
  vigente), no se crea otra persona:
  - se reutiliza el asistente y se actualizan su correo, su celular y su
    procedencia o facultad;
  - si su vinculación vigente es distinta, se cierra y se abre la nueva.
- **RF205 — Una asistencia por persona y sesión.** Un segundo registro de la
  misma persona en la misma sesión choca con `uq_registro_sesion_asis` (ya
  existe en el script). La API lo traduce a `200 {estado: "ya_registrado"}`,
  sin error, y el front muestra «Ya estabas registrado en esta sesión».
- **RF206 — Evidencia del registro (D71).** Como `chk_registro_clave` exige
  `fk_clave` cuando el origen es `QR` o `ENLACE`, cada registro crea una
  **clave técnica** en `clave_acceso`, ya marcada como usada. No se envía
  ningún correo.

### Ubicación y geocerca

- **RF207 — La ubicación depende de la modalidad (D60).**
  - `PRESENCIAL`: latitud y longitud obligatorias; sin ellas → 500 con «La
    ubicación es obligatoria en las sesiones presenciales».
  - `HIBRIDA`: se guardan si llegan, pero no bloquean.
  - `VIRTUAL` y `TELEPRESENCIAL`: se ignoran.
- **RF208 — Geocerca (D61).** En las presenciales, si la sede tiene
  coordenadas, la distancia (fórmula de Haversine) entre el punto recibido y la
  sede debe ser ≤ el radio de la sede (`sede.radio_metros`) o, si la sede no lo
  tiene, ≤ `GEOCERCA_RADIO_METROS` (300 por defecto). Si no lo es → 500 con
  «Estás a N m de la sede; debes estar a menos de R m». La distancia calculada
  se guarda en `registro_asistencia.distancia_sede_metros`.
  - Si la sede no tiene coordenadas, la geocerca no se aplica: no bloquea y la
    distancia queda en NULL.
- **RF209 — Coordenadas de la sede.** `Sede` acepta `latitud`, `longitud` y
  `radio_metros` (opcionales) en `POST`, `PUT` y `PATCH`. `/admin/sedes` los
  muestra y los edita.

### Acceso

- **RF210 — Catálogo público.** `/`, `/catedras` y `/catedras/<id>` funcionan
  sin sesión y no muestran datos personales de ponentes ni de asistentes.
- **RF211 — Solo el administrador inicia sesión.** `POST /api/Usuario/login`
  responde 401 (con el mismo mensaje genérico) si el usuario no tiene el rol
  `ADMIN` vigente. Ya no se provisionan cuentas de estudiante en el primer
  ingreso (se apaga el camino de D54).
- **RF212 — El front solo conoce al administrador.** `endpoint_de_inicio`
  lleva siempre a `admin.panel`. El navbar no muestra "Mi panel" de estudiante
  ni de ponente.

### Apagado de la v7.1 (D58)

- **RF213 — Rutas apagadas.** Los blueprints `estudiante`, `ponente` y
  `ponente_catedras` dejan de registrarse en `app.py`, así que sus rutas
  responden 404. Las plantillas y los módulos se quedan en el repositorio.
- **RF214 — Inscripción y cupo sin efecto.**
  - Los enlaces y QR nuevos nacen con `requiere_inscripcion = false`.
  - Los formularios de sesión dejan el `cupo` vacío por defecto.
  - La API `InscripcionCatedra` sigue existiendo, pero ninguna pantalla la llama.
- **RF215 — Usuarios de roles retirados.** La migración 057 cierra
  (`fecha_fin = now()`) los `rol_por_usuario` vigentes de `ESTUDIANTE` y
  `PONENTE`. No borra usuarios.

### Encuesta y datos personales

- **RF216 — Encuesta opcional (D66).** Si la sesión tiene `fk_encuesta`, la
  pantalla de confirmación ofrece «Responder la encuesta (opcional)» y lleva a
  `/sesiones/<id>/encuesta?registro=<id_registro>` (V6), para los 3 tipos de
  usuario.
- **RF217 — Tratamiento de datos (D67).** Docente y externo deben marcar
  `acepta_datos = true`; si no lo hacen → 422. El procedimiento inserta en
  `consentimiento_datos` con `version_politica` = el parámetro
  `VERSION_POLITICA_DATOS` y `canal = 'WEB'`. El estudiante no la marca: ya
  está en el maestro institucional.

## 4. Requisitos no funcionales

- **RNF-V8-1:** registrar a un estudiante en menos de 10 s desde que escanea el
  QR, en un celular con datos móviles (un campo y un botón).
- **RNF-V8-2:** ningún dato personal en la URL ni en el query string.
- **RNF-V8-3:** las 3 operaciones de registro son atómicas: un solo `CALL`
  (Guía IA, Regla 1).
- **RNF-V8-4:** las pruebas de las versiones 1–7.1 siguen pasando; donde una
  prueba de la v7.1 dependa de lo apagado, se marca como "apagada por D58", no
  se borra.

## 5. Criterios de aceptación

1. Desde el QR de una sesión presencial abierta, un estudiante con un ID
   existente se registra digitando solo su ID y su ubicación dentro de los 300 m.
2. El mismo estudiante, desde 2 km, recibe el mensaje de la geocerca y no queda
   registrado.
3. Un docente nuevo se registra con su cédula, nombres, apellidos, correo y
   facultad, sin programa. Quedan creados el asistente, su documento, su
   vinculación `DOCENTE`, su consentimiento y el registro.
4. Un externo con pasaporte `AB123456` se registra con su procedencia. Si
   vuelve a la sesión siguiente, se reutiliza el mismo asistente.
5. Repetir cualquier registro en la misma sesión responde `ya_registrado` y no
   crea filas.
6. En una sesión `VIRTUAL` nadie ve la petición de ubicación y el registro pasa
   sin coordenadas.
7. Un usuario con rol `ESTUDIANTE` no puede iniciar sesión; el administrador sí.
8. `/estudiante`, `/ponente` y `/ponente/catedras` responden 404; `/catedras`
   carga sin sesión.
9. La prueba de capas y las verificaciones de las versiones anteriores siguen en verde.
