# Plan técnico — Versión 8: registro de asistencia sin login, por tipo de usuario

> ⚠️ **BORRADOR · por construir** — ver nota de [2_spec.md](2_spec.md).

---

## 1. Stack

No cambia respecto de la v7:
- **BD:** PostgreSQL 16;
- **API:** ASP.NET Core con Dapper; los repositorios hacen `CALL catedras.sp_…`;
- **Front:** Flask con Jinja, que llama a la API por HTTP;
- **Arranque:** Docker Compose, con 3 servicios.

La v8 no agrega servicios.

## 2. Estructura de carpetas (lo que se agrega o toca)

```
db/postgres/migraciones/
  054_tipos_registro_v8.sql          EXTERNO, DOCENTE sin programa, columnas de asistente y registro
  055_geocerca_sede.sql              coordenadas y radio en sede, parámetro, fn_distancia_metros
  056_sp_registro_por_tipo.sql       sp_registrar_estudiante / _docente / _externo (+ fn_*_api)
  057_apagado_v71.sql                roles ESTUDIANTE/PONENTE cerrados, requiere_inscripcion por defecto false
docker-compose.yml                   monta 054-057 como z054_ … z057_

api_catedras/
  Controllers/RegistroController.cs          NUEVO · POST /api/Registro/{estudiante|docente|externo}
  Servicios/IServicioRegistro.cs, ServicioRegistro.cs                  NUEVO
  Repositorios/IRepositorioRegistro.cs, RepositorioRegistroPostgres.cs NUEVO
  Peticiones/RegistroEstudiante.cs, RegistroDocente.cs, RegistroExterno.cs   NUEVO
  Modelos/ResultadoRegistro.cs               NUEVO
  Modelos/Sede.cs, Peticiones/Sede*.cs, Repositorios/RepositorioSedePostgres.cs   + latitud, longitud, radio_metros
  Servicios/ServicioUsuario.cs               LoginAsync: solo ADMIN; sin primer ingreso
  Program.cs                                 registra las capas de Registro
  pruebas/Programa.cs                        bloque "V8 · Registro por tipo"
  pruebas/verificar_v8.ps1, correr_todo_v8.ps1   NUEVOS

front_catedras/
  app.py                       sin los blueprints estudiante, ponente y ponente_catedras
  auth_helpers.py              endpoint_de_inicio -> admin.panel
  routes/registro.py           3 botones + 3 formularios; sin el flujo de clave
  templates/registro_tipo.html, registro_estudiante.html, registro_docente.html,
            registro_externo.html, registro_listo.html            NUEVAS
  static/geolocalizacion.js    pide la ubicación solo si data-ubicacion = "obligatoria" | "opcional"
  templates/partials/navbar.html   sin menús de estudiante y ponente
  routes/admin.py + templates/admin/sede_*.html   campos de coordenadas
```

## 3. Arquitectura en capas

Es la misma de siempre (Artículo 3):

```
RegistroController ── IServicioRegistro ── IRepositorioRegistro ── CALL/SELECT catedras.fn_registrar_*_api
      (HTTP, 422/404/500)   (reglas de forma,       (Dapper; lee el JSON
                             p. ej. acepta_datos)     que devuelve la BD)
```

- **El controlador** traduce las excepciones a códigos HTTP: `NoEncontradoExcepcion`
  → 404, `NpgsqlException` → 500 con `detalle`, `ArgumentException` → 400.
- **El servicio** solo valida lo que no puede validar el atributo: que
  `acepta_datos` sea `true` en docente y externo, que no vengan solo una de las
  dos coordenadas y el formato del token.
- **El repositorio** llama a una función `fn_registrar_*_api` que envuelve el
  procedimiento y devuelve una fila. Es el mismo molde de `fn_validar_clave_geo_api`
  en la v7.1.

## 4. Decisiones de diseño clave

### 4.1 Un procedimiento por tipo, todo en un `CALL` (D57, Regla 1)

Docente y externo pueden ser personas nuevas. Crear el asistente, su documento,
su vinculación y su consentimiento y luego registrar son **una sola operación**.
Si falla el registro (ventana cerrada, geocerca), no queda una persona a medias.

### 4.2 Clave técnica en vez de clave por correo (D71)

`registro_asistencia` tiene el CHECK `chk_registro_clave`: si el origen es `QR` o
`ENLACE`, `fk_clave` no puede ser NULL. No se edita el script (Artículo 5). En
cambio, el procedimiento inserta una fila en `clave_acceso` con:
- `clave_hash = digest('v8-registro-directo' || token, 'sha256')`;
- `expira_en = now() + 1 minuto`;
- `usado_en = now()`.

Así queda la evidencia (IP, enlace y hora) sin enviar ningún correo.

### 4.3 La ubicación se decide en la BD (D60, D61)

El front pide la ubicación según la modalidad que le dice la API (`GET
/api/Registro/contexto?token=`), pero **la regla se aplica en el procedimiento**:
el front podría saltársela. La distancia se calcula con Haversine en
`fn_distancia_metros` (SQL puro, sin PostGIS).

### 4.4 Apagar sin borrar (D58)

- **Blueprints:** basta con no registrarlos en `app.py`; el código queda.
- **Roles:** se cierran con `fecha_fin`; no se borran usuarios.
- **Inscripción:** `requiere_inscripcion` cambia su DEFAULT a `false`, y
  `sp_emitir_qr_sesion` deja de ponerlo en `true`. El disparador
  `trg_registro_inscripcion` sigue, pero no actúa cuando el enlace no lo exige.

### 4.5 Estudiante por ID sin verificación adicional (D55)

Mientras no exista la V9, cualquiera que conozca un ID podría registrarlo desde el
enlace. Es un riesgo aceptado y temporal: la V9 agrega el código dinámico.
Se documenta en la guía para que la IA no "lo arregle" metiendo otra vez la clave
por correo.

## 5. Frontend

```
GET  /registro                        pegar el enlace (igual que hoy)
GET  /registro/<token>                3 botones (RF198); 404 si el enlace no existe
GET  /registro/<token>/estudiante     formulario de 1 campo
GET  /registro/<token>/docente        formulario docente/administrativo (+ select de facultad)
GET  /registro/<token>/externo        formulario externo
POST /registro/<token>/<tipo>         llama a POST /api/Registro/<tipo>; muestra registro_listo.html
```

- La pantalla final dice "Registrado" o "Ya estabas registrado" y, si la sesión
  tiene encuesta, muestra el botón opcional (RF216).
- Los errores de la BD se muestran con su `detalle`, como hoy en `_motivo(ex)`.

## 6. Docker

No hay servicios nuevos. Se montan 4 migraciones (`z054_` … `z057_`). Con un
volumen ya creado se aplican a mano, en orden (ver [7_quickstart.md](7_quickstart.md)).
