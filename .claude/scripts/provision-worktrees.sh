#!/usr/bin/env bash
#
# Provisions the worktrees for a `/implement-unblocked` batch, plus the browser lock its
# sub-agents share. See .claude/commands/implement-unblocked.md, step 4.
#
#   .claude/scripts/provision-worktrees.sh <base commit> <issue>=<branch>...
#
# e.g. .claude/scripts/provision-worktrees.sh 8812e0e 364=feature/364-configure-wolverine 365=bugfix/365-fix-it
#
# Each issue gets `.claude/worktrees/issue-<issue>` in the primary checkout, on a new branch
# `<branch>` from <base commit>. An issue whose directory or branch already exists is skipped,
# never reused or overwritten. Prints one line per issue, then the lock's path:
#
#   created <issue> <worktree path> <branch>
#   skipped <issue> <reason>
#   failed  <issue> <reason>
#   lock    <browser lock path>
#
# Exits non-zero if any issue failed or an argument was malformed; skipped issues do not count.
#
# It exists so that the fanning agent never writes this loop itself: the Bash tool's shell may be
# zsh, which does not word-split an unquoted `$var`, so a loop that splits "<issue> <branch>" pairs
# with `set -- $spec` gets an empty branch name and creates nothing. Every value here arrives as
# its own argument, and the script runs under bash whatever the caller's shell.
set -euo pipefail

if [[ $# -lt 2 ]]; then
  echo "usage: $0 <base commit> <issue>=<branch>..." >&2
  exit 2
fi

base="$1"
shift

common_dir="$(git rev-parse --path-format=absolute --git-common-dir)"
primary="$(dirname "$common_dir")"
worktrees="$primary/.claude/worktrees"

if ! git -C "$primary" rev-parse --verify --quiet "$base^{commit}" >/dev/null; then
  echo "error: base '$base' is not a commit" >&2
  exit 2
fi

status=0
for spec in "$@"; do
  issue="${spec%%=*}"
  branch="${spec#*=}"
  if [[ "$spec" != *=* || ! "$issue" =~ ^[0-9]+$ || -z "$branch" ]]; then
    echo "failed  $spec expected <issue>=<branch>"
    status=1
    continue
  fi
  path="$worktrees/issue-$issue"
  if [[ -e "$path" ]]; then
    echo "skipped $issue $path already exists"
  elif git -C "$primary" show-ref --verify --quiet "refs/heads/$branch"; then
    echo "skipped $issue branch $branch already exists"
  elif out="$(git -C "$primary" worktree add -q "$path" -b "$branch" "$base" 2>&1)"; then
    echo "created $issue $path $branch"
  else
    echo "failed  $issue ${out//$'\n'/ }"
    status=1
  fi
done

lock="$common_dir/browser-tests.lock"
flock "$lock" true
echo "lock    $lock"

exit "$status"
