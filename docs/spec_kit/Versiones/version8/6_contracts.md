# Contratos de integración — Versión 8: registro de asistencia sin login, por tipo de usuario

> ⚠️ **BORRADOR · por construir** — ver nota de [2_spec.md](2_spec.md).
> **Documento 6 de 8.** Puertos, variables de entorno y credenciales: sin
> cambios (ver [version1/6_contracts.md](../version1/6_contracts.md)).

---

## 1. Convenciones

- Los cuerpos y las respuestas van en snake_case.
- Los errores devuelven `{estado, mensaje, detalle}`; el 422 agrega `errores:[{campo, mensaje}]`.
- Todos los endpoints de `Registro` son **públicos**: la API no tiene autorización por rol (D28 de la v5, sigue igual).

## 2. `Registro` — 4 endpoints nuevos

| Verbo y ruta | Cuerpo | Respuesta |
|---|---|---|
| `GET /api/Registro/contexto?token={uuid}` | — | 200 `{id_sesion, catedra, titulo, inicio, fin, modalidad, ubicacion, tiene_encuesta, abierto}` · 404 (el token no existe) |
| `POST /api/Registro/estudiante` | **RegistroEstudiante** | 200 · 404 (ID inexistente) · 422 · 500 |
| `POST /api/Registro/docente` | **RegistroDocente** | 200 · 422 · 500 |
| `POST /api/Registro/externo` | **RegistroExterno** | 200 · 422 · 500 |

### 2.1 Cuerpos

```json
// RegistroEstudiante
{ "token": "3f6c…", "id_asis": "1000513C", "latitud": 6.2529, "longitud": -75.5646,
  "ip": "190.1.2.3", "user_agent": "Mozilla/5.0 …" }

// RegistroDocente
{ "token": "3f6c…", "tipo_documento": "CC", "numero_documento": "71234567",
  "nombres": "Ana María", "apellidos": "Gómez Ruiz", "correo": "ana.gomez@usbmed.edu.co",
  "celular": "3001234567", "fk_facultad": "F_ING", "es_administrativo": false,
  "acepta_datos": true, "latitud": 6.2529, "longitud": -75.5646, "ip": null, "user_agent": null }

// RegistroExterno
{ "token": "3f6c…", "tipo_documento": "PA", "numero_documento": "AB123456",
  "nombres": "Luis", "apellidos": "Pérez", "correo": "luis@gmail.com", "celular": null,
  "procedencia": "Barrio Belén", "acepta_datos": true,
  "latitud": null, "longitud": null, "ip": null, "user_agent": null }
```

**Validación de forma (422):**

| Campo | Regla |
|---|---|
| `token` | UUID |
| `id_asis` | `^[0-9A-Za-z]{5,15}$` |
| `numero_documento` | `^[0-9A-Za-z-]{4,20}$` (como `chk_documento_numero`) |
| `nombres`, `apellidos` | 1 a 100 caracteres |
| `correo` | Formato de correo |
| `procedencia` | 1 a 150 caracteres |
| `acepta_datos` | Debe ser `true` |
| `latitud` / `longitud` | Van juntas; latitud -90..90, longitud -180..180 |

### 2.2 Respuesta 200

```json
{ "estado": "registrado", "mensaje": "Asistencia registrada.", "id_registro": 9123,
  "nombre": "Ana María Gómez Ruiz", "tiene_encuesta": true, "id_sesion": 5712 }
```

Si la persona ya estaba registrada, la respuesta es `"estado": "ya_registrado"`,
con el mismo `id_registro` de antes.

### 2.3 Errores de negocio (500, `estado: "error_motor"`)

| `detalle` empieza por | Cuándo |
|---|---|
| `RN-15:` | Enlace revocado o fuera de su ventana |
| `V8: la ubicación es obligatoria` | Sesión presencial sin coordenadas |
| `V8: estás a` | Fuera de la geocerca |
| `V8: el registro de externos está deshabilitado` | `PERMITE_EXTERNOS = false` |
| `RN-07:` | Estudiante sin programa activo cuando su vinculación lo exige |

## 3. Cambios en endpoints existentes

| Endpoint | Cambio |
|---|---|
| `POST /api/Usuario/login` | 401 si el usuario no tiene el rol `ADMIN` vigente. Ya no provisiona cuentas de estudiante (RF211) |
| `GET/POST/PUT/PATCH /api/Sede` | Agrega `latitud`, `longitud` y `radio_metros`, opcionales (RF209) |
| `POST /api/ClaveAcceso/solicitar` y `/validar` | Siguen existiendo (D58), pero el front ya no los llama |
| `/api/InscripcionCatedra` | Sigue existiendo, sin uso desde el front |

## 4. Contrato API ↔ BD

| Repositorio | SQL |
|---|---|
| `ObtenerContextoAsync(token)` | `CALL catedras.sp_contexto_registro(@token, NULL);` |
| `RegistrarEstudianteAsync(…)` | `SELECT * FROM catedras.fn_registrar_estudiante_api(@token, @idAsis, @latitud, @longitud, @ip::inet, @userAgent);` |
| `RegistrarDocenteAsync(…)` | `SELECT * FROM catedras.fn_registrar_docente_api(…);` |
| `RegistrarExternoAsync(…)` | `SELECT * FROM catedras.fn_registrar_externo_api(…);` |

## 5. Contrato del front con la API

| Ruta del front | Llama a |
|---|---|
| `GET /registro/<token>` | `GET /api/Registro/contexto?token=` |
| `POST /registro/<token>/estudiante` | `POST /api/Registro/estudiante` |
| `POST /registro/<token>/docente` | `GET /api/Facultad` (para el select) y `POST /api/Registro/docente` |
| `POST /registro/<token>/externo` | `POST /api/Registro/externo` |
| `/admin/sedes/*` | `/api/Sede` con las coordenadas |

## 6. Volúmenes y montajes

Se agregan 4 líneas en `docker-compose.yml`, servicio `postgres`, después de `z053_`:

```yaml
- ./db/postgres/migraciones/054_tipos_registro_v8.sql:/docker-entrypoint-initdb.d/z054_tipos_registro_v8.sql:ro
- ./db/postgres/migraciones/055_geocerca_sede.sql:/docker-entrypoint-initdb.d/z055_geocerca_sede.sql:ro
- ./db/postgres/migraciones/056_sp_registro_por_tipo.sql:/docker-entrypoint-initdb.d/z056_sp_registro_por_tipo.sql:ro
- ./db/postgres/migraciones/057_apagado_v71.sql:/docker-entrypoint-initdb.d/z057_apagado_v71.sql:ro
```
