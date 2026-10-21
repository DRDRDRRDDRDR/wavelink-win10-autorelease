<#
.SYNOPSIS
    Detect the latest Elgato Wave Link (Windows) BETA release and download its x64 MSIX.

.DESCRIPTION
    The stable channel is discovered through the Zendesk release-notes articles, but Elgato does
    NOT publish release-notes articles for beta builds, so detect-wavelink-msix.ps1 can never see
    them. The beta channel is instead served by the official update-check endpoint used by the
    Wave Link updater itself:

      https://gc-updates.elgato.com/windows/ewlw-update/beta/app-version-check.json.php

    IMPORTANT: in that JSON the Automatic.Version / Manual.Version fields merely ECHO BACK the
    appVersion you sent in the query string. They are not the upstream version. The authoritative
    signal is the MSIX file name inside fileURLs, which looks like
    https://edge.elgato.com/egc/windows/ewlw/3.5/Beta/Elgato.WaveLink_3.5.0.4892_x64.msix

    This script therefore probes with a deliberately low appVersion, extracts the real version
    from the x86_64 fileURL, downloads it, validates it, and tracks the seen version in its own
    state file so scheduled runs skip re-releasing the same build.

    Outputs a JSON summary to stdout: { version, msixUrl, msixPath, shouldRelease, channel }.
    Inside GitHub Actions it also writes step outputs should_release / release_tag / app_version /
    msix_path / channel.

.PARAMETER OutputDir
    Where to save the downloaded MSIX. Default: input-beta.

.PARAMETER VersionFile
    State file holding the last released beta version. Default: wavelink-beta-version.txt.

.PARAMETER Channel
    Update channel segment in the endpoint path. Default: beta.

.PARAMETER ProbeVersion
    appVersion sent to the endpoint. Any value works because the response echoes it back; a low
    value keeps the request semantically honest.

.PARAMETER Force
    Ignore the version-dedup check and always treat this as a new release.

.EXAMPLE
    pwsh scripts/detect-wavelink-msix-beta.ps1 -OutputDir input-beta
#>
[CmdletBinding()]
param(
    [string]$OutputDir    = "input-beta",
    [string]$VersionFile  = "wavelink-beta-version.txt",
    [string]$Channel      = "beta",
    [string]$ProbeVersion = "0.0.0.0",
    [switch]$Force
)

if ([string]::IsNullOrWhiteSpace($Channel)) { $Channel = "beta" }

$ErrorActionPreference = "Stop"
$ua = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"

function Fetch-Json($Url) {
    Write-Host "Fetching: $Url"
    $tmp = New-TemporaryFile
    try {
        $status = (curl.exe --compressed -sL -A $ua -H "Accept: application/json" -o $tmp.FullName -w "%{http_code}" $Url).Trim()
        $raw = Get-Content -Raw $tmp.FullName
        Write-Host "  -> HTTP $status | $($raw.Length) bytes"
        if ($status -ne "200") {
            $head = if ($raw.Length -gt 200) { $raw.Substring(0, 200) } else { $raw }
            Write-Host "  -> head: $head"
            return $null
        }
        return ($raw | ConvertFrom-Json)
    } finally {
        Remove-Item $tmp.FullName -Force -ErrorAction SilentlyContinue
    }
}

# 1) Ask the beta channel endpoint what it would install.
$apiUrl = "https://gc-updates.elgato.com/windows/ewlw-update/$Channel/app-version-check.json.php" +
          "?osName=Windows&osVersion=10.0.19045.0&osPlatform=x86_64" +
          "&appVersion=$ProbeVersion&appPlatform=x86_64&appLanguage=en-US"

$json = Fetch-Json $apiUrl
if (-not $json) { throw "Could not retrieve the $Channel update manifest." }

# 2) Prefer the Automatic branch, fall back to Manual. Both carry the same fileURLs.
$branch = $null
if ($json.Automatic -and $json.Automatic.fileURLs -and $json.Automatic.fileURLs.x86_64) {
    $branch = $json.Automatic
} elseif ($json.Manual -and $json.Manual.fileURLs -and $json.Manual.fileURLs.x86_64) {
    $branch = $json.Manual
}
if (-not $branch) { throw "Neither Automatic nor Manual branch exposed an x86_64 MSIX in the $Channel manifest." }

$msixUrl = $branch.fileURLs.x86_64
Write-Host "MSIX: $msixUrl"

# 3) The real version lives in the file name, not in the echoed Version field.
$vm = [regex]::Match($msixUrl, 'Elgato\.WaveLink_([\d.]+)_x64\.msix')
if (-not $vm.Success) { throw "Could not parse a version out of the MSIX URL: $msixUrl" }
$latest = $vm.Groups[1].Value

$echoed = $branch.Version
if ($echoed -and $echoed -ne $latest) {
    Write-Host "Note: manifest Version field says '$echoed' but the MSIX filename says '$latest'."
    Write-Host "      The filename is authoritative; the Version field echoes the requested appVersion."
}
Write-Host "Latest Wave Link ($Channel): $latest"

