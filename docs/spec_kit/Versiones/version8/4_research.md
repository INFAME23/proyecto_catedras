# Decisiones de diseño — Versión 8: registro de asistencia sin login, por tipo de usuario

> ⚠️ **BORRADOR · por construir** — ver nota de [2_spec.md](2_spec.md).
> Las decisiones D55–D70 las tomó el equipo el 2026-11-04 y están completas en
> [../decisiones_v8_v11.md](../decisiones_v8_v11.md). Aquí se resumen las que
> rigen la v8 y se agregan las que salieron al cruzar el plan con la BD
> (D71 y D72).

---

## Decisiones del equipo que aplican a la v8

| ADR | Decisión | Alternativa descartada | Por qué |
|---|---|---|---|
| D55 | El estudiante se registra solo con su ID | ID + clave por correo (como en la v7.1) | Más rápido en el auditorio; la seguridad la da el QR dinámico de la V9 |
| D56 | `DOCENTE` con `exige_programa = false` | Pedirle el programa | El cliente solo pidió la facultad (R 12:45) |
| D57 | Tipo `EXTERNO` + procedencia; alta en un solo `CALL` | Una tabla aparte de "asistentes rápidos" | Encaja con el modelo y con los reportes de la V11 |
| D58 | Apagar sin borrar lo de la v7.1 | Borrar el código | Es reversible y no rompe las pruebas viejas |
| D59 | ID alfanumérico de 5 a 15 | Solo números | El maestro real trae IDs como `1000513C` |
| D60 | Ubicación: obligatoria en presencial, opcional en híbrida y ausente en virtual | Obligatoria siempre (v7.1) | En virtual no importa desde dónde se conecta la persona (R 17:20) |
| D61 | Geocerca de 300 m por sede | Solo exigir la ubicación | Es lo único que de verdad evita registrarse desde la casa |
| D66 | Encuesta ofrecida, no obligatoria | Obligatoria | No frenar el registro |
| D67 | Casilla de tratamiento de datos obligatoria (docente y externo) | No pedirla | Ley 1581; `consentimiento_datos` ya existe para eso |

## D71 — Clave técnica para cumplir `chk_registro_clave`

**Contexto.** El script trae:

```sql
CONSTRAINT chk_registro_clave CHECK (origen IN ('MANUAL','IMPORTADO') OR fk_clave IS NOT NULL)
```

Un registro hecho desde el QR (`origen = 'QR'` o `'ENLACE'`) tiene que apuntar a
una clave. Con D55 ya no hay clave por correo.

**Opciones.**
1. Quitar o cambiar el CHECK. Es editar una regla del script provisto, lo que va
   contra el Artículo 5 de la constitución.
2. Registrar con `origen = 'MANUAL'`. Es falso: mezclaría los registros por QR
   con los que digita el administrador y dañaría las estadísticas.
3. **Crear una clave técnica ya usada** en `clave_acceso`.

**Decisión:** la opción 3. El procedimiento de registro inserta una fila en
`clave_acceso` con:
- `fk_asistente` y `fk_enlace` del registro;
- `clave_hash = digest('v8-registro-directo' || token::text, 'sha256')`;
- `enviado_a` = el correo de la persona (la tabla exige uno con formato válido);
- `generado_en = now()`, `expira_en = now() + interval '1 minute'` y `usado_en = now()`;
- `ip_solicitud` = la IP de la petición.

El disparador `trg_clave_derivar` calcula `es_correo_institucional` como siempre.
Se documenta con `COMMENT ON` en la migración 056.

**Consecuencia.**
- Hay un estudiante sin correo en el maestro: `chk_asistente_correo` lo hace casi
  imposible, porque exige al menos un correo.
- Si aun así no tiene ninguno, el procedimiento usa `sin-correo@usbmed.edu.co`
  y la fila queda marcada. Se verifica en la Fase 62 cuántos casos hay.

## D72 — Dónde se guardan la facultad del docente y la procedencia del externo

**Contexto.** `asistente` no tiene ni facultad ni procedencia. El registro
(`registro_asistencia`) guarda fotos de la vinculación y del programa, pero no de
la facultad.

**Decisión.** La migración 054 agrega dos columnas a `asistente`, ambas opcionales:
- `fk_facultad varchar(10)`, con FK a `facultad`;
- `procedencia varchar(150)`.

Agrega también una a `registro_asistencia`, opcional:
- `fk_facultad_snapshot varchar(10)`, con FK a `facultad`.

El procedimiento docente llena la facultad de la persona y su foto en el
registro: si mañana el docente cambia de facultad, el registro viejo no cambia
(el mismo criterio de RN-22).

**Alternativa descartada:** guardar la facultad en `vinculacion_asistente`. Esa
tabla no tiene columna para eso y mezclaría dos conceptos.

## D73 — Nombre completo en dos campos

`asistente` exige `nombres` y `apellidos` por separado (`NOT NULL`). El cliente
habló de "nombre". El formulario pide **Nombres** y **Apellidos** en dos cajas;
no se intenta partir un nombre completo (falla con apellidos compuestos).

## Riesgos aceptados

- **Suplantación por ID (D55)**, mientras llega la V9: se mitiga con la
  ventana de registro al final de la sesión, que ya existe, y con la geocerca en
  las presenciales.
- **Coordenadas de la sede todavía no cargadas:** la geocerca no bloquea
  hasta que el administrador las cargue (RF208).
