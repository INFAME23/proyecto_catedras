# Modelo de datos — Versión 10: calendario público y solicitudes de ponentes

> ⚠️ **BORRADOR · por construir** — ver nota de [2_spec.md](2_spec.md).
> Migraciones 061–063, idempotentes, montadas como `z061_` … `z063_`.

## 1. Cadena de FK

```
sede (v1) ─┐
modalidad ─┼─< franja_disponible >── solicitud_catedra (opcional) · sesion (opcional, v4)
           │
solicitud_catedra >── franja_disponible · tipo_documento (v1) · tipo_evento (v1, opcional)
                  >── asistente (opcional, se llena al aprobar) · usuario (resuelve, opcional)
perfil_ponente  >── asistente (1 a 1)
```

## 2. Migración 061 — `franja_disponible`

```sql
CREATE TABLE IF NOT EXISTS catedras.franja_disponible (
    id_franja          bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    fk_sede            varchar(15) NOT NULL REFERENCES catedras.sede(id_sede),
    fk_modalidad       varchar(15) NOT NULL REFERENCES catedras.modalidad(id_modalidad),
    inicio             timestamptz NOT NULL,
    fin                timestamptz NOT NULL,
    estado             varchar(12) NOT NULL DEFAULT 'LIBRE'
                       CHECK (estado IN ('LIBRE','EN_REVISION','OCUPADA','BLOQUEADA')),
    fk_solicitud       bigint,                    -- FK se agrega en 062
    fk_sesion          bigint REFERENCES catedras.sesion(id_sesion) ON DELETE SET NULL,
    creado_en          timestamptz NOT NULL DEFAULT now(),
    CHECK (fin > inicio),
    EXCLUDE USING gist (fk_sede WITH =, tstzrange(inicio, fin, '[)') WITH &&)
);
```

`btree_gist` hace falta para el `EXCLUDE` con `=`. El script ya lo usa en
`ex_enlace_sin_solape`; se confirma en la Fase 74.

**Procedimientos:**
- los 5 verbos por procedimiento (molde de `sede`): `sp_listar_franja`,
  `sp_consultar_franja`, `sp_crear_franja`, `sp_actualizar_franja` (solo el
  estado, RF231) y `sp_eliminar_franja` (RF232);
- `sp_plantilla_franjas(p_desde, p_hasta, p_sede, p_modalidad, p_dias int[], p_horas time[], p_duracion, INOUT p_json)`,
  que devuelve `{creadas, omitidas}`;
- `sp_calendario_franjas(p_anio, p_mes, p_sede, INOUT p_json)`, que trae solo
  los datos públicos (RF234).

## 3. Migración 062 — `solicitud_catedra` y `perfil_ponente`

