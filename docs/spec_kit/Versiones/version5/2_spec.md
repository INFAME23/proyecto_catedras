# Especificación — Versión 5 del proyecto: usuarios, login y registro de asistencia

> ⚠️ **BORRADOR.** Escrito mientras V2-V4 todavía no tienen su tag. El
> Artículo 1 de la constitución dice que la spec siguiente se escribe
> **después** de cerrar la versión en curso con su tag. Este documento
> se revisa una vez más contra lo que realmente quedó en V4 antes de
> considerarse spec oficial — puede cambiar.
>
> Rige la constitución del proyecto:
> [../1_constitution.md](../1_constitution.md) y la
> [guía operativa para la IA](../../guia_ia.md) — en esta versión
> entra en juego por primera vez su **Regla 3** (BCrypt). Mapa completo
> de versiones: [../0_mapa_versiones.md](../0_mapa_versiones.md).
>
> En v5 el sistema ES esto: la API agrega las **5 tablas** que hacen
> real el login de personal (`usuario`, `rol_por_usuario`) y el
> auto-registro de asistencia de los asistentes vía QR/enlace + clave
> de un solo uso (`enlace_registro`, `clave_acceso`,
> `registro_asistencia`). Es la versión con más decisiones de diseño
> nuevas del proyecto — ver [4_research.md](4_research.md), con **8
> ADR nuevos** (D21-D28).
>
> | Documento de esta versión | Contenido |
> |---|---|
> | **2_spec.md** (este) | QUÉ construir en v5 y sus criterios de aceptación |
> | [3_plan.md](3_plan.md) | CÓMO: capas de la API, `SELECT * FROM fn_..._api(...)` en vez de `CALL` |
> | [4_research.md](4_research.md) | Decisiones y alternativas *(lectura opcional, pero esta vez recomendada)* |
> | [5_data_model.md](5_data_model.md) | Las 5 tablas de esta versión, documentadas |
> | [6_contracts.md](6_contracts.md) | Contratos de integración |
> | [7_quickstart.md](7_quickstart.md) | Arranque y smoke test de v5 |
> | [8_tasks.md](8_tasks.md) | Orden de construcción |

---

## 1. Propósito de la v5

V1-V4 dejaron catálogos, estructura académica, personas y el
calendario de sesiones, pero **nadie podía todavía iniciar sesión** ni
**registrar su propia asistencia**. V5 resuelve dos problemas
distintos, con dos mecanismos que **no se mezclan**:

1. **Login de personal** (`usuario` + `rol_por_usuario`): un
   administrador o coordinador entra con usuario/clave (BCrypt) y tiene
   uno o más roles vigentes. Reemplaza el selector de rol de prueba que
   el front usa desde v1.
2. **Auto-registro de un asistente** (`enlace_registro` +
   `clave_acceso` + `registro_asistencia`): un asistente que llega a
   una sesión escanea un QR o abre un enlace, pide una clave de un solo
   uso (que le llegaría por correo — en este proyecto didáctico, la API
   la devuelve directamente, ver RNF11) y la usa para quedar registrado
   en esa sesión.

```
usuario (nueva) ──< rol_por_usuario >── rol (v1)
   │  1
   └──> asistente (v3, uq_usuario_persona: 1 usuario por persona, máximo)

sesion (v4) ──< enlace_registro ──< clave_acceso >── asistente (v3)
                     │                    │
                     └────────────────────┴──< registro_asistencia
                                                  (snapshots de vinculacion_asistente
                                                   y programa_asistente, v3; v1 tipo_vinculacion)
```

**Decisión de diseño central:** a diferencia de v2-v4, en v5 la API **no
escribe directamente** en `enlace_registro`, `clave_acceso` ni en la
mayoría de `registro_asistencia`: llama **funciones SQL que la BD ya
trae desde el script de v1** (`fn_emitir_enlace_api`,
`fn_solicitar_clave_api`, `fn_validar_clave_api`), que a su vez
invocan los `CALL` a sus respectivos `PROCEDURE`. La API no diseña
estas reglas (ventanas de tiempo, cupo, snapshots, intentos máximos,
expiración): **ya existen**, resuelven más de 15 reglas de negocio
numeradas (`RN-01` a `RN-38`) y la tarea de v5 es exponerlas por HTTP,
no reimplementarlas (Artículo 5).

