# Quickstart — Proyecto completo

> **Documento 7 de 8** del spec kit raíz. Validación del sistema ya
> construido.
>
> **Nota de alcance (v1):** sin `front`, sin API doble, sin MariaDB/SQL
> Server/phpMyAdmin — eso es la plantilla de otro proyecto de referencia
> (ver Documento 6). La v1 real de Construcción 1 es **un solo servicio
> `api` + PostgreSQL**, y lo único que se puede crear/editar/borrar es
> `sede`.

---

## 1. Levantar

```powershell
git clone https://github.com/INFAME23/proyecto_catedras.git
cd proyecto_catedras
docker compose up -d --build     # primera vez: varios minutos
```

## 2. Verificación en 8 pasos

```powershell
docker compose ps
# Esperado: api, postgres corriendo (Up / healthy)
```

1. **http://localhost:8000/docs** → abre el Swagger UI de la API con el
   recurso `sede` (`GET/POST/PUT/DELETE /api/sede`,
   `GET/PUT/DELETE /api/sede/{id_sede}`).
2. **GET /api/sede** → responde con las 3 sedes de ejemplo (`SAN_BENITO`,
   `BELLO`, `VIRTUAL`).
3. **CRUD completo**: desde `/docs`, crear una sede nueva
   (`id_sede`, `nombre`, `direccion` opcional, `es_virtual`, `activo`),
   editarla (`PUT`) y borrarla (`DELETE`) — cada paso responde `2xx` y el
   `GET` posterior refleja el cambio.
4. **Validación de la API**: enviar un `POST` con `nombre` vacío o sin
   `id_sede` → `422` con el detalle del campo que falló, **sin** llegar a
   tocar la BD.
5. **Validación de la BD**: crear dos sedes con el mismo `id_sede` (o el
   mismo `nombre`) → la segunda falla con `500` y el mensaje del motor
   (viola `pk_sede` o `uq_sede_nombre`).
6. Persistencia: crear una sede → `docker compose down` → `up -d` → sigue
   ahí.
7. Reset: `docker compose down -v` → `up -d` → solo quedan las 3 sedes
   originales del script.
8. Herramienta externa: pgAdmin/DBeaver a `localhost:15432` /
   `sa`/`Construccion123!` / BD `bdcatedras_postgres_local` → ver la
   tabla `sede` (las otras 36 tablas del esquema `catedras` también están
   ahí, pero son territorio de otras versiones).

Opcional: `F1 → Dev Containers: Reopen in Container` → SQLTools con la
conexión a `postgres:5432` lista.

## 3. Problemas frecuentes

| Síntoma | Diagnóstico |
|---|---|
| Badge rojo / `/docs` no responde | `docker compose logs api` |
| localhost:8000 no abre | `docker compose ps` + `docker compose logs api` |
| Puerto ocupado (8000/15432) | Cerrar el otro programa o cambiar el mapeo en compose |
| Cambié `script_bdcatedras.sql` y no pasa nada | Solo corre con volumen vacío: `down -v` + `up -d` |
| `POST /api/sede` responde 500 en vez de 422 | La API no está validando antes del `INSERT`; revisar el modelo/anotaciones, no la BD |
| Todo roto | `docker compose down -v` + `docker compose up -d --build` |