# Packages API

Status: **planned**. This document is the design for publishing, listing and removing packages in a
hosted repository, and is the source the implementation issues are cut from. It follows the
conventions established in [`authorization-plan.md`](authorization-plan.md); where it departs from
them, it says so.

## Goals

* A user with publish rights on a repository — today, its owner — can push a built `.pkg.tar.*`
  file at it over HTTP and have the package appear in that repository's `.db.tar.gz`.
* Anyone who may see a repository may see and download its packages, with no separate visibility
  rules of their own.
* Package metadata in the API is the metadata libalpm reports for the uploaded file, not anything
  the client asserts.

## Non-goals for this work

Recorded here so the issues stay bounded; each has a follow-up in
[Deferred work](#deferred-work).

* Detached package signatures (`.sig`) and signature verification.
* Postgres full-text search and relevance ranking.
* Serving a repository in the layout a `pacman` client expects (`Server = …`), which needs the
  `.db.tar.gz` and the package files under one base URL. The `content` route below downloads a
  package by id; it is a management route, not a pacman mirror.

---

## Domain model

### Entity

`PacmanManager.Entities.PacmanPackage`, configured by data annotations like every other entity.

The entity is `PacmanPackage`, not `Package`, mirroring `PacmanRepository`. The public model below
is `PacmanManager.RepoHost.Models.Package`, and `PackageService` imports both namespaces the way
`RepositoryService` does; two types called `Package` in scope would be a `CS0104` on every
unqualified use. The DbSet is `PacmanPackages`, matching `PacmanRepositories`.

| Column | Type | Notes |
| :--- | :--- | :--- |
| `Id` | `Guid` | Primary key, `Guid.CreateVersion7()`. |
| `RepositoryId` | `Guid` | FK to `PacmanRepository`, required, cascade delete. |
| `Repository` | nav | |
| `PublisherId` | `Guid` | FK to `User`, required. The user who most recently published this package. It records who, and confers no rights of its own — see [Authorization](#authorization). |
| `Publisher` | nav | |
| `Name` | `string` | From `alpm_pkg_get_name`. |
| `Version` | `string` | Full `epoch:pkgver-pkgrel`. |
| `Description` | `string?` | |
| `Base` | `string?` | `pkgbase`. |
| `Url` | `string?` | Upstream URL. |
| `Architecture` | `string` | Package architecture, which is not always the repository's. |
| `Packager` | `string?` | |
| `FileName` | `string` | Basename of the stored package file, derived from the metadata below — never `alpm_pkg_get_filename`. See [Storage](#storage-layout). |
| `CompressedSize` | `long` | `alpm_pkg_get_size`. |
| `InstalledSize` | `long` | `alpm_pkg_get_isize`. |
| `BuildDate` | `DateTimeOffset` | |
| `Sha256Sum` | `string` | Computed over the uploaded bytes, not read from libalpm. See [Checksums](#checksums-are-computed-not-read). |
| `Md5Sum` | `string` | Likewise. Recorded because `repo-add` writes both into the database. |
| `Licenses`, `Groups`, `Provides`, `Replaces` | `string[]` | Postgres `text[]` via Npgsql. |
| `Depends`, `OptDepends`, `MakeDepends`, `CheckDepends`, `Conflicts` | `string[]` | Stored in pacman's own spelling (`foo>=1.2`, `bar: reason`) rather than decomposed. |
| `CreatedAt` | `DateTimeOffset` | When this package first appeared in this repository. |
| `UpdatedAt` | `DateTimeOffset` | When it was last published. Backs `updatedSince`. |

Unique index on `(RepositoryId, Name)`. A repository holds exactly one version of a package, which
is what makes `(repositoryId, name)` a usable route key and what the upsert in
[Publishing](#publishing) relies on.

This is the model, not a simplification to be revisited later. Pacman rolls forward and has no
rollback: the only remedy for a bad package is to publish a higher version over it. Keeping previous
versions would therefore be storage nothing can ever ask for, and it would cost the unique index and
with it the natural route key.

Dependency lists are string arrays rather than a related table because nothing in this API queries
into them, and because pacman's own dependency syntax round-trips losslessly as text. Splitting
them into a `Dependency` table is a change we can make later without touching the public model.

Validation limits go in `PacmanManager.Entities.PackageValidationConstants`, alongside the existing
`PacmanRepositoryValidationConstants`. `RegularExpressions.PackageName` already exists in RepoHost;
a `PackageVersion` expression needs adding for the `[epoch:]pkgver[-pkgrel]` shape.

### Public model

`PacmanManager.RepoHost.Models.Package` mirrors the `PacmanPackage` entity, with `publisher`
projected as a summary rather than a bare id, following `Repository.Projection`. It keeps the
unprefixed name for the same reason `Repository` does: the `Pacman` prefix distinguishes the
storage type, and the wire model is the one callers read about:

```jsonc
{
  "id": "0199…",                       // GUIDv7
  "repositoryId": "0198…",
  "publisher": { "id": "0197…", "displayName": "Paul" },
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
| Read | Private | Repository owner | `200` |
| Read | Private | Anyone else | `404` (hide existence) |
| Publish (new) | Any visible | Holds `Publish` on the repository | `201` |
| Publish (replace) | Any visible | Holds `Publish` on the repository | `200` |
| Delete | Any visible | Holds `Publish` on the repository | `204` |
| Publish or delete | Public | Anyone else | `403` |
| Publish or delete | Private | Not the owner | `404` (already absent from the visible set) |
| Publish or delete | Any | Unauthenticated | `401` |

### The `Publish` permission is held over a repository, not over a package

**Anyone holding `Publish` on a repository may publish, replace or remove any package in it.** A
package's `PublisherId` records who last pushed it and grants that person nothing.

The alternative — giving a package's publisher rights over "their" package — reads as the safer
design and is not. It makes the act of publishing a claim: the first person to push `my-tool` owns
it, and anyone else with publish rights who pushes a new build of the same package takes that claim
over. A repository owner republishing a colleague's package would quietly transfer it to themselves,
which is a permission change nobody asked for, made as a side effect of a routine build. Deciding
the question at the repository, where the grant was actually made, means republishing a package is
only ever an update to a package.

It follows that the person who published a package can also delete it — not because they published
it, but because publishing it at all required the repository permission that delete needs too.

`Publish` is named as a distinct action, rather than being folded into "repository owner writes", so
that a future grant table has somewhere to attach. Until that table exists, holding `Publish` on a
repository means owning it.

### Where it is enforced

In `PackageService`, structurally, the same way `RepositoryService` does it — see
[`authorization-plan.md`](authorization-plan.md) for why this is not an action filter.

The one thing worth spelling out is how package visibility reuses the repository rule instead of
restating it. `RepositoryAccessPolicy.VisibleTo` already returns
`Expression<Func<PacmanRepository, bool>>`; a `PackageService.VisibleAsync` composes it as a
semi-join rather than rewriting it into a package expression:

```csharp
private async ValueTask<IQueryable<PacmanPackage>> VisibleAsync(CancellationToken ct)
{
    var actor = await actorAccessor.GetActorAsync(ct);
    var visibleRepositories = dbContext.PacmanRepositories.Where(accessPolicy.VisibleTo(actor));
    return dbContext.PacmanPackages.Where(p => visibleRepositories.Any(r => r.Id == p.RepositoryId));
}
```

This keeps one definition of repository visibility. Restating it as
`p.Repository.IsPublic || p.Repository.OwnerId == userId` would be a second copy of the rule to
keep in step, which is the thing the existing design goes out of its way to avoid.

`PackageAccessPolicy` then holds only what is genuinely new: `CheckPublish(repository, actor)`,
returning the existing `RepositoryAccess` verdict enum so the *verdict* type is unchanged.
`PackageForbiddenException` mirrors `RepositoryForbiddenException`, and
`AuthorizationExceptionHandler` does need a new arm for it: the handler switches on exception type
(`NoCurrentUserException` → 401, `RepositoryForbiddenException` → 403, everything else unhandled),
so an unmapped `PackageForbiddenException` would fall through to a `500`. Issue 6 covers the added
mapping and a title of its own ("You may not publish to this repository.").

Note the signature. Because the permission is repository-scoped, the check needs no package
argument, and the same method answers for publish, replace and delete. It is nonetheless a separate
method from `RepositoryAccessPolicy.CheckWrite` even though the two agree today, because `Publish`
and "may edit the repository itself" are different questions that a grant table will answer
differently.

As with repositories, `PackageService` must touch `DbContext.PacmanPackages` in exactly one place,
and a `PackageServiceEnforcementTests` asserts it, mirroring `RepositoryServiceEnforcementTests`.

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
| `DELETE` | `/api/v1/packages/{packageId}` | Required | `204` | `401`, `403`, `404`, `409` |
| `DELETE` | `/api/v1/repositories/{repositoryId}/packages/{name}` | Required | `204` | `401`, `403`, `404`, `409` |

The repository-scoped forms are aliases: they resolve `(repositoryId, name)` to the same package
and share a service method with the id form. They exist because the natural key is what a build
script knows — it has just built `my-tool` for repository X and does not know a `packageId`.

`404` covers three cases that must be indistinguishable from outside: the package does not exist,
the repository does not exist, and the repository is private and belongs to someone else.

`409` on both mutating verbs means the repository database was locked by another writer — see
[Concurrency](#publishing). Delete mutates the same `.db.tar.gz` that publish does, so it can fail
the same way and must advertise the same status.

### Listing

`GET /api/v1/packages` takes the three separate concerns the repository listing established:
`PaginationParams` (`offset`, `pageSize`), a `PackageFilter` query object, and
`SortOptions<PackageSortField>` (`sortBy`, `direction`).

`PackageFilter` (every member optional):

| Parameter | Applied by | Meaning |
| :--- | :--- | :--- |
| `repositoryIds` | `AnyOfQuery` | Restrict to these repositories. |
| `publisherIds` | `AnyOfQuery` | Restrict to packages last published by these users. |
| `architecture` | `EqualsQuery` | Package architecture. |
| `nameContains` | `StringContainsQuery` | Substring of the package name. |
| `updatedSince` | `GreaterThanQuery` on `UpdatedAt` | ISO 8601. Packages published since this instant. |
| `search` | `IQueryObject<PacmanPackage>` | Case-insensitive substring over `Name`, `Base` and `Description`. |

Two implementation notes that the issues need to carry:

* **`search` spans columns, so `PackageFilter` implements `IQueryObject<PacmanPackage>`.** A
  `PTrampert.QueryObjects` attribute binds one query property to one target column, which `search`
  cannot be. `IQueryObject<T>` is the library's own extension point for exactly this: it declares a
  single `Expression<Func<T, bool>> BuildQueryExpression()`, and `QueryableExtensions.Where` ANDs
  whatever it returns onto the attribute-derived predicate — or skips it when the method returns
  `null`, which is what `PackageFilter` returns when `search` is unset. The body ORs a
  `Contains` over the three columns together with `ExpressionExtensions.OrElse`.

  Two details of that `Contains` are load-bearing and must not be simplified away. `string.Contains`
  is **case-sensitive** under both Npgsql (it translates to `strpos`/`LIKE`, and the default
  collation is case-sensitive) and `Microsoft.EntityFrameworkCore.InMemory` (it is
  `string.Contains`), so a bare `p.Name.Contains(search)` would not deliver the case-insensitive
  match this table promises. Lower both sides — `p.Name.ToLower().Contains(term)` with `term`
  lowered once in C# — which Npgsql translates to `lower(…)` and InMemory evaluates directly. And
  `Base` and `Description` are nullable: `p.Base.Contains(term)` is a null-check-free dereference
  that Npgsql happens to turn into SQL that yields `NULL` (so the row simply does not match), but
  that InMemory evaluates in memory and throws `NullReferenceException` on the first package with no
  `pkgbase`. Guard each nullable column explicitly:

  ```csharp
  var term = search.ToLowerInvariant();
  Expression<Func<PacmanPackage, bool>> expr = p =>
      p.Name.ToLower().Contains(term) ||
      (p.Base != null && p.Base.ToLower().Contains(term)) ||
      (p.Description != null && p.Description.ToLower().Contains(term));
  ```

  The `lower(…)` calls mean no index can serve `search`; nothing indexes these columns today, and
  the full-text work below is where that stops being acceptable.

  This is strictly better than applying `search` outside the filter. `.Where(filter)` stays the one
  call site, so the guarantee that a filter can only narrow the visible set remains structural
  rather than something `PackageService` has to keep remembering, and the whole filter is still
  testable as one object.

  It does not stand in the way of true full-text search later. `BuildQueryExpression` returns an
  expression tree, and an `EF.Functions.ToTsVector(…).Matches(…)` call is expressible in one, so
  moving to FTS is a change to that method's body and nothing else — no caller, no signature and no
  query parameter changes. The reason v1 is a substring match is only that
  `Microsoft.EntityFrameworkCore.InMemory` cannot translate `ToTsVector`, so an FTS implementation
  would be coverable by E2E tests alone, whereas `Contains` is exercised by the same service-level
  unit tests as every other criterion. Ranking is the part that genuinely waits for FTS, since it
  needs a `rank` to sort by. See [deferred work](#deferred-work).
* **Comma-separated multi-values need a model binder.** ASP.NET Core binds `?repositoryIds=a&repositoryIds=b`
  out of the box but does not split `?repositoryIds=a,b` into a `Guid[]`. Supporting the comma form
  the way this API advertises it needs a small binder, applied to the array properties of the
  filter.

Like `RepositoryFilter`, the filter is ANDed onto the already-visible query and can only ever
remove rows. Naming a repository in `repositoryIds` that the caller cannot see yields nothing
rather than an error.

`PackageSortField`: `Name`, `Updated`, `Created`, `InstalledSize`. **`Name` is first, so an
unsorted listing is ordered by package name** — the order someone browsing a repository expects,
and the one that makes paging through a large repository legible. `SortOptions` takes the first
enum member as its default, so this is expressed by the member order and nothing else; the XML doc
on the enum must say so, as `RepositorySortField`'s does, because reordering it silently changes
what every unsorted request returns.

Note that this differs from `RepositorySortField`, which leads with `Created`. A repository listing
is a short list of things you own and want newest-first; a package listing is a long list you look
things up in.

**Direction defaults per sort field.** `SortOptions<TSortFields>` currently declares
`[DefaultValue(SortDirection.Descending)]`, which is right for `Updated`, `Created` and
`InstalledSize` — newest and biggest first — and wrong for `Name`, where it would make the default
package listing run Z→A. One default cannot serve both, because which way round is "natural"
belongs to the field, not to the listing. So `SortOptions.Direction` becomes
`SortDirection?`, and each listing's `ApplySort` supplies the default when the caller omitted one:

```csharp
var direction = options.Direction ?? options.SortBy switch
{
    PackageSortField.Name => SortDirection.Ascending,
    _ => SortDirection.Descending,
};
```

An explicitly supplied `direction` always wins, so `?sortBy=Name&direction=Descending` still gives
Z→A. Swagger keeps documenting the effective default per field in the parameter description, since
a nullable enum cannot carry it in `[DefaultValue]` any more.

This touches the repository listing too: `RepositorySortExtensions` gains the same switch, and
`GET /api/v1/repositories?sortBy=Name` changes from Z→A to A→Z. That is a change in an existing
endpoint's default ordering — not a contract break, since the parameter and response shape are
unchanged and any caller that cared was already sending `direction` — but it is observable, so it
gets its own issue rather than riding along inside the package work.

**There is deliberately no sort by `Version`.** A listing spans packages, and comparing one
package's version to a different package's version is meaningless — the ordering would answer no
question anyone has. It would also be wrong on its own terms: `Version` is a string, so a
lexicographic sort puts `1.10.0-1` before `1.9.0-1`, and ordering it correctly would mean pushing
pacman's `vercmp` algorithm into SQL. Since a repository holds exactly one version of each package,
version ordering has no within-package meaning to recover either.

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
name is never used to build a path. That is a real narrowing — the only inputs left are `name`,
`version` and `architecture`, each already matched against `RegularExpressions.PackageName`, the
new `PackageVersion` expression and the repository's own architecture — but it is not by itself a
proof: the metadata still comes out of a file the caller chose. The derivation must therefore
validate before it formats, and assert that the result contains no path separator, which is what
issue 4's tests pin down.

`alpm_pkg_get_filename` cannot back this column either. For a package loaded with `alpm_pkg_load`
libalpm reports back the path it was handed rather than a basename, so for a file sitting in
`{DATA_DIR}/tmp` it returns that temporary path. It is bound for completeness in issue 2; nothing
in the publish path may use it.

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
2. **Stream the body to a temporary file** under `{DATA_DIR}/tmp`, **hashing as it goes** so that
   `Sha256Sum` and `Md5Sum` come out of the same pass — see
   [Checksums](#checksums-are-computed-not-read). Package files run to hundreds of megabytes;
   nothing may buffer the upload in memory, and Kestrel's 30 MB default body limit has to be raised
   (a configurable maximum, not `DisableRequestSizeLimit`, so the limit is a `413` and not an
   out-of-disk).
3. **Extract metadata** with `ILibAlpm.LoadPackageFile` over the temporary file.
4. **Validate** the extracted values: name and version match the expected shapes, and the package's
   architecture is either the repository's architecture or `any`. A mismatch is a `400`.
5. **Stage the row** on `(repositoryId, name)`, and touch the repository's `UpdatedAt` since its
   contents changed. A new row is `201` with a `Location` header; an existing row is updated in
   place — keeping `Id` and `CreatedAt`, and setting `PublisherId` to the publishing user — and
   returns `200`. Because a package holds exactly one version, replacement is the normal path, not
   an edge case. Nothing is committed yet.
6. **Move the file into place, run `repo-add`, then `SaveChangesAsync`.** Side effects first,
   commit last — the ordering `CreateRepositoryAsync` already uses, so that a failed `repo-add`
   costs nothing more than a discarded change tracker. If the replaced version had a different file
   name, delete the old file only *after* the commit succeeds; until then it is what a rollback
   restores the database to.

### Checksums are computed, not read

`IPackage` gains `GetSha256Sum` and `GetMd5Sum` in issue 2, but neither can populate the columns
here. libalpm fills those fields from a sync database entry; for a package loaded off disk with
`alpm_pkg_load` there is no such entry and both come back `NULL`. Reading them would silently store
nulls for every package this API ever accepts, while `repo-add` wrote real `%MD5SUM%` and
`%SHA256SUM%` values into the same repository's `.db.tar.gz` — the API and the database a pacman
client reads would disagree about the same file.

So the service computes them itself, with `IncrementalHash` (or two `CryptoStream`s) over the
request body as step 2 writes it out, which costs one pass and no extra I/O. The columns are
non-nullable as a result. Issue 10 owns this; issue 2's `GetSha256Sum`/`GetMd5Sum` exist to complete
the binding and are documented as null for file-loaded packages.

**Content type.** Recommended: `application/octet-stream` with the raw file as the body. Because the
stored name is derived from metadata (above), there is nothing a `multipart/form-data` envelope
would carry that we would trust, and raw-body streaming is markedly simpler to get right. The
implementing issue should confirm this against how the eventual publish client is expected to work.

**Concurrency.** Two writers must not run `repo-add`/`repo-remove` against the same database
concurrently. Both tools take the same lock file beside the database and fail rather than corrupting
it, which is a safe floor but a poor experience. **Every mutation of a repository database is
therefore serialized per repository in-process** by one keyed async lock, keyed on repository id and
shared by the publish and delete paths — `repo-remove` mutates the same file as `repo-add`, so a
lock that only covered publish would leave exactly the race it was added to prevent. The lock is
held across the side effects and the commit, and a lock-file failure from either tool surfaces as a
`409` on either route. This is correct for a single instance only; a multi-instance deployment
needs a Postgres advisory lock, and that is [deferred](#deferred-work) along with the note that
nothing else about the app is horizontally scalable yet.

**Failure handling.** The row, the package file and the repository database have to end up
agreeing. All three of step 6's operations sit in one try/catch inside the per-repository lock, and
there are two distinct failures to unwind:

* **`repo-add` failed.** `SaveChangesAsync` is never reached, so the change tracker is discarded and
  no row changed. Delete the package file just written; on a replacement, the previous file is still
  on disk and the database still names it, so the repository is exactly as it was.
* **`SaveChangesAsync` failed after `repo-add` succeeded.** This is the case
  `CreateRepositoryAsync`'s pattern does not cover — creating an empty database has no entry to
  undo, whereas here the `.db.tar.gz` now advertises a package the rows do not know about, and no
  later code path would ever notice. Compensate explicitly: run `repo-remove {name}`, then, for a
  new package, delete the file just written; for a replacement, `repo-add` the previous file, which
  step 6 has not yet deleted for precisely this reason.

If the compensating step itself fails, log at error and let the original exception surface. The
repository database is then genuinely out of step with the rows, and the only cure is the
reconciliation in [deferred work](#deferred-work) — which is filed because this window exists, not
because it is expected to be common. A package file we fail to delete remains orphaned but inert,
the same known gap repositories already have.

---

## Deleting

`DELETE /api/v1/packages/{packageId}` (and the `(repositoryId, name)` alias) resolves the package
from the visible set, runs `CheckPublish`, and then, **under the same per-repository lock publishing
uses**, mirrors publish's ordering:

1. Run `repo-remove {repositoryId}.db.tar.gz {name}`.
2. Remove the row and touch the repository's `UpdatedAt`, then `SaveChangesAsync`.
3. Delete the package file.

Side effect first, commit second, for the same reason: **a failed `repo-remove` must abort the
request with nothing committed** rather than delete a row whose entry is still in the database file.
It surfaces as a `409` when it is the lock file, and otherwise as the same `500` any failed
`ICliTool` produces; either way the package is still listed and still installable, which is the
recoverable state. If `SaveChangesAsync` then fails, compensate by re-running `repo-add` against the
package file — still on disk, because step 3 comes last — and, as in publishing, log at error and
rethrow if that compensation also fails.

Step 3 is the one operation that may fail harmlessly. As with repository deletion, the row is the
source of truth: a file that cannot be removed is logged at warning and does not fail the request.

A delete of a package that is not in the visible set is a `404`, and so is a delete of one that was
already deleted; the operation is idempotent from the caller's point of view only in that a second
call cannot half-succeed.

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

Three of these behave differently for a package loaded from a file than the names suggest, and the
XML docs must say so, because the packages API is the only caller and every package it sees is
file-loaded:

* `GetSha256Sum` and `GetMd5Sum` return `null` — libalpm populates them from a sync database entry,
  and there is none. `PackageService` computes both itself; see
  [Checksums](#checksums-are-computed-not-read).
* `GetFileName` returns the path handed to `alpm_pkg_load`, not a basename. Nothing may use it to
  name a stored file; see [Storage](#storage-layout).

*Acceptance:* each new member is covered in `AlpmPackageTests`; the install-date fix has a test that
asserts null; tests pin the file-loaded behaviour of `GetSha256Sum`, `GetMd5Sum` and `GetFileName`
so that a later libalpm change is caught rather than silently changing what the API stores.

*Depends on:* nothing, but pairs naturally with issue 1.

### 3. `PacmanPackage` entity and migration — `MINOR`

The `PacmanPackage` entity as tabulated above, `PackageValidationConstants`, `PacmanPackages` on
`PacmanManagerDbContext`, a `RegularExpressions.PackageVersion`, and a migration named
`AddTable_PacmanPackages`.

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

`CheckPublish(repository, actor)` returning `RepositoryAccess`, answering for publish, replace and
delete alike; `PackageForbiddenException`; `AuthorizationExceptionHandler` extended with an arm
mapping it to `403` (without which it would fall through to a `500`).

*Acceptance:* `PackageAccessPolicyTests` covers every row of the rules table above as a plain unit
test, no database involved, mirroring `RepositoryAccessPolicyTests`; a handler test asserts
`PackageForbiddenException` produces `403`.

*Depends on:* 3.

### 7. `IPackageService` read paths — `MINOR`

`GetPackagesAsync`, `GetPackageByIdAsync`, `GetPackageByNameAsync(repositoryId, name)`, plus
`PackageFilter` (including its `IQueryObject<PacmanPackage>` implementation for `search`),
`PackageSortField`, `PackageSortExtensions`, and the semi-join `VisibleAsync`.

*Acceptance:* `PackageServiceTests` covers each filter and sort field; tests named for the
properties assert that no filter value widens the visible set (a private repository's packages stay
invisible to a non-owner, `repositoryIds` naming an invisible repository returns empty).
`search` has tests for a term whose case differs from the stored value and for a repository
containing a package with a null `Base` and null `Description`, both of which fail against the
naive `Contains`. `PackageServiceEnforcementTests` asserts `DbContext.PacmanPackages` is named in
exactly one method.

*Depends on:* 3, 6, 14.

### 8. Comma-separated array model binder — `PATCH`

A binder that accepts both `?ids=a,b` and `?ids=a&ids=b` for array-valued query parameters, applied
to `repositoryIds` and `publisherIds`.

*Acceptance:* unit tests over both forms, the mixed form, and empty/whitespace entries.

### 9. `PackagesController` read routes — `MINOR`

`GET /api/v1/packages`, `GET /api/v1/packages/{packageId}`, and the two repository-scoped aliases.
Thin controllers: log, call the service, translate `null` to `NotFound()`.

*Acceptance:* E2E tests for each route, including a private repository's package returning `404` to
a non-owner and `200` to its owner, and an anonymous listing showing public repositories only.

*Depends on:* 7, 8, 13.

### 10. Publishing — `MINOR`

`POST /api/v1/repositories/{repositoryId}/packages` end to end: authorize, stream to temp while
hashing, extract, validate, stage the row, `repo-add`, commit, per-repository serialization,
compensation on failure, configurable size limit.

*Acceptance:* service unit tests for the upsert (new → `201`-shaped result, existing → updated in
place with `Id`/`CreatedAt` preserved and `PublisherId` reassigned); architecture mismatch rejected;
`repo-add` failure leaving no row and no new file; and a `SaveChangesAsync` failure after a
successful `repo-add` invoking the compensating `repo-remove` — plus, on a replacement, the
`repo-add` that restores the previous file — which is the case that needs an explicitly faked
commit failure to reach. E2E tests publish a real fixture package and then read it back through
`GET`, assert the stored `sha256Sum`/`md5Sum` match the uploaded bytes and the `%SHA256SUM%`
`repo-add` recorded, and assert `403` publishing to someone else's public repository and `404` to
someone else's private one.

*Depends on:* 1, 2, 3, 4, 5, 6, 7, 13.

### 11. Package content download — `MINOR`

`GET /api/v1/packages/{packageId}/content` and its alias, streaming the stored file with a
`Content-Disposition` of the stored file name and `application/octet-stream`.

*Acceptance:* E2E test downloads a published package and compares the bytes to what was uploaded;
visibility behaves as for the metadata routes.

*Depends on:* 10.

### 12. Deleting — `MINOR`

`DELETE /api/v1/packages/{packageId}` and its alias: `repo-remove` then commit then file cleanup,
repository `UpdatedAt`, under the same per-repository lock publishing takes.

*Acceptance:* service unit tests for the authorization outcomes (the repository owner and, once
grants exist, any holder of `Publish` → `204`, including over a package someone else published;
another user on a public repository → `403`; another user on a private one → `404`); for a
`repo-remove` failure leaving the row intact and failing the request, with a lock-file failure
surfacing as `409`; for a commit failure after a successful `repo-remove` triggering the
compensating `repo-add`; and for a file deletion failure being logged but not failing the request.
E2E test deletes a published package and confirms it is gone from the listing.

*Depends on:* 10.

### 13. Rename `/api/v1/repository` to `/api/v1/repositories` — `MAJOR`

`ControllerConstants.ControllerBaseRoute` derives the segment from `[controller]`, so the rename is
either an explicit route on `RepositoryController` or a rename of the controller type. Breaking for
any existing client.

*Acceptance:* existing E2E tests updated to the new path; `/api/v1/repository` no longer resolves.

*Depends on:* nothing. Should land before 9 and 10 so the alias routes are written once.

### 14. Per-field default sort direction — `MINOR`

`SortOptions<TSortFields>.Direction` becomes `SortDirection?`, and `RepositorySortExtensions` (and,
in issue 7, `PackageSortExtensions`) resolves an omitted direction from the sort field: ascending
for `Name`, descending for everything else. Swagger parameter descriptions carry the effective
default now that `[DefaultValue]` cannot.

Changes the repository listing's behaviour for `sortBy=Name`, from descending to ascending, which is
why it is `MINOR` and separate rather than folded into the package work.

*Acceptance:* unit tests over each sort field with `direction` omitted and with each direction
supplied explicitly, for both listings; existing `RepositorySortExtensions` tests updated to the new
`Name` default.

*Depends on:* nothing. Must land before 7.

---

## Deferred work

Worth filing as issues, but explicitly out of scope for the work above.

* **Serving a pacman-consumable repository.** `GetRepositoryFileByIdAsync` and
  `GetRepositoryFileByNameAsync` exist on `IRepositoryService` but no route calls them, so the
  `.db.tar.gz` cannot be fetched at all. A `pacman` client configured with `Server = …` needs the
  database and every package file resolvable under one base URL by the basename `repo-add` recorded.
  Until that exists, this API can publish packages but nothing can install them.
* **Postgres full-text search** with a generated `tsvector` column, a GIN index and relevance
  ranking, replacing the substring match inside `PackageFilter.BuildQueryExpression`. Ranking also
  needs a `Relevance` member on `PackageSortField`, which is the part that cannot be faked with
  `Contains`.
* **Detached signatures.** Accepting a `.sig` alongside the package, storing it, passing it to
  `repo-add`, and serving it. Required for any repository with `SigLevel = Required`.
* **Multi-instance publishing.** The per-repository lock is in-process; concurrent publishes from
  two instances would rely on `repo-add`'s own lock file and surface as `409`s. A Postgres advisory
  lock keyed by repository id fixes it.
* **Reconciling a repository database from the rows.** Publish and delete compensate for a
  side-effect-succeeded-then-commit-failed window, but if the compensation itself fails the
  `.db.tar.gz` and the `PacmanPackages` rows disagree with nothing to detect or repair it. Since the
  rows are the source of truth and every package file is on disk, a database can be rebuilt from
  them with one `repo-add` per package; a maintenance command that does so — and a check that
  reports the drift — closes the last gap in the failure handling above.
* **Quotas.** Nothing bounds how much a user can upload.
* **`ItemExistsException` is still unused**, as noted in `authorization-plan.md`; a name collision
  in either API surfaces as a `500` rather than a `409`.
