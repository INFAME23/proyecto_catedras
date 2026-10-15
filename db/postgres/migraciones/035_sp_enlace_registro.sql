-- ==========================================================================
--  db/postgres/migraciones/035_sp_enlace_registro.sql
--  Proyecto Cátedras · RETROFIT criterio 3 (VERSION_2.md §4) · Fase 5
--  Molde: 005_sp_sede_ejemplo.sql (ver PLAN_RETROFIT_SP.md, Grupo C).
--
--  'enlace_registro' ya tenía su escritura principal en la BD:
--     EMITIR (crear) -> fn_emitir_enlace_api -> sp_emitir_enlace (script
--     provisto, RN-14). NO se toca ni se reemplaza.
--  Este archivo agrega lo que faltaba:
--     sp_listar_enlace_registro, sp_consultar_enlace_registro y
--     sp_revocar_enlace_registro (antes era un UPDATE suelto en C#).
--
--  POR QUÉ NO HAY 'sp_actualizar_' NI 'sp_eliminar_' GENÉRICOS: la
--  revocación ES la baja lógica de un enlace ('revocado_en' cumple el
--  papel de 'activo': ex_enlace_sin_solape solo mira los no revocados), y
--  un UPDATE libre de token/url/ventana rompería RN-14/RN-15. El "editar"
--  legítimo de un enlace es emitir uno nuevo (que revoca el anterior).
--
--  AJUSTES AL MOLDE DE 'sede':
--   1) 'ventana' es tstzrange: se parte en ventana_desde/ventana_hasta
--      (lower/upper) dentro del JSON, igual que hacía el SELECT de C#.
--   2) Revocar devuelve la FILA (con revocado_en) en vez de
--      {"eliminado": ...}; NULL = no existe o ya estaba revocado.
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  LISTAR — filtro opcional por sesión, límite opcional. Incluye los
--  revocados (es historial; antes también los listaba).
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_listar_enlace_registro(
    IN    p_fk_sesion bigint  DEFAULT NULL,
    IN    p_limite    integer DEFAULT NULL,
    INOUT p_resultado json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(e), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_enlace, fk_sesion, token, url_publica, ruta_imagen_qr,
                   canal, lower(ventana) AS ventana_desde,
                   upper(ventana) AS ventana_hasta, usos_maximos,
                   revocado_en, fk_usuario_crea, creado_en
              FROM enlace_registro
             WHERE p_fk_sesion IS NULL OR fk_sesion = p_fk_sesion
             ORDER BY id_enlace
             LIMIT p_limite) e;
END;
$$;

-- --------------------------------------------------------------------------
--  CONSULTAR — por id. NULL = no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_consultar_enlace_registro(
    IN    p_id_enlace bigint,
    INOUT p_resultado json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(e) INTO p_resultado
      FROM (SELECT id_enlace, fk_sesion, token, url_publica, ruta_imagen_qr,
                   canal, lower(ventana) AS ventana_desde,
                   upper(ventana) AS ventana_hasta, usos_maximos,
                   revocado_en, fk_usuario_crea, creado_en
              FROM enlace_registro
             WHERE id_enlace = p_id_enlace) e;
END;
$$;

-- --------------------------------------------------------------------------
--  REVOCAR — baja lógica del enlace. 'AND revocado_en IS NULL': un segundo
--  intento no cambia nada y deja p_resultado en NULL (el servicio lo
--  traduce a 400, igual que antes con "0 filas").
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_revocar_enlace_registro(
    IN    p_id_enlace bigint,
    INOUT p_resultado json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE enlace_registro
       SET revocado_en = now()
     WHERE id_enlace = p_id_enlace
       AND revocado_en IS NULL;

    IF FOUND THEN
        SELECT row_to_json(e) INTO p_resultado
          FROM (SELECT id_enlace, fk_sesion, token, url_publica, ruta_imagen_qr,
                       canal, lower(ventana) AS ventana_desde,
                       upper(ventana) AS ventana_hasta, usos_maximos,
                       revocado_en, fk_usuario_crea, creado_en
                  FROM enlace_registro
                 WHERE id_enlace = p_id_enlace) e;
    END IF;
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_enlace_registro IS
    'Retrofit criterio 3. Filtro opcional por sesión; ventana partida en desde/hasta.';
COMMENT ON PROCEDURE catedras.sp_consultar_enlace_registro IS
    'Retrofit criterio 3. NULL = no existe.';
COMMENT ON PROCEDURE catedras.sp_revocar_enlace_registro IS
    'Retrofit criterio 3. Baja lógica (revocado_en=now()). NULL = no existe o ya revocado.';
