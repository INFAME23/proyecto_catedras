# Prueba v7.2 (D73 · RF-S15 · P21), sin BD ni API: el ASISTENTE ve, genera y regenera el QR de una sesión,
# pero NO ve la asistencia (datos de personas); el ADMIN conserva todo y el resto de /admin sigue siendo solo del ADMIN.
# La API se sustituye por datos falsos (con un almacén de QR en memoria para ver qué token sirve).
# Uso, desde front_catedras:   python pruebas/prueba_asistente_qr_v72.py
import datetime as dt
import os
import sys

os.chdir(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
sys.path.insert(0, '.')
import werkzeug
werkzeug.__version__ = '3'

import app as A
import routes.admin as AD
import routes.sesiones as SE
from servicios.api_catedras import ErrorApi

ahora = dt.datetime.now(dt.timezone.utc)
iso = lambda d: d.strftime('%Y-%m-%dT%H:%M:%SZ')

CAT = {'id_catedra': 2, 'id_evento_asis': '000000002', 'nombre': 'Catedra Dos', 'nombre_asis': 'Catedra Dos', 'activo': True,
       'fk_tipo_evento': 'CAAB', 'fk_dependencia': None, 'creado_en': '2026-01-01T00:00:00Z'}
SESIONES = {n: {'id_sesion': n, 'fk_catedra': 2, 'numero_reunion': n, 'estado': 'PROGRAMADA', 'titulo': None, 'lugar': 'Aula 1',
                'fk_modalidad': 'PRESENCIAL', 'fk_sede': 'SAN_BENITO', 'fk_periodo': '2026-2', 'fk_encuesta': None,
                'inicio': iso(ahora - dt.timedelta(hours=1)), 'fin': iso(ahora + dt.timedelta(hours=1))} for n in (1, 2)}
TOK_VIEJO = 'aaaaaaaa-0000-0000-0000-000000000001'
ASISTENCIA = [{'fk_asistente': 21, 'id_asis': '300', 'nombres': 'Ana', 'apellidos': 'Uno', 'registrado_en': iso(ahora),
               'latitud': 6.2442, 'longitud': -75.5812, 'origen': 'QR'}]

# Estado de la API falsa: un QR por sesión (el vigente), los tokens ya anulados y las llamadas hechas.
qr = {}
anulados = []
llamadas = []
sig = [1]
consultas_asistencia = []


def poner_qr(id_sesion, token):
    qr[id_sesion] = {'id_enlace': 1, 'fk_sesion': id_sesion, 'token': token, 'canal': 'QR', 'ya_existia': False, 'revocado_en': None,
                     'ventana_desde': iso(ahora - dt.timedelta(minutes=5)), 'ventana_hasta': iso(ahora + dt.timedelta(minutes=55))}


def crear(recurso, datos):
    llamadas.append((recurso, datos))
    if recurso != 'QrSesion':
        raise AssertionError(recurso)
    s = datos['fk_sesion']
    if s in qr and not datos.get('regenerar'):
        return {**qr[s], 'ya_existia': True}
    if s in qr:
        anulados.append(qr[s]['token'])
    sig[0] += 1
    poner_qr(s, f'aaaaaaaa-0000-0000-0000-{sig[0]:012d}')
    return qr[s]


def consultar(ruta, **p):
    if ruta == 'QrSesion':
        return qr.get(p['fkSesion'])
    if ruta == 'QrSesion/asistencia':
        consultas_asistencia.append(p['fkSesion'])
        return {'total': len(ASISTENCIA), 'datos': [dict(x) for x in ASISTENCIA]}
    raise AssertionError(ruta)


def obtener(recurso, i):
    if recurso == 'Sesion' and int(i) in SESIONES:
        return SESIONES[int(i)]
    if recurso == 'Catedra' and int(i) == 2:
        return CAT
    raise ErrorApi('No existe.', codigo=404)


def listar(recurso, limite=None, **f):
    if recurso == 'Sesion':
        return [s for s in SESIONES.values() if s['fk_catedra'] == f.get('fkCatedra')]
    return [CAT] if recurso == 'Catedra' else []


AD.crear, AD.consultar, AD.obtener, AD.listar = crear, consultar, obtener, listar
AD._opciones = lambda recurso: []
AD._catalogos_sesion = lambda: {'modalidades': [], 'sedes': [], 'periodos': [], 'encuestas': [], 'estados': []}
import servicios.qr_sesiones as Q
Q.crear, Q.consultar = crear, consultar
AD.emitir_qr = Q.emitir_qr
AD.qr_de_sesion = Q.qr_de_sesion
SE.obtener, SE.listar = obtener, listar
SE.qr_de_sesion = Q.qr_de_sesion
SE.ponentes_de = lambda id_sesion: []

app = A.app
app.config['TESTING'] = True
ok = fall = 0


def v(caso, cond):
    global ok, fall
    print(('  OK    ' if cond else '  FALLA ') + caso)
    ok += bool(cond)
    fall += (not cond)


def cliente(roles=None, id_usuario=9):
    c = app.test_client()
    if roles is not None:
        with c.session_transaction() as s:
            s['usuario'] = {'id_usuario': id_usuario, 'usuario': 'asis', 'fk_asistente': 40}
            s['roles'] = roles
    return c


ASISTENTE, ADMIN = ['ASISTENTE'], ['ADMIN']
URL = '/admin/sesiones/1/qr'
poner_qr(1, TOK_VIEJO)

# ---------------------------------------------------------------- P21.1 abrir el QR desde el detalle de la cátedra
t = cliente(ASISTENTE).get('/admin/catedras/2').get_data(as_text=True)
v('P21.1 El detalle de la cátedra le ofrece el botón de QR de cada sesión', URL in t)
r = cliente(ASISTENTE).get(URL)
t = r.get_data(as_text=True)
esperado = f'http://localhost/registro/{TOK_VIEJO}'
v('P21.1 El ASISTENTE abre la pantalla del QR (200)', r.status_code == 200)
v('P21.1 ...ve el QR dibujado, el enlace con «Copiar» y «Proyectar en pantalla completa»',
  '<svg viewBox' in t and esperado in t and Q.svg_qr(esperado) in t and 'id="copiar-url"' in t and 'id="ampliar"' in t)
v('P21.1 ...ve el estado de la ventana y las horas en que sirve', 'Abierto: se cierra a las' in t and 'El QR sirve de las' in t)
v('P21.1 ...ve el título de la cátedra y la sesión', 'Catedra Dos' in t and 'reunión 1' in t)
v('P21.1 ...y el botón «Regenerar QR» con su confirmación', 'Regenerar QR' in t and 'dejará de servir' in t)

# ---------------------------------------------------------------- P21.2 sin la tarjeta de asistencia
v('P21.2 ...NO ve la tarjeta de asistencia ni los datos de las personas',
  'Asistencia de esta sesión' not in t and 'Ana' not in t and 'maps.google.com' not in t and 'filas-asistencia' not in t)
v('P21.2 ...el JavaScript de refresco no corre ni conoce la ruta de asistencia (no hay llamadas que acaben en 403)',
  '/asistencia' not in t and 'setInterval' not in t and 'fetch(' not in t)
v('P21.2 ...y el servidor ni siquiera pidió la asistencia a la API', consultas_asistencia == [])
v('P21.2 ...la miga de pan no le ofrece enlaces que le dan 403 (panel, detalle de la sesión)',
  'href="/admin/"' not in t and 'href="/admin/sesiones/1"' not in t and 'href="/admin/catedras/2"' in t)
r = cliente(ASISTENTE).get('/admin/sesiones/1/asistencia')
v('P21.2 GET /admin/sesiones/1/asistencia le da 403', r.status_code == 403)

# ---------------------------------------------------------------- P21.3 generar el QR de una sesión que no lo tenía
v('P21.3 La sesión 2 todavía no tiene QR', 2 not in qr)
t = cliente(ASISTENTE).get('/admin/sesiones/2/qr').get_data(as_text=True)
v('P21.3 ...y el asistente ve «Generar el QR»', 'Generar el QR' in t and '<svg viewBox' not in t)
llamadas.clear()
r = cliente(ASISTENTE, 9).post('/admin/sesiones/2/qr')
v('P21.3 El ASISTENTE genera el QR (302 de vuelta a la pantalla) y la API recibe regenerar=false',
  r.status_code == 302 and r.headers['Location'].endswith('/admin/sesiones/2/qr') and 2 in qr and llamadas[0][1]['regenerar'] is False)

# ---------------------------------------------------------------- P21.5 rastro: quién lo creó
v('P21.5 La API recibe el usuario del ASISTENTE como quien lo crea (queda en fk_usuario_crea)', llamadas[0][1]['fk_usuario'] == 9)
llamadas.clear()
cliente(ADMIN, 3).post('/admin/sesiones/1/qr', data={'regenerar': '1'})
v('P21.5 ...y con un ADMIN, el usuario del ADMIN (cada quien deja su rastro)', llamadas[0][1]['fk_usuario'] == 3)
poner_qr(1, TOK_VIEJO)
anulados.clear()

# ---------------------------------------------------------------- P21.4 regenerar
llamadas.clear()
r = cliente(ASISTENTE, 9).post(URL, data={'regenerar': '1'})
nuevo = qr[1]['token']
v('P21.4 El ASISTENTE regenera el QR (302) y la API recibe regenerar=true con su usuario',
  r.status_code == 302 and llamadas[0][1]['regenerar'] is True and llamadas[0][1]['fk_usuario'] == 9)
v('P21.4 ...el anterior deja de servir (quedó anulado) y hay un token nuevo', TOK_VIEJO in anulados and nuevo != TOK_VIEJO)
t = cliente(ASISTENTE).get(URL).get_data(as_text=True)
v('P21.4 ...la pantalla ya dibuja el nuevo y no el anterior', f'/registro/{nuevo}' in t and TOK_VIEJO not in t)

# ---------------------------------------------------------------- P21.6 el resto de /admin sigue cerrado
for ruta in ('/admin/', '/admin/usuarios', '/admin/sedes', '/admin/parametros', '/admin/sesiones', '/admin/sesiones/1',
             '/admin/exportar', '/admin/asistentes'):
    v(f'P21.6 ASISTENTE en {ruta} -> 403', cliente(ASISTENTE).get(ruta).status_code == 403)
v('P21.6 La lista del asistente tiene el QR pero no la asistencia en vivo',
  'admin.sesiones_qr' in AD.ENDPOINTS_DEL_ASISTENTE and 'admin.sesiones_asistencia_json' not in AD.ENDPOINTS_DEL_ASISTENTE)
v('P21.6 Otros roles con sesión (ESTUDIANTE, PONENTE) siguen en 403 también en el QR',
  all(cliente([rol]).get(URL).status_code == 403 for rol in ('ESTUDIANTE', 'PONENTE')))

# ---------------------------------------------------------------- P21.7 el ADMIN conserva todo
consultas_asistencia.clear()
poner_qr(1, TOK_VIEJO)
r = cliente(ADMIN).get(URL)
t = r.get_data(as_text=True)
v('P21.7 El ADMIN abre el QR (200) con QR, copiar, proyectar y regenerar',
  r.status_code == 200 and '<svg viewBox' in t and 'id="copiar-url"' in t and 'id="ampliar"' in t and 'Regenerar QR' in t)
v('P21.7 ...y ve la asistencia (Ana, el mapa, el contador) con su refresco en vivo',
  'Asistencia de esta sesión' in t and 'Ana' in t and 'maps.google.com/?q=6.2442,-75.5812' in t and 'id="n-asistieron">1<' in t
  and '/admin/sesiones/1/asistencia' in t and 'setInterval' in t and consultas_asistencia == [1])
v('P21.7 ...con la miga de pan completa (panel y detalle de la sesión)', 'href="/admin/"' in t and 'href="/admin/sesiones/1"' in t)
r = cliente(ADMIN).get('/admin/sesiones/1/asistencia')
j = r.get_json()
v('P21.7 /asistencia sigue respondiendo al ADMIN (filas, total y estado)', r.status_code == 200 and j['total'] == 1 and len(j['filas']) == 1 and j['estado'] == 'abierto')
llamadas.clear()
r = cliente(ADMIN, 3).post('/admin/sesiones/1/qr')
v('P21.7 El ADMIN genera y regenera como siempre (302)', r.status_code == 302)

# ---------------------------------------------------------------- P21.8 y P21.9
for metodo in ('get', 'post'):
    r = getattr(cliente(), metodo)(URL)
    v(f'P21.8 Sin sesión, {metodo.upper()} -> redirige al login', r.status_code == 302 and '/login' in r.headers['Location'])
v('P21.9 Sesión inexistente (ASISTENTE) -> 404', cliente(ASISTENTE).get('/admin/sesiones/999/qr').status_code == 404)
v('P21.9 ...y para el ADMIN también', cliente(ADMIN).get('/admin/sesiones/999/qr').status_code == 404)

# ---------------------------------------------------------------- P21.10 la página pública no publica el token
poner_qr(1, TOK_VIEJO)
r = cliente().get('/sesiones/1')
t = r.get_data(as_text=True)
v('P21.10 La página pública de la sesión no muestra el token del QR', r.status_code == 200 and TOK_VIEJO not in t and '/registro/' not in t)

print(f'\n{ok} bien, {fall} mal')
sys.exit(1 if fall else 0)
