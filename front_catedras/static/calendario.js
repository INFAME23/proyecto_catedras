// ==========================================================================
//  static/calendario.js  ·  Subversión v7.1 (D56)
//  Calendario público (FullCalendar) + el botón «Soy ponente».
//
//  Horas: las sesiones llegan de /calendario/eventos como hora de pared de
//  Colombia SIN zona, y el calendario se configura en 'UTC': se muestran tal
//  cual, sin importar la zona del navegador. Para que el indicador de «ahora»
//  coincida, 'now' se corre cinco horas (Colombia es UTC-5 todo el año).
// ==========================================================================

document.addEventListener('DOMContentLoaded', function () {
    const el = document.getElementById('calendario');
    if (!el || typeof FullCalendar === 'undefined') {
        const aviso = document.getElementById('calendario-error');
        if (aviso) {
            aviso.textContent = 'No se pudo cargar el calendario (¿sin conexión a internet?).';
            aviso.classList.remove('d-none');
        }
        return;
    }

    const aviso = document.getElementById('calendario-error');
    const modalSesionEl = document.getElementById('modal-sesion');
    const modalPonenteEl = document.getElementById('modal-ponente');
    const modalSesion = bootstrap.Modal.getOrCreateInstance(modalSesionEl);
    const modalPonente = bootstrap.Modal.getOrCreateInstance(modalPonenteEl);

    const ahoraColombia = () => new Date(Date.now() - 5 * 60 * 60 * 1000);
    const esCelular = window.matchMedia('(max-width: 768px)').matches;

    function mostrarError(texto) {
        aviso.textContent = texto;
        aviso.classList.toggle('d-none', !texto);
    }

    function formatoHora(fecha) {
        return fecha.toLocaleTimeString('es-CO', { hour: '2-digit', minute: '2-digit', timeZone: 'UTC' });
    }

    function formatoFecha(fecha) {
        return fecha.toLocaleDateString('es-CO', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric', timeZone: 'UTC' });
    }

    const calendario = new FullCalendar.Calendar(el, {
        locale: 'es',
        timeZone: 'UTC',
        now: ahoraColombia,
        nowIndicator: true,
        initialView: esCelular ? 'listMonth' : 'dayGridMonth',
        headerToolbar: {
            left: 'prev,next today',
            center: 'title',
            right: 'dayGridMonth,timeGridWeek,listMonth',
        },
        buttonText: { today: 'Hoy', month: 'Mes', week: 'Semana', list: 'Lista' },
        height: 'auto',
        firstDay: 1,
        navLinks: true,
        dayMaxEvents: 3,
        moreLinkText: n => `+${n} más`,
        noEventsText: 'No hay sesiones programadas en este periodo.',
        slotMinTime: '06:00:00',
        slotMaxTime: '22:00:00',
        allDaySlot: false,
        eventTimeFormat: { hour: '2-digit', minute: '2-digit', hour12: false },
        events: function (info, acierto, fallo) {
            const url = el.dataset.eventos + '?desde=' + encodeURIComponent(info.startStr) +
                        '&hasta=' + encodeURIComponent(info.endStr);
            fetch(url, { headers: { 'Accept': 'application/json' } })
                .then(r => r.json().then(cuerpo => ({ ok: r.ok, cuerpo })))
                .then(({ ok, cuerpo }) => {
                    if (!ok) throw new Error(cuerpo.mensaje || 'La API respondió con un error.');
                    mostrarError('');
                    acierto(cuerpo);
                })
                .catch(err => {
                    mostrarError('No se pudieron cargar las sesiones: ' + err.message);
                    fallo(err);
                });
        },
        eventClick: function (info) {
            info.jsEvent.preventDefault();
            const e = info.event;
            const p = e.extendedProps;

            modalSesionEl.querySelector('#modal-sesion-titulo').textContent = e.title;
            const q = s => modalSesionEl.querySelector(`[data-sesion="${s}"]`);
            q('cuando').textContent = `${formatoFecha(e.start)}, de ${formatoHora(e.start)} a ${formatoHora(e.end)}`;
            q('donde').textContent = [p.sede, p.lugar].filter(Boolean).join(' · ') || 'Por confirmar';
            q('modalidad').textContent = p.modalidad ? p.modalidad.charAt(0) + p.modalidad.slice(1).toLowerCase() : 'Por confirmar';
            q('reunion').textContent = `Reunión ${p.numero_reunion} de la cátedra`;
            q('enlace').href = p.url;
            modalSesion.show();
        },
    });
    calendario.render();

    // Cátedra nueva (D58): no se puede elegir una fecha pasada, y al elegir el inicio el fin se propone dos horas después.
    const campoInicio = document.getElementById('f_inicio');
    const campoFin = document.getElementById('f_fin');
    const textoLocal = fecha => fecha.toISOString().slice(0, 16);   // 'AAAA-MM-DDTHH:MM' (ver ahoraColombia)
    if (campoInicio && campoFin) {
        const minimo = textoLocal(ahoraColombia());
        campoInicio.min = minimo;
        campoFin.min = minimo;
        // Elige solo el periodo académico que cubre la fecha (si ninguno la cubre, no toca lo que ya había elegido).
        const campoPeriodo = document.getElementById('f_fk_periodo');
        const elegirPeriodo = function () {
            if (!campoPeriodo || !campoInicio.value) return;
            const dia = campoInicio.value.slice(0, 10);
            const cubre = Array.from(campoPeriodo.options).find(o => o.dataset.ini && o.dataset.ini <= dia && dia <= o.dataset.fin);
            if (cubre) campoPeriodo.value = cubre.value;
        };
        campoInicio.addEventListener('change', function () {
            elegirPeriodo();
            if (!campoInicio.value) return;
            if (!campoFin.value || campoFin.value <= campoInicio.value) {
                campoFin.value = textoLocal(new Date(new Date(campoInicio.value + ':00Z').getTime() + 2 * 60 * 60 * 1000));
            }
            campoFin.min = campoInicio.value;
        });
    }

    // Contador del resumen.
    const resumen = document.getElementById('f_resumen_ponencia');
    const contador = document.getElementById('resumen-contador');
    if (resumen && contador) {
        const contar = () => { contador.textContent = resumen.value.length; };
        resumen.addEventListener('input', contar);
        contar();
    }

    // Validación del navegador: marca lo que falta antes de enviar (el servidor valida de nuevo).
    const formulario = document.getElementById('form-ponente');
    if (formulario) {
        formulario.addEventListener('submit', function (evento) {
            if (!formulario.checkValidity()) {
                evento.preventDefault();
                evento.stopPropagation();
                formulario.classList.add('was-validated');
                const primero = formulario.querySelector(':invalid');
                if (primero) primero.focus();
            }
        });
    }

    // Si el servidor devolvió el formulario con errores, se vuelve a abrir y se lleva a la vista el primer aviso
    // (el cuerpo del formulario hace scroll: un error arriba o abajo podría quedar fuera de la pantalla).
    if (el.dataset.abrirFormulario === '1') {
        modalPonenteEl.addEventListener('shown.bs.modal', function () {
            const primero = modalPonenteEl.querySelector('.alert-danger, .is-invalid');
            if (primero) primero.scrollIntoView({ block: 'center' });
        }, { once: true });
        modalPonente.show();
    }
});
