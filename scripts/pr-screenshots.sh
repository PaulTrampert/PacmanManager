#!/usr/bin/env bash
#
# Adds screenshots to pull request <pr>'s `screenshots/pr-<pr>` branch and prints the Markdown that
# embeds each one in the PR description. See AGENTS.md, "Screenshots".
#
#   scripts/pr-screenshots.sh 212 after-signed-in.png after-session-menu.png
#
# Each file is stored under its base name, replacing an image of the same name, and every other image
# already on the branch is kept. The branch holds only images and has no history in common with
# main. It is written with git plumbing, so nothing is checked out and the working tree and index are
# untouched, and the push is a fast-forward, so earlier commits -- which the description may still
# link to -- stay reachable. The Markdown links by commit SHA rather than by branch name, so an image
# shows what it showed when it was linked, even after later updates.
set -euo pipefail

if [[ $# -lt 2 || ! $1 =~ ^[0-9]+$ ]]; then
  echo "usage: $0 <pr number> <image>..." >&2
  exit 2
fi

pr="$1"
shift
branch="screenshots/pr-$pr"
root="$(git -C "$(dirname "${BASH_SOURCE[0]}")" rev-parse --show-toplevel)"
git() { command git -C "$root" "$@"; }

# owner/name from the origin URL, in either its HTTPS or its SSH form.
origin="$(git remote get-url origin)"
slug="$(sed -E 's#^(https://[^/]+/|git@[^:]+:)##; s#\.git$##' <<< "$origin")"

declare -A blobs=()
names=()
for file in "$@"; do
  if [[ ! -f $file ]]; then
    echo "error: $file not found" >&2
    exit 1
  fi
  name="$(basename "$file")"
  if [[ -n ${blobs[$name]:-} ]]; then
    echo "error: two images are named $name" >&2
    exit 1
  fi
  blobs[$name]="$(git hash-object -w -- "$(realpath "$file")")"
  names+=("$name")
done

parent=()
if git fetch --quiet origin "refs/heads/$branch" 2>/dev/null; then
  head="$(git rev-parse FETCH_HEAD)"
  parent=(-p "$head")
fi

# --full-tree: without it ls-tree lists only the current directory's part of the tree.
tree="$(
  {
    if [[ ${#parent[@]} -gt 0 ]]; then
      git ls-tree --full-tree "$head" | while IFS=$'\t' read -r entry name; do
        [[ -n ${blobs[$name]:-} ]] || printf '%s\t%s\n' "$entry" "$name"
      done
    fi
    for name in "${names[@]}"; do
      printf '100644 blob %s\t%s\n' "${blobs[$name]}" "$name"
    done
  } | git mktree
)"

commit="$(git commit-tree "$tree" "${parent[@]}" -m "Screenshots for #$pr: ${names[*]}")"
git push --quiet origin "$commit:refs/heads/$branch"

echo "Pushed $commit to $branch. Embed with:"
for name in "${names[@]}"; do
  echo "![${name%.*}](https://raw.githubusercontent.com/$slug/$commit/$name)"
done
