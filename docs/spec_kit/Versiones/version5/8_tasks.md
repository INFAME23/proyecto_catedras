# Tareas — Versión 5: usuarios, login y registro de asistencia

> ✅ **Construida el 2026-10-11** — ver nota de [2_spec.md](2_spec.md).
> Las casillas marcadas `[x]` son trabajo hecho; cada fase dice además
> **qué se verificó y dónde**. Todo se corrió en Docker (PostgreSQL 16 +
> API + front) el 2026-10-11.
>
> Guía para hacerlo con un asistente de IA: [guia_ia_v5.md](guia_ia_v5.md).
>
> Continúa la numeración de fases de
> [version4/8_tasks.md](../version4/8_tasks.md) (termina en Fase 37).
>
> **Nota de alcance (v5):** sin migraciones SQL nuevas (igual que v4);
> sí hay un paquete NuGet nuevo (`BCrypt.Net-Next`, Fase 38). La BD ya
> tiene las 5 tablas de esta versión (y sus datos de ejemplo) desde el
> script de la v1 (Artículo 5).

---

## Fase 38 — Preparación: paquete NuGet y utilidades comunes
> RNF13-RNF14 de [2_spec.md](2_spec.md). Va primero porque las Fases
> 39 y 41-42 dependen de que `BCrypt.Net-Next` ya esté referenciado.

- [x] `ApiCatedras.csproj` — agregar
      `<PackageReference Include="BCrypt.Net-Next" Version="4.0.3" />`.
- [x] Confirmar que `dotnet restore` resuelve el paquete sin tocar
      ningún otro `PackageReference` existente (Artículo 2: no se
      agrega nada más "de paso").

**Verificación planeada**: `dotnet build` compila sin advertencias
nuevas; un `using BCrypt.Net;` de prueba en un archivo temporal
resuelve `BCrypt.HashPassword`/`Verify` (se borra el archivo temporal
antes de seguir).

**Verificado (2026-10-11, en la máquina del autor):** `dotnet restore` y
`dotnet build` en `api_catedras` terminan con `Build succeeded`. **No se
hizo** la prueba del `using BCrypt.Net;` temporal: el paquete se usará de
verdad en la Fase 39, donde un fallo de resolución saltaría de inmediato.

## Fase 39 — La aplicación (capas de `Usuario`)
> RF132-RF139 de [2_spec.md](2_spec.md), ADR D23, D27, §2.1 de
> [5_data_model.md](5_data_model.md). Depende de la Fase 38 y de
> `Asistente` (v3, ya construida).

- [x] `Modelos/Usuario.cs` — `ClaveHash` con `[JsonIgnore]` (D23);
      `IdUsuario`/`CreadoEn` sin `required`.
- [x] `Peticiones/Usuario{Crear,Reemplazo,Actualizar}.cs` — `Usuario`
      con `[RegularExpression("^[a-z0-9._-]{4,40}$")]`; `Clave` con
      `[MinLength(8)]`, **sin** `[JsonIgnore]` aquí (es la petición, no
      el Modelo — el cliente SÍ manda la clave en texto plano al
      crear/reemplazar/actualizar); en `Actualizar`, `Clave` es
      `string?` (si no viene, el servicio no toca el hash existente).
- [x] `Peticiones/UsuarioLogin.cs` — `Usuario`, `Clave` obligatorios.
- [x] `Repositorios/IRepositorioUsuario.cs` +
      `RepositorioUsuarioPostgres.cs` — CRUD estándar +
      `ObtenerPorLoginAsync(string usuario)` +
      `ActualizarUltimoAccesoAsync(long idUsuario)`. **Nunca** llama a
      `BCrypt`: recibe/devuelve `ClaveHash` como cadena opaca.
- [x] `Repositorios/IRepositorioRolPorUsuario.cs` (ver Fase 40) —
      inyectado en `ServicioUsuario` para `LoginAsync` (necesita los
      roles vigentes).
