# ==========================================================================
#  verificar_v7.ps1  -  Proyecto Catedras - Version 7 (Fase 59)
# --------------------------------------------------------------------------
#  Verificacion contra la API REAL de los criterios de aceptacion 1-6 de
#  docs/spec_kit/Versiones/version7/2_spec.md, con los datos del paragrafo 8
#  de version7/guia_ia_v7.md:
#
#    0.  La API responde, version v7 y 37 recursos en /swagger
#    1.  Generar un lote (sesion 5712 en CERRADA -> lote de 166 -> MIGRADA)
#    2.  Sesion sin nada que migrar -> 500
#    3.  PATCH de estado del lote
#    4.  PATCH de resultado del detalle y ux_detmig_aceptado (registros 167 y 1)
#    5.  Bitacora de solo lectura (+ la fila que deja DELETE /api/Asistente/2)
#    6.  Parametro, CRUD con llave de texto
#    7.  Verbos que NO existen (anota el codigo real; se espera 405)
#
#  Uso (con la API levantada sobre una BD RECIEN CREADA, desde api_catedras):
#      docker compose down -v ; docker compose up -d --build
#      powershell -ExecutionPolicy Bypass -File .\pruebas\verificar_v7.ps1
#  Otro host/puerto:  ... -Api http://127.0.0.1:8142
#
#  Escribe datos: al terminar, vuelve a crear la BD (down -v / up) y fija de
#  nuevo la clave del administrador (PATCH /api/Usuario/1 {"clave":"Admin12345"}).
#  (sin tildes a proposito: PowerShell 5.1 lee mal los .ps1 sin BOM)
# ==========================================================================
param([string]$Api = "http://127.0.0.1:8142")

$ErrorActionPreference = "Stop"
$script:pasadas = 0
$script:fallidas = 0
$script:codigosReales = @()
$sufijo = Get-Random -Minimum 10000 -Maximum 99999

# Llama a la API y devuelve { Codigo, Texto, Json } SIN lanzar excepcion en 4xx/5xx.
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

# Verbo que no debe existir: se anota el codigo real (para 2_spec y 7_quickstart).
function VerboInexistente {
    param([string]$Metodo, [string]$Ruta, $Cuerpo = $null)
    $r = Llamar $Metodo $Ruta $Cuerpo
    $script:codigosReales += [pscustomobject]@{ Peticion = "$Metodo $Ruta"; Codigo = $r.Codigo }
    Verificar "$Metodo $Ruta no existe -> $($r.Codigo) (esperado 405)" ($r.Codigo -eq 405) $r
}

function Titulo([string]$t) { Write-Host "`n== $t ==" -ForegroundColor Cyan }

# --------------------------------------------------------------------------
Titulo "0. La API responde (criterio 1)"
$raiz = Llamar GET "/"
Verificar "GET / responde 200 con version v7 ($($raiz.Json.version))" ($raiz.Codigo -eq 200 -and $raiz.Json.version -eq "v7") $raiz

$swagger = Llamar GET "/swagger/v1/swagger.json"
$recursos = @()
if ($swagger.Json) {
    $recursos = @($swagger.Json.paths.PSObject.Properties.Name |
        ForEach-Object { ($_ -split '/')[2] } | Where-Object { $_ } | Sort-Object -Unique)
}
Verificar "/swagger/v1/swagger.json documenta 37 recursos ($($recursos.Count))" ($recursos.Count -eq 37) $swagger
foreach ($nuevo in "LoteMigracion", "DetalleMigracion", "Bitacora", "Parametro") {
    Verificar "  ...incluido $nuevo" ($recursos -contains $nuevo)
}

# --------------------------------------------------------------------------
Titulo "1. Generar un lote de migracion (criterio 2, RF181/RF184/RF185)"
$r = Llamar PATCH "/api/Sesion/5712" @{ estado = "CERRADA" }
Verificar "PATCH /api/Sesion/5712 estado CERRADA -> 200 (viene MIGRADA en los datos sembrados)" ($r.Codigo -eq 200) $r
$r = Llamar GET "/api/Sesion/5712"
Verificar "GET /api/Sesion/5712 -> CERRADA antes de generar" ($r.Json.estado -eq "CERRADA") $r

