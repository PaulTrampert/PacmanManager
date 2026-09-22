# Pacman Controller

Status: **accepted**. This document is the source the implementation issues are cut from. It follows
the conventions established in [`authorization-plan.md`](authorization-plan.md) and
[`packages-api.md`](packages-api.md); where it departs from them, it says so.

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

It is one of three documents that together let a `pacman` client install from a hosted repository:

1. [**User Management**](user-management.md) — reading users, and changing your own display name.
2. [**Basic Auth**](basic-auth.md) — the credentials a `pacman.conf` carries for a private
   repository.
3. **Pacman Controller** (this document) — the repository routes themselves.

It depends on [Basic Auth](basic-auth.md), and not on [User Management](user-management.md).

It closes the first item under [`packages-api.md`'s deferred work](packages-api.md#deferred-work):
the API can publish packages, but nothing can install them, because a `pacman` client configured
with `Server = …` needs the `.db.tar.gz` and every package file resolvable under one base URL by the
basename `repo-add` recorded. There is no such URL today.

## Goals

* A `pacman` client configured with a single `Server = …` line can `pacman -Sy` and
  `pacman -S <package>` from a hosted repository.
* A **public** repository works for an unauthenticated client, with no credentials configured at
  all.
* A **private** repository works for its owner, and is indistinguishable from a repository that does
  not exist to everybody else.
* The routes behave well as HTTP: a `pacman -Sy` that changes nothing transfers nothing, and an
  interrupted package download resumes.

## Non-goals for this work

Recorded here so the issues stay bounded; each has a follow-up in
[Deferred work](#deferred-work).

* Repository and package **signing** (`.db.sig`, `.pkg.tar.zst.sig`). Consumers must configure
  `SigLevel = Optional TrustAll`; see [Signature levels](#signature-levels).
* A browsable **directory index** at the repository root. `pacman` never asks for one.
* **Delta packages** (`.delta`), which pacman removed support for in 5.0 and nothing generates.
* **Writing anything.** Every route under this root is a read, and the credential that reaches them
  [cannot write in the first place](basic-auth.md#authority-is-a-property-of-the-actor-not-of-the-route).
* **Rate limiting** and download quotas.
* **A dispute process for repository names.** Names are first-come, first-served, and nothing
  arbitrates between two people who want the same one. A name released by
  [a rename](#renaming-a-repository) is a different matter and is in scope.

**Why:**

* [squatting, and the absence of a dispute process](#squatting-and-the-absence-of-a-dispute-process)

---

## The route root

```
/pacman/{repoName}/{repoArch}/{fileName}
```

* **It is outside `/api`, and it is not versioned.** The mechanism is **`[ApiVersionNeutral]`** on the
  controller, which takes it out of `VersionByNamespaceConvention` altogether: no version is
  required, none is reported, and the route template carries no version segment.
  `SubstituteApiVersionInUrl` is inert here, since it rewrites a `{version:apiVersion}` token the
  template does not contain. It stays on for everything under `/api`.
* **Swagger does not document these routes.** `ConfigureSwaggerGenOptions` already creates a document
  per API version `Where(desc => desc.ApiVersion != ApiVersion.Neutral)`, so a neutral controller
  belongs to no document. That is the intended outcome rather than an accident of configuration, so
  [issue 4](#4-pacmancontroller--minor) asserts it.
* **The path uses names, not ids**, so that a `pacman.conf` is legible and so that the URL survives a
  `DATA_DIR` rebuild. The mapping from `(repoName, repoArch)` to a repository id, and from a
  requested file name to a stored file, happens in the service layer; see [Resolution](#resolution).
* **There is no owner segment.** See
  [Repository names are globally unique](#repository-names-are-globally-unique).
* **The two segments are exactly pacman's `$repo` and `$arch`** — the section name, and the first
  configured architecture.

```ini
[myrepo]
Server = https://packages.example.com/pacman/$repo/$arch
SigLevel = Optional TrustAll
```

For a private repository the same line carries credentials, which libcurl reads out of the userinfo
and sends as an `Authorization: Basic` header. The username is
[the token's identifier and the password its secret](basic-auth.md#the-identifier-goes-in-the-username-the-secret-in-the-password):

```ini
Server = https://pmt_0199…:pms_kJ8…@packages.example.com/pacman/$repo/$arch
```

**Why:**

* [the route root is outside `/api` and unversioned](#why-the-route-root-is-outside-api-and-unversioned)
* [the root names the client, not the resource](#why-the-root-names-the-client-not-the-resource)

---

## Repository names are globally unique

`PacmanRepository` is unique on `(OwnerId, Name, Architecture)` today. It becomes unique on
**`Name`**: a repository name belongs to exactly one repository across the whole deployment, and
therefore to exactly one owner. Architecture leaves the key entirely — a repository now
[supports a set of architectures](#a-repository-supports-architectures-a-package-has-one) rather
than being one.

The application is not live, so this is an index change with no data to migrate.

**Why:**

* [repository names became globally unique](#why-repository-names-became-globally-unique)
* [the key is `Name` alone](#why-the-key-is-name-alone)

### The trade: a repository's name is public even when the repository is not

Creating a repository whose name is taken has to fail. That failure is observable, so **any caller
can discover whether a name is in use, one guess at a time, including when the repository holding it
is private.**

Two requirements bound it:

* **The `409` must not name the owner**, or say anything else about the repository. "That name is
  taken" is the whole message. Who has it, whether it is public, and when it was created are not
  disclosed.
* **Nothing else changes.** The collision response is the only new signal.
  `GET /api/v1/repositories` still shows only what the caller may see, and a private repository is
  still a `404` by id and by name.

The property a user must be told, and which
[issue 5](#5-document-consuming-a-hosted-repository--patch) must state plainly, is: **a private
repository's name is not a secret. Its existence-as-yours, its contents, its packages and its owner
are.** A user who needs the name itself to be unguessable should choose an unguessable name.

**Why:**

* [a public name is an acceptable trade](#why-a-public-name-is-an-acceptable-trade)

### What changes in the code: the name index

* The unique index on `PacmanRepository` becomes `Name` alone.
* **A separate index on `OwnerId` has to be added.** The composite index served
  `RepositoryFilter.ownerId` as a prefix scan, and no longer starts with it.
* `RepositoryKey` collapses to the name, and `GetRepositoryByNameAsync` takes a `string`. That is a
  breaking change to a public service signature.
* Creating or renaming into a taken name raises `ItemExistsException` → `409`, rather than the
  `DbUpdateException` → `500` that
  [`authorization-plan.md`](authorization-plan.md#known-gaps) records as a known gap.
* [`authorization-plan.md`](authorization-plan.md#looking-a-repository-up-by-name) says a repository
  name on its own identifies nothing, and that a friendlier URL form needs a user-facing name on
  `User`. Both sentences become wrong. The issue updates that section.

---

## A repository supports architectures; a package has one

`PacmanRepository.Architecture` — one string, `x86_64` or `any` — becomes **`SupportedArchitectures`**,
the set of architectures a repository publishes for. Two rules define it:

* **`any` is not something a repository may support.** Only real machine architectures are allowed,
  and for now that set is exactly `x86_64`. What else `aarch64` or `armv7h` would need is
  [deferred](#deferred-work) rather than assumed.
* **An `any` package is published into every supported architecture's database.** It is stored once
  and listed everywhere it applies.

**Why:**

* [`any` is a package property, not a repository one](#why-any-is-a-package-property-not-a-repository-one)

### One database per architecture

`repo-add` produces a database per architecture, and the route's `{repoArch}` segment selects it. A
request naming an architecture the repository does not support is a `404`, decided from
`SupportedArchitectures` before any file system call.

Architecture `x86_64`'s database contains every package in the repository whose `Architecture` is
`x86_64` **or** `any`. Publishing an `any` package therefore runs `repo-add` once per supported
architecture, against the same stored file; publishing an `x86_64` package runs it once.

### The package key gains the architecture

`PacmanPackage` is unique on `(RepositoryId, Name)`, which was correct while a repository was one
architecture and is not any more. The key becomes **`(RepositoryId, Name, Architecture)`**.

An `any` package is one row whose `Architecture` is `any`, one file on disk, and an entry in every
architecture's database. **It cannot coexist with an architecture-specific build of the same name**,
which is the same rule Arch applies and the same rule `repo-add` would enforce for you.

### What changes in the code: architectures

* `PacmanRepository.SupportedArchitectures`, a `string[]` — `text[]` under Npgsql, and a member the
  in-memory provider handles for the service tests. `Architecture` is dropped in the same migration;
  there is no data to preserve.
* The allowed set is a shared constant in `PacmanManager.Entities`
  (`PacmanRepositoryValidationConstants.SupportedArchitectures`), like every other validation limit,
  and the wire model validates each element against it. It must also be **non-empty**: a repository
  that supports nothing can serve nothing.
* `RepositoryFilter.Architecture` can no longer be `[EqualsQuery]`, because the column is now a
  collection. It becomes
  [`[ContainsQuery]`](https://github.com/PaulTrampert/PTrampert.QueryObjects/blob/main/PTrampert.QueryObjects/Attributes/ContainsQueryAttribute.cs),
  which requires the target property to be a collection and builds
  `repository.SupportedArchitectures.Contains(architecture)`, which Npgsql turns into an array
  containment test. It ignores a null value by default, so an unset `architecture` still contributes
  nothing. No `BuildQueryExpression` escape hatch is needed.
* `PacmanPackage`'s unique index becomes `(RepositoryId, Name, Architecture)`.
* Publishing resolves which databases a package belongs in, and `RepositoryDatabase` gains the
  architecture — see [Storage layout](#storage-layout).

---

## Resolution

The `/pacman` routes are served by a new `IPacmanRepoService`. It resolves a path to a file and
**owns no database access of its own**. Resolution composes the existing chokepoints:

1. **Repository** via `IRepositoryService.GetRepositoryByNameAsync(repoName)`, which already applies
   `VisibleTo`. A private repository belonging to somebody else is absent here, so it is a `404`
   without any code in the new service deciding that.
2. **Architecture**, by asking the repository that came back whether `repoArch` is among its
   [`SupportedArchitectures`](#a-repository-supports-architectures-a-package-has-one). This is the
   one check the new service makes itself.
3. **File** — a database file via `IRepositoryService`, or a package file
   [read straight from the repository's directory](#what-file-names-are-served) once its name has
   been validated.

**There is no `PacmanRepoAccessPolicy`, and there must not be one.** `IUserService` is not involved
at all, and an unknown owner is not a case that exists.

**Why:**

* [resolution owns no database access](#why-resolution-owns-no-database-access)

### Case sensitivity

The repository name is matched **exactly as stored**, which is what `GetRepositoryByNameAsync`
already does and what the database's unique index enforces, and the architecture segment is compared
to `SupportedArchitectures` the same way. A request for `/pacman/MyRepo/x86_64/…` against a
repository named `myrepo` is a `404`.

**Why:**

* [names are matched case-sensitively](#why-names-are-matched-case-sensitively)

### Storage layout

Everything belonging to a repository lives under one directory, named by its id:

```
{DATA_DIR}/repositories/{id}/
├── <package basename>.pkg.tar.zst      every package file, stored once
└── db/<arch>/                          repo-add's output for that architecture
    ├── {id}.db.tar.gz
    ├── {id}.files.tar.gz
    ├── {id}.db     → {id}.db.tar.gz    (symlink written by repo-add)
    ├── {id}.files  → {id}.files.tar.gz (symlink written by repo-add)
    └── {id}.db.tar.gz.old, {id}.files.tar.gz.old
```

* This is a move: the databases live in `{DATA_DIR}/libalpm/sync` today, flat and keyed by id.
  **Under this layout deleting a repository is deleting one directory.**
* The file names keep the repository's **id**, not its name, so that a database does not have to be
  renamed when [the repository is](#renaming-a-repository).
* `repo-add`'s working directory becomes `{DATA_DIR}/repositories/{id}/db/{arch}`, so
  `RepositoryDatabase` gains the architecture alongside the id.
* Nothing registers these as libalpm sync databases; `ILibAlpm` is used to read uploaded package
  files, not to read repository databases back.
* An `any` package is stored **once**, in the repository directory, and appears in every supported
  architecture's database. Resolution is by lookup rather than by path, so the same file is served
  under every architecture's URL without a second copy or a symlink.

**Why:**

* [the storage layout is one directory per repository](#why-the-storage-layout-is-one-directory-per-repository)

### What file names are served

Let `{repo}` be the repository's name, `{arch}` the requested architecture and `{id}` the
repository's id. `{db}` abbreviates `{DATA_DIR}/repositories/{id}/db/{arch}`.

| Requested `{fileName}` | Served from | Purpose |
| :--- | :--- | :--- |
| `{repo}.db` | `{db}/{id}.db.tar.gz` | The sync database. `pacman -Sy`. |
| `{repo}.db.tar.gz` | `{db}/{id}.db.tar.gz` | The same bytes under its real name. |
| `{repo}.files` | `{db}/{id}.files.tar.gz` | The files database. `pacman -Fy`. |
| `{repo}.files.tar.gz` | `{db}/{id}.files.tar.gz` | The same bytes under its real name. |
| A package basename | `{DATA_DIR}/repositories/{id}/{fileName}` | The package file, by the basename `repo-add` recorded. |
| Anything else | — | `404`. |

Everything is `application/octet-stream`. A `{repoArch}` the repository does not support is a `404`
before any of this is consulted.

* **The database file name must match the repository name.** A request for
  `/pacman/myrepo/x86_64/other.db` is a `404`, not a redirect and not a served database.
* **`repo-add` writes a files database**, and nothing in this solution currently knows that. Verified
  against pacman `7.1.0.r9.g54d9411-2`: `create_db` and `rotate_db` both loop over
  `for repo in "db" "files"`, so one invocation produces `{id}.db.tar.gz`, `{id}.files.tar.gz`, the
  symlinks `{id}.db` and `{id}.files`, and `.old` backups of each.
  `RepositoryService.GetRepositoryFileName` knows only about `.db.tar.gz`, which has two
  consequences this work has to deal with — the files database has to be servable, and repository
  deletion currently orphans everything except the one file it knows about. See
  [issue 2](#2-one-directory-per-repository-and-the-whole-repo-add-file-set--patch).
* **The symlinks are not used.** `{repo}.db` is served by reading `{id}.db.tar.gz` directly.
* **A package file is served from disk by name, with no row involved.** There is no `PacmanPackage`
  lookup, no index on `FileName`, and no query on the path a client hits once per package installed.
* **A signature request is a `404`, not a `500`.** A `pacman -Sy` against an unsigned repository
  still probes for `{repo}.db.sig`, and a package download probes for the package's `.sig` — both
  observed against pacman 7.1.0.
* **A row without a file is a `404`, not a `500`.** `GetRepositoryFileByIdAsync` calls `OpenRead`
  with no `Exists` check today. The pacman routes check first.
* **Reading while `repo-add` is writing** can see a file briefly absent, because `repo-add` rotates
  by rename. That is answered by the rule above — a missing file is a `404`, and the next
  `pacman -Sy` succeeds — and the read path takes no lock. Closing the window entirely is
  [deferred](#deferred-work) rather than dismissed.

Validation replaces the package lookup, and runs before any file system call:

* **The name must parse as a package file name** — `<name>-<version>-<arch>.pkg.tar.<ext>`, the exact
  shape `IPackagePathResolver.DeriveFileName` composes, checked with the same
  `RegularExpressions.PackageName`, `PackageVersion` and `PackageArchitecture` it validates with.
  Anything else is a `404` without touching the disk.
* **The architecture in the name must match the request** — `{repoArch}`, or `any`.
* **It must still be a plain basename.** `IPackagePathResolver.GetPackageFilePath` already throws
  otherwise, and keeps doing so; the shape check is a narrowing on top of it, not a replacement.
* **Path traversal.** `{fileName}` is a single route segment, so a literal `/` cannot appear in it,
  but `%2F`, `..` and encoded control characters can be attempted. Both defences above are tested.

**The ordering rule.** Both publishing and deleting touch a database file and a package file, and
either can fail between the two:

> **A file on disk that nothing references is acceptable. A database entry whose file is missing is
> not.**

* **Deleting** removes the package from every architecture's database with `repo-remove` and drops
  the row first, and unlinks the file last.
* **Publishing** does the reverse — the file is in place before `repo-add` names it.
* `IRepositoryDatabaseLock` already serializes the writers, so the sequence is not racing anything.

**Why:**

* [a package file is served without a row](#why-a-package-file-is-served-without-a-row)
* [an orphaned file is preferred to a missing one](#why-an-orphaned-file-is-preferred-to-a-missing-one)
* [the symlinks are not used](#why-the-symlinks-are-not-used)
* [the database file name must match the repository name](#why-the-database-file-name-must-match-the-repository-name)

---

## Renaming a repository

A repository's name is in the URL, and the URL is in a `pacman.conf` on a machine we do not
administer. Renames are expected to be rare, so the goal is not to make them cheap. It is to make
them safe, and to make sure a name is not locked up forever afterwards.

### What a rename does

1. **The old name is reserved**, immediately and automatically. Nobody else can claim it, and the
   repository that gave it up can always take it back.
2. **For 30 days the old URL redirects** to the new one, per file:
   `/pacman/custom/x86_64/custom.db` → `/pacman/custom2/x86_64/custom2.db`. The target is resolved
   from the retirement's `RepositoryId` at request time, to **whatever that repository is called
   now** — so a repository renamed twice inside the window redirects both old names straight at the
   current one, with no chain to follow and no second hop.
3. **After that the old URL is `410 Gone`**, and the reservation continues on a sliding window: the
   name is held until 30 days after the *most recent request* for it. A name nobody has asked for in
   30 days is released and can be claimed by anyone.

The redirect is **`307 Temporary Redirect`, with `Cache-Control: no-store`**. The expiry is a `410`
rather than a `404`. Because `pacman` says nothing about either, **the warning has to travel out of
band**: the notification is the web UI, and eventually an email.

**Why:**

* [a rename reserves the old name on a sliding window](#why-a-rename-reserves-the-old-name-on-a-sliding-window)
* [a temporary redirect rather than a permanent one](#why-a-temporary-redirect-rather-than-a-permanent-one)
* [the expiry is `410` rather than `404`](#why-the-expiry-is-410-rather-than-404)

### Recording who is still asking

Each request for a retired name updates `LastRequestedAt`, which is what the sliding hold is
computed from, and records the requesting user when the request was authenticated.

It is a **last**-requester record, not an audit trail: one row per retired name, not one per request.
Emailing *everyone* still configured for a name needs the set rather than the latest member, which is
[deferred](#deferred-work) along with the messaging that would send it.
`LastRequestedAt` is written coarsely, at a configurable resolution, for the reason
[`basic-auth.md`](basic-auth.md#recording-use) gives about `LastUsedAt`. An hour's resolution is
invisible against a 30-day window.

### `RetiredRepositoryName`

| Column | Type | Notes |
| :--- | :--- | :--- |
| `Name` | `string` | Primary key. The retired name, matched exactly as stored. |
| `RepositoryId` | `Guid` | The repository that gave it up. FK, cascade delete — deleting a repository releases the names it once had, since there is nothing left to redirect to. |
| `Repository` | nav | |
| `RetiredAt` | `DateTimeOffset` | |
| `RedirectUntil` | `DateTimeOffset` | `RetiredAt` + 30 days. After this the name answers `410`. |
| `LastRequestedAt` | `DateTimeOffset?` | Coarsely maintained; see above. |
| `LastRequesterId` | `Guid?` | FK to `User`, null for anonymous requests. |

A name is **held** while `now < RedirectUntil` or `now < LastRequestedAt + 30 days`, and released
otherwise. Release is evaluated **lazily**, when somebody tries to claim the name: an expired row is
deleted and the claim succeeds. That avoids needing a scheduler for correctness, and leaves a
recurring sweep as an optimisation rather than a requirement — it is [deferred](#deferred-work).

Both windows are configuration, not literals, with 30 days as the default.

### The rules this adds

* Creating or renaming into a **held** name is the same `409` as colliding with a live repository,
  with the same body. It must not say whether the name is taken by a repository or by a retirement,
  because that would leak the existence of a repository the caller cannot see.
* **The repository that retired a name may always take it back**, which makes an accidental rename
  undoable. Reclaiming deletes that retirement — the redirect it was serving would otherwise point
  the name at itself — and retires the name being vacated in its place.
* Retired names and live names are one namespace as far as a claim is concerned. There is no
  arrangement in which a `409` depends on which of the two kinds holds the name.
* **Removing an architecture** from `SupportedArchitectures` breaks clients on that architecture the
  same way a rename does, and this machinery does not cover it — there is no name to reserve. It is
  an immediate `404` for that architecture, and the UI should say so before the change is made.

---

## HTTP behaviour

All of this applies to every `/pacman` response.

* **Conditional GET.** libalpm sets libcurl's `CURLOPT_TIMECOND` from the local database file's
  modification time, so every `pacman -Sy` sends `If-Modified-Since`. Serve via the
  `File(stream, contentType, fileName, lastModified, entityTag, enableRangeProcessing)` overload:
  `FileResultExecutorBase` evaluates the preconditions and returns `304` itself. **Without
  `Last-Modified` and a `304`, every `pacman -Sy` re-downloads every database in full.**
* **`IFileSystem` gains a last-write-time member.** It exposes `Exists`, `DirectoryExists`, `Delete`,
  `OpenRead`, `OpenWrite`, `Move` and `CreateDirectory` and nothing else today. Reading the time off
  the returned `FileStream` instead would work for the physical implementation and break every
  service test, all of which mock the interface.
* **`ETag`**, cheaply derived from that time and the stream's length, which also answers
  `If-None-Match`. libalpm does not send it, so `Last-Modified` is the load-bearing header.
* **Range requests.** libalpm downloads to `{name}.part` and resumes with
  `CURLOPT_RESUME_FROM_LARGE`, which sends `Range: bytes=N-` and expects `206 Partial Content`.
  `enableRangeProcessing: true` provides it, and works because `IFileSystem.OpenRead` returns a
  seekable `FileStream`.
* **`Content-Length`** follows from a seekable stream, and pacman's `CheckSpace` and progress
  reporting both want it. **Nothing may serve these responses chunked.**
* **`HEAD`** is routed to the `GET` action by ASP.NET automatically and must not stream a body.
* **No `Cache-Control` at all**, deliberately.
* **`Vary: Authorization`** on every response under this root.
* **No `Content-Disposition`.** pacman names the file from the URL it requested.

Note that HTTP dates have one-second resolution and ASP.NET rounds `Last-Modified` down to a whole
second; a test that publishes and immediately re-reads inside the same second will see a `304` where
it expected a `200` unless it accounts for that.

**Why:**

* [there is no `Cache-Control`](#why-there-is-no-cache-control)
* [`Vary: Authorization` stays](#why-vary-authorization-stays)

### Signature levels

Arch's stock `/etc/pacman.conf` sets `SigLevel = Required DatabaseOptional` globally. Because this
work does not sign anything, **a client that does not override `SigLevel` for the repository section
will refuse every package**, with an error about a missing signature rather than anything pointing at
configuration.

`SigLevel = Optional TrustAll` is therefore a **requirement**, not an example, and has to be
documented everywhere the URL is.

**Why:**

* [`SigLevel = Optional TrustAll` is required](#why-siglevel--optional-trustall-is-required)

---

## Implementation plan

One issue per heading. Dependencies are noted; anything without a dependency can start immediately.

### 1a. Raise `ItemExistsException` on repository name collisions — `PATCH`

Raise `ItemExistsException` on the **existing** `(OwnerId, Name, Architecture)` collision, from create
and from update, replacing the `DbUpdateException` → `500` that
[`authorization-plan.md`](authorization-plan.md#known-gaps) records as a known gap. The mapping to
`409` is [a story of its own](authorization-plan.md#itemexistsexception--409--patch), shared with
[Basic Auth](basic-auth.md#5-accesstokenscontroller--minor).

The unique index decides the collision. `RepositoryService` does not check for one before writing:
it translates the index's unique violation into `ItemExistsException`. It identifies the index by
the name the EF model gives it, not by a literal. When
[1c](#1c-the-global-name-index-and-its-migration--major) changes the index's columns, the
translation follows without a change of its own.

*Acceptance:* service tests, run against Postgres because the in-memory provider does not enforce
unique indexes. A colliding create and a colliding update each raise `ItemExistsException`. A
colliding create removes the database file it wrote and no other. An E2E test shows that each
collision produces a `409`. **A test asserts the body names neither
the owner nor anything else about the colliding repository.** The `packages-api.md` deferred bullet
recording the exception as unused is deleted.

*Depends on:* [`ItemExistsException` → `409`](authorization-plan.md#itemexistsexception--409--patch).

**Why:**

* [issue 1a goes first](#why-issue-1a-goes-first)
* [a collision is the index's to report](#why-a-collision-is-the-indexs-to-report)
* [a public name is an acceptable trade](#why-a-public-name-is-an-acceptable-trade)

### 1b. Look a repository up by name alone — `MAJOR`

The service change that follows the index: `RepositoryKey` collapses to a name,
`GetRepositoryByNameAsync` takes a `string`, and this issue makes the correction to
[`authorization-plan.md`](authorization-plan.md#looking-a-repository-up-by-name). Uniqueness itself
belongs to [issue 1c](#1c-the-global-name-index-and-its-migration--major). The index decides it, and
1a's translation reports it, so there is no collision check here to change.

It breaks `IRepositoryService`'s callers, and any client that looks a repository up by owner and
architecture, hence `MAJOR`.

*Acceptance:* service unit tests show that `GetRepositoryByNameAsync` finds a repository by name
alone, and still returns nothing for a private repository the caller may not see. Callers of the old
key are updated.

*Depends on:* 1c. Until the global index exists, two owners may share a name, and a lookup by name
alone could match both.

**Why:**

* [the key is `Name` alone](#why-the-key-is-name-alone)
* [the index lands before the key collapses](#why-the-index-lands-before-the-key-collapses)

### 1c. The global name index and its migration — `MAJOR`

The schema change, which brings the rule with it. The unique index on `PacmanRepository` becomes
`Name` alone. A new non-unique index on `OwnerId` replaces the prefix scan that the composite index
gave `RepositoryFilter.ownerId`. The migration is named `AddIndex_IX_PacmanRepositories_Name`. No
data migration is needed, because the application is not live. 1a translates a violation of
whichever unique index the model declares, so as soon as this lands, a name another owner holds is a
`409` with no service change.

It breaks any existing client, since a name another owner holds could be created before and is now
refused, hence `MAJOR`. Although it is numbered after 1b, it lands first.

*Acceptance:*

* `dotnet ef migrations list` shows the migration. Applying it against the compose Postgres
  succeeds, the old composite index is gone, and both new indexes exist.
* Service tests against Postgres: two users cannot both create `custom`, including when the first
  user's `custom` is private. A rename into a name another owner holds raises
  `ItemExistsException`. A rename to the repository's own current name is not a self-collision.
* An E2E test shows that two users creating `custom` produces a `409` rather than a `500`, and that
  its body discloses nothing about the other repository.
* A test asserts that a private repository is still a `404` by id, and absent from the listing, for
  a non-owner. The collision response is then the only new signal, which is the bound on
  [the disclosure this change accepts](#the-trade-a-repositorys-name-is-public-even-when-the-repository-is-not).
* Existing tests that create same-named repositories under different owners are updated.

*Depends on:* 1a.

**Why:**

* [repository names became globally unique](#why-repository-names-became-globally-unique)
* [the key is `Name` alone](#why-the-key-is-name-alone)
* [a public name is an acceptable trade](#why-a-public-name-is-an-acceptable-trade)
* [the index lands before the key collapses](#why-the-index-lands-before-the-key-collapses)

### 1d. `SupportedArchitectures` — `MAJOR`

[Architecture moves off the key and onto the repository](#a-repository-supports-architectures-a-package-has-one):
`SupportedArchitectures` as a `string[]` with `any` disallowed and the allowed set a shared constant,
`Architecture` dropped, the wire model and `RepositoryFilter` following, `PacmanPackage`'s unique
index becoming `(RepositoryId, Name, Architecture)`, and publishing running `repo-add` once per
architecture a package belongs in — its own for a specific build, all of them for an `any` build.

Migrations: `AddColumn_PacmanRepository_SupportedArchitectures`, which drops `Architecture` in the
same step, and `AddIndex_IX_PacmanPackages_RepositoryId_Name_Architecture`.

*Acceptance:* `dotnet ef migrations list` shows both; applying them against the compose Postgres
succeeds. Validation tests: a repository cannot be created supporting `any`, supporting nothing, or
supporting an unknown string. Service unit tests: an `x86_64` package lands in the `x86_64` database
only; an `any` package lands in every supported architecture's database and is stored once; a
repository may hold `foo`/`x86_64` and `foo`/`aarch64` as two packages, and may not hold `foo`/`any`
alongside either. A filter test for `architecture=` against the collection column, which no longer
translates as an equality.

*Depends on:* 1c.

**Why:**

* [`any` is a package property, not a repository one](#why-any-is-a-package-property-not-a-repository-one)

### 2. One directory per repository, and the whole `repo-add` file set — `PATCH`

`RepositoryService` knows only `{id}.db.tar.gz` in a flat sync directory. `repo-add` also writes
`{id}.files.tar.gz`, the `{id}.db` and `{id}.files` symlinks, and `.old` backups of each — six files
per architecture, five of which repository deletion currently orphans, alongside a package directory
it never touches at all.

Move to [the layout](#storage-layout) where everything a repository owns is under
`{DATA_DIR}/repositories/{id}`, give the file set one owner — `RepositoryDatabase`, now carrying the
architecture too — and make repository deletion **delete the directory**. `IFileSystem` gains a
recursive directory delete; it has none today.

*Acceptance:* unit tests over the file set and the paths the type reports, for one architecture and
for two. An E2E test creates a repository, publishes a package, deletes the repository, and asserts
`{DATA_DIR}/repositories/{id}` no longer exists — which subsumes the six-files assertion and the
package files with it.

*Depends on:* 1d.

**Why:**

* [the storage layout is one directory per repository](#why-the-storage-layout-is-one-directory-per-repository)

### 3. `IPacmanRepoService` — `MINOR`

Path resolution as described: the name lookup through `IRepositoryService`, the architecture check
against `SupportedArchitectures`, file-kind classification, and a return type carrying the stream and
its [modification time](#http-behaviour). A file that is not there resolves to nothing, so the route
answers `404`.

`IFileSystem` gains a **last-write-time** member, and that is the only change it needs.
`IRepositoryService`'s existing `GetRepositoryFileByIdAsync` returns a bare `Stream?`, which cannot
answer a conditional request; it gains a sibling returning the richer type, alongside the files
database it cannot currently name at all.

A package file is [read straight from the repository's directory](#what-file-names-are-served) once
its name has been validated, so this issue adds a **parser** for the package file-name shape — the
inverse of `IPackagePathResolver.DeriveFileName`, over the same regular expressions — and no
database access for packages at all.

*Constraints:* the service must name neither `DbContext` `DbSet`, and there must be no
`PacmanRepoAccessPolicy`. Both are asserted rather than assumed.

*Acceptance:* unit tests for every row of the file-name table, including an unrecognised name, a
`.sig` request, a database name that disagrees with the repository name, an architecture the
repository does not support, a file absent from disk, and traversal attempts (`..`, `%2F`, an
encoded control character). A test that the modification time reaches the return type from
`IFileSystem` rather than from the stream, so a mocked file system is enough to exercise the
conditional-GET path. Parser tests: a well-formed package name resolves; a name with a bad
version, a bad architecture token or no `.pkg.tar.*` suffix is rejected before any file system call,
asserted by the file system mock never being touched; `db` is rejected by the same rule; and a
package whose architecture token disagrees with the requested `{repoArch}` is a `404` unless it is
`any`. Tests that a private repository resolves for its owner and not for anyone else, and that the
name and architecture segments are case-sensitive. An enforcement test asserts the service names
neither `DbContext` `DbSet`.

*Depends on:* 1b, 1d, 2.

**Why:**

* [resolution owns no database access](#why-resolution-owns-no-database-access)
* [a package file is served without a row](#why-a-package-file-is-served-without-a-row)
* [names are matched case-sensitively](#why-names-are-matched-case-sensitively)

### 4. `PacmanController` — `MINOR`

The routes under `/pacman/{repoName}/{repoArch}/{fileName}`, thin as ever, plus the HTTP
behaviour: `Last-Modified`, `ETag`, `304`, `Range`/`206`, `Content-Length`, `Vary: Authorization`,
`application/octet-stream`, and `[ApiVersionNeutral]`. Most of that is one `File(stream, contentType,
fileName, lastModified, entityTag, enableRangeProcessing)` call with the right arguments. The routes
are `[AllowAnonymous]`; they name no scheme, because
[the `Authorization` prefix selects the handler](basic-auth.md#the-authorization-prefix-picks-the-handler)
before any endpoint metadata is consulted.

*Acceptance:* E2E tests fetching the database, the files database and a package from a public
repository anonymously; the same against a private repository with a token, and `404` without one;
a mistyped token producing `401` rather than `404` — the
[`[AllowAnonymous]` trap](basic-auth.md#present-but-invalid-credentials-are-a-401-and-this-is-easy-to-get-wrong),
which this is the first real endpoint able to test; a conditional request returning `304`; a `Range`
request returning `206` with the right bytes; and `Vary: Authorization` present with no
`Cache-Control` on any of them, so that the omission is a decision the tests hold rather than
something a later change drifts back into. A test asserts the routes are absent from every Swagger
document, which `[ApiVersionNeutral]` gives but nothing else pins.

*Depends on:* 3, and [Basic Auth](basic-auth.md#4-the-basic-scheme-and-its-handler--minor).

**Why:**

* [there is no `Cache-Control`](#why-there-is-no-cache-control)
* [`Vary: Authorization` stays](#why-vary-authorization-stays)
* [the route root is outside `/api` and unversioned](#why-the-route-root-is-outside-api-and-unversioned)

### 5. Document consuming a hosted repository — `PATCH`

A `docs/consuming-a-repository.md` with the `pacman.conf` snippet, the `$repo`/`$arch` form, the
`SigLevel = Optional TrustAll` requirement and why it is needed, how to mint a token, how to put
credentials in the `Server` line, and — stated plainly —
[that a repository name is public even when the repository is private](#the-trade-a-repositorys-name-is-public-even-when-the-repository-is-not).
Linked from `README.md` and from this document.

It also has to say [what a rename does to a configured client](#renaming-a-repository): that the old
URL keeps working for 30 days, silently, and then starts failing the whole `pacman -Syu` with a
`410`; and that an owner renaming a repository should expect to tell its users, because `pacman` will
not.

This is the user-facing counterpart to three design documents, and it is the only one of the four a
person configuring a machine will read.

*Depends on:* 4, 7.

**Why:**

* [`SigLevel = Optional TrustAll` is required](#why-siglevel--optional-trustall-is-required)
* [a public name is an acceptable trade](#why-a-public-name-is-an-acceptable-trade)

### 6. End-to-end: a real `pacman` client installs a package — `MINOR`

The test that proves the feature. A Testcontainers `archlinux/archlinux` container, given a
`pacman.conf` pointing at the API on the test network, runs `pacman -Sy` and then
`pacman -S <fixture package>` and asserts the package is installed.

Run it against a public repository anonymously and against a private one with Basic credentials, and
use the `$repo`/`$arch` form rather than a hardcoded URL, so that the test exercises the substitution
the documentation tells users to rely on. Assert that a second `pacman -Sy` with no intervening
publish reports the database as up to date, which is the only test that actually proves the
conditional-GET path end to end.

The fixture package under `test-fixtures/packages` is what gets published; locate it with
`PacmanManager.TestUtils.PackageFixtures` rather than composing the path again. The
[`libalpm` CI job](../AGENTS.md#continuous-integration) already runs an `archlinux/archlinux`
container, so the image and the toolchain are precedented.

*Acceptance:* as described. This is the acceptance criterion for all three documents together —
every other test across them asserts a part, and this one asserts that the parts add up to something
`pacman` can use.

*Depends on:* 4.

### 7. Renaming a repository — `MINOR`

[The retirement machinery](#renaming-a-repository): the `RetiredRepositoryName` entity and its
migration (`AddTable_RetiredRepositoryNames`), a row written on every rename, the held-name check
folded into the same collision path as a live name, the `307` while the redirect is live and the
`410` after it, the coarse `LastRequestedAt`/`LastRequesterId` write, and lazy release when a claim
arrives for a name nothing has asked for in 30 days. Both windows are configuration with a 30-day
default.

*Acceptance:* service unit tests: renaming writes the retirement; claiming a held name is a `409`
whether it is held by a live repository or by a retirement, with the same body either way; the
retiring repository can take its own name back; a name past both windows is claimable and the row is
gone afterwards; a request inside the `410` period pushes the release date out by 30 days.

E2E: a rename, then a fetch of the old database URL returning `307` to the new one and the redirect
being followed to the right bytes; the same URL returning `410` once the redirect window has passed
(with the window configured short for the test); and `Cache-Control: no-store` on the redirect. The
`307` case is worth asserting with a real client — `pacman -Sy` through the redirect must still write
the local database under the *old* section name, which is the behaviour that makes this safe.

*Depends on:* 1b, 4.

**Why:**

* [a rename reserves the old name on a sliding window](#why-a-rename-reserves-the-old-name-on-a-sliding-window)
* [a temporary redirect rather than a permanent one](#why-a-temporary-redirect-rather-than-a-permanent-one)
* [the expiry is `410` rather than `404`](#why-the-expiry-is-410-rather-than-404)

---

## Deferred work

Worth filing as issues, but explicitly out of scope for the work above.

* **Repository and package signing.** A per-repository GPG key, `repo-add --sign`, storing and
  serving `.db.sig` and `.pkg.tar.zst.sig`, and publishing the public key so clients can move from
  `SigLevel = Optional TrustAll` to `Required`. Until this exists the repositories are only as
  trustworthy as the TLS connection serving them, and the `TrustAll` setting is the biggest
  usability wart in the feature.
* **Reserved repository names and administrative transfer**, which is what a deployment open to
  strangers needs before [squatting](#squatting-and-the-absence-of-a-dispute-process) becomes
  somebody's problem.
* **Atomic database writes.** Publishing would copy the current database, modify the copy, and
  rename it over the original, so a reader can never observe the gap `repo-add`'s own rotation
  leaves. It closes [the read-during-write window](#what-file-names-are-served) properly, rather
  than relying on a `404` and the client's next attempt.
* **A recurring reconciliation and cleanup job.** Keeping a database and a filesystem in step is
  hard, and this design currently relies on each write path cleaning up after itself. A periodic
  sweep would reap retired names nothing has asked for, delete repository directories with no row, unlink package files nothing references,
  and rebuild a `.db.tar.gz` that disagrees with the packages table — the last of which is
  [already filed under `packages-api.md`](packages-api.md#deferred-work) and is the same job.
  Because of [the ordering rule](#what-file-names-are-served), this reclaims space rather than
  restoring correctness, which is what makes it deferrable at all.
* **Support for other architectures.** `aarch64` and `armv7h` are the obvious candidates, and the
  work is to find out what beyond the allowed-values list actually needs to change — whether
  `repo-add` behaves, what a multi-architecture repository costs in storage, and how a client on one
  architecture is kept from seeing another's packages.
* **An audit of who is still requesting a retired name**, rather than
  [the last requester alone](#recording-who-is-still-asking), plus the asynchronous messaging that
  would email them. Together they turn a rename from something a user finds out about when their
  updates break into something they are told about while the redirect still works.
* **A directory index** at `/pacman/{name}/{arch}/`. pacman never needs one, but it is the
  first thing a human opens the URL expecting to see.
* **Serving package files through a reverse proxy.** Streaming hundreds of megabytes through Kestrel
  works but occupies a request thread's worth of resources for the duration; `X-Accel-Redirect` or
  `X-Sendfile` would hand it to the proxy. Only worth doing once there is a deployment with a proxy
  in front.
* **A generated repository configuration or installer script.** Nothing yet produces a `repo.conf`
  fragment — section, `Server` line with the token's username and secret, and `SigLevel` — or a script
  that installs one. This is the intended way for users to configure a client, and it is what makes
  [a token being two values](basic-auth.md#format) cost nothing in practice; until it exists,
  [issue 5](#5-document-consuming-a-hosted-repository--patch) documents the manual form.
* **Mirrorlists.** Nothing generates a mirrorlist for a user, which is the obvious convenience once
  more than one host serves the same repositories.
* **Rate limiting and download quotas.** Nothing bounds how often an anonymous caller can pull a
  public repository.
* **Reconciling a repository database from the rows**, already filed under
  [`packages-api.md`](packages-api.md#deferred-work) and made more visible by this work: a
  `.db.tar.gz` that disagrees with the rows is now something a `pacman` client sees directly.

---

## Appendix

The argument for each decision in the plan, including what was considered and rejected. Nothing
here adds a requirement — the plan above is the specification. These entries record what is
**settled**: if implementation suggests a different choice, the entry is the thing to argue with,
in an issue, rather than something to quietly depart from.

### Why the route root is outside `/api` and unversioned

Every other route in this application is `/api/v{version}/…` via `VersionByNamespaceConvention`.
This one is not, because the URL is not ours to version: it goes into a user's `pacman.conf`, on
machines we do not administer, and it has to keep working unchanged for as long as those machines
exist.

There is also nothing to version — the response bodies are the wire formats `repo-add` and `makepkg`
define, not models we control. A future incompatible layout would be a different root, not a `v2` of
this one.

Swagger omitting these routes is the intended outcome rather than an accident of configuration:
these are routes for a package manager, described by `pacman.conf`, not an API surface a human
explores.

### Why the root names the client, not the resource

`/api/v1/repositories/{id}` is the management representation — JSON, versioned, keyed by id.
`/pacman/{name}/{arch}` is the same repository seen through the only lens `pacman` has — tarballs,
unversioned, keyed by the name a client configures. Naming the root after the consumer rather than
the noun is what keeps the two from being read as one API with an inconsistent style, because they
are not: one is ours to design and the other is a wire format `repo-add` and `makepkg` define.

It also leaves `/repositories` unclaimed, which matters for a reason outside this document: a web UI
served from this same host will want the short, obvious paths, and a root reserved for a package
manager is a poor thing to have taken one. Nothing here needs that segment, so nothing here takes
it.

### Why repository names became globally unique

The obvious effect is that the URL loses its owner segment, and with it
[the username on `User`](user-management.md#why-there-is-no-username) that only ever existed to fill
that segment — along with its case-insensitivity rule, its generation and backfill, its reserved-name
list, and the retired-name table that stopped a renamed user's clients silently following whoever
picked up their old name.

The less obvious effect is the one that actually decides it. **A `pacman.conf` section name must be
unique within the file, and `$repo` substitutes that section name into the URL.** Under a per-owner
namespace, Alice and Bob could each own a repository called `custom`, and a user wanting both would
have to rename one section to `custom-bob` — at which point `$repo` expands to `custom-bob` and the
URL points at a repository that does not exist. That user has to abandon `$repo` and hardcode both
URLs, and every document telling them to use `$repo` has just become wrong for them.

A global namespace makes the section name a client is *forced* to choose and the repository name the
server knows the same string, always. The `$repo` form in [The route root](#the-route-root) is
correct for every client rather than for clients that happen not to have collided, and pacman's own
flat namespace — a machine has one set of enabled repositories, with distinct names — is matched
rather than fought.

### Why a public name is an acceptable trade

Making a name globally unique means a collision is observable, and therefore that a name's existence
is discoverable one guess at a time even when the repository holding it is private.

This is a real narrowing of the promise in
[`authorization-plan.md`](authorization-plan.md#authorization-rules), which reports a private
repository a caller may not see as `404` specifically to hide its existence. That still holds
everywhere it is stated — the listing, the get, the packages, the pacman routes — and a private
repository is still absent from every one of them. What is no longer hidden is the *name*.

It is inherent to a global namespace rather than a defect in this implementation: npm, PyPI,
crates.io and Docker Hub all leak name existence the same way, because a namespace that refuses
duplicates cannot avoid saying that a duplicate is what you have. Accepting it is the price of the
`$repo` property above.

### Squatting, and the absence of a dispute process

First-come, first-served. One user taking `core` or `aur` denies it to everyone else for as long as
they keep the repository, and nothing here arbitrates that.

What the design does avoid is a name being locked up by a repository that no longer exists under it:
[a renamed-away name is released](#renaming-a-repository) once nothing is asking for it any more.
That is the same concern one step on — a valuable name should not be held forever by an accident of
history — and it is the part worth solving, because it is the part that happens without anybody
choosing it.

Deliberate squatting is acceptable for what this application is: a self-hosted repository host whose
users are colleagues rather than strangers. It would not be acceptable for a public multi-tenant
service, which needs a reserved-name list and an administrative transfer; both are
[deferred](#deferred-work).

### Why the key is `Name` alone

An earlier draft kept the pair `(Name, Architecture)`, which had one consequence nobody wanted: a
*name* would not belong to a person. Alice could hold `custom`/`x86_64` while Bob held `custom`/`any`.
No single client would see both, since `$arch` expands to the machine's architecture and never to
`any` — but "the name `custom`" belonging to nobody in particular is not a mental model people have
about a global namespace, and it is a plausible source of surprise.

Moving architecture onto the repository as a *set* removes the question rather than answering it.
One repository named `custom` supports whichever architectures its owner publishes for, so there is
nothing left to split ownership along, and the index is on `Name` because that is now the whole
identity.

### Why `any` is a package property, not a repository one

This matches what Arch itself does, which is worth stating because it is easy to assume otherwise.
Verified against the official mirrors: `arch=any` packages are served out of the **`x86_64`**
directory — `core/os/x86_64/licenses-20240728-1-any.pkg.tar.zst` sits alongside
`core/os/x86_64/bash-5.3.15-1-x86_64.pkg.tar.zst` — and there is no `os/any` directory anywhere in
the tree. Arch Linux ARM and Arch Linux 32 do the same thing under their own architectures.

`any` describes a *package*, never a repository or a path segment, and `pacman` cannot ask for one
anyway: `$arch` expands to the machine's architecture and never to `any`.

So the awkward case the earlier draft had to document — a repository whose architecture is `any`,
unreachable through the `$repo/$arch` form the documentation tells everyone to use, needing a
hardcoded URL — stops existing. Every repository is addressable by substitution, for every client.

### Why resolution owns no database access

[`authorization-plan.md`](authorization-plan.md#why-this-is-hard-to-get-wrong) and
[`packages-api.md`](packages-api.md#where-it-is-enforced) both rest on there being exactly one method
in the solution that touches `DbContext.PacmanRepositories` and exactly one that touches
`DbContext.PacmanPackages`, each asserted by an enforcement test. A third service reaching into
either `DbSet` would be a second copy of the visibility rule to keep in step — precisely what that
structure exists to prevent.

Composing the existing chokepoints means every authorization outcome in this feature falls out of
methods that were already enforcing it, and `IPacmanRepoService` contains no rule of its own to get
wrong. A `PacmanRepoAccessPolicy` would be a second statement of the repository visibility rule,
which is the thing the existing design goes out of its way to avoid.

Dropping the owner segment removed a step from the list. There is no username to resolve, so
`IUserService` is not involved at all and an unknown owner is not a case that exists.

### Why names are matched case-sensitively

Repository names are already case-sensitive everywhere else in this API, and pacman's `$repo`
substitution reproduces the section name verbatim, so a client using the documented form is exact by
construction.

Note that this makes `myrepo` and `MyRepo` two different repositories that may be owned by two
different people — which is a mild wart of a case-sensitive global namespace, and the one argument
for folding case that a future change might act on.

### Why the storage layout is one directory per repository

The databases live in `{DATA_DIR}/libalpm/sync` today, flat and keyed by id, which is why
`RepositoryService.DeleteRepositoryAsync` has to know their names in order to clean up, and why it
currently misses five of the six files. Under one directory per repository, deleting a repository is
deleting one directory, and there is nothing left to enumerate or forget.

Keeping the id in the file names inside it is deliberate even though the directory now disambiguates:
a database named for the repository's id does not have to be renamed when
[the repository is](#renaming-a-repository).

The old path was shaped like a libalpm `dbpath` for tidiness rather than because anything required
it — nothing in RepoHost registers these as libalpm sync databases — so moving it costs nothing.

### Why a package file is served without a row

The filesystem and the database file **agree by construction**. `repo-add` generates the `.db.tar.gz`
from the package files themselves, and a client only ever asks for a basename it read out of that
database as `%FILENAME%`. The two cannot disagree about what exists, because one is derived from the
other. The `PacmanPackage` rows are the management API's view of a repository — what it can list,
filter and describe — not the client's index of it.

Validation replaces the lookup and is stricter than one: the shape check admits no separator at all,
covers `.sig` requests and the `db` subdirectory, and runs before any file system call.

The trade is worth it: a hot path with no query, one less index and one less migration, and an
invariant a reader can check by listing a directory.

### Why the database file name must match the repository name

Serving the database under whatever `*.db` name was asked for would mean two names for one resource
and a `pacman.conf` whose section name is silently meaningless. Since `$repo` *is* the section name,
and the namespace is now global, a correctly configured client is exact by construction and the
constraint costs it nothing.

### Why an orphaned file is preferred to a missing one

An orphaned file costs disk space and is invisible — nothing lists it, and a client never asks for
it, because the only names a client knows come out of the `.db.tar.gz`. A database entry pointing at
a file that is gone is the opposite: every client that syncs learns the package exists, asks for it,
and gets a failure that aborts the whole transaction rather than just that package.

The ordering of both write paths follows from that preference, and it is what makes the
[reconciliation sweep](#deferred-work) the thing that reclaims space rather than the thing that
preserves correctness — it can run monthly, or never, without a client noticing.

### Why the symlinks are not used

Depending on `repo-add`'s own rotation symlinks would mean following a symlink out of a directory the
application writes to, for no benefit — the bytes are identical to the `.tar.gz` they point at.

### Why a rename reserves the old name on a sliding window

Renaming breaks clients, and under a global namespace it can break them *silently*: the moment
`custom` is free, somebody else may take it, and Alice's un-updated clients start syncing from Bob's
repository. That is precisely the failure
[dropping the username](user-management.md#why-there-is-no-username) was supposed to have removed,
reappearing one level up.

The sliding hold is the part that earns its complexity. A fixed expiry either releases a name while
somebody's `pacman.conf` still points at it — the silent-stranger failure — or holds every name
forever, which is unacceptable if this is ever run for more than one household. Keying the release on
"nothing has asked for a month" ties it to the only evidence that actually matters: whether any
client is still configured for it.

### Why a temporary redirect rather than a permanent one

Not `301`: a permanent redirect is
[cacheable by default](https://www.rfc-editor.org/rfc/rfc9110#name-301-moved-permanently) with no
specified lifetime, and browsers in practice keep one until the user clears their cache. This
redirect expires in 30 days by design, so it must be one that nothing is entitled to remember. `302`
would behave the same here — every route under this root is a `GET` — and `307` is preferred only
because it says what it means without relying on that.

Three things about how `pacman` treats it, verified against pacman 7.1.0 rather than assumed:

* **The redirect is silent.** libalpm follows it through libcurl and prints nothing; there is no
  warning, and no configuration that produces one. `--debug` shows the requested URL and nothing
  about the hop.
* **A different basename at the target is fine.** Redirecting `custom.db` to `custom2.db` works, and
  the client still writes its local database as `custom.db`, named from the URL it asked for. The
  [database-name rule](#what-file-names-are-served) is satisfied at both ends: the old name serves a
  redirect, the new name serves a file named after itself.
* **Nothing caches it.** libcurl has no HTTP cache, so the redirect is re-followed on every
  transaction and the 30-day expiry is exact.

### Why the expiry is `410` rather than `404`

**The status code reaches the user, the reason phrase does not.** A failed database fetch prints
`error: failed retrieving file 'custom.db' from … : The requested URL returned error: 410`. The
number is the entire message we control — sending `410 Gone` with a helpful phrase gets the phrase
dropped by curl. It is the only signal a user actually sees, and it distinguishes "this name was
retired" from "you typed it wrong".

It is also why the warning has to travel out of band. There is no way to make `pacman` say anything.

One more consequence worth knowing before choosing the window: a database that fails to download
aborts the *whole* transaction — `error: failed to synchronize all databases` — so once the redirect
lapses, that user's `pacman -Syu` stops working entirely, not just for this repository. Thirty days
of redirect is the grace period for that.

### Why there is no `Cache-Control`

The client these routes exist for is not an HTTP cache and does not behave like one: `pacman` keeps
packages in `/var/cache/pacman/pkg` and databases under `/var/lib/pacman/sync`, decides for itself
what to re-fetch, and never re-requests a package file it already holds. A long `max-age` on packages
would therefore be advice to intermediaries that nothing in the intended deployment has, in exchange
for a policy to get wrong; and the database's freshness is already handled properly by the
conditional GET, which is a stronger guarantee than any `max-age` and does not go stale.

Omitting the header is also safe for a private repository, which is the case worth checking. Those
responses are only ever produced for a request carrying an `Authorization` header, and
[RFC 9111 §3.5](https://www.rfc-editor.org/rfc/rfc9111#section-3.5) already forbids a shared cache
from using such a response to satisfy any later request unless the response explicitly opts in with
`public`, `s-maxage` or `must-revalidate`. Sending none of those is the opt-out.

If a deployment later puts a CDN in front of this — the point at which intermediaries stop being
hypothetical — the headers come back as a considered decision rather than a default nobody chose.

### Why `Vary: Authorization` stays

It survives the absence of `Cache-Control` because it is not a caching policy but a correctness fact
about the resource: the same URL produces a `404` for an anonymous caller and a `200` for an
authenticated one, and anything that does cache must not confuse the two.

### Why `SigLevel = Optional TrustAll` is required

Arch's stock `/etc/pacman.conf` sets `SigLevel = Required DatabaseOptional` globally — verified on
this repository's own reference machine. Because this work does not sign anything, a client that does
not override it will refuse every package, with an error about a missing signature rather than
anything pointing at configuration.

That makes the `pacman.conf` snippet in [The route root](#the-route-root) not an example but a
requirement. Signing is the fix and is [deferred](#deferred-work); until then, the setting is the
feature's single largest usability trap and the reason
[issue 5](#5-document-consuming-a-hosted-repository--patch) exists.

### Why issue 1a goes first

It goes first rather than last for a sequencing reason: once names are global, collisions stop being
an edge case and become something users hit routinely. Landing the index first would mean shipping a
period where the routine outcome is a `500`. It also closes a standing gap on its own merits — which
is what finally makes the `DbUpdateException` → `500` worth fixing.

### Why a collision is the index's to report

1a first shipped with a check before the write: a query through `VisibleAsync` for a repository that
already held the key. During 1a's review, with project-owner sign-off, that check was replaced by
translating the index's own unique violation. There were three reasons:

* **The check raced.** Two concurrent writes of one name could both pass it. The loser then got the
  `DbUpdateException` and `500` that 1a exists to remove. The index cannot race.
* **It would not have survived global names.** Once a name is global, a check has to see
  repositories the caller cannot see. That needs a second path to `PacmanRepositories`, one that
  bypasses `VisibleAsync`, which `RepositoryServiceEnforcementTests` forbids. The translation reads
  nothing.
* **One source of truth.** The rule lives in the index, not in a predicate that has to be kept in
  step with it.

The change has two costs. The in-memory provider does not enforce unique indexes, so collision tests
run against Postgres. And a colliding create now runs `repo-add` before the database refuses the
row. Database files are named for the new repository's id, never its name, so the create's existing
cleanup removes that file and nothing else.

### Why the index lands before the key collapses

Uniqueness moves in one step, in the database. 1a reports a violation of whichever unique index the
model declares. Once 1c makes that index `Name` alone, a name another owner holds is a `409` with no
service change. 1b cannot go first: without the global index, two owners may still share a name, and
a lookup by name alone could match both.

Keeping the two issues apart still keeps each review to one kind of change. 1c is an index, a
migration and the collision tests, reviewed against the database. 1b is a signature change and its
callers, reviewed as code.

The plan originally landed 1b first. Under that order, `RepositoryService` would have checked `Name`
against every repository, visible or not, before the database agreed. The point was that the global
index would never arrive while the service still checked the old key, which would have turned a
cross-owner collision into a `500`. Replacing that check with the index's own verdict (see
[a collision is the index's to report](#why-a-collision-is-the-indexs-to-report)) removed the
reason for that order.
