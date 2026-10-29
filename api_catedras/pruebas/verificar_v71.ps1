# ==========================================================================
#  verificar_v71.ps1  -  Proyecto Catedras - Subversion v7.1
# --------------------------------------------------------------------------
#  Verificacion contra la API REAL (Docker + PostgreSQL) de lo que agrega la
#  v7.1: roles ESTUDIANTE y PONENTE (RF-S3), estudiante semilla (RF-S4),
#  geolocalizacion obligatoria al validar la clave (RF-S1), cambio de clave
#  (RF-S5) y recuperacion de clave por codigo (RF-S5/RF-S6).
#  Cada caso imprime OK o FALLA; al final da el conteo y sale con codigo 0 si
#  todo paso, 1 si algo fallo.
#
#  Uso (con la API ya levantada, desde la carpeta api_catedras):
#      powershell -ExecutionPolicy Bypass -File .\pruebas\verificar_v71.ps1
#  Otro puerto/host:  ... -File .\pruebas\verificar_v71.ps1 -Api http://127.0.0.1:8142
#
#  REQUISITO: la BD debe tener las migraciones 045-049 (BD nueva con
#  'docker compose up -d --build', o aplicadas a mano; ver README).
#
#  IMPORTANTE: el script ESCRIBE datos (una sesion, un enlace, un registro de
#  asistencia, claves de acceso y codigos de recuperacion). La clave del
#  estudiante semilla queda como estaba ('Cambiar-123'). Para dejar la BD como
#  nueva:  docker compose down -v ; docker compose up -d --build
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

$claveSemilla = "Cambiar-123"
$lat = 6.244203
$lng = -75.581211

# --------------------------------------------------------------------------
Titulo "0. La API responde"
$raiz = Llamar GET "/"
Verificar "GET / responde 200" ($raiz.Codigo -eq 200) $raiz

# --------------------------------------------------------------------------
Titulo "1. RF-S3: los roles ESTUDIANTE y PONENTE existen"
$roles = Llamar GET "/api/Rol"
Verificar "GET /api/Rol -> 200 con ESTUDIANTE y PONENTE" `
    ($roles.Codigo -eq 200 -and (@($roles.Json.datos | ForEach-Object { $_.id_rol }) -contains "ESTUDIANTE") -and (@($roles.Json.datos | ForEach-Object { $_.id_rol }) -contains "PONENTE")) $roles

# --------------------------------------------------------------------------
Titulo "2. RF-S4: el estudiante semilla inicia sesion con su rol"
$login = Llamar POST "/api/Usuario/login" @{ usuario = "alejandro.zuleta"; clave = $claveSemilla }
Verificar "Login de alejandro.zuleta -> 200" ($login.Codigo -eq 200) $login
Verificar "...con el rol ESTUDIANTE y sin ADMIN" (($login.Json.roles -contains "ESTUDIANTE") -and -not ($login.Json.roles -contains "ADMIN")) $login
Verificar "...y la respuesta NO trae ningun hash" ($login.Texto -notmatch "clave_hash" -and $login.Texto -notmatch '\$2[aby]\$') $login
$idUsuario = $login.Json.usuario.id_usuario
$idAsistente = $login.Json.usuario.fk_asistente

$asis = Llamar GET "/api/Asistente/$idAsistente"
Verificar "El asistente es el codigo 30000124724 (Alejandro Zuleta Valderrama)" `
    ($asis.Codigo -eq 200 -and $asis.Json.id_asis -eq "30000124724" -and $asis.Json.nombres -eq "Alejandro" -and $asis.Json.apellidos -eq "Zuleta Valderrama") $asis
Verificar "...con el correo PERSONAL alejandrozuleta81@gmail.com y sin correo institucional" `
    ($asis.Json.correo_personal -eq "alejandrozuleta81@gmail.com" -and -not $asis.Json.correo_institucional) $asis

$progs = Llamar GET "/api/ProgramaAsistente?fkAsistente=$idAsistente"
Verificar "...matriculado en el programa M0286 (Ingenieria de Datos y Software)" `
    ($progs.Codigo -eq 200 -and (@($progs.Json.datos | ForEach-Object { $_.fk_programa }) -contains "M0286")) $progs
