# ==========================================================================
#  verificar_v5.ps1  -  Proyecto Catedras - Version 5 - Fase 44
# --------------------------------------------------------------------------
#  Verificacion contra la API REAL (Docker + PostgreSQL). Recorre en orden los
#  pasos del quickstart (7_quickstart.md, seccion 2) y de la Fase 44
#  (8_tasks.md): login, roles, enlaces, claves, registro manual, RESTRICT y el
#  limite conocido RF153. Cada caso imprime OK o FALLA; al final da el conteo y
#  sale con codigo 0 si todo paso, 1 si algo fallo.
#
#  Uso (con la API ya levantada, desde la carpeta api_catedras):
#      powershell -ExecutionPolicy Bypass -File .\pruebas\verificar_v5.ps1
#  Otro puerto/host:  ... -File .\pruebas\verificar_v5.ps1 -Api http://127.0.0.1:8142
#
#  v7.1 (RF-S1): 'ClaveAcceso/validar' ahora exige latitud y longitud; las
#  llamadas de este script las mandan. La verificacion propia de la v7.1 esta
#  en verificar_v71.ps1.
#
#  IMPORTANTE: el script ESCRIBE datos (usuarios, sesion, enlaces, registros).
#  Al terminar, para dejar la BD como nueva:
#      docker compose down -v ; docker compose up -d --build
#  (se escribe sin tildes a proposito: PowerShell 5.1 lee mal los .ps1 sin BOM)
# ==========================================================================
param([string]$Api = "http://127.0.0.1:8142")

$ErrorActionPreference = "Stop"
$script:pasadas = 0
$script:fallidas = 0

# Llama a la API y devuelve { Codigo, Texto, Json } SIN lanzar excepcion en 4xx/5xx.
function Llamar {
    param([string]$Metodo, [string]$Ruta, $Cuerpo = $null)

    $parametros = @{ Method = $Metodo; Uri = "$Api$Ruta"; UseBasicParsing = $true }
    if ($null -ne $Cuerpo) {
        $parametros.ContentType = "application/json; charset=utf-8"
        $parametros.Body = [System.Text.Encoding]::UTF8.GetBytes(($Cuerpo | ConvertTo-Json -Compress))
    }

    $texto = $null
    try {
        $r = Invoke-WebRequest @parametros
        $codigo = [int]$r.StatusCode
        $texto = $r.Content
    }
    catch {
        if ($null -eq $_.Exception.Response) { throw }          # no hubo respuesta (API apagada)
        $codigo = [int]$_.Exception.Response.StatusCode
        $texto = $_.ErrorDetails.Message
        if (-not $texto) {
            try {
                $lector = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
                $texto = $lector.ReadToEnd()
            } catch { }
        }
    }

    $json = $null
    if ($texto) { try { $json = $texto | ConvertFrom-Json } catch { } }
    [pscustomobject]@{ Codigo = $codigo; Texto = $texto; Json = $json }
}

function Verificar {
    # $Condicion sin tipo: algunas comprobaciones pasan un texto (p. ej. una fecha);
    # el [bool] de abajo lo vuelve verdadero si trae algo y falso si esta vacio o es $null.
    param([string]$Caso, $Condicion, $Respuesta = $null)
    if ([bool]$Condicion) {
        Write-Host "  OK    $Caso" -ForegroundColor Green
        $script:pasadas++
    }
    else {
        Write-Host "  FALLA $Caso" -ForegroundColor Red
        if ($null -ne $Respuesta) { Write-Host "        -> HTTP $($Respuesta.Codigo): $($Respuesta.Texto)" -ForegroundColor Red }
        $script:fallidas++
    }
}

function Titulo([string]$t) { Write-Host "`n== $t ==" -ForegroundColor Cyan }

# --------------------------------------------------------------------------
Titulo "0. La API responde y es la v5 o posterior"
$raiz = Llamar GET "/"
# Retrofit fase 4: la API ya dice "v6"; exigir "v5" exacto hacia fallar esta
# regresion por algo que no es un error. Se acepta v5 o cualquier version posterior.
Verificar "GET / responde 200 con version v5 o posterior ($($raiz.Json.version))" ($raiz.Codigo -eq 200 -and $raiz.Json.version -match '^v([5-9]|[1-9][0-9])$') $raiz

# --------------------------------------------------------------------------
Titulo "1. Login: clave correcta (200), incorrecta (401), usuario inexistente (401)"
$r = Llamar PATCH "/api/Usuario/1" @{ clave = "Admin12345" }
Verificar "PATCH /api/Usuario/1 fija la clave del administrador de ejemplo (200)" ($r.Codigo -eq 200) $r

