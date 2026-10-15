#!/usr/bin/env bash
# Decides which pipelines a change needs, from the paths it touches.
#
# Reads changed paths, one per line, on stdin and prints `dotnet=`, `web=`, `docker=` and `e2e=`
# lines (each `true` or `false`) in the form `$GITHUB_OUTPUT` takes. Each path falls into the first
# category that matches it:
#
#   shared   the API contract, the compose stack, Keycloak, CI itself and the tooling and fixtures
#            the builds read: everything runs
#   docs     documentation and editor or agent configuration: nothing runs
#   dotnet   the solution and its projects: the .NET pipeline runs
#   web      pacmanmanager-web: the web pipeline runs
#
# A path that matches no category counts as shared, so a new top-level file runs everything until
# somebody decides it needs less. The images and the end-to-end tests run whenever anything but
# documentation changed.
set -euo pipefail

dotnet=false
web=false
everything=false

while IFS= read -r path; do
  [[ -z "$path" ]] && continue
  # `*` in a case pattern also matches `/`, so `docs/*` covers everything under docs.
  case "$path" in
    compose.yaml | keycloak/* | pacmanmanager-web/openapi/* | test-fixtures/* | scripts/* | \
      .github/* | .config/* | .dockerignore | global.json | nuget.config | Directory.*)
      everything=true ;;
    docs/* | *.md | .claude/* | .run/* | .idea/* | .gitignore | LICENSE*)
      ;;
    *.sln | LibAlpmSharp*/* | PacmanManager.*/*)
      dotnet=true ;;
    pacmanmanager-web/*)
      web=true ;;
    *)
      everything=true ;;
  esac
done

if [[ "$everything" == true ]]; then
  dotnet=true
  web=true
fi

images=false
if [[ "$dotnet" == true || "$web" == true ]]; then
  images=true
fi

echo "dotnet=$dotnet"
echo "web=$web"
echo "docker=$images"
echo "e2e=$images"