$vincs = Llamar GET "/api/VinculacionAsistente?fkAsistente=$idAsistente"
Verificar "...con una vinculacion ESTUDIANTE vigente" `
    ($vincs.Codigo -eq 200 -and @($vincs.Json.datos | Where-Object { $_.fk_tipo_vinculacion -eq "ESTUDIANTE" -and -not $_.fecha_fin }).Count -ge 1) $vincs

# --------------------------------------------------------------------------
Titulo "3. RF-S1: la ubicacion es obligatoria al validar la clave"
$ahora = [DateTimeOffset]::Now
$ses = Llamar POST "/api/Sesion" @{
    fk_catedra = 5498; inicio = $ahora.AddHours(-2).ToString("o"); fin = $ahora.AddHours(2).ToString("o")
    fk_modalidad = "PRESENCIAL"; fk_sede = "SAN_BENITO"; fk_periodo = "2026-2"
}
Verificar "POST /api/Sesion alrededor de AHORA -> 200" ($ses.Codigo -eq 200 -and $ses.Json.id_sesion -gt 0) $ses
$idSesion = $ses.Json.id_sesion
$enl = Llamar POST "/api/EnlaceRegistro" @{ fk_sesion = $idSesion }
Verificar "POST /api/EnlaceRegistro -> 200" ($enl.Codigo -eq 200 -and $enl.Json.id_enlace -gt 0) $enl
$token = (Llamar GET "/api/EnlaceRegistro/$($enl.Json.id_enlace)").Json.token

$sol = Llamar POST "/api/ClaveAcceso/solicitar" @{ identificador = "30000124724"; token = $token }
Verificar "El estudiante semilla pide su clave de acceso por su codigo -> 200" ($sol.Codigo -eq 200 -and $sol.Json.clave -match '^\d{6}$') $sol
Verificar "...y se envia a su correo PERSONAL (no hay institucional)" ($sol.Json.enviado_a -eq "alejandrozuleta81@gmail.com") $sol
$idClave = $sol.Json.id_clave
$claveAcceso = $sol.Json.clave

$sinUbic = Llamar POST "/api/ClaveAcceso/validar" @{ id_clave = $idClave; clave = $claveAcceso }
Verificar "Validar SIN latitud ni longitud -> 422" ($sinUbic.Codigo -eq 422) $sinUbic
$soloLat = Llamar POST "/api/ClaveAcceso/validar" @{ id_clave = $idClave; clave = $claveAcceso; latitud = $lat }
Verificar "Validar con latitud pero SIN longitud -> 422" ($soloLat.Codigo -eq 422) $soloLat
$fuera1 = Llamar POST "/api/ClaveAcceso/validar" @{ id_clave = $idClave; clave = $claveAcceso; latitud = 91; longitud = $lng }
Verificar "Validar con latitud fuera de rango (91) -> 422" ($fuera1.Codigo -eq 422) $fuera1
$fuera2 = Llamar POST "/api/ClaveAcceso/validar" @{ id_clave = $idClave; clave = $claveAcceso; latitud = $lat; longitud = -181 }
Verificar "Validar con longitud fuera de rango (-181) -> 422" ($fuera2.Codigo -eq 422) $fuera2
$claveIntacta = Llamar GET "/api/ClaveAcceso/$idClave"
Verificar "...ninguno de esos rechazos gasto la clave (sigue sin usarse, 0 intentos)" `
    ($claveIntacta.Json.intentos -eq 0 -and -not $claveIntacta.Json.usado_en) $claveIntacta

$val = Llamar POST "/api/ClaveAcceso/validar" @{ id_clave = $idClave; clave = $claveAcceso; ip = "10.0.0.5"; user_agent = "verificar_v71.ps1"; latitud = $lat; longitud = $lng }
Verificar "Validar CON ubicacion -> 200 con id_registro" ($val.Codigo -eq 200 -and $val.Json.id_registro -gt 0) $val
$reg = Llamar GET "/api/RegistroAsistencia/$($val.Json.id_registro)"
Verificar "El registro guardo la latitud y la longitud" `
    ($reg.Codigo -eq 200 -and [math]::Abs([double]$reg.Json.latitud - $lat) -lt 0.000001 -and [math]::Abs([double]$reg.Json.longitud - $lng) -lt 0.000001) $reg
Verificar "...y es de origen QR con la vinculacion ESTUDIANTE de ese momento" ($reg.Json.origen -eq "QR" -and $reg.Json.fk_vinculacion_snapshot -eq "ESTUDIANTE") $reg
$lista = Llamar GET "/api/RegistroAsistencia?fkAsistente=$idAsistente"
Verificar "GET ?fkAsistente= (lo que usa el panel del estudiante) trae el registro con su ubicacion" `
    ($lista.Codigo -eq 200 -and @($lista.Json.datos | Where-Object { $_.id_registro -eq $val.Json.id_registro -and $null -ne $_.latitud }).Count -eq 1) $lista

