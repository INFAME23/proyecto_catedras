-- ==========================================================================
--  db/postgres/migraciones/005_sp_sede_ejemplo.sql
--  Proyecto Cátedras · RETROFIT criterio 3 de VERSION_2.md (PRUEBA DE
--  CONCEPTO, solo 'sede'): los 5 verbos pasan por procedimientos
--  almacenados, con el mismo molde exacto del ejemplo del profesor
--  (PROCEDURE, INOUT p_resultado JSON, un procedimiento por verbo y por
--  tabla: sp_listar_/sp_consultar_/sp_crear_/sp_actualizar_/sp_eliminar_).
--
--  POR QUÉ 'sede' PRIMERO: es la tabla más simple del esquema (sin FK
--  salientes, sin maestro-detalle) — sirve para probar el molde antes de
--  aplicarlo a las otras 32. Convertir el resto es PLAN_RETROFIT_SP.md.
--
--  DECISIÓN QUE TOMA ESTE RETROFIT (y que aplica a las otras 19 tablas con
--  Reemplazo+Actualizar, ver PLAN_RETROFIT_SP.md §2): UN SOLO
--  'sp_actualizar_<tabla>' que reemplaza TODAS las columnas (como el PUT
--  de hoy). El PATCH parcial YA NO arma un UPDATE dinámico en SQL: el
--  repositorio trae la fila actual, la mezcla con lo que llegó, y llama a
--  este MISMO procedimiento con el conjunto completo — así hay un solo
--  procedimiento por tabla, tal como pide el documento ("un nombre por
--  operación y por tabla"), y el repositorio queda siendo "la llamada al
--  procedimiento, con sus parámetros" (§4).
--
--  'sede' SÍ tiene columna 'activo' -> 'sp_eliminar_sede' es LÓGICO
--  (UPDATE ... SET activo = false), NO un DELETE (§4: "si sus tablas
--  tienen una columna activo, el borrado es lógico").
-- ==========================================================================

SET search_path TO catedras, public;

-- --------------------------------------------------------------------------
--  LISTAR — solo las activas (igual que el ejemplo del profesor), con un
--  límite OPCIONAL: 'LIMIT p_limite' con p_limite NULL = sin límite (así
--  funciona LIMIT en PostgreSQL, no hace falta COALESCE).
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_listar_sede(
    IN    p_limite    integer DEFAULT NULL,
    INOUT p_resultado json    DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT COALESCE(json_agg(s), '[]'::json)
      INTO p_resultado
      FROM (SELECT id_sede, nombre, direccion, es_virtual, activo
              FROM sede
             WHERE activo
             ORDER BY id_sede
             LIMIT p_limite) s;
END;
$$;

-- --------------------------------------------------------------------------
--  CONSULTAR — una sede por id. Si no existe, 'p_resultado' queda NULL
--  (row_to_json de una subconsulta vacía es NULL): el repositorio lo lee
--  como "no encontrada", igual que hoy QuerySingleOrDefaultAsync.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_consultar_sede(
    IN    p_id_sede   character varying(15),
    INOUT p_resultado json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    SELECT row_to_json(s) INTO p_resultado
      FROM (SELECT id_sede, nombre, direccion, es_virtual, activo
              FROM sede
             WHERE id_sede = p_id_sede) s;
END;
$$;

-- --------------------------------------------------------------------------
--  CREAR — devuelve la fila COMO QUEDÓ GUARDADA (§4). 'id_sede' no lo
--  genera la BD (lo manda el cliente, es su clave primaria "de negocio"),
--  así que no hace falta un 'v_id' intermedio: ya se conoce.
--  Clave repetida (pk_sede) o nombre repetido (uq_sede_nombre) ->
--  NpgsqlException; el controlador la traduce a 409 (ver PLAN_RETROFIT_SP.md,
--  eso es trabajo del criterio 7, NO de este procedimiento).
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_crear_sede(
    IN    p_id_sede    character varying(15),
    IN    p_nombre     character varying(80),
    IN    p_direccion  character varying(200) DEFAULT NULL,
    IN    p_es_virtual boolean DEFAULT false,
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    INSERT INTO sede (id_sede, nombre, direccion, es_virtual)
    VALUES (p_id_sede, p_nombre, p_direccion, p_es_virtual);

    SELECT row_to_json(s) INTO p_resultado
      FROM (SELECT id_sede, nombre, direccion, es_virtual, activo
              FROM sede
             WHERE id_sede = p_id_sede) s;
END;
$$;

-- --------------------------------------------------------------------------
--  ACTUALIZAR — reemplazo COMPLETO (sirve para PUT directo y para PATCH,
--  que en el repositorio llega ya mezclado con la fila actual: ver
--  RepositorioSedePostgres.cs). Todas las columnas de negocio son
--  obligatorias aquí porque el repositorio SIEMPRE manda el conjunto
--  completo, nunca un subconjunto.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_actualizar_sede(
    IN    p_id_sede    character varying(15),
    IN    p_nombre     character varying(80),
    IN    p_direccion  character varying(200),
    IN    p_es_virtual boolean,
    IN    p_activo     boolean,
    INOUT p_resultado  json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE sede
       SET nombre     = p_nombre,
           direccion  = p_direccion,
           es_virtual = p_es_virtual,
           activo     = p_activo
     WHERE id_sede = p_id_sede;

    -- Sin FOUND -> no existía -> p_resultado queda NULL (igual criterio
    -- que 'consultar': el repositorio lo lee como "no encontrada").
    IF FOUND THEN
        SELECT row_to_json(s) INTO p_resultado
          FROM (SELECT id_sede, nombre, direccion, es_virtual, activo
                  FROM sede
                 WHERE id_sede = p_id_sede) s;
    END IF;
END;
$$;

-- --------------------------------------------------------------------------
--  ELIMINAR — LÓGICO: 'sede' tiene columna 'activo' (§4). Si ya estaba
--  inactiva, también cuenta como "no encontrada" (ya no hay nada que
--  retirar), igual criterio que un DELETE físico sobre algo que no existe.
-- --------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE catedras.sp_eliminar_sede(
    IN    p_id_sede   character varying(15),
    INOUT p_resultado json DEFAULT NULL
)
    LANGUAGE plpgsql
    SET search_path TO 'catedras', 'public'
    AS $$
BEGIN
    UPDATE sede
       SET activo = false
     WHERE id_sede = p_id_sede
       AND activo;   -- ya inactiva -> no cuenta como retirada otra vez

    p_resultado := json_build_object('eliminado', FOUND);
END;
$$;

COMMENT ON PROCEDURE catedras.sp_listar_sede IS
    'Retrofit criterio 3 (VERSION_2.md §4). Solo activas, con límite opcional.';
COMMENT ON PROCEDURE catedras.sp_consultar_sede IS
    'Retrofit criterio 3. NULL en p_resultado = no existe.';
COMMENT ON PROCEDURE catedras.sp_crear_sede IS
    'Retrofit criterio 3. Devuelve la fila como quedó guardada.';
COMMENT ON PROCEDURE catedras.sp_actualizar_sede IS
    'Retrofit criterio 3. Reemplazo completo; PATCH mezcla antes de llamar.';
COMMENT ON PROCEDURE catedras.sp_eliminar_sede IS
    'Retrofit criterio 3. Borrado LÓGICO (activo=false): la tabla tiene activo.';
