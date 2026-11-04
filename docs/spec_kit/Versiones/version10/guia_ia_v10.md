# Guía IA — Versión 10: calendario público y solicitudes de ponentes

> **Jerarquía:** [constitución](../../1_constitution.md) › [guía IA](../../guia_ia.md)
> › spec kit v10 › esta guía. Si el spec contradice la BD real, gana la BD.
> **Estado de partida:** v8 en `main`; la v9 puede estar a medias en otra rama.

## 1. Prompt de arranque

```
Vas a trabajar la versión 10 del Proyecto Cátedras (calendario público y
solicitudes de ponentes). Lee docs/spec_kit/1_constitution.md,
docs/spec_kit/guia_ia.md, docs/spec_kit/Versiones/plan_v8_v11.md y todo
docs/spec_kit/Versiones/version10/. La v9 se construye EN PARALELO en otra rama:
no toques sus archivos (migraciones 058-060, QrSesion, proyeccion.*).
Reglas: no edites script_bdcatedras.sql; migraciones 061-063; aprobar una
solicitud es UN solo CALL que reutiliza sp_guardar_catedra y sp_guardar_asistente;
JSON en snake_case; todo en español. No contradigas D62, D64 ni D77-D80.
Trabaja solo la fase que te indique.
```

## 2. La v10 en una página

- **Franjas:**
  - el admin crea franjas por sede (una a una o con la plantilla semanal);
  - una franja está `LIBRE`, `EN_REVISION`, `OCUPADA` o `BLOQUEADA`;
  - no se solapan en la misma sede.
- **Solicitud:**
  - `/calendario` es público;
  - el ponente pincha una franja libre y envía su solicitud con sus datos ASIS.
- **Resolución:**
  - el admin aprueba la solicitud digitando el `id_evento_asis`; se crean la
    persona, el perfil de ponente, la cátedra, la sesión y la ponencia;
  - o la rechaza con un motivo, y la franja se libera.
- **Correos:** los envía el front: recibida, aprobada y rechazada.

## 3. Lo que la IA tiende a "arreglar" mal

| Tentación | Por qué no |
|---|---|
| Crear cuentas de ponente con login | El cliente dijo que solo inicia sesión el admin (D58) |
| Inventar un `id_evento_asis` provisional | D78: rompe el archivo de ASIS; lo digita el admin |
| Agregar `perfil_profesional` a `asistente` | D77: va en `perfil_ponente` |
| Aprobar con varios POST desde el front | Regla 1: un solo `CALL`, todo o nada |
| Controlar la carrera en C# con un lock | Se resuelve en la BD con `FOR UPDATE` |
| Mandar los correos desde la API | Los correos viven en `servicios/correo.py` del front |
| Mostrar el nombre del ponente en el calendario público | RNF-V10-3 |
| Tocar los archivos de la v9 | Se construye en paralelo; la integración es D79 |

## 4. Prompts por fase

- **Fase 74:** «Fase 74 de version10/8_tasks.md. Sin código. Confirma con SQL cada punto y corrige los documentos si algo no coincide.»
- **Fase 75:** «Escribe 061_franja_disponible.sql según 5_data_model.md §2, con los 5 SP en el molde de la migración 005 (sede). Escribe y corre prueba_sp_franjas_v10.sql.»
- **Fase 76:** «Escribe 062 y 063 según §3 y §4. sp_aprobar_solicitud reutiliza sp_guardar_catedra y sp_guardar_asistente: lee sus firmas antes. Prueba el todo-o-nada.»
- **Fase 77:** «Capas de FranjaDisponible y SolicitudCatedra según 6_contracts.md, con el molde de los controladores existentes. Bloque de la prueba de capas.»
- **Fase 78:** «Blueprint público calendario según 3_plan.md §4. Cuadrícula mensual en static/calendario.js, sin bibliotecas externas.»
- **Fase 79:** «Pantallas de admin para franjas y solicitudes, con el aviso de pendientes en el panel. Correos con servicios/correo.py.»
- **Fase 80:** «Integración D79 si la v9 está en main. verificar_v10.ps1 con los pasos de 7_quickstart.md §2. README y mapa.»
