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
global namespace, unique on `Name` alone across the deployment, so that a `pacman.conf`
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

*This describes the namespace after the global name index in*
[`pacman-controller.md`](pacman-controller.md#1c-the-global-name-index-and-its-migration--major) *lands.
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
[`pacman-controller.md`](pacman-controller.md#1b-look-a-repository-up-by-name-alone--major)*, which
reduces* `RepositoryKey` *to the name, and*
[*issue 1c*](pacman-controller.md#1c-the-global-name-index-and-its-migration--major)*, which makes the
index* `Name` *alone — architecture moves onto the repository as* `SupportedArchitectures`*, so it is
no longer part of any key. Issue 1b owns rewriting this section. The friendlier URL form this section used to call for —*
`{owner}/{name}/{arch}` *— was abandoned along with the user-facing name on* `User` *it would have
needed; a globally unique repository name removes the owner from the URL entirely, and*
[`user-management.md`](user-management.md#why-there-is-no-username) *records why that is the better
trade.*

## Known gaps

*   Deleting a repository removes the database row and then the backing `.db.tar.gz`. A failure to
    remove the file is logged and ignored, leaving an orphaned file that nothing references.

## Implementation plan

Stories that no single feature document owns, because more than one feature needs them.

### `ItemExistsException` → `409` — `PATCH`

The arm on `AuthorizationExceptionHandler` that maps `ItemExistsException` to `409 Conflict`, and
nothing else. Nothing raises the exception yet; that is left to the stories with a collision to
report — [Pacman Controller 1a](pacman-controller.md#1a-raise-itemexistsexception-on-repository-name-collisions--patch)
for a repository name and [Basic Auth 5](basic-auth.md#5-accesstokenscontroller--minor) for a token
name — and both depend on this one.

It is a story of its own because both of those need it and neither depends on the other. Folding it
into either would make one feature wait on the other, or leave the two racing to add it; on its own it
is a few lines and a handler test.

**The arm writes a fixed title and no `Detail`**, where the existing arms copy `exception.Message`
into it. A `409` on a repository name must disclose nothing but "taken" — see
[What hiding existence covers](#what-hiding-existence-covers) — and keeping the message out of the
body makes that true for every raise site by construction, rather than by each one remembering to
word its message carefully.

*Acceptance:* `AuthorizationExceptionHandlerTests` gains a case asserting that `ItemExistsException`
is handled, sets `409`, and writes a problem-details body that does not contain the exception's
message.

*Depends on:* nothing.
