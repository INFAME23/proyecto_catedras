# Prueba v7.1 (D55), sin BD ni API: SOLO el administrador inicia sesión; no hay paneles de estudiante ni de ponente;
# no hay inscripción ni cupo; el QR de cada sesión, su asistencia y la disponibilidad de horario viven en /admin;
# la asistencia por QR es pública (clave por correo, ubicación) y la encuesta se responde justo después.
# La API se sustituye por datos falsos y el correo por un registro en memoria.
# Uso, desde front_catedras:   python pruebas/prueba_d55_v71.py
import datetime as dt
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
import routes.catedras as CA
import routes.encuestas as EN
import routes.perfil as PF
import routes.registro as RG
import routes.sesiones as SE
import servicios.qr_sesiones as Q
from servicios import correo as CO
from servicios.api_catedras import ErrorApi

ahora = dt.datetime.now(dt.timezone.utc)
iso = lambda d: d.strftime('%Y-%m-%dT%H:%M:%SZ')
TOK = '11111111-2222-3333-4444-555555555555'

db = {
    'catedras': {2: {'id_catedra': 2, 'id_evento_asis': '000000002', 'nombre': 'Catedra Dos', 'nombre_asis': 'Catedra Dos', 'activo': True,
                     'fk_tipo_evento': 'CAAB', 'fk_dependencia': None, 'creado_en': '2026-01-01T00:00:00Z'}},
    'sesiones': {1: {'id_sesion': 1, 'fk_catedra': 2, 'numero_reunion': 1, 'estado': 'PROGRAMADA', 'titulo': None, 'lugar': 'Aula 1',
                     'fk_modalidad': 'PRESENCIAL', 'fk_sede': 'SAN_BENITO', 'fk_periodo': '2026-2', 'fk_encuesta': 5,
                     'inicio': iso(ahora - dt.timedelta(hours=1)), 'fin': iso(ahora + dt.timedelta(hours=1))}},
    'qr': {}, 'llamadas': [], 'sig': 100, 'falla_disp': False, 'choque': None, 'agenda': [],
    'asistencia': [
        {'fk_asistente': 21, 'id_asis': '300', 'nombres': 'Ana', 'apellidos': 'Uno', 'registrado_en': iso(ahora - dt.timedelta(minutes=5)),
         'latitud': 6.2442, 'longitud': -75.5812, 'origen': 'QR'},
        {'fk_asistente': 23, 'id_asis': None, 'nombres': 'Cami', 'apellidos': 'Tres', 'registrado_en': iso(ahora - dt.timedelta(minutes=2)),
         'latitud': 6.2, 'longitud': -75.5, 'origen': 'ENLACE'},
    ],
    'roles_login': ['ADMIN'], 'login_ok': True, 'debe': False, 'cambio_ok': True,
}


def poner_qr(id_sesion, desde_min, hasta_min, token=TOK, canal='QR'):
    db['qr'][id_sesion] = {'id_enlace': 1, 'fk_sesion': id_sesion, 'token': token, 'canal': canal,
                           'ventana_desde': iso(ahora + dt.timedelta(minutes=desde_min)),
                           'ventana_hasta': iso(ahora + dt.timedelta(minutes=hasta_min)), 'revocado_en': None, 'ya_existia': False}


def listar(recurso, limite=None, **f):
    if recurso == 'Sesion':
        return [s for s in db['sesiones'].values() if s['fk_catedra'] == f.get('fkCatedra')]
    if recurso == 'Catedra':
        return list(db['catedras'].values())
    if recurso == 'Usuario':
        return [{'id_usuario': 5, 'usuario': 'zeta.uno', 'fk_asistente': 3, 'activo': True, 'debe_cambiar_clave': True,
                 'ultimo_acceso': None, 'creado_en': '2026-10-01T10:00:00Z'}]
    return []


