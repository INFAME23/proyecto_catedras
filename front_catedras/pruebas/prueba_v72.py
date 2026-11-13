# Prueba v7.2, sin BD ni API: el front de la subversión v7.2.
#   · carga de estudiantes (CC, Carnet_estudiante, Programa) con periodo en un <select>, idempotente (RF-S8);
#   · registro público con 3 opciones (estudiante, docente, externo), sin clave por correo (RF-S9, D60);
#   · ubicación SOLO en sesiones presenciales (canal QR) y aviso temprano (RF-S10, D61);
#   · sedes con latitud, longitud y radio (RF-S11) y aviso cuando el QR no se puede generar (RF-S12, D72);
#   · exportación por cátedra: CSV, catálogo y ZIP, solo ADMIN (RF-S13, RF-S14, D70, D71).
# La API se sustituye por datos falsos en memoria.
# Uso, desde front_catedras:   python pruebas/prueba_v72.py     (necesita las librerías de requirements.txt;
#                              con Docker:  docker exec mi_v1_catedras_front python pruebas/prueba_v72.py)
import datetime as dt
import io
import os
import re
import sys
import zipfile

os.chdir(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
sys.path.insert(0, '.')
import werkzeug
werkzeug.__version__ = '3'

import app as A
import routes.admin as AD
import routes.admin_exportar as AX
import routes.carga as CG
import routes.registro as RG
import servicios.exportacion as EX
import servicios.qr_sesiones as Q
from servicios import carga_asistentes as CA
from servicios.api_catedras import ErrorApi

ahora = dt.datetime.now(dt.timezone.utc)
iso = lambda d: d.strftime('%Y-%m-%dT%H:%M:%SZ')
TOK = '11111111-2222-3333-4444-555555555555'
TOK_V = '99999999-2222-3333-4444-555555555555'
BOM = b'\xef\xbb\xbf'

# --------------------------------------------------------------------------------- API falsa
db = {
    'llamadas': [],
    'periodos': [
        {'id_periodo': '2025-2', 'fecha_ini': '2025-07-21', 'fecha_fin': '2025-12-06', 'activo': True},
        {'id_periodo': '2026-2', 'fecha_ini': '2026-07-20', 'fecha_fin': '2026-12-05', 'activo': True},
    ],
    'programas': [{'codigo': 'INDS', 'nombre': 'Ingenieria de Datos y Software (mock)'}, {'codigo': 'ADMO', 'nombre': 'Administracion de Empresas (mock)'}],
    'vistos': {},                      # carnet -> cc (la carga es idempotente)
    'enlace': {'canal': 'QR', 'requiere_ubicacion': True, 'estado_ventana': 'abierto', 'modalidad': 'PRESENCIAL'},
    'falla_registro': None,            # ErrorApi que debe lanzar AsistenciaPublica/*
    'ubicacion': {'estado': 'OK', 'mensaje': 'Estás en el lugar de la cátedra.', 'dentro': True, 'distancia_metros': 0.0,
                  'radio_metros': 200, 'requiere_ubicacion': True},
    'sin_geo': {2},                    # sesiones cuyo QR NO se puede generar (sede sin coordenadas)
    'sesiones': [{'id_sesion': 1, 'fk_catedra': 9}, {'id_sesion': 2, 'fk_catedra': 9}, {'id_sesion': 3, 'fk_catedra': 9}],
}

CATEDRAS_EXP = [
    {'id_catedra': '000000001', 'titulo': 'Primera cátedra', 'activo': True, 'estudiantes': 2, 'docentes': 1, 'externos': 1, 'otros': 2,
     'asistentes': [
         {'tipo': 'ESTUDIANTE', 'cc': '1032456789', 'carnet': '30000124726', 'programa': 'INDS'},
         {'tipo': 'ESTUDIANTE', 'cc': '1005823417', 'carnet': '30000124727', 'programa': 'ADMO'},
         {'tipo': 'DOCENTE', 'cc': '1012345678', 'nombre': '=HYPERLINK("http://malo")', 'contacto': 'ana@usbmed.edu.co', 'facultad': 'Facultad de Ingenierias'},
         {'tipo': 'EXTERNO', 'cc': '1012345679', 'nombre': 'Luis Gómez, Jr.', 'contacto': '3001234567', 'procedencia': '+Colegio "San José"'},
     ]},
    {'id_catedra': '000000002', 'titulo': 'Segunda sin asistentes', 'activo': True, 'estudiantes': 0, 'docentes': 0, 'externos': 0, 'otros': 0, 'asistentes': []},
    {'id_catedra': '000000003', 'titulo': 'Tercera, inactiva', 'activo': False, 'estudiantes': 1, 'docentes': 0, 'externos': 0, 'otros': 0,
     'asistentes': [{'tipo': 'ESTUDIANTE', 'cc': '1001234567', 'carnet': '30000124734', 'programa': 'EDUC'}]},
]


def listar(recurso, limite=None, **f):
    if recurso == 'PeriodoAcademico':
        return list(db['periodos'])
    if recurso == 'ProgramaAcademico':
        return list(db['programas'])
    if recurso == 'AliasPrograma':
        return []
    if recurso == 'Facultad':
        return [{'id_facultad': 'F_ING', 'nombre': 'Facultad de Ingenierias'}, {'id_facultad': 'F_EDU', 'nombre': 'Facultad de Educacion'}]
    if recurso == 'Sesion':
        return [s for s in db['sesiones'] if s['fk_catedra'] == f.get('fkCatedra')]
    if recurso == 'Sede':
        return [{'id_sede': 'BELLO', 'nombre': 'Campus Bello', 'direccion': 'Calle 45', 'es_virtual': False, 'activo': True,
                 'latitud': 6.333129, 'longitud': -75.569174, 'radio_metros': None, 'radio_efectivo_metros': 200},
                {'id_sede': 'NUEVA', 'nombre': 'Sede sin completar', 'direccion': None, 'es_virtual': False, 'activo': True,
                 'latitud': None, 'longitud': None, 'radio_metros': None, 'radio_efectivo_metros': 200},
                {'id_sede': 'VIRTUAL', 'nombre': 'Virtual', 'direccion': None, 'es_virtual': True, 'activo': True,
                 'latitud': None, 'longitud': None, 'radio_metros': None, 'radio_efectivo_metros': 200}]
    return []


def obtener(recurso, i):
    if recurso == 'RegistroAsistencia':
        return {'id_registro': int(i), 'fk_sesion': 1}
    if recurso == 'Sesion':
        return {'id_sesion': 1, 'fk_encuesta': None}
    if recurso == 'Sede':
        return {'id_sede': i, 'nombre': 'Campus Bello', 'direccion': None, 'es_virtual': False, 'activo': True,
                'latitud': 6.333129, 'longitud': -75.569174, 'radio_metros': 80, 'radio_efectivo_metros': 80}
    raise ErrorApi('No existe.', codigo=404)


def crear(recurso, d):
    db['llamadas'].append((recurso, d))
    if recurso == 'LoteCargaAsistente':
        return {'id_lote_carga': 7}
    if recurso == 'LoteCargaAsistente/7/cerrar':
        return {}
    if recurso == 'Asistente':           # el camino de personas de siempre
        return {'id_asistente': 5}
    if recurso == 'Asistente/cargar-estudiante':
        if d['programa'] == 'ZZZZ':
            raise ErrorApi(f'El programa "{d["programa"]}" no existe en el catálogo', codigo=409, estado='conflicto')
        carnet = d['id_asis']
        if carnet in db['vistos']:
            return {'id_asistente': 1, 'accion': 'ACTUALIZADO'}
        db['vistos'][carnet] = d['documento']
        return {'id_asistente': 1, 'accion': 'CREADO'}
    if recurso == 'QrSesion':
        if d['fk_sesion'] in db['sin_geo']:
            raise ErrorApi('La sede "Sede sin completar" (NUEVA) no tiene coordenadas: complétalas en Administración > Sedes antes de generar el QR de la sesión 2',
                           codigo=409, estado='conflicto')
        return {'token': TOK, 'ya_existia': False}
    if recurso.startswith('AsistenciaPublica/'):
        if db['falla_registro']:
            raise db['falla_registro']
        return {'estado': 'OK', 'mensaje': 'Asistencia registrada.', 'id_registro': 900}
    if recurso == 'Sede':
        return {'estado': 'creada'}
    raise AssertionError(recurso)


def reemplazar(recurso, i, d):
    db['llamadas'].append((f'PUT {recurso}/{i}', d))
    return {'filasAfectadas': 1}


def consultar(ruta, **p):
    if ruta.startswith('AsistenciaPublica/enlace/'):
        token = ruta.rsplit('/', 1)[1]
        if token == '00000000-0000-0000-0000-000000000000':
            raise ErrorApi('Este enlace de registro no existe.', codigo=404, estado='ENLACE_NO_EXISTE')
        e = dict(db['enlace'])
        if token == TOK_V:
            e.update(canal='ENLACE', requiere_ubicacion=False, modalidad='VIRTUAL')
        return {'id_enlace': 1, 'id_sesion': 1, 'titulo_sesion': None, 'numero_reunion': 3, 'id_catedra': '000000002',
                'titulo_catedra': 'Catedra Dos', 'inicio': iso(ahora - dt.timedelta(hours=1)), 'fin': iso(ahora + dt.timedelta(hours=1)),
                'ventana_desde': iso(ahora - dt.timedelta(minutes=5)), 'ventana_hasta': iso(ahora + dt.timedelta(minutes=30)), **e}
    if ruta == 'AsistenciaPublica/ubicacion':
        db['llamadas'].append(('GET ubicacion', p))
        if db['ubicacion'].get('error'):
            raise db['ubicacion']['error']
        return dict(db['ubicacion'])
    if ruta == 'Exportacion/catedras':
        return {'periodo': p['periodo'], 'total': len(CATEDRAS_EXP), 'datos': [{k: v for k, v in c.items() if k != 'asistentes'} for c in CATEDRAS_EXP]} \
            if p['periodo'] != '2024-1' else None
    if ruta == 'Exportacion/asistentes':
        db['llamadas'].append(('GET asistentes', dict(p)))
        todas = [] if p['periodo'] == '2024-1' else [c for c in CATEDRAS_EXP if p.get('desde') is None or c['id_catedra'] > p['desde']]
        pagina = todas[:p['limite']]
        siguiente = pagina[-1]['id_catedra'] if len(pagina) == p['limite'] else None
        return {'periodo': p['periodo'], 'total': len(pagina), 'siguiente': siguiente,
                'datos': [{'id_catedra': c['id_catedra'], 'titulo': c['titulo'], 'asistentes': c['asistentes']} for c in pagina]}
    raise AssertionError(ruta)


for m in (RG, CG, AD, AX):
    m.listar = listar
for m in (RG, AD):
    m.obtener = obtener
    m.crear = crear
    m.consultar = consultar
RG.crear = crear
CG.crear = crear
AD.reemplazar = reemplazar
AX.listar = listar
Q.listar = listar
Q.crear = crear
EX.consultar = consultar

app = A.app
app.config.update(TESTING=True, SECRET_KEY='prueba')
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


def texto(r):
    return r.get_data(as_text=True)


ADMIN = ['ADMIN']
llamadas_desde = lambda n: db['llamadas'][n:]

# ===================================================================================== 1. CARGA DE ESTUDIANTES (RF-S8)
print('\n== 1. Carga de estudiantes ==')
v('SINONIMOS reconoce «CC» y «Carnet_estudiante»',
  CA._encabezado_a_campo('CC') == 'documento' and CA._encabezado_a_campo('Carnet_estudiante') == 'id_asis' and CA._encabezado_a_campo('Programa') == 'programa')
v('Un encabezado de solo CC, Carnet_estudiante, Programa es un archivo de ESTUDIANTES',
  CA.es_archivo_de_estudiantes({'documento', 'id_asis', 'programa'}))
v('...pero con nombres, correo o periodo ya NO lo es (sigue el camino de personas de siempre)',
  not CA.es_archivo_de_estudiantes({'documento', 'id_asis', 'programa', 'nombres'}) and not CA.es_archivo_de_estudiantes({'documento', 'id_asis', 'programa', 'periodo'})
  and not CA.es_archivo_de_estudiantes({'id_asis', 'nombres', 'apellidos', 'correo_institucional'}))

indice = CA.indice_de_programas(db['programas'], [])
body = CA.construir_estudiante({'id_asis': '30000124726', 'documento': '1032456789', 'programa': 'inds'}, '2026-2', 7, indice)
v('construir_estudiante arma el body de la API con el periodo del select, el lote y el programa como CÓDIGO',
  body == {'id_asis': '30000124726', 'documento': '1032456789', 'programa': 'INDS', 'periodo': '2026-2', 'fk_lote_carga': 7})
v('...y un dato que falta viaja como null (la API responde «campo obligatorio»)',
  CA.construir_estudiante({'id_asis': '30000124726'}, '2026-2', 7, indice)['documento'] is None)

ARCHIVO = BOM + b'CC,Carnet_estudiante,Programa\r\n1032456789,30000124726,INDS\r\n1005823417,30000124727,ADMO\r\n1001234567,30000124799,ZZZZ\r\n'


def subir(contenido, nombre='estudiantes.csv', **campos):
    return cliente(ADMIN).post('/carga_datos', data={'archivo': (io.BytesIO(contenido), nombre), **campos},
                               content_type='multipart/form-data')


t = texto(cliente(ADMIN).get('/carga_datos'))
v('La pantalla de carga propone el periodo que cubre hoy o el último (2026-2) en el <select>',
  re.search(r'<option value="2026-2"\s+selected', t) is not None)
v('...el <select> lista los periodos y explica el archivo de estudiantes',
  'value="2025-2"' in t and 'value="2026-2"' in t and 'Carnet_estudiante' in t and 'estudiantes_ejemplo.csv' in t)
v('El archivo de ejemplo de estudiantes existe, con BOM y las 3 columnas',
  open('static/ejemplos/estudiantes_ejemplo.csv', 'rb').read().startswith(BOM + b'CC,Carnet_estudiante,Programa'))

n = len(db['llamadas'])
r = subir(ARCHIVO, periodo='2026-2')
t = texto(r)
cargas = [d for rec, d in llamadas_desde(n) if rec == 'Asistente/cargar-estudiante']
v('Cargar el archivo de estudiantes llama a POST Asistente/cargar-estudiante, una vez por fila, con el periodo elegido y el lote',
  len(cargas) == 3 and all(d['periodo'] == '2026-2' and d['fk_lote_carga'] == 7 for d in cargas) and not any(rec == 'Asistente' for rec, _ in llamadas_desde(n)))
v('...muestra «Archivo de estudiantes · periodo 2026-2», 2 nuevos y la fila rechazada con su motivo',
  'Archivo de estudiantes · periodo 2026-2' in t and '<strong>2</strong> estudiantes nuevos' in t and 'no existe en el catálogo' in t)
cierre = [d for rec, d in llamadas_desde(n) if rec == 'LoteCargaAsistente/7/cerrar'][0]
v('El lote se cierra con 3 leídas, 2 aceptadas, 1 rechazada y una novedad con el motivo de la API',
  (cierre['filas_leidas'], cierre['filas_aceptadas'], cierre['filas_rechazadas']) == (3, 2, 1) and len(cierre['novedades']) == 1
  and 'no existe en el catálogo' in cierre['novedades'][0]['motivo'] and cierre['novedades'][0]['numero_fila'] == 4)

n = len(db['llamadas'])
t = texto(subir(ARCHIVO, periodo='2026-2'))
v('Volver a subir el MISMO archivo no duplica: 0 nuevos y 2 «ya existían y se actualizaron»',
  '<strong>0</strong> estudiantes nuevos' in t and '<strong>2</strong> que ya existían' in t)

n = len(db['llamadas'])
t = texto(subir(ARCHIVO))
v('Sin elegir periodo, el archivo de estudiantes NO se carga (ni se abre el lote): pide elegirlo',
  'elige uno de la lista' in t and len(db['llamadas']) == n)
t = texto(subir(ARCHIVO, periodo='1999-1'))
v('Con un periodo que no existe tampoco se carga', 'elige uno de la lista' in t and len(db['llamadas']) == n)

n = len(db['llamadas'])
viejo = 'id_asis;nombres;apellidos;correo_institucional;documento;tipo_vinculacion;programa;periodo\nCARGA0001;Ana;Prueba;ana@usbmed.edu.co;80010001;ESTUDIANTE;INDS;2026-1\n'.encode()
t = texto(subir(viejo, 'personas.csv', periodo='2026-2'))
v('Un archivo con las columnas de siempre sigue el camino de personas (POST Asistente) y no usa el periodo del select',
  any(rec == 'Asistente' for rec, _ in llamadas_desde(n)) and not any(rec == 'Asistente/cargar-estudiante' for rec, _ in llamadas_desde(n))
  and 'Archivo de estudiantes · periodo' not in t)
v('La carga sigue siendo solo del ADMIN', cliente().post('/carga_datos', data={}).status_code == 302 and cliente([]).post('/carga_datos', data={}).status_code == 403)

# ===================================================================================== 2. REGISTRO PÚBLICO (RF-S9, D60)
print('\n== 2. Registro público: 3 opciones, sin clave por correo ==')
t = texto(cliente().get(f'/registro/{TOK}'))
v('El QR abre la página pública con el título de la cátedra, la sesión y las TRES opciones',
  'Catedra Dos' in t and 'Sesión 3' in t and t.count('?tipo=') == 3 and 'tipo=estudiante' in t and 'tipo=docente' in t and 'tipo=externo' in t)
v('...sin pedir clave, correo ni «documento o correo» (D60)', 'name="identificador"' not in t and 'Enviarme la clave' not in t and 'name="clave"' not in t)
v('...y es PÚBLICA (no pide iniciar sesión)', cliente().get(f'/registro/{TOK}').status_code == 200)
v('Un token que no es un UUID da 404', cliente().get('/registro/no-es-un-token').status_code == 404)
r = cliente().get('/registro/00000000-0000-0000-0000-000000000000')
v('Un enlace que no existe da 404 con un mensaje claro (no un error del servidor)', r.status_code == 404 and 'no existe' in texto(r))
v('Ya no hay pantalla para pegar el enlace: GET y POST /registro dan 404 (solo se entra con el QR o el enlace de la sesión)',
  cliente().get('/registro').status_code == 404 and cliente().post('/registro', data={'enlace': 'x'}).status_code in (404, 405))

for estado, frase in (('pendiente', 'Todavía no se puede'), ('cerrado', 'ya cerró'), ('revocado', 'ya no es válido')):
    db['enlace']['estado_ventana'] = estado
    t = texto(cliente().get(f'/registro/{TOK}'))
    t2 = texto(cliente().get(f'/registro/{TOK}?tipo=estudiante'))
    v(f'Ventana «{estado}»: avisa «{frase}» y NO ofrece opciones ni formulario',
      frase in t and 'tipo=docente' not in t and 'name="carnet"' not in t2)
db['enlace']['estado_ventana'] = 'abierto'

t = texto(cliente().get(f'/registro/{TOK}?tipo=estudiante'))
v('Estudiante: el formulario pide SOLO el carnet', 'name="carnet"' in t and 'name="cedula"' not in t and 'name="fk_facultad"' not in t)
t = texto(cliente().get(f'/registro/{TOK}?tipo=docente'))
v('Docente: cédula, nombre, contacto (un solo campo) y facultad en un <select> con «id · nombre» (Regla 2)',
  all(f'name="{c}"' in t for c in ('cedula', 'nombre', 'contacto', 'fk_facultad')) and 'F_ING · Facultad de Ingenierias' in t and 'name="procedencia"' not in t)
t = texto(cliente().get(f'/registro/{TOK}?tipo=externo'))
v('Externo: cédula, nombre, contacto y de dónde viene', all(f'name="{c}"' in t for c in ('cedula', 'nombre', 'contacto', 'procedencia')) and 'name="fk_facultad"' not in t)
v('Un tipo desconocido cae en las 3 opciones (no rompe)', 'tipo=docente' in texto(cliente().get(f'/registro/{TOK}?tipo=profesor')))
v('Las rutas viejas de la clave por correo ya no existen (POST /registro/<token>/validar -> 404)',
  cliente().post(f'/registro/{TOK}/validar', data={}).status_code == 404)

# ===================================================================================== 3. UBICACIÓN POR CANAL (RF-S10, D61)
print('\n== 3. Ubicación solo en sesiones presenciales ==')
t = texto(cliente().get(f'/registro/{TOK}?tipo=estudiante'))
v('Presencial (canal QR): el formulario pide ubicación, bloquea los campos hasta comprobarla y apunta al aviso temprano',
  'data-requiere-ubicacion' in t and 'name="latitud"' in t and 'disabled data-ubicacion-campos' in t
  and f'data-url-ubicacion="/registro/{TOK}/ubicacion"' in t and 'geolocalizacion.js' in t and 'data-ubicacion-enviar disabled' in t)
t = texto(cliente().get(f'/registro/{TOK_V}?tipo=estudiante'))
v('Virtual (canal ENLACE): el formulario NO pide ubicación, no bloquea campos y ni carga el script',
  'data-requiere-ubicacion' not in t and 'name="latitud"' not in t and 'data-ubicacion-campos' not in t and 'geolocalizacion.js' not in t and 'name="carnet"' in t)

n = len(db['llamadas'])
r = cliente().post(f'/registro/{TOK}/estudiante', data={'carnet': '30000124726'})
v('Presencial SIN ubicación: se rechaza ANTES de llamar a la API, con un aviso claro y el formulario conserva lo escrito',
  not any(rec.startswith('AsistenciaPublica') for rec, _ in llamadas_desde(n)) and 'Falta tu ubicación' in texto(r) and 'value="30000124726"' in texto(r))
for lat, lon in (('abc', '1'), ('95', '0'), ('6.2', '-181'), ('nan', 'nan')):
    n = len(db['llamadas'])
    r = cliente().post(f'/registro/{TOK}/estudiante', data={'carnet': '30000124726', 'latitud': lat, 'longitud': lon})
    v(f'Ubicación inválida ({lat}, {lon}): no se llama a la API', not any(rec.startswith('AsistenciaPublica') for rec, _ in llamadas_desde(n)) and 'Falta tu ubicación' in texto(r))

n = len(db['llamadas'])
r = cliente().post(
    f'/registro/{TOK}/estudiante', data={'carnet': ' 30000124726 ', 'latitud': '6,333129', 'longitud': '-75.569174'},
    headers={'User-Agent': 'prueba-navegador'}, environ_base={'REMOTE_ADDR': '10.1.2.3'})
body = [d for rec, d in llamadas_desde(n) if rec == 'AsistenciaPublica/estudiante'][0]
v('Presencial CON ubicación: llama a AsistenciaPublica/estudiante con token, carnet, ubicación (acepta coma decimal), IP y navegador',
  body['token'] == TOK and body['carnet'] == '30000124726' and body['latitud'] == 6.333129 and body['longitud'] == -75.569174
  and body['ip'] == '10.1.2.3' and body['user_agent'] == 'prueba-navegador' and 'clave' not in body)
v('...y muestra «¡Asistencia registrada!»', '¡Asistencia registrada!' in texto(r))

n = len(db['llamadas'])
cliente().post(f'/registro/{TOK_V}/estudiante', data={'carnet': '30000124726', 'latitud': '6.2', 'longitud': '-75.5'})
body = [d for rec, d in llamadas_desde(n) if rec == 'AsistenciaPublica/estudiante'][0]
v('Virtual: aunque el navegador mande coordenadas, NO se mandan a la API (latitud y longitud quedan sin enviar)',
  'latitud' not in body and 'longitud' not in body)
n = len(db['llamadas'])
r = cliente().post(f'/registro/{TOK_V}/estudiante', data={'carnet': '30000124726'})
v('Virtual sin ubicación: se registra (no se exige nada)', any(rec == 'AsistenciaPublica/estudiante' for rec, _ in llamadas_desde(n)) and '¡Asistencia registrada!' in texto(r))

# el canal sale de la API, no del formulario
n = len(db['llamadas'])
cliente().post(f'/registro/{TOK}/estudiante', data={'carnet': '30000124726', 'canal': 'ENLACE'})
v('El canal NO se toma del formulario: un «canal=ENLACE» escrito a mano no evita la ubicación', not any(rec.startswith('AsistenciaPublica') for rec, _ in llamadas_desde(n)))

# aviso temprano
r = cliente().get(f'/registro/{TOK}/ubicacion?latitud=6.333129&longitud=-75.569174')
j = r.get_json()
v('Aviso temprano dentro del radio: JSON con estado OK y dentro = true', r.status_code == 200 and j['estado'] == 'OK' and j['dentro'] is True and j['radio_metros'] == 200)
db['ubicacion'] = {'estado': 'FUERA_DE_RADIO', 'mensaje': 'Parece que no estás en el lugar de la cátedra.', 'dentro': False, 'distancia_metros': 8687.1,
                   'radio_metros': 200, 'requiere_ubicacion': True}
j = cliente().get(f'/registro/{TOK}/ubicacion?latitud=6.255095&longitud=-75.572969').get_json()
v('Aviso temprano fuera del radio: dentro = false, con el mensaje y la distancia', j['estado'] == 'FUERA_DE_RADIO' and j['dentro'] is False and j['distancia_metros'] == 8687.1)
r = cliente().get(f'/registro/{TOK}/ubicacion?latitud=abc&longitud=1')
v('Aviso temprano con datos inválidos: 400 y no llega a la API', r.status_code == 400 and r.get_json()['estado'] == 'DATOS_INVALIDOS')
db['ubicacion'] = {'error': ErrorApi('Este enlace ya no es válido.', codigo=409, estado='ENLACE_REVOCADO')}
r = cliente().get(f'/registro/{TOK}/ubicacion?latitud=6.2&longitud=-75.5')
v('Aviso temprano con el enlace revocado: pasa el 409 con su código y mensaje', r.status_code == 409 and r.get_json()['estado'] == 'ENLACE_REVOCADO')
db['ubicacion'] = {'error': ErrorApi('No se pudo contactar la API.')}
r = cliente().get(f'/registro/{TOK}/ubicacion?latitud=6.2&longitud=-75.5')
v('Aviso temprano con la API caída: 502 (el script NO bloquea; la BD verifica al registrar)', r.status_code == 502)
db['ubicacion'] = {'estado': 'OK', 'mensaje': 'Estás en el lugar de la cátedra.', 'dentro': True, 'distancia_metros': 0.0, 'radio_metros': 200, 'requiere_ubicacion': True}

js = open('static/geolocalizacion.js', encoding='utf-8').read()
v('geolocalizacion.js consulta el aviso temprano (data-url-ubicacion), mantiene los campos bloqueados y no tiene «continuar sin ubicación»',
  'data-url-ubicacion' in js and 'data-ubicacion-campos' in js and 'FUERA_DE_RADIO' in js and 'data-ubicacion-continuar' not in js)

# ===================================================================================== 4. RECHAZOS DE NEGOCIO Y TIPOS
print('\n== 4. Docente, externo y rechazos ==')
UBI = {'latitud': '6.333129', 'longitud': '-75.569174'}
n = len(db['llamadas'])
r = cliente().post(f'/registro/{TOK}/docente', data={'cedula': '1012345678', 'nombre': ' Ana Pérez ', 'contacto': 'ana@usbmed.edu.co', 'fk_facultad': 'F_ING', **UBI})
body = [d for rec, d in llamadas_desde(n) if rec == 'AsistenciaPublica/docente'][0]
v('Docente: llama a AsistenciaPublica/docente con cédula, nombre, contacto y facultad (sin espacios sobrantes)',
  (body['cedula'], body['nombre'], body['contacto'], body['fk_facultad']) == ('1012345678', 'Ana Pérez', 'ana@usbmed.edu.co', 'F_ING') and '¡Asistencia registrada!' in texto(r))
n = len(db['llamadas'])
r = cliente().post(f'/registro/{TOK}/externo', data={'cedula': '1012345679', 'nombre': 'Luis', 'contacto': '3001234567', 'procedencia': 'Colegio X', **UBI})
body = [d for rec, d in llamadas_desde(n) if rec == 'AsistenciaPublica/externo'][0]
v('Externo: llama a AsistenciaPublica/externo con la procedencia', body['procedencia'] == 'Colegio X' and body['contacto'] == '3001234567' and '¡Asistencia registrada!' in texto(r))

db['falla_registro'] = ErrorApi('Parece que no estás en el lugar de la cátedra. Acércate a la sede para registrar tu asistencia.', codigo=409,
                                estado='FUERA_DE_RADIO', cuerpo={'distancia_metros': 8687.1, 'radio_metros': 200})
r = cliente().post(f'/registro/{TOK}/estudiante', data={'carnet': '30000124726', **UBI})
t = texto(r)
v('Fuera del radio (verificación definitiva): «Parece que no estás en el lugar de la cátedra», sin éxito y con el formulario',
  'Parece que no estás en el lugar de la cátedra' in t and '¡Asistencia registrada!' not in t and 'name="carnet"' in t)
db['falla_registro'] = ErrorApi('Ya registraste tu asistencia en esta sesión.', codigo=409, estado='YA_REGISTRADO')
r = cliente().post(f'/registro/{TOK}/estudiante', data={'carnet': '30000124726', **UBI})
t = texto(r)
v('Ya registrado: mensaje amistoso (200, «Ya estabas registrado»), no un error ni un 500', r.status_code == 200 and 'Ya estabas registrado' in t)
db['falla_registro'] = ErrorApi('No encontramos ese código de estudiante.', codigo=409, estado='ESTUDIANTE_NO_EXISTE')
t = texto(cliente().post(f'/registro/{TOK}/estudiante', data={'carnet': 'NOEXISTE9', **UBI}))
v('Carnet inexistente: el mensaje de la API y el formulario con lo escrito', 'No encontramos ese código de estudiante' in t and 'value="NOEXISTE9"' in t)
db['falla_registro'] = ErrorApi('El cuerpo no cumple.', codigo=422, errores=[{'campo': 'carnet', 'mensaje': 'El campo carnet debe tener entre 5 y 15 letras o dígitos.'}])
t = texto(cliente().post(f'/registro/{TOK}/estudiante', data={'carnet': 'ab', **UBI}))
v('Un 422 de la API se pinta junto al formulario (campo: mensaje)', 'carnet: El campo carnet debe tener' in t)
db['falla_registro'] = ErrorApi('No se pudo contactar la API.')
t = texto(cliente().post(f'/registro/{TOK}/estudiante', data={'carnet': '30000124726', **UBI}))
v('API caída: aviso genérico en español, sin traza', 'No se pudo contactar el servicio de registro' in t)
db['falla_registro'] = None
v('Un tipo desconocido en la URL del POST da 404', cliente().post(f'/registro/{TOK}/profesor', data={}).status_code == 404)

# ===================================================================================== 5. SEDES (RF-S11) y QR (RF-S12)
print('\n== 5. Sedes con ubicación y aviso de QR ==')
n = len(db['llamadas'])
cliente(ADMIN).post('/admin/sedes/nueva', data={'id_sede': 'CENTRO', 'nombre': 'Centro', 'latitud': '6,25', 'longitud': '-75.57', 'radio_metros': '120', 'activo': 'on'})
body = [d for rec, d in llamadas_desde(n) if rec == 'Sede'][0]
v('Nueva sede: viajan latitud, longitud (coma decimal aceptada) y radio como números', body['latitud'] == 6.25 and body['longitud'] == -75.57 and body['radio_metros'] == 120)
n = len(db['llamadas'])
r = cliente(ADMIN).post('/admin/sedes/nueva', data={'id_sede': 'RARA', 'nombre': 'Rara', 'latitud': 'abc', 'longitud': '-75.57', 'activo': 'on'})
v('Una latitud que no es número NO llega a la API: el formulario la señala y conserva lo escrito',
  not any(rec == 'Sede' for rec, _ in llamadas_desde(n)) and 'latitud: No es un número válido' in texto(r) and 'value="abc"' in texto(r))
n = len(db['llamadas'])
cliente(ADMIN).post('/admin/sedes/nueva', data={'id_sede': 'SINGEO', 'nombre': 'Sin coordenadas', 'activo': 'on'})
body = [d for rec, d in llamadas_desde(n) if rec == 'Sede'][0]
v('Una sede sin ubicación se crea sin esas claves (es opcional)', 'latitud' not in body and 'longitud' not in body and 'radio_metros' not in body)
n = len(db['llamadas'])
cliente(ADMIN).post('/admin/sedes/nueva', data={'id_sede': 'VIRT2', 'nombre': 'Virtual dos', 'es_virtual': 'on', 'latitud': '6.2', 'longitud': '-75.5', 'activo': 'on'})
body = [d for rec, d in llamadas_desde(n) if rec == 'Sede'][0]
v('Una sede VIRTUAL no manda ubicación aunque se escriba', body['es_virtual'] is True and 'latitud' not in body)
n = len(db['llamadas'])
cliente(ADMIN).post('/admin/sedes/BELLO/editar', data={'nombre': 'Campus Bello', 'activo': 'on'})
body = [d for rec, d in llamadas_desde(n) if rec == 'PUT Sede/BELLO'][0]
v('Editar con la ubicación en blanco NO la manda (la API la conserva)', 'latitud' not in body and 'longitud' not in body)
n = len(db['llamadas'])
cliente(ADMIN).post('/admin/sedes/BELLO/editar', data={'nombre': 'Campus Bello', 'activo': 'on', 'latitud': '6.4', 'longitud': '-75.6', 'radio_metros': '60'})
body = [d for rec, d in llamadas_desde(n) if rec == 'PUT Sede/BELLO'][0]
v('Editar con ubicación nueva la manda completa', (body['latitud'], body['longitud'], body['radio_metros']) == (6.4, -75.6, 60))
t = texto(cliente(ADMIN).get('/admin/sedes/BELLO/editar'))
v('El formulario de edición muestra la ubicación guardada, el radio efectivo y que en blanco no cambia',
  'value="6.333129"' in t and 'value="-75.569174"' in t and 'Hoy se aplica: 80 m' in t and 'no cambia' in t)
t = texto(cliente(ADMIN).get('/admin/sedes'))
v('La lista de sedes muestra las coordenadas y marca «Sin coordenadas» SOLO en la sede física que no las tiene (no en la virtual)',
  '6.333129, -75.569174' in t and t.count('Sin coordenadas') == 2 and 'google.com/maps?q=6.333129,-75.569174' in t)

motivos = []
hechos, total = Q.asegurar_qr_de_catedra(9, 1, motivos)
v('Crear la cátedra: si una sesión presencial está en una sede sin coordenadas, el motivo de la API (nombra la sede) se recoge',
  (hechos, total) == (2, 3) and len(motivos) == 1 and 'Sede sin completar' in motivos[0] and 'no tiene coordenadas' in motivos[0])
v('...sin pasar la lista, la función se comporta como antes ((hechos, total))', Q.asegurar_qr_de_catedra(9, 1) == (2, 3))
avisos = []
Q.avisar_qr_pendientes(lambda m, c: avisos.append((m, c)), hechos, total, motivos)
v('...y se avisa con ese motivo como error FIJO (flash de error), no con «2 de 3 sesiones»',
  avisos == [(motivos[0], 'error')])
avisos = []
Q.avisar_qr_pendientes(lambda m, c: avisos.append((m, c)), 2, 3, [])
v('Sin motivo de la API se conserva el aviso por conteo', avisos == [('Se generó el QR de 2 de 3 sesiones.', 'error')])
avisos = []
Q.avisar_qr_pendientes(lambda m, c: avisos.append((m, c)), 3, 3, [])
v('Si todas tienen QR, no se avisa nada', avisos == [])

# ===================================================================================== 6. EXPORTACIÓN (RF-S13, RF-S14)
print('\n== 6. Exportación por cátedra ==')
v('Seguridad del CSV: = + - @ se neutralizan con una comilla; el texto normal y el vacío no cambian',
  EX.celda_segura('=1+1') == "'=1+1" and EX.celda_segura('+57') == "'+57" and EX.celda_segura('-x') == "'-x" and EX.celda_segura('@a') == "'@a"
  and EX.celda_segura('Ana') == 'Ana' and EX.celda_segura(None) == '' and EX.celda_segura('\tx') == "'\tx")

asist = CATEDRAS_EXP[0]['asistentes']
b = EX.csv_de_asistentes('estudiantes', asist)
v('CSV de estudiantes: UTF-8 con BOM, \\r\\n, y EXACTAMENTE las columnas del archivo de carga (CC, Carnet_estudiante, Programa)',
  b.startswith(BOM) and b.decode('utf-8-sig').split('\r\n')[0] == 'CC,Carnet_estudiante,Programa' and b.count(b'\r\n') == 3)
v('...con los estudiantes (y solo ellos): CC, carnet y programa tal como se cargó',
  b.decode('utf-8-sig').split('\r\n')[1:3] == ['1032456789,30000124726,INDS', '1005823417,30000124727,ADMO'])
b = EX.csv_de_asistentes('docentes', asist)
v('CSV de docentes: CC, Nombre, Contacto, Facultad; el nombre que empieza por «=» sale protegido',
  b.decode('utf-8-sig').split('\r\n')[0] == 'CC,Nombre,Contacto,Facultad' and "'=HYPERLINK" in b.decode('utf-8') and ',=HYPERLINK' not in b.decode('utf-8'))
b = EX.csv_de_asistentes('externos', asist)
v('CSV de externos: CC, Nombre, Contacto, Procedencia; con comas y comillas bien escapadas y «+» protegido',
  b.decode('utf-8-sig').split('\r\n')[0] == 'CC,Nombre,Contacto,Procedencia'
  and '"Luis Gómez, Jr."' in b.decode('utf-8-sig') and '"\'+Colegio ""San José"""' in b.decode('utf-8-sig'))
v('Un grupo sin asistentes da un CSV con solo el encabezado (y BOM)', EX.csv_de_asistentes('docentes', []).decode('utf-8-sig') == 'CC,Nombre,Contacto,Facultad\r\n')
v('El catálogo trae id_catedra,titulo de TODAS las cátedras que entrega la API',
  EX.csv_catalogo([{k: c[k] for k in ('id_catedra', 'titulo')} for c in CATEDRAS_EXP]).decode('utf-8-sig').split('\r\n')[:4]
  == ['id_catedra,titulo', '000000001,Primera cátedra', '000000002,Segunda sin asistentes', '000000003,"Tercera, inactiva"'])
v('El id de la cátedra va en el NOMBRE del archivo', EX.nombre_archivo('000000001', 'estudiantes') == 'catedra_000000001_estudiantes.csv')
v('Validaciones de la URL: periodo y id de 9 dígitos',
  EX.periodo_valido('2026-2') and not EX.periodo_valido('../x') and not EX.periodo_valido('') and EX.id_catedra_valido('000000001')
  and not EX.id_catedra_valido('1') and not EX.id_catedra_valido('00000000a'))

n = len(db['llamadas'])
z, cantidad = EX.zip_del_periodo('2026-2')
nombres = sorted(zipfile.ZipFile(io.BytesIO(z)).namelist())
v('El ZIP trae los TRES CSV de CADA cátedra del periodo (3 cátedras -> 9 archivos), sin carpetas',
  cantidad == 3 and len(nombres) == 9 and all('/' not in x for x in nombres)
  and nombres[:3] == ['catedra_000000001_docentes.csv', 'catedra_000000001_estudiantes.csv', 'catedra_000000001_externos.csv'])
zf = zipfile.ZipFile(io.BytesIO(z))
v('...los de una cátedra sin asistentes vienen SOLO con el encabezado (contenido predecible)',
  zf.read('catedra_000000002_estudiantes.csv').decode('utf-8-sig') == 'CC,Carnet_estudiante,Programa\r\n'
  and zf.read('catedra_000000002_docentes.csv').decode('utf-8-sig') == 'CC,Nombre,Contacto,Facultad\r\n'
  and zf.read('catedra_000000002_externos.csv').decode('utf-8-sig') == 'CC,Nombre,Contacto,Procedencia\r\n')
v('...y los CSV del ZIP son los mismos que los individuales (UTF-8 con BOM)',
  zf.read('catedra_000000001_estudiantes.csv') == EX.csv_de_asistentes('estudiantes', asist) and zf.read('catedra_000000001_estudiantes.csv').startswith(BOM))
v('El ZIP pide los asistentes por PÁGINAS de 100 cátedras (no una petición por cátedra)',
  [p for rec, p in llamadas_desde(n) if rec == 'GET asistentes'] == [{'periodo': '2026-2', 'desde': None, 'limite': 100}])

# paginación real: con páginas de 2 cátedras se recorren todas
tam = EX.TAMANO_PAGINA
EX.TAMANO_PAGINA = 2
n = len(db['llamadas'])
ids = [c['id_catedra'] for c in EX.paginas_de_asistentes('2026-2')]
EX.TAMANO_PAGINA = tam
v('La lectura por páginas recorre TODAS las cátedras hasta que «siguiente» es null (páginas de 2: 3 cátedras, 2 peticiones)',
  ids == ['000000001', '000000002', '000000003'] and len([1 for rec, _ in llamadas_desde(n) if rec == 'GET asistentes']) == 2)
v('Los asistentes de UNA cátedra se piden con la página de 1 que empieza justo antes de su id',
  len(EX.asistentes_de_una_catedra('2026-2', '000000001')) == 4 and EX.asistentes_de_una_catedra('2026-2', '000000002') == []
  and db['llamadas'][-1][1]['desde'] == '000000001' and db['llamadas'][-1][1]['limite'] == 1)
v('...y una cátedra que no existe en el periodo da lista vacía (no la del vecino)', EX.asistentes_de_una_catedra('2026-2', '000000009') == [])

# rutas
for ruta in ('/admin/exportar', '/admin/exportar/2026-2/catalogo.csv', '/admin/exportar/2026-2/todas.zip', '/admin/exportar/2026-2/catedra/000000001/estudiantes.csv'):
    v(f'{ruta}: sin sesión va al login y con otro rol da 403 (solo ADMIN)', cliente().get(ruta).status_code == 302 and cliente(['CONSULTA']).get(ruta).status_code == 403)

t = texto(cliente(ADMIN).get('/admin/exportar'))
v('La pantalla propone el periodo que cubre hoy (o el más reciente) y trae el <select> de periodos (Regla 2)',
  'name="periodo"' in t and 'value="2026-2"' in t and 'value="2025-2"' in t)
v('...lista cada cátedra con su ID de 9 dígitos, título y los conteos de estudiantes, docentes y externos',
  '000000001' in t and 'Primera cátedra' in t and 'Segunda sin asistentes' in t and 'Tercera, inactiva' in t)
v('...con tres botones por fila: el de un grupo SIN asistentes sale apagado (igual en los tres tipos)',
  '/admin/exportar/2026-2/catedra/000000001/estudiantes.csv' in t and '/admin/exportar/2026-2/catedra/000000001/docentes.csv' in t
  and '/admin/exportar/2026-2/catedra/000000001/externos.csv' in t
  and '/catedra/000000002/estudiantes.csv' not in t and '/catedra/000000003/docentes.csv' not in t and 'btn-outline-secondary disabled' in t)
v('...marca la cátedra inactiva, avisa de las «otras vinculaciones» que no se exportan y ofrece el ZIP y el catálogo',
  'inactiva' in t and 'no se exportan' in t and '/admin/exportar/2026-2/todas.zip' in t and '/admin/exportar/2026-2/catalogo.csv' in t and 'catedras_2026-2.csv' in t)
v('...y avisa que no calcula el total por estudiante', 'no calcula ese total' in t)

r = cliente(ADMIN).get('/admin/exportar/2026-2/catedra/000000001/estudiantes.csv')
v('Descarga de un CSV: text/csv UTF-8, como adjunto con el id en el nombre, BOM y contenido del tipo pedido',
  r.status_code == 200 and r.mimetype == 'text/csv' and 'attachment; filename="catedra_000000001_estudiantes.csv"' in r.headers['Content-Disposition']
  and r.data.startswith(BOM) and b'30000124726' in r.data and b'ana@usbmed' not in r.data)
r = cliente(ADMIN).get('/admin/exportar/2026-2/catedra/000000001/docentes.csv')
v('Docentes: sin los estudiantes y con el texto libre protegido', b'ana@usbmed.edu.co' in r.data and b'30000124726' not in r.data and b"'=HYPERLINK" in r.data)
r = cliente(ADMIN).get('/admin/exportar/2026-2/catalogo.csv')
v('Catálogo: catedras_2026-2.csv con todas las cátedras', r.status_code == 200 and 'filename="catedras_2026-2.csv"' in r.headers['Content-Disposition']
  and r.data.count(b'\r\n') == 4 and b'000000003' in r.data)
r = cliente(ADMIN).get('/admin/exportar/2026-2/todas.zip')
v('ZIP: application/zip como adjunto asistencia_2026-2.zip con los 9 archivos',
  r.status_code == 200 and r.mimetype == 'application/zip' and 'asistencia_2026-2.zip' in r.headers['Content-Disposition']
  and len(zipfile.ZipFile(io.BytesIO(r.data)).namelist()) == 9)
v('Tipo, periodo o id inválidos dan 404 (no se arma ninguna consulta con ellos)',
  cliente(ADMIN).get('/admin/exportar/2026-2/catedra/000000001/admin.csv').status_code == 404
  and cliente(ADMIN).get('/admin/exportar/2026-2/catedra/1/estudiantes.csv').status_code == 404
  and cliente(ADMIN).get('/admin/exportar/muy-largo-xx/catalogo.csv').status_code == 404)
r = cliente(ADMIN).get('/admin/exportar?periodo=2024-1')
v('Un periodo sin cátedras (la API responde 204) no rompe: dice que no hay cátedras y apaga el catálogo',
  r.status_code == 200 and 'no tiene cátedras con sesiones' in texto(r) and 'btn btn-outline-primary disabled' in texto(r))
r = cliente(ADMIN).get('/admin/exportar/2024-1/todas.zip', follow_redirects=True)
v('...y pedir el ZIP de ese periodo avisa en vez de bajar un ZIP vacío', 'no tiene cátedras' in texto(r) and r.mimetype == 'text/html')
t = texto(cliente(ADMIN).get('/admin/'))
v('El panel de administración tiene la tarjeta «Exportar asistencia»', 'Exportar asistencia' in t and '/admin/exportar' in t)

# ===================================================================================== 7. P15: IP real detrás del proxy
print('\n== 7. IP real detrás de un proxy ==')
v('PROXY_CONFIABLES vale 0 por defecto (no se confía en X-Forwarded-For sin proxy) y está documentado',
  app.config['PROXY_CONFIABLES'] == 0 and 'PROXY_CONFIABLES' in open('env.ejemplo', encoding='utf-8').read()
  and (not os.path.exists('../docker-compose.yml') or 'PROXY_CONFIABLES' in open('../docker-compose.yml', encoding='utf-8').read()))   # en el contenedor solo se monta front_catedras
from werkzeug.middleware.proxy_fix import ProxyFix
from flask import Flask, request as rq
prueba = Flask('proxy')
prueba.wsgi_app = ProxyFix(prueba.wsgi_app, x_for=1)
prueba.add_url_rule('/', 'ip', lambda: rq.remote_addr)
v('Con ProxyFix(x_for=1) la IP que ve Flask es la del encabezado X-Forwarded-For (la de la persona, no la del proxy)',
  prueba.test_client().get('/', headers={'X-Forwarded-For': '190.1.2.3'}, environ_base={'REMOTE_ADDR': '10.0.0.1'}).get_data(as_text=True) == '190.1.2.3')

print(f'\n{ok} pasadas, {fall} fallidas')
sys.exit(1 if fall else 0)
