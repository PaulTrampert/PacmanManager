#!/usr/bin/env bash
# Waits for something on a PacmanManager pull request that its sub-agent must act on, then prints
# it and exits. Used by the /implement-unblocked sub-agents (see .claude/commands/implement-unblocked.md).
#
# Usage: await-pr-activity.sh <pr number> <handled file> [max seconds] [poll seconds]
#
# <handled file> lists keys already dealt with, one per line; it is created if missing. The script
# only reads it -- the agent appends a key once it has acted on it.
#
# Output is MERGED, CLOSED or TIMEOUT alone, or else one or more of CONFLICTS, CHECKS and COMMENT:
#   MERGED <login>            the PR was merged, by <login>
#   CLOSED                    the PR was closed without being merged
#   CONFLICTS <key>           the PR conflicts with its base branch
#   CHECKS <key> <names>      checks failed on the PR's head commit; <names> is a ", "-joined list
#   COMMENT <key> <url>       one line per unaddressed @claude comment by the gh user
#   TIMEOUT                   nothing happened within [max seconds] (default 540); call again
#
# The CONFLICTS key is "conflicts:<head sha>:<base sha>", so a conflict the agent could not resolve
# and recorded as handled is not reported again until either branch moves. GitHub reports
# mergeability as UNKNOWN while it recomputes after a push; only a definite CONFLICTING counts.
#
# The CHECKS key is "checks:<head sha>", reported only once no check on that commit is still queued
# or running, so the agent sees every failure of a run at once. A new push is a new key. A check
# that someone cancelled or that was skipped is not a failure.
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
      mergeable
      headRefOid
      baseRef { target { oid } }
      commits(last: 1) {
        nodes {
          commit {
            statusCheckRollup {
              contexts(first: 100) {
                nodes {
                  __typename
                  ... on CheckRun { name status conclusion }
                  ... on StatusContext { context state }
                }
              }
            }
          }
        }
      }
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
    ( select($pr.mergeable == "CONFLICTING")
      | "conflicts:\($pr.headRefOid):\($pr.baseRef.target.oid)"
      | select(IN($handled[]) | not)
      | "CONFLICTS \(.)" ),
    ( [$pr.commits.nodes[0].commit.statusCheckRollup.contexts.nodes[]?
        | if .__typename == "CheckRun"
          then {name, done: (.status == "COMPLETED"),
                failed: (.conclusion | IN("FAILURE", "TIMED_OUT", "STARTUP_FAILURE", "ACTION_REQUIRED"))}
          else {name: .context, done: (.state | IN("PENDING", "EXPECTED") | not),
                failed: (.state | IN("FAILURE", "ERROR"))}
          end ] as $checks
      | select($checks | length > 0 and all(.done) and any(.failed))
      | "checks:\($pr.headRefOid)"
      | select(IN($handled[]) | not)
      | "CHECKS \(.) \([$checks[] | select(.failed) | .name] | join(", "))" ),
    ( ( ($pr.comments.nodes[] | {key: "issue:\(.databaseId)", c: .}),
        ($pr.reviews.nodes[] | {key: "review:\(.databaseId)", c: .}),
        ($pr.reviewThreads.nodes[] | select(.isResolved | not)
          | .comments.nodes[] | {key: "thread:\(.databaseId)", c: .}) )
      | select(.key | IN($handled[]) | not)
      | select(.c.author.login == $me)
      | select(.c.body | test("@claude\\b"; "i"))
      | "COMMENT \(.key) \(.c.url)" )
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
