# AGENTS.md

Guidance for coding agents working in this repository. `CLAUDE.md` is a symlink to this
file, so Claude Code and any other agent read the same instructions.

## What this is

A self-hosted **private pacman (Arch Linux) repository host**. `PacmanManager.RepoHost` is an
ASP.NET Core API that lets authenticated users create and manage pacman repositories; repository
database files (`.db.tar.gz`) live under `DATA_DIR` (default `/data`) and are manipulated with
`repo-add`. `LibAlpmSharp` is a from-scratch P/Invoke binding and `pacman.conf` parser (ANTLR
grammar) over libalpm.

Target framework is **net10.0** across every project. Nullable reference types and implicit usings
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

**Note on `dotnet test`:** `PacmanManager.RepoHost.Test` contains Testcontainers-based E2E tests
that build the RepoHost and Migrations Docker images and start Postgres and Keycloak containers.
A full `dotnet test` therefore needs a working Docker daemon and takes minutes. When iterating on
non-E2E logic, filter down to the unit fixtures instead.

**Note on non-Arch hosts:** `AlpmPackageTests` and `AlpmDatabaseTests.GetPackages_ReturnsListOfPackages`
read the host's local pacman database and expect an installed package named `pacman`, so they only
pass on an Arch machine. Everything else, the fixture packages included, works anywhere libalpm is
installed. `docs/cloud-environment.md` covers running the repository from a claude.ai cloud session,
where that applies.

## Running the stack

```bash
docker compose up -d --build       # full stack
docker compose up -d postgres      # database only (matches the Rider "Start Database Only" config)
```

Services in `compose.yaml`:

| Service | Notes |
| :--- | :--- |
| `pacmanmanager.repohost` | The API. Host port **8082** → container 8080. Built from an `archlinux` base image (it needs pacman tooling), not the Microsoft runtime image. |
| `auth` | Keycloak on port **8080**, importing the `localdev` realm from `keycloak/localdev.json`. |
| `migrations` | Runs EF migrations to completion and exits. |
| `postgres` | Postgres 18, port **5432**, user/password `pacmanmanager`/`password`. |

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

Adding a project means adding it to `PacmanManager.sln` (and to the relevant `Dockerfile` `COPY`
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

### Testing expectations

* Every new service or logic change needs unit tests in the matching `.Test` project.
* Every new or modified API endpoint needs an E2E test.
* Test stack: **NUnit** (globally imported via `GlobalUsings.cs` / `<Using Include="NUnit.Framework" />`),
  **Moq**, **Testcontainers**, and `Microsoft.EntityFrameworkCore.InMemory` for service-level tests.
* `InternalsVisibleTo` exposes RepoHost internals to `PacmanManager.RepoHost.Test`.
* A PR is expected to have a passing `dotnet build` and `dotnet test`.

## Continuous integration

`.github/workflows/ci.yml` runs on every pull request to `main` and on every push to `main`. It is
a gate, not a release pipeline — the project is unreleased and **nothing is published**.

| Job | What it does |
| :--- | :--- |
| `test` | Installs pacman tooling and libalpm on the Ubuntu runner, then `dotnet build` and `dotnet test` across the solution. |
| `docker` | Builds the RepoHost and Migrations images from their Dockerfiles with `push: false`, as a sanity check that both still build. |

`.github/workflows/pr-title.yml` is separate, and enforces the `MAJOR`/`MINOR`/`PATCH` prefix from
[Branches, commits, and PRs](#branches-commits-and-prs). It is its own workflow so that it can
trigger on `edited`: a mistyped prefix is fixed by editing the title, and the check re-runs on its
own rather than needing an empty commit. Putting `edited` on `ci.yml` would re-run the whole test
and image-build matrix every time somebody touched a title or description.

The runner is Ubuntu, so CI runs the same filter a non-Arch host needs:

```bash
dotnet test PacmanManager.sln --configuration Release --no-build \
    --filter "FullyQualifiedName!~AlpmPackageTests&FullyQualifiedName!~AlpmDatabaseTests.GetPackages"
```

`PacmanManager.RepoHost/Dockerfile.dockerignore` and `PacmanManager.Migrations/Dockerfile.dockerignore`
exist because Testcontainers looks for the ignore file **next to the Dockerfile**
(`<dockerfile>.dockerignore`, falling back to `.dockerignore` in that same directory) and never
reads the context root's `.dockerignore`, even though it builds with the solution root as context.
Without them the host's `bin/` and `obj/` are tarred into the build context, and `obj/` records
absolute host paths -- the sources ANTLR generates for `LibAlpmSharp` among them -- so
`dotnet build` inside the image fails with `CS2001: Source file ... could not be found`. That bites
only after a local build has populated `obj/`, which is always true in CI, and only for the RepoHost
image, since Migrations does not reference `LibAlpmSharp`. Keep all three ignore files in step.

Everything else runs, the E2E fixtures included — the runner's Docker daemon is what Testcontainers
starts Postgres and Keycloak on. If you add a test that only passes on Arch, it will fail in CI;
prefer seeding a database under a temporary root and pointing `LibAlpm.Initialize(root, dbPath)` at
it, which is the lasting fix for the two excluded fixtures as well.

## Version control

### Worktrees

Several agents are often working on this repository at the same time, and a shared working tree
means conflicting edits, a shared index, and branch switches that pull the ground out from under
another agent's build. Every unit of work therefore gets its own worktree — but *who* creates it
depends on what kind of agent you are.

**If you are a top-level agent** (a session the user drives directly), create your own worktree
before making any edits — `git worktree add`, or the `EnterWorktree` tool if your harness provides
one — and work there rather than in the primary checkout.

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
* Commit early and often — a meaningful change that builds is a good commit point.
* PR titles start with `PATCH`, `MINOR`, or `MAJOR` depending on the nature of the change.
* PR descriptions reference the issue they fix (`Fixes #123`) and explain what the diff does not
  make obvious.
* GitHub repo: <https://github.com/PaulTrampert/PacmanManager>.

## Repository metadata

* `CLAUDE.md` is a symlink to this file. Edit `AGENTS.md`; never replace the symlink with a copy,
  since the point is that there is only one set of instructions to keep current.
* `.claude/hooks/session-start.sh` provisions a remote (cloud) session: the .NET SDK, pacman
  tooling, libalpm, a JRE for ANTLR, and the Docker daemon. It is a no-op locally.
  `docs/cloud-environment.md` is the companion, and covers the network allowlist a cloud
  environment needs before the E2E tests can run.
* `.github/workflows/` holds the CI workflows described under
  [Continuous integration](#continuous-integration). They install the same toolchain as
  `.claude/hooks/session-start.sh`; if a build dependency changes, both need updating.
* `docs/` holds design documents. `docs/authorization-plan.md` documents the authorization design
  and its known gaps.
* `.run/` holds Rider run configurations.
* `test-fixtures/` holds binary fixtures shared by more than one test project — currently the
  minimal pacman package under `test-fixtures/packages` and the same package one version on, both
  committed rather than built during the test run and both regenerated by the `build-fixture.sh`
  next to them. Locate a fixture from a test with `PacmanManager.TestUtils.PackageFixtures` rather
  than composing the path again.
