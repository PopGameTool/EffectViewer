#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/../.." && pwd)"
cd "$repo_root"

read_version() {
  sed -n 's:.*<VersionPrefix[^>]*>\(.*\)</VersionPrefix>.*:\1:p' Directory.Build.props | head -n 1
}

host_label_from_uname() {
  case "$(uname -s)" in
    Darwin*) echo "macos" ;;
    Linux*) echo "linux" ;;
    MINGW*|MSYS*|CYGWIN*) echo "windows" ;;
    *) uname -s | tr '[:upper:]' '[:lower:]' ;;
  esac
}

default_android_home() {
  case "$(uname -s)" in
    Darwin*)
      printf '%s\n' "$HOME/Library/Android/sdk"
      ;;
    Linux*)
      printf '%s\n' "$HOME/Android/Sdk"
      ;;
    MINGW*|MSYS*|CYGWIN*)
      if [[ -n "${LOCALAPPDATA:-}" ]]; then
        if command -v cygpath >/dev/null 2>&1; then
          cygpath -u "$LOCALAPPDATA/Android/Sdk"
        else
          printf '%s\n' "$LOCALAPPDATA\\Android\\Sdk"
        fi
      else
        printf '%s\n' "$HOME/AppData/Local/Android/Sdk"
      fi
      ;;
    *)
      printf '%s\n' "$HOME/Android/Sdk"
      ;;
  esac
}

to_posix_path() {
  local value="$1"
  if command -v cygpath >/dev/null 2>&1 && [[ "$value" =~ ^[A-Za-z]:[\\/].* ]]; then
    cygpath -u "$value"
  else
    printf '%s\n' "$value"
  fi
}

to_msbuild_path() {
  local value="$1"
  case "$(uname -s)" in
    MINGW*|MSYS*|CYGWIN*)
      if command -v cygpath >/dev/null 2>&1; then
        cygpath -w "$value"
      else
        printf '%s\n' "$value"
      fi
      ;;
    *)
      printf '%s\n' "$value"
      ;;
  esac
}

configuration="${CONFIGURATION:-Release}"
framework="${ANDROID_FRAMEWORK:-net10.0-android}"
runtime="${ANDROID_RUNTIME:-android-arm64}"
ndk_version="${ANDROID_NDK_VERSION:-27.2.12479018}"
version="${VERSION:-$(read_version)}"
host_label="${HOST_LABEL:-$(host_label_from_uname)}"
publish_root="${PUBLISH_ROOT:-artifacts/publish}"
dist_dir="${DIST_DIR:-artifacts/dist}"
publish_dir="$publish_root/android-$runtime-$host_label"
artifact_base="${ARTIFACT_BASE:-effectviewer-$version-android-$runtime-$host_label}"

if [[ -z "$version" ]]; then
  echo "Could not read VersionPrefix from Directory.Build.props. Set VERSION explicitly." >&2
  exit 1
fi

android_home="${ANDROID_HOME:-$(default_android_home)}"
android_home_posix="$(to_posix_path "$android_home")"
android_home_msbuild="$(to_msbuild_path "$android_home_posix")"
export ANDROID_HOME="$android_home"

android_ndk_home="${ANDROID_NDK_HOME:-$android_home_posix/ndk/$ndk_version}"
android_ndk_posix="$(to_posix_path "$android_ndk_home")"
if [[ ! -d "$android_ndk_posix" ]]; then
  echo "Android NDK not found at '$android_ndk_home'." >&2
  echo "Install ndk;$ndk_version or set ANDROID_NDK_HOME/ANDROID_NDK_VERSION." >&2
  exit 1
fi

android_ndk_msbuild="$(to_msbuild_path "$android_ndk_posix")"
case "$android_ndk_msbuild" in
  *[\\/]*) ;;
  *) android_ndk_msbuild="$android_ndk_msbuild/" ;;
esac
export ANDROID_NDK_HOME="$android_ndk_home"

rm -rf \
  EffectViewer.Android/bin \
  EffectViewer.Android/obj \
  EffectViewer/bin \
  EffectViewer/obj \
  "$publish_dir"
mkdir -p "$publish_dir" "$dist_dir"

dotnet publish EffectViewer.Android/EffectViewer.Android.csproj \
  --configuration "$configuration" \
  --framework "$framework" \
  --runtime "$runtime" \
  --output "$publish_dir" \
  -p:AndroidSdkDirectory="$android_home_msbuild" \
  -p:AndroidNdkDirectory="$android_ndk_msbuild" \
  -p:AndroidNdkVersion="$ndk_version" \
  -p:Version="$version"

package_path="$(
  {
    find "$publish_dir" "EffectViewer.Android/bin/$configuration" \
      -type f \( -name '*.apk' -o -name '*.aab' \) 2>/dev/null || true
  } |
    sort |
    tail -n 1
)"

if [[ -z "$package_path" ]]; then
  echo "Android publish completed, but no .apk or .aab package was found." >&2
  exit 1
fi

package_ext="${package_path##*.}"
artifact_path="$dist_dir/$artifact_base.$package_ext"
cp "$package_path" "$artifact_path"

echo "Android package: $artifact_path"
