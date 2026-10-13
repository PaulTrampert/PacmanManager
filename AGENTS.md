# AGENTS.md

Guidance for coding agents working in this repository. `CLAUDE.md` is a symlink to this
file, so Claude Code and any other agent read the same instructions.

## What this is

A self-hosted **private pacman (Arch Linux) repository host**. `PacmanManager.RepoHost` is an
ASP.NET Core API that lets authenticated users create and manage pacman repositories; repository
database files (`.db.tar.gz`) live under `DATA_DIR` (default `/data`) and are manipulated with
`repo-add`. `LibAlpmSharp` is a from-scratch P/Invoke binding and `pacman.conf` parser (ANTLR
grammar) over libalpm. `pacmanmanager-web` is the web front end: a React app, built with Vite, that
`docs/web-ui-architecture/web-ui-architecture.md` describes.

Target framework is **net10.0** across every .NET project. Nullable reference types and implicit usings
are enabled everywhere.

## Build and test commands

```bash
dotnet build                                    # build all; also serves as typecheck/lint
dotnet test                                     # run all tests
dotnet test PacmanManager.RepoHost.Test         # one test project
dotnet test --filter "FullyQualifiedName=Namespace.ClassName"             # one fixture
dotnet test --filter "FullyQualifiedName=Namespace.ClassName.MethodName"  # one test
```

There is no separate lint or typecheck step — `dotnet build` is both.

The web app is an npm project and has its own commands, run from `pacmanmanager-web/` with the Node
LTS (`.nvmrc` is `lts/*`):

```bash
npm ci                     # install exactly what package-lock.json pins; all a fresh clone needs
npm run compose-up         # docker compose up -d --build for the whole stack, from the repository root
npm run dev                # compose-up, then the Vite dev server on port 3000, proxying the web host's paths to :8081
npm run format:check       # Prettier (`npm run format` rewrites)
npm run lint               # ESLint
npm run typecheck          # tsc
npm test                   # Vitest, once: the unit project (jsdom) and the storybook project (Chromium)
npm run test:watch         # the same, watching
npm run build              # production bundle in dist/
npm run storybook          # Storybook on port 6006 (`npm run build-storybook` builds it)
npm run e2e                # Playwright against the compose stack: compose-up, then the tests in e2e/
npx playwright install chromium   # once, for the storybook project's and the e2e tests' browser
```

`npm run e2e` runs `compose-up` first, so the stack is built from the current sources; Playwright
never starts it, and its global setup only waits up to two minutes for `http://localhost:8081/` and
Keycloak's discovery document to answer. Tests sign in through the real Keycloak form with `signIn`
from `e2e/fixtures.ts`, as the realm's `test` and `other` users, and share one stack and database,
so each creates what it needs under a unique name. Playwright tests are `e2e/**/*.spec.ts`; the
`e2e/**/*.test.ts` beside them are Vitest unit tests of the harness's own helpers.

`npm run generate` writes the gitignored `*.gen.ts` files: the TanStack Router route tree, and
`src/api/schema.gen.ts`, the API types `openapi-typescript` generates from `openapi/v1.json`.
`dev`, `build`, `typecheck`, `lint`, `test` and `storybook` run it first, so nothing generated is ever
committed or needed before them. The types are generated for the paths the API client is called
with, under its `/api/pacmanmanager` base URL: the document's `/api/v1/Repositories` is the client's
`/v1/Repositories`. Call the API only through `client` and `unwrap` from `src/api`, and mock it in
tests and stories with `apiHandler` from `src/mocks/api.ts`, which is typed from the same schema.

**Note on `dotnet test`:** `PacmanManager.RepoHost.Test` contains Testcontainers-based E2E tests
that build the RepoHost and Migrations Docker images and start Postgres and Keycloak containers.
A full `dotnet test` therefore needs a working Docker daemon and takes minutes. When iterating on
non-E2E logic, filter down to the unit fixtures instead.

**Note on non-Arch hosts:** `LibAlpmSharp.Test` runs anywhere libalpm is installed. The fixtures
that need installed packages seed their own local database under a temporary root
(`PacmanManager.TestUtils.LocalPackageDatabase`) and point `LibAlpm.Initialize(root, dbPath)` at it,
rather than reading the host's own database at `/var/lib/pacman`. The one exception is
`LibAlpmTests.Initialize_WithDefaultPaths_CreatesInstance`, whose subject *is* the default paths: it
warns and stops where `/var/lib/pacman` is not a pacman database the current user may open.
`docs/cloud-environment.md` covers running the repository from a claude.ai cloud session.

