# Decisiones de diseño — Versión 9: QR dinámico y control por modalidad

> ⚠️ **BORRADOR · por construir** — ver nota de [2_spec.md](2_spec.md).

## Decisiones del equipo que aplican

| ADR | Decisión |
|---|---|
| D55 | La seguridad del registro por ID descansa en el QR dinámico |
| D62 | Proyecta el ponente con un enlace secreto; el admin, desde `/admin` |
| D63 | El código vive 60 s y se acepta también el anterior, estilo TOTP |

## D74 — Algoritmo del código

**Decisión:**
- el código es `upper(left(base32(hmac(paso::text, secreto, 'sha256')), 8))`, con
  `paso = floor(extract(epoch from now()) / QR_ROTACION_SEGUNDOS)`;
- se usa el alfabeto base32 de RFC 4648 sin el relleno (`=`);
- tiene 8 caracteres: con 32⁸ ≈ 10¹² combinaciones, adivinarlo es inviable.

`base32` no existe en PostgreSQL; se implementa como una función
`fn_base32(bytea)` en SQL. Como alternativa, se usan 10 caracteres
hexadecimales con `encode(…, 'hex')`. Se decide en la Fase 69; el resto del diseño
no cambia.

**Alternativas descartadas:**
- un `enlace_registro` nuevo por minuto: genera muchas filas, choca con
  `ex_enlace_sin_solape` y con RN-14, y el cliente dijo que «sería muy
  complicado crear tantos»;
- calcular el código en C#: duplicaría la regla fuera de la BD.

## D75 — Pase de registro

**Contexto:** el código vale entre 60 y 120 s; llenar el formulario de externo
puede tardar más.

**Decisión:**
- `fn_emitir_pase(token, codigo)` devuelve
  `pase = encode(paso_emision || hmac('pase:' || token || ':' || paso_emision, secreto), 'base64')`;
- no se guarda en ninguna tabla;
- `fn_validar_pase` lo recalcula y revisa que no tenga más de `QR_PASE_MINUTOS`.

**Consecuencia:** un pase se puede reenviar a otra persona durante sus 10
minutos. Se acepta: quien lo recibe igual necesita un ID o un documento real y,
si la sesión es presencial, estar dentro de la geocerca.

## D76 — Enlace de proyección con su propio secreto

**Decisión:**
- la tabla `enlace_proyeccion` guarda `(id, fk_sesion, token_hash, creado_en,
  revocado_en, fk_usuario_crea)`;
- el token en claro se devuelve una sola vez, al crearlo;
- se guarda `digest(token, 'sha256')`;
- puede haber uno vigente por sesión (índice parcial único).

**Alternativa descartada:** reutilizar el token del `enlace_registro`. Ese token
va en el QR, así que es público.