$r = Llamar POST "/api/LoteMigracion" @{ fkSesion = 5712 }
Verificar "POST con cuerpo en camelCase (fkSesion) -> 422 (los cuerpos van en snake_case)" ($r.Codigo -eq 422) $r

$gen = Llamar POST "/api/LoteMigracion" @{ fk_sesion = 5712; fk_usuario = $null }
$idLote = $gen.Json.id_lote_migracion
Verificar "POST /api/LoteMigracion {fk_sesion 5712} -> 200 (lote $idLote; 6 en una BD nueva)" ($gen.Codigo -eq 200 -and $idLote -gt 0) $gen
Verificar "  ...respuesta {estado, mensaje, id_lote_migracion, lote}" ($gen.Json.estado -eq "creado" -and $gen.Json.mensaje -and $gen.Json.lote.id_lote_migracion -eq $idLote) $gen
Verificar "  ...total_filas 166 (excluye al externo), estado GENERADO" ($gen.Json.lote.total_filas -eq 166 -and $gen.Json.lote.fk_estado_proceso -eq "GENERADO") $gen
Verificar "  ...archivo ME_CATEDRA_000099001_R1.xlsx" ($gen.Json.lote.nombre_archivo -eq "ME_CATEDRA_000099001_R1.xlsx") $gen

$det = Llamar GET "/api/DetalleMigracion?fkLoteMigracion=$idLote&limite=1000"
$detalles = @($det.Json.datos)
Verificar "GET /api/DetalleMigracion?fkLoteMigracion=$idLote -> 166 filas" ($det.Codigo -eq 200 -and $det.Json.total -eq 166) $det
Verificar "  ...todas PENDIENTE" (@($detalles | Where-Object { $_.resultado -ne "PENDIENTE" }).Count -eq 0)

$r = Llamar GET "/api/Sesion/5712"
Verificar "GET /api/Sesion/5712 -> MIGRADA despues de generar (efecto de sp_generar_lote_migracion)" ($r.Json.estado -eq "MIGRADA") $r

$r = Llamar GET "/api/LoteMigracion?fkSesion=5712&limite=1"
Verificar "GET /api/LoteMigracion?fkSesion=5712&limite=1 -> el mas reciente primero (lote $idLote)" ($r.Codigo -eq 200 -and $r.Json.datos[0].id_lote_migracion -eq $idLote) $r

$r = Llamar POST "/api/LoteMigracion" @{ fk_usuario = 1 }
Verificar "POST sin fk_sesion -> 422" ($r.Codigo -eq 422) $r

# --------------------------------------------------------------------------
Titulo "2. Sesion sin nada que migrar"
$r = Llamar POST "/api/LoteMigracion" @{ fk_sesion = 1 }
Verificar "POST {fk_sesion 1} -> 500 'No hay registros migrables'" ($r.Codigo -eq 500 -and $r.Texto -match "No hay registros migrables") $r

# --------------------------------------------------------------------------
Titulo "3. PATCH de estado del lote (criterio 3, RF182)"
$r = Llamar PATCH "/api/LoteMigracion/$idLote" @{ fk_estado_proceso = "EN_CURSO" }
Verificar "PATCH fk_estado_proceso EN_CURSO -> 200" ($r.Codigo -eq 200) $r
$r = Llamar GET "/api/LoteMigracion/$idLote"
Verificar "  ...GET confirma EN_CURSO y conserva total_filas" ($r.Json.fk_estado_proceso -eq "EN_CURSO" -and $r.Json.total_filas -eq 166) $r
$r = Llamar PATCH "/api/LoteMigracion/$idLote" @{ observaciones = "Enviado a ASIS (prueba v7)" }
$r2 = Llamar GET "/api/LoteMigracion/$idLote"
Verificar "PATCH solo de observaciones -> 200 y el estado sigue EN_CURSO" ($r.Codigo -eq 200 -and $r2.Json.fk_estado_proceso -eq "EN_CURSO" -and $r2.Json.observaciones -eq "Enviado a ASIS (prueba v7)") $r2
$r = Llamar PATCH "/api/LoteMigracion/$idLote" @{ fk_estado_proceso = "NO_EXISTE" }
Verificar "PATCH fk_estado_proceso NO_EXISTE -> 500 (fk_lotemig_estado)" ($r.Codigo -eq 500 -and $r.Texto -match "fk_lotemig_estado") $r
$r = Llamar PATCH "/api/LoteMigracion/$idLote" @{}
Verificar "PATCH {} -> 400" ($r.Codigo -eq 400) $r
$r = Llamar PATCH "/api/LoteMigracion/999999999" @{ observaciones = "x" }
Verificar "PATCH a un lote inexistente -> 404" ($r.Codigo -eq 404) $r
$r = Llamar GET "/api/LoteMigracion/999999999"
Verificar "GET de un lote inexistente -> 404" ($r.Codigo -eq 404) $r

