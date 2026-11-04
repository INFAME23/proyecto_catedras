# Plan técnico — Versión 9: QR dinámico y control por modalidad

> ⚠️ **BORRADOR · por construir** — ver nota de [2_spec.md](2_spec.md).

## 1. Stack

Sin cambios. El QR se dibuja en el navegador con una biblioteca JS cargada desde
`static/` (p. ej. `qrcode.min.js`, versión fija y copiada al repo). El front ya
genera QR en el servidor (`servicios/qr_sesiones.py`); para la pantalla que se
refresca sola conviene dibujarlo en el cliente.

## 2. Archivos

```
db/postgres/migraciones/
  058_qr_dinamico.sql             secreto en enlace_registro, parámetros, fn_codigo_qr, fn_codigo_qr_valido
  059_pase_registro.sql           fn_emitir_pase, fn_validar_pase; los SP de registro de la v8 exigen el pase
  060_enlace_proyeccion.sql       tabla enlace_proyeccion, sp_emitir_enlace_proyeccion, sp_resolver_proyeccion

api_catedras/
  Controllers/QrSesionController.cs   + GET {id}/codigo, POST {id}/enlace-proyeccion
  Controllers/RegistroController.cs   + POST pase
  Peticiones/Registro*.cs             + pase
  (servicios y repositorios de QrSesion y Registro: métodos nuevos)

front_catedras/
  routes/registro.py            canje del ?c= al abrir; pase oculto en los formularios
  routes/proyeccion.py          NUEVO blueprint público /proyectar/<token>
  routes/admin.py               /admin/sesiones/<id>/qr y "Generar enlace de proyección"
  templates/proyeccion.html     NUEVA (pantalla completa)
  static/proyeccion.js          NUEVO: refresco del código cada rotación y del contador cada 15 s
  static/vendor/qrcode.min.js   NUEVO (biblioteca fija)
```

## 3. Decisiones de diseño clave

1. **El código se calcula, no se guarda (D63, D74).** No hay una fila por
   minuto: el código es `HMAC(secreto, paso)`, el mismo principio de TOTP (RFC
   6238). `pgcrypto` ya está instalado (lo usa `digest`) y trae `hmac()`.
2. **Pase de 10 minutos (D75).** El código de 60 s alcanza para escanear, no
   para que un externo llene el formulario. El pase separa los dos tiempos.
3. **La proyección tiene su propio secreto (D76).** El token del enlace de
   registro es público (va en el QR). El de proyección no: si alguien lo
   tuviera, podría ver los códigos desde su casa.
4. **La validación vive en la BD.** Los procedimientos de registro de la v8 ganan
   un parámetro `p_pase`. Cambia la firma, así que se borra (`DROP`) la versión
   anterior en la misma migración.

## 4. Frontend

```
GET  /registro/<token>?c=XXXX      POST /api/Registro/pase -> pase oculto; redirige a /registro/<token> sin ?c
GET  /proyectar/<token_proy>       pantalla de proyección pública (solo el QR)
GET  /admin/sesiones/<id>/qr       misma pantalla, con login de admin
POST /admin/sesiones/<id>/enlace-proyeccion   genera y muestra el enlace para copiarlo
```

El pase se guarda en la sesión de Flask (cookie firmada), no en la URL (RNF-V8-2).

## 5. Docker

Sin servicios nuevos. Se montan las migraciones `z058_` a `z060_`.
