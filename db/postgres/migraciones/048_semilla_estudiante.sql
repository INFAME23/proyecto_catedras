-- ==========================================================================
--  db/postgres/migraciones/048_semilla_estudiante.sql
--  Proyecto Cátedras · SUBVERSIÓN v7.1 · RF-S4 (estudiante semilla)
-- --------------------------------------------------------------------------
--  UN estudiante de prueba, cargado a mano mientras no exista el archivo de
--  carga masiva del administrador.
--
--  ⚠️ HALLAZGO AL ESCRIBIRLA: el id_asis 30000124724 YA EXISTE en los datos
--  del script provisto, como una persona sintética del archivo del ASIS:
--      id_asistente 12808 · "Asistente ASIS 30000124724"
--      correo_institucional asis.30000124724@usbmed.edu.co (sin correo personal)
--      programa M0501 (periodo 2026-1) · documento CC 1011390488
--  Como el usuario pidió ESTOS datos para ese código, la migración AJUSTA
--  esa fila en vez de crear otra (id_asis es único). Lo que se pierde es
--  solo ese nombre/correo de relleno; los valores originales quedan aquí
--  escritos por si hay que deshacerlo. Si el id_asis no existe (BD sin los
--  datos del script), crea la persona completa con sp_guardar_asistente
--  (Guía IA, Regla 1: asistente + documento + vinculación + programa en UN
--  solo CALL).
--
--  DATOS CONFIRMADOS POR EL USUARIO
--    código de estudiante (id_asis) .... 30000124724
--    nombre ............................ Alejandro Zuleta Valderrama
--                                        nombres = Alejandro
--                                        apellidos = Zuleta Valderrama
--    correo ............................ alejandrozuleta81@gmail.com
--                                        Es PERSONAL (gmail): va en
--                                        correo_personal y correo_institucional
--                                        queda NULL (chk_asistente_correo exige
--                                        al menos uno). Importa: el correo de
--                                        destino de las claves es el
--                                        institucional si existe (RN-19); con
--                                        el de relleno, los códigos habrían
--                                        ido a un buzón que no existe.
--    programa .......................... Ingeniería en Datos y Software
--                                        = M0286 en catedras.programa_academico.
--
--  ⚠️ PLACEHOLDERS: el usuario NO los especificó. Están marcados para que
--  nadie los tome por reales; cámbialos cuando se confirmen.
--    PENDIENTE-1  documento de identidad. Si la persona ya tiene uno
--                 (como aquí: el de relleno CC 1011390488) se conserva; si
--                 no, se crea CC 'PEND-30000124724' (cumple
--                 ^[0-9A-Za-z-]{4,20}$). Reemplázalo por el real.
--    PENDIENTE-2  periodo académico de la matrícula: '2026-2' (el vigente
--                 el 2026-10-29, cuando se escribió esta migración).
--    PENDIENTE-3  vinculación ESTUDIANTE: si no tiene una vigente se crea
--                 desde '2026-07-20' (inicio de ese periodo). Sin
--                 vinculación vigente no puede autorregistrarse (RN-07/22).
--    PENDIENTE-4  credenciales: usuario 'alejandro.zuleta', clave TEMPORAL
--                 'Cambiar-123'. El hash se calcula aquí con pgcrypto
--                 (crypt + gen_salt('bf')): BCrypt.Net lo verifica porque es
--                 el mismo algoritmo ($2a$). Es la única excepción a "el hash
--                 se calcula en el servicio" (Regla 3): un script SQL sin API.
--                 El estudiante debe cambiarla con RF-S5 (/perfil/clave).
--                 Credencial DIDÁCTICA, como la del admin sembrado
--                 (Constitución, Art. 8): no la uses fuera de local.
--    PENDIENTE-5  la matrícula de relleno M0501 / 2026-1 se CONSERVA (es
--                 historia del archivo). El panel muestra primero el periodo
--                 más reciente, que es M0286 / 2026-2. Bórrala si el equipo
--                 decide que no debe estar.
--
--  Idempotente. Necesita las migraciones 002 (sp_guardar_asistente), 033
--  (sp_crear_usuario) y 046 (rol ESTUDIANTE).
-- ==========================================================================

