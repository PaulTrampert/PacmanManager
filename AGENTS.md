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

## Version control

* **Do new work in a worktree.** Create one before making any edits — `git worktree add`, or the
  `EnterWorktree` tool if your harness provides it — and work there rather than in the primary
  checkout. Several agents are often working on this repository at the same time, and a shared
  working tree means conflicting edits, a shared index, and branch switches that pull the ground
  out from under another agent's build.
* **Never commit directly to `main`.** Branch as `feature/...` or `bugfix/...`.
* Commit early and often — a meaningful change that builds is a good commit point.
* PR titles start with `PATCH`, `MINOR`, or `MAJOR` depending on the nature of the change.
* PR descriptions reference the issue they fix (`Fixes #123`) and explain what the diff does not
  make obvious.
* GitHub repo: <https://github.com/PaulTrampert/PacmanManager>.

## Repository metadata

* `CLAUDE.md` is a symlink to this file. Edit `AGENTS.md`; never replace the symlink with a copy,
  since the point is that there is only one set of instructions to keep current.
* `docs/` holds design documents. `docs/authorization-plan.md` documents the authorization design
  and its known gaps.
* `.run/` holds Rider run configurations.
* `test-fixtures/` holds binary fixtures shared by more than one test project — currently the
  minimal pacman package under `test-fixtures/packages`, which is committed rather than built
  during the test run and is regenerated by the `build-fixture.sh` next to it. Locate a fixture
  from a test with `PacmanManager.TestUtils.PackageFixtures` rather than composing the path again.
