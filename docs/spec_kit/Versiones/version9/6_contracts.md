# Contratos de integración — Versión 9: QR dinámico y control por modalidad

> ⚠️ **BORRADOR · por construir** — ver nota de [2_spec.md](2_spec.md). Puertos,
> variables y credenciales: sin cambios. Para que el QR sirva en los celulares
> sigue haciendo falta `URL_PUBLICA` en https (v7.1).

## 1. Endpoints nuevos o modificados

| Verbo y ruta | Cuerpo / consulta | Respuesta |
|---|---|---|
| `POST /api/Registro/pase` | `{"token": "uuid", "codigo": "K7Q2M9XA"}` | 200 `{pase, expira_en}` · 422 · 500 `V9: el código del QR ya expiró…` |
| `POST /api/Registro/estudiante` · `/docente` · `/externo` | Cuerpos de la v8 **+ `"pase": "…"`** (obligatorio si el enlace tiene secreto) | Igual que la v8 · 500 `V9: el pase no es válido o venció…` |
| `GET /api/QrSesion/{idSesion}/codigo?tokenProyeccion=` | — | 200 `{url_registro, codigo, segundos_restantes, rotacion_segundos, abierto, ventana_abre, ventana_cierra}` · 404 |
| `POST /api/QrSesion/{idSesion}/enlace-proyeccion` | `{"fk_usuario": 1, "regenerar": false}` | 200 `{token_proyeccion}` (el front arma la URL) · 404 (la sesión no existe) |
| `GET /api/QrSesion/proyeccion/{tokenProyeccion}` | — | 200 `{id_sesion}` · 404 |

**Seguridad de `/codigo`:**
- sin `tokenProyeccion`, solo lo llama el front desde `/admin`: el front protege
  la ruta con el login (D28 de la v5: la API no tiene roles);
- con `tokenProyeccion`, la API comprueba que pertenezca a esa sesión.

`url_registro` tiene la forma `{URL_PUBLICA}/registro/{token}?c={codigo}`. El front
la completa con su `URL_PUBLICA`; la API devuelve la ruta relativa
`/registro/{token}?c={codigo}`.

## 2. API ↔ BD

| Método | SQL |
|---|---|
| `EmitirPaseAsync` | `SELECT * FROM catedras.fn_emitir_pase(@token::uuid, @codigo);` |
| `ObtenerCodigoAsync` | `CALL catedras.sp_codigo_qr_sesion(@fkSesion, NULL);` |
| `EmitirEnlaceProyeccionAsync` | `CALL catedras.sp_emitir_enlace_proyeccion(@fkSesion, @fkUsuario::bigint, @regenerar, NULL);` |
| `ResolverProyeccionAsync` | `CALL catedras.sp_resolver_proyeccion(@token, NULL);` |

## 3. Front ↔ API

| Ruta del front | Llama a |
|---|---|
| `GET /registro/<token>?c=` | `POST /api/Registro/pase` |
| `GET /proyectar/<t>` | `GET /api/QrSesion/proyeccion/{t}`, y `proyeccion.js` cada 60 s llama a `GET /proyectar/<t>/codigo.json` (el front hace de proxy de `/api/QrSesion/{id}/codigo`) |
| `GET /admin/sesiones/<id>/qr` | Lo mismo, con login |
| Contador | `GET /api/QrSesion/asistencia?fkSesion=` cada 15 s, también por proxy |

El navegador **nunca** habla directamente con la API, que en Docker solo es
accesible por la red interna (`api:8042`). Todo pasa por el front.

## 4. Montajes

```yaml
- ./db/postgres/migraciones/058_qr_dinamico.sql:/docker-entrypoint-initdb.d/z058_qr_dinamico.sql:ro
- ./db/postgres/migraciones/059_pase_registro.sql:/docker-entrypoint-initdb.d/z059_pase_registro.sql:ro
- ./db/postgres/migraciones/060_enlace_proyeccion.sql:/docker-entrypoint-initdb.d/z060_enlace_proyeccion.sql:ro
```
