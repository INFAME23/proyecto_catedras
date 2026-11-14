# Subversión v7.2 — asistencia por QR con 3 tipos de asistente, ubicación contra la sede, carga de estudiantes y exportación por cátedra

> **Estado: IMPLEMENTADA y verificada el 2026-11-12 (falta el commit y el tag). El borrador original se conserva abajo; lo que
> salió distinto está en la sección 12.**
> Según las reglas de entrega del prompt, primero se aprueba este documento
> (en especial la sección 11, «Pendientes») y solo después se escribe, por
> partes y con tu OK entre ellas, migraciones → API → front → script de
> verificación.
>
> **No es una versión nueva del mapa** (mantenimiento sobre V5–V7, como la
> v7.1). Sus requisitos son `RF-S8`…`RF-S14`, sus decisiones `D60`…`D72` y sus
> migraciones empiezan en la `060` (el último número usado por la v7.1 es
> `RF-S7`, `D59` y la migración `059`). La constitución no se modifica; ante
> conflicto, gana.
>
> Nace del prompt «Subversión v7.2» y de revisar el código y el esquema reales.
> Donde el prompt y el repositorio no coincidían, mandó el repositorio
> (sección 1) y quedó anotado como pendiente.

## 1. Lo que se verificó antes de proponer (y lo que no coincidía)

| El prompt asumía | Lo que se encontró en el repositorio | Consecuencia |
|---|---|---|
| El archivo de ejemplo `personas_mock_usbmed.csv` es lo que dice el prompt | **Leído el 2026-11-12** (`Downloads/personas_mock_usbmed.csv`): UTF-8 **con BOM**, separador coma, encabezado exacto `CC,Carnet_estudiante,Programa`, **10 filas**, carnets de 11 dígitos (`30000124726`…`30000124735`) y CC de 10 dígitos (todos válidos para `chk_asistente_idasis` y `chk_documento_numero`), 9 códigos de programa distintos (`INDS` se repite con dos carnets distintos). **Hallazgo:** en la base local de Docker (no pude consultar la desplegada) los carnets `30000124729` y `30000124730` **ya existen** como personas de relleno del ASIS con **otra** CC (`1092456500`, `1055831005`) y otro programa (`M0801`, `M0226`); ninguna de las 10 CC del archivo existe todavía. | Cae en el caso «mismo carnet, otra CC» de D68 — **P19**. |
| `SINONIMOS` no reconoce `cc` ni `carnet_estudiante` | Confirmado (`servicios/carga_asistentes.py`): solo `documento/numero_documento/num_documento` y `id_asis/emplid/codigo_asis`. | RF-S8: agregar los dos sinónimos. |
| Se puede cargar la persona con `POST /api/Asistente` y repetir la carga | **No es idempotente.** `sp_guardar_asistente` hace `INSERT` puro: repetir un `id_asis` lanza `uq_asistente_asis` (500). Además `nombres` y `apellidos` son `NOT NULL` y la API (`AsistenteCrear`) los exige (422). | Hace falta un procedimiento nuevo de «cargar estudiante» con *upsert* (D68). |
| Relajar `chk_asistente_correo` «solo para este caso» | El CHECK exige `correo_institucional` **o** `correo_personal`. El celular **no cuenta**: un docente o externo que dé solo su celular también lo viola. | Se relaja con una marca explícita, no quitando el CHECK (D67). |
| Un docente se crea sin más | `chk_asistente_externo`: `es_externo = false` **exige** `id_asis`. Un docente del formulario no trae código ASIS, así que solo puede crearse con `es_externo = true`. Hay precedente: la D56 hizo lo mismo con los ponentes. | D67: la clase de persona sale de la **vinculación**, no de `es_externo`. Afecta cómo se consulta la exportación (D70). |
| La vinculación `DOCENTE` entra sin programa | **`DOCENTE` tiene `exige_programa = true`** (script provisto, fila de `tipo_vinculacion`), y el disparador `fn_trg_registro_validar` (RN-07) rechaza el registro si no hay programa activo. Solo `ADMINISTRATIVO`, `RUTAS_PAZ`, `COLEGIO` y `EXTERNO` entran sin programa. | Hay que poner `exige_programa = false` a `DOCENTE` (un `UPDATE` de una fila, que es justo el mecanismo que documenta el script) — **decidido el 2026-11-12 (P7): sí se pone**. |
| `registro_asistencia` exige `fk_clave` salvo `MANUAL`/`IMPORTADO` | Confirmado: `chk_registro_clave`. Pero `origen` hoy dice **por qué canal** entró (`QR` o `ENLACE`, D53/053), y `chk_registro_origen` solo admite `QR/ENLACE/MANUAL/IMPORTADO`. | No conviene meter `QR_CODIGO` en `origen`: se perdería el canal. Se propone una columna aparte (D65). |
| Los códigos del archivo (`INDS`, `ADMO`, …) no están en el catálogo | `programa_academico.codigo` tiene `CHECK ^[A-Z][0-9A-Z]{3,4}$`: **los 9 códigos son válidos como código de programa**. `fk_facultad` es nulable. | Se pueden crear los programas con esos mismos códigos (D69). |
| «Exportar el Programa tal como se cargó» | Con **alias** (`alias_programa`) el texto se traduce a `M0286` y eso es lo que queda en `programa_asistente` y en el *snapshot*: se exportaría `M0286`, **no** `INDS`. | Solo crear los programas con el código original cumple el requisito (D69, **P5**). |
| En híbridas, «por el QR se exige y por el enlace virtual no» | El modelo tiene **un solo enlace vigente por sesión** (RN-14, `ex_enlace_sin_solape`) y, para modalidad `AMBOS`, lo emite con canal `QR`: no hay dos canales que distinguir. | **Decidido el 2026-11-12 (P1): las híbridas quedan fuera de alcance** (D61). No se toca el modelo de enlaces. |
| La IP del asistente se guarda siempre | `routes/registro.py` usa `request.remote_addr`. **No hay `ProxyFix`** en el front. Detrás del proxy de Render (hay un commit «subir a render») eso es la IP del proxy, no la de la persona. | Las mitigaciones basadas en IP (sección 9) no servirían hasta configurarlo (**P15**). |
| Los scripts de verificación viven en `pruebas/verificar_v72.ps1` | Los de v5/v7/v7.1 están en **`api_catedras/pruebas/`**. | Se crea `api_catedras/pruebas/verificar_v72.ps1` por consistencia (**P17**). |

## 2. Requisitos y dónde quedará cada uno

| # | Requisito | Capas |
|---|---|---|
| RF-S8 | Carga de estudiantes (`CC, Carnet_estudiante, Programa`), periodo por `<select>`, idempotente | BD (`sp_cargar_estudiante`, relajar nombres/correo) · API (`POST /api/Asistente/cargar-estudiante`) · front (`routes/carga.py`, `servicios/carga_asistentes.py`, `carga_datos.html`) |
| RF-S9 | Flujo público tras escanear el QR con 3 opciones: Estudiante, Docente, Externo | BD (3 procedimientos) · API (controlador `AsistenciaPublica`) · front (`routes/registro.py`, plantillas nuevas) |
| RF-S10 | Ubicación solo en sesiones presenciales (canal `QR`) y comparada con la sede; **cambia la RF-S1/D38 de la v7.1** | BD (`fn_distancia_metros`, SPs) · API (aviso temprano) · front (`geolocalizacion.js`) |
| RF-S11 | Coordenadas y radio de la sede, parámetro global, distancia auditada | BD (`sede`, `parametro`, `registro_asistencia`) · API (`Sede`) · front (`sede_form`, `sede_lista`) |
| RF-S12 | El QR de una sesión presencial falla al generarse si la sede no tiene coordenadas | BD (`sp_emitir_qr_sesion`) · front (mensaje fijo al crear cátedra y en la pantalla del QR) |
| RF-S13 | Exportación por cátedra (3 CSV por cátedra + ZIP), solo ADMIN | BD (funciones de consulta) · API (`Exportacion`, solo lectura) · front (`/admin/exportar`) |
| RF-S14 | Catálogo de cátedras del periodo en CSV | igual que RF-S13 |

## 3. Decisiones (ADR de la v7.2, continúan después de D59)

**D60 — El registro público ya no usa la clave de 6 dígitos por correo (RF-S9; reemplaza la D53 *para el registro público*).**
El estudiante se identifica con su carnet; docente y externo con un formulario. Las rutinas de la API y de la BD de `ClaveAcceso`
(el controlador `ClaveAcceso`, `sp_validar_clave_y_registrar_geo`, `fn_validar_clave_geo_api` y las demás rutinas de clave) **no se borran**: el front deja de llamarlas y se
documenta como «sin efecto desde D60», igual que la D55 hizo con la inscripción. Las pantallas `registro_solicitar.html` y `registro_validar.html`
dejan de usarse (se conservan en el repo hasta que el equipo decida borrarlas). `servicios/correo.py` queda para la recuperación de clave del administrador.
Consecuencia buscada y aceptada: la identidad ya no depende de que la persona controle su correo, solo de que su carnet no sea conocido por terceros (sección 9).

**D61 — La ubicación se exige por **canal del enlace**, no por la persona (RF-S10).** El procedimiento mira `enlace_registro.canal` del token: `QR` → exige ubicación y la compara con la sede; `ENLACE` → no pide ni valida nada y guarda `latitud` y `longitud` en `NULL` (aunque el navegador las hubiera enviado: se descartan, para que el dato siga significando «verificado presencialmente»). La v7.1 (D38) exigía ubicación siempre; esa regla **se cambia** y las columnas ya eran nulables, así que no hay cambio de esquema por esto.
*Híbridas — decidido el 2026-11-12 (P1): fuera de alcance.* No se agrega ningún tratamiento especial y **no se tocan** RN-13, RN-14, `ex_enlace_sin_solape` ni `sp_consultar_qr_sesion`. Una sesión de modalidad `HIBRIDA` (`canal_difusion = 'AMBOS'`) sigue teniendo un solo enlace vigente con canal `QR`, así que **se comporta como presencial**: exige ubicación y una sede con coordenadas. Consecuencia aceptada: quien se conecte de forma virtual a una sesión híbrida no podría registrarse por ese enlace; por eso conviene no programar sesiones `HIBRIDA` mientras rija la v7.2 (**P1b**: ¿desactivo la modalidad?).

**D62 — Sede con coordenadas y radio; parámetro global `RADIO_VALIDACION_METROS` = 200 (RF-S11).**
`sede` gana `latitud` y `longitud` (`numeric(9,6)`, con los mismos rangos y la regla «las dos o ninguna» de la `045`) y `radio_metros` (`integer` nulable, `CHECK` entre 10 y 5000).
Radio efectivo = `sede.radio_metros` si no es nulo; si no, el parámetro `RADIO_VALIDACION_METROS` (`ENTERO`, valor inicial **200**); si el parámetro faltara, 200 en el código del SP.
*Por qué 200:* el GPS de un teléfono al aire libre suele errar entre 5 y 20 m, pero dentro de un edificio (techos, vidrio) puede errar 50–100 m o más; el punto que se guarda
es **un** punto de la sede, no su perímetro, y un campus mide cientos de metros. 200 m deja holgura para el error y el tamaño del campus sin aceptar a quien está en otro barrio.
Es un valor inicial: se ajusta en `/admin/parametros` y, si un edificio necesita otro, por sede. **Coordenadas confirmadas el 2026-11-12 (P4):** `BELLO` = `6.333128794233984, -75.56917364441165` y `SAN_BENITO` = `6.25509455859939, -75.57296888404679`; con `numeric(9,6)` quedan `6.333129, -75.569174` y `6.255095, -75.572969`. Ninguna sede lleva radio propio: las dos usan el global (200 m). `VIRTUAL` no lleva coordenadas.
Sedes virtuales (`es_virtual`) no llevan coordenadas.

**D63 — `fn_distancia_metros` con Haversine, sin PostGIS (RF-S10).**
`fn_distancia_metros(lat1, lon1, lat2, lon2) RETURNS double precision`, `IMMUTABLE`, `LANGUAGE sql`, radio de la Tierra 6 371 008,8 m:
`2·R·asin( least(1, sqrt( sin²(Δlat/2) + cos(lat1)·cos(lat2)·sin²(Δlon/2) )) )` con todo en radianes. El `least(1, …)` evita el error de `asin` por redondeo.
Devuelve `NULL` si algún argumento es `NULL`. Los SP la llaman; el aviso temprano de la API también (una sola fórmula). Se prueba con pares de coordenadas de distancia conocida.

**D64 — Los 3 procedimientos de registro devuelven un *estado*, no lanzan para los casos de negocio (RF-S9, Regla 1).**
`sp_registrar_asistencia_estudiante`, `_docente` y `_externo` hacen **en una sola transacción**: enlace (existe, no revocado, dentro de la ventana) → ubicación (si el canal es `QR`) → persona →
vinculación → duplicado → `INSERT`. Reciben `p_ip` y `p_user_agent` y los guardan **siempre**. Devuelven `p_estado` (texto) y `p_id_registro`; no insertan nada si el estado no es `OK`.
Mismo criterio que la D41: una excepción deshace el propio `CALL`, y aquí además el mensaje debe ser amable. Los errores **inesperados** (FK, `CHECK`) siguen subiendo como excepción (500).
El disparador `trg_registro_validar` (ventana, snapshots, RN-07) sigue activo como segunda barrera. Estados: `OK`, `ENLACE_NO_EXISTE`, `ENLACE_REVOCADO`, `FUERA_DE_VENTANA`, `SIN_UBICACION`,
`SEDE_SIN_COORDENADAS`, `FUERA_DE_RADIO`, `ESTUDIANTE_NO_EXISTE`, `SIN_VINCULACION_VIGENTE`, `PERSONA_INACTIVA`, `YA_REGISTRADO`, `DATOS_INVALIDOS`. La carrera entre dos envíos simultáneos de la misma persona se resuelve
atrapando `unique_violation` de `uq_registro_sesion_asis` y devolviendo `YA_REGISTRADO` (nunca un 500).

**D65 — Cómo se identifica quien registra: columna `identificado_por`, sin tocar `origen` (RF-S9).**
Se agrega `registro_asistencia.identificado_por varchar(12) NOT NULL DEFAULT 'CLAVE'` con `CHECK IN ('CLAVE','CARNET','DECLARADO')` (`CLAVE` = el flujo de la v5/v7.1 y todas las filas existentes;
`CARNET` = estudiante por código; `DECLARADO` = docente/externo por formulario). `chk_registro_clave` se reemplaza por
`fk_clave IS NOT NULL OR origen IN ('MANUAL','IMPORTADO') OR identificado_por IN ('CARNET','DECLARADO')`.
`origen` sigue diciendo el canal (`QR`/`ENLACE`), que importa para los informes y la migración a ASIS. *Alternativa que propone el prompt* (`origen = 'QR_CODIGO'`): obligaría a ampliar `chk_registro_origen`
y a perder el canal; se descarta, pero si prefieres ese camino dímelo (**P16**).

