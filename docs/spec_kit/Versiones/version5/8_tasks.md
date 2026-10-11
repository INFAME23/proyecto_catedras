# Tareas — Versión 5: usuarios, login y registro de asistencia

> ✅ **Revisada contra la BD real (2026-10-08), sin construir** — ver nota
> de [2_spec.md](2_spec.md). Plan, no registro de trabajo hecho: ninguna
> casilla está marcada todavía.
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

- [ ] `Modelos/Usuario.cs` — `ClaveHash` con `[JsonIgnore]` (D23);
      `IdUsuario`/`CreadoEn` sin `required`.
- [ ] `Peticiones/Usuario{Crear,Reemplazo,Actualizar}.cs` — `Usuario`
      con `[RegularExpression("^[a-z0-9._-]{4,40}$")]`; `Clave` con
      `[MinLength(8)]`, **sin** `[JsonIgnore]` aquí (es la petición, no
      el Modelo — el cliente SÍ manda la clave en texto plano al
      crear/reemplazar/actualizar); en `Actualizar`, `Clave` es
      `string?` (si no viene, el servicio no toca el hash existente).
- [ ] `Peticiones/UsuarioLogin.cs` — `Usuario`, `Clave` obligatorios.
- [ ] `Repositorios/IRepositorioUsuario.cs` +
      `RepositorioUsuarioPostgres.cs` — CRUD estándar +
      `ObtenerPorLoginAsync(string usuario)` +
      `ActualizarUltimoAccesoAsync(long idUsuario)`. **Nunca** llama a
      `BCrypt`: recibe/devuelve `ClaveHash` como cadena opaca.
- [ ] `Repositorios/IRepositorioRolPorUsuario.cs` (ver Fase 40) —
      inyectado en `ServicioUsuario` para `LoginAsync` (necesita los
      roles vigentes).
- [ ] `Servicios/IServicioUsuario.cs` + `ServicioUsuario.cs` —
      `CrearAsync`/`ReemplazarAsync`/`ActualizarAsync` llaman
      `BCrypt.Net.BCrypt.HashPassword` (único lugar del proyecto que lo
      hace, D23); `LoginAsync` llama `BCrypt.Net.BCrypt.Verify` y
      devuelve `null` si usuario inexistente, inactivo o clave
      incorrecta (sin distinguir cuál — D27), o
      `(Usuario, List<string> Roles)` si es válido.
- [ ] `Controllers/UsuarioController.cs` — 6 endpoints + `[HttpPost
      ("login")]`: si `ServicioUsuario.LoginAsync` devuelve `null` →
      `401 {estado: "no_autorizado", mensaje: "Usuario o clave
      incorrectos"}`; si no, `200` con el usuario (ya sin
      `ClaveHash`, por el `[JsonIgnore]` del Modelo) y los roles.
- [ ] `Program.cs` — registrar la pareja nueva.

**Verificación planeada** (contra PostgreSQL real): `POST
/api/Usuario` con `clave` en texto plano → `200`, y ni esa respuesta
ni un `GET` posterior incluyen ningún campo de clave; `POST
/api/Usuario/login` con la misma clave → `200` con roles; con una
clave incorrecta → `401`; con `activo: false` → `401`; `PATCH` sin
`clave` en el body → el hash anterior sigue siendo válido para el
login (confirmar volviendo a loguearse con la clave original).

## Fase 40 — La aplicación (capas de `RolPorUsuario`)
> RF140-RF142 de [2_spec.md](2_spec.md), ADR D16 (reusado de v3), §2.2
> de [5_data_model.md](5_data_model.md). Depende de la Fase 39
> (`fk_usuario`) y de `Rol` (v1, ya construida).

- [ ] `Modelos/RolPorUsuario.cs` — sin `Id` (D16).
- [ ] `Peticiones/RolPorUsuarioCrear.cs` / `RolPorUsuarioCerrar.cs` —
      mismo molde que `VinculacionAsistenteCrear`/`Cerrar` de v3.
- [ ] `Repositorios/IRepositorioRolPorUsuario.cs` +
      `RepositorioRolPorUsuarioPostgres.cs` — 3 métodos
      (`ObtenerTodosAsync`, `CrearAsync`, `CerrarAsync`) +
      `ObtenerVigentesAsync(long fkUsuario)` (devuelve
      `List<string>` de `fk_rol`, usado por `ServicioUsuario.LoginAsync`
      — Fase 39).
- [ ] `Servicios/IServicioRolPorUsuario.cs` +
      `ServicioRolPorUsuario.cs` — `CerrarAsync` valida existencia y
      que no esté ya cerrada (mismo criterio que v3).
