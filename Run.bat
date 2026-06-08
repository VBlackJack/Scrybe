@echo off
:: Scrybe - Quick launch (Debug)
::
:: Just run this to launch Scrybe from source.
:: This intentionally does not fetch or auto-pull: Scrybe is a local-only repo.
::
:: Usage:
::   Run.bat             launch the app
::   Run.bat --help      forward args to the .NET app

setlocal
cd /d "%~dp0"
title Scrybe - Debug
set EXIT=0

:: Detect double-click launch (Explorer wraps the call in cmd.exe /c "...").
:: When set, all exit points hit a final pause so the user can read output.
set DOUBLE_CLICK=
echo %CMDCMDLINE% | find /i "/c" >nul && set DOUBLE_CLICK=1

echo ----------------------------------------------------------------
echo  Scrybe - Quick launch (Debug)
echo ----------------------------------------------------------------
echo.

dotnet run --nologo --project src\Scrybe.App\Scrybe.App.csproj -- %*
set EXIT=%ERRORLEVEL%
if %EXIT% NEQ 0 (
    echo.
    echo Run FAILED with exit code %EXIT%.
)

if defined DOUBLE_CLICK (
    echo.
    pause
) else if not "%EXIT%"=="0" (
    pause
)
exit /b %EXIT%
