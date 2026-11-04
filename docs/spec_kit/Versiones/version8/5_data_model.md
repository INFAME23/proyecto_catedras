# Modelo de datos — Versión 8: registro de asistencia sin login, por tipo de usuario

> ⚠️ **BORRADOR · por construir** — ver nota de [2_spec.md](2_spec.md).
> No se edita `script_bdcatedras.sql` (Artículo 5). Todo va en las migraciones
> **054–057**, idempotentes (`IF NOT EXISTS`, `ON CONFLICT DO NOTHING`,
> `CREATE OR REPLACE`), montadas en `docker-compose.yml` como `z054_` … `z057_`.

---

## 1. Lo que ya existe y se reutiliza

| Objeto | De dónde | Para qué en la v8 |
|---|---|---|
| `asistente`, `documento_asistente`, `vinculacion_asistente`, `consentimiento_datos` | Script (v3) | Alta de docentes y externos |
| `enlace_registro` (token, ventana, `revocado_en`, `requiere_inscripcion`) | Script (v5) + 051 | Saber a qué sesión entra el registro |
| `registro_asistencia` (+ `latitud` y `longitud` de 045) | Script (v5) | El registro |
| `uq_registro_sesion_asis UNIQUE (fk_sesion, fk_asistente)` | Script | RF205, sin migración |
| `trg_registro_validar` (RN-15, RN-22, RN-07, RN-38) | Script | Ventana, fotos de vinculación y programa, cupo |
| `trg_registro_inscripcion` | 051 | No actúa si `requiere_inscripcion = false` |
| `fn_resolver_asistente(text)` | Script | Resolver el asistente por `id_asis` o documento |
| `chk_registro_clave` | Script | Obliga a crear una clave técnica (D71) |
| Parámetros `PERMITE_EXTERNOS` y `VERSION_POLITICA_DATOS` | Script | RF203 y RF217 |

## 2. Migración 054 — tipos de registro (D56, D57, D72)

```sql
-- Tipo EXTERNO (no existía: hay ESTUDIANTE, DOCENTE, ADMINISTRATIVO, EGRESADO, RUTAS_PAZ y COLEGIO)
INSERT INTO catedras.tipo_vinculacion (id_tipo_vinculacion, nombre, es_interno, exige_programa, activo)
VALUES ('EXTERNO', 'Externo / comunidad', false, false, true)
ON CONFLICT (id_tipo_vinculacion) DO NOTHING;

-- D56: el docente no da programa
UPDATE catedras.tipo_vinculacion SET exige_programa = false WHERE id_tipo_vinculacion = 'DOCENTE';

-- D72: facultad y procedencia de la persona; foto de la facultad en el registro
ALTER TABLE catedras.asistente
    ADD COLUMN IF NOT EXISTS fk_facultad varchar(10) REFERENCES catedras.facultad(id_facultad)
        ON UPDATE CASCADE ON DELETE SET NULL,
    ADD COLUMN IF NOT EXISTS procedencia varchar(150);
ALTER TABLE catedras.registro_asistencia
    ADD COLUMN IF NOT EXISTS fk_facultad_snapshot varchar(10) REFERENCES catedras.facultad(id_facultad)
        ON UPDATE CASCADE ON DELETE SET NULL,
    ADD COLUMN IF NOT EXISTS distancia_sede_metros integer;
```

## 3. Migración 055 — geocerca por sede (D61)

```sql
ALTER TABLE catedras.sede
    ADD COLUMN IF NOT EXISTS latitud      numeric(9,6),
    ADD COLUMN IF NOT EXISTS longitud     numeric(9,6),
    ADD COLUMN IF NOT EXISTS radio_metros integer CHECK (radio_metros IS NULL OR radio_metros > 0);
-- CHECK: latitud y longitud van juntas (como chk_registro_geo_par)

INSERT INTO catedras.parametro (clave, valor, tipo_dato, descripcion)
VALUES ('GEOCERCA_RADIO_METROS', '300', 'ENTERO',
        'v8. Radio por defecto, en metros, alrededor de la sede en que se acepta un registro presencial')
ON CONFLICT (clave) DO NOTHING;

-- Haversine en SQL puro (sin PostGIS)
CREATE OR REPLACE FUNCTION catedras.fn_distancia_metros(lat1 numeric, lon1 numeric,
                                                        lat2 numeric, lon2 numeric)
RETURNS integer LANGUAGE sql IMMUTABLE AS $$ … 6371000 * 2 * asin(sqrt(…)) … $$;
```

Además, `sp_crear_sede`, `sp_actualizar_sede`, `sp_consultar_sede` y
`sp_listar_sede` (de la migración 005) se reemplazan con `CREATE OR REPLACE`
para incluir las 3 columnas nuevas. Cambia la firma, así que se hace
`DROP PROCEDURE` de la firma vieja dentro de la misma migración.