- [ ] `Controllers/RolPorUsuarioController.cs` — 3 endpoints.
- [ ] `Program.cs` — registrar la pareja nueva.

**Verificación planeada**: mismo patrón de pruebas que
`VinculacionAsistente` en v3 (Fase 24), adaptado a `fk_usuario`/
`fk_rol`.

## Fase 41 — La aplicación (capas de `EnlaceRegistro`)
> RF143-RF148 de [2_spec.md](2_spec.md), ADR D22, D24, D25, §2.3 de
> [5_data_model.md](5_data_model.md). Depende de `Sesion` (v4) y,
> opcionalmente, de la Fase 39 (`fk_usuario_crea`).

- [ ] `Modelos/EnlaceRegistro.cs` — `VentanaDesde`/`VentanaHasta`
      como 2 propiedades planas (nota de mapeo en
      [5_data_model.md](5_data_model.md) §2.3); el resto sin
      `required` salvo `FkSesion` (todo lo demás lo calcula la BD).
- [ ] `Peticiones/EnlaceRegistroEmitir.cs` — **única** petición de
      esta entidad: `FkSesion` obligatorio; `MinutosAntes`,
      `MinutosDespues`, `FkUsuario` opcionales.
- [ ] `Repositorios/IRepositorioEnlaceRegistro.cs` +
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
- [ ] `Servicios/IServicioEnlaceRegistro.cs` +
      `ServicioEnlaceRegistro.cs` — `RevocarAsync` primero confirma
      que el enlace existe (404 si no) y que `revocado_en` es `null`
      (400 si no) antes de llamar al repositorio.
- [ ] `Controllers/EnlaceRegistroController.cs` — `GET` lista,
      `GET/{id}`, `[HttpPost]` (= emitir),
      `[HttpPatch("{id}/revocar")]`. Sin `PUT`/`PATCH` genérico ni
      `DELETE`.
- [ ] `Program.cs` — registrar la pareja nueva.

**Verificación planeada** (contra PostgreSQL real): `POST
/api/EnlaceRegistro` → 200 con `canal` decidido por la BD; un segundo
`POST` sobre la misma sesión → 200 con un `id_enlace` nuevo, y el
primero queda revocado (`RN-14`); `PATCH .../revocar` → 200; repetir →
400; `fk_sesion` inexistente → 500.

## Fase 42 — La aplicación (capas de `ClaveAcceso` y `RegistroAsistencia`)
> RF149-RF158 de [2_spec.md](2_spec.md), ADR D24, D25, D26, §2.4-2.5
> de [5_data_model.md](5_data_model.md). Depende de la Fase 41
> (`fk_enlace`) y de `Asistente` (v3). Se construyen juntas porque el
> flujo de `ClaveAcceso`/validar es, en la práctica, quien crea la
> mayoría de los `RegistroAsistencia` — separar sus pruebas sin que
> ambas existan sería probar solo la mitad del flujo.

- [ ] `Modelos/ClaveAcceso.cs` — `ClaveHash` (`byte[]`) con
      `[JsonIgnore]`.
- [ ] `Modelos/RegistroAsistencia.cs` — `FkVinculacionSnapshot`,
      `FkProgramaSnapshot` sin `required` (los pone el trigger, nunca
      el cliente).
- [ ] `Peticiones/ClaveAccesoSolicitar.cs` / `ClaveAccesoValidar.cs` —
      **únicas** peticiones de esta entidad.
- [ ] `Peticiones/RegistroAsistenciaCrear.cs` — `Origen` obligatorio
      (`[Required]`), **sin** `[RegularExpression]`: la lista cerrada
      `{MANUAL, IMPORTADO}` la valida **solo el servicio** (D26) para que
      un `origen: "QR"` responda **400** como piden RF156 y el criterio 6.
      Con la anotación respondería 422 y el servicio nunca se enteraría.
- [ ] `Repositorios/IRepositorioClaveAcceso.cs` +
      `RepositorioClaveAccesoPostgres.cs` — `ObtenerTodosAsync`,
      `ObtenerPorIdAsync`, `SolicitarAsync`, `ValidarAsync` (código
      exacto de ambas en §4.3 de [3_plan.md](3_plan.md)).
- [ ] `Repositorios/IRepositorioRegistroAsistencia.cs` +
      `RepositorioRegistroAsistenciaPostgres.cs` — `ObtenerTodosAsync`
      (con `fkSesion`/`fkAsistente` opcionales), `ObtenerPorIdAsync`,
      `CrearAsync` (`INSERT` directo, sin `fk_clave`/`fk_enlace`,
      dejando que el trigger llene los 2 snapshots).