# --------------------------------------------------------------------------
Titulo "4. PATCH de resultado del detalle y ux_detmig_aceptado (criterio 4, RF188)"
$fila167 = $detalles | Where-Object { $_.fk_registro -eq 167 } | Select-Object -First 1
$fila1 = $detalles | Where-Object { $_.fk_registro -eq 1 } | Select-Object -First 1
Verificar "El lote trae las filas de los registros 167 y 1" ($null -ne $fila167 -and $null -ne $fila1)
if ($fila167 -and $fila1) {
    $r = Llamar PATCH "/api/DetalleMigracion/$($fila167.id_detalle)" @{ resultado = "ACEPTADO" }
    Verificar "PATCH ACEPTADO del registro 167 (sin ACEPTADO previo) -> 200" ($r.Codigo -eq 200) $r
    $r = Llamar GET "/api/DetalleMigracion/$($fila167.id_detalle)"
    Verificar "  ...GET confirma ACEPTADO" ($r.Json.resultado -eq "ACEPTADO") $r
    $r = Llamar PATCH "/api/DetalleMigracion/$($fila1.id_detalle)" @{ resultado = "ACEPTADO" }
    Verificar "PATCH ACEPTADO del registro 1 (ya ACEPTADO en el lote 1) -> 500 ux_detmig_aceptado" ($r.Codigo -eq 500 -and $r.Texto -match "ux_detmig_aceptado") $r
    $r = Llamar PATCH "/api/DetalleMigracion/$($fila1.id_detalle)" @{ resultado = "RECHAZADO"; mensaje = "Duplicado en ASIS" }
    Verificar "PATCH RECHAZADO con mensaje del registro 1 -> 200" ($r.Codigo -eq 200) $r
    $r = Llamar PATCH "/api/DetalleMigracion/$($fila1.id_detalle)" @{ resultado = "OTRO" }
    Verificar "PATCH resultado fuera de la lista -> 422" ($r.Codigo -eq 422) $r
    $r = Llamar PATCH "/api/DetalleMigracion/$($fila1.id_detalle)" @{}
    Verificar "PATCH {} -> 400" ($r.Codigo -eq 400) $r
}
$r = Llamar GET "/api/DetalleMigracion/999999999"
Verificar "GET de un detalle inexistente -> 404" ($r.Codigo -eq 404) $r

# --------------------------------------------------------------------------
Titulo "5. Bitacora de solo lectura (criterio 5, RF190-RF191)"
$r = Llamar GET "/api/Bitacora?limite=5"
Verificar "GET /api/Bitacora?limite=5 -> 200 con 5 filas" ($r.Codigo -eq 200 -and $r.Json.total -eq 5) $r
$r = Llamar GET "/api/Bitacora?nombreTabla=registro_asistencia&limite=1000"
Verificar "GET ?nombreTabla=registro_asistencia -> las 168 filas sembradas, todas INSERT ($($r.Json.total))" ($r.Json.total -eq 168 -and @($r.Json.datos | Where-Object { $_.operacion -ne "INSERT" }).Count -eq 0) $r
Verificar "  ...fk_usuario null en todas (por eso no hay filtro por usuario)" (@($r.Json.datos | Where-Object { $null -ne $_.fk_usuario }).Count -eq 0)

