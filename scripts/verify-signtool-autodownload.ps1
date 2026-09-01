# =============================================================
# verify-signtool-autodownload.ps1
# -------------------------------------------------------------
# On-machine verification script: on a Windows 10 PC that does
# NOT have the Windows 10 SDK installed, verify that the
# wavelink-win10-autorelease installer's "auto-download signtool
# -> sign" logic actually works.
#
# Why this is needed:
#   GitHub CI (windows-latest) ships with the Windows 10 SDK, so a
#   CI release run hits the "signtool already present" branch and
#   NEVER exercises the auto-download path. To prove the download
#   path works you must run it on a real PC without the SDK.
#
# How to run (on the target Win10, in PowerShell):
#   powershell.exe -NoProfile -ExecutionPolicy Bypass -File verify-signtool-autodownload.ps1
#
# The script FORCES the "SDK absent" branch (ignores any SDK that
# may be installed locally) so it truly tests the download ->
# extract -> launch -> sign chain. It mirrors DownloadSigntool()
# in Installer.cs exactly.
#
# NOTE: this file is intentionally ASCII-only (no Chinese / no
# non-ASCII chars) so PowerShell on any locale parses it correctly.
# =============================================================

$ErrorActionPreference = 'Stop'

# ---- keep in sync with SdkBuildToolsVersion in Installer.cs ----
$SdkBuildToolsVersion = '10.0.28000.2705'
# x64 tool directory inside the nupkg (signtool + 3 SxS DLLs + manifests)
$NupkgX64Path = 'bin/10.0.28000.0/x64'

# isolated subdir so it never clashes with the real installer cache
$cacheDir = Join-Path $env:LOCALAPPDATA 'WaveLinkWin10Setup\signtool-verify'
$signtool = Join-Path $cacheDir 'signtool.exe'

# 4 fatal-required files: signtool.exe + 3 SxS signing DLLs.
# Copying signtool.exe alone fails with a side-by-side activation
# error, so the whole x64 directory must be extracted.
$CriticalFiles = @(
    'signtool.exe',
    'Microsoft.Windows.Build.Signing.mssign32.dll',
    'Microsoft.Windows.Build.Signing.wintrust.dll',
    'Microsoft.Windows.Build.Appx.AppxSip.dll'
)
# companion manifests (extracted together with the directory; missing
# ones break SxS activation)
$ManifestFiles = @(
    'signtool.exe.manifest',
    'Microsoft.Windows.Build.Signing.mssign32.dll.manifest',
    'Microsoft.Windows.Build.Signing.wintrust.dll.manifest',
    'Microsoft.Windows.Build.Appx.AppxSip.dll.manifest'
)

function Write-Step($n, $msg) {
    Write-Host ("[{0}/6] {1}" -f $n, $msg) -ForegroundColor Cyan
}

# -------------------------------------------------------------
Write-Step 1 'Prepare isolated cache directory'
if (Test-Path $cacheDir) { Remove-Item $cacheDir -Recurse -Force }
New-Item -ItemType Directory -Path $cacheDir -Force | Out-Null
Write-Host "      cache dir: $cacheDir" -ForegroundColor DarkGray

# -------------------------------------------------------------
Write-Step 2 'Download Windows SDK BuildTools nupkg'
$nupkgUrl  = ("https://api.nuget.org/v3-flatcontainer/microsoft.windows.sdk.buildtools/" +
              "$SdkBuildToolsVersion/microsoft.windows.sdk.buildtools.$SdkBuildToolsVersion.nupkg")
$nupkgPath = Join-Path $env:TEMP ("wlsdk_$SdkBuildToolsVersion.nupkg")
if (Test-Path $nupkgPath) { Remove-Item $nupkgPath -Force }

$downloaded = $false
if (Get-Command curl.exe -ErrorAction SilentlyContinue) {
    & curl.exe -L --retry 3 -o $nupkgPath $nupkgUrl
    if ($LASTEXITCODE -eq 0 -and (Test-Path $nupkgPath) -and ((Get-Item $nupkgPath).Length -gt 1MB)) {
        $downloaded = $true
    }
}
if (-not $downloaded) {
    Invoke-WebRequest -Uri $nupkgUrl -OutFile $nupkgPath -UseBasicParsing
}
$nupkgSize = (Get-Item $nupkgPath).Length
Write-Host ("      downloaded: {0} ({1} MB)" -f $nupkgPath, [math]::Round($nupkgSize/1MB, 1)) -ForegroundColor Green