- [ ] `Servicios/IServicioClaveAcceso.cs` + `ServicioClaveAcceso.cs`.
- [ ] `Servicios/IServicioRegistroAsistencia.cs` +
      `ServicioRegistroAsistencia.cs` — `CrearAsync` valida `origen`
      (D26) ANTES de llamar al repositorio.
- [ ] `Controllers/ClaveAccesoController.cs` — `GET` lista,
      `GET/{id}`, `[HttpPost("solicitar")]`, `[HttpPost("validar")]`.
- [ ] `Controllers/RegistroAsistenciaController.cs` — `GET` lista,
      `GET/{id}`, `[HttpPost]`. Sin `PUT`/`PATCH`/`DELETE`.
- [ ] `Program.cs` — registrar las 2 parejas nuevas.

**Verificación planeada** (contra PostgreSQL real, requiere una
`Sesion` y un `EnlaceRegistro` cuya ventana cubra el momento de la
prueba — Fases 32/41): `POST /api/ClaveAcceso/solicitar` → 200 con
`clave` en texto plano; `POST /api/ClaveAcceso/validar` con esa clave
→ 200 con `id_registro`; repetir → 500 (ya usada); `POST
/api/RegistroAsistencia` con `origen: "QR"` → 400; con `"MANUAL"` →
200.

## Fase 43 — Prueba de capas ampliada (criterio de aceptación 8)
> Depende de las Fases 39-42 (las 5 entidades deben existir).

- [ ] Agregar a `pruebas/Programa.cs` un repositorio FALSO y sus
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

**Verificación planeada**: `dotnet run --project pruebas` → cubre las
**29** entidades, código de salida 0.

## Fase 44 — Verificación de integridad referencial y reglas de negocio (criterios de aceptación 2-7)
> Depende de las Fases 39-42. Como en versiones anteriores, es la fase
> dedicada a probar lo que la BD (y sus funciones) hacen solas.

- [ ] Ejecutar, contra Docker + PostgreSQL reales y EN ESTE ORDEN:
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
- [ ] `docker compose down -v && docker compose up -d --build` —
      revertir los efectos de arriba.

**Verificación planeada**: los 6 resultados coinciden con lo
documentado en [5_data_model.md](5_data_model.md) y con los criterios
de aceptación 2-7 de [2_spec.md](2_spec.md).

## Fase 45 — Frontend: login real y autorregistro público
> Criterios 9-10 de [2_spec.md](2_spec.md), §5 de [3_plan.md](3_plan.md).
> Depende de las Fases 39-42 (API completa).

- [ ] `routes/auth.py` + `templates/login.html` — reemplaza el
      placeholder «en construcción» (v1-v4); guarda usuario y roles en
      la sesión de Flask; agrega `/logout`.
- [ ] `auth_helpers.py` (v2) — deja de devolver siempre "sin sesión";
      lee la sesión real y agrega `rol_requerido('ADMIN')`.
- [ ] `routes/admin.py` — `before_request` en `admin_bp` que exige sesión
      con rol `ADMIN` (sin sesión → `/login`; sin rol → 403).
- [ ] `templates/partials/navbar.html` — muestra el usuario y «Cerrar
      sesión»; el enlace «Administración» solo con rol `ADMIN`.
- [ ] `routes/registro.py` + `templates/registro_solicitar.html` y
      `templates/registro_validar.html` — completa el placeholder (ya
      registrado en `app.py`): `/registro`, `/registro/<token>` y
      `/registro/<token>/validar`, sin pasar por `/admin`.
- [ ] `routes/admin.py` — 5 bloques nuevos; `EnlaceRegistro` con
      "emitir"/"revocar"; `ClaveAcceso` de solo lectura.

**Verificación planeada**: login real funciona de punta a punta contra
Docker/PostgreSQL reales (fijando antes la clave de `admin.catedras`,
paso 2 de [7_quickstart.md](7_quickstart.md)); `/admin` sin sesión
redirige a `/login`; la pantalla pública de autorregistro permite a un
asistente de ejemplo pedir y validar su clave sin ninguna sesión de
administrador abierta.

## Fase 46 — Cierre de la v5
> Última fase. Solo entonces se escribe la spec de la v6 (Artículo 1).

- [ ] Confirmar que los 10 criterios de aceptación de
      [2_spec.md](2_spec.md) §5 pasan, en orden, sobre un entorno
      recién reseteado.
- [ ] `dotnet run --project pruebas` → anotar el total final de
      verificaciones pasadas (Fase 43).
- [ ] Revisar este spec kit completo (los 7 documentos) contra lo que
      realmente quedó construido — cambiar el banner «sin construir» por
      «construida».
- [ ] Commit y tag: `git tag v5`.