- [x] `Servicios/IServicioUsuario.cs` + `ServicioUsuario.cs` —
      `CrearAsync`/`ReemplazarAsync`/`ActualizarAsync` llaman
      `BCrypt.Net.BCrypt.HashPassword` (único lugar del proyecto que lo
      hace, D23); `LoginAsync` llama `BCrypt.Net.BCrypt.Verify` y
      devuelve `null` si usuario inexistente, inactivo o clave
      incorrecta (sin distinguir cuál — D27), o
      `(Usuario, List<string> Roles)` si es válido.
- [x] `Controllers/UsuarioController.cs` — 6 endpoints + `[HttpPost
      ("login")]`: si `ServicioUsuario.LoginAsync` devuelve `null` →
      `401 {estado: "no_autorizado", mensaje: "Usuario o clave
      incorrectos"}`; si no, `200` con el usuario (ya sin
      `ClaveHash`, por el `[JsonIgnore]` del Modelo) y los roles.
- [x] `Program.cs` — registrar la pareja nueva.

**Verificación planeada** (contra PostgreSQL real): `POST
/api/Usuario` con `clave` en texto plano → `200`, y ni esa respuesta
ni un `GET` posterior incluyen ningún campo de clave; `POST
/api/Usuario/login` con la misma clave → `200` con roles; con una
clave incorrecta → `401`; con `activo: false` → `401`; `PATCH` sin
`clave` en el body → el hash anterior sigue siendo válido para el
login (confirmar volviendo a loguearse con la clave original).

**Verificado (2026-10-11, Docker):** pasos 1-2 de `verificar_v5.ps1`: `POST
/api/Usuario` → 200 sin ningún campo de clave en la respuesta ni en el
`GET`; login con clave correcta → 200 con roles, incorrecta → 401,
usuario inexistente → 401 y cuenta inactiva → 401, los tres con el
mismo mensaje (D27). **No se repitió por HTTP** la comprobación de que un
`PATCH` sin `clave` conserva el hash anterior.

## Fase 40 — La aplicación (capas de `RolPorUsuario`)
> RF140-RF142 de [2_spec.md](2_spec.md), ADR D16 (reusado de v3), §2.2
> de [5_data_model.md](5_data_model.md). Depende de la Fase 39
> (`fk_usuario`) y de `Rol` (v1, ya construida).

- [x] `Modelos/RolPorUsuario.cs` — sin `Id` (D16).
- [x] `Peticiones/RolPorUsuarioCrear.cs` / `RolPorUsuarioCerrar.cs` —
      mismo molde que `VinculacionAsistenteCrear`/`Cerrar` de v3.
- [x] `Repositorios/IRepositorioRolPorUsuario.cs` +
      `RepositorioRolPorUsuarioPostgres.cs` — 3 métodos
      (`ObtenerTodosAsync`, `CrearAsync`, `CerrarAsync`) +
      `ObtenerVigentesAsync(long fkUsuario)` (devuelve
      `List<string>` de `fk_rol`, usado por `ServicioUsuario.LoginAsync`
      — Fase 39).
- [x] `Servicios/IServicioRolPorUsuario.cs` +
      `ServicioRolPorUsuario.cs` — `CerrarAsync` valida existencia y
      que no esté ya cerrada (mismo criterio que v3).
- [x] `Controllers/RolPorUsuarioController.cs` — 3 endpoints.
- [x] `Program.cs` — registrar la pareja nueva.

**Verificación planeada**: mismo patrón de pruebas que
`VinculacionAsistente` en v3 (Fase 24), adaptado a `fk_usuario`/
`fk_rol`.

**Verificado (2026-10-11, Docker):** paso 3 de `verificar_v5.ps1`: abrir un rol
→ 200, cerrarlo → 200, repetir el cierre → 400, cerrar una llave que no
existe → 404. Además, a mano en el front: asignar `CONSULTA` a un usuario
nuevo desde Administración › Rol por usuario.