$login = Llamar POST "/api/Usuario/login" @{ usuario = "admin.catedras"; clave = "Admin12345" }
Verificar "Login correcto -> 200" ($login.Codigo -eq 200) $login
Verificar "...con los roles vigentes ADMIN y CONSULTA" (($login.Json.roles -contains "ADMIN") -and ($login.Json.roles -contains "CONSULTA")) $login
Verificar "...y la respuesta NO trae ningun hash" ($login.Texto -notmatch "clave_hash" -and $login.Texto -notmatch '\$2[aby]\$') $login

$mala = Llamar POST "/api/Usuario/login" @{ usuario = "admin.catedras"; clave = "ClaveIncorrecta" }
Verificar "Login con clave incorrecta -> 401" ($mala.Codigo -eq 401) $mala
$noExiste = Llamar POST "/api/Usuario/login" @{ usuario = "no.existe.nadie"; clave = "Admin12345" }
Verificar "Login con usuario inexistente -> 401" ($noExiste.Codigo -eq 401) $noExiste
Verificar "...mismo mensaje en ambos casos (no revela la causa, D27)" ($mala.Texto -eq $noExiste.Texto) $mala

# --------------------------------------------------------------------------
Titulo "2. Crear usuario: el hash nunca se expone (criterio 2)"
$corta = Llamar POST "/api/Usuario" @{ fk_asistente = 100; usuario = "prueba.login"; clave = "corta" }
Verificar "Clave de menos de 8 caracteres -> 422" ($corta.Codigo -eq 422) $corta

$crear = Llamar POST "/api/Usuario" @{ fk_asistente = 100; usuario = "prueba.login"; clave = "Secreta123" }
Verificar "POST /api/Usuario -> 200 con id_usuario" ($crear.Codigo -eq 200 -and $crear.Json.id_usuario -gt 0) $crear
Verificar "...la respuesta del POST no trae ninguna clave ni hash" ($crear.Texto -notmatch "clave" -and $crear.Texto -notmatch '\$2[aby]\$') $crear
$idUsuario = $crear.Json.id_usuario

$get = Llamar GET "/api/Usuario/$idUsuario"
Verificar "GET /api/Usuario/{id} -> 200 y sin clave_hash" ($get.Codigo -eq 200 -and $get.Texto -notmatch "clave_hash" -and $get.Json.usuario -eq "prueba.login") $get

$dup = Llamar POST "/api/Usuario" @{ fk_asistente = 100; usuario = "otro.nombre"; clave = "Secreta123" }
Verificar "Segundo usuario para el MISMO asistente -> 500 (uq_usuario_persona)" ($dup.Codigo -eq 500) $dup

$loginNuevo = Llamar POST "/api/Usuario/login" @{ usuario = "prueba.login"; clave = "Secreta123" }
Verificar "Login del usuario recien creado (sin roles) -> 401: desde la v7.1 (D55) SOLO el administrador inicia sesion" ($loginNuevo.Codigo -eq 401) $loginNuevo
Verificar "...con el mismo mensaje que los otros 401 (D27)" ($loginNuevo.Texto -eq $mala.Texto) $loginNuevo

$inact = Llamar PATCH "/api/Usuario/$idUsuario" @{ activo = $false }
Verificar "PATCH activo=false -> 200" ($inact.Codigo -eq 200) $inact
$loginInact = Llamar POST "/api/Usuario/login" @{ usuario = "prueba.login"; clave = "Secreta123" }
Verificar "Login de una cuenta INACTIVA (clave correcta) -> 401" ($loginInact.Codigo -eq 401) $loginInact
Verificar "...con el mismo mensaje que los otros 401 (D27)" ($loginInact.Texto -eq $mala.Texto) $loginInact
$reac = Llamar PATCH "/api/Usuario/$idUsuario" @{ activo = $true }

# --------------------------------------------------------------------------
Titulo "3. RolPorUsuario: abrir, cerrar, repetir el cierre (D16)"
$abrir = Llamar POST "/api/RolPorUsuario" @{ fk_usuario = $idUsuario; fk_rol = "CONSULTA" }
Verificar "POST /api/RolPorUsuario -> 200" ($abrir.Codigo -eq 200) $abrir
# Se saca la fecha del TEXTO crudo: PowerShell 7 convertiria "2026-10-11" en DateTime al parsear el JSON.
$fechaIni = [regex]::Match($abrir.Texto, '"fecha_ini":"([0-9-]+)"').Groups[1].Value