## 2. Alcance

**Incluye:**
- `Usuario`: CRUD con clave hasheada en BCrypt (Guía IA, Regla 3 —
  primera vez que se aplica) y un endpoint de **login** (nuevo:
  primera vez que la API responde `401`).
- `RolPorUsuario`: tabla de vigencia con llave compuesta — mismo
  patrón D16 que v3 (`VinculacionAsistente`/`ProgramaAsistente`): sin
  `/{id}`, sin `PUT`, con "cerrar" en vez de `DELETE`.
- `EnlaceRegistro`: **no** es CRUD — son 2 verbos de negocio,
  "emitir" y "revocar", que llaman a `fn_emitir_enlace_api` (ver ADR
  D23).
- `ClaveAcceso`: **no** es CRUD — son 2 verbos de negocio,
  "solicitar" y "validar", que llaman a `fn_solicitar_clave_api` y
  `fn_validar_clave_api` respectivamente (ADR D23). Lectura (`GET`)
  solo para auditoría, sin exponer nunca el hash ni la clave en claro
  después de la respuesta de "solicitar".
- `RegistroAsistencia`: lectura completa + una vía de creación
  **manual** (`POST` directo, para un administrador que registra
  asistencia sin QR/enlace — p. ej. una lista en papel). La vía normal
  (QR/enlace) nace exclusivamente de `ClaveAcceso`/validar.
- Frontend: login real (reemplaza el selector de rol de prueba),
  pantalla pública de "pedir mi clave" / "validar mi clave" para el
  asistente, y paneles de administración para las 5 tablas.

**No incluye (es de otra versión):**
- `pregunta`, `opcion_pregunta`, `respuesta_encuesta`, `respuesta_item`
  (v6).
- `lote_migracion`, `detalle_migracion`, `bitacora`, `parametro` (v7) —
  aunque las 3 funciones SQL de esta versión **ya leen** `parametro`
  internamente (`VENTANA_MINUTOS_ANTES`, `CLAVE_VIGENCIA_MINUTOS`,
  `DOMINIOS_INSTITUCIONALES`, etc., con valores por defecto si la fila
  no existe); v5 no expone esa tabla ni la necesita exponer para que
  el flujo funcione — es un detalle interno de la BD (Artículo 5).
- **Autorización por rol dentro de la API** (middleware que bloquee un
  endpoint según el rol del usuario autenticado): fuera de alcance.
  v5 resuelve **autenticación** (quién es, con clave verificada) pero
  no **autorización** (qué puede hacer cada rol) — el front decide qué
  panel mostrar según el rol que el login le devuelve, la API sigue
  respondiendo todos sus endpoints sin exigir sesión (mismo nivel de
  seguridad didáctico que el resto del proyecto, documentado aquí para
  que no se lea como un descuido).
- Enviar el correo real con la clave de un solo uso: no hay paquete de
  correo en el proyecto (ni lo pide ninguna spec); la clave en claro
  se devuelve en la respuesta HTTP de "solicitar" (RNF11).

## 3. Entidades y requisitos funcionales

Numeración de RF continúa desde v4 (terminó en RF131).

### Usuario

> RF132-RF139. Cuenta de acceso de **personal** (no de asistentes en
> general): `uq_usuario_persona` garantiza **máximo un usuario por
> asistente**. `clave_hash` (BCrypt, Guía IA Regla 3) nunca sale de la
> API en ninguna respuesta.

- **RF132 — Listar** (`GET /api/Usuario`): 200; vacía → 204.
- **RF133 — Obtener por id** (`GET /api/Usuario/{idUsuario}`): 200;
  inexistente → 404.
