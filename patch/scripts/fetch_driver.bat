@echo off
:: Download the official public Wave Link driver MSI from Elgato CDN.
:: Use this instead of committing the binary if you want a binary-free repo.
setlocal EnableExtensions

set "URL=https://edge.elgato.com/egc/windows/ewlw/drivers/WaveLinkDriver_3.0.0.466_x64.msi"
set "OUT=%~dp0..\driver\WaveLinkDriver_3.0.0.466_x64.msi"

if exist "%OUT%" (
    echo [OK] Already present: %OUT%
    goto :eof
)

echo Downloading Wave Link Driver 3.0.0.466 from Elgato CDN ...
powershell -NoProfile -Command "Invoke-WebRequest -Uri '%URL%' -OutFile '%OUT%' -UseBasicParsing"

if exist "%OUT%" (
    echo [OK] Saved to: %OUT%
) else (
    echo [!!] Download failed. Check network / CDN availability.
    exit /b 1
)
