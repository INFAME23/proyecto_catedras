# Prueba v7.1 (D56 · D58), sin BD ni API: el calendario público, el formulario «Soy ponente» (cátedra nueva con fecha y
# hora) y la bandeja del ADMIN, donde aprobar crea la cátedra. La API se simula (monkeypatch del cliente HTTP).
# Uso, desde front_catedras:   python pruebas/prueba_calendario_ponente_v71.py
import os
import re
import sys
from datetime import datetime, timedelta, timezone

os.chdir(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
sys.path.insert(0, '.')
import werkzeug
werkzeug.__version__ = '3'

import app as A
from routes import admin as R_admin
from routes import admin_solicitudes as R_sol
from routes import calendario as R_cal
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


# ---------------------------------------------------------------- API simulada
AHORA = datetime.now(timezone.utc)
llamadas_agenda = []
creadas = []
resueltas = []
qr_pedidos = []
modo = {'crear_falla': None, 'agenda_falla': False, 'choques': [], 'conflictos_falla': False, 'sedes': 2}

TIPOS = [{'id_tipo_documento': i, 'nombre': n} for i, n in
         [('CC', 'Cedula de ciudadania'), ('TI', 'Tarjeta de identidad'), ('PA', 'Pasaporte'),
          ('LC', 'Licencia de conduccion'), ('CI', 'Codigo institucional'), ('NIT', 'NIT')]]
VENTANAS = [{'id_restriccion': 1, 'nombre': 'Periodo 2026-2', 'activo': True,
             'texto': '«Periodo 2026-2»: del 2026-08-03 al 2027-12-31, de 06:00 a 21:00, lun, mar, mié, jue, vie, sáb'}]
CATALOGOS = {
    'TipoEvento': [{'id_tipo_evento': 'CAAB', 'nombre': 'Catedra abierta'}, {'id_tipo_evento': 'DLLH', 'nombre': 'Desarrollo humano'}],
    'Modalidad': [{'id_modalidad': 'PRESENCIAL', 'nombre': 'Presencial'}, {'id_modalidad': 'VIRTUAL', 'nombre': 'Virtual'}],
    'PeriodoAcademico': [{'id_periodo': '2026-2', 'fecha_ini': '2026-08-03', 'fecha_fin': '2027-12-31', 'activo': True},
                         {'id_periodo': '2025-2', 'fecha_ini': '2025-07-21', 'fecha_fin': '2025-12-06', 'activo': True},       # ya terminó
                         {'id_periodo': '2028-1', 'fecha_ini': '2028-01-10', 'fecha_fin': '2028-06-10', 'activo': False}],    # inactivo
}


def sedes():
    todas = [{'id_sede': 'MED', 'nombre': 'Sede Medellin', 'activo': True}, {'id_sede': 'BEL', 'nombre': 'Sede Bello', 'activo': True},
             {'id_sede': 'OLD', 'nombre': 'Sede cerrada', 'activo': False}]
    return todas[:modo['sedes']] if modo['sedes'] == 1 else todas


def sesion(id_sesion, dias, nombre, modalidad='PRESENCIAL'):
    ini = AHORA + timedelta(days=dias)
    return {'id_sesion': id_sesion, 'fk_catedra': 100 + id_sesion, 'nombre_catedra': nombre, 'numero_reunion': 1,
            'inicio': ini.strftime('%Y-%m-%dT%H:%M:%SZ'), 'fin': (ini + timedelta(hours=2)).strftime('%Y-%m-%dT%H:%M:%SZ'),
            'fk_sede': 'MED', 'nombre_sede': 'Sede Medellin', 'lugar': 'Aula 101', 'fk_modalidad': modalidad,
            'estado': 'PROGRAMADA'}


SESIONES = [sesion(1, 3, 'Catedra de Ciencia'), sesion(2, 10, 'Catedra de Arte', 'VIRTUAL'), sesion(3, -5, 'Catedra pasada')]
PROPUESTA_INI = (AHORA + timedelta(days=10)).replace(hour=19, minute=0, second=0, microsecond=0)     # 14:00 en Colombia
PROPUESTA_FIN = PROPUESTA_INI + timedelta(hours=2)
iso = lambda d: d.strftime('%Y-%m-%dT%H:%M:%SZ')


def solicitud(id_s, estado='PENDIENTE', nueva=True, **extra):
    base = {'id_solicitud': id_s, 'nombres': 'Ana', 'apellidos': "O'Brien", 'nombre_completo': "Ana O'Brien",
            'fk_tipo_documento': 'CC', 'nombre_tipo_documento': 'Cedula de ciudadania', 'numero_documento': '12345678',
            'perfil_profesional': 'Ingeniera', 'direccion': 'Calle 1 # 2-3', 'telefono': '3001234567',
            'correo_institucional': 'ana@usbmed.edu.co', 'correo_es_institucional': True,
            'titulo_ponencia': 'IA y educacion', 'resumen_ponencia': 'Linea uno\nLinea dos ' + 'x' * 40,
            'inicio_propuesto': iso(PROPUESTA_INI) if nueva else None, 'fin_propuesto': iso(PROPUESTA_FIN) if nueva else None,
            'fk_modalidad': 'VIRTUAL' if nueva else None, 'nombre_modalidad': 'Virtual' if nueva else None,
            'fk_sede': 'BEL' if nueva else None, 'nombre_sede': 'Sede Bello' if nueva else None, 'fk_periodo': '2026-2' if nueva else None,
            'fk_sesion': None if nueva else 1, 'fk_catedra': None, 'nombre_catedra': None if nueva else 'Catedra de Ciencia',
            'numero_reunion': None if nueva else 1, 'inicio_sesion': None if nueva else SESIONES[0]['inicio'], 'estado': estado,
            'observacion_admin': None, 'fk_asistente': None, 'resuelto_por': None, 'resuelto_en': None, 'creado_en': iso(AHORA)}
    base.update(extra)
    return base


BANDEJA = [
    solicitud(1),                                                                           # pendiente: cátedra nueva con fecha
    solicitud(2, 'APROBADA', fk_asistente=9, resuelto_en=iso(AHORA), fk_catedra=50, fk_sesion=500,
              nombre_catedra='IA y educacion', numero_reunion=1, inicio_sesion=iso(PROPUESTA_INI)),
    solicitud(3, 'RECHAZADA', observacion_admin='No encaja', resuelto_en=iso(AHORA)),
    solicitud(4, nueva=False),                                                              # pendiente anterior a la 057 (eligió sesión)
]


def consultar_falso(ruta, **p):
    llamadas_agenda.append((ruta, p))
    if modo['agenda_falla']:
        raise ErrorApi('No se pudo contactar la API', detalle='caida')
    desde = datetime.fromisoformat(p['desde'])
    hasta = datetime.fromisoformat(p['hasta'])
    assert hasta - desde <= timedelta(days=31), 'la agenda de la API no acepta mas de 31 dias'
    datos = [s for s in SESIONES if desde < datetime.fromisoformat(s['fin'].replace('Z', '+00:00')) and
             hasta > datetime.fromisoformat(s['inicio'].replace('Z', '+00:00'))]
    return {'total': len(datos), 'datos': datos}


def listar_falso(recurso, limite=None, timeout=5, **f):
    if recurso == 'TipoDocumento':
        return TIPOS
    if recurso == 'SolicitudPonente':
        est = f.get('estado')
        return [s for s in BANDEJA if est is None or s['estado'] == est]
    if recurso == 'Sede':
        return sedes()
    return CATALOGOS.get(recurso, [])


def crear_falso(recurso, datos):
    creadas.append((recurso, datos))
    if modo['crear_falla']:
        raise modo['crear_falla']
    return {'estado': 'creado', 'id_solicitud': 55, 'solicitud': {}}


def obtener_falso(recurso, id_valor):
    if recurso == 'SolicitudPonente':
        for s in BANDEJA:
            if s['id_solicitud'] == int(id_valor):
                return s
        raise ErrorApi('No encontrada', codigo=404)
    raise ErrorApi('?', codigo=404)


def parcial_falso(recurso, id_valor, datos):
    resueltas.append((recurso, id_valor, datos))
    return {'estado': 'aprobada', 'mensaje': 'Solicitud resuelta (simulada).', 'solicitud': {'fk_catedra': 77 if datos.get('aprobar') and datos.get('fk_sede') else None}}


def conflictos_falso(inicio, fin, sede=None, lugar=None):
    if modo['conflictos_falla']:
        raise ErrorApi('No se pudo contactar la API', detalle='caida')
    conflictos_pedidos.append((inicio, fin, sede, lugar))
    return modo['choques']


conflictos_pedidos = []
R_cal.consultar = consultar_falso
R_cal.listar = listar_falso
R_cal.crear = crear_falso
R_cal.ventanas_activas = lambda: VENTANAS
R_sol.listar = listar_falso
R_sol.obtener = obtener_falso
R_sol.actualizar_parcial = parcial_falso
R_sol.asegurar_qr_de_catedra = lambda id_catedra, id_usuario=None: (qr_pedidos.append((id_catedra, id_usuario)) or (1, 1))
R_admin._conflictos_de_horario = conflictos_falso

FORM = {'nombres': 'Ana', 'apellidos': 'Perez', 'fk_tipo_documento': 'PA', 'numero_documento': 'AB123456',
        'perfil_profesional': 'Ingeniera de sistemas', 'direccion': 'Calle 10 # 20-30', 'telefono': '+57 300 123 4567',
        'correo_institucional': 'ana@usbmed.edu.co', 'titulo_ponencia': 'IA en el aula',
        'resumen_ponencia': 'Un resumen con mas de treinta caracteres para pasar la regla.',
        'inicio': '2026-12-01T14:00', 'fin': '2026-12-01T16:00', 'fk_modalidad': 'PRESENCIAL', 'fk_sede': 'MED', 'fk_periodo': '2026-2',
        'acepta_datos': '1', 'sitio_web': ''}

# ------------------------------------------------------------ página pública
print('\n== Calendario público ==')
r = cliente().get('/calendario')
t = r.get_data(as_text=True)
v('GET /calendario sin sesión -> 200 (es público)', r.status_code == 200)
v('Tiene el botón «Soy ponente»', 'Soy ponente' in t and 'id="btn-soy-ponente"' in t)
v('El navbar tiene «Calendario» y queda activo en esa página',
  'href="/calendario"' in t and 'fa-calendar-alt"></i> Calendario' in t)
v('El navbar de la portada también lo muestra', 'Calendario' in cliente().get('/').get_data(as_text=True))
v('Carga FullCalendar + su idioma + calendario.js', 'fullcalendar@6.1.15/index.global.min.js' in t and 'es.global.min.js' in t
  and 'calendario.js' in t)
v('El contenedor apunta a /calendario/eventos', 'data-eventos="/calendario/eventos"' in t)
for campo in ('nombres', 'apellidos', 'fk_tipo_documento', 'numero_documento', 'perfil_profesional', 'direccion', 'telefono',
              'correo_institucional', 'titulo_ponencia', 'resumen_ponencia', 'inicio', 'fin', 'fk_modalidad', 'fk_sede', 'fk_periodo'):
    v(f'El formulario tiene el campo «{campo}»', f'name="{campo}"' in t)
v('Tipos de documento: solo cédula, pasaporte, licencia y código institucional (no TI ni NIT)',
  'value="CC"' in t and 'value="PA"' in t and 'value="LC"' in t and 'value="CI"' in t
  and 'value="TI"' not in t and 'value="NIT"' not in t)
v('Los nombres salen del catálogo', 'Licencia de conduccion' in t and 'Codigo institucional' in t)
# D58: ya no se elige una sesión que existe
v('«Sesión a la que te postulas» ya no está: no hay <select> de sesiones ni campo fk_sesion',
  'Sesión a la que te postulas' not in t and 'name="fk_sesion"' not in t and 'Catedra de Ciencia' not in t.split('id="modal-ponente"')[1])
v('El formulario habla de «Cátedra nueva» y de que se crea sola al aprobar', 'Cátedra nueva' in t and 'se crea automáticamente en ese horario' in t)
v('Elige la fecha y la hora con <input type="datetime-local"> de inicio y de fin',
  len(re.findall(r'type="datetime-local"[^>]*name="(inicio|fin)"', t)) == 2)
v('El ponente elige modalidad, sede y periodo de <select> del catálogo (Regla 2)',
  all(f'name="{c}"' in t and re.search(rf'<select[^>]*name="{c}"[^>]*required', t) for c in ('fk_modalidad', 'fk_sede', 'fk_periodo')))
v('...modalidades: presencial y virtual; sedes: solo las activas (no «Sede cerrada»)',
  'value="PRESENCIAL"' in t and 'value="VIRTUAL"' in t and 'value="MED"' in t and 'value="BEL"' in t and 'value="OLD"' not in t and 'Sede cerrada' not in t)
v('...periodos: solo los activos que no han terminado (no el 2025-2 ni el 2028-1 inactivo), con sus fechas para elegirlo solo',
  'value="2026-2" data-ini="2026-08-03" data-fin="2027-12-31"' in t and 'value="2025-2"' not in t and 'value="2028-1"' not in t)
v('Muestra los periodos en que se pueden programar cátedras (D57), si el admin los definió',
  'id="ventanas-programacion"' in t and 'Periodo 2026-2' in t)
R_cal.ventanas_activas = lambda: []
v('...y no los muestra si no hay ninguno', 'id="ventanas-programacion"' not in cliente().get('/calendario').get_data(as_text=True))
R_cal.ventanas_activas = lambda: VENTANAS
v('El detalle de una sesión ya no ofrece «Soy ponente de esta sesión»', 'Soy ponente de esta sesión' not in t and 'btn-ponente-en-sesion' not in t)
js = open('static/calendario.js', encoding='utf-8').read()
v('calendario.js ya no manipula el <select> de sesiones, limita la fecha mínima y elige solo el periodo que cubre la fecha',
  'f_fk_sesion' not in js and 'btn-ponente-en-sesion' not in js and 'campoInicio.min' in js and 'f_fk_periodo' in js and 'o.dataset.ini' in js)
v('Trae trampa antispam y consentimiento', 'name="sitio_web"' in t and 'name="acepta_datos"' in t)
v('El formulario apunta a /calendario/ponente por POST', 'action="/calendario/ponente"' in t)

# ------------------------------------------------------------ eventos JSON
print('\n== /calendario/eventos ==')
llamadas_agenda.clear()
ini = (AHORA - timedelta(days=10)).strftime('%Y-%m-%dT00:00:00Z')
fin = (AHORA + timedelta(days=32)).strftime('%Y-%m-%dT00:00:00Z')    # 42 días: la cuadrícula de un mes
r = cliente().get('/calendario/eventos', query_string={'desde': ini, 'hasta': fin})
ev = r.get_json()
v('42 días -> 200', r.status_code == 200)
v('La agenda se pidió en 2 tramos de <= 31 días (la API no acepta más)', len(llamadas_agenda) == 2)
v('Devuelve las 3 sesiones del rango, sin repetir', sorted(e['id'] for e in ev) == [1, 2, 3])
e1 = next(e for e in ev if e['id'] == 1)
v('Hora de pared de Colombia sin zona (UTC-5)', e1['start'] == (datetime.fromisoformat(SESIONES[0]['inicio'].replace('Z', '+00:00'))
                                                                  - timedelta(hours=5)).strftime('%Y-%m-%dT%H:%M:%S'))
v('Trae título, color y clase de modalidad y datos para el detalle',
  e1['title'] == 'Catedra de Ciencia' and 'mod-presencial' in e1['classNames'] and e1['color'] == '#EF7D00'
  and e1['extendedProps']['sede'] == 'Sede Medellin' and e1['extendedProps']['url'] == '/sesiones/1')
v('Sin parámetros -> 400', cliente().get('/calendario/eventos').status_code == 400)
v('Rango invertido -> 400', cliente().get('/calendario/eventos', query_string={'desde': fin, 'hasta': ini}).status_code == 400)
v('Más de 95 días -> 400', cliente().get('/calendario/eventos', query_string={
    'desde': ini, 'hasta': (AHORA + timedelta(days=200)).strftime('%Y-%m-%dT00:00:00Z')}).status_code == 400)
modo['agenda_falla'] = True
r = cliente().get('/calendario/eventos', query_string={'desde': ini, 'hasta': fin})
v('Si la API falla -> 502 con el mensaje (no se inventan sesiones)', r.status_code == 502 and 'No se pudo contactar' in r.get_json()['mensaje'])
modo['agenda_falla'] = False

# ------------------------------------------------------------ enviar la solicitud
print('\n== POST /calendario/ponente ==')
creadas.clear()
r = cliente().post('/calendario/ponente', data=FORM)
v('Solicitud válida -> 302 de vuelta al calendario', r.status_code == 302 and r.headers['Location'].endswith('/calendario'))
v('Se llamó a la API UNA vez, a SolicitudPonente', len(creadas) == 1 and creadas[0][0] == 'SolicitudPonente')
datos = creadas[0][1] if creadas else {}
v('Manda los campos con los nombres de la API', all(datos.get(k) == FORM[k] for k in (
    'nombres', 'apellidos', 'fk_tipo_documento', 'numero_documento', 'perfil_profesional', 'direccion', 'telefono',
    'correo_institucional', 'titulo_ponencia', 'resumen_ponencia')))
v('Manda la modalidad, la sede y el periodo elegidos', datos.get('fk_modalidad') == 'PRESENCIAL' and datos.get('fk_sede') == 'MED' and datos.get('fk_periodo') == '2026-2')
v('La fecha y la hora viajan con la zona de Colombia (-05:00)', datos.get('inicio') == '2026-12-01T14:00:00-05:00'
  and datos.get('fin') == '2026-12-01T16:00:00-05:00')
v('No manda fk_sesion, ni el campo trampa ni el consentimiento a la API', all(k not in datos for k in ('fk_sesion', 'sitio_web', 'acepta_datos')))
t = cliente().post('/calendario/ponente', data=FORM, follow_redirects=True).get_data(as_text=True)
v('Muestra el aviso de éxito con el número y dice que la cátedra se crea al aprobar',
  'Recibimos tu solicitud (n.º 55)' in t and 'tu cátedra se crea en la fecha y la hora que elegiste' in t)

creadas.clear()
r = cliente().post('/calendario/ponente', data={**FORM, 'inicio': '', 'fin': ''})
v('Sin fecha ni hora se manda null (la API responde 422 y el formulario lo muestra)', creadas and creadas[0][1]['inicio'] is None and creadas[0][1]['fin'] is None)

creadas.clear()
r = cliente().post('/calendario/ponente', data={**FORM, 'sitio_web': 'http://spam'})
v('Campo trampa lleno -> parece éxito pero NO se llama a la API', r.status_code == 302 and not creadas)

r = cliente().post('/calendario/ponente', data={k: v_ for k, v_ in FORM.items() if k != 'acepta_datos'})
v('Sin autorizar los datos -> 422, no se llama a la API, el formulario se reabre con lo escrito',
  r.status_code == 422 and not creadas and 'data-abrir-formulario="1"' in r.get_data(as_text=True)
  and 'value="AB123456"' in r.get_data(as_text=True) and 'value="2026-12-01T14:00"' in r.get_data(as_text=True)
  and 'autorizar el tratamiento' in r.get_data(as_text=True))

modo['crear_falla'] = ErrorApi('El cuerpo no cumple', codigo=422, errores=[
    {'campo': 'telefono', 'mensaje': "El campo 'telefono' debe tener de 7 a 20 caracteres."},
    {'campo': 'inicio', 'mensaje': 'Falta la fecha y la hora de inicio.'},
    {'campo': 'fk_periodo', 'mensaje': 'Falta el periodo.'}])
r = cliente().post('/calendario/ponente', data={**FORM, 'telefono': '12'})
t = r.get_data(as_text=True)
v('La API responde 422 -> 422, cada error junto a su campo (también el de la fecha)', r.status_code == 422
  and "debe tener de 7 a 20 caracteres" in t and 'Falta la fecha y la hora de inicio.' in t and 'Falta el periodo.' in t and t.count('is-invalid') >= 3)
v('...conserva lo escrito y reabre el formulario', 'value="12"' in t and 'value="AB123456"' in t and 'data-abrir-formulario="1"' in t)

modo['crear_falla'] = ErrorApi('La fecha y la hora que propones ya pasaron: elige una futura.', codigo=409)
r = cliente().post('/calendario/ponente', data=FORM)
t = r.get_data(as_text=True)
v('Fecha pasada o fuera de periodo (409) -> se muestra el motivo en un aviso rojo y se conserva todo',
  r.status_code == 409 and 'ya pasaron' in t and 'alert alert-danger' in t and 'value="2026-12-01T16:00"' in t)
modo['crear_falla'] = ErrorApi('La fecha de la cátedra (2026-12-01 14:00 a 16:00) no cae dentro del periodo 2026-2.', codigo=409)
r = cliente().post('/calendario/ponente', data=FORM)
t = r.get_data(as_text=True)
v('La fecha fuera del periodo elegido (409) -> motivo en un aviso y se conserva la modalidad, la sede y el periodo elegidos',
  r.status_code == 409 and 'no cae dentro del periodo 2026-2' in t and re.search(r'<option value="PRESENCIAL" selected', t)
  and re.search(r'<option value="MED" selected', t) and re.search(r'<option value="2026-2" data-ini="[^"]*" data-fin="[^"]*" selected', t))
modo['crear_falla'] = ErrorApi('Ya hay una solicitud pendiente de este documento con la misma ponencia.', codigo=409)
r = cliente().post('/calendario/ponente', data=FORM)
v('Ya hay una pendiente igual (409) -> aviso general', r.status_code == 409 and 'Ya hay una solicitud pendiente' in r.get_data(as_text=True))
modo['crear_falla'] = ErrorApi('Petición inválida.', detalle='La hora de fin debe ser posterior a la de inicio.', codigo=400)
r = cliente().post('/calendario/ponente', data=FORM)
v('Fin antes del inicio (400) -> 400 y el motivo', r.status_code == 400 and 'posterior a la de inicio' in r.get_data(as_text=True))
modo['crear_falla'] = ErrorApi('No se pudo contactar la API', detalle='caida')
r = cliente().post('/calendario/ponente', data=FORM)
v('Si la API no responde -> 502 y aviso, sin perder lo escrito', r.status_code == 502 and 'value="AB123456"' in r.get_data(as_text=True))
modo['crear_falla'] = None
v('Cuando hay errores, calendario.js lleva a la vista el primer aviso (el cuerpo del formulario hace scroll)',
  'scrollIntoView' in js and "'.alert-danger, .is-invalid'" in js)

# ------------------------------------------------------------ bandeja del ADMIN
print('\n== Bandeja del administrador ==')
r = cliente().get('/admin/solicitudes-ponente')
v('Sin sesión -> redirige al login', r.status_code == 302 and '/login' in r.headers['Location'])
v('Con sesión pero sin ADMIN -> 403', cliente(['ESTUDIANTE']).get('/admin/solicitudes-ponente').status_code == 403)
v('Resolver sin ser ADMIN -> 403 y no se llama a la API',
  cliente(['ESTUDIANTE']).post('/admin/solicitudes-ponente/1/resolver', data={'decision': 'aprobar'}).status_code == 403 and not resueltas)

admin = lambda: cliente(['ADMIN'])
r = admin().get('/admin/solicitudes-ponente')
t = r.get_data(as_text=True)
v('ADMIN: la lista abre en «Pendientes»', r.status_code == 200 and 'Bandeja de ponentes' in t and 'IA y educacion' in t)
v('...muestra las 2 pendientes (la nueva y la anterior), no la aprobada ni la rechazada', t.count('<tr>') == 3 and t.count('></i>Revisar') == 2
  and "Ana O&#39;Brien" in t)
v('...la pendiente nueva dice «Cátedra nueva» con su fecha propuesta; la anterior, su sesión',
  'Cátedra nueva' in t and (PROPUESTA_INI - timedelta(hours=5)).strftime('%Y-%m-%d %H:%M') in t and 'Catedra de Ciencia' in t)
v('...con las pestañas y sus conteos', 'Pendientes <span class="badge bg-light text-dark ms-1">2</span>' in t
  and 'Todas <span class="badge bg-light text-dark ms-1">4</span>' in t)
t = admin().get('/admin/solicitudes-ponente?estado=todas').get_data(as_text=True)
v('?estado=todas muestra las 4', t.count('<tr>') == 5 and 'Aprobada' in t and 'Rechazada' in t)
v('Estado desconocido -> cae en pendientes', 'Revisar' in admin().get('/admin/solicitudes-ponente?estado=xx').get_data(as_text=True))

print('\n== Detalle: la cátedra que se creará al aprobar ==')
r = admin().get('/admin/solicitudes-ponente/1')
t = r.get_data(as_text=True)
v('Detalle de una pendiente -> 200 con todos los datos', r.status_code == 200 and '12345678' in t and 'Ingeniera' in t
  and 'Calle 1 # 2-3' in t and '3001234567' in t and 'ana@usbmed.edu.co' in t and 'IA y educacion' in t)
v('Dice qué fecha y hora propone para su cátedra nueva (hora de Colombia)',
  'Cátedra nueva' in t and (PROPUESTA_INI - timedelta(hours=5)).strftime('%Y-%m-%d') in t and '14:00' in t and '16:00' in t)
v('Tiene la tarjeta «Cátedra que se creará al aprobar»', 'id="catedra-a-crear"' in t)
campos = ('nombre_catedra', 'inicio', 'fin', 'fk_modalidad', 'fk_sede', 'lugar', 'fk_periodo', 'fk_tipo_evento', 'id_evento_asis',
          'enlace_virtual')
v('...con los 10 campos (incluido el enlace de ingreso), todos ligados al formulario de decisión (form="form-decision")',
  all(re.search(rf'name="{c}"[^>]*form="form-decision"', t) for c in campos))
v('...el enlace de ingreso es un campo url, opcional, y la tarjeta avisa de la hora de margen entre cátedras',
  re.search(r'type="url"[^>]*name="enlace_virtual"', t) is not None and 'Enlace de ingreso' in t and 'al menos una hora' in t)
v('...sugiere: nombre = título de la ponencia, fecha y hora propuestas y tipo «Cátedra abierta»',
  'value="IA y educacion"' in t and f'value="{(PROPUESTA_INI - timedelta(hours=5)).strftime("%Y-%m-%dT%H:%M")}"' in t
  and re.search(r'<option value="CAAB" selected', t))
v('...y trae ya elegidos la modalidad (Virtual), la sede (Bello) y el periodo (2026-2) que eligió el PONENTE',
  re.search(r'<option value="VIRTUAL" selected', t) and re.search(r'<option value="BEL" selected', t)
  and re.search(r'<option value="2026-2" selected', t) and not re.search(r'<option value="PRESENCIAL" selected', t))
v('...y el detalle dice qué eligió el ponente', 'Eligió: modalidad' in t and 'Virtual' in t and 'Sede Bello' in t and '2026-2' in t)
v('...la sede inactiva no se ofrece', 'value="OLD"' not in t and 'value="MED"' in t and 'value="BEL"' in t)
# una solicitud que no trae lo que eligió el ponente (anterior a la 058): valores sugeridos
for campo in ('fk_modalidad', 'fk_sede', 'fk_periodo'):
    BANDEJA[0][campo] = None
t1 = admin().get('/admin/solicitudes-ponente/1').get_data(as_text=True)
v('Sin lo que eligió el ponente (solicitud anterior a la 058): modalidad presencial sugerida, el periodo que cubre la fecha y la sede sin adivinar',
  re.search(r'<option value="PRESENCIAL" selected', t1) and re.search(r'<option value="2026-2" selected', t1)
  and not re.search(r'<option value="(MED|BEL)" selected', t1) and 'Eligió: modalidad' not in t1)
modo['sedes'] = 1
t1 = admin().get('/admin/solicitudes-ponente/1').get_data(as_text=True)
v('...con una sola sede activa ya viene elegida', re.search(r'<option value="MED" selected', t1) is not None)
modo['sedes'] = 2
BANDEJA[0]['fk_modalidad'], BANDEJA[0]['fk_sede'], BANDEJA[0]['fk_periodo'] = 'VIRTUAL', 'BEL', '2026-2'
v('Tiene Aprobar («y crear la cátedra») y Rechazar, y el campo de observación',
  'value="aprobar"' in t and 'Aprobar y crear la cátedra' in t and 'value="rechazar"' in t and 'name="observacion"' in t)
v('...Rechazar no exige los datos de la cátedra (formnovalidate)', re.search(r'value="rechazar"[^>]*formnovalidate', t) is not None)
v('...y avisa que se crea la cátedra con su sesión y su QR', 'crea la cátedra</strong> con su sesión y su QR' in t)
v('...el nombre con comilla no rompe el JavaScript (va en data-confirmar, escapado)', 'data-confirmar="¿Aprobar a Ana O&#39;Brien' in t
  and "confirm('¿Aprobar" not in t and 'Se creará la cátedra y su sesión.' in t)

t = admin().get('/admin/solicitudes-ponente/4').get_data(as_text=True)
v('Una pendiente ANTERIOR a «cátedra nueva» (eligió sesión) no ofrece crear cátedra: se aprueba como antes',
  'id="catedra-a-crear"' not in t and 'Aprobar ponente y ponencia' in t and 'Sesión elegida' in t and 'Se le asignará a la sesión elegida' in t)
t = admin().get('/admin/solicitudes-ponente/2').get_data(as_text=True)
v('Una aprobada: sin botones de decisión, con enlaces a la persona, a la cátedra creada y a su sesión',
  'value="aprobar"' not in t and 'id="catedra-a-crear"' not in t and '/admin/asistentes/9' in t and '/admin/catedras/50' in t
  and '/admin/sesiones/500' in t and 'Cátedra creada' in t)
t = admin().get('/admin/solicitudes-ponente/3').get_data(as_text=True)
v('Una rechazada: muestra la observación y no ofrece nada', 'No encaja' in t and 'value="rechazar"' not in t and 'id="catedra-a-crear"' not in t)
v('Solicitud inexistente -> 404', admin().get('/admin/solicitudes-ponente/999').status_code == 404)

print('\n== Aprobar = crear la cátedra ==')
DECISION = {'decision': 'aprobar', 'observacion': 'Bienvenida', 'nombre_catedra': 'IA y educación', 'fk_tipo_evento': 'CAAB',
            'id_evento_asis': '', 'fk_modalidad': 'PRESENCIAL', 'fk_sede': 'MED', 'fk_periodo': '2026-2', 'lugar': 'Aula 101',
            'inicio': '2026-12-01T14:00', 'fin': '2026-12-01T16:00', 'enlace_virtual': ' https://meet.example.com/xyz '}
resueltas.clear(); qr_pedidos.clear(); conflictos_pedidos.clear()
r = admin().post('/admin/solicitudes-ponente/1/resolver', data=DECISION)
v('Aprobar -> 302 y UNA llamada a PATCH .../1/resolver', r.status_code == 302 and len(resueltas) == 1 and resueltas[0][:2] == ('SolicitudPonente', '1/resolver'))
d = resueltas[0][2] if resueltas else {}
v('Manda la decisión y TODOS los datos de la cátedra (fecha y hora con zona de Colombia; código ASIS vacío = null)',
  d.get('aprobar') is True and d.get('observacion') == 'Bienvenida' and d.get('resuelto_por') == 7 and d.get('nombre_catedra') == 'IA y educación'
  and d.get('fk_tipo_evento') == 'CAAB' and d.get('id_evento_asis') is None and d.get('fk_modalidad') == 'PRESENCIAL'
  and d.get('fk_sede') == 'MED' and d.get('fk_periodo') == '2026-2' and d.get('lugar') == 'Aula 101'
  and d.get('inicio') == '2026-12-01T14:00:00-05:00' and d.get('fin') == '2026-12-01T16:00:00-05:00')
v('D59: el enlace de ingreso escrito (sin espacios sobrantes) viaja a la API para guardarse en la sesión que se crea',
  d.get('enlace_virtual') == 'https://meet.example.com/xyz')
v('Antes de aprobar revisó que el horario no se cruce en esa sede y salón (con las fechas con zona)',
  conflictos_pedidos == [('2026-12-01T14:00:00-05:00', '2026-12-01T16:00:00-05:00', 'MED', 'Aula 101')])
v('Generó el QR de la cátedra creada (la 77 que devolvió la API), con el id del administrador', qr_pedidos == [(77, 7)])
t = admin().post('/admin/solicitudes-ponente/1/resolver', data=DECISION, follow_redirects=True).get_data(as_text=True)
v('Vuelve al detalle con el aviso de la API', 'Solicitud resuelta (simulada).' in t)

resueltas.clear(); qr_pedidos.clear()
modo['choques'] = [{'nombre_catedra': 'Otra catedra', 'numero_reunion': 3, 'inicio': iso(PROPUESTA_INI), 'fin': iso(PROPUESTA_FIN),
                    'fk_sede': 'MED', 'lugar': 'Aula 101', 'motivos': ['LUGAR']}]
r = admin().post('/admin/solicitudes-ponente/1/resolver', data=DECISION)
t = r.get_data(as_text=True)
v('Si el horario se cruza con otra sesión en la misma sede y salón: 409, NO se aprueba (no se llama a la API ni se genera QR)',
  r.status_code == 409 and not resueltas and not qr_pedidos and 'No se aprobó: el horario choca con' in t and 'Otra catedra' in t)
v('...y la pantalla conserva lo que escribió el administrador (nombre, salón, fecha, enlace)',
  'value="IA y educación"' in t and 'value="Aula 101"' in t and 'value="2026-12-01T14:00"' in t and re.search(r'<option value="MED" selected', t)
  and 'value="https://meet.example.com/xyz"' in t)
modo['choques'] = [{'nombre_catedra': 'Charla vecina', 'numero_reunion': 1, 'inicio': iso(PROPUESTA_INI - timedelta(hours=2)),
                    'fin': iso(PROPUESTA_INI - timedelta(minutes=30)), 'fk_sede': 'BEL', 'lugar': None, 'motivos': ['HORARIO'], 'solapa': False}]
r = admin().post('/admin/solicitudes-ponente/1/resolver', data=DECISION)
t = r.get_data(as_text=True)
v('D59: a menos de una hora de otra cátedra (aunque sea de otra sede): 409, NO se aprueba y se explica',
  r.status_code == 409 and not resueltas and 'Charla vecina' in t and 'menos de una hora' in t and 'mismo enlace' in t
  and 'al menos una hora entre una cátedra y otra' in t)
modo['choques'] = []
modo['conflictos_falla'] = True
r = admin().post('/admin/solicitudes-ponente/1/resolver', data=DECISION)
v('Si no se puede consultar la disponibilidad, no se aprueba (falla cerrado) y se avisa',
  r.status_code == 502 and not resueltas and 'no se pudo verificar la disponibilidad' in r.get_data(as_text=True).lower())
modo['conflictos_falla'] = False

resueltas.clear(); conflictos_pedidos.clear()
admin().post('/admin/solicitudes-ponente/1/resolver', data={**DECISION, 'fk_sede': '', 'id_evento_asis': ' 000012345 ', 'nombre_catedra': ''})
d = resueltas[0][2]
v('Sin sede igual se revisa el margen entre sesiones (D59: vale para cualquier sede); el nombre vacío va null y el código ASIS se manda',
  len(conflictos_pedidos) == 1 and d['fk_sede'] is None and d['nombre_catedra'] is None and d['id_evento_asis'] == '000012345')

resueltas.clear(); qr_pedidos.clear()
admin().post('/admin/solicitudes-ponente/4/resolver', data={'decision': 'aprobar', 'observacion': ''})
v('Aprobar una solicitud ANTERIOR (sin fecha propuesta) manda solo la decisión y no genera QR',
  resueltas == [('SolicitudPonente', '4/resolver', {'aprobar': True, 'observacion': None, 'resuelto_por': 7})] and not qr_pedidos)

resueltas.clear()
r = admin().post('/admin/solicitudes-ponente/1/resolver', data={'decision': 'rechazar', 'observacion': '   '})
v('Rechazar sin motivo -> NO se llama a la API', r.status_code == 302 and not resueltas)
t = admin().post('/admin/solicitudes-ponente/1/resolver', data={'decision': 'rechazar', 'observacion': ''}, follow_redirects=True).get_data(as_text=True)
v('...y se explica por qué', 'escribe el motivo' in t)
resueltas.clear(); qr_pedidos.clear(); conflictos_pedidos.clear()
admin().post('/admin/solicitudes-ponente/1/resolver', data={**DECISION, 'decision': 'rechazar', 'observacion': 'No es del tema'})
v('Rechazar con motivo manda solo la decisión (aunque el formulario traiga datos de cátedra), sin QR ni revisión de cruces',
  resueltas == [('SolicitudPonente', '1/resolver', {'aprobar': False, 'observacion': 'No es del tema', 'resuelto_por': 7})]
  and not qr_pedidos and not conflictos_pedidos)
resueltas.clear()
admin().post('/admin/solicitudes-ponente/1/resolver', data={'decision': 'quizas'})
v('Decisión inventada -> no se llama a la API', not resueltas)

for codigo, texto in ((409, 'La fecha propuesta ya pasó: ajusta la fecha y la hora.'), (400, 'Para aprobar hay que indicar la modalidad.')):
    def falla(*a, _c=codigo, _t=texto, **k):
        raise ErrorApi(_t, codigo=_c)
    R_sol.actualizar_parcial = falla
    qr_pedidos.clear()
    t = admin().post('/admin/solicitudes-ponente/1/resolver', data=DECISION, follow_redirects=True).get_data(as_text=True)
    v(f'Si la API rechaza ({codigo}) -> el motivo se muestra en el detalle y no se genera QR', texto in t and 'Bandeja de ponentes' in t and not qr_pedidos)
R_sol.actualizar_parcial = parcial_falso

print('\n== Panel de administración ==')
t = admin().get('/admin/').get_data(as_text=True)
v('El panel muestra la bandeja con 2 pendientes', 'Bandeja de ponentes' in t and '2 pendientes</span>' in t)
v('...y enlaza a la bandeja', '/admin/solicitudes-ponente' in t)
v('El panel conserva sus 39 secciones (la bandeja no es una de ellas)', t.count('<a class="card text-center') == 39)

print(f'\n{ok} pasadas, {fall} fallidas')
sys.exit(1 if fall else 0)
