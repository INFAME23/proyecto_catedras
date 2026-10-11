# Quickstart — Versión 5: usuarios, login y registro de asistencia

> ✅ **Revisada contra la BD real (2026-10-08), sin construir** — ver nota de [2_spec.md](2_spec.md).
>
> **Documento 7 de 8** del spec kit. Validación del sistema ya
> construido en v5, sobre lo que dejaron v1-v4.
>
> **Nota de alcance (v5):** mismo backend que v1-v4, sin montajes
> nuevos, pero con un paquete NuGet nuevo (`BCrypt.Net-Next` — ver
> [6_contracts.md](6_contracts.md) §3.1). Hoy la API expone **29**
> recursos: los 24 de v1-v4 más `Usuario`, `RolPorUsuario`,
> `EnlaceRegistro`, `ClaveAcceso` y `RegistroAsistencia`.

---

## 1. Levantar

Sin cambios respecto a v1-v4
([version1/7_quickstart.md](../version1/7_quickstart.md) §1) — sin
ningún paso de migración extra (igual que v4).

```powershell
git clone https://github.com/INFAME23/proyecto_catedras.git
cd proyecto_catedras
docker compose up -d --build
```

## 2. Verificación de la API — 9 pasos

```powershell
docker compose ps
```

1. **http://localhost:8142/swagger** → ahora muestra **29** recursos;
   `EnlaceRegistro`/`ClaveAcceso` sin los verbos CRUD habituales (ver
   [6_contracts.md](6_contracts.md) §3.4-3.5).
2. **Fijar la clave del administrador de ejemplo y hacer login (RF136,
   RF139):** la cuenta `admin.catedras` (id 1) trae una clave que nadie
   conoce, así que primero se fija una: `PATCH /api/Usuario/1` con
   `{"clave": "Admin12345"}` → `200`. Luego `POST /api/Usuario/login` con
   `{"usuario": "admin.catedras", "clave": "Admin12345"}` → `200` con los
   roles vigentes `ADMIN` y `CONSULTA`; con una clave incorrecta → `401`.
   **Se repite tras cada `down -v`.**
3. **Crear usuario y verificar que el hash nunca se expone (criterio
   2):** `POST /api/Usuario` con
   `{"fk_asistente": 100, "usuario": "prueba.login", "clave": "Secreta123"}`
   (usar un `fk_asistente` que no tenga ya una cuenta — confirmar antes
   con `GET /api/Usuario`) → `200`; la respuesta y un `GET
   /api/Usuario/{id}` posterior **no** incluyen ningún campo de clave.
   `POST /api/Usuario/login` con `{"usuario": "prueba.login", "clave":
   "Secreta123"}` → `200`.
4. **`RolPorUsuario` (D16, igual que v3):** `POST
   /api/RolPorUsuario` con `{"fk_usuario": {id del paso 3}, "fk_rol":
   "CONSULTA"}` → `200`; `PATCH /api/RolPorUsuario/cerrar` con la
   misma llave + `fecha_fin` → `200`; repetir → `400`.
5. **Emitir un enlace para una sesión nueva (así la ventana incluye la
   hora real de la prueba):** primero `POST /api/Sesion` con
   `inicio`/`fin` que bracketen el momento en que se ejecuta esta
   prueba (p. ej. 2 horas antes y 2 horas después de "ahora"),
   `fk_catedra: 5498`, `fk_modalidad: "PRESENCIAL"` (canal `QR`),
   `fk_sede: "SAN_BENITO"`, `fk_periodo: "2026-2"`. Luego `POST
   /api/EnlaceRegistro` con `{"fk_sesion": {id recién creado}}` → `200`
   con `id_enlace`; `GET /api/EnlaceRegistro/{idEnlace}` muestra
   `canal: "QR"` (decidido por la BD, no mandado por el cliente) y
   `ventana_desde`/`ventana_hasta` cubriendo el momento actual.
6. **Solicitar y validar una clave (RF149-150, criterio 5):** `POST
   /api/ClaveAcceso/solicitar` con
   `{"identificador": "1000090", "token": "<el token de GET
   /api/EnlaceRegistro/{idEnlace}>"}` (`1000090` es un `id_asis` de
   ejemplo con vinculación vigente) → `200` con una `clave` de 6
   dígitos en texto plano. `POST /api/ClaveAcceso/validar` con
   `{"id_clave": {el id recién devuelto}, "clave": "{esa misma
   clave}"}` → `200` con `id_registro`; `GET
   /api/RegistroAsistencia/{idRegistro}` muestra `origen: "QR"` y un
   `fk_vinculacion_snapshot` ya resuelto. Repetir el mismo `validar` →
   `500` (`RN-20`, clave ya usada). (Solicitar y validar dentro de los 10
   minutos de vigencia de la clave. `1000090` es el asistente 1.)
   **Límite conocido (RF153):** pedir otra clave y mandar `validar` con
   una clave incorrecta → `500`; `GET /api/ClaveAcceso/{idClave}` sigue
   mostrando `intentos: 0` (la excepción de la BD deshace el contador).
