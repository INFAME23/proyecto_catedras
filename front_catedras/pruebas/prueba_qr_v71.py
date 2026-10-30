# Prueba v7.1 (D53), sin BD ni API: el QR de cierre de cada sesión. Al crear una cátedra se genera el QR de
# cada sesión; el ponente de la sesión lo ve, lo proyecta y ve quién asistió; nadie más lo ve.
# La API se sustituye por un almacén en memoria.
# Uso, desde front_catedras:   python pruebas/prueba_qr_v71.py
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
import routes.ponente as P
import routes.ponente_catedras as PC
import servicios.qr_sesiones as Q
from servicios.api_catedras import ErrorApi

ahora = dt.datetime.now(dt.timezone.utc)
iso = lambda d: d.strftime('%Y-%m-%dT%H:%M:%SZ')
TOK = '11111111-2222-3333-4444-555555555555'

db = {
    'catedras': {2: {'id_catedra': 2, 'id_evento_asis': '000000002', 'nombre': 'Catedra Dos', 'activo': True, 'fk_tipo_evento': 'CAAB'}},
    'sesiones': {1: {'id_sesion': 1, 'fk_catedra': 2, 'numero_reunion': 1, 'estado': 'PROGRAMADA', 'titulo': None,
                     'inicio': iso(ahora - dt.timedelta(hours=1)), 'fin': iso(ahora + dt.timedelta(hours=1))}},
    'ponencias': {(1, 5, 'PONENTE'), (1, 6, 'INVITADO'), (1, 8, 'ORGANIZADOR')},
    'qr': {},                       # id_sesion -> dict del QR
    'llamadas': [], 'falla_qr': False, 'sig': 100,
    'asistencia': [
        {'fk_asistente': 21, 'id_asis': '300', 'nombres': 'Ana', 'apellidos': 'Uno', 'inscrito': True, 'asistio': True,
         'registrado_en': iso(ahora - dt.timedelta(minutes=5)), 'latitud': 6.2442, 'longitud': -75.5812, 'origen': 'QR'},
        {'fk_asistente': 22, 'id_asis': '301', 'nombres': 'Beto', 'apellidos': 'Dos', 'inscrito': True, 'asistio': False,
         'registrado_en': None, 'latitud': None, 'longitud': None, 'origen': None},
        {'fk_asistente': 23, 'id_asis': None, 'nombres': 'Cami', 'apellidos': 'Tres', 'inscrito': False, 'asistio': True,
         'registrado_en': iso(ahora - dt.timedelta(minutes=2)), 'latitud': 6.2, 'longitud': -75.5, 'origen': 'QR'},
    ],
}


def poner_qr(id_sesion, desde_min, hasta_min, token=TOK):
    db['qr'][id_sesion] = {'id_enlace': 1, 'fk_sesion': id_sesion, 'token': token,
                           'ventana_desde': iso(ahora + dt.timedelta(minutes=desde_min)),
                           'ventana_hasta': iso(ahora + dt.timedelta(minutes=hasta_min)),
                           'revocado_en': None, 'requiere_inscripcion': True, 'ya_existia': False}


def listar(recurso, limite=None, **f):
    if recurso == 'Sesion':
        return [s for s in db['sesiones'].values() if s['fk_catedra'] == f.get('fkCatedra')]
    if recurso == 'Ponencia':
        return [{'fk_sesion': s, 'fk_asistente': a, 'rol': r} for (s, a, r) in db['ponencias']
                if f.get('fkSesion') in (None, s) and f.get('fkAsistente') in (None, a)]
    if recurso == 'Catedra':
        return list(db['catedras'].values())
    return []


def obtener(recurso, i):
    if recurso == 'Sesion' and int(i) in db['sesiones']:
        return db['sesiones'][int(i)]
    if recurso == 'Catedra' and int(i) in db['catedras']:
        return db['catedras'][int(i)]
    if recurso == 'Asistente':
        return {'id_asistente': i, 'nombres': 'N', 'apellidos': 'A', 'id_asis': '9'}
    raise ErrorApi('No existe.', codigo=404)


