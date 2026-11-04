# Contratos de integración — Versión 10: calendario público y solicitudes de ponentes

> ⚠️ **BORRADOR · por construir** — ver nota de [2_spec.md](2_spec.md). Puertos,
> variables y credenciales: sin cambios (el SMTP es el de la v7.1).

## 1. `FranjaDisponible` — 7 endpoints

| Verbo y ruta | Cuerpo | Respuesta |
|---|---|---|
| `GET /api/FranjaDisponible?desde=&hasta=&fkSede=&estado=&limite=` | — | 200 lista · 204 |
| `GET /api/FranjaDisponible/{id}` | — | 200 · 404 |
| `GET /api/FranjaDisponible/calendario?anio=&mes=&fkSede=` | — | 200 `{anio, mes, dias:[{fecha, franjas:[{id_franja, inicio, fin, fk_sede, estado, catedra?}]}]}` |
| `POST /api/FranjaDisponible` | `{"fecha":"2027-02-15","hora_inicio":"07:00","duracion_minutos":60,"fk_sede":"SAN_BENITO","fk_modalidad":"PRESENCIAL"}` | 200 `{estado:"creada", id_franja}` · 422 · 500 (solapada) |
| `POST /api/FranjaDisponible/plantilla` | `{"desde":"2027-02-01","hasta":"2027-02-28","fk_sede":"SAN_BENITO","fk_modalidad":"PRESENCIAL","dias":[1,2,3,4,5],"horas":["06:00","07:00","18:00"]}` | 200 `{creadas, omitidas}` |
| `PATCH /api/FranjaDisponible/{id}` | `{"estado":"BLOQUEADA"}` | 200 · 400 · 404 · 500 (`OCUPADA`/`EN_REVISION`) |
| `DELETE /api/FranjaDisponible/{id}` | — | 200 `{filasEliminadas}` · 404 · 500 |

## 2. `SolicitudCatedra` — 6 endpoints

| Verbo y ruta | Cuerpo | Respuesta |
|---|---|---|
| `POST /api/SolicitudCatedra` (pública) | ver §2.1 | 200 `{estado:"PENDIENTE", id_solicitud}` · 422 · 500 (franja no disponible) |
| `GET /api/SolicitudCatedra?estado=PENDIENTE&limite=` | — | 200 lista · 204 |
| `GET /api/SolicitudCatedra/{id}` | — | 200 · 404 |
| `GET /api/SolicitudCatedra/pendientes` | — | 200 `{total}` |
| `POST /api/SolicitudCatedra/{id}/aprobar` | `{"id_evento_asis":"000123456","fk_tipo_evento":"CATA","fk_dependencia":null,"fk_encuesta":1,"fk_catedra":null,"fk_usuario":1}` | 200 `{estado:"APROBADA", id_catedra, id_sesion, correo_ponente, nombre_ponente}` · 404 · 422 · 500 |
| `POST /api/SolicitudCatedra/{id}/rechazar` | `{"motivo":"La franja se reservó para un evento institucional.","fk_usuario":1}` | 200 `{estado:"RECHAZADA"}` · 404 · 422 · 500 |

Los ids de `fk_tipo_evento` y `fk_encuesta` son ejemplos. En la Fase 74 se
confirman contra los datos reales (`tipo_evento` es `char(4)`).

### 2.1 Cuerpo de la solicitud

```json
{
  "fk_franja": 120,
  "nombre_catedra": "Ética y ciudad",
  "resumen_ponencia": "Conversación sobre …",
  "fk_tipo_evento": null,
  "tipo_documento": "CC", "numero_documento": "43123456",
  "nombres": "Claudia", "apellidos": "Restrepo",
  "perfil_profesional": "Filósofa, magíster en …",
  "direccion": "Calle 10 # 20-30, Medellín",
  "telefono": "3011234567",
  "correo_institucional": "claudia.restrepo@usbmed.edu.co",
  "es_externo": false,
  "acepta_datos": true
}
```

## 3. API ↔ BD

| Método | SQL |
|---|---|
| Crear, listar, consultar, actualizar y eliminar franja | `CALL catedras.sp_{verbo}_franja(…, NULL);` |
| Plantilla | `CALL catedras.sp_plantilla_franjas(@desde, @hasta, @sede, @modalidad, @dias, @horas::time[], @duracion, NULL);` |
| Calendario | `CALL catedras.sp_calendario_franjas(@anio, @mes, @sede, NULL);` |
| Solicitud | `CALL catedras.sp_crear_solicitud(…)` · `sp_listar_solicitud` · `sp_consultar_solicitud` · `sp_contar_solicitudes_pendientes` |
| Aprobar y rechazar | `CALL catedras.sp_aprobar_solicitud(…)` · `CALL catedras.sp_rechazar_solicitud(…)` |

## 4. Front ↔ API

| Ruta del front | Llama a |
|---|---|
| `GET /calendario` | `GET /api/FranjaDisponible/calendario` y `GET /api/Sede` |
| `GET/POST /calendario/solicitar/<franja>` | `GET /api/FranjaDisponible/{id}`, `GET /api/TipoDocumento` y `POST /api/SolicitudCatedra`; luego, el correo "recibida" |
| `/admin/franjas…` | `FranjaDisponible` (CRUD + plantilla) |
| `/admin/solicitudes…` | `SolicitudCatedra`, `GET /api/TipoEvento`, `GET /api/Encuesta`; al aprobar, el correo (+ `POST /api/QrSesion/{id}/enlace-proyeccion` si la v9 está, D79) |
| Panel `/admin` | `GET /api/SolicitudCatedra/pendientes` |

## 5. Montajes

```yaml
- ./db/postgres/migraciones/061_franja_disponible.sql:/docker-entrypoint-initdb.d/z061_franja_disponible.sql:ro
- ./db/postgres/migraciones/062_solicitud_catedra.sql:/docker-entrypoint-initdb.d/z062_solicitud_catedra.sql:ro
- ./db/postgres/migraciones/063_aprobar_solicitud.sql:/docker-entrypoint-initdb.d/z063_aprobar_solicitud.sql:ro
```
