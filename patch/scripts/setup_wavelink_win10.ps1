<#
.SYNOPSIS
    End-to-end installer: bypass the MSIX Win11 gate AND force-install the Wave Link 3.x driver on Windows 10.
.DESCRIPTION
    Runs the full flow documented in README:
      0. (optional) Patch + repack the official Wave Link MSIX so it installs on Windows 10 (Developer Mode, unsigned).
      1. Enable Windows Developer Mode (required for unsigned AppX install via -AllowUnsigned).
      2. Install the patched MSIX.
      3. Force-install the official driver MSI.
      4. Verify services + app presence.
    Requires Administrator. Will auto-elevate if not already running as admin.
.PARAMETER MsixPath
    Path to the official Wave Link MSIX you obtained (e.g. input/Elgato.WaveLink_*.msix).
    If omitted, the first *.msix in input/ is used.
.PARAMETER SkipApp
    Skip the MSIX patch/install (app already installed).
.PARAMETER SkipDriver
    Skip the driver MSI install (driver already installed).
.PARAMETER MinBuild
    Target Windows 10 build floor for MinVersion (default 19041 = 2004). Also the -AllowUnsigned floor.
#>
[CmdletBinding()]
param(
    [string]$MsixPath,
    [switch]$SkipApp,
    [switch]$SkipDriver,
    [int]$MinBuild = 19041
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$repoRoot = (Resolve-Path (Join-Path $scriptDir "..")).Path
$logFile  = Join-Path $repoRoot "setup_wavelink_win10.log"

# ---- auto-elevate ----
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    $argList = "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`""
    if ($MsixPath)  { $argList += " -MsixPath `"$MsixPath`"" }
    if ($SkipApp)   { $argList += " -SkipApp" }
    if ($SkipDriver) { $argList += " -SkipDriver" }
    if ($MinBuild -ne 19041) { $argList += " -MinBuild $MinBuild" }
    Start-Process powershell.exe -Verb RunAs -ArgumentList $argList
    exit
}

Start-Transcript -Path $logFile -Force | Out-Null
function Log($m) { Write-Host $m }

try {
    Log "=== Wave Link 3.x on Windows 10 - automated setup ==="
    Log "Repo root: $repoRoot"

    # Step 0: OS check
    $os = Get-CimInstance Win32_OperatingSystem
    $build = [int]$os.BuildNumber
    Log "OS: $($os.Caption) (build $build)"
    if ($build -lt 17763) { throw "Windows 10 1809 (build 17763) or newer is required. Detected build $build." }

    # Step 1: locate MSIX
    if (-not $SkipApp) {
        if (-not $MsixPath) {
            $inputDir = Join-Path $repoRoot "input"
            $cand = Get-ChildItem $inputDir -Filter *.msix -ErrorAction SilentlyContinue
            if ($cand.Count -eq 0) {
                throw "No MSIX found in '$inputDir'. Place the official Wave Link MSIX there (see input/README.txt)."
            }
            $MsixPath = $cand[0].FullName
        }
        if (-not (Test-Path $MsixPath)) { throw "MSIX not found: $MsixPath" }
        Log "Using MSIX: $MsixPath"

        # Step 2: enable Developer Mode
        Log "Enabling Windows Developer Mode ..."
        $key = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock"
        if (-not (Test-Path $key)) { New-Item -Path $key -Force | Out-Null }
        Set-ItemProperty -Path $key -Name AllowDevelopmentWithoutDevLicense -Value 1 -Type DWord -Force
        Log "Developer Mode enabled."

        # Step 3: patch + repack
        $patchedMsix = Join-Path $repoRoot "input\WaveLink_Win10_patched.msix"
        Log "Patching MSIX (lowering MinVersion to 10.0.$MinBuild.0) ..."
        & "$scriptDir\patch_manifest.ps1" -InputMsix $MsixPath -OutputMsix $patchedMsix -MinBuild $MinBuild
        if (-not (Test-Path $patchedMsix)) { throw "Patch produced no output: $patchedMsix" }

        # Step 4: install unsigned
        Log "Installing patched MSIX (unsigned, Developer Mode) ..."
        try {
            Add-AppxPackage -Path $patchedMsix -AllowUnsigned -ForceApplicationShutdown
        }
        catch {
            Log "Add-AppxPackage -AllowUnsigned failed: $_"
            Log "On Windows 10 1809/1909, -AllowUnsigned is unavailable; you must sign the patched MSIX with a trusted certificate (.pfx) and use install.ps1 instead."
            throw
        }
        $pkg = Get-AppxPackage -Name "Elgato.WaveLink" -ErrorAction SilentlyContinue
        if (-not $pkg) { throw "App install failed: Elgato.WaveLink not found after Add-AppxPackage." }
        Log "App installed: $($pkg.Name) $($pkg.Version)"
    }
    else {
        Log "(SkipApp) Assuming Wave Link 3.x app already installed."
    }

    # Step 5: install driver
    if (-not $SkipDriver) {
        $msi = Join-Path $repoRoot "driver\WaveLinkDriver_3.0.0.466_x64.msi"
        if (-not (Test-Path $msi)) {
            Log "Driver MSI missing; re-downloading via fetch_driver.bat ..."
            & "$scriptDir\fetch_driver.bat" | Out-Null
        }
        if (-not (Test-Path $msi)) { throw "Driver MSI still missing: $msi" }
        Log "Installing driver MSI (native host, /qn) ..."
        $drvLog = Join-Path $repoRoot "driver\msi_install_automated.log"
        $proc = Start-Process msiexec.exe -ArgumentList "/i", "`"$msi`"", "/qn", "/norestart", "/l*v", "`"$drvLog`"" -Wait -PassThru
        if ($proc.ExitCode -ne 0) { throw "Driver MSI failed with exit code $($proc.ExitCode). See $drvLog" }
        Log "Driver MSI installed (exit 0)."
    }
    else {
        Log "(SkipDriver) Assuming driver already installed."
    }

    # Step 6: verify
    Log "Verifying services ..."
    $ok = $true
    foreach ($svc in @("ElgatoVirtUsbAudioEmu", "ElgatoUsbAudio", "ElgatoUsbAudioks")) {
        $s = Get-Service $svc -ErrorAction SilentlyContinue
        $status = if ($s) { $s.Status } else { "MISSING" }
        Log "  $svc : $status"
        if (-not $s -or $s.Status -ne "Running") { $ok = $false }
    }
    $appx = Get-AppxPackage -Name "Elgato.WaveLink" -ErrorAction SilentlyContinue
    Log "  Appx Elgato.WaveLink : $(if ($appx) { $appx.Version } else { 'MISSING' })"

    Log ""
    if ($ok) { Log "=== DONE. Wave Link 3.x driver is installed and running on Windows 10. ===" }
    else     { Log "=== DONE WITH WARNINGS. Check services above. See $logFile ===" }
}
catch {
    Log "ERROR: $_"
    Log "See transcript: $logFile"
    exit 1
}
finally {
    Stop-Transcript | Out-Null
}