$man = Llamar POST "/api/RegistroAsistencia" @{ fk_sesion = $idSesion; fk_asistente = 2; origen = "MANUAL" }
Verificar "Un registro MANUAL sigue siendo posible y NO tiene ubicacion (null)" `
    ($man.Codigo -eq 200 -and $null -eq $man.Json.latitud -and $null -eq $man.Json.longitud) $man

# --------------------------------------------------------------------------
Titulo "4. RF-S5: cambiar la clave sabiendo la actual"
$nueva = "Nueva-Clave-71"
$c1 = Llamar POST "/api/Usuario/$idUsuario/cambiar-clave" @{ clave_actual = "equivocada"; clave_nueva = $nueva }
Verificar "Con la clave actual INCORRECTA -> 401" ($c1.Codigo -eq 401) $c1
$c2 = Llamar POST "/api/Usuario/$idUsuario/cambiar-clave" @{ clave_actual = $claveSemilla; clave_nueva = "corta" }
Verificar "Con clave nueva de menos de 8 caracteres -> 422" ($c2.Codigo -eq 422) $c2
$c3 = Llamar POST "/api/Usuario/$idUsuario/cambiar-clave" @{ clave_actual = $claveSemilla; clave_nueva = $claveSemilla }
Verificar "Con la clave nueva IGUAL a la actual -> 400" ($c3.Codigo -eq 400) $c3
$c4 = Llamar POST "/api/Usuario/99999999/cambiar-clave" @{ clave_actual = "x"; clave_nueva = $nueva }
Verificar "De un usuario que no existe -> 404" ($c4.Codigo -eq 404) $c4

$c5 = Llamar POST "/api/Usuario/$idUsuario/cambiar-clave" @{ clave_actual = $claveSemilla; clave_nueva = $nueva }
Verificar "Con la clave actual correcta -> 200" ($c5.Codigo -eq 200) $c5
Verificar "...y la respuesta NO trae ningun hash" ($c5.Texto -notmatch '\$2[aby]\$' -and $c5.Texto -notmatch "clave_hash") $c5
Verificar "El login ya funciona con la clave nueva" ((Llamar POST "/api/Usuario/login" @{ usuario = "alejandro.zuleta"; clave = $nueva }).Codigo -eq 200)
Verificar "...y la clave anterior dejo de servir (401)" ((Llamar POST "/api/Usuario/login" @{ usuario = "alejandro.zuleta"; clave = $claveSemilla }).Codigo -eq 401)

# --------------------------------------------------------------------------
Titulo "5. RF-S5/RF-S6: recuperar una clave olvidada con un codigo de un solo uso"
$sinCuenta = Llamar POST "/api/Usuario/recuperacion/solicitar" @{ identificador = "nadie.existe.por.aqui" }
Verificar "Pedir el codigo de una cuenta que NO existe -> 200 (no revela quien existe, D27)" ($sinCuenta.Codigo -eq 200) $sinCuenta
Verificar "...sin codigo ni correo en la respuesta" ($sinCuenta.Texto -notmatch '"codigo"' -and $sinCuenta.Texto -notmatch "@") $sinCuenta
$ipMala = Llamar POST "/api/Usuario/recuperacion/solicitar" @{ identificador = "alejandro.zuleta"; ip = "999.9.9.9" }
Verificar "Pedir el codigo con una ip invalida -> 400" ($ipMala.Codigo -eq 400) $ipMala
$vacio = Llamar POST "/api/Usuario/recuperacion/solicitar" @{ identificador = "" }
Verificar "Pedir el codigo sin identificador -> 422" ($vacio.Codigo -eq 422) $vacio

$r1 = Llamar POST "/api/Usuario/recuperacion/solicitar" @{ identificador = "alejandro.zuleta"; ip = "10.0.0.9" }
Verificar "Pedir el codigo por NOMBRE DE USUARIO -> 200 con un codigo de 6 digitos" ($r1.Codigo -eq 200 -and $r1.Json.codigo -match '^\d{6}$' -and $r1.Json.id_codigo -gt 0) $r1
Verificar "...que se envia al correo personal del estudiante" ($r1.Json.enviado_a -eq "alejandrozuleta81@gmail.com") $r1

$r2 = Llamar POST "/api/Usuario/recuperacion/solicitar" @{ identificador = "AlejandroZuleta81@gmail.com" }
Verificar "Pedirlo otra vez, ahora por CORREO (sin importar mayusculas) -> 200 con otro codigo" ($r2.Codigo -eq 200 -and $r2.Json.id_codigo -ne $r1.Json.id_codigo) $r2
$viejo = Llamar POST "/api/Usuario/recuperacion/restablecer" @{ id_codigo = $r1.Json.id_codigo; codigo = $r1.Json.codigo; clave_nueva = "Recuperada-71" }
Verificar "El codigo ANTERIOR quedo anulado al pedir otro -> 400" ($viejo.Codigo -eq 400) $viejo

$mal = Llamar POST "/api/Usuario/recuperacion/restablecer" @{ id_codigo = $r2.Json.id_codigo; codigo = "000000"; clave_nueva = "Recuperada-71" }
Verificar "Restablecer con un codigo INCORRECTO -> 400" ($mal.Codigo -eq 400) $mal
$corta = Llamar POST "/api/Usuario/recuperacion/restablecer" @{ id_codigo = $r2.Json.id_codigo; codigo = $r2.Json.codigo; clave_nueva = "corta" }
Verificar "Restablecer con clave nueva de menos de 8 caracteres -> 422" ($corta.Codigo -eq 422) $corta

$ok = Llamar POST "/api/Usuario/recuperacion/restablecer" @{ id_codigo = $r2.Json.id_codigo; codigo = $r2.Json.codigo; clave_nueva = "Recuperada-71" }
Verificar "Restablecer con el codigo correcto -> 200" ($ok.Codigo -eq 200) $ok
Verificar "El login ya funciona con la clave recuperada" ((Llamar POST "/api/Usuario/login" @{ usuario = "alejandro.zuleta"; clave = "Recuperada-71" }).Codigo -eq 200)
Verificar "...y la clave de antes del olvido dejo de servir (401)" ((Llamar POST "/api/Usuario/login" @{ usuario = "alejandro.zuleta"; clave = $nueva }).Codigo -eq 401)
$otraVez = Llamar POST "/api/Usuario/recuperacion/restablecer" @{ id_codigo = $r2.Json.id_codigo; codigo = $r2.Json.codigo; clave_nueva = "Otra-Clave-71" }
Verificar "Usar el MISMO codigo otra vez -> 400 (un solo uso)" ($otraVez.Codigo -eq 400) $otraVez

# Tres intentos fallidos anulan el codigo (CLAVE_MAX_INTENTOS = 3). A diferencia de la clave de
# acceso de la v5 (RF153), aqui el contador SI se acumula: el SP devuelve un estado en vez de
# lanzar una excepcion que deshaga el UPDATE.
$r3 = Llamar POST "/api/Usuario/recuperacion/solicitar" @{ identificador = "alejandro.zuleta" }
foreach ($i in 1..3) { $null = Llamar POST "/api/Usuario/recuperacion/restablecer" @{ id_codigo = $r3.Json.id_codigo; codigo = "000000"; clave_nueva = "Otra-Clave-71" } }
$tras = Llamar POST "/api/Usuario/recuperacion/restablecer" @{ id_codigo = $r3.Json.id_codigo; codigo = $r3.Json.codigo; clave_nueva = "Otra-Clave-71" }
Verificar "Tras 3 intentos fallidos el codigo queda anulado, aun con el valor correcto -> 400" ($tras.Codigo -eq 400) $tras
Verificar "...y la clave NO cambio" ((Llamar POST "/api/Usuario/login" @{ usuario = "alejandro.zuleta"; clave = "Recuperada-71" }).Codigo -eq 200)

# --------------------------------------------------------------------------
Titulo "6. Se deja la cuenta semilla como estaba"
$resto = Llamar POST "/api/Usuario/$idUsuario/cambiar-clave" @{ clave_actual = "Recuperada-71"; clave_nueva = $claveSemilla }
Verificar "La clave del estudiante semilla vuelve a la temporal" ($resto.Codigo -eq 200) $resto
Verificar "...y el login con ella funciona" ((Llamar POST "/api/Usuario/login" @{ usuario = "alejandro.zuleta"; clave = $claveSemilla }).Codigo -eq 200)

# --------------------------------------------------------------------------
Titulo "7. D50: inscripcion de una persona a una catedra"
$null = Llamar POST "/api/InscripcionCatedra/cancelar" @{ fk_catedra = 5498; fk_asistente = $idAsistente }   # punto de partida limpio
$i1 = Llamar POST "/api/InscripcionCatedra" @{ fk_catedra = 5498; fk_asistente = $idAsistente }
Verificar "Inscribirse en una catedra activa -> 200 'inscrita'" ($i1.Codigo -eq 200 -and $i1.Json.estado -eq "inscrita") $i1
$i2 = Llamar POST "/api/InscripcionCatedra" @{ fk_catedra = 5498; fk_asistente = $idAsistente }
Verificar "Inscribirse OTRA VEZ no duplica -> 200 'ya_inscrita'" ($i2.Codigo -eq 200 -and $i2.Json.estado -eq "ya_inscrita") $i2
$il = Llamar GET "/api/InscripcionCatedra?fkAsistente=$idAsistente&fkCatedra=5498"
Verificar "El listado trae UNA inscripcion ACTIVA con el nombre de la catedra y de la persona" `
    ($il.Codigo -eq 200 -and $il.Json.total -eq 1 -and $il.Json.datos[0].estado -eq "ACTIVA" -and $il.Json.datos[0].nombre_catedra -and $il.Json.datos[0].nombres -eq "Alejandro") $il
