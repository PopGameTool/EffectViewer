[CmdletBinding()]
param(
    [string]$Version = $env:VERSION,
    [string]$Configuration = $env:CONFIGURATION,
    [string]$Framework = $env:BROWSER_FRAMEWORK,
    [string]$PublishRoot = $env:PUBLISH_ROOT,
    [string]$DistDir = $env:DIST_DIR,
    [string]$ArtifactBase = $env:ARTIFACT_BASE,
    [switch]$NoClean,
    [switch]$RestoreWorkload
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Use-DefaultWhenBlank {
    param(
        [string]$Value,
        [string]$DefaultValue
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $DefaultValue
    }

    return $Value
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
Set-Location $repoRoot

if ([string]::IsNullOrWhiteSpace($Version)) {
    [xml]$props = Get-Content "Directory.Build.props"
    $Version = $props.SelectSingleNode("//VersionPrefix").InnerText.Trim()
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    throw "Could not read VersionPrefix from Directory.Build.props. Set -Version or VERSION explicitly."
}

$Configuration = Use-DefaultWhenBlank $Configuration "Release"
$Framework = Use-DefaultWhenBlank $Framework "net10.0-browser"
$PublishRoot = Use-DefaultWhenBlank $PublishRoot "artifacts/publish"
$DistDir = Use-DefaultWhenBlank $DistDir "artifacts/dist"
$ArtifactBase = Use-DefaultWhenBlank $ArtifactBase "effectviewer-$Version-browser"
$publishDir = Join-Path $PublishRoot "browser"

if ($RestoreWorkload) {
    dotnet workload restore "EffectViewer.Browser/EffectViewer.Browser.csproj" --skip-manifest-update
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet workload restore failed with exit code $LASTEXITCODE."
    }
}

if (-not $NoClean) {
    @(
        "EffectViewer.Browser/bin",
        "EffectViewer.Browser/obj",
        "EffectViewer/bin",
        "EffectViewer/obj",
        $publishDir
    ) | ForEach-Object {
        Remove-Item $_ -Recurse -Force -ErrorAction Ignore
    }
}

New-Item -ItemType Directory -Path $publishDir, $DistDir -Force | Out-Null

$publishArgs = @(
    "publish",
    "EffectViewer.Browser/EffectViewer.Browser.csproj",
    "--configuration", $Configuration,
    "--framework", $Framework,
    "--output", $publishDir,
    "-p:Version=$Version"
)

dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$archivePath = Join-Path $DistDir "$ArtifactBase.zip"
Remove-Item $archivePath -Force -ErrorAction Ignore
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $archivePath -Force

Write-Host "Browser package: $archivePath"
