# Quickstart — Versión 2: estructura académica y catálogo de cátedras

> **Documento 7 de 8** del spec kit. Validación del sistema ya
> construido en v2 (API + front), sobre lo que ya dejó la v1.
>
> **Nota de alcance (v2):** mismo backend que v1 (`api` +
> `postgres`, sin cambios de infraestructura — ver
> [6_contracts.md](6_contracts.md)). Hoy la API expone 15 recursos: los
> 10 de v1 más `Facultad`, `Dependencia`, `ProgramaAcademico`,
> `AliasPrograma` y `Catedra`. El front (`front_catedras/`) sigue aparte
> de `docker compose`, con el catálogo público y el panel de
> administración ya extendidos a las 5 tablas nuevas.

---

## 1. Levantar

Sin cambios respecto a v1
([version1/7_quickstart.md](../version1/7_quickstart.md) §1):

```powershell
git clone https://github.com/INFAME23/proyecto_catedras.git
cd proyecto_catedras
docker compose up -d --build
```

**Retrofit — si tu volumen de PostgreSQL ya existía** (lo creaste antes
de agregar `db/postgres/migraciones/`): initdb no vuelve a correr, así
que el SP `sp_guardar_programa_academico` no existe todavía. Dos
opciones:

```powershell
# Opción A: reset completo (borra los datos de prueba y resiembra todo)
docker compose down -v
docker compose up -d --build

# Opción B: aplicar solo la migración, conservando los datos
Get-Content db/postgres/migraciones/001_sp_guardar_programa_academico.sql | docker compose exec -T postgres psql -U sa -d bdcatedras_postgres_local
```

Sin el SP, `POST /api/ProgramaAcademico` responde 500 con
`procedure catedras.sp_guardar_programa_academico(...) does not exist`.

## 2. Verificación de la API — 11 pasos

```powershell
docker compose ps
# Esperado: mi_v1_catedras_api, mi_v1_catedras_postgres corriendo (Up / healthy)
```

1. **http://localhost:8142/swagger** → ahora muestra **15** recursos;
   los 5 nuevos son `Facultad`, `Dependencia`, `ProgramaAcademico`,
   `AliasPrograma` (solo 4 verbos — sin `PUT`/`PATCH`, ver
   [6_contracts.md](6_contracts.md) §3) y `Catedra`.
2. **GET /api/Facultad** → 9 filas, con `USB` como única raíz
   (`fk_facultad_padre: null`). **GET /api/Dependencia** → 8 filas, con
   `VICE_ACAD` y `BIENESTAR` como raíces.
3. **GET /api/ProgramaAcademico** → 105 filas; **GET
   /api/ProgramaAcademico/M0286** → `"Ingenieria de Datos y Software"`,
   `fk_facultad: "F_ING"`. **GET /api/AliasPrograma** → 3 filas, las
   tres con `fk_programa: "M0286"`.
4. **GET /api/Catedra?limite=5** → 5 de las 5502 cátedras, con
   `total: 5502`; **GET /api/Catedra/1** → `id_evento_asis:
   "000023636"`, `nombre_asis: "PELICULA:LA MIRADA DEL AMOR"` (ya
   calculado por la BD, sin que ningún `POST` lo haya enviado).
5. **Autorreferencia:** `POST /api/Facultad` con
   `{"id_facultad": "X", "fk_facultad_padre": "X", ...}` → `400`
   ("Una facultad no puede ser su propio padre"), sin tocar la BD.
6. **Formato y listas cerradas:** `POST /api/ProgramaAcademico` con
   `codigo: "m0001"` (minúscula) → `422`; con `nivel: "TECNICO"` → `422`.
   `POST /api/Catedra` con `id_evento_asis: "12345"` (5 dígitos, no 9)
   → `422`.
7. **`SET NULL`, verificado (no falla):**
   `DELETE /api/Facultad/F_ING` → `200`; luego
   `GET /api/ProgramaAcademico/M0286` → `fk_facultad: null`. Repetir el
   mismo patrón con `DELETE /api/Dependencia/DESA_HUM` → `200`; la
   única cátedra de ejemplo que la usaba queda con `fk_dependencia:
   null`. **Importante:** este paso modifica datos de ejemplo; si se
   sigue probando después, usar el reset del paso 10 para dejar todo
   como estaba.
8. **`RESTRICT`, verificado (si falla):** `DELETE
   /api/ProgramaAcademico/M0286` → `500` (bloqueado por sus 58 filas en
   `programa_asistente`, una tabla de v3 que la BD ya trae sembrada —
   ver [5_data_model.md](5_data_model.md) §2.3). Sus 3 `AliasPrograma`
   siguen intactos: la cascada hacia ellos nunca llegó a ejecutarse.