## 4. Migración 056 — procedimientos de registro (D55, D57, D60, D61, D71)

Un procedimiento por tipo y, para cada uno, una función `fn_*_api` que lo
envuelve y devuelve una fila (el molde de `fn_validar_clave_geo_api`).

| Procedimiento | Entrada | Qué hace, en orden |
|---|---|---|
| `sp_contexto_registro(p_token, INOUT p_json)` | token | Devuelve sesión, cátedra, modalidad, `ubicacion` (`obligatoria` / `opcional` / `no_aplica`), `tiene_encuesta` y `abierto` |
| `sp_registrar_estudiante(p_token, p_id_asis, p_lat, p_lon, p_ip, p_ua, INOUT p_id_registro, INOUT p_estado)` | ID | 1. Enlace vigente (RN-15) · 2. Asistente con ese `id_asis` y `activo` (si no hay → `no_data_found`) · 3. Ubicación por modalidad y geocerca · 4. Si ya está registrado → `p_estado = 'ya_registrado'` y sale · 5. Clave técnica (D71) · 6. `INSERT registro_asistencia` |
| `sp_registrar_docente(p_token, p_tipo_doc, p_numero, p_nombres, p_apellidos, p_correo, p_celular, p_fk_facultad, p_es_administrativo, p_lat, p_lon, p_ip, p_ua, INOUT …)` | Formulario | 1. Enlace vigente · 2. Persona por documento vigente; si no existe: `asistente` + `documento_asistente`; si existe: actualizar correo, celular y facultad · 3. Vinculación vigente `DOCENTE`/`ADMINISTRATIVO` (abrir o cambiar) · 4. Consentimiento · 5. Ubicación · 6. Duplicado · 7. Clave técnica · 8. Registro con `fk_facultad_snapshot` |
| `sp_registrar_externo(p_token, p_tipo_doc, p_numero, p_nombres, p_apellidos, p_correo, p_celular, p_procedencia, p_lat, p_lon, p_ip, p_ua, INOUT …)` | Formulario | Igual que el docente, pero con `es_externo = true`, la vinculación `EXTERNO` y la procedencia. Antes de todo revisa `PERMITE_EXTERNOS` |
| `sp_validar_ubicacion_registro(p_id_sesion, p_lat, p_lon, INOUT p_distancia)` | — | Aplica D60 y D61. Lo llaman los tres anteriores |

**Correo del asistente:** se guarda en `correo_institucional` si su dominio está
en `DOMINIOS_INSTITUCIONALES`; si no, en `correo_personal`.

**Mensajes de error** (van al `detalle` del 500):
- `V8: la ubicación es obligatoria en las sesiones presenciales`
- `V8: estás a N m de la sede; debes estar a menos de R m`
- `V8: el registro de externos está deshabilitado`
- `RN-15: el enlace no existe, está revocado o está fuera de su ventana`

## 5. Migración 057 — apagado de la v7.1 (D58)

```sql
-- Cerrar los roles retirados (no se borran usuarios)
-- fecha_fin es DATE y debe ser >= fecha_ini (chk_rol_usuario_fechas)
UPDATE catedras.rol_por_usuario SET fecha_fin = CURRENT_DATE
 WHERE fk_rol IN ('ESTUDIANTE', 'PONENTE') AND fecha_fin IS NULL;

-- Los QR y enlaces nuevos ya no exigen inscripción
ALTER TABLE catedras.enlace_registro ALTER COLUMN requiere_inscripcion SET DEFAULT false;
-- sp_emitir_qr_sesion (053): CREATE OR REPLACE para que inserte requiere_inscripcion = false
```

Las columnas de `rol_por_usuario` son `fk_usuario`, `fk_rol`, `fecha_ini` y
`fecha_fin` (DATE). Como un rol cerrado hoy todavía cuenta como vigente hoy si la
consulta usa `fecha_fin >= CURRENT_DATE`, **el login de la v8 no depende de
esto**: `LoginAsync` exige explícitamente el rol `ADMIN` (RF211). En la Fase 62
se confirma cómo calcula la vigencia `sp_consultar_usuario_por_login`.

## 6. Reglas de esta versión

| Regla | Dónde vive |
|---|---|
| RN-15: ventana y revocación del enlace | `trg_registro_validar` (script) y los procedimientos 056 |
| RN-22: foto de la vinculación y del programa | `trg_registro_validar` (script) |
| D56: docente sin programa | Datos de `tipo_vinculacion` (054) |
| D60 y D61: ubicación y geocerca | `sp_validar_ubicacion_registro` (056) |
| D71: clave técnica | Procedimientos 056 |
| RF205: una asistencia por persona y sesión | `uq_registro_sesion_asis` (script) + chequeo previo en 056 |
| RF217: consentimiento obligatorio | API (422) + inserción en 056 |
