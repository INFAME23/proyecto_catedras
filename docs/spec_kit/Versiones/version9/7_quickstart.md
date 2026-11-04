# Quickstart — Versión 9: QR dinámico y control por modalidad

> ⚠️ **BORRADOR · por construir** — ver nota de [2_spec.md](2_spec.md).

## 1. Levantar

**BD recién creada:**

```powershell
docker compose down -v
docker compose up -d --build
```

**Volumen ya creado:** aplica las migraciones 058 a 060 a mano, después de las de la v8.

```bash
for n in 058_qr_dinamico 059_pase_registro 060_enlace_proyeccion; do
  docker compose cp db/postgres/migraciones/$n.sql postgres:/tmp/m.sql
  docker compose exec -T postgres psql -U sa -d bdcatedras_postgres_local -v ON_ERROR_STOP=1 -f /tmp/m.sql
done
```

## 2. Verificación de la API (automatizada en `pruebas/verificar_v9.ps1`)

1. Emite el QR de una sesión con la ventana abierta, desde `/admin` o con
   `POST /api/QrSesion`. En la BD, el enlace tiene `secreto IS NOT NULL`.
2. `GET /api/QrSesion/{id}/codigo` → `codigo` y `segundos_restantes`.
3. `POST /api/Registro/pase` con ese código → 200 `pase`.
4. Espera 2 minutos y canjea el mismo código → 500 `V9: el código del QR ya expiró…`.
5. `POST /api/Registro/estudiante` con el pase del paso 3 → 200 `registrado`,
   aunque el código ya haya cambiado.
6. El mismo `POST` sin pase → 500 `V9: el pase no es válido…`.
7. `POST /api/QrSesion/{id}/enlace-proyeccion` → `token_proyeccion`.
   `GET /api/QrSesion/proyeccion/{token}` → `id_sesion`. Con un token inventado → 404.

## 3. Front, a mano

1. Abre `/admin/sesiones/<id>/qr` en un portátil conectado a un proyector.
2. Escanea con un celular: abre el formulario sin `?c=` en la barra de direcciones.
3. Toma un pantallazo, espera 3 minutos y escanéalo: aparece «Este código ya expiró».
4. Genera el enlace de proyección y ábrelo en una ventana privada, sin login:
   muestra el QR de esa sesión.

## 4. Problemas frecuentes

| Síntoma | Causa |
|---|---|
| Todos los códigos "expiran" al instante | El reloj del contenedor de postgres está desfasado; revisa con `SELECT now()` |
| `function hmac(text, bytea, unknown) does not exist` | Falta castear: `hmac(p::text::bytea, secreto, 'sha256')` |
| El QR no se ve en el proyector | `static/vendor/qrcode.min.js` no está copiado |
