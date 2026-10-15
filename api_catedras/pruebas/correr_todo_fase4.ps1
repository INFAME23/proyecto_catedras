# ==========================================================================
#  correr_todo_fase4.ps1  -  Proyecto Catedras - Retrofit criterio 3, Fase 4
# --------------------------------------------------------------------------
#  UN comando para la verificacion completa del lote, desde una BD NUEVA:
#
#    0. docker compose down -v ; docker compose up -d --build
#       (asi corren TODAS las migraciones, 001 a 032, en initdb)
#    1. Inventario: cuantos procedimientos del molde quedaron en la BD
#    2. Pruebas SQL de los procedimientos   (db/postgres/pruebas/prueba_sp_retrofit_fase4.sql)
#    3. Pruebas de capas                    (dotnet run --project pruebas)  <- incluye la regresion v1
#    4. Verificacion v5 contra la API       (pruebas/verificar_v5.ps1)
#    5. Verificacion de la Fase 4 + regresion v1 por HTTP (pruebas/verificar_retrofit_fase4.ps1)
#
#  Uso (desde cualquier carpeta):
#      powershell -ExecutionPolicy Bypass -File .\api_catedras\pruebas\correr_todo_fase4.ps1
#  Sin reset de la BD (mas rapido, pero los datos de corridas previas quedan):
#      ... -File .\api_catedras\pruebas\correr_todo_fase4.ps1 -SinReset
#
#  Sale con 0 si TODO paso; 1 si algo fallo. (sin tildes: PowerShell 5.1)
# ==========================================================================
param(
    [string]$Api = "http://127.0.0.1:8142",
    [switch]$SinReset
)

$ErrorActionPreference = "Continue"
$raiz = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Set-Location $raiz
$resumen = New-Object System.Collections.Generic.List[object]

function Paso([string]$nombre, [int]$codigo) {
    $resumen.Add([pscustomobject]@{ Paso = $nombre; Resultado = $(if ($codigo -eq 0) { "OK" } else { "FALLA ($codigo)" }) })
}
function Titulo([string]$t) { Write-Host "`n############ $t ############" -ForegroundColor Cyan }

# --------------------------------------------------------------------------
if (-not $SinReset) {
    Titulo "0. docker compose down -v ; up -d --build"
    docker compose down -v
    docker compose up -d --build
    Paso "0. docker compose up -d --build" $LASTEXITCODE
}

# dotnet watch compila al arrancar: se espera a que GET / responda (max ~4 min).
Write-Host "Esperando a la API en $Api ..."
$lista = $false
for ($i = 0; $i -lt 48 -and -not $lista; $i++) {
    try { $r = Invoke-WebRequest -Uri "$Api/" -UseBasicParsing -TimeoutSec 5; $lista = ($r.StatusCode -eq 200) } catch { Start-Sleep -Seconds 5 }
}
Paso "   API en linea" $(if ($lista) { 0 } else { 1 })
if (-not $lista) { Write-Host "La API no respondio. Revisa: docker compose logs api" -ForegroundColor Red }

# --------------------------------------------------------------------------
Titulo "1. Inventario de procedimientos en la BD"
docker compose exec -T postgres psql -U sa -d bdcatedras_postgres_local -Atc `
  "SELECT count(*) || ' procedimientos sp_(listar|consultar|crear|actualizar|eliminar)_*  (esperado >= 49 tras la Fase 4)' FROM pg_proc WHERE pronamespace = 'catedras'::regnamespace AND prokind = 'p' AND proname ~ '^sp_(listar|consultar|crear|actualizar|eliminar)_'"

# --------------------------------------------------------------------------
Titulo "2. Pruebas SQL de los procedimientos (BEGIN ... ROLLBACK, no deja rastro)"
# Se copia el archivo al contenedor (en vez de canalizarlo con '|'): PowerShell
# 5.1 cambiaria las tildes del archivo al pasarlo por la tuberia.
docker compose cp db/postgres/pruebas/prueba_sp_retrofit_fase4.sql postgres:/tmp/prueba_sp_retrofit_fase4.sql
docker compose exec -T postgres psql -U sa -d bdcatedras_postgres_local -v ON_ERROR_STOP=1 -f /tmp/prueba_sp_retrofit_fase4.sql
Paso "2. Pruebas SQL (prueba_sp_retrofit_fase4.sql)" $LASTEXITCODE

# --------------------------------------------------------------------------
Titulo "3. Pruebas de capas (dotnet run --project pruebas) - incluye regresion v1"
if (Get-Command dotnet -ErrorAction SilentlyContinue) {
    Push-Location (Join-Path $raiz "api_catedras")
    dotnet run --project pruebas
    $codigo = $LASTEXITCODE
    Pop-Location
}
else {
    # Sin SDK local: se corre dentro del contenedor de la API (trae el SDK).
    docker compose exec -T api dotnet run --project pruebas
    $codigo = $LASTEXITCODE
}
Paso "3. Pruebas de capas (pruebas/Programa.cs)" $codigo

# --------------------------------------------------------------------------
Titulo "4. verificar_v5.ps1"
& powershell -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "verificar_v5.ps1") -Api $Api
Paso "4. verificar_v5.ps1" $LASTEXITCODE

# --------------------------------------------------------------------------
Titulo "5. verificar_retrofit_fase4.ps1 (9 tablas + regresion v1 por HTTP)"
& powershell -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "verificar_retrofit_fase4.ps1") -Api $Api
Paso "5. verificar_retrofit_fase4.ps1" $LASTEXITCODE

# --------------------------------------------------------------------------
Titulo "RESUMEN"
$resumen | Format-Table -AutoSize
$fallos = @($resumen | Where-Object { $_.Resultado -ne "OK" }).Count
if ($fallos -eq 0) { Write-Host "TODO OK" -ForegroundColor Green; exit 0 }
else { Write-Host "$fallos paso(s) con FALLA" -ForegroundColor Red; exit 1 }
