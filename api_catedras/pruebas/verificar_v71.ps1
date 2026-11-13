# ==========================================================================
#  verificar_v71.ps1  -  Proyecto Catedras - Subversion v7.1
# --------------------------------------------------------------------------
#  Verificacion contra la API REAL (Docker + PostgreSQL) de lo que agrega la
#  v7.1: roles ESTUDIANTE y PONENTE en el catalogo, estudiante semilla como PERSONA (RF-S4),
#  geolocalizacion obligatoria al validar la clave (RF-S1), cambio de clave
#  (RF-S5), recuperacion de clave por codigo (RF-S5/RF-S6), disponibilidad de horario (D52),
#  QR de cierre de sesion (D53) y, desde la D55, SOLO EL ADMINISTRADOR INICIA SESION, sin
#  inscripcion ni cupo. Las pruebas de claves usan una cuenta ADMIN de prueba que el propio
#  script crea (con un sufijo al azar, asi que se puede repetir).
#  Cada caso imprime OK o FALLA; al final da el conteo y sale con codigo 0 si
#  todo paso, 1 si algo fallo.
#
#  Uso (con la API ya levantada, desde la carpeta api_catedras):
#      powershell -ExecutionPolicy Bypass -File .\pruebas\verificar_v71.ps1
#  Otro puerto/host:  ... -File .\pruebas\verificar_v71.ps1 -Api http://127.0.0.1:8142
#
#  REQUISITO: la BD debe tener las migraciones 045-054 (BD nueva con
#  'docker compose up -d --build', o aplicadas a mano; ver README).
#
#  IMPORTANTE: el script ESCRIBE datos (una sesion, un enlace, un registro de
#  asistencia, claves de acceso, codigos de recuperacion y cuentas ADMIN de prueba). Para dejar la BD como
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

$sufijoAdm = "{0:D6}" -f (Get-Random -Minimum 0 -Maximum 999999)
$usuarioPrueba = "adm.prueba$sufijoAdm"                # cuenta ADMIN de prueba (la crea la seccion 2)
$correoAdm = "adm.prueba$sufijoAdm@ejemplo.com"
$claveSemilla = "Clave-Admin-71"
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
Titulo "2. RF-S4 / D55: el estudiante semilla es una PERSONA; solo el administrador inicia sesion"
$busca = Llamar GET "/api/Asistente?q=30000124724&limite=5"
$asis = @($busca.Json.datos | Where-Object { $_.id_asis -eq "30000124724" })[0]
$idAsistente = $asis.id_asistente
Verificar "El asistente es el codigo 30000124724 (Alejandro Zuleta Valderrama)" `
    ($busca.Codigo -eq 200 -and $asis.nombres -eq "Alejandro" -and $asis.apellidos -eq "Zuleta Valderrama") $busca
Verificar "...con el correo PERSONAL alejandrozuleta81@gmail.com y sin correo institucional" `
    ($asis.correo_personal -eq "alejandrozuleta81@gmail.com" -and -not $asis.correo_institucional) $busca

$progs = Llamar GET "/api/ProgramaAsistente?fkAsistente=$idAsistente"
Verificar "...matriculado en el programa M0286 (Ingenieria de Datos y Software)" `
    ($progs.Codigo -eq 200 -and (@($progs.Json.datos | ForEach-Object { $_.fk_programa }) -contains "M0286")) $progs
$vincs = Llamar GET "/api/VinculacionAsistente?fkAsistente=$idAsistente"
Verificar "...con una vinculacion ESTUDIANTE vigente" `
    ($vincs.Codigo -eq 200 -and @($vincs.Json.datos | Where-Object { $_.fk_tipo_vinculacion -eq "ESTUDIANTE" -and -not $_.fecha_fin }).Count -ge 1) $vincs