## Running the stack

```bash
docker compose up -d --build       # full stack
docker compose up -d postgres      # database only (matches the Rider "Start Database Only" config)
```

Services in `compose.yaml`:

| Service | Notes |
| :--- | :--- |
| `pacmanmanager.repohost` | The API. Host port **8082** → container 8080. Built from an `archlinux` base image (it needs pacman tooling), not the Microsoft runtime image. Runs in `Development`, after the migrations complete. |
| `pacmanmanager.web` | The web app in SpaHost. Host port **8081** → container 80. Signs users in as Keycloak's `pacman-manager-web` client and proxies `/api/pacmanmanager/…` to RepoHost's `/api/…` with their access token. Built with `pacmanmanager-web` as its context. |
| `auth` | Keycloak on port **8080**, importing the `localdev` realm from `keycloak/localdev.json`. |
| `pacmanmanager.migrations` | Runs EF migrations to completion and exits. |
| `postgres` | Postgres 18, port **5432**, user/password `pacmanmanager`/`password`. |

A service built from this repository is named `pacmanmanager.<component>`; an external dependency
keeps a plain role name (`auth`, `postgres`). Follow that when adding a service.

Swagger UI is enabled in Development (or when `Swagger:EnableSwaggerUI` is set) and is wired for
OAuth against Keycloak with PKCE.

## Project layout

| Project | Responsibility |
| :--- | :--- |
| `LibAlpmSharp` | libalpm interop plus `pacman.conf` reader/serializer. Uses `AllowUnsafeBlocks` and ANTLR (`**/*.g4`). |
| `PacmanManager.CliTools` | Generic runner for external command line tools. |
| `PacmanManager.AurClient` | Client for the Arch User Repository. |
| `PacmanManager.Entities` | EF Core entities and `PacmanManagerDbContext`. Configuration is by data annotations; there is no `OnModelCreating`. |
| `PacmanManager.Migrations` | EF migrations plus a console host that applies them (with retries) on startup. |
| `PacmanManager.RepoHost` | The API: controllers, services, authentication, authorization. |
| `PacmanManager.TestUtils` | Shared test helpers (`DirUtils.FindSolutionDirectory`, `TestOutputLogger`, wait strategies). |
| `*.Test` | One test project per production project. |
| `pacmanmanager-web` | The web front end: an npm project beside the solution, **not** in `PacmanManager.sln`. Its layout is set out in `docs/web-ui-architecture/web-ui-architecture.md`. |

Adding a .NET project means adding it to `PacmanManager.sln` (and to the relevant `Dockerfile` `COPY`
lines if it becomes a build dependency of RepoHost or Migrations).

## Architecture notes worth knowing before changing things

### Authorization lives in the service layer, not in filters

Read `docs/authorization-plan.md` before touching anything access-control related. The short
version:

* `RepositoryService` enforces visibility and ownership itself, so CLI tools and background jobs
  get the same rules as HTTP callers. An action filter was deliberately rejected for this reason.
* The identity a unit of work runs as is an `Actor`, supplied by `IActorAccessor`.
  `HttpContextActorAccessor` is registered by the web host; `FixedActorAccessor` is for tools and
  tests. **Nothing is registered by default** — a host that forgets to choose fails at DI
  resolution rather than silently running anonymous.
* `RepositoryAccessPolicy` is the single definition of the rules and has no DB/HTTP/logging
  dependencies, so each rule is a plain unit test in `RepositoryAccessPolicyTests`.
* `RepositoryService` touches `DbContext.PacmanRepositories` in exactly one private method
  (`VisibleAsync`), which applies the visibility predicate. Any new query must go through it;
  `RepositoryServiceEnforcementTests` asserts this.
* Services throw `NoCurrentUserException` / `RepositoryForbiddenException`;
  `AuthorizationExceptionHandler` maps them to 401/403. A repository the caller may not see is
  reported as **404**, to hide its existence.

### API conventions

* Versioned by namespace: `Controllers/v1/...` maps to `/api/v1/...` via
  `VersionByNamespaceConvention`.
* JSON is camelCase, nulls omitted, enums serialized as strings.
* Listing endpoints separate three concerns: `PaginationParams` (`offset`, `pageSize`), a filter
  query object (`PTrampert.QueryObjects` attributes), and `SortOptions<TSortField>`
  (`sortBy`, `direction`). Filters are ANDed onto the already-visible query and can never widen
  what a caller sees. The first member of a sort-field enum is the default.