$r = Llamar DELETE "/api/Asistente/2"
Verificar "DELETE /api/Asistente/2 (borrado logico) -> 200" ($r.Codigo -eq 200) $r
$r = Llamar GET "/api/Bitacora?limite=1"
$ultima = $r.Json.datos[0]
Verificar "La bitacora crecio sola: fila nueva asistente / UPDATE, llave 2 (trg_bitacora_asistente)" ($ultima.nombre_tabla -eq "asistente" -and $ultima.operacion -eq "UPDATE" -and $ultima.llave -eq "2") $r
Verificar "  ...datos_antes.activo true y datos_despues.activo false" ($ultima.datos_antes.activo -eq $true -and $ultima.datos_despues.activo -eq $false) $r
$r = Llamar GET "/api/Bitacora/$($ultima.id_bitacora)"
Verificar "GET /api/Bitacora/$($ultima.id_bitacora) -> 200" ($r.Codigo -eq 200 -and $r.Json.id_bitacora -eq $ultima.id_bitacora) $r
$r = Llamar GET "/api/Bitacora/999999999"
Verificar "GET de una entrada inexistente -> 404" ($r.Codigo -eq 404) $r
$r = Llamar GET "/api/Bitacora?limite=0"
Verificar "GET ?limite=0 -> 400" ($r.Codigo -eq 400) $r
$r = Llamar GET "/api/Bitacora?nombreTabla=no_existe"
Verificar "GET ?nombreTabla=no_existe -> 204" ($r.Codigo -eq 204) $r

VerboInexistente POST "/api/Bitacora" @{ nombre_tabla = "x"; operacion = "INSERT" }
VerboInexistente PUT "/api/Bitacora/1" @{ nombre_tabla = "x"; operacion = "INSERT" }
VerboInexistente PATCH "/api/Bitacora/1" @{ llave = "x" }
VerboInexistente DELETE "/api/Bitacora/1"
$r = Llamar GET "/api/Bitacora/1"
Verificar "  ...y la entrada 1 sigue intacta" ($r.Codigo -eq 200 -and $r.Json.nombre_tabla -eq "registro_asistencia") $r

# --------------------------------------------------------------------------
Titulo "6. Parametro, CRUD con llave de texto (criterio 6, RF192-RF197)"
$r = Llamar GET "/api/Parametro"
Verificar "GET /api/Parametro -> 200 con las 9 claves sembradas" ($r.Codigo -eq 200 -and $r.Json.total -ge 9 -and @($r.Json.datos.clave) -contains "CLAVE_LONGITUD") $r
$r = Llamar GET "/api/Parametro/CLAVE_LONGITUD"
Verificar "GET /api/Parametro/CLAVE_LONGITUD -> valor 6, ENTERO" ($r.Json.valor -eq "6" -and $r.Json.tipo_dato -eq "ENTERO") $r
$r = Llamar GET "/api/Parametro/NO_EXISTE_$sufijo"
Verificar "GET de una clave inexistente -> 404" ($r.Codigo -eq 404) $r

$clave = "PRUEBA_V7_$sufijo"
$r = Llamar POST "/api/Parametro" @{ clave = $clave; valor = "1"; tipo_dato = "ENTERO"; descripcion = "Prueba v7" }
Verificar "POST $clave -> 200" ($r.Codigo -eq 200) $r
$creado = (Llamar GET "/api/Parametro/$clave").Json
$r = Llamar POST "/api/Parametro" @{ clave = $clave; valor = "1"; tipo_dato = "ENTERO"; descripcion = "Prueba v7" }
Verificar "POST repetido -> 500 (pk_parametro)" ($r.Codigo -eq 500 -and $r.Texto -match "pk_parametro") $r
$r = Llamar POST "/api/Parametro" @{ clave = "prueba_$sufijo"; valor = "1"; descripcion = "x" }
Verificar "POST con clave en minusculas -> 422 (D37-D)" ($r.Codigo -eq 422) $r
$r = Llamar POST "/api/Parametro" @{ clave = "SIN_TIPO_$sufijo"; valor = ""; descripcion = "Sin tipo_dato" }
$sinTipo = Llamar GET "/api/Parametro/SIN_TIPO_$sufijo"
Verificar "POST sin tipo_dato y valor vacio -> 200 y la BD pone TEXTO" ($r.Codigo -eq 200 -and $sinTipo.Json.tipo_dato -eq "TEXTO" -and $sinTipo.Json.valor -eq "") $sinTipo
$r = Llamar POST "/api/Parametro" @{ clave = "TIPO_MALO_$sufijo"; valor = "1"; tipo_dato = "OTRO"; descripcion = "x" }
Verificar "POST con tipo_dato fuera de la lista -> 422" ($r.Codigo -eq 422) $r

