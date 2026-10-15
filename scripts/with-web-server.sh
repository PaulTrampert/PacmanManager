#!/usr/bin/env bash
#
# Serves pacmanmanager-web, runs one command against it, and stops the server again. It exists so
# that a PR's screenshots can be retaken with one command that needs no background process, no
# polling and no `kill`, which is all an agent without a general shell -- the `@claude` workflow
# among them -- is allowed to run. See AGENTS.md, "Screenshots".
#
#   scripts/with-web-server.sh storybook node shots.cjs
#   scripts/with-web-server.sh preview node shots.cjs
#
#   storybook  Storybook's dev server, for a component's stories. `storybook dev` rather than a
#              static build, since only the dev server runs each story's `play` function.
#   preview    the app's production build under `vite preview`; the command answers the requests it
#              makes (`/userinfo` and the API) with Playwright's `page.route`.
#
# The command runs from the current directory with BASE_URL set to the server's address, and with
# NODE_PATH pointing at the web app's node_modules, so a Playwright script kept anywhere can
# `require("@playwright/test")`. Dependencies are installed first if the lockfile has changed since
# the last `npm ci`, and so is Playwright's Chromium if it is missing. The exit status is the
# command's.
set -euo pipefail

if [[ $# -lt 2 || ! $1 =~ ^(storybook|preview)$ ]]; then
  echo "usage: $0 storybook|preview <command> [args...]" >&2
  exit 2
fi

server="$1"
shift
web="$(cd "$(dirname "${BASH_SOURCE[0]}")/../pacmanmanager-web" && pwd)"

if [[ ! $web/node_modules/.package-lock.json -nt $web/package-lock.json ]]; then
  (cd "$web" && npm ci --no-audit --no-fund)
fi
(cd "$web" && npx playwright install chromium)

# A free port, so this can run beside a developer's own Storybook or dev server.
port="$(node -e 'const s = require("net").createServer().listen(0, () => { console.log(s.address().port); s.close(); });')"
log="$(mktemp)"

case $server in
  storybook)
    (cd "$web" && exec setsid npm run storybook -- --port "$port" --ci --no-open) > "$log" 2>&1 &
    ready="http://localhost:$port/index.json"
    ;;
  preview)
    (cd "$web" && npm run build && exec setsid npx vite preview --port "$port" --strictPort) > "$log" 2>&1 &
    ready="http://localhost:$port/"
    ;;
esac
pid=$!

# setsid puts the server in a process group of its own, so killing the group also stops the
# children npm and Storybook start.
stop() {
  kill -- "-$(ps -o pgid= "$pid" | tr -d ' ')" 2>/dev/null || kill "$pid" 2>/dev/null || true
  rm -f "$log"
}
trap stop EXIT

# The build and Storybook's first compile both take a while on a cold cache.
for _ in $(seq 300); do
  if curl -sf -o /dev/null "$ready"; then
    break
  fi
  if ! kill -0 "$pid" 2>/dev/null; then
    echo "error: the $server server exited before it answered:" >&2
    cat "$log" >&2
    exit 1
  fi
  sleep 1
done
if ! curl -sf -o /dev/null "$ready"; then
  echo "error: the $server server did not answer $ready within five minutes:" >&2
  cat "$log" >&2
  exit 1
fi

BASE_URL="http://localhost:$port" NODE_PATH="$web/node_modules" "$@"
