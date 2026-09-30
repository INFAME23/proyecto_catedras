# Quickstart — Versión 1: ApiCatedras + PostgreSQL (+ front)

> **Documento 7 de 8** del spec kit. Validación del sistema ya construido.
>
> **Nota de alcance (v1):** sin segundo motor, sin devcontainer. El
> backend es **un solo servicio `api` (C#/ASP.NET Core) + PostgreSQL**, y
> hoy se puede crear/editar/borrar `Sede`, `Encuesta`, `EstadoProceso` y
> `Modalidad` (6 más están especificadas, pendientes de implementar). El
> frontend `front_catedras/` (Flask) es un proyecto aparte, con su propio
> arranque (§3) — todavía no está dentro de `docker compose`.

---

## 1. Levantar

```powershell
git clone https://github.com/INFAME23/proyecto_catedras.git
cd proyecto_catedras
docker compose up -d --build     # primera vez: varios minutos (descarga SDK + restore NuGet)
```

## 2. Verificación en 8 pasos

```powershell
docker compose ps
# Esperado: mi_v1_catedras_api, mi_v1_catedras_postgres corriendo (Up / healthy)
```

1. **http://localhost:8142/swagger** → abre el Swagger UI de la API con
   los recursos `Sede`, `Encuesta`, `EstadoProceso` y `Modalidad`
   (`GET/POST /api/{Recurso}`, `GET/PUT/PATCH/DELETE /api/{Recurso}/{id}`).
2. **GET /api/Sede** → responde con las 3 sedes de ejemplo (`SAN_BENITO`,
   `BELLO`, `VIRTUAL`).
3. **CRUD completo**: desde `/swagger`, crear una sede nueva
   (`id_sede`, `nombre`, `direccion` opcional, `es_virtual`, `activo`),
   reemplazarla (`PUT`), modificarle un campo (`PATCH`) y borrarla
   (`DELETE`) — cada paso responde `2xx` y el `GET` posterior refleja el
   cambio.
4. **Validación de la API**: enviar un `POST` con `nombre` vacío o sin
   `id_sede` → `422` con el detalle del campo que falló, **sin** llegar a
   tocar la BD.
5. **Validación de la BD**: crear dos sedes con el mismo `id_sede` (o el
   mismo `nombre`) → la segunda falla con `500` y el mensaje del motor
   (viola `pk_sede` o `uq_sede_nombre`).
6. Persistencia: crear una sede → `docker compose down` → `up -d` → sigue
   ahí.
7. Reset: `docker compose down -v` → `up -d --build` → solo quedan las 3
   sedes originales del script.
8. Herramienta externa: pgAdmin/DBeaver a `localhost:15542` /
   `sa`/`Construccion123!` / BD `bdcatedras_postgres_local` → ver la
   tabla `catedras.sede` (las otras 36 tablas del esquema también están
   ahí, pero son territorio de otras versiones).

**Prueba de capas (criterio 6):**
```powershell
cd api_catedras
dotnet run --project pruebas
```
Ejecuta `ServicioSede` con un repositorio FALSO en memoria (sin
PostgreSQL) y termina con código 0 si todas las verificaciones pasan.

## 3. Frontend (`front_catedras/`, aparte del `docker compose` de arriba)

```powershell
cd front_catedras
python -m venv venv
venv\Scripts\activate
pip install -r requirements.txt
python run.py
```

Con la API ya corriendo (paso 1), abre **http://localhost:5000** → la
portada con la identidad de la Universidad San Buenaventura Medellín, y
**http://localhost:5000/admin/** → CRUD completo (crear/editar/eliminar)
de las 4 entidades, hablando por HTTP contra `ApiCatedras`
(`localhost:8142`, configurado en `front_catedras/.env`).

## 4. Problemas frecuentes

| Síntoma | Diagnóstico |
|---|---|
| Badge rojo / `/swagger` no responde | `docker compose logs api` |
| localhost:8142 no abre | `docker compose ps` + `docker compose logs api` |
| Puerto ocupado (8142/15542) | Cerrar el otro programa o cambiar el mapeo en `docker-compose.yml` |
| Cambié `script_bdcatedras.sql` y no pasa nada | Solo corre con volumen vacío: `down -v` + `up -d --build` |
| `POST /api/Sede` responde 500 en vez de 422 | La API no está validando antes del `INSERT`; revisar la petición (`SedeCrear`) y sus anotaciones, no la BD |
| Todo roto | `docker compose down -v` + `docker compose up -d --build` |