def obtener(recurso, i):
    if recurso == 'Sesion' and int(i) in db['sesiones']:
        return db['sesiones'][int(i)]
    if recurso == 'Catedra' and int(i) in db['catedras']:
        return db['catedras'][int(i)]
    if recurso == 'RegistroAsistencia':
        return {'id_registro': int(i), 'fk_sesion': 1, 'registrado_en': iso(ahora)}
    raise ErrorApi('No existe.', codigo=404)


def crear(recurso, d):
    db['llamadas'].append((recurso, d))
    if recurso == 'Usuario/login':
        if not db['login_ok']:
            raise ErrorApi('Usuario o clave incorrectos', codigo=401)
        return {'estado': 'autenticado', 'roles': db['roles_login'],
                'usuario': {'id_usuario': 9, 'usuario': 'admin', 'fk_asistente': 40, 'debe_cambiar_clave': db['debe']}}
    if recurso.endswith('/cambiar-clave'):
        if not db['cambio_ok']:
            raise ErrorApi('La clave actual no es correcta', codigo=401)
        return {'estado': 'cambiada'}
    if recurso.endswith('/exigir-cambio-clave'):
        return {'estado': 'exigido'}
    if recurso == 'QrSesion':
        s = d['fk_sesion']
        if s in db['qr'] and not d.get('regenerar'):
            return {**db['qr'][s], 'ya_existia': True}
        db['sig'] += 1
        poner_qr(s, 45, 60, token=f'aaaaaaaa-0000-0000-0000-{db["sig"]:012d}')
        return db['qr'][s]
    if recurso == 'Catedra':
        id_c = 50 + len([c for c in db['catedras'] if c >= 50])
        db['catedras'][id_c] = {'id_catedra': id_c, 'id_evento_asis': d['id_evento_asis'], 'nombre': d['nombre'], 'nombre_asis': d['nombre'][:30],
                                'activo': True, 'fk_tipo_evento': 'CAAB', 'fk_dependencia': None, 'creado_en': '2026-01-01T00:00:00Z'}
        for n, s in enumerate(d.get('sesiones', []), start=1):
            db['sig'] += 1
            db['sesiones'][db['sig']] = {'id_sesion': db['sig'], 'fk_catedra': id_c, 'numero_reunion': n, 'estado': 'PROGRAMADA', 'titulo': None,
                                         'inicio': s['inicio'], 'fin': s['fin'], 'fk_modalidad': s['fk_modalidad'], 'fk_sede': s['fk_sede'],
                                         'fk_periodo': s['fk_periodo'], 'lugar': s.get('lugar')}
        return {'id_catedra': id_c, 'nombre_asis': d['nombre'][:30]}
    if recurso == 'ClaveAcceso/solicitar':
        return {'id_clave': 77, 'clave': '482915', 'enviado_a': 'ana@ejemplo.com'}
    if recurso == 'ClaveAcceso/validar':
        return {'id_registro': 900, 'estado': 'registrado'}
    raise AssertionError(recurso)


def consultar(ruta, **p):
    if ruta == 'QrSesion':
        return db['qr'].get(p['fkSesion'])
    if ruta == 'QrSesion/asistencia':
        return {'total': len(db['asistencia']), 'datos': [dict(x) for x in db['asistencia']]}
    if ruta == 'Disponibilidad/conflictos':
        if db['falla_disp']:
            raise ErrorApi('No se pudo contactar la API.', detalle='caída')
        c = db['choque']
        if c and p['inicio'] < c['fin_iso'] and p['fin'] > c['inicio_iso'] and p.get('fkSede') == c['fk_sede']:
            return {'disponible': False, 'total': 1, 'conflictos': [c]}
        return {'disponible': True, 'total': 0, 'conflictos': []}
    if ruta == 'Disponibilidad/agenda':
        return {'total': len(db['agenda']), 'datos': db['agenda']}
    raise AssertionError(ruta)


