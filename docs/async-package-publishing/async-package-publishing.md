# Async Package Publishing

Status: **design**. This document is the source the **Async Package Publishing** issues are cut
from. It was written after the issues, from them, and states what they already ask for; the issue
number is in each heading of the [Implementation plan](#implementation-plan).

**How to read it.** The plan states what will be built. The argument for each decision — including
what was considered and rejected — is in the [Appendix](#appendix), and each section ends with a
**Why** footer linking the entries that bear on it. Once this document is on `main` the whole plan
is settled, whether or not a given detail has a Why entry: implement it as written and raise an
issue rather than re-deciding it.

Deviating from the plan during implementation is allowed, but never quietly. A deviation **must**
have sign-off from a project owner, and **must** carry all three of:

* the plan updated to say what is actually being built;
* an Appendix entry recording why it changed;
* every dependent issue updated to match.

It builds on the publishing behaviour [Package Management API](../package-management-api/packages-api.md)
specifies, and changes only *when* that behaviour runs and how its outcome is reported.

## Goals

* Publishing a package no longer copies the file into the repository or runs `repo-add` during the
  HTTP request. The request authorizes the caller, validates the upload, records a job, and returns.
* A caller can find out whether their publish succeeded, and why it failed if it did.
* A caller can list their jobs and clear away the ones that have finished.
* The rules a publish obeys — who may publish, which architectures, which versions — are the same
  whether they are checked in RepoHost or in the Worker, because both use the same code.
* Changes to a repository's database stay serialized when more than one process, or more than one
  replica of a process, can make them.
* An upload whose publish never completes does not stay on disk forever.

## Non-goals for this work

Recorded here so the issues stay bounded; each has a follow-up in
[Deferred work](#deferred-work).

* **Work beyond publishing.** Icon extraction and malware scanning are what the job infrastructure
  is for, but neither is built here.
* **Asynchronous delete.** Deleting a package stays on the request.
* **A web UI for jobs.** The web app has no upload screen yet; showing a job's progress arrives with
  one.

---

## Process layout

| Project | Kind | Holds |
| :--- | :--- | :--- |
| `PacmanManager.Authentication` | class library | `Actor`, `ActorScope`, `ScopeValues`, `AccessTokenFormat`, `IActorAccessor`, `FixedActorAccessor`. **No reference to ASP.NET Core.** |
| `PacmanManager.Packages` | class library | Publishing and removal side effects, the `repo-add`/`repo-remove` wrappers, `IPackagePathResolver`, `IFileSystem`, package reading through libalpm, `RepositoryAccessPolicy`, `PackageAccessPolicy`, `IRepositoryDatabaseLock`. |
| `PacmanManager.Commands` | class library | Command messages: data objects only, no behaviour. |
| `PacmanManager.Worker` | executable | Wolverine message handlers and scheduled work. |
| `PacmanManager.RepoHost` | executable | Unchanged in role. Keeps the ASP.NET sign-in code, `HttpContextActorAccessor`, its controllers and the read side of `PackageService`. |

Every new project is in `PacmanManager.sln` and has a matching `.Test` project.

| Compose service | Notes |
| :--- | :--- |
| `rabbitmq` | RabbitMQ, with one user for RepoHost and one for the Worker. |
| `pacmanmanager.worker` | Built from an `archlinux` base, like RepoHost. Mounts **the same `DATA_DIR` volume** as `pacmanmanager.repohost`. |

* The upload directory is on the shared `DATA_DIR` volume, so the Worker reads the upload RepoHost
  wrote.
* CI's Docker Build builds the Worker image alongside the others.

**Why:**

* [Why RabbitMQ and Wolverine](#why-rabbitmq-and-wolverine)
* [Why `Authentication` is split rather than moved whole](#why-authentication-is-split-rather-than-moved-whole)
* [Why the publishing logic gets a library of its own](#why-the-publishing-logic-gets-a-library-of-its-own)
* [Why commands have a data-only project](#why-commands-have-a-data-only-project)

---

## Jobs

A `Job` records one unit of off-request work. It is **one entity and one table** for every kind of
job; it is not generic.

| Member | Type | Notes |
| :--- | :--- | :--- |
| `Id` | `Guid` | Version 7. |
| `CreatedAt` | `DateTimeOffset` | UTC. |
| `State` | enum | `Pending`, `Running`, `Completed`, `Failed`. `Completed` and `Failed` are **final**. |
| `Error` | `string?` | Set when `Failed`. |
| `UserId` | `Guid?` | Foreign key to `User`: the user who scheduled the job. `null` when the `System` actor, which has no user, scheduled it. |
| `MessageType` | `string` | Names the command type the message holds. |
| `Message` | jsonb | The command itself. |
| `AffectedEntities` | collection of `JobAffectedEntity` | The entities the job acts on, as far as they are known. |

`JobAffectedEntity` is a row of its own, keyed by `JobId`, `EntityType` and `EntityId`:

| Member | Type | Notes |
| :--- | :--- | :--- |
| `JobId` | `Guid` | Foreign key to `Job`, deleted with it. |
| `EntityType` | enum | `JobEntityType`: `Repository`, `Package`. |
| `EntityId` | `Guid` | The entity's id. Not a foreign key, so it survives the entity's deletion. |

* An affected entity is recorded **as soon as it is known**: whoever schedules a job records what it
  already names, and the inner handler reports what it creates.

* `Job.As<TMessage>()` returns a `Job<TMessage>`: a typed view, not mapped by EF, carrying the
  job's id, its user and its message deserialized as `TMessage`. It throws `JobMessageTypeException`
  when `MessageType` does not name `TMessage`.
* `PacmanManager.Entities` does not reference `PacmanManager.Commands`: `As<TMessage>()` is generic
  over any type, and only its callers name a command.
* The job is **composed with** its message: there is no base message type, and the job, not the
  command, carries the user and the tracking state.

A generic job-tracking handler wraps every handler that runs a job. It loads the `Job`, takes its
typed view with `As<TMessage>()`, and passes the inner handler that view's message. The inner
handler returns the entities it affected that the job did not already record, which may be none:

| Event | State afterwards |
| :--- | :--- |
| message received | `Running` |
| inner handler returns | `Completed`, with the affected entities it returned added to the job |
| inner handler throws `JobRejectedException` | `Failed` at once, with the rejection in `Error`. **Never retried.** |
| inner handler throws anything else, retry remaining | `Pending` |
| inner handler throws anything else, retries exhausted | `Failed`, with the failure in `Error` |

`JobRejectedException` is how an inner handler reports a **deliberate rejection**: a decision about
the message that running it again cannot change. Any other exception is treated as a failure that a
retry may get past.

**Why:**

* [Why deliberate rejections are never retried](#why-deliberate-rejections-are-never-retried)
* [Why the job is composed with its message](#why-the-job-is-composed-with-its-message)
* [Why one `Job` table with a typed view](#why-one-job-table-with-a-typed-view)
* [Why the job stores only a user id](#why-the-job-stores-only-a-user-id)
* [Why jobs record the entities they affect](#why-jobs-record-the-entities-they-affect)

---

## Publishing

### On the request

`POST /api/v1/repositories/{repositoryId}/packages`:

1. Checks that the caller may publish to the repository **before reading any of the body**, with
   the responses publishing gives today: `401` when anonymous, `403` for a public repository the
   caller does not own, `404` for one that does not exist or that the caller cannot see.
2. Writes the upload to a temporary file in the upload directory. An upload over the configured
   maximum is a `413`.
3. Rejects, with the same responses publishing gives today and **before anything is enqueued**:
   * an unreadable package;
   * a package whose architecture the repository does not support;
   * a package whose version is not newer than every stored build it would supersede (see
     [Version check](#version-check)).
4. Creates a `Pending` job, with the current actor's user as `UserId` and the repository as an
   affected entity, whose `Message` is a `PublishPackageCommand`:

   ```json
   { "repositoryId": "…", "packageFilePath": "…" }
   ```

5. Publishes the command. The job row and the published message are **committed together**: neither
   ever exists without the other.
6. Responds **`202 Accepted`** with the job, as [`GET /api/v1/Jobs/{id}`](#job-routes) returns it,
   and a `Location` of that route. This replaces today's `201 Created` and `200 OK` with the package.

The version check in step 3 can race with another publish. It **may let an invalid version
through**, which the Worker then fails; it **must never reject a valid version**.

The response change stays under **`v1`**. No new API version is introduced; `openapi/v1.json` is
regenerated in the same PR.

### Version check

A package name can have several stored builds in one repository, one per architecture or a single
`any` build. A new build is compared **only with the stored builds it would supersede** — those
`Replaces` pairs it with: the build of the same architecture, any `any` build, and, when the new
build is itself `any`, every build of the name. It must be newer than each of them. A build for
another specific architecture is never compared, so publishing `foo` 2.0 for `aarch64` beside `foo`
3.0 for `x86_64` is valid. This is the rule `StoreUploadAsync` applies today; both the request and
the Worker use it.

### In the Worker

The `PublishPackageCommand` handler, inside the job-tracking handler:

1. Runs as the job's user with unrestricted scope (`Actor.SystemFor(user)`), or as `Actor.System`
   when the job has no user, through a `FixedActorAccessor`. **Permissions are not checked again**:
   they decided whether the job could be scheduled, and stop mattering once it was.
2. Takes the repository's lock, and under it rejects, by throwing `JobRejectedException` with the
   reason as its message:
   * a repository that no longer exists;
   * an upload that is missing or unreadable;
   * an unsupported architecture;
   * a version that is not newer than every stored build it would supersede.

   Each of these fails the job **at once, without retrying**. In particular, the repository's
   absence is a rejection, never a `null` the handler returns from, which would mark the job
   `Completed` with nothing published.
3. **Copies** the upload into the repository directory, adds it with `repo-add`, and upserts the
   package entity — the behaviour [Package Management API](../package-management-api/packages-api.md)
   specifies for publishing, except that the upload is copied rather than moved. A failure removes
   the copy, as today's compensation removes the moved file, and **leaves the upload in place**, so
   a retry starts from the same file.
4. Once the publish has committed, deletes the upload. A delete that fails is logged and does not
   fail the job; [Upload cleanup](#upload-cleanup) removes the file later.
5. Returns the published package as an affected entity, so the completed job names it. Builds the
   publish superseded are not reported: they no longer exist.

**Why:**

* [Why publishing leaves the request](#why-publishing-leaves-the-request)
* [Why the request authorizes before writing the upload](#why-the-request-authorizes-before-writing-the-upload)
* [Why the request still validates](#why-the-request-still-validates)
* [Why permissions are checked only when the job is scheduled](#why-permissions-are-checked-only-when-the-job-is-scheduled)
* [Why the Worker copies the upload rather than moving it](#why-the-worker-copies-the-upload-rather-than-moving-it)
* [Why the job and the message commit together](#why-the-job-and-the-message-commit-together)
* [Why the contract change stays in `v1`](#why-the-contract-change-stays-in-v1)

---

## Repository lock

`IRepositoryDatabaseLock` keeps its contract and gets a new implementation:

* A **Postgres advisory lock** replaces the in-process lock.
* It uses the **two-key form**, `pg_advisory_lock(key1, key2)`:
  * `key1` is a fixed constant, declared once, that names the lock's purpose (repository databases);
  * `key2` is derived from the repository's id: the XOR of the `Guid`'s four 32-bit words. The
    derivation must give the same key in every process and runtime, so it is **never**
    `Guid.GetHashCode()`, which .NET randomizes per process.
* It is a **session-level** lock. `AcquireAsync` opens a dedicated connection for it, not one a
  `DbContext` is using; disposing the handle calls `pg_advisory_unlock` and closes that connection.
  If the holding process dies, Postgres ends the session and releases the lock.
* `AcquireAsync` waits for exclusive access to one repository and honours its cancellation token.
* Two different repositories share a `key2` only by collision, roughly a one in 2³² chance for any
  pair. Colliding repositories serialize against each other, which costs waiting, **never
  correctness**. Otherwise locks on different repositories never block each other.
* Publish and delete, in either process, take the same lock.

**Why:**

* [Why a Postgres advisory lock](#why-a-postgres-advisory-lock)
* [Why the lock key is derived this way](#why-the-lock-key-is-derived-this-way)

---

## Job routes

A job is shown as:

```json
{
  "id": "…",
  "state": "Completed",
  "createdAt": "…",
  "affectedEntities": [
    { "type": "Repository", "id": "…" },
    { "type": "Package", "id": "…" }
  ]
}
```

`error` is present only when `state` is `Failed`. `affectedEntities` lists what is known so far: a
publish names its repository from the start and its package once it completes.

| Route | Caller | Response |
| :--- | :--- | :--- |
| `GET /api/v1/Jobs/{id}` | the user who scheduled the job | `200` with the job |
| `GET /api/v1/Jobs` | any signed-in user | `200` with a page of **their own** jobs |
| `DELETE /api/v1/Jobs/{id}` | the user who scheduled the job, job in a final state | `204` |
| `DELETE /api/v1/Jobs/{id}` | the user who scheduled the job, job `Pending` or `Running` | `409` |
| `GET` or `DELETE /api/v1/Jobs/{id}` | any other signed-in user, or no such job | `404` |
| any | anonymous | `401` |

* `GET /api/v1/Jobs` follows the listing conventions: `PaginationParams`, a filter query object
  with an optional `state`, and `SortOptions<JobSortField>` whose first member, the default, is
  `CreatedAt`, newest first. The filter is ANDed onto the caller's own jobs and can never widen
  them.
* Deleting a finished job removes its row only. A completed publish has already deleted its upload,
  and a failed one's is left to [Upload cleanup](#upload-cleanup).
* The rules live in a `JobAccessPolicy` with no DB, HTTP or logging dependencies, including the
  visibility predicate the listing applies and the rule that only a final job may be deleted.
* The service enforces the policy itself, as `RepositoryService` does, not the controller.

**Why:**

* [Why job status is `/Jobs/{id}`](#why-job-status-is-jobsid)
* [Why jobs record the entities they affect](#why-jobs-record-the-entities-they-affect)
* [Why only finished jobs can be deleted](#why-only-finished-jobs-can-be-deleted)

---

## Upload cleanup

The Worker runs a cleanup on a configurable interval. Each run deletes the files in the upload
directory that are:

* older than a configurable minimum age, **and**
* not the `PackageFilePath` of a `Pending` or `Running` job.

It finds the live uploads by loading the `Pending` and `Running` jobs whose `MessageType` names
`PublishPackageCommand` and reading each one's `PackageFilePath` through
`As<PublishPackageCommand>()`.

A file that fails to delete is logged and does not stop the run.

**Why:**

* [Why cleanup is a scheduled sweep in the Worker](#why-cleanup-is-a-scheduled-sweep-in-the-worker)

---

## Test environment

* `EndToEndTestFixture` starts RabbitMQ and the Worker alongside Postgres, Keycloak and RepoHost.
* The Worker image is built with **the solution root as the Dockerfile directory**
  (`.WithDockerfile("PacmanManager.Worker/Dockerfile")`), as RepoHost's is.
* The Worker and RepoHost containers share one data directory.
* E2E tests that publish wait for the job to complete before checking the result.

**Why:**

* [Why the E2E fixture changes ahead of the endpoint](#why-the-e2e-fixture-changes-ahead-of-the-endpoint)

---

## Implementation plan

One issue per heading. Dependencies are noted; anything without a dependency can start immediately.

### 1. Add RabbitMQ to the compose stack (#363) — `PATCH`

A `rabbitmq` service that starts with the dev stack, with a user for RepoHost and a user for the
Worker.

*Acceptance:* the stack comes up with RabbitMQ, and both users can connect.

*Depends on:* nothing.

**Why:**

* [Why RabbitMQ and Wolverine](#why-rabbitmq-and-wolverine)

### 2. Configure Wolverine in RepoHost (#364) — `PATCH`

The Wolverine package reference, configured to publish to RabbitMQ through Wolverine's **EF Core
transactional outbox** on Postgres, enlisted with `PacmanManagerDbContext`. Wolverine creates its
message storage tables at startup.

*Acceptance:* RepoHost starts against the compose stack with the outbox's tables created; a test
shows that a message published with a `SaveChangesAsync` that commits is sent, and one published
with a transaction that rolls back is not.

*Depends on:* #363.

**Why:**

* [Why RabbitMQ and Wolverine](#why-rabbitmq-and-wolverine)
* [Why the job and the message commit together](#why-the-job-and-the-message-commit-together)

### 3. Create `PacmanManager.Worker` (#365) — `MINOR`

An executable project with its test project, receiving messages through Wolverine, run as
`pacmanmanager.worker` in compose. See [Process layout](#process-layout).

*Constraints:* the image needs an `archlinux` base, and the container needs the same `DATA_DIR`
volume as RepoHost.

*Acceptance:* the Worker starts in the compose stack and connects to RabbitMQ; CI's Docker Build
builds its image.

*Depends on:* #363.

### 4. Extract `PacmanManager.Authentication` (#382) — `PATCH`

Move the types [Process layout](#process-layout) lists into the new library, behaviour unchanged,
with their tests.

*Constraints:* the library must not reference ASP.NET Core. RepoHost's `Dockerfile` copies the new
`.csproj`.

*Acceptance:* the moved tests pass in `PacmanManager.Authentication.Test`; RepoHost builds and its
tests pass unchanged.

*Depends on:* nothing.

**Why:**

* [Why `Authentication` is split rather than moved whole](#why-authentication-is-split-rather-than-moved-whole)

### 5. Extract `PacmanManager.Packages` (#383) — `PATCH`

Move the publishing and removal side effects and everything [Process layout](#process-layout) lists
for the library, behaviour unchanged, with their tests.

*Constraints:* `PackageServiceEnforcementTests` and `RepositoryServiceEnforcementTests` keep passing.
RepoHost's `Dockerfile` copies the new `.csproj`.

*Acceptance:* the moved tests pass in `PacmanManager.Packages.Test`; RepoHost builds, its tests pass
unchanged, and RepoHost's `Dockerfile` builds.

*Depends on:* #382.

**Why:**

* [Why the publishing logic gets a library of its own](#why-the-publishing-logic-gets-a-library-of-its-own)

### 6. Postgres advisory repository lock (#384) — `PATCH`

The implementation [Repository lock](#repository-lock) specifies.

*Acceptance:* tests against a real Postgres show that two lock instances exclude each other on one
repository and not on different ones, that a cancelled wait gives up without the lock, and that a
lock whose connection is closed is released. A unit test shows the key derivation gives the same
key for the same id on every call.

*Depends on:* nothing.

**Why:**

* [Why a Postgres advisory lock](#why-a-postgres-advisory-lock)
* [Why the lock key is derived this way](#why-the-lock-key-is-derived-this-way)

### 7. Add the `Job` table (#367) — `MINOR`

The entities [Jobs](#jobs) specifies, `Job` and `JobAffectedEntity`, the typed view
`Job<TMessage>` and `JobMessageTypeException`, and their migration.

*Acceptance:* the migration applies, and deleting a job deletes its affected entities; unit tests
show `As<TMessage>()` returns the stored message for the type `MessageType` names and throws
`JobMessageTypeException` for any other.

*Depends on:* nothing.

**Why:**

* [Why one `Job` table with a typed view](#why-one-job-table-with-a-typed-view)
* [Why the job stores only a user id](#why-the-job-stores-only-a-user-id)

### 8. Create `PublishPackageCommand` (#369) — `PATCH`

Create `PacmanManager.Commands` and the command, with `RepositoryId` and `PackageFilePath`.

*Acceptance:* the project is in `PacmanManager.sln` with its test project, references no other
project in the solution, and a test shows the command round-trips through JSON unchanged.

*Depends on:* nothing.

**Why:**

* [Why commands have a data-only project](#why-commands-have-a-data-only-project)

### 9. Job-tracking message handler (#368) — `PATCH`

The generic handler [Jobs](#jobs) specifies, and `JobRejectedException`.

*Acceptance:* unit tests of every transition in the table, including that a `JobRejectedException`
with retries remaining fails the job without the inner handler running again, and that the
affected entities an inner handler returns are recorded on the completed job.

*Depends on:* #365, #367.

**Why:**

* [Why deliberate rejections are never retried](#why-deliberate-rejections-are-never-retried)

### 10. `PublishPackageCommand` handler (#366) — `MINOR`

The Worker side of [Publishing](#in-the-worker).

*Acceptance:* tests show that a deleted repository, a missing upload, an unreadable upload, an
unsupported architecture and a version no longer newer each throw `JobRejectedException` without
publishing; that a failure after the copy leaves the upload in place and a retry then publishes it;
and that a successful publish deletes the upload and reports the published package as an
affected entity.

*Depends on:* #365, #368, #369, #383, #384.

**Why:**

* [Why deliberate rejections are never retried](#why-deliberate-rejections-are-never-retried)
* [Why permissions are checked only when the job is scheduled](#why-permissions-are-checked-only-when-the-job-is-scheduled)
* [Why the Worker copies the upload rather than moving it](#why-the-worker-copies-the-upload-rather-than-moving-it)

### 11. `GET /api/v1/Jobs/{id}` (#385) — `MINOR`

The single-job `GET` route [Job routes](#job-routes) specifies, its `JobAccessPolicy`, and the
regenerated `openapi/v1.json`.

*Acceptance:* each policy rule is a unit test; an E2E test covers the route, including its
`affectedEntities`.

*Depends on:* #367, #382.

**Why:**

* [Why job status is `/Jobs/{id}`](#why-job-status-is-jobsid)

### 12. `GET /api/v1/Jobs` (#389) — `MINOR`

The listing route [Job routes](#job-routes) specifies, with `JobSortField`, its filter query object,
the visibility predicate in `JobAccessPolicy`, and the regenerated `openapi/v1.json`.

*Acceptance:* unit tests show a caller sees only their own jobs, that the `state` filter cannot
widen that, and that the default order is newest first; an E2E test covers the route.

*Depends on:* #385.

### 13. `DELETE /api/v1/Jobs/{id}` (#390) — `MINOR`

The delete route [Job routes](#job-routes) specifies, its rule in `JobAccessPolicy`, and the
regenerated `openapi/v1.json`.

*Acceptance:* unit tests show a final job of the caller's own is deleted, a `Pending` or `Running`
one is refused, and another user's is not found; an E2E test covers the route.

*Depends on:* #385.

**Why:**

* [Why only finished jobs can be deleted](#why-only-finished-jobs-can-be-deleted)

### 14. Run RabbitMQ and the Worker in the E2E fixture (#386) — `PATCH`

The fixture changes [Test environment](#test-environment) specifies, short of the tests waiting for
jobs.

*Acceptance:* the existing E2E tests pass with the new containers running.

*Depends on:* #365.

**Why:**

* [Why the E2E fixture changes ahead of the endpoint](#why-the-e2e-fixture-changes-ahead-of-the-endpoint)

### 15. Publish endpoint validates and enqueues (#380) — `MAJOR`

The request side of [Publishing](#on-the-request), the regenerated `openapi/v1.json`, and the E2E
tests that publish waiting for their job.

*Constraints:* the caller's permission is checked before the body is read. The version check must
never reject a valid version. The route stays in `v1`.

*Acceptance:* E2E tests show an anonymous caller, a caller without rights and a missing repository
get `401`, `403` and `404` with no upload written and no job created; that the three validation
failures are rejected before a job exists; and that a valid publish returns `202` naming the repository, and its job
completes naming the published package.

*Depends on:* #364, #366, #367, #369, #385, #386.

**Why:**

* [Why the request authorizes before writing the upload](#why-the-request-authorizes-before-writing-the-upload)
* [Why the request still validates](#why-the-request-still-validates)
* [Why the job and the message commit together](#why-the-job-and-the-message-commit-together)
* [Why the contract change stays in `v1`](#why-the-contract-change-stays-in-v1)

### 16. Scheduled upload cleanup (#387) — `PATCH`

The sweep [Upload cleanup](#upload-cleanup) specifies.

*Acceptance:* unit tests show a file with a live job is kept, a file younger than the minimum age is
kept, and an old file with only a finished job, or with none, is deleted.

*Depends on:* #365, #367, #369.

**Why:**

* [Why cleanup is a scheduled sweep in the Worker](#why-cleanup-is-a-scheduled-sweep-in-the-worker)

---

## Deferred work

Worth filing as issues, but explicitly out of scope for the work above.

* **Icon extraction and malware scanning** — further handlers in the Worker, each its own command
  and job. Additive.
* **Asynchronous delete** — a `RemovePackageCommand` handled the same way. The shared lock already
  makes a delete in RepoHost safe beside a publish in the Worker, so nothing forces it.
* **Job progress in the web UI** — arrives with a package upload screen, polling
  `GET /api/v1/Jobs/{id}`.
* **Wider job visibility** — letting a repository's owner see jobs others started against it is a
  new rule in `JobAccessPolicy`.

---

## Appendix

The argument for each decision in the plan, including what was considered and rejected. Nothing
here adds a requirement — the plan above is the specification, and it is the plan, once merged,
that is settled. These entries are the supporting reasoning: if implementation suggests a different
choice, they are what to argue with, in an issue, rather than something to quietly depart from.
A deviation that is agreed to updates the plan, this Appendix and the dependent issues together, as
the **How to read it** paragraph at the top sets out.

### Why publishing leaves the request

More has to happen to an uploaded package than a request should wait for: extracting its icon,
perhaps scanning it for malware, and adding it to the repository itself. Moving the publish off the
request gives that work a place to run, and means a slow `repo-add` or a held repository lock no
longer holds an HTTP connection open.

### Why RabbitMQ and Wolverine

RabbitMQ does everything this work needs of a broker, and is one the project owner already knows,
so it was chosen over brokers that would have to be learned for no gain.

MassTransit was the first choice of library over it, but its licensing recently changed, and
Wolverine fills the same role: handlers, retries, RabbitMQ transport and an EF Core outbox.

### Why `Authentication` is split rather than moved whole

The first proposal was to lift the whole `PacmanManager.RepoHost.Authentication` namespace into the
new library. Half of it is ASP.NET sign-in code — `BasicAuthenticationHandler`, `ClaimsTransformer`,
the JwtBearer setup — which depends on `Microsoft.AspNetCore.Authentication`, and two of those types
depend on `IUserService` in RepoHost's services. Moving it all would put ASP.NET in the Worker and
drag `IUserService` along. Meanwhile the types the Worker does need, `IActorAccessor` and
`FixedActorAccessor`, live in `Services`, not `Authentication`. The split follows what the Worker
needs instead of where the files happen to sit.

### Why the publishing logic gets a library of its own

The Worker's handler needs `PackageService`'s commit and compensation, the CLI wrappers, path
resolution, libalpm package reading and both access policies — all `internal` to RepoHost. Referencing
RepoHost from the Worker would pull in the web host. Copying the code would let the two disagree
about what a valid publish is, which is exactly what the
[Goals](#goals) rule out. A library both hosts reference keeps one definition.

### Why commands have a data-only project

RepoHost publishes commands and the Worker handles them, so both reference their types. Putting
them in `PacmanManager.Packages` would make a message contract depend on publishing logic. A
data-only project can be referenced by anything without carrying behaviour along. #369 creates it,
rather than an issue of its own, because the command is all it holds at first.

### Why the job is composed with its message

A job type that each command derived from would tie every command to job tracking, and a command
could not be sent without a job. Composing them keeps the command a plain data object, lets the
job-tracking handler be generic over any message, and lets EF load the content with the job.

### Why one `Job` table with a typed view

The first draft made the entity itself generic, `Job<TMessage>`. EF maps each closed generic type as
an entity of its own, so that would have meant either a table per command — which
`GET /api/v1/Jobs/{id}`, the listing and the cleanup sweep would each have to search — or one table
whose jsonb could not be turned back into a type, since nothing recorded which command it held.
Closing the generic in the model would also have made `PacmanManager.Entities`, and with it the
migrations, reference `PacmanManager.Commands`.

One non-generic `Job` with a `MessageType` column keeps every job in one table that the routes and
the sweep query without knowing the type, while `As<TMessage>()` gives a handler the typed message
it wants. Checking `MessageType` there turns a handler reading the wrong kind of job into a specific
exception rather than a half-deserialized object.

### Why the job stores only a user id

`Actor` wraps a `User` entity and lives in `PacmanManager.Authentication`; `Job` lives in
`PacmanManager.Entities`, which cannot reference it. Serializing an `Actor` into jsonb would also
copy a `User` row into every job. The first draft also stored the actor's scopes, so that the Worker
could rebuild the actor and recheck its permissions; with that recheck dropped (see
[Why permissions are checked only when the job is scheduled](#why-permissions-are-checked-only-when-the-job-is-scheduled)),
the user id is all that is left to keep: it decides who may see and delete the job, and is the user
the Worker publishes as.

### Why the request authorizes before writing the upload

Publishing today authorizes before a byte of the body is read, so a caller who may not publish is
turned away without uploading megabytes first, and gets a `401`, `403` or `404` that says why.
Writing the upload first would let anyone fill `DATA_DIR` with uploads to repositories they cannot
touch, and would turn a permission error into a job that fails later. The asynchronous publish keeps
that order.

### Why the request still validates

Without validation on the request, every mistake — an unreadable file, the wrong architecture, an
old version — would be reported only through a failed job, after the caller had been told `202`.
The common mistakes are cheap to catch on the request and are caught there.

The version check cannot be made exact on the request without holding the repository lock until the
Worker finishes, which would undo the point of going asynchronous. The asymmetry is deliberate:
letting an invalid version through costs a failed job, which the Worker reports properly; rejecting
a valid one would be a wrong answer with no recourse.

The check compares only with the builds the new one would supersede because that is what a newer
version is protecting: a client that already holds a published build's bytes. A build for another
architecture is never replaced, so its version says nothing about whether the new one may be
published.

### Why permissions are checked only when the job is scheduled

Permissions decide whether a caller may schedule a publish. Once a job is scheduled, it is the
system's commitment to carry out, not a request still awaiting the caller's rights. Rechecking them
in the Worker would need the job to carry enough of the credential to rebuild the actor exactly —
its scopes, whether it was a system actor, and whether the access token it came from still exists —
and would still leave a window between the recheck and the publish. Running the job as its user
with unrestricted scope keeps the user recorded as the publisher without any of that.

### Why the Worker copies the upload rather than moving it

Today's publish moves the upload to its destination before running `repo-add`, and deletes the
destination if anything after that fails. On the request that was harmless: the caller's upload was
gone, but so was the request. In a job that may be retried it is fatal: a `repo-add` that cannot take
its lock file, or a lost database connection — exactly the failures retries exist for — would destroy
the upload, and every retry would then fail as an unreadable package. Copying leaves the upload
untouched until the publish has committed, so a retry starts from the same file, and deleting it only
afterwards means the only cost of a crash in between is a file the cleanup sweep removes.

### Why deliberate rejections are never retried

A missing repository, an unusable upload, an unsupported architecture or a version that is no longer
newer is a decision about the message, not a fault in running it. Retrying it repeats the same work
to reach the same answer, and holds the repository lock and delays the caller's `Failed` while it
does. Only failures that might clear on their own — a lost connection, a `repo-add` that could not
take its lock file — are worth a retry.

The handler signals the difference with a specific exception type rather than a list of exception
types configured per handler. A list would have to be kept in step with every exception a handler,
and the library code it calls, might throw; a rejection the list forgot would quietly be retried.
With `JobRejectedException`, a handler states its decision where it makes it.

### Why the job and the message commit together

Writing the job row and then publishing the message, as two steps, leaves two failures to handle: a
job that stays `Pending` with no message to run it, or a message whose job does not exist. A
transactional outbox, such as Wolverine's EF Core integration, makes the two one commit.

Wolverine creates its own storage tables at startup rather than through `PacmanManager.Migrations`.
Mapping them into `PacmanManagerDbContext` would need an `OnModelCreating`, which the entities
otherwise do without, and Wolverine already knows how to migrate its own schema.

### Why the contract change stays in `v1`

The project is unreleased, so there are no clients of the publish route outside this repository to
keep working. A `v2` would carry a second publish route, and its tests, for no one. The breaking
change is made in place, and `openapi/v1.json` is regenerated with it, as `AGENTS.md` requires.

### Why a Postgres advisory lock

The in-process lock stops being correct as soon as a second process changes repository databases —
here the Worker beside RepoHost — and would be needed anyway for replica sets of either.

* **`repo-add`'s own lock file** already prevents corruption, but makes the second writer fail
  rather than wait.
* **One consumer per repository in RabbitMQ** would serialize publishes, but not RepoHost's deletes.
* **A row lock** (`SELECT … FOR UPDATE` on the repository's row) would be exact, but is held by a
  transaction, which would then stay open across `repo-add`, and it would also block unrelated
  changes to the repository, such as a rename.
* **A Postgres advisory lock** uses infrastructure every process already has, waits rather than
  fails, and as a session-level lock is released by Postgres when the holding process dies, so a
  crashed Worker cannot leave a repository locked.

### Why the lock key is derived this way

An advisory lock is keyed by one 64-bit integer or two 32-bit ones, and a repository's id is a
128-bit `Guid`, so the key has to be derived and can collide. A collision only makes two
repositories wait for each other; it never lets two writers into one repository, so a small chance
of it is acceptable and an exact mapping is not worth a table of its own.

The two-key form is used so that the first key can name the lock's purpose. Postgres keeps one-key
and two-key locks in separate spaces, and Wolverine takes advisory locks of its own in the one-key
space, so the namespace keeps the two from ever meeting. The single-key form would give 64 bits of
the id rather than 32 and fewer collisions, but would share its space with Wolverine's.

XOR of the four words uses every bit of the id. Taking the first four bytes would not: in a version
7 `Guid` they are the top of the creation timestamp, which repositories created within about a
minute of each other share. The derivation has to be stable across processes, which rules out
`Guid.GetHashCode()`.

### Why jobs record the entities they affect

Today's publish answers with the package and a `Location` for it. A job that only reported
`Completed` would leave the caller guessing the name, version and architecture and searching the
repository for it, with no way to tell its own publish from a concurrent one. Recording the
affected entities on the job gives the answer back.

They are a list of typed ids rather than a `PackageId` column because other jobs will affect other
things — icon extraction a package, an asynchronous delete a package that is then gone — and a
column per kind would grow with every command. They are rows of their own rather than jsonb so that
a later rule, such as letting a repository's owner see jobs against it, can query them. `EntityId`
is not a foreign key because a job outlives what it affected: the record that a package was
published stays true after the package is deleted.

Superseded builds are left out because the job's answer is what now exists; the ids of rows that
were deleted would lead the caller nowhere.

### Why only finished jobs can be deleted

Jobs accumulate, and a caller who lists theirs needs a way to clear the ones they have dealt with.
A `Pending` or `Running` job is still the record the Worker updates and the cleanup sweep relies on
to keep its upload; deleting it would orphan the upload and leave the caller nothing to poll. A
finished job is only a record of what happened, so removing it loses nothing the system needs.

### Why job status is `/Jobs/{id}`

A route under the package, `/Packages/{id}/Jobs/{jobId}`, was considered because it would let the
package's access rules decide who sees the job. But a first publish has no package id when the job
is created — the package row only appears when the Worker upserts it — so that route could not be
the `Location` of the `202`. A repository-scoped route would work but ties the job API to one kind
of job. `/Jobs/{id}` reflects the table directly, and a `JobAccessPolicy` keyed on the job's user is
as simple a rule as any of the others.

### Why cleanup is a scheduled sweep in the Worker

An upload can be abandoned more than one way: its job fails for good, the request dies between
writing the file and committing the job, or the Worker publishes it and then fails to delete it.
Cleaning up on job failure would catch only the first. A periodic sweep catches all of them, and the
Worker is already the process that owns off-request work. The minimum age keeps the sweep away from
an upload whose job is still being committed.

### Why the E2E fixture changes ahead of the endpoint

#380 changes the publish contract and every E2E test that publishes. Building the harness that runs
the Worker in the same PR would make it the largest in the milestone. Doing the fixture first, while
publishing is still synchronous, proves the harness against tests that already pass.
