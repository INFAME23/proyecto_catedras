# Especificación — Versión 3 del proyecto: personas y carga de asistentes

> ✅ **CONSTRUIDA Y VERIFICADA (2026-10-07)** — pendiente solo el commit
> y el tag `v3` (Fase 31 de [8_tasks.md](8_tasks.md)). V2 quedó con su tag
> `v2`. Esta spec se revisó contra la BD real y contra lo construido; las
> correcciones respecto al borrador están marcadas con **(corregido)** y
> resumidas en el ADR D19 de [4_research.md](4_research.md).
>
> Rige la constitución del proyecto:
> [../1_constitution.md](../1_constitution.md) y la
> [guía operativa para la IA](../../guia_ia.md). Mapa completo de
> versiones: [../0_mapa_versiones.md](../0_mapa_versiones.md).
>
> En v3 el sistema ES esto: la API agrega las **7 tablas** de personas y
> carga de asistentes: `lote_carga_asistente`, `asistente`,
> `novedad_carga`, `documento_asistente`, `vinculacion_asistente`,
> `programa_asistente` y `consentimiento_datos`. Con esto queda resuelto
> "¿quién asistió?" — el prerequisito de toda sesión y todo registro de
> asistencia de las versiones siguientes.
>
> | Documento de esta versión | Contenido |
> |---|---|
> | **2_spec.md** (este) | QUÉ construir en v3 y sus criterios de aceptación |
> | [3_plan.md](3_plan.md) | CÓMO: capas de la API y los 2 SP maestro-detalle |
> | [4_research.md](4_research.md) | Decisiones y alternativas *(lectura opcional)* |
> | [5_data_model.md](5_data_model.md) | Las 7 tablas de esta versión, documentadas |
> | [6_contracts.md](6_contracts.md) | Contratos de integración |
> | [7_quickstart.md](7_quickstart.md) | Arranque y smoke test de v3 |
> | [8_tasks.md](8_tasks.md) | Orden de construcción |

---

## 1. Propósito de la v3

V1 y V2 dejaron catálogos y estructura académica, sin ninguna persona
todavía. V3 agrega **quién** — el `asistente` — y resuelve un problema
real y medible: el archivo de personas del ASIS (16.093 filas en el
caso real; 14.811 ya sembradas de ejemplo) llega con errores, nombres
repetidos, programas escritos en texto libre y documentos sin validar.
Cargarlo de una vez, sin evidencia de qué se aceptó y qué no, es
indepurable.

```
lote_carga_asistente (↺ cada ejecución de la carga)
   │  1
   ├──< asistente (↺ FK NULL hasta que exista 'usuario' en v5)
   │       │
   │       ├──< documento_asistente ──> tipo_documento (v1)
   │       ├──< vinculacion_asistente ──> tipo_vinculacion (v1)
   │       ├──< programa_asistente ──> programa_academico (v2), periodo_academico (v1)
   │       └──< consentimiento_datos
   │
   └──< novedad_carga   (filas que la carga RECHAZÓ, con el motivo)
```

Dos relaciones **maestro-detalle** nuevas, resueltas con un SP cada una
(Guía IA, Regla 1 — ver [3_plan.md](3_plan.md) §4):

1. **`asistente` + sus 4 detalles.** Crear una persona junto con su
   primer documento, su vinculación vigente, su programa y su
   consentimiento, en una sola transacción — tanto si lo hace un
   administrador a mano como si lo hace la carga masiva, fila por fila.
2. **`lote_carga_asistente` + `novedad_carga`.** Cerrar una ejecución de
   carga registrando, en una sola transacción, los contadores finales
   (`filas_leidas/aceptadas/rechazadas`) y el detalle de cada fila
   rechazada.

**Decisión de diseño central (ver ADR D15):** a diferencia del retrofit
de v2 (todo-o-nada), la carga masiva es **deliberadamente parcial**: una
fila mal diligenciada entre miles no debe descartar las demás. Por eso
cada `asistente` de la carga se guarda en **su propia llamada** al SP 1
(una transacción por fila); el SP 2 solo **cierra** el lote al final,
con el recuento de lo que pasó.

