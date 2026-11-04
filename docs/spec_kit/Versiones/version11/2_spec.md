# Especificación — Versión 11 del proyecto: archivos planos, importación y reportes

> ⚠️ **BORRADOR · por construir.** Depende de la **v8** (los registros por tipo) y
> de la **v10** (los datos de los ponentes en `perfil_ponente`). Es la última
> versión del plan V8–V11.
>
> Rige la [constitución](../../1_constitution.md). Plan:
> [../plan_v8_v11.md](../plan_v8_v11.md). Decisiones: D65, D68 en
> [../decisiones_v8_v11.md](../decisiones_v8_v11.md) y D81–D84 en
> [4_research.md](4_research.md). Guía: [guia_ia_v11.md](guia_ia_v11.md).
>
> **Pendiente con el cliente (P2):** un archivo plano real de ejemplo de ASIS.
> Mientras llega, las columnas son configurables (D65).
>
> | Documento | Contenido |
> |---|---|
> | **2_spec.md** (este) | Qué construir y sus criterios |
> | [3_plan.md](3_plan.md) | Cómo, incluido el 4.º servicio `cron` |
> | [4_research.md](4_research.md) | D65, D68, D81–D84 |
> | [5_data_model.md](5_data_model.md) | Migraciones 064–066 |
> | [6_contracts.md](6_contracts.md) | Endpoints y servicio `cron` |
> | [7_quickstart.md](7_quickstart.md) | Prueba |
> | [8_tasks.md](8_tasks.md) | Fases 81–87 |

---

## 1. Propósito de la v11

Al terminar cada cátedra, la oficina tiene **3 días hábiles** para subir a ASIS el
archivo plano con los estudiantes (W 01:32). Hoy lo arma a mano. Además necesita:

- el listado completo de asistentes (notas del compañero);
- los datos de los ponentes, para los certificados y para SNIES (R 13:37, R 15:13);
- el impacto social, es decir, cuántos externos asisten por año (R 13:51);
- cargar la asistencia de las cátedras de posgrado, que llega en Excel (W 02:17).

La v7 ya genera lotes de migración (`sp_generar_lote_migracion`). La v11 los usa,
con columnas configurables, y los genera solos a las 21:00.

```
cron (21:00) ─ POST /api/Exportacion/jornada ─> sp_generar_archivos_jornada
     └─ por cada sesión terminada hoy y no migrada: sp_generar_lote_migracion (v7) → lote GENERADO
Admin ─ /admin/archivos ─ descarga ASIS (.csv / .xlsx) · asistencia general · ponentes · estadísticas
Admin ─ /admin/importar-asistencia ─ CSV/Excel de posgrado ─> registros IMPORTADO
```

## 2. Alcance

**Incluye:**
- archivo ASIS configurable (RF248–RF251);
- generación de la jornada (RF252–RF255);
- archivo general de asistencia (RF256);
- importación (RF257–RF259);
- ponentes para certificados y SNIES (RF260);
- ficha ASIS de la sesión (RF261);
- estadísticas (RF262–RF263).

**No incluye:**
- subir el archivo a ASIS: no hay integración real; la oficina lo sube a mano;
- generar los certificados en PDF (posible versión futura).

## 3. Requisitos funcionales

La numeración arranca en RF248 (la v10 terminó en RF247).

### Archivo plano ASIS (D65)

- **RF248 — Columnas configurables.** El parámetro `ASIS_COLUMNAS` es una lista
  separada por comas con las columnas, en orden.
  - Columnas permitidas: `programa`, `id_asis`, `cedula`, `reunion`, `nombres`,
    `apellidos` y `correo`.
  - Valor inicial: `programa,id_asis,cedula`.
  - Con una columna que no existe → error 500 al exportar, con la lista de las válidas.
- **RF249 — Contenido.** Solo entran los registros que hoy entran a la
  migración (RN-08 y RN-28 de `fn_exportar_asis`): con `id_asis`, con programa y
  no externos. La `cedula` sale del `documento_asistente` vigente; si no hay,
  queda vacía.
- **RF250 — Exportar una sesión.** `GET /api/Exportacion/asis/{idSesion}` devuelve
  `{nombre_archivo, columnas, filas: [[…], …]}`.
  - El front lo baja como **CSV** (UTF-8 con BOM, separador `;`) o como **Excel**
    (`.xlsx`, con `openpyxl`).
  - Parámetro `ASIS_ENCABEZADO` (`true` por defecto): si se incluye la fila de títulos.
- **RF251 — Desde un lote.** `GET /api/Exportacion/asis/lote/{idLoteMigracion}`
  hace lo mismo con las filas de `detalle_migracion` de ese lote, para que el
  archivo coincida con lo que quedó registrado como enviado.

### Jornada automática (D68)