def crear(recurso, d):
    db['llamadas'].append((recurso, d))
    if recurso == 'QrSesion':
        if db['falla_qr']:
            raise ErrorApi('La base de datos rechazó la operación.', detalle='caída', codigo=500)
        s = d['fk_sesion']
        if s not in db['sesiones']:
            raise ErrorApi('Sesión no encontrada.', codigo=404)
        if s in db['qr'] and not d.get('regenerar'):
            return {**db['qr'][s], 'ya_existia': True}
        db['sig'] += 1
        poner_qr(s, 45, 60, token=f'aaaaaaaa-0000-0000-0000-{db["sig"]:012d}')
        return db['qr'][s]
    if recurso == 'Catedra':
        id_c = 50 + len([c for c in db['catedras'] if c >= 50])
        db['catedras'][id_c] = {'id_catedra': id_c, 'id_evento_asis': d['id_evento_asis'], 'nombre': d['nombre'], 'activo': True, 'fk_tipo_evento': 'CAAB'}
        for n, s in enumerate(d.get('sesiones', []), start=1):
            db['sig'] += 1
            db['sesiones'][db['sig']] = {'id_sesion': db['sig'], 'fk_catedra': id_c, 'numero_reunion': n, 'estado': 'PROGRAMADA',
                                         'titulo': None, 'inicio': s['inicio'], 'fin': s['fin']}
        return {'id_catedra': id_c, 'nombre_asis': d['nombre'][:30]}
    if recurso == 'Ponencia':
        db['ponencias'].add((d['fk_sesion'], d['fk_asistente'], d['rol']))
        return {'mensaje': 'ok'}
    raise AssertionError(recurso)


def consultar(ruta, **p):
    if ruta == 'QrSesion':
        return db['qr'].get(p['fkSesion'])
    if ruta == 'QrSesion/asistencia':
        return {'total': len(db['asistencia']), 'datos': [dict(x) for x in db['asistencia']]}
    if ruta == 'Disponibilidad/conflictos':
        return {'disponible': True, 'total': 0, 'conflictos': []}
    raise AssertionError(ruta)


for m in (PC, P, Q, AD):
    m.listar = listar
    if hasattr(m, 'obtener'):
        m.obtener = obtener
for m in (PC, Q):
    m.crear = crear
    m.consultar = consultar
AD.crear = crear
AD.listar = lambda recurso, **f: []

app = A.app
app.config['TESTING'] = True
ok = fall = 0


def v(caso, cond):
    global ok, fall
    print(('  OK    ' if cond else '  FALLA ') + caso)
    ok += bool(cond)
    fall += (not cond)


def cliente(roles, asistente=5, usuario=50):
    c = app.test_client()
    if roles is not None:
        with c.session_transaction() as s:
            s['usuario'] = {'id_usuario': usuario, 'usuario': f'u{asistente}', 'fk_asistente': asistente}
            s['roles'] = roles
    return c


def hora(h):
    return (ahora + dt.timedelta(hours=h)).strftime('%Y-%m-%dT%H:00')


def filas_sesion(*horas):
    d = {'ses_titulo': [], 'ses_inicio': [], 'ses_fin': [], 'ses_fk_modalidad': [], 'ses_fk_sede': [], 'ses_fk_periodo': [], 'ses_lugar': [], 'ses_cupo': []}
    for h in horas:
        d['ses_titulo'].append(''); d['ses_inicio'].append(hora(h)); d['ses_fin'].append(hora(h + 1)); d['ses_fk_modalidad'].append('PRESENCIAL')
        d['ses_fk_sede'].append('SAN_BENITO'); d['ses_fk_periodo'].append('2026-2'); d['ses_lugar'].append('Aula 1'); d['ses_cupo'].append('')
    return d


base = {'id_evento_asis': '000099001', 'nombre': 'Catedra con QR', 'fk_tipo_evento': 'CAAB', 'fk_dependencia': ''}

# --- 1. al crear la cátedra se genera el QR de cada sesión ---------------------------------------------
db['llamadas'].clear()
r = cliente(['PONENTE'], 5, usuario=50).post('/ponente/catedras/nueva', data={**base, **filas_sesion(10, 30)})
qrs = [d for (rec, d) in db['llamadas'] if rec == 'QrSesion']
v('Ponente: crear una cátedra con 2 sesiones genera el QR de CADA sesión', r.status_code == 302 and len(qrs) == 2)
v('...cada QR lleva la sesión nueva y quién lo emitió (sin regenerar)', all(d['fk_usuario'] == 50 and d['regenerar'] is False for d in qrs) and len({d['fk_sesion'] for d in qrs}) == 2)
t = cliente(['PONENTE'], 5).get(r.headers['Location']).get_data(as_text=True)
v('...y en la cátedra, el ponente ve «Ver QR» en sus sesiones', t.count('Ver QR') == 2 and '/sesion/' in t and '/qr' in t)
db['llamadas'].clear()
db['falla_qr'] = True
r = cliente(['PONENTE'], 5).post('/ponente/catedras/nueva', data={**base, 'id_evento_asis': '000099002', **filas_sesion(40)}, follow_redirects=True)
t = r.get_data(as_text=True)
v('Si el QR no se pudo generar, la cátedra SÍ se creó y se avisa dónde generarlo', 'Se generó el QR de 0 de 1 sesiones' in t and 'Catedra con QR' in t)
db['falla_qr'] = False
db['llamadas'].clear()
r = cliente(['ADMIN'], 1, usuario=1).post('/admin/catedras/nueva', data={**base, 'id_evento_asis': '000099003', **filas_sesion(50)})
v('Administrador: crear una cátedra desde /admin también genera el QR de sus sesiones', r.status_code == 302 and sum(1 for (rec, _) in db['llamadas'] if rec == 'QrSesion') == 1)

