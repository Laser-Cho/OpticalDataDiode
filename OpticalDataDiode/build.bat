@echo off
setlocal
cd /d "%~dp0"

echo ========================================================
echo  OpticalDataDiode Build Script
echo ========================================================
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Build failed with exit code %ERRORLEVEL%
    pause
    exit /b %ERRORLEVEL%
)

echo.
pause
