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
#  REQUISITO: la BD debe tener las migraciones 045-051 (BD nueva con
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
Titulo "9. D53: QR de cierre de sesion (ventana al final, inscripcion obligatoria)"
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
Verificar "POST /api/QrSesion crea el QR (ya_existia=false, exige inscripcion)" ($q1.Codigo -eq 200 -and $q1.Json.ya_existia -eq $false -and $q1.Json.requiere_inscripcion -eq $true -and $q1.Json.token) $q1
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

# La inscripcion es obligatoria con el QR
$null = Llamar POST "/api/InscripcionCatedra/cancelar" @{ fk_catedra = $catQr.Json.id_catedra; fk_asistente = $idAsistente }
$sinInsc = Llamar POST "/api/ClaveAcceso/solicitar" @{ identificador = "30000124724"; token = $q3.Json.token }
Verificar "SIN inscripcion: pedir la clave con el QR se rechaza y dice que falta inscribirse" ($sinInsc.Codigo -eq 500 -and $sinInsc.Texto -match "inscrit") $sinInsc
$null = Llamar POST "/api/InscripcionCatedra" @{ fk_catedra = $catQr.Json.id_catedra; fk_asistente = $idAsistente }
$conInsc = Llamar POST "/api/ClaveAcceso/solicitar" @{ identificador = "30000124724"; token = $q3.Json.token }
Verificar "Inscrito: pedir la clave con el QR -> 200" ($conInsc.Codigo -eq 200 -and $conInsc.Json.clave -match '^\d{6}$') $conInsc
$regQr = Llamar POST "/api/ClaveAcceso/validar" @{ id_clave = $conInsc.Json.id_clave; clave = $conInsc.Json.clave; latitud = $lat; longitud = $lng }
Verificar "Validar con ubicacion DENTRO de la ventana -> registro de asistencia" ($regQr.Codigo -eq 200 -and $regQr.Json.id_registro -gt 0) $regQr
$asQr = Llamar GET "/api/QrSesion/asistencia?fkSesion=$($sAbierta.id_sesion)"
$filaQr = @($asQr.Json.datos | Where-Object { $_.fk_asistente -eq $idAsistente })[0]
Verificar "La asistencia de la sesion lo muestra inscrito, que asistio, con hora y ubicacion" ($asQr.Codigo -eq 200 -and $filaQr.inscrito -eq $true -and $filaQr.asistio -eq $true -and $filaQr.registrado_en -and $null -ne $filaQr.latitud) $asQr

# Fuera de la ventana
$qCerr = Llamar POST "/api/QrSesion" @{ fk_sesion = $sCerrada.id_sesion }
$fuera = Llamar POST "/api/ClaveAcceso/solicitar" @{ identificador = "30000124724"; token = $qCerr.Json.token }
Verificar "QR de una sesion que ya termino (fuera de ventana) -> rechazado (RN-15)" ($fuera.Codigo -eq 500 -and $fuera.Texto -match "RN-15") $fuera

# La segunda barrera: cancelar la inscripcion ENTRE pedir y validar la clave
$catQr2 = Llamar POST "/api/Catedra" @{
    id_evento_asis = ("{0:D9}" -f (Get-Random -Minimum 900000000 -Maximum 999999999)); nombre = "Catedra de prueba del QR (2)"; fk_tipo_evento = $tipoQr; activo = $true
    sesiones = @(@{ inicio = (IsoQr $ahoraQr.AddMinutes(-50)); fin = (IsoQr $ahoraQr.AddMinutes(10)); fk_modalidad = "PRESENCIAL"; fk_sede = "SAN_BENITO"; fk_periodo = "2026-2"; lugar = "Aula QR 2" })
}
$sB = (Llamar GET "/api/Sesion?fkCatedra=$($catQr2.Json.id_catedra)").Json.datos[0]
$qB = Llamar POST "/api/QrSesion" @{ fk_sesion = $sB.id_sesion }
$null = Llamar POST "/api/InscripcionCatedra" @{ fk_catedra = $catQr2.Json.id_catedra; fk_asistente = $idAsistente }
$solB = Llamar POST "/api/ClaveAcceso/solicitar" @{ identificador = "30000124724"; token = $qB.Json.token }
$null = Llamar POST "/api/InscripcionCatedra/cancelar" @{ fk_catedra = $catQr2.Json.id_catedra; fk_asistente = $idAsistente }
$valB = Llamar POST "/api/ClaveAcceso/validar" @{ id_clave = $solB.Json.id_clave; clave = $solB.Json.clave; latitud = $lat; longitud = $lng }
Verificar "Si cancela la inscripcion entre pedir y validar la clave, el registro se rechaza (disparador)" ($valB.Codigo -eq 500 -and $valB.Texto -match "inscrito") $valB

# Los enlaces de siempre (emitidos por el administrador) no cambian
$sC = (Llamar GET "/api/Sesion?fkCatedra=$($catQr2.Json.id_catedra)").Json.datos[0]
$enlC = Llamar POST "/api/EnlaceRegistro" @{ fk_sesion = $sC.id_sesion }
$tokC = (Llamar GET "/api/EnlaceRegistro/$($enlC.Json.id_enlace)").Json.token
Verificar "Un enlace emitido por el administrador (v5) NO exige inscripcion" ((Llamar POST "/api/ClaveAcceso/solicitar" @{ identificador = "30000124724"; token = $tokC }).Codigo -eq 200)
$null = Llamar POST "/api/InscripcionCatedra/cancelar" @{ fk_catedra = $catQr.Json.id_catedra; fk_asistente = $idAsistente }   # se deja sin inscripcion

# --------------------------------------------------------------------------
Write-Host ""
$color = if ($script:fallidas -eq 0) { "Green" } else { "Red" }
Write-Host "$($script:pasadas) pasadas, $($script:fallidas) fallidas." -ForegroundColor $color
if ($script:fallidas -gt 0) { exit 1 } else { exit 0 }
