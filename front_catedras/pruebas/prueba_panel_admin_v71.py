# Prueba v7.1 (D51), sin BD ni API: el panel de /admin ya no tiene barra lateral, sus categorías
# son desplegables, los títulos no llevan versión y no hay la línea «Las 37 tablas...».
# Uso, desde front_catedras:   python pruebas/prueba_panel_admin_v71.py
import os
import re
import sys

os.chdir(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
sys.path.insert(0, '.')
import werkzeug
werkzeug.__version__ = '3'

import app as A

app = A.app
app.config['TESTING'] = True
ok = fall = 0


def v(caso, cond):
    global ok, fall
    print(('  OK    ' if cond else '  FALLA ') + caso)
    ok += bool(cond)
    fall += (not cond)


def admin():
    c = app.test_client()
    with c.session_transaction() as s:
        s['usuario'] = {'id_usuario': 1, 'usuario': 'a', 'fk_asistente': 1}
        s['roles'] = ['ADMIN']
    return c


r = admin().get('/admin/')
t = r.get_data(as_text=True)
v('GET /admin/ -> 200', r.status_code == 200)
v('No hay barra lateral (ni su botón «Menú», ni su script)',
  'admin-sidebar' not in t and 'admin-shell' not in t and 'admin_sidebar.js' not in t and 'data-admin-sidebar' not in t)
v('Falta la línea «Las 37 tablas que expone...»', '37 tablas' not in t and 'Las 37' not in t)

grupos = re.findall(r'<details class="admin-grupo">\s*<summary>.*?<span>(.*?)</span>', t, re.S)
v('Hay 7 categorías desplegables (<details>)', len(grupos) == 7)
v('...y ninguna viene abierta (no llenan la pantalla)', '<details class="admin-grupo" open' not in t)
v('Las categorías son las de siempre, p. ej. «Estructura académica y cátedras»',
  'Estructura académica y cátedras' in grupos and 'Catálogos' in grupos and 'Sesiones y ponentes' in grupos)
v('Los títulos ya NO llevan la versión (sin «v2»...«v7» en ningún título)',
  not re.search(r'<span class="badge badge-primary">v\d</span>', t)
  and not re.search(r'<summary>.*?\bv[1-7]\b.*?</summary>', t, re.S | re.I))
tarjetas = len(re.findall(r'<a class="card text-center', t))
v('Quedan las 39 secciones del panel (con «Periodos y horarios», v7.1 D57) (sin «Calendario del ponente», «Nuevo ponente» ni «Inscripción a cátedra», que ya no existen)',
  tarjetas == 39 and 'Nuevo ponente' not in t and 'Inscripción a cátedra' not in t and 'Calendario del ponente' not in t)
v('Las tarjetas están DENTRO de un <details> (se esconden al cerrar)',
  len(re.findall(r'<a class="card text-center', re.sub(r'<details.*?</details>', '', t, flags=re.S))) == 0)

# Las demás pantallas de /admin tampoco llevan barra lateral
for ruta in ('/admin/usuarios/nuevo', '/admin/catedras/nueva', '/admin/'):
    r = admin().get(ruta)
    h = r.get_data(as_text=True)
    v(f'{ruta}: sin barra lateral y con su contenido', 'admin-sidebar' not in h and 'admin-shell' not in h and r.status_code in (200, 500, 502))

css = open('static/estilo.css', encoding='utf-8').read()
v('estilo.css: sin los estilos de la barra lateral y con los de las categorías', 'admin-sidebar' not in css and '.admin-grupo' in css)
v('Los archivos de la barra lateral ya no existen',
  not any(os.path.exists(f) for f in ('static/admin_sidebar.js', 'templates/admin/_sidebar.html', 'templates/admin/_menu_datos.html')))

print(f'\n{ok} pasadas, {fall} fallidas')
sys.exit(1 if fall else 0)
