@echo off
:: Scrybe - Run all tests

cd /d "%~dp0"
dotnet test Scrybe.slnx --verbosity normal
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Tests FAILED.
    pause
    exit /b 1
) else (
    echo.
    echo All tests passed.
)
pause