* Controllers are thin: they log, call the service, and translate `null` to `NotFound()`.

### The OpenAPI document

`pacmanmanager-web/openapi/v1.json` is RepoHost's `v1` OpenAPI document, the contract the web app
is built against. It is generated, never hand-edited. Regenerate it from the repository root:

```bash
dotnet tool restore                     # once; provides the Swashbuckle CLI
dotnet build PacmanManager.RepoHost
scripts/generate-openapi.sh             # or: scripts/generate-openapi.sh Release
```

`OpenApiDocumentTests` generates the document the same way and fails when the committed one has a
**breaking** difference from it, as `oasdiff` judges it at its `ERR` level; additions pass without
regenerating. A change that breaks the contract regenerates the document in the same PR.
Generation runs the whole of `Program.cs` against a server that never listens, but without hosted
services, so startup work that needs `DATA_DIR` or the database belongs in an `IHostedService`
(like `PacmanDirectoriesInitializer`), never after `builder.Build()`. See
`docs/web-ui-architecture/web-ui-architecture.md`.

### Database and migrations

Migrations live in `PacmanManager.Migrations`, which is both the migrations assembly and its own
startup project:

```bash
dotnet tool restore   # once; provides dotnet-ef
dotnet ef migrations add <Name> --project PacmanManager.Migrations --startup-project PacmanManager.Migrations
dotnet ef migrations list --project PacmanManager.Migrations --startup-project PacmanManager.Migrations --no-connect
```

Existing migration names follow a `<Verb>_<Object>` convention: `AddTable_Users`,
`AddColumn_PacmanRepository_IsPublic`, `AddIndex_IX_UserMappings_ExternalAuthority_ExternalId`.
Follow it.

The migrations host reads `ConnectionStrings:pacmanmanager` and optional `TargetMigration`,
`MaxRetries`, `RetryDelaySeconds` from configuration, retries on failure, and exits non-zero if it
never succeeds.

## Coding standards

These come from `CONTRIBUTING.md`; the highlights that most often apply:

* **Document every public member** with XML doc comments (`///`). `GenerateDocumentationFile` is on
  for RepoHost, so missing docs surface as warnings.
* **Interface-driven**: new services get an interface (`IRepositoryService`, `IAurClient`) so they
  can be mocked and injected.
* **Specific exceptions**, never bare `Exception` — e.g. `AurServerException`, `ItemExistsException`.
* Validation limits are shared constants in `PacmanManager.Entities` (e.g.
  `PacmanRepositoryValidationConstants.NameMaxLength`), not literals at the use site.
* Check existing `.csproj` files before adding a NuGet package; prefer what is already in use.
* New infrastructure dependencies must be added to `compose.yaml`.
* **Components render; hooks behave.** In `pacmanmanager-web`, a component never calls `useEffect`
  directly and holds at most one `useState` of a small value (not a large state object). Its
  behaviour lives in a `use<Component>` hook in the component's folder, not exported from
  `index.ts`, and the component renders what that hook returns. Behaviour that isn't specific to
  the component is a reusable hook of its own (`src/useTemporaryFeedback.ts`), which the
  component's hook uses.

### Testing expectations

* Every new service or logic change needs unit tests in the matching `.Test` project.
* Every new or modified API endpoint needs an E2E test.
* Test stack: **NUnit** (globally imported via `GlobalUsings.cs` / `<Using Include="NUnit.Framework" />`),
  **Moq**, **Testcontainers**, and `Microsoft.EntityFrameworkCore.InMemory` for service-level tests.
* `InternalsVisibleTo` exposes RepoHost internals to `PacmanManager.RepoHost.Test`.
* In `pacmanmanager-web`, logic gets a Vitest unit test beside it (`Name.test.ts(x)`), a
  component's behaviour hook included (`useName.test.ts`, with `renderHook`), and every
  component in `src/components` a story per meaningful state (`Name.stories.tsx`), with a `play`
  function for each interaction. Stories run as tests under `npm test`. Unit tests and stories talk
  to MSW handlers from `src/mocks`, and a request no handler matches fails the test.
* A PR is expected to have a passing `dotnet build` and `dotnet test`.

### Wireframes