AU.crear = crear
PF.crear = crear
for m in (AD, SE, CA, EN, RG):
    m.listar = listar
    m.obtener = obtener
AD.crear = crear
AD.consultar = consultar
RG.crear = crear
EN.crear = crear
Q.listar, Q.crear, Q.consultar = listar, crear, consultar
AD._opciones = lambda recurso: ([{'id_sede': 'SAN_BENITO', 'nombre': 'Campus San Benito', 'es_virtual': False},
                                 {'id_sede': 'VIRTUAL', 'nombre': 'Virtual', 'es_virtual': True}] if recurso == 'Sede' else [])
AD._catalogos_sesion = lambda: {'modalidades': [], 'sedes': AD._opciones('Sede'), 'periodos': [], 'encuestas': [], 'estados': []}
enviados = []
CO.enviar_clave_asistencia = lambda destino, codigo, minutos: enviados.append((destino, codigo, minutos))

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
            s['usuario'] = {'id_usuario': 1, 'usuario': 'admin', 'fk_asistente': 40}
            s['roles'] = roles
    return c


ADMIN = ['ADMIN']
tarjetas = lambda t: len(re.findall(r'<a class="card text-center', t))

# ------------------------------------------------------------------ 1. solo el administrador inicia sesión
c = cliente()
r = c.post('/login', data={'usuario': 'admin', 'clave': 'Admin12345'})
v('El administrador inicia sesión y va a su panel', r.status_code == 302 and r.headers['Location'].endswith('/admin/'))
db['roles_login'] = ['ESTUDIANTE']
r = cliente().post('/login', data={'usuario': 'ana', 'clave': 'x'})
v('Si la API devolviera una cuenta que NO es administrador, el front tampoco la deja entrar (401, mensaje genérico)',
  r.status_code == 401 and 'incorrectos' in r.get_data(as_text=True).lower())
db['roles_login'] = ['PONENTE', 'ESTUDIANTE']
v('...ni un ponente', cliente().post('/login', data={'usuario': 'p', 'clave': 'x'}).status_code == 401)
db['roles_login'] = ['ADMIN']
db['login_ok'] = False
v('Con credenciales malas: 401', cliente().post('/login', data={'usuario': 'admin', 'clave': 'mala'}).status_code == 401)
db['login_ok'] = True
t = cliente().get('/login').get_data(as_text=True)
v('La pantalla de login dice que es solo para el administrador y que estudiantes y ponentes no necesitan cuenta',
  'Acceso solo para el administrador' in t and 'No necesitas cuenta' in t and 'correo institucional' not in t and 'código de estudiante' not in t)
for ruta in ('/estudiante/', '/ponente/', '/ponente/catedras/', '/ponente/catedras/nueva', '/estudiante/catedras/2/inscribirse'):
    v(f'{ruta} ya no existe (404)', cliente(ADMIN).get(ruta).status_code == 404)
v('Un usuario con sesión pero sin ADMIN no entra a /admin (403)', cliente(['ESTUDIANTE']).get('/admin/').status_code == 403)
v('Sin sesión, /admin manda a /login', cliente().get('/admin/').status_code == 302)

# ------------------------------------------------------------------ 2. barra, portada, páginas públicas
for etiqueta, roles in (('visitante', None), ('administrador', ADMIN)):
    t = cliente(roles).get('/').get_data(as_text=True)
    barra = t.split('<main')[0]
    v(f'{etiqueta}: la barra no trae «Registro de asistencia», «Ponente» ni «Mi panel»', 'Registro de asistencia' not in barra and 'Mi panel' not in barra
      and 'fa-chalkboard-teacher' not in barra and 'fa-user-graduate' not in barra)
t = cliente().get('/').get_data(as_text=True)
v('Portada: información genérica (catálogo, asistencia con QR, administración, cómo participar)', 'Consulta el catálogo' in t and 'Asistencia con QR' in t
  and 'Solo el administrador inicia sesión' in t and 'Cómo participar' in t)
