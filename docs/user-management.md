# User Management

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

1. **User Management** (this document) — reading users, and changing your own display name.
2. [**Basic Auth**](basic-auth.md) — access tokens, and the HTTP Basic scheme that lets a
   `pacman.conf` carry credentials.
3. [**Pacman Controller**](pacman-controller.md) — the repository routes themselves.

The three are independent. Nothing here blocks either of the others, and nothing here depends on
them.

**Why:**

* [this document stands alone](#why-this-document-stands-alone)

## Goals

* A caller can read their own user record, including the parts that are not public.
* Anyone can look up a user by id, or list users, and learn nothing personal by doing so.
* A user can change their own display name, and nobody else's.

## Non-goals for this work

Recorded here so the issues stay bounded; each has a follow-up in
[Deferred work](#deferred-work).

* **Usernames.** There is no unique, user-facing name on `User`.
* **Administrative user management.** Nothing here lets one user act on another, or delete an
  account. The write route addresses the caller and only the caller.
* **Registration and deletion.** Users are still provisioned exclusively by `ClaimsTransformer` on
  first login, and there is no way to remove one.
* **Profiles.** No avatar, biography or organisation.
* **Access tokens**, which are [Basic Auth](basic-auth.md)'s subject even though they hang off
  `/api/v1/users/me`.

**Why:**

* [there is no username](#why-there-is-no-username)

---

## Routes

`/api/v1/users`, versioned by namespace and plural like every other resource.

| Method | Path | Auth | Success | Failure |
| :--- | :--- | :--- | :--- | :--- |
| `GET` | `/api/v1/users` | Anonymous | `200` page of users | — |
| `GET` | `/api/v1/users/{userId}` | Anonymous | `200` user | `404` |
| `GET` | `/api/v1/users/me` | Required | `200` current user | `401` |
| `PATCH` | `/api/v1/users/me` | Required | `200` updated | `400`, `401` |

* `me` is a literal alias on the read route. A literal segment beats the `{userId:guid}` constraint
  in route matching, so the two do not conflict.
* The write route accepts **`me` and nothing else**. `PATCH /api/v1/users/{userId}` does not exist at
  any id, including the caller's own, and must 404 from routing rather than be handled.
* There is **no `IUserAccessPolicy`**, and no visibility predicate on the user query. Every user is
  readable by everyone; only `me` is writable.
* The token routes under `/api/v1/users/me/tokens` share this path prefix but belong to
  [Basic Auth](basic-auth.md#routes), and are served by a controller of their own — they hang off a
  different resource.

**Why:**

* [`me` is the only way to reach the write route](#me-is-the-only-way-to-reach-the-write-route)
* [there is no `IUserAccessPolicy`](#there-is-no-iuseraccesspolicy)

### Models

`PublicUserInfo` is unchanged. It is already what `Repository.Owner` projects to, and it already
carries the only two things about a user that are public:

```jsonc
// PublicUserInfo — anonymous read routes and every embedded owner
{ "id": "0197…", "displayName": "Paul" }
```

`CurrentUser` is a separate model backing `/users/me`, adding `email`. **No anonymous route may
expose an email**, and `PublicUserInfo` must have no field capable of carrying one.

```jsonc
// CurrentUser — /api/v1/users/me only
{ "id": "0197…", "displayName": "Paul", "email": "paul@example.com" }
```

```jsonc
// WriteUserRequest — the body of PATCH /api/v1/users/me, every property optional
{ "displayName": "Paul" }
```

`WriteUserRequest` follows `WriteRepositoryRequest`'s naming. It holds the writable surface of a
user — today just `displayName` — and gains a property when something else becomes writable, without
the route changing shape.

The entity stays `User`, unprefixed, against the `Pacman` convention in
[`packages-api.md`](packages-api.md#entity); the wire models take the distinct names above.

**Why:**

* [`CurrentUser` is a separate model](#why-currentuser-is-a-separate-model)
* [`User` keeps its bare name](#why-user-keeps-its-bare-name)

### Listing

`PaginationParams`, a `UserFilter` and `SortOptions<UserSortField>`, as every listing does.

| Parameter | Applied by | Meaning |
| :--- | :--- | :--- |
| `displayNameContains` | `StringContainsQuery` | Substring of the display name, case-insensitive. |

* `UserSortField` has one member, `DisplayName`, carrying
  `[DefaultSortDirection(SortDirection.Ascending)]`. Being first, it makes an unsorted listing
  alphabetical. There is no `Created` member and no timestamp column to back one.
* `displayNameContains` must match regardless of case. Implement it by lowering both sides —
  `u.DisplayName.ToLower().Contains(term)`, with `term` lowered once in C# — as
  [`packages-api.md`](packages-api.md#listing) does for the package search. `DisplayName` is
  non-nullable, so it needs none of that section's null guarding.
* The listing is anonymous and projects `PublicUserInfo`. There is no `emailContains` and no other
  parameter that reaches `Email`.

**Why:**

* [`UserSortField` has one member](#why-usersortfield-has-one-member)
* [`displayNameContains` lowers both sides](#why-displaynamecontains-lowers-both-sides)
* [the listing can be anonymous](#why-the-listing-can-be-anonymous)
* [there is no `emailContains`](#why-there-is-no-emailcontains)

### Changing a display name

`PATCH /api/v1/users/me`, with the body modelled by
[`PTrampert.SimplePatch`](https://github.com/PaulTrampert/PTrampert.SimplePatch). The action takes
the generated patch object rather than the write model:

```csharp
[HttpPatch("me")]
public async Task<ActionResult<CurrentUser>> PatchCurrentUser(
    [FromBody] IPatchObject<WriteUserRequest> patch)
```

Required behaviour:

| Body | Result |
| :--- | :--- |
| `{"displayName": "Paul"}` | `200`, name changed |
| `{}` | `200`, nothing changed |
| `{"displayName": null}` | `400` — an explicit null never clears the name |
| name over `UserValidationConstants.DisplayNameMaxLength` | `400` |
| unauthenticated | `401` |

* The route reads the current user, projects it to a `WriteUserRequest` of its present values,
  applies the patch to that, and hands the result to `IUserManagementService`. **The entity is never patched
  directly** — `Patch(target)` returns a new instance rather than mutating its target.
* `WriteUserRequest.DisplayName` carries `[Required]` and
  `[MaxLength(UserValidationConstants.DisplayNameMaxLength)]` — the shared constant in
  `PacmanManager.Entities`, not a literal at the use site. These
  survive onto the generated patch object and apply **only when the property is present**, which is
  what makes an empty body a `200` and an explicit null a `400`. The controller keeps its ordinary
  `ModelState` check.
* `Program.cs` gains `opts.JsonSerializerOptions.AddSimplePatchConverters()` in the `AddJsonOptions`
  block it already has for camelCase, null omission and string enums.
* The patch type is **generated**, so its Swagger schema is not the write model's. The route must
  still document a body with an optional `displayName`; if it does not, that is a
  `ConfigureSwaggerGenOptions` fix belonging to the same issue.
* Display names are not unique and nothing addresses a user by one, so there is no `409` on this
  route.

`PUT /api/v1/repositories/{id}` is untouched. It replaces the whole writable surface, so the
omitted-versus-null question does not arise; if it ever becomes a `PATCH`, it gains the same
treatment.

**Why:**

* [`PATCH`, and `PTrampert.SimplePatch`](#why-patch-and-why-ptrampertsimplepatch)

### The service

The users API is served by a new **`IUserManagementService`**, not by `IUserService`.

| Method | Returns | Serves |
| :--- | :--- | :--- |
| `GetCurrentUserAsync` | `CurrentUser` | `GET /api/v1/users/me` |
| `GetUserByIdAsync` | `PublicUserInfo?` | `GET /api/v1/users/{userId}` |
| `ListUsersAsync` | a page of `PublicUserInfo` | `GET /api/v1/users` |
| `UpdateCurrentUserAsync` | `CurrentUser` | `PATCH /api/v1/users/me` |

* The two `me` methods find the caller through `ICurrentUserService` and throw
  `NoCurrentUserException` when there is none, which `AuthorizationExceptionHandler` already maps to
  `401`. Neither takes a user id.
* The service returns wire models, never the `User` entity, so no caller can project an email into an
  anonymous response by accident.
* **`IUserService` is not changed by this document.** It stays the pre-authentication service that
  `ClaimsTransformer` and [Basic Auth's token verification](basic-auth.md#two-services-own-this-not-one)
  use.

**Why:**

* [the users API has its own service](#why-the-users-api-has-its-own-service)

---

## Implementation plan

One issue per heading. Dependencies are noted; anything without a dependency can start immediately.
Nothing in this document depends on either of the other two.

### 1. Generic `ApplySort` — `PATCH`

Replace `RepositorySortExtensions` and `PackageSortExtensions` with one generic
`ApplySort<TEntity, TSortField>` taking the key-selector dictionary and the id selector, leaving each
listing with only the table that is genuinely its own.

*Acceptance:* the existing `RepositorySortExtensions` and `PackageSortExtensions` tests pass
unchanged against the generic implementation, including the unknown-`sortBy` fallback and the
stable-paging tie-break.

*Depends on:* nothing. Must land before 3.

**Why:**

* [`ApplySort` becomes generic now](#why-applysort-becomes-generic-now)

### 2. `IUserManagementService` — `MINOR`

The service [the users API is built on](#the-service): `IUserManagementService`, its implementation,
its DI registration, the `CurrentUser` model, and `GetCurrentUserAsync` — the one method that gives
the service something to test before the others arrive.

*Acceptance:* service unit tests that `GetCurrentUserAsync` returns the current user projected to
`CurrentUser`, and throws `NoCurrentUserException` when there is none.

*Depends on:* nothing.

**Why:**

* [the users API has its own service](#why-the-users-api-has-its-own-service)
* [`CurrentUser` is a separate model](#why-currentuser-is-a-separate-model)
* [`User` keeps its bare name](#why-user-keeps-its-bare-name)

### 3. `ListUsersAsync`, `UserFilter` and `UserSortField` — `MINOR`

The paged listing on `IUserManagementService`, and the filter and sort-field types it takes, as
[Listing](#listing) specifies. No route.

*Acceptance:* service unit tests for paging, for the filter, and for the unsorted default being
alphabetical by display name. No migration and no schema change: the sort field enum has one member
and `User` is untouched. A test that `displayNameContains` matches a term whose case differs from the
stored value, which fails against the naive `Contains`. A test that the result is projected to
`PublicUserInfo`, so no email can reach it.

*Depends on:* 1, 2.

**Why:**

* [`UserSortField` has one member](#why-usersortfield-has-one-member)
* [`displayNameContains` lowers both sides](#why-displaynamecontains-lowers-both-sides)
* [the listing can be anonymous](#why-the-listing-can-be-anonymous)
* [there is no `emailContains`](#why-there-is-no-emailcontains)

### 4. `GetUserByIdAsync` — `MINOR`

The lookup by id on `IUserManagementService`, returning `PublicUserInfo` or `null`. No route.

*Acceptance:* service unit tests that a known id returns that user as `PublicUserInfo` and an unknown
id returns `null`.

*Depends on:* 2.

**Why:**

* [there is no `IUserAccessPolicy`](#there-is-no-iuseraccesspolicy)

### 5. `UsersController` and its read routes — `MINOR`

The controller and all three read routes — `GET /api/v1/users`, `GET /api/v1/users/{userId}` and
`GET /api/v1/users/me` — each a thin call to the service method behind it.

*Acceptance:* E2E tests for each route; `me` returns the authenticated user and `401` without
authentication; a `404` for an unknown id; the listing pages, filters and sorts. The anonymous
routes — the listing and the lookup by id — never include an email, asserted against the raw response
body rather than a deserialised model so that an added property cannot slip past.

*Depends on:* 3, 4.

**Why:**

* [`me` is the only way to reach the write route](#me-is-the-only-way-to-reach-the-write-route)
* [the listing can be anonymous](#why-the-listing-can-be-anonymous)

### 6. `PATCH /api/v1/users/me` — `MINOR`

Changing the display name, with `WriteUserRequest`, the `PTrampert.SimplePatch` package reference,
`AddSimplePatchConverters()` on the existing `AddJsonOptions` block, the route on `UsersController`,
and `UpdateCurrentUserAsync` on `IUserManagementService` behind it.

*Constraints:* the behaviour table and the four bullets under
[Changing a display name](#changing-a-display-name) are the specification for this issue. The
`[Required]`-with-empty-body pairing in particular is deliberate, not a contradiction.

*Acceptance:* E2E tests: the change takes effect and is visible from `GET /api/v1/users/{userId}`;
an over-long name is a `400`; the route is a `401` unauthenticated; and there is no route by which
one user can change another's, asserted by `PATCH /api/v1/users/{someOtherId}` returning `404` from
routing rather than being handled.

The patch semantics get their own tests, because they are the reason the library is here: an empty
body `{}` leaves the display name untouched and is a `200` rather than a `400`; a body naming only
`displayName` changes only it; and an explicit `{"displayName": null}` is a `400` from the `Required`
attribute rather than silently clearing the name. A test also asserts the route's Swagger schema
still shows an optional `displayName`, since the patch type is generated rather than declared.

*Depends on:* 5.

**Why:**

* [`PATCH`, and `PTrampert.SimplePatch`](#why-patch-and-why-ptrampertsimplepatch)
* [`me` is the only way to reach the write route](#me-is-the-only-way-to-reach-the-write-route)

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

---

## Appendix

The argument for each decision in the plan, including what was considered and rejected. Nothing
here adds a requirement — the plan above is the specification. These entries record what is
**settled**: if implementation suggests a different choice, the entry is the thing to argue with,
in an issue, rather than something to quietly depart from.

### Why this document stands alone

An earlier draft of this design routed repositories under their owner's name, which required a
unique username on `User` and made this document a prerequisite for the other two. [Repositories are
now addressed by a globally unique name](pacman-controller.md#repository-names-are-globally-unique)
instead, so nothing here blocks anything else — and the username, along with the client breakage that
renaming one would have caused, is gone from the design entirely.

What remains is worth doing on its own. The API has no way to read a user at all today, not even
your own, which is a gap [`authorization-plan.md`](authorization-plan.md#filtering) already names:
it rejected an "only mine" repository filter on the grounds that it means the same thing as passing
your own id in `ownerId`, and noted that "callers that do not yet know their own id will get an
endpoint that tells them." This is that endpoint.

### Why there is no username

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

### `me` is the only way to reach the write route

A route incapable of naming another user cannot be got wrong: there is no check to forget, no
id-confusion bug to write, and nothing for a future administrative feature to accidentally widen.
That is why there is no `PATCH /api/v1/users/{userId}` guarded by a comparison against the caller.
When one user genuinely needs to act on another, that is an administrative capability with its own
route and its own permission, not a relaxation of this one.

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

### Why `CurrentUser` is a separate model

Email could have been a nullable field on `PublicUserInfo`, populated only on `/users/me`. A field
that is only sometimes populated is a field that will one day be populated by accident — by a
projection written in a hurry, or by a later route that reuses the model without noticing which
branch fills it in. Two models make the anonymous shape incapable of carrying an email at all, which
is a guarantee rather than a convention.

### Why `User` keeps its bare name

[`packages-api.md`](packages-api.md#entity) records that entities take a `Pacman` prefix and the wire
model keeps the bare name. `User` is the exception: the entity shipped unprefixed, and renaming it to
`PacmanUser` would touch the whole solution for consistency alone — a user is not a pacman-domain
concept the way a repository or a package is. Since the bare name is taken by the entity, the wire
models get names of their own, `PublicUserInfo` and `CurrentUser`.

### Why `UserSortField` has one member

`User` is `Id`, `DisplayName` and `Email` and nothing more — it has no creation timestamp — so a sort
by `Created` would mean a column, a migration and a backfill in service of an ordering nobody has
asked for. Adding the column is [deferred](#deferred-work).

A single-member enum still earns its place: it keeps this listing on the same
`SortOptions<TSortField>` shape as every other one, so a second sort field later is an added enum
member rather than a new query parameter.

### Why `displayNameContains` lowers both sides

The naive `Contains` is case-sensitive under both Npgsql, where it translates to a case-sensitive
`LIKE`, and `Microsoft.EntityFrameworkCore.InMemory`, where it is `string.Contains` — so a
case-insensitive match has to be written as one. Lowering both sides is what
[`packages-api.md`](packages-api.md#listing) settled on for the package search, and using the same
shape here keeps one idiom rather than two.

### Why the listing can be anonymous

The listing exposes nothing but a label a user chose to be shown by. Email is not projected, and no
query parameter reaches it, so there is nothing an anonymous caller learns about a person that the
person did not publish by picking a display name.

### Why there is no `emailContains`

It would turn the anonymous listing into an oracle for "does this person have an account here",
answerable one guess at a time.

### Why `PATCH`, and why `PTrampert.SimplePatch`

`PATCH` rather than `PUT`, so that a route which grows a second writable field later does not force
callers to resend the first.

The library solves the one problem that makes `PATCH` harder than `PUT`: telling a property that was
**omitted** from one that was explicitly sent as `null`. A plain write model cannot express the
difference — a null `DisplayName` has to mean either "leave it alone" or "clear it", and whichever
convention is chosen, the other operation becomes unreachable.

Three properties of the library decide the shape the plan describes:

* **`Patch(target)` returns a new instance and does not mutate the target**, and its target is the
  *write model*, not the entity. Hence the read-project-apply-hand-off sequence: the entity is never
  patched directly, which keeps the service's input a validated model rather than a half-applied one.
* **Validation attributes survive onto the patch object**, and a `[Required]` property is only
  required *when it is present*. `[Required]` plus `[MaxLength(DisplayNameMaxLength)]` therefore
  reads exactly as intended, and the controller keeps its ordinary `ModelState` check.
* **Registration is one line**, `opts.JsonSerializerOptions.AddSimplePatchConverters()`.

Adding the package is consistent with `CONTRIBUTING.md`'s dependency rule — check what is already
there before adding to it. `PTrampert.QueryObjects` is already a `RepoHost` dependency and every
listing in this API is built on it, so this is the same ecosystem rather than a new one.

### Why the users API has its own service

`IUserService` already exists and would have been the obvious home. It was not used, because of who
calls it: `ClaimsTransformer` provisions users through it on first login, and
[Basic Auth](basic-auth.md#why-verification-is-on-iuserservice) verifies access tokens through it,
both before any actor or current user exists. Adding methods that depend on `ICurrentUserService` to
that type would put pre-authentication and post-authentication methods side by side, which is the
mixed shape [Basic Auth](basic-auth.md#why-token-management-is-not-on-iuserservice) already declined
for token management.

A separate service also keeps the wire models on one side of a line: `IUserService` deals in the
`User` entity for authentication's sake, and `IUserManagementService` deals only in `PublicUserInfo`
and `CurrentUser`, so nothing it returns can carry an email to a route that should not show one.

Building it as its own story, before any method that needs a route, lets the listing and the lookup by
id each land as a small, independent change on top of it.

### Why `ApplySort` becomes generic now

`RepositorySortExtensions` and `PackageSortExtensions` are the same twenty lines with two type
parameters substituted: a dictionary from sort field to key selector, a `TryGetValue` falling back to
`default`, a direction ternary, and a `ThenBy` on the id. The users listing would be a third copy,
which is the point at which the duplication stops being cheaper than the abstraction.
