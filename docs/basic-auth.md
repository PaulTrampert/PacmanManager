# Basic Auth

Status: **design**. This document is the source the implementation issues are cut from. It follows
the conventions established in [`authorization-plan.md`](authorization-plan.md) and
[`packages-api.md`](packages-api.md); where it departs from them, it says so.

It is one of three documents that together let a `pacman` client install from a hosted repository:

1. [**User Management**](user-management.md) — reading users, and changing your own display name.
2. **Basic Auth** (this document) — access tokens, and the HTTP Basic scheme.
3. [**Pacman Controller**](pacman-controller.md) — the repository routes themselves.

This document depends on neither of the others and can land at any time. [Pacman
Controller](pacman-controller.md) depends on it, for the credentials a private repository needs.

`pacman` cannot obtain or refresh an OAuth token. It has one credential mechanism: a URL with
userinfo in it, which libcurl turns into an `Authorization: Basic` header. So a private repository is
reachable by `pacman` only if this application accepts Basic credentials — and the thing being
handed to a package manager on a laptop must not be the account password, and must not be able to
change anything.

## Goals

* A user can mint, list and revoke long-lived access tokens for their own account.
* An `Authorization: Basic` header carrying one authenticates as that user, with exactly the read
  access they already have.
* **A token cannot write anything, through any route, present or future.**
* Verification is cheap enough to sit in front of a route that is called once per package
  downloaded.

## Non-goals for this work