## Fase 41 — La aplicación (capas de `EnlaceRegistro`)
> RF143-RF148 de [2_spec.md](2_spec.md), ADR D22, D24, D25, §2.3 de
> [5_data_model.md](5_data_model.md). Depende de `Sesion` (v4) y,
> opcionalmente, de la Fase 39 (`fk_usuario_crea`).

- [x] `Modelos/EnlaceRegistro.cs` — `VentanaDesde`/`VentanaHasta`
      como 2 propiedades planas (nota de mapeo en
      [5_data_model.md](5_data_model.md) §2.3); el resto sin
      `required` salvo `FkSesion` (todo lo demás lo calcula la BD).
- [x] `Peticiones/EnlaceRegistroEmitir.cs` — **única** petición de
      esta entidad: `FkSesion` obligatorio; `MinutosAntes`,
      `MinutosDespues`, `FkUsuario` opcionales.
- [x] `Repositorios/IRepositorioEnlaceRegistro.cs` +
      `RepositorioEnlaceRegistroPostgres.cs` — `ObtenerTodosAsync`,
      `ObtenerPorIdAsync` (con `SELECT lower(ventana) AS
      ventana_desde, upper(ventana) AS ventana_hasta, ...`),
      `EmitirAsync` (`SELECT * FROM fn_emitir_enlace_api(...)`, código
      exacto en §4.3 de [3_plan.md](3_plan.md)), `RevocarAsync`
      (`UPDATE enlace_registro SET revocado_en = now() WHERE id_enlace
      = @Id AND revocado_en IS NULL` — el `AND revocado_en IS NULL`
      hace que un segundo intento afecte 0 filas, que el servicio
      traduce a 400, no a un `UPDATE` que "tuvo éxito" sin cambiar
      nada).
- [x] `Servicios/IServicioEnlaceRegistro.cs` +
      `ServicioEnlaceRegistro.cs` — `RevocarAsync` primero confirma
      que el enlace existe (404 si no) y que `revocado_en` es `null`
      (400 si no) antes de llamar al repositorio.
- [x] `Controllers/EnlaceRegistroController.cs` — `GET` lista,
      `GET/{id}`, `[HttpPost]` (= emitir),
      `[HttpPatch("{id}/revocar")]`. Sin `PUT`/`PATCH` genérico ni
      `DELETE`.
- [x] `Program.cs` — registrar la pareja nueva.

**Verificación planeada** (contra PostgreSQL real): `POST
/api/EnlaceRegistro` → 200 con `canal` decidido por la BD; un segundo
`POST` sobre la misma sesión → 200 con un `id_enlace` nuevo, y el
primero queda revocado (`RN-14`); `PATCH .../revocar` → 200; repetir →
400; `fk_sesion` inexistente → 500.

**Verificado (2026-10-11, Docker):** paso 4 de `verificar_v5.ps1` (sesión nueva
alrededor de «ahora» + dos enlaces: el primero queda revocado, `canal` lo
decide la BD, `fk_sesion` inexistente → 500, sin `fk_sesion` → 422,
revocar → 200, repetir → 400, inexistente → 404).

## Fase 42 — La aplicación (capas de `ClaveAcceso` y `RegistroAsistencia`)
> RF149-RF158 de [2_spec.md](2_spec.md), ADR D24, D25, D26, §2.4-2.5
> de [5_data_model.md](5_data_model.md). Depende de la Fase 41
> (`fk_enlace`) y de `Asistente` (v3). Se construyen juntas porque el
> flujo de `ClaveAcceso`/validar es, en la práctica, quien crea la
> mayoría de los `RegistroAsistencia` — separar sus pruebas sin que
> ambas existan sería probar solo la mitad del flujo.

- [x] `Modelos/ClaveAcceso.cs` — `ClaveHash` (`byte[]`) con
      `[JsonIgnore]`.