7. **Registro manual, y el bloqueo de origen (RF156, criterio 6):**
   `POST /api/RegistroAsistencia` con
   `{"fk_sesion": {la misma sesión}, "fk_asistente": 2, "origen": "QR"}`
   (asistente distinto, para no chocar con `uq_registro_sesion_asis`)
   → `400` (origen no permitido en un `POST` directo). El mismo body
   con `"origen": "MANUAL"` → `200`.
8. **`RESTRICT` hacia `asistente` (criterio 7):** `DELETE
   /api/Asistente/{un id con registro_asistencia de ejemplo, p. ej.
   el del paso 6 o 7}` → `500`; confirmar con `GET
   /api/DocumentoAsistente?fkAsistente={id}` que sus documentos de v3
   siguen intactos.
9. **Reset** (recomendado después de las pruebas anteriores):
   `docker compose down -v && docker compose up -d --build` → las 29
   tablas vuelven a sus datos originales.

**Prueba de capas (criterio de aceptación 8):**
```powershell
cd api_catedras
dotnet run --project pruebas
```
Ahora cubre las **29** entidades con repositorios FALSOS, incluida la
verificación de `BCrypt.Verify` (con una clave y un hash fijos en el
código de prueba, sin necesidad de PostgreSQL) y el bloqueo de
`origen: QR/ENLACE` en `RegistroAsistencia`. Termina con código 0.

## 3. Frontend (`front_catedras/`, aparte del `docker compose` de arriba)

Arranque sin cambios respecto a v1-v4.

Con la API corriendo (§1-2 de aquí):

1. **http://localhost:5000/login** → reemplaza el placeholder «en
   construcción»; un login incorrecto muestra un mensaje de error
   genérico (sin distinguir usuario inexistente de clave incorrecta,
   D27); `admin.catedras` con la clave del paso 2 entra con sus roles.
2. **http://localhost:5000/registro/\<token del enlace del paso 5\>**
   (pública, sin login; también `/registro`, que enlaza el navbar) →
   pide el identificador, muestra la clave de 6 dígitos (o "se envió un
   correo", simulando RNF12) y un formulario para validarla; al validar,
   confirma "asistencia registrada" o explica el motivo del rechazo.
3. **http://localhost:5000/admin/** → sin sesión redirige a `/login`;
   con `admin.catedras` muestra 5 tarjetas nuevas; `EnlaceRegistro`
   ofrece "emitir"/"revocar"; `ClaveAcceso` solo lista (de solo lectura,
   sin ningún formulario de creación directa).
4. **Datos mock eliminados (igual que v1-v4):** apagar la API y
   recargar cualquier pantalla de esta versión → error claro.

## 4. Problemas frecuentes

| Síntoma | Diagnóstico |
|---|---|
| `POST /api/ClaveAcceso/solicitar` responde 500 "RN-15" | El enlace está fuera de su ventana — revisar que la `Sesion` de prueba tenga `inicio`/`fin` que incluyan la hora real de la prueba (paso 5), no fechas de ejemplo ya pasadas |
| `POST /api/ClaveAcceso/validar` responde 500 aunque la clave "se ve bien" | Revisar que no se esté reusando una clave ya `validada` antes (`usado_en` no nulo) ni una ya expirada (10 minutos por defecto desde que se pidió) |
| `POST /api/Usuario/login` responde 401 con una clave "que debería funcionar" | Confirmar que el usuario tenga `activo: true` — una cuenta inactiva responde 401 igual que una clave incorrecta, a propósito (D27) |
| `GET /api/Usuario/{id}` "no trae la clave" | Correcto: `clave_hash` nunca se serializa (D23) — no es un bug, es la especificación (RF138) |
| `POST /api/RegistroAsistencia` con `origen: "ENLACE"` responde 400 | Correcto: la única vía para `QR`/`ENLACE` es `POST /api/ClaveAcceso/validar` — un `POST` directo solo acepta `MANUAL`/`IMPORTADO` (D26) |
| Todo roto | `docker compose down -v && docker compose up -d --build` (API); para el front, confirmar que `API_BASE_URL` siga en `http://localhost:8142/api` |