- **RF252 — Generar la jornada.** `POST /api/Exportacion/jornada` con
  `{fecha?}` (hoy por defecto).
  - Para cada sesión con `fin` en esa fecha, `fin <= now()` y `estado <>
    'MIGRADA'`, llama a `sp_generar_lote_migracion` (v7).
  - Si una sesión falla (por ejemplo, «No hay registros migrables»), se anota y
    se sigue con la siguiente.
  - Responde `{fecha, generados: [{id_sesion, id_lote}], omitidas: [{id_sesion, motivo}]}`.
- **RF253 — Hora de cierre.** El parámetro `HORA_CIERRE_JORNADA` vale `21:00` por
  defecto y lo lee el servicio `cron`.
- **RF254 — Botón manual.** En `/admin/archivos`, «Generar archivos del día»
  llama al mismo endpoint.
- **RF255 — Plazo.** Cada lote muestra su fecha límite: 3 días hábiles después
  de la sesión, sin sábados ni domingos (D84). Si quedan menos de 24 h, se
  resalta.

### Archivo general de asistencia

- **RF256 — Todos los asistentes.** `GET /api/Exportacion/asistencia/{idSesion}`
  devuelve una fila por registro con:
  - **la persona:** tipo de usuario (de la foto de la vinculación), `id_asis`,
    tipo y número de documento, nombres, apellidos y correo;
  - **su contexto:** programa, facultad (la foto de la v8) y procedencia;
  - **el registro:** hora, origen, latitud, longitud y distancia a la sede.

  Se baja como CSV o Excel.

### Importación

- **RF257 — Cargar un archivo.** `/admin/importar-asistencia`: se elige la
  sesión y se sube un CSV o Excel con las columnas del archivo ASIS (las de
  `ASIS_COLUMNAS`, D83). El front lo lee y manda las filas como JSON.
- **RF258 — Importar.** `POST /api/Importacion/asistencia` con
  `{fk_sesion, filas: [{id_asis, programa?, cedula?}], fk_usuario?}`. Por cada fila:
  - resuelve al asistente por `id_asis` (o por la cédula, si no hay ID);
  - inserta el registro con `origen = 'IMPORTADO'` (la BD permite `fk_clave NULL`
    en ese caso).
  - Responde `{importadas, ya_registradas, errores: [{fila, motivo}]}`.
- **RF259 — Sin tumbar la carga.** Una fila con error no detiene las demás (el
  mismo criterio de las novedades de la v3).

### Ponentes, ficha ASIS y estadísticas

- **RF260 — Ponentes del periodo.** `GET /api/Exportacion/ponentes?fkPeriodo=&anio=`
  devuelve una fila por ponencia con:
  - **el ponente:** documento, nombres, apellidos, perfil profesional,
    dirección, teléfono, correo y si es externo;
  - **su ponencia:** cátedra, sesión, fecha y resumen de la ponencia (de la
    solicitud de la v10, si existe).

  El filtro `soloExternos=true` sirve para SNIES.
- **RF261 — Ficha ASIS de la sesión.** `GET /api/Exportacion/ficha/{idSesion}`
  devuelve:
  - del evento: `id_evento_asis`, el nombre ASIS (`nombre_asis`), el tipo de
    evento y el número de la reunión;
  - de la sesión: fecha, inicio, fin, sede y nombres de los ponentes.

  Es lo que hace falta para crear el evento y la reunión en ASIS. Se ve en el
  detalle de la sesión, con un botón para copiar.
- **RF262 — Impacto social.** `GET /api/Estadistica/impacto?anio=&fkPeriodo=`
  devuelve:
  - el total de asistentes por tipo de vinculación;
  - los externos por procedencia (las 20 primeras);
  - los asistentes por mes;
  - el número de cátedras y de sesiones.
- **RF263 — Pantalla de estadísticas.** `/admin/estadisticas`: tablas y gráficos
  simples (barras en HTML/CSS, sin bibliotecas), con un filtro por año y
  exportación a CSV.

## 4. Requisitos no funcionales

- **RNF-V11-1:** el archivo de una sesión de 500 asistentes se genera en menos de 3 s.
- **RNF-V11-2:** el servicio `cron` no tiene credenciales ni datos: solo llama
  a un endpoint por la red interna de Docker.
- **RNF-V11-3:** si el `cron` falla un día, el botón manual (y volver a llamar
  el endpoint) lo recupera: la operación es idempotente, porque las sesiones ya
  `MIGRADA` se saltan.

## 5. Criterios de aceptación

1. A las 21:00 (o al llamar el endpoint) quedan lotes `GENERADO` para cada sesión
   terminada del día que tenga estudiantes migrables. Las otras aparecen en
   `omitidas` con su motivo.
2. Cambiar `ASIS_COLUMNAS` a `reunion,id_asis,programa` cambia el archivo sin tocar código.
3. El archivo general de una sesión trae a los estudiantes, a los docentes y a
   los externos con su tipo.
4. Un CSV de posgrado con 30 filas (2 con un ID inexistente) importa 28 y reporta 2 errores.
5. El reporte de ponentes externos de 2027 trae sus datos completos.
6. `/admin/estadisticas` muestra cuántos externos asistieron en el año.
