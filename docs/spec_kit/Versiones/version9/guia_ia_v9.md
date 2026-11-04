# Guía IA — Versión 9: QR dinámico y control por modalidad

> **Jerarquía:** [constitución](../../1_constitution.md) › [guía IA](../../guia_ia.md)
> › spec kit v9 › esta guía. Si el spec contradice la BD real, gana la BD.
> **Estado de partida:** v8 construida y fusionada; v9 solo en papel.

## 1. Prompt de arranque

```
Vas a trabajar la versión 9 del Proyecto Cátedras (QR dinámico). Lee
docs/spec_kit/1_constitution.md, docs/spec_kit/guia_ia.md,
docs/spec_kit/Versiones/plan_v8_v11.md y todo docs/spec_kit/Versiones/version9/.
La v8 ya está construida: los procedimientos sp_registrar_* existen (migración 056).
Reglas: no edites script_bdcatedras.sql; migraciones nuevas 058-060; el código del
QR se CALCULA con HMAC en la BD, no se guarda; JSON en snake_case; todo en español.
No contradigas D55, D62, D63 ni D74-D76. Trabaja solo la fase que te indique.
```

## 2. La v9 en una página

- **El código rotativo:**
  - es `HMAC(secreto del enlace, minuto)` y cambia cada 60 s;
  - se acepta también el código anterior.
- **El pase:**
  - al escanear, el código se cambia por un pase de 10 minutos para llenar el formulario;
  - los procedimientos de registro de la v8 lo exigen si el enlace tiene secreto.
- **La proyección:**
  - el ponente tiene un enlace secreto de proyección, que se guarda solo como hash;
  - el admin proyecta desde `/admin`.

## 3. Lo que la IA tiende a "arreglar" mal

| Tentación | Por qué no |
|---|---|
| Crear un `enlace_registro` por minuto | D74: choca con `ex_enlace_sin_solape` y con RN-14; el código se calcula |
| Calcular el código en C# o en JS | Duplica la regla; el secreto nunca sale de la BD (RNF-V9-1) |
| Poner el pase en la URL | RNF-V8-2: va en la sesión de Flask |
| Usar el token del QR como enlace de proyección | D76: ese token es público |
| Usar `pg_sleep` en las pruebas | Las funciones reciben `p_paso` para probar sin esperar |
| Usar el reloj del navegador | RNF-V9-3: manda `now()` de la BD |
| Llamar a la API desde `proyeccion.js` | La API no es accesible desde el navegador; se usa el proxy del front |

## 4. Prompts por fase

- **Fase 69:** «Fase 69 de version9/8_tasks.md. Sin código. Confirma con SQL que hmac y gen_random_bytes existen y propón el formato del código (D74) con un ejemplo calculado.»
- **Fase 70:** «Escribe 058 y 059 según 5_data_model.md §2 y §3, y prueba_sp_qr_v9.sql con los casos de 8_tasks.md. Córrela.»
- **Fase 71:** «Escribe 060 y agrega a la API los endpoints de 6_contracts.md §1, con el molde de QrSesionController. Agrega el bloque de la prueba de capas.»
- **Fase 72:** «Front de la v9 según 3_plan.md §4 y 6_contracts.md §3. El QR se dibuja en el cliente con static/vendor/qrcode.min.js.»
- **Fase 73:** «verificar_v9.ps1 con los pasos de 7_quickstart.md §2. Actualiza el README y el mapa de versiones.»
