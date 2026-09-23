#!/usr/bin/env bash
# Waits for something on a PacmanManager pull request that its sub-agent must act on, then prints
# it and exits. Used by the /implement-unblocked sub-agents (see .claude/commands/implement-unblocked.md).
#
# Usage: await-pr-activity.sh <pr number> <handled file> [max seconds] [poll seconds]
#
# <handled file> lists comment keys already addressed, one per line; it is created if missing.
# The script only reads it -- the agent appends a key once it has replied to that comment.
#
# Output, one of:
#   MERGED <login>            the PR was merged, by <login>
#   CLOSED                    the PR was closed without being merged
#   COMMENT <key> <url>       one line per unaddressed @claude comment by the gh user
#   TIMEOUT                   nothing happened within [max seconds] (default 540); call again
#
# A comment qualifies when its author is the account gh is logged in as, its body mentions
# @claude, and, for an inline review comment, its thread is not resolved (resolving a thread
# withdraws the request). Keys are "issue:<id>", "review:<id>" and "thread:<id>", where <id> is the
# comment's REST id, which is what the reply endpoints take.
set -euo pipefail

pr="${1:?pr number}"
handled="${2:?handled file}"
max="${3:-540}"
poll="${4:-60}"

touch "$handled"
me="$(gh api user --jq .login)"
deadline=$(( $(date +%s) + max ))

query='
query($n: Int!) {
  repository(owner: "PaulTrampert", name: "PacmanManager") {
    pullRequest(number: $n) {
      state
      mergedBy { login }
      comments(last: 100) { nodes { databaseId author { login } body url } }
      reviews(last: 100) { nodes { databaseId author { login } body url } }
      reviewThreads(last: 100) {
        nodes {
          isResolved
          comments(first: 100) { nodes { databaseId author { login } body url } }
        }
      }
    }
  }
}'

filter='
.data.repository.pullRequest as $pr
| if $pr.state == "MERGED" then "MERGED \($pr.mergedBy.login // "unknown")"
  elif $pr.state == "CLOSED" then "CLOSED"
  else
    ( ($pr.comments.nodes[] | {key: "issue:\(.databaseId)", c: .}),
      ($pr.reviews.nodes[] | {key: "review:\(.databaseId)", c: .}),
      ($pr.reviewThreads.nodes[] | select(.isResolved | not)
        | .comments.nodes[] | {key: "thread:\(.databaseId)", c: .}) )
    | select(.key | IN($handled[]) | not)
    | select(.c.author.login == $me)
    | select(.c.body | test("@claude\\b"; "i"))
    | "COMMENT \(.key) \(.c.url)"
  end'

while :; do
  # A transient API failure should not end the watch; it is retried on the next poll.
  if out="$(gh api graphql -f query="$query" -F n="$pr" 2>/dev/null \
      | jq -r --arg me "$me" --rawfile h "$handled" \
          '($h | split("\n") | map(select(length > 0))) as $handled | '"$filter")"; then
    if [[ -n "$out" ]]; then
      printf '%s\n' "$out"
      exit 0
    fi
  fi
  if (( $(date +%s) + poll > deadline )); then
    echo TIMEOUT
    exit 0
  fi
  sleep "$poll"
done
