# Especificación — Versión 9 del proyecto: QR dinámico y control por modalidad

> ⚠️ **BORRADOR · por construir.** Depende de la **v8 fusionada en `main`**. Se
> puede construir en paralelo con la [v10](../version10/2_spec.md) (D70): no
> comparten archivos, salvo el bloque de `docker-compose.yml`, donde cada una
> agrega sus propias líneas.
>
> Rige la [constitución](../../1_constitution.md). Plan:
> [../plan_v8_v11.md](../plan_v8_v11.md). Decisiones:
> [../decisiones_v8_v11.md](../decisiones_v8_v11.md) (D62, D63) y
> [4_research.md](4_research.md) (D74–D76). Guía: [guia_ia_v9.md](guia_ia_v9.md).
>
> | Documento | Contenido |
> |---|---|
> | **2_spec.md** (este) | Qué construir y sus criterios de aceptación |
> | [3_plan.md](3_plan.md) | Cómo: capas y pantallas |
> | [4_research.md](4_research.md) | D62, D63, D74–D76 |
> | [5_data_model.md](5_data_model.md) | Migraciones 058–060 |
> | [6_contracts.md](6_contracts.md) | Endpoints |
> | [7_quickstart.md](7_quickstart.md) | Prueba |
> | [8_tasks.md](8_tasks.md) | Fases 69–73 |

---

## 1. Propósito de la v9

En la v8 cualquiera que tenga el enlace y conozca un ID puede registrar a otra
persona, incluso desde la casa si la sesión es virtual. El cliente pidió un **QR
dinámico que cambie cada minuto** (R 16:30, notas del compañero): un pantallazo
reenviado deja de servir enseguida.

```
Pantalla de proyección (ponente con enlace secreto, o admin)
   └─ cada 60 s pide GET /api/QrSesion/{id}/codigo → código C(t)
        └─ dibuja el QR de  https://…/registro/<token>?c=<C(t)>
Celular
   └─ abre /registro/<token>?c=…  → el código se cambia por un "pase" de 10 min
        └─ el formulario envía el pase → el procedimiento de la v8 lo valida
```

## 2. Alcance

**Incluye:**
- el código rotativo y el pase (RF218–RF223);
- la pantalla de proyección y el enlace secreto de proyección (RF224–RF227);
- la ventana de registro al final de la sesión, que ya existe (RF228).

**No incluye:**
- enviar el enlace secreto al ponente por correo: llega con la V10, que es
  donde se aprueba la cátedra;
- cambiar la ventana de registro; los parámetros de la v7.1 siguen.

## 3. Requisitos funcionales

La numeración continúa la de la v8, que terminó en RF217.

### Código rotativo

- **RF218 — Secreto por enlace.** Todo enlace o QR que se emita desde la v9 nace
  con un `secreto` aleatorio de 32 bytes (`gen_random_bytes`). Los enlaces
  anteriores quedan con `secreto = NULL` y siguen como en la v8, sin código.
- **RF219 — Código vigente (D63, D74).** Para un instante `t`:
  - `paso = floor(epoch(t) / QR_ROTACION_SEGUNDOS)`;
  - `C(t)` = los primeros 8 caracteres en base32 de `hmac(paso, secreto, 'sha256')`.

  `QR_ROTACION_SEGUNDOS` vale 60 por defecto.
- **RF220 — Tolerancia.** Un código es válido si coincide con el del paso actual
  o con alguno de los `QR_CODIGOS_TOLERADOS` pasos anteriores (1 por defecto).
  Es decir, vale entre 60 y 120 s.
- **RF221 — Canje por un pase (D75).** `POST /api/Registro/pase` con
  `{token, codigo}`:
  - si el código es válido → `200 {pase, expira_en}`;
  - el pase vale `QR_PASE_MINUTOS` (10 por defecto) para llenar el formulario;
  - si el código no es válido → 500 con «V9: el código del QR ya expiró;
    escanea el que está en pantalla».
- **RF222 — El registro exige el pase.** Si el enlace tiene `secreto`, los
  procedimientos de la v8 exigen un pase válido de ese mismo enlace. Las
  peticiones `Registro*` agregan el campo `pase`. Sin pase, o con uno vencido o
  de otro enlace → 500 con «V9: …».
- **RF223 — Front.** `/registro/<token>?c=<codigo>` canjea el código apenas se
  abre:
  - si es válido, guarda el pase en un campo oculto del formulario y quita `c`
    de la URL;
  - si no, muestra «Este código ya expiró, escanea el QR que está en pantalla».

### Proyección

- **RF224 — Código para la pantalla.** `GET /api/QrSesion/{idSesion}/codigo?secreto=` (o con la sesión de admin desde el front)
  → `200 {url_registro, codigo, segundos_restantes, rotacion_segundos}`. Sin un
  secreto de proyección válido → 404.
- **RF225 — Enlace secreto de proyección (D62, D76).**
  `POST /api/QrSesion/{idSesion}/enlace-proyeccion`:
  - crea (o, con `regenerar: true`, reemplaza) un token de proyección para la sesión;
  - devuelve `{url_proyeccion}` con la forma `https://…/proyectar/<token_proyeccion>`;
  - en la BD solo se guarda el hash del token.
- **RF226 — Pantalla de proyección.** `/proyectar/<token_proyeccion>` (sin login)
  y `/admin/sesiones/<id>/qr` (con login):
  - QR grande y nombre de la cátedra;
  - cuenta regresiva hasta el siguiente cambio;
  - contador de registrados;
  - se redibuja sola, sin recargar la página.
- **RF227 — Contador en vivo.** La pantalla consulta cada 15 s
  `GET /api/QrSesion/asistencia?fkSesion=` (ya existe, migración 051) y muestra
  el total.

### Ventana

- **RF228 — Ventana al final (se mantiene).** El QR solo sirve dentro de la
  ventana del enlace: `QR_MINUTOS_ANTES_FIN` antes del fin hasta
  `QR_MINUTOS_DESPUES_FIN` después. Fuera de ella, la pantalla de proyección
  muestra «El registro abre a las HH:MM».

## 4. Requisitos no funcionales

- **RNF-V9-1:** el código no se puede adivinar ni calcular sin el `secreto`, que
  nunca sale de la BD.
- **RNF-V9-2:** la pantalla de proyección tolera una conexión lenta: si no
  alcanza a refrescar, sigue mostrando el último código y avisa «sin conexión».
- **RNF-V9-3:** los relojes los manda la BD (`now()`), no el navegador.

## 5. Criterios de aceptación

1. Un pantallazo del QR deja de servir unos 2 minutos después: el canje del
   código falla.
2. Quien escaneó a tiempo tiene 10 minutos para llenar el formulario, aunque el
   QR ya haya cambiado.
3. Un pase de la sesión A no sirve para registrarse en la sesión B.
4. `/proyectar/<token>` muestra el QR de esa sesión y de ninguna otra. Con un
   token inventado → 404.
5. El contador sube después de un registro.
6. Los enlaces emitidos antes de la v9 siguen funcionando como en la v8.
