# Pacman Controller

Status: **design**. This document is the source the implementation issues are cut from. It follows
the conventions established in [`authorization-plan.md`](authorization-plan.md) and
[`packages-api.md`](packages-api.md); where it departs from them, it says so.

It is one of three documents that together let a `pacman` client install from a hosted repository:

1. [**User Management**](user-management.md) — reading users, and changing your own display name.
2. [**Basic Auth**](basic-auth.md) — the credentials a `pacman.conf` carries for a private
   repository.
3. **Pacman Controller** (this document) — the repository routes themselves.

It depends on [Basic Auth](basic-auth.md), and not on
[User Management](user-management.md).

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
  [cannot write in the first place](basic-auth.md#read-only-is-a-property-of-the-actor-not-of-the-route).
* **Rate limiting** and download quotas.
* **Reclaiming a repository name.** Names are first-come, first-served and there is no dispute
  process; see [Squatting](#squatting-and-the-absence-of-a-dispute-process).

---

## The route root

```
/repositories/{repoName}/{repoArch}/{fileName}
```

Four things about this shape are deliberate.

**It is outside `/api`, and it is not versioned.** Every other route in this application is
`/api/v{version}/…` via `VersionByNamespaceConvention`. This one is not, because the URL is not ours
to version: it goes into a user's `pacman.conf`, on machines we do not administer, and it has to keep
working unchanged for as long as those machines exist. There is also nothing to version — the
response bodies are the wire formats `repo-add` and `makepkg` define, not models we control. A
future incompatible layout would be a different root, not a `v2` of this one.

**The path uses names, not ids**, so that a `pacman.conf` is legible and so that the URL survives
a `DATA_DIR` rebuild. The mapping from `(repoName, repoArch)` to a repository id, and from a
requested file name to a stored file, happens in the service layer; see [Resolution](#resolution).

**There is no owner segment**, which is the change that makes the rest of this design small. See
[Repository names are globally unique](#repository-names-are-globally-unique).

**It lines up with pacman's own variable substitution.** pacman defines `$repo` as the section name
and `$arch` as the first configured architecture, so the two segments are exactly what a client
writes as `$repo/$arch`:

```ini
[myrepo]
Server = https://packages.example.com/repositories/$repo/$arch
SigLevel = Optional TrustAll
```

For a private repository the same line carries credentials, which libcurl reads out of the userinfo
and sends as an `Authorization: Basic` header. The username is a
[self-describing placeholder and is not validated](basic-auth.md#the-token-goes-in-the-password-field-and-the-username-is-ignored):

```ini
Server = https://token:pmt_0199…_kJ8…@packages.example.com/repositories/$repo/$arch
```

**Two roots name the same resource, and that is intentional.** `/api/v1/repositories/{id}` is the
management representation — JSON, versioned, keyed by id. `/repositories/{name}/{arch}` is the
pacman representation — tarballs, unversioned, keyed by the name a client configures. The shared
noun says they are the same thing; the `/api/v1` prefix says which representation you are asking
for.

---

## Repository names are globally unique

`PacmanRepository` is unique on `(OwnerId, Name, Architecture)` today. It becomes unique on
**`(Name, Architecture)`**: a repository name belongs to one repository per architecture across the
whole deployment, not to one per owner.

The application is not live, so this is an index change with no data to migrate.

### Why, beyond dropping a path segment

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

### The trade: a repository's name is public even when the repository is not

Creating a repository whose name is taken has to fail. That failure is observable, so **any caller
can discover whether a name is in use, one guess at a time, including when the repository holding it
is private.**

This is a real narrowing of the promise in
[`authorization-plan.md`](authorization-plan.md#authorization-rules), which reports a private
repository a caller may not see as `404` specifically to hide its existence. That still holds
everywhere it is stated — the listing, the get, the packages, the pacman routes — and a private
repository is still absent from every one of them. What is no longer hidden is the *name*.

It is inherent to a global namespace rather than a defect in this implementation: npm, PyPI,
crates.io and Docker Hub all leak name existence the same way, because a namespace that refuses
duplicates cannot avoid saying that a duplicate is what you have. Accepting it is the price of the
`$repo` property above.

Two things bound it, and both are requirements rather than observations:

* **The `409` must not name the owner**, or say anything else about the repository. "That name is
  taken" is the whole message. Who has it, whether it is public, and when it was created are not
  disclosed.
* **Nothing else changes.** The collision response is the only new signal. `GET /api/v1/repositories`
  still shows only what the caller may see, and a private repository is still a `404` by id and by
  name.

So the property a user should be told, and which
[issue 5](#5-document-consuming-a-hosted-repository--patch) must state plainly, is: **a private
repository's name is not a secret. Its existence-as-yours, its contents, its packages and its owner
are.** A user who needs the name itself to be unguessable should choose an unguessable name.

### Squatting, and the absence of a dispute process

First-come, first-served, with no reclamation. One user taking `core` or `aur` denies it to everyone
else permanently, and nothing here changes that.

This is acceptable for what this application is — a self-hosted private repository host, where a
deployment's users are colleagues rather than strangers — and would not be acceptable for a public
multi-tenant service. A deployment that grows into the second case needs a reserved-name list and an
administrative transfer, which is [deferred](#deferred-work).

### Open question: `(Name, Architecture)` or `Name` alone

**This is deliberately not settled, and does not need to be to start the work.** The application is
pre-release, so the index can be narrowed later by an issue of its own; what follows is the
reasoning, and the position the first implementation takes.

The pair is the key for now, so one user may still own both an `x86_64` and an `any` repository
called `custom` — the capability
[`authorization-plan.md`](authorization-plan.md#looking-a-repository-up-by-name) documents today, and
the reason the route needs an architecture segment at all.

The consequence, and the reason this is a question rather than a decision, is that a *name* is then
not owned by a person: Alice may hold `custom`/`x86_64` while Bob holds `custom`/`any`. In practice
no client sees both, because `$arch` expands to the machine's architecture and never to `any`, so a
given machine consistently resolves `[custom]` to one of them. But "the name `custom`" belonging to
nobody in particular is a mental model most people do not have about a global namespace, and it is a
plausible source of surprise.

The two positions:

* **`(Name, Architecture)` unique** — what [issue 1](#1-globally-unique-repository-names--major)
  implements. Keeps multi-architecture repositories under one name; accepts split ownership of a
  name.
* **`Name` unique** — a name belongs to exactly one repository and therefore one owner. Removes the
  surprise; costs the multi-architecture capability, since a user could then have `custom` for
  `x86_64` *or* `any` but not both.

Choosing the pair first is the reversible direction: narrowing `(Name, Architecture)` to `Name`
later is an index change and a migration, while widening the other way would have to invent an
architecture for rows that never had one. Nothing else in this document depends on which is chosen —
the route carries both segments regardless.

### What changes in the code

* The unique index on `PacmanRepository` becomes `(Name, Architecture)`.
* **A separate index on `OwnerId` has to be added.** The composite index served
  `RepositoryFilter.ownerId` as a prefix scan, and no longer starts with it.
* `RepositoryKey` loses `OwnerId` and becomes `(Name, Architecture)`, and
  `GetRepositoryByNameAsync` with it. That is a breaking change to a public service signature.
* Creating or renaming into a taken name raises `ItemExistsException` → `409`, rather than the
  `DbUpdateException` → `500` that
  [`authorization-plan.md`](authorization-plan.md#known-gaps) records as a known gap. Global
  uniqueness turns that from a rare edge case into something a user will hit routinely, which is
  what finally makes it worth fixing.
* [`authorization-plan.md`](authorization-plan.md#looking-a-repository-up-by-name) says a repository
  name on its own identifies nothing, and that a friendlier URL form needs a user-facing name on
  `User`. Both sentences become wrong. The issue updates that section.

---

## Resolution

The `/repositories` routes are served by a new `IPacmanRepoService`. It resolves a path to a file and
**owns no database access of its own**, which is the design decision worth stating first, because it
is the question a reader asks immediately.

[`authorization-plan.md`](authorization-plan.md#why-this-is-hard-to-get-wrong) and
[`packages-api.md`](packages-api.md#where-it-is-enforced) both rest on there being exactly one method
in the solution that touches `DbContext.PacmanRepositories` and exactly one that touches
`DbContext.PacmanPackages`, each asserted by an enforcement test. A third service reaching into
either `DbSet` would be a second copy of the visibility rule to keep in step — precisely what that
structure exists to prevent.

So resolution composes the existing chokepoints:

1. **Repository** via `IRepositoryService.GetRepositoryByNameAsync(new RepositoryKey(repoName,
   repoArch))`, which already applies `VisibleTo`. A private repository belonging to somebody else is
   absent here, so it is a `404` without any code in the new service deciding that.
2. **File** via `IRepositoryService` for a database file, or `IPackageService` for a package file.

Every authorization outcome in this feature therefore falls out of methods that were already
enforcing it, and `IPacmanRepoService` contains no rule of its own to get wrong. **There is no
`PacmanRepoAccessPolicy`, and there must not be one** — a policy here would be a second statement of
the repository visibility rule, which is the thing the existing design goes out of its way to avoid.

Dropping the owner segment removed a step from this list. There is no username to resolve, so
`IUserService` is not involved at all and an unknown owner is not a case that exists.

### Case sensitivity

The repository name and the architecture are matched **exactly as stored**, which is what
`GetRepositoryByNameAsync` already does and what the database's unique index already enforces. A
request for `/repositories/MyRepo/x86_64/…` against a repository named `myrepo` is a `404`.

Repository names are already case-sensitive everywhere else in this API, and pacman's `$repo`
substitution reproduces the section name verbatim, so a client using the documented form is exact by
construction. Note that this makes `myrepo` and `MyRepo` two different repositories that may be owned
by two different people — which is a mild wart of a case-sensitive global namespace, and the one
argument for folding case that a future change might act on.

### What file names are served

Let `{repo}` be the repository's name and `{id}` its id. The sync directory is
`{DATA_DIR}/libalpm/sync`.

| Requested `{fileName}` | Served from | Purpose |
| :--- | :--- | :--- |
| `{repo}.db` | `{id}.db.tar.gz` | The sync database. `pacman -Sy`. |
| `{repo}.db.tar.gz` | `{id}.db.tar.gz` | The same bytes under its real name. |
| `{repo}.files` | `{id}.files.tar.gz` | The files database. `pacman -Fy`. |
| `{repo}.files.tar.gz` | `{id}.files.tar.gz` | The same bytes under its real name. |
| A package basename | `{DATA_DIR}/repositories/{id}/{fileName}` | The package file, by the basename `repo-add` recorded. |
| Anything else | — | `404`. |

Everything is `application/octet-stream`.

**The database file name must match the repository name.** `pacman` requests its database as
`{Server}/{sectionName}.db`, and the path segment is `{repoName}`; a request for
`/repositories/myrepo/x86_64/other.db` is a `404`, not a redirect and not a served database. Serving
the database under whatever `*.db` name was asked for would mean two names for one resource and a
`pacman.conf` whose section name is silently meaningless. Since `$repo` *is* the section name, and
the namespace is now global, a correctly configured client is exact by construction and the
constraint costs it nothing.

**`repo-add` writes a files database, and nothing in this solution currently knows that.** Verified
against pacman `7.1.0.r9.g54d9411-2`: `create_db` and `rotate_db` in `/usr/bin/repo-add` both loop
over `for repo in "db" "files"`, so a single `repo-add` invocation produces `{id}.db.tar.gz`,
`{id}.files.tar.gz`, the symlinks `{id}.db` and `{id}.files` pointing at them, and `.old` backups of
each. `RepositoryService.GetRepositoryFileName` knows only about `.db.tar.gz`, which has two
consequences this work has to deal with — the files database has to be servable, and repository
deletion currently orphans everything except the one file it knows about. See
[issue 2](#2-account-for-the-whole-repo-add-file-set--patch).

**The symlinks are not used.** `{repo}.db` is served by reading `{id}.db.tar.gz` directly. The
symlinks are an artefact of `repo-add`'s own rotation and depending on them would mean following a
symlink out of a directory the application writes to, for no benefit — the bytes are identical.

**A package file name is resolved by lookup, never by parsing.** The client asks for exactly the
basename recorded as `%FILENAME%` in the database, so the service matches `PacmanPackage.FileName`
within the resolved repository. It does not decompose the name into `(name, version, architecture)`
and it does not build a path from the request: `IPackagePathResolver.GetPackageFilePath` composes the
path from the repository id and the **stored** basename, and already throws on anything that is not a
plain basename.

This needs an index. `PacmanPackage` is unique on `(RepositoryId, Name)`, but `FileName` is not
indexed at all, and this lookup runs once per package installed. Add
`[Index(nameof(RepositoryId), nameof(FileName))]`.

**A signature request is a `404`, not a `500`.** With `SigLevel` configured as documented pacman does
not ask for `.sig` files, but a client configured otherwise will, and it must get a clean answer.

**Path traversal.** `{fileName}` is a single route segment, so a literal `/` cannot appear in it, but
`%2F`, `..` and encoded control characters can be attempted. The name is validated as a plain
basename before it reaches any file system call, and the lookup-not-parse rule above means an
unrecognised name has no row and therefore no path. Both defences are tested.

---

## HTTP behaviour

`pacman` is an HTTP client with specific expectations, and meeting them is the difference between a
repository that works and one that works *well*. All of this applies to every `/repositories`
response.

**Conditional GET.** libalpm sets libcurl's `CURLOPT_TIMECOND` from the local database file's
modification time, so every `pacman -Sy` sends `If-Modified-Since`. Without a `Last-Modified`
response header and a `304` on a match, **every `pacman -Sy` re-downloads every database in full**.
Serving it via the `File(stream, contentType, fileName, lastModified, entityTag,
enableRangeProcessing)` overload gets this from the framework: `FileResultExecutorBase` evaluates the
preconditions and returns `304` itself.

Supply an `ETag` as well, cheaply derived from the file's length and modification ticks, which also
answers `If-None-Match`. Note that HTTP dates have one-second resolution and ASP.NET rounds
`Last-Modified` down to a whole second; a test that publishes and immediately re-reads inside the
same second will see a `304` where it expected a `200` unless it accounts for that.

**Range requests.** libalpm downloads a package to `{name}.part` and resumes an interrupted transfer
with `CURLOPT_RESUME_FROM_LARGE`, which sends `Range: bytes=N-` and expects `206 Partial Content`.
`enableRangeProcessing: true` on the same overload provides it, and works because `IFileSystem.OpenRead`
returns a seekable `FileStream`.

**`Content-Length`.** Follows from a seekable stream, and pacman's `CheckSpace` and progress
reporting both want it. Nothing may serve these responses chunked.

**`HEAD`.** Routed to the `GET` action by ASP.NET automatically. It must not stream a body, which the
framework also handles.

**Cache-Control, and this one is a security requirement.** A private repository's database and
packages are served on the strength of an `Authorization` header, so any intermediate cache that
stored them would serve them to the next caller without one:

* Private repository: `Cache-Control: private, no-store`.
* Public repository: `Cache-Control: public, max-age=…` is safe and desirable.

**`Vary: Authorization`** on every response under this root, for the same reason: the same URL can
produce a `404` for an anonymous caller and a `200` for an authenticated one.

**No `Content-Disposition`.** pacman names the file from the URL it requested. Setting a download
name is harmless but pointless here, and omitting it keeps the response minimal.

### Signature levels

Arch's stock `/etc/pacman.conf` sets `SigLevel = Required DatabaseOptional` globally — verified on
this repository's own reference machine. Because this work does not sign anything, **a client that
does not override `SigLevel` for the repository section will refuse every package**, with an error
about a missing signature rather than anything pointing at configuration.

That makes the `pacman.conf` snippet in [The route root](#the-route-root) not an example but a
requirement, and `SigLevel = Optional TrustAll` has to be documented everywhere the URL is. Signing
is the fix and is [deferred](#deferred-work); until then, the setting is the feature's single largest
usability trap and the reason [issue 5](#5-document-consuming-a-hosted-repository--patch) exists.

---

## Implementation plan

One issue per heading. Dependencies are noted; anything without a dependency can start immediately.

### 1. Globally unique repository names — `MAJOR`

The index change and everything that follows from it: `(Name, Architecture)` unique, a new index on
`OwnerId`, `RepositoryKey` and `GetRepositoryByNameAsync` losing their owner, `ItemExistsException`
raised on a collision from both create and update, the `409` arm on
`AuthorizationExceptionHandler` if [Basic Auth](basic-auth.md#5-accesstokenscontroller--minor) has
not already added it, a migration named `AddIndex_IX_PacmanRepositories_Name_Architecture`, and the
correction to
[`authorization-plan.md`](authorization-plan.md#looking-a-repository-up-by-name).

Breaking for any existing client and for `IRepositoryService`'s callers, hence `MAJOR`. No data
migration is needed — the application is not live.

*Acceptance:* `dotnet ef migrations list` shows the migration; applying it against the compose
Postgres succeeds and the old composite index is gone. Service unit tests: two users cannot both
create `custom`/`x86_64`; one user can still own `custom`/`x86_64` and `custom`/`any`; a rename into
a taken name is a `409`; a rename to the repository's own current name is a no-op rather than a
self-collision. **A test asserts the `409` body names neither the owner nor anything else about the
colliding repository** — the bound on
[the disclosure this change accepts](#the-trade-a-repositorys-name-is-public-even-when-the-repository-is-not).
A test asserts a private repository is still a `404` by id and absent from the listing for a
non-owner, so the collision response is the only new signal. Existing tests that create same-named
repositories under different owners are updated.

*Depends on:* nothing.

### 2. Account for the whole `repo-add` file set — `PATCH`

`RepositoryService` knows only `{id}.db.tar.gz`. `repo-add` also writes `{id}.files.tar.gz`, the
`{id}.db` and `{id}.files` symlinks, and `.old` backups of each. Give the file set one owner — the
natural home is `RepositoryDatabase`, which already exists so that the database file name and the
tools' working directory are defined once — and make repository deletion remove all of it.

*Acceptance:* unit tests over the file set the type reports. An E2E test creates a repository,
publishes a package, deletes the repository, and asserts the sync directory holds none of the six
files afterwards.

*Depends on:* nothing.

### 3. `IPacmanRepoService` — `MINOR`

Path resolution as described: the `RepositoryKey` lookup through `IRepositoryService`, file-kind
classification, `IPackageService.GetPackageContentByFileNameAsync`, the `(RepositoryId, FileName)`
index, and the return type carrying the stream, length and modification time that the conditional-GET
and range handling need.

`IRepositoryService`'s existing `GetRepositoryFileByIdAsync` returns a bare `Stream?`, which cannot
answer a conditional request. It gains a sibling returning the richer type, alongside the files
database it cannot currently name at all.

*Acceptance:* unit tests for every row of the file-name table, including an unrecognised name, a
`.sig` request, a database name that disagrees with the repository name, and traversal attempts
(`..`, `%2F`, an encoded control character). Tests that a private repository resolves for its owner
and not for anyone else, and that the name and architecture segments are case-sensitive. An
enforcement test asserts the service names neither `DbContext` `DbSet` — the property the whole
design rests on.

*Depends on:* 1, 2.

### 4. `PacmanController` — `MINOR`

The routes under `/repositories/{repoName}/{repoArch}/{fileName}`, thin as ever, plus the HTTP
behaviour: `Last-Modified`, `ETag`, `304`, `Range`/`206`, `Content-Length`, `Cache-Control` by
visibility, `Vary: Authorization`, and `application/octet-stream`. The routes are `[AllowAnonymous]`
and accept the `Basic` scheme alongside `Bearer`.

*Acceptance:* E2E tests fetching the database, the files database and a package from a public
repository anonymously; the same against a private repository with a token, and `404` without one;
a mistyped token producing `401` rather than `404` — the
[`[AllowAnonymous]` trap](basic-auth.md#present-but-invalid-credentials-are-a-401-and-this-is-easy-to-get-wrong),
which this is the first real endpoint able to test; a conditional request returning `304`; a `Range`
request returning `206` with the right bytes; and a private repository's response carrying
`Cache-Control: private, no-store` while a public one does not.

*Depends on:* 3, and [Basic Auth](basic-auth.md#4-the-basic-scheme-and-its-handler--minor).

### 5. Document consuming a hosted repository — `PATCH`

A `docs/consuming-a-repository.md` with the `pacman.conf` snippet, the `$repo`/`$arch` form, the
`SigLevel = Optional TrustAll` requirement and why it is needed, how to mint a token, how to put
credentials in the `Server` line, and — stated plainly —
[that a repository name is public even when the repository is private](#the-trade-a-repositorys-name-is-public-even-when-the-repository-is-not).
Linked from `README.md` and from this document.

This is the user-facing counterpart to three design documents, and it is the only one of the four a
person configuring a machine will read.

*Depends on:* 4.

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
* **Settling [`(Name, Architecture)` versus `Name` alone](#open-question-name-architecture-or-name-alone).**
  Deferred while the application is pre-release, when it is still an index change and a migration
  rather than a breaking change to live deployments. It should be decided before the first release,
  because that is the point at which the cheap direction closes.
* **A directory index** at `/repositories/{name}/{arch}/`. pacman never needs one, but it is the
  first thing a human opens the URL expecting to see.
* **Serving package files through a reverse proxy.** Streaming hundreds of megabytes through Kestrel
  works but occupies a request thread's worth of resources for the duration; `X-Accel-Redirect` or
  `X-Sendfile` would hand it to the proxy. Only worth doing once there is a deployment with a proxy
  in front.
* **`Usage` and mirrorlists.** Nothing generates a `pacman.conf` fragment or a mirrorlist for a user,
  which is the obvious convenience once more than one host serves the same repositories.
* **Rate limiting and download quotas.** Nothing bounds how often an anonymous caller can pull a
  public repository.
* **Reconciling a repository database from the rows**, already filed under
  [`packages-api.md`](packages-api.md#deferred-work) and made more visible by this work: a
  `.db.tar.gz` that disagrees with the rows is now something a `pacman` client sees directly.
