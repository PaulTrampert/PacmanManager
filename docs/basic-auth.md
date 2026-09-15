# Basic Auth

Status: **design**. This document is the source the implementation issues are cut from. It follows
the conventions established in [`authorization-plan.md`](authorization-plan.md) and
[`packages-api.md`](packages-api.md); where it departs from them, it says so.

**How to read it.** The plan states what will be built. The argument for each decision — including
what was considered and rejected — is in the [Appendix](#appendix), and each section ends with a
**Why** footer linking the entries that bear on it. A decision with an appendix entry is settled:
implement it as written and raise an issue rather than re-deciding it.

It is one of three documents that together let a `pacman` client install from a hosted repository:

1. [**User Management**](user-management.md) — reading users, and changing your own display name.
2. **Basic Auth** (this document) — access tokens, and the HTTP Basic scheme.
3. [**Pacman Controller**](pacman-controller.md) — the repository routes themselves.

This document depends on neither of the others and can land at any time. [Pacman
Controller](pacman-controller.md) depends on it, for the credentials a private repository needs.

`pacman` has one credential mechanism: a URL with userinfo in it, which libcurl turns into an
`Authorization: Basic` header. A private repository is therefore reachable by `pacman` only if this
application accepts Basic credentials — and what is handed to a package manager on a laptop must be
neither the account password nor able to change anything. This document specifies both that
credential and the general mechanism it turns out to be a special case of: OAuth 2.0 scopes, and how
a `Bearer` token carries them.

**Why:**

* [this document also specifies scopes](#why-this-document-also-specifies-scopes)

## Goals

* A user can mint, list and revoke long-lived access tokens for their own account.
* An `Authorization: Basic` header carrying one authenticates as that user, with exactly the read
  access they already have.
* **A token cannot write anything, through any route, present or future.**
* Verification is cheap enough to sit in front of a route that is called once per package
  downloaded.
* What a credential may be used for is carried by the credential and decided in one place. The
  scheme it arrived under is an implementation detail of the handler, not something a service or a
  route ever asks about.

## Non-goals for this work

Recorded here so the issues stay bounded; each has a follow-up in
[Deferred work](#deferred-work).

* **Minting a narrowed access token.** Every `PacmanAccessToken` is issued the same
  [scope](#the-scope-claim), `pacman-manager:*:read`: its owner's read access, whole. The mechanism
  that would let a user mint a narrower one is [deferred](#deferred-work) — the enforcement is not,
  because it is the same mechanism a `Bearer` token's scopes go through.
* **Narrowing a credential to one repository.** Scopes name an entity *kind* and an action, never an
  instance.
* **Tokens that can write.** Not a limitation to be lifted later by relaxing this design — a
  writing credential is a different thing, with a different threat model, and would need its own.
* **Basic auth as a general alternative to `Bearer`.** `Bearer` remains what the management API is
  driven with; `Basic` exists for clients that cannot speak OAuth.
* **An audit trail.** Revocation is a delete, and nothing records where a token was used from.
* **Rate limiting.** Nothing bounds how much one token can consume.

**Why:**

* [instances are not in here](#why-instances-are-not-in-here)

---

## Scopes and the actor

Authority is carried by the credential and enforced on the `Actor`. No route names a scheme, and no
service asks which scheme a request arrived under.

### The `scope` claim

Both schemes produce a principal carrying the same two things: the existing
`AuthnConstants.AppUserIdClaimType` claim, which says **who**, and the standard OAuth 2.0 **`scope`**
claim, which says **what this credential may be used for**.

Per [RFC 6749 §3.3](https://www.rfc-editor.org/rfc/rfc6749#section-3.3) `scope` is one
space-delimited, case-sensitive list of opaque strings. Every value of ours has this shape:

```
<audience>:<entity>:<action>
```

| Part | Values |
| :--- | :--- |
| `pacman-manager:` | The API's audience, as a literal prefix on every value. |
| `entity` | `repositories`, `packages`, `users`, `tokens`, or `*` — the plural, matching the route segment that serves it |
| `action` | `read`, `create`, `write`, `delete`, `publish`, or `*` |

`pacman-manager:repositories:read` reads repositories. `pacman-manager:*:read` is "read anything
this user can read". `pacman-manager:packages:publish` is "publish packages and nothing else".
`pacman-manager:repositories:*` is every operation on repositories. A credential carries as many
values as it needs and they are ORed: permitted if any one of them matches.

Three rules govern the claim, and all three are requirements rather than observations:

* **Scopes are purely additive: absent means denied.** A credential may do what its scopes name and
  nothing else, so a token carrying none of our values can do nothing at all. A bug in the parser
  therefore fails closed. This has a consequence that must be planned for rather than discovered;
  see [Every existing token becomes powerless](#every-existing-token-becomes-powerless).
* **A scope never grants, it only narrows.** What it names is a *ceiling*, not a permission: every
  rule in [`authorization-plan.md`](authorization-plan.md#authorization-rules) still runs afterwards,
  unchanged, so `pacman-manager:repositories:write` on a repository somebody else owns is still a
  refusal. An identity provider can hand out any scope it likes; it cannot hand out access.
* **A value that is not ours is ignored**, never fatal and never permissive. `scope` legitimately
  carries `openid`, `profile`, `email`, `roles` and the bare `pacman-manager` audience value, and
  none of those grant anything here. A malformed value of ours is dropped the same way and logged,
  so a provider that mangles one cannot escalate through it — it can only take a permission away.

**Why:**

* [the audience prefix is on every value](#why-the-audience-prefix-is-on-every-value)
* [scopes are additive, and what that costs](#why-scopes-are-additive-and-what-that-costs)
* [every existing token becomes powerless](#every-existing-token-becomes-powerless)
* [instances are not in here](#why-instances-are-not-in-here)

### `Basic` produces a read-only scope

A `PacmanAccessToken` authenticates its owner and is issued exactly one scope value,
`pacman-manager:*:read`. That is the whole mechanism by which "a token cannot write" is true: not a
check on the scheme, not a check on the route, but a credential that arrives already unable to name
a write.

The handler mints this itself rather than reading it from anywhere. Nothing in the Basic path talks
to the identity provider.

### Scopes on a `Bearer` token

The same values, issued by the identity provider rather than by us, let a caller hold a token that
can do less than they can. Nothing in this application mints that token and nothing validates the
scopes beyond parsing them.

In Keycloak each value is a client scope. The broad ones ride on the existing `pacman-manager`
client scope, which is **default** on the interactive clients so they keep working; a narrow
credential is a client configured with a narrower set instead. This half needs no application code
beyond the parser.

### How the actor enforces it

`Actor` carries the parsed scope, and read-only becomes a question asked of it rather than a
separate flag:

```csharp
public ActorScope Scope { get; }

public bool IsReadOnly => !Scope.PermitsAnyWrite;

public static Actor For(User user, ActorScope scope) => new(user, isSystem: false, scope);
```

* **Every verdict method asks the scope first**, before the ownership tests, and returns
  `RepositoryAccess.Forbidden` when it does not permit that operation on that entity — `CheckRead`
  as much as `CheckWrite`, `CheckCreate` and `PackageAccessPolicy.CheckPublish`. Reads are included
  because absence now denies.
* **An actor with no credential behind it is unrestricted.** `Actor.System` and `FixedActorAccessor`
  take `ActorScope.Unrestricted` explicitly, so background jobs and command-line tools are
  unaffected.
* **A credential never gets less than an anonymous caller would.** An `[AllowAnonymous]` route that
  consults no actor — the public repository listing, the pacman routes for a public repository —
  serves a narrowly scoped credential exactly as it serves a stranger. Anything else would mean a
  user could see more by deleting their `Authorization` header, which is absurd on its face and
  would teach people to do it.
* `scope` arrives as **one space-delimited string**, not as repeated claims, so it is split before it
  is parsed.
* `Program.cs` already calls `JsonWebTokenHandler.DefaultInboundClaimTypeMap.Clear()`, so the claim
  reaches the principal as `scope` rather than remapped to a WS-Federation-era URI. It must stay
  cleared.
* The verdict is `Forbidden` (`403`) rather than `Unauthenticated` (`401`), including on a private
  repository the actor owns: the caller is authenticated, re-authenticating will not help, and they
  already know the repository exists.

**Why:**

* [authority is a property of the actor, not of the route](#authority-is-a-property-of-the-actor-not-of-the-route)
* [not a claim read by each service, or a scheme check, or a filter](#why-not-a-claim-read-by-each-service-or-a-scheme-check-or-a-filter)

### `VisibleTo` does not change at all

`VisibleTo` and `RepositoryService.VisibleAsync` are **untouched**. A scope can refuse an operation;
it can never narrow the set of rows a caller is shown. A credential that may read repositories at
all sees exactly what its user sees, their own private repositories included.

**Why:**

* [a scope restricts what may be done, not what may be known](#why-a-scope-restricts-what-may-be-done-not-what-may-be-known)

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

A token is **two strings**, one for each half of a Basic credential:

```
username: pmt_{tokenId:N}
password: pms_{base64url(32 random bytes)}
```

* `pmt_{tokenId:N}` is the token's **identifier**: the row's primary key in hex, 36 characters. It is
  not secret.
* `pms_{…}` is the **secret**: 32 bytes from `RandomNumberGenerator`, Base64Url encoded to 43
  characters, 47 with the prefix.
* Base64**url**, not Base64: the secret is written into a `Server` URL's userinfo, where `+` and `/`
  are not safe. It is encoded correctly at the point it is minted.
* The identifier is hex and needs no such care.

**Why:**

* [the identifier and the secret are separate fields](#why-the-identifier-and-the-secret-are-separate-fields)

### Hashing

`SHA-256` over the 32 decoded secret bytes, **unsalted**, compared in the database; see
[Verification is one query](#verification-is-one-query).

**This is load-bearing and must not be generalised.** The fast hash is correct only because the
secret is 256 bits generated by this application from a CSPRNG. If a user-chosen value ever becomes
acceptable here, the fast hash becomes wrong on the same day. The XML docs on the minting and
verification methods must say so.

**Why:**

* [the hash is fast and unsalted](#why-the-hash-is-fast-and-unsalted)

### Verification is one query

The service parses both fields, hashes the presented secret, and asks for the row matching **both**
at once:

```csharp
var token = await dbContext.PacmanAccessTokens
    .Where(t => t.Id == tokenId && t.TokenHash == presentedHash)
    .SingleOrDefaultAsync(cancellationToken);
```

* An unknown identifier and a wrong secret are the same outcome — no row — reached by the same path.
  The hash is computed before the query in both cases, and nothing branches on which half was wrong.
  **Fetching by id and comparing in memory is not an acceptable implementation.**
* The primary key already serves this query. No index on `(Id, TokenHash)` is needed.
* **There is no `FixedTimeEquals`**, and the comparison stays in the database. The XML docs on the
  query must say why, so that nobody later "fixes" it by moving the comparison into memory — or,
  worse, by storing the secret.
* **Expiry is checked on the returned row, not in the predicate.** The response is the same `401`
  either way; the distinction is in the log.

**Why:**

* [verification is a single query](#why-verification-is-a-single-query)
* [there is no constant-time comparison](#why-there-is-no-constant-time-comparison)

### The token is shown once

`POST /api/v1/users/me/tokens` returns the secret in its `201` response body and it is never
retrievable again, because only the hash is kept. Every other route that mentions a token returns
its id, username, name and dates only.

### Revocation is a delete

`DELETE /api/v1/users/me/tokens/{tokenId}` removes the row. A token that belongs to another user is
a **`404`**, not a `403`.

**Why:**

* [revocation is a delete rather than a flag](#why-revocation-is-a-delete-rather-than-a-flag)

### Recording use

`LastUsedAt` is written only when the stored value is older than a configurable resolution, default
one hour. That bounds it to at most one write per token per hour.

* The write is **best-effort**: a failure to record use is logged and never fails a request that was
  otherwise authorized.
* Two concurrent requests may both decide the value is stale and both write it. This is harmless:
  they write near-identical values, and the column has no reader that cares.
* The endpoint documents the resolution, so a user is not misled by a timestamp that is deliberately
  coarse.

**Why:**

* [`LastUsedAt` is coarse](#why-lastusedat-is-coarse)

### Every use is logged

Every successful verification logs, at minimum:

* **The token id** — parsed from the username, and [not secret](#format).
* **The client IP address** the request arrived from.

Failures are logged the same way, with the token id when the username parsed as one, and with the
reason: a malformed credential, no match, or an expired token. **An unknown id and a wrong secret are
both logged as "no match"**, because [the query](#verification-is-one-query) cannot tell them apart.

**The secret is never logged, in any form** — not the password field, not the header, not a prefix of
either.

One deployment detail the implementing issue must handle rather than assume: `Program.cs` does not
configure forwarded headers, so behind the reverse proxy that [terminates TLS](#transport)
`RemoteIpAddress` is the proxy's address. Either `UseForwardedHeaders` is configured or the header is
read explicitly and logged as what it is. If it is `UseForwardedHeaders`:

* **The defaults trust loopback only**, so a proxy on a compose bridge network is ignored and nothing
  appears to happen.
* The list that accepts a subnet is **`KnownIPNetworks`** (`IList<System.Net.IPNetwork>`, so
  `IPNetwork.Parse("172.16.0.0/12")`), which is what a container network wants since the proxy's
  address is not stable. `KnownProxies` takes individual addresses, and the older `KnownNetworks` is
  `[Obsolete]` on net10.0 along with `Microsoft.AspNetCore.HttpOverrides.IPNetwork`.
* It must name something: clearing the lists trusts whatever any caller cares to send.

**Why:**

* [every use is logged before there is an audit trail](#why-every-use-is-logged-before-there-is-an-audit-trail)

### Two services own this, not one

**`IBasicAuthenticationService` is not actor-scoped** and has one job: turn a set of Basic
credentials into a `ClaimsPrincipal`, or into nothing. Parsing both fields,
[the lookup on id and hash](#verification-is-one-query), the expiry check, the claims it issues, the
coarse `LastUsedAt` write and [the use log](#every-use-is-logged) all live here. **It cannot take
`IActorAccessor`**: the accessor depends on `ICurrentUserService`, which depends on the authenticated
principal, which is the thing this service produces.

**`IAccessTokenService` is actor-scoped** like every other service: minting (format,
`RandomNumberGenerator`, SHA-256, the once-only return), the
[listing](#the-listing-follows-the-standard-shape), and deletion. Every method takes the actor's own
tokens as its subject, so a caller can never name another user's.

`IUserService` is left exactly as it is.

**Which service owns the `DbSet`.** `PacmanAccessTokens` is touched by both, and the enforcement test
names both and asserts that nothing else does. This is a deliberate departure from the one-method
rule [`authorization-plan.md`](authorization-plan.md#why-this-is-hard-to-get-wrong) holds for
`PacmanRepositories`.

**Why:**

* [two services, not one](#why-two-services-not-one)
* [token management is not on `IUserService`](#why-token-management-is-not-on-iuserservice)

---

## Scheme selection, and the handler

### The `Authorization` prefix picks the handler

The default authentication scheme is a **policy scheme that forwards on the header prefix**:

```csharp
builder.Services.AddAuthentication(AuthnConstants.SelectorScheme)
    .AddPolicyScheme(AuthnConstants.SelectorScheme, AuthnConstants.SelectorScheme, options =>
        options.ForwardDefaultSelector = context =>
            context.Request.Headers.Authorization.ToString()
                .StartsWith("Basic ", StringComparison.OrdinalIgnoreCase)
                ? AuthnConstants.BasicScheme
                : JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer()
    .AddScheme<BasicAuthenticationSchemeOptions, BasicAuthenticationHandler>(
        AuthnConstants.BasicScheme, _ => { });
```

The prefix is the whole selection rule: `Basic` goes to the Basic handler, and everything else —
`Bearer`, an unrecognised scheme, an absent header — goes to `Bearer` exactly as it does today.
Nothing about the existing management API changes, and **no route names a scheme.**

**Why:**

* [a policy scheme selects the handler](#why-a-policy-scheme-selects-the-handler)

### The handler

An `AuthenticationHandler<BasicAuthenticationSchemeOptions>` whose whole body delegates to
[`IBasicAuthenticationService`](#2a-ibasicauthenticationservice--minor): it decodes
`Authorization: Basic base64(username:password)`, and the service parses the identifier from the
username and the secret from the password, looks the row up by both, and checks expiry.

* The principal carries the existing `AuthnConstants.AppUserIdClaimType` claim plus the
  [`pacman-manager:*:read` scope](#basic-produces-a-read-only-scope). Reusing `app_user_id` is what
  leaves the rest of the pipeline unchanged.
* `HttpContextActorAccessor` reads the `scope` claim off the principal — without caring which scheme
  produced it — and builds the actor with the resulting [scope](#the-scope-claim).
* `ClaimsTransformer` **must be a no-op for this scheme**. It already short-circuits when
  `app_user_id` is present, so no change is needed, but a test pins it: a Basic-authenticated request
  must never provision a user or an `ExternalProviderUserMapping`.
* **Every failure mode returns the same `401`**, with no detail distinguishing an unparseable header
  from an unknown token id from a wrong secret from an expired token. The failures are logged with
  the distinction; the response does not carry it.

### The identifier goes in the username, the secret in the password

The credential is `Authorization: Basic base64(pmt_…:pms_…)`, and **both fields are required and
validated**. A username that is missing, lacks the `pmt_` prefix or is not a hex GUID after it, or a
password that is missing, lacks the `pms_` prefix or does not decode to exactly 32 bytes, fails
before any database access — with the same `401` as every other failure.

```ini
Server = https://pmt_0199…:pms_kJ8…@packages.example.com/pacman/$repo/$arch
```

The username is not an account name, and nothing compares it to one. It names the token, and the
token names its owner. [There is no username on `User`](user-management.md#why-there-is-no-username)
for it to be confused with.

**Why:**

* [the username carries the identifier rather than a placeholder](#why-the-username-carries-the-identifier-rather-than-a-placeholder)

### Present-but-invalid credentials are a `401`, and this is easy to get wrong

**If an `Authorization: Basic` header is present and does not validate, the response is `401` with
`WWW-Authenticate: Basic realm="pacman"`, whatever the endpoint allows.** Only the *absence* of
credentials falls through to anonymous.

[The forwarding scheme](#the-authorization-prefix-picks-the-handler) gets the handler invoked, which
is half of it. The other half is a short piece of middleware immediately after `UseAuthentication()`:

```csharp
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated != true &&
        context.Request.Headers.Authorization.ToString()
            .StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate = "Basic realm=\"pacman\"";
        return;
    }

    await next(context);
});
```

It asks only whether a Basic header was offered and not accepted, so it cannot develop an opinion
about which routes take the scheme, and it **sits before authorization** so an `[AllowAnonymous]`
endpoint cannot swallow it. The implementing issue owns the test that pins it.

A well-formed request from a correctly authenticated caller for a repository they may not see is
still a `404`. Only bad credentials are answered honestly.

**Why:**

* [a present-but-invalid credential must not fall through](#why-a-present-but-invalid-credential-must-not-fall-through)

### Transport

* **A deployment serving private repositories must terminate TLS.** Basic auth sends a reusable
  credential in a header on every request, and it is written in cleartext in the user's
  `pacman.conf`.
* **The `Authorization` header must never be logged.** `UseSerilogRequestLogging` does not log
  headers by default, so this is a constraint on future changes rather than a change to make now;
  the issue adds a test asserting that a token does not appear in the request log.

**Why:**

* [the credential is a token rather than the account password](#why-the-credential-is-a-token-rather-than-the-account-password)

---

## Routes

`/api/v1/users/me/tokens`. The path hangs off the user resource, but the actions belong to an
`AccessTokensController` of its own, addressed by an absolute route template in
`ControllerConstants` — exactly the arrangement `RepositoryScopedPackagesRoute` already uses for a
repository's packages.

| Method | Path | Auth | Success | Failure |
| :--- | :--- | :--- | :--- | :--- |
| `GET` | `/api/v1/users/me/tokens` | Required | `200` page of tokens | `401` |
| `POST` | `/api/v1/users/me/tokens` | Required | `201` token, **secret included once** | `400`, `401`, `409` |
| `DELETE` | `/api/v1/users/me/tokens/{tokenId}` | Required | `204` | `401`, `404` |

These routes require **`Bearer`**, which follows from the header prefix alone: a request carrying a
`Basic` credential is authenticated by that scheme, and the `pacman-manager:*:read` scope it arrives
with cannot name a write. A token therefore cannot be used to mint another token without any rule
saying so.

`me` is the only subject, for the reason
[User Management](user-management.md#me-is-the-only-way-to-reach-the-write-route) gives. This
document does not otherwise depend on that one; the reasoning is shared, the code is not.

### The listing follows the standard shape

`GET /api/v1/users/me/tokens` takes `PaginationParams`, an `AccessTokenFilter` and
`SortOptions<AccessTokenSortField>`, exactly as [every other listing](packages-api.md#listing) does.

| Parameter | Applied by | Meaning |
| :--- | :--- | :--- |
| `nameContains` | `StringContainsQuery` | Substring of the token's name, matched case-insensitively by lowering both sides as the package search does. |

* `AccessTokenSortField`: `CreatedAt`, `Name`, `ExpiresAt`, `LastUsedAt`. `CreatedAt` is first and
  therefore the default, carrying `[DefaultSortDirection(SortDirection.Descending)]`.
* There is no filter on expiry.
* The filter is ANDed onto a query already restricted to the calling user's own tokens, so it can
  only ever remove rows.
* `409` on `POST` is a duplicate token name for that user, raised as `ItemExistsException`. The arm
  on `AuthorizationExceptionHandler` that maps it to `409` is
  [a story of its own](authorization-plan.md#itemexistsexception--409--patch), because
  [Pacman Controller](pacman-controller.md#1a-raise-itemexistsexception-on-repository-name-collisions--patch)
  needs the same arm and neither document depends on the other.

**Why:**

* [the token listing is paged](#why-the-token-listing-is-paged)
* [there is no filter on expiry](#why-there-is-no-filter-on-expiry)

### Models

```jsonc
// CreateAccessTokenRequest — the POST body
{ "name": "laptop", "expiresAt": null }
```

* `name` is required and validated against `AccessTokenValidationConstants.NameMaxLength`; a name the
  user already has is the `409` above.
* `expiresAt` is optional and, when present, **must be in the future** — a past date is a `400`, not
  a token that is dead on arrival. There is no maximum.
* **An omitted or null `expiresAt` means the token never expires.** Stating the default explicitly is
  worth the line precisely because the safer-looking reading — omitted means "some sensible default
  lifetime" — is the wrong one.

```jsonc
// AccessToken — what the listing returns. No secret.
{ "id": "0199…", "username": "pmt_0199…", "name": "laptop", "createdAt": "…", "expiresAt": null,
  "lastUsedAt": "…" }
```

```jsonc
// CreatedAccessToken — the 201 from POST, and the only place the secret ever appears
{ "id": "0199…", "username": "pmt_0199…", "name": "laptop", "createdAt": "…", "expiresAt": null,
  "lastUsedAt": null, "secret": "pms_kJ8…" }
```

`username` is derived from `id` rather than stored, and is on both models so that a client never has
to know how the one is spelled from the other.

**Why:**

* [two token models rather than a nullable secret](#why-two-token-models-rather-than-a-nullable-secret)
* [there is no maximum lifetime](#why-there-is-no-maximum-lifetime)

---

## Implementation plan

One issue per heading. Dependencies are noted; anything without a dependency can start immediately.

**The numbering is not the order.** Issue 6 is the realm change, and
[it has to land before issue 3](#every-existing-token-becomes-powerless) turns enforcement on. It is
last in this list because it is the smallest and the least interesting, not because it is the last
thing to do.

### 1. `PacmanAccessToken` entity and migration — `MINOR`

The entity as tabulated, `AccessTokenValidationConstants`, `PacmanAccessTokens` on
`PacmanManagerDbContext`, and a migration named `AddTable_PacmanAccessTokens`.

*Acceptance:* `dotnet ef migrations list` shows it; applying it against the compose Postgres
succeeds; the unique index on `(UserId, Name)` exists and the FK cascades from `User`.

*Depends on:* nothing.

### 2a. `IBasicAuthenticationService` — `MINOR`

The pre-auth half, [as described above](#two-services-own-this-not-one): parsing the presented
username and password, [the lookup on id and hash](#verification-is-one-query), the expiry check,
the principal and its claims, the coarse `LastUsedAt` write, and [the use log](#every-use-is-logged).
Not actor-scoped, and nothing here takes `IActorAccessor`.

Verification needs a token to verify, so minting the format lives here too — as an internal detail
this service owns, which [issue 2b](#2b-iaccesstokenservice--minor) then exposes to a user.

*Constraints:* the fast unsalted hash under [Hashing](#hashing) is correct only for a CSPRNG-generated
secret, and the single-query lookup under [Verification is one query](#verification-is-one-query) is
a requirement rather than an optimisation. Both carry XML docs saying so.

*Acceptance:* unit tests that a minted token verifies; that a secret differing in one character does
not; that a malformed, truncated or wrongly-prefixed username or password is rejected without
throwing and without querying — including the two prefixes swapped between the fields, and a secret
containing `_`, which is in the Base64Url alphabet and must not confuse the parser; that an unknown
token id is rejected; that a real id with another token's secret is rejected; that an expired token
is rejected; that the stored hash is not the secret and the secret is not recoverable from the row;
and that a minted secret is Base64Url and therefore URL-safe.

The lookup is asserted to be a single query predicated on both id and hash, so that an unknown id
and a wrong secret take the same path; a test that moves the comparison into memory must fail.

`LastUsedAt` is written when stale and skipped when fresh, a failure to write it does not fail
verification, and its resolution is a configuration key with an entry in `appsettings.json` and a
default of one hour, tested at both a stale and a fresh value.

The log is asserted, not assumed: a successful verification writes the token id and the client
address, a failed one writes the token id where the username yielded one along with the reason —
malformed, no match, or expired — and **no test-visible log line contains the secret**, asserted
against a captured log rather than by reading the code.

*Depends on:* 1.

**Why:**

* [the hash is fast and unsalted](#why-the-hash-is-fast-and-unsalted)
* [verification is a single query](#why-verification-is-a-single-query)
* [there is no constant-time comparison](#why-there-is-no-constant-time-comparison)
* [`LastUsedAt` is coarse](#why-lastusedat-is-coarse)
* [every use is logged before there is an audit trail](#why-every-use-is-logged-before-there-is-an-audit-trail)

### 2b. `IAccessTokenService` — `MINOR`

The actor-scoped half: minting on behalf of the caller (over the format
[issue 2a](#2a-ibasicauthenticationservice--minor) owns), the
[listing](#the-listing-follows-the-standard-shape), and deletion. Every method's subject is the
actor's own tokens.

*Acceptance:* listing returns only the actor's own tokens, filtered and sorted as specified;
deleting another user's token is a `404`-shaped miss rather than a forbidden; minting returns the
secret exactly once and never again. An enforcement test asserts that this service and
`IBasicAuthenticationService` are the only things naming `DbContext.PacmanAccessTokens`.

*Depends on:* 2a.

**Why:**

* [two services, not one](#why-two-services-not-one)
* [token management is not on `IUserService`](#why-token-management-is-not-on-iuserservice)
* [revocation is a delete rather than a flag](#why-revocation-is-a-delete-rather-than-a-flag)

### 3. Scoped actors — `MINOR`

`ActorScope` and its parser (split the space-delimited `scope` claim, keep the values matching
`<audience>:<entity>:<action>`, `*` accepted for entity and action, everything else ignored, and an
**empty** scope when none of the values are ours); `Actor.Scope` with `IsReadOnly` derived from it;
`ActorScope.Unrestricted` passed explicitly by `Actor.System` and `FixedActorAccessor`; and the
scope arm in every verdict method — `CheckRead` as well as `CheckWrite`, `CheckCreate` and
`PackageAccessPolicy.CheckPublish` — checked before the ownership tests.

`VisibleTo` is deliberately **not** touched.

**This issue cannot land before [issue 6](#6-scopes-on-a-bearer-token--minor)**, which is what makes
the realm emit the scopes. Turning enforcement on against a realm that emits none makes every token
powerless.

Separate from issue 4 because it is a pure policy change with no HTTP in it, testable entirely by
`RepositoryAccessPolicyTests` and `PackageAccessPolicyTests`, and reviewable on its own — which is
the property that makes it worth reviewing carefully, since it is the guarantee the whole document
rests on.

*Acceptance:* parser unit tests for a wildcard entity, a wildcard action, both, an unknown entity, an
unknown action, a missing prefix, a wrong prefix, and a value with the right prefix but too few
segments. Then the four that matter most, because they are where a mistake is silent:

* A claim of `openid profile email roles pacman-manager` — none of it ours — parses to an **empty**
  scope, and an actor carrying it is refused every operation. This is the test that pins the
  direction of the default.
* A claim mixing ours with somebody else's (`openid pacman-manager:repositories:read`) keeps the one
  and ignores the rest.
* An actor built by `FixedActorAccessor` or `Actor.System` is unrestricted.
* `VisibleTo` is unchanged for every scope, asserted directly.

Both policy test fixtures gain rows for a `pacman-manager:*:read` scope on every verdict — including
a read-only owner of a private repository getting `Forbidden` rather than `NotFound` on a write, and
that same owner still *reading* it — plus a scope that permits an operation still losing to the
ownership rules underneath.

*Depends on:* 6.

**Why:**

* [scopes are additive, and what that costs](#why-scopes-are-additive-and-what-that-costs)
* [every existing token becomes powerless](#every-existing-token-becomes-powerless)
* [a scope restricts what may be done, not what may be known](#why-a-scope-restricts-what-may-be-done-not-what-may-be-known)
* [not a claim read by each service, or a scheme check, or a filter](#why-not-a-claim-read-by-each-service-or-a-scheme-check-or-a-filter)

### 4. The `Basic` scheme and its handler — `MINOR`

The [selector policy scheme](#the-authorization-prefix-picks-the-handler) and the constants naming
it, the `Basic` scheme and its options, the handler over `IBasicAuthenticationService`, the scope
claim it issues, `HttpContextActorAccessor` building an `Actor` from whatever scope claim the
principal carries, and the
[present-but-invalid-is-a-`401` middleware](#present-but-invalid-credentials-are-a-401-and-this-is-easy-to-get-wrong).

**Swagger is untouched:** `Basic` gets no security definition and the OAuth flow stays exactly as it
is. That is a decision, not a backlog item.

*Acceptance:* handler unit tests cover a valid header, a missing header, a malformed one, a
non-Basic scheme, a wrong secret, an unknown token id and an expired token, and assert every failure
produces the same undifferentiated `401`. Tests assert the username is required: the right secret
with an empty username, with the owner's display name, with an arbitrary string, or with another
token's identifier is the same `401`.

A test asserts the selector routes by prefix: a `Bearer` request is still handled by the JWT scheme
untouched, an absent header still falls through to anonymous, and an unrecognised scheme is treated
as `Bearer` rather than as `Basic`.

An E2E test asserts that a Basic-authenticated `POST` to `/api/v1/repositories` is a `403` — the test
that proves the restriction is a property of the credential rather than of the route. A further test
asserts a token does not appear in the request log, and one asserts a Basic-authenticated request
creates no `ExternalProviderUserMapping`.

The `[AllowAnonymous]`-plus-`401` behaviour needs an anonymous endpoint to test against, and the
pacman routes do not exist yet. Add a test-only anonymous endpoint, or defer that single assertion to
[Pacman Controller](pacman-controller.md#4-pacmancontroller--minor) — the implementing issue picks,
but it must not go untested in both.

*Depends on:* 2a, 3.

**Why:**

* [a policy scheme selects the handler](#why-a-policy-scheme-selects-the-handler)
* [a present-but-invalid credential must not fall through](#why-a-present-but-invalid-credential-must-not-fall-through)
* [`Basic` gets no Swagger security definition](#why-basic-gets-no-swagger-security-definition)
* [the username carries the identifier rather than a placeholder](#why-the-username-carries-the-identifier-rather-than-a-placeholder)

### 5. `AccessTokensController` — `MINOR`

The three routes, the three wire models
([`CreateAccessTokenRequest`, `AccessToken` and `CreatedAccessToken`](#models)), the
[`AccessTokenFilter` and `AccessTokenSortField`](#the-listing-follows-the-standard-shape), the
`ControllerConstants` template, and raising `ItemExistsException` on a duplicate token name.

*Acceptance:* E2E tests: creating a token returns the secret exactly once, and listing tokens never
returns it, asserted against the raw response body; the listing shows the same `username` the create
returned; the returned username and secret then authenticate a request; deleting it makes it stop
authenticating; a second token with the same name is a `409`; a past `expiresAt` is a `400` and an
omitted one produces a token with no expiry; another user's token is a `404` to delete; the listing
pages, filters and sorts, and never shows another user's token; every route is a `401`
unauthenticated; and a Basic-authenticated caller cannot mint a token.

*Depends on:* 2b, 4, and [`ItemExistsException` → `409`](authorization-plan.md#itemexistsexception--409--patch).

**Why:**

* [two token models rather than a nullable secret](#why-two-token-models-rather-than-a-nullable-secret)
* [the token listing is paged](#why-the-token-listing-is-paged)
* [there is no maximum lifetime](#why-there-is-no-maximum-lifetime)

### 6. Scopes on a `Bearer` token — `MINOR`

The realm half of [the `scope` claim](#the-scope-claim), and **the prerequisite for issue 3 rather
than a follow-up to it**: the values as client scopes in `keycloak/localdev.json`, riding on the
existing `pacman-manager` client scope so that the clients which already receive it keep working
once enforcement is on, plus the documentation of the grammar for anyone configuring a different
provider.

Confirm before assuming the naming: whether Keycloak accepts `:` in a client scope name. If it does
not, the values still have to appear verbatim in the token's `scope` claim, which a protocol mapper
can do regardless of what the scope object is called.

`ConfigureSwaggerGenOptions` lists the scopes its security requirement asks for, so the new values
are added there too.

No application code at all: the parser from issue 3 is the only thing that reads these.

*Acceptance:* a token from the updated realm carries the expected values, asserted by decoding it in
a test rather than by inspection. Then, once issue 3 lands, E2E tests with a token carrying
`pacman-manager:*:read` (writes are `403`), one carrying `pacman-manager:packages:publish`
(publishing succeeds, creating a repository is `403`), one carrying `pacman-manager:repositories:*`
(repository writes succeed, publishing is `403`), and one carrying none of ours (every operation is
a `403`).

*Depends on:* nothing in this document, but it touches `keycloak/localdev.json` and therefore the
`auth` service in `compose.yaml` rather than only C#.

**Why:**

* [every existing token becomes powerless](#every-existing-token-becomes-powerless)
* [the audience prefix is on every value](#why-the-audience-prefix-is-on-every-value)

---

## Deferred work

Worth filing as issues, but explicitly out of scope for the work above.

* **A queryable audit trail for tokens.** [Use is logged](#every-use-is-logged), which answers the
  question that matters after a suspected leak, and that is the whole interim measure. What is
  deferred is history a *user* can read — every use rather than the last, and revocations, which
  would change revocation from a delete to a soft delete.
* **Minting a narrowed `PacmanAccessToken`.** Only the Basic half is missing. A narrowed **`Bearer`**
  credential already works and needs nothing from us: register a client that requests a smaller set
  of scopes. What has no equivalent is a user asking for a `PacmanAccessToken` carrying anything
  other than `pacman-manager:*:read`, because this application mints those itself — a scope field on
  the create request, and a concept in the UI to go with it.
* **Per-instance authorization** — narrowing a credential to one repository rather than to
  repositories as a kind. **Postponed indefinitely**, and recorded here so the question is not
  reopened from scratch rather than because it is queued: a repository host of this size has no
  demonstrated need for it, and every mechanism that provides it is a large change.
  [It is deliberately outside what `scope` can express](#why-instances-are-not-in-here), and the
  three specified ways in are **RFC 9396** rich authorization requests (`authorization_details` with
  `actions` and an `identifier` per entry, returned with the token and in introspection), **UMA
  2.0** as implemented by Keycloak's Authorization Services (an RPT carrying per-resource
  permissions), and **RFC 8693** token exchange (narrow at mint time and leave the grammar alone).
  Whichever is chosen, the intersection rule above is what keeps it safe.
* **A credential that can publish.** Today a build pipeline still needs an OAuth client to push a
  package. That is the right default, but a purpose-built publishing credential — scoped to one
  repository, and distinct from these — is the obvious next thing to want.
* **Token expiry notifications.** An `ExpiresAt` that nothing warns about produces a build that
  breaks on a date nobody remembers.
* **Rate limiting per token**, which is where a compromised token stops being unbounded.
* **A maximum token count per user**, absent which nothing bounds the table.

---

## Appendix

The argument for each decision in the plan, including what was considered and rejected. Nothing
here adds a requirement — the plan above is the specification. These entries record what is
**settled**: if implementation suggests a different choice, the entry is the thing to argue with,
in an issue, rather than something to quietly depart from.

### Why this document also specifies scopes

`pacman` cannot obtain or refresh an OAuth token. It has one credential mechanism: a URL with
userinfo in it, which libcurl turns into an `Authorization: Basic` header. So a private repository is
reachable by `pacman` only if this application accepts Basic credentials — and the thing being
handed to a package manager on a laptop must not be the account password, and must not be able to
change anything.

That last requirement turns out to be a special case of a general one — a credential carrying a
statement of what it may be used for — so this document specifies that too, as ordinary OAuth 2.0
scopes, and how a `Bearer` token carries them. That half is not needed by `pacman`, and is
deliberately last in the plan; it is here because it is the same mechanism, and describing it
anywhere else would mean two statements of one rule.

### Authority is a property of the actor, not of the route

The requirement is that Basic auth can never write. The obvious implementation is to accept the
`Basic` scheme only on the [pacman routes](pacman-controller.md), which are all reads. That is true
today and stops being true the first time somebody adds a write route and reaches for the scheme
list without thinking about it.

So the restriction goes where this codebase already puts its authorization invariants — into the
`Actor`, which is
[the single thing every service authorizes against](authorization-plan.md#the-pieces). The
credential decides what its bearer may do; the route never does, and no service ever asks which
scheme a request arrived under.

The verdict methods are the one place that already knows an operation is a write. Putting the check
there means a new write path gets the behaviour by construction rather than by remembering — the
same structural argument that put enforcement in the service layer instead of an action filter in
the first place.

### Why the audience prefix is on every value

`pacman-manager` is this API's audience — `appsettings.json` sets it and an `oidc-audience-mapper` on
the realm's `pacman-manager` client scope emits it — so a scope value names the API it belongs to and
then the permission within it. That is the same shape Azure uses (`api://<id>/Files.Read`) and, in
spirit, Google's scope URIs.

It matters because `scope` is a namespace shared by every client in a realm: unprefixed values like
`users:read` or `tokens:write` are exactly the names another application would also want, and `aud`
and `scope` should not be able to disagree about which API is under discussion.

The entity is the **plural** resource name, so a scope value reads as the route it governs:
`pacman-manager:repositories:read` is `GET /api/v1/repositories`, and there is no second vocabulary
to learn or keep in step.

### Why scopes are additive, and what that costs

Deny-by-default is the conservative direction: a permission that has to be granted explicitly cannot
be granted by an oversight, and a bug in the parser fails closed.

The intersection rule — a scope narrows and never grants — is what makes trusting a provider's
`scope` claim safe at all. It is the same rule AWS session policies and GitHub App installation
tokens use. An identity provider can hand out any scope it likes; it cannot hand out access, because
every rule in [`authorization-plan.md`](authorization-plan.md#authorization-rules) still runs
afterwards.

The cost is [Every existing token becomes powerless](#every-existing-token-becomes-powerless).

### Every existing token becomes powerless

Deny-by-default is the safer rule and it is not free. Every `Bearer` token the realm issues today
carries `openid profile email roles pacman-manager` and nothing that matches the grammar, so on the
day this lands **every one of them can do nothing** — the web client and the Swagger UI included.
There is no version of additive scopes where that is not true; the only question is whether it is
planned for.

So the realm change is not the last step of this work, it is a **prerequisite**. The realm already
has a `pacman-manager` client scope, and it is already a *default* scope on `pacman-manager-web` and
`pacman-manager-swagger` — so the values go there, the existing clients keep receiving what they
receive today plus the scopes that make it mean something, and no client registration changes.
[Issue 6](#6-scopes-on-a-bearer-token--minor) therefore lands **before**
[issue 3](#3-scoped-actors--minor) turns enforcement on, and the two are expected to ship together.

A deployment against a provider that cannot be taught to emit these scopes does not have a
compatibility story under this rule. That is the cost of the conservative direction, and it is
recorded here rather than left for somebody to find.

### Why instances are not in here

The grammar names an entity *kind* and an action. It cannot say "this one repository", and that is a
consequence of choosing `scope` rather than an omission.

Scope values have to be **registered with the authorization server ahead of time** — they are
configuration, enumerable for a consent screen and for client registration. Keycloak models them as
client scopes, which are realm objects; `repositories:read:<guid>` would mean minting a realm object
per repository, and no OIDC provider is built to do that. Every ecosystem that uses plain scopes
lands in the same place: Slack's `chat:write`, GitHub's `read:org`, Auth0's `read:users` and Azure's
`Files.Read.All` all name a kind and an action, and none of them carry an identifier.

Instance-level authorization has its own specified answers, and all three are
[deferred](#deferred-work) rather than improvised here: **RFC 9396** rich authorization requests,
which carry a JSON `authorization_details` array with `actions` and an `identifier` per entry;
**UMA 2.0**, which is what Keycloak's own Authorization Services implement, returning per-resource
permissions in an RPT; and **RFC 8693** token exchange, which narrows at mint time and leaves the
grammar alone.

### Why a scope restricts what may be done, not what may be known

This falls out of [the grammar naming no instances](#why-instances-are-not-in-here), and it is the
best argument for that grammar. A scope can say "may not read repositories", which `CheckRead`
answers with a `403`, but it cannot say "may read only *these* repositories" — so there is no
predicate to intersect, and `VisibleTo` and `RepositoryService.VisibleAsync` are untouched.

The distinction is between a credential being **refused** an operation and a credential being shown
a **smaller world**. This design only ever does the first. Getting it wrong would silently hide a
user's own private repositories from their own `pacman` client, and the surest way to avoid it is to
have no code that could cause it.

### Why not a claim read by each service, or a scheme check, or a filter

Three alternatives were considered.

A **claim read at each call site** — every service pulling `scope` off the principal and
interpreting it — puts the rule in every service instead of in one place, which is what
`RepositoryAccessPolicy` exists to prevent. The claim is the transport; the `Actor` is the one place
that understands it, parsed once at the edge.

A **scheme check** (`if (scheme == "Basic")`) is the route-shaped version of the same mistake and
stops being true the first time a second read-only credential exists.

An **authorization filter** on the write routes only protects HTTP callers, which is the reason
[`authorization-plan.md`](authorization-plan.md#architecture-strategy-enforcement-in-the-service-layer)
rejected a filter for the repository rules.

Checking the scope in the **`IActorAccessor`** cannot work at all: the accessor produces the actor,
and has no idea what is about to be done with it.

### Why the identifier and the secret are separate fields

**Separating the identifier from the secret is the point of the format.** Verification is
[one query on the primary key](#verification-is-one-query). Without an identifier, verification would
have to hash the presented secret against every stored row, which is `O(tokens)` work on every single
HTTP request — and `pacman -Su` on a large repository issues one request per package.

**Each half carries its own prefix**, for different reasons. `pms_` is what lets a secret scanner
recognise a leaked secret on its own — in a CI variable, a paste, a password field — where a bare
43-character Base64Url string would be indistinguishable from any other random blob. `pmt_` makes the
username self-describing in a config file or a log, and lets a future credential type be told apart
by its prefix rather than by guessing. A secret found without its identifier can still be traced to
its row, since `TokenHash` is a deterministic hash of it; that is an investigation, not a hot path,
so it needs no index.

Putting the two halves in the two fields Basic auth already has, rather than joining them into one
string in the password, has three consequences worth having:

* **Parsing is two prefix checks, not a split.** A joined `pmt_{id}_{secret}` would have to split on
  exactly the first two underscores, because the Base64Url alphabet contains `_`.
* **The loggable half and the secret half arrive separately.** The handler can log the username
  freely and never touch the password, rather than carving the secret off a single string before
  logging anything.
* **Credential stores behave.** `.netrc`, git credential helpers and OS keyrings key an entry on host
  and username, so several tokens for one host sit side by side instead of overwriting each other.

The cost is two values to copy rather than one. That is acceptable because hand-written client
configuration is not the expected path: a generated repository configuration or installer script is
[planned](pacman-controller.md#deferred-work), and it writes both.

Knowing an identifier without the matching secret is worth nothing, so the fact that ids are
enumerable and that a v7 GUID discloses its creation time costs nothing here.

### Why the hash is fast and unsalted

**Not bcrypt, Argon2 or PBKDF2, and this is a deliberate departure from how a password would be
stored.** Those are slow *on purpose*, to make guessing a low-entropy human-chosen secret expensive.
This secret is 256 bits from a CSPRNG: there is no dictionary, no reuse across sites and nothing to
guess, so the work factor buys nothing — while costing tens of milliseconds on every request. A
`pacman -Syu` pulling 300 packages would spend seconds of server CPU on key derivation alone, which
turns a security control into a self-inflicted denial of service. This is the same reasoning behind
GitHub's personal access tokens, which are also a fast hash over a high-entropy random value.

Unsalted for the same reason: a salt defeats precomputation across a stolen table, and there is
nothing to precompute against a uniformly random 256-bit value.

### Why verification is a single query

An unknown identifier and a wrong secret must be the same outcome, reached by the same path. Fetching
by id and comparing in memory would not have that property: an unknown id skips the comparison and
returns sooner, telling a caller whether a token id exists. Ids are not secret, so that would be a
small leak, but closing it costs nothing.

The primary key already serves the query. No index on `(Id, TokenHash)` is needed, and a unique one
would add nothing: `Id` is unique on its own, so the pair already is.

**Expiry is checked on the returned row rather than in the predicate** because a request that reaches
that check has presented the right identifier and the right secret, so distinguishing "expired" from
"no match" discloses nothing to anyone who does not already hold the token — and "my token expired"
is the failure a user most needs diagnosed.

### Why there is no constant-time comparison

The comparison happens in the database, which does not compare in constant time, and that is safe
for a reason specific to comparing *hashes*. An attacker controls the secret, not its hash, so
learning how many leading bytes of `SHA-256(guess)` match the stored value tells them nothing about
which secret to try next. The constant-time requirement is for comparing raw secrets, which this
design never does.

### Why revocation is a delete rather than a flag

The absence of the row is then the only state there is, which is one fewer thing for the
verification path to check and no possibility of a "revoked" token that some code path forgets to
filter out. An audit trail of revocations would be worth having and is [deferred](#deferred-work);
it is not worth complicating the hot path for now.

A token that belongs to another user is a `404` rather than a `403` because the route is scoped to
`me`, so a token that is not the caller's is simply not addressable. Saying "forbidden" would confirm
that the id names a real token belonging to somebody.

### Why `LastUsedAt` is coarse

`LastUsedAt` exists so a user can tell which of their tokens is still in service. Writing it on every
request would mean **one `UPDATE` per package downloaded**, which is a write amplification the
feature does not justify.

Writing it only when stale reduces it to at most one write per token per hour while keeping the value
accurate enough for the question it answers — "is this token still in use", not "when exactly was
its last request".

### Why every use is logged before there is an audit trail

`LastUsedAt` answers "is this token still in service". It cannot answer "where was it used from", and
that is the question somebody asks when a token may have leaked. A full audit trail is
[deferred](#deferred-work); **a log line is not**, and it costs nothing to write now.

The timestamp comes free from the logging configuration, and the owning user is derivable from the
token id, so those are not repeated into the message.

The constraint that the secret is never logged already exists for [the `Authorization`
header](#transport); the use log is the place it would most plausibly be broken, because a log line
about a credential is exactly where somebody reaches for the credential.

Logging an address nobody can rely on would be worse than logging none, which is why the forwarded
headers question has to be settled in the same issue rather than left as a follow-up.

### Why two services, not one

The work divides cleanly along whether an `Actor` exists yet, and the two halves become two services
because of it. `IBasicAuthenticationService` runs *before* an actor exists; `IAccessTokenService` is
actor-scoped like every other service.

A single service holding both would have a method that deliberately bypasses the actor sitting next
to methods that depend on it, which is exactly the shape somebody later copies by accident. Two
types, with two jobs and two rules about what they may assume, cannot be confused for one another.

Letting both name `PacmanAccessTokens` is safe for a reason worth writing down: the pre-auth query
has no visibility rule to duplicate. It is a lookup by primary key whose result authenticates
somebody rather than being shown to them.

### Why token management is not on `IUserService`

Token management is arguably user management, and `IUserService` already exists — but it is not
actor-scoped and cannot become so, because `ClaimsTransformer` calls it during authentication, before
there is an actor to scope to. Putting actor-scoped methods on it would recreate the mixed-authority
problem the split exists to avoid.

### Why a policy scheme selects the handler

Registering a second scheme is not enough on its own, and this is the part of the design most likely
to be got wrong. `Program.cs` registers
`AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer()`, and the authentication
middleware runs **only the default scheme**.

Nor can the pacman routes opt in with `[Authorize(AuthenticationSchemes = "Basic")]`: those routes
are `[AllowAnonymous]`, which is precisely the metadata that suppresses that attribute. A `Basic`
header would reach the `Bearer` handler, which returns `NoResult`, the request would proceed as
anonymous, and the Basic handler would never run at all.

A policy scheme forwarding on the header prefix is the one mechanism that runs before any endpoint
metadata is consulted.

### Why a present-but-invalid credential must not fall through

The routes this scheme serves are `[AllowAnonymous]`, because a public repository has to serve an
unauthenticated client. **ASP.NET Core's default behaviour on an `[AllowAnonymous]` endpoint is to
ignore a failed authentication result and continue as anonymous.** A user who mistypes their token
would then get a `404` on their private repository — indistinguishable from the repository not
existing, and the single most confusing failure this feature could produce.

[The forwarding scheme](#the-authorization-prefix-picks-the-handler) gets the handler invoked, which
is half of it. The other half is that a failed authentication result on an `[AllowAnonymous]`
endpoint is discarded before anything challenges, so the handler's own `HandleChallengeAsync` is
never reached — hence the middleware.

The asymmetry with the visibility rules is correct rather than an inconsistency. Bad credentials are
a fact about the caller and disclose nothing about what exists, so they are answered honestly.

### Why `Basic` gets no Swagger security definition

Swagger is how somebody learns to drive this API, and the answer it should give is `Bearer`. `Basic`
exists for `pacman`, which cannot speak OAuth, and advertising it beside the OAuth flow would present
it as an equivalent way in. It is not one, and nothing should suggest it is.

### Why the username carries the identifier rather than a placeholder

An earlier draft of this design put the whole token in the password and **ignored** the username,
following the convention GitHub, GitLab and npm use. That convention suits a token typed into config
by hand, as one string to copy.

Here the field does work instead of carrying a placeholder, for the reasons under [Why the identifier
and the secret are separate fields](#why-the-identifier-and-the-secret-are-separate-fields) — two
prefix checks rather than a split, a loggable half that arrives separately, and one credential-store
entry per token — and hand-written configuration is not the path this service expects users to take.

### Why the credential is a token rather than the account password

Basic auth sends a reusable credential in a header on every request, and it ends up written in
cleartext in the user's `pacman.conf`. That is the cost of the only mechanism `pacman` supports, and
it is the reason tokens are per-user, individually revocable and optionally expiring: the credential
on the laptop is one that can be thrown away without touching the account.

### Why the token listing is paged

One user's token count is bounded by their patience, so paging here is arguably overkill. It is the
shape anyway, for two reasons: a listing that ships without pagination cannot grow it later without
breaking every caller, and a reader of this API should not have to learn which listings are special.

### Why there is no filter on expiry

Sorting by `ExpiresAt` puts the tokens nearest to lapsing together, which is the question a user
actually asks. A filter for it would be a second way to ask the same thing over a handful of rows.

### Why two token models rather than a nullable secret

The same reason the user models split on `email`: a field that is only sometimes populated is a
field that will one day be populated by accident. Here that accident would put a live credential in
a listing.

### Why there is no maximum lifetime

A credential that lives in a `pacman.conf` on a machine somebody administers by hand is one whose
lifetime the owner is better placed to judge than we are, and
[expiry notifications](#deferred-work) are the thing that would make a cap humane.
