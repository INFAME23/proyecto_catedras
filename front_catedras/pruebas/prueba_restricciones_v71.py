# Prueba v7.1 (D57), sin BD ni API: «Periodos y horarios de programación» del ADMIN, el aviso en los formularios de
# cátedra y sesión, el aviso en vivo de disponibilidad, el botón «Administración» sin sesión y el scroll del formulario
# «Soy ponente». La API se simula (monkeypatch de las funciones del cliente HTTP).
# Uso, desde front_catedras:   python pruebas/prueba_restricciones_v71.py
import os
import re
import sys

os.chdir(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
sys.path.insert(0, '.')
import werkzeug
werkzeug.__version__ = '3'

import app as A
from routes import admin as R_admin
from routes import admin_restricciones as R
from servicios.api_catedras import ErrorApi

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
            s['usuario'] = {'id_usuario': 7, 'usuario': 'admin.prueba', 'fk_asistente': 1}
            s['roles'] = roles
    return c


admin = lambda: cliente(['ADMIN'])

# ---------------------------------------------------------------- API simulada
VENTANAS = [
    {'id_restriccion': 1, 'nombre': 'Periodo 2026-2', 'fecha_desde': '2026-08-03', 'fecha_hasta': '2026-12-05', 'hora_desde': '06:00:00',
     'hora_hasta': '21:00:00', 'dias_semana': [1, 2, 3, 4, 5, 6], 'activo': True,
     'texto': '«Periodo 2026-2»: del 2026-08-03 al 2026-12-05, de 06:00 a 21:00, lun, mar, mié, jue, vie, sáb'},
    {'id_restriccion': 2, 'nombre': 'Intersemestral', 'fecha_desde': '2027-06-01', 'fecha_hasta': '2027-07-15', 'hora_desde': None,
     'hora_hasta': None, 'dias_semana': None, 'activo': False, 'texto': '«Intersemestral»: del 2027-06-01 al 2027-07-15'},
]
creadas, reemplazos, borradas, consultas = [], [], [], []
modo = {'crear_falla': None, 'validar': {'permitido': True}}


def listar_falso(recurso, limite=None, timeout=5, **f):
    if recurso == 'RestriccionProgramacion':
        return [dict(x) for x in VENTANAS if not f.get('soloActivas') or x['activo']]
    return []


def obtener_falso(recurso, id_valor):
    for x in VENTANAS:
        if x['id_restriccion'] == int(id_valor):
            return dict(x)
    raise ErrorApi('No encontrada', codigo=404)


def crear_falso(recurso, datos):
    creadas.append(datos)
    if modo['crear_falla']:
        raise modo['crear_falla']
    return {'estado': 'creado', 'id_restriccion': 9}


def reemplazar_falso(recurso, id_valor, datos):
    reemplazos.append((int(id_valor), datos))
    return {'estado': 'actualizado'}


def eliminar_falso(recurso, id_valor):
    borradas.append(int(id_valor))
    return {'estado': 'eliminado'}


def consultar_falso(ruta, **p):
    consultas.append((ruta, p))
    if ruta == 'RestriccionProgramacion/validar':
        if isinstance(modo['validar'], Exception):
            raise modo['validar']
        return modo['validar']
    if ruta == 'Disponibilidad/conflictos':
        return {'conflictos': []}
    raise ErrorApi('ruta no simulada: ' + ruta)


for nombre, f in (('listar', listar_falso), ('obtener', obtener_falso), ('crear', crear_falso), ('reemplazar', reemplazar_falso),
                  ('eliminar', eliminar_falso), ('consultar', consultar_falso)):
    setattr(R, nombre, f)
R_admin.consultar = consultar_falso

FORM = {'nombre': 'Periodo 2027-1', 'fecha_desde': '2027-02-01', 'fecha_hasta': '2027-06-05', 'hora_desde': '07:00',
        'hora_hasta': '20:00', 'dias_semana': ['1', '3', '5'], 'activo': 'on'}

# ---------------------------------------------------------------- acceso
print('\n== Acceso ==')
v('Sin sesión -> redirige al login', cliente().get('/admin/restricciones-programacion').status_code == 302)
v('Sin ser ADMIN -> 403 (lista, crear y borrar)', all(cliente(['ESTUDIANTE']).open(ruta, method=m).status_code == 403 for ruta, m in (
    ('/admin/restricciones-programacion', 'GET'), ('/admin/restricciones-programacion/nueva', 'POST'),
    ('/admin/restricciones-programacion/1/eliminar', 'POST'), ('/admin/restricciones-programacion/1/estado', 'POST'))))

# ---------------------------------------------------------------- panel
print('\n== Panel ==')
t = admin().get('/admin/').get_data(as_text=True)
grupo = re.search(r'<details class="admin-grupo">\s*<summary>.*?Estructura académica y cátedras</span><span class="badge badge-secondary">(\d+)</span>.*?</details>', t, re.S)
v('«Estructura académica y cátedras» ahora tiene 6 secciones', grupo is not None and grupo.group(1) == '6')
v('...y «Periodos y horarios» está DENTRO de ese grupo y enlaza a su lista',
  grupo is not None and 'Periodos y horarios' in grupo.group(0) and '/admin/restricciones-programacion' in grupo.group(0))

# ---------------------------------------------------------------- lista
print('\n== Lista ==')
r = admin().get('/admin/restricciones-programacion')
t = r.get_data(as_text=True)
v('La lista abre (200) con las dos ventanas', r.status_code == 200 and 'Periodo 2026-2' in t and 'Intersemestral' in t)
v('Muestra fechas, horas y días (abreviados) de la ventana 1', '2026-08-03 → 2026-12-05' in t and '06:00 – 21:00' in t and '>sáb<' in t)
v('La ventana sin horas ni días dice «Todo el día» y «Todos»', 'Todo el día' in t and '>Todos<' in t)
v('Marca activa y pausada', 'Activa' in t and 'Pausada' in t)
v('Con una ventana activa no avisa «Hoy no hay restricción»', 'aviso-sin-restriccion' not in t)
VENTANAS[0]['activo'] = False
t = admin().get('/admin/restricciones-programacion').get_data(as_text=True)
v('Sin ninguna activa avisa que hoy no hay restricción', 'aviso-sin-restriccion' in t and 'cualquier fecha y hora' in t)
VENTANAS[0]['activo'] = True
v('El nombre con comillas va escapado en el confirm (data-confirmar)', 'data-confirmar="¿Eliminar la ventana «Periodo 2026-2»?' in t)

# ---------------------------------------------------------------- crear / editar
print('\n== Crear y editar ==')
t = admin().get('/admin/restricciones-programacion/nueva').get_data(as_text=True)
v('El formulario trae nombre, fechas, horas, los 7 días y el interruptor de activa',
  all(f'name="{c}"' in t for c in ('nombre', 'fecha_desde', 'fecha_hasta', 'hora_desde', 'hora_hasta', 'activo')) and t.count('name="dias_semana"') == 7)
creadas.clear()
r = admin().post('/admin/restricciones-programacion/nueva', data=FORM)
v('Crear -> 302 a la lista y UNA llamada a la API', r.status_code == 302 and len(creadas) == 1)
d = creadas[0] if creadas else {}
v('Manda nombre, fechas, horas HH:mm, días como enteros ordenados y activo=true',
  d.get('nombre') == 'Periodo 2027-1' and d.get('fecha_desde') == '2027-02-01' and d.get('hora_desde') == '07:00'
  and d.get('hora_hasta') == '20:00' and d.get('dias_semana') == [1, 3, 5] and d.get('activo') is True)
creadas.clear()
admin().post('/admin/restricciones-programacion/nueva', data={'nombre': 'Solo fechas', 'fecha_desde': '2027-02-01', 'fecha_hasta': '2027-03-01'})
d = creadas[0]
v('Sin horas, sin días y sin marcar «activa»: horas null, días null, activo=false',
  d['hora_desde'] is None and d['hora_hasta'] is None and d['dias_semana'] is None and d['activo'] is False)

modo['crear_falla'] = ErrorApi('El cuerpo no cumple', codigo=422, errores=[
    {'campo': 'nombre', 'mensaje': "El campo 'nombre' es obligatorio."}, {'campo': 'fecha_hasta', 'mensaje': 'Falta la fecha final'}])
r = admin().post('/admin/restricciones-programacion/nueva', data={**FORM, 'nombre': ''})
t = r.get_data(as_text=True)
v('Error 422 de la API -> 422 con cada mensaje junto a su campo', r.status_code == 422 and 'Falta la fecha final' in t and t.count('is-invalid') >= 2)
v('...conserva lo escrito (fechas, horas y días marcados)', 'value="2027-02-01"' in t and 'value="07:00"' in t
  and re.search(r'id="dia3"[^>]*checked', t) is not None and re.search(r'id="dia2"[^>]*checked', t) is None)
modo['crear_falla'] = ErrorApi('Petición inválida.', detalle="'hora_hasta' debe ser posterior a 'hora_desde'.", codigo=400)
r = admin().post('/admin/restricciones-programacion/nueva', data=FORM)
t = r.get_data(as_text=True)
v('Error 400 (regla cruzada) -> 400 y el motivo en un aviso rojo', r.status_code == 400 and 'debe ser posterior' in t and 'alert-danger' in t)
modo['crear_falla'] = None

t = admin().get('/admin/restricciones-programacion/1/editar').get_data(as_text=True)
v('Editar abre con los datos de la ventana (hora recortada a HH:mm y días marcados)',
  'value="Periodo 2026-2"' in t and 'value="06:00"' in t and 'value="21:00"' in t
  and re.search(r'id="dia6"[^>]*checked', t) is not None and re.search(r'id="dia7"[^>]*checked', t) is None and re.search(r'id="activo"[^>]*checked', t) is not None)
v('Editar una ventana inexistente -> 404', admin().get('/admin/restricciones-programacion/99/editar').status_code == 404)
reemplazos.clear()
r = admin().post('/admin/restricciones-programacion/1/editar', data=FORM)
v('Guardar la edición -> PUT con el id y los datos', r.status_code == 302 and reemplazos and reemplazos[0][0] == 1 and reemplazos[0][1]['nombre'] == 'Periodo 2027-1')

# ---------------------------------------------------------------- activar / pausar / eliminar
print('\n== Activar, pausar y eliminar ==')
reemplazos.clear()
r = admin().post('/admin/restricciones-programacion/1/estado')
(i, d), = reemplazos
v('Pausar una activa: PUT con activo=false y el resto igual (horas HH:mm, días, fechas)',
  r.status_code == 302 and i == 1 and d['activo'] is False and d['hora_desde'] == '06:00' and d['hora_hasta'] == '21:00'
  and d['dias_semana'] == [1, 2, 3, 4, 5, 6] and d['fecha_desde'] == '2026-08-03' and d['nombre'] == 'Periodo 2026-2')
reemplazos.clear()
admin().post('/admin/restricciones-programacion/2/estado')
(i, d), = reemplazos
v('Activar una pausada: PUT con activo=true y horas/días null', d['activo'] is True and d['hora_desde'] is None and d['dias_semana'] is None)
t = admin().post('/admin/restricciones-programacion/2/estado', follow_redirects=True).get_data(as_text=True)
v('Avisa qué pasó', 'activada: ahora limita la programación' in t or 'pausada: ya no limita' in t)
borradas.clear()
r = admin().post('/admin/restricciones-programacion/2/eliminar')
v('Eliminar -> DELETE y vuelve a la lista', r.status_code == 302 and borradas == [2])

# ---------------------------------------------------------------- avisos en los formularios
print('\n== Avisos en los formularios de cátedra y sesión ==')
t = admin().get('/admin/catedras/nueva').get_data(as_text=True)
v('«Nueva cátedra» muestra el aviso con las ventanas ACTIVAS', 'id="ventanas-programacion"' in t and 'Periodo 2026-2' in t
  and 'Solo se pueden programar sesiones en' in t and 'Intersemestral' not in t)
v('...fijo en la página (no es un flash que se cierre solo)', 'alert-dismissible' not in t.split('id="ventanas-programacion"')[0][-200:])
r = admin().get('/admin/sesiones/nueva?fk_catedra=1')
t = r.get_data(as_text=True)
v('«Nueva sesión» también lo muestra', r.status_code in (200, 502) and 'id="ventanas-programacion"' in t)
VENTANAS[0]['activo'] = False
VENTANAS[1]['activo'] = False
t = admin().get('/admin/catedras/nueva').get_data(as_text=True)
v('Sin ventanas activas no hay aviso', 'id="ventanas-programacion"' not in t)
VENTANAS[0]['activo'] = True

# ---------------------------------------------------------------- aviso en vivo
print('\n== Disponibilidad en vivo ==')
modo['validar'] = {'permitido': False, 'hay_restricciones': True, 'mensaje': 'Ese horario está fuera de los periodos. Se puede programar en: X.'}
consultas.clear()
r = admin().get('/admin/catedras/disponibilidad', query_string={'inicio': '2027-02-02T10:00', 'fin': '2027-02-02T12:00'})
j = r.get_json()
v('Fuera de periodo: disponible=false y el texto en fuera_de_periodo', r.status_code == 200 and j['disponible'] is False and 'fuera de los periodos' in j['fuera_de_periodo'])
v('...preguntó a la API con las fechas con zona de Colombia', ('RestriccionProgramacion/validar', {'inicio': '2027-02-02T10:00:00-05:00', 'fin': '2027-02-02T12:00:00-05:00'}) in consultas)
modo['validar'] = {'permitido': True, 'hay_restricciones': True, 'mensaje': None}
j = admin().get('/admin/catedras/disponibilidad', query_string={'inicio': '2026-09-02T10:00', 'fin': '2026-09-02T12:00'}).get_json()
v('Dentro del periodo y sin cruces: disponible=true', j['disponible'] is True and j['fuera_de_periodo'] is None)
modo['validar'] = ErrorApi('API caída')
j = admin().get('/admin/catedras/disponibilidad', query_string={'inicio': '2026-09-02T10:00', 'fin': '2026-09-02T12:00'}).get_json()
v('Si no se puede consultar el periodo, no bloquea el aviso (la base de datos decide al guardar)', j['disponible'] is True and j['fuera_de_periodo'] is None)
js = open('static/disponibilidad.js', encoding='utf-8').read()
v('disponibilidad.js pinta el motivo de fuera_de_periodo', 'fuera_de_periodo' in js)

# ---------------------------------------------------------------- navbar y formulario
print('\n== Botón «Administración» y scroll del formulario ==')
t = cliente().get('/').get_data(as_text=True)
barra = t.split('<main')[0]
v('Sin sesión el botón de la barra dice solo «Administración»', re.search(r'<a class="btn btn-outline-primary btn-sm" href="/login">\s*<i class="fas fa-cogs me-1"></i>Administración\s*</a>', barra) is not None)
v('...y ya no dice «Iniciar sesión» en la barra', 'Iniciar sesión' not in barra)
t = admin().get('/').get_data(as_text=True).split('<main')[0]
v('Con sesión la barra sigue igual (usuario, cambiar clave, cerrar sesión)', 'Cerrar sesión' in t and 'admin.prueba' in t)
css = open('static/estilo.css', encoding='utf-8').read()
v('estilo.css: el <form> del modal es una columna flexible y su cuerpo hace scroll (el cuerpo ya no queda recortado)',
  '.modal-dialog-scrollable .modal-content > form' in css and 'overflow-y: auto' in css and 'flex-direction: column' in css)
t = cliente().get('/calendario').get_data(as_text=True)
v('El formulario «Soy ponente» está en un modal con scroll y su cuerpo llega hasta el último campo',
  'modal-dialog modal-lg modal-dialog-scrollable' in t and '<div class="modal-body">' in t and 'name="acepta_datos"' in t)

print(f'\n{ok} pasadas, {fall} fallidas')
sys.exit(1 if fall else 0)
