# Contratos de integración — Versión 5: usuarios, login y registro de asistencia

> ✅ **Revisada contra la BD real (2026-10-08), sin construir** — ver nota de [2_spec.md](2_spec.md).
>
> **Documento 6 de 8** del spec kit. Los endpoints HTTP (verbos,
> formatos exactos) están en [2_spec.md](2_spec.md) (RF132-RF158) y se
> navegan en vivo desde `/swagger`.
>
> **Nota de alcance (v5):** la v5 **no cambia ningún contrato de
> infraestructura** de v1-v4 — mismo mapa de puertos, mismas
> variables de entorno, mismos dos servicios, mismo healthcheck, sin
> montajes nuevos (como v4). Lo que crece es, otra vez, solo el
> **contrato de la API** (§3) — pero esta vez con un cambio real en
> `ApiCatedras.csproj` (§3.1) y, por primera vez, un código HTTP nuevo
> (`401`).

---

## 1. Mapa de puertos

Sin cambios: [version1/6_contracts.md](../version1/6_contracts.md) §1.

## 2. Variables de entorno

Sin cambios: [version1/6_contracts.md](../version1/6_contracts.md) §2.
Ninguna clave de BCrypt, secreto de sesión ni configuración nueva: el
coste de `BCrypt.HashPassword` usa el valor por defecto de la
librería, sin parámetro configurable en esta versión.

## 3. Contrato de la API (lo que crece en v5)

### 3.1 `ApiCatedras.csproj` — primer paquete nuevo desde v1

```xml
<PackageReference Include="BCrypt.Net-Next" Version="4.0.3" />
```
Permitido explícitamente por la Guía IA, Regla 3 (RNF14 de
[2_spec.md](2_spec.md)) — la única excepción al límite de 3 paquetes
del Artículo 2 que el proyecto ha usado hasta ahora.

### 3.2 `Usuario` — 6 verbos + login

- CRUD estándar (`GET/POST /api/Usuario`, `GET/PUT/PATCH/DELETE
  /api/Usuario/{idUsuario}`). `clave_hash` **nunca** aparece en
  ninguna respuesta (D23); `POST`/`PUT` reciben `clave` en texto plano
  (mínimo 8 caracteres, 422 si no cumple) y la API la hashea antes de
  guardar — el cliente nunca manda ni lee un hash.
- **`POST /api/Usuario/login`** (`UsuarioLogin`): `{"usuario": "...",
  "clave": "..."}`. Éxito → `200
  {estado, usuario: {...sin clave_hash...}, roles: ["ADMIN", ...]}`.
  Fallo (usuario inexistente, inactivo, o clave incorrecta) → **401**
  `{estado: "no_autorizado", mensaje: "Usuario o clave incorrectos"}`
  — mismo mensaje genérico para las 3 causas (D27: no se revela cuál
  de las tres ocurrió).

### 3.3 `RolPorUsuario` — 3 verbos, llave compuesta (D16, igual que v3)

`GET /api/RolPorUsuario?fkUsuario=`, `POST /api/RolPorUsuario`,
`PATCH /api/RolPorUsuario/cerrar`. Sin `/{id}`, `PUT` ni `DELETE` —
cualquier llamada a esas rutas → 404 de ASP.NET.

### 3.4 `EnlaceRegistro` — 4 verbos de negocio (D22, D24)

- `GET /api/EnlaceRegistro?fkSesion=`, `GET/{idEnlace}` — normales;
  el `GET` puntual expone `ventana_desde`/`ventana_hasta` (2 columnas
  planas, no un rango — ver nota de mapeo en
  [5_data_model.md](5_data_model.md) §2.3).
- `POST /api/EnlaceRegistro` con **EnlaceRegistroEmitir**
  (`{"fk_sesion": 10, "minutos_antes": 30, "minutos_despues": 30,
  "fk_usuario": null}` — los 3 últimos opcionales) → `200 {estado,
  mensaje, id_enlace}`. Llama `SELECT * FROM
  catedras.fn_emitir_enlace_api(...)`. Si la sesión ya tenía un enlace
  vigente, ese enlace anterior queda revocado como efecto colateral
  documentado (`RN-14`) — la respuesta del `POST` no lo menciona
  explícitamente (mismo criterio que los `DELETE` en cascada de v2-v4:
  el efecto colateral se documenta aquí y en el smoke test, no se
  inventa un campo nuevo en la respuesta). `fk_sesion` inexistente →
  **500** (la función lanza su propio `RAISE EXCEPTION`, D25).
- `PATCH /api/EnlaceRegistro/{idEnlace}/revocar` → `200
  {estado, mensaje, filasEliminadas: 1}` (reusa la forma de respuesta
  de un `DELETE`, aunque la fila sigue existiendo — se documenta así
  para no inventar un cuarto nombre de campo de respuesta en el
  proyecto). Ya revocado → **400**; inexistente → **404**.
- **Sin** `POST` de columnas sueltas, `PUT`, `PATCH` genérico ni
  `DELETE` — esas combinaciones responden 404 de ASP.NET.

### 3.5 `ClaveAcceso` — 4 verbos de negocio (D24)

- `GET /api/ClaveAcceso?fkAsistente=`, `GET/{idClave}` — nunca
  incluyen `clave_hash` (D23, mismo criterio que `Usuario`).
