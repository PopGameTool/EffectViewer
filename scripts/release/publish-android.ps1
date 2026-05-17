[CmdletBinding()]
param(
    [string]$Version = $env:VERSION,
    [string]$Configuration = $env:CONFIGURATION,
    [string]$Framework = $env:ANDROID_FRAMEWORK,
    [string]$Runtime = $env:ANDROID_RUNTIME,
    [string]$AndroidHome = $env:ANDROID_HOME,
    [string]$AndroidNdkHome = $env:ANDROID_NDK_HOME,
    [string]$AndroidNdkVersion = $env:ANDROID_NDK_VERSION,
    [string]$HostLabel = $env:HOST_LABEL,
    [string]$PublishRoot = $env:PUBLISH_ROOT,
    [string]$DistDir = $env:DIST_DIR,
    [string]$ArtifactBase = $env:ARTIFACT_BASE,
    [string]$AndroidSigningKeyStore = $env:ANDROID_SIGNING_KEYSTORE,
    [string]$AndroidSigningKeyStoreBase64 = $env:ANDROID_SIGNING_KEYSTORE_BASE64,
    [string]$AndroidSigningKeyStorePath = $env:ANDROID_SIGNING_KEYSTORE_PATH,
    [string]$AndroidSigningKeyAlias = $env:ANDROID_SIGNING_KEY_ALIAS,
    [string]$AndroidSigningStorePass = $env:ANDROID_SIGNING_STORE_PASS,
    [string]$AndroidSigningKeyPass = $env:ANDROID_SIGNING_KEY_PASS,
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

function Resolve-AndroidSigningKeyStore {
    param(
        [string]$KeyStore,
        [string]$KeyStoreBase64,
        [string]$KeyStorePath,
        [string]$PublishRoot,
        [string]$HostLabel
    )

    if (-not [string]::IsNullOrWhiteSpace($KeyStore) -and -not [string]::IsNullOrWhiteSpace($KeyStoreBase64)) {
        throw "Set only one of -AndroidSigningKeyStore/ANDROID_SIGNING_KEYSTORE or -AndroidSigningKeyStoreBase64/ANDROID_SIGNING_KEYSTORE_BASE64."
    }

    if (-not [string]::IsNullOrWhiteSpace($KeyStoreBase64)) {
        $targetPath = Use-DefaultWhenBlank $KeyStorePath (Join-Path $PublishRoot "android-signing-$HostLabel.jks")
        $targetDir = Split-Path -Parent $targetPath
        if (-not [string]::IsNullOrWhiteSpace($targetDir)) {
            New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
        }

        $normalizedKeyStoreBase64 = $KeyStoreBase64 -replace "\s", ""
        [System.IO.File]::WriteAllBytes($targetPath, [System.Convert]::FromBase64String($normalizedKeyStoreBase64))
        return (Resolve-Path -LiteralPath $targetPath).Path
    }

    if ([string]::IsNullOrWhiteSpace($KeyStore)) {
        $KeyStore = $KeyStorePath
    }

    if (-not [string]::IsNullOrWhiteSpace($KeyStore)) {
        if (-not (Test-Path -LiteralPath $KeyStore -PathType Leaf)) {
            throw "Android signing keystore not found: '$KeyStore'."
        }

        return (Resolve-Path -LiteralPath $KeyStore).Path
    }

    return $null
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "../..")
Set-Location $repoRoot

if ([string]::IsNullOrWhiteSpace($Version)) {
    [xml]$props = Get-Content "Directory.Build.props"
    $Version = $props.SelectSingleNode("//VersionPrefix").InnerText.Trim()
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    throw "Could not read VersionPrefix from Directory.Build.props. Set -Version or VERSION explicitly."
}

$Configuration = Use-DefaultWhenBlank $Configuration "Release"
$Framework = Use-DefaultWhenBlank $Framework "net10.0-android"
$Runtime = Use-DefaultWhenBlank $Runtime "android-arm64"
$AndroidNdkVersion = Use-DefaultWhenBlank $AndroidNdkVersion "27.2.12479018"
$HostLabel = Use-DefaultWhenBlank $HostLabel "windows"
$PublishRoot = Use-DefaultWhenBlank $PublishRoot "artifacts/publish"
$DistDir = Use-DefaultWhenBlank $DistDir "artifacts/dist"
$ArtifactBase = Use-DefaultWhenBlank $ArtifactBase "effectviewer-$Version-android-$Runtime-$HostLabel"

if ([string]::IsNullOrWhiteSpace($AndroidHome)) {
    if ([string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) {
        $AndroidHome = Join-Path $HOME "AppData/Local/Android/Sdk"
    } else {
        $AndroidHome = Join-Path $env:LOCALAPPDATA "Android/Sdk"
    }
}

if (-not (Test-Path -LiteralPath $AndroidHome -PathType Container)) {
    throw "Android SDK not found at '$AndroidHome'. Install Android SDK or set -AndroidHome/ANDROID_HOME."
}

$AndroidHome = (Resolve-Path -LiteralPath $AndroidHome).Path

if ([string]::IsNullOrWhiteSpace($AndroidNdkHome)) {
    $AndroidNdkHome = Join-Path $AndroidHome "ndk/$AndroidNdkVersion"
}

if (-not (Test-Path -LiteralPath $AndroidNdkHome -PathType Container)) {
    throw "Android NDK not found at '$AndroidNdkHome'. Install ndk;$AndroidNdkVersion or set -AndroidNdkHome/ANDROID_NDK_HOME."
}

$AndroidNdkHome = (Resolve-Path -LiteralPath $AndroidNdkHome).Path
$env:ANDROID_HOME = $AndroidHome
$env:ANDROID_NDK_HOME = $AndroidNdkHome

$publishDir = Join-Path $PublishRoot "android-$Runtime-$HostLabel"

if (-not $NoClean) {
    @(
        "EffectViewer.Android/bin",
        "EffectViewer.Android/obj",
        "EffectViewer/bin",
        "EffectViewer/obj",
        $publishDir
    ) | ForEach-Object {
        Remove-Item $_ -Recurse -Force -ErrorAction Ignore
    }
}

New-Item -ItemType Directory -Path $publishDir, $DistDir -Force | Out-Null

$resolvedSigningKeyStore = Resolve-AndroidSigningKeyStore `
    -KeyStore $AndroidSigningKeyStore `
    -KeyStoreBase64 $AndroidSigningKeyStoreBase64 `
    -KeyStorePath $AndroidSigningKeyStorePath `
    -PublishRoot $PublishRoot `
    -HostLabel $HostLabel

if ($RestoreWorkload) {
    dotnet workload restore "EffectViewer.Android/EffectViewer.Android.csproj" --skip-manifest-update
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet workload restore failed with exit code $LASTEXITCODE."
    }
}

$publishArgs = @(
    "publish",
    "EffectViewer.Android/EffectViewer.Android.csproj",
    "--configuration", $Configuration,
    "--framework", $Framework,
    "--runtime", $Runtime,
    "--output", $publishDir,
    "-p:AndroidSdkDirectory=$AndroidHome",
    "-p:AndroidNdkDirectory=$AndroidNdkHome",
    "-p:AndroidNdkVersion=$AndroidNdkVersion",
    "-p:Version=$Version"
)

if (-not [string]::IsNullOrWhiteSpace($resolvedSigningKeyStore)) {
    if ([string]::IsNullOrWhiteSpace($AndroidSigningKeyAlias) -or [string]::IsNullOrWhiteSpace($AndroidSigningStorePass)) {
        throw "Android signing requires -AndroidSigningKeyAlias/ANDROID_SIGNING_KEY_ALIAS and -AndroidSigningStorePass/ANDROID_SIGNING_STORE_PASS."
    }

    $AndroidSigningKeyPass = Use-DefaultWhenBlank $AndroidSigningKeyPass $AndroidSigningStorePass
    $publishArgs += @(
        "-p:AndroidKeyStore=true",
        "-p:AndroidSigningKeyStore=$resolvedSigningKeyStore",
        "-p:AndroidSigningStorePass=$AndroidSigningStorePass",
        "-p:AndroidSigningKeyAlias=$AndroidSigningKeyAlias",
        "-p:AndroidSigningKeyPass=$AndroidSigningKeyPass"
    )
}

dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$searchRoots = @(
    $publishDir,
    (Join-Path "EffectViewer.Android/bin" $Configuration)
) | Where-Object {
    Test-Path -LiteralPath $_ -PathType Container
}

$packages = $searchRoots |
    ForEach-Object {
        Get-ChildItem -LiteralPath $_ -Recurse -File -ErrorAction SilentlyContinue |
            Where-Object { $_.Extension -in ".apk", ".aab" }
    }

$package = $packages |
    Where-Object { $_.Name -like "*-Signed.apk" } |
    Sort-Object LastWriteTimeUtc, FullName -Descending |
    Select-Object -First 1

if ($null -eq $package) {
    $package = $packages |
        Sort-Object LastWriteTimeUtc, FullName -Descending |
        Select-Object -First 1
}

if ($null -eq $package) {
    throw "Android publish completed, but no .apk or .aab package was found."
}

$artifactPath = Join-Path $DistDir "$ArtifactBase$($package.Extension)"
Copy-Item -LiteralPath $package.FullName -Destination $artifactPath -Force

Write-Host "Android package: $artifactPath"