# Solo el administrador inicia sesion: la cuenta de la semilla (si existe) tiene rol ESTUDIANTE y NO entra.
Verificar "La cuenta de estudiante de la semilla (alejandro.zuleta, con su codigo o con 'Cambiar-123') NO inicia sesion -> 401" `
    ((Llamar POST "/api/Usuario/login" @{ usuario = "alejandro.zuleta"; clave = "30000124724" }).Codigo -eq 401 -and
     (Llamar POST "/api/Usuario/login" @{ usuario = "alejandro.zuleta"; clave = "Cambiar-123" }).Codigo -eq 401)

# Cuenta ADMIN de prueba (persona + usuario + rol ADMIN), para las pruebas de claves
$admP = Llamar POST "/api/Asistente" @{ nombres = "Admin"; apellidos = "De Prueba $sufijoAdm"; correo_personal = $correoAdm; es_externo = $true }
$admU = Llamar POST "/api/Usuario" @{ fk_asistente = $admP.Json.id_asistente; usuario = $usuarioPrueba; clave = $claveSemilla }
$null = Llamar POST "/api/RolPorUsuario" @{ fk_usuario = $admU.Json.id_usuario; fk_rol = "ADMIN" }
$idUsuario = $admU.Json.id_usuario
$login = Llamar POST "/api/Usuario/login" @{ usuario = $usuarioPrueba; clave = $claveSemilla }
Verificar "Una cuenta con el rol ADMIN inicia sesion -> 200" ($login.Codigo -eq 200 -and ($login.Json.roles -contains "ADMIN")) $login
Verificar "...y la respuesta NO trae ningun hash" ($login.Texto -notmatch "clave_hash" -and $login.Texto -notmatch '\$2[aby]\$') $login

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
Verificar "El login ya funciona con la clave nueva" ((Llamar POST "/api/Usuario/login" @{ usuario = $usuarioPrueba; clave = $nueva }).Codigo -eq 200)
Verificar "...y la clave anterior dejo de servir (401)" ((Llamar POST "/api/Usuario/login" @{ usuario = $usuarioPrueba; clave = $claveSemilla }).Codigo -eq 401)

# --------------------------------------------------------------------------
Titulo "5. RF-S5/RF-S6: recuperar una clave olvidada con un codigo de un solo uso"
$sinCuenta = Llamar POST "/api/Usuario/recuperacion/solicitar" @{ identificador = "nadie.existe.por.aqui" }
Verificar "Pedir el codigo de una cuenta que NO existe -> 200 (no revela quien existe, D27)" ($sinCuenta.Codigo -eq 200) $sinCuenta
Verificar "...sin codigo ni correo en la respuesta" ($sinCuenta.Texto -notmatch '"codigo"' -and $sinCuenta.Texto -notmatch "@") $sinCuenta
$ipMala = Llamar POST "/api/Usuario/recuperacion/solicitar" @{ identificador = $usuarioPrueba; ip = "999.9.9.9" }
Verificar "Pedir el codigo con una ip invalida -> 400" ($ipMala.Codigo -eq 400) $ipMala
$vacio = Llamar POST "/api/Usuario/recuperacion/solicitar" @{ identificador = "" }
Verificar "Pedir el codigo sin identificador -> 422" ($vacio.Codigo -eq 422) $vacio

$r1 = Llamar POST "/api/Usuario/recuperacion/solicitar" @{ identificador = $usuarioPrueba; ip = "10.0.0.9" }
Verificar "Pedir el codigo por NOMBRE DE USUARIO -> 200 con un codigo de 6 digitos" ($r1.Codigo -eq 200 -and $r1.Json.codigo -match '^\d{6}$' -and $r1.Json.id_codigo -gt 0) $r1
Verificar "...que se envia al correo personal de la persona" ($r1.Json.enviado_a -eq $correoAdm) $r1

$r2 = Llamar POST "/api/Usuario/recuperacion/solicitar" @{ identificador = $correoAdm.ToUpper() }
Verificar "Pedirlo otra vez, ahora por CORREO (sin importar mayusculas) -> 200 con otro codigo" ($r2.Codigo -eq 200 -and $r2.Json.id_codigo -ne $r1.Json.id_codigo) $r2
$viejo = Llamar POST "/api/Usuario/recuperacion/restablecer" @{ id_codigo = $r1.Json.id_codigo; codigo = $r1.Json.codigo; clave_nueva = "Recuperada-71" }
Verificar "El codigo ANTERIOR quedo anulado al pedir otro -> 400" ($viejo.Codigo -eq 400) $viejo

$mal = Llamar POST "/api/Usuario/recuperacion/restablecer" @{ id_codigo = $r2.Json.id_codigo; codigo = "000000"; clave_nueva = "Recuperada-71" }
Verificar "Restablecer con un codigo INCORRECTO -> 400" ($mal.Codigo -eq 400) $mal
$corta = Llamar POST "/api/Usuario/recuperacion/restablecer" @{ id_codigo = $r2.Json.id_codigo; codigo = $r2.Json.codigo; clave_nueva = "corta" }
Verificar "Restablecer con clave nueva de menos de 8 caracteres -> 422" ($corta.Codigo -eq 422) $corta

$ok = Llamar POST "/api/Usuario/recuperacion/restablecer" @{ id_codigo = $r2.Json.id_codigo; codigo = $r2.Json.codigo; clave_nueva = "Recuperada-71" }
Verificar "Restablecer con el codigo correcto -> 200" ($ok.Codigo -eq 200) $ok
Verificar "El login ya funciona con la clave recuperada" ((Llamar POST "/api/Usuario/login" @{ usuario = $usuarioPrueba; clave = "Recuperada-71" }).Codigo -eq 200)
Verificar "...y la clave de antes del olvido dejo de servir (401)" ((Llamar POST "/api/Usuario/login" @{ usuario = $usuarioPrueba; clave = $nueva }).Codigo -eq 401)
$otraVez = Llamar POST "/api/Usuario/recuperacion/restablecer" @{ id_codigo = $r2.Json.id_codigo; codigo = $r2.Json.codigo; clave_nueva = "Otra-Clave-71" }
Verificar "Usar el MISMO codigo otra vez -> 400 (un solo uso)" ($otraVez.Codigo -eq 400) $otraVez

# Tres intentos fallidos anulan el codigo (CLAVE_MAX_INTENTOS = 3). A diferencia de la clave de
# acceso de la v5 (RF153), aqui el contador SI se acumula: el SP devuelve un estado en vez de
# lanzar una excepcion que deshaga el UPDATE.
$r3 = Llamar POST "/api/Usuario/recuperacion/solicitar" @{ identificador = $usuarioPrueba }
foreach ($i in 1..3) { $null = Llamar POST "/api/Usuario/recuperacion/restablecer" @{ id_codigo = $r3.Json.id_codigo; codigo = "000000"; clave_nueva = "Otra-Clave-71" } }
$tras = Llamar POST "/api/Usuario/recuperacion/restablecer" @{ id_codigo = $r3.Json.id_codigo; codigo = $r3.Json.codigo; clave_nueva = "Otra-Clave-71" }
Verificar "Tras 3 intentos fallidos el codigo queda anulado, aun con el valor correcto -> 400" ($tras.Codigo -eq 400) $tras
Verificar "...y la clave NO cambio" ((Llamar POST "/api/Usuario/login" @{ usuario = $usuarioPrueba; clave = "Recuperada-71" }).Codigo -eq 200)

# --------------------------------------------------------------------------
Titulo "6-7. D55: ya no hay inscripcion a catedras ni cupo"
Verificar "POST /api/InscripcionCatedra ya no existe (404)" ((Llamar POST "/api/InscripcionCatedra" @{ fk_catedra = 5498; fk_asistente = $idAsistente }).Codigo -eq 404)
Verificar "GET /api/InscripcionCatedra ya no existe (404)" ((Llamar GET "/api/InscripcionCatedra").Codigo -eq 404)
$sesSinCupo = Llamar GET "/api/Sesion/$idSesion"
Verificar "Las sesiones ya no traen 'cupo'" ($sesSinCupo.Codigo -eq 200 -and $sesSinCupo.Texto -notmatch '"cupo"') $sesSinCupo
$sesCupo = Llamar POST "/api/Sesion" @{ fk_catedra = 5498; inicio = $ahora.AddHours(30).ToString("o"); fin = $ahora.AddHours(31).ToString("o"); fk_modalidad = "PRESENCIAL"; fk_sede = "SAN_BENITO"; fk_periodo = "2026-2"; cupo = 1 }
Verificar "Crear una sesion mandando 'cupo' no falla: el campo se ignora" ($sesCupo.Codigo -eq 200 -and $sesCupo.Texto -notmatch '"cupo"') $sesCupo

# --------------------------------------------------------------------------
Titulo "8. D52: disponibilidad de horario (que no se crucen dos sesiones)"
# Una catedra de prueba con una sesion el ${dia} de 14:00 a 16:00 (Colombia), SAN_BENITO, 'Aula 101'.
$dia = (Get-Date "2032-01-01").AddDays((Get-Random -Maximum 3000))
$dia1 = $dia.AddDays(1).ToString("yyyy-MM-dd"); $dia2 = $dia.AddDays(2).ToString("yyyy-MM-dd"); $dia40 = $dia.AddDays(40).ToString("yyyy-MM-dd"); $dia = $dia.ToString("yyyy-MM-dd")
# El codigo ASIS es aleatorio para poder repetir el script sin chocar con la corrida anterior.
$codigoDisp = "{0:D9}" -f (Get-Random -Minimum 800000000 -Maximum 899999999)
$tipoEv = (Llamar GET "/api/TipoEvento").Json.datos[0].id_tipo_evento
$catDisp = Llamar POST "/api/Catedra" @{
    id_evento_asis = $codigoDisp; nombre = "Catedra de prueba de disponibilidad"; fk_tipo_evento = $tipoEv; activo = $true
    sesiones = @(@{ inicio = "${dia}T14:00:00-05:00"; fin = "${dia}T16:00:00-05:00"; fk_modalidad = "PRESENCIAL"
                    fk_sede = "SAN_BENITO"; fk_periodo = "2026-2"; lugar = "Aula 101" })
}
Verificar "Se crea la catedra de prueba con una sesion" ($catDisp.Codigo -eq 200) $catDisp
$sesDisp = (Llamar GET "/api/Sesion?fkCatedra=$($catDisp.Json.id_catedra)").Json.datos[0].id_sesion
$null = Llamar POST "/api/Ponencia" @{ fk_sesion = $sesDisp; fk_asistente = $idAsistente; rol = "PONENTE" }

function Conflictos($ini, $fin, $extra = "") { Llamar GET ("/api/Disponibilidad/conflictos?inicio=" + [uri]::EscapeDataString($ini) + "&fin=" + [uri]::EscapeDataString($fin) + $extra) }
$d1 = Conflictos "${dia}T15:00:00-05:00" "${dia}T17:00:00-05:00" "&fkSede=SAN_BENITO&lugar=Aula%20101"
Verificar "Mismo salon y horario traslapado -> NO disponible por LUGAR" ($d1.Codigo -eq 200 -and $d1.Json.disponible -eq $false -and $d1.Json.conflictos[0].motivos -contains "LUGAR") $d1
$d2 = Conflictos "${dia}T15:00:00-05:00" "${dia}T17:00:00-05:00" "&fkSede=SAN_BENITO&lugar=Aula%20202"
Verificar "Misma sede, otro salon -> disponible" ($d2.Codigo -eq 200 -and $d2.Json.disponible -eq $true) $d2
$d3 = Conflictos "${dia}T16:00:00-05:00" "${dia}T18:00:00-05:00" "&fkSede=SAN_BENITO&lugar=Aula%20101"
Verificar "Empezar justo cuando termina la otra -> disponible (no cruza)" ($d3.Codigo -eq 200 -and $d3.Json.disponible -eq $true) $d3
$d4 = Conflictos "${dia}T15:00:00-05:00" "${dia}T17:00:00-05:00" "&fkSede=BELLO&lugar=Salon%20X&fkAsistente=$idAsistente"
Verificar "Otra sede pero el ponente ya esta en esa franja -> NO disponible por PONENTE" ($d4.Codigo -eq 200 -and $d4.Json.disponible -eq $false -and $d4.Json.conflictos[0].motivos -contains "PONENTE") $d4
$d5 = Conflictos "${dia}T15:00:00-05:00" "${dia}T17:00:00-05:00" "&fkSede=VIRTUAL"
Verificar "Sede virtual (sin salon) y sin ponente -> disponible" ($d5.Codigo -eq 200 -and $d5.Json.disponible -eq $true) $d5
$d6 = Conflictos "${dia}T15:00:00-05:00" "${dia}T17:00:00-05:00" "&fkAsistente=$idAsistente&excluirSesion=$sesDisp"
Verificar "excluirSesion deja fuera la sesion que se esta asignando" ($d6.Codigo -eq 200 -and $d6.Json.disponible -eq $true) $d6
Verificar "Rango invalido (fin antes de inicio) -> 400" ((Conflictos "${dia}T17:00:00-05:00" "${dia}T15:00:00-05:00" "&fkSede=SAN_BENITO").Codigo -eq 400)
Verificar "Rango de mas de 24 h -> 400" ((Conflictos "${dia}T00:00:00-05:00" "${dia2}T00:00:00-05:00" "&fkSede=SAN_BENITO").Codigo -eq 400)
$ag = Llamar GET ("/api/Disponibilidad/agenda?desde=" + [uri]::EscapeDataString("${dia}T00:00:00-05:00") + "&hasta=" + [uri]::EscapeDataString("${dia1}T00:00:00-05:00"))
Verificar "Agenda del dia: trae la sesion con el nombre de la catedra y de la sede" ($ag.Codigo -eq 200 -and $ag.Json.total -ge 1 -and $ag.Json.datos[0].nombre_catedra -and $ag.Json.datos[0].nombre_sede) $ag
$ag2 = Llamar GET ("/api/Disponibilidad/agenda?desde=" + [uri]::EscapeDataString("${dia1}T00:00:00-05:00") + "&hasta=" + [uri]::EscapeDataString("${dia2}T00:00:00-05:00"))
Verificar "Agenda de un dia sin sesiones -> 200 con total 0" ($ag2.Codigo -eq 200 -and $ag2.Json.total -eq 0) $ag2
Verificar "Agenda de mas de 31 dias -> 400" ((Llamar GET ("/api/Disponibilidad/agenda?desde=" + [uri]::EscapeDataString("${dia}T00:00:00-05:00") + "&hasta=" + [uri]::EscapeDataString("${dia40}T00:00:00-05:00"))).Codigo -eq 400)

# --------------------------------------------------------------------------
Titulo "9. D53/D55: QR de cierre de sesion (ventana al final, sin inscripcion)"
# Dos sesiones: una que termina en 10 minutos (su QR esta ABIERTO ahora: abre 15 min antes del fin) y otra que
# termino hace una hora (su QR ya cerro). Codigo ASIS aleatorio para poder repetir el script.
$ahoraQr = [DateTimeOffset]::Now
function IsoQr($d) { $d.ToString("yyyy-MM-dd'T'HH:mm:sszzz") }
$tipoQr = (Llamar GET "/api/TipoEvento").Json.datos[0].id_tipo_evento
$codigoQr = "{0:D9}" -f (Get-Random -Minimum 900000000 -Maximum 999999999)
$catQr = Llamar POST "/api/Catedra" @{
    id_evento_asis = $codigoQr; nombre = "Catedra de prueba del QR"; fk_tipo_evento = $tipoQr; activo = $true
    sesiones = @(
        @{ inicio = (IsoQr $ahoraQr.AddMinutes(-50)); fin = (IsoQr $ahoraQr.AddMinutes(10)); fk_modalidad = "PRESENCIAL"; fk_sede = "SAN_BENITO"; fk_periodo = "2026-2"; lugar = "Aula QR" },
        @{ inicio = (IsoQr $ahoraQr.AddHours(-3)); fin = (IsoQr $ahoraQr.AddHours(-2)); fk_modalidad = "PRESENCIAL"; fk_sede = "SAN_BENITO"; fk_periodo = "2026-2"; lugar = "Aula QR" })
}
Verificar "Se crea la catedra de prueba del QR con 2 sesiones" ($catQr.Codigo -eq 200) $catQr
$sesQr = @((Llamar GET "/api/Sesion?fkCatedra=$($catQr.Json.id_catedra)").Json.datos | Sort-Object id_sesion)
$sAbierta = $sesQr[0]; $sCerrada = $sesQr[1]

$q1 = Llamar POST "/api/QrSesion" @{ fk_sesion = $sAbierta.id_sesion }
Verificar "POST /api/QrSesion crea el QR (ya_existia=false)" ($q1.Codigo -eq 200 -and $q1.Json.ya_existia -eq $false -and $q1.Json.token) $q1
$finQr = [DateTimeOffset]::Parse($sAbierta.fin)
$minAntes = ($finQr - [DateTimeOffset]::Parse($q1.Json.ventana_desde)).TotalMinutes
$minDespues = ([DateTimeOffset]::Parse($q1.Json.ventana_hasta) - $finQr).TotalMinutes
Verificar "La ventana abre 15 minutos antes del FIN de la sesion y cierra cuando termina" ([math]::Round($minAntes) -eq 15 -and [math]::Round($minDespues) -eq 0) $q1
$q2 = Llamar POST "/api/QrSesion" @{ fk_sesion = $sAbierta.id_sesion }
Verificar "Emitir otra vez es idempotente (mismo token, ya_existia=true)" ($q2.Codigo -eq 200 -and $q2.Json.token -eq $q1.Json.token -and $q2.Json.ya_existia -eq $true) $q2
$q3 = Llamar POST "/api/QrSesion" @{ fk_sesion = $sAbierta.id_sesion; regenerar = $true }
Verificar "Regenerar da otro token" ($q3.Codigo -eq 200 -and $q3.Json.token -ne $q1.Json.token -and $q3.Json.ya_existia -eq $false) $q3
$qc = Llamar GET "/api/QrSesion?fkSesion=$($sAbierta.id_sesion)"
Verificar "GET /api/QrSesion devuelve el QR vigente (el nuevo)" ($qc.Codigo -eq 200 -and $qc.Json.token -eq $q3.Json.token) $qc
Verificar "GET de una sesion sin QR -> 204" ((Llamar GET "/api/QrSesion?fkSesion=$($sCerrada.id_sesion)").Codigo -eq 204)
Verificar "QR de una sesion inexistente -> 404" ((Llamar POST "/api/QrSesion" @{ fk_sesion = 99999999 }).Codigo -eq 404)
Verificar "POST sin fk_sesion -> 422" ((Llamar POST "/api/QrSesion" @{}).Codigo -eq 422)
Verificar "GET sin fkSesion -> 400" ((Llamar GET "/api/QrSesion").Codigo -eq 400)

# Con el QR NO hace falta inscribirse (la inscripcion ya no existe)
$conInsc = Llamar POST "/api/ClaveAcceso/solicitar" @{ identificador = "30000124724"; token = $q3.Json.token }
Verificar "Pedir la clave con el QR -> 200 (sin inscripcion)" ($conInsc.Codigo -eq 200 -and $conInsc.Json.clave -match '^\d{6}$') $conInsc
$regQr = Llamar POST "/api/ClaveAcceso/validar" @{ id_clave = $conInsc.Json.id_clave; clave = $conInsc.Json.clave; latitud = $lat; longitud = $lng }
Verificar "Validar con ubicacion DENTRO de la ventana -> registro de asistencia" ($regQr.Codigo -eq 200 -and $regQr.Json.id_registro -gt 0) $regQr
$asQr = Llamar GET "/api/QrSesion/asistencia?fkSesion=$($sAbierta.id_sesion)"
$filaQr = @($asQr.Json.datos | Where-Object { $_.fk_asistente -eq $idAsistente })[0]
Verificar "La asistencia de la sesion lo muestra con hora y ubicacion (sin lista de inscritos)" ($asQr.Codigo -eq 200 -and $filaQr.registrado_en -and $null -ne $filaQr.latitud -and $asQr.Texto -notmatch "inscrit") $asQr

# Fuera de la ventana
$qCerr = Llamar POST "/api/QrSesion" @{ fk_sesion = $sCerrada.id_sesion }
$fuera = Llamar POST "/api/ClaveAcceso/solicitar" @{ identificador = "30000124724"; token = $qCerr.Json.token }
Verificar "QR de una sesion que ya termino (fuera de ventana) -> rechazado (RN-15)" ($fuera.Codigo -eq 500 -and $fuera.Texto -match "RN-15") $fuera

# Los enlaces de siempre (emitidos por el administrador) no cambian
$sC = (Llamar GET "/api/Sesion?fkCatedra=$($catQr.Json.id_catedra)").Json.datos[0]
$enlC = Llamar POST "/api/EnlaceRegistro" @{ fk_sesion = $sC.id_sesion }
$tokC = (Llamar GET "/api/EnlaceRegistro/$($enlC.Json.id_enlace)").Json.token
Verificar "Un enlace emitido por el administrador (v5) sigue funcionando igual" ((Llamar POST "/api/ClaveAcceso/solicitar" @{ identificador = "30000124724"; token = $tokC }).Codigo -eq 200)

# Todas las modalidades tienen su QR: el CANAL sale de la modalidad (RN-13 del script provisto): presencial -> QR,
# virtual y telepresencial -> QR (catalogo de este proyecto: modalidad.canal_difusion = QR; ver la migracion 073: no piden ubicacion).
function SesionModal($modalidad, $sede) {
    @{ inicio = (IsoQr $ahoraQr.AddMinutes(-50)); fin = (IsoQr $ahoraQr.AddMinutes(10)); fk_modalidad = $modalidad; fk_sede = $sede; fk_periodo = "2026-2"; lugar = "Sala $modalidad" }
}
$catMod = Llamar POST "/api/Catedra" @{
    id_evento_asis = ("{0:D9}" -f (Get-Random -Minimum 900000000 -Maximum 999999999)); nombre = "Catedra de prueba de modalidades"; fk_tipo_evento = $tipoQr; activo = $true
    sesiones = @((SesionModal "PRESENCIAL" "SAN_BENITO"), (SesionModal "VIRTUAL" "VIRTUAL"), (SesionModal "TELEPRESENCIAL" "VIRTUAL"))
}
Verificar "Se crea una catedra con una sesion de cada modalidad" ($catMod.Codigo -eq 200) $catMod
$sesMod = @((Llamar GET "/api/Sesion?fkCatedra=$($catMod.Json.id_catedra)").Json.datos)
foreach ($par in @(@("PRESENCIAL", "QR"), @("VIRTUAL", "QR"), @("TELEPRESENCIAL", "QR"))) {
    $sm = $sesMod | Where-Object { $_.fk_modalidad -eq $par[0] } | Select-Object -First 1
    $qm = Llamar POST "/api/QrSesion" @{ fk_sesion = $sm.id_sesion }
    Verificar "Sesion $($par[0]): el QR se genera (200) con canal $($par[1])" ($qm.Codigo -eq 200 -and $qm.Json.canal -eq $par[1] -and $qm.Json.token) $qm
}
$smV = $sesMod | Where-Object { $_.fk_modalidad -eq "VIRTUAL" } | Select-Object -First 1
$qV = Llamar GET "/api/QrSesion?fkSesion=$($smV.id_sesion)"
$solV = Llamar POST "/api/ClaveAcceso/solicitar" @{ identificador = "30000124724"; token = $qV.Json.token }
$regV = Llamar POST "/api/ClaveAcceso/validar" @{ id_clave = $solV.Json.id_clave; clave = $solV.Json.clave; latitud = $lat; longitud = $lng }
Verificar "Sesion virtual: se registra la asistencia con el QR (200), sin inscripcion" ($solV.Codigo -eq 200 -and $regV.Codigo -eq 200) $regV
Verificar "...y el origen del registro es QR (el canal de la sesion virtual)" ((Llamar GET "/api/RegistroAsistencia/$($regV.Json.id_registro)").Json.origen -eq "QR")

# --------------------------------------------------------------------------
Titulo "10. D55: solo el administrador inicia sesion; cambio obligatorio de clave a quien se marque"
$sufijo = "{0:D6}" -f (Get-Random -Minimum 0 -Maximum 999999)

# Una cuenta que NO es administrador no entra, aunque usuario y clave sean correctos
$pe = Llamar POST "/api/Asistente" @{ id_asis = "T9$sufijo"; nombres = "Estudiante"; apellidos = "De Prueba $sufijo"; correo_institucional = "est.prueba$sufijo@usbmed.edu.co"; es_externo = $false
    vinculaciones = @(@{ fk_tipo_vinculacion = "ESTUDIANTE" }) }
$ue = Llamar POST "/api/Usuario" @{ fk_asistente = $pe.Json.id_asistente; usuario = "est.prueba$sufijo"; clave = "Clave-Estudiante-1" }
Verificar "Se crea una cuenta de estudiante de prueba (sin ningun rol)" ($ue.Codigo -eq 200) $ue
Verificar "Sin rol: usuario y clave correctos -> 401 (no es administrador)" ((Llamar POST "/api/Usuario/login" @{ usuario = "est.prueba$sufijo"; clave = "Clave-Estudiante-1" }).Codigo -eq 401)
$null = Llamar POST "/api/RolPorUsuario" @{ fk_usuario = $ue.Json.id_usuario; fk_rol = "ESTUDIANTE" }
Verificar "Con el rol ESTUDIANTE -> 401" ((Llamar POST "/api/Usuario/login" @{ usuario = "est.prueba$sufijo"; clave = "Clave-Estudiante-1" }).Codigo -eq 401)
$null = Llamar POST "/api/RolPorUsuario" @{ fk_usuario = $ue.Json.id_usuario; fk_rol = "PONENTE" }
Verificar "Con ESTUDIANTE y PONENTE -> 401" ((Llamar POST "/api/Usuario/login" @{ usuario = "est.prueba$sufijo"; clave = "Clave-Estudiante-1" }).Codigo -eq 401)
Verificar "El correo institucional ya no es un usuario valido para entrar (aunque la clave sea el codigo) -> 401" `
    ((Llamar POST "/api/Usuario/login" @{ usuario = "est.prueba$sufijo@usbmed.edu.co"; clave = "T9$sufijo" }).Codigo -eq 401)
