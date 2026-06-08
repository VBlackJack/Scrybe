@echo off
:: Scrybe - Debug build (no publish)

cd /d "%~dp0"
dotnet build Scrybe.slnx -c Debug
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Build FAILED.
    pause
    exit /b 1
) else (
    echo.
    echo Build complete.
)
pause
