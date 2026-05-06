#!/usr/bin/env bash
set -euo pipefail

if [[ "$(uname -s)" != Darwin* ]]; then
  echo "macOS publishing must run on macOS." >&2
  exit 1
fi

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/../.." && pwd)"
cd "$repo_root"

read_version() {
  sed -n 's:.*<VersionPrefix[^>]*>\(.*\)</VersionPrefix>.*:\1:p' Directory.Build.props | head -n 1
}

configuration="${CONFIGURATION:-Release}"
framework="${MACOS_FRAMEWORK:-net10.0-macos}"
runtime="${MACOS_RUNTIME:-osx-arm64}"
version="${VERSION:-$(read_version)}"
publish_root="${PUBLISH_ROOT:-artifacts/publish}"
dist_dir="${DIST_DIR:-artifacts/dist}"
publish_dir="$publish_root/macos-$runtime"
dmg_stage="$publish_root/macos-dmg-stage-$runtime"
app_name="${APP_NAME:-Effect Viewer}"
bundle_name="${APP_BUNDLE_NAME:-$app_name.app}"
artifact_base="${ARTIFACT_BASE:-effectviewer-$version-macos-$runtime}"

if [[ -z "$version" ]]; then
  echo "Could not read VersionPrefix from Directory.Build.props. Set VERSION explicitly." >&2
  exit 1
fi

if ! command -v hdiutil >/dev/null 2>&1; then
  echo "hdiutil is required to create a DMG." >&2
  exit 1
fi

rm -rf \
  EffectViewer.macOS/bin \
  EffectViewer.macOS/obj \
  EffectViewer/bin \
  EffectViewer/obj \
  "$publish_dir" \
  "$dmg_stage"
mkdir -p "$publish_dir" "$dist_dir" "$dmg_stage"

publish_args=(
  publish EffectViewer.macOS/EffectViewer.macOS.csproj
  --configuration "$configuration"
  --framework "$framework"
  --runtime "$runtime"
  --self-contained true
  --output "$publish_dir"
  -p:Version="$version"
)

if [[ -n "${MACOS_CODESIGN_KEY:-}" ]]; then
  publish_args+=("-p:CodesignKey=$MACOS_CODESIGN_KEY")
fi

if [[ -n "${MACOS_CODESIGN_PROVISION:-}" ]]; then
  publish_args+=("-p:CodesignProvision=$MACOS_CODESIGN_PROVISION")
fi

if [[ -n "${MACOS_CODESIGN_KEYCHAIN:-}" ]]; then
  publish_args+=("-p:CodesignKeychain=$MACOS_CODESIGN_KEYCHAIN")
fi

dotnet "${publish_args[@]}"

app_path="$(
  {
    find "$publish_dir" "EffectViewer.macOS/bin/$configuration/$framework/$runtime" \
      -maxdepth 5 -type d -name '*.app' 2>/dev/null || true
  } |
    sort |
    head -n 1
)"

if [[ -z "$app_path" ]]; then
  echo "macOS publish completed, but no .app bundle was found." >&2
  exit 1
fi

ditto "$app_path" "$dmg_stage/$bundle_name"
ln -s /Applications "$dmg_stage/Applications"

dmg_path="$dist_dir/$artifact_base.dmg"
rm -f "$dmg_path"
hdiutil create \
  -volname "$app_name $version" \
  -srcfolder "$dmg_stage" \
  -ov \
  -format UDZO \
  "$dmg_path"

echo "macOS DMG: $dmg_path"
