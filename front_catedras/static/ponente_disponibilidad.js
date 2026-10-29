// ==========================================================================
//  static/ponente_disponibilidad.js  ·  v7.1 · D52
//  Pantalla «Nueva cátedra» del ponente (templates/ponente_catedra_nueva.html):
//    1. Cada sesión escrita se consulta sola (con una pausa corta) apenas hay
//       inicio y fin, y avisa «Disponible» o con qué sesión se cruza.
//    2. «Ver ocupación de una fecha» lista lo que ya hay ese día (y en la sede elegida).
//  Los textos que vienen del servidor se insertan con textContent (nunca como HTML).
//  Esto es una AYUDA: el servidor vuelve a comprobar todo al guardar.
// ==========================================================================
(function () {
    'use strict';

    var formulario = document.getElementById('form-catedra');
    if (!formulario) { return; }
    var urlDisponibilidad = formulario.dataset.urlDisponibilidad;
    var urlAgenda = formulario.dataset.urlAgenda;

    function valor(fila, nombre) {
        var campo = fila.querySelector('[name="' + nombre + '"]');
        return campo ? campo.value.trim() : '';
    }

    function pintar(destino, clase, texto, lineas) {
        destino.className = 'small mt-2 ' + clase;
        destino.textContent = '';
        var t = document.createElement('div');
        t.textContent = texto;
        destino.appendChild(t);
        (lineas || []).forEach(function (l) {
            var d = document.createElement('div');
            d.className = 'ms-3';
            d.textContent = '• ' + l;
            destino.appendChild(d);
        });
    }

    // ---- 1. Estado de cada sesión escrita -----------------------------------
    var pendientes = new WeakMap();

    function consultarFila(fila) {
        var destino = fila.querySelector('[data-disponibilidad]');
        if (!destino) { return; }
        var inicio = valor(fila, 'ses_inicio'), fin = valor(fila, 'ses_fin');
        if (!inicio || !fin) { destino.textContent = ''; destino.className = ''; return; }
        if (fin <= inicio) { pintar(destino, 'text-danger', 'El fin debe ser posterior al inicio.'); return; }

        var params = new URLSearchParams({
            inicio: inicio, fin: fin, sede: valor(fila, 'ses_fk_sede'), lugar: valor(fila, 'ses_lugar')
        });
        pintar(destino, 'text-muted', 'Consultando disponibilidad…');
        fetch(urlDisponibilidad + '?' + params.toString(), { headers: { 'Accept': 'application/json' } })
            .then(function (r) { return r.json().then(function (j) { return { ok: r.ok, json: j }; }); })
            .then(function (r) {
                if (!r.ok) { pintar(destino, 'text-warning', r.json.error || 'No se pudo consultar la disponibilidad.'); return; }
                if (r.json.disponible) {
                    pintar(destino, 'text-success', '✔ Horario disponible.');
                } else {
                    pintar(destino, 'text-danger', '✖ Horario NO disponible: se cruza con',
                        r.json.conflictos.map(function (c) { return c.texto; }));
                }
            })
            .catch(function () { pintar(destino, 'text-warning', 'No se pudo consultar la disponibilidad.'); });
    }

    function programar(fila) {
        clearTimeout(pendientes.get(fila));
        pendientes.set(fila, setTimeout(function () { consultarFila(fila); }, 450));
    }

    ['input', 'change'].forEach(function (evento) {
        formulario.addEventListener(evento, function (e) {
            var fila = e.target.closest ? e.target.closest('[data-fila]') : null;
            if (fila && /^ses_(inicio|fin|fk_sede|lugar)$/.test(e.target.name || '')) { programar(fila); }
        });
    });
    // Al volver con un error del servidor las filas ya traen datos: se consultan al cargar.
    formulario.querySelectorAll('[data-filas-cuerpo] [data-fila]').forEach(consultarFila);

    // ---- 2. Ocupación de una fecha ------------------------------------------------
    var boton = document.getElementById('ver-ocupacion');
    var campoFecha = document.getElementById('ocupacion-fecha');
    var campoSede = document.getElementById('ocupacion-sede');
    var resultado = document.getElementById('ocupacion-resultado');
    if (!boton) { return; }

    boton.addEventListener('click', function () {
        if (!campoFecha.value) { pintar(resultado, 'text-danger', 'Elige una fecha.'); return; }
        var params = new URLSearchParams({ fecha: campoFecha.value, sede: campoSede.value });
        pintar(resultado, 'text-muted', 'Consultando…');
        fetch(urlAgenda + '?' + params.toString(), { headers: { 'Accept': 'application/json' } })
            .then(function (r) { return r.json().then(function (j) { return { ok: r.ok, json: j }; }); })
            .then(function (r) {
                if (!r.ok) { pintar(resultado, 'text-warning', r.json.error || 'No se pudo consultar la agenda.'); return; }
                var sesiones = r.json.sesiones;
                if (!sesiones.length) {
                    pintar(resultado, 'text-success', '✔ El ' + r.json.fecha + ' no hay ninguna sesión programada' +
                        (campoSede.value ? ' en esa sede.' : '.'));
                    return;
                }
                pintar(resultado, 'text-body', 'El ' + r.json.fecha + ' ya hay ' + sesiones.length +
                    (sesiones.length === 1 ? ' sesión' : ' sesiones') + ' (hora de Colombia). Elige un horario que no se cruce:',
                    sesiones.map(function (s) {
                        return s.hora + ' · ' + s.nombre_catedra + ' (reunión ' + s.numero_reunion + ') · ' +
                            (s.nombre_sede || s.fk_sede) + (s.lugar ? ', ' + s.lugar : '');
                    }));
            })
            .catch(function () { pintar(resultado, 'text-warning', 'No se pudo consultar la agenda.'); });
    });
}());
