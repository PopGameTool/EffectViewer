#!/usr/bin/env bash
set -euo pipefail

if [[ "$(uname -s)" != Linux* ]]; then
  echo "Linux publishing must run on Linux. Use publish-linux-docker.sh or publish-linux-docker.ps1 from macOS/Windows." >&2
  exit 1
fi

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/../.." && pwd)"
cd "$repo_root"

read_version() {
  sed -n 's:.*<VersionPrefix[^>]*>\(.*\)</VersionPrefix>.*:\1:p' Directory.Build.props | head -n 1
}

host_runtime() {
  case "$(uname -m)" in
    x86_64|amd64) echo "linux-x64" ;;
    aarch64|arm64) echo "linux-arm64" ;;
    *)
      echo "Unsupported Linux host architecture '$(uname -m)'." >&2
      exit 1
      ;;
  esac
}

validate_runtime_toolchain() {
  local runtime="$1"
  local host="$2"

  if [[ "$runtime" == "$host" ]]; then
    return
  fi

  if [[ "${ALLOW_LINUX_CROSS_ARCH:-}" == "1" ]]; then
    case "$runtime" in
      linux-arm64)
        if ! command -v aarch64-linux-gnu-gcc >/dev/null 2>&1; then
          echo "linux-arm64 cross-publish needs an arm64 native toolchain." >&2
          echo "On Ubuntu amd64, install: clang llvm binutils-aarch64-linux-gnu gcc-aarch64-linux-gnu zlib1g-dev:arm64" >&2
          exit 1
        fi
        ;;
      linux-x64)
        if ! command -v x86_64-linux-gnu-gcc >/dev/null 2>&1 && ! command -v gcc >/dev/null 2>&1; then
          echo "linux-x64 cross-publish needs an x64 native toolchain." >&2
          exit 1
        fi
        ;;
    esac

    return
  fi

  echo "Refusing native cross-architecture Linux publish from '$host' to '$runtime'." >&2
  echo "NativeAOT requires the target architecture C toolchain and target libraries." >&2
  echo "Use scripts/release/publish-linux-docker.sh for both x64 and arm64, or set ALLOW_LINUX_CROSS_ARCH=1 after installing the target toolchain." >&2
  exit 1
}

configuration="${CONFIGURATION:-Release}"
version="${VERSION:-$(read_version)}"
publish_root="${PUBLISH_ROOT:-artifacts/publish}"
dist_dir="${DIST_DIR:-artifacts/dist}"
artifact_prefix="${ARTIFACT_PREFIX:-effectviewer-$version-linux}"
host="$(host_runtime)"

if [[ -z "$version" ]]; then
  echo "Could not read VersionPrefix from Directory.Build.props. Set VERSION explicitly." >&2
  exit 1
fi

if (($# > 0)); then
  runtimes=("$@")
elif [[ -n "${LINUX_RUNTIMES:-}" ]]; then
  read -r -a runtimes <<< "$LINUX_RUNTIMES"
else
  runtimes=("$host")
fi

if ((${#runtimes[@]} == 0)); then
  echo "No Linux runtimes requested." >&2
  exit 1
fi

if ! command -v dotnet >/dev/null 2>&1; then
  echo "dotnet is required to publish Linux packages." >&2
  exit 1
fi

if ! command -v clang >/dev/null 2>&1; then
  echo "clang is required for NativeAOT Linux publishing. Install clang and zlib development headers." >&2
  exit 1
fi

if [[ "${NO_CLEAN:-}" != "1" ]]; then
  rm -rf \
    EffectViewer.Desktop/bin \
    EffectViewer.Desktop/obj \
    EffectViewer/bin \
    EffectViewer/obj
fi

mkdir -p "$publish_root" "$dist_dir"

for runtime in "${runtimes[@]}"; do
  case "$runtime" in
    linux-x64|linux-arm64) ;;
    *)
      echo "Unsupported Linux runtime '$runtime'. Expected linux-x64 or linux-arm64." >&2
      exit 1
      ;;
  esac
  validate_runtime_toolchain "$runtime" "$host"

  publish_dir="$publish_root/linux-$runtime"
  archive_arch="${runtime#linux-}"
  archive_path="$dist_dir/$artifact_prefix-$archive_arch.tar.gz"

  rm -rf "$publish_dir"
  mkdir -p "$publish_dir"

  dotnet publish EffectViewer.Desktop/EffectViewer.Desktop.csproj \
    --configuration "$configuration" \
    --runtime "$runtime" \
    --self-contained true \
    --output "$publish_dir" \
    -p:Version="$version"

  rm -f "$archive_path"
  tar -czf "$archive_path" -C "$publish_dir" .

  echo "Linux package: $archive_path"
done
