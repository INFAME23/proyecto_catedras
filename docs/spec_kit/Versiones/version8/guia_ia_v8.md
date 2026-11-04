# Guía IA — Versión 8: registro de asistencia sin login, por tipo de usuario

> **Qué es este documento.** La guía para construir, revisar o verificar la
> **v8** con un asistente de IA. No crea reglas nuevas: aterriza a la v8 las
> que ya existen.
>
> **Jerarquía ante un conflicto:**
> [constitución](../../1_constitution.md) › [guía IA general](../../guia_ia.md)
> › spec kit v8 (`2_spec.md` … `8_tasks.md`) › esta guía.
> Si el spec contradice la BD real, **gana la BD** y se registra un ADR nuevo.
>
> **Estado de partida (2026-11-04):**
> - v7.1 construida (migraciones 001–053);
> - v8 solo en papel;
> - decisiones D55–D70 tomadas ([../decisiones_v8_v11.md](../decisiones_v8_v11.md));
> - D71–D73 propuestas en [4_research.md](4_research.md).

---

## 1. Prompt de arranque (cópialo tal cual)

```
Vas a trabajar la versión 8 del Proyecto Cátedras. Lee, en este orden:
docs/spec_kit/1_constitution.md, docs/spec_kit/guia_ia.md,
docs/spec_kit/Versiones/plan_v8_v11.md y todo docs/spec_kit/Versiones/version8/.
Reglas: la BD manda (no edites db/postgres/script_bdcatedras.sql; todo cambio en
una migración nueva desde la 054), arquitectura Controller -> Servicio ->
Repositorio (CALL catedras.sp_...), JSON en snake_case, todo en español y
comentado para principiantes. No contradigas los ADR D55-D73.
Trabaja SOLO la fase que te indique y, al terminar, dime qué verificaste y con
qué comando.
```

## 2. La v8 en una página

- Hay **3 registros públicos**:
  - el **estudiante** da su ID;
  - el **docente o administrativo** llena cédula, nombres, apellidos, correo y facultad;
  - el **externo** llena cédula, nombres, apellidos, correo y procedencia.
- Cada registro es **un solo `CALL`**, que crea a la persona si es nueva y registra la asistencia.
- **Ubicación:** obligatoria en presencial, con geocerca de 300 m; opcional en
  híbrida; no se pide en virtual.
- **Solo el admin inicia sesión.** `/estudiante`, `/ponente` y
  `/ponente/catedras` se apagan; no se borran.
- **Sin clave por correo.** Para cumplir `chk_registro_clave` se crea una clave
  técnica ya usada (D71).

## 3. Lo que la IA tiende a "arreglar" mal

| Tentación | Por qué no |
|---|---|
| Volver a pedir la clave por correo "por seguridad" | D55: el equipo lo decidió. La seguridad llega con el QR dinámico de la V9 |
| Quitar `chk_registro_clave` o registrar con `origen = 'MANUAL'` | Lo primero edita el script (Artículo 5); lo segundo falsea las estadísticas. Se usa la clave técnica (D71) |
| Borrar las tablas, las rutas o los SP de la v7.1 | D58: se apagan sin borrar |
| Validar el ID como solo números | D59: hay IDs reales como `1000513C` |
| Partir "nombre completo" en nombres y apellidos | D73: dos campos en el formulario |
| Hacer la geocerca solo en JavaScript | Se valida en la BD; el front se puede saltar |
| Usar PostGIS | Haversine en SQL puro (`fn_distancia_metros`); la imagen `postgres:16-alpine` no trae PostGIS |
| Crear el asistente con un POST y registrar con otro | Regla 1: un solo `CALL`, todo o nada |
| Devolver 409 en el duplicado | RF205: 200 con `ya_registrado` |
| Agregar autorización por rol a la API | D28 de la v5: la API no la tiene; el front es el que protege `/admin` |

## 4. Fases con prompts listos

### Fase 62 — Preparación
```
Fase 62 de docs/spec_kit/Versiones/version8/8_tasks.md. No escribas código.
Levanta la BD (script + migraciones 001-053) y confirma cada punto de la fase
con una consulta SQL. Anota en 8_tasks.md la consulta y el resultado. Si algo
del spec no coincide con la BD, propón el cambio al documento y espera mi visto bueno.
```

### Fase 63 — Migraciones 054 y 055
```
Fase 63. Escribe db/postgres/migraciones/054_tipos_registro_v8.sql y
055_geocerca_sede.sql según 5_data_model.md §2 y §3, idempotentes, con el
encabezado y los comentarios de las migraciones anteriores (mira la 051). Móntalas
en docker-compose.yml como z054_ y z055_. Verifica aplicándolas dos veces seguidas
sobre una BD con el volumen existente y probando fn_distancia_metros.
```

### Fase 64 — Procedimientos de registro
```
Fase 64. Escribe 056_sp_registro_por_tipo.sql según 5_data_model.md §4, con la
clave técnica de D71 (4_research.md). Escribe también
db/postgres/pruebas/prueba_sp_registro_v8.sql, con los casos de 8_tasks.md, en el
estilo de prueba_sp_maestro_detalle_formulario.sql. Córrela y pégame el resumen.
```

### Fase 65 — Apagado (D58)
```
Fase 65. Escribe 057_apagado_v71.sql. En el front: quita de app.py el registro de
los blueprints estudiante, ponente y ponente_catedras (no borres los archivos);
haz que auth_helpers.endpoint_de_inicio lleve a admin.panel; limpia el navbar.
En la API: LoginAsync solo acepta usuarios con rol ADMIN y ya no provisiona
estudiantes. No borres código: comenta "Apagado en v8 (D58)" donde aplique.
```

### Fase 66 — API
```
Fase 66. Crea las capas de Registro (Peticiones, Repositorio, Servicio y
Controller) según 6_contracts.md, con el molde de ClaveAccesoController y su
repositorio. Regístralas en Program.cs. Agrega las coordenadas a Sede. Agrega el
bloque "V8 · Registro por tipo" a pruebas/Programa.cs y córrelo con
docker compose exec api dotnet run --project pruebas.
```

### Fase 67 — Front
```
Fase 67. Reescribe routes/registro.py para la pantalla de 3 botones y los 3
formularios (3_plan.md §5); crea las plantillas, extendiendo base.html como las
actuales; adapta geolocalizacion.js a data-ubicacion. Agrega las coordenadas a
/admin/sedes. No pongas datos personales en la URL.
```

### Fase 68 — Cierre
```
Fase 68. Escribe pruebas/verificar_v8.ps1 (los 7 pasos de 7_quickstart.md §3) y
correr_todo_v8.ps1 (copia el molde de correr_todo_v7.ps1). Actualiza el README y
0_mapa_versiones.md. No quites el banner ni crees el tag: eso lo hace el equipo.
```

## 5. Lista de revisión para cualquier cambio de la v8

- [ ] Ninguna línea nueva en `script_bdcatedras.sql`.
- [ ] Cada migración es idempotente y está montada en `docker-compose.yml`.
- [ ] Cada registro es un solo `CALL`.
- [ ] Ningún dato personal en la URL ni en el log.
- [ ] Mensajes y comentarios en español.
- [ ] Las pruebas de las versiones anteriores siguen pasando.
