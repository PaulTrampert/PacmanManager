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

* Publishing a package no longer moves the file into the repository or runs `repo-add` during the
  HTTP request. The request validates the upload, records a job, and returns.
* A caller can find out whether their publish succeeded, and why it failed if it did.
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
* **Listing jobs.** A caller can read one job by id, not search them.

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

* The upload directory is on the `DATA_DIR` filesystem, so moving an upload into a repository is a
  rename, never a copy.
* CI's Docker Build builds the Worker image alongside the others.

**Why:**

* [Why `Authentication` is split rather than moved whole](#why-authentication-is-split-rather-than-moved-whole)
* [Why the publishing logic gets a library of its own](#why-the-publishing-logic-gets-a-library-of-its-own)
* [Why commands have a data-only project](#why-commands-have-a-data-only-project)

---

## Jobs

A `Job` records one unit of off-request work.

| Member | Type | Notes |
| :--- | :--- | :--- |
| `Id` | `Guid` | Version 7. |
| `Created` | `DateTime` | UTC. |
| `State` | enum | `Pending`, `Running`, `Completed`, `Failed`. |
| `Error` | `string?` | Set when `Failed`. |
| `UserId` | `Guid?` | Foreign key to `User`; the actor's user. |
| `Scopes` | strings | The actor's scope values. |
| `Message` | `TMessage` | The command itself, stored as jsonb. |

* The job is **composed with** its message: `Message` is the command's content, and EF loads it with
  the job. There is no base message type.
* The job, not the command, carries the actor and the tracking state.

A generic job-tracking handler wraps every handler that runs a job:

| Event | State afterwards |
| :--- | :--- |
| message received | `Running` |
| inner handler returns | `Completed` |
| inner handler throws, retry remaining | `Pending` |
| inner handler throws, retries exhausted | `Failed`, with the failure in `Error` |

The inner handler receives the job's `Message`.

**Why:**

* [Why the job is composed with its message](#why-the-job-is-composed-with-its-message)
* [Why the job stores a user id and scopes, not an `Actor`](#why-the-job-stores-a-user-id-and-scopes-not-an-actor)

---

## Publishing

### On the request

`POST /api/v1/repositories/{repositoryId}/packages`:

1. Writes the upload to a temporary file in the upload directory.
2. Rejects, with the same responses publishing gives today and **before anything is enqueued**:
   * an unreadable package;
   * a package whose architecture the repository does not support;
   * a package whose version is not newer than the one currently stored.
3. Creates a `Pending` job, recording the current actor, whose `Message` is a `PublishPackageCommand`:

   ```json
   { "repositoryId": "…", "packageFilePath": "…" }
   ```

4. Publishes the command. The job row and the published message are **committed together**: neither
   ever exists without the other.
5. Responds **`202 Accepted`** with the job, as [`GET /api/v1/Jobs/{id}`](#job-status) returns it,
   and a `Location` of that route. This replaces today's `201 Created` and `200 OK` with the package.

The version check in step 2 can race with another publish. It **may let an invalid version
through**, which the Worker then fails; it **must never reject a valid version**.

The response change stays under **`v1`**. No new API version is introduced; `openapi/v1.json` is
regenerated in the same PR.

### In the Worker

The `PublishPackageCommand` handler, inside the job-tracking handler:

1. Runs as the job's actor, through a `FixedActorAccessor`.
2. Checks, with the same access policy RepoHost uses, that the actor **may still publish** to the
   repository. If not, the job fails and nothing is published.
3. Takes the repository's lock, and under it repeats the architecture and version checks. A package
   that fails either fails the job, with the reason in `Error`.
4. Moves the temporary file into the repository directory, adds it with `repo-add`, and upserts the
   package entity — the behaviour [Package Management API](../package-management-api/packages-api.md)
   specifies for publishing, unchanged.

**Why:**

* [Why publishing leaves the request](#why-publishing-leaves-the-request)
* [Why the request still validates](#why-the-request-still-validates)
* [Why the Worker rechecks permissions](#why-the-worker-rechecks-permissions)
* [Why the job and the message commit together](#why-the-job-and-the-message-commit-together)
* [Why the contract change stays in `v1`](#why-the-contract-change-stays-in-v1)

---

## Repository lock

`IRepositoryDatabaseLock` keeps its contract and gets a new implementation:

* A **Postgres advisory lock**, keyed by the repository's id, replaces the in-process lock.
* It is a **session-level** lock on the connection holding it, so it is released if that process
  dies.
* `AcquireAsync` waits for exclusive access to one repository and honours its cancellation token;
  disposing the handle releases the lock.
* Locks on different repositories never block each other.
* Publish and delete, in either process, take the same lock.

**Why:**

* [Why a Postgres advisory lock](#why-a-postgres-advisory-lock)

---

## Job status

`GET /api/v1/Jobs/{id}` returns:

```json
{ "id": "…", "state": "Failed", "created": "…", "error": "…" }
```

`error` is present only when `state` is `Failed`.

| Caller | Response |
| :--- | :--- |
| the user who started the job | `200` |
| any other signed-in user | `404` |
| anonymous | `401` |

* The rules live in a `JobAccessPolicy` with no DB, HTTP or logging dependencies.
* The service enforces the policy itself, as `RepositoryService` does, not the controller.

**Why:**

* [Why job status is `/Jobs/{id}`](#why-job-status-is-jobsid)

---

## Upload cleanup

The Worker runs a cleanup on a configurable interval. Each run deletes the files in the upload
directory that are:

* older than a configurable minimum age, **and**
* not the `PackageFilePath` of a `Pending` or `Running` job.

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

### 2. Configure Wolverine in RepoHost (#364) — `PATCH`

The Wolverine package reference, configured to publish to RabbitMQ.

*Depends on:* #363.

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

*Depends on:* #382.

**Why:**

* [Why the publishing logic gets a library of its own](#why-the-publishing-logic-gets-a-library-of-its-own)

### 6. Postgres advisory repository lock (#384) — `PATCH`

The implementation [Repository lock](#repository-lock) specifies.

*Acceptance:* tests against a real Postgres show that two lock instances exclude each other on one
repository and not on different ones, and that a cancelled wait gives up without the lock.

*Depends on:* nothing.

**Why:**

* [Why a Postgres advisory lock](#why-a-postgres-advisory-lock)

### 7. Add the `Job` table (#367) — `MINOR`

The entity [Jobs](#jobs) specifies, and its migration.

*Depends on:* nothing.

**Why:**

* [Why the job stores a user id and scopes, not an `Actor`](#why-the-job-stores-a-user-id-and-scopes-not-an-actor)

### 8. Create `PublishPackageCommand` (#369) — `PATCH`

Create `PacmanManager.Commands` and the command, with `RepositoryId` and `PackageFilePath`.

*Depends on:* nothing.

**Why:**

* [Why commands have a data-only project](#why-commands-have-a-data-only-project)

### 9. Job-tracking message handler (#368) — `PATCH`

The generic handler [Jobs](#jobs) specifies.

*Acceptance:* unit tests of every transition in the table.

*Depends on:* #365, #367.

### 10. `PublishPackageCommand` handler (#366) — `MINOR`

The Worker side of [Publishing](#in-the-worker).

*Acceptance:* tests show a revoked permission, an unsupported architecture and a version no longer
newer each fail the job without publishing.

*Depends on:* #365, #368, #369, #383, #384.

**Why:**

* [Why the Worker rechecks permissions](#why-the-worker-rechecks-permissions)

### 11. `GET /api/v1/Jobs/{id}` (#385) — `MINOR`

The route [Job status](#job-status) specifies, its `JobAccessPolicy`, and the regenerated
`openapi/v1.json`.

*Acceptance:* each policy rule is a unit test; an E2E test covers the route.

*Depends on:* #367, #382.

**Why:**

* [Why job status is `/Jobs/{id}`](#why-job-status-is-jobsid)

### 12. Run RabbitMQ and the Worker in the E2E fixture (#386) — `PATCH`

The fixture changes [Test environment](#test-environment) specifies, short of the tests waiting for
jobs.

*Acceptance:* the existing E2E tests pass with the new containers running.

*Depends on:* #365.

**Why:**

* [Why the E2E fixture changes ahead of the endpoint](#why-the-e2e-fixture-changes-ahead-of-the-endpoint)

### 13. Publish endpoint validates and enqueues (#380) — `MAJOR`

The request side of [Publishing](#on-the-request), the regenerated `openapi/v1.json`, and the E2E
tests that publish waiting for their job.

*Constraints:* the version check must never reject a valid version. The route stays in `v1`.

*Depends on:* #364, #366, #367, #369, #385, #386.

**Why:**

* [Why the request still validates](#why-the-request-still-validates)
* [Why the job and the message commit together](#why-the-job-and-the-message-commit-together)
* [Why the contract change stays in `v1`](#why-the-contract-change-stays-in-v1)

### 14. Scheduled upload cleanup (#387) — `PATCH`

The sweep [Upload cleanup](#upload-cleanup) specifies.

*Acceptance:* unit tests show a file with a live job is kept, a file younger than the minimum age is
kept, and an old file with only a finished job, or with none, is deleted.

*Depends on:* #365, #367.

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
* **Listing jobs** — a `GET /api/v1/Jobs` with the usual pagination, filter and sort, under the same
  `JobAccessPolicy`. Additive.
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

### Why the job stores a user id and scopes, not an `Actor`

`Actor` wraps a `User` entity and lives in `PacmanManager.Authentication`; `Job` lives in
`PacmanManager.Entities`, which cannot reference it. Serializing an `Actor` into jsonb would also
copy a `User` row into every job. The user id and scopes are what is needed to rebuild the actor in
the Worker.

### Why the request still validates

Without validation on the request, every mistake — an unreadable file, the wrong architecture, an
old version — would be reported only through a failed job, after the caller had been told `202`.
The common mistakes are cheap to catch on the request and are caught there.

The version check cannot be made exact on the request without holding the repository lock until the
Worker finishes, which would undo the point of going asynchronous. The asymmetry is deliberate:
letting an invalid version through costs a failed job, which the Worker reports properly; rejecting
a valid one would be a wrong answer with no recourse.

### Why the Worker rechecks permissions

A job can wait in the queue. A user whose publish rights are revoked in
the meantime must not have their publish land anyway. Running as the job's actor through
`FixedActorAccessor` means the Worker applies the same policy RepoHost does, which is what
`IActorAccessor` was designed for: a host that is not the web host chooses its actor explicitly.

### Why the job and the message commit together

Writing the job row and then publishing the message, as two steps, leaves two failures to handle: a
job that stays `Pending` with no message to run it, or a message whose job does not exist. A
transactional outbox, such as Wolverine's EF Core integration, makes the two one commit.

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
* **A Postgres advisory lock** uses infrastructure every process already has, waits rather than
  fails, and as a session-level lock is released by Postgres when the holding process dies, so a
  crashed Worker cannot leave a repository locked.

### Why job status is `/Jobs/{id}`

A route under the package, `/Packages/{id}/Jobs/{jobId}`, was considered because it would let the
package's access rules decide who sees the job. But a first publish has no package id when the job
is created — the package row only appears when the Worker upserts it — so that route could not be
the `Location` of the `202`. A repository-scoped route would work but ties the job API to one kind
of job. `/Jobs/{id}` reflects the table directly, and a `JobAccessPolicy` keyed on the job's user is
as simple a rule as any of the others.

### Why cleanup is a scheduled sweep in the Worker

An upload can be abandoned more than one way: its job fails for good, or the request dies between
writing the file and committing the job. Cleaning up on job failure would catch only the first. A
periodic sweep catches both, and the Worker is already the process that owns off-request work. The
minimum age keeps the sweep away from an upload whose job is still being committed.

### Why the E2E fixture changes ahead of the endpoint

#380 changes the publish contract and every E2E test that publishes. Building the harness that runs
the Worker in the same PR would make it the largest in the milestone. Doing the fixture first, while
publishing is still synchronous, proves the harness against tests that already pass.
