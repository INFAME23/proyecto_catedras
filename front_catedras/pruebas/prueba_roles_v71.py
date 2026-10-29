# Prueba v7.1 (sin BD ni API): quien puede asignar roles en una sesion, cargar datos y
# registrarse. Sustituye las llamadas a la API por datos falsos.
# Uso, desde front_catedras:   python pruebas/prueba_roles_v71.py
import os
os.chdir(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import sys, datetime as dt
import werkzeug; werkzeug.__version__ = "3"
sys.path.insert(0, ".")
import routes.sesiones as S, routes.carga as C, routes.encuestas as E, routes.registro as R
import app as A

ahora = dt.datetime.now(dt.timezone.utc)
iso = lambda d: d.isoformat()
estado = {'enlaces': [], 'llamadas': []}

def listar(recurso, limite=None, **f):
    estado['llamadas'].append(('listar', recurso, f))
    if recurso == 'EnlaceRegistro': return estado['enlaces']
    if recurso == 'Ponencia': return [{'fk_sesion': 1, 'fk_asistente': 9, 'rol': 'PONENTE'}]
    if recurso == 'Asistente': return [{'id_asistente': 5, 'nombres': 'X', 'apellidos': 'Y', 'id_asis': '1'}]
    return []
def obtener(recurso, i):
    if recurso == 'Sesion': return {'id_sesion': 1, 'fk_catedra': 2, 'numero_reunion': 1, 'estado': 'ABIERTA',
        'inicio': iso(ahora - dt.timedelta(hours=1)), 'fin': iso(ahora + dt.timedelta(hours=1)),
        'fk_modalidad': 'PRESENCIAL', 'fk_sede': 'SAN_BENITO', 'fk_periodo': '2026-2', 'fk_encuesta': None}
    if recurso == 'Catedra': return {'id_catedra': 2, 'nombre': 'Cat', 'id_evento_asis': 'E'}
    return {'id_asistente': i, 'nombres': 'N', 'apellidos': 'A', 'id_asis': '9'}
def crear(r, d): estado['llamadas'].append(('crear', r, d)); return {'mensaje': 'ok'}
def eliminar(r, i): estado['llamadas'].append(('eliminar', r, i)); return {'mensaje': 'ok'}
for m in (S, C, E, R):
    for n, f in (('listar', listar), ('obtener', obtener), ('crear', crear), ('eliminar', eliminar)):
        if hasattr(m, n): setattr(m, n, f)

app = A.app; app.config['TESTING'] = True
ok = fall = 0
def v(caso, cond):
    global ok, fall
    print(('  OK    ' if cond else '  FALLA ') + caso); ok += cond; fall += (not cond)

def cliente(roles):
    c = app.test_client()
    if roles is not None:
        with c.session_transaction() as s:
            s['usuario'] = {'id_usuario': 1, 'usuario': 'u', 'fk_asistente': 5}
            s['roles'] = roles
    return c

post = {'fk_asistente': '5', 'rol': 'ORGANIZADOR'}
for etiqueta, roles, esperado in (('anonimo', None, 302), ('ESTUDIANTE', ['ESTUDIANTE'], 403),
                                  ('PONENTE', ['PONENTE'], 403), ('ADMIN', ['ADMIN'], 302)):
    for ruta in ('/sesiones/1/ponentes', '/sesiones/1/ponentes/quitar'):
        estado['llamadas'].clear()
        r = cliente(roles).post(ruta, data=post)
        escribio = any(l[0] in ('crear', 'eliminar') for l in estado['llamadas'])
        v(f'{etiqueta} POST {ruta} -> {esperado}' + (' y llega a la API' if etiqueta == 'ADMIN' else ' y NO toca la API'),
          r.status_code == esperado and escribio == (etiqueta == 'ADMIN'))
    if etiqueta == 'anonimo':
        v('...el anonimo va a /login', '/login' in r.headers['Location'])

for etiqueta, roles, esperado in (('anonimo', None, 302), ('ESTUDIANTE', ['ESTUDIANTE'], 403), ('ADMIN', ['ADMIN'], 200)):
    r = cliente(roles).get('/carga_datos')
    v(f'{etiqueta} GET /carga_datos -> {esperado}', r.status_code == esperado)
v('ESTUDIANTE POST /carga_datos -> 403', cliente(['ESTUDIANTE']).post('/carga_datos', data={}).status_code == 403)

# detalle: lo que ve cada quien
tok = '11111111-2222-3333-4444-555555555555'
vig = {'token': tok, 'ventana_desde': iso(ahora - dt.timedelta(hours=2)), 'ventana_hasta': iso(ahora + dt.timedelta(hours=2)), 'revocado_en': None}
estado['enlaces'] = [vig]
h = cliente(['ESTUDIANTE']).get('/sesiones/1?q=algo').get_data(as_text=True)
v('ESTUDIANTE ve «Registrar mi asistencia» hacia /registro/<token>', 'Registrar mi asistencia' in h and f'/registro/{tok}' in h)
v('...no ve el formulario de asignar ni el de quitar', 'Asignar una persona' not in h and 'fa-user-minus' not in h and 'name="rol"' not in h)
v('...y la busqueda ?q= no le lista personas', 'Persona (' not in h)
h = cliente(None).get('/sesiones/1').get_data(as_text=True)
v('Anonimo no ve boton de registro ni formularios', 'Registrar mi asistencia' not in h and 'Asignar una persona' not in h)
h = cliente(['ADMIN']).get('/sesiones/1?q=algo').get_data(as_text=True)
v('ADMIN sigue viendo asignar/quitar y candidatos', 'Asignar una persona' in h and 'fa-user-minus' in h and 'Persona (' in h)
v('...y no ve el boton de estudiante', 'Registrar mi asistencia' not in h)
for caso, enlaces in (('revocado', [{**vig, 'revocado_en': iso(ahora)}]),
                      ('vencido', [{**vig, 'ventana_hasta': iso(ahora - dt.timedelta(minutes=1))}]),
                      ('aun no abre', [{**vig, 'ventana_desde': iso(ahora + dt.timedelta(minutes=5))}]), ('sin enlace', [])):
    estado['enlaces'] = enlaces
    h = cliente(['ESTUDIANTE']).get('/sesiones/1').get_data(as_text=True)
    v(f'Enlace {caso}: sin boton, con aviso', 'Registrar mi asistencia' not in h and 'no está abierto' in h)
r = cliente(['ADMIN']).get('/admin/ponencias?fk_sesion=1')
h = r.get_data(as_text=True)
v('Admin: pantalla de ponencias de /admin conserva el formulario', r.status_code == 200 and 'Asignar una persona' in h)
h = cliente(['ESTUDIANTE']).get(f'/registro/{tok}').get_data(as_text=True)
v('Estudiante: /registro/<token> viene con su codigo ASIS precargado', 'value="9"' in h)
h = cliente(None).get(f'/registro/{tok}').get_data(as_text=True)
v('Anonimo: el formulario de registro sigue vacio', 'value="9"' not in h)
# D47: un PONENTE solo ve su propio calendario (su fk_asistente es 5)
import routes.ponente as P
P.listar, P.obtener = listar, obtener
h = cliente(['PONENTE']).get('/ponente/')
t = h.get_data(as_text=True)
v('PONENTE: /ponente/ abre su calendario y NO muestra buscador', h.status_code == 200 and 'type="search"' not in t and 'Este es tu calendario' in t)
v('PONENTE: ?asistente=<otro> -> 403', cliente(['PONENTE']).get('/ponente/?asistente=9').status_code == 403)
v('PONENTE: ?asistente=<el suyo> -> 200', cliente(['PONENTE']).get('/ponente/?asistente=5').status_code == 200)
estado['llamadas'].clear()
t = cliente(['PONENTE']).get('/ponente/?q=algo').get_data(as_text=True)
v('PONENTE: ?q= se ignora (no busca personas)', not any(l[1] == 'Asistente' and 'q' in l[2] for l in estado['llamadas']))
r = cliente(['ADMIN']).get('/ponente/?asistente=9'); t = r.get_data(as_text=True)
v('ADMIN: ve el calendario de otra persona y tiene buscador', r.status_code == 200 and 'type="search"' in t)
# D48: el admin crea al ponente en un paso (persona + cuenta + rol), sin buscarlo
import routes.admin as AD
from servicios.api_catedras import ErrorApi
fallar = {'recurso': None}
def crear_adm(r, d):
    estado['llamadas'].append(('crear', r, d))
    if fallar['recurso'] == r: raise ErrorApi('La base de datos rechazo la operacion.', detalle='duplicado', codigo=500)
    return {'Asistente': {'id_asistente': 77}, 'Usuario': {'id_usuario': 88}}.get(r, {'mensaje': 'ok'})
AD.crear, AD.eliminar = crear_adm, eliminar
form = {'nombres': 'Maria', 'apellidos': 'Perez', 'correo_personal': 'maria@ejemplo.com', 'es_externo': 'on', 'usuario': 'maria.perez', 'clave': ''}
def alta(roles, **cambios):
    estado['llamadas'].clear(); fallar['recurso'] = cambios.pop('falla', None)
    r = cliente(roles).post('/admin/ponentes/nuevo', data={**form, **cambios})
    return r, r.get_data(as_text=True), [(l[0], l[1]) for l in estado['llamadas'] if l[0] in ('crear', 'eliminar')]
v('ADMIN: GET /admin/ponentes/nuevo -> 200', cliente(['ADMIN']).get('/admin/ponentes/nuevo').status_code == 200)
for etiqueta, roles, esperado in (('anonimo', None, 302), ('ESTUDIANTE', ['ESTUDIANTE'], 403), ('PONENTE', ['PONENTE'], 403)):
    r, t, llamadas = alta(roles)
    v(f'{etiqueta}: POST /admin/ponentes/nuevo -> {esperado} y no crea nada', r.status_code == esperado and not llamadas)
r, t, llamadas = alta(['ADMIN'])
v('ADMIN: crea Asistente, Usuario y rol, en ese orden', llamadas == [('crear', 'Asistente'), ('crear', 'Usuario'), ('crear', 'RolPorUsuario')])
rol = [l for l in estado['llamadas'] if l[1] == 'RolPorUsuario'][0][2]
usr = [l for l in estado['llamadas'] if l[1] == 'Usuario'][0][2]
asi = [l for l in estado['llamadas'] if l[1] == 'Asistente'][0][2]
v('...el rol es PONENTE, ligado al usuario creado', rol == {'fk_usuario': 88, 'fk_rol': 'PONENTE'})
v('...la cuenta queda ligada a la persona creada (sin buscarla)', usr['fk_asistente'] == 77 and usr['usuario'] == 'maria.perez')
v('...externo y sin codigo ASIS', asi['es_externo'] is True and 'id_asis' not in asi)
v('...clave vacia -> se genera una de 12 caracteres y se muestra una vez', len(usr['clave']) == 12 and usr['clave'] in t and 'maria.perez' in t)
r, t, llamadas = alta(['ADMIN'], clave='MiClave-123')
v('...si el admin escribe la clave, se usa esa', [l for l in estado['llamadas'] if l[1] == 'Usuario'][0][2]['clave'] == 'MiClave-123')
r, t, llamadas = alta(['ADMIN'], correo_personal='')
v('Sin ningun correo: no crea nada y avisa', not llamadas and 'al menos un correo' in t)
r, t, llamadas = alta(['ADMIN'], falla='Usuario')
v('Falla al crear la cuenta -> se borra la persona', llamadas == [('crear', 'Asistente'), ('crear', 'Usuario'), ('eliminar', 'Asistente')])
r, t, llamadas = alta(['ADMIN'], falla='RolPorUsuario')
v('Falla al dar el rol -> se borran la cuenta y la persona (en ese orden)', llamadas == [('crear', 'Asistente'), ('crear', 'Usuario'), ('crear', 'RolPorUsuario'), ('eliminar', 'Usuario'), ('eliminar', 'Asistente')])
r, t, llamadas = alta(['ADMIN'], falla='Asistente')
v('Falla al crear la persona -> no se crea nada mas', llamadas == [('crear', 'Asistente')] and 'Crear ponente' in t)
print(f'\n{ok} pasadas, {fall} fallidas'); sys.exit(1 if fall else 0)
