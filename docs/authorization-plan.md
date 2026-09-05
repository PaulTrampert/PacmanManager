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

`GET /api/v1/repository` accepts a `RepositoryFilter` bound from the query string:
`nameContains`, `architecture`, `isPublic`, `ownerId`, `mineOnly` and `sort`. Paging is separate,
in `PaginationParams` (`offset`, `pageSize`).

The filter is applied to the already-visible query and ANDed onto it, so **no combination of
filter values can widen what a caller sees**. Asking for `isPublic=false` returns the caller's own
private repositories and nothing else; an anonymous caller asking the same gets an empty page.
`mineOnly=true` yields nothing for an anonymous caller rather than degrading into "no owner
restriction". These properties are covered by tests named after them in `RepositoryServiceTests`.

An arbitrary dynamic query language (OData, `System.Linq.Dynamic.Core`) was considered and
rejected: composing user-supplied expressions with a security predicate is difficult to reason
about, and the domain's filter surface is small and knowable.

## Looking a repository up by name

A repository name on its own identifies nothing. The database enforces uniqueness over
`(OwnerId, Name, Architecture)`, so two users may each own a repository called `custom`, and one
user may own both an `x86_64` and an `any` repository under that name.

`GetRepositoryByNameAsync` therefore takes a `RepositoryKey` — that same triple — rather than a
bare string, which means the lookup matches at most one row and needs no tie-breaking rule.
Supplying an owner is not a way around the visibility rules: the key selects a row from the
already-visible set, so naming someone else's private repository still returns nothing.

Note that the owner is identified by user id. A friendlier URL form (`{owner}/{name}/{arch}`, as
pacman's `Server` setting would want) needs a stable, unique, user-facing name on `User`, which
does not exist yet.

## Known gaps

*   Deleting a repository removes the database row and then the backing `.db.tar.gz`. A failure to
    remove the file is logged and ignored, leaving an orphaned file that nothing references.
*   Renaming a repository into a collision with the `(OwnerId, Name, Architecture)` index surfaces
    as a `DbUpdateException`, and so a `500`, rather than a `409`. `ItemExistsException` exists for
    this but is not yet raised anywhere.