## 2. Alcance

**Incluye:**
- CRUD de `Asistente`, con un POST que acepta detalles iniciales
  opcionales (documentos, vinculaciones, programas, consentimiento) —
  igual en espíritu al retrofit de `ProgramaAcademico` en v2.
- `DocumentoAsistente`: CRUD completo (5 verbos); la regla "solo un
  documento vigente por asistente" la vigila un índice único parcial de
  la BD (`ux_documento_vigente`) — no se revalida en C# (D9 de v2).
- `VinculacionAsistente` y `ProgramaAsistente`: tablas de **llave
  compuesta** (historial de vigencias, sin columna `id`). Siguen un
  patrón nuevo, documentado en el ADR D16: sin `GET/{id}` de una sola
  columna, sin `PUT`, sin `DELETE` — una acción de **cerrar** en vez de
  borrar.
- `ConsentimientoDatos`: solo listar, obtener y crear (Ley 1581: un
  consentimiento es un hecho legal que se registra, nunca se borra —
  más estricto que `AliasPrograma`, que sí admite `DELETE`).
- `LoteCargaAsistente`: crear (abre el lote) y cerrar (invoca el SP 2
  con los contadores y las novedades); sin `PUT`/`PATCH` genérico ni
  `DELETE` (es la bitácora de una ejecución).
- `NovedadCarga`: solo lectura — nace exclusivamente al cerrar un lote.
- Frontend: pantalla de **carga de datos** (`front_catedras/`, nueva
  ruta) que sube un archivo, lo interpreta en Python (no en la API: ver
  ADR D17) y llama al flujo de la API fila por fila; listado de
  asistentes con buscador.

**No incluye (es de otra versión):**
- `sesion`, `ponencia` (v4) y todo lo que dependa de `usuario` (v5):
  login real, `rol_por_usuario`, `enlace_registro`, `clave_acceso`,
  `registro_asistencia`.
- El campo `fk_usuario` de `lote_carga_asistente` existe en la tabla
  pero en v3 viaja **siempre vacío** (la salvedad de la FK circular,
  §3 de [0_mapa_versiones.md](../0_mapa_versiones.md)): se expone en la
  API como un número opcional, sin ningún `Usuario` detrás todavía.
- Encuestas, migración a ASIS, bitácora y parámetros (v6-v7) — aunque la
  BD ya trae, desde el script de la v1, las funciones y procedimientos
  que esas versiones usarán (`sp_responder_encuesta`,
  `sp_generar_lote_migracion`, etc.); v3 no los toca.

## 3. Entidades y requisitos funcionales

Numeración de RF continúa desde v2 (terminó en RF94).

### Asistente

> RF95-RF101. La tabla central de la versión. `id_asis` es el código
> ASIS (alfanumérico, 5-15 caracteres); puede ser `NULL` solo si
> `es_externo = true`. Debe haber **al menos un correo** (institucional
> o personal). `correo_institucional`/`correo_personal` son `citext`
> (comparación sin distinguir mayúsculas) — la BD lo garantiza, la
> petición no revalida ese criterio, solo el formato básico
> (`@` y `.`).

- **RF95 — Listar** (`GET /api/Asistente`): 200; vacía → 204. Acepta
  `?limite=` (igual que las demás) y, para que el front pueda buscar
  sin descargar 14.811 filas de golpe (a diferencia del catálogo de
  `Catedra` en v2, que sí lo hacía — ver ADR D18 sobre por qué aquí NO
  se repite esa decisión), `?q=` que filtra por nombre, apellido o
  `id_asis` **en SQL** (`ILIKE`), no en memoria, y `?pagina=` (desde 1,
  de `limite` filas cada una: `LIMIT/OFFSET` en SQL; `pagina <= 0` → 400).
  El sobre agrega `pagina`.
