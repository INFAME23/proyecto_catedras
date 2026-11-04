# Quickstart — Versión 8: registro de asistencia sin login, por tipo de usuario

> ⚠️ **BORRADOR · por construir** — ver nota de [2_spec.md](2_spec.md). Los ids
> de ejemplo se confirman en la Fase 62 contra una BD recién creada.

---

## 1. Levantar

**BD recién creada:**

```powershell
docker compose down -v
docker compose up -d --build
```

**Volumen ya creado:** aplica las migraciones 054 a 057 a mano, en orden.

```bash
for n in 054_tipos_registro_v8 055_geocerca_sede 056_sp_registro_por_tipo 057_apagado_v71; do
  docker compose cp db/postgres/migraciones/$n.sql postgres:/tmp/m.sql
  docker compose exec -T postgres psql -U sa -d bdcatedras_postgres_local -v ON_ERROR_STOP=1 -f /tmp/m.sql
done
```

## 2. Preparar una sesión abierta

1. Entra a `http://localhost:5000/login` con el usuario administrador.
2. En `/admin/sedes/SAN_BENITO` carga las coordenadas de la sede (p. ej.
   `6.2297, -75.5869`) y el radio (300).
3. En `/admin`, busca una sesión `PRESENCIAL` de hoy en `SAN_BENITO` (o crea una
   que termine dentro de 10 minutos) y emite su enlace o QR.
4. Copia el token del enlace.

## 3. Verificación de la API — 7 pasos

> **Automatizado:** `pruebas/verificar_v8.ps1` corre estos pasos.
> `pruebas/correr_todo_v8.ps1` crea una BD nueva y corre todo: SQL, prueba de
> capas, regresión de la v5 y `verificar_v8.ps1`.

1. `GET /api/Registro/contexto?token=<token>` → 200 con `modalidad: "PRESENCIAL"`
   y `ubicacion: "obligatoria"`.
2. `POST /api/Registro/estudiante` con un `id_asis` del maestro y las coordenadas
   de la sede → 200 `registrado`.
3. Repite el paso 2 → 200 `ya_registrado` y el mismo `id_registro`.
4. El mismo `POST` con otro estudiante y `latitud: 6.30` (unos 8 km) → 500 con
   `detalle` "V8: estás a … m de la sede…".
5. `POST /api/Registro/docente` con cédula nueva, facultad válida y
   `acepta_datos: true` → 200. `GET /api/Asistente?q=<apellido>` muestra a la
   persona con `fk_facultad`.
6. `POST /api/Registro/externo` con `acepta_datos: false` → 422.
7. `POST /api/Usuario/login` con un usuario de rol `ESTUDIANTE` → 401; con el
   admin → 200.

## 4. Front, a mano

1. Desde el celular (por https o con `URL_PUBLICA`, ver el README de la v7.1),
   abre `/registro/<token>`: aparecen los 3 botones.
2. Haz los tres registros desde el celular; en la presencial, el navegador pide
   la ubicación.
3. En una sesión `VIRTUAL` no se pide la ubicación.
4. `/estudiante` y `/ponente` → 404. `/catedras` abre sin sesión.

## 5. Problemas frecuentes

| Síntoma | Causa |
|---|---|
| `42883: function fn_registrar_estudiante_api does not exist` | Falta la migración 056 |
| El docente falla con `RN-07` | Falta la migración 054 (`DOCENTE` todavía exige programa) |
| La ubicación siempre se rechaza desde `http://192.168…` | El navegador solo entrega la ubicación en https o localhost |
| La geocerca nunca bloquea | La sede no tiene coordenadas cargadas |