- [x] `Modelos/RegistroAsistencia.cs` — `FkVinculacionSnapshot`,
      `FkProgramaSnapshot` sin `required` (los pone el trigger, nunca
      el cliente).
- [x] `Peticiones/ClaveAccesoSolicitar.cs` / `ClaveAccesoValidar.cs` —
      **únicas** peticiones de esta entidad.
- [x] `Peticiones/RegistroAsistenciaCrear.cs` — `Origen` obligatorio
      (`[Required]`), **sin** `[RegularExpression]`: la lista cerrada
      `{MANUAL, IMPORTADO}` la valida **solo el servicio** (D26) para que
      un `origen: "QR"` responda **400** como piden RF156 y el criterio 6.
      Con la anotación respondería 422 y el servicio nunca se enteraría.
- [x] `Repositorios/IRepositorioClaveAcceso.cs` +
      `RepositorioClaveAccesoPostgres.cs` — `ObtenerTodosAsync`,
      `ObtenerPorIdAsync`, `SolicitarAsync`, `ValidarAsync` (código
      exacto de ambas en §4.3 de [3_plan.md](3_plan.md)).
- [x] `Repositorios/IRepositorioRegistroAsistencia.cs` +
      `RepositorioRegistroAsistenciaPostgres.cs` — `ObtenerTodosAsync`
      (con `fkSesion`/`fkAsistente` opcionales), `ObtenerPorIdAsync`,
      `CrearAsync` (`INSERT` directo, sin `fk_clave`/`fk_enlace`,
      dejando que el trigger llene los 2 snapshots).
- [x] `Servicios/IServicioClaveAcceso.cs` + `ServicioClaveAcceso.cs`.
- [x] `Servicios/IServicioRegistroAsistencia.cs` +
      `ServicioRegistroAsistencia.cs` — `CrearAsync` valida `origen`
      (D26) ANTES de llamar al repositorio.
- [x] `Controllers/ClaveAccesoController.cs` — `GET` lista,
      `GET/{id}`, `[HttpPost("solicitar")]`, `[HttpPost("validar")]`.
- [x] `Controllers/RegistroAsistenciaController.cs` — `GET` lista,
      `GET/{id}`, `[HttpPost]`. Sin `PUT`/`PATCH`/`DELETE`.
- [x] `Program.cs` — registrar las 2 parejas nuevas.

**Verificación planeada** (contra PostgreSQL real, requiere una
`Sesion` y un `EnlaceRegistro` cuya ventana cubra el momento de la
prueba — Fases 32/41): `POST /api/ClaveAcceso/solicitar` → 200 con
`clave` en texto plano; `POST /api/ClaveAcceso/validar` con esa clave
→ 200 con `id_registro`; repetir → 500 (ya usada); `POST
/api/RegistroAsistencia` con `origen: "QR"` → 400; con `"MANUAL"` →
200.

**Verificado (2026-10-11, Docker):** pasos 5 y 7 de `verificar_v5.ps1` (solicitar
con token mal formado → 400, IP inválida → 400, identificador sin
persona → 500; solicitar y validar → 200 con `origen: "QR"`; repetir →
500; `origen` inválido o ausente → 400; `MANUAL` → 200; el mismo
asistente dos veces en la sesión → 500).

## Fase 43 — Prueba de capas ampliada (criterio de aceptación 8)
> Depende de las Fases 39-42 (las 5 entidades deben existir).