$lista = Llamar GET "/api/RolPorUsuario?fkUsuario=$idUsuario"
Verificar "GET ?fkUsuario= lista el rol abierto" ($lista.Codigo -eq 200 -and $lista.Json.total -eq 1) $lista

$loginRol = Llamar POST "/api/Usuario/login" @{ usuario = "prueba.login"; clave = "Secreta123" }
Verificar "Con el rol CONSULTA tampoco entra (solo ADMIN inicia sesion, D55) -> 401" ($loginRol.Codigo -eq 401) $loginRol

$cerrar = Llamar PATCH "/api/RolPorUsuario/cerrar" @{ fk_usuario = $idUsuario; fk_rol = "CONSULTA"; fecha_ini = $fechaIni }
Verificar "PATCH /api/RolPorUsuario/cerrar -> 200" ($cerrar.Codigo -eq 200) $cerrar
$cerrar2 = Llamar PATCH "/api/RolPorUsuario/cerrar" @{ fk_usuario = $idUsuario; fk_rol = "CONSULTA"; fecha_ini = $fechaIni }
Verificar "Repetir el cierre -> 400" ($cerrar2.Codigo -eq 400) $cerrar2
$cerrar3 = Llamar PATCH "/api/RolPorUsuario/cerrar" @{ fk_usuario = $idUsuario; fk_rol = "ADMIN"; fecha_ini = "2020-01-01" }
Verificar "Cerrar una llave inexistente -> 404" ($cerrar3.Codigo -eq 404) $cerrar3
$sinRuta = Llamar DELETE "/api/RolPorUsuario/1"
Verificar "DELETE /api/RolPorUsuario/1 no existe (404 o 405)" ($sinRuta.Codigo -eq 404 -or $sinRuta.Codigo -eq 405) $sinRuta

# --------------------------------------------------------------------------
Titulo "4. Sesion nueva alrededor de AHORA + dos enlaces (RN-14)"
$ahora = [DateTimeOffset]::Now
$ses = Llamar POST "/api/Sesion" @{
    fk_catedra = 5498; inicio = $ahora.AddHours(-2).ToString("o"); fin = $ahora.AddHours(2).ToString("o")
    fk_modalidad = "PRESENCIAL"; fk_sede = "SAN_BENITO"; fk_periodo = "2026-2"
}
Verificar "POST /api/Sesion (2 h antes y 2 h despues de ahora) -> 200" ($ses.Codigo -eq 200 -and $ses.Json.id_sesion -gt 0) $ses
$idSesion = $ses.Json.id_sesion

$enlA = Llamar POST "/api/EnlaceRegistro" @{ fk_sesion = $idSesion }
Verificar "POST /api/EnlaceRegistro (primero) -> 200 con id_enlace" ($enlA.Codigo -eq 200 -and $enlA.Json.id_enlace -gt 0) $enlA
$idA = $enlA.Json.id_enlace
$getA1 = Llamar GET "/api/EnlaceRegistro/$idA"
Verificar "GET del enlace: canal QR decidido por la BD, ventana y token presentes" `
    ($getA1.Codigo -eq 200 -and $getA1.Json.canal -eq "QR" -and $getA1.Json.token -and $getA1.Json.ventana_desde -and $getA1.Json.ventana_hasta -and -not $getA1.Json.revocado_en) $getA1
$tokenA = $getA1.Json.token

$enlB = Llamar POST "/api/EnlaceRegistro" @{ fk_sesion = $idSesion }
Verificar "POST /api/EnlaceRegistro (segundo, misma sesion) -> 200 con otro id" ($enlB.Codigo -eq 200 -and $enlB.Json.id_enlace -ne $idA) $enlB
$idB = $enlB.Json.id_enlace
$getA2 = Llamar GET "/api/EnlaceRegistro/$idA"
Verificar "RN-14: el PRIMER enlace quedo revocado" ($getA2.Json.revocado_en) $getA2
$getB = Llamar GET "/api/EnlaceRegistro/$idB"
Verificar "...y el segundo esta vigente" (-not $getB.Json.revocado_en) $getB
$tokenB = $getB.Json.token

$enlMal = Llamar POST "/api/EnlaceRegistro" @{ fk_sesion = 99999999 }
Verificar "Emitir para una sesion inexistente -> 500 (RAISE de la BD, D25)" ($enlMal.Codigo -eq 500) $enlMal
$enlSinId = Llamar POST "/api/EnlaceRegistro" @{ minutos_antes = 10 }
Verificar "Emitir sin fk_sesion -> 422" ($enlSinId.Codigo -eq 422) $enlSinId

# --------------------------------------------------------------------------
Titulo "5. Solicitar y validar una clave (criterio 5)"
$solMal = Llamar POST "/api/ClaveAcceso/solicitar" @{ identificador = "1000090"; token = $tokenA }
Verificar "Solicitar con el token del enlace REVOCADO -> 500 (RN-15)" ($solMal.Codigo -eq 500) $solMal
$solTok = Llamar POST "/api/ClaveAcceso/solicitar" @{ identificador = "1000090"; token = "no-es-un-uuid" }
Verificar "Solicitar con un token que no es uuid -> 400" ($solTok.Codigo -eq 400) $solTok
$solIp = Llamar POST "/api/ClaveAcceso/solicitar" @{ identificador = "1000090"; token = $tokenB; ip = "999.9.9.9" }
Verificar "Solicitar con una ip invalida -> 400" ($solIp.Codigo -eq 400) $solIp
$solNadie = Llamar POST "/api/ClaveAcceso/solicitar" @{ identificador = "identificador-que-no-existe"; token = $tokenB }
Verificar "Solicitar con un identificador que no resuelve a nadie -> 500 (RN-01)" ($solNadie.Codigo -eq 500) $solNadie

$sol = Llamar POST "/api/ClaveAcceso/solicitar" @{ identificador = "1000090"; token = $tokenB; ip = "10.0.0.5" }
Verificar "Solicitar con el enlace vigente -> 200" ($sol.Codigo -eq 200) $sol
Verificar "...devuelve una clave de 6 digitos en texto plano (RNF12) y el correo de envio" `
    ($sol.Json.clave -match '^\d{6}$' -and $sol.Json.enviado_a -match '@') $sol
