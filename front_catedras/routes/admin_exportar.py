# ==========================================================================
#  routes/admin_exportar.py  ·  Front Cátedras · Subversión v7.2 · RF-S13 / RF-S14
# --------------------------------------------------------------------------
#  EXPORTACIÓN DE FIN DE PERIODO (solo ADMIN: las rutas cuelgan del blueprint /admin,
#  cuyo before_request exige el rol ADMIN; ver routes/admin.py).
#
#  Para qué sirve: llevar el recuento de A CUÁNTAS CÁTEDRAS ASISTIÓ cada estudiante.
#  Un estudiante aparece en un archivo por cada cátedra a la que fue; contar en
#  cuántos archivos sale su carnet da el total. El sistema NO calcula ese total.
#
#    GET /admin/exportar?periodo=2026-2                               la pantalla (select de periodo)
#    GET /admin/exportar/<periodo>/catedra/<id>/<tipo>.csv            un CSV de una cátedra (tipo = estudiantes | docentes | externos)
#    GET /admin/exportar/<periodo>/catalogo.csv                       id_catedra, titulo de TODAS las cátedras del periodo
#    GET /admin/exportar/<periodo>/todas.zip                          los 3 CSV de cada cátedra del periodo
#
#  Los datos salen de GET /api/Exportacion/... (la BD decide quién cuenta y evita repetidos);
#  aquí solo se arma el archivo (servicios/exportacion.py).
# ==========================================================================

from datetime import date

from flask import Response, abort, flash, redirect, render_template, request, url_for

from servicios import exportacion
from servicios.api_catedras import ErrorApi, listar


def _descarga(contenido, nombre, tipo_mime):
    """Respuesta que el navegador GUARDA como archivo (Content-Disposition: attachment)."""
    return Response(contenido, mimetype=tipo_mime, headers={'Content-Disposition': f'attachment; filename="{nombre}"'})


def _csv(contenido, nombre):
    return _descarga(contenido, nombre, 'text/csv; charset=utf-8')


def _avisar(ex):
    flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')


def _periodos():
    """Los periodos académicos para el <select> (Regla 2), del más reciente al más antiguo, y el que se propone
    por defecto: el que cubre la fecha de hoy o, si ninguno, el más reciente."""
    periodos = sorted(listar('PeriodoAcademico', limite=500), key=lambda p: p['id_periodo'], reverse=True)
    hoy = date.today().isoformat()
    por_defecto = next((p['id_periodo'] for p in periodos if p['fecha_ini'] <= hoy <= p['fecha_fin']), None)
    if por_defecto is None and periodos:
        por_defecto = periodos[0]['id_periodo']
    return periodos, por_defecto


def registrar_rutas(admin_bp):
    """Cuelga las rutas de exportación del blueprint de administración (mismo patrón que admin_restricciones)."""

    @admin_bp.route('/exportar')
    def exportar():
        try:
            periodos, por_defecto = _periodos()
        except ErrorApi as ex:
            _avisar(ex)
            periodos, por_defecto = [], None

        periodo = request.args.get('periodo') or por_defecto
        if periodo and not exportacion.periodo_valido(periodo):
            abort(400)

        catedras, error = [], None
        if periodo:
            try:
                catedras = exportacion.catedras_del_periodo(periodo)
            except ErrorApi as ex:
                error = ex

        totales = {'estudiantes': sum(c['estudiantes'] for c in catedras),
                   'docentes': sum(c['docentes'] for c in catedras),
                   'externos': sum(c['externos'] for c in catedras),
                   'otros': sum(c['otros'] for c in catedras)}
        return render_template('admin/exportar.html', periodos=periodos, periodo=periodo, catedras=catedras,
                               totales=totales, error=error)

    @admin_bp.route('/exportar/<periodo>/catedra/<id_catedra>/<tipo>.csv')
    def exportar_catedra_csv(periodo, id_catedra, tipo):
        if tipo not in exportacion.TIPOS or not exportacion.periodo_valido(periodo) or not exportacion.id_catedra_valido(id_catedra):
            abort(404)
        try:
            asistentes = exportacion.asistentes_de_una_catedra(periodo, id_catedra)
        except ErrorApi as ex:
            _avisar(ex)
            return redirect(url_for('admin.exportar', periodo=periodo))
        return _csv(exportacion.csv_de_asistentes(tipo, asistentes), exportacion.nombre_archivo(id_catedra, tipo))

    @admin_bp.route('/exportar/<periodo>/catalogo.csv')
    def exportar_catalogo(periodo):
        if not exportacion.periodo_valido(periodo):
            abort(404)
        try:
            catedras = exportacion.catedras_del_periodo(periodo)
        except ErrorApi as ex:
            _avisar(ex)
            return redirect(url_for('admin.exportar', periodo=periodo))
        return _csv(exportacion.csv_catalogo(catedras), f'catedras_{periodo}.csv')

    @admin_bp.route('/exportar/<periodo>/todas.zip')
    def exportar_zip(periodo):
        if not exportacion.periodo_valido(periodo):
            abort(404)
        try:
            contenido, cantidad = exportacion.zip_del_periodo(periodo)
        except ErrorApi as ex:
            _avisar(ex)
            return redirect(url_for('admin.exportar', periodo=periodo))
        if cantidad == 0:
            flash(f'El periodo {periodo} no tiene cátedras: no hay nada que comprimir.', 'error')
            return redirect(url_for('admin.exportar', periodo=periodo))
        return _descarga(contenido, f'asistencia_{periodo}.zip', 'application/zip')
