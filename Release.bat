@echo off
:: Scrybe - One-click local release
:: Publishes the self-contained executable under Dist.

cd /d "%~dp0"
powershell -ExecutionPolicy Bypass -File "%~dp0Build.ps1" -Mode Release -Output "%~dp0Dist"
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Release FAILED.
    pause
    exit /b 1
)
echo.
echo Release complete.
pause
