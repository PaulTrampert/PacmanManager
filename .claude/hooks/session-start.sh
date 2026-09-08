#!/usr/bin/env bash
#
# SessionStart hook for Claude Code on the web.
#
# The cloud image is Ubuntu, but this repository is a pacman repository host: it needs the .NET 10
# SDK, pacman's own tooling (repo-add/repo-remove and libalpm, which LibAlpmSharp P/Invokes), a JRE
# for the ANTLR grammar build, and a Docker daemon for the Testcontainers end to end tests. This
# installs all of it. A local Arch checkout already has the pacman half, so the hook is a no-op
# outside the remote environment.
set -euo pipefail

# Everything this hook says goes to stderr; stdout is the hook's protocol channel.
log() { printf '[session-start] %s\n' "$*" >&2; }

if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  log "not a remote session, nothing to install"
  exit 0
fi

export DEBIAN_FRONTEND=noninteractive

# sudo is present and passwordless in the cloud image; fall back to running directly when the hook
# is exercised as root.
if [ "$(id -u)" -eq 0 ]; then
  as_root() { "$@"; }
else
  as_root() { sudo -E "$@"; }
fi

# --- apt packages -------------------------------------------------------------------------------
#
# dotnet-sdk-10.0            net10.0, the target framework of every project. Ubuntu 24.04 ships it
#                            in noble-updates, so no third party feed is needed.
# pacman-package-manager     repo-add and repo-remove, which RepoHost shells out to.
# libalpm-dev                the libalpm.so symlink NativeMethods' DllImport("libalpm.so") resolves;
#                            it pulls in libalpm13t64, the library itself.
# libarchive-tools, zstd     bsdtar and zstd, which test-fixtures/packages/build-fixture.sh uses.
# default-jre-headless       Antlr4BuildTasks runs the ANTLR jar to generate the pacman.conf parser.
PACKAGES=(
  dotnet-sdk-10.0
  pacman-package-manager
  libalpm-dev
  libarchive-tools
  zstd
  default-jre-headless
)

missing=()
for pkg in "${PACKAGES[@]}"; do
  dpkg-query -W -f='${Status}' "$pkg" 2>/dev/null | grep -q "^install ok installed$" || missing+=("$pkg")
done

if [ ${#missing[@]} -gt 0 ]; then
  log "installing: ${missing[*]}"
  # Third party PPAs baked into the image are not reachable through the egress proxy and make
  # apt-get update noisy; their failures are warnings, so the update still succeeds.
  as_root apt-get update -qq || log "apt-get update reported errors, continuing with the cached lists"
  as_root apt-get install -y -qq --no-install-recommends "${missing[@]}" >&2
else
  log "apt packages already present"
fi

# --- docker -------------------------------------------------------------------------------------
#
# PacmanManager.RepoHost.Test starts Postgres and Keycloak with Testcontainers and builds the
# RepoHost and Migrations images. The daemon is installed but not started in a fresh container.
if docker info >/dev/null 2>&1; then
  log "docker daemon already running"
else
  log "starting the docker daemon"
  as_root sh -c 'nohup dockerd >/var/log/dockerd.log 2>&1 &'
  for _ in $(seq 1 30); do
    docker info >/dev/null 2>&1 && break
    sleep 1
  done
  docker info >/dev/null 2>&1 \
    && log "docker daemon up" \
    || log "docker daemon did not come up; see /var/log/dockerd.log (end to end tests will fail)"
fi

# --- environment --------------------------------------------------------------------------------
if [ -n "${CLAUDE_ENV_FILE:-}" ]; then
  {
    echo 'export DOTNET_CLI_TELEMETRY_OPTOUT=1'
    echo 'export DOTNET_NOLOGO=1'
  } >> "$CLAUDE_ENV_FILE"
fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

# --- nuget --------------------------------------------------------------------------------------
#
# Restoring here warms ~/.nuget for the whole session, and the container image is cached once the
# hook finishes, so later sessions start with it already populated.
cd "${CLAUDE_PROJECT_DIR:-$(dirname "$0")/../..}"
log "restoring the dotnet-ef local tool"
dotnet tool restore >&2 || log "dotnet tool restore failed"
log "restoring NuGet packages"
dotnet restore PacmanManager.sln >&2 || log "dotnet restore failed"

log "ready: .NET $(dotnet --version), $(pacman --version | grep -o 'Pacman v[^ ]*'), libalpm $(pacman --version | grep -o 'libalpm v[^ ]*' | cut -dv -f2)"