$idClave = $sol.Json.id_clave
$claveOk = $sol.Json.clave

$claveGet = Llamar GET "/api/ClaveAcceso/$idClave"
Verificar "GET /api/ClaveAcceso/{id} NO trae ni clave_hash ni la clave" `
    ($claveGet.Codigo -eq 200 -and $claveGet.Texto -notmatch "clave_hash" -and $claveGet.Texto -notmatch $claveOk) $claveGet

$val = Llamar POST "/api/ClaveAcceso/validar" @{ id_clave = $idClave; clave = $claveOk; ip = "10.0.0.5"; user_agent = "verificar_v5.ps1"; latitud = 6.244203; longitud = -75.581211 }
Verificar "Validar con la clave correcta -> 200 con id_registro" ($val.Codigo -eq 200 -and $val.Json.id_registro -gt 0) $val
$reg = Llamar GET "/api/RegistroAsistencia/$($val.Json.id_registro)"
Verificar "GET del registro: origen QR y fk_vinculacion_snapshot resuelto por el trigger" `
    ($reg.Codigo -eq 200 -and $reg.Json.origen -eq "QR" -and $reg.Json.fk_vinculacion_snapshot) $reg
$val2 = Llamar POST "/api/ClaveAcceso/validar" @{ id_clave = $idClave; clave = $claveOk; latitud = 6.244203; longitud = -75.581211 }
Verificar "Repetir el mismo validar -> 500 (clave ya usada, RN-20)" ($val2.Codigo -eq 500) $val2

