<#
.SYNOPSIS
    Detect the latest Elgato Wave Link (Windows) release and download its x64 MSIX.

.DESCRIPTION
    Uses the Elgato Zendesk REST API on the RAW host elgato.zendesk.com (NOT help.elgato.com,
    which sits behind Akamai and only serves a JS-challenge page to non-browser clients, so the
    MSIX link can never be extracted from it). Picks the highest Windows version among the
    release-notes articles, extracts the official CDN MSIX direct link from the article body,
    downloads it, validates it (ZIP/PK magic + minimum size), and tracks the seen version in a
    state file so scheduled runs skip re-releasing the same version.

    Outputs a JSON summary to stdout: { version, msixUrl, msixPath, shouldRelease }.
    When running inside GitHub Actions (env:GITHUB_OUTPUT set), it also writes the step outputs
    should_release / release_tag / app_version / msix_path.

.PARAMETER OutputDir
    Where to save the downloaded MSIX (default: input).

.PARAMETER VersionFile
    State file holding the last released version (default: wavelink-app-version.txt).

.PARAMETER Force
    Ignore the version-dedup check and always treat this as a new release.

.PARAMETER ApiHost
    Zendesk API host (default: elgato.zendesk.com).

.PARAMETER SectionId
    Help-center section id that contains the Wave Link release-notes articles.

.EXAMPLE
    pwsh scripts/detect-wavelink-msix.ps1 -OutputDir input -VersionFile wavelink-app-version.txt
#>
[CmdletBinding()]
param(
    [string]$OutputDir   = "input",
    [string]$VersionFile = "wavelink-app-version.txt",
    [switch]$Force,
    [string]$ApiHost     = "elgato.zendesk.com",
    [string]$SectionId   = "4913442828941"
)

# Guard against an empty host/section (e.g. a stray empty positional argument from the
# calling workflow) so we never build a malformed "https:///..." URL that makes curl fail.
if ([string]::IsNullOrWhiteSpace($ApiHost))   { $ApiHost   = "elgato.zendesk.com" }
if ([string]::IsNullOrWhiteSpace($SectionId)) { $SectionId = "4913442828941" }

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
            Write-Host "  -> head: $($raw.Substring(0, [Math]::Min(200, $raw.Length)))"
            return $null
        }
        return ($raw | ConvertFrom-Json)
    } finally {
        Remove-Item $tmp.FullName -Force -ErrorAction SilentlyContinue
    }
}

# 1) Article list for the release-notes section.
$sectionJson = Fetch-Json "https://$ApiHost/api/v2/help_center/en-us/sections/$SectionId/articles.json"
if (-not $sectionJson -or -not $sectionJson.articles) {
    throw "Could not retrieve the release-notes article list from $ApiHost."
}

# 2) Highest Windows version (title format: "Elgato Wave Link X.Y.Z (Windows) Release Notes").
$best = $null; $bestVer = [version]'0.0.0'
foreach ($a in $sectionJson.articles) {
    if ($a.title -match 'Elgato Wave Link (\d+\.\d+\.\d+) \(Windows\) Release Notes') {
        try {
            $v = [version]$Matches[1]
            if ($v -gt $bestVer) { $bestVer = $v; $best = $a }
        } catch { }
    }
}
if (-not $best) { throw "No Windows release-notes article found in the API response." }
$latest    = $bestVer.ToString()
$articleId = $best.id
Write-Host "Latest Wave Link (Windows): $latest (article id $articleId)"

# 3) Article body -> MSIX direct link.
$artJson = Fetch-Json "https://$ApiHost/api/v2/help_center/en-us/articles/$articleId.json"
if (-not $artJson) { throw "Could not retrieve article $articleId JSON." }
$bodyHtml = $artJson.article.body
$ms = [regex]::Match($bodyHtml, 'https://edge\.elgato\.com/egc/windows/ewlw/[\d.]+/Stable/Elgato\.WaveLink_[\d.]+_x64\.msix')
if (-not $ms.Success) { throw "No x64 MSIX URL found in the release-notes article body." }
$msixUrl = $ms.Value
Write-Host "MSIX: $msixUrl"

# 4) Dedup vs. the last seen version.
$current = if (Test-Path $VersionFile) { (Get-Content $VersionFile -Raw).Trim() } else { "" }
$should  = if ($Force) { $true } else { ($latest -ne $current) }
if (-not $should) {
    Write-Host "No update (current=$current, latest=$latest). Skipping release."
    @{ version = $latest; msixUrl = $msixUrl; msixPath = $null; shouldRelease = $false } | ConvertTo-Json -Compress
    if ($env:GITHUB_OUTPUT) { Add-Content -Path $env:GITHUB_OUTPUT -Value "should_release=false" }
    exit 0
}

# 5) Download + validate the MSIX.
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$dest = Join-Path $OutputDir ([System.IO.Path]::GetFileName($msixUrl))
curl.exe --compressed -sL -A $ua -o $dest $msixUrl
if (-not (Test-Path $dest)) { throw "MSIX download failed (file not created)." }
$bytes = [System.IO.File]::ReadAllBytes($dest)
if ($bytes.Length -lt 100KB -or $bytes[0] -ne 0x50 -or $bytes[1] -ne 0x4B) {
    throw ("Downloaded file is not a valid MSIX (size=" + $bytes.Length + ", magic=" + $bytes[0] + "/" + $bytes[1] + ").")
}
Write-Host ("Downloaded -> " + $dest + " (" + [math]::Round($bytes.Length / 1MB, 2) + " MB)")

Set-Content -Path $VersionFile -Value $latest -NoNewline
@{ version = $latest; msixUrl = $msixUrl; msixPath = $dest; shouldRelease = $true } | ConvertTo-Json -Compress
if ($env:GITHUB_OUTPUT) {
    Add-Content -Path $env:GITHUB_OUTPUT -Value "should_release=true"
    Add-Content -Path $env:GITHUB_OUTPUT -Value "release_tag=wavelink-$latest"
    Add-Content -Path $env:GITHUB_OUTPUT -Value "app_version=$latest"
    Add-Content -Path $env:GITHUB_OUTPUT -Value "msix_path=$dest"
}
