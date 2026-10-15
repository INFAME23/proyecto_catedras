# ==========================================================================
#  verificar_retrofit_fase4.ps1  -  Proyecto Catedras - Retrofit criterio 3, Fase 4
# --------------------------------------------------------------------------
#  Verificacion contra la API REAL (Docker + PostgreSQL) de las 9 tablas del
#  lote (PLAN_RETROFIT_SP.md, Fase 4), mas la REGRESION DE LA V1 (seccion 9
#  de VERSION_2.md) automatizada a partir de version1/7_quickstart.md.
#
#    R.  Regresion v1: Sede (CRUD, 422, 500 por duplicado) + catalogos v1
#    1.  Asistente              (eliminar ahora es LOGICO)
#    2.  DocumentoAsistente
#    3.  VinculacionAsistente   (cerrar = sp_eliminar_vinculacion_asistente)
#    4.  ProgramaAsistente      (cambiar-estado = sp_actualizar_programa_asistente)
#    5.  ConsentimientoDatos    (sigue SIN PUT/PATCH/DELETE)
#    6.  LoteCargaAsistente + NovedadCarga
#    7.  LoteMigracion          (nueva)
#    8.  DetalleMigracion       (nueva)
#
#  Uso (con la API ya levantada, desde la carpeta api_catedras):
#      powershell -ExecutionPolicy Bypass -File .\pruebas\verificar_retrofit_fase4.ps1
#  Otro host/puerto:  ... -Api http://127.0.0.1:8142
#
#  Escribe datos (con sufijos aleatorios, se puede correr varias veces).
#  Para dejar la BD como nueva:  docker compose down -v ; docker compose up -d --build
#  (sin tildes a proposito: PowerShell 5.1 lee mal los .ps1 sin BOM)
# ==========================================================================
param([string]$Api = "http://127.0.0.1:8142")

$ErrorActionPreference = "Stop"
$script:pasadas = 0
$script:fallidas = 0
$sufijo = Get-Random -Minimum 10000 -Maximum 99999

