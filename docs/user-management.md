# User Management

Status: **design**. This document is the source the implementation issues are cut from. It follows
the conventions established in [`authorization-plan.md`](authorization-plan.md) and
[`packages-api.md`](packages-api.md); where it departs from them, it says so.

It is one of three documents that together let a `pacman` client install from a hosted repository:

1. **User Management** (this document) — reading users, and changing your own display name.
2. [**Basic Auth**](basic-auth.md) — access tokens, and the HTTP Basic scheme that lets a
   `pacman.conf` carry credentials.
3. [**Pacman Controller**](pacman-controller.md) — the repository routes themselves.

**The three are independent.** An earlier draft of this design routed repositories under their
owner's name, which required a unique username on `User` and made this document a prerequisite for
the other two. [Repositories are now addressed by a globally unique
name](pacman-controller.md#repository-names-are-globally-unique) instead, so nothing here blocks
anything else — and the username, along with the client breakage that renaming one would have
caused, is gone from the design entirely.

What remains is worth doing on its own. The API has no way to read a user at all today, not even
your own, which is a gap [`authorization-plan.md`](authorization-plan.md#filtering) already names:
it rejected an "only mine" repository filter on the grounds that it means the same thing as passing
your own id in `ownerId`, and noted that "callers that do not yet know their own id will get an
endpoint that tells them." This is that endpoint.

## Goals

* A caller can read their own user record, including the parts that are not public.
* Anyone can look up a user by id, or list users, and learn nothing personal by doing so.
* A user can change their own display name, and nobody else's.

## Non-goals for this work

Recorded here so the issues stay bounded; each has a follow-up in
[Deferred work](#deferred-work).

* **Usernames.** There is no unique, user-facing name on `User`, because nothing needs one any more.
  See [Why there is no username](#why-there-is-no-username).
* **Administrative user management.** Nothing here lets one user act on another, or delete an
  account. The write route addresses the caller and only the caller.
* **Registration and deletion.** Users are still provisioned exclusively by `ClaimsTransformer` on
  first login, and there is no way to remove one.
* **Profiles.** No avatar, biography or organisation.
* **Access tokens**, which are [Basic Auth](basic-auth.md)'s subject even though they hang off
  `/api/v1/users/me`.

---

## Why there is no username

A username would be a second identifier for a user, and this design has no use for one.

The reason to want it was the route. Addressing a repository as
`/repos/{ownerName}/{repoName}/{architecture}` needs a stable, unique, URL-safe name on `User`, and
[`authorization-plan.md`](authorization-plan.md#looking-a-repository-up-by-name) records that the
absence of one is what stood between this application and a pacman-consumable URL. Adding it brought
a schema decision about case-insensitive uniqueness, a normalisation and generation rule, a backfill
migration, a reserved-name list, and — worst — a rename that silently broke every `pacman.conf`
naming the old value, along with a table of retired names to stop a released name being picked up by
somebody else.

All of that existed to support one path segment. [Dropping the segment](pacman-controller.md#the-route-root)
removes the whole apparatus.

It does not, on its own, remove the class of failure where a user's client quietly starts tracking a
stranger's repository: a *repository* rename releases a name into a global namespace with the same
consequence, one level up. What it does is reduce two occurrences of that hazard to one, and the
remaining one is
[answered where it now lives](pacman-controller.md#renaming-a-repository) — a reserved name, a
temporary redirect, and a release only once nothing is asking for it. A user rename, by contrast, now
has no effect on any URL at all, which is the stronger outcome and the one worth having.

`User` therefore keeps exactly what it has: an `Id` that identifies it, a `DisplayName` that is a
label with no uniqueness requirement, and an `Email` that is personal data and stays out of every
anonymous response. A display name can be changed freely precisely because nothing addresses a user
by it.

If a username is ever wanted again — for a friendlier profile URL, say — it is additive, and it
arrives without the rename hazard, because no repository URL would depend on it.

---

## Routes

`/api/v1/users`, versioned by namespace and plural like every other resource.

| Method | Path | Auth | Success | Failure |
| :--- | :--- | :--- | :--- | :--- |
| `GET` | `/api/v1/users` | Anonymous | `200` page of users | — |
| `GET` | `/api/v1/users/{userId}` | Anonymous | `200` user | `404` |
| `GET` | `/api/v1/users/me` | Required | `200` current user | `401` |
| `PATCH` | `/api/v1/users/me` | Required | `200` updated | `400`, `401` |

The token routes under `/api/v1/users/me/tokens` share this path prefix but belong to
[Basic Auth](basic-auth.md#routes), and are served by a controller of their own for the same reason
the repository-scoped package routes are: they hang off a different resource.

### `me` is the only way to reach the write route

`GET /api/v1/users/{userId}` accepts a real id, and `me` is a literal alias for it — a literal
segment beats the `{userId:guid}` constraint in route matching, so the two do not conflict.

The write route accepts **`me` and nothing else**. There is deliberately no
`PATCH /api/v1/users/{userId}` that checks whether `{userId}` is the caller, because a route
incapable of naming another user cannot be got wrong: there is no check to forget, no id-confusion
bug to write, and nothing for a future administrative feature to accidentally widen. When one user
genuinely needs to act on another, that is an administrative capability with its own route and its
own permission, not a relaxation of this one.

The asymmetry with the read route is intentional. Reading a user is a public act; changing one is
not.

### There is no `IUserAccessPolicy`

Every other resource in this application has an access policy, and this one does not, which is worth
justifying rather than leaving as an omission.

A policy exists to answer a question that has more than one answer. Repositories have visibility,
ownership and a `Publish` grant, so `RepositoryAccessPolicy` earns its place. Users have exactly two
rules — everything on the public model is readable by anyone, and only `me` can be written — and the
second is enforced by the route not being able to express anything else. A policy object here would
be a class whose every method returns a constant, and a second place to look for a rule that the
route already settles.

If an administrative capability ever arrives, it brings a real question with it, and a policy with
it.

### Models

`PublicUserInfo` is unchanged. It is already what `Repository.Owner` projects to, and it already
carries the only two things about a user that are public:

```jsonc
// PublicUserInfo — anonymous read routes and every embedded owner
{ "id": "0197…", "displayName": "Paul" }
```

A separate `CurrentUser` model backs `/users/me` and adds `email`. **The anonymous routes must not
expose email**, which is why this is a second model rather than a nullable field on the first: a
field that is only sometimes populated is a field that will one day be populated by accident.

```jsonc
// CurrentUser — /api/v1/users/me only
{ "id": "0197…", "displayName": "Paul", "email": "paul@example.com" }
```

```jsonc
// WriteUserRequest — the body of PATCH /api/v1/users/me, every property optional
{ "displayName": "Paul" }
```

`WriteUserRequest` follows `WriteRepositoryRequest`'s naming. It is the model the patch is *against*,
so it holds the writable surface of a user — today just `displayName` — and gains a property when
something else becomes writable, without the route changing shape.

**A note on the naming convention.** [`packages-api.md`](packages-api.md#entity) records that
entities take a `Pacman` prefix and the wire model keeps the bare name. `User` is the exception: the
entity shipped unprefixed, and renaming it to `PacmanUser` would touch the whole solution for
consistency alone — a user is not a pacman-domain concept the way a repository or a package is. So
the wire models are `PublicUserInfo` and `CurrentUser` rather than `User`.

### Listing

`PaginationParams`, a `UserFilter` and `SortOptions<UserSortField>`, as every listing does.

| Parameter | Applied by | Meaning |
| :--- | :--- | :--- |
| `displayNameContains` | `StringContainsQuery` | Substring of the display name. |

`UserSortField`: `DisplayName`, and nothing else. It is first, so an unsorted listing is
alphabetical, and it carries `[DefaultSortDirection(SortDirection.Ascending)]`.

**There is deliberately no `Created`.** `User` is `Id`, `DisplayName` and `Email` and nothing more —
it has no creation timestamp — so a sort by one would mean a column, a migration and a backfill in
service of an ordering nobody has asked for. A single-member enum still earns its place: it keeps
this listing on the same `SortOptions<TSortField>` shape as every other one, so a second sort field
later is an added enum member rather than a new query parameter. Adding the column is
[deferred](#deferred-work).

`displayNameContains` must be **case-insensitive**, which the naive `Contains` is not: it is
case-sensitive under both Npgsql, where it translates to a case-sensitive `LIKE`, and
`Microsoft.EntityFrameworkCore.InMemory`, where it is `string.Contains`. Lower both sides —
`u.DisplayName.ToLower().Contains(term)` with `term` lowered once in C# — exactly as
[`packages-api.md`](packages-api.md#listing) documents for the package search, and for the same
reason. `DisplayName` is non-nullable, so it needs none of that section's null guarding.

Users have no visibility rules — every user is visible to everyone, which is what makes the listing
anonymous. The reason that is acceptable is that the listing exposes nothing but a label a user
chose to be shown by. Email is not projected, and no query parameter reaches it.

There is no `emailContains`, deliberately. It would turn the anonymous listing into an oracle for
"does this person have an account here", answerable one guess at a time.

### Changing a display name

A display name is not unique and nothing addresses a user by it, so there is no collision to report
and no `409` on this route. It validates against `UserValidationConstants.DisplayNameMaxLength` and
is otherwise accepted as given.

`PATCH` rather than `PUT`, so that a route which grows a second writable field later does not force
callers to resend the first.

**The patch itself is [`PTrampert.SimplePatch`](https://github.com/PaulTrampert/PTrampert.SimplePatch)**,
rather than anything hand-rolled. It solves the one problem that makes `PATCH` harder than `PUT`:
telling a property that was **omitted** from one that was explicitly sent as `null`. A plain write
model cannot express the difference — a null `DisplayName` has to mean either "leave it alone" or
"clear it", and whichever convention is chosen, the other operation becomes unreachable.

The action takes the generated patch object instead of the write model:

```csharp
[HttpPatch("me")]
public async Task<ActionResult<CurrentUser>> PatchCurrentUser(
    [FromBody] IPatchObject<WriteUserRequest> patch)
```

Three properties of the library decide the shape of the rest:

* **`Patch(target)` returns a new instance and does not mutate the target**, and its target is the
  *write model*, not the entity. So the route reads the current user, projects it to a
  `WriteUserRequest` of its present values, applies the patch to that, and hands the result to
  `IUserService`. The entity is never patched directly, which keeps the service's input a validated
  model rather than a half-applied one.
* **Validation attributes survive onto the patch object**, and a `[Required]` property is only
  required *when it is present*. `[Required]` plus `[MaxLength(DisplayNameMaxLength)]` on
  `WriteUserRequest.DisplayName` therefore reads exactly as intended: omit it and nothing happens,
  send it and it must be a valid display name. The controller keeps its ordinary `ModelState` check.
* **Registration is one line**, `opts.JsonSerializerOptions.AddSimplePatchConverters()`, added to the
  `AddJsonOptions` block `Program.cs` already has for camelCase, null omission and string enums.

Adding the package is consistent with `CONTRIBUTING.md`'s dependency rule — check what is already
there before adding to it. `PTrampert.QueryObjects` is already a `RepoHost` dependency and every
listing in this API is built on it, so this is the same ecosystem rather than a new one.

One thing the implementing issue has to check rather than assume: the patch type is **generated**,
so its Swagger schema is not the write model's. `PATCH /api/v1/users/me` must still document a body
with an optional `displayName`, and if it does not, that is a `ConfigureSwaggerGenOptions` problem to
solve in the same issue rather than a surprise for the next person.

`PUT /api/v1/repositories/{id}` is untouched by this. It is a `PUT` precisely because it replaces the
whole writable surface, and the omitted-versus-null question does not arise; if it ever becomes a
`PATCH`, it gains the same treatment.

---

## Implementation plan

One issue per heading. Dependencies are noted; anything without a dependency can start immediately.
Nothing in this document depends on either of the other two.

### 1. Generic `ApplySort` — `PATCH`

`RepositorySortExtensions` and `PackageSortExtensions` are the same twenty lines with two type
parameters substituted: a dictionary from sort field to key selector, a `TryGetValue` falling back to
`default`, a direction ternary, and a `ThenBy` on the id. The users listing would be a third copy,
which is the point at which the duplication stops being cheaper than the abstraction.

Replace them with one generic `ApplySort<TEntity, TSortField>` taking the key-selector dictionary and
the id selector, leaving each listing with only the table that is genuinely its own.

*Acceptance:* the existing `RepositorySortExtensions` and `PackageSortExtensions` tests pass
unchanged against the generic implementation, including the unknown-`sortBy` fallback and the
stable-paging tie-break.

*Depends on:* nothing. Must land before 3.

### 2. `UsersController` read routes — `MINOR`

`GET /api/v1/users/{userId}`, `GET /api/v1/users/me`, the `CurrentUser` model, and the service
methods behind them.

*Acceptance:* E2E tests for each route; `me` returns the authenticated user and `401` without
authentication; a `404` for an unknown id. The anonymous route never includes an email, asserted
against the raw response body rather than a deserialised model so that an added property cannot slip
past.

*Depends on:* nothing.

### 3. `UserFilter`, `UserSortField` and the listing — `MINOR`

`GET /api/v1/users`, the filter, the sort field enum with its default direction, and the paged
listing.

*Acceptance:* service unit tests for the filter and for the unsorted default being alphabetical by
display name. No migration and no schema change: the sort field enum has one member and `User` is
untouched. A test that `displayNameContains` matches a term whose case differs
from the stored value, which fails against the naive `Contains`. An E2E test asserts the listing
exposes no email, against the raw body.

*Depends on:* 1, 2.

### 4. `PATCH /api/v1/users/me` — `MINOR`

Changing the display name, with `WriteUserRequest`, the `PTrampert.SimplePatch` package reference,
`AddSimplePatchConverters()` on the existing `AddJsonOptions` block, and the service method behind
it.

*Acceptance:* E2E tests: the change takes effect and is visible from `GET /api/v1/users/{userId}`;
an over-long name is a `400`; the route is a `401` unauthenticated; and there is no route by which
one user can change another's, asserted by `PATCH /api/v1/users/{someOtherId}` returning `404` from
routing rather than being handled.

The patch semantics get their own tests, because they are the reason the library is here: an empty
body `{}` leaves the display name untouched and is a `200` rather than a `400`; a body naming only
`displayName` changes only it; and an explicit `{"displayName": null}` is a `400` from the `Required`
attribute rather than silently clearing the name. A test also asserts the route's Swagger schema
still shows an optional `displayName`, since the patch type is generated rather than declared.

*Depends on:* 2.

---

## Deferred work

Worth filing as issues, but explicitly out of scope for the work above.

* **Administrative user management** — an operator's ability to rename, suspend or delete an account,
  with a real access policy behind it. This is the change that would give
  [`IUserAccessPolicy`](#there-is-no-iuseraccesspolicy) something to decide.
* **Account deletion**, which needs an answer for the repositories and packages a user owns before
  it can be specified at all.
* **A creation timestamp on `User`**, which is what a `Created` member on
  [`UserSortField`](#listing) would need and the reason there isn't one. Additive — a nullable
  column, or a non-nullable one backfilled from the earliest thing that references each user.
* **A username**, if a friendlier profile URL is ever wanted. Additive, and — now that no repository
  URL depends on it — free of the rename hazard that made it expensive here.
