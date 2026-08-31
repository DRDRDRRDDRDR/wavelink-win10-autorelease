$log = "C:\Users\DR\WorkBuddy\2026-08-25-21-52-18\wavelink_patched\install_log.txt"
$out = @()
$cer = "C:\Users\DR\WorkBuddy\2026-08-25-21-52-18\wavelink_patched\WaveLinkPatch.cer"
$msix = "C:\Users\DR\WorkBuddy\2026-08-25-21-52-18\wavelink_patched\Elgato.WaveLink_3.2.10.4073_x64_Win10.msix"

# 1) Trust the self-signed cert (LocalMachine -> Trusted Root)
try {
    $imp = Import-Certificate -FilePath $cer -CertStoreLocation "Cert:\LocalMachine\TrustedRoot" -ErrorAction Stop
    $out += "Cert imported to TrustedRoot: $($imp.Thumbprint)"
} catch {
    $out += "Cert import FAILED: $_"
}

# 2) Install the patched MSIX
try {
    Add-AppxPackage -Path $msix -ErrorAction Stop
    $out += "Add-AppxPackage: OK"
} catch {
    $out += "Add-AppxPackage FAILED: $_"
}

Start-Sleep -Seconds 2
$pkg = Get-AppxPackage -Name Elgato.WaveLink -ErrorAction SilentlyContinue
if ($pkg) {
    $out += "INSTALLED: $($pkg.Name) $($pkg.Version)  InstallLocation=$($pkg.InstallLocation)"
} else {
    $out += "Elgato.WaveLink NOT found after install attempt"
}

$out -join [Environment]::NewLine | Out-File -FilePath $log -Encoding utf8
Write-Host "DONE"