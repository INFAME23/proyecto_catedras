# Guía del estudiante — Proyecto Cátedras, Versión 1

Esta guía te lleva de "acabo de clonar el repo" a "entiendo y sé probar la
v1". La v1 es el CRUD de **una entidad (`Sede`)** contra **PostgreSQL**, con
la arquitectura en capas completa (controlador → servicio → repositorio,
unidos por interfaces de C#).

---

## 1. Requisitos

| Herramienta | Para qué |
|---|---|
| Docker Desktop | Levantar todo con un comando |
| .NET SDK 10 | Solo si quieres compilar/probar fuera de Docker |
| VS Code | Editor (terminal integrada de PowerShell) |
| pgAdmin o DBeaver | Opcional: mirar la base de datos |

---

## 2. Instalación y arranque (un solo comando)

```powershell
cd "ruta\a\Proyecto_catedras"
docker compose up -d --build
```

La primera vez tarda varios minutos: descarga las imágenes, siembra la base
de datos (script de 78k líneas) y hace `dotnet restore` dentro del
contenedor. Cuando termine:

```powershell
docker compose ps
```

Debes ver dos contenedores arriba:

- `mi_v1_catedras_postgres` — `Up (healthy)`
- `mi_v1_catedras_api` — `Up`

> **Puertos con +100.** Este proyecto publica la API en **8142** y
> PostgreSQL en **15542** para convivir con el proyecto del curso en la
> misma máquina. Dentro de los contenedores siguen siendo 8042 y 5432.
> El proyecto Compose se llama `mi_v1_catedras` (contenedores y volúmenes
> propios).

---

## 3. Primer recorrido con Swagger

Abre **http://localhost:8142/swagger**. Verás el recurso `Sede` con sus
verbos. Pruébalos en orden:

1. **GET /** (fuera de Swagger: http://localhost:8142/ ) → JSON de
   diagnóstico con `"version": "v1"`.
2. **GET /api/Sede** → las 3 sedes de ejemplo: `SAN_BENITO`, `BELLO`,
   `VIRTUAL`. Respuesta con sobre `{ tabla, limite, total, datos }`.
3. **GET /api/Sede?limite=2** → exactamente 2 filas.
4. **GET /api/Sede/SAN_BENITO** → esa sede. `GET /api/Sede/NO_VA` → 404
   con `{ estado, mensaje, detalle }`.
5. **POST /api/Sede** con:
   ```json
   { "id_sede": "PRUEBA", "nombre": "Sede de prueba", "direccion": "Calle 1", "es_virtual": false, "activo": true }
   ```
   → 200 `{ estado, mensaje }`.
6. **PUT /api/Sede/PRUEBA** (reemplazo completo, todos los campos):
   ```json
   { "nombre": "Sede de prueba 2", "direccion": null, "es_virtual": true, "activo": false }
   ```
   → 200 `{ filasAfectadas: 1 }`.
7. **PATCH /api/Sede/PRUEBA** (parcial):
   ```json
   { "activo": true }
   ```
   → 200 `{ filasAfectadas: 1 }` (solo cambió `activo`).
8. **DELETE /api/Sede/PRUEBA** → `{ filasEliminadas: 1 }`. Un segundo
   DELETE → 404.

### El contraste PUT vs PATCH (mira esto con calma)

Manda este body **sin `nombre`**:
```json
{ "direccion": "x", "es_virtual": false, "activo": true }
```
- en **PUT /api/Sede/SAN_BENITO** → **422** con `errores:[…]` (PUT exige
  todos los campos).
- en **PATCH /api/Sede/SAN_BENITO** → **200** (PATCH solo toca lo que
  llega).

### La validación es la frontera

- `POST` con `"es_virtual": "texto"` o `"nombre": ""` → **422** con la
  lista de errores, **sin** tocar la base.
- `POST` con un `id_sede` que ya existe → **500** con el mensaje del motor
  en `detalle` (violó `pk_sede`). Lo mismo con un `nombre` repetido
  (`uq_sede_nombre`).

---

## 4. Comandos del día a día

| Necesito… | Comando |
|---|---|
| Levantar todo | `docker compose up -d --build` |
| Ver estado | `docker compose ps` |
| Ver logs de la API | `docker compose logs -f api` |
| Ver logs de la base | `docker compose logs -f postgres` |
| Apagar (conserva datos) | `docker compose down` |
| Apagar y **borrar** la base | `docker compose down -v` |
| Probar las capas sin Docker | `cd api_catedras; dotnet run --project pruebas` |
| Compilar sin Docker | `cd api_catedras; dotnet build` |

**Recarga en caliente:** con los contenedores arriba, edita cualquier
`.cs` de `api_catedras/` y guarda. En `docker compose logs -f api` verás
`dotnet watch` recompilar y reiniciar en segundos. No hace falta
reconstruir.

---

## 5. Mirar la base de datos (opcional)

Con pgAdmin/DBeaver:

| Campo | Valor |
|---|---|
| Host | `localhost` |
| Puerto | `15542` |
| Base | `bdcatedras_postgres_local` |
| Usuario | `sa` |
| Contraseña | `Construccion123!` |

La tabla es `catedras.sede`. Las otras 36 tablas del esquema `catedras`
también están, pero son territorio de versiones posteriores: **el código
de la v1 solo puede nombrar `sede`**.

```sql
SELECT * FROM catedras.sede;
```

---

## 6. Reset completo

```powershell
docker compose down -v
docker compose up -d --build
```

`down -v` borra el volumen `mi_v1_catedras_pgdata`; al volver a arrancar,
el script se ejecuta otra vez y quedan solo las 3 sedes originales.

---

## 7. Cómo está armado el código (mapa rápido)

```
HTTP  →  SedeController        Controllers/SedeController.cs   (traduce a códigos HTTP)
      →  IServicioSede         Servicios/IServicioSede.cs      (contrato de negocio)
      →  ServicioSede          Servicios/ServicioSede.cs       (reglas: límite>0, PATCH no vacío, 404)
      →  IRepositorioSede      Repositorios/IRepositorioSede.cs (contrato de datos)
      →  RepositorioSedePostgres  Repositorios/RepositorioSedePostgres.cs  (Dapper + SQL a mano)
      →  PostgreSQL
```

- La **validación de forma** vive en `Peticiones/` (una clase por verbo):
  `SedeCrear` (POST), `SedeReemplazo` (PUT), `SedeActualizar` (PATCH).
- El **modelo** (`Modelos/Sede.cs`) es la entidad = una fila de la tabla.
- **Solo `Program.cs`** conoce clases concretas (el "ensamblador" de la
  inyección de dependencias). Todo lo demás recibe interfaces.
- `pruebas/` prueba `ServicioSede` con un repositorio falso en memoria,
  **sin** PostgreSQL: la prueba de que las capas están bien separadas.

Para el detalle de cada decisión, lee `3_plan.md` y `4_research.md`.