- **RF96 — Obtener por id** (`GET /api/Asistente/{idAsistente}`): 200;
  inexistente → 404.
- **RF97 — Crear** (`POST /api/Asistente` con **AsistenteCrear**):
  `nombres`, `apellidos` obligatorios; `id_asis` obligatorio si
  `es_externo` es `false` u omitido (formato `^[0-9A-Za-z]{5,15}$`);
  al menos uno de `correo_institucional`/`correo_personal`. **(corregido)**
  Estas dos reglas cruzan campos del mismo body: las valida el
  **servicio** → **400**, antes de tocar la BD (ver RF101 y ADR D19).
  Acepta también `nombre_completo_asis` (opcional). Responde
  `200 {estado, mensaje, id_asistente}`. **Retrofit
  del mismo día (Guía IA, Regla 1):** acepta además 4 listas
  **opcionales** — `documentos`, `vinculaciones`, `programas`,
  `consentimiento` (un solo objeto, no lista) — que se guardan en la
  MISMA transacción vía `sp_guardar_asistente` (ver
  [3_plan.md](3_plan.md) §4.1). Si cualquier detalle falla (documento
  duplicado, programa inexistente), **no se crea ni el asistente**.
  `id_asis` o correo institucional duplicados → 500.
- **RF98 — Reemplazar** (`PUT .../{idAsistente}` con
  **AsistenteReemplazo**): las columnas propias (sin tocar detalles);
  inexistente → 404.
- **RF99 — Actualizar parcialmente** (`PATCH .../{idAsistente}`):
  todo opcional; body vacío → 400; inexistente → 404.
- **RF100 — Eliminar** (`DELETE .../{idAsistente}`): `filasEliminadas`;
  inexistente → 404. `documento_asistente`, `vinculacion_asistente`,
  `programa_asistente` y `consentimiento_datos` (y `clave_acceso`, de v5)
  son `ON DELETE CASCADE` (se borran con el asistente); pero `usuario`,
  `ponencia` y `registro_asistencia` (de v4/v5) son `RESTRICT` — borrar
  un asistente que ya tiene cualquiera de esas filas → 500, sin que la
  cascada hacia los 4 detalles llegue a ejecutarse (mismo patrón que
  `ProgramaAcademico` en v2, RF79). **(corregido)** El borrador decía que
  `programa_asistente` era `RESTRICT`: en el esquema es `CASCADE`
  (`fk_progasi_asistente`). Lo que de verdad bloquea en los datos de
  ejemplo son las **168** filas de `registro_asistencia` y la **1** de
  `usuario` (`ponencia` aún no tiene filas).
- **RF101 — `id_asis` obligatorio si NO es externo:** **(corregido)** el
  `CHECK chk_asistente_externo` es `(es_externo = false AND id_asis IS NOT
  NULL) OR es_externo = true`: un externo **puede** tener `id_asis` (no
  son excluyentes). En `POST` y `PUT` (donde llega la fila completa) el
  **servicio** valida el cruce → **400** con un mensaje claro; en `PATCH`
  (que solo trae una parte de la fila) no puede hacerlo, y el `CHECK` de
  la BD queda como última muralla → 500. Mismo criterio para "al menos un
  correo" (`chk_asistente_correo`). Ver ADR D19.

### DocumentoAsistente

> RF102-RF106. CRUD completo (5 verbos, a diferencia de
> `AliasPrograma`): un documento se corrige editando `numero` o
> `vigente`, no solo borrando y recreando, porque `vigente` es un
> estado que cambia con intención (RF104).

- **RF102 — Listar** (`GET /api/DocumentoAsistente?fkAsistente=`):
  sin el filtro, todas hasta el límite; con él, los documentos de un
  asistente.