# --- 2. la pantalla del QR ------------------------------------------------------------------------------
poner_qr(1, -5, 55)
url_qr = f'/ponente/catedras/sesion/1/qr'
r = cliente(['PONENTE'], 5).get(url_qr)
t = r.get_data(as_text=True)
esperado_url = f'http://localhost/registro/{TOK}'
v('El ponente de la sesión abre la pantalla del QR (200) con el dibujo SVG', r.status_code == 200 and '<svg' in t and 'viewBox' in t)
v('...el QR codifica la URL del front con el token (el SVG es el de ESA URL)', esperado_url in t and Q.svg_qr(esperado_url) in t)
v('...muestra el estado «Abierto» y hasta qué hora sirve', 'Abierto: se cierra a las' in t)
v('...ofrece copiar el enlace, proyectar a pantalla completa y regenerar', 'id="copiar-url"' in t and 'id="ampliar"' in t and 'Regenerar QR' in t)
v('...y lista la asistencia: inscritos, quién asistió y la ubicación', 'Ana Uno' in t and 'Beto Dos' in t and 'Cami Tres' in t and 'maps.google.com/?q=6.2442,-75.5812' in t)
v('...con los contadores (1 de 2 inscritos asistieron; 1 sin inscripción)', 'id="n-asistieron">1<' in t and 'id="n-inscritos">2<' in t and '1 sin inscripción' in t)
poner_qr(1, 30, 45)
t = cliente(['PONENTE'], 5).get(url_qr).get_data(as_text=True)
v('Antes de la ventana: «Aún no abre» con la hora de apertura', 'Aún no abre: se abre a las' in t)
poner_qr(1, -45, -30)
t = cliente(['PONENTE'], 5).get(url_qr).get_data(as_text=True)
v('Después de la ventana: «Cerrado»', 'Cerrado a las' in t)
app.config['URL_PUBLICA'] = 'https://catedras.example.edu/'
poner_qr(1, -5, 55)
t = cliente(['PONENTE'], 5).get(url_qr).get_data(as_text=True)
v('Con URL_PUBLICA configurada, el QR usa esa dirección (https, sin doble barra)', f'https://catedras.example.edu/registro/{TOK}' in t)
app.config['URL_PUBLICA'] = ''
del db['qr'][1]
t = cliente(['PONENTE'], 5).get(url_qr).get_data(as_text=True)
v('Una sesión sin QR ofrece «Generar el QR» (y no dibuja nada)', 'Generar el QR' in t and '<svg viewBox' not in t)

# --- 3. generar y regenerar ----------------------------------------------------------------------------
db['llamadas'].clear()
r = cliente(['PONENTE'], 5, usuario=50).post(url_qr)
v('POST sin «regenerar» genera el QR de la sesión (302 a la pantalla)', r.status_code == 302 and 1 in db['qr'] and db['llamadas'][0][1]['regenerar'] is False)
tok1 = db['qr'][1]['token']
db['llamadas'].clear()
r = cliente(['PONENTE'], 5, usuario=50).post(url_qr, data={'regenerar': '1'})
v('POST con regenerar=1 crea OTRO token (el anterior deja de servir)', r.status_code == 302 and db['qr'][1]['token'] != tok1 and db['llamadas'][0][1]['regenerar'] is True)

# --- 4. quién puede -----------------------------------------------------------------------------------
for etiqueta, roles, asis, esperado in (('anónimo', None, 5, 302), ('ESTUDIANTE', ['ESTUDIANTE'], 5, 403),
                                        ('PONENTE ajeno a la sesión', ['PONENTE'], 7, 403),
                                        ('PONENTE solo INVITADO en la sesión', ['PONENTE'], 6, 403),
                                        ('PONENTE de la sesión', ['PONENTE'], 5, 200),
                                        ('ORGANIZADOR de la sesión', ['PONENTE'], 8, 200), ('ADMIN', ['ADMIN'], 1, 200)):
    r = cliente(roles, asis).get(url_qr)
    v(f'{etiqueta}: GET QR -> {esperado}', r.status_code == esperado)
    if esperado != 200:
        llamadas_antes = len(db['llamadas'])
        r2 = cliente(roles, asis).post(url_qr, data={'regenerar': '1'})
        v(f'{etiqueta}: POST regenerar -> {esperado} y no toca el QR', r2.status_code == esperado and len(db['llamadas']) == llamadas_antes)
        r3 = cliente(roles, asis).get('/ponente/catedras/sesion/1/asistencia')
        v(f'{etiqueta}: GET asistencia.json -> {esperado}', r3.status_code == esperado)
