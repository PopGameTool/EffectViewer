[CmdletBinding()]
param(
    [string]$Version = $env:VERSION,
    [string]$Configuration = $env:CONFIGURATION,
    [string[]]$Runtimes = @(),
    [string]$PublishRoot = $env:PUBLISH_ROOT,
    [string]$DistDir = $env:DIST_DIR,
    [string]$DockerImage = $env:DOCKER_IMAGE
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

function Get-DockerPlatform {
    param([string]$Runtime)

    switch ($Runtime) {
        "linux-x64" { return "linux/amd64" }
        "linux-arm64" { return "linux/arm64" }
        default { throw "Unsupported Linux runtime '$Runtime'. Expected linux-x64 or linux-arm64." }
    }
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
$PublishRoot = Use-DefaultWhenBlank $PublishRoot "artifacts/publish"
$DistDir = Use-DefaultWhenBlank $DistDir "artifacts/dist"
$DockerImage = Use-DefaultWhenBlank $DockerImage "mcr.microsoft.com/dotnet/sdk:10.0"

if ($Runtimes.Count -eq 0) {
    if ([string]::IsNullOrWhiteSpace($env:LINUX_RUNTIMES)) {
        $Runtimes = @("linux-x64", "linux-arm64")
    } else {
        $Runtimes = $env:LINUX_RUNTIMES -split "\s+|," | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    }
}

docker version | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "Docker is required to publish Linux packages from this host."
}

$containerCommand = @'
set -euo pipefail
apt-get update
apt-get install -y --no-install-recommends clang zlib1g-dev ca-certificates
bash scripts/release/publish-linux.sh
'@

foreach ($runtime in $Runtimes) {
    $platform = Get-DockerPlatform $runtime
    $volume = "${repoRoot}:/src"

    $dockerArgs = @(
        "run", "--rm",
        "--platform", $platform,
        "-e", "DOTNET_NOLOGO=true",
        "-e", "DOTNET_CLI_TELEMETRY_OPTOUT=true",
        "-e", "VERSION=$Version",
        "-e", "CONFIGURATION=$Configuration",
        "-e", "LINUX_RUNTIMES=$runtime",
        "-e", "PUBLISH_ROOT=$PublishRoot",
        "-e", "DIST_DIR=$DistDir",
        "-v", $volume,
        "-w", "/src",
        $DockerImage,
        "bash", "-lc", $containerCommand
    )

    docker @dockerArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Docker Linux publish failed for $runtime with exit code $LASTEXITCODE."
    }
}