v('...sin hablar de inscripción, cuentas de estudiante ni paneles', 'nscri' not in t and 'código de estudiante' not in t and 'Mi panel' not in t and '/admin' not in t)
t = cliente().get('/catedras/2').get_data(as_text=True)
v('La ficha de una cátedra ya no tiene fila de inscripción ni botones', 'Inscri' not in t and 'inscribirse' not in t and 'id="inscripcion"' not in t)
poner_qr(1, 45, 60)
t = cliente().get('/sesiones/1').get_data(as_text=True)
v('La sesión pública no muestra cupo', 'Cupo' not in t and 'cupo' not in t)
v('...explica cómo registrar la asistencia (QR del ponente, sin cuenta) con el horario en que sirve', 'id="como-asistir"' in t and 'No necesitas cuenta' in t and 'sirve de ' in t)
v('...y NUNCA muestra el token del QR (visitante ni administrador)', TOK not in t and TOK not in cliente(ADMIN).get('/sesiones/1').get_data(as_text=True))
db['qr'].clear()
t = cliente().get('/sesiones/1').get_data(as_text=True)
v('...sin QR todavía dice que sirve en los últimos minutos', 'sirve solo en los últimos minutos' in t)

# ------------------------------------------------------------------ 3. administración: sin cupo, sin inscripción, sin cuentas de ponente
t = cliente(ADMIN).get('/admin/').get_data(as_text=True)
v('Panel: sin tarjetas de ponente, «Nuevo ponente» ni inscripciones', 'Calendario del ponente' not in t and 'Nuevo ponente' not in t and 'Inscripción a cátedra' not in t)
v('...y los contadores de cada categoría coinciden con sus tarjetas',
  all(int(n) == tarjetas(b) for n, b in re.findall(r'badge badge-secondary">(\d+)</span></summary>(.*?)</details>', t, re.S)))
for ruta in ('/admin/ponentes/nuevo', '/admin/inscripciones'):
    v(f'{ruta} ya no existe (404)', cliente(ADMIN).get(ruta).status_code == 404)
t = cliente(ADMIN).get('/admin/usuarios').get_data(as_text=True)
v('Usuarios: sin el botón «Nuevo ponente»; conserva «Cambio pendiente» y «Exigir cambio de clave»', 'Nuevo ponente' not in t and 'Cambio pendiente' in t and 'exigir-cambio-clave' in t)
t = cliente(ADMIN).get('/admin/catedras/nueva').get_data(as_text=True)
v('Formulario de cátedra: sin campo de cupo, con la ocupación por fecha y el aviso por sesión', 'cupo' not in t.lower() and 'Ver ocupación de una fecha' in t and 'data-disponibilidad' in t
  and 'disponibilidad.js' in t and 'data-url-agenda' in t)
v('El formulario de sesión (admin) no tiene cupo', 'cupo' not in cliente(ADMIN).get('/admin/sesiones/nueva?fk_catedra=2').get_data(as_text=True).lower())

# ------------------------------------------------------------------ 4. crear cátedra: disponibilidad y QR de cada sesión
def hora(h):
    return (ahora + dt.timedelta(hours=h)).strftime('%Y-%m-%dT%H:00')


def iso_h(h):
    return (ahora + dt.timedelta(hours=h)).replace(minute=0, second=0, microsecond=0).strftime('%Y-%m-%dT%H:%M:00-05:00')


def filas(*horas, lugar='Aula 1', sede='SAN_BENITO'):
    d = {'ses_titulo': [], 'ses_inicio': [], 'ses_fin': [], 'ses_fk_modalidad': [], 'ses_fk_sede': [], 'ses_fk_periodo': [], 'ses_lugar': [], 'ses_fk_encuesta': []}
    for h in horas:
        d['ses_titulo'].append(''); d['ses_inicio'].append(hora(h)); d['ses_fin'].append(hora(h + 1)); d['ses_fk_modalidad'].append('PRESENCIAL')
        d['ses_fk_sede'].append(sede); d['ses_fk_periodo'].append('2026-2'); d['ses_lugar'].append(lugar); d['ses_fk_encuesta'].append('')
    return d