- [x] Agregar a `pruebas/Programa.cs` un repositorio FALSO y sus
      verificaciones para las 5 entidades. Puntos especiales:
      - `RepositorioUsuarioFalso` guarda el hash tal cual
        `ServicioUsuario` lo calcule — la prueba verifica
        `BCrypt.Verify(clave, hashGuardado)` directamente, sin
        PostgreSQL, confirmando que el hash real de BCrypt (no un
        valor simulado) quedó donde debía.
      - Un caso de prueba de login con usuario inexistente, otro con
        clave incorrecta y otro con `activo: false` — los 3 deben
        devolver el mismo resultado (`null` desde el servicio, 401
        desde el controlador), confirmando D27.
      - `RepositorioEnlaceRegistroFalso`/`RepositorioClaveAccesoFalso`
        simulan lo que las funciones SQL devolverían (un `id`, una
        `clave` fija para la prueba), sin necesidad de levantar
        PostgreSQL para probar el flujo completo de capas.
      - Un caso de prueba de `RegistroAsistenciaCrear` con
        `origen: "ENLACE"` confirma el 400 ANTES de que el repositorio
        falso sea invocado (igual criterio que el `CerrarAsync` ya
        cerrado de v3/v4).

**Verificado (2026-10-11):** `docker compose exec api dotnet run --project
pruebas` → **345 pasadas, 0 fallidas**, las 29 entidades, código de salida
0 (incluye `BCrypt.Verify` real sobre el hash guardado por el repositorio
falso y el 400 de `origen` antes de llegar al repositorio).

## Fase 44 — Verificación de integridad referencial y reglas de negocio (criterios de aceptación 2-7)
> Depende de las Fases 39-42. Como en versiones anteriores, es la fase
> dedicada a probar lo que la BD (y sus funciones) hacen solas.

- [x] Ejecutar, contra Docker + PostgreSQL reales y EN ESTE ORDEN:
  1. Login con clave correcta, incorrecta y usuario inactivo → 200,
     401, 401 respectivamente.
  2. Abrir y cerrar un `RolPorUsuario`; repetir el cierre → 400.
  3. Emitir 2 enlaces para la misma sesión; confirmar que el primero
     queda revocado.
  4. Solicitar y validar una clave; repetir la validación → 500.
  5. `POST /api/RegistroAsistencia` con `origen` inválido → 400; con
     `MANUAL` → 200.
  6. `DELETE /api/Asistente/{id con registro_asistencia}` → 500.
  7. Límite conocido (RF153): con una clave pedida y SIN usar, 4 `validar`
     con clave incorrecta → 500 cada uno, y `GET /api/ClaveAcceso/{id}` →
     `intentos: 0`; la clave correcta todavía se acepta después. Si
     `intentos` sí subiera, revertir el punto 4 de «Ajustes al borrador»
     en [4_research.md](4_research.md).
- [x] `docker compose down -v && docker compose up -d --build` —
      revertir los efectos de arriba.

**Verificado (2026-10-11):** `api_catedras/pruebas/verificar_v5.ps1`
automatiza los 9 pasos (0-8) contra Docker real: **62 pasadas, 0
fallidas**. Comando, desde la raíz:
`powershell -ExecutionPolicy Bypass -File .\api_catedras\pruebas\verificar_v5.ps1`
(usar `127.0.0.1`, no `localhost`; el script escribe datos, así que se
repite solo tras `docker compose down -v`). RF153 confirmado: tras 4
`validar` con clave incorrecta, `intentos` sigue en 0 y la clave
correcta todavía se acepta, así que el punto 4 de «Ajustes al borrador»
de [4_research.md](4_research.md) se mantiene. `DELETE /api/Asistente/1`
→ 500 por `registro_asistencia` (criterio 7).

## Fase 45 — Frontend: login real y autorregistro público
> Criterios 9-10 de [2_spec.md](2_spec.md), §5 de [3_plan.md](3_plan.md).
> Depende de las Fases 39-42 (API completa).

- [x] `routes/auth.py` + `templates/login.html` — reemplaza el
      placeholder «en construcción» (v1-v4); guarda usuario y roles en
      la sesión de Flask; agrega `/logout`.
- [x] `auth_helpers.py` (v2) — deja de devolver siempre "sin sesión";
      lee la sesión real y agrega `rol_requerido('ADMIN')`.
