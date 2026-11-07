# Detiene la instancia en ejecución, compila y abre la aplicación.
#
# Por qué existe: en Windows, una instancia viva de FonPrebel.exe bloquea
# FonPrebel.dll y la compilación falla con "The process cannot access the file
# because it is being used by another process". Este script cierra la instancia
# antes de compilar, así el error no aparece nunca.
#
# Uso, desde la raíz del repositorio:
#   powershell -ExecutionPolicy Bypass -File tools/run-app.ps1
#   powershell -ExecutionPolicy Bypass -File tools/run-app.ps1 -SkipBuild

param(
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $root 'FonPrebel.sln'
$exe = Join-Path $root 'src/FonPrebel/bin/Debug/net8.0-windows/FonPrebel.exe'

$running = Get-Process -Name 'FonPrebel' -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "Cerrando la instancia en ejecución (PID $($running.Id -join ', '))..."
    $running | Stop-Process -Force
    Start-Sleep -Milliseconds 800
}

if (-not $SkipBuild) {
    Write-Host 'Compilando...'
    & dotnet build $solution -v q
    if ($LASTEXITCODE -ne 0) {
        throw 'La compilación falló. Revisá los errores de arriba.'
    }
}

if (-not (Test-Path $exe)) {
    throw "No existe el ejecutable: $exe. Compilá primero sin -SkipBuild."
}

Start-Process -FilePath $exe | Out-Null
Write-Host 'Aplicación iniciada.'