- `POST /api/ClaveAcceso/solicitar` con **ClaveAccesoSolicitar**
  (`{"identificador": "1000090", "token": "<uuid del enlace>", "ip":
  null}`) → `200 {estado, mensaje, id_clave, clave: "483920",
  enviado_a: "asis.1000090@usbmed.edu.co"}` — la **única** respuesta de
  todo el proyecto donde un secreto de un solo uso viaja en texto
  plano (RNF12), a propósito. `identificador` no resuelto, o enlace
  inexistente/revocado/fuera de ventana → **500** (D25).
- `POST /api/ClaveAcceso/validar` con **ClaveAccesoValidar**
  (`{"id_clave": 501, "clave": "483920", "ip": null, "user_agent":
  null}`) → `200 {estado, mensaje, id_registro}`. Clave incorrecta,
  usada, expirada, cupo agotado o sin vinculación vigente → **500**
  (D25; el mensaje de la excepción indica cuál de las 5 razones fue,
  pero el código HTTP es siempre 500).
- **Sin** `POST` plano, `PUT`, `PATCH` genérico, `DELETE`.

### 3.6 `RegistroAsistencia` — 3 verbos, solo lectura + alta manual

- `GET /api/RegistroAsistencia?fkSesion=&fkAsistente=`, `GET/{id}` —
  normales; incluyen `fk_vinculacion_snapshot`/`fk_programa_snapshot` ya
  resueltos.
- `POST /api/RegistroAsistencia` con **RegistroAsistenciaCrear**:
  `origen` debe ser `"MANUAL"` o `"IMPORTADO"` — cualquier otro valor
  → **400** (D26, antes de tocar la BD). `fk_clave`/`fk_enlace` no
  existen en esta petición (siempre `NULL`). Un asistente sin
  vinculación vigente → **500** (`RN-07`, mismo trigger que la vía de
  `ClaveAcceso`/validar).
- **Sin** `PUT`, `PATCH` ni `DELETE`.

### 3.7 Errores — resumen de lo que cambia

Mismo sobre que v1-v4 para 422/400/404/500. **Nuevo: 401**
(`{estado: "no_autorizado", mensaje}`), exclusivo de
`POST /api/Usuario/login` (D27) — ningún otro endpoint de la API lo
usa en esta versión (D28: no hay autorización por rol en el resto de
la API).

## 4. Contrato API ↔ BD

Sin cambios de mecánica general. **Nuevo patrón de invocación (D24):**
`EnlaceRegistro`/`ClaveAcceso` usan `SELECT * FROM
catedras.fn_..._api(...)` en vez de `CALL` — porque son `FUNCTION`
(devuelven un valor o una fila directamente), no `PROCEDURE`. La API
no sabe ni necesita saber que, internamente, esas funciones hacen un
`CALL` a su propio `PROCEDURE` — eso es un detalle de implementación
de la BD (Artículo 5).

## 5. Healthchecks y arranque

Sin cambios: [version1/6_contracts.md](../version1/6_contracts.md) §5.

## 6. Contratos de volúmenes y montajes

**Sin cambios — y sin ningún montaje nuevo**, igual que v4. Las 5
tablas y las 3 funciones/procedimientos que v5 usa ya estaban en
`script_bdcatedras.sql` desde el montaje de la v1.

## 7. Devcontainer

Sin cambios: no existe en v5.

## 8. Credenciales

Sin cambios: [version1/6_contracts.md](../version1/6_contracts.md) §8.
La clave de BCrypt (coste de hash) no es una credencial de conexión —
no hay nada que agregar aquí.

## 9. Contrato del front (`front_catedras/`) con la API — lo que crece en v5

- **Login real:** `routes/auth.py` llama `POST /api/Usuario/login`;
  guarda el usuario y sus roles en la **sesión de Flask** (firmada con
  `SECRET_KEY`, mecanismo propio del front, no de la API — D28: la API
  sigue sin sesión ni token). `auth_helpers.py` (v2, siempre "sin
  sesión") pasa a leer esa sesión real. `/admin` queda protegido en el
  FRONT (`before_request` en `admin_bp`): sin sesión → `/login`; sin rol
  `ADMIN` → 403. La API sigue sin exigir nada (D28).
- **Pantalla pública de autorregistro** (`routes/registro.py`, que hoy es
  un placeholder ya registrado y enlazado en el navbar; **sin** pasar por
  `/admin`): `/registro/<token>` pide el identificador, llama
  `POST /api/ClaveAcceso/solicitar`, muestra la clave (RNF12 — en
  producción real sería un correo, aquí se muestra o se "simula" el
  envío) y un formulario que envía a `/registro/<token>/validar`, que
  llama `POST /api/ClaveAcceso/validar`.
- El front sigue sin estar en `docker-compose.yml`; `API_BASE_URL`
  sigue en `http://localhost:8142/api`, sin cambios.

## 10. Reset completo

Sin cambios: [version1/6_contracts.md](../version1/6_contracts.md) §9.
`docker compose down -v && docker compose up -d --build` recrea las 37
tablas con sus datos de ejemplo — incluidos el `usuario` y los 3
`rol_por_usuario` de ejemplo, y los 9/167/168 `enlace_registro`/
`clave_acceso`/`registro_asistencia` que v5 expone por primera vez.