- **RF103 — Crear** (`POST` con **DocumentoAsistenteCrear**):
  `fk_asistente`, `fk_tipo_documento`, `numero` obligatorios; `vigente`
  opcional (por defecto `true`). Si ya existe un documento vigente para
  ese asistente y este también llega `vigente: true` → 500
  (`ux_documento_vigente`, índice único parcial — D9: no se revalida).
  El flujo recomendado para "reemplazar" el vigente es
  `PATCH` el anterior a `vigente: false` y luego `POST` el nuevo.
- **RF104 — Actualizar/Reemplazar** (`PUT`/`PATCH`): igual molde que
  siempre; inexistente → 404.
- **RF105 — Eliminar**: `filasEliminadas`; sin FK entrante, nunca falla
  por integridad referencial (como `AliasPrograma`, RF83 de v2).
- **RF106 — `numero` único por tipo de documento** (`uq_documento_numero`,
  compuesta con `fk_tipo_documento`): dos documentos del mismo tipo con
  el mismo número → 500. Dos asistentes pueden compartir número de
  documento si el **tipo** es distinto (p. ej. una cédula y un
  pasaporte con la misma cadena, caso de prueba de datos sucios).

### VinculacionAsistente y ProgramaAsistente — tablas de vigencia, llave compuesta

> RF107-RF111. Ver ADR **D16**. Ninguna de las dos tiene columna `id`:
> la llave es `(fk_asistente, fk_tipo_vinculacion, fecha_ini)` y
> `(fk_asistente, fk_programa, fk_periodo)` respectivamente. El molde de
> 5 verbos con `/{id}` numérico **no aplica** — en su lugar:

- **RF107 — Listar** (`GET /api/VinculacionAsistente?fkAsistente=`):
  sin el filtro, todas hasta el límite.
- **RF108 — Abrir una vinculación** (`POST` con
  **VinculacionAsistenteCrear**): `fk_asistente`, `fk_tipo_vinculacion`
  obligatorios; `fecha_ini` opcional (por defecto hoy). Sin `GET/{id}`
  ni `PUT`: la llave es compuesta y se manda completa en cada petición
  que la necesite.
- **RF109 — Cerrar una vinculación** (`PATCH
  /api/VinculacionAsistente/cerrar` con **VinculacionAsistenteCerrar**):
  body = las 3 columnas de la llave + `fecha_fin` (por defecto hoy).
  Combinación inexistente → 404; ya cerrada (`fecha_fin` no nulo) → 400
  (regla de negocio: no se "re-cierra" algo que ya terminó, a
  diferencia de un `PATCH` normal que sí permite pisar un valor — se
  valida en el servicio). Sin `DELETE`: el historial no se borra.
- **RF110 — Listar/abrir/cambiar estado de `ProgramaAsistente`**
  (`GET/POST /api/ProgramaAsistente`, `PATCH .../cambiar-estado`):
  mismo patrón D16 que `VinculacionAsistente`, con una diferencia: en
  vez de `fecha_fin`, lo que cambia es `estado` (`ACTIVO`, `RETIRADO`,
  `GRADUADO`, `APLAZADO` — lista cerrada, `[RegularExpression]` en la
  petición). `PATCH /api/ProgramaAsistente/cambiar-estado` con
  combinación inexistente → 404.
- **RF111 — Llave compuesta como defensa, no como adorno:** el archivo
  real del ASIS trae el mismo par asistente-programa repetido hasta 3
  veces; la llave de 3 columnas absorbe esos duplicados sin necesidad
  de deduplicar en C# antes de insertar (comentario del propio
  esquema). Un `POST` con la misma combinación exacta ya existente →
  500 (duplicado de PK, D9).

### ConsentimientoDatos

> RF112-RF113. Ley 1581 de 2012: el consentimiento es un **hecho**
> registrado, nunca un aviso en pantalla ni una fila editable.

