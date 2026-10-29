# Prueba v7.1 (D49), sin BD ni API: el ponente crea una cátedra y otro ponente se asigna a ella.
# La API se sustituye por un almacén en memoria (cátedras, sesiones y ponencias).
# Uso, desde front_catedras:   python pruebas/prueba_ponente_catedras_v71.py
import datetime as dt
import os
import sys

os.chdir(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
sys.path.insert(0, '.')
import werkzeug
werkzeug.__version__ = '3'

import app as A
import routes.admin as AD
import routes.ponente as P
import routes.ponente_catedras as PC
from servicios.api_catedras import ErrorApi

ahora = dt.datetime.now(dt.timezone.utc)
db = {'catedras': {}, 'sesiones': {}, 'ponencias': set(), 'sig_cat': 100, 'sig_ses': 1000, 'llamadas': []}


def listar(recurso, limite=None, **f):
    if recurso == 'Catedra':
        return [c for c in db['catedras'].values() if c['activo']]
    if recurso == 'Sesion':
        return [s for s in db['sesiones'].values() if s['fk_catedra'] == f.get('fkCatedra')]
    if recurso == 'Ponencia':
        return [{'fk_sesion': s, 'fk_asistente': a, 'rol': r} for (s, a, r) in db['ponencias']
                if f.get('fkAsistente') in (None, a)]
    return []


def obtener(recurso, i):
    if recurso == 'Catedra' and int(i) in db['catedras']:
        return db['catedras'][int(i)]
    if recurso == 'Sesion' and int(i) in db['sesiones']:
        return db['sesiones'][int(i)]
    if recurso == 'Asistente':
        return {'id_asistente': i, 'nombres': 'N', 'apellidos': 'A', 'id_asis': '9'}
    raise ErrorApi('No existe.', codigo=404)


def crear(recurso, datos):
    db['llamadas'].append(('crear', recurso, datos))
    if recurso == 'Catedra':
        db['sig_cat'] += 1
        id_c = db['sig_cat']
        db['catedras'][id_c] = {'id_catedra': id_c, 'id_evento_asis': datos['id_evento_asis'], 'nombre': datos['nombre'],
                                'fk_tipo_evento': datos['fk_tipo_evento'], 'activo': datos['activo']}
        for n, s in enumerate(datos.get('sesiones', []), start=1):
            db['sig_ses'] += 1
            db['sesiones'][db['sig_ses']] = {'id_sesion': db['sig_ses'], 'fk_catedra': id_c, 'numero_reunion': n,
                                             'estado': 'PROGRAMADA', 'titulo': s.get('titulo'),
                                             'inicio': s['inicio'], 'fin': s['fin']}
        return {'id_catedra': id_c, 'nombre_asis': datos['nombre'][:30]}
    if recurso == 'Ponencia':
        llave = (datos['fk_sesion'], datos['fk_asistente'], datos['rol'])
        if llave in db['ponencias']:
            raise ErrorApi('La base de datos rechazó la operación.', detalle='PK duplicada', codigo=500)
        db['ponencias'].add(llave)
        return {'mensaje': 'ok'}
    raise AssertionError(recurso)


def eliminar(recurso, i):
    db['llamadas'].append(('eliminar', recurso, i))
    s, a, r = i.split('/')
    db['ponencias'].discard((int(s), int(a), r))
    return {'mensaje': 'ok'}


for m in (PC, P):
    m.listar, m.obtener = listar, obtener
PC.crear, PC.eliminar = crear, eliminar
AD.listar = lambda recurso, **f: []          # catálogos de los <select>

app = A.app
app.config['TESTING'] = True
ok = fall = 0


def v(caso, cond):
    global ok, fall
    print(('  OK    ' if cond else '  FALLA ') + caso)
    ok += bool(cond)
    fall += (not cond)


def cliente(roles, asistente=5):
    c = app.test_client()
    if roles is not None:
        with c.session_transaction() as s:
            s['usuario'] = {'id_usuario': asistente, 'usuario': f'u{asistente}', 'fk_asistente': asistente}
            s['roles'] = roles
    return c


def hora(horas):
    return (ahora + dt.timedelta(hours=horas)).strftime('%Y-%m-%dT%H:%M')


def sesion_form(*horas):
    d = {'ses_titulo': [], 'ses_inicio': [], 'ses_fin': [], 'ses_fk_modalidad': [], 'ses_fk_sede': [],
         'ses_fk_periodo': [], 'ses_lugar': [], 'ses_cupo': []}
    for h in horas:
        d['ses_titulo'].append('T'); d['ses_inicio'].append(hora(h)); d['ses_fin'].append(hora(h + 1))
        d['ses_fk_modalidad'].append('PRESENCIAL'); d['ses_fk_sede'].append('SAN_BENITO')
        d['ses_fk_periodo'].append('2026-2'); d['ses_lugar'].append(''); d['ses_cupo'].append('')
    return d


base = {'id_evento_asis': '000099001', 'nombre': 'Catedra de prueba', 'fk_tipo_evento': 'CATEDRA', 'fk_dependencia': ''}

# --- quién puede entrar -----------------------------------------------------
rutas = [('GET', '/ponente/catedras/'), ('GET', '/ponente/catedras/nueva'), ('POST', '/ponente/catedras/nueva'),
         ('GET', '/ponente/catedras/101'), ('POST', '/ponente/catedras/101/asignarme'),
         ('POST', '/ponente/catedras/101/quitarme')]
for etiqueta, roles, esperado in (('anonimo', None, 302), ('ESTUDIANTE', ['ESTUDIANTE'], 403)):
    for metodo, ruta in rutas:
        r = cliente(roles).open(ruta, method=metodo, data=base if metodo == 'POST' else None)
        v(f'{etiqueta} {metodo} {ruta} -> {esperado}', r.status_code == esperado)
v('...y no se creo nada', not db['catedras'] and not db['ponencias'])

# --- el ponente A crea una cátedra ------------------------------------------
v('PONENTE A: GET /ponente/catedras/nueva -> 200', cliente(['PONENTE'], 5).get('/ponente/catedras/nueva').status_code == 200)
r = cliente(['PONENTE'], 5).post('/ponente/catedras/nueva', data=base)
v('Crear una catedra SIN sesiones no la crea (no habria a que asignarse)', r.status_code == 200 and not db['catedras'])
r = cliente(['PONENTE'], 5).post('/ponente/catedras/nueva', data={**base, **sesion_form(1, 25)})
v('PONENTE A: crea la catedra con 2 sesiones -> redirige a su detalle', r.status_code == 302 and '/ponente/catedras/101' in r.headers['Location'])
ses = sorted(db['sesiones'])
v('...la catedra quedo activa con sus 2 sesiones', db['catedras'][101]['activo'] and len(ses) == 2)
v('...y A quedo como PONENTE en las dos', db['ponencias'] == {(ses[0], 5, 'PONENTE'), (ses[1], 5, 'PONENTE')})

# --- el ponente B la encuentra y se asigna ----------------------------------
t = cliente(['PONENTE'], 6).get('/ponente/catedras/?q=prueba').get_data(as_text=True)
v('PONENTE B: la encuentra buscando por nombre y NO figura como participante', 'Catedra de prueba' in t and 'Participas' not in t)
t = cliente(['PONENTE'], 5).get('/ponente/catedras/').get_data(as_text=True)
v('PONENTE A: en la lista aparece como «Participas»', 'Participas' in t)
t = cliente(['PONENTE'], 6).get('/ponente/catedras/101').get_data(as_text=True)
v('PONENTE B: el detalle muestra las 2 sesiones con casillas', t.count('name="sesiones"') == 2)

r = cliente(['PONENTE'], 6).post('/ponente/catedras/101/asignarme', data={'sesiones': [str(ses[0])]})
v('B se asigna a la sesion 1 -> queda como PONENTE solo ahi', r.status_code == 302 and (ses[0], 6, 'PONENTE') in db['ponencias'] and (ses[1], 6, 'PONENTE') not in db['ponencias'])
db['llamadas'].clear()
cliente(['PONENTE'], 6).post('/ponente/catedras/101/asignarme', data={'sesiones': [str(ses[0])]})
v('B repite la misma sesion -> no se vuelve a crear (sin error de llave duplicada)', not any(l[1] == 'Ponencia' for l in db['llamadas']))
cliente(['PONENTE'], 6).post('/ponente/catedras/101/asignarme', data={'sesiones': ['99999', 'abc']})
v('Ids de sesion ajenos o basura se ignoran', not any(l[1] == 'Ponencia' for l in db['llamadas']))
cliente(['PONENTE'], 6).post('/ponente/catedras/101/asignarme', data={'sesiones': [str(ses[1])]})
v('B se asigna tambien a la sesion 2', (ses[1], 6, 'PONENTE') in db['ponencias'])

# --- solo se actúa sobre uno mismo -------------------------------------------
cliente(['PONENTE'], 6).post('/ponente/catedras/101/asignarme', data={'sesiones': [str(ses[0])], 'fk_asistente': '9', 'rol': 'ORGANIZADOR'})
v('Mandar fk_asistente/rol en el formulario no sirve: nadie mas queda asignado', {a for (_, a, _) in db['ponencias']} == {5, 6} and {r for (_, _, r) in db['ponencias']} == {'PONENTE'})
cliente(['PONENTE'], 6).post('/ponente/catedras/101/quitarme', data={'sesiones': [str(ses[0])], 'fk_asistente': '5'})
v('B se quita de la sesion 1: solo sale B, A sigue', (ses[0], 6, 'PONENTE') not in db['ponencias'] and (ses[0], 5, 'PONENTE') in db['ponencias'])
db['llamadas'].clear()
cliente(['PONENTE'], 7).post('/ponente/catedras/101/quitarme', data={'sesiones': [str(ses[0]), str(ses[1])]})
v('Un ponente C que no participa no puede quitar a nadie', not any(l[0] == 'eliminar' for l in db['llamadas']) and (ses[0], 5, 'PONENTE') in db['ponencias'])

# --- otros casos -------------------------------------------------------------
db['catedras'][101]['activo'] = False
v('Una catedra inactiva no se muestra ni se puede asignar (404)',
  cliente(['PONENTE'], 6).get('/ponente/catedras/101').status_code == 404 and
  cliente(['PONENTE'], 6).post('/ponente/catedras/101/asignarme', data={'sesiones': [str(ses[0])]}).status_code == 404)
db['catedras'][101]['activo'] = True
v('Una catedra inexistente -> 404', cliente(['PONENTE'], 6).get('/ponente/catedras/555').status_code == 404)
v('ADMIN tambien puede abrir la lista', cliente(['ADMIN'], 1).get('/ponente/catedras/').status_code == 200)
t = cliente(['PONENTE'], 5).get('/ponente/').get_data(as_text=True)
v('El calendario del ponente ofrece «Nueva cátedra» y «asignarme a una existente»', 'Nueva cátedra' in t and 'asignarme a una existente' in t)

print(f'\n{ok} pasadas, {fall} fallidas')
sys.exit(1 if fall else 0)