# Llama a la API y devuelve { Codigo, Texto, Json } SIN lanzar excepcion en 4xx/5xx.
# -Depth 10: el ConvertTo-Json por defecto corta a 2 niveles (y las listas
# de detalles de Asistente / novedades del lote van en el nivel 3).
function Llamar {
    param([string]$Metodo, [string]$Ruta, $Cuerpo = $null)

    $parametros = @{ Method = $Metodo; Uri = "$Api$Ruta"; UseBasicParsing = $true }
    if ($null -ne $Cuerpo) {
        $parametros.ContentType = "application/json; charset=utf-8"
        $parametros.Body = [System.Text.Encoding]::UTF8.GetBytes(($Cuerpo | ConvertTo-Json -Compress -Depth 10))
    }

    $texto = $null
    try {
        $r = Invoke-WebRequest @parametros
        $codigo = [int]$r.StatusCode
        $texto = $r.Content
    }
    catch {
        if ($null -eq $_.Exception.Response) { throw }
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

function NoExiste($r) { return ($r.Codigo -eq 404 -or $r.Codigo -eq 405) }

function Titulo([string]$t) { Write-Host "`n== $t ==" -ForegroundColor Cyan }

# --------------------------------------------------------------------------
Titulo "0. La API responde"
$raiz = Llamar GET "/"
Verificar "GET / responde 200 ($($raiz.Json.version))" ($raiz.Codigo -eq 200) $raiz

# --------------------------------------------------------------------------
Titulo "R. REGRESION V1 (version1/7_quickstart.md, pasos 2-5)"
$sedes = Llamar GET "/api/Sede"
Verificar "GET /api/Sede -> 200 con las sedes de ejemplo (SAN_BENITO, BELLO, VIRTUAL)" ($sedes.Codigo -eq 200 -and (@($sedes.Json.datos | ForEach-Object { $_.id_sede }) -contains "SAN_BENITO")) $sedes
$idSede = "R4S$sufijo"
$c = Llamar POST "/api/Sede" @{ id_sede = $idSede; nombre = "Sede regresion $sufijo"; direccion = "Calle 1"; es_virtual = $false; activo = $true }
Verificar "POST /api/Sede -> 200" ($c.Codigo -eq 200) $c
$p = Llamar PUT "/api/Sede/$idSede" @{ nombre = "Sede regresion $sufijo (PUT)"; direccion = $null; es_virtual = $true; activo = $true }
$g = Llamar GET "/api/Sede/$idSede"
Verificar "PUT reemplaza todo (direccion queda null)" ($p.Codigo -eq 200 -and $g.Json.es_virtual -eq $true -and $null -eq $g.Json.direccion) $g
$pa = Llamar PATCH "/api/Sede/$idSede" @{ direccion = "Calle 2" }
$g = Llamar GET "/api/Sede/$idSede"
Verificar "PATCH cambia solo 'direccion' (y conserva el nombre del PUT)" ($pa.Codigo -eq 200 -and $g.Json.direccion -eq "Calle 2" -and $g.Json.nombre -like "*(PUT)") $g
$d = Llamar DELETE "/api/Sede/$idSede"
$g = Llamar GET "/api/Sede/$idSede"
# Retrofit fase 1: DELETE de sede es LOGICO -> el GET sigue en 200 con activo=false.
Verificar "DELETE -> 200 y el GET posterior refleja el cambio (activo=false, borrado logico)" ($d.Codigo -eq 200 -and $g.Codigo -eq 200 -and $g.Json.activo -eq $false) $g
$d2 = Llamar DELETE "/api/Sede/$idSede"
Verificar "segundo DELETE -> 404 (ya estaba inactiva)" ($d2.Codigo -eq 404) $d2
$v = Llamar POST "/api/Sede" @{ nombre = "" }
Verificar "POST sin id_sede y con nombre vacio -> 422 sin tocar la BD" ($v.Codigo -eq 422) $v
$dup = Llamar POST "/api/Sede" @{ id_sede = "SAN_BENITO"; nombre = "Otra $sufijo"; es_virtual = $false; activo = $true }
Verificar "POST con id_sede repetido -> 500 (pk_sede, criterio 7 aun pendiente)" ($dup.Codigo -eq 500) $dup
foreach ($recurso in @("Modalidad", "EstadoProceso", "Encuesta")) {
    $r = Llamar GET "/api/$recurso"
    Verificar "GET /api/$recurso -> 200 o 204" ($r.Codigo -eq 200 -or $r.Codigo -eq 204) $r
}

# --------------------------------------------------------------------------
Titulo "1. Asistente (listar/consultar/actualizar/eliminar por SP; crear = sp_guardar_asistente)"
$idAsis = "R4A$sufijo"
$a = Llamar POST "/api/Asistente" @{
    id_asis = $idAsis; nombres = "Rita"; apellidos = "Retrofit$sufijo"
    correo_institucional = "rita.$sufijo@prueba.edu.co"; es_externo = $false
    documentos = @(@{ fk_tipo_documento = "CC"; numero = "R4-$sufijo" })
}
$idA = $a.Json.id_asistente
Verificar "POST /api/Asistente con 1 documento -> 200 con id_asistente" ($a.Codigo -eq 200 -and $idA -gt 0) $a
$l = Llamar GET "/api/Asistente?q=retrofit$sufijo&limite=10"
Verificar "GET ?q= (ILIKE dentro del SP) lo encuentra" ($l.Codigo -eq 200 -and $l.Json.total -eq 1 -and $l.Json.datos[0].id_asistente -eq $idA) $l
$l1 = Llamar GET "/api/Asistente?limite=5&pagina=1"
$l2 = Llamar GET "/api/Asistente?limite=5&pagina=2"
Verificar "Paginacion: la pagina 2 empieza despues de la 1" ($l1.Json.total -eq 5 -and $l2.Json.datos[0].id_asistente -gt $l1.Json.datos[4].id_asistente) $l2
$pa = Llamar PATCH "/api/Asistente/$idA" @{ celular = "3001234567" }
$g = Llamar GET "/api/Asistente/$idA"
Verificar "PATCH parcial: cambia celular y conserva nombres/correo (consulta+mezcla+SP)" ($pa.Codigo -eq 200 -and $g.Json.celular -eq "3001234567" -and $g.Json.nombres -eq "Rita" -and $g.Json.correo_institucional -like "rita.*") $g
$pu = Llamar PUT "/api/Asistente/$idA" @{ id_asis = $idAsis; nombres = "Rita M."; apellidos = "Retrofit$sufijo"; correo_institucional = "rita.$sufijo@prueba.edu.co"; es_externo = $false; activo = $true }
$g = Llamar GET "/api/Asistente/$idA"
Verificar "PUT reemplaza todo (celular vuelve a null)" ($pu.Codigo -eq 200 -and $g.Json.nombres -eq "Rita M." -and $null -eq $g.Json.celular) $g
$vac = Llamar PATCH "/api/Asistente/$idA" @{ }
Verificar "PATCH vacio -> 400" ($vac.Codigo -eq 400) $vac
# Externo SIN id_asis; un PATCH {es_externo:false} deja la fila MEZCLADA
# rompiendo chk_asistente_externo -> el CHECK sigue siendo la ultima muralla.
$ext = Llamar POST "/api/Asistente" @{ nombres = "Ext"; apellidos = "Externo$sufijo"; correo_personal = "ext.$sufijo@gmail.com"; es_externo = $true }
$chk = Llamar PATCH "/api/Asistente/$($ext.Json.id_asistente)" @{ es_externo = $false }
Verificar "PATCH que rompe un CHECK sobre la fila mezclada -> 500 (chk_asistente_externo)" ($ext.Codigo -eq 200 -and $chk.Codigo -eq 500) $chk
$pn = Llamar PATCH "/api/Asistente/99999999" @{ celular = "1" }
Verificar "PATCH a un asistente inexistente -> 404" ($pn.Codigo -eq 404) $pn
$d = Llamar DELETE "/api/Asistente/$idA"
$g = Llamar GET "/api/Asistente/$idA"
Verificar "DELETE -> 200 y es LOGICO: GET sigue en 200 con activo=false" ($d.Codigo -eq 200 -and $g.Codigo -eq 200 -and $g.Json.activo -eq $false) $g
$docs = Llamar GET "/api/DocumentoAsistente?fkAsistente=$idA"
Verificar "...y su documento NO se borro en cascada" ($docs.Codigo -eq 200 -and $docs.Json.total -eq 1) $docs
$l = Llamar GET "/api/Asistente?q=retrofit$sufijo"
Verificar "...y ya no aparece en la lista (solo activos)" ($l.Codigo -eq 204) $l
$d2 = Llamar DELETE "/api/Asistente/$idA"
Verificar "segundo DELETE -> 404" ($d2.Codigo -eq 404) $d2

# Asistente "vivo" para el resto de las secciones.
$b = Llamar POST "/api/Asistente" @{ id_asis = "R4B$sufijo"; nombres = "Beto"; apellidos = "Retrofit$sufijo"; correo_institucional = "beto.$sufijo@prueba.edu.co"; es_externo = $false }
$idB = $b.Json.id_asistente

# --------------------------------------------------------------------------
Titulo "2. DocumentoAsistente (sin 'activo' -> DELETE fisico)"
$doc = Llamar POST "/api/DocumentoAsistente" @{ fk_asistente = $idB; fk_tipo_documento = "CC"; numero = "R4D-$sufijo" }
$idDoc = $doc.Json.id_documento
Verificar "POST -> 200 con id_documento y registrado_en" ($doc.Codigo -eq 200 -and $idDoc -gt 0 -and $doc.Json.registrado_en) $doc
$doc2 = Llamar POST "/api/DocumentoAsistente" @{ fk_asistente = $idB; fk_tipo_documento = "TI"; numero = "R4E-$sufijo" }
Verificar "segundo vigente del mismo asistente -> 500 (ux_documento_vigente)" ($doc2.Codigo -eq 500) $doc2
$pa = Llamar PATCH "/api/DocumentoAsistente/$idDoc" @{ vigente = $false }
$g = Llamar GET "/api/DocumentoAsistente/$idDoc"
Verificar "PATCH vigente=false conserva tipo y numero" ($pa.Codigo -eq 200 -and $g.Json.vigente -eq $false -and $g.Json.numero -eq "R4D-$sufijo") $g
$pu = Llamar PUT "/api/DocumentoAsistente/$idDoc" @{ fk_tipo_documento = "CE"; numero = "R4F-$sufijo"; vigente = $false }
$g = Llamar GET "/api/DocumentoAsistente/$idDoc"
Verificar "PUT reemplaza los 3 campos" ($pu.Codigo -eq 200 -and $g.Json.fk_tipo_documento -eq "CE" -and $g.Json.numero -eq "R4F-$sufijo") $g
$l = Llamar GET "/api/DocumentoAsistente?fkAsistente=$idB"
Verificar "GET ?fkAsistente= filtra" ($l.Codigo -eq 200 -and $l.Json.total -eq 1) $l
$d = Llamar DELETE "/api/DocumentoAsistente/$idDoc"
$g = Llamar GET "/api/DocumentoAsistente/$idDoc"
Verificar "DELETE -> 200 y es FISICO: GET -> 404" ($d.Codigo -eq 200 -and $g.Codigo -eq 404) $g

# --------------------------------------------------------------------------
Titulo "3. VinculacionAsistente (llave compuesta; cerrar = sp_eliminar_)"
$vin = Llamar POST "/api/VinculacionAsistente" @{ fk_asistente = $idB; fk_tipo_vinculacion = "ESTUDIANTE"; fecha_ini = "2026-01-20" }
Verificar "POST abre la vinculacion (fecha_fin null)" ($vin.Codigo -eq 200 -and $null -eq $vin.Json.vinculacion.fecha_fin) $vin
$dupV = Llamar POST "/api/VinculacionAsistente" @{ fk_asistente = $idB; fk_tipo_vinculacion = "ESTUDIANTE"; fecha_ini = "2026-01-20" }
Verificar "misma llave -> 500 (PK)" ($dupV.Codigo -eq 500) $dupV
$cer = Llamar PATCH "/api/VinculacionAsistente/cerrar" @{ fk_asistente = $idB; fk_tipo_vinculacion = "ESTUDIANTE"; fecha_ini = "2026-01-20"; fecha_fin = "2026-06-30" }
Verificar "PATCH /cerrar -> 200 con fecha_fin (fechas viajan con CAST a date)" ($cer.Codigo -eq 200 -and $cer.Json.vinculacion.fecha_fin -eq "2026-06-30") $cer
$cer2 = Llamar PATCH "/api/VinculacionAsistente/cerrar" @{ fk_asistente = $idB; fk_tipo_vinculacion = "ESTUDIANTE"; fecha_ini = "2026-01-20" }
Verificar "cerrar otra vez -> 400" ($cer2.Codigo -eq 400) $cer2
$cer3 = Llamar PATCH "/api/VinculacionAsistente/cerrar" @{ fk_asistente = $idB; fk_tipo_vinculacion = "DOCENTE"; fecha_ini = "2026-01-20" }
Verificar "cerrar una llave inexistente -> 404" ($cer3.Codigo -eq 404) $cer3
$l = Llamar GET "/api/VinculacionAsistente?fkAsistente=$idB"
Verificar "GET ?fkAsistente= -> la fila sigue (cerrar NO borra)" ($l.Codigo -eq 200 -and $l.Json.total -eq 1 -and $l.Json.datos[0].fecha_fin -eq "2026-06-30") $l
Verificar "DELETE /api/VinculacionAsistente no existe (404/405, ADR D16)" (NoExiste (Llamar DELETE "/api/VinculacionAsistente")) $null

# --------------------------------------------------------------------------
Titulo "4. ProgramaAsistente (llave compuesta; cambiar-estado = sp_actualizar_)"
$prog = (Llamar GET "/api/ProgramaAcademico?limite=1").Json.datos[0].codigo
$pr = Llamar POST "/api/ProgramaAsistente" @{ fk_asistente = $idB; fk_programa = $prog; fk_periodo = "2026-1" }
Verificar "POST matricula (estado ACTIVO por defecto)" ($pr.Codigo -eq 200 -and $pr.Json.programa.estado -eq "ACTIVO") $pr
$ce = Llamar PATCH "/api/ProgramaAsistente/cambiar-estado" @{ fk_asistente = $idB; fk_programa = $prog; fk_periodo = "2026-1"; estado = "GRADUADO" }
Verificar "PATCH /cambiar-estado -> 200 GRADUADO" ($ce.Codigo -eq 200 -and $ce.Json.programa.estado -eq "GRADUADO") $ce
$ce2 = Llamar PATCH "/api/ProgramaAsistente/cambiar-estado" @{ fk_asistente = $idB; fk_programa = $prog; fk_periodo = "2026-1"; estado = "GRADUADO" }
Verificar "mismo estado -> 400" ($ce2.Codigo -eq 400) $ce2
$ce3 = Llamar PATCH "/api/ProgramaAsistente/cambiar-estado" @{ fk_asistente = $idB; fk_programa = $prog; fk_periodo = "2025-1"; estado = "ACTIVO" }
Verificar "llave inexistente -> 404" ($ce3.Codigo -eq 404) $ce3
$l = Llamar GET "/api/ProgramaAsistente?fkAsistente=$idB"
Verificar "GET ?fkAsistente= refleja el estado nuevo" ($l.Codigo -eq 200 -and $l.Json.datos[0].estado -eq "GRADUADO") $l

# --------------------------------------------------------------------------
Titulo "5. ConsentimientoDatos (Ley 1581: solo listar/obtener/crear)"
$co = Llamar POST "/api/ConsentimientoDatos" @{ fk_asistente = $idB; version_politica = "v$sufijo"; ip = "10.0.0.1" }
$idCo = $co.Json.id_consentimiento
Verificar "POST -> 200 con id_consentimiento y aceptado_en" ($co.Codigo -eq 200 -and $idCo -gt 0 -and $co.Json.aceptado_en) $co
$g = Llamar GET "/api/ConsentimientoDatos/$idCo"
Verificar "GET/{id}: ip como texto, canal WEB por defecto" ($g.Codigo -eq 200 -and $g.Json.ip -eq "10.0.0.1" -and $g.Json.canal -eq "WEB") $g
$dupC = Llamar POST "/api/ConsentimientoDatos" @{ fk_asistente = $idB; version_politica = "v$sufijo" }
Verificar "misma version -> 500 (uq_consentimiento_ver)" ($dupC.Codigo -eq 500) $dupC
Verificar "PUT /api/ConsentimientoDatos/{id} no existe (404/405)" (NoExiste (Llamar PUT "/api/ConsentimientoDatos/$idCo" @{ canal = "WEB" })) $null
Verificar "PATCH /api/ConsentimientoDatos/{id} no existe (404/405)" (NoExiste (Llamar PATCH "/api/ConsentimientoDatos/$idCo" @{ canal = "WEB" })) $null
Verificar "DELETE /api/ConsentimientoDatos/{id} no existe (404/405)" (NoExiste (Llamar DELETE "/api/ConsentimientoDatos/$idCo")) $null

# --------------------------------------------------------------------------
Titulo "6. LoteCargaAsistente + NovedadCarga"
$lo = Llamar POST "/api/LoteCargaAsistente" @{ nombre_archivo = "retrofit_$sufijo.xlsx" }
$idLo = $lo.Json.id_lote_carga
Verificar "POST abre el lote (ahora por sp_crear_lote_carga_asistente)" ($lo.Codigo -eq 200 -and $idLo -gt 0 -and $lo.Json.cargado_en) $lo
$g = Llamar GET "/api/LoteCargaAsistente/$idLo"
Verificar "GET/{id}: contadores en 0 y 'cerrado' = false" ($g.Codigo -eq 200 -and $g.Json.filas_leidas -eq 0 -and $g.Json.cerrado -eq $false) $g
$l = Llamar GET "/api/LoteCargaAsistente?limite=1"
Verificar "GET lista: el mas reciente primero" ($l.Codigo -eq 200 -and $l.Json.datos[0].id_lote_carga -eq $idLo) $l
$cl = Llamar POST "/api/LoteCargaAsistente/$idLo/cerrar" @{ filas_leidas = 3; filas_aceptadas = 2; filas_rechazadas = 1; novedades = @(@{ numero_fila = 2; contenido_crudo = "a;b"; motivo = "correo duplicado" }) }
Verificar "POST /cerrar -> 200 (sp_guardar_lote_carga, sin cambios)" ($cl.Codigo -eq 200 -and $cl.Json.lote.cerrado -eq $true) $cl
$cl2 = Llamar POST "/api/LoteCargaAsistente/$idLo/cerrar" @{ filas_leidas = 0; filas_aceptadas = 0; filas_rechazadas = 0 }
Verificar "cerrar otra vez -> 400" ($cl2.Codigo -eq 400) $cl2
$nv = Llamar GET "/api/NovedadCarga?fkLoteCarga=$idLo"
Verificar "GET /api/NovedadCarga?fkLoteCarga= -> 1 novedad" ($nv.Codigo -eq 200 -and $nv.Json.total -eq 1) $nv
$g = Llamar GET "/api/NovedadCarga/$($nv.Json.datos[0].id_novedad)"
Verificar "GET /api/NovedadCarga/{id} -> 200" ($g.Codigo -eq 200 -and $g.Json.motivo -eq "correo duplicado") $g
Verificar "GET /api/NovedadCarga/99999999 -> 404" ((Llamar GET "/api/NovedadCarga/99999999").Codigo -eq 404) $null

# --------------------------------------------------------------------------
Titulo "7. LoteMigracion (nueva: listar, obtener, generar, PATCH)"
$l = Llamar GET "/api/LoteMigracion?fkSesion=5712"
Verificar "GET ?fkSesion=5712 -> los lotes de ejemplo, el mas reciente primero" ($l.Codigo -eq 200 -and $l.Json.total -ge 5 -and $l.Json.datos[0].id_lote_migracion -gt $l.Json.datos[1].id_lote_migracion) $l
$g = Llamar GET "/api/LoteMigracion/1"
Verificar "GET /api/LoteMigracion/1 -> CORRECTO" ($g.Codigo -eq 200 -and $g.Json.fk_estado_proceso -eq "CORRECTO") $g
Verificar "GET /api/LoteMigracion/99999999 -> 404" ((Llamar GET "/api/LoteMigracion/99999999").Codigo -eq 404) $null
$ge = Llamar POST "/api/LoteMigracion" @{ fk_sesion = 5712 }
$idLm = $ge.Json.id_lote_migracion
Verificar "POST genera el lote (GENERADO, total_filas > 0)" ($ge.Codigo -eq 200 -and $idLm -gt 0 -and $ge.Json.lote.fk_estado_proceso -eq "GENERADO" -and $ge.Json.lote.total_filas -gt 0) $ge
$det = Llamar GET "/api/DetalleMigracion?fkLoteMigracion=$idLm"
Verificar "...con una fila de detalle por cada fila migrable" ($det.Codigo -eq 200 -and $det.Json.total -eq $ge.Json.lote.total_filas) $det
$ses = Llamar GET "/api/Sesion/5712"
Verificar "...y la sesion queda MIGRADA (RF184)" ($ses.Codigo -eq 200 -and $ses.Json.estado -eq "MIGRADA") $ses
$sin = Llamar POST "/api/LoteMigracion" @{ fk_sesion = 1 }
Verificar "POST de una sesion sin registros migrables -> 500 (RF181)" ($sin.Codigo -eq 500) $sin
$v = Llamar POST "/api/LoteMigracion" @{ fk_usuario = 1 }
Verificar "POST sin fk_sesion -> 422" ($v.Codigo -eq 422) $v
$pa = Llamar PATCH "/api/LoteMigracion/$idLm" @{ fk_estado_proceso = "EN_CURSO" }
$g = Llamar GET "/api/LoteMigracion/$idLm"
Verificar "PATCH estado EN_CURSO -> 200 y conserva total_filas" ($pa.Codigo -eq 200 -and $g.Json.fk_estado_proceso -eq "EN_CURSO" -and $g.Json.total_filas -eq $ge.Json.lote.total_filas) $g
$pa = Llamar PATCH "/api/LoteMigracion/$idLm" @{ observaciones = "enviado a PSUNX01" }
$g = Llamar GET "/api/LoteMigracion/$idLm"
Verificar "PATCH solo observaciones conserva el estado" ($pa.Codigo -eq 200 -and $g.Json.fk_estado_proceso -eq "EN_CURSO" -and $g.Json.observaciones -eq "enviado a PSUNX01") $g
$pe = Llamar PATCH "/api/LoteMigracion/$idLm" @{ fk_estado_proceso = "NO_EXISTE" }
Verificar "PATCH con estado inexistente -> 500 (FK)" ($pe.Codigo -eq 500) $pe
$pv = Llamar PATCH "/api/LoteMigracion/$idLm" @{ }
Verificar "PATCH vacio -> 400" ($pv.Codigo -eq 400) $pv
$pn = Llamar PATCH "/api/LoteMigracion/99999999" @{ fk_estado_proceso = "EN_COLA" }
Verificar "PATCH a un lote inexistente -> 404" ($pn.Codigo -eq 404) $pn
Verificar "PUT /api/LoteMigracion/{id} no existe (404/405, RF183)" (NoExiste (Llamar PUT "/api/LoteMigracion/$idLm" @{ fk_estado_proceso = "EN_COLA" })) $null
Verificar "DELETE /api/LoteMigracion/{id} no existe (404/405, RF185)" (NoExiste (Llamar DELETE "/api/LoteMigracion/$idLm")) $null

# --------------------------------------------------------------------------
Titulo "8. DetalleMigracion (nueva: listar, obtener, PATCH resultado)"
$l2 = Llamar GET "/api/DetalleMigracion?fkLoteMigracion=2"
Verificar "GET ?fkLoteMigracion=2 -> las 164 filas del lote de ejemplo" ($l2.Codigo -eq 200 -and $l2.Json.total -eq 164) $l2
$l1 = Llamar GET "/api/DetalleMigracion?fkLoteMigracion=1"
$aceptados = @($l1.Json.datos | Where-Object { $_.resultado -eq "ACEPTADO" } | ForEach-Object { $_.fk_registro })
$fila = $l2.Json.datos | Where-Object { $aceptados -contains $_.fk_registro } | Select-Object -First 1
$pa = Llamar PATCH "/api/DetalleMigracion/$($fila.id_detalle)" @{ resultado = "ACEPTADO" }
Verificar "ACEPTADO de un registro ya ACEPTADO en otro lote -> 500 (ux_detmig_aceptado, RF188)" ($pa.Codigo -eq 500) $pa
$pa = Llamar PATCH "/api/DetalleMigracion/$($fila.id_detalle)" @{ resultado = "RECHAZADO"; mensaje = "duplicado en ASIS" }
$g = Llamar GET "/api/DetalleMigracion/$($fila.id_detalle)"
Verificar "PATCH RECHAZADO + mensaje -> 200 y no toca id_asis_enviado" ($pa.Codigo -eq 200 -and $g.Json.resultado -eq "RECHAZADO" -and $g.Json.mensaje -eq "duplicado en ASIS" -and $g.Json.id_asis_enviado -eq $fila.id_asis_enviado) $g
$pa = Llamar PATCH "/api/DetalleMigracion/$($fila.id_detalle)" @{ mensaje = "corregido" }
$g = Llamar GET "/api/DetalleMigracion/$($fila.id_detalle)"
Verificar "PATCH solo mensaje conserva el resultado" ($pa.Codigo -eq 200 -and $g.Json.resultado -eq "RECHAZADO" -and $g.Json.mensaje -eq "corregido") $g
$pr = Llamar PATCH "/api/DetalleMigracion/$($fila.id_detalle)" @{ resultado = "QUIZAS" }
Verificar "resultado fuera de la lista -> 422" ($pr.Codigo -eq 422) $pr
$pv = Llamar PATCH "/api/DetalleMigracion/$($fila.id_detalle)" @{ }
Verificar "PATCH vacio -> 400" ($pv.Codigo -eq 400) $pv
Verificar "GET /api/DetalleMigracion/99999999 -> 404" ((Llamar GET "/api/DetalleMigracion/99999999").Codigo -eq 404) $null
Verificar "POST /api/DetalleMigracion no existe (404/405, RF189)" (NoExiste (Llamar POST "/api/DetalleMigracion" @{ fk_lote_migracion = 1 })) $null
Verificar "DELETE /api/DetalleMigracion/{id} no existe (404/405, RF189)" (NoExiste (Llamar DELETE "/api/DetalleMigracion/$($fila.id_detalle)")) $null

# --------------------------------------------------------------------------
Write-Host "`n$($script:pasadas) pasadas, $($script:fallidas) fallidas." -ForegroundColor Cyan
Write-Host "Para dejar la BD como nueva:  docker compose down -v ; docker compose up -d --build"
if ($script:fallidas -eq 0) { exit 0 } else { exit 1 }