- **RF112 — Listar, obtener, crear** (`GET` con `?fkAsistente=`
  opcional **(corregido: el quickstart ya lo usaba)**, `GET/{id}`, `POST` con
  **ConsentimientoDatosCrear**): `fk_asistente`, `version_politica`
  obligatorios; `canal` opcional (por defecto `WEB`, lista cerrada:
  `WEB`, `PRESENCIAL`, `CORREO`, `IMPORTADO`); `aceptado_en` lo pone la
  BD (`now()`); `ip` opcional. Un asistente que ya aceptó esa
  `version_politica` → 500 (`uq_consentimiento_ver`).
- **RF113 — Sin `PUT`, `PATCH` ni `DELETE`:** más estricto que
  `AliasPrograma` (D12 de v2): un consentimiento no se corrige ni se
  retira por API — es evidencia de cumplimiento legal. Si una versión
  futura necesita revocarlo, será una fila NUEVA con otro `canal`/fecha,
  nunca un borrado del hecho anterior.

### LoteCargaAsistente y NovedadCarga

> RF114-RF118. Ver el ADR **D15** (por qué es parcial, no todo-o-nada)
> y **D17** (por qué el archivo se interpreta en Python, no en C#).

- **RF114 — Listar, obtener** (`GET`, `GET/{id}`): normales.
- **RF115 — Abrir un lote** (`POST /api/LoteCargaAsistente` con
  **LoteCargaAsistenteCrear**): solo `nombre_archivo` obligatorio.
  `fk_usuario` siempre `null` en v3 (§2). Los 3 contadores nacen en 0.
  Devuelve `id_lote_carga`: el cliente (el front, durante la carga) lo
  usa como `fk_lote_carga` en cada `POST /api/Asistente` de esa
  ejecución.
- **RF116 — Cerrar un lote** (`POST
  /api/LoteCargaAsistente/{id}/cerrar` con **LoteCargaAsistenteCerrar**):
  body = `filas_leidas`, `filas_aceptadas`, `filas_rechazadas`,
  `observaciones` opcional, y `novedades` (lista, puede ser vacía) con
  `numero_fila`, `contenido_crudo`, `motivo` por cada fila rechazada
  **(corregido: snake_case, como todo el JSON del proyecto; el borrador
  mezclaba camelCase)**. Guarda los contadores y las novedades en **una
  sola transacción** (`sp_guardar_lote_carga`, Guía IA Regla 1).
  `filas_aceptadas + filas_rechazadas > filas_leidas` → 400, y **una
  novedad por cada fila rechazada** (`novedades.length !=
  filas_rechazadas` → 400, RNF8) — ambas en el servicio, ANTES de tocar
  el repositorio. `motivo` de más de 200 caracteres → 422 (es
  `VARCHAR(200)`: si llegara a la BD, revertiría el cierre entero).
  Lote inexistente → 404. Lote ya cerrado (algún contador > 0 o
  `observaciones` con valor — ver [5_data_model.md](5_data_model.md)
  §2.1) → 400: un lote no se cierra dos veces. Responde
  `200 {estado, mensaje, lote}`.
- **RF117 — Sin `PUT`/`PATCH` genérico ni `DELETE`:** el lote es la
  bitácora de una ejecución ya ocurrida (mismo espíritu que `Bitacora`
  de v7, de solo lectura por triggers — aquí de "solo lectura tras
  cerrarse").
- **RF118 — `NovedadCarga` — listar y obtener únicamente**
  (`GET /api/NovedadCarga?fkLoteCarga=`, `GET/{id}`): sin `POST` propio
  (nace solo dentro de RF116), sin `PUT`/`PATCH`/`DELETE`.

## 4. Requisitos no funcionales

Rigen los mismos RNF1-RNF7 de v1/v2, sin cambios. Se añaden:

- **RNF8 — Operación parcial con evidencia (D15):** ninguna operación
  de carga masiva puede perder la causa de un rechazo; toda fila
  rechazada queda en `novedad_carga` con su motivo textual.
- **RNF9 — El archivo se interpreta fuera de la API (D17):** ninguna
  librería de lectura de Excel/CSV se agrega a `ApiCatedras.csproj`
  (Artículo 2: solo 3 paquetes permitidos, y ninguna spec de v3 pide
  uno nuevo); la interpretación del archivo vive en
  `front_catedras/` (Python), que ya tiene herramientas naturales para
  eso.

## 5. Criterios de aceptación

1. `docker compose up -d --build` deja los 22 recursos (15 de v1+v2 +
   7 de v3) respondiendo en `/swagger`.
2. `GET /api/Asistente?limite=5` devuelve 5 de los 14.811 asistentes de
   ejemplo, con `total` reflejando el límite (mismo criterio que
   `Catedra` en v2); `GET /api/Asistente?q=1000445` filtra por SQL
   **(corregido: los nombres sembrados son genéricos —"Asistente ASIS
   1000445"—, así que `?q=Garcia` no encuentra a nadie y responde 204)**.
3. `POST /api/Asistente` con `documentos`, `vinculaciones`, `programas`
   y `consentimiento` en el mismo body → 200, y las 4 tablas de detalle
   muestran las filas correspondientes con el `id_asistente` recién
   creado; el mismo `POST` repetido con el mismo `id_asis` → 500 y
   **nada** queda creado (ni el asistente ni ningún detalle).
4. `POST /api/DocumentoAsistente` con un segundo documento `vigente:
   true` para un asistente que ya tiene uno vigente → 500
   (`ux_documento_vigente`); tras `PATCH` el primero a `vigente: false`,
   el mismo `POST` → 200.
5. `POST /api/VinculacionAsistente` abre una vinculación; `PATCH
   /api/VinculacionAsistente/cerrar` con la misma llave la cierra
   (`fecha_fin` queda puesta); repetir el `PATCH` → 400 (ya estaba
   cerrada).
6. `POST /api/LoteCargaAsistente` abre un lote; crear 2 `Asistente` con
   ese `fk_lote_carga` y luego `POST .../{id}/cerrar` con
   `filas_leidas: 3, filas_aceptadas: 2, filas_rechazadas: 1` y 1 novedad
   → 200; `GET /api/NovedadCarga?fkLoteCarga={id}` muestra esa fila con
   su motivo. El mismo `POST .../cerrar` repetido → 400 (ya cerrado).
7. `DELETE /api/Asistente/1` (tiene `registro_asistencia` sembrado,
   `RESTRICT`) → 500; sus `documento_asistente`/`vinculacion_asistente`
   NO se borran (la cascada no llegó a ejecutarse — mismo patrón que
   `ProgramaAcademico` en v2). **(corregido: el borrador usaba
   `programa_asistente`, que es `CASCADE` y no bloquea.)** Un asistente
   creado en la prueba sí se borra, y con él sus 4 detalles.
8. **Prueba de capas ampliada:** `dotnet run --project pruebas` cubre
   las 22 entidades sin PostgreSQL, incluida la validación de
   `id_asis`/`es_externo` y de los contadores del cierre de lote con
   repositorios FALSOS.
9. El front muestra una pantalla de carga de datos que sube un archivo,
   lo interpreta en Python y reporta cuántas filas se aceptaron y
   cuántas se rechazaron (con el motivo de cada una); el listado de
   asistentes (`/admin/asistentes` o equivalente) pagina y busca por
   nombre/`id_asis` sin descargar las 14.811 filas de una vez (a
   diferencia del catálogo de cátedras de v2 — ver D18).
10. El panel de administración (`/admin`) muestra las 7 tarjetas nuevas
    (o 5, si `NovedadCarga` y los detalles de `Asistente` se integran
    en su pantalla — decisión de UI, no de contrato, libre en
    [3_plan.md](3_plan.md)). **Decisión tomada:** las 7 tarjetas más una
    de "Carga de datos"; los detalles se crean y se cierran desde la
    ficha del asistente (`/admin/asistentes/{id}`), así su
    `fk_asistente` nunca se digita (Guía IA, Regla 2).
