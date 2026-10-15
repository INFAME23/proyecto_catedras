# ==========================================================================
#  correr_todo_v7.ps1  -  Proyecto Catedras - Version 7 (cierre, Fase 61)
# --------------------------------------------------------------------------
#  UN comando para los criterios de aceptacion de la v7 contra Docker real:
#
#    1. correr_todo_fase4.ps1   (BD nueva + SQL de procedimientos + prueba de
#                                capas de 37 entidades + verificar_v5 +
#                                verificar_retrofit_fase4 con regresion v1)
#    2. BD nueva otra vez y verificar_v7.ps1 (criterios 1-6 de la v7; necesita
#       los datos sembrados intactos: sesion 5712, 168 filas de bitacora...)
#    3. BD nueva al final, con la clave del administrador fijada
#       (admin.catedras / Admin12345) para probar el front a mano (criterio 8).
#
#  Uso (desde cualquier carpeta):
#      powershell -ExecutionPolicy Bypass -File .\api_catedras\pruebas\correr_todo_v7.ps1
#
#  Sale con 0 si TODO paso; 1 si algo fallo. (sin tildes: PowerShell 5.1)
# ==========================================================================
param([string]$Api = "http://127.0.0.1:8142")

$ErrorActionPreference = "Continue"
$raiz = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Set-Location $raiz
$resumen = New-Object System.Collections.Generic.List[object]

function Paso([string]$nombre, [int]$codigo) {
    $resumen.Add([pscustomobject]@{ Paso = $nombre; Resultado = $(if ($codigo -eq 0) { "OK" } else { "FALLA ($codigo)" }) })
}
function Titulo([string]$t) { Write-Host "`n############ $t ############" -ForegroundColor Cyan }

# BD nueva (down -v borra el volumen; initdb vuelve a correr script + migraciones).
function BdNueva {
    docker compose down -v
    docker compose up -d --build
    Write-Host "Esperando a la API en $Api ..."
    for ($i = 0; $i -lt 48; $i++) {
        try { $r = Invoke-WebRequest -Uri "$Api/" -UseBasicParsing -TimeoutSec 5; if ($r.StatusCode -eq 200) { return $true } } catch { Start-Sleep -Seconds 5 }
    }
    Write-Host "La API no respondio. Revisa: docker compose logs api" -ForegroundColor Red
    return $false
}

# --------------------------------------------------------------------------
Titulo "1. correr_todo_fase4.ps1 (SQL + capas + v5 + retrofit/regresion v1)"
& powershell -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "correr_todo_fase4.ps1") -Api $Api
Paso "1. correr_todo_fase4.ps1" $LASTEXITCODE

# --------------------------------------------------------------------------
Titulo "2. BD nueva + verificar_v7.ps1 (criterios 1-6)"
$lista = BdNueva
Paso "   BD nueva y API en linea" $(if ($lista) { 0 } else { 1 })
& powershell -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "verificar_v7.ps1") -Api $Api
Paso "2. verificar_v7.ps1" $LASTEXITCODE

# --------------------------------------------------------------------------
Titulo "3. BD nueva para probar el front a mano (criterio 8)"
$lista = BdNueva
try {
    Invoke-RestMethod -Method Patch -Uri "$Api/api/Usuario/1" -ContentType "application/json" -Body '{"clave":"Admin12345"}' | Out-Null
    Paso "3. BD nueva + clave del administrador" 0
}
catch { Paso "3. BD nueva + clave del administrador" 1 }

# --------------------------------------------------------------------------
Titulo "RESUMEN"
$resumen | Format-Table -AutoSize
$fallos = @($resumen | Where-Object { $_.Resultado -ne "OK" }).Count
if ($fallos -eq 0) {
    Write-Host "TODO OK. Siguiente: criterio 8 a mano en el front (version7/7_quickstart.md, paragrafo 3)." -ForegroundColor Green
    exit 0
}
else { Write-Host "$fallos paso(s) con FALLA" -ForegroundColor Red; exit 1 }
