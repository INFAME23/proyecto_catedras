# Modelo de datos — Versión 9: QR dinámico y control por modalidad

> ⚠️ **BORRADOR · por construir** — ver nota de [2_spec.md](2_spec.md). Las
> migraciones 058–060 son idempotentes y van montadas como `z058_` … `z060_`.

## 1. Lo que se reutiliza

| Objeto | Para qué |
|---|---|
| `enlace_registro` (token, ventana, `revocado_en`) | El QR sigue siendo el enlace de la v5 |
| `sp_emitir_qr_sesion` (051/053/057) | Ahora también llena el `secreto` |
| `sp_asistencia_sesion` (051) | Contador de la pantalla |
| `pgcrypto` (`digest`, `hmac`, `gen_random_bytes`) | Ya instalado por el script |
| `sp_registrar_*` (056, v8) | Ganan el parámetro `p_pase` |

## 2. Migración 058 — código rotativo

```sql
ALTER TABLE catedras.enlace_registro ADD COLUMN IF NOT EXISTS secreto bytea;   -- NULL = enlace de antes de la v9

INSERT INTO catedras.parametro (clave, valor, tipo_dato, descripcion) VALUES
 ('QR_ROTACION_SEGUNDOS', '60', 'ENTERO', 'v9. Cada cuántos segundos cambia el código del QR'),
 ('QR_CODIGOS_TOLERADOS', '1',  'ENTERO', 'v9. Cuántos códigos anteriores se siguen aceptando'),
 ('QR_PASE_MINUTOS',      '10', 'ENTERO', 'v9. Minutos para llenar el formulario después de escanear')
ON CONFLICT (clave) DO NOTHING;

-- fn_codigo_qr(p_secreto bytea, p_paso bigint) RETURNS text          (D74)
-- fn_codigo_qr_valido(p_id_enlace bigint, p_codigo text) RETURNS boolean   (pasos actual .. actual - tolerados)
-- sp_emitir_qr_sesion: CREATE OR REPLACE; al insertar el enlace pone secreto = gen_random_bytes(32)
```

## 3. Migración 059 — pase de registro

```sql
-- fn_emitir_pase(p_token uuid, p_codigo text) RETURNS TABLE(pase text, expira_en timestamptz)
--   RN-15 (enlace vigente) + fn_codigo_qr_valido; si falla: RAISE 'V9: el código del QR ya expiró…'
-- fn_validar_pase(p_id_enlace bigint, p_pase text) RETURNS boolean        (D75)
-- sp_registrar_estudiante / _docente / _externo y sus fn_*_api: + p_pase text DEFAULT NULL
--   si el enlace tiene secreto y NOT fn_validar_pase(...) -> RAISE 'V9: el pase no es válido o venció; escanea el QR otra vez'
--   (DROP de las firmas de la 056 antes del CREATE)
```

## 4. Migración 060 — enlace de proyección

```sql
CREATE TABLE IF NOT EXISTS catedras.enlace_proyeccion (
    id_enlace_proyeccion bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    fk_sesion       bigint NOT NULL REFERENCES catedras.sesion(id_sesion) ON DELETE CASCADE,
    token_hash      bytea  NOT NULL UNIQUE,
    creado_en       timestamptz NOT NULL DEFAULT now(),
    revocado_en     timestamptz,
    fk_usuario_crea bigint REFERENCES catedras.usuario(id_usuario) ON DELETE SET NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_proyeccion_vigente
    ON catedras.enlace_proyeccion (fk_sesion) WHERE revocado_en IS NULL;

-- sp_emitir_enlace_proyeccion(p_fk_sesion, p_fk_usuario, p_regenerar, INOUT p_token text)
--   devuelve el token en claro UNA vez; guarda digest(token,'sha256')
-- sp_resolver_proyeccion(p_token text, INOUT p_fk_sesion bigint)   NULL si no existe o está revocado
-- sp_codigo_qr_sesion(p_fk_sesion, INOUT p_json)
--   {url_registro, codigo, segundos_restantes, rotacion_segundos, ventana_abre, ventana_cierra, abierto}
```

## 5. Reglas de esta versión

| Regla | Dónde vive |
|---|---|
| D63: código de 60 s + 1 tolerado | `fn_codigo_qr_valido` |
| D75: pase de 10 min atado al enlace | `fn_emitir_pase`, `fn_validar_pase` |
| D76: un enlace de proyección vigente por sesión | `ux_proyeccion_vigente` |
| RN-15: ventana del enlace | `trg_registro_validar` (script) y `fn_emitir_pase` |
