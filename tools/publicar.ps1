# Publica la aplicacion autocontenida para win-x64 y la deja lista para copiar
# a una maquina sin SDK de .NET.
#
# Por que existe: la maquina del trabajo tiene el servidor de archivos pero no
# necesariamente el SDK ni Visual Studio. Con un publicado autocontenido alcanza
# con copiar la carpeta y ejecutar FonPrebel.exe.
#
# No usa recorte (PublishTrimmed) ni archivo unico: WPF no admite el recorte y
# conviene mantener las librerias nativas de SQLite a la vista.
#
# Uso, desde la raiz del repositorio:
#   powershell -ExecutionPolicy Bypass -File tools/publicar.ps1
#   powershell -ExecutionPolicy Bypass -File tools/publicar.ps1 -NoZip

param(
    [string]$Configuration = 'Release',
    [switch]$NoZip
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src/FonPrebel/FonPrebel.csproj'
$publishRoot = Join-Path $root 'publish'
$out = Join-Path $publishRoot 'FonPrebel'
$zip = Join-Path $publishRoot 'FonPrebel-win-x64.zip'

# Una instancia viva bloquea FonPrebel.dll y hace fallar la publicacion.
$running = Get-Process -Name 'FonPrebel' -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "Cerrando la instancia en ejecucion (PID $($running.Id -join ', '))..."
    $running | Stop-Process -Force
    Start-Sleep -Milliseconds 800
}

# Se borra el paquete anterior junto con la carpeta: si la publicacion falla, no
# queda un zip viejo con aspecto de actual. Un artefacto que sobrevive a un build
# fallido termina siendo el que se entrega.
if (Test-Path $out) {
    Remove-Item $out -Recurse -Force
}

if (Test-Path $zip) {
    Remove-Item $zip -Force
}

# Los intermedios de Release se borran a proposito: si se reusan, el compilador
# conserva el bloque de depuracion (RSDS) que lleva la ruta absoluta del .pdb en
# esta maquina, y ese bloque viaja dentro de FonPrebel.dll.
foreach ($stale in @('src/FonPrebel/obj/Release', 'src/FonPrebel/bin/Release')) {
    $path = Join-Path $root $stale
    if (Test-Path $path) {
        Remove-Item $path -Recurse -Force
    }
}

Write-Host 'Publicando autocontenido win-x64...'
# DebugType=None evita el bloque de depuracion en el PE, que ademas de los .pdb lleva
# la ruta absoluta de esta maquina de desarrollo.
& dotnet publish $project -c $Configuration -r win-x64 --self-contained true -p:DebugType=None -p:DebugSymbols=false -o $out
if ($LASTEXITCODE -ne 0) {
    throw 'La publicacion fallo. Revisa los errores de arriba.'
}

$exe = Join-Path $out 'FonPrebel.exe'
if (-not (Test-Path $exe)) {
    throw "No se genero el ejecutable esperado: $exe"
}

# Prueba de que el runtime viaja adentro: si falta, no es autocontenido.
$required = @('hostfxr.dll', 'coreclr.dll', 'e_sqlite3.dll')
foreach ($file in $required) {
    if (-not (Test-Path (Join-Path $out $file))) {
        throw "Falta '$file' en la publicacion: no quedo autocontenida."
    }
}

# Los .pdb no se entregan: llevan la ruta absoluta de esta maquina de desarrollo y
# el nombre de usuario, que no tienen por que viajar a la maquina del cliente.
# El costo es que un seguimiento de pila en errors.log muestra metodo sin numero de
# linea; para el analista no cambia nada.
$symbols = Get-ChildItem $out -Filter '*.pdb' -File
if ($symbols) {
    $symbols | Remove-Item -Force
    Write-Host "Simbolos quitados del paquete: $($symbols.Count) archivo(s) .pdb"
}

$size = [math]::Round(((Get-ChildItem $out -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB), 1)
Write-Host "Carpeta publicada: $out ($size MB)"

if ($NoZip) {
    Write-Host 'Sin zip por pedido (-NoZip): se publico solo la carpeta.'
}
else {
    Compress-Archive -Path (Join-Path $out '*') -DestinationPath $zip -CompressionLevel Optimal
    $zipSize = [math]::Round(((Get-Item $zip).Length / 1MB), 1)
    Write-Host "Paquete para copiar: $zip ($zipSize MB)"
}

Write-Host ''
Write-Host 'Listo. En la maquina de destino: descomprimir y ejecutar FonPrebel.exe.'
