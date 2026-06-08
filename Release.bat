@echo off
:: Scrybe - One-click local release
:: Publishes the self-contained executable under Dist.
:: No GitHub push/publish: Scrybe releases are local-only.

cd /d "%~dp0"
powershell -ExecutionPolicy Bypass -File "%~dp0Build.ps1" -Configuration Release -Output "%~dp0Dist"
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Release FAILED.
    pause
    exit /b 1
)
echo.
echo Release complete.
pause