base = {'id_evento_asis': '000099001', 'nombre': 'Catedra con QR', 'fk_tipo_evento': 'CAAB', 'fk_dependencia': '', 'activo': 'on'}
n_cat = len(db['catedras'])
db['llamadas'].clear()
r = cliente(ADMIN).post('/admin/catedras/nueva', data={**base, **filas(10, 30)})
qrs = [d for (rec, d) in db['llamadas'] if rec == 'QrSesion']
v('Admin crea una cátedra con 2 sesiones libres: se crea y se genera el QR de CADA sesión', r.status_code == 302 and len(db['catedras']) == n_cat + 1 and len(qrs) == 2)
v('...cada QR lleva su sesión y el usuario que lo emitió (sin regenerar)', all(d['regenerar'] is False and d['fk_usuario'] == 1 for d in qrs) and len({d['fk_sesion'] for d in qrs}) == 2)
sesiones_c = [d for (rec, d) in db['llamadas'] if rec == 'Catedra'][0]['sesiones']
v('...y a la API no le llega ningún «cupo»', all('cupo' not in s for s in sesiones_c))

db['choque'] = {'id_sesion': 900, 'fk_catedra': 50, 'nombre_catedra': 'Otra catedra', 'numero_reunion': 3, 'inicio': iso(ahora + dt.timedelta(hours=1)),
                'fin': iso(ahora + dt.timedelta(hours=3)), 'inicio_iso': iso_h(1), 'fin_iso': iso_h(3), 'fk_sede': 'SAN_BENITO', 'lugar': 'Aula 1', 'motivos': ['LUGAR']}
n_cat = len(db['catedras'])
db['llamadas'].clear()
r = cliente(ADMIN).post('/admin/catedras/nueva', data={**base, 'id_evento_asis': '000099002', **filas(1)})
t = r.get_data(as_text=True)
v('Una sesión que se cruza (mismo salón, mismo horario): NO se crea nada y se vuelve al formulario', r.status_code == 200 and len(db['catedras']) == n_cat
  and not any(rec == 'Catedra' for (rec, _) in db['llamadas']))
v('...los choques quedan FIJOS en la página (alert sin «dismissible») y lo escrito se conserva', 'id="choques"' in t and 'Otra catedra' in t and 'el mismo salón' in t
  and 'alert-dismissible' not in t.split('id="choques"')[1][:300] and 'Catedra con QR' in t)
r = cliente(ADMIN).post('/admin/catedras/nueva', data={**base, 'id_evento_asis': '000099003', **filas(1, 1, lugar='Aula Z')})
v('Dos sesiones del MISMO formulario que se traslapan en el mismo salón: se bloquean y se dice cuáles', r.status_code == 200 and 'Las sesiones 1 y 2' in r.get_data(as_text=True))
r = cliente(ADMIN).post('/admin/catedras/nueva', data={**base, 'id_evento_asis': '000099004', **filas(5, 6, lugar='Aula Z')})
v('Una que empieza justo cuando termina la otra NO se cruza (se crea)', r.status_code == 302)
db['choque'] = None
db['falla_disp'] = True
n_cat = len(db['catedras'])
r = cliente(ADMIN).post('/admin/catedras/nueva', data={**base, 'id_evento_asis': '000099005', **filas(60)})
v('Si la disponibilidad no se puede consultar, no se crea nada (falla cerrado) y se avisa',
  r.status_code == 200 and len(db['catedras']) == n_cat and 'No se pudo verificar la disponibilidad' in r.get_data(as_text=True))
db['falla_disp'] = False