Verificar "...ni el primer ingreso con codigo de estudiante crea cuentas (sigue sin haber login) -> 401" `
    ((Llamar POST "/api/Usuario/login" @{ usuario = "est2.prueba$sufijo@usbmed.edu.co"; clave = "T9$sufijo" }).Codigo -eq 401)
$null = Llamar POST "/api/RolPorUsuario" @{ fk_usuario = $ue.Json.id_usuario; fk_rol = "ADMIN" }
Verificar "Cuando se le da el rol ADMIN, entra (manda el rol, no la persona) -> 200" ((Llamar POST "/api/Usuario/login" @{ usuario = "est.prueba$sufijo"; clave = "Clave-Estudiante-1" }).Codigo -eq 200)
Verificar "Con el rol ADMIN pero la clave mal escrita -> 401" ((Llamar POST "/api/Usuario/login" @{ usuario = "est.prueba$sufijo"; clave = "otra-clave" }).Codigo -eq 401)

# Cambio obligatorio de clave (sirve a cualquier cuenta con clave temporal)
$ca = Llamar POST "/api/Asistente" @{ nombres = "Admin"; apellidos = "Nuevo $sufijo"; correo_personal = "adn$sufijo@ejemplo.com"; es_externo = $true }
$cu = Llamar POST "/api/Usuario" @{ fk_asistente = $ca.Json.id_asistente; usuario = "adn$sufijo"; clave = "Temporal-$sufijo"; debe_cambiar_clave = $true }
$null = Llamar POST "/api/RolPorUsuario" @{ fk_usuario = $cu.Json.id_usuario; fk_rol = "ADMIN" }
$lg = Llamar POST "/api/Usuario/login" @{ usuario = "adn$sufijo"; clave = "Temporal-$sufijo" }
Verificar "Una cuenta creada con debe_cambiar_clave=true entra pidiendo el cambio" ($lg.Codigo -eq 200 -and $lg.Json.usuario.debe_cambiar_clave -eq $true) $lg
$camb = Llamar POST "/api/Usuario/$($cu.Json.id_usuario)/cambiar-clave" @{ clave_actual = "Temporal-$sufijo"; clave_nueva = "Definitiva-$sufijo" }
Verificar "Cambiar la clave -> 200" ($camb.Codigo -eq 200) $camb
Verificar "...y el siguiente ingreso YA NO exige el cambio" ((Llamar POST "/api/Usuario/login" @{ usuario = "adn$sufijo"; clave = "Definitiva-$sufijo" }).Json.usuario.debe_cambiar_clave -eq $false)
$ex = Llamar POST "/api/Usuario/$($cu.Json.id_usuario)/exigir-cambio-clave"
Verificar "El administrador puede volver a exigir el cambio -> 200" ($ex.Codigo -eq 200) $ex
Verificar "...y el siguiente ingreso lo trae pendiente" ((Llamar POST "/api/Usuario/login" @{ usuario = "adn$sufijo"; clave = "Definitiva-$sufijo" }).Json.usuario.debe_cambiar_clave -eq $true)
Verificar "Exigir el cambio a un usuario inexistente -> 404" ((Llamar POST "/api/Usuario/99999999/exigir-cambio-clave").Codigo -eq 404)
$rec = Llamar POST "/api/Usuario/recuperacion/solicitar" @{ identificador = "adn$sufijo" }
$null = Llamar POST "/api/Usuario/recuperacion/restablecer" @{ id_codigo = $rec.Json.id_codigo; codigo = $rec.Json.codigo; clave_nueva = "Recuperada-$sufijo" }
Verificar "Recuperar la clave por correo tambien apaga la exigencia" ((Llamar POST "/api/Usuario/login" @{ usuario = "adn$sufijo"; clave = "Recuperada-$sufijo" }).Json.usuario.debe_cambiar_clave -eq $false)

# --------------------------------------------------------------------------
Write-Host ""
$color = if ($script:fallidas -eq 0) { "Green" } else { "Red" }
Write-Host "$($script:pasadas) pasadas, $($script:fallidas) fallidas." -ForegroundColor $color
if ($script:fallidas -gt 0) { exit 1 } else { exit 0 }
