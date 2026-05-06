#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/../.." && pwd)"
cd "$repo_root"

read_version() {
  sed -n 's:.*<VersionPrefix[^>]*>\(.*\)</VersionPrefix>.*:\1:p' Directory.Build.props | head -n 1
}

docker_platform_for_runtime() {
  case "$1" in
    linux-x64) echo "linux/amd64" ;;
    linux-arm64) echo "linux/arm64" ;;
    *)
      echo "Unsupported Linux runtime '$1'. Expected linux-x64 or linux-arm64." >&2
      exit 1
      ;;
  esac
}

configuration="${CONFIGURATION:-Release}"
version="${VERSION:-$(read_version)}"
publish_root="${PUBLISH_ROOT:-artifacts/publish}"
dist_dir="${DIST_DIR:-artifacts/dist}"
docker_image="${DOCKER_IMAGE:-mcr.microsoft.com/dotnet/sdk:10.0}"

if [[ -z "$version" ]]; then
  echo "Could not read VersionPrefix from Directory.Build.props. Set VERSION explicitly." >&2
  exit 1
fi

if (($# > 0)); then
  runtimes=("$@")
else
  read -r -a runtimes <<< "${LINUX_RUNTIMES:-linux-x64 linux-arm64}"
fi

if ! command -v docker >/dev/null 2>&1; then
  echo "Docker is required to publish Linux packages from this host." >&2
  exit 1
fi

host_uid=""
host_gid=""
if command -v id >/dev/null 2>&1; then
  host_uid="$(id -u)"
  host_gid="$(id -g)"
fi

container_command='
set -euo pipefail
apt-get update
apt-get install -y --no-install-recommends clang zlib1g-dev ca-certificates
bash scripts/release/publish-linux.sh
if [ -n "${HOST_UID:-}" ] && [ -n "${HOST_GID:-}" ]; then
  chown -R "$HOST_UID:$HOST_GID" artifacts EffectViewer/bin EffectViewer/obj EffectViewer.Desktop/bin EffectViewer.Desktop/obj 2>/dev/null || true
fi
'

for runtime in "${runtimes[@]}"; do
  platform="$(docker_platform_for_runtime "$runtime")"

  docker run --rm \
    --platform "$platform" \
    -e DOTNET_NOLOGO=true \
    -e DOTNET_CLI_TELEMETRY_OPTOUT=true \
    -e VERSION="$version" \
    -e CONFIGURATION="$configuration" \
    -e LINUX_RUNTIMES="$runtime" \
    -e PUBLISH_ROOT="$publish_root" \
    -e DIST_DIR="$dist_dir" \
    -e HOST_UID="$host_uid" \
    -e HOST_GID="$host_gid" \
    -v "$repo_root:/src" \
    -w /src \
    "$docker_image" \
    bash -lc "$container_command"
done