- **RF134 — Crear** (`POST /api/Usuario` con **UsuarioCrear**):
  `fk_asistente`, `usuario` (`^[a-z0-9._-]{4,40}$`), `clave` (texto
  plano, mínimo 8 caracteres — validado en la petición, nunca se manda
  `clave_hash` desde el cliente) obligatorios; `activo` opcional, por
  defecto `true`. El **servicio** hashea `clave` con
  `BCrypt.Net.BCrypt.HashPassword` antes de que el repositorio la
  guarde (ADR D22) — ni el repositorio ni el `Controller` ven la clave
  en texto plano más que de paso. `usuario` o `fk_asistente`
  duplicados → 500.
- **RF135 — Reemplazar** (`PUT .../{idUsuario}` con
  **UsuarioReemplazo**): igual molde que siempre, incluida `clave`
  (un `PUT` reemplaza el recurso completo, así que también fija una
  clave nueva); inexistente → 404.
- **RF136 — Actualizar parcialmente** (`PATCH .../{idUsuario}` con
  **UsuarioActualizar**): todo opcional, incluida `clave`; si `clave`
  **no** viene en el body, el hash existente se conserva intacto (el
  servicio no lo toca); si viene, se re-hashea. Body vacío → 400;
  inexistente → 404.
- **RF137 — Eliminar** (`DELETE .../{idUsuario}`): `filasEliminadas`;
  inexistente → 404. `rol_por_usuario` es `CASCADE` (se borra el
  historial de roles de ESE usuario, junto con la cuenta misma —
  distinto de "cerrar" un rol vigente, que es borrar solo el futuro de
  una fila, no la cuenta entera); `bitacora`, `enlace_registro`
  (`fk_usuario_crea`), `lote_carga_asistente`, `lote_migracion` y
  `parametro` quedan con su referencia a este usuario en `NULL`
  (`SET NULL`, sin romper nada, aunque esas 4 tablas sean de otras
  versiones).
- **RF138 — `clave_hash` nunca se serializa:** ninguna respuesta de
  ningún verbo de `Usuario` (ni `GET`, ni la confirmación de `POST`)
  incluye ese campo — ver ADR D22.
- **RF139 — Login** (`POST /api/Usuario/login` con **UsuarioLogin**:
  `usuario`, `clave`): busca por el campo `usuario`, compara `clave`
  contra el hash con `BCrypt.Net.BCrypt.Verify`. Si no existe, está
  `activo: false`, o la clave no coincide → **401** (primer uso de
  este código en el proyecto — ver ADR D26). Si coincide: actualiza
  `ultimo_acceso = now()` y responde `200` con los datos del usuario
  (sin `clave_hash`) y sus roles **vigentes** (`rol_por_usuario` con
  `fecha_fin IS NULL`).

### RolPorUsuario

> RF140-RF142. Mismo patrón que `VinculacionAsistente`/
> `ProgramaAsistente` de v3 (ADR **D16**, reutilizado sin cambios):
> llave compuesta `(fk_usuario, fk_rol, fecha_ini)`, sin columna `id`,
> sin `PUT`, sin `DELETE` — se abre con `POST` y se cierra con
> `PATCH .../cerrar`.

- **RF140 — Listar** (`GET /api/RolPorUsuario?fkUsuario=`): sin el
  filtro, todas hasta el límite.
- **RF141 — Abrir** (`POST` con **RolPorUsuarioCrear**): `fk_usuario`,
  `fk_rol` obligatorios; `fecha_ini` opcional, por defecto hoy.
- **RF142 — Cerrar** (`PATCH /api/RolPorUsuario/cerrar` con
  **RolPorUsuarioCerrar**): body = las 3 columnas de la llave +
  `fecha_fin` (por defecto hoy). Combinación inexistente → 404; ya
  cerrada → 400 (mismo criterio que `VinculacionAsistente` en v3).

### EnlaceRegistro

> RF143-RF148. **No** es CRUD (ADR **D23**): la API no decide el
> `token`, la `ventana` ni el `canal` — los calcula
> `sp_emitir_enlace`/`fn_emitir_enlace_api`, que ya existen en la BD
> desde v1. El `canal` lo determina la `modalidad` de la `sesion`
> (`RN-13`, trigger `fn_trg_enlace_canal`): si la modalidad difunde por
> `AMBOS`, el enlace nace como `QR`; si no, copia el canal de la
> modalidad. Emitir un enlace nuevo para una sesión que ya tenía uno
> vigente **revoca el anterior automáticamente** (`RN-14`, dentro del
> mismo `sp_emitir_enlace` — no es un error, es el comportamiento
> documentado).