$notesUrl = if ($branch.ReleaseNotes -and $branch.ReleaseNotes.en) { $branch.ReleaseNotes.en } else { "" }
$breaking = if ($branch.IsBreakingUpdate) { $true } else { $false }
if ($notesUrl) { Write-Host "Release notes: $notesUrl" }
if ($breaking) { Write-Host "WARNING: upstream flags this as a BREAKING update." }

# IsManualInstallationRequired is NOT a property of the build. It is computed against the
# appVersion you asked about: probe with 0.0.0.0 or any version below 3.0 and the endpoint says
# "manual installation required", because it assumes an ancient client that cannot self-update.
# Probe with a plausible recent version and the same build reports False. It is therefore
# meaningless here and deliberately not surfaced as a warning; the SupportedOSVersionEQGT gate
# is the real signal and it is reported separately below.
$minOs = if ($json.SupportedOSVersionEQGT) { ($json.SupportedOSVersionEQGT -join ', ') } else { "" }
if ($minOs) {
    Write-Host "Upstream minimum OS: $minOs (the Win10 patch is what makes the package installable regardless)"
}

# 4) Dedup vs. the last released beta version.
$current = if (Test-Path $VersionFile) { (Get-Content $VersionFile -Raw).Trim() } else { "" }
$should  = if ($Force) { $true } else { ($latest -ne $current) }
if (-not $should) {
    Write-Host "No beta update (current=$current, latest=$latest). Skipping release."
    @{ version = $latest; msixUrl = $msixUrl; msixPath = $null; shouldRelease = $false; channel = $Channel } |
        ConvertTo-Json -Compress
    if ($env:GITHUB_OUTPUT) { Add-Content -Path $env:GITHUB_OUTPUT -Value "should_release=false" }
    exit 0
}

# 5) Download + validate the MSIX.
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$dest = Join-Path $OutputDir ([System.IO.Path]::GetFileName($msixUrl))

# Reuse an already-downloaded copy when it is intact. A 187 MB MSIX over a flaky link is the
# most common failure point of this workflow, and a plain retry re-downloads everything for
# nothing. A local file already carrying the expected name and passing the ZIP magic + size
# check holds the same bytes the CDN would hand back, so skip the transfer.
$reused = $false
if ((Test-Path $dest) -and -not $Force) {
    $existing = Get-Item $dest
    if ($existing.Length -gt 100MB) {
        $st = [System.IO.File]::OpenRead($dest)
        try {
            $m = New-Object byte[] 2
            $null = $st.Read($m, 0, 2)
        } finally { $st.Dispose() }
        if ($m[0] -eq 0x50 -and $m[1] -eq 0x4B) {
            $reused = $true
            Write-Host ("Reusing existing local MSIX (" + [math]::Round($existing.Length / 1MB, 2) + " MB); skipping download.")
        }
    }
}
if (-not $reused) {
    curl.exe --compressed -sL -A $ua -o $dest $msixUrl
    if (-not (Test-Path $dest)) { throw "MSIX download failed (file not created)." }
}
$bytes = [System.IO.File]::ReadAllBytes($dest)
if ($bytes.Length -lt 100KB -or $bytes[0] -ne 0x50 -or $bytes[1] -ne 0x4B) {
    throw ("Downloaded file is not a valid MSIX (size=" + $bytes.Length + ", magic=" + $bytes[0] + "/" + $bytes[1] + ").")
}
$sha = (Get-FileHash $dest -Algorithm SHA256).Hash
if ($reused) { Write-Host ("Reused   -> " + $dest + " (" + [math]::Round($bytes.Length / 1MB, 2) + " MB)") }
else         { Write-Host ("Downloaded -> " + $dest + " (" + [math]::Round($bytes.Length / 1MB, 2) + " MB)") }
Write-Host ("SHA256    -> " + $sha)

Set-Content -Path $VersionFile -Value $latest -NoNewline

$summary = @{
    version       = $latest
    msixUrl       = $msixUrl
    msixPath      = $dest
    shouldRelease = $true
    channel       = $Channel
    sha256        = $sha
    sizeBytes     = $bytes.Length
    releaseNotes  = $notesUrl
}
$summary | ConvertTo-Json -Compress

if ($env:GITHUB_OUTPUT) {
    Add-Content -Path $env:GITHUB_OUTPUT -Value "should_release=true"
    Add-Content -Path $env:GITHUB_OUTPUT -Value "release_tag=wavelink-$Channel-$latest"
    Add-Content -Path $env:GITHUB_OUTPUT -Value "app_version=$latest"
    Add-Content -Path $env:GITHUB_OUTPUT -Value "msix_path=$dest"
    Add-Content -Path $env:GITHUB_OUTPUT -Value "channel=$Channel"
    Add-Content -Path $env:GITHUB_OUTPUT -Value "sha256=$sha"
    Add-Content -Path $env:GITHUB_OUTPUT -Value "size_bytes=$($bytes.Length)"
    Add-Content -Path $env:GITHUB_OUTPUT -Value "release_notes=$notesUrl"
    Add-Content -Path $env:GITHUB_OUTPUT -Value "breaking=$breaking"
}