# JSON de disponibilidad
db['choque'] = {'id_sesion': 900, 'fk_catedra': 50, 'nombre_catedra': 'Otra catedra', 'numero_reunion': 3, 'inicio': iso(ahora + dt.timedelta(hours=1)),
                'fin': iso(ahora + dt.timedelta(hours=3)), 'inicio_iso': iso_h(1), 'fin_iso': iso_h(3), 'fk_sede': 'SAN_BENITO', 'lugar': 'Aula 1', 'motivos': ['LUGAR']}
r = cliente(ADMIN).get('/admin/catedras/disponibilidad', query_string={'inicio': hora(2), 'fin': hora(4), 'sede': 'SAN_BENITO', 'lugar': 'Aula 1'})
j = r.get_json()
v('/admin/catedras/disponibilidad: horario ocupado -> disponible=false con la sesión y su texto', r.status_code == 200 and j['disponible'] is False and 'Otra catedra' in j['conflictos'][0]['texto'])
v('...horario libre -> disponible=true', cliente(ADMIN).get('/admin/catedras/disponibilidad', query_string={'inicio': hora(8), 'fin': hora(9), 'sede': 'SAN_BENITO'}).get_json()['disponible'] is True)
v('...sin inicio ni fin -> 400', cliente(ADMIN).get('/admin/catedras/disponibilidad').status_code == 400)
db['agenda'] = [{**db['choque'], 'nombre_sede': 'Campus San Benito', 'motivos': None}]
r = cliente(ADMIN).get('/admin/catedras/agenda', query_string={'fecha': '2030-03-10', 'sede': 'SAN_BENITO'})
j = r.get_json()
v('/admin/catedras/agenda: devuelve las sesiones del día con hora y sede', r.status_code == 200 and len(j['sesiones']) == 1 and j['sesiones'][0]['hora'] and j['sesiones'][0]['nombre_sede'])
v('...fecha ilegible -> 400', cliente(ADMIN).get('/admin/catedras/agenda?fecha=ayer').status_code == 400)
db['choque'] = None
for ruta in ('/admin/catedras/disponibilidad?inicio=a&fin=b', '/admin/catedras/agenda?fecha=2030-01-01', '/admin/sesiones/1/qr', '/admin/sesiones/1/asistencia'):
    v(f'{ruta.split("?")[0]}: sin sesión -> /login (302), sin ADMIN -> 403', cliente().get(ruta).status_code == 302 and cliente(['ESTUDIANTE']).get(ruta).status_code == 403)

# ------------------------------------------------------------------ 5. el QR de la sesión (administrador)
poner_qr(1, -5, 55)
url_qr = '/admin/sesiones/1/qr'
r = cliente(ADMIN).get(url_qr)
t = r.get_data(as_text=True)
esperado = f'http://localhost/registro/{TOK}'
v('Admin: la pantalla del QR dibuja el SVG de la URL del front con el token', r.status_code == 200 and '<svg viewBox' in t and esperado in t and Q.svg_qr(esperado) in t)
v('...con el estado «Abierto», copiar, proyectar a pantalla completa y regenerar', 'Abierto: se cierra a las' in t and 'id="copiar-url"' in t and 'id="ampliar"' in t and 'Regenerar QR' in t)
v('...y la asistencia: quien registró, con hora, origen y mapa', 'Ana Uno' in t and 'Cami Tres' in t and 'maps.google.com/?q=6.2442,-75.5812' in t
  and 'id="n-asistieron">2<' in t)