v('Una sesión inexistente -> 404', cliente(['PONENTE'], 5).get('/ponente/catedras/sesion/999/qr').status_code == 404)

# --- 5. el token no se filtra a quien no es ponente ----------------------------------------------------------
t = cliente(['ESTUDIANTE'], 21).get('/ponente/catedras/sesion/1/qr').get_data(as_text=True)
v('El estudiante no ve ni el token ni el QR por ninguna de estas rutas', db['qr'][1]['token'] not in t)

# --- 6. JSON de asistencia -----------------------------------------------------------------------------------
r = cliente(['PONENTE'], 5).get('/ponente/catedras/sesion/1/asistencia')
j = r.get_json()
v('/asistencia devuelve filas, contadores y estado de la ventana', r.status_code == 200 and len(j['filas']) == 3 and j['contadores']['asistieron'] == 1
  and j['contadores']['inscritos'] == 2 and j['contadores']['faltan'] == 1 and j['estado'] in ('pendiente', 'abierto', 'cerrado'))
v('...con la hora local del registro y sin datos de más', j['filas'][0]['hora'] and j['filas'][1]['hora'] is None)
v('La pantalla del QR trae su script de actualización con textContent (sin innerHTML)',
  'asistencia_json' not in open('templates/ponente_sesion_qr.html', encoding='utf-8').read() or 'innerHTML' not in open('templates/ponente_sesion_qr.html', encoding='utf-8').read())

# --- 7. estado de la ventana ----------------------------------------------------------------------------------
v('estado_de_ventana: sin QR / pendiente / abierto / cerrado / revocado',
  Q.estado_de_ventana(None) == 'sin_qr'
  and Q.estado_de_ventana({'ventana_desde': iso(ahora + dt.timedelta(minutes=5)), 'ventana_hasta': iso(ahora + dt.timedelta(minutes=20))}) == 'pendiente'
  and Q.estado_de_ventana({'ventana_desde': iso(ahora - dt.timedelta(minutes=5)), 'ventana_hasta': iso(ahora + dt.timedelta(minutes=20))}) == 'abierto'
  and Q.estado_de_ventana({'ventana_desde': iso(ahora - dt.timedelta(minutes=50)), 'ventana_hasta': iso(ahora - dt.timedelta(minutes=20))}) == 'cerrado'
  and Q.estado_de_ventana({'ventana_desde': iso(ahora - dt.timedelta(minutes=5)), 'ventana_hasta': iso(ahora + dt.timedelta(minutes=20)), 'revocado_en': iso(ahora)}) == 'revocado')

# --- 8. la clave de asistencia llega por CORREO (no se muestra en pantalla) ----------------------------------
import routes.registro as RG
from servicios import correo as CO

enviados = []


def crear_registro(recurso, d):
    if recurso == 'ClaveAcceso/solicitar':
        return {'id_clave': 77, 'clave': '482915', 'enviado_a': 'ana@ejemplo.com'}
    raise AssertionError(recurso)


RG.crear = crear_registro
CO.enviar_clave_asistencia = lambda destino, codigo, minutos: enviados.append((destino, codigo, minutos))
app.config['SMTP_HOST'] = ''
app.config['CORREO_MODO_CONSOLA'] = False
t = cliente(None).post(f'/registro/{TOK}', data={'identificador': '300'}).get_data(as_text=True)
v('Sin correo configurado (desarrollo): la clave se MUESTRA como simulación del envío y no se manda nada', '482915' in t and 'Simulación del correo' in t and not enviados)
app.config['SMTP_HOST'] = 'smtp.ejemplo.com'
t = cliente(None).post(f'/registro/{TOK}', data={'identificador': '300'}).get_data(as_text=True)
v('Con correo configurado: la clave se ENVÍA al correo de la persona (con su vigencia)', enviados == [('ana@ejemplo.com', '482915', 10)])
v('...y NO aparece en la pantalla (solo «Te enviamos la clave a …»)', '482915' not in t and 'Te enviamos la clave a' in t and 'ana@ejemplo.com' in t and 'name="clave"' in t)


def falla(destino, codigo, minutos):
    raise CO.ErrorEnvio('No se pudo enviar el correo (SMTPException).')


CO.enviar_clave_asistencia = falla
t = cliente(None).post(f'/registro/{TOK}', data={'identificador': '300'}).get_data(as_text=True)
v('Si el correo falla: se avisa, se vuelve al primer paso y la clave NUNCA se muestra', 'No se pudo enviar el correo' in t and '482915' not in t and 'name="identificador"' in t)
app.config['SMTP_HOST'] = ''

print(f'\n{ok} pasadas, {fall} fallidas')
sys.exit(1 if fall else 0)