- **RF143 — Listar** (`GET /api/EnlaceRegistro?fkSesion=`): sin el
  filtro, todas hasta el límite.
- **RF144 — Obtener por id** (`GET /api/EnlaceRegistro/{idEnlace}`):
  200; inexistente → 404.
- **RF145 — Emitir** (`POST /api/EnlaceRegistro` con
  **EnlaceRegistroEmitir**): `fk_sesion` obligatorio; `minutosAntes`,
  `minutosDespues`, `fkUsuario` (quién lo emite, opcional — ver
  salvedad de FK circular ya cerrada, §4 de
  [0_mapa_versiones.md](../0_mapa_versiones.md)) opcionales. Llama
  `SELECT * FROM fn_emitir_enlace_api(...)` (ADR D23) y devuelve
  `id_enlace`; un `GET` posterior trae `token`, `url_publica`,
  `ruta_imagen_qr`, `canal` y `ventana` ya calculados por la BD. Una
  `fk_sesion` inexistente → 500 (la función hace su propio `RAISE
  EXCEPTION`, ver ADR **D24** sobre cómo se mapean estas excepciones).
- **RF146 — Revocar** (`PATCH /api/EnlaceRegistro/{idEnlace}/revocar`):
  pone `revocado_en = now()`; ya revocado → 400 (validado en el
  servicio: un enlace revocado dos veces no tiene efecto adicional,
  mismo criterio que "cerrar dos veces" en D16, aunque aquí SÍ exista
  un `id` — ver ADR **D21** sobre por qué de todos modos no hay
  `DELETE`); inexistente → 404.
- **RF147 — Sin `POST` plano de columnas sueltas, sin `PUT`/`PATCH`
  genérico, sin `DELETE`:** solo "emitir" y "revocar" — 2 verbos de
  negocio, no 6 de CRUD (ADR D23).
- **RF148 — El cliente nunca manda `canal`, `token`, `ventana` ni
  `url_publica`:** ninguno de los dos existe en **EnlaceRegistroEmitir**
  — son siempre calculados por la BD.

### ClaveAcceso

> RF149-RF153. Tampoco es CRUD (ADR D23): 2 verbos de negocio,
> "solicitar" y "validar", que llaman a funciones ya existentes desde
> v1. La clave en texto plano **solo existe una vez**, en la respuesta
> de "solicitar" (RNF11) — la BD únicamente guarda su `sha256` con el
> correo de destino como sal (`RN-34`, ya resuelto por la BD, no por la
> API).

- **RF149 — Solicitar una clave** (`POST /api/ClaveAcceso/solicitar`
  con **ClaveAccesoSolicitar**: `identificador` — código ASIS, correo
  o número de documento, resuelto por `fn_resolver_asistente`, que ya
  existe desde v1 —, `token` del enlace vigente, `ip` opcional):
  llama `SELECT * FROM fn_solicitar_clave_api(...)` y devuelve
  `{id_clave, clave, enviado_a}` — el único momento en que `clave`
  (texto plano) aparece. `identificador` no resuelto a ningún
  asistente (`RN-01`), o enlace inexistente/revocado/fuera de su
  ventana (`RN-15`) → **500** (D24: son excepciones de negocio de la
  BD, no un "no encontrado" de la API).
- **RF150 — Validar la clave y registrar asistencia**
  (`POST /api/ClaveAcceso/validar` con **ClaveAccesoValidar**:
  `idClave`, `clave` (texto plano que el usuario escribió), `ip` y
  `userAgent` opcionales): llama
  `SELECT * FROM fn_validar_clave_api(...)` y devuelve `id_registro`
  (el `registro_asistencia` recién creado). Clave incorrecta (`RN-?`,
  cuenta intentos y anula tras 3), ya usada (`RN-20`), expirada
  (`RN-18`), sesión sin cupo (`RN-38`), o el asistente sin vinculación
  vigente (`RN-07`/`RN-22`) → **500** (D24, igual criterio que RF149).