9. **`CASCADE` oculto hacia v4 — probar SOLO con una cátedra segura:**
   `DELETE /api/Catedra/5502` → `200`, sin efectos en otras tablas (es
   una de las 5 cátedras de ejemplo sin sesiones). **No** probar este
   verbo contra `id_catedra` 1-5497: tienen sesiones de v4 ya
   sembradas y `DELETE` las borraría en cascada sin aviso (ver
   [5_data_model.md](5_data_model.md) §2.5). Para probar el verbo sobre
   datos propios, crear una cátedra con `POST` primero y borrar esa.
10. **Reset** (recomendado después de los pasos 7-9, que modifican
    datos de ejemplo): `docker compose down -v && docker compose up -d
    --build` → las 15 tablas vuelven a sus datos originales.

11. **Maestro-detalle (retrofit, criterio 11):** `POST
    /api/ProgramaAcademico` con
    `{"codigo":"Z0001","nombre":"Prueba","alias":[{"texto_normalizado":"prueba uno"},{"texto_normalizado":"prueba dos","origen":"CARGA"}]}`
    → `200`; `GET /api/AliasPrograma` → aparecen los 2 alias con
    `fk_programa: "Z0001"`. Luego el mismo body con `codigo: "Z0002"` y
    un alias `"ingenieria de datos"` (ya existe) → `500`, y
    `GET /api/ProgramaAcademico/Z0002` → `404`: el SP revirtió todo.
    Limpieza: `DELETE /api/ProgramaAcademico/Z0001` (sus alias se van en
    cascada).

**Prueba de capas (criterio de aceptación 8):**
```powershell
cd api_catedras
dotnet run --project pruebas
```
Ahora cubre las **15** entidades con repositorios FALSOS en memoria,
incluida la verificación de autorreferencia de `Facultad`/`Dependencia`.
Termina con código 0 si todo pasa.

## 3. Frontend (`front_catedras/`, aparte del `docker compose` de arriba)

Arranque sin cambios respecto a v1
([version1/7_quickstart.md](../version1/7_quickstart.md) §3):

```powershell
cd front_catedras
python -m venv venv
venv\Scripts\activate
pip install -r requirements.txt
python run.py
```

Con la API corriendo (§1-2 de aquí):

1. **http://localhost:5000** → la plantilla ya integrada (RF92 de
   [2_spec.md](2_spec.md)): mismo navbar, ahora con los endpoints
   corregidos; ningún enlace rota a un `BuildError` de Flask.
2. **http://localhost:5000/catedras** → catálogo público: lista
   paginada de las 5502 cátedras (24 por página, o el tamaño que fije
   `3_plan.md` §4.7), con un buscador de texto por `nombre` que filtra
   sobre el catálogo completo, no solo sobre la página visible.
   Buscar `"amor"` debe incluir `000023636` ("PELICULA:LA MIRADA DEL
   AMOR"), esté en la página que esté.
3. **http://localhost:5000/catedras/1** → detalle de la cátedra
   `000023636`: nombre completo, tipo de evento (`CAAB`) y, si
   `fk_dependencia` no es nulo, el nombre de la dependencia.
4. **http://localhost:5000/admin/** → 5 tarjetas nuevas
   (`Facultad`, `Dependencia`, `ProgramaAcademico`, `AliasPrograma`,
   `Catedra`); los formularios de `Facultad`/`Dependencia` muestran un
   `<select>` con las filas existentes como posible padre.
   `AliasPrograma` solo ofrece crear y eliminar (sin "editar"), acorde
   a RF80-83.
5. **Datos mock eliminados (RF93):** apagar la API
   (`docker compose stop api`) y recargar `/catedras` → debe mostrar un
   error claro, nunca una lista de ejemplo inventada por el front.

## 4. Problemas frecuentes

| Síntoma | Diagnóstico |
|---|---|
| `DELETE /api/Facultad/{id}` responde 500 en vez de 200 | Revisar que no se esté probando sobre una facultad que, además de hijos/programas, tenga alguna otra FK que esta versión no documentó — repasar [5_data_model.md](5_data_model.md) §1 antes de asumir un bug |
| `DELETE /api/Catedra/1` "borró algo que no pedí" | Es `ON DELETE CASCADE` hacia `sesion` (v4, ya sembrada) — comportamiento esperado y documentado, no un bug; usar `id_catedra` 5498-5502 para pruebas limpias (§2, paso 9) |
| `GET /catedras` del front tarda varios segundos | Está trayendo las 5502 filas en una sola llamada a propósito (ADR D13 de [4_research.md](4_research.md)); no es un error, es el costo aceptado de esa decisión |
| `POST /api/AliasPrograma` funciona pero no hay forma de editarlo | Correcto: esta tabla no tiene `PUT`/`PATCH` (RF80-83); para corregir un alias, se borra y se crea de nuevo |
| El navbar del front da `werkzeug.routing.BuildError` | La integración de la plantilla (RF92) dejó algún `url_for` sin corregir — revisar `templates/partials/navbar.html` contra los blueprints reales de `app.py` |
| Todo roto | `docker compose down -v && docker compose up -d --build` (API); para el front, confirmar que `API_BASE_URL` siga en `http://localhost:8142/api` |
