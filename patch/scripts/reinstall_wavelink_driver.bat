@echo off
setlocal EnableExtensions
:: One-click reinstall of Elgato Wave Link Driver 3.0.0.466 on Windows 10
:: This bat runs under cmd.exe natively (no MSYS path mangling).
:: Keep this .bat next to WaveLinkDriver_3.0.0.466_x64.msi.

:: Auto-elevate to Administrator if not already elevated
net session >nul 2>&1 || (
    echo Requesting administrator rights...
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

set "MSI=%~dp0WaveLinkDriver_3.0.0.466_x64.msi"
if not exist "%MSI%" (
    echo [ERR] Driver package not found: %MSI%
    pause
    exit /b 1
)

echo Installing Elgato Wave Link Driver 3.0.0.466 ...
msiexec.exe /i "%MSI%" /qn /norestart /l*v "%~dp0msi_reinstall.log"

echo msiexec exit code: %ERRORLEVEL%
if "%ERRORLEVEL%"=="0" (
    echo [OK] Installed successfully. No reboot required.
) else (
    echo [!!] Install returned %ERRORLEVEL%. See %~dp0msi_reinstall.log
)
pause
