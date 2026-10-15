document.addEventListener('DOMContentLoaded', function() {
    console.log('Catedras USB - Aplicacion cargada');

    document.querySelectorAll('.alert').forEach(alert => {
        setTimeout(() => {
            alert.style.transition = 'opacity 0.5s ease';
            alert.style.opacity = '0';
            setTimeout(() => alert.remove(), 500);
        }, 5000);
    });

    document.querySelectorAll('[data-filas]').forEach(renumerarFilas);
});


// ==========================================================================
//  Maestro-detalle en el formulario (Guía IA, Regla 4)
//  Ver templates/partials/filas_detalle.html: "Agregar" clona la fila vacía
//  del <template>; "Quitar" la elimina. Los campos de cada fila se llaman
//  igual en todas las filas y Flask los lee con request.form.getlist().
// ==========================================================================

function renumerarFilas(bloque) {
    const filas = bloque.querySelectorAll('[data-filas-cuerpo] > [data-fila]');
    filas.forEach((fila, i) => {
        fila.querySelectorAll('[data-numero]').forEach(n => { n.textContent = i + 1; });
    });
    const vacio = bloque.querySelector('[data-filas-vacio]');
    if (vacio) vacio.hidden = filas.length > 0;
}

document.addEventListener('click', function(evento) {
    const agregar = evento.target.closest('[data-agregar-fila]');
    if (agregar) {
        const bloque = document.getElementById(agregar.dataset.agregarFila);
        const plantilla = bloque.querySelector('template');
        const cuerpo = bloque.querySelector('[data-filas-cuerpo]');
        cuerpo.appendChild(plantilla.content.cloneNode(true));
        renumerarFilas(bloque);
        const nueva = cuerpo.lastElementChild;
        const primerCampo = nueva && nueva.querySelector('input, select, textarea');
        if (primerCampo) primerCampo.focus();
        return;
    }

    const quitar = evento.target.closest('[data-quitar-fila]');
    if (quitar) {
        const bloque = quitar.closest('[data-filas]');
        quitar.closest('[data-fila]').remove();
        renumerarFilas(bloque);
        return;
    }

    // Buscar una persona para una fila (Regla 2: el asistente no se digita,
    // se busca y se elige). Llena el <select> de ESA fila con lo que
    // responde /admin/asistentes/buscar?q=.
    const buscar = evento.target.closest('[data-buscar-persona]');
    if (buscar) {
        const fila = buscar.closest('[data-fila]');
        const texto = fila.querySelector('[data-texto-busqueda]').value.trim();
        const destino = fila.querySelector('[data-resultados]');
        if (!texto) return;
        destino.innerHTML = '<option value="">Buscando…</option>';
        fetch(buscar.dataset.buscarPersona + '?q=' + encodeURIComponent(texto), { headers: { 'Accept': 'application/json' } })
            .then(r => r.ok ? r.json() : Promise.reject(r.status))
            .then(personas => {
                destino.innerHTML = '';
                if (personas.length === 0) {
                    destino.innerHTML = '<option value="">Nadie coincide</option>';
                    return;
                }
                personas.forEach(p => {
                    const opcion = document.createElement('option');
                    opcion.value = p.id_asistente;
                    opcion.textContent = p.texto;
                    destino.appendChild(opcion);
                });
            })
            .catch(() => { destino.innerHTML = '<option value="">No se pudo buscar</option>'; });
    }
});

// Enter dentro del campo de búsqueda de una fila busca (no envía el formulario).
document.addEventListener('keydown', function(evento) {
    if (evento.key !== 'Enter' || !evento.target.matches('[data-texto-busqueda]')) return;
    evento.preventDefault();
    const boton = evento.target.closest('[data-fila]').querySelector('[data-buscar-persona]');
    if (boton) boton.click();
});
