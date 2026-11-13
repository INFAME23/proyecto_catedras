# ==========================================================================
#  verificar_v72.ps1  -  Proyecto Catedras - Subversion v7.2
# --------------------------------------------------------------------------
#  Verificacion contra la API REAL (Docker + PostgreSQL) de lo que agrega la v7.2:
#    1. Sedes con coordenadas y radio (RF-S11)
#    2. El QR de una sesion presencial exige una sede con coordenadas (RF-S12, D72)
#    3. Carga idempotente de estudiantes (RF-S8, D68): CREADO / ACTUALIZADO, CC ajena,
#       P19 (CC anterior no vigente, programa del mismo periodo RETIRADO)
#    4. Registro publico de estudiante, docente y externo, sin clave por correo
#       (RF-S9, D60) y ubicacion SOLO en sesiones presenciales (RF-S10, D61):
#       dentro / fuera del radio, sin ubicacion, virtual, radio propio de la sede,
#       duplicados, enlace revocado o fuera de ventana, distancia guardada
#    5. Exportacion por catedra (RF-S13/RF-S14): conteos, sin repetir personas, paginacion
#  Cada caso imprime OK o FALLA; al final da el conteo y sale con codigo 0 si todo paso, 1 si no.
#
#  Uso (con la API ya levantada, desde la carpeta api_catedras):
#      powershell -ExecutionPolicy Bypass -File .\pruebas\verificar_v72.ps1
#  Otro puerto/host:  ... -File .\pruebas\verificar_v72.ps1 -Api http://127.0.0.1:8142
#
#  REQUISITO: la BD debe tener las migraciones 060-066 (BD nueva con
#  'docker compose up -d --build', o aplicadas a mano; ver README).
#
#  QUE TOCA (y por que es seguro repetirlo):
#   - ESCRIBE datos de prueba con un sufijo al azar: una catedra y sus sesiones, enlaces,
#     sedes 'V72...', personas 'V72...', registros de asistencia. Para dejar la BD como nueva:
#         docker compose down -v ; docker compose up -d --build
#   - Para poder probar sesiones "abiertas" en dias FUTUROS (que no choquen con la
#     separacion de una hora entre sesiones ni con los periodos de programacion), cambia UN
#     parametro mientras corre: QR_MINUTOS_ANTES_FIN (ventana del QR). Lo deja como estaba al
#     terminar, incluso si algo falla (bloque finally). Si matas el script a la fuerza, vuelve a
#     dejarlo en su valor normal (15) en /admin/parametros.
#   - Las sesiones se ubican en el primer horario que acepten los periodos y horarios que
#     tenga el administrador (D57): no los pausa ni los cambia.
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
        $parametros.Body = [System.Text.Encoding]::UTF8.GetBytes(($Cuerpo | ConvertTo-Json -Compress -Depth 6))
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

function Sufijo([int]$digitos) { (Get-Random -Minimum 0 -Maximum ([int][math]::Pow(10, $digitos) - 1)).ToString("D$digitos") }

# --------------------------------------------------------------------------
#  Ayudas de la verificacion
# --------------------------------------------------------------------------
$sufijo = Sufijo 6
$lat = 6.333129          # BELLO (migracion 060)
$lng = -75.569174
$latSanBenito = 6.255095 # SAN_BENITO: a ~8,7 km de Bello
$lngSanBenito = -75.572969
$dLat50m = 0.00045       # 0,00045 grados de latitud = ~50 m

function Iso($fecha) { $fecha.ToUniversalTime().ToString("o") }

# Crea una sesion de 1 hora en el primer horario libre que acepten los periodos/horarios del administrador (D57)
# y la separacion de una hora entre sesiones (D59): prueba dias futuros a partir de $script:dia (hasta ~100 dias), a las 9, 11, 14 y 16 (hora de Colombia).
$script:dia = 1 + (Get-Random -Minimum 0 -Maximum 14)
function NuevaSesion {
    param([long]$IdCatedra, [string]$Modalidad, [string]$Sede)
    for ($intento = 0; $intento -lt 400; $intento++) {
        $d = (Get-Date).Date.AddDays($script:dia + [math]::Floor($intento / 4))
        $hora = @(9, 11, 14, 16)[$intento % 4]
        $inicio = [DateTimeOffset]::new($d.Year, $d.Month, $d.Day, $hora, 0, 0, [TimeSpan]::FromHours(-5))
        $r = Llamar POST "/api/Sesion" @{
            fk_catedra = $IdCatedra; inicio = (Iso $inicio); fin = (Iso $inicio.AddHours(1))
            fk_modalidad = $Modalidad; fk_sede = $Sede; fk_periodo = "2026-2"; lugar = "Sala V72"
        }
        if ($r.Codigo -eq 200) {
            $script:dia = $script:dia + [math]::Floor(($intento + 1) / 4) + 1   # la proxima, otro dia
            return $r
        }
        if ($r.Codigo -ne 409) { return $r }                                     # un error que no es de horario: se devuelve
    }
    return $r
}

function Qr([long]$IdSesion, [bool]$Regenerar = $false) { Llamar POST "/api/QrSesion" @{ fk_sesion = $IdSesion; regenerar = $Regenerar } }
function RegEst($token, $carnet, $la = $null, $lo = $null, $ip = "190.10.20.30") {
    $c = @{ token = $token; carnet = $carnet; ip = $ip; user_agent = "verificar_v72/1.0" }
    if ($null -ne $la) { $c.latitud = $la; $c.longitud = $lo }
    Llamar POST "/api/AsistenciaPublica/estudiante" $c
}
function RegDoc($token, $cedula, $nombre, $contacto, $facultad, $la = $null, $lo = $null) {
    $c = @{ token = $token; cedula = $cedula; nombre = $nombre; contacto = $contacto; fk_facultad = $facultad }
    if ($null -ne $la) { $c.latitud = $la; $c.longitud = $lo }
    Llamar POST "/api/AsistenciaPublica/docente" $c
}
function RegExt($token, $cedula, $nombre, $contacto, $procedencia, $la = $null, $lo = $null) {
    $c = @{ token = $token; cedula = $cedula; nombre = $nombre; contacto = $contacto; procedencia = $procedencia }
    if ($null -ne $la) { $c.latitud = $la; $c.longitud = $lo }
    Llamar POST "/api/AsistenciaPublica/externo" $c
}
function Cargar($carnet, $cc, $programa, $periodo = "2026-2") {
    Llamar POST "/api/Asistente/cargar-estudiante" @{ id_asis = $carnet; documento = $cc; programa = $programa; periodo = $periodo }
}
# Cuantos registros hay de una persona en una sesion (la API responde 204 sin cuerpo si no hay ninguno)
function ContarRegistros($idSesion, $idAsistente) {
    $r = Llamar GET "/api/RegistroAsistencia?fkSesion=$idSesion&fkAsistente=$idAsistente"
    if ($r.Codigo -eq 204) { return 0 }
    return @($r.Json.datos).Count
}
function Registro($idRegistro) { (Llamar GET "/api/RegistroAsistencia/$idRegistro").Json }

