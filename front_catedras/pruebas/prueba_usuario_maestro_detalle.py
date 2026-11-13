# Prueba (Guía IA, Regla 1), sin BD ni API: "Nuevo usuario" es un MAESTRO-DETALLE. El usuario SE CREA
# (persona + cuenta) con sus ROLES en filas, en UN solo POST a /api/Usuario/con-roles; ya no se busca a
# una persona. Se reemplazan 'listar' y 'crear' para no necesitar la API.
# Uso, desde front_catedras:   python pruebas/prueba_usuario_maestro_detalle.py
import os
import sys

os.chdir(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
sys.path.insert(0, '.')
import werkzeug
werkzeug.__version__ = '3'

import app as A
from routes import admin as R
from servicios.api_catedras import ErrorApi

app = A.app
app.config['TESTING'] = True
ok = fall = 0
enviados = []
fallar = {'ex': None}


def v(caso, cond):
    global ok, fall
    print(('  OK    ' if cond else '  FALLA ') + caso)
    ok += bool(cond)
    fall += (not cond)


R.listar = lambda recurso, **kw: [
    {'id_rol': 'ADMIN', 'nombre': 'Administrador'},
    {'id_rol': 'ASISTENTE', 'nombre': 'Asistente'},
] if recurso == 'Rol' else []


def crear_falso(recurso, datos):
    if fallar['ex']:
        raise fallar['ex']
    enviados.append((recurso, datos))
    return {'id_usuario': 9}


R.crear = crear_falso


def admin():
    c = app.test_client()
    with c.session_transaction() as s:
        s['usuario'] = {'id_usuario': 1, 'usuario': 'a', 'fk_asistente': 1}
        s['roles'] = ['ADMIN']
    return c


r = admin().get('/admin/usuarios/nuevo')
t = r.get_data(as_text=True)
v('GET /admin/usuarios/nuevo -> 200', r.status_code == 200)
v('Ya no hay búsqueda de personas (el usuario se crea, no se busca)',
  'name="q"' not in t and 'name="fk_asistente"' not in t and 'Buscar a la persona' not in t)
v('Trae los datos de la persona (nombres, apellidos, correos, celular)',
  all(f'name="{c}"' in t for c in ('nombres', 'apellidos', 'correo_institucional', 'correo_personal', 'celular')))
v('Trae los datos de la cuenta (usuario, clave, activo, cambiar clave)',
  all(f'name="{c}"' in t for c in ('usuario', 'clave', 'activo', 'debe_cambiar_clave')))
v('Trae el bloque de roles como filas de detalle, con una fila vacía lista',
  'id="roles"' in t and 'data-agregar-fila="roles"' in t and t.count('name="rol_fk_rol"') == 2)   # 1 fila + el <template>
v('...y el <select> del rol ofrece todos los roles del catálogo',
  all(f'value="{r_}"' in t for r_ in ('ADMIN', 'ASISTENTE')))

form = {
    'nombres': ' Ana ', 'apellidos': 'Gómez', 'correo_institucional': 'ana@usbmed.edu.co',
    'correo_personal': '', 'celular': '', 'usuario': 'ana.gomez', 'clave': 'Clave-Segura-1',
    'activo': 'on', 'debe_cambiar_clave': 'on',
    'rol_fk_rol': ['ADMIN', 'ASISTENTE', ''], 'rol_fecha_ini': ['', '2026-02-01', ''],
}
r = admin().post('/admin/usuarios/nuevo', data=form)
v('POST válido -> redirige a la lista de usuarios', r.status_code == 302 and r.headers['Location'].endswith('/admin/usuarios'))
recurso, cuerpo = enviados[-1] if enviados else (None, {})
v('Un solo POST a Usuario/con-roles (persona + cuenta + roles juntos)', recurso == 'Usuario/con-roles' and len(enviados) == 1)
v('La persona viaja recortada y los correos vacíos como null',
  cuerpo.get('nombres') == 'Ana' and cuerpo.get('correo_institucional') == 'ana@usbmed.edu.co'
  and cuerpo.get('correo_personal') is None and cuerpo.get('celular') is None)
v('La cuenta viaja con sus banderas', cuerpo.get('usuario') == 'ana.gomez' and cuerpo.get('activo') is True
  and cuerpo.get('debe_cambiar_clave') is True and cuerpo.get('clave') == 'Clave-Segura-1')
v('Las filas de rol vacías se descartan y la fecha vacía no se manda',
  cuerpo.get('roles') == [{'fk_rol': 'ADMIN'}, {'fk_rol': 'ASISTENTE', 'fecha_ini': '2026-02-01'}])

# Un error de la API: el formulario vuelve con TODO lo escrito (menos la clave) y sin crear nada.
fallar['ex'] = ErrorApi('La base de datos rechazó la operación.', 'uq_usuario_login')
r = admin().post('/admin/usuarios/nuevo', data=form)
t = r.get_data(as_text=True)
v('POST rechazado por la API -> vuelve el formulario (200), no redirige', r.status_code == 200 and 'name="nombres"' in t)
v('...conservando persona, cuenta y roles elegidos',
  'value="Ana"' in t and 'value="ana.gomez"' in t and 'value="ana@usbmed.edu.co"' in t
  and 'value="ADMIN" selected' in t and 'value="ASISTENTE" selected' in t and 'value="2026-02-01"' in t)
v('...pero NUNCA la clave', 'Clave-Segura-1' not in t)
fallar['ex'] = None

# Editar sigue igual: la persona dueña no se cambia y no hay bloque de roles.
R.obtener = lambda recurso, id_: {'id_usuario': 5, 'fk_asistente': 3, 'usuario': 'ana.gomez', 'activo': True}
r = admin().get('/admin/usuarios/5/editar')
t = r.get_data(as_text=True)
v('Editar -> 200, con la persona fija y sin bloque de roles',
  r.status_code == 200 and 'fk_asistente_actual' in t and 'id="roles"' not in t and 'name="nombres"' not in t)

print(f'\n{ok} pasadas, {fall} fallidas')
sys.exit(1 if fall else 0)
