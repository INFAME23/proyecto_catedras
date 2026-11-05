// ==========================================================================
//  static/geolocalizacion.js  ·  Subversión v7.1 · RF-S1
// --------------------------------------------------------------------------
//  Pide la ubicación del navegador ANTES de dejar enviar el autorregistro de
//  asistencia (templates/registro_validar.html).
//
//  DECISIÓN EXPLÍCITA (RF-S1): la ubicación es OBLIGATORIA, sin excepción.
//  Si la persona niega el permiso, el navegador no la soporta o falla, el
//  botón de enviar SE QUEDA BLOQUEADO y se muestra por qué, cómo permitirla
//  y un botón "Intentar de nuevo". No hay "continuar sin ubicación": la
//  asistencia sin ubicación no se registra. El mismo requisito se vuelve a
//  exigir en Flask (routes/registro.py), en la API (422) y en la BD
//  (sp_validar_clave_y_registrar_geo): este script es la comodidad, no la
//  garantía.
//
//  Marcado que espera la página:
//    <form data-requiere-ubicacion> con
//      · <input name="latitud"> y <input name="longitud"> (ocultos),
//      · [data-ubicacion-estado]   el texto de estado (aria-live),
//      · [data-ubicacion-reintentar] el botón de reintento,
//      · [data-ubicacion-enviar]   el botón de enviar.
//
//  Ojo: los navegadores solo entregan la ubicación en un contexto seguro
//  (https:// o http://localhost). Abrir el front por la IP de la red
//  (http://192.168.x.x:5000) la deniega siempre; el script lo avisa.
// ==========================================================================

(function () {
    'use strict';

    var formulario = document.querySelector('form[data-requiere-ubicacion]');
    if (!formulario) return;

    var campoLat = formulario.querySelector('input[name="latitud"]');
    var campoLng = formulario.querySelector('input[name="longitud"]');
    var estado = formulario.querySelector('[data-ubicacion-estado]');
    var reintentar = formulario.querySelector('[data-ubicacion-reintentar]');
    var enviar = formulario.querySelector('[data-ubicacion-enviar]');

    function mostrar(tipo, texto) {
        // tipo: 'espera' | 'ok' | 'error'
        estado.className = 'small rounded-3 p-2 mb-3 border ' + {
            espera: 'text-muted bg-light',
            ok: 'text-success border-success bg-light',
            error: 'text-danger border-danger bg-light'
        }[tipo];
        estado.textContent = texto;
    }

    function bloquear(motivo) {
        campoLat.value = '';
        campoLng.value = '';
        enviar.disabled = true;
        reintentar.hidden = false;
        mostrar('error', motivo);
    }

    function alObtener(posicion) {
        var lat = posicion.coords.latitude;
        var lng = posicion.coords.longitude;
        // numeric(9,6) en la BD: seis decimales (~0,1 m), de sobra para un GPS.
        campoLat.value = lat.toFixed(6);
        campoLng.value = lng.toFixed(6);
        enviar.disabled = false;
        reintentar.hidden = true;
        var precision = Math.round(posicion.coords.accuracy);
        mostrar('ok', 'Ubicación obtenida (precisión aproximada de ' + precision + ' m). Ya puedes registrar tu asistencia.');
    }

    function alFallar(error) {
        var mensajes = {
            1: 'Negaste el permiso de ubicación. Es obligatorio para registrar la asistencia: permítelo en el candado de la barra de direcciones (Ubicación › Permitir) y pulsa «Intentar de nuevo».',
            2: 'No se pudo determinar tu ubicación (señal o servicio de ubicación no disponible). Activa el GPS o la ubicación del dispositivo y pulsa «Intentar de nuevo».',
            3: 'Se agotó el tiempo esperando tu ubicación. Pulsa «Intentar de nuevo».'
        };
        bloquear(mensajes[error.code] || 'No se pudo obtener tu ubicación. Pulsa «Intentar de nuevo».');
    }

    function pedir() {
        enviar.disabled = true;
        reintentar.hidden = true;

        if (!('geolocation' in navigator)) {
            bloquear('Este navegador no permite obtener la ubicación, y es obligatoria para registrar la asistencia. Usa otro navegador (Chrome, Edge, Firefox o Safari actualizados).');
            reintentar.hidden = true;
            return;
        }
        if (window.isSecureContext === false) {
            bloquear('El navegador solo entrega la ubicación en páginas seguras (https://) o en localhost. Abre el sistema por la dirección https que te indicó el organizador.');
            return;
        }

        mostrar('espera', 'Solicitando tu ubicación… acepta el permiso que te pide el navegador.');
        navigator.geolocation.getCurrentPosition(alObtener, alFallar, {
            enableHighAccuracy: true,
            timeout: 20000,
            maximumAge: 0           // siempre una lectura fresca: es evidencia de dónde estaba al registrarse
        });
    }

    reintentar.addEventListener('click', pedir);

    // Última barrera en el navegador: sin coordenadas no se envía.
    formulario.addEventListener('submit', function (evento) {
        if (!campoLat.value || !campoLng.value) {
            evento.preventDefault();
            bloquear('Falta tu ubicación: es obligatoria para registrar la asistencia.');
        }
    });

    pedir();
})();