- **RF151 — Listar y obtener, solo para auditoría**
  (`GET /api/ClaveAcceso?fkAsistente=`, `GET/{id}`): nunca incluyen
  `clave_hash` (es `bytea`, ni siquiera tendría sentido mostrarlo) ni,
  por supuesto, la clave en texto plano (que la API nunca vuelve a
  tener después de RF149).
- **RF152 — Sin `POST` plano, `PUT`, `PATCH` genérico, `DELETE`:**
  "solicitar" y "validar" son los únicos 2 verbos de escritura (ADR
  D23) — crear o editar una `ClaveAcceso` fuera de ese flujo no tiene
  sentido de negocio.
- **RF153 — Intentos y expiración los vigila la BD, no la API:** la
  API no cuenta reintentos ni decide cuándo anular una clave — eso ya
  ocurre dentro de `sp_validar_clave_y_registrar` (D9, extendido a una
  regla de negocio de seguridad, mismo espíritu que D20 de v4 con
  `numero_reunion`).

### RegistroAsistencia

> RF154-RF158. Lectura completa + una vía de creación **manual**
> (origen `MANUAL`/`IMPORTADO`, para cuando un administrador registra
> asistencia sin QR/enlace). La vía normal nace de RF150, nunca de un
> `POST` directo.

- **RF154 — Listar** (`GET /api/RegistroAsistencia?fkSesion=&fkAsistente=`):
  ambos filtros opcionales y combinables.
- **RF155 — Obtener por id** (`GET .../{idRegistro}`): 200;
  inexistente → 404.
- **RF156 — Crear manual** (`POST /api/RegistroAsistencia` con
  **RegistroAsistenciaCrear**): `fk_sesion`, `fk_asistente`, `origen`
  obligatorios. `origen` **debe** ser `MANUAL` o `IMPORTADO` — un
  `origen: "QR"` o `"ENLACE"` aquí → **400** (`ArgumentException`,
  validado en el servicio — ver ADR **D25**: es la única regla de esta
  versión donde la API revalida algo que el `CHECK
  chk_registro_clave` de la BD técnicamente permitiría igual).
  `fk_clave`/`fk_enlace` **no** existen en esta petición (siempre
  `NULL` para un registro manual). `fk_vinculacion_snapshot` y
  `fk_programa_snapshot` tampoco se mandan: el mismo trigger
  `fn_trg_registro_validar` que usa el flujo de QR/enlace los calcula
  igual aquí (`RN-22`/`RN-07`) — si el asistente no tiene ninguna
  vinculación vigente, el `INSERT` falla con 500 sin que la API lo
  revalide antes (D9, sin extender esta vez: es la misma regla para
  ambas vías de creación).
- **RF157 — Sin `PUT`, `PATCH` ni `DELETE`:** un registro de
  asistencia es un hecho auditable (mismo espíritu que
  `ConsentimientoDatos` en v3) — se corrige con una fila de soporte en
  `bitacora` (v7), no editando ni borrando el hecho original.
- **RF158 — `RESTRICT` hacia `asistente`, `sesion`, `programa_academico`
  y `tipo_vinculacion`:** borrar cualquiera de esas cuatro filas
  mientras tenga algún `registro_asistencia` que las referencie (como
  instantánea) → 500 — ya anticipado parcialmente en v3/v4, ahora con
  un caso concreto y poblado (167 filas de ejemplo).

## 4. Requisitos no funcionales

Rigen los mismos RNF1-RNF10 de v1-v4, sin cambios. Se añaden:

- **RNF11 — La clave de un solo uso se devuelve en texto plano, UNA
  vez (RF149):** no hay servicio de correo en este proyecto didáctico;
  la API responde `clave` directamente en el `POST /solicitar` para
  que el front la muestre (simulando el envío) o la use de inmediato en
  una prueba. Fuera de esa respuesta, la clave en claro no se vuelve a
  mostrar ni se guarda en ningún lugar (la BD solo guarda su `sha256`).
