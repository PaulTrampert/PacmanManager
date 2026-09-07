# Packages API

Status: **planned**. This document is the design for publishing, listing and removing packages in a
hosted repository, and is the source the implementation issues are cut from. It follows the
conventions established in [`authorization-plan.md`](authorization-plan.md); where it departs from
them, it says so.

## Goals

* A repository owner can push a built `.pkg.tar.*` file at a repository over HTTP and have it
  appear in that repository's `.db.tar.gz`.
* Anyone who may see a repository may see and download its packages, with no separate visibility
  rules of their own.
* Package metadata in the API is the metadata libalpm reports for the uploaded file, not anything
  the client asserts.

## Non-goals for this work

Recorded here so the issues stay bounded; each has a follow-up in
[Deferred work](#deferred-work).

* Detached package signatures (`.sig`) and signature verification.
* Retaining more than one version of a package per repository.
* Postgres full-text search and relevance ranking.
* Serving a repository in the layout a `pacman` client expects (`Server = …`), which needs the
  `.db.tar.gz` and the package files under one base URL. The `content` route below downloads a
  package by id; it is a management route, not a pacman mirror.

---

## Domain model

### Entity

`PacmanManager.Entities.Package`, configured by data annotations like every other entity.

| Column | Type | Notes |
| :--- | :--- | :--- |
| `Id` | `Guid` | Primary key, `Guid.CreateVersion7()`. |
| `RepositoryId` | `Guid` | FK to `PacmanRepository`, required, cascade delete. |
| `Repository` | nav | |
| `OwnerId` | `Guid` | FK to `User`, required. The user who published this version. |
| `Owner` | nav | |
| `Name` | `string` | From `alpm_pkg_get_name`. |
| `Version` | `string` | Full `epoch:pkgver-pkgrel`. |
| `Description` | `string?` | |
| `Base` | `string?` | `pkgbase`. |
| `Url` | `string?` | Upstream URL. |
| `Architecture` | `string` | Package architecture, which is not always the repository's. |
| `Packager` | `string?` | |
| `FileName` | `string` | Basename of the stored package file. See [Storage](#storage-layout). |
| `CompressedSize` | `long` | `alpm_pkg_get_size`. |
| `InstalledSize` | `long` | `alpm_pkg_get_isize`. |
| `BuildDate` | `DateTimeOffset` | |
| `Sha256Sum` | `string?` | |
| `Md5Sum` | `string?` | |
| `Licenses`, `Groups`, `Provides`, `Replaces` | `string[]` | Postgres `text[]` via Npgsql. |
| `Depends`, `OptDepends`, `MakeDepends`, `CheckDepends`, `Conflicts` | `string[]` | Stored in pacman's own spelling (`foo>=1.2`, `bar: reason`) rather than decomposed. |
| `CreatedAt` | `DateTimeOffset` | When this package first appeared in this repository. |
| `UpdatedAt` | `DateTimeOffset` | When it was last published. Backs `updatedSince`. |

Unique index on `(RepositoryId, Name)`. A repository holds at most one version of a package, which
is what makes `(repositoryId, name)` a usable route key and what the upsert in
[Publishing](#publishing) relies on.

Dependency lists are string arrays rather than a related table because nothing in this API queries
into them, and because pacman's own dependency syntax round-trips losslessly as text. Splitting
them into a `Dependency` table is a change we can make later without touching the public model.

Validation limits go in `PacmanManager.Entities.PackageValidationConstants`, alongside the existing
`PacmanRepositoryValidationConstants`. `RegularExpressions.PackageName` already exists in RepoHost;
a `PackageVersion` expression needs adding for the `[epoch:]pkgver[-pkgrel]` shape.

### Public model

`PacmanManager.RepoHost.Models.Package` mirrors the entity, with `owner` and `repository` projected
as summaries rather than ids alone, following `Repository.Projection`:

```jsonc
{
  "id": "0199…",                       // GUIDv7
  "repositoryId": "0198…",
  "owner": { "id": "0197…", "displayName": "Paul" },
  "name": "my-tool",
  "version": "1.4.2-1",
  "description": "…",
  "base": "my-tool",
  "url": "https://…",
  "architecture": "x86_64",
  "packager": "Paul <paul@example.com>",
  "fileName": "my-tool-1.4.2-1-x86_64.pkg.tar.zst",
  "compressedSize": 184320,
  "installedSize": 962560,
  "buildDate": "2026-09-01T12:00:00+00:00",
  "sha256Sum": "…",
  "licenses": ["MIT"],
  "groups": [],
  "provides": [],
  "replaces": [],
  "depends": ["glibc"],
  "optDepends": ["git: for the sync command"],
  "makeDepends": [],
  "checkDepends": [],
  "conflicts": [],
  "createdAt": "…",
  "updatedAt": "…"
}
```

A note on "validated according to libalpm's rules": every field above is *extracted from the
uploaded file*, never supplied by the caller. Validation is therefore a sanity check on what
libalpm reported — a guard against a malformed package producing an unstorable row or a dangerous
file name — and not input validation in the usual sense. In particular `fileName` must never be
taken from the client or used unsanitised in a path; see [Storage](#storage-layout).

---

## Authorization

Packages have no visibility of their own: **a package is visible exactly when its repository is.**
Everything below is the repository rules table from `authorization-plan.md` projected onto
packages, plus the `Publish` action.

| Operation | Repository | Actor | Result |
| :--- | :--- | :--- | :--- |
| Read (list, get, content) | Public | Anyone, including anonymous | `200` |
| Read | Private | Owner | `200` |
| Read | Private | Anyone else | `404` (hide existence) |
| Publish, new package | Any visible | Repository owner | `201` |
| Publish, existing package | Any visible | Repository owner or package owner | `200` |
| Publish | Public | Some other user | `403` |
| Publish | Private | Not the owner | `404` (already absent from the visible set) |
| Publish | Any | Unauthenticated | `401` |
| Delete | Any visible | Repository owner or package owner | `204` |
| Delete | Public | Some other user | `403` |
| Delete | Any | Unauthenticated | `401` |

The repository owner is included in delete (and in publish over an existing package) because they
own the repository's contents: without it, a repository owner who has granted publish rights to
someone else could not remove what that person pushed except by deleting the whole repository.
`Publish` is named as a distinct action, rather than being folded into "owner writes", precisely so
that a future grant table has somewhere to attach.

### Where it is enforced

In `PackageService`, structurally, the same way `RepositoryService` does it — see
[`authorization-plan.md`](authorization-plan.md) for why this is not an action filter.

The one thing worth spelling out is how package visibility reuses the repository rule instead of
restating it. `RepositoryAccessPolicy.VisibleTo` already returns
`Expression<Func<PacmanRepository, bool>>`; a `PackageService.VisibleAsync` composes it as a
semi-join rather than rewriting it into a package expression:

```csharp
private async ValueTask<IQueryable<Package>> VisibleAsync(CancellationToken ct)
{
    var actor = await actorAccessor.GetActorAsync(ct);
    var visibleRepositories = dbContext.PacmanRepositories.Where(accessPolicy.VisibleTo(actor));
    return dbContext.Packages.Where(p => visibleRepositories.Any(r => r.Id == p.RepositoryId));
}
```

This keeps one definition of repository visibility. Restating it as
`p.Repository.IsPublic || p.Repository.OwnerId == userId` would be a second copy of the rule to
keep in step, which is the thing the existing design goes out of its way to avoid.

`PackageAccessPolicy` then holds only what is genuinely new — `CheckPublish(repository, existing,
actor)` and `CheckDelete(repository, package, actor)`, returning the existing `RepositoryAccess`
verdict enum so `AuthorizationExceptionHandler` needs no new mapping. `PackageForbiddenException`
mirrors `RepositoryForbiddenException`.

As with repositories, `PackageService` must touch `DbContext.Packages` in exactly one place, and a
`PackageServiceEnforcementTests` asserts it, mirroring `RepositoryServiceEnforcementTests`.

---

## Routes

All routes are versioned by namespace (`Controllers/v1/…`) as usual.

**Route naming.** These routes are plural (`/api/v1/packages`), and the existing
`/api/v1/repository` is being renamed to `/api/v1/repositories` to match. That rename is breaking
and gets its own `MAJOR` issue; the package work depends on it so that the alias routes below are
right the first time.

| Method | Path | Auth | Success | Failure |
| :--- | :--- | :--- | :--- | :--- |
| `GET` | `/api/v1/packages` | Anonymous | `200` page of packages | — |
| `GET` | `/api/v1/repositories/{repositoryId}/packages` | Anonymous | `200` page | `404` repo not visible |
| `GET` | `/api/v1/packages/{packageId}` | Anonymous | `200` package | `404` |
| `GET` | `/api/v1/repositories/{repositoryId}/packages/{name}` | Anonymous | `200` package | `404` |
| `GET` | `/api/v1/packages/{packageId}/content` | Anonymous | `200` file | `404` |
| `GET` | `/api/v1/repositories/{repositoryId}/packages/{name}/content` | Anonymous | `200` file | `404` |
| `POST` | `/api/v1/repositories/{repositoryId}/packages` | Required | `201` created, `200` updated | `400`, `401`, `403`, `404`, `409`, `413` |
| `DELETE` | `/api/v1/packages/{packageId}` | Required | `204` | `401`, `403`, `404` |
| `DELETE` | `/api/v1/repositories/{repositoryId}/packages/{name}` | Required | `204` | `401`, `403`, `404` |

The repository-scoped forms are aliases: they resolve `(repositoryId, name)` to the same package
and share a service method with the id form. They exist because the natural key is what a build
script knows — it has just built `my-tool` for repository X and does not know a `packageId`.

`404` covers three cases that must be indistinguishable from outside: the package does not exist,
the repository does not exist, and the repository is private and belongs to someone else.

### Listing

`GET /api/v1/packages` takes the three separate concerns the repository listing established:
`PaginationParams` (`offset`, `pageSize`), a `PackageFilter` query object, and
`SortOptions<PackageSortField>` (`sortBy`, `direction`).

`PackageFilter` (every member optional, each annotated with a `PTrampert.QueryObjects` attribute):

| Parameter | Attribute | Meaning |
| :--- | :--- | :--- |
| `repositoryIds` | `AnyOfQuery` | Restrict to these repositories. |
| `ownerIds` | `AnyOfQuery` | Restrict to packages published by these users. |
| `architecture` | `EqualsQuery` | Package architecture. |
| `nameContains` | `StringContainsQuery` | Substring of the package name. |
| `updatedSince` | `GreaterThanQuery` on `UpdatedAt` | ISO 8601. Packages published since this instant. |
| `search` | *(applied separately)* | See below. |

Two implementation notes that the issues need to carry:

* **`search` is not a query-object criterion.** It spans several columns, and `PTrampert.QueryObjects`
  attributes bind one property to one column. It is applied to the query explicitly, after the
  filter, as a case-insensitive substring match over `Name`, `Base` and `Description`. This is
  deliberately not Postgres full-text search: `EF.Functions.ToTsVector` does not translate under
  `Microsoft.EntityFrameworkCore.InMemory`, so an FTS implementation could only be covered by
  E2E tests, while a `Contains` match is exercised by the same service-level unit tests as every
  other criterion. Real FTS is a [deferred](#deferred-work) follow-up.
* **Comma-separated multi-values need a model binder.** ASP.NET Core binds `?repositoryIds=a&repositoryIds=b`
  out of the box but does not split `?repositoryIds=a,b` into a `Guid[]`. Supporting the comma form
  the way this API advertises it needs a small binder, applied to the array properties of the
  filter.

Like `RepositoryFilter`, the filter is ANDed onto the already-visible query and can only ever
remove rows. Naming a repository in `repositoryIds` that the caller cannot see yields nothing
rather than an error.

`PackageSortField`: `Updated`, `Created`, `Name`, `Version`, `InstalledSize`. `Updated` is first, so
an unsorted listing returns most-recently-published first; `SortOptions` defaults `direction` to
descending. Sorting is applied by a `PackageSortExtensions.ApplySort`, tie-broken by `Id` for stable
paging, exactly as `RepositorySortExtensions` does.

On the repository-scoped alias, `repositoryIds` is not accepted; the path segment supplies it. If a
caller sends it anyway the request is a `400` rather than being silently ignored.

---

## Storage layout

Today a repository's database lives at `{DbPath}/sync/{repositoryId}.db.tar.gz`, where
`DbPath = {DATA_DIR}/libalpm`, because that is where libalpm expects registered sync databases.
Package files need a home too, and it cannot be that same directory: `repo-add` records only the
*basename* of a package file in the database, so two repositories each holding `my-tool-1.0-1-x86_64.pkg.tar.zst`
would collide on a shared directory.

Package files therefore live per repository:

```
{DATA_DIR}/repositories/{repositoryId}/my-tool-1.4.2-1-x86_64.pkg.tar.zst
```

`{DATA_DIR}/repositories/*/*.conf` is already the `Include` glob in `PacmanConfigSettings`, so this
reuses a tree the configuration already anticipates.

**File names are derived, never accepted.** The stored basename is built from metadata libalpm
reported — `{name}-{version}-{architecture}.pkg.tar.{ext}` — with `ext` determined by sniffing the
uploaded file's compression magic against an allowlist (`zst`, `xz`, `gz`, `bz2`). A client-supplied
name is never used to build a path, which removes path traversal as a concern rather than defending
against it. An unrecognised compression is a `400`.

`IFileSystem` currently exposes `Exists`, `Delete` and `OpenRead`; publishing needs at least
`OpenWrite`/`Move` and directory creation, added there so the service stays testable.

### repo-add and repo-remove

`RepoAdd` currently takes `(name, repoHome)` and passes only the database file name, which is how an
empty database gets created. It needs to accept package file paths as well:

```
repo-add {repositoryId}.db.tar.gz {absolute path to package file}    # working dir {DbPath}/sync
```

and a sibling `RepoRemove` (`repo-remove {repositoryId}.db.tar.gz {packageName}`) is needed for
delete. Both are `ICliTool` implementations run through `ICliToolRunner`, like the existing one.

---

## Publishing

`POST /api/v1/repositories/{repositoryId}/packages` with the package file as the request body.

1. **Authorize before reading the body.** Resolve the repository from the visible set and run
   `CheckPublish` first, so an unauthorized caller is rejected without uploading megabytes.
2. **Stream the body to a temporary file** under `{DATA_DIR}/tmp`. Package files run to hundreds of
   megabytes; nothing may buffer the upload in memory, and Kestrel's 30 MB default body limit has to
   be raised (a configurable maximum, not `DisableRequestSizeLimit`, so the limit is a `413` and not
   an out-of-disk).
3. **Extract metadata** with `ILibAlpm.LoadPackageFile` over the temporary file.
4. **Validate** the extracted values: name and version match the expected shapes, and the package's
   architecture is either the repository's architecture or `any`. A mismatch is a `400`.
5. **Upsert** on `(repositoryId, name)`. A new row is `201` with a `Location` header; an existing row
   is updated in place — keeping `Id`, `CreatedAt` and, per the rules table, updating `OwnerId` to
   the publishing user — and returns `200`.
6. **Move the file into place** and run `repo-add`. If the replaced version had a different file
   name, delete the old file.
7. **Touch the repository's `UpdatedAt`**, since its contents changed.

**Content type.** Recommended: `application/octet-stream` with the raw file as the body. Because the
stored name is derived from metadata (above), there is nothing a `multipart/form-data` envelope
would carry that we would trust, and raw-body streaming is markedly simpler to get right. The
implementing issue should confirm this against how the eventual publish client is expected to work.

**Concurrency.** Two publishes to the same repository must not run `repo-add` against the same
database concurrently. `repo-add` takes a lock file beside the database and fails rather than
corrupting it, which is a safe floor but a poor experience. Publishing is therefore serialized per
repository in-process (a keyed async lock), and a `repo-add` lock failure surfaces as `409`. This is
correct for a single instance only; a multi-instance deployment needs a Postgres advisory lock, and
that is [deferred](#deferred-work) along with the note that nothing else about the app is
horizontally scalable yet.

**Failure handling.** The database row and the on-disk state have to end up agreeing. Save the row
and run `repo-add` inside the same try/catch the way `CreateRepositoryAsync` does, and on failure
remove the newly written package file and let the transaction roll back. The known-gap that already
applies to repositories applies here too: a file we fail to delete is orphaned but inert.

---

## Deleting

`DELETE /api/v1/packages/{packageId}` (and the `(repositoryId, name)` alias) removes the row, runs
`repo-remove` against the repository database, deletes the package file, and touches the
repository's `UpdatedAt`. As with repository deletion, the row is the source of truth: a file that
cannot be removed is logged at warning and does not fail the request.

---

## Implementation plan

One issue per heading. Dependencies are noted; anything without a dependency can start immediately.

### 1. `LibAlpmSharp`: load a package from a file — `MINOR`

`alpm_pkg_load` and `alpm_pkg_free` are already bound in `NativeMethods.Package.cs` but nothing
managed exposes them. Add `ILibAlpm.LoadPackageFile(string path, bool full = true, int sigLevel = 0)`
returning an `IPackage` that owns the native handle and frees it on dispose, throwing `AlpmException`
on failure.

*Acceptance:* unit tests in `LibAlpmSharp.Test` load a small fixture `.pkg.tar.zst` and assert the
reported name, version and architecture; loading a missing or malformed file throws `AlpmException`.

### 2. `LibAlpmSharp`: complete the `IPackage` surface — `MINOR`

`IPackage` exposes roughly half of what the natives already bind. Add `GetFileName`, `GetLicenses`,
`GetGroups`, `GetProvides`, `GetReplaces`, `GetMakeDepends`, `GetCheckDepends`, `GetSha256Sum`,
`GetMd5Sum`, following the existing `Get*` naming. While in here, fix `GetInstallDate`, which is
documented as returning null when not installed but currently returns the Unix epoch.

*Acceptance:* each new member is covered in `AlpmPackageTests`; the install-date fix has a test that
asserts null.

*Depends on:* nothing, but pairs naturally with issue 1.

### 3. `Package` entity and migration — `MINOR`

The entity as tabulated above, `PackageValidationConstants`, `Packages` on `PacmanManagerDbContext`,
a `RegularExpressions.PackageVersion`, and a migration named `AddTable_Packages`.

*Acceptance:* `dotnet ef migrations list` shows it; applying it against the compose Postgres
succeeds; the unique index on `(RepositoryId, Name)` exists.

### 4. Package file storage — `MINOR`

`IFileSystem` gains the write-side operations, and a small path-resolution type owns the
`{DATA_DIR}/repositories/{repositoryId}/…` layout and the derived-file-name rule (including
compression sniffing and the extension allowlist).

*Acceptance:* unit tests for the file-name derivation, including rejection of an unrecognised
compression and of metadata that would otherwise produce a name containing a path separator.

### 5. `RepoAdd` package arguments and `RepoRemove` — `PATCH`

Extend `RepoAdd` to take package paths; add `RepoRemove`.

*Acceptance:* unit tests assert the emitted argument lists and working directory. Existing empty-database
creation is unchanged.

### 6. `PackageAccessPolicy` — `MINOR`

`CheckPublish` and `CheckDelete` returning `RepositoryAccess`; `PackageForbiddenException`;
`AuthorizationExceptionHandler` extended to map it.

*Acceptance:* `PackageAccessPolicyTests` covers every row of the rules table above as a plain unit
test, no database involved, mirroring `RepositoryAccessPolicyTests`.

*Depends on:* 3.

### 7. `IPackageService` read paths — `MINOR`

`GetPackagesAsync`, `GetPackageByIdAsync`, `GetPackageByNameAsync(repositoryId, name)`, plus
`PackageFilter`, `PackageSortField`, `PackageSortExtensions`, the semi-join `VisibleAsync`, and the
separately-applied `search` term.

*Acceptance:* `PackageServiceTests` covers each filter and sort field; tests named for the
properties assert that no filter value widens the visible set (a private repository's packages stay
invisible to a non-owner, `repositoryIds` naming an invisible repository returns empty).
`PackageServiceEnforcementTests` asserts `DbContext.Packages` is named in exactly one method.

*Depends on:* 3, 6.

### 8. Comma-separated array model binder — `PATCH`

A binder that accepts both `?ids=a,b` and `?ids=a&ids=b` for array-valued query parameters, applied
to `repositoryIds` and `ownerIds`.

*Acceptance:* unit tests over both forms, the mixed form, and empty/whitespace entries.

### 9. `PackagesController` read routes — `MINOR`

`GET /api/v1/packages`, `GET /api/v1/packages/{packageId}`, and the two repository-scoped aliases.
Thin controllers: log, call the service, translate `null` to `NotFound()`.

*Acceptance:* E2E tests for each route, including a private repository's package returning `404` to
a non-owner and `200` to its owner, and an anonymous listing showing public repositories only.

*Depends on:* 7, 8, 13.

### 10. Publishing — `MINOR`

`POST /api/v1/repositories/{repositoryId}/packages` end to end: authorize, stream to temp, extract,
validate, upsert, `repo-add`, per-repository serialization, rollback on failure, configurable size
limit.

*Acceptance:* service unit tests for the upsert (new → `201`-shaped result, existing → updated in
place with `Id`/`CreatedAt` preserved and `OwnerId` reassigned), architecture mismatch rejected,
`repo-add` failure rolling back both row and file. E2E tests publish a real fixture package and then
read it back through `GET`, and assert `403` publishing to someone else's public repository and
`404` to someone else's private one.

*Depends on:* 1, 2, 3, 4, 5, 6, 7, 13.

### 11. Package content download — `MINOR`

`GET /api/v1/packages/{packageId}/content` and its alias, streaming the stored file with a
`Content-Disposition` of the stored file name and `application/octet-stream`.

*Acceptance:* E2E test downloads a published package and compares the bytes to what was uploaded;
visibility behaves as for the metadata routes.

*Depends on:* 10.

### 12. Deleting — `MINOR`

`DELETE /api/v1/packages/{packageId}` and its alias: `repo-remove`, file cleanup, repository
`UpdatedAt`.

*Acceptance:* service unit tests for the authorization outcomes (package owner, repository owner,
other user on a public repository → `403`, other user on a private one → `404`) and for a file
deletion failure being logged but not failing the request. E2E test deletes a published package and
confirms it is gone from the listing.

*Depends on:* 10.

### 13. Rename `/api/v1/repository` to `/api/v1/repositories` — `MAJOR`

`ControllerConstants.ControllerBaseRoute` derives the segment from `[controller]`, so the rename is
either an explicit route on `RepositoryController` or a rename of the controller type. Breaking for
any existing client.

*Acceptance:* existing E2E tests updated to the new path; `/api/v1/repository` no longer resolves.

*Depends on:* nothing. Should land before 9 and 10 so the alias routes are written once.

---

## Deferred work

Worth filing as issues, but explicitly out of scope for the work above.

* **Serving a pacman-consumable repository.** `GetRepositoryFileByIdAsync` and
  `GetRepositoryFileByNameAsync` exist on `IRepositoryService` but no route calls them, so the
  `.db.tar.gz` cannot be fetched at all. A `pacman` client configured with `Server = …` needs the
  database and every package file resolvable under one base URL by the basename `repo-add` recorded.
  Until that exists, this API can publish packages but nothing can install them.
* **Postgres full-text search** with a generated `tsvector` column, a GIN index and relevance
  ranking, replacing the substring `search`.
* **Detached signatures.** Accepting a `.sig` alongside the package, storing it, passing it to
  `repo-add`, and serving it. Required for any repository with `SigLevel = Required`.
* **Version retention.** Keeping the previous N versions of a package rather than replacing, which
  changes the unique index and makes `(repositoryId, name)` no longer a unique route key.
* **Multi-instance publishing.** The per-repository lock is in-process; concurrent publishes from
  two instances would rely on `repo-add`'s own lock file and surface as `409`s. A Postgres advisory
  lock keyed by repository id fixes it.
* **Quotas.** Nothing bounds how much a user can upload.
* **`ItemExistsException` is still unused**, as noted in `authorization-plan.md`; a name collision
  in either API surfaces as a `500` rather than a `409`.