Verificar "Catedra inexistente -> 404" ((Llamar POST "/api/InscripcionCatedra" @{ fk_catedra = 99999999; fk_asistente = $idAsistente }).Codigo -eq 404)
Verificar "Persona inexistente -> 404" ((Llamar POST "/api/InscripcionCatedra" @{ fk_catedra = 5498; fk_asistente = 99999999 }).Codigo -eq 404)
Verificar "Sin campos -> 422" ((Llamar POST "/api/InscripcionCatedra" @{}).Codigo -eq 422)
$ic = Llamar POST "/api/InscripcionCatedra/cancelar" @{ fk_catedra = 5498; fk_asistente = $idAsistente }
Verificar "Cancelar -> 200 'cancelada'" ($ic.Codigo -eq 200 -and $ic.Json.estado -eq "cancelada") $ic
Verificar "Cancelar sin inscripcion activa -> 404" ((Llamar POST "/api/InscripcionCatedra/cancelar" @{ fk_catedra = 5498; fk_asistente = $idAsistente }).Codigo -eq 404)
Verificar "Ya no sale entre las ACTIVAS (204)" ((Llamar GET "/api/InscripcionCatedra?fkAsistente=$idAsistente&fkCatedra=5498").Codigo -eq 204)
$it = Llamar GET "/api/InscripcionCatedra?fkAsistente=$idAsistente&fkCatedra=5498&soloActivas=false"
Verificar "...pero se conserva como CANCELADA con soloActivas=false" ($it.Codigo -eq 200 -and $it.Json.datos[0].estado -eq "CANCELADA") $it
$ir = Llamar POST "/api/InscripcionCatedra" @{ fk_catedra = 5498; fk_asistente = $idAsistente }
Verificar "Volver a inscribirse la reactiva -> 200 'inscrita'" ($ir.Codigo -eq 200 -and $ir.Json.estado -eq "inscrita") $ir
$null = Llamar POST "/api/InscripcionCatedra/cancelar" @{ fk_catedra = 5498; fk_asistente = $idAsistente }   # se deja sin inscripcion

# --------------------------------------------------------------------------
Write-Host ""
$color = if ($script:fallidas -eq 0) { "Green" } else { "Red" }
Write-Host "$($script:pasadas) pasadas, $($script:fallidas) fallidas." -ForegroundColor $color
if ($script:fallidas -gt 0) { exit 1 } else { exit 0 }