v('...no menciona inscripción por ningún lado', 'nscri' not in t)
poner_qr(1, 30, 45)
v('Antes de la ventana: «Aún no abre»', 'Aún no abre: se abre a las' in cliente(ADMIN).get(url_qr).get_data(as_text=True))
poner_qr(1, -45, -30)
v('Después de la ventana: «Cerrado»', 'Cerrado a las' in cliente(ADMIN).get(url_qr).get_data(as_text=True))
poner_qr(1, -5, 55, canal='ENLACE')
t = cliente(ADMIN).get(url_qr).get_data(as_text=True)
v('Sesión virtual (canal ENLACE): también hay QR y se explica que se pegue el enlace en el chat', '<svg viewBox' in t and 'id="aviso-virtual"' in t)
app.config['URL_PUBLICA'] = 'https://catedras.example.edu/'
poner_qr(1, -5, 55)
v('Con URL_PUBLICA, el QR usa esa dirección', f'https://catedras.example.edu/registro/{TOK}' in cliente(ADMIN).get(url_qr).get_data(as_text=True))
app.config['URL_PUBLICA'] = ''
del db['qr'][1]
t = cliente(ADMIN).get(url_qr).get_data(as_text=True)
v('Sin QR ofrece «Generar el QR»', 'Generar el QR' in t and '<svg viewBox' not in t)
db['llamadas'].clear()
r = cliente(ADMIN).post(url_qr)
tok1 = db['qr'][1]['token']
v('POST genera el QR (302) sin regenerar', r.status_code == 302 and db['llamadas'][0][1]['regenerar'] is False)
db['llamadas'].clear()
r = cliente(ADMIN).post(url_qr, data={'regenerar': '1'})
v('POST regenerar=1 crea otro token', r.status_code == 302 and db['qr'][1]['token'] != tok1 and db['llamadas'][0][1]['regenerar'] is True)
j = cliente(ADMIN).get('/admin/sesiones/1/asistencia').get_json()
v('/asistencia devuelve filas, total y estado, con hora local', len(j['filas']) == 2 and j['total'] == 2 and j['estado'] in ('pendiente', 'abierto', 'cerrado') and j['filas'][0]['hora'])
v('Sesión inexistente -> 404', cliente(ADMIN).get('/admin/sesiones/999/qr').status_code == 404)
t = cliente(ADMIN).get('/admin/catedras/2').get_data(as_text=True)
v('El detalle de la cátedra tiene el enlace al QR de cada sesión', '/admin/sesiones/1/qr' in t)
t = cliente(ADMIN).get('/admin/sesiones/1').get_data(as_text=True)
v('El detalle de la sesión tiene el botón «QR de asistencia» y no muestra cupo', 'QR de asistencia' in t and 'Cupo' not in t)
js = open('static/disponibilidad.js', encoding='utf-8').read()
v('El script de disponibilidad escribe con textContent (sin innerHTML)', 'innerHTML' not in js and not os.path.exists('static/ponente_disponibilidad.js'))
js = open('static/main.js', encoding='utf-8-sig').read()
v("main.js solo quita solos los flash de ÉXITO: ni los avisos ni los errores", "querySelectorAll('.alert.alert-success.alert-dismissible')" in js and "querySelectorAll('.alert')" not in js)

# ------------------------------------------------------------------ 6. asistencia pública por QR + encuesta
app.config['SMTP_HOST'] = ''
app.config['CORREO_MODO_CONSOLA'] = False
enviados.clear()
t = cliente().post(f'/registro/{TOK}', data={'identificador': '300'}).get_data(as_text=True)
v('Sin correo configurado (desarrollo): la clave se MUESTRA como simulación y no se manda nada', '482915' in t and 'Simulación del correo' in t and not enviados)
app.config['SMTP_HOST'] = 'smtp.ejemplo.com'
t = cliente().post(f'/registro/{TOK}', data={'identificador': '300'}).get_data(as_text=True)
v('Con correo configurado: la clave se ENVÍA al correo de la persona y NO aparece en pantalla',
  enviados == [('ana@ejemplo.com', '482915', 10)] and '482915' not in t and 'Te enviamos la clave a' in t)
