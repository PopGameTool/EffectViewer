#!/usr/bin/env bash
set -euo pipefail

if [[ "$(uname -s)" != Darwin* ]]; then
  echo "iOS publishing must run on macOS with Xcode installed." >&2
  exit 1
fi

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/../.." && pwd)"
cd "$repo_root"

read_version() {
  sed -n 's:.*<VersionPrefix[^>]*>\(.*\)</VersionPrefix>.*:\1:p' Directory.Build.props | head -n 1
}

configuration="${CONFIGURATION:-Release}"
framework="${IOS_FRAMEWORK:-net10.0-ios}"
runtime="${IOS_RUNTIME:-ios-arm64}"
version="${VERSION:-$(read_version)}"
publish_root="${PUBLISH_ROOT:-artifacts/publish}"
dist_dir="${DIST_DIR:-artifacts/dist}"
publish_dir="$publish_root/ios-$runtime"
artifact_base="${ARTIFACT_BASE:-effectviewer-$version-ios-$runtime}"

if [[ -z "$version" ]]; then
  echo "Could not read VersionPrefix from Directory.Build.props. Set VERSION explicitly." >&2
  exit 1
fi

rm -rf \
  EffectViewer.iOS/bin \
  EffectViewer.iOS/obj \
  EffectViewer/bin \
  EffectViewer/obj \
  "$publish_dir"
mkdir -p "$publish_dir" "$dist_dir"

dist_abs="$(cd "$dist_dir" && pwd)"
ipa_path="$dist_abs/$artifact_base.ipa"

publish_args=(
  publish EffectViewer.iOS/EffectViewer.iOS.csproj
  --configuration "$configuration"
  --framework "$framework"
  --runtime "$runtime"
  --output "$publish_dir"
  -p:BuildIpa=true
  -p:IpaPackagePath="$ipa_path"
  -p:Version="$version"
)

if [[ -n "${IOS_CODESIGN_KEY:-}" ]]; then
  publish_args+=("-p:CodesignKey=$IOS_CODESIGN_KEY")
fi

if [[ -n "${IOS_CODESIGN_PROVISION:-}" ]]; then
  publish_args+=("-p:CodesignProvision=$IOS_CODESIGN_PROVISION")
fi

if [[ -n "${IOS_CODESIGN_KEYCHAIN:-}" ]]; then
  publish_args+=("-p:CodesignKeychain=$IOS_CODESIGN_KEYCHAIN")
fi

if [[ -n "${IOS_CODESIGN_ENTITLEMENTS:-}" ]]; then
  publish_args+=("-p:CodesignEntitlements=$IOS_CODESIGN_ENTITLEMENTS")
fi

dotnet "${publish_args[@]}"

if [[ ! -f "$ipa_path" ]]; then
  discovered_ipa="$(
    {
      find "$publish_dir" "EffectViewer.iOS/bin/$configuration" \
        -type f -name '*.ipa' 2>/dev/null || true
    } |
      sort |
      tail -n 1
  )"

  if [[ -z "$discovered_ipa" ]]; then
    echo "iOS publish completed, but no .ipa package was found." >&2
    exit 1
  fi

  cp "$discovered_ipa" "$ipa_path"
fi

echo "iOS package: $ipa_path"
