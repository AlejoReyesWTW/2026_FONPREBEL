@echo off
chcp 65001 >nul
setlocal

rem Genera el publicado autocontenido para llevarlo a una maquina sin SDK.
rem Deja publish\FonPrebel (carpeta lista para ejecutar) y publish\FonPrebel-win-x64.zip.

cd /d "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 (
    echo.
    echo No se encontro el SDK de .NET ^(el comando "dotnet"^).
    echo.
    echo Este script compila, asi que necesita SDK. La maquina de DESTINO es la
    echo que no lo necesita: alla solo se ejecuta FonPrebel.exe.
    echo.
    pause
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "tools\publicar.ps1" %*
if errorlevel 1 (
    echo.
    echo La publicacion no se pudo completar. Revisa los errores de arriba.
    echo.
    pause
    exit /b 1
)

echo.
echo Listo: copia publish\FonPrebel-win-x64.zip a la otra maquina.
pause
exit /b 0
