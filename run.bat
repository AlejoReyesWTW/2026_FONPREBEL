@echo off
chcp 65001 >nul
setlocal

rem Ejecuta la aplicacion desde la raiz del repositorio, en un solo paso:
rem cierra una instancia viva (si la hay), compila y abre la app.
rem
rem Requiere el SDK de .NET 8 instalado: es el mismo requisito que abrir la
rem solucion en Visual Studio. En una maquina sin SDK usar el publicado
rem autocontenido.
rem
rem Uso:
rem   run.bat                 compila y abre la aplicacion
rem   run.bat -SkipBuild      abre sin compilar (si ya compilaste)

cd /d "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 (
    echo.
    echo No se encontro el SDK de .NET ^(el comando "dotnet"^).
    echo.
    echo Opciones:
    echo   1. Instalar el SDK de .NET 8 en esta maquina.
    echo   2. Usar el publicado autocontenido, que no necesita SDK.
    echo.
    pause
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "tools\run-app.ps1" %*
if errorlevel 1 (
    echo.
    echo La aplicacion no se pudo iniciar. Revisa los errores de arriba.
    echo.
    pause
    exit /b 1
)

echo.
echo Listo: la aplicacion esta abierta.
rem Se usa la ruta absoluta porque el "timeout" de Git for Windows tambien se
rem llama timeout.exe y gana en el PATH cuando se ejecuta desde esa consola.
"%SystemRoot%\System32\timeout.exe" /t 3 /nobreak >nul 2>nul
exit /b 0
