# Prueba v7.1 (D54), sin BD ni API: el estudiante entra con su correo institucional, su clave inicial es su
# código y en el primer ingreso se le obliga a cambiarla; la portada es genérica; la barra no trae
# «Registro de asistencia». La API se sustituye por datos falsos.
# Uso, desde front_catedras:   python pruebas/prueba_primer_ingreso_v71.py
import os
import re
import sys

os.chdir(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
sys.path.insert(0, '.')
import werkzeug
werkzeug.__version__ = '3'

import app as A
import routes.admin as AD
import routes.auth as AU
import routes.perfil as PF
import routes.estudiante as ES
from servicios.api_catedras import ErrorApi

llamadas = []
estado = {'debe': True, 'roles': ['ESTUDIANTE'], 'cambio_ok': True}


def crear(recurso, d):
    llamadas.append((recurso, d))
    if recurso == 'Usuario/login':
        if d['usuario'] != 'ana.perez@usbmed.edu.co' or d['clave'] != '3000123456' and not d['clave'].startswith('Nueva'):
            raise ErrorApi('Usuario o clave incorrectos', codigo=401)
        return {'estado': 'autenticado', 'roles': estado['roles'],
                'usuario': {'id_usuario': 9, 'usuario': 'ana.perez', 'fk_asistente': 40, 'debe_cambiar_clave': estado['debe']}}
    if recurso.endswith('/cambiar-clave'):
        if not estado['cambio_ok']:
            raise ErrorApi('La clave actual no es correcta', codigo=401)
        return {'estado': 'cambiada'}
    if recurso.endswith('/exigir-cambio-clave'):
        return {'estado': 'exigido'}
    if recurso in ('Asistente', 'Usuario', 'RolPorUsuario'):
        return {'id_asistente': 70, 'id_usuario': 71, 'mensaje': 'ok'}
    raise AssertionError(recurso)


AU.crear = crear
PF.crear = crear
AD.crear = crear
ES.listar = lambda *a, **k: []
ES.obtener = lambda r, i: {'id_asistente': i, 'nombres': 'Ana', 'apellidos': 'Perez', 'id_asis': '3000123456'}
AD.listar = lambda recurso, **f: []

app = A.app
app.config['TESTING'] = True
ok = fall = 0


def v(caso, cond):
    global ok, fall
    print(('  OK    ' if cond else '  FALLA ') + caso)
    ok += bool(cond)
    fall += (not cond)


def cliente(roles=None):
    c = app.test_client()
    if roles is not None:
        with c.session_transaction() as s:
            s['usuario'] = {'id_usuario': 1, 'usuario': 'u', 'fk_asistente': 40}
            s['roles'] = roles
    return c


# ---------------------------------------------------------------- 1. barra y portada
for etiqueta, roles in (('anónimo', None), ('estudiante', ['ESTUDIANTE']), ('ponente', ['PONENTE']), ('administrador', ['ADMIN'])):
    t = cliente(roles).get('/').get_data(as_text=True)
    v(f'{etiqueta}: la barra ya NO tiene «Registro de asistencia»', 'Registro de asistencia' not in t.split('<main')[0])

t = cliente(None).get('/').get_data(as_text=True)
v('Portada (visitante): explica qué son las cátedras y cómo participar', '¿Qué son las cátedras?' in t and 'Cómo participar en una cátedra' in t)
v('...con información para estudiantes, ponentes y la asistencia con QR', 'Para estudiantes' in t and 'Para ponentes' in t and 'Asistencia con QR' in t)
v('...y sin atajos de administración ni el texto técnico anterior',
  '/admin' not in t and 'Ir a Administración' not in t and 'Este front consume' not in t and 'admin.sedes' not in t)
v('...el visitante ve «Explorar el catálogo» e «Iniciar sesión»', 'Explorar el catálogo de cátedras' in t and 'Iniciar sesión' in t.split('<main')[1])
t = cliente(['ESTUDIANTE']).get('/').get_data(as_text=True).split('<main')[1]
v('Estudiante: el botón de la portada lleva a su panel (y no a /admin)', 'Mi panel' in t and '/estudiante' in t and '/admin' not in t)
t = cliente(['PONENTE']).get('/').get_data(as_text=True).split('<main')[1]
v('Ponente: el botón lleva a su panel de ponente', 'Mi panel de ponente' in t and '/admin' not in t)
t = cliente(['ADMIN']).get('/').get_data(as_text=True).split('<main')[1]
v('Administrador: el botón lleva a Administración', 'href="/admin/"' in t)

# ---------------------------------------------------------------- 2. pantalla de login
t = cliente(None).get('/login').get_data(as_text=True)
v('Login: «Usuario o correo institucional» y la explicación para estudiantes (código como clave inicial)',
  'Usuario o correo institucional' in t and 'tu código de estudiante' in t)

# ---------------------------------------------------------------- 3. primer ingreso
c = cliente(None)
llamadas.clear()
r = c.post('/login', data={'usuario': 'ana.perez@usbmed.edu.co', 'clave': '3000123456'})
v('PRIMER INGRESO: entra con el correo institucional y el código; la API recibe exactamente eso',
  llamadas[0] == ('Usuario/login', {'usuario': 'ana.perez@usbmed.edu.co', 'clave': '3000123456'}))
v('...y va derecho a CAMBIAR LA CLAVE (no a su panel)', r.status_code == 302 and r.headers['Location'].endswith('/perfil/clave'))
with c.session_transaction() as s:
    v('...la sesión queda marcada como «debe cambiar la clave»', s.get('debe_cambiar') is True and s.get('roles') == ['ESTUDIANTE'])

t = c.get('/perfil/clave').get_data(as_text=True)
v('La pantalla dice que es el primer ingreso y que la clave actual es el código; el aviso no se cierra solo',
  'Es tu primer ingreso' in t and 'código de estudiante' in t and 'id="primer-ingreso"' in t and 'alert-dismissible' not in t.split('id="primer-ingreso"')[1][:200])
for ruta in ('/', '/catedras', '/estudiante/', '/registro', '/sesiones/1', '/ponente/', '/admin/', '/recuperar'):
    r = c.get(ruta)
    v(f'Hasta que cambie la clave, GET {ruta} lo devuelve a cambiarla', r.status_code == 302 and r.headers['Location'].endswith('/perfil/clave'))
r = c.post('/estudiante/catedras/1/inscribirse')
v('...tampoco puede hacer acciones (POST inscribirse)', r.status_code == 302 and r.headers['Location'].endswith('/perfil/clave'))
v('...pero los archivos estáticos sí cargan', c.get('/static/estilo.css').status_code == 200)

r = c.post('/perfil/clave', data={'clave_actual': '3000123456', 'clave_nueva': 'Nueva-Clave-54', 'clave_confirmacion': 'otra-cosa'})
v('Con la confirmación distinta, sigue en la pantalla y sigue obligado', r.status_code == 200 and 'no coincide' in r.get_data(as_text=True))
r = c.post('/perfil/clave', data={'clave_actual': '3000123456', 'clave_nueva': '3000123456', 'clave_confirmacion': '3000123456'})
v('Dejar la misma clave (su código) NO vale', r.status_code == 200 and 'distinta' in r.get_data(as_text=True))
r = c.post('/perfil/clave', data={'clave_actual': '3000123456', 'clave_nueva': 'corta', 'clave_confirmacion': 'corta'})
v('Una clave de menos de 8 caracteres NO vale', r.status_code == 200 and '8 caracteres' in r.get_data(as_text=True))
estado['cambio_ok'] = False
r = c.post('/perfil/clave', data={'clave_actual': 'equivocada', 'clave_nueva': 'Nueva-Clave-54', 'clave_confirmacion': 'Nueva-Clave-54'})
v('Con la clave actual equivocada la API la rechaza y sigue obligado', r.status_code == 200 and 'no es correcta' in r.get_data(as_text=True)
  and c.get('/').status_code == 302)
estado['cambio_ok'] = True
llamadas.clear()
r = c.post('/perfil/clave', data={'clave_actual': '3000123456', 'clave_nueva': 'Nueva-Clave-54', 'clave_confirmacion': 'Nueva-Clave-54'})
v('Cambia la clave bien: la API recibe actual y nueva y lo lleva a su panel',
  llamadas[0] == ('Usuario/9/cambiar-clave', {'clave_actual': '3000123456', 'clave_nueva': 'Nueva-Clave-54'}) and r.status_code == 302 and '/estudiante' in r.headers['Location'])
with c.session_transaction() as s:
    v('...y la sesión ya NO está marcada: queda libre', s.get('debe_cambiar') is False)
v('...ahora sí abre la portada y su panel', c.get('/').status_code == 200 and c.get('/estudiante/').status_code == 200)
t = c.get('/perfil/clave').get_data(as_text=True)
v('...y cambiar la clave más adelante ya no trae el aviso de primer ingreso', 'Es tu primer ingreso' not in t)

# cerrar sesión siempre se puede
c2 = cliente(None)
c2.post('/login', data={'usuario': 'ana.perez@usbmed.edu.co', 'clave': '3000123456'})
r = c2.post('/logout')
with c2.session_transaction() as s:
    v('Con el cambio pendiente, «Cerrar sesión» funciona y limpia todo', r.status_code == 302 and 'usuario' not in s and not s.get('debe_cambiar'))

# ingreso normal (ya cambió la clave antes)
estado['debe'] = False
c3 = cliente(None)
r = c3.post('/login', data={'usuario': 'ana.perez@usbmed.edu.co', 'clave': '3000123456'})
v('Ingreso normal (sin cambio pendiente): va a su panel, no a cambiar la clave', r.status_code == 302 and '/estudiante' in r.headers['Location'])
with c3.session_transaction() as s:
    v('...y la sesión no queda marcada', s.get('debe_cambiar') is False)
estado['debe'] = True
r = cliente(None).post('/login', data={'usuario': 'ana.perez@usbmed.edu.co', 'clave': 'incorrecta'})
v('Con una clave incorrecta: el mismo rechazo de siempre (401)', r.status_code == 401)

# ---------------------------------------------------------------- 4. el ponente que crea el administrador
llamadas.clear()
r = cliente(['ADMIN']).post('/admin/ponentes/nuevo', data={'nombres': 'Pepe', 'apellidos': 'Ponente', 'correo_personal': 'p@ejemplo.com',
                                                           'es_externo': 'on', 'usuario': 'pepe.ponente', 'clave': ''})
cuenta = [d for (rec, d) in llamadas if rec == 'Usuario']
v('Nuevo ponente (administrador): la cuenta se crea con «debe cambiar la clave» y la pantalla lo avisa',
  cuenta and cuenta[0]['debe_cambiar_clave'] is True and 'se le pedirá cambiarla' in r.get_data(as_text=True))

# ---------------------------------------------------------------- 5. el administrador exige el cambio
llamadas.clear()
AD.listar = lambda recurso, **f: [{'id_usuario': 5, 'usuario': 'zeta.uno', 'fk_asistente': 3, 'activo': True, 'debe_cambiar_clave': True,
                                   'ultimo_acceso': None, 'creado_en': '2026-10-01T10:00:00Z'}] if recurso == 'Usuario' else []
t = cliente(['ADMIN']).get('/admin/usuarios').get_data(as_text=True)
v('Admin: la lista de usuarios marca «Cambio pendiente» y ofrece «Exigir cambio de clave»', 'Cambio pendiente' in t and 'exigir-cambio-clave' in t)
r = cliente(['ADMIN']).post('/admin/usuarios/5/exigir-cambio-clave')
v('Admin: exigir el cambio llama a la API para ese usuario', r.status_code == 302 and ('Usuario/5/exigir-cambio-clave', {}) in llamadas)
llamadas.clear()
v('Un estudiante o un ponente NO puede exigir cambios (403) y no toca la API',
  cliente(['ESTUDIANTE']).post('/admin/usuarios/5/exigir-cambio-clave').status_code == 403
  and cliente(['PONENTE']).post('/admin/usuarios/5/exigir-cambio-clave').status_code == 403 and not llamadas)

print(f'\n{ok} pasadas, {fall} fallidas')
sys.exit(1 if fall else 0)
