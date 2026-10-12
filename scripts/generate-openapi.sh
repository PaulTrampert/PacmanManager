#!/usr/bin/env bash
#
# Regenerates pacmanmanager-web/openapi/v1.json, RepoHost's v1 OpenAPI document, with the
# Swashbuckle CLI from the tool manifest. Build RepoHost in the same configuration first:
#
#   dotnet tool restore
#   dotnet build PacmanManager.RepoHost
#   scripts/generate-openapi.sh            # Debug
#   scripts/generate-openapi.sh Release
#
# The environment must be Development: the document embeds Swagger:OpenIdConnectUrl, which only
# appsettings.Development.json sets. The content root must be RepoHost's output directory, which is
# where the appsettings files are copied; the CLI otherwise takes the current directory and finds
# none. OpenApiDocumentTests generates the document the same way.
set -euo pipefail

configuration="${1:-Debug}"
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
bin="$root/PacmanManager.RepoHost/bin/$configuration/net10.0"
dll="$bin/PacmanManager.RepoHost.dll"

if [[ ! -f "$dll" ]]; then
  echo "error: $dll not found; run 'dotnet build PacmanManager.RepoHost -c $configuration' first" >&2
  exit 1
fi

mkdir -p "$root/pacmanmanager-web/openapi"
cd "$root"
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_CONTENTROOT="$bin" \
  dotnet swagger tofile --output "$root/pacmanmanager-web/openapi/v1.json" "$dll" v1