Start-Sleep -Milliseconds 1100
$r = Llamar PATCH "/api/Parametro/$clave" @{ valor = "2" }
$tras = (Llamar GET "/api/Parametro/$clave").Json
Verificar "PATCH valor 2 -> 200, conserva descripcion" ($r.Codigo -eq 200 -and $tras.valor -eq "2" -and $tras.descripcion -eq "Prueba v7") $r
Verificar "  ...modificado_en avanza ($($creado.modificado_en) -> $($tras.modificado_en))" ([datetime]$tras.modificado_en -gt [datetime]$creado.modificado_en)
$r = Llamar PATCH "/api/Parametro/$clave" @{}
Verificar "PATCH {} -> 400" ($r.Codigo -eq 400) $r
$r = Llamar PUT "/api/Parametro/$clave" @{ valor = "3"; descripcion = "Sin tipo" }
Verificar "PUT sin tipo_dato -> 422 (reemplazo completo)" ($r.Codigo -eq 422) $r
$r = Llamar PUT "/api/Parametro/$clave" @{ valor = "3"; tipo_dato = "ENTERO"; descripcion = "Reemplazada" }
Verificar "PUT completo -> 200" ($r.Codigo -eq 200) $r
$r = Llamar PATCH "/api/Parametro/NO_EXISTE_$sufijo" @{ valor = "1" }
Verificar "PATCH a una clave inexistente -> 404" ($r.Codigo -eq 404) $r

$r = Llamar DELETE "/api/Parametro/$clave"
Verificar "DELETE $clave -> 200 {filasEliminadas: 1}" ($r.Codigo -eq 200 -and $r.Json.filasEliminadas -eq 1) $r
$r = Llamar DELETE "/api/Parametro/$clave"
Verificar "DELETE otra vez -> 404" ($r.Codigo -eq 404) $r
$null = Llamar DELETE "/api/Parametro/SIN_TIPO_$sufijo"

# --------------------------------------------------------------------------
Titulo "7. Verbos que no existen (RF183, RF189)"
VerboInexistente PUT "/api/LoteMigracion/$idLote" @{ fk_sesion = 5712 }
VerboInexistente DELETE "/api/LoteMigracion/$idLote"
VerboInexistente POST "/api/DetalleMigracion" @{ fk_lote_migracion = $idLote }
VerboInexistente PUT "/api/DetalleMigracion/$($fila1.id_detalle)" @{ resultado = "ACEPTADO" }
VerboInexistente DELETE "/api/DetalleMigracion/$($fila1.id_detalle)"
$r = Llamar GET "/api/LoteMigracion/$idLote"
Verificar "  ...el lote $idLote sigue ahi" ($r.Codigo -eq 200) $r

# --------------------------------------------------------------------------
Titulo "Codigos reales de los verbos inexistentes (para 2_spec.md y 7_quickstart.md)"
foreach ($c in $script:codigosReales) { Write-Host ("  {0,-45} -> {1}" -f $c.Peticion, $c.Codigo) }

Write-Host "`n$($script:pasadas) pasadas, $($script:fallidas) fallidas." -ForegroundColor $(if ($script:fallidas -eq 0) { "Green" } else { "Red" })
Write-Host "Para dejar la BD como nueva: docker compose down -v ; docker compose up -d --build"
if ($script:fallidas -eq 0) { exit 0 } else { exit 1 }