```sql
CREATE TABLE IF NOT EXISTS catedras.solicitud_catedra (
    id_solicitud        bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    fk_franja           bigint NOT NULL REFERENCES catedras.franja_disponible(id_franja),
    nombre_catedra      varchar(200) NOT NULL,
    resumen_ponencia    text NOT NULL,
    fk_tipo_evento      char(4) REFERENCES catedras.tipo_evento(id_tipo_evento),
    -- copia de lo que escribió el ponente (D77)
    fk_tipo_documento   varchar(5)  NOT NULL REFERENCES catedras.tipo_documento(id_tipo_documento),
    numero_documento    varchar(20) NOT NULL CHECK (numero_documento ~ '^[0-9A-Za-z-]{4,20}$'),
    nombres             varchar(100) NOT NULL,
    apellidos           varchar(100) NOT NULL,
    perfil_profesional  text NOT NULL,
    direccion           varchar(200),
    telefono            varchar(20) NOT NULL,
    correo_institucional citext NOT NULL CHECK (correo_institucional ~~ '%@%.%'),
    es_externo          boolean NOT NULL,
    acepta_datos        boolean NOT NULL CHECK (acepta_datos),
    estado              varchar(10) NOT NULL DEFAULT 'PENDIENTE'
                        CHECK (estado IN ('PENDIENTE','APROBADA','RECHAZADA')),
    motivo              varchar(500),
    creado_en           timestamptz NOT NULL DEFAULT now(),
    resuelta_en         timestamptz,
    fk_usuario_resuelve bigint REFERENCES catedras.usuario(id_usuario) ON DELETE SET NULL,
    fk_asistente        bigint REFERENCES catedras.asistente(id_asistente),
    fk_sesion           bigint REFERENCES catedras.sesion(id_sesion),
    CHECK (estado <> 'RECHAZADA' OR motivo IS NOT NULL)
);
ALTER TABLE catedras.franja_disponible
    ADD CONSTRAINT fk_franja_solicitud FOREIGN KEY (fk_solicitud)
        REFERENCES catedras.solicitud_catedra(id_solicitud) ON DELETE SET NULL;   -- con guarda IF NOT EXISTS

CREATE TABLE IF NOT EXISTS catedras.perfil_ponente (
    fk_asistente       bigint PRIMARY KEY REFERENCES catedras.asistente(id_asistente) ON DELETE CASCADE,
    perfil_profesional text NOT NULL,
    direccion          varchar(200),
    telefono           varchar(20),
    es_externo_usb     boolean NOT NULL,
    actualizado_en     timestamptz NOT NULL DEFAULT now()
);
```

**Procedimientos:**
- `sp_crear_solicitud` (RF236, con `FOR UPDATE`);
- `sp_listar_solicitud` (filtra por estado);
- `sp_consultar_solicitud`;
- `sp_contar_solicitudes_pendientes`.

## 4. Migración 063 — aprobar y rechazar

```sql
-- sp_aprobar_solicitud(p_id_solicitud, p_id_evento_asis, p_fk_catedra, p_fk_tipo_evento,
--                      p_fk_dependencia, p_fk_encuesta, p_fk_usuario, INOUT p_json)
--   1. SELECT … FOR UPDATE; estado = 'PENDIENTE' o RAISE 'V10: la solicitud ya fue resuelta'
--   2. persona: fn_resolver_asistente(numero_documento) o alta (asistente + documento + vinculación
--      DOCENTE/EXTERNO según es_externo) ; perfil_ponente INSERT … ON CONFLICT DO UPDATE ; consentimiento
--   3. catedra: p_fk_catedra o INSERT con p_id_evento_asis (chk_catedra_asis / uq_catedra_asis responden)
--   4. sesion: inicio/fin de la franja, fk_sede, fk_modalidad, fk_periodo (D80), fk_encuesta
--   5. ponencia (fk_sesion, fk_asistente, 'PONENTE')
--   6. franja: estado 'OCUPADA', fk_sesion ; solicitud: 'APROBADA', resuelta_en, fk_usuario_resuelve, fk_asistente, fk_sesion
--   devuelve {id_catedra, id_sesion, correo_ponente, nombre_ponente}
-- sp_rechazar_solicitud(p_id_solicitud, p_motivo, p_fk_usuario, INOUT p_json)
--   estado 'RECHAZADA' + motivo ; franja 'LIBRE', fk_solicitud NULL
```

## 5. Reglas de esta versión

| Regla | Dónde vive |
|---|---|
| Una franja por sede y horario | `EXCLUDE` de `franja_disponible` |
| Solo se pide una franja `LIBRE` | `sp_crear_solicitud` + `FOR UPDATE` |
| Una solicitud se resuelve una sola vez | `sp_aprobar_solicitud` y `sp_rechazar_solicitud` |
| Rechazo con motivo | CHECK de `solicitud_catedra` |
| `id_evento_asis` de 9 dígitos y único | `chk_catedra_asis` y `uq_catedra_asis` (script) |
| Periodo de la sesión | D80, en `sp_aprobar_solicitud` |