- **RNF12 — BCrypt solo en el servicio (Guía IA, Regla 3):**
  `BCrypt.Net.BCrypt.HashPassword`/`Verify` se llaman únicamente desde
  `ServicioUsuario`; ni `UsuarioController` ni
  `RepositorioUsuarioPostgres` conocen la clave en texto plano más que
  de paso (parámetro de entrada, nunca persistido ni logueado).
- **RNF13 — Primer paquete NuGet nuevo del proyecto desde v1:**
  `BCrypt.Net-Next` se agrega a `ApiCatedras.csproj` — permitido
  explícitamente por la Guía IA (Regla 3), que es la única spec hasta
  ahora que invoca la excepción del Artículo 2 ("3 paquetes, salvo que
  una spec pida uno nuevo con una razón real").

## 5. Criterios de aceptación

1. `docker compose up -d --build` deja los 29 recursos (24 de v1-v4 +
   5 de v5) respondiendo en `/swagger` (`EnlaceRegistro` y
   `ClaveAcceso` sin los verbos CRUD habituales — ver
   [6_contracts.md](6_contracts.md)).
2. `POST /api/Usuario` crea una cuenta con `clave: "Secreta123"`; `GET
   /api/Usuario/{id}` y la respuesta del propio `POST` **no** incluyen
   `clave_hash` en ningún punto. `POST /api/Usuario/login` con esa
   misma clave → 200 con los roles vigentes; con una clave incorrecta
   → 401; con `activo: false` → 401.
3. `POST /api/RolPorUsuario` abre un rol; `PATCH
   /api/RolPorUsuario/cerrar` con la misma llave lo cierra; repetir →
   400.
4. `POST /api/EnlaceRegistro` sobre una sesión real (con modalidad
   `PRESENCIAL`, canal `QR`) → 200, y `GET /api/EnlaceRegistro/{id}`
   muestra `canal: "QR"` sin que el cliente lo haya mandado. Un
   segundo `POST` sobre la MISMA sesión → 200 con un `id_enlace`
   distinto, y el primero queda revocado (`GET` del primero muestra
   `revocado_en` no nulo) — comportamiento esperado (`RN-14`), no un
   bug.
5. `POST /api/ClaveAcceso/solicitar` con el `identificador` de un
   asistente real y el `token` del enlace del paso 4 → 200 con una
   `clave` de 6 dígitos en texto plano. `POST /api/ClaveAcceso/validar`
   con esa `clave` → 200 con un `id_registro`; `GET
   /api/RegistroAsistencia/{id_registro}` muestra `origen: "QR"` (o el
   canal real del enlace) y un `fk_vinculacion_snapshot` ya resuelto
   por la BD. Repetir el mismo `validar` con la misma clave → 500 (ya
   usada).
6. `POST /api/RegistroAsistencia` con `origen: "QR"` directo (sin pasar
   por `ClaveAcceso`) → 400, sin tocar la BD. El mismo body con
   `origen: "MANUAL"` → 200.
7. `DELETE /api/Asistente/{id}` sobre un asistente con
   `registro_asistencia` ya sembrado (167 filas de ejemplo en total) →
   500, sin que la cascada de v3 hacia sus 4 detalles llegue a
   ejecutarse.
8. **Prueba de capas ampliada:** `dotnet run --project pruebas` cubre
   las 29 entidades sin PostgreSQL, incluida la verificación de
   `BCrypt.Verify` con repositorios FALSOS (sin tocar la BD real para
   probar el hash) y el bloqueo de `origen: QR/ENLACE` en
   `RegistroAsistencia`.
9. El front reemplaza el selector de rol de prueba por un login real
   contra `POST /api/Usuario/login`; una pantalla pública permite a un
   asistente pedir su clave y validarla sin pasar por `/admin`.
10. El panel de administración (`/admin`) muestra las 5 tarjetas
    nuevas; `EnlaceRegistro` y `ClaveAcceso` ofrecen solo sus verbos de
    negocio ("emitir"/"revocar", "solicitar"/"validar"), no un
    formulario de edición genérico.
