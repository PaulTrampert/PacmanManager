---
description: Assign every unblocked, unassigned issue to the gh user, move it to In Progress, and implement each one as a PR from its own worktree via sub-agents.
argument-hint: "[issue numbers to restrict to] [--dry-run]"
allowed-tools: Bash(gh:*), Bash(git:*), Agent
---

# Implement unblocked issues

You are the **fanning agent** described under *Worktrees* in `AGENTS.md`: you provision one worktree
per issue up front, then hand each sub-agent a path that already exists. Sub-agents only ever work;
they never provision.

Arguments: `$ARGUMENTS`

* Any bare numbers restrict the run to those issues (they must still pass the filter in step 2).
* `--dry-run` stops after step 2: print the issues that would be picked up and change nothing.

The repository is `PaulTrampert/PacmanManager`.

## 1. Preflight

Stop and tell the user how to fix it if this fails. Do not work around a failure.

* `gh auth status` must list the `project` scope, which moving an issue on a project board needs.
  If it is missing, the fix is `gh auth refresh -s project` — ask the user to run it as
  `! gh auth refresh -s project`.

## 2. Find the issues

An issue qualifies when it is **open**, has **no assignees**, and has **no open blocking issue**
(GitHub issue dependencies — a blocker that is closed no longer blocks). Also skip any issue that
already has an open pull request that will close it, since someone is already on it.

```bash
gh api graphql --paginate -f query='
query($endCursor: String) {
  repository(owner: "PaulTrampert", name: "PacmanManager") {
    issues(states: OPEN, first: 100, after: $endCursor) {
      pageInfo { hasNextPage endCursor }
      nodes {
        number
        title
        labels(first: 20) { nodes { name } }
        assignees(first: 1) { totalCount }
        blockedBy(first: 50) { nodes { number state } }
        closedByPullRequestsReferences(first: 10, includeClosedPrs: false) { nodes { number state } }
      }
    }
  }
}' --jq '.data.repository.issues.nodes[]
  | select(.assignees.totalCount == 0)
  | select([.blockedBy.nodes[] | select(.state == "OPEN")] | length == 0)
  | select([.closedByPullRequestsReferences.nodes[] | select(.state == "OPEN")] | length == 0)
  | {number, title, labels: [.labels.nodes[].name]}'
```

Print the list (number and title). If it is empty, say so and stop. If `--dry-run` was given, stop
here.

## 3. Claim them

For each issue:

1. Assign it to the user driving this session — the account `gh` is logged in as:
   `gh issue edit <n> --repo PaulTrampert/PacmanManager --add-assignee @me`.
2. Move it to **In Progress** on every project board it belongs to. Look up its project items and
   each project's `Status` field:

   ```bash
   gh api graphql -f query='
   query($n: Int!) {
     repository(owner: "PaulTrampert", name: "PacmanManager") {
       issue(number: $n) {
         projectItems(first: 10) {
           nodes {
             id
             project {
               id
               title
               field(name: "Status") {
                 ... on ProjectV2SingleSelectField { id options { id name } }
               }
             }
           }
         }
       }
     }
   }' -F n=<n>
   ```

   Then set the option whose name is `In Progress` (compare case-insensitively):

   ```bash
   gh project item-edit --id <item id> --project-id <project id> \
     --field-id <field id> --single-select-option-id <option id>
   ```

   If the issue is on no project, or a project has no `In Progress` option, note it for the final
   report and carry on — do not add the issue to a project or invent a status.

Claim every issue before starting any implementation, so the board reflects the whole batch at once.

## 4. Provision the worktrees

Worktrees live beside the primary checkout, under `../PacmanManager-worktrees/`. Resolve the primary
checkout from the shared git directory so this works whichever worktree you were started in:

```bash
PRIMARY="$(dirname "$(git rev-parse --path-format=absolute --git-common-dir)")"
WORKTREES="$(dirname "$PRIMARY")/PacmanManager-worktrees"
git -C "$PRIMARY" fetch origin main
```

For each issue, pick a branch name: `bugfix/<n>-<slug>` if the issue carries the `bug` label,
otherwise `feature/<n>-<slug>`, where `<slug>` is the title lower-cased, reduced to `[a-z0-9-]`, and
cut to a few words. Then:

```bash
git -C "$PRIMARY" worktree add "$WORKTREES/issue-<n>" -b <branch> origin/main
```

If the branch or the directory already exists, do not reuse or overwrite it — skip that issue and
report it. Never switch the branch of an existing worktree.

## 5. Implement, one sub-agent per issue

Launch every sub-agent **in a single message** so they run concurrently. Use the `general-purpose`
agent type, do **not** pass `isolation` (the worktree already exists), and give each one this
prompt, filled in:

> You are implementing GitHub issue #<n> ("<title>") in `PaulTrampert/PacmanManager`.
>
> Work only in the worktree at `<absolute worktree path>`, which is already checked out on branch
> `<branch>` from `origin/main`. Use absolute paths or `git -C` for everything. Do not create another
> worktree and do not switch branches — if `git branch --show-current` there is not `<branch>`, stop
> and report that.
>
> 1. Read `AGENTS.md` in the worktree and follow it; it is the project's coding standard, test policy
>    and PR convention.
> 2. Read the issue in full: `gh issue view <n> --repo PaulTrampert/PacmanManager --comments`. If it
>    points at a design document under `docs/`, read that too. A design document on `main` is
>    settled: implement it as written. If the issue cannot be delivered as written, or needs a
>    decision the issue and design doc do not make, stop and report why instead of guessing — do not
>    deviate from the design.
> 3. Implement the issue, with the unit tests `AGENTS.md` requires, and an E2E test for any new or
>    changed endpoint. Keep the change scoped to this issue.
> 4. `dotnet build` must pass with no new warnings. Run the unit tests for the projects you touched
>    with `dotnet test <project> --filter ...`. Run E2E fixtures only if you added or changed one,
>    and only those fixtures — other agents are building Docker images at the same time.
> 5. Commit in meaningful steps. End each commit message with
>    `Co-Authored-By: Claude <noreply@anthropic.com>`.
> 6. Push with `git -C <path> push -u origin <branch>` and open a PR against `main` with
>    `gh pr create`. The title starts with `(PATCH)`, `(MINOR)` or `(MAJOR)` per `AGENTS.md`; the body
>    starts with `Fixes #<n>`, explains what the diff does not make obvious, and ends with
>    `🤖 Generated with [Claude Code](https://claude.com/claude-code)`.
>
> Finish with a short report: the PR URL (or why there is none), what you tested and how, and
> anything the reviewer should look at first.

Replace `Claude` in the co-author line with the attribution your own system prompt specifies, if it
gives one.

## 6. Report

When every sub-agent has finished, give the user one table: issue, branch, PR link (or the reason
there is none), and any issue that could not be moved to *In Progress*. Leave the worktrees in place
— they hold the branches under review — and give the command to remove one once its PR merges:
`git worktree remove <path>`.

Do not unassign an issue or move it back on the board when its sub-agent fails; report the failure
and leave the decision to the user.
