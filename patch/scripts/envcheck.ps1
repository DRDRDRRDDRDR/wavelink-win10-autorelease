$log = "C:\Users\DR\WorkBuddy\2026-08-25-21-52-18\wavelink_patched\envcheck.txt"
$os = Get-CimInstance Win32_OperatingSystem
$out = @()
$out += "Caption: $($os.Caption)"
$out += "Version: $($os.Version)"
$out += "Build:   $($os.BuildNumber)"
$out += "Arch:    $($os.OSArchitecture)"
$wc = Get-AppxPackage -Name Microsoft.WindowsAppRuntime.2 -ErrorAction SilentlyContinue
$out += "WindowsAppRuntime.2 installed: $(if($wc){$wc.Version}else{'NO'})"
$vc = Get-AppxPackage -Name Microsoft.VCLibs.140.00.UWPDesktop -ErrorAction SilentlyContinue
$out += "VCLibs.140.00.UWPDesktop installed: $(if($vc){$vc.Version}else{'NO'})"
$out -join [Environment]::NewLine | Out-File -FilePath $log -Encoding utf8
Write-Host "DONE"