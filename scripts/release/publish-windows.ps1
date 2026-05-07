[CmdletBinding()]
param(
    [string]$Version = $env:VERSION,
    [string]$Configuration = "Release",
    [string[]]$Runtimes = @("win-x64", "win-arm64"),
    [string]$PublishRoot = "artifacts/publish",
    [string]$DistDir = "artifacts/dist",
    [switch]$NoClean
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "../..")
Set-Location $repoRoot

if ([string]::IsNullOrWhiteSpace($Version)) {
    [xml]$props = Get-Content "Directory.Build.props"
    $Version = $props.SelectSingleNode("//VersionPrefix").InnerText.Trim()
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    throw "Could not read VersionPrefix from Directory.Build.props. Set -Version or VERSION explicitly."
}

if (-not $NoClean) {
    @(
        "EffectViewer.Windows/bin",
        "EffectViewer.Windows/obj",
        "EffectViewer/bin",
        "EffectViewer/obj"
    ) | ForEach-Object {
        Remove-Item $_ -Recurse -Force -ErrorAction Ignore
    }
}

New-Item -ItemType Directory -Path $PublishRoot, $DistDir -Force | Out-Null

foreach ($runtime in $Runtimes) {
    $publishDir = Join-Path $PublishRoot "windows-$runtime"
    Remove-Item $publishDir -Recurse -Force -ErrorAction Ignore
    New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

    dotnet publish "EffectViewer.Windows/EffectViewer.Windows.csproj" `
        --configuration $Configuration `
        --runtime $runtime `
        --self-contained true `
        --output $publishDir `
        "/p:Version=$Version"

    $archivePath = Join-Path $DistDir "effectviewer-$Version-windows-$runtime.zip"
    Remove-Item $archivePath -Force -ErrorAction Ignore
    Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $archivePath -Force

    Write-Host "Windows package: $archivePath"
}
