#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/../.." && pwd)"
cd "$repo_root"

read_version() {
  sed -n 's:.*<VersionPrefix[^>]*>\(.*\)</VersionPrefix>.*:\1:p' Directory.Build.props | head -n 1
}

configuration="${CONFIGURATION:-Release}"
framework="${BROWSER_FRAMEWORK:-net10.0-browser}"
version="${VERSION:-$(read_version)}"
publish_root="${PUBLISH_ROOT:-artifacts/publish}"
dist_dir="${DIST_DIR:-artifacts/dist}"
publish_dir="$publish_root/browser"
artifact_base="${ARTIFACT_BASE:-effectviewer-$version-browser}"

if [[ -z "$version" ]]; then
  echo "Could not read VersionPrefix from Directory.Build.props. Set VERSION explicitly." >&2
  exit 1
fi

if ! command -v dotnet >/dev/null 2>&1; then
  echo "dotnet is required to publish Browser packages." >&2
  exit 1
fi

if ! command -v zip >/dev/null 2>&1; then
  echo "zip is required to package Browser output." >&2
  exit 1
fi

if [[ "${RESTORE_WORKLOAD:-}" == "1" ]]; then
  dotnet workload restore EffectViewer.Browser/EffectViewer.Browser.csproj --skip-manifest-update
fi

if [[ "${NO_CLEAN:-}" != "1" ]]; then
  rm -rf \
    EffectViewer.Browser/bin \
    EffectViewer.Browser/obj \
    EffectViewer/bin \
    EffectViewer/obj \
    "$publish_dir"
fi

mkdir -p "$publish_dir" "$dist_dir"

dotnet publish EffectViewer.Browser/EffectViewer.Browser.csproj \
  --configuration "$configuration" \
  --framework "$framework" \
  --output "$publish_dir" \
  -p:Version="$version"

archive_path="$dist_dir/$artifact_base.zip"
rm -f "$archive_path"
(
  cd "$publish_dir"
  zip -qr "$repo_root/$archive_path" .
)

echo "Browser package: $archive_path"