# Parametro que se cambia mientras corre (se restaura en el finally del final)
$paramAntes = $null

try {
    # ----------------------------------------------------------------------
    Titulo "0. La API responde y las migraciones 060-066 estan aplicadas"
    $raiz = Llamar GET "/"
    Verificar "GET / responde 200" ($raiz.Codigo -eq 200) $raiz
    $sedeB = Llamar GET "/api/Sede/BELLO"
    Verificar "GET /api/Sede/BELLO trae latitud, longitud y radio efectivo (migracion 060)" `
        ($sedeB.Codigo -eq 200 -and $sedeB.Json.latitud -eq $lat -and $sedeB.Json.longitud -eq $lng -and $sedeB.Json.radio_efectivo_metros -gt 0) $sedeB
    $pRadio = Llamar GET "/api/Parametro/RADIO_VALIDACION_METROS"
    Verificar "Existe el parametro RADIO_VALIDACION_METROS (200 por defecto)" ($pRadio.Codigo -eq 200 -and [int]$pRadio.Json.valor -gt 0) $pRadio
    $sedeS = Llamar GET "/api/Sede/SAN_BENITO"
    Verificar "SAN_BENITO tiene sus coordenadas y VIRTUAL no" `
        ($sedeS.Json.latitud -eq $latSanBenito -and (Llamar GET "/api/Sede/VIRTUAL").Json.latitud -eq $null) $sedeS
    Verificar "La modalidad HIBRIDA ya no existe en el catalogo (migracion 072)" `
        (@((Llamar GET "/api/Modalidad").Json.datos | Where-Object { $_.id_modalidad -eq "HIBRIDA" }).Count -eq 0)
    $progs = Llamar GET "/api/ProgramaAcademico/INDS"
    Verificar "Existen los programas del archivo de estudiantes (INDS, EDUC...)" `
        ($progs.Codigo -eq 200 -and (Llamar GET "/api/ProgramaAcademico/EDUC").Codigo -eq 200) $progs

    # Se amplia la ventana del QR para poder usar sesiones en dias futuros (se restaura al final)
    $p = Llamar GET "/api/Parametro/QR_MINUTOS_ANTES_FIN"
    if ($p.Codigo -eq 200) {
        $paramAntes = $p.Json.valor
        $cambio = Llamar PATCH "/api/Parametro/QR_MINUTOS_ANTES_FIN" @{ valor = "100000" }
        Verificar "Se amplia temporalmente QR_MINUTOS_ANTES_FIN (se restaura al terminar)" ($cambio.Codigo -eq 200) $cambio
    }

    # ----------------------------------------------------------------------
    Titulo "1. RF-S11: coordenadas y radio de la sede (validaciones y compatibilidad)"
    $s1 = "V72A$sufijo"
    $r = Llamar POST "/api/Sede" @{ id_sede = $s1; nombre = "Sede V72 $sufijo"; es_virtual = $false; activo = $true; latitud = 6.2 }
    Verificar "POST /api/Sede con latitud SIN longitud -> 422 (van juntas o ninguna)" ($r.Codigo -eq 422 -and $r.Texto -match "longitud") $r
    $r = Llamar POST "/api/Sede" @{ id_sede = $s1; nombre = "Sede V72 $sufijo"; es_virtual = $false; activo = $true; latitud = 95; longitud = 0 }
    Verificar "...con latitud 95 (fuera de rango) -> 422" ($r.Codigo -eq 422) $r
    $r = Llamar POST "/api/Sede" @{ id_sede = $s1; nombre = "Sede V72 $sufijo"; es_virtual = $false; activo = $true; latitud = 6.2; longitud = -75.5; radio_metros = 5 }
    Verificar "...con radio 5 (menos de 10) -> 422" ($r.Codigo -eq 422) $r
    $r = Llamar POST "/api/Sede" @{ id_sede = $s1; nombre = "Sede V72 $sufijo"; es_virtual = $false; activo = $true; latitud = 6.2; longitud = -75.5; radio_metros = 6000 }
    Verificar "...con radio 6000 (mas de 5000) -> 422" ($r.Codigo -eq 422) $r

    $r = Llamar POST "/api/Sede" @{ id_sede = $s1; nombre = "Sede V72 $sufijo"; es_virtual = $false; activo = $true; latitud = 6.2; longitud = -75.5; radio_metros = 90 }
    Verificar "POST /api/Sede con coordenadas y radio -> 200" ($r.Codigo -eq 200) $r
    $g = Llamar GET "/api/Sede/$s1"
    Verificar "...se guardan y el radio efectivo es el propio (90)" ($g.Json.latitud -eq 6.2 -and $g.Json.radio_metros -eq 90 -and $g.Json.radio_efectivo_metros -eq 90) $g

    $r = Llamar PUT "/api/Sede/$s1" @{ nombre = "Sede V72 $sufijo editada"; es_virtual = $false; activo = $true }
    $g = Llamar GET "/api/Sede/$s1"
    Verificar "PUT sin ubicacion (como la v7.1) NO borra las coordenadas ni el radio" ($r.Codigo -eq 200 -and $g.Json.latitud -eq 6.2 -and $g.Json.radio_metros -eq 90) $g
    $r = Llamar PATCH "/api/Sede/$s1" @{ radio_metros = 120 }
    $g = Llamar GET "/api/Sede/$s1"
    Verificar "PATCH solo con el radio cambia el radio y conserva las coordenadas" ($r.Codigo -eq 200 -and $g.Json.radio_metros -eq 120 -and $g.Json.latitud -eq 6.2) $g
    $r = Llamar PATCH "/api/Sede/$s1" @{ latitud = 6.3 }
    Verificar "PATCH con solo la latitud -> 422" ($r.Codigo -eq 422) $r
    $r = Llamar PATCH "/api/Sede/$s1" @{ latitud = 6.3; longitud = -75.6 }
    $g = Llamar GET "/api/Sede/$s1"
    Verificar "PATCH con latitud y longitud las cambia" ($r.Codigo -eq 200 -and $g.Json.latitud -eq 6.3 -and $g.Json.longitud -eq -75.6) $g
    $r = Llamar PATCH "/api/Sede/$s1" @{ es_virtual = $true }
    $g = Llamar GET "/api/Sede/$s1"
    Verificar "Volver virtual a la sede borra su ubicacion y su radio" ($r.Codigo -eq 200 -and $g.Json.latitud -eq $null -and $g.Json.radio_metros -eq $null) $g
    $r = Llamar PATCH "/api/Sede/$s1" @{ es_virtual = $false }
    $null = Llamar DELETE "/api/Sede/$s1"

    # ----------------------------------------------------------------------
    Titulo "2. RF-S12 / D72: el QR de una sesion presencial exige una sede con coordenadas"
    $tipo = (Llamar GET "/api/TipoEvento").Json.datos[0].id_tipo_evento
    $cat1 = Llamar POST "/api/Catedra" @{ id_evento_asis = ("{0:D9}" -f (Get-Random -Minimum 900000000 -Maximum 999999999)); nombre = "Catedra V72 $sufijo"; fk_tipo_evento = $tipo; activo = $true }
    Verificar "Se crea la catedra de prueba" ($cat1.Codigo -eq 200 -and $cat1.Json.id_catedra -gt 0) $cat1
    $idCat1 = $cat1.Json.id_catedra
    $cat2 = Llamar POST "/api/Catedra" @{ id_evento_asis = ("{0:D9}" -f (Get-Random -Minimum 900000000 -Maximum 999999999)); nombre = "Catedra V72 B $sufijo"; fk_tipo_evento = $tipo; activo = $true }
    $idCat2 = $cat2.Json.id_catedra
    $idEvento1 = $cat1.Json.id_evento_asis
    if (-not $idEvento1) { $idEvento1 = (Llamar GET "/api/Catedra/$idCat1").Json.id_evento_asis }
    $idEvento2 = (Llamar GET "/api/Catedra/$idCat2").Json.id_evento_asis

    $sinGeo = "V72G$sufijo"
    $null = Llamar POST "/api/Sede" @{ id_sede = $sinGeo; nombre = "Sede sin coordenadas $sufijo"; es_virtual = $false; activo = $true }
    $sSG = NuevaSesion $idCat1 "PRESENCIAL" $sinGeo
    Verificar "Una sesion PRESENCIAL se crea aunque la sede no tenga coordenadas (el problema aparece al generar el QR)" ($sSG.Codigo -eq 200) $sSG
    $q = Qr $sSG.Json.id_sesion
    Verificar "T10 - POST /api/QrSesion en esa sesion -> 409 y el mensaje nombra la sede a completar" `
        ($q.Codigo -eq 409 -and $q.Json.mensaje -match "no tiene coordenadas" -and $q.Json.mensaje -match [regex]::Escape("Sede sin coordenadas $sufijo")) $q
    Verificar "...y no quedo ningun QR" ((Llamar GET "/api/QrSesion?fkSesion=$($sSG.Json.id_sesion)").Codigo -eq 204)
    $null = Llamar PATCH "/api/Sede/$sinGeo" @{ latitud = $lat; longitud = $lng }
    $q = Qr $sSG.Json.id_sesion
    Verificar "Al completar las coordenadas de la sede, el QR SI se genera (200, canal QR)" ($q.Codigo -eq 200 -and $q.Json.canal -eq "QR" -and $q.Json.token) $q
    $tokSG = $q.Json.token         # se usa mas abajo: las coordenadas se borran DESPUES de emitido este QR

    $sSV = NuevaSesion $idCat1 "PRESENCIAL" "VIRTUAL"
    $q = Qr $sSV.Json.id_sesion
    Verificar "Una sesion presencial en una sede VIRTUAL (dato inconsistente) -> 409 con su propio mensaje" `
        ($q.Codigo -eq 409 -and $q.Json.mensaje -match "es virtual") $q
    $sVi = NuevaSesion $idCat1 "VIRTUAL" "VIRTUAL"
    $qVi = Qr $sVi.Json.id_sesion
    Verificar "Una sesion VIRTUAL no necesita coordenadas: el QR se genera (canal QR, 073)" ($qVi.Codigo -eq 200 -and $qVi.Json.canal -eq "QR") $qVi

    # ----------------------------------------------------------------------
    Titulo "3. RF-S8 / D68 / P19: carga idempotente de estudiantes"
    $e1 = "V72" + (Sufijo 6); $cc1 = "9" + (Sufijo 9)
    $c = Cargar $e1 $cc1 "INDS"
    Verificar "Cargar un estudiante nuevo -> 200 CREADO con su id" ($c.Codigo -eq 200 -and $c.Json.accion -eq "CREADO" -and $c.Json.id_asistente -gt 0) $c
    $idE1 = $c.Json.id_asistente
    $c2 = Cargar $e1 $cc1 "INDS"
    Verificar "Cargarlo OTRA VEZ -> 200 ACTUALIZADO con el MISMO id (idempotente)" ($c2.Codigo -eq 200 -and $c2.Json.accion -eq "ACTUALIZADO" -and $c2.Json.id_asistente -eq $idE1) $c2
    Verificar "T16 - No se duplico nada: 1 persona, 1 documento, 1 programa, 1 vinculacion ESTUDIANTE vigente" `
        ((Llamar GET "/api/Asistente?q=$e1&limite=10").Json.datos.Count -eq 1 -and
         @((Llamar GET "/api/DocumentoAsistente?fkAsistente=$idE1").Json.datos).Count -eq 1 -and
         @((Llamar GET "/api/ProgramaAsistente?fkAsistente=$idE1").Json.datos).Count -eq 1 -and
         @((Llamar GET "/api/VinculacionAsistente?fkAsistente=$idE1").Json.datos | Where-Object { $_.fk_tipo_vinculacion -eq "ESTUDIANTE" -and -not $_.fecha_fin }).Count -eq 1)
    $ficha = Llamar GET "/api/Asistente/$idE1"
    Verificar "La persona nueva queda como 'Estudiante <carnet>', no externa y sin correo (el archivo no los trae)" `
        ($ficha.Json.nombres -eq "Estudiante $e1" -and $ficha.Json.es_externo -eq $false) $ficha

    $c = Cargar ("V72" + (Sufijo 6)) $cc1 "INDS"
    Verificar "T17 - Una CC que ya es de OTRA persona -> 409 y el mensaje lo dice" ($c.Codigo -eq 409 -and $c.Json.mensaje -match "ya pertenece a otra persona") $c
    $c = Cargar ("V72" + (Sufijo 6)) ("9" + (Sufijo 9)) "ZZZZ"
    Verificar "Un programa que no existe -> 409 con el motivo" ($c.Codigo -eq 409 -and $c.Json.mensaje -match "no existe") $c
    $c = Cargar ("V72" + (Sufijo 6)) ("9" + (Sufijo 9)) "INDS" "1999-1"
    Verificar "Un periodo que no existe -> 409 con el motivo" ($c.Codigo -eq 409 -and $c.Json.mensaje -match "periodo") $c
    Verificar "Un carnet con formato invalido (2 caracteres) -> 422 sin tocar la BD" ((Cargar "ab" ("9" + (Sufijo 9)) "INDS").Codigo -eq 422)
    Verificar "Falta el periodo -> 422" ((Llamar POST "/api/Asistente/cargar-estudiante" @{ id_asis = "V72" + (Sufijo 6); documento = "9" + (Sufijo 9); programa = "INDS" }).Codigo -eq 422)

    # T31 / P19: carnet que ya existia con otra CC y otro programa en el mismo periodo
    $e2 = "V72" + (Sufijo 6); $ccVieja = "8" + (Sufijo 9); $ccNueva = "7" + (Sufijo 9)
    $hoy = (Get-Date).ToString("yyyy-MM-dd")
    $pre = Llamar POST "/api/Asistente" @{
        id_asis = $e2; nombres = "Relleno"; apellidos = "ASIS $sufijo"; correo_personal = "relleno$sufijo@ejemplo.com"; es_externo = $false
        documentos = @(@{ fk_tipo_documento = "CC"; numero = $ccVieja })
        vinculaciones = @(@{ fk_tipo_vinculacion = "ESTUDIANTE"; fecha_ini = (Get-Date).AddDays(-30).ToString("yyyy-MM-dd") })
        programas = @(@{ fk_programa = "ADMO"; fk_periodo = "2026-2" }, @{ fk_programa = "COMU"; fk_periodo = "2026-1" })
    }
    Verificar "Se crea una persona previa con otra CC y programas en dos periodos (ADMO en 2026-2, COMU en 2026-1)" ($pre.Codigo -eq 200) $pre
    $idE2 = $pre.Json.id_asistente
    $c = Cargar $e2 $ccNueva "PSIC"
    Verificar "T31 - Cargar ese carnet con otra CC y otro programa -> 200 ACTUALIZADO" ($c.Codigo -eq 200 -and $c.Json.accion -eq "ACTUALIZADO" -and $c.Json.id_asistente -eq $idE2) $c
    $docs = @((Llamar GET "/api/DocumentoAsistente?fkAsistente=$idE2").Json.datos)
    Verificar "...la CC vieja queda NO vigente y la nueva vigente (no se borra nada)" `
        (@($docs | Where-Object { $_.numero -eq $ccVieja -and -not $_.vigente }).Count -eq 1 -and @($docs | Where-Object { $_.numero -eq $ccNueva -and $_.vigente }).Count -eq 1) $null
    $prg = @((Llamar GET "/api/ProgramaAsistente?fkAsistente=$idE2").Json.datos)
    Verificar "...el programa viejo DEL MISMO PERIODO (ADMO 2026-2) pasa a RETIRADO y el nuevo (PSIC) queda ACTIVO" `
        (@($prg | Where-Object { $_.fk_programa -eq "ADMO" -and $_.fk_periodo -eq "2026-2" -and $_.estado -eq "RETIRADO" }).Count -eq 1 -and
         @($prg | Where-Object { $_.fk_programa -eq "PSIC" -and $_.fk_periodo -eq "2026-2" -and $_.estado -eq "ACTIVO" }).Count -eq 1) $null
    Verificar "...el programa de OTRO periodo (COMU 2026-1) no se toca" `
        (@($prg | Where-Object { $_.fk_programa -eq "COMU" -and $_.fk_periodo -eq "2026-1" -and $_.estado -eq "ACTIVO" }).Count -eq 1) $null
    Verificar "...y el nombre real de la persona no se pisa" ((Llamar GET "/api/Asistente/$idE2").Json.nombres -eq "Relleno")

    # Estudiante con la vinculacion cerrada (T15) y un tercero para las pruebas de registro
    $e3 = "V72" + (Sufijo 6)
    $pre3 = Llamar POST "/api/Asistente" @{
        id_asis = $e3; nombres = "Sin"; apellidos = "Vinculacion $sufijo"; correo_personal = "sinvinc$sufijo@ejemplo.com"; es_externo = $false
        vinculaciones = @(@{ fk_tipo_vinculacion = "ESTUDIANTE"; fecha_ini = (Get-Date).AddDays(-10).ToString("yyyy-MM-dd") })
        programas = @(@{ fk_programa = "INDS"; fk_periodo = "2026-2" })
    }
    $idE3 = $pre3.Json.id_asistente
    $cierre = Llamar PATCH "/api/VinculacionAsistente/cerrar" @{ fk_asistente = $idE3; fk_tipo_vinculacion = "ESTUDIANTE"; fecha_ini = (Get-Date).AddDays(-10).ToString("yyyy-MM-dd"); fecha_fin = $hoy }
    Verificar "Se prepara un estudiante cuya vinculacion ESTUDIANTE ya esta cerrada" ($pre3.Codigo -eq 200 -and $cierre.Codigo -eq 200) $cierre

    # ----------------------------------------------------------------------
    Titulo "4. RF-S9 / RF-S10: registro publico sin clave, con ubicacion solo en sesiones presenciales"
    $sP = NuevaSesion $idCat1 "PRESENCIAL" "BELLO"
    $tokP = (Qr $sP.Json.id_sesion).Json.token
    $sP2 = NuevaSesion $idCat1 "PRESENCIAL" "BELLO"
    $tokP2 = (Qr $sP2.Json.id_sesion).Json.token
    $tokV = $qVi.Json.token
    $idSesV = $sVi.Json.id_sesion
    Verificar "Hay dos sesiones presenciales en BELLO y una virtual, cada una con su QR" ($tokP -and $tokP2 -and $tokV) $null

    $info = Llamar GET "/api/AsistenciaPublica/enlace/$tokP"
    Verificar "GET enlace/{token}: trae titulo de la catedra, canal QR, requiere ubicacion y ventana abierta" `
        ($info.Codigo -eq 200 -and $info.Json.titulo_catedra -eq "Catedra V72 $sufijo" -and $info.Json.canal -eq "QR" -and $info.Json.requiere_ubicacion -eq $true -and $info.Json.estado_ventana -eq "abierto") $info
    Verificar "...y NO trae las coordenadas de la sede" ($info.Texto -notmatch "latitud" -and $info.Texto -notmatch "6\.3331")
    $infoV = Llamar GET "/api/AsistenciaPublica/enlace/$tokV"
    Verificar "Una sesion virtual: canal QR pero no requiere ubicacion (073)" ($infoV.Json.canal -eq "QR" -and $infoV.Json.requiere_ubicacion -eq $false) $infoV
    Verificar "Un token que no existe -> 404 ENLACE_NO_EXISTE" ((Llamar GET "/api/AsistenciaPublica/enlace/00000000-0000-0000-0000-000000000000").Codigo -eq 404)

    # Aviso temprano
    $av = Llamar GET "/api/AsistenciaPublica/ubicacion?token=$tokP&latitud=$lat&longitud=$lng"
    Verificar "Aviso temprano DENTRO del radio: 200, dentro = true, distancia ~0" ($av.Codigo -eq 200 -and $av.Json.dentro -eq $true -and $av.Json.distancia_metros -lt 1) $av
    $av = Llamar GET "/api/AsistenciaPublica/ubicacion?token=$tokP&latitud=$latSanBenito&longitud=$lngSanBenito"
    Verificar "Aviso temprano FUERA del radio: 200, dentro = false, ~8,7 km y el mensaje en espanol" `
        ($av.Codigo -eq 200 -and $av.Json.dentro -eq $false -and $av.Json.distancia_metros -gt 8000 -and $av.Json.distancia_metros -lt 9000 -and $av.Json.mensaje -match "no est") $av
    $av = Llamar GET "/api/AsistenciaPublica/ubicacion?token=$tokV&latitud=$latSanBenito&longitud=$lngSanBenito"
    Verificar "Aviso temprano en una sesion virtual: no requiere ubicacion" ($av.Codigo -eq 200 -and $av.Json.requiere_ubicacion -eq $false -and $av.Json.dentro -eq $null) $av
    Verificar "Aviso temprano con solo la latitud -> 400" ((Llamar GET "/api/AsistenciaPublica/ubicacion?token=$tokP&latitud=6.3").Codigo -eq 400)

    # --- Estudiante, presencial ---
    $r = RegEst $tokP $e1 $lat $lng
    Verificar "T05 - Estudiante presencial DENTRO del radio -> 201 y guarda el registro" ($r.Codigo -eq 201 -and $r.Json.estado -eq "OK" -and $r.Json.id_registro -gt 0) $r
    $reg = Registro $r.Json.id_registro
    Verificar "...con la ubicacion, la distancia (~0 m), el canal QR, la identificacion por CARNET, el programa del dia y SIN clave" `
        ($reg.latitud -eq $lat -and $reg.longitud -eq $lng -and $reg.distancia_metros -lt 1 -and $reg.origen -eq "QR" -and $reg.identificado_por -eq "CARNET" -and
         $reg.fk_vinculacion_snapshot -eq "ESTUDIANTE" -and $reg.fk_programa_snapshot -eq "INDS" -and -not $reg.fk_clave -and $reg.user_agent -eq "verificar_v72/1.0" -and $reg.ip -eq "190.10.20.30") $reg
    $r = RegEst $tokP $e1 $lat $lng
    Verificar "T02 - El MISMO estudiante otra vez en la misma sesion -> 409 YA_REGISTRADO (no un 500)" ($r.Codigo -eq 409 -and $r.Json.estado -eq "YA_REGISTRADO") $r
    Verificar "...y sigue habiendo un solo registro suyo en esa sesion" ((ContarRegistros $sP.Json.id_sesion $idE1) -eq 1)
    $r = RegEst $tokP ("N" + (Sufijo 8)) $lat $lng
    Verificar "T01 - Un carnet que no existe -> 409 ESTUDIANTE_NO_EXISTE" ($r.Codigo -eq 409 -and $r.Json.estado -eq "ESTUDIANTE_NO_EXISTE") $r
    $r = RegEst $tokP $e2
    Verificar "T04 - Presencial SIN ubicacion -> 409 SIN_UBICACION" ($r.Codigo -eq 409 -and $r.Json.estado -eq "SIN_UBICACION") $r
    Verificar "...y no quedo registro" ((ContarRegistros $sP.Json.id_sesion $idE2) -eq 0)
    $r = RegEst $tokP $e2 $latSanBenito $lngSanBenito
    Verificar "T06 - Presencial FUERA del radio (a ~8,7 km) -> 409 FUERA_DE_RADIO con la distancia y el radio" `
        ($r.Codigo -eq 409 -and $r.Json.estado -eq "FUERA_DE_RADIO" -and $r.Json.distancia_metros -gt 8000 -and $r.Json.radio_metros -gt 0 -and $r.Json.mensaje -match "lugar de la c") $r
    Verificar "...y NO queda registro" ((ContarRegistros $sP.Json.id_sesion $idE2) -eq 0)
    $r = RegEst $tokP $e3 $lat $lng
    Verificar "T15 - Estudiante sin vinculacion vigente -> 409 SIN_VINCULACION_VIGENTE" ($r.Codigo -eq 409 -and $r.Json.estado -eq "SIN_VINCULACION_VIGENTE") $r
    $r = RegEst $tokP $e2 95 0
    Verificar "Ubicacion fuera de rango (latitud 95) -> 422" ($r.Codigo -eq 422) $r
    $r = Llamar POST "/api/AsistenciaPublica/estudiante" @{ token = $tokP; carnet = $e2; latitud = $lat }
    Verificar "Latitud sin longitud -> 422" ($r.Codigo -eq 422) $r
    Verificar "Carnet de 2 caracteres -> 422" ((RegEst $tokP "ab" $lat $lng).Codigo -eq 422)
    Verificar "Sin token -> 422" ((Llamar POST "/api/AsistenciaPublica/estudiante" @{ carnet = $e2; latitud = $lat; longitud = $lng }).Codigo -eq 422)
    Verificar "Una IP mal formada -> 400 (no un 500 del motor)" ((RegEst $tokP $e2 $lat $lng "esto-no-es-una-ip").Codigo -eq 400)
    Verificar "Un token que no existe -> 404" ((RegEst "00000000-0000-0000-0000-000000000000" $e2 $lat $lng).Codigo -eq 404)

    # --- Estudiante, virtual ---
    $r = RegEst $tokV $e1
    Verificar "T07 - Sesion VIRTUAL sin ubicacion -> 201" ($r.Codigo -eq 201 -and $r.Json.estado -eq "OK") $r
    $reg = Registro $r.Json.id_registro
    Verificar "...con latitud, longitud y distancia en NULL y el origen QR (el canal, 073)" ($reg.latitud -eq $null -and $reg.longitud -eq $null -and $reg.distancia_metros -eq $null -and $reg.origen -eq "QR") $reg
    $r = RegEst $tokV $e2 $latSanBenito $lngSanBenito
    Verificar "T08 - Sesion VIRTUAL con ubicacion enviada (aunque este lejos) -> 201" ($r.Codigo -eq 201) $r
    $reg = Registro $r.Json.id_registro
    Verificar "...las coordenadas se DESCARTAN (NULL): el dato significa 'verificado presencialmente'" ($reg.latitud -eq $null -and $reg.longitud -eq $null -and $reg.distancia_metros -eq $null) $reg

    # --- Radio propio de la sede (T11) y distancia guardada (T12) ---
    $sedeR10 = "V72R$sufijo"; $sedeR200 = "V72Q$sufijo"
    $null = Llamar POST "/api/Sede" @{ id_sede = $sedeR10; nombre = "Sede radio 10 $sufijo"; es_virtual = $false; activo = $true; latitud = $lat; longitud = $lng; radio_metros = 10 }
    $null = Llamar POST "/api/Sede" @{ id_sede = $sedeR200; nombre = "Sede radio global $sufijo"; es_virtual = $false; activo = $true; latitud = $lat; longitud = $lng }
    $sR10 = NuevaSesion $idCat2 "PRESENCIAL" $sedeR10
    $tokR10 = (Qr $sR10.Json.id_sesion).Json.token
    $sR200 = NuevaSesion $idCat2 "PRESENCIAL" $sedeR200
    $tokR200 = (Qr $sR200.Json.id_sesion).Json.token
    $r = RegEst $tokR10 $e1 ($lat + $dLat50m) $lng
    Verificar "T11 - A ~50 m con el radio PROPIO de la sede en 10 m -> 409 FUERA_DE_RADIO y el radio informado es 10" ($r.Codigo -eq 409 -and $r.Json.estado -eq "FUERA_DE_RADIO" -and $r.Json.radio_metros -eq 10) $r
    $r = RegEst $tokR200 $e1 ($lat + $dLat50m) $lng
    Verificar "T11 - A ~50 m con el radio GLOBAL (200 m, sede sin radio propio) -> 201" ($r.Codigo -eq 201) $r
    $reg = Registro $r.Json.id_registro
    Verificar "T12 - La distancia guardada es la real (~50 m, entre 45 y 55) y se devuelve en la respuesta" `
        ($reg.distancia_metros -gt 45 -and $reg.distancia_metros -lt 55 -and $r.Json.distancia_metros -gt 45 -and $r.Json.distancia_metros -lt 55) $reg

    # --- Dos sesiones de la misma catedra y de dos catedras (para la exportacion) ---
    $null = RegEst $tokP2 $e1 $lat $lng
    $null = RegEst $tokP2 $e2 $lat $lng

    # --- Docente ---
    $ccD = "5" + (Sufijo 9); $ccD2 = "4" + (Sufijo 9)
    $r = RegDoc $tokP $ccD "Docente Prueba $sufijo" "docente$sufijo@usbmed.edu.co" "F_ING" $lat $lng
    Verificar "Docente presencial dentro del radio -> 201" ($r.Codigo -eq 201 -and $r.Json.estado -eq "OK") $r
    $reg = Registro $r.Json.id_registro
    $idD = $reg.fk_asistente
    Verificar "...registro DOCENTE con la facultad que declaro, identificado por DECLARADO y sin programa (DOCENTE ya no exige programa)" `
        ($reg.fk_vinculacion_snapshot -eq "DOCENTE" -and $reg.fk_facultad_snapshot -eq "F_ING" -and $reg.identificado_por -eq "DECLARADO" -and -not $reg.fk_programa_snapshot -and -not $reg.procedencia) $reg
    $fd = Llamar GET "/api/Asistente/$idD"
    Verificar "...la persona se crea con el nombre completo, sin codigo ASIS y como externa (no tiene id_asis)" `
        ($fd.Json.nombre_completo_asis -eq "Docente Prueba $sufijo" -and -not $fd.Json.id_asis -and $fd.Json.es_externo -eq $true -and $fd.Json.correo_personal -eq "docente$sufijo@usbmed.edu.co") $fd
    Verificar "...con la vinculacion DOCENTE vigente" (@((Llamar GET "/api/VinculacionAsistente?fkAsistente=$idD").Json.datos | Where-Object { $_.fk_tipo_vinculacion -eq "DOCENTE" -and -not $_.fecha_fin }).Count -eq 1)
    $r = RegDoc $tokP $ccD "Otro Nombre" "otro@x.co" "F_EDU" $lat $lng
    Verificar "Docente repetido en la misma sesion -> 409 YA_REGISTRADO" ($r.Codigo -eq 409 -and $r.Json.estado -eq "YA_REGISTRADO") $r
    $r = RegDoc $tokP2 $ccD "Otro Nombre" "otro@x.co" "F_EDU" $lat $lng
    Verificar "T13 - La misma cedula en OTRA sesion -> 201 y se reutiliza la persona" ($r.Codigo -eq 201) $r
    $reg2 = Registro $r.Json.id_registro
    Verificar "...misma persona, un solo documento, un solo DOCENTE vigente, el nombre NO se pisa y la facultad del dia queda en el registro" `
        ($reg2.fk_asistente -eq $idD -and @((Llamar GET "/api/DocumentoAsistente?fkAsistente=$idD").Json.datos).Count -eq 1 -and
         (Llamar GET "/api/Asistente/$idD").Json.nombre_completo_asis -eq "Docente Prueba $sufijo" -and $reg2.fk_facultad_snapshot -eq "F_EDU") $reg2
    $r = RegDoc $tokP $ccD2 "Docente Celular" "300 123 4567" "F_ING" $lat $lng
    Verificar "T14 - Docente que da solo su celular -> 201" ($r.Codigo -eq 201) $r
    $fc = Llamar GET "/api/Asistente/$((Registro $r.Json.id_registro).fk_asistente)"
    Verificar "...queda en 'celular' y sin correo (el CHECK de correo sigue protegiendo a los demas)" ($fc.Json.celular -eq "300 123 4567" -and -not $fc.Json.correo_personal -and -not $fc.Json.correo_institucional) $fc
    $r = RegDoc $tokP ("3" + (Sufijo 9)) "X" "x@x.co" "F_NOEXISTE" $lat $lng
    Verificar "Una facultad que no existe -> 400 DATOS_INVALIDOS" ($r.Codigo -eq 400 -and $r.Json.estado -eq "DATOS_INVALIDOS") $r
    $r = RegDoc $tokP ("3" + (Sufijo 9)) "X" "esto no es un contacto" "F_ING" $lat $lng
    Verificar "Un contacto que no es correo ni celular -> 400 DATOS_INVALIDOS" ($r.Codigo -eq 400 -and $r.Json.estado -eq "DATOS_INVALIDOS") $r
    $r = RegDoc $tokP ("3" + (Sufijo 9)) "X" "x@x.co" "F_ING"
    Verificar "Docente presencial sin ubicacion -> 409 SIN_UBICACION (se aplica a los tres tipos)" ($r.Codigo -eq 409 -and $r.Json.estado -eq "SIN_UBICACION") $r
    $r = RegDoc $tokP ("3" + (Sufijo 9)) "X" "x@x.co" "F_ING" $latSanBenito $lngSanBenito
    Verificar "Docente presencial fuera del radio -> 409 FUERA_DE_RADIO" ($r.Codigo -eq 409 -and $r.Json.estado -eq "FUERA_DE_RADIO") $r
    Verificar "Docente sin facultad -> 422" ((Llamar POST "/api/AsistenciaPublica/docente" @{ token = $tokP; cedula = "3" + (Sufijo 9); nombre = "X"; contacto = "x@x.co" }).Codigo -eq 422)

    # --- Externo ---
    $ccX = "2" + (Sufijo 9)
    $r = RegExt $tokP $ccX "Externo Prueba $sufijo" "externo$sufijo@correo.com" "Colegio San Jose" $lat $lng
    Verificar "Externo presencial dentro del radio -> 201" ($r.Codigo -eq 201 -and $r.Json.estado -eq "OK") $r
    $reg = Registro $r.Json.id_registro
    Verificar "...registro EXTERNO con la procedencia, sin facultad ni programa" `
        ($reg.fk_vinculacion_snapshot -eq "EXTERNO" -and $reg.procedencia -eq "Colegio San Jose" -and -not $reg.fk_facultad_snapshot -and -not $reg.fk_programa_snapshot -and $reg.identificado_por -eq "DECLARADO") $reg
    Verificar "Externo repetido en la misma sesion -> 409 YA_REGISTRADO" ((RegExt $tokP $ccX "Externo Prueba" "e@x.co" "Colegio" $lat $lng).Json.estado -eq "YA_REGISTRADO")
    Verificar "T21 - El mismo externo en la 2a sesion de la catedra (diciendo ahora otra procedencia) -> 201 (se exporta una sola vez)" ((RegExt $tokP2 $ccX "Externo Prueba" "e@x.co" "Colegio" $lat $lng).Codigo -eq 201)
    $r = RegExt $tokV ("1" + (Sufijo 9)) "Externo Virtual" "3001234567" "Empresa X"
    Verificar "Externo en sesion virtual sin ubicacion -> 201" ($r.Codigo -eq 201) $r
    Verificar "Externo sin procedencia -> 422" ((Llamar POST "/api/AsistenciaPublica/externo" @{ token = $tokP; cedula = "1" + (Sufijo 9); nombre = "X"; contacto = "x@x.co" }).Codigo -eq 422)

    # --- Enlace revocado y fuera de ventana (T03) ---
    $sRev = NuevaSesion $idCat2 "PRESENCIAL" "BELLO"
    $tokRevViejo = (Qr $sRev.Json.id_sesion).Json.token
    $qNuevo = Qr $sRev.Json.id_sesion $true
    Verificar "Regenerar el QR de una sesion da otro token (el anterior queda revocado)" ($qNuevo.Codigo -eq 200 -and $qNuevo.Json.token -ne $tokRevViejo) $qNuevo
    $r = RegEst $tokRevViejo $e1 $lat $lng
    Verificar "T03 - El token revocado -> 409 ENLACE_REVOCADO" ($r.Codigo -eq 409 -and $r.Json.estado -eq "ENLACE_REVOCADO") $r
    Verificar "...tambien en el aviso temprano -> 409" ((Llamar GET "/api/AsistenciaPublica/ubicacion?token=$tokRevViejo&latitud=$lat&longitud=$lng").Codigo -eq 409)
    Verificar "El token nuevo si sirve (201)" ((RegEst $qNuevo.Json.token $e1 $lat $lng).Codigo -eq 201)

    # Se restaura la ventana normal del QR (15 min antes del fin) y se emite el QR de una sesion futura: su ventana aun no abre
    if ($null -ne $paramAntes) { $null = Llamar PATCH "/api/Parametro/QR_MINUTOS_ANTES_FIN" @{ valor = "$paramAntes" } }
    $sLejos = NuevaSesion $idCat2 "PRESENCIAL" "BELLO"
    $qLejos = Qr $sLejos.Json.id_sesion
    $r = RegEst $qLejos.Json.token $e1 $lat $lng
    Verificar "T03 - Un QR cuya ventana aun no abre -> 409 FUERA_DE_VENTANA" ($r.Codigo -eq 409 -and $r.Json.estado -eq "FUERA_DE_VENTANA") $r
    Verificar "...y la pagina publica lo describe como 'pendiente'" ((Llamar GET "/api/AsistenciaPublica/enlace/$($qLejos.Json.token)").Json.estado_ventana -eq "pendiente")

    # Las coordenadas de la sede se borran despues de emitido el QR: no se acepta sin comprobar el lugar
    $null = Llamar PATCH "/api/Sede/$sinGeo" @{ es_virtual = $true }
    $null = Llamar PATCH "/api/Sede/$sinGeo" @{ es_virtual = $false }
    $r = RegEst $tokSG $e1 $lat $lng
    Verificar "Si la sede se queda sin coordenadas DESPUES de emitido el QR, el registro se rechaza (SEDE_SIN_COORDENADAS), no se acepta sin comprobar el lugar" `
        ($r.Codigo -eq 409 -and $r.Json.estado -eq "SEDE_SIN_COORDENADAS") $r

    # ----------------------------------------------------------------------
    Titulo "5. RF-S13 / RF-S14: consultas de exportacion"
    $cats = Llamar GET "/api/Exportacion/catedras?periodo=2026-2"
    $mia = @($cats.Json.datos | Where-Object { $_.id_catedra -eq $idEvento1 })[0]
    Verificar "GET /api/Exportacion/catedras lista mi catedra con su id de 9 digitos (id_evento_asis) y titulo" `
        ($cats.Codigo -eq 200 -and $mia -and $mia.titulo -eq "Catedra V72 $sufijo" -and $mia.id_catedra.Length -eq 9) $cats
    Verificar "T19/T21 - Conteos SIN repetir personas: estudiantes 2 (e1 y e2, aunque e1 fue a 2 sesiones), docentes 2, externos 2" `
        ($mia.estudiantes -eq 2 -and $mia.docentes -eq 2 -and $mia.externos -eq 2 -and $mia.otros -eq 0) $cats
    $mia2 = @($cats.Json.datos | Where-Object { $_.id_catedra -eq $idEvento2 })[0]
    Verificar "T18 - e1 asistio a la 2a catedra tambien: aparece alli (estudiantes >= 1)" ($mia2 -and $mia2.estudiantes -ge 1) $cats

    $all = Llamar GET "/api/Exportacion/asistentes?periodo=2026-2&desde=$([int64]$idEvento1 - 1)&limite=1"
    $cabeza = $all.Json.datos[0]
    Verificar "Asistentes: la pagina de 1 catedra que empieza justo antes de mi id trae MI catedra" ($all.Codigo -eq 200 -and $cabeza.id_catedra -eq $idEvento1) $all
    $est = @($cabeza.asistentes | Where-Object { $_.tipo -eq "ESTUDIANTE" })
    Verificar "T19 - e1 aparece UNA sola vez en su catedra aunque fue a 2 sesiones; trae CC, carnet y el programa tal como se cargo (INDS)" `
        (@($est | Where-Object { $_.carnet -eq $e1 }).Count -eq 1 -and (@($est | Where-Object { $_.carnet -eq $e1 })[0].cc -eq $cc1) -and (@($est | Where-Object { $_.carnet -eq $e1 })[0].programa -eq "INDS")) $all
    $doc = @($cabeza.asistentes | Where-Object { $_.tipo -eq "DOCENTE" -and $_.cc -eq $ccD })
    Verificar "T22 - El docente trae nombre, contacto y la NOMBRE de la facultad; aparece una vez aunque fue a 2 sesiones" `
        ($doc.Count -eq 1 -and $doc[0].nombre -eq "Docente Prueba $sufijo" -and $doc[0].contacto -eq "docente$sufijo@usbmed.edu.co" -and $doc[0].facultad -match "Facultad") $all
    $ext = @($cabeza.asistentes | Where-Object { $_.tipo -eq "EXTERNO" -and $_.cc -eq $ccX })
    Verificar "T21/T22 - El externo (2 sesiones) aparece UNA vez, con la procedencia de su registro MAS RECIENTE y el contacto que dio al crearse" ($ext.Count -eq 1 -and $ext[0].procedencia -eq "Colegio" -and $ext[0].contacto -eq "externo$sufijo@correo.com") $all

    $pag = Llamar GET "/api/Exportacion/asistentes?periodo=2026-2&limite=1"
    Verificar "Paginacion: con limite 1 viene 1 catedra y 'siguiente' trae su id para pedir la proxima" `
        ($pag.Codigo -eq 200 -and $pag.Json.datos.Count -eq 1 -and $pag.Json.siguiente -eq $pag.Json.datos[0].id_catedra) $pag
    $pag2 = Llamar GET "/api/Exportacion/asistentes?periodo=2026-2&desde=$($pag.Json.siguiente)&limite=1"
    Verificar "...y la pagina siguiente trae OTRA catedra (de id mayor)" ($pag2.Codigo -eq 200 -and ($pag2.Json.datos.Count -eq 0 -or $pag2.Json.datos[0].id_catedra -gt $pag.Json.datos[0].id_catedra)) $pag2
    Verificar "Un periodo sin catedras -> 204 en el catalogo y pagina vacia en los asistentes" `
        ((Llamar GET "/api/Exportacion/catedras?periodo=2001-1").Codigo -eq 204 -and (Llamar GET "/api/Exportacion/asistentes?periodo=2001-1").Json.datos.Count -eq 0)
    Verificar "Sin periodo -> 400" ((Llamar GET "/api/Exportacion/catedras").Codigo -eq 400 -and (Llamar GET "/api/Exportacion/asistentes").Codigo -eq 400)
    Verificar "limite 0 y limite 501 -> 400" ((Llamar GET "/api/Exportacion/asistentes?periodo=2026-2&limite=0").Codigo -eq 400 -and (Llamar GET "/api/Exportacion/asistentes?periodo=2026-2&limite=501").Codigo -eq 400)
    Verificar "Un 'desde' que no es un id de catedra -> 400" ((Llamar GET "/api/Exportacion/asistentes?periodo=2026-2&desde=abc").Codigo -eq 400)
    Verificar "Un periodo de mas de 6 caracteres -> 400" ((Llamar GET "/api/Exportacion/catedras?periodo=2026-2-xx").Codigo -eq 400)
}
finally {
    # Se deja el parametro como estaba, pase lo que pase.
    if ($null -ne $paramAntes) {
        $rest = Llamar PATCH "/api/Parametro/QR_MINUTOS_ANTES_FIN" @{ valor = "$paramAntes" }
        Write-Host "`n  (QR_MINUTOS_ANTES_FIN restaurado a $paramAntes -> HTTP $($rest.Codigo))" -ForegroundColor DarkGray
    }
}

# --------------------------------------------------------------------------
Write-Host ""
$color = if ($script:fallidas -eq 0) { "Green" } else { "Red" }
Write-Host "$($script:pasadas) pasadas, $($script:fallidas) fallidas." -ForegroundColor $color
if ($script:fallidas -gt 0) { exit 1 } else { exit 0 }