Wireframes and mockups of the web UI are drawn in Penpot. **Their font is Roboto unless the design
document says otherwise**, even where an exported image shows something else. Penpot's exporter
currently renders every web font in a serif fallback, so an exported wireframe's typeface is never
evidence of the intended one; its layout, sizes, weights and colours still are.

## Continuous integration

`.github/workflows/ci.yml` runs on every pull request to `main` and on every push to `main`. It is
a gate, not a release pipeline — the project is unreleased and **nothing is published**.

| Job | What it does |
| :--- | :--- |
| `test` | Installs pacman tooling and libalpm on the Ubuntu runner, then `dotnet build`, `dotnet tool restore` and `dotnet test` across the solution. |
| `libalpm` | Runs `LibAlpmSharp.Test` inside an `archlinux/archlinux` container, against a real pacman installation and a newer libalpm. |
| `web` | Sets up the Node LTS from `pacmanmanager-web/.nvmrc`, then in `pacmanmanager-web`: `npm ci`, installs Chromium, and runs `format:check`, `lint`, `typecheck`, `test`, `build` and `build-storybook`. |
| `e2e` | In `pacmanmanager-web`: `npm ci`, installs Chromium, and runs `npm run e2e` against the compose stack it brings up. On failure it prints `docker compose logs` and uploads them with the Playwright report and traces as the `e2e-report` artifact; `docker compose down -v` always runs. |
| `docker` | Builds the RepoHost, Migrations and web images from their Dockerfiles with `push: false`, as a sanity check that they still build. The web image's context is `pacmanmanager-web`; the other two use the repository root. |

