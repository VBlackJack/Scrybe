@echo off
:: Scrybe - Debug build (no publish)

setlocal
cd /d "%~dp0"
title Scrybe - Build
set EXIT=0

:: Detect double-click launch (Explorer wraps the call in cmd.exe /c "...").
:: When set, all exit points hit a final pause so the user can read output.
set DOUBLE_CLICK=
echo %CMDCMDLINE% | find /i "/c" >nul
if %ERRORLEVEL% EQU 0 (
    echo %CMDCMDLINE% | find /i "%~f0" >nul && set DOUBLE_CLICK=1
)

dotnet build Scrybe.slnx -c Debug
set EXIT=%ERRORLEVEL%
if %EXIT% NEQ 0 (
    echo.
    echo Build FAILED with exit code %EXIT%.
) else (
    echo.
    echo Build complete.
)

if defined DOUBLE_CLICK (
    echo.
    pause
) else if not "%EXIT%"=="0" (
    echo.
    pause
)
exit /b %EXIT%
