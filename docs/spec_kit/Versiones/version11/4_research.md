# Decisiones de diseño — Versión 11: archivos planos, importación y reportes

> ⚠️ **BORRADOR · por construir** — ver nota de [2_spec.md](2_spec.md).

## Decisiones del equipo que aplican

| ADR | Decisión |
|---|---|
| D65 | Las columnas del archivo ASIS son configurables (`ASIS_COLUMNAS`); el valor inicial es programa, ID y cédula |
| D68 | 4.º servicio `cron` en docker-compose que llama a la API a las 21:00, más un botón manual |

## Contexto que no cambia

`fn_exportar_asis` (script) devuelve exactamente 3 columnas: `reunion`, `id_asis`
y `programa`, según el manual de ASIS. Su comentario dice «ni una más». La nota
de la reunión pide programa, ID y cédula. Por eso D65 hace configurable el
archivo **sin tocar** `fn_exportar_asis`: la v11 agrega sus propios
procedimientos de exportación, que usan los mismos filtros (RN-08 y RN-28).

## D81 — La BD arma las filas; el front escribe el archivo

**Decisión.** Los procedimientos devuelven JSON con `{columnas, filas}`. El
front, con `servicios/archivos_planos.py`, lo escribe como:
- **CSV:** UTF-8 con BOM, para que Excel muestre bien las tildes, y separador
  `;`, el de Excel en español;
- **XLSX:** con `openpyxl`.

**Alternativa descartada:** generar el XLSX en C#. Habría que agregar un paquete
NuGet y el archivo es presentación.

## D82 — Quién decide la hora

**Opción A (por defecto).** El `crontab` dice `0 21 * * *`. Es lo más simple y
el parámetro solo documenta la hora.

**Opción B.** El `cron` corre cada 15 minutos con `{"solo_si_paso_cierre": true}`
y el procedimiento compara la hora con `HORA_CIERRE_JORNADA`. Así, cambiar el
parámetro cambia la hora sin reiniciar nada.

Se decide en la Fase 81. El endpoint acepta las dos formas.

## D83 — Formato de importación = formato de exportación

El cliente dijo «el mismo formato, simplemente para atrás» (W 01:23). La
importación lee las columnas de `ASIS_COLUMNAS`, por nombre si el archivo trae
encabezado o por posición si no lo trae. Solo `id_asis` o `cedula` son
indispensables para encontrar a la persona.

## D84 — Días hábiles

`fn_dias_habiles(fecha, n)` suma `n` días saltando los sábados y los domingos.
Los festivos de Colombia **no** se tienen en cuenta: no hay una tabla de
festivos. Se anota como mejora futura: una tabla `festivo` cargada por el admin.

## Riesgos

- **El formato real de ASIS puede tener otras exigencias**, como la longitud fija
  o un nombre de hoja en el Excel. Se resuelve cuando llegue el archivo de
  ejemplo (P2), sin cambiar el diseño.
- **La jornada deja la sesión en `MIGRADA`** (el efecto colateral documentado en
  la v7, RF184). Es intencional: así no se genera dos veces.