The `test` job publishes its `.trx` files with
[`EnricoMi/publish-unit-test-result-action`](https://github.com/EnricoMi/publish-unit-test-result-action):
one PR comment, updated in place on each later run, plus a **Test results** check run carrying the
per-test detail. Read that before the raw log. It is skipped for pull requests from forks, which
run with a read-only token; the full results are still uploaded as the `test-results` artifact
either way.

`.github/workflows/pr-title.yml` is separate, and enforces the `(MAJOR)`/`(MINOR)`/`(PATCH)` prefix
from [Branches, commits, and PRs](#branches-commits-and-prs). It does not check the title itself --
it calls the shared
[`check-pr-title.yml`](https://github.com/PaulTrampert/github-workflows/blob/main/.github/workflows/check-pr-title.yml),
so the rule and its wording stay the same across repositories. The pattern it matches lives in
`.github/pr-title-checker-config.json`, which `thehanimo/pr-title-checker` reads from the repository
at the event SHA -- the merge commit on a pull request, so a change to it takes effect on the PR
that makes it. It is its own workflow so that it can trigger on `edited`: a mistyped prefix is fixed
by editing the title, and the check re-runs on its own rather than needing an empty commit. Putting
`edited` on `ci.yml` would re-run the whole test and image-build matrix every time somebody touched
a title or description.

The runner is Ubuntu, and the whole solution runs on it unfiltered:

```bash
dotnet test PacmanManager.sln --configuration Release --no-build
```

The E2E fixtures build their images with the **solution root as the Dockerfile directory**, passing
the project directory as part of the Dockerfile path (`.WithDockerfile("PacmanManager.RepoHost/Dockerfile")`).
Keep it that way: Testcontainers looks for the ignore file next to the Dockerfile directory
(`<dockerfile>.dockerignore`, falling back to `.dockerignore` there) and never reads the context
root's, so pointing the Dockerfile directory at a project silently skips the root `.dockerignore`.
The host's `bin/` and `obj/` are then tarred into the build context, and `obj/` records absolute
host paths -- the sources ANTLR generates for `LibAlpmSharp` among them -- so `dotnet build` inside
the image fails with `CS2001: Source file ... could not be found`. It bites only after a local build
has populated `obj/`, which is always true in CI, and only for the RepoHost image, since Migrations
does not reference `LibAlpmSharp`.

Everything else runs, the E2E fixtures included — the runner's Docker daemon is what Testcontainers
starts Postgres and Keycloak on.

The **`libalpm`** job runs the same `LibAlpmSharp.Test` suite inside an `archlinux/archlinux`
container, installing `dotnet-sdk` and a JRE with `pacman`. Both jobs run all of it, and the overlap
is the point: the binding is exercised against a distribution that only carries libalpm (Ubuntu's
13.x) and against a real Arch system (pacman 7's 15.x), which is what proves the seeded local
database is read the same way by both.

### Code scanning (`codeql.yml`)

`.github/workflows/codeql.yml` runs CodeQL over `actions` and `csharp` on pull requests, pushes to
`main` and a weekly schedule. It is an advanced setup rather than GitHub's default setup only
because default setup cannot exclude individual queries; the exclusions live in
`.github/codeql/codeql-config.yml`, each with the reason it is excluded. Currently that is
`cs/log-forging`, since logging uses structured message templates. Default setup must stay off in
the repository settings, or this workflow's uploads are rejected.

### Screenshot branch cleanup (`screenshots-cleanup.yml`)

`.github/workflows/screenshots-cleanup.yml` runs when a pull request closes, merged or not, and
deletes its `screenshots/pr-<n>` branch from [Screenshots](#screenshots), so nobody deletes it by
hand. A PR without one passes quietly; any other failure to look the branch up fails the job. Its
token has only `contents: write`. It triggers on `pull_request_target` rather than `pull_request`
so that it also works for PRs from forks, whose `pull_request` runs get a read-only token; that is
safe because it never checks out or runs anything from the PR, only reads its number.

### Claude on pull requests (`claude.yml`)

`.github/workflows/claude.yml` runs
[`anthropics/claude-code-action`](https://github.com/anthropics/claude-code-action) when the
repository owner mentions `@claude` on a pull request, whether in a PR comment, an inline review
comment or a review body. Claude answers, or makes the change on the PR branch and pushes it. Each
run starts fresh and reads the PR for its context; this file is its `CLAUDE.md`, so the rules here
bind it too. Mentions from anyone else are ignored, and so are Claude's own replies, which are
posted as `claude[bot]`.

A review is answered as a whole. Submitting one fires an event per inline comment and one for the
review itself, and runs on a PR are serialized with only one allowed to wait, so a run per comment
would see all but two cancelled unanswered. A small `review` job therefore counts the review's
`@claude` comments first. A review with a single request, such as a lone comment or a reply in a
thread, is answered from its comment event, so the reply lands in that thread. A review with several
requests, or with `@claude` in its body, is answered by one run from the review event, which is
handed every request as its `prompt` and reports on each in one progress comment on the PR.

It needs two things set up once, which every Claude workflow here shares:

* the [Claude GitHub App](https://github.com/apps/claude) installed on the repository. The action
  exchanges the job's OIDC token for the App's token, and pushes made with it trigger CI, where
  pushes made with the workflow's own `GITHUB_TOKEN` would not;
* a `CLAUDE_CODE_OAUTH_TOKEN` repository secret, generated with `claude setup-token`.

Claude may run `dotnet`, `git`, `npm`, `npx`, `node`, `scripts/pr-screenshots.sh`, read-only `gh`
commands and `gh pr edit`; `gh pr merge` is denied outright. The last three exist for
[Screenshots](#screenshots): a change that alters the UI retakes the PR's screenshots with a
Playwright script, stores them with the script and links them from the description.

### Claude fixes failing CI (`claude-fix-ci.yml`)

`.github/workflows/claude-fix-ci.yml` runs when `CI` or `PR Title` fails on an open pull request
labelled **`claude-autofix`**. The label is the opt-in: `/implement-unblocked` adds it to the PRs it
opens, and a PR without it is left alone. Claude reads the failed run, fixes the cause on the PR
branch and pushes, reruns a failure unrelated to the change once, or explains in a PR comment what
it could not fix. It may not skip or loosen tests, suppress warnings or edit workflows.

It attempts each failure at most once. A fix commit carries a `Claude-Autofix: ci` trailer and is
never fixed again. Once Claude's step completes, the workflow's progress comment gains a hidden
marker naming the failed workflow and commit, and a later run that finds that marker stops; that
stops reruns and title edits from retrying the same commit. An attempt that dies before Claude
finishes is reported in a new PR comment and not counted, so rerunning the failed workflow tries
again. It needs the same one-time setup as `claude.yml`.

### Claude resolves merge conflicts (`claude-resolve-conflicts.yml`)

`.github/workflows/claude-resolve-conflicts.yml` runs when `CI` finishes on a push to `main` (the
action does not accept `push` events itself), and when a pull request is labelled `claude-autofix`. It finds the open PRs carrying that label that conflict with
`main`, and runs Claude on each: it merges `main` into the branch (never rebasing, which would need a
force-push and would orphan review comments), resolves the conflicts so both sides' intent survives,
builds, runs the affected unit tests, pushes, and comments with a summary. A conflict that needs a
decision is aborted and explained in a PR comment instead. Each pair of branch and `main` commits gets
one *finished* attempt: the workflow's progress comment gains a hidden marker only when Claude's step
completes, so an unresolved conflict is retried only once `main` moves. An attempt that dies before
then -- a failed setup step, a timeout, a cancellation -- is reported in a new PR comment and not
counted, so the next trigger tries again. It needs the same one-time setup as
`claude.yml`.

## Version control

### Worktrees

Several agents are often working on this repository at the same time, and a shared working tree
means conflicting edits, a shared index, and branch switches that pull the ground out from under
another agent's build. Every unit of work therefore gets its own worktree — but *who* creates it
depends on what kind of agent you are.

**If you are a top-level agent** (a session the user drives directly), create your own worktree
before making any edits — `git worktree add`, or the `EnterWorktree` tool if your harness provides
one — and work there rather than in the primary checkout.

**Worktrees live under `.claude/worktrees/` inside the primary checkout**, never beside it:
`git worktree add .claude/worktrees/<name> -b <branch> origin/main`. `EnterWorktree` and sub-agent
isolation already put them there. The directory is ignored by git and by Docker, so a nested
worktree neither shows as untracked nor bloats the E2E image build context. It is also inside the
directory a sandboxed agent may write to, where a sibling directory is not. Remove a worktree with
`git worktree remove` once its branch is merged.

**If you are a sub-agent**, you almost certainly cannot. A sub-agent spawned from a session that is
itself worktree-isolated inherits the parent's pin: `git worktree add` will appear to succeed, but
every later git operation targeting the new directory is refused, and `git switch` /
`git checkout -b` are blocked outright. So work in the worktree you were handed and do not try to
provision another. If you were not handed one and genuinely need a separate checkout, the only
approach that works from a pinned session is a worktree nested *inside* the pinned one; treat that
as a workaround and remove it once your work is pushed.

**Never switch the branch of a worktree you did not create.** This is the rule that actually
protects a concurrent agent: checking your own branch out in someone else's worktree moves their
files and their index out from under them mid-build. If the worktree you are in is on a branch that
does not match your task, say so and stop rather than switching it. (This is also why a worktree's
directory name is not authoritative — check `git branch --show-current`, not the path.)

**When fanning work out across several issues**, the agent doing the fanning creates the worktrees
up front, one per issue, each already checked out on the right branch, and hands each sub-agent a
path that already exists. That way a sub-agent only ever works; it never has to provision.

### Branches, commits, and PRs

* **Never commit directly to `main`.** Branch as `feature/...` or `bugfix/...`.
* **An agent never merges a pull request on its own initiative**, only when the user directly asks
  it to. Finished work, green CI or an approving review does not count as being asked.
* Commit early and often — a meaningful change that builds is a good commit point.
* PR titles start with a parenthesised change level -- `(PATCH) ...`, `(MINOR) ...`, `(MAJOR) ...` --
  depending on the nature of the change. The parentheses matter: that is the form the shared
  release tooling reads, and the form the title check enforces.
* PR descriptions reference the issue they fix (`Fixes #123`) and explain what the diff does not
  make obvious.
* A PR that changes the UI carries screenshots in its description; see [Screenshots](#screenshots).
* GitHub repo: <https://github.com/PaulTrampert/PacmanManager>.

### Screenshots

`CONTRIBUTING.md` requires screenshots of the UI **before** and **after** in the description of any
PR that changes it. "Before" may be left out when the change is purely additive, and a new component
not yet used anywhere needs only its Storybook render.

GitHub has no API for attaching an image to a description, so the images live on a branch of their
own, `screenshots/pr-<n>`, which holds only images and is never merged. It is deleted
automatically when the PR closes, merged or not, by
[`screenshots-cleanup.yml`](#screenshot-branch-cleanup-screenshots-cleanupyml). Never commit them to
the PR's branch. `scripts/pr-screenshots.sh <n> <image>...` adds images to that branch, replacing
any of the same name and keeping the rest, and prints the Markdown that embeds each one. The links
name the commit, not the branch, so an image a description already shows never changes under it. Put the images under a `## Screenshots` heading, with
**Before** and **After** subheadings, and say how they were taken.

Take them with Playwright (`npx playwright install chromium` once). The real app is best, run
however suits the change: the app's production build under `npx vite preview`, with Playwright's
`page.route` answering the requests it makes (`/userinfo` and the API), needs no compose stack; a
Storybook render suits a single component. "Before" comes from the PR's base on `main`, which a
`git worktree add` of it, or `git archive` into a scratch directory, provides. Don't overwrite the
shared `pacmanmanager.*` images while doing so: `compose.yaml` names them, so a second compose
project still re-tags them.

**Keep them current.** A commit that changes what a screenshot shows -- addressing review feedback
included, and an `@claude` run on the PR in particular -- retakes the screenshots it affects, adds
them with the script, and updates the description to link the new commit, all in the same piece of
work. A screenshot of the UI as it was before the review is not an "after".

### Issue and pull request size

**One issue per pull request, and one pull request per issue.** An issue that cannot be delivered
as a single reviewable PR is two issues; split it before starting rather than growing the branch.

**Keep the PR small enough that a reviewer can hold the whole change in their head.** Changes that
are usually that size:

* adding a single method to a class, together with the interface change that goes with it;
* a diff under roughly 300 lines, tests aside;
* a new class with a single method on it.

Those are illustrations, not thresholds to measure against. Some changes are unavoidably large —
changing the signature of a widely used method has to land in one piece, since nothing builds
half-way through — but a large pull request should be the exception rather than the habit. When
cutting issues from a design document, prefer several independent issues to a chain of dependent
ones.

## Repository metadata

* `CLAUDE.md` is a symlink to this file. Edit `AGENTS.md`; never replace the symlink with a copy,
  since the point is that there is only one set of instructions to keep current.
* `.claude/hooks/session-start.sh` provisions a remote (cloud) session: the .NET SDK, pacman
  tooling, libalpm, a JRE for ANTLR, the Docker daemon, and the Node LTS with the web app's npm
  dependencies and Chromium. It is a no-op locally.
  `docs/cloud-environment.md` is the companion, and covers the network allowlist a cloud
  environment needs before the E2E tests can run.
* `.claude/commands/implement-unblocked.md` is the `/implement-unblocked` slash command: it assigns
  every open, unassigned issue with no open blocker and no `needs decision` label to the `gh` user, moves it to *In Progress*, and
  fans the batch out to sub-agents, one worktree and one PR per issue, following
  [Worktrees](#worktrees).
* `.github/workflows/` holds the CI workflows described under
  [Continuous integration](#continuous-integration). The toolchain they build with is installed
  by the local composite action `.github/actions/setup-toolchain` (the Node LTS included, for the
  web app), which mirrors
  `.claude/hooks/session-start.sh`; if a build dependency changes, both need updating.
* `docs/` holds design documents. A milestone's planning documents live in a folder named for the
  milestone, its title as a lowercase, hyphenated slug (`docs/web-ui-architecture/`,
  `docs/basic-auth/`), together with the images they show; exports of Penpot wireframes go in a
  `wireframes/` folder inside it. Documents that belong to no one milestone, such as the template,
  stay at the top of `docs/`. `docs/authorization-plan.md` documents the authorization design and
  its known gaps. `docs/web-ui-architecture/web-ui-architecture.md` fixes the web front end's
  architecture — read it before working on anything under the Web UI milestones. A new design
  document starts from `docs/design-doc-template.md`, which carries
  both the skeleton and the rules behind it: the body states *what* will be built, the argument for
  each decision lives in an Appendix that records what was rejected, each section ends with a
  bulleted **Why** footer linking those entries, and every `###` under the implementation plan is one
  issue with its own acceptance criteria and dependencies. A design document on `main` is settled —
  implement it as written. Deviating from one during implementation needs sign-off from a project
  owner, and the same change must update the plan, add an Appendix entry saying why, and update
  every dependent issue.
* `.github/dependabot.yml` checks each ecosystem (NuGet, npm, Docker, Compose, GitHub Actions) daily
  and batches its version updates into one pull request per ecosystem, and leaves security updates
  ungrouped so each arrives as its own PR. Its PR titles carry the `(PATCH) ` prefix the title check requires.
* `.run/` holds Rider run configurations.
* `test-fixtures/` holds binary fixtures shared by more than one test project — currently the
  minimal pacman package under `test-fixtures/packages` and the same package one version on, both
  committed rather than built during the test run and both regenerated by the `build-fixture.sh`
  next to them. Locate a fixture from a test with `PacmanManager.TestUtils.PackageFixtures` rather
  than composing the path again.
