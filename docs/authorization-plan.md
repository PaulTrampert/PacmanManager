# Repository Authorization

## Overview

This document describes how authorization is enforced for pacman repositories, and why the
enforcement lives where it does.

### Authorization Rules

| Scenario | Repository Visibility | User Relationship | Required HTTP Result |
| :--- | :--- | :--- | :--- |
| **Read** | Private | Owner | `200 OK` |
| **Read** | Private | Not Owner | `404 Not Found` (Hide existence) |
| **Read** | Public | Any | `200 OK` |
| **Write/Delete** | Private | Owner | `200/201/204 OK` |
| **Write/Delete** | Private | Not Owner | `404 Not Found` (Hide existence) |
| **Write/Delete** | Public | Not Owner | `403 Forbidden` (Identify as public, deny access) |
| **Create** | n/a | Unauthenticated | `401 Unauthorized` |

### What hiding existence covers

"Hide existence" in the table above is a promise about the *repository*: whether it exists, who owns
it, what it holds, and whether it is yours. It is not a promise about every string associated with
it, and one narrowing is accepted deliberately.

**A repository's name is public, even when the repository is private.** Repository names are a
global namespace, unique on `(Name, Architecture)` across the deployment, so that a `pacman.conf`
section name and the name this application knows are always the same string —
[`pacman-controller.md`](pacman-controller.md#repository-names-are-globally-unique) has the full
reasoning. A namespace that refuses duplicates cannot avoid telling a caller that a duplicate is
what they have, so creating or renaming into a taken name fails, and that failure is observable.
Anyone can therefore discover whether a name is in use, one guess at a time, **including when the
repository holding it is private**.

This is inherent to a global namespace rather than a defect in the enforcement above — npm, PyPI,
crates.io and Docker Hub all disclose name existence the same way — and it is accepted as the price
of the section-name property. Two bounds on it are requirements, not observations:

*   **The `409` discloses nothing but "taken".** Not the owner, not whether the repository is
    public, not when it was created.
*   **The collision response is the only new signal.** Every row of the table above still holds. A
    private repository is still `404` by id and by name, still absent from
    `GET /api/v1/repositories`, and still `404` from every package and pacman route.

So the property to state to a user is that a private repository's *name* is not a secret, while its
existence-as-yours, its owner, its contents and its packages are. A name that has to be unguessable
must be chosen to be unguessable.

*This describes the namespace after the index change in*
[`pacman-controller.md`](pacman-controller.md#1-globally-unique-repository-names--major) *lands.
Until then uniqueness is per owner, as* [Looking a repository up by name](#looking-a-repository-up-by-name)
*describes, and no name is disclosed.*

## Architecture Strategy: Enforcement in the Service Layer

An earlier draft of this plan proposed an `IAsyncAuthorizationFilter` attribute applied to
controller actions. That was rejected because it only protects HTTP callers: CLI tools and
background jobs talk to `IRepositoryService` directly and would bypass it entirely.

Instead, `RepositoryService` itself enforces the rules, against an **actor** that the host
supplies. Its public methods take no user, principal or permission argument, so no caller — a
controller, a CLI tool, a job — has to reason about authorization to use it correctly.

### The pieces

*   **`Actor`** (`Authentication/Actor.cs`) — the identity a unit of work runs as. Either
    anonymous, a specific `User`, or a trusted system host (optionally acting for a user, which is
    what lets a system host create owned repositories).
*   **`IActorAccessor`** (`Services/IActorAccessor.cs`) — supplies the actor for the current
    scope. `HttpContextActorAccessor` derives it from the authenticated principal;
    `FixedActorAccessor` is for CLI tools, background jobs and tests. Nothing is registered by
    default, so a host that fails to choose one fails at dependency resolution rather than
    silently running unauthenticated.
*   **`RepositoryAccessPolicy`** (`Services/RepositoryAccessPolicy.cs`) — the single definition of
    the rules table above. It has no database, HTTP or logging dependency, so every row of that
    table is a plain unit test in `RepositoryAccessPolicyTests`. `VisibleTo` returns an expression
    that composes into an EF Core query; `CheckWrite` and `CheckCreate` return a
    `RepositoryAccess` verdict.

### Why this is hard to get wrong

`RepositoryService` has exactly one private method — `VisibleAsync` — that touches
`DbContext.PacmanRepositories`, and it applies `VisibleTo` before returning the query. Every read
and every write starts there. A method that skips the rules therefore has to name the `DbSet`
itself, which is conspicuous in review and is asserted against by
`RepositoryServiceEnforcementTests`.

Writes then run through `LoadForWriteAsync`, which loads from the visible set and translates the
policy verdict into the result the caller expects. A private repository owned by someone else
never reaches the check — it is already absent from the visible set — so the 404-versus-403
distinction falls out of the structure rather than being restated per operation.

### HTTP semantics

Services do not know about HTTP, so they signal the two non-`null` outcomes with exceptions:
`NoCurrentUserException` and `RepositoryForbiddenException`. `AuthorizationExceptionHandler`
(registered as an `IExceptionHandler`) maps them to `401` and `403`. Everything else is expressed
as a `null` or `false` return, which controllers turn into `404`.

## Filtering

`GET /api/v1/repositories` accepts a `RepositoryFilter` bound from the query string:
`nameContains`, `architecture`, `isPublic` and `ownerId`. Ordering is separate, in
`SortOptions<RepositorySortField>` (`sortBy`, `direction`), as is paging, in `PaginationParams`
(`offset`, `pageSize`): a filter decides which repositories are in the result set, a sort only the
sequence they come back in.

`SortOptions<TSortFields>` is generic so that every listing spells ordering the same way. Only the
set of sortable properties differs between endpoints, so only that is a type parameter, and
`sortBy`/`direction` mean the same thing everywhere by construction. The first member of the
sort-field enum is the default, since an omitted parameter binds to the enum's zero value.

Each criterion is annotated with a `PTrampert.QueryObjects` attribute saying how it narrows the
query, and the library turns the filter into the predicate. There is deliberately no "only mine"
criterion: it would mean the same thing as passing the caller's own id in `ownerId`, and a second
spelling of one criterion is a second thing to keep correct. Callers that do not yet know their
own id will get an endpoint that tells them.

The filter is applied to the already-visible query and ANDed onto it, so **no combination of
filter values can widen what a caller sees**. Asking for `isPublic=false` returns the caller's own
private repositories and nothing else; an anonymous caller asking the same gets an empty page.
Naming an owner in `ownerId` likewise narrows the visible set rather than reaching into that
owner's private repositories. These properties are covered by tests named after them in
`RepositoryServiceTests`.

An arbitrary dynamic query language (OData, `System.Linq.Dynamic.Core`) was considered and
rejected: composing user-supplied expressions with a security predicate is difficult to reason
about, and the domain's filter surface is small and knowable. The annotated query object keeps
that property, because the set of criteria is still fixed by the type.

## Looking a repository up by name

A repository name on its own identifies nothing. The database enforces uniqueness over
`(OwnerId, Name, Architecture)`, so two users may each own a repository called `custom`, and one
user may own both an `x86_64` and an `any` repository under that name.

`GetRepositoryByNameAsync` therefore takes a `RepositoryKey` — that same triple — rather than a
bare string, which means the lookup matches at most one row and needs no tie-breaking rule.
Supplying an owner is not a way around the visibility rules: the key selects a row from the
already-visible set, so naming someone else's private repository still returns nothing.

Note that the owner is identified by user id.

*Both paragraphs above are superseded by*
[`pacman-controller.md`](pacman-controller.md#1-globally-unique-repository-names--major)*, which
makes the index* `(Name, Architecture)` *and reduces* `RepositoryKey` *to that pair. That issue owns
rewriting this section. The friendlier URL form this section used to call for —*
`{owner}/{name}/{arch}` *— was abandoned along with the user-facing name on* `User` *it would have
needed; a globally unique repository name removes the owner from the URL entirely, and*
[`user-management.md`](user-management.md#why-there-is-no-username) *records why that is the better
trade.*

## Known gaps

*   Deleting a repository removes the database row and then the backing `.db.tar.gz`. A failure to
    remove the file is logged and ignored, leaving an orphaned file that nothing references.
*   Renaming a repository into a collision with the `(OwnerId, Name, Architecture)` index surfaces
    as a `DbUpdateException`, and so a `500`, rather than a `409`. `ItemExistsException` exists for
    this but is not yet raised anywhere. This is fixed by
    [`pacman-controller.md`](pacman-controller.md#1-globally-unique-repository-names--major): a
    global namespace turns a collision from a rare edge case into something a user hits routinely,
    which is what finally makes it worth raising properly.