**D66 — *Snapshots* de facultad y procedencia, y auditoría de la distancia (RF-S9, RF-S11).**
Columnas nuevas en `registro_asistencia`, igual que `fk_programa_snapshot`: `fk_facultad_snapshot varchar(10) REFERENCES facultad` (solo docentes), `procedencia varchar(150)` (solo externos) y `distancia_metros numeric(9,1)`
(solo cuando el canal fue `QR`). Son **hechos del día del registro**: no se actualizan después. La persona (`asistente`) no gana columnas de facultad ni procedencia: un docente cambia de facultad y un externo
viene de sitios distintos cada vez, por eso van en el registro y no en la persona. Con `latitud`/`longitud` ya existentes (`045`) queda la auditoría completa. Un `CHECK` impide `procedencia` en filas que no sean externas
y `fk_facultad_snapshot` en filas que no sean docentes.

**D67 — Personas sin nombre, sin correo o sin código ASIS (RF-S8, RF-S9).** Tres ajustes mínimos, cada uno con su razón:
1. **Nombres de estudiantes cargados.** `nombres`/`apellidos` siguen `NOT NULL` (la vista `v_asistente_vigente` concatena `nombres || ' ' || apellidos`, y un `NULL` borraría el nombre en toda la UI). El procedimiento de carga escribe
   `nombres = 'Estudiante <carnet>'` y `apellidos = '-'` **solo si la persona es nueva**; si ya existía con nombre real, **no se sobrescribe**. Así las listas del admin muestran algo identificable. Alternativa: hacerlas nulables (toca vistas y modelos C#) — **P9**.
2. **Correo.** Se agrega `asistente.contacto_sin_correo boolean NOT NULL DEFAULT false` y `chk_asistente_correo` pasa a
   `correo_institucional IS NOT NULL OR correo_personal IS NOT NULL OR contacto_sin_correo`. Solo los procedimientos de la v7.2 ponen `true` (estudiantes cargados sin correo; docente/externo que dio solo celular). La API de `Asistente`
   y la carga antigua no tocan la marca: **siguen exigiendo un correo**, y el relajo vale «solo para este caso».
3. **Código ASIS.** Un docente o externo nuevo se crea con `es_externo = true` (única forma de que `chk_asistente_externo` lo admita sin `id_asis`; mismo precedente que D56). **Su clase real sale de la vinculación y del
   *snapshot* de la vinculación**, nunca de `es_externo` (D70). Además, `tipo_vinculacion.exige_programa` de `DOCENTE` pasa a `false` (si no, RN-07 rechazaría a todo docente) — **decidido el 2026-11-12 (P7)**.
La persona existente **no se pisa**: si la cédula ya existe, se reutiliza y solo se rellenan campos vacíos (correo, celular). Una persona puede quedar con varias vinculaciones vigentes (p. ej. ESTUDIANTE y DOCENTE): el procedimiento
pasa la vinculación **explícitamente** al `INSERT` del registro (el disparador solo la deduce cuando llega `NULL`), así la asistencia queda clasificada como lo que la persona eligió en el formulario.

**D68 — La carga de estudiantes es un *upsert* por carnet (RF-S8).** Nuevo `sp_cargar_estudiante` y `POST /api/Asistente/cargar-estudiante` (en su propio controlador `CargaEstudiante`, para no tocar el de `Asistente` que ya funciona). Por fila, en una transacción:
(1) buscar `asistente` por `id_asis = carnet`; si no existe, crearlo (D67); (2) documento `CC`: si el `numero` ya es de **otra** persona, **rechazar la fila** con motivo claro (`uq_documento_numero`); si es de la misma, nada;
si la persona tenía otro documento vigente, ese pasa a `vigente = false` y se agrega el nuevo; (3) programa: `INSERT … ON CONFLICT DO NOTHING` en `programa_asistente (asistente, programa, periodo)` con estado `ACTIVO`
(repetir el archivo no duplica). **Decidido el 2026-11-12 (P19):** el archivo manda para el periodo elegido, así que antes de insertar, los **otros** programas `ACTIVO` de esa persona **en ese mismo periodo** pasan a `RETIRADO` (no se borran). Así queda un solo programa activo por persona y periodo, y el *snapshot* que elige `fn_trg_registro_validar` es determinista. Los programas de otros periodos no se tocan; (4) vinculación `ESTUDIANTE`: si no hay una vigente, crearla con `fecha_ini = hoy`; si la hay, se conserva su fecha.
Responde `CREADO` o `ACTUALIZADO`. Se conservan el lote (`lote_carga_asistente`), las novedades por fila rechazada y la carga parcial de la D15. El front detecta el formato por los encabezados (`cc` + `carnet_estudiante` →
modo estudiantes, con el `<select>` de periodo); un archivo con las columnas de siempre sigue cargándose con el flujo antiguo.

**D69 — Los códigos de programa del archivo se crean como programas (RF-S8) — decidido el 2026-11-12 (P5, camino A).** Se crean 9 filas en `programa_academico` con los códigos exactos del archivo (válidos para `^[A-Z][0-9A-Z]{3,4}$`), así el `Programa` exportado sale tal como se cargó. Como el archivo es de prueba, **los nombres, las facultades y el nivel son ficticios** (elegidos al azar por indicación tuya, con facultades que ya existen en el catálogo), y llevan el sufijo «(mock)» para reconocerlos y borrarlos fácil:

| Código | Nombre (ficticio) | Facultad | Nivel |
|---|---|---|---|
| `INDS` | Ingenieria de Datos y Software (mock) | `F_ING` | `PREGRADO` |
| `ADMO` | Administracion de Empresas (mock) | `F_CIENCIAS` | `PREGRADO` |
| `PSIC` | Psicologia (mock) | `F_PSICO` | `PREGRADO` |
| `DERE` | Derecho (mock) | `F_DERECHO` | `PREGRADO` |
| `ARQU` | Arquitectura (mock) | `F_ARTES` | `PREGRADO` |
| `INGC` | Ingenieria Civil (mock) | `F_ING` | `PREGRADO` |
| `CONT` | Contaduria Publica (mock) | `F_CIENCIAS` | `PREGRADO` |
| `COMU` | Comunicacion Social (mock) | `F_ARTES` | `PREGRADO` |
| `EDUC` | Licenciatura en Educacion (mock) | `F_EDU` | `PREGRADO` |

Se insertan con `INSERT … ON CONFLICT (codigo) DO NOTHING` (idempotente). Si más adelante llega el archivo real, se reemplazan los nombres con un `UPDATE`, sin cambiar los códigos. Descartadas: alias hacia los `M0xxx` (se perdería el código original) y una columna `codigo_archivo`.

**D70 — Reglas de la exportación (RF-S13, RF-S14).**
1. **Clasificación por *snapshot* de vinculación**, no por `es_externo`: `ESTUDIANTE` → estudiantes, `DOCENTE` → docentes, `EXTERNO` → externos. Las asistencias de **otras** vinculaciones anteriores a la v7.2 (`ADMINISTRATIVO`, `EGRESADO`,
   `RUTAS_PAZ`, `COLEGIO`) **no se exportan**, pero la pantalla muestra «N asistencias de otras vinculaciones» para que nada se pierda en silencio (**P11**: si prefieres que entren en «Externos», se cambia una condición).
2. **Periodo:** una cátedra pertenece a un periodo por sus sesiones (`sesion.fk_periodo`). Regla propuesta: la cátedra aparece **en cada periodo donde tiene al menos una sesión**, y en ese periodo cuentan **solo los registros de esas sesiones**.
   Una cátedra con sesiones en `2026-1` y `2026-2` sale en las dos listas, cada una con sus asistentes; un estudiante que fue a una sesión de cada periodo sale en ambos archivos (uno por periodo). (**P14**)
3. **Sin duplicados por cátedra:** `DISTINCT` por persona dentro de la cátedra y el periodo (una persona en 3 sesiones de la misma cátedra aparece una vez).
4. **Identificador:** `id_catedra` = `catedra.id_evento_asis` (9 dígitos con ceros a la izquierda). En el nombre del archivo, en la pantalla y en el catálogo. (**P12**)
5. **Columnas:** estudiantes `CC, Carnet_estudiante, Programa` (idénticas al archivo de carga; `Programa` = `fk_programa_snapshot`, el del día de la cátedra); docentes `CC, Nombre, Contacto, Facultad`; externos `CC, Nombre, Contacto, Procedencia`.
   `CC` = número del documento vigente; `Nombre` = nombres + apellidos de la persona, o `nombre_completo_asis` si lo trae; `Contacto` = correo (institucional primero) o celular; `Facultad` = **nombre** de la facultad del *snapshot* (más legible que el código).
6. **Cátedra sin asistentes de un grupo:** en la pantalla el botón sale **deshabilitado** con el contador `0` (no genera archivos vacíos que no aportan nada y evita dudas), igual en los tres tipos. **En el ZIP los archivos sí se generan siempre,
   con solo el encabezado**, para que el contenido sea predecible y un script pueda contar archivos. (**P13**)
7. **Cátedras incluidas (implementado en la `066`, no estaba en el prompt):** las **activas** con al menos una sesión en el periodo y las **inactivas que tengan asistencias** en él. La D59 desactivó miles de cátedras sin asistencias; sin este criterio llenarían la lista de ceros. Si prefieres «absolutamente todas», es quitar una condición (**P20**).
8. **El sistema no calcula el total por estudiante**: solo produce los archivos; el recuento («en cuántos archivos sale este carnet») lo hace quien administra.

**D71 — Formato y seguridad de los CSV y del ZIP (RF-S13, RF-S14).** UTF-8 **con BOM**, separador coma, `csv` de Python (comillas automáticas), final de línea `\r\n`. Dentro del ZIP los archivos van **sin carpetas** (`catedra_<id>_estudiantes.csv`, …),
y el ZIP se arma en memoria con `zipfile`. **Inyección de fórmulas:** los campos de texto libre de docentes y externos (`Nombre`, `Contacto`, `Facultad`, `Procedencia`) los escribe un desconocido y se abrirán en Excel; si empiezan por `=`, `+`, `-`, `@`, tab o retorno
se les antepone `'`. Los CSV de estudiantes y el catálogo no se tocan (valores controlados), así siguen siendo idénticos al archivo de carga. **Ceros a la izquierda:** Excel, al abrir con doble clic, convierte `000035392` en `35392`; los archivos no se alteran
(siguen siendo CSV puro) y la pantalla avisa «impórtalo como texto» — alternativa descartada: `="000035392"`, que rompería el cruce programático.

**D72 — El QR de una sesión presencial no se genera si la sede no tiene coordenadas (RF-S12).** `sp_emitir_qr_sesion` comprueba, cuando el canal es `QR`, que la sede de la sesión sea física (`es_virtual = false`) y tenga `latitud`/`longitud`; si no,
lanza `RAISE EXCEPTION … USING ERRCODE = 'P0001'` con el texto «La sede “<nombre>” (<id>) no tiene coordenadas: complétalas en Administración › Sedes antes de generar el QR» (la API lo devuelve como **409**, patrón de D56/D57).
En el front, `asegurar_qr_de_catedra` (que hoy se traga el error) pasa a **devolver los mensajes** y la pantalla de crear cátedra los muestra como aviso **fijo** (no flash que desaparece a los 5 s), con un enlace a la sede. En el registro, si llegara el caso
(coordenadas borradas después de emitir el QR), el estado es `SEDE_SIN_COORDENADAS` y **no se acepta** la asistencia. Una sesión presencial con una sede **virtual** (inconsistencia de datos) también falla al emitir, con su propio mensaje (**P10**).

## 4. Cambios de base de datos (migraciones desde la `060`, todas idempotentes como la `045`)

> Se montan en `docker-compose.yml` como `z060`… y hay que **aplicarlas a mano a la base ya desplegada** (Render/Neon) y a cualquier volumen de Docker existente, **antes** de desplegar la API y el front nuevos
> (el script provisto no se toca; D39).

| Mig. | Contenido | Decisión |
|---|---|---|
| `060_sede_coordenadas.sql` | `sede`: `latitud`, `longitud` `numeric(9,6)` + `radio_metros` nulable (CHECK de rangos, «las dos o ninguna», radio 10–5000, virtual sin coordenadas); parámetro `RADIO_VALIDACION_METROS` = 200; coordenadas de `BELLO` y `SAN_BENITO`; **`fn_distancia_metros`**; `sp_listar/consultar/crear/actualizar_sede`. **Compatible con la API actual:** los parámetros nuevos de crear/actualizar van **después de `p_resultado`**, con valor por defecto, y `sp_actualizar_sede` solo cambia las coordenadas si recibe `p_actualizar_geo = true` (una llamada anterior a la v7.2 no las borra) | D62, D63 |
| `061_registro_publico_columnas.sql` | `registro_asistencia`: `identificado_por`, `fk_facultad_snapshot`, `procedencia`, `distancia_metros`; reemplazo de `chk_registro_clave`; `CHECK`s de coherencia por tipo; `asistente.contacto_sin_correo` y nuevo `chk_asistente_correo`; `UPDATE tipo_vinculacion SET exige_programa = false WHERE id_tipo_vinculacion = 'DOCENTE'` (P7, decidido); reemplazo de `listar/consultar/crear_registro_asistencia` para que el JSON traiga las columnas nuevas | D65, D66, D67 |
| `062_qr_exige_sede_con_coordenadas.sql` | Reemplaza `sp_emitir_qr_sesion` (partiendo de la versión de la `054`, con `qr_cierre`) agregando solo la comprobación de la sede (D72), y **desactiva la modalidad `HIBRIDA`** (P1b). No cambia enlaces, canales ni restricciones | D72 |
| `063_sp_registro_publico.sql` | `fn_validar_enlace_y_ubicacion` (la regla común de enlace + ubicación), `fn_consultar_enlace_publico`, `fn_verificar_ubicacion` (aviso temprano), `sp_registrar_asistencia_estudiante`, `_docente`, `_externo` y dos piezas internas (`fn_interpretar_contacto`, `fn_persona_por_cedula`). Sin envoltorios `fn_*_api`: los procedimientos devuelven sus `INOUT` y el repositorio los lee con `CALL`, como `sp_guardar_asistente` | D64 |
| `064_sp_cargar_estudiante.sql` | `sp_cargar_estudiante` (upsert por carnet, retira los otros programas ACTIVO del periodo, P19) | D68 |
| `065_programas_archivo.sql` | Los 9 programas ficticios de D69 (`ON CONFLICT DO NOTHING`) | D69 |
| `066_exportacion_catedras.sql` | `fn_exportar_catedras_periodo(periodo)` (id, título, conteos por tipo, «otros») y `fn_exportar_asistentes_periodo(periodo, desde, limite)` (filas por cátedra y tipo, ya deduplicadas) | D70 |

Las firmas definitivas, los códigos de error y las pruebas SQL se documentan en cada archivo (comentarios en español línea a línea, Artículo 6).

## 5. Endpoints (API)

El front sigue sin tocar la BD. Nada de esto agrega tablas; se agregan **3 controladores** (`AsistenciaPublica`, `CargaEstudiante` y `Exportacion`, de solo lectura). Había 42 archivos de controlador antes de la v7.2 (la prueba de capas esperaba 41 y ya fallaba); ahora son **45**.

| Método y ruta | Quién | Qué hace |
|---|---|---|
| `GET /api/AsistenciaPublica/enlace/{token}` | público | Datos para pintar la página: título de la cátedra y de la sesión, modalidad, **canal**, si **requiere ubicación**, estado de la ventana y si está revocado. No devuelve las coordenadas de la sede. 404 si el token no existe |
| `GET /api/AsistenciaPublica/ubicacion?token=&latitud=&longitud=` | público | **Aviso temprano:** `{requiere_ubicacion, dentro, distancia_metros, radio_metros}`. Si el canal es `ENLACE`: `requiere_ubicacion=false`. 409 si la sede no tiene coordenadas |
| `POST /api/AsistenciaPublica/estudiante` | público | `{token, carnet, latitud?, longitud?, ip, user_agent}` → `201 {id_registro}` |
| `POST /api/AsistenciaPublica/docente` | público | `{token, cedula, nombre, contacto, fk_facultad, latitud?, longitud?, ip, user_agent}` |
| `POST /api/AsistenciaPublica/externo` | público | `{token, cedula, nombre, contacto, procedencia, latitud?, longitud?, ip, user_agent}` |
| `POST /api/Asistente/cargar-estudiante` | front (admin) | `{id_asis, documento, programa, periodo, fk_lote_carga}` → `{id_asistente, accion}` |
| `POST/PUT/PATCH /api/Sede` (cambian) | front (admin) | aceptan `latitud`, `longitud`, `radio_metros` (422 si va una sola coordenada o están fuera de rango). **Un PUT sin ubicación NO la borra** (compatibilidad con la v7.1: el body de un PUT que no la conoce dejaría las sedes sin coordenadas); una sede que pasa a virtual sí la pierde. Las lecturas traen además `radio_efectivo_metros` |
| `GET /api/Exportacion/catedras?periodo=` | front (admin) | Lista de cátedras del periodo con conteos por tipo (alimenta la pantalla y el catálogo CSV) |
| `GET /api/Exportacion/asistentes?periodo=&desde=&limite=` | front (admin) | Asistentes por cátedra y tipo, **paginado por cátedra** (el ZIP lo recorre por tramos para no hacer 1 petición por cátedra) |

`POST /api/QrSesion` ahora responde **409** con el mensaje de la sede cuando una sesión presencial no puede generar QR (D72). El mensaje del aviso temprano en OK es «Estás en el lugar de la cátedra» (no «Asistencia registrada»).

Códigos: 422 = body inválido (anotaciones de la petición); 404 = enlace inexistente; 409 = rechazo de negocio con `{estado, mensaje, detalle}` y el **estado** de D64 (`FUERA_DE_VENTANA`, `FUERA_DE_RADIO`, `YA_REGISTRADO`, …);
500 = solo lo inesperado. La API sigue abierta (D28): la protección del `/admin` es del front.

## 6. Pantallas (front)

**Carga de estudiantes (`/carga_datos`, ADMIN).** Se agrega el `<select>` de **periodo** (de `GET /api/PeriodoAcademico`, Regla 2) y se explica el formato nuevo. El resultado muestra además cuántos fueron `CREADO` y cuántos `ACTUALIZADO`.
Sinónimos: `cc` → `documento`, `carnet_estudiante` → `id_asis`.

**Registro público (`/registro/<token>`).** Sin login, sin correo. Muestra el título de la cátedra y la sesión y **tres tarjetas**: Estudiante, Docente, Externo. Cada una abre su formulario:
- *Estudiante:* carnet.
- *Docente:* cédula, nombre, contacto (correo o celular en un solo campo; con `@` → correo, si no → celular), **facultad** (`<select>` de `GET /api/Facultad`).
- *Externo:* cédula, nombre, contacto, de dónde viene.
Si el enlace es de canal `QR`, el formulario pide la ubicación con `geolocalizacion.js` (que se amplía): en cuanto el navegador la entrega, consulta `GET /registro/<token>/ubicacion` (ruta de Flask que llama a la API) y, si está fuera del radio,
lo dice **antes** de que la persona escriba nada («Parece que no estás en el lugar de la cátedra»). Si la persona niega el permiso, no puede enviar y se le explica cómo activarlo (se conserva el texto de la v7.1). En canal `ENLACE` el script **no se carga**. Tras registrar,
`session['registro_reciente']` sigue habilitando la encuesta. «Ya estás registrado en esta sesión» se muestra como mensaje normal. El token del enlace sigue siendo una constante en la URL: el front valida su formato (404 si no es UUID), como hoy.

**Sedes (`/admin/sedes`).** El formulario pide latitud, longitud y radio (con una ayuda: «clic derecho en Google Maps › copiar coordenadas»), valida las dos o ninguna y muestra el radio efectivo (la sede o el global). La lista marca «Sin coordenadas» en las sedes físicas que las necesitan.

**Exportación (`/admin/exportar`, solo ADMIN; tarjeta nueva en `/admin`).** `<select>` de periodo y dos apartados:
- *4.1 Descarga por cátedra:* tabla con **id**, título y conteos de estudiantes, docentes y externos, tres botones por fila (deshabilitados si el conteo es 0) y el botón **«Descargar todas (ZIP)»**.
- *4.2 Catálogo:* botón que descarga `catedras_<periodo>.csv` (`id_catedra, titulo`) con **todas** las cátedras del periodo.
- Línea informativa con las asistencias de «otras vinculaciones» (D70.1).
Rutas de descarga: `/admin/exportar/<periodo>/catedra/<id>/<tipo>.csv`, `/admin/exportar/<periodo>/catalogo.csv`, `/admin/exportar/<periodo>/todas.zip`. Todas exigen rol `ADMIN`.

## 7. Cómo se dividirá la entrega (después de que confirmes)

1. **Migraciones `060`–`066`** + su prueba SQL (idempotencia: se aplican dos veces sin error).
2. **API** (repositorio, servicio, controlador; prueba de capas: se agregan las peticiones nuevas a `api_catedras/pruebas/Programa.cs`).
3. **Front** por bloques: carga → registro público (3 opciones + ubicación) → sedes → exportación.
4. **`api_catedras/pruebas/verificar_v72.ps1`** con los casos de la sección 8 y pruebas Python del front con la API simulada (`front_catedras/pruebas/prueba_*_v72.py`).
5. **Documentación:** `README.md`, `0_mapa_versiones.md` (sección «Subversión v7.2») y la tabla de `guia_ia.md`. Sin tocar la constitución.
Entre una parte y otra espero tu OK.

## 8. Criterios de aceptación y casos de prueba

| # | Caso | Resultado esperado |
|---|---|---|
| T01 | Carnet inexistente | `ESTUDIANTE_NO_EXISTE`, no se guarda nada |
| T02 | Carnet repetido en la misma sesión | `YA_REGISTRADO` (409 amistoso, no 500); lo mismo con dos envíos simultáneos |
| T03 | Enlace revocado / fuera de ventana | `ENLACE_REVOCADO` / `FUERA_DE_VENTANA`, no se guarda |
| T04 | Presencial **sin** ubicación | `SIN_UBICACION`, rechazada |
| T05 | Presencial con ubicación **dentro** del radio | aceptada; `latitud`, `longitud` y `distancia_metros` guardadas |
| T06 | Presencial con ubicación **fuera** del radio | `FUERA_DE_RADIO`, mensaje «Parece que no estás en el lugar de la cátedra», **sin fila** en `registro_asistencia` |
| T07 | Virtual **sin** ubicación | aceptada; `latitud` y `longitud` = `NULL`, `distancia_metros` = `NULL` |
| T08 | Virtual **con** ubicación enviada | aceptada; coordenadas descartadas (`NULL`) |
| T09 | Sesión `HIBRIDA` (si existiera): un solo enlace de canal `QR` | se comporta como presencial: exige ubicación y sede con coordenadas (D61) |
| T10 | Sede sin coordenadas y sesión presencial | **falla al generar el QR** con el nombre de la sede; si el QR existiera, el registro da `SEDE_SIN_COORDENADAS` |
| T11 | `radio_metros` de la sede sobrescribe el parámetro global | con la misma distancia, una sede con radio 50 rechaza y otra con el global (200) acepta |
| T12 | Distancia guardada | `distancia_metros` coincide con `fn_distancia_metros` (±0,1 m) y con pares de distancia conocida |
| T13 | Cédula repetida de docente / externo | se reutiliza la persona (no se duplica `documento_asistente`), nueva vinculación solo si falta |
| T14 | Docente con solo celular | se registra (`contacto_sin_correo = true`), `chk_asistente_correo` sigue protegiendo el resto |
| T15 | Estudiante sin vinculación vigente | `SIN_VINCULACION_VIGENTE` |
| T16 | Recarga del mismo archivo de estudiantes | 0 duplicados en `asistente`, `documento_asistente`, `programa_asistente`, `vinculacion_asistente`; segunda corrida = todo `ACTUALIZADO` |
| T17 | Carga: CC ya perteneciente a **otra** persona | fila rechazada con novedad y motivo; las demás se guardan |
| T31 | Carnet existente con otra CC y otro programa en el mismo periodo (caso `30000124729`) | la CC vieja queda `vigente = false`, la nueva vigente; el programa viejo pasa a `RETIRADO`, el nuevo `ACTIVO`; el *snapshot* de una asistencia posterior es el nuevo |
| T18 | Estudiante que asistió a 4 cátedras | aparece en 4 archivos `_estudiantes.csv` |
| T19 | Estudiante en 2 sesiones de la misma cátedra | 1 vez en ese archivo |
| T20 | Encabezado del CSV de estudiantes | exactamente `CC,Carnet_estudiante,Programa`, igual que el archivo de carga |
| T21 | Docente en 3 cátedras / externo en 2 sesiones de una cátedra | 3 archivos `_docentes.csv` / 1 aparición en `_externos.csv` |
| T22 | Columnas de docentes y externos | `CC,Nombre,Contacto,Facultad` y `CC,Nombre,Contacto,Procedencia`, con los valores como se registraron |
| T23 | ZIP | trae los 3 CSV de **cada** cátedra del periodo (con solo encabezado si no hay asistentes) |
| T24 | Catálogo | `catedras_<periodo>.csv` con **todas** las cátedras del periodo y su `id_evento_asis` |
| T25 | Cátedra sin asistentes | botones deshabilitados con `0`; en el ZIP, archivos con solo encabezado |
| T26 | Texto con `=`, `+`, `-`, `@` al inicio en nombre/procedencia | se exporta con `'` delante |
| T27 | Cátedra con sesiones en dos periodos | aparece en ambos, con los asistentes de cada uno (D70.2) |
| T28 | Un docente y un estudiante con la misma persona | cada asistencia se exporta según el tipo elegido al registrarse |
| T29 | `/admin/exportar` sin sesión o con otro rol | 302 al login / 403 |
| T30 | Aviso temprano: fuera de radio / dentro / canal `ENLACE` | `dentro=false` con distancia / `true` / `requiere_ubicacion=false` |

## 9. Riesgos

**Riesgo aceptado por el equipo — identidad solo con el carnet (D60).** Sin la clave por correo de la D53, quien registra a un estudiante demuestra «saber su carnet» y estar donde la ubicación lo permite; nada más.

| Lo que la ubicación **sí** resuelve | Lo que **no** resuelve |
|---|---|
| Firmar desde fuera del lugar de una cátedra **presencial** (T06) | (a) Alguien que **sí está en el lugar** y firma con el carnet de un compañero ausente |
| | (b) Falsear la ubicación del dispositivo (apps de GPS falso, DevTools) |
| | (c) Las sesiones **virtuales/telepresenciales**: no hay verificación de lugar |

Otros riesgos que se documentan y se aceptan o se mitigan:
- **Docente/externo = declarativo.** Cualquiera escribe una cédula; el tipo (docente/externo) lo elige la persona. Un estudiante puede registrarse como «externo» y desaparecer de su conteo. Queda como límite conocido.
- **IP del cliente detrás de Render.** Sin `ProxyFix` el front guarda la IP del proxy: toda la auditoría por IP y las mitigaciones de abajo serían inútiles. Hay que confiar en `X-Forwarded-For` **solo** del proxy de Render (P15).
- **El aviso temprano revela cercanía.** `GET /ubicacion` responde distancia; alguien podría sondearlo para deducir las coordenadas de la sede. El campus es público y no se devuelven coordenadas, así que se acepta; un tope de peticiones por IP lo cerraría (mitigación opcional).
- **Orden de despliegue.** Si el front nuevo sale antes que las coordenadas de las sedes, **todas** las sesiones presenciales rechazan el registro (`SEDE_SIN_COORDENADAS`) y la creación de cátedras avisa que no hay QR. Por eso P4 va primero y las migraciones antes que el código.
- **QR ya emitidos.** Los de sedes sin coordenadas dejan de servir para registrar. Las sesiones `HIBRIDA` que existan exigirán ubicación (D61).
- **Datos existentes.** La base trae miles de personas del ASIS (14.811 según `guia_ia.md`); si una CC del archivo ya es de otra persona, esa fila se rechaza (T17). No se fusionan personas automáticamente.
- **Excel y ceros a la izquierda / fórmulas** en los CSV (D71).
- **Los CSV de docentes y externos contienen datos personales** (cédula, contacto): solo ADMIN los descarga y no se guardan en el servidor; la política de tratamiento de datos (Ley 1581) ya cubre el uso estadístico, pero conviene confirmarlo con quien responda por ella.

**Mitigaciones opcionales (se proponen, NO se implementan en la v7.2):**
1. **Límite de intentos por IP** en el registro y en el aviso de ubicación (p. ej. N por minuto; requiere P15).
2. **Alerta al administrador** cuando muchas asistencias de una sesión salen de la **misma IP o `user_agent`** (consulta sobre `registro_asistencia`; un contador en la pantalla de la sesión).
3. **Enviar `coords.accuracy`** del navegador y **descartar lecturas muy imprecisas** (p. ej. > 100 m) para no aceptar ni rechazar por una lectura mala; hoy el JS solo la muestra. Implicaría una columna `precision_metros`.

## 10. Fuera de alcance

- Cambiar o borrar la tabla/API de `ClaveAcceso` (solo deja de usarse el flujo, D60).
- Cuentas, login o paneles de estudiante o docente (D55 sigue vigente: solo el administrador inicia sesión).
- Calcular el total de cátedras por estudiante (lo hace quien administra con los archivos).
- Importar docentes o externos por archivo, o editar la facultad/procedencia de un registro ya hecho.
- Verificación del lugar en sesiones virtuales, detección de GPS falso, captcha.
- Cambios al script provisto (`script_bdcatedras.sql`) y a la constitución.

## 11. Pendientes por confirmar contigo (nada se escribe hasta que respondas)

| # | Tema | Mi recomendación |
|---|---|---|
| **P1** | ~~Híbridas por canal~~ | ✅ **Resuelto 2026-11-12:** fuera de alcance; la híbrida se comporta como presencial (D61) |
| **P2** | **Verificación de ubicación en los tres tipos** (estudiante, docente, externo) cuando el canal es `QR` | Sí, los tres (así lo escribe el prompt) |
| **P3** | **Radio inicial** `RADIO_VALIDACION_METROS` | 200 m, ajustable por sede |
| **P4** | ~~Coordenadas de las sedes~~ | ✅ **Resuelto 2026-11-12:** Bello y San Benito confirmadas (D62); sin radio propio |
| **P5** | ~~Códigos de programa del archivo~~ | ✅ **Resuelto 2026-11-12:** camino A con datos ficticios elegidos al azar, sufijo «(mock)» (D69) |
| **P6** | ~~`personas_mock_usbmed.csv`~~ | ✅ **Resuelto 2026-11-12:** leído (sección 1) |
| **P7** | ~~`DOCENTE.exige_programa`~~ | ✅ **Resuelto 2026-11-12:** se pone en `false` (migración `061`) |
| **P8** | Docente/externo nuevo con `es_externo = true` por no tener código ASIS (como D56) | Sí |
| **P9** | Estudiante cargado sin nombre: `nombres = 'Estudiante <carnet>'`, `apellidos = '-'` (D67) | Sí; alternativa: columnas nulables |
| **P10** | Sesión presencial cuya sede es **virtual** (dato inconsistente): ¿falla al emitir el QR? | Sí, con mensaje propio |
| **P11** | Asistencias de otras vinculaciones (administrativo, egresado…): ¿no se exportan (solo se cuentan en pantalla) o entran en «Externos»? | No se exportan, se cuentan |
| **P12** | Identificador de la cátedra: `id_evento_asis` (9 dígitos) o `id_catedra` numérico interno | `id_evento_asis` |
| **P13** | Grupo sin asistentes: botón deshabilitado (pantalla) y archivo solo con encabezado (ZIP) | Como está escrito en D70.6 |
| **P14** | Cátedra con sesiones en dos periodos: aparece en ambos con los registros de cada uno | Sí (D70.2) |
| **P15** | `ProxyFix` en el front para obtener la IP real detrás de Render (hoy se guarda la del proxy) | Sí, antes de usar cualquier mitigación por IP |
| **P16** | Cómo marcar el registro por carnet: columna `identificado_por` (D65) o el origen `QR_CODIGO` que sugiere el prompt | `identificado_por` |
| **P17** | Ruta del script de verificación: `api_catedras/pruebas/verificar_v72.ps1` (como v5/v7/v7.1) o `pruebas/verificar_v72.ps1` en la raíz | `api_catedras/pruebas/` |
| **P18** | La asistencia de la v7.2 **no exige** inscripción ni cuenta, como ya era desde la D55; confirmo que no se reactiva nada de eso | Confirmado en este borrador |
| **P1b** | ~~¿Desactivar la modalidad `HIBRIDA`?~~ | ✅ **Resuelto 2026-11-12:** sí, `activo = false` en la migración `062` |
| **P19** | ~~Carnet que ya existe con otra CC / otro programa~~ | ✅ **Resuelto 2026-11-12:** el carnet manda; la CC anterior queda `vigente = false` y los otros programas `ACTIVO` del mismo periodo pasan a `RETIRADO` (D68) |
| **P20** | Cátedras de la exportación: activas + inactivas con asistencias (D70.7), no absolutamente todas | Como está implementado |

**Respondidos el 2026-11-12:** P1, P1b, P4, P5, P6, P7 y P19. **Migraciones `060`–`066` escritas y probadas (parte 1).** Queda abierto solo P20 (criterio de cátedras exportadas, ya implementado como D70.7); tu OK a las migraciones habilita la parte 2 (API).

## 12. Estado de la entrega (2026-11-12)

Todo lo anterior se construyó **por partes con OK entre ellas**: migraciones `060`–`066` → API → front → verificación y documentos.

**Qué quedó y dónde**

| Parte | Archivos principales |
|---|---|
| Base de datos | `db/postgres/migraciones/060…066` (montadas en `docker-compose.yml` como `z060…z066`); prueba SQL `db/postgres/pruebas/prueba_migraciones_v72.sql` |
| API | `AsistenciaPublica`, `CargaEstudiante` y `Exportacion` (controlador, servicio, repositorio, peticiones y modelos); `Sede` con ubicación y radio; `QrSesion` responde 409 si la sede no tiene coordenadas; el modelo `RegistroAsistencia` expone `identificado_por`, `fk_facultad_snapshot`, `procedencia` y `distancia_metros` |
| Front | `routes/registro.py` (3 opciones, aviso temprano), `routes/carga.py` y `servicios/carga_asistentes.py`, `routes/admin_exportar.py` y `servicios/exportacion.py`, sedes y panel; `static/geolocalizacion.js`; `ProxyFix` opcional (`PROXY_CONFIABLES`) |
| Pruebas | capas (`api_catedras/pruebas/Programa.cs`, 689/1), front (`front_catedras/pruebas/prueba_v72.py`, 104/0), API y BD reales (`api_catedras/pruebas/verificar_v72.ps1`, 114/0, repetible y con una ventana de programación activa) |

**Lo que salió distinto del borrador (y por qué)**

1. **Sin envoltorios `fn_*_api`** en la `063` ni en la `064`: los procedimientos devuelven sus `INOUT` y el repositorio los lee con `CALL`, como `sp_guardar_asistente`.
2. **`CargaEstudiante` es un controlador propio**, no una acción de `Asistente`, para no tocar uno que ya funciona. Son **3 controladores nuevos** (de 42 a 45 archivos).
3. **Un PUT de `Sede` sin ubicación no la borra** (compatibilidad con la v7.1); una sede que pasa a virtual sí la pierde. En la BD, `sp_actualizar_sede` solo cambia la ubicación si recibe `p_actualizar_geo = true`.
4. **El modelo `RegistroAsistencia` de la API no traía las 4 columnas nuevas** y las lecturas no las mostraban; se agregó (se descubrió al escribir `verificar_v72.ps1`).
5. **Un token «vacío» (`00000000-…`) daba 500** en `GET enlace/{token}`; ahora es un enlace que no existe (404). También se descubrió al verificar.
6. **Cátedras exportadas:** las activas y las inactivas **con asistencias** en el periodo (D70.7, P20).
7. **Estado `PERSONA_INACTIVA`** además de los de la D64, para una cédula cuya persona está desactivada.
8. **El front viejo de la clave** (`registro_solicitar.html`, `registro_validar.html`) quedó marcado «sin efecto» y sin borrar (D60). Las pruebas `prueba_d55_v71.py`, `prueba_panel_admin_v71.py` y `prueba_calendario_ponente_v71.py` se ajustaron al flujo nuevo y a las 40 tarjetas del panel.
9. **Estado de la prueba de capas:** 689 pasadas y 1 fallida («No quedan peticiones de escritura de Bitacora»), que **ya fallaba antes de la v7.2**; la de «41 controladores» también fallaba (había 42) y ahora afirma los 45 reales.

**Qué falta antes de usarlo en producción (decisión del equipo)**

- Aplicar las migraciones `060`–`066` a la base **desplegada** (Render/Neon) **antes** de desplegar la API y el front; no se tocó esa base.
- Poner `PROXY_CONFIABLES=1` en el front desplegado en Render (si no, la IP guardada es la del proxy).
- Reemplazar los 9 programas ficticios `(mock)` por los reales si llega el archivo real de programas.
- Confirmar con quien responda por el tratamiento de datos (Ley 1581) el uso de las cédulas y contactos de docentes y externos.
- Mitigaciones de la sección 9 (límite de intentos por IP, alerta por muchas firmas desde una IP, `coords.accuracy`): propuestas, **no implementadas**.
- Las pantallas de administración nuevas (carga con periodo, sedes con ubicación, `/admin/exportar`) se verificaron con HTML renderizado y por HTTP, pero **no en un navegador con sesión de administrador**.

## 13. Apartado adicional — El ASISTENTE ve, genera y regenera el QR de una sesión (D73 · RF-S15 · P21)

> **Estado: APROBADO e IMPLEMENTADO el 2026-11-13. No hay migración y no se toca la API ni la BD.**
> Prueba: `front_catedras/pruebas/prueba_asistente_qr_v72.py` (39 pasadas, cubre P21.1 a P21.10).
> **Lo que salió distinto del borrador:** el detalle de la cátedra mostraba toda la columna «Acciones» (incluido el botón de QR) solo al ADMIN,
> así que el asistente nunca veía el botón; ahora ve la columna con **solo** el botón de QR (`templates/admin/catedra_detalle.html`). Además la
> miga de pan de la pantalla del QR no le ofrece al asistente los enlaces al panel y al detalle de la sesión (le darían 403), y si la API falla al
> cargar la sesión, vuelve a la lista de cátedras en vez de a la de sesiones.
> Es un cambio de permisos del front: continúa después de `D72`, `RF-S14` y `P20`.

**RF-S15 — El ASISTENTE puede abrir la pantalla del QR de una sesión (`/admin/sesiones/<id>/qr`), generar el QR si falta y regenerarlo si hubo un error**, con los mismos botones que el ADMIN.
Hoy el asistente llega al detalle de la cátedra, pulsa el botón de QR de una sesión y recibe **403**, porque `admin.sesiones_qr` no está en `ENDPOINTS_DEL_ASISTENTE`.

**D73 — Qué cambia y qué no.**
1. **Permiso.** `admin.sesiones_qr` se agrega a `ENDPOINTS_DEL_ASISTENTE` (`routes/admin.py`). Sirve para el GET (ver) y el POST («Generar el QR» y «Regenerar QR», este con la confirmación del navegador que ya existe: «el anterior dejará de servir»).
2. **Qué ve el asistente:** título de la cátedra y de la sesión, el QR, el enlace con «Copiar», «Proyectar en pantalla completa», la insignia de estado de la ventana y las horas en que sirve. **No ve** la tarjeta «Asistencia de esta sesión» (son datos de personas).
3. **El servidor decide, no la plantilla.** La ruta calcula `puede_ver_asistencia = tiene_rol(ROL_ADMIN)` y, si es falso, **ni siquiera consulta** `QrSesion/asistencia` (no viajan datos de personas a la plantilla). La plantilla solo pinta la tarjeta y el JavaScript de refresco cuando la bandera es verdadera, así el asistente no hace llamadas que terminarían en 403.
4. **`admin.sesiones_asistencia_json` sigue siendo solo del ADMIN** (no entra en `ENDPOINTS_DEL_ASISTENTE`).
5. **Rastro.** `sesiones_qr` ya envía `usuario_actual()['id_usuario']` a `POST /api/QrSesion` (`fk_usuario`), y `sp_emitir_qr_sesion` lo guarda en `enlace_registro.fk_usuario_crea`. Cuando lo hace un ASISTENTE queda **su** usuario como creador; no se necesita ningún cambio para eso, y una prueba lo comprobará.
6. **Todo lo demás queda igual:** la regla de la ventana (D55, migración 051; la impone la BD), que la página pública no publique el token, el 403 del asistente en el resto de `/admin` (usuarios, sedes, parámetros…) y todo lo que ya hace el ADMIN.

**Riesgo aceptado por el equipo — regenerar invalida el QR anterior.** Si el asistente regenera después de que el QR ya se compartió o se proyectó, quienes lo tengan no podrán registrarse con él. Es el comportamiento que ya existe para el ADMIN; la confirmación del navegador lo advierte. No se agrega ninguna restricción extra.

**Pruebas (P21).**

| # | Caso | Resultado esperado |
|---|---|---|
| P21.1 | ASISTENTE abre el QR desde el detalle de la cátedra | 200; ve QR, enlace, estado y horas |
| P21.2 | ASISTENTE en esa pantalla | **no** ve la tarjeta de asistencia; `GET /admin/sesiones/<id>/asistencia` → 403 |
| P21.3 | ASISTENTE genera el QR de una sesión que no lo tenía | se crea |
| P21.4 | ASISTENTE regenera el QR | el anterior deja de servir y el nuevo sí |
| P21.5 | QR creado por un ASISTENTE | `fk_usuario` enviado = el del asistente (queda como `fk_usuario_crea`) |
| P21.6 | ASISTENTE en el resto de `/admin` (p. ej. `/admin/usuarios`) | 403 |
| P21.7 | ADMIN | conserva todo: QR, regenerar y asistencia en vivo |
| P21.8 | Sin sesión | 302 al login |
| P21.9 | Sesión inexistente | 404 |
| P21.10 | Página pública de la sesión | sigue sin mostrar el token |

**Archivos que se tocarían:** `front_catedras/routes/admin.py` (la lista y `sesiones_qr`), `templates/admin/sesion_qr.html` (tarjeta y script bajo la bandera), una prueba nueva en `front_catedras/pruebas/`, y al final `README.md` y `0_mapa_versiones.md` con una línea cada uno. La constitución no se modifica.

| # | Pendiente | Mi recomendación |
|---|---|---|
| **P21** | ¿Aprobáis D73 tal como está (el asistente regenera sin pedir nada más que la confirmación existente)? | Sí |

## 14. Apartado adicional — El ASISTENTE sube y descarga los CSV de asistencia (D74 · RF-S16)

**D74.** El rol `ASISTENTE` puede (1) **subir** el CSV de estudiantes en `/carga_datos` y (2) **descargar** los CSV de `/admin/exportar`
(uno por cátedra y tipo, el catálogo del periodo y el ZIP). Se agregan `admin.exportar`, `admin.exportar_catedra_csv`, `admin.exportar_catalogo`
y `admin.exportar_zip` a `ENDPOINTS_DEL_ASISTENTE`, y `carga.carga_datos` acepta `ROL_ASISTENTE`. No cambia la API ni la BD.

- Sigue siendo solo del ADMIN: el detalle de lotes (`/admin/lotes/...`; al asistente se le muestra el número del lote sin enlace), personas, usuarios,
  sesiones y la lista en vivo de quienes se registraron (`admin.sesiones_asistencia_json`).
- **Dato a tener presente:** los CSV de exportación contienen carnets y datos de contacto de docentes y externos; abrirlos al asistente amplía
  quién ve datos de personas respecto de D73. Fue una decisión pedida expresamente.
- **Corrección de D73:** `catedra_detalle.html` envolvía toda la columna de acciones en `tiene_rol('ADMIN')`, así que el asistente no veía el botón de QR
  (la prueba `P21.1` fallaba). Ahora la columna se muestra al ADMIN y al ASISTENTE; el asistente solo ve el botón de QR.
- Pruebas: `prueba_v72.py` sección 8, `prueba_rol_asistente.py` y `prueba_asistente_qr_v72.py` (el `/admin/exportar` ya no se espera en 403).