SET search_path TO catedras, public;

DO $$
DECLARE
    v_asistente bigint;
    v_usuario   json;
BEGIN
    SELECT id_asistente INTO v_asistente FROM asistente WHERE id_asis = '30000124724';

    IF v_asistente IS NULL THEN
        -- BD sin los datos del script: se crea la persona completa.
        CALL catedras.sp_guardar_asistente(
            '30000124724',                          -- id_asis (código de estudiante)
            'Alejandro',                            -- nombres
            'Zuleta Valderrama',                    -- apellidos
            'Alejandro Zuleta Valderrama',          -- nombre_completo_asis
            NULL,                                   -- correo institucional
            'alejandrozuleta81@gmail.com',          -- correo personal
            NULL,                                   -- celular
            false,                                  -- es_externo
            true,                                   -- activo
            NULL,                                   -- fk_lote_carga (carga a mano)
            '[{"fk_tipo_documento":"CC","numero":"PEND-30000124724","vigente":true}]'::jsonb,
            '[{"fk_tipo_vinculacion":"ESTUDIANTE","fecha_ini":"2026-07-20"}]'::jsonb,
            '[{"fk_programa":"M0286","fk_periodo":"2026-2","estado":"ACTIVO"}]'::jsonb,
            NULL,
            v_asistente);
    ELSE
        -- Ya existe (fila de relleno del script): se ajusta a los datos pedidos.
        UPDATE asistente
           SET nombres              = 'Alejandro',
               apellidos            = 'Zuleta Valderrama',
               nombre_completo_asis = 'Alejandro Zuleta Valderrama',
               correo_institucional = NULL,
               correo_personal      = 'alejandrozuleta81@gmail.com',
               es_externo           = false,
               activo               = true
         WHERE id_asistente = v_asistente;

        INSERT INTO programa_asistente (fk_asistente, fk_programa, fk_periodo, estado)
        VALUES (v_asistente, 'M0286', '2026-2', 'ACTIVO')
        ON CONFLICT DO NOTHING;

        IF NOT EXISTS (SELECT 1 FROM vinculacion_asistente
                        WHERE fk_asistente = v_asistente
                          AND fk_tipo_vinculacion = 'ESTUDIANTE'
                          AND fecha_fin IS NULL) THEN
            INSERT INTO vinculacion_asistente (fk_asistente, fk_tipo_vinculacion, fecha_ini)
            VALUES (v_asistente, 'ESTUDIANTE', DATE '2026-07-20');
        END IF;

        IF NOT EXISTS (SELECT 1 FROM documento_asistente WHERE fk_asistente = v_asistente) THEN
            INSERT INTO documento_asistente (fk_asistente, fk_tipo_documento, numero, vigente)
            VALUES (v_asistente, 'CC', 'PEND-30000124724', true);
        END IF;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM usuario WHERE usuario = 'alejandro.zuleta')
       AND NOT EXISTS (SELECT 1 FROM usuario WHERE fk_asistente = v_asistente) THEN
        CALL catedras.sp_crear_usuario(
            v_asistente,
            'alejandro.zuleta',
            crypt('Cambiar-123', gen_salt('bf', 10)),
            true,
            v_usuario);
    END IF;

    INSERT INTO rol_por_usuario (fk_usuario, fk_rol, fecha_ini)
    SELECT u.id_usuario, 'ESTUDIANTE', DATE '2026-07-20'
      FROM usuario u
     WHERE u.usuario = 'alejandro.zuleta'
       AND NOT EXISTS (SELECT 1 FROM rol_por_usuario r
                        WHERE r.fk_usuario = u.id_usuario AND r.fk_rol = 'ESTUDIANTE');
END;
$$;
