# Quickstart — Versión 10: calendario público y solicitudes de ponentes

> ⚠️ **BORRADOR · por construir** — ver nota de [2_spec.md](2_spec.md).

## 1. Levantar

**BD recién creada:** `docker compose down -v` y luego `docker compose up -d --build`.

**Volumen ya creado:** aplica las migraciones 061 a 063 a mano, después de las de la v8.

```bash
for n in 061_franja_disponible 062_solicitud_catedra 063_aprobar_solicitud; do
  docker compose cp db/postgres/migraciones/$n.sql postgres:/tmp/m.sql
  docker compose exec -T postgres psql -U sa -d bdcatedras_postgres_local -v ON_ERROR_STOP=1 -f /tmp/m.sql
done
```

## 2. Verificación de la API (automatizada en `pruebas/verificar_v10.ps1`)

1. `POST /api/FranjaDisponible/plantilla` para un mes, de lunes a viernes, a
   las 07:00 y 18:00, en `SAN_BENITO` → `creadas` > 0. Repetirla → `creadas: 0`.
2. `GET /api/FranjaDisponible/calendario?anio=…&mes=…` → días con franjas `LIBRE`.
3. `POST /api/SolicitudCatedra` sobre una franja → `PENDIENTE`. La franja queda `EN_REVISION`.
4. Otra solicitud a la misma franja → 500 «V10: esa franja ya no está disponible».
5. `GET /api/SolicitudCatedra/pendientes` → `{total: 1}`.
6. `POST …/{id}/aprobar` con un `id_evento_asis` nuevo de 9 dígitos → `id_catedra` e `id_sesion`.
   - `GET /api/Ponencia?fkSesion=` muestra al ponente.
   - La franja queda `OCUPADA`.
7. Aprobarla otra vez → 500 «V10: la solicitud ya fue resuelta».
8. Otra solicitud y `…/rechazar` sin motivo → 422; con motivo → `RECHAZADA` y la franja vuelve a `LIBRE`.

## 3. Front, a mano

1. Entra a `/calendario` sin sesión: verde, rojo y gris; cambia de mes.
2. Pincha una franja libre, llena el formulario y envía. Con
   `CORREO_MODO_CONSOLA=1`, el correo "recibida" sale en el log del front.
3. En `/admin` aparece el aviso con la solicitud. Apruébala desde
   `/admin/solicitudes/<id>` y revisa el correo de aprobación en el log.

## 4. Problemas frecuentes

| Síntoma | Causa |
|---|---|
| `data type bigint has no default operator class for access method "gist"` | Falta `CREATE EXTENSION IF NOT EXISTS btree_gist` |
| Aprobar falla con «no hay periodo académico» | Crea el periodo en `/admin/periodos` (D80) |
| Aprobar falla con `chk_catedra_asis` | El `id_evento_asis` debe tener exactamente 9 dígitos |