- [x] `routes/admin.py` — `before_request` en `admin_bp` que exige sesión
      con rol `ADMIN` (sin sesión → `/login`; sin rol → 403).
- [x] `templates/partials/navbar.html` — muestra el usuario y «Cerrar
      sesión»; el enlace «Administración» solo con rol `ADMIN`.
- [x] `routes/registro.py` + `templates/registro_inicio.html`,
      `registro_solicitar.html` y `registro_validar.html` — completa el
      placeholder (ya registrado en `app.py`): `/registro`,
      `/registro/<token>` y `/registro/<token>/validar`, sin pasar por
      `/admin`.
- [x] `routes/admin.py` — 5 bloques nuevos; `EnlaceRegistro` con
      "emitir"/"revocar"; `ClaveAcceso` de solo lectura.

**Verificación planeada**: login real funciona de punta a punta contra
Docker/PostgreSQL reales (fijando antes la clave de `admin.catedras`,
paso 2 de [7_quickstart.md](7_quickstart.md)); `/admin` sin sesión
redirige a `/login`; la pantalla pública de autorregistro permite a un
asistente de ejemplo pedir y validar su clave sin ninguna sesión de
administrador abierta.

**Verificado (2026-10-11, Docker, a mano en el navegador):** `/admin/` sin
sesión → `/login?siguiente=/admin/`; clave incorrecta → «Usuario o clave
incorrectos.»; `admin.catedras` entra y ve las 5 tarjetas nuevas; se
emitió un enlace para una sesión creada alrededor de «ahora»; en otra
ventana, sin sesión, `/registro/<token>` pidió el documento `1000090`,
mostró la clave de 6 dígitos, la clave incorrecta se rechazó con su motivo
y la correcta confirmó «¡Asistencia registrada!» (el registro apareció en
Administración con origen `QR`); un usuario creado desde la pantalla con
solo el rol `CONSULTA` inicia sesión, no ve «Administración» y recibe 403
en `/admin/`. Además, 58 comprobaciones automáticas de las rutas Flask
contra una API simulada. **No se repitió** el apagado de la API (`docker
compose stop api`) para ver el mensaje de error en cada pantalla nueva.

**Regla 2 (guía IA) en las pantallas nuevas:** `fk_asistente` de una
cuenta y de un registro manual se busca (`?q=`) y se elige de un
`<select>`; `fk_sesion` de enlaces y registros lo fija la URL (se entra
desde el botón de la lista de sesiones); `fk_usuario` y `fk_rol` salen de
`<select>`. Los filtros de las listas sí aceptan un id escrito (es un
filtro de búsqueda, no un campo que se guarde).

**Trampa de front:** `main.js` borra todo `.alert` a los 5 s; la clave de
un solo uso se muestra en un recuadro que **no** es `.alert`.

## Fase 46 — Cierre de la v5
> Última fase. Solo entonces se escribe la spec de la v6 (Artículo 1).

- [x] Confirmar que los 10 criterios de aceptación de
      [2_spec.md](2_spec.md) §5 pasan, en orden, sobre un entorno
      recién reseteado. *(2026-10-11: criterio 1 en `/swagger`; 2-7 por HTTP
      con `verificar_v5.ps1`; 8 con la prueba de capas; 9-10 en el
      front.)*
- [x] `dotnet run --project pruebas` → anotar el total final de
      verificaciones pasadas (Fase 43). ***345 pasadas, 0 fallidas.***
- [x] Revisar este spec kit completo (los 7 documentos) contra lo que
      realmente quedó construido — cambiar el banner «sin construir» por
      «construida». *(Corregido además: `6_contracts.md` §9 (el front sí
      está en `docker-compose.yml`) y el fragmento de `ServicioUsuario` de
      `3_plan.md` §4.1.)*
- [x] Commit y tag: `git tag v5`.