Recorded here so the issues stay bounded; each has a follow-up in
[Deferred work](#deferred-work).

* **Scoped tokens.** A token grants its owner's read access, whole. There is no per-repository or
  per-operation narrowing.
* **Tokens that can write.** Not a limitation to be lifted later by relaxing this design — a
  writing credential is a different thing, with a different threat model, and would need its own.
* **Basic auth as a general alternative to `Bearer`.** `Bearer` remains what the management API is
  driven with; `Basic` exists for clients that cannot speak OAuth.
* **An audit trail.** Revocation is a delete, and nothing records where a token was used from.
* **Rate limiting.** Nothing bounds how much one token can consume.

---

## Read-only is a property of the actor, not of the route

The requirement is that Basic auth can never write. The obvious implementation is to accept the
`Basic` scheme only on the [pacman routes](pacman-controller.md), which are all reads. That is true
today and stops being true the first time somebody adds a write route and reaches for the scheme
list without thinking about it.

So the restriction goes where this codebase already puts its authorization invariants — into the
`Actor`, which is [the single thing every service authorizes against](authorization-plan.md#the-pieces):

```csharp
public bool IsReadOnly { get; }

public static Actor ReadOnlyFor(User user) => new(user, isSystem: false, isReadOnly: true);
```

`RepositoryAccessPolicy.CheckWrite`, `CheckCreate` and `PackageAccessPolicy.CheckPublish` each
return `RepositoryAccess.Forbidden` for a read-only actor, checked **first**, before the ownership
tests. A token therefore cannot write through any route, present or future, including one that
forgets to think about schemes at all — which is the same structural argument that put enforcement
in the service layer instead of an action filter in the first place.

`VisibleTo` is deliberately **not** affected. A read-only actor sees exactly what that user sees,
their own private repositories included. Read-only restricts what may be done, not what may be
known.

`Forbidden` (`403`) rather than `Unauthenticated` (`401`) is the right verdict, including on a
private repository the actor owns: the caller is authenticated, re-authenticating will not help, and
they already know the repository exists. Nothing leaks.

### Why not a claim, or a scheme check, or a filter

Three alternatives were considered.

A **claim inspected by each service** puts the rule in every service instead of in one place, which
is what `RepositoryAccessPolicy` exists to prevent. An **authorization filter** on the write routes
only protects HTTP callers, which is the reason
[`authorization-plan.md`](authorization-plan.md#architecture-strategy-enforcement-in-the-service-layer)
rejected a filter for the repository rules. Checking `IsReadOnly` in the **`IActorAccessor`** cannot
work at all: the accessor produces the actor, and has no idea what is about to be done with it.

The verdict methods are the one place that already knows an operation is a write. That is where the
check belongs, and putting it there means a new write path gets the behaviour by construction rather
than by remembering.

---

## Tokens

A new entity, `PacmanAccessToken`, following the `Pacman` prefix convention that distinguishes
storage types from wire models.

| Column | Type | Notes |
| :--- | :--- | :--- |
| `Id` | `Guid` | Primary key, `Guid.CreateVersion7()`. Also the token's **lookup key**; see below. |
| `UserId` | `Guid` | FK to `User`, required, cascade delete. |
| `User` | nav | |
| `Name` | `string` | A label the user chose, so a list of tokens is legible. Unique per user. |
| `TokenHash` | `string` | Base64 SHA-256 of the secret. |
| `CreatedAt` | `DateTimeOffset` | |
| `ExpiresAt` | `DateTimeOffset?` | Optional. A token past it fails to authenticate. |
| `LastUsedAt` | `DateTimeOffset?` | Coarsely maintained; see [Recording use](#recording-use). |

Unique index on `(UserId, Name)`. Validation limits go in
`PacmanManager.Entities.AccessTokenValidationConstants`, alongside the existing per-entity constants.

Cascade delete from `User` is what makes account deletion tractable later; it is not reachable today,
since nothing deletes a user.

### Format

```
pmt_{tokenId:N}_{base64url(32 random bytes)}
```

`pmt_` identifies the string as one of ours, which is what lets a secret scanner recognise a leaked
token and what stops a support conversation being about an unlabelled blob. `tokenId:N` is the row's
primary key in hex. The secret is 32 bytes from `RandomNumberGenerator`, Base64Url encoded to 43
characters. The whole thing is 80 characters, comfortably inside any header limit.

**Splitting the identifier from the secret is the point of the format.** Verification is a primary
key lookup followed by exactly one hash comparison. Without it, verification would have to hash the
presented secret against every stored row, which is `O(tokens)` work on every single HTTP request —
and `pacman -Su` on a large repository issues one request per package.

Base64**url**, not Base64: the token is written into a `Server` URL's userinfo, where `+` and `/`
are not safe. Encoding it correctly at the point it is minted is cheaper than every consumer
remembering to escape it.

The token id is a lookup key and is not secret. Knowing one without the matching secret is worth
nothing, so the fact that ids are enumerable and that a v7 GUID discloses its creation time costs
nothing here.

### Hashing

`SHA-256` over the secret bytes, unsalted, compared with `CryptographicOperations.FixedTimeEquals`.

**Not bcrypt, Argon2 or PBKDF2, and this is a deliberate departure from how a password would be
stored.** Those are slow *on purpose*, to make guessing a low-entropy human-chosen secret expensive.
This secret is 256 bits from a CSPRNG: there is no dictionary, no reuse across sites and nothing to
guess, so the work factor buys nothing — while costing tens of milliseconds on every request. A
`pacman -Syu` pulling 300 packages would spend seconds of server CPU on key derivation alone, which
turns a security control into a self-inflicted denial of service. This is the same reasoning behind
GitHub's personal access tokens, which are also a fast hash over a high-entropy random value.

Unsalted for the same reason: a salt defeats precomputation across a stolen table, and there is
nothing to precompute against a uniformly random 256-bit value. `FixedTimeEquals` is still required —
the comparison is against a value an attacker supplies.

**This reasoning is load-bearing and must not be generalised.** It holds only because the secret is
generated by this application from a CSPRNG. If a user-chosen value ever becomes acceptable here,
the fast hash becomes wrong on the same day. The XML docs on the minting and verification methods
say so.

### The token is shown once

`POST /api/v1/users/me/tokens` returns the full string in its `201` response body and it is never
retrievable again, because only the hash is kept. Every other route that mentions a token returns
its id, name and dates only.

### Revocation is a delete

`DELETE /api/v1/users/me/tokens/{tokenId}` removes the row. The absence of the row is then the only
state there is, which is one fewer thing for the verification path to check and no possibility of a
"revoked" token that some code path forgets to filter out. An audit trail of revocations would be
worth having and is [deferred](#deferred-work); it is not worth complicating the hot path for now.

A token that belongs to another user is a **`404`**, not a `403`. The route is scoped to `me`, so a
token that is not the caller's is simply not addressable, and saying "forbidden" would confirm that
the id names a real token belonging to somebody.

### Recording use

`LastUsedAt` exists so a user can tell which of their tokens is still in service. Writing it on every
request would mean **one `UPDATE` per package downloaded**, which is a write amplification the
feature does not justify.

It is therefore written only when the stored value is more than a configurable resolution (default
one hour) old. That reduces it to at most one write per token per hour while keeping the value
accurate enough for the question it answers — "is this token still in use", not "when exactly was
its last request". The endpoint documents the resolution, so a user is not misled by a timestamp
that is deliberately coarse.

The write is best-effort: a failure to record use is logged and never fails a request that was
otherwise authorized. Two concurrent requests may both decide the value is stale and both write it,
which is harmless — they write near-identical values, and the column has no reader that cares.

---

## The Basic handler

An `AuthenticationHandler<BasicAuthenticationSchemeOptions>` registered as a second scheme. `Bearer`
stays the **default** scheme, so nothing about the existing management API changes; `Basic` is opted
into by the routes that accept it.

The handler decodes `Authorization: Basic base64(username:token)`, parses the token, loads the row by
id, checks expiry, compares the hash, and issues a principal carrying the existing
`AuthnConstants.AppUserIdClaimType` claim plus a marker claim identifying the credential as
read-only.

Reusing `app_user_id` is what makes the rest of the pipeline unchanged: `CurrentUserService` already
resolves that claim to a `User`, and `HttpContextActorAccessor` already turns a `User` into an
`Actor`. The only change there is that the accessor produces `Actor.ReadOnlyFor(user)` when the
marker claim is present.

`ClaimsTransformer` must be a no-op for this scheme. It short-circuits when `app_user_id` is already
present, which it is, so no change is needed — but there is a test to write that pins it, because a
Basic-authenticated request must never provision a user or an `ExternalProviderUserMapping`.

**Every failure mode returns the same `401`**, with no detail distinguishing an unparseable header
from an unknown token id from a wrong secret from an expired token. The failures are logged with the
distinction; the response does not carry it.

### The token goes in the password field, and the username is ignored

The credential is `Authorization: Basic base64(anything:pmt_…)`. **The username field is not
validated.** A token identifies its owner on its own, so there is nothing for the username to add.

The documented form uses the literal `token`, which is self-describing in a config file:

```ini
Server = https://token:pmt_0199…_kJ8…@packages.example.com/repositories/$repo/$arch
```

An earlier draft of this design *required* the username to equal the owner's, which was worth doing
then and is not now. The point of the check was to fail loudly after a rename: when a repository URL
contained its owner's name, a user who renamed themselves had a stale `pacman.conf` carrying the old
name in two places, and matching the credential to the owner turned a half-working configuration
into one clean `401`. [Repositories are no longer addressed by their
owner](pacman-controller.md#repository-names-are-globally-unique) and there is no username to
rename, so the check would now protect against nothing while adding a way for a correct token to be
rejected.

Basic auth requires *some* userinfo to be present — libcurl sends a header for
`https://user@host` and for `https://:pass@host` alike — so the field cannot simply be omitted from
the URL. Naming it `token` is a convention, not a constraint: any value authenticates, and the
handler must not grow an opinion about it later without a reason better than tidiness.

### Present-but-invalid credentials are a `401`, and this is easy to get wrong

The routes this scheme serves are `[AllowAnonymous]`, because a public repository has to serve an
unauthenticated client. **ASP.NET Core's default behaviour on an `[AllowAnonymous]` endpoint is to
ignore a failed authentication result and continue as anonymous.** A user who mistypes their token
would then get a `404` on their private repository — indistinguishable from the repository not
existing, and the single most confusing failure this feature could produce.

The requirement is therefore explicit: **if an `Authorization: Basic` header is present and does not
validate, the response is `401` with `WWW-Authenticate: Basic realm="pacman"`, whatever the endpoint
allows.** Only the *absence* of credentials falls through to anonymous. The implementing issue owns
the mechanism — the cleanest is for the handler to write the challenge itself rather than relying on
`HandleChallengeAsync` being reached — and owns the test that pins it, because nothing else in the
pipeline will catch a regression here.

Note the asymmetry with the visibility rules, which is correct rather than an inconsistency. Bad
credentials are a fact about the caller and disclose nothing about what exists, so they are answered
honestly. A well-formed request from a correctly authenticated caller for a repository they may not
see is still a `404`.

### Transport

Basic auth sends a reusable credential in a header on every request, so **a deployment serving
private repositories must terminate TLS.** The credential also ends up written in cleartext in the
user's `pacman.conf`. That is the cost of the only mechanism `pacman` supports, and it is the reason
tokens are per-user, individually revocable and optionally expiring rather than the account
password: the credential on the laptop is one that can be thrown away without touching the account.

The `Authorization` header must never be logged. `UseSerilogRequestLogging` does not log headers by
default, so this is a constraint on future changes rather than a change to make now; the issue adds a
test asserting that a token does not appear in the request log.

---

## Routes

`/api/v1/users/me/tokens`. The path hangs off the user resource, but the actions belong to an
`AccessTokensController` of its own, addressed by an absolute route template in
`ControllerConstants` — exactly the arrangement `RepositoryScopedPackagesRoute` already uses for a
repository's packages, and for the same reason.

| Method | Path | Auth | Success | Failure |
| :--- | :--- | :--- | :--- | :--- |
| `GET` | `/api/v1/users/me/tokens` | Required | `200` page of tokens | `401` |
| `POST` | `/api/v1/users/me/tokens` | Required | `201` token, **secret included once** | `400`, `401`, `409` |
| `DELETE` | `/api/v1/users/me/tokens/{tokenId}` | Required | `204` | `401`, `404` |

These routes require **`Bearer`**. A token cannot be used to mint another token, which follows from
[read-only actors](#read-only-is-a-property-of-the-actor-not-of-the-route) without needing a rule of
its own: minting is a write.

`me` is the only subject, for the reason
[User Management](user-management.md#me-is-the-only-way-to-reach-the-write-route) gives — a route
incapable of naming another user has no authorization check to forget. Note that this document does
not otherwise depend on that one; the reasoning is shared, the code is not.

`409` on `POST` is a duplicate token name for that user, raised as `ItemExistsException` and mapped
by a new arm on `AuthorizationExceptionHandler`, without which it would fall through to a `500`
exactly as `PackageForbiddenException` would have. That exception has existed unused since the
original authorization work — [`authorization-plan.md`](authorization-plan.md#known-gaps) records it
as a known gap — and this is its first use. [Pacman Controller](pacman-controller.md#1-globally-unique-repository-names--major)
needs the same arm for a repository name collision; whichever of the two issues lands first adds it,
and the second finds it already there.

### Models

```jsonc
// AccessToken — what the listing returns. No secret.
{ "id": "0199…", "name": "laptop", "createdAt": "…", "expiresAt": null, "lastUsedAt": "…" }
```

```jsonc
// CreatedAccessToken — the 201 from POST, and the only place the secret ever appears
{ "id": "0199…", "name": "laptop", "createdAt": "…", "expiresAt": null, "lastUsedAt": null,
  "token": "pmt_0199…_kJ8…" }
```

Two models rather than one with a nullable `token`, for the reason the user models split on `email`:
a field that is only sometimes populated is a field that will one day be populated by accident. Here
that accident would put a live credential in a listing.

---

## Implementation plan

One issue per heading. Dependencies are noted; anything without a dependency can start immediately.

### 1. `PacmanAccessToken` entity and migration — `MINOR`

The entity as tabulated, `AccessTokenValidationConstants`, `PacmanAccessTokens` on
`PacmanManagerDbContext`, and a migration named `AddTable_PacmanAccessTokens`.

*Acceptance:* `dotnet ef migrations list` shows it; applying it against the compose Postgres
succeeds; the unique index on `(UserId, Name)` exists and the FK cascades from `User`.

*Depends on:* nothing.

### 2. `IAccessTokenService` — `MINOR`

Minting (format, `RandomNumberGenerator`, SHA-256, the once-only return), parsing, verification
(primary key lookup, expiry, `FixedTimeEquals`), listing, deletion, and the coarse `LastUsedAt`
update with its configurable resolution.

*Acceptance:* unit tests that a minted token verifies; that a token differing in one character does
not; that a malformed, truncated or wrongly-prefixed string is rejected without throwing; that an
unknown token id is rejected; that an expired token is rejected; that the stored hash is not the
token and the token is not recoverable from the row; that a minted secret is Base64Url and therefore
URL-safe; that `LastUsedAt` is written when stale and skipped when fresh; and that a failure to write
it does not fail verification.

*Depends on:* 1.

### 3. Read-only actors — `MINOR`

`Actor.IsReadOnly` and `Actor.ReadOnlyFor`; the read-only arm in `RepositoryAccessPolicy.CheckWrite`,
`CheckCreate` and `PackageAccessPolicy.CheckPublish`.

Separate from issue 4 because it is a pure policy change with no HTTP in it, testable entirely by
`RepositoryAccessPolicyTests` and `PackageAccessPolicyTests`, and reviewable on its own — which is
the property that makes it worth reviewing carefully, since it is the guarantee the whole document
rests on.

*Acceptance:* both policy test fixtures gain a read-only row for every write verdict, including a
read-only owner of a private repository getting `Forbidden` rather than `NotFound`, and assert that
`VisibleTo` is unchanged for a read-only actor — the case where getting it wrong would silently hide
a user's own private repositories from their own client.

*Depends on:* nothing.

### 4. The `Basic` scheme and its handler — `MINOR`

The scheme, its options, the handler, the marker claim, `HttpContextActorAccessor` producing a
read-only actor from it, and the present-but-invalid-is-a-`401` behaviour.

*Acceptance:* handler unit tests cover a valid header, a missing header, a malformed one, a
non-Basic scheme, a wrong secret, an unknown token id and an expired token, and assert every failure
produces the same undifferentiated `401`. A test asserts the username field is ignored: the same
token authenticates with `token:`, with the owner's display name, and with an arbitrary string.

An E2E test asserts that a Basic-authenticated `POST` to `/api/v1/repositories` is a `403` — the test
that proves the restriction is a property of the credential rather than of the route, and the one
that would catch someone later removing the read-only arm. A further test asserts a token does not
appear in the request log, and one asserts a Basic-authenticated request creates no
`ExternalProviderUserMapping`.

The `[AllowAnonymous]`-plus-`401` behaviour needs an anonymous endpoint to test against, and the
pacman routes do not exist yet. Add a test-only anonymous endpoint, or defer that single assertion to
[Pacman Controller](pacman-controller.md#4-pacmancontroller--minor) — the implementing issue picks,
but it must not go untested in both.

*Depends on:* 2, 3.

### 5. `AccessTokensController` — `MINOR`

The three routes, the two wire models, the `ControllerConstants` template, and the
`ItemExistsException` → `409` arm if it is not already there.

*Acceptance:* E2E tests: creating a token returns the secret exactly once, and listing tokens never
returns it, asserted against the raw response body; the returned token then authenticates a request;
deleting it makes it stop authenticating; a second token with the same name is a `409`; another
user's token is a `404` to delete; every route is a `401` unauthenticated; and a Basic-authenticated
caller cannot mint a token. A handler test asserts `ItemExistsException` produces `409`.

*Depends on:* 2, 4.

---

## Deferred work

Worth filing as issues, but explicitly out of scope for the work above.

* **An audit trail for tokens** — revocations, and the address a token was last used from — which
  would change revocation from a delete to a soft delete.
* **Scoped tokens**, narrowing a credential to particular repositories, so that a build machine's
  token is not equivalent to its owner's whole read access.
* **A credential that can publish.** Today a build pipeline still needs an OAuth client to push a
  package. That is the right default, but a purpose-built publishing credential — scoped to one
  repository, and distinct from these — is the obvious next thing to want.
* **Token expiry notifications.** An `ExpiresAt` that nothing warns about produces a build that
  breaks on a date nobody remembers.
* **Rate limiting per token**, which is where a compromised token stops being unbounded.
* **A maximum token count per user**, absent which nothing bounds the table.