# -------------------------------------------------------------
Write-Step 3 'Extract x64 tool directory from nupkg (same as C# ZipFile)'
# a nupkg is just a zip; use System.IO.Compression.ZipFile to pull
# only the x64 directory tree, exactly like the installer does.
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip     = [System.IO.Compression.ZipFile]::OpenRead($nupkgPath)
$prefix  = "$NupkgX64Path/"
$entries = $zip.Entries | Where-Object { $_.FullName -like "$prefix*" }
if ($entries.Count -eq 0) {
    $zip.Dispose()
    throw "nupkg does not contain $prefix; version path may have changed, check SdkBuildToolsVersion"
}
foreach ($e in $entries) {
    $rel = $e.FullName.Substring($prefix.Length)
    if ([string]::IsNullOrEmpty($rel)) { continue }   # the directory itself
    $dest    = Join-Path $cacheDir $rel
    $destDir = Split-Path $dest -Parent
    if (-not (Test-Path $destDir)) { New-Item -ItemType Directory -Path $destDir -Force | Out-Null }
    if ($e.Name -ne '') { [System.IO.Compression.ZipFile]::ExtractToFile($e, $dest, $true) }
}
$zip.Dispose()

# -------------------------------------------------------------
Write-Step 4 'Verify critical files'
$allCritOk = $true
foreach ($f in $CriticalFiles) {
    $p = Join-Path $cacheDir $f
    if (Test-Path $p) { Write-Host "      [OK]      $f" -ForegroundColor Green }
    else { Write-Host "      [MISSING] $f" -ForegroundColor Red; $allCritOk = $false }
}
foreach ($f in $ManifestFiles) {
    $p = Join-Path $cacheDir $f
    if (Test-Path $p) { Write-Host "      [manifest] $f" -ForegroundColor DarkGray }
    else { Write-Host "      [NO MANIFEST] $f  (SxS may fail to activate)" -ForegroundColor Yellow }
}
if (-not $allCritOk) { throw 'Missing fatal file(s); auto-download logic cannot work' }

# -------------------------------------------------------------
Write-Step 5 'Run signtool.exe to confirm it launches (no side-by-side error)'
& $signtool 2>&1 | Select-Object -First 2 | ForEach-Object { Write-Host "      $_" -ForegroundColor DarkGray }
if ($LASTEXITCODE -ne 0) {
    Write-Host "      signtool failed to launch (exit=$LASTEXITCODE); a dependency may still be missing" -ForegroundColor Red
    throw 'signtool launch verification failed'
}
Write-Host '      signtool launched successfully' -ForegroundColor Green

# -------------------------------------------------------------
Write-Step 6 'Optional: self-signed cert + sign a PE file to verify the full signing chain'
# Copy signtool.exe as the "file to sign", then sign it with a
# self-signed cert. This loads the mssign32/wintrust SxS DLLs --
# exactly the chain we need to prove works. (Real MSIX signing also
# loads AppxSip.dll; this step already proves the SxS chain loads.)
$copy    = Join-Path $env:TEMP 'signtool_test.exe'
$certPfx = Join-Path $env:TEMP 'wl_verify_cert.pfx'
Copy-Item $signtool $copy -Force
$cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject 'CN=WaveLinkVerify' `
        -CertStoreLocation 'Cert:\CurrentUser\My' -KeyUsage DigitalSignature
$pw   = ConvertTo-SecureString -String 'TempPass123!' -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath $certPfx -Password $pw | Out-Null
$signOut = & $signtool sign /fd SHA256 /f $certPfx /p TempPass123! $copy 2>&1
$signOut | ForEach-Object { Write-Host "      $_" -ForegroundColor DarkGray }
if ($LASTEXITCODE -eq 0) {
    Write-Host '      sample PE signed successfully; SxS signing chain OK' -ForegroundColor Green
} else {
    Write-Host '      sample signing failed (affects this test step only; investigate further)' -ForegroundColor Yellow
}
# clean up test cert and temp files
Remove-Item "Cert:\CurrentUser\My\$($cert.Thumbprint)" -Force -ErrorAction SilentlyContinue
Remove-Item $certPfx -Force -ErrorAction SilentlyContinue
Remove-Item $copy    -Force -ErrorAction SilentlyContinue

# -------------------------------------------------------------
Write-Host ''
Write-Host '===== Verification result =====' -ForegroundColor Cyan
Write-Host 'signtool auto-download -> extract -> launch: PASSED' -ForegroundColor Green
Write-Host "cache location: $cacheDir" -ForegroundColor White
Write-Host 'In the real installer the next step signs the MSIX via AppxSip (requires a code-signing cert on the machine).' -ForegroundColor White
