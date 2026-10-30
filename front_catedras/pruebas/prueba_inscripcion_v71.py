# Prueba v7.1 (D50), sin BD ni API: el estudiante se inscribe a una cátedra, la ve en su panel
# y puede cancelar. La API se sustituye por un almacén en memoria.
# Uso, desde front_catedras:   python pruebas/prueba_inscripcion_v71.py
import os
import sys

os.chdir(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
sys.path.insert(0, '.')
import werkzeug
werkzeug.__version__ = '3'

import app as A
import routes.admin as AD
import routes.catedras as CA
import routes.estudiante as ES
import routes.ponente_catedras as PC
from servicios.api_catedras import ErrorApi

CATEDRAS = {10: {'id_catedra': 10, 'id_evento_asis': '000000010', 'nombre': 'Catedra Diez', 'nombre_asis': 'Catedra Diez',
                 'fk_tipo_evento': 'CATE', 'fk_dependencia': None, 'activo': True, 'creado_en': '2026-01-01T00:00:00Z'},
            11: {'id_catedra': 11, 'id_evento_asis': '000000011', 'nombre': 'Catedra Inactiva', 'nombre_asis': 'x',
                 'fk_tipo_evento': 'CATE', 'fk_dependencia': None, 'activo': False, 'creado_en': '2026-01-01T00:00:00Z'}}
insc = {}          # (catedra, asistente) -> 'ACTIVA' | 'CANCELADA'
llamadas = []


def fila(c, a):
    return {'id_inscripcion': 1, 'fk_catedra': c, 'id_evento_asis': CATEDRAS[c]['id_evento_asis'], 'nombre_catedra': CATEDRAS[c]['nombre'],
            'fk_asistente': a, 'id_asis': '1', 'nombres': 'Ana', 'apellidos': 'Z', 'estado': insc[(c, a)],
            'inscrita_en': '2026-10-01T10:00:00Z', 'cancelada_en': None}


def listar(recurso, limite=None, **f):
    if recurso == 'InscripcionCatedra':
        todas = f.get('soloActivas') == 'false'
        return [fila(c, a) for (c, a), e in insc.items()
                if f.get('fkAsistente') in (None, a) and f.get('fkCatedra') in (None, c) and (todas or e == 'ACTIVA')]
    return []


def obtener(recurso, i):
    if recurso == 'Catedra' and int(i) in CATEDRAS:
        return CATEDRAS[int(i)]
    if recurso == 'Asistente':
        return {'id_asistente': i, 'nombres': 'Ana', 'apellidos': 'Z', 'id_asis': '1'}
    raise ErrorApi('No existe.', codigo=404)


def crear(recurso, d):
    llamadas.append((recurso, d))
    c, a = d['fk_catedra'], d['fk_asistente']
    if recurso == 'InscripcionCatedra':
        if c not in CATEDRAS or not CATEDRAS[c]['activo']:
            raise ErrorApi('No se pudo inscribir.', detalle='no existe', codigo=404)
        ya = insc.get((c, a)) == 'ACTIVA'
        insc[(c, a)] = 'ACTIVA'
        return {'estado': 'ya_inscrita' if ya else 'inscrita'}
    if recurso == 'InscripcionCatedra/cancelar':
        if insc.get((c, a)) != 'ACTIVA':
            raise ErrorApi('No hay inscripcion activa.', codigo=404)
        insc[(c, a)] = 'CANCELADA'
        return {'estado': 'cancelada'}
    raise AssertionError(recurso)


for m in (CA, ES, AD, PC):
    m.listar = listar
    if hasattr(m, 'obtener'):
        m.obtener = obtener
CA.obtener = obtener
ES.crear = crear

app = A.app
app.config['TESTING'] = True
ok = fall = 0


def v(caso, cond):
    global ok, fall
    print(('  OK    ' if cond else '  FALLA ') + caso)
    ok += bool(cond)
    fall += (not cond)


def cliente(roles, asistente=5, seguir=False):
    c = app.test_client()
    if roles is not None:
        with c.session_transaction() as s:
            s['usuario'] = {'id_usuario': asistente, 'usuario': f'u{asistente}', 'fk_asistente': asistente}
            s['roles'] = roles
    return c


EST = ['ESTUDIANTE']

# --- el catálogo: quién ve el botón ------------------------------------------
t = cliente(EST).get('/catedras/10').get_data(as_text=True)
v('ESTUDIANTE: la cátedra muestra «Inscribirme»', 'Inscribirme' in t and '/estudiante/catedras/10/inscribirse' in t)
t = cliente(None).get('/catedras/10').get_data(as_text=True)
v('Anónimo: ve la información pero no el botón', 'Código ASIS' in t and 'Inscribirme' not in t)
t = cliente(['PONENTE']).get('/catedras/10').get_data(as_text=True)
v('PONENTE: no ve el botón de inscripción', 'Inscribirme' not in t)
t = cliente(EST).get('/catedras/11').get_data(as_text=True)
v('Cátedra inactiva: no se ofrece inscribirse', 'Inscribirme' not in t and 'no admite inscripciones' in t)

# --- inscribirse --------------------------------------------------------------
r = cliente(EST).post('/estudiante/catedras/10/inscribirse')
v('ESTUDIANTE: POST inscribirse -> redirige a la cátedra', r.status_code == 302 and r.headers['Location'].endswith('/catedras/10'))
v('...queda ACTIVA con SU persona (5)', insc.get((10, 5)) == 'ACTIVA')
t = cliente(EST).get('/catedras/10').get_data(as_text=True)
v('...y la cátedra ahora dice «inscrito» con «Cancelar inscripción»', 'inscrito' in t and 'Cancelar inscripción' in t and 'Inscribirme' not in t)
llamadas.clear()
cliente(EST).post('/estudiante/catedras/10/inscribirse')
v('Inscribirse otra vez no duplica (la API responde ya_inscrita)', insc == {(10, 5): 'ACTIVA'} and len(llamadas) == 1)

# --- la persona sale de la sesión, no del formulario -------------------------------
llamadas.clear()
cliente(EST, asistente=6).post('/estudiante/catedras/10/inscribirse', data={'fk_asistente': '5', 'fk_catedra': '99'})
v('Un fk_asistente/fk_catedra en el formulario se ignora: se inscribe la persona de la sesión (6) en la cátedra de la URL',
  llamadas == [('InscripcionCatedra', {'fk_catedra': 10, 'fk_asistente': 6})] and insc[(10, 6)] == 'ACTIVA')

# --- panel ----------------------------------------------------------------------
t = cliente(EST).get('/estudiante/').get_data(as_text=True)
v('El panel lista «Mis cátedras» con la cátedra inscrita', 'Mis cátedras' in t and 'Catedra Diez' in t and 'Cancelar' in t)
t = cliente(EST, asistente=7).get('/estudiante/').get_data(as_text=True)
v('Otro estudiante sin inscripciones ve el aviso vacío, no la de otro', 'Catedra Diez' not in t and 'Todavía no estás inscrito' in t)

# --- cancelar -----------------------------------------------------------------------
r = cliente(EST).post('/estudiante/catedras/10/cancelar-inscripcion')
v('POST cancelar-inscripcion -> CANCELADA solo la propia (la de la persona 6 sigue ACTIVA)',
  r.status_code == 302 and insc[(10, 5)] == 'CANCELADA' and insc[(10, 6)] == 'ACTIVA')
t = cliente(EST).get('/estudiante/').get_data(as_text=True)
v('...y desaparece de «Mis cátedras»', 'Catedra Diez' not in t)
r = cliente(EST).post('/estudiante/catedras/10/cancelar-inscripcion', follow_redirects=True)
v('Cancelar sin inscripción activa avisa y no falla', r.status_code == 200 and 'no tienes una inscripción activa' in r.get_data(as_text=True))
cliente(EST).post('/estudiante/catedras/10/inscribirse')
v('Volver a inscribirse tras cancelar la reactiva', insc[(10, 5)] == 'ACTIVA')

# --- quién puede actuar -----------------------------------------------------------------
for etiqueta, roles, esperado in (('anónimo', None, 302), ('PONENTE', ['PONENTE'], 403), ('ADMIN', ['ADMIN'], 403)):
    llamadas.clear()
    for ruta in ('/estudiante/catedras/10/inscribirse', '/estudiante/catedras/10/cancelar-inscripcion'):
        r = cliente(roles).post(ruta)
        v(f'{etiqueta}: POST {ruta.split("/")[-1]} -> {esperado} y no toca la API', r.status_code == esperado and not llamadas)

# --- admin y ponente ----------------------------------------------------------------------
r = cliente(['ADMIN']).get('/admin/inscripciones')
t = r.get_data(as_text=True)
v('ADMIN: /admin/inscripciones lista las inscripciones (nombre de cátedra y persona)', r.status_code == 200 and 'Catedra Diez' in t and 'Ana Z' in t)
v('ESTUDIANTE y PONENTE no abren /admin/inscripciones (403)',
  cliente(EST).get('/admin/inscripciones').status_code == 403 and cliente(['PONENTE']).get('/admin/inscripciones').status_code == 403)
t = cliente(['ADMIN']).get('/admin/inscripciones?fk_catedra=10&todas=1').get_data(as_text=True)
v('ADMIN: con filtros y «incluir canceladas» responde 200', 'Inscripciones a cátedras' in t)

# --- la opción es PERMANENTE: ficha de la cátedra, no un aviso que main.js quita a los 5 s ---------
import re
t = cliente(EST, asistente=8).get('/catedras/10').get_data(as_text=True)
ficha = re.search(r'<dd class="col-sm-8" id="inscripcion">(.*?)</dd>', t, re.S)
v('«Inscribirme» está dentro de la ficha de la cátedra (fila «Inscripción»)', ficha is not None and 'Inscribirme' in ficha.group(1))
v('...y no hay ningún .alert que la contenga (nada que se borre solo)', 'class="alert' not in (ficha.group(1) if ficha else 'class="alert'))
t = cliente(None).get('/catedras/10').get_data(as_text=True)
v('Anónimo: la fila «Inscripción» invita a iniciar sesión (con regreso a la cátedra)', 'Inicia sesión como estudiante' in t and 'siguiente=' in t)
t = cliente(['PONENTE']).get('/catedras/10').get_data(as_text=True)
v('PONENTE: la fila dice que solo los estudiantes se inscriben', 'Solo los estudiantes se inscriben' in t)
js = open('static/main.js', encoding='utf-8-sig').read()
v("main.js solo quita solos los flash de ÉXITO ('.alert-success.alert-dismissible'): ni los avisos ni los errores",
  "querySelectorAll('.alert.alert-success.alert-dismissible')" in js and "querySelectorAll('.alert')" not in js
  and "querySelectorAll('.alert.alert-dismissible')" not in js)

# --- pedidos de limpieza: panel sin «Mis sesiones» y portada sin el texto de la API --------------------
t = cliente(EST).get('/estudiante/').get_data(as_text=True)
v('El panel del estudiante ya NO tiene «Mis sesiones» (conserva información, Mis cátedras y atajos)',
  'Mis sesiones' not in t and 'Todavía no te has registrado' not in t and 'Mis cátedras' in t and 'Mi información' in t)
t = cliente(None).get('/').get_data(as_text=True)
v('La pantalla de inicio ya NO dice «Este front consume ApiCatedras...» y conserva sus botones',
  'Este front consume' not in t and 'ApiCatedras' not in t and 'Ver el catálogo de cátedras' in t)

print(f'\n{ok} pasadas, {fall} fallidas')
sys.exit(1 if fall else 0)
