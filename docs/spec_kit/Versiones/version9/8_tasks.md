# Tareas — Versión 9: QR dinámico y control por modalidad

> ⚠️ **BORRADOR · por construir** — ver nota de [2_spec.md](2_spec.md). Las fases
> continúan las de la [v8](../version8/8_tasks.md) (Fases 62–68). La v10 usa las
> Fases 74–80, así que las dos pueden avanzar en paralelo sin chocar.

## Fase 69 — Preparación
- [ ] La v8 está fusionada en `main` y la rama de la v9 sale de ahí.
- [ ] Confirmar en la BD:
  - [ ] que `hmac` y `gen_random_bytes` existen (`\df hmac`);
  - [ ] la zona horaria y el reloj del contenedor.
- [ ] Decidir el formato del código: base32 de 8 caracteres o hex de 10 (D74).
      Anotarlo en `4_research.md`.

## Fase 70 — Migraciones 058 y 059
- [ ] `058_qr_dinamico.sql` y `059_pase_registro.sql`, con su montaje en compose.
- [ ] `db/postgres/pruebas/prueba_sp_qr_v9.sql`:
  - [ ] el código cambia de un paso al siguiente;
  - [ ] se tolera el anterior y se rechaza el de hace 3 pasos;
  - [ ] el pase vale 10 min y no sirve en otro enlace;
  - [ ] un enlace sin secreto se registra sin pase;
  - [ ] un enlace con secreto se rechaza sin pase.
  Para mover los pasos se pasa `p_paso` explícito; no se usa `pg_sleep`.

## Fase 71 — Migración 060 y API
- [ ] `060_enlace_proyeccion.sql`.
- [ ] API: `POST /api/Registro/pase`; el campo `pase` en las peticiones;
      `QrSesion` con `/codigo`, `/enlace-proyeccion` y `/proyeccion/{t}`.
- [ ] Prueba de capas: bloque "V9 · QR dinámico".

## Fase 72 — Front
- [ ] Canje de `?c=` en `routes/registro.py`, con el pase guardado en la sesión de Flask.
- [ ] Blueprint `proyeccion` (público), `templates/proyeccion.html`,
      `static/proyeccion.js` y `static/vendor/qrcode.min.js`.
- [ ] `/admin/sesiones/<id>/qr` y el botón «Generar enlace de proyección».
- [ ] Sale de servicio la pantalla fija `ponente_sesion_qr.html`, sin borrarla (D58).
- [ ] Verificación a mano: el §3 del quickstart.

## Fase 73 — Cierre de la v9
- [ ] `pruebas/verificar_v9.ps1` y `correr_todo_v9.ps1` en verde.
- [ ] README y `0_mapa_versiones.md` actualizados.
- [ ] Banner fuera, tag `v9` y PR a `main`.