# --------------------------------------------------------------------------
Titulo "6. Limite conocido RF153: los intentos NO se acumulan"
$sol2 = Llamar POST "/api/ClaveAcceso/solicitar" @{ identificador = "1010372"; token = $tokenB }
Verificar "Pedir otra clave (asistente 100, id_asis 1010372) -> 200" ($sol2.Codigo -eq 200) $sol2
$idClave2 = $sol2.Json.id_clave
$todos500 = $true
foreach ($i in 1..4) {
    $m = Llamar POST "/api/ClaveAcceso/validar" @{ id_clave = $idClave2; clave = "000000"; latitud = 6.244203; longitud = -75.581211 }
    if ($m.Codigo -ne 500) { $todos500 = $false }
}
Verificar "4 validar con clave incorrecta -> 500 cada uno" $todos500
$claveGet2 = Llamar GET "/api/ClaveAcceso/$idClave2"
Verificar "GET /api/ClaveAcceso/{id} sigue mostrando intentos = 0 (la excepcion deshace el contador)" `
    ($claveGet2.Json.intentos -eq 0) $claveGet2
$valFinal = Llamar POST "/api/ClaveAcceso/validar" @{ id_clave = $idClave2; clave = $sol2.Json.clave; latitud = 6.244203; longitud = -75.581211 }
Verificar "...y la clave CORRECTA todavia se acepta -> 200" ($valFinal.Codigo -eq 200) $valFinal

# --------------------------------------------------------------------------
Titulo "7. Registro manual y el bloqueo de origen (criterio 6)"
foreach ($origen in @("QR", "ENLACE", "manual")) {
    $x = Llamar POST "/api/RegistroAsistencia" @{ fk_sesion = $idSesion; fk_asistente = 2; origen = $origen }
    Verificar "POST /api/RegistroAsistencia con origen '$origen' -> 400 (D26)" ($x.Codigo -eq 400) $x
}
$sinOrigen = Llamar POST "/api/RegistroAsistencia" @{ fk_sesion = $idSesion; fk_asistente = 2 }
Verificar "Sin 'origen' -> 422" ($sinOrigen.Codigo -eq 422) $sinOrigen

$man = Llamar POST "/api/RegistroAsistencia" @{ fk_sesion = $idSesion; fk_asistente = 2; origen = "MANUAL" }
Verificar "Con origen MANUAL -> 200" ($man.Codigo -eq 200 -and $man.Json.id_registro -gt 0) $man
Verificar "...con la instantanea de vinculacion que calculo el trigger" ($man.Json.fk_vinculacion_snapshot) $man
$man2 = Llamar POST "/api/RegistroAsistencia" @{ fk_sesion = $idSesion; fk_asistente = 2; origen = "MANUAL" }
Verificar "Registrar al mismo asistente dos veces en la sesion -> 500 (uq_registro_sesion_asis)" ($man2.Codigo -eq 500) $man2

$lstS = Llamar GET "/api/RegistroAsistencia?fkSesion=$idSesion"
Verificar "GET ?fkSesion= lista los 3 registros de la sesion (QR, QR, MANUAL)" ($lstS.Codigo -eq 200 -and $lstS.Json.total -eq 3) $lstS
$put = Llamar PUT "/api/RegistroAsistencia/$($man.Json.id_registro)" @{ origen = "MANUAL" }
Verificar "PUT /api/RegistroAsistencia/{id} no existe (404 o 405, RF157)" ($put.Codigo -eq 404 -or $put.Codigo -eq 405) $put

# --------------------------------------------------------------------------
Titulo "8. Asistente con registros: borrado LOGICO (retrofit fase 4) y revocar un enlace"
# ANTES del retrofit: DELETE fisico -> RESTRICT de registro_asistencia -> 500.
# AHORA (regla 3 del retrofit: la tabla tiene 'activo'): sp_eliminar_asistente
# hace activo=false. Ya no hay nada que RESTRICT pueda bloquear, y los
# registros de asistencia quedan intactos.
$del = Llamar DELETE "/api/Asistente/1"
Verificar "DELETE /api/Asistente/1 (tiene registro_asistencia) -> 200, borrado logico" ($del.Codigo -eq 200) $del
$sigue = Llamar GET "/api/Asistente/1"
Verificar "...y el asistente 1 sigue existiendo, con activo=false" ($sigue.Codigo -eq 200 -and $sigue.Json.activo -eq $false) $sigue
$regs = Llamar GET "/api/RegistroAsistencia?fkAsistente=1"
Verificar "...y sus registros de asistencia siguen ahi" ($regs.Codigo -eq 200 -and $regs.Json.total -ge 1) $regs

$enlC = Llamar POST "/api/EnlaceRegistro" @{ fk_sesion = $idSesion; minutos_antes = 15; minutos_despues = 15 }
$idC = $enlC.Json.id_enlace
$rev = Llamar PATCH "/api/EnlaceRegistro/$idC/revocar"
Verificar "PATCH /api/EnlaceRegistro/{id}/revocar -> 200" ($rev.Codigo -eq 200) $rev
$rev2 = Llamar PATCH "/api/EnlaceRegistro/$idC/revocar"
Verificar "Revocar otra vez -> 400" ($rev2.Codigo -eq 400) $rev2
$rev3 = Llamar PATCH "/api/EnlaceRegistro/99999999/revocar"
Verificar "Revocar un enlace inexistente -> 404" ($rev3.Codigo -eq 404) $rev3
$delEnl = Llamar DELETE "/api/EnlaceRegistro/$idC"
Verificar "DELETE /api/EnlaceRegistro/{id} no existe (404 o 405, RF147)" ($delEnl.Codigo -eq 404 -or $delEnl.Codigo -eq 405) $delEnl

# --------------------------------------------------------------------------
Write-Host "`n$($script:pasadas) pasadas, $($script:fallidas) fallidas." -ForegroundColor Cyan
Write-Host "Para dejar la BD como nueva:  docker compose down -v ; docker compose up -d --build"
if ($script:fallidas -eq 0) { exit 0 } else { exit 1 }
