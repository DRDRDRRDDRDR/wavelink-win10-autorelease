<#
.SYNOPSIS
    Patch an Elgato Wave Link MSIX to bypass the Windows 11 (MinVersion 22000) gate and repack it unsigned.
.DESCRIPTION
    1. Unpack the MSIX (makeappx if present, else .NET zip fallback).
    2. Edit AppxManifest.xml: lower MinVersion 10.0.22000.x -> 10.0.<MinBuild>.0,
       and rewrite the main <Identity> Publisher to CN=WaveLinkPatch.
    3. Remove the stale AppxSignature.p7x so the package is unsigned.
    4. Repack to <OutputMsix> (ready for Developer-Mode unsigned install).
.PARAMETER InputMsix
    Path to the official Wave Link MSIX you obtained (e.g. input/Elgato.WaveLink_*.msix).
.PARAMETER OutputMsix
    Output patched MSIX path. Default: <input-dir>\WaveLink_Win10_patched.msix
.PARAMETER MinBuild
    Target Windows 10 build floor (default 19041 = 2004). Also the floor for -AllowUnsigned.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$InputMsix,
    [string]$OutputMsix,
    [int]$MinBuild = 19041
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $InputMsix)) { throw "Input MSIX not found: $InputMsix" }

if (-not $OutputMsix) {
    $dir = Split-Path $InputMsix
    $OutputMsix = Join-Path $dir "WaveLink_Win10_patched.msix"
}

# makeappx is REQUIRED. An MSIX repacked with a plain zip library is not a valid
# AppX/OPC container: signtool then fails with "This file format cannot be signed
# because it is not recognized" and the package can never be installed. The old
# silent .NET-zip fallback was removed for exactly that reason.
function Find-MakeAppx {
    $c = Get-Command makeappx.exe -ErrorAction SilentlyContinue
    if ($c) { return $c.Source }

    # Auto-downloaded Windows SDK Build Tools cache populated by the installer.
    $cache = Join-Path $env:LOCALAPPDATA "WaveLinkWin10Setup"
    if (Test-Path $cache) {
        $f = Get-ChildItem $cache -Recurse -Filter makeappx.exe -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($f) { return $f.FullName }
    }

    $roots = @("C:\Program Files (x86)\Windows Kits\10\bin", "C:\Program Files\Windows Kits\10\bin")
    foreach ($r in $roots) {
        if (Test-Path $r) {
            $f = Get-ChildItem $r -Recurse -Filter makeappx.exe -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($f) { return $f.FullName }
        }
    }
    return $null
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$makeappx = Find-MakeAppx
if (-not $makeappx) {
    throw ("makeappx.exe not found. The MSIX packaging tool from the Windows SDK is required " +
        "to produce a valid package. Install the 'MSIX Packaging Tools' component of the " +
        "Windows SDK, then re-run. Without it the patched package would be a plain zip " +
        "archive that signtool cannot sign.")
}

$unpackDir = Join-Path $env:TEMP ("wl_unpack_" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $unpackDir | Out-Null

Write-Host "Unpacking via makeappx ..."
& $makeappx unpack /p "$InputMsix" /d "$unpackDir" /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw "makeappx unpack failed with exit code $LASTEXITCODE" }

$manifest = Join-Path $unpackDir "AppxManifest.xml"
if (-not (Test-Path $manifest)) { throw "AppxManifest.xml not found after unpack." }

$xml = [System.IO.File]::ReadAllText($manifest, [System.Text.Encoding]::UTF8)

$n1 = ([regex]::Matches($xml, 'MinVersion="10\.0\.22000')).Count
$xml = $xml -replace 'MinVersion="10\.0\.22000\.0"', "MinVersion=`"10.0.$MinBuild.0`""
$xml = $xml -replace 'MinVersion="10\.0\.22000"',  "MinVersion=`"10.0.$MinBuild.0`""
Write-Host "MinVersion(22000) occurrences replaced: $n1"

# Rewrite only the main <Identity> Publisher (matches the original patch_manifest.py behaviour)
$k = 0
$xml = [regex]::Replace($xml, '(?s)(<Identity[^>]*?Publisher=")[^"]*(")', {
        param($m)
        $script:k++
        return $m.Groups[1].Value + "CN=WaveLinkPatch" + $m.Groups[2].Value
    })
Write-Host "Identity Publisher replaced: $k"
if ($k -ne 1) { Write-Warning "Expected exactly 1 <Identity> Publisher; found $k (continuing anyway)." }

[System.IO.File]::WriteAllText($manifest, $xml, [System.Text.Encoding]::UTF8)

# Drop the stale signature so the repacked package is unsigned (Developer-Mode install needs this)
$sig = Join-Path $unpackDir "AppxSignature.p7x"
if (Test-Path $sig) { Remove-Item $sig -Force; Write-Host "Removed stale AppxSignature.p7x" }

# Repack. makeappx pack produces a real AppX/OPC container (with AppxBlockMap.xml and
# [Content_Types].xml) which is what signtool and Add-AppxPackage require.
if (Test-Path $OutputMsix) { Remove-Item $OutputMsix -Force }
Write-Host "Repacking to $OutputMsix ..."
& $makeappx pack /d "$unpackDir" /p "$OutputMsix" /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw "makeappx pack failed with exit code $LASTEXITCODE" }
if (-not (Test-Path $OutputMsix)) { throw "makeappx pack produced no output: $OutputMsix" }
Write-Host "Patched MSIX -> $OutputMsix"