app.config['SMTP_HOST'] = ''
t = cliente().get(f'/registro/{TOK}').get_data(as_text=True)
v('El formulario del QR es público y empieza vacío (no hay cuenta que lo precargue)', 'name="identificador"' in t and 'value="9"' not in t)
c2 = cliente()
t = c2.post(f'/registro/{TOK}/validar', data={'id_clave': '77', 'clave': '482915', 'latitud': '6.24', 'longitud': '-75.58'}).get_data(as_text=True)
v('Validar con clave y ubicación: «¡Asistencia registrada!» y se ofrece la encuesta de la sesión (tiene encuesta)',
  '¡Asistencia registrada!' in t and 'Responder la encuesta' in t and '/sesiones/1/encuesta' in t)
with c2.session_transaction() as s:
    v('...el registro queda en la cookie firmada para poder responder la encuesta justo ahora', s.get('registro_reciente') == {'registro': 900, 'sesion': 1})
r = c2.get('/sesiones/1/encuesta')
v('Con ese registro la pantalla de la encuesta abre (200) sin iniciar sesión', r.status_code == 200)
r = cliente().get('/sesiones/1/encuesta', follow_redirects=True)
v('Sin haber registrado la asistencia, la encuesta NO abre: explica que se responde después de registrar',
  'justo después de registrar tu asistencia' in r.get_data(as_text=True) and 'login' not in r.request.path)
c3 = cliente()
with c3.session_transaction() as s:
    s['registro_reciente'] = {'registro': 901, 'sesion': 2}
v('...un registro de OTRA sesión no sirve para esta encuesta', 'justo después de registrar tu asistencia' in c3.get('/sesiones/1/encuesta', follow_redirects=True).get_data(as_text=True))

# ------------------------------------------------------------------ 7. cambio obligatorio de clave (cuenta con clave temporal)
db['debe'] = True
c4 = cliente()
r = c4.post('/login', data={'usuario': 'admin', 'clave': 'Temporal-1'})
v('Cuenta con cambio pendiente: al entrar va a CAMBIAR LA CLAVE', r.status_code == 302 and r.headers['Location'].endswith('/perfil/clave'))
t = c4.get('/perfil/clave').get_data(as_text=True)
v('...la pantalla dice que la clave actual es la temporal (no habla de códigos de estudiante)', 'Es tu primer ingreso' in t and 'clave temporal' in t and 'estudiante' not in t.lower())
v('...mientras tanto /, /catedras y /admin/ la devuelven a cambiarla',
  all(c4.get(x).status_code == 302 and c4.get(x).headers['Location'].endswith('/perfil/clave') for x in ('/', '/catedras', '/admin/')))
v('...pero los estáticos cargan', c4.get('/static/estilo.css').status_code == 200)
r = c4.post('/perfil/clave', data={'clave_actual': 'Temporal-1', 'clave_nueva': 'Temporal-1', 'clave_confirmacion': 'Temporal-1'})
v('Dejar la misma clave NO vale', r.status_code == 200 and 'distinta' in r.get_data(as_text=True))
r = c4.post('/perfil/clave', data={'clave_actual': 'Temporal-1', 'clave_nueva': 'Definitiva-2026', 'clave_confirmacion': 'Definitiva-2026'})
v('Cambiarla bien lo libera y lo lleva al panel de administración', r.status_code == 302 and '/admin' in r.headers['Location'] and c4.get('/admin/').status_code == 200)
c5 = cliente()
c5.post('/login', data={'usuario': 'admin', 'clave': 'Temporal-1'})
r = c5.post('/logout')
with c5.session_transaction() as s:
    v('Con el cambio pendiente, «Cerrar sesión» funciona', r.status_code == 302 and 'usuario' not in s)
db['debe'] = False
llam = len(db['llamadas'])
r = cliente(ADMIN).post('/admin/usuarios/5/exigir-cambio-clave')
v('El administrador puede exigir el cambio de clave a una cuenta', r.status_code == 302 and ('Usuario/5/exigir-cambio-clave', {}) in db['llamadas'][llam:])

print(f'\n{ok} pasadas, {fall} fallidas')
sys.exit(1 if fall else 0)
