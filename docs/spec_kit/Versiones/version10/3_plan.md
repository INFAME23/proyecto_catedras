# Plan técnico — Versión 10: calendario público y solicitudes de ponentes

> ⚠️ **BORRADOR · por construir** — ver nota de [2_spec.md](2_spec.md).

## 1. Stack

Sin cambios. El calendario se dibuja con HTML, CSS y JS propios en
`static/calendario.js`, sin bibliotecas nuevas. La v7.1 ya tiene
`ponente_disponibilidad.js` como referencia.

## 2. Archivos

```
db/postgres/migraciones/
  061_franja_disponible.sql      tabla + EXCLUDE por sede + 5 SP por verbo + sp_plantilla_franjas + sp_calendario_franjas
  062_solicitud_catedra.sql      tablas solicitud_catedra y perfil_ponente + sp_crear/listar/consultar_solicitud
  063_aprobar_solicitud.sql      sp_aprobar_solicitud (reutiliza sp_guardar_catedra 042 y sp_guardar_asistente 002) + sp_rechazar_solicitud

api_catedras/
  Controllers/FranjaDisponibleController.cs     NUEVO (CRUD + plantilla + calendario)
  Controllers/SolicitudCatedraController.cs     NUEVO (crear, listar, obtener, pendientes, aprobar, rechazar)
  Modelos/, Peticiones/, Servicios/, Repositorios/   capas de las 2 entidades
  Program.cs                                    registro en el ensamblador

front_catedras/
  routes/calendario.py            NUEVO blueprint público: /calendario, /calendario/solicitar
  routes/admin.py                 /admin/franjas, /admin/solicitudes (+ aviso en el panel)
  templates/calendario.html, calendario_solicitud.html, calendario_enviada.html      NUEVAS
  templates/admin/franja_*.html, solicitud_lista.html, solicitud_detalle.html        NUEVAS
  static/calendario.js            NUEVO
  servicios/correo.py             + enviar_solicitud_recibida / _aprobada / _rechazada
```

## 3. Decisiones clave

1. **La franja tiene estado** (`LIBRE`, `EN_REVISION`, `OCUPADA` y `BLOQUEADA`)
   y apunta a la solicitud o a la sesión que la ocupa. Así el calendario es una
   sola consulta.
2. **Todo lo de aprobar va en un `CALL`** (Regla 1). Se reutilizan `sp_guardar_catedra`
   (042) y `sp_guardar_asistente` (002); no se reescriben.
3. **El `id_evento_asis` lo digita el admin al aprobar** (D78): la tabla
   `catedra` lo exige y debe ser el del evento creado en ASIS.
4. **Datos del ponente en `perfil_ponente`** (D77): ni `asistente` ni `ponencia`
   tienen perfil profesional ni dirección.
5. **Correo desde el front:** el envío ya vive en `servicios/correo.py`; la API
   no manda correos.

## 4. Frontend

```
GET  /calendario?anio=&mes=&sede=     cuadrícula (público)
GET  /calendario/solicitar/<franja>   formulario (público)
POST /calendario/solicitar/<franja>   POST /api/SolicitudCatedra -> correo "recibida" -> calendario_enviada.html
GET  /admin/franjas                   lista + "Crear franja" + "Aplicar plantilla"
GET  /admin/solicitudes               bandeja (filtro por estado)
GET  /admin/solicitudes/<id>          detalle + formulario aprobar (id_evento_asis, tipo de evento…) / rechazar (motivo)
```

El panel `/admin` agrega la categoría "Solicitudes de ponentes", con el contador
de pendientes (RF241).

## 5. Docker

Sin servicios nuevos. Se montan `z061_` a `z063_`.
