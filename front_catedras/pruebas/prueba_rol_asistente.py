# Prueba, sin BD ni API: el rol ASISTENTE inicia sesión y gestiona SOLO cátedras (listar, ver, agregar, editar,
# borrar). Todo lo demás de /admin sigue siendo del ADMIN; estudiantes y ponentes siguen sin poder entrar.
# La API se sustituye por datos falsos.
# Uso, desde front_catedras:   python pruebas/prueba_rol_asistente.py
import os
import sys

os.chdir(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
sys.path.insert(0, '.')
import werkzeug
werkzeug.__version__ = '3'

import app as A
import routes.admin as AD
import routes.admin_restricciones as AR
import routes.admin_solicitudes as AS
import routes.auth as AU
from servicios.api_catedras import ErrorApi

CAT = {'id_catedra': 2, 'id_evento_asis': '000000002', 'nombre': 'Catedra Dos', 'nombre_asis': 'Catedra Dos', 'activo': True,
       'fk_tipo_evento': 'CAAB', 'fk_dependencia': None, 'creado_en': '2026-01-01T00:00:00Z'}
SES = {'id_sesion': 1, 'fk_catedra': 2, 'numero_reunion': 1, 'estado': 'PROGRAMADA', 'titulo': None, 'lugar': 'Aula 1',
       'fk_modalidad': 'PRESENCIAL', 'fk_sede': 'SAN_BENITO', 'fk_periodo': '2026-2', 'fk_encuesta': None,
       'inicio': '2030-01-01T10:00:00Z', 'fin': '2030-01-01T12:00:00Z'}
llamadas = []
roles_login = {'roles': ['ASISTENTE']}


def listar(recurso, limite=None, **f):
    return [CAT] if recurso == 'Catedra' else ([SES] if recurso == 'Sesion' else [])


def obtener(recurso, i):
    if recurso == 'Catedra':
        return CAT
    raise ErrorApi('No existe.', codigo=404)


def crear(recurso, datos):
    llamadas.append(('crear', recurso))
    if recurso == 'Usuario/login':
        return {'estado': 'autenticado', 'roles': roles_login['roles'],
                'usuario': {'id_usuario': 9, 'usuario': 'asis', 'fk_asistente': 40, 'debe_cambiar_clave': False}}
    if recurso == 'Catedra':
        return {'id_catedra': 77, 'nombre_asis': datos['nombre'][:30]}
    raise AssertionError(recurso)


AD.listar, AD.obtener, AD.crear = listar, obtener, crear
AD.reemplazar = lambda recurso, i, d: llamadas.append(('reemplazar', recurso, i))
AD.eliminar = lambda recurso, i: llamadas.append(('eliminar', recurso, i))
AD.consultar = lambda ruta, **p: {'disponible': True, 'total': 0, 'conflictos': [], 'datos': []}
AD._opciones = lambda recurso: []
AD._catalogos_sesion = lambda: {'modalidades': [], 'sedes': [], 'periodos': [], 'encuestas': [], 'estados': []}
AR.consultar = lambda ruta, **p: {'permitido': True, 'hay_restricciones': False, 'mensaje': None, 'ventanas': []}
AR.listar = lambda *a, **k: []
AU.crear = crear

# La bandeja de ponentes (routes/admin_solicitudes.py trae sus propias referencias a la API).
SOL = {'id_solicitud': 7, 'estado': 'PENDIENTE', 'nombre_completo': 'Pepa Ponente', 'titulo_ponencia': 'Una charla', 'resumen': 'Una charla sobre algo importante para todos.',
       'fk_asistente': 21, 'fk_catedra': None, 'fk_sesion': None, 'inicio_propuesto': None, 'fin_propuesto': None,
       'nombre_catedra': None, 'creado_en': '2026-01-01T00:00:00Z'}


def listar_sol(recurso, limite=None, **f):
    return [SOL] if recurso == 'SolicitudPonente' else []


def obtener_sol(recurso, i):
    if recurso == 'SolicitudPonente':
        return SOL
    raise ErrorApi('No existe.', codigo=404)


def resolver_sol(recurso, llave, datos):
    llamadas.append(('resolver', recurso, llave, datos))
    return {'mensaje': 'Solicitud resuelta.', 'solicitud': {}}


AS.listar, AS.obtener, AS.actualizar_parcial = listar_sol, obtener_sol, resolver_sol

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
            s['usuario'] = {'id_usuario': 9, 'usuario': 'asis', 'fk_asistente': 40}
            s['roles'] = roles
    return c


ASISTENTE, ADMIN = ['ASISTENTE'], ['ADMIN']

# ---------------------------------------------------------------- 1. inicio de sesión
r = cliente().post('/login', data={'usuario': 'asis', 'clave': 'Clave-Asistente-1'})
v('Un ASISTENTE inicia sesión y va a la lista de cátedras', r.status_code == 302 and r.headers['Location'].endswith('/admin/catedras'))
roles_login['roles'] = ['ADMIN']
r = cliente().post('/login', data={'usuario': 'admin', 'clave': 'x'})
v('Un ADMIN sigue yendo a su panel', r.status_code == 302 and r.headers['Location'].endswith('/admin/'))
for roles in (['ESTUDIANTE'], ['PONENTE', 'ESTUDIANTE'], []):
    roles_login['roles'] = roles
    v(f'Con los roles {roles or "ninguno"} el front tampoco deja entrar (401)', cliente().post('/login', data={'usuario': 'x', 'clave': 'x'}).status_code == 401)

# ---------------------------------------------------------------- 2. lo que SÍ puede hacer: cátedras
r = cliente(ASISTENTE).get('/admin/catedras')
t = r.get_data(as_text=True)
v('Lista las cátedras (200)', r.status_code == 200 and 'Catedra Dos' in t)
v('...sin el enlace al panel de Administración en la miga de pan', 'href="/admin/"' not in t)
v('La barra de navegación le ofrece «Gestionar cátedras» y no «Administración»',
  'Gestionar cátedras' in t and 'fa-cogs' not in t)
v('La barra del asistente NO trae «Catálogo» (se entra desde el inicio) pero el ADMIN sí', 'href="/catedras"' not in t.split('</nav>')[0] and 'href="/catedras"' in cliente(ADMIN).get('/admin/catedras').get_data(as_text=True).split('</nav>')[0])
r = cliente(ASISTENTE).get('/admin/catedras/nueva')
t = r.get_data(as_text=True)
v('Abre el formulario de cátedra nueva (200)', r.status_code == 200 and 'name="nombre"' in t)
r = cliente(ASISTENTE).get('/admin/catedras/2')
t = r.get_data(as_text=True)
v('Ve el detalle de una cátedra (200), con su botón Editar y Eliminar',
  r.status_code == 200 and '/admin/catedras/2/editar' in t and '/admin/catedras/2/eliminar' in t)
v('...pero sin enlaces a las pantallas de sesiones (no son suyas), salvo el QR de cada sesión (D73)',
  '/admin/sesiones/1"' not in t and '/admin/sesiones/nueva' not in t and '/admin/sesiones/1/editar' not in t
  and '/admin/sesiones/1/eliminar' not in t and 'Nueva sesión' not in t and '/admin/sesiones/1/qr' in t)
r = cliente(ASISTENTE).get('/admin/catedras/2/editar')
v('Abre el formulario de editar (200), sin enlaces a /admin/sesiones', r.status_code == 200 and '/admin/sesiones' not in r.get_data(as_text=True))
r = cliente(ASISTENTE).post('/admin/catedras/nueva', data={'id_evento_asis': '000000077', 'nombre': 'Nueva Catedra', 'fk_tipo_evento': 'CAAB', 'activo': 'on'})
v('AGREGA una cátedra (redirige al detalle) y la API recibe el POST',
  r.status_code == 302 and '/admin/catedras/77' in r.headers['Location'] and ('crear', 'Catedra') in llamadas)
r = cliente(ASISTENTE).post('/admin/catedras/2/editar', data={'id_evento_asis': '000000002', 'nombre': 'Catedra Dos v2', 'fk_tipo_evento': 'CAAB', 'activo': 'on'})
v('EDITA una cátedra (redirige) y la API recibe el reemplazo', r.status_code == 302 and ('reemplazar', 'Catedra', 2) in llamadas)
r = cliente(ASISTENTE).post('/admin/catedras/2/eliminar')
v('BORRA una cátedra (redirige a la lista) y la API recibe el borrado', r.status_code == 302 and ('eliminar', 'Catedra', 2) in llamadas)
r = cliente(ASISTENTE).get('/admin/catedras/disponibilidad', query_string={'inicio': '2030-01-01T10:00:00Z', 'fin': '2030-01-01T11:00:00Z', 'sede': 'SAN_BENITO'})
v('Usa la consulta de disponibilidad del formulario (200)', r.status_code == 200)

# ---------------------------------------------------------------- 2b. la bandeja de ponentes (como la del ADMIN)
r = cliente(ASISTENTE).get('/admin/catedras')
t = r.get_data(as_text=True)
v('La lista de cátedras del asistente muestra la tarjeta «Bandeja de ponentes» con 1 pendiente',
  'Bandeja de ponentes' in t and '1 pendiente' in t and '/admin/solicitudes-ponente' in t)
v('La barra del asistente ya NO trae «Bandeja de ponentes» (queda la tarjeta de la lista)', '/admin/solicitudes-ponente' not in t.split('</nav>')[0])
r = cliente(ASISTENTE).get('/admin/solicitudes-ponente')
t = r.get_data(as_text=True)
v('Abre la bandeja (200) con la solicitud de Pepa', r.status_code == 200 and 'Pepa' in t)
v('...sin enlace al panel de Administración (su miga lleva a Cátedras)', 'href="/admin/"' not in t and 'href="/admin/catedras"' in t)
r = cliente(ASISTENTE).get('/admin/solicitudes-ponente/7')
t = r.get_data(as_text=True)
v('Abre una solicitud (200) con el formulario de decisión', r.status_code == 200 and 'form-decision' in t)
v('...sin enlaces a pantallas que no son suyas (asistentes, sesiones, ponencias)',
  '/admin/asistentes' not in t and '/admin/sesiones' not in t and '/admin/ponencias' not in t)
r = cliente(ASISTENTE).post('/admin/solicitudes-ponente/7/resolver', data={'decision': 'rechazar', 'observacion': 'No corresponde a la cátedra.'})
v('RECHAZA una solicitud con su motivo y la API recibe la decisión',
  r.status_code == 302 and any(x[0] == 'resolver' and x[2] == '7/resolver' and x[3]['aprobar'] is False and x[3]['resuelto_por'] == 9 for x in llamadas))
r = cliente(ASISTENTE).post('/admin/solicitudes-ponente/7/resolver', data={'decision': 'rechazar', 'observacion': ''})
v('Rechazar sin motivo no llega a la API (vuelve con aviso)', r.status_code == 302 and sum(1 for x in llamadas if x[0] == 'resolver') == 1)
r = cliente(ASISTENTE).post('/admin/solicitudes-ponente/7/resolver', data={'decision': 'aprobar'})
v('APRUEBA una solicitud (sin cátedra propuesta) y la API recibe la aprobación',
  r.status_code == 302 and any(x[0] == 'resolver' and x[3]['aprobar'] is True for x in llamadas))
t = cliente(ADMIN).get('/admin/catedras').get_data(as_text=True)
v('El ADMIN NO ve esa tarjeta en la lista de cátedras (él la tiene en su panel)', 'que llegan del calendario' not in t)
v('Un ESTUDIANTE con sesión no entra a la bandeja (403)', cliente(['ESTUDIANTE']).get('/admin/solicitudes-ponente').status_code == 403)

# ---------------------------------------------------------------- 3. lo que NO puede hacer: todo lo demás
for ruta in ('/admin/', '/admin/usuarios', '/admin/usuarios/nuevo', '/admin/roles-usuario', '/admin/sedes', '/admin/sesiones/nueva',
             '/admin/sesiones/1', '/admin/asistentes', '/admin/encuestas', '/admin/restricciones-programacion'):
    v(f'GET {ruta} -> 403', cliente(ASISTENTE).get(ruta).status_code == 403)
antes = len(llamadas)
r = cliente(ASISTENTE).post('/admin/usuarios/nuevo', data={'nombres': 'X'})
v('POST /admin/usuarios/nuevo -> 403 y NO llega a la API', r.status_code == 403 and len(llamadas) == antes)
r = cliente(ASISTENTE).post('/admin/usuarios/5/eliminar')
v('POST /admin/usuarios/5/eliminar -> 403 y NO llega a la API', r.status_code == 403 and len(llamadas) == antes)
r = cliente(ASISTENTE).post('/admin/sesiones/1/eliminar')
v('POST /admin/sesiones/1/eliminar -> 403', r.status_code == 403)
v('Las zonas de ponente y de estudiante ya no existen (404, D55): el asistente tampoco entra',
  cliente(ASISTENTE).get('/ponente/').status_code in (403, 404) and cliente(ASISTENTE).get('/estudiante/').status_code in (403, 404))
v('Sin sesión, /admin/catedras manda a /login', cliente().get('/admin/catedras').status_code == 302)
v('Un ESTUDIANTE con sesión no entra ni a las cátedras de /admin (403)', cliente(['ESTUDIANTE']).get('/admin/catedras').status_code == 403)

# ---------------------------------------------------------------- 4. el ADMIN conserva todo
t = cliente(ADMIN).get('/admin/catedras/2').get_data(as_text=True)
v('El ADMIN sigue viendo en el detalle los enlaces de sesiones y «Nueva sesión»', '/admin/sesiones/1' in t and 'Nueva sesión' in t)
v('El ADMIN sigue viendo la miga «Administración»', 'href="/admin/"' in cliente(ADMIN).get('/admin/catedras').get_data(as_text=True))
v('El ADMIN sigue entrando a /admin/usuarios', cliente(ADMIN).get('/admin/usuarios').status_code == 200)

# ---------------------------------------------------------------- 5. vista de registro por enlace eliminada + equipo
v('La pantalla para pegar el enlace ya no existe (GET /registro -> 404)', cliente().get('/registro').status_code == 404)
t = cliente().get('/').get_data(as_text=True)
v('El pie de página no ofrece «Registro de asistencia» por enlace ni «Pégalo aquí»', 'Registro de asistencia' not in t and 'Pégalo aquí' not in t)
v('El login tampoco ofrece pegar un enlace', 'Pégalo aquí' not in cliente().get('/login').get_data(as_text=True))
v('El pie de página nombra al grupo: Isaac Ocampo, Alejandro Zuleta, German Valencia y Jhoan Velez',
  'Isaac Ocampo, Alejandro Zuleta, German Valencia y Jhoan Velez' in t)
v('...y también aparece dentro de /admin (mismo pie)', 'Jhoan Velez' in cliente(ADMIN).get('/admin/catedras').get_data(as_text=True))

# ---------------------------------------------------------------- 6. la entidad persona se llama «Personas» y muestra su ROL (no «Externo»)
PERSONAS = [
    {'id_asistente': 5, 'id_asis': None, 'nombres': 'Ana', 'apellidos': 'Uno', 'correo_institucional': 'a@b.co', 'correo_personal': None,
     'es_externo': True, 'activo': True, 'roles_cuenta': ['Asistente'], 'vinculaciones': []},
    {'id_asistente': 6, 'id_asis': '3001', 'nombres': 'Beto', 'apellidos': 'Dos', 'correo_institucional': 'b@b.co', 'correo_personal': None,
     'es_externo': False, 'activo': True, 'roles_cuenta': [], 'vinculaciones': ['Estudiante USB']},
    {'id_asistente': 7, 'id_asis': None, 'nombres': 'Cami', 'apellidos': 'Tres', 'correo_institucional': None, 'correo_personal': 'c@b.co',
     'es_externo': True, 'activo': True, 'roles_cuenta': [], 'vinculaciones': []},
]
AD.listar = lambda recurso, limite=None, **f: PERSONAS if recurso == 'Asistente' else listar(recurso, limite, **f)
t = cliente(ADMIN).get('/admin/asistentes').get_data(as_text=True)
v('La lista se titula «Personas» y su botón dice «Nueva persona»', 'Personas · Administración' in t and 'Nueva persona' in t)
v('...sin la palabra «Asistentes» (Asistente es un rol, no la persona)', 'Asistentes' not in t and 'Nuevo asistente' not in t)
v('La columna «Externo» se cambió por «Rol»', '<th>Rol</th>' in t and '<th>Externo</th>' not in t and '>Externo<' not in t)
v('Una persona con cuenta muestra el rol de su cuenta (Asistente)', 'badge-primary">Asistente<' in t)
v('Una persona sin cuenta muestra su vinculación vigente (Estudiante USB)', 'badge-secondary">Estudiante USB<' in t)
v('Una persona sin cuenta ni vinculación muestra un guion, y ya NO «Externo» aunque es_externo sea verdadero', t.count('es_externo') == 0 and '>Externo<' not in t)
t = cliente(ADMIN).get('/admin/').get_data(as_text=True)
v('El panel nombra la tarjeta «Persona» y la sección «Personas y carga masiva»', '>Persona</h6>' in t and 'Personas y carga masiva' in t and 'carga de asistentes' not in t)
AD.listar = listar

print(f'\n{ok} pasadas, {fall} fallidas')
sys.exit(1 if fall else 0)
