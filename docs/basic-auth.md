# Basic Auth

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
| `action` | one the entity has, from the table below, or `*` |

Each entity has only the actions that exist for it:

| Entity | Actions | Notes |
| :--- | :--- | :--- |
| `repositories` | `read`, `create`, `update`, `delete` | |
| `packages` | `read`, `create`, `delete` | Packages are upserted, so `create` covers publishing a new version over an old one. There is no `update`. |
| `users` | `read`, `update` | Users are created on first sign-in, not by a request, and are never deleted. |
| `tokens` | `read`, `create`, `delete` | A token is never changed after it is minted. |

`pacman-manager:repositories:read` reads repositories. `pacman-manager:*:read` is "read anything
this user can read". `pacman-manager:packages:create` is "publish packages", and on its own is
useless: see the rule about reads below. `pacman-manager:repositories:*` is every operation on
repositories. `pacman-manager:*:update` names `update` on every entity that has it. A credential
carries as many values as it needs and they are `OR`'d: permitted if any one of them matches.

Four rules govern the claim, and all four are requirements rather than observations:

* **Scopes are purely additive: absent means denied.** A credential may do what its scopes name and
  nothing else, so a token carrying none of our values can change nothing, and reads what an
  anonymous caller could read. It is still its user's token: the actor keeps the user, and only
  the permissions are absent. A bug in the parser therefore fails closed. This has a consequence that
  must be planned for rather than discovered; see
  [Every existing token becomes powerless](#every-existing-token-becomes-powerless).
* **Every action on an entity needs `read` on it as well, and a package needs `read` on its
  repository too.** A credential that cannot read an entity [reads it as an anonymous caller
  would](#a-credential-that-cannot-read-reads-as-anonymous), so the entity it wants to change is not
  there to be found, and every change verdict demands the read scope alongside the action. So
  `pacman-manager:repositories:delete` on its own deletes nothing, and publishing needs
  `pacman-manager:packages:create`, `pacman-manager:packages:read` and
  `pacman-manager:repositories:read`.
* **A scope never grants, it only narrows.** What it names is a *ceiling*, not a permission: every
  rule in [`authorization-plan.md`](authorization-plan.md#authorization-rules) still runs afterwards,
  unchanged, so `pacman-manager:repositories:update` on a repository somebody else owns is still a
  refusal. An identity provider can hand out any scope it likes; it cannot hand out access.
* **A value that is not ours is ignored**, never fatal and never permissive. `scope` legitimately
  carries `openid`, `profile`, `email`, `roles` and the bare `pacman-manager` audience value, and
  none of those grant anything here. A malformed value of ours is dropped the same way and logged,
  and a well-formed value naming an action its entity does not have (`pacman-manager:users:delete`)
  counts as malformed. A provider that mangles a value cannot escalate through it; it can only take
  a permission away.

**Why:**

* [each entity has its own actions](#why-each-entity-has-its-own-actions)
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

In Keycloak each value is a client scope of the same name, and every value the grammar allows is
registered in `keycloak/localdev.json`:

* **`pacman-manager:*:*`** is a **default** client scope on the interactive clients,
  `pacman-manager-web` and `pacman-manager-swagger`, and on the realm's default default scopes, so
  they keep working once enforcement is on.
* **Every narrower value** is a realm default *optional* client scope, issued only when a client
  asks for it.
* The existing **`pacman-manager`** client scope is unchanged: it carries the audience mapper and
  nothing else.
* A narrow credential is a client configured with a narrower set. The realm has one,
  **`pacman-manager-scoped`**: a public client that receives `pacman-manager` by default but not
  `pacman-manager:*:*`, and may be issued any narrower value. A token from it carries exactly the
  values it asks for.

This half needs no application code beyond the parser. `ScopeValues` in RepoHost names the values,
and `ConfigureSwaggerGenOptions` lists them in its security requirement, but nothing reads them off
a token until [issue 3](#3-scoped-actors--minor).

[Issue 6](#6-scopes-on-a-bearer-token--minor) registered the grammar as it first stood: five actions
crossed with every entity. [Issue 8](#8-each-entity-has-its-own-actions--minor) replaces those values
with the ones tabulated above. The clients, and which values they receive by default, do not change.

**Why:**

* [`pacman-manager:*:*` is its own client scope](#why-pacman-manager-is-its-own-client-scope)
* [each entity has its own actions](#why-each-entity-has-its-own-actions)

### Configuring another identity provider

Nothing in this application is specific to Keycloak. A different provider works if it puts our values
into the access token's **`scope`** claim, as one space-delimited string, alongside an `aud` that
includes `pacman-manager`. A token without that audience is rejected before its scope is read. Our
values are exactly these 21 strings, and nothing else of the form `pacman-manager:…` means anything:

| Form | Values |
| :--- | :--- |
| Everything | `pacman-manager:*:*` |
| `<entity>:<action>` | the 12 pairs [tabulated above](#the-scope-claim): `repositories` with `read`, `create`, `update`, `delete`; `packages` with `read`, `create`, `delete`; `users` with `read`, `update`; `tokens` with `read`, `create`, `delete` |
| `<entity>:*` | `pacman-manager:repositories:*`, `pacman-manager:packages:*`, `pacman-manager:users:*`, `pacman-manager:tokens:*` |
| `*:<action>` | `pacman-manager:*:read`, `pacman-manager:*:create`, `pacman-manager:*:update`, `pacman-manager:*:delete` |

The values are case-sensitive. A client that should have its user's full access needs
`pacman-manager:*:*`; one that should only read needs `pacman-manager:*:read`. Every rule under
[the `scope` claim](#the-scope-claim) applies whichever provider issues it: a token carrying none of
these values can change nothing, and reads as an anonymous caller would.

### How the actor enforces it

`Actor` carries the parsed scope, and read-only becomes a question asked of it rather than a
separate flag:

```csharp
public ActorScope Scope { get; }

public bool IsReadOnly => !Scope.PermitsAnyActionButRead;

public static Actor For(User user, ActorScope scope) => new(user, isSystem: false, scope);
```

* **`HttpContextActorAccessor` builds the actor from the principal**: the user from the
  `AppUserIdClaimType` claim, as [issue 9](#9-the-actor-accessor-reads-the-principal-itself--patch)
  arranges, and the scope from the `scope` claim. It is the one place the claim is parsed.
* **A credential carrying none of our values is not a special case.** The accessor builds an actor
  for the user with an empty scope, exactly as it would for any other scope. That actor may change
  nothing and reads what an anonymous caller could read, but it is still recognisably that user to
  anything that asks who is calling: the logs, and anything later built on identity, such as rate
  limiting. A token without the `pacman-manager` audience never gets this far; the `Bearer` handler
  rejects it. `Actor.Anonymous` carries `ActorScope.Empty`, which no verdict consults, because the
  no-user arm of every verdict answers before the scope arm does.
* **Every verdict asks the scope**, after the no-user arm and before the ownership tests, and
  returns `Forbidden` when the scope does not permit that action on that entity, together with
  `read` on it:

  | Verdict | Needs |
  | :--- | :--- |
  | `RepositoryAccessPolicy.CheckCreate` | `repositories:create`, `repositories:read` |
  | `RepositoryAccessPolicy.CheckUpdate` | `repositories:update`, `repositories:read` |
  | `RepositoryAccessPolicy.CheckDelete` | `repositories:delete`, `repositories:read` |
  | `PackageAccessPolicy.CheckPublish` | `packages:create`, `packages:read`, `repositories:read` |
  | `PackageAccessPolicy.CheckDelete` | `packages:delete`, `packages:read`, `repositories:read` |
  | `UserAccessPolicy.CheckReadCurrent` | `users:read` |
  | `UserAccessPolicy.CheckUpdateCurrent` | `users:update`, `users:read` |
  | `AccessTokenAccessPolicy.CheckRead` | `tokens:read` |
  | `AccessTokenAccessPolicy.CheckCreate` | `tokens:create`, `tokens:read` |
  | `AccessTokenAccessPolicy.CheckDelete` | `tokens:delete`, `tokens:read` |

  [Issue 10](#10-a-verdict-per-action--patch) splits the repository and package verdicts so that
  each action has its own. The user and token policies are new, in
  [issues 11](#11-useraccesspolicy--minor) and [12](#12-accesstokenaccesspolicy--minor).
* **Repositories and packages have no read verdict. A credential that cannot read them reads them as
  anonymous**; see [below](#a-credential-that-cannot-read-reads-as-anonymous). Users and tokens do
  have read verdicts, but only where no anonymous view exists: `/users/me` and every token route.
  `/users` and `/users/{userId}` are anonymous routes, so they need no scope at all.
* **An actor with no credential behind it is unrestricted.** `Actor.System` and `FixedActorAccessor`
  take `ActorScope.Unrestricted` explicitly, so background jobs and command-line tools are
  unaffected.
* **A credential never gets less than an anonymous caller would.** An `[AllowAnonymous]` route that
  consults no actor, such as the pacman routes for a public repository, serves a narrowly scoped
  credential exactly as it serves a stranger. So does a route that consults one, because a missing
  read scope falls back to the anonymous view rather than to a refusal. Anything else would mean a
  user could see more by deleting their `Authorization` header, which is absurd on its face and
  would teach people to do it.
* **Every entity's scope check is in its access policy.** Users and tokens had none, so each gets
  one, `UserAccessPolicy` and `AccessTokenAccessPolicy`, shaped like the other two: internal, with
  no database, HTTP or logging dependency, and with every rule a plain unit test.
* `scope` arrives as **one space-delimited string**, not as repeated claims, so it is split before it
  is parsed.
* `Program.cs` already calls `JsonWebTokenHandler.DefaultInboundClaimTypeMap.Clear()`, so the claim
  reaches the principal as `scope` rather than remapped to a WS-Federation-era URI. It must stay
  cleared.
* A refused change is `Forbidden` (`403`) rather than `Unauthenticated` (`401`), including on a
  private repository the actor owns: the caller is authenticated, re-authenticating with the same
  credential will not help, and they already know the repository exists. That includes a credential
  with none of our values: its user is known, so it is refused, not challenged.

**Why:**

* [authority is a property of the actor, not of the route](#authority-is-a-property-of-the-actor-not-of-the-route)
* [not a claim read by each service, or a scheme check, or a filter](#why-not-a-claim-read-by-each-service-or-a-scheme-check-or-a-filter)
* [a credential with none of our values keeps its user](#why-a-credential-with-none-of-our-values-keeps-its-user)
* [delete is its own action](#why-delete-is-its-own-action)
* [users and tokens get access policies](#why-users-and-tokens-get-access-policies)

### A credential that cannot read reads as anonymous

Repository and package reads have no verdict method. A read is refused by showing the caller less,
never by a `403`:

* **`RepositoryAccessPolicy.VisibleTo`** gains one arm, after `IsSystem`: when the actor's scope does
  not permit `repositories:read`, it returns the predicate it returns for `Actor.Anonymous`, which
  is the public repositories. Otherwise it is unchanged, and a credential that may read repositories
  sees exactly what its user sees, their own private repositories included.
* **Package visibility needs both `packages:read` and `repositories:read`.** `PackageAccessPolicy`
  gains a `VisibleTo` of its own that returns `RepositoryAccessPolicy.VisibleTo` for the actor when
  its scope permits `packages:read`, and the anonymous predicate when it does not.
  `PackageService.VisibleRepositoriesAsync` calls it instead of `RepositoryAccessPolicy.VisibleTo`,
  and is still the only definition of package visibility `PackageService` has.
* **Every change starts from the visible set**, since `RepositoryService.VisibleAsync` and
  `PackageService.VisibleRepositoriesAsync` are where every write finds its row. Without the read
  scope, the actor's own private repository is not in that set, and the change is the same `404` a
  stranger would get. Their public repository is found, and the change verdict refuses it with a
  `403`, because the verdict's scope arm demands the read scope too.
* A listing with no read scope is the public page, not a `403`.

**Why:**

* [a credential that cannot read reads as anonymous](#why-a-credential-that-cannot-read-reads-as-anonymous)
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
| `Name` | `string` | A label the user chose, so a list of tokens is legible. Shown exactly as entered. |
| `NormalizedName` | `string` | `Name.ToLowerInvariant()`. Used for indexing and matching, **never shown**. |
| `TokenHash` | `string` | Base64 SHA-256 of the secret. |
| `CreatedAt` | `DateTimeOffset` | |
| `ExpiresAt` | `DateTimeOffset?` | Optional. A token past it fails to authenticate. |
| `LastUsedAt` | `DateTimeOffset?` | Coarsely maintained; see [Recording use](#recording-use). |

Unique index on **`(UserId, NormalizedName)`**, so `laptop` and `Laptop` are the same name and the
second is a `409`. Validation limits go in `PacmanManager.Entities.AccessTokenValidationConstants`,
alongside the existing per-entity constants; `NormalizedName` shares `Name`'s maximum length.
Cascade delete from `User` is what makes account deletion tractable later; it is not reachable today,
since nothing deletes a user.

* `NormalizedName` is written by the service, from `Name`, in the same statement that writes `Name`.
  Nothing else sets it, and no wire model carries it — in or out.
* Lowering uses the **invariant culture**, never the current one, so the same name normalizes the
  same way on every host.

**Why:**

* [a token name has a normalized copy](#why-a-token-name-has-a-normalized-copy)

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

The address logged is `HttpContext.Connection.RemoteIpAddress`, and it is only the client's address
once [trusted forwarded-header sources](#trusted-forwarded-header-sources) are configured. Behind the
reverse proxy that [terminates TLS](#transport) it is otherwise the proxy's.

**Why:**

* [every use is logged before there is an audit trail](#why-every-use-is-logged-before-there-is-an-audit-trail)

### Trusted forwarded-header sources

`Program.cs` configures no forwarded headers today. It gains `UseForwardedHeaders`, driven by a
configuration section naming the proxies whose `X-Forwarded-*` headers are believed:

```jsonc
// appsettings.json
"ForwardedHeaders": {
  "TrustedSources": [ "10.0.0.5", "172.16.0.0/12" ]
}
```

* **Each entry is a single IP address or a subnet in CIDR notation.** An entry containing `/` is
  parsed with `System.Net.IPNetwork.Parse` and added to `KnownIPNetworks`; any other entry is parsed
  with `IPAddress.Parse` and added to `KnownProxies`. IPv4 and IPv6 are both accepted.
* **An entry that parses as neither fails startup**, through options validation, naming the entry.
  A mistyped proxy address must not quietly become a proxy that is not trusted.
* The headers forwarded are `X-Forwarded-For` and `X-Forwarded-Proto`, and `UseForwardedHeaders` runs
  **before** `UseAuthentication()` and before request logging, so everything downstream sees the
  client's address.
* **An absent or empty section keeps ASP.NET Core's defaults**, which trust loopback only. The lists
  are never cleared: an empty list must not mean "trust every caller", since that lets any client
  forge the address the use log records.
* Use `KnownIPNetworks` (`IList<System.Net.IPNetwork>`) for subnets. The older `KnownNetworks` is
  `[Obsolete]` on net10.0, along with `Microsoft.AspNetCore.HttpOverrides.IPNetwork`.
* `appsettings.json` carries the section with an empty `TrustedSources`, so the key is discoverable,
  and the setting is overridable from the environment like every other key
  (`ForwardedHeaders__TrustedSources__0`).

**Why:**

* [trusted proxies are configuration](#why-trusted-proxies-are-configuration)

### Two services own this, not one

**Verification is a method on `IUserService`**, which is already not actor-aware:

```csharp
/// Returns the user the access token belongs to if and only if the token is valid; otherwise null.
Task<User?> GetUserByAccessTokenAsync(string username, string password, CancellationToken ct = default);
```

* It takes the two decoded Basic fields and returns the owning **`User`, or `null`** — for a malformed
  field, an unknown identifier, a wrong secret and an expired token alike. It never throws for a bad
  credential.
* Parsing both fields, [the lookup on id and hash](#verification-is-one-query), the expiry check, the
  coarse `LastUsedAt` write and [the use log](#every-use-is-logged) all happen inside it. The method
  stays free of HTTP: the client address reaches its log lines through a logging scope the
  [handler](#the-handler) opens around the call, not through a parameter or `HttpContext`.
* **It returns a `User`, not a `ClaimsPrincipal`.** Building the principal and its claims is
  [the handler's](#the-handler) job.
* **It cannot take `IActorAccessor`**: the accessor depends on the authenticated principal, which is
  what this method's result is used to build. `UserService`
  takes no actor today and must not start.
* The token format — parsing `pmt_`/`pms_`, generating a secret, hashing it — is a static
  `AccessTokenFormat` with no dependencies, shared by this method and by minting.

**`IAccessTokenService` is actor-scoped** like every other service: minting (over
`AccessTokenFormat`, `RandomNumberGenerator`, the once-only return), the
[listing](#the-listing-follows-the-standard-shape), and deletion. Every method takes the actor's own
tokens as its subject, so a caller can never name another user's.

**Which service owns the `DbSet`.** `PacmanAccessTokens` is touched by `UserService` and
`AccessTokenService`, and the enforcement test names both and asserts that nothing else does. This is
a deliberate departure from the one-method rule
[`authorization-plan.md`](authorization-plan.md#why-this-is-hard-to-get-wrong) holds for
`PacmanRepositories`.

**Why:**

* [two services, not one](#why-two-services-not-one)
* [verification is on `IUserService`](#why-verification-is-on-iuserservice)
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

An `AuthenticationHandler<BasicAuthenticationSchemeOptions>` that decodes
`Authorization: Basic base64(username:password)` and passes both fields, with the client address, to
[`IUserService.GetUserByAccessTokenAsync`](#two-services-own-this-not-one) inside a logging scope
carrying the client address. A `null` is a failure; a `User` becomes the principal.

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
| `nameContains` | `StringContainsQuery` | Substring of the token's name, case-insensitive: the term is lowered with `ToLowerInvariant()` once in C# and matched against `NormalizedName`. |

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
  user already has, compared case-insensitively through `NormalizedName`, is the `409` above.
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
thing to do. Issue 7 is the same: it is small and independent, and it lands before issue 4. Issues 8,
9 and 10 are groundwork that issue 3 was found to need once implementation started, and all three
land before it. Issues 11 and 12 follow it.

### 1. `PacmanAccessToken` entity and migration — `MINOR`

The entity as tabulated, `AccessTokenValidationConstants`, `PacmanAccessTokens` on
`PacmanManagerDbContext`, and a migration named `AddTable_PacmanAccessTokens`.

*Acceptance:* `dotnet ef migrations list` shows it; applying it against the compose Postgres
succeeds; the unique index on `(UserId, NormalizedName)` exists, there is no unique index on `Name`,
and the FK cascades from `User`.

*Depends on:* nothing.

**Why:**

* [a token name has a normalized copy](#why-a-token-name-has-a-normalized-copy)

### 2a. `IUserService.GetUserByAccessTokenAsync` — `MINOR`

The pre-auth half, [as described above](#two-services-own-this-not-one): the method on
`IUserService` and `UserService`, taking the presented username and password and returning the
owning `User` or `null` — parsing, [the lookup on id and hash](#verification-is-one-query), the
expiry check, the coarse `LastUsedAt` write, and [the use log](#every-use-is-logged). `UserService`
stays not actor-scoped, and nothing here takes `IActorAccessor`.

Verification needs a token to verify, so the static `AccessTokenFormat` — parsing, generating and
hashing — lands here too, and [issue 2b](#2b-iaccesstokenservice--minor) mints over it.

*Constraints:* the fast unsalted hash under [Hashing](#hashing) is correct only for a CSPRNG-generated
secret, and the single-query lookup under [Verification is one query](#verification-is-one-query) is
a requirement rather than an optimisation. Both carry XML docs saying so.

*Acceptance:* unit tests that a minted token returns its owner; that every failure below returns
`null` rather than throwing; that a secret differing in one character does not verify; that a
malformed, truncated or wrongly-prefixed username or password is rejected without
throwing and without querying — including the two prefixes swapped between the fields, and a secret
containing `_`, which is in the Base64Url alphabet and must not confuse the parser; that an unknown
token id is rejected; that a real id with another token's secret is rejected; that an expired token
is rejected; that the stored hash is not the secret and the secret is not recoverable from the row;
and that a minted secret is Base64Url and therefore URL-safe. `AccessTokenFormat` has unit tests of
its own, needing no database.

The lookup is asserted to be a single query predicated on both id and hash, so that an unknown id
and a wrong secret take the same path; a test that moves the comparison into memory must fail.

`LastUsedAt` is written when stale and skipped when fresh, a failure to write it does not fail
verification, and its resolution is a configuration key with an entry in `appsettings.json` and a
default of one hour, tested at both a stale and a fresh value.

The log is asserted, not assumed: a successful verification writes the token id, a failed one writes the token id where the username yielded one along with the reason —
malformed, no match, or expired — and **no test-visible log line contains the secret**, asserted
against a captured log rather than by reading the code. The client address is the handler's to
assert, in issue 4, since it arrives through the handler's logging scope.

*Depends on:* 1.

**Why:**

* [the hash is fast and unsalted](#why-the-hash-is-fast-and-unsalted)
* [verification is a single query](#why-verification-is-a-single-query)
* [there is no constant-time comparison](#why-there-is-no-constant-time-comparison)
* [`LastUsedAt` is coarse](#why-lastusedat-is-coarse)
* [every use is logged before there is an audit trail](#why-every-use-is-logged-before-there-is-an-audit-trail)
* [verification is on `IUserService`](#why-verification-is-on-iuserservice)

### 2b. `IAccessTokenService` — `MINOR`

The actor-scoped half: minting on behalf of the caller (over the `AccessTokenFormat`
[issue 2a](#2a-iuserservicegetuserbyaccesstokenasync--minor) adds, and writing `NormalizedName`
alongside `Name`), the
[listing](#the-listing-follows-the-standard-shape), and deletion. Every method's subject is the
actor's own tokens.

The types the service's signatures are written in land here too: the three wire models
([`CreateAccessTokenRequest`, `AccessToken` and `CreatedAccessToken`](#models)), including the rule
that a past `expiresAt` fails validation; the
[`AccessTokenFilter` and `AccessTokenSortField`](#the-listing-follows-the-standard-shape); and raising
`ItemExistsException` on a duplicate token name, which only the minting method can do.

*Acceptance:* listing returns only the actor's own tokens, filtered and sorted as specified;
deleting another user's token is a `404`-shaped miss rather than a forbidden; minting returns the
secret exactly once and never again, and the listing model has no secret to return; a minted token's
`NormalizedName` is its name lowered with the invariant culture, including under a non-invariant
current culture such as `tr-TR`; a second token with the same name, or one differing only in case,
raises `ItemExistsException`, asserted against Postgres since the in-memory provider does not enforce
unique indexes; `CreateAccessTokenRequest` rejects an `expiresAt` that is not in the future and
accepts an omitted one. An enforcement test asserts that `AccessTokenService` and `UserService` are
the only things naming `DbContext.PacmanAccessTokens`.

*Depends on:* 2a.

**Why:**

* [two services, not one](#why-two-services-not-one)
* [token management is not on `IUserService`](#why-token-management-is-not-on-iuserservice)
* [revocation is a delete rather than a flag](#why-revocation-is-a-delete-rather-than-a-flag)
* [the service owns its models, filter and sort field](#why-the-service-owns-its-models-filter-and-sort-field)
* [two token models rather than a nullable secret](#why-two-token-models-rather-than-a-nullable-secret)
* [there is no maximum lifetime](#why-there-is-no-maximum-lifetime)

### 3. Scoped actors — `MINOR`

`ActorScope` and its parser. The parser splits the space-delimited `scope` claim and keeps the values
matching `<audience>:<entity>:<action>` where the action is
[one the entity has](#the-scope-claim), with `*` accepted for entity and action. Everything else is
ignored, and the scope is **empty** when none of the values are ours. The entities and actions come
from `ScopeValues`, not from a second list.

Also in this issue:

* `Actor.Scope`, and `IsReadOnly` derived from it. `Actor.Anonymous` carries `ActorScope.Empty`.
  `Actor.System` and `FixedActorAccessor` pass `ActorScope.Unrestricted` explicitly.
* `HttpContextActorAccessor` parses the principal's `scope` claim and passes it to `Actor.For`,
  whatever it parses to, an empty scope included.
* The scope arm in every change verdict, after the no-user arm and before the ownership tests, as
  [tabulated above](#how-the-actor-enforces-it).
* The read arm in `RepositoryAccessPolicy.VisibleTo`, and `PackageAccessPolicy.VisibleTo`, which
  `PackageService.VisibleRepositoriesAsync` switches to, as described under
  [A credential that cannot read reads as anonymous](#a-credential-that-cannot-read-reads-as-anonymous).
* `InsufficientScopeException`, naming the entity and action refused, and an arm in
  `AuthorizationExceptionHandler` mapping it to `403`. Nothing in this issue throws it. It is the
  groundwork [issues 11](#11-useraccesspolicy--minor) and
  [12](#12-accesstokenaccesspolicy--minor) both need, and it lands here so that neither of them adds
  it.

**This issue cannot land before [issue 6](#6-scopes-on-a-bearer-token--minor)**, which is what makes
the realm emit the scopes, **nor before [issue 8](#8-each-entity-has-its-own-actions--minor)**, which
makes it emit the values this parser accepts. Turning enforcement on against a realm that emits none
of them leaves every token unable to change anything. The accessor change and the verdicts land
together for the same reason: verdicts that enforce a scope the accessor never supplies would do the
same to every `Bearer` caller.

Separate from issue 4 because it has no scheme in it, and reviewable on its own, which is what makes
it worth reviewing carefully: it is the guarantee the whole document rests on.

*Acceptance:* parser unit tests for a wildcard entity, a wildcard action, both, an unknown entity, an
unknown action, an action the entity does not have (`pacman-manager:users:delete`,
`pacman-manager:packages:update`), a missing prefix, a wrong prefix, and a value with the right
prefix but too few segments. Then the four that matter most, because they are where a mistake is
silent:

* A claim of `openid profile email roles pacman-manager`, none of it ours, parses to an **empty**
  scope. `HttpContextActorAccessor` turns a principal carrying it into an actor that still has its
  user, and that actor is `Forbidden` on every change and reads only what `Actor.Anonymous` reads.
  This is the test that pins the direction of the default.
* A claim mixing ours with somebody else's (`openid pacman-manager:repositories:read`) keeps the one
  and ignores the rest.
* An actor built by `FixedActorAccessor` or `Actor.System` is unrestricted.
* `RepositoryAccessPolicy.VisibleTo` for a scope without `repositories:read` is the public predicate,
  and `PackageAccessPolicy.VisibleTo` for a scope with `repositories:read` but not `packages:read` is
  too. Both are asserted directly.

Both policy test fixtures gain rows for a `pacman-manager:*:read` scope on every verdict, including
a read-only owner of a private repository getting `Forbidden` rather than `NotFound` on a change,
and that same owner still *reading* it. They also gain rows for a change scope without its read
scope being `Forbidden` on the owner's own repository, and for a scope that permits an operation
still losing to the ownership rules underneath. A handler test asserts that `InsufficientScopeException`
produces a `403`.

E2E tests with tokens from `pacman-manager-scoped`:

* one carrying `pacman-manager:*:read`: changes are `403`, and the caller's own private repository
  is listed;
* one carrying `pacman-manager:packages:*` and `pacman-manager:repositories:read`: publishing
  succeeds, and creating a repository is `403`;
* one carrying `pacman-manager:repositories:*`: repository changes succeed, and publishing is `403`;
* one carrying `pacman-manager:repositories:delete` alone: deleting the caller's own private
  repository is `404`, and the listing shows only public repositories;
* one carrying none of ours: every change is a `403`, and the listing is exactly the anonymous one.

*Depends on:* 6, 8, 9, 10.

**Why:**

* [scopes are additive, and what that costs](#why-scopes-are-additive-and-what-that-costs)
* [every existing token becomes powerless](#every-existing-token-becomes-powerless)
* [a credential that cannot read reads as anonymous](#why-a-credential-that-cannot-read-reads-as-anonymous)
* [a credential with none of our values keeps its user](#why-a-credential-with-none-of-our-values-keeps-its-user)
* [not a claim read by each service, or a scheme check, or a filter](#why-not-a-claim-read-by-each-service-or-a-scheme-check-or-a-filter)

### 4. The `Basic` scheme and its handler — `MINOR`

The [selector policy scheme](#the-authorization-prefix-picks-the-handler) and the constants naming
it, the `Basic` scheme and its options, the handler over `IUserService.GetUserByAccessTokenAsync`, the scope
claim it issues, and the
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
creates no `ExternalProviderUserMapping`. A handler test asserts the verification log lines carry
the client address, from the logging scope the handler opens.

The `[AllowAnonymous]`-plus-`401` behaviour needs an anonymous endpoint to test against, and the
pacman routes do not exist yet. Add a test-only anonymous endpoint, or defer that single assertion to
[Pacman Controller](pacman-controller.md#4-pacmancontroller--minor) — the implementing issue picks,
but it must not go untested in both.

*Depends on:* 2a, 3, 7.

**Why:**

* [a policy scheme selects the handler](#why-a-policy-scheme-selects-the-handler)
* [a present-but-invalid credential must not fall through](#why-a-present-but-invalid-credential-must-not-fall-through)
* [`Basic` gets no Swagger security definition](#why-basic-gets-no-swagger-security-definition)
* [the username carries the identifier rather than a placeholder](#why-the-username-carries-the-identifier-rather-than-a-placeholder)

### 5. `AccessTokensController` — `MINOR`

The three routes and the `ControllerConstants` template, over the `IAccessTokenService`, wire
models, filter and sort field that [issue 2b](#2b-iaccesstokenservice--minor) adds. The controller
maps the service's results onto the statuses in [Routes](#routes); it adds no rules of its own.

*Acceptance:* E2E tests: creating a token returns the secret exactly once, and listing tokens never
returns it, asserted against the raw response body; the listing shows the same `username` the create
returned; the returned username and secret then authenticate a request; deleting it makes it stop
authenticating; a second token with the same name is a `409`, and so is one whose name differs only in case; a past `expiresAt` is a `400` and an
omitted one produces a token with no expiry; another user's token is a `404` to delete; the listing
pages, filters and sorts, and never shows another user's token; every route is a `401`
unauthenticated; and a Basic-authenticated caller cannot mint a token.

*Depends on:* 2b, 4, and [`ItemExistsException` → `409`](authorization-plan.md#itemexistsexception--409--patch).

**Why:**

* [two token models rather than a nullable secret](#why-two-token-models-rather-than-a-nullable-secret)
* [the token listing is paged](#why-the-token-listing-is-paged)
* [there is no maximum lifetime](#why-there-is-no-maximum-lifetime)
* [the service owns its models, filter and sort field](#why-the-service-owns-its-models-filter-and-sort-field)

### 6. Scopes on a `Bearer` token — `MINOR`

The realm half of [the `scope` claim](#the-scope-claim), and **the prerequisite for issue 3 rather
than a follow-up to it**: every value the grammar allows as a client scope in
`keycloak/localdev.json`, as [described above](#scopes-on-a-bearer-token) —
`pacman-manager:*:*` a default client scope on `pacman-manager-web`, `pacman-manager-swagger` and the
realm's default defaults, so the interactive clients keep working once enforcement is on; every
narrower value a default optional one; and a `pacman-manager-scoped` client that can hold a narrow
token — plus the [documentation of the grammar](#configuring-another-identity-provider) for anyone
configuring a different provider.

Keycloak accepts `:` and `*` in a client scope name, so each value is a client scope of the same
name. This does change the client registrations; see
[`pacman-manager:*:*` is its own client scope](#why-pacman-manager-is-its-own-client-scope).

`ConfigureSwaggerGenOptions` lists the scopes its security requirement asks for, so the new values
are added there too.

No application code beyond naming the values: the parser from issue 3 is the only thing that reads
these.

*Acceptance:* a token from the updated realm carries the expected values, asserted by decoding it in
a test rather than by inspection: `pacman-manager:*:*` from the Swagger client without asking for it,
none of ours from `pacman-manager-scoped` unless asked, and exactly the values asked for when it
does. The E2E tests of what a narrow token may do were listed here originally. They belong to
[issue 3](#3-scoped-actors--minor), which is where enforcement turns on, and are listed there in
the vocabulary [issue 8](#8-each-entity-has-its-own-actions--minor) introduces.

*Depends on:* nothing in this document, but it touches `keycloak/localdev.json` and therefore the
`auth` service in `compose.yaml` rather than only C#.

**Why:**

* [every existing token becomes powerless](#every-existing-token-becomes-powerless)
* [the audience prefix is on every value](#why-the-audience-prefix-is-on-every-value)
* [`pacman-manager:*:*` is its own client scope](#why-pacman-manager-is-its-own-client-scope)

### 7. Trusted forwarded-header sources — `MINOR`

The `ForwardedHeaders:TrustedSources` configuration section, its options type and validation, the
empty entry in `appsettings.json`, and `UseForwardedHeaders` in `Program.cs` built from it — exactly
as [Trusted forwarded-header sources](#trusted-forwarded-header-sources) specifies.

*Constraints:* an empty or absent section must leave ASP.NET Core's loopback defaults in place, never
clear them. Clearing `KnownProxies` and `KnownIPNetworks` is the easy mistake, and it lets any caller
choose the address that is logged.

*Acceptance:* options unit tests that a single IPv4 address, a single IPv6 address, an IPv4 CIDR and
an IPv6 CIDR each land in the right list, and that an unparseable entry fails validation naming it.
Integration tests over the pipeline: with a trusted source configured, a request from it carrying
`X-Forwarded-For` reports the forwarded address as `RemoteIpAddress`; the same header from an
untrusted address is ignored; and with the section empty, a forwarded header from a non-loopback
address is ignored.

*Depends on:* nothing. Must land before issue 4, so that no token is usable while its use log records
the proxy's address.

**Why:**

* [trusted proxies are configuration](#why-trusted-proxies-are-configuration)

### 8. Each entity has its own actions — `MINOR`

The vocabulary [tabulated under the `scope` claim](#the-scope-claim), replacing the one issue 6
registered:

* `ScopeValues.Entities` maps each entity to its actions, and `ScopeValues.Actions` is their union:
  `read`, `create`, `update`, `delete`. `ScopeValues.All` becomes the 21 values listed under
  [Configuring another identity provider](#configuring-another-identity-provider), in the same order
  as before: `Everything`, then each entity and action pair, then each `<entity>:*`, then each
  `*:<action>`.
* In `keycloak/localdev.json`, a client scope for each new value, and the client scopes for every
  value that no longer exists removed, together with their entries in the realm's default optional
  scopes and on `pacman-manager-scoped`. `pacman-manager:*:*` and the clients that receive it by
  default are unchanged.
* `ConfigureSwaggerGenOptions` already lists `ScopeValues.All`, so it follows without a change of its
  own.

Nothing enforces a scope yet, so this changes no behaviour of the API.

*Acceptance:* `ScopeValuesTests` asserts the 21 values, including that no pair names an action its
entity lacks. `LocalDevRealmTests` asserts the realm registers every value in `ScopeValues.All`, and
that no client scope whose name starts `pacman-manager:` is outside it, so a stale value from the old
grammar fails the test. The existing `KeycloakScopeTests` pass against the new values.

*Depends on:* nothing.

**Why:**

* [each entity has its own actions](#why-each-entity-has-its-own-actions)
* [delete is its own action](#why-delete-is-its-own-action)

### 9. The actor accessor reads the principal itself — `PATCH`

`HttpContextActorAccessor` replaces `ICurrentUserService`:

* The accessor takes `IHttpContextAccessor` and `PacmanManagerDbContext`, and finds the user from the
  `AppUserIdClaimType` claim itself. The claim lookup, the parse and the warnings when either fails
  move from `CurrentUserService` unchanged. It still caches the actor for the request.
* `UserManagementService` takes `IActorAccessor` in place of `ICurrentUserService`. The `me` methods
  take the user from `Actor.User`, and throw `NoCurrentUserException` when there is none, as they do
  today.
* `ICurrentUserService`, `CurrentUserService` and their registration in `Program.cs` are deleted.
* [`user-management.md`](user-management.md), which says the `me` methods use
  `ICurrentUserService`, is updated to say `IActorAccessor`.

This changes no behaviour. It gives the accessor the principal, which it needs in order to read the
`scope` claim in issue 3.

*Acceptance:* accessor unit tests: a principal carrying a valid user id claim produces an actor for
that user; a missing claim, an unparseable one and an id with no user each produce
`Actor.Anonymous`; and a second call in the same request does not query again. The existing
`UserManagementServiceTests` pass against an `IActorAccessor` in place of the mocked
`ICurrentUserService`, and the existing E2E tests pass unchanged.

*Depends on:* nothing.

**Why:**

* [the actor accessor reads the principal itself](#why-the-actor-accessor-reads-the-principal-itself)

### 10. A verdict per action — `PATCH`

`RepositoryAccessPolicy.CheckWrite` becomes `CheckUpdate` and `CheckDelete`, and
`PackageAccessPolicy` gains a `CheckDelete` beside `CheckPublish`, which from now on answers for
publish and replace only. Each new method has exactly the rules of the method it came from, so this
changes no behaviour. `RepositoryService` and `PackageService` call the verdict that matches the
operation. Where a private helper loads a row for either change, it takes the verdict to apply
rather than choosing one.

[`authorization-plan.md`](authorization-plan.md) and [`packages-api.md`](packages-api.md) name
`CheckWrite`, and say that `CheckPublish` answers for delete. Both are updated to match.

*Acceptance:* `RepositoryAccessPolicyTests` and `PackageAccessPolicyTests` carry every row the
old verdict had, for each of the verdicts that replace it. The existing service and E2E tests pass
unchanged.

*Depends on:* nothing.

**Why:**

* [delete is its own action](#why-delete-is-its-own-action)

### 11. `UserAccessPolicy` — `MINOR`

`UserAccessPolicy`, alongside `RepositoryAccessPolicy` and `PackageAccessPolicy` and shaped like
them: internal, with no database, HTTP or logging dependency, returning the existing
`RepositoryAccess` verdict as `PackageAccessPolicy` already does. Its two verdicts,
`CheckReadCurrent` and `CheckUpdateCurrent`, have the arms the others have: `IsSystem` allowed, no
user `Unauthenticated`, and the scope arm [tabulated above](#how-the-actor-enforces-it). There are
no ownership rules underneath, because `me` is the only subject either verdict answers for.

`UserManagementService` asks `CheckReadCurrent` in `GetCurrentUserAsync` and `CheckUpdateCurrent`
in `UpdateCurrentUserAsync`, before doing anything else. `Unauthenticated` throws
`NoCurrentUserException`, as today, and `Forbidden` throws the `InsufficientScopeException` that
issue 3 adds, which is a `403`.

`ListUsersAsync` and `GetUserByIdAsync` ask nothing. They serve anonymous routes, so a credential
without `users:read` reads them exactly as a stranger does, which is in full.

[`user-management.md`](user-management.md#there-is-no-iuseraccesspolicy) said there would be no
policy for users; it records that this has been reversed, and why.

*Acceptance:* `UserAccessPolicyTests` covers every row of both verdicts as a plain unit test, with no
database involved. Service unit tests that each `me` method is refused on a `Forbidden` verdict and
proceeds otherwise. E2E tests with a `pacman-manager-scoped` token carrying
`pacman-manager:repositories:*`: `GET /api/v1/users/me` and `PATCH /api/v1/users/me` are `403`,
and `GET /api/v1/users` and `GET /api/v1/users/{userId}` are `200`. Another carrying
`pacman-manager:users:read`, for which `GET /api/v1/users/me` succeeds and the `PATCH` is `403`.

*Depends on:* 3, and User Management's
[`PATCH /api/v1/users/me`](user-management.md#6-patch-apiv1usersme--minor) issue, whose method it
guards.

**Why:**

* [users and tokens get access policies](#why-users-and-tokens-get-access-policies)

### 12. `AccessTokenAccessPolicy` — `MINOR`

`AccessTokenAccessPolicy`, shaped like `UserAccessPolicy`, with `CheckRead`, `CheckCreate` and
`CheckDelete`, whose scope arms are [tabulated above](#how-the-actor-enforces-it). There are no
ownership rules underneath: every token method's subject is the actor's own tokens, and another
user's token is a `404` from the query rather than from the policy.

`AccessTokenService` asks the matching verdict at the top of the listing, minting and deleting, with
the same mapping as issue 11. Every token route hangs off `/users/me` and there is no anonymous view
of tokens, so a read without `tokens:read` is refused rather than shown less.

*Acceptance:* `AccessTokenAccessPolicyTests` covers every row as a plain unit test. Service unit
tests that each method is refused on a `Forbidden` verdict and proceeds otherwise. E2E tests with a
`pacman-manager-scoped` token carrying `pacman-manager:tokens:read`, which lists tokens and is a
`403` minting or deleting one, and one carrying `pacman-manager:repositories:*`, for which every
token route is a `403`.

*Depends on:* 3, 5.

**Why:**

* [users and tokens get access policies](#why-users-and-tokens-get-access-policies)

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
* **Publishing to a repository somebody else owns.** `pacman-manager:packages:create` is a ceiling,
  and underneath it only a repository's owner may publish to it. Letting other users publish is a
  change to the ownership rules in [`authorization-plan.md`](authorization-plan.md), not to the
  scope grammar, and it is left for later.
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
`users:read` or `tokens:delete` are exactly the names another application would also want, and `aud`
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
day this lands **every one of them can do nothing a stranger could not**, the web client and the
Swagger UI included, because [a credential with none of our values keeps its user but none of
their permissions](#why-a-credential-with-none-of-our-values-keeps-its-user).
There is no version of additive scopes where that is not true; the only question is whether it is
planned for.

So the realm change is not the last step of this work, it is a **prerequisite**. The existing
clients keep receiving what they receive today plus `pacman-manager:*:*`, which is a new *default*
client scope on `pacman-manager-web` and `pacman-manager-swagger` alongside the `pacman-manager` one
they already had. That is a change to their registrations; the plan originally said there would be
none, and [`pacman-manager:*:*` is its own client scope](#why-pacman-manager-is-its-own-client-scope)
records why there has to be.
[Issue 6](#6-scopes-on-a-bearer-token--minor) therefore lands **before**
[issue 3](#3-scoped-actors--minor) turns enforcement on, and the two are expected to ship together.

A deployment against a provider that cannot be taught to emit these scopes does not have a
compatibility story under this rule. That is the cost of the conservative direction, and it is
recorded here rather than left for somebody to find.

### Why `pacman-manager:*:*` is its own client scope

The plan originally had the broad values "ride on" the existing `pacman-manager` client scope, so
that no client registration would change, with a protocol mapper as the fallback should Keycloak
refuse `:` in a scope name. Neither works, and the reason is how Keycloak builds the claim:

* **`scope` is the list of names of the client scopes Keycloak applied** — the ones the client has
  as defaults plus the optional ones requested — for each scope marked `include.in.token.scope`. A
  client scope cannot contain or imply another, so the `pacman-manager` scope can only ever
  contribute the value `pacman-manager`.
* **Keycloak ignores a protocol mapper that writes `scope`.** Tried against Keycloak 26.6 with an
  `oidc-hardcoded-claim-mapper` for claim `scope` on the `pacman-manager` client scope: the token's
  `scope` was unchanged. The standard claim wins over any mapper.

The naming worry, on the other hand, was unfounded: Keycloak accepts `:` and `*` in a client scope
name, and emits it verbatim. So each value is a client scope of the same name, and a client that
should hold `pacman-manager:*:*` has to have that client scope — a registration change on
`pacman-manager-web` and `pacman-manager-swagger`, signed off by the project owner.

Keeping `pacman-manager` audience-only is what makes a narrow token possible at all. Values are
`OR`'d, so a client that receives `pacman-manager:*:*` by default can never hold less; were the broad
value on `pacman-manager` itself, a narrow client would also need a second audience mapper. That is
why the realm carries a separate `pacman-manager-scoped` client for narrow tokens rather than
narrowing the interactive ones.

Every value the grammar allows is registered, not only the ones in use: scope values have to be
[registered ahead of time](#why-instances-are-not-in-here), and a grammar whose values exist only
partly in the realm would be a second vocabulary to keep in step.

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
best argument for that grammar. A scope can say "may not read repositories", but it cannot say "may
read only *these* repositories", so there is no predicate to intersect with what the user may see.

The distinction is between a credential being **refused** an operation and a credential being shown
a **smaller world** of its own. Getting that wrong would silently hide a user's own private
repositories from their own `pacman` client. So a credential that may read an entity at all sees
exactly what its user sees.

*Amended.* This entry originally went on to say that "may not read repositories" would be answered
by a `CheckRead` with a `403`, and that `VisibleTo` would not change at all. There is no `CheckRead`:
[a credential that cannot read reads as anonymous](#why-a-credential-that-cannot-read-reads-as-anonymous),
which is a smaller world, but the one every stranger is shown rather than one built for the
credential. `VisibleTo` gains that one arm and nothing else.

### Why each entity has its own actions

*Added during implementation of issue 3, with the project owner's sign-off.* The grammar first
crossed five actions (`read`, `create`, `write`, `delete`, `publish`) with every entity. That
admitted twenty pairs, many of them meaningless: nothing creates or deletes a user, nothing updates
a token, and `publish` and `write` overlapped on packages. A value that names an operation nothing
performs is a value a provider can hand out, a reader has to wonder about, and a test has to cover.

So each entity lists the actions that exist for it, and nothing else parses:

* **Repositories** are read, created, updated and deleted.
* **Packages** are read, created and deleted. Publishing a new version replaces the old one in the
  same operation, because [pacman rolls forward](packages-api.md), so there is no separate update.
* **Users** are read and updated. They are created on first sign-in by `ClaimsTransformer`, which
  runs before any actor exists, and they are never deleted.
* **Tokens** are read, created and deleted. A token is never changed after it is minted.

`write` is gone in favour of `update`, which says what it covers; `publish` is gone in favour of
`packages:create`, which is what a publish is.

Every action on an entity needs `read` on it too, and a package needs `read` on its repository,
because none of the other actions makes sense on something the credential cannot see. The rule is
not a separate check: the change verdicts demand the read scope alongside the action, and the lookup
every change starts from shows an unreadable entity's private rows to nobody.

### Why delete is its own action

*Added during implementation of issue 3, with the project owner's sign-off.* `CheckWrite` answered
for both updating and deleting a repository, and `CheckPublish` for publishing, replacing and
deleting a package, because under the ownership rules they are the same question. Under scopes they
are not. A credential that may rename a repository should not have to be able to delete it, and a
build pipeline that publishes should not have to be able to delete packages. So deleting a
repository needs `repositories:delete`, not `repositories:update`, and deleting a package needs
`packages:delete`.

That needs a verdict per action. Adding an action parameter to one verdict was the alternative. It
was not taken, because a verdict per action keeps each one a plain method whose rows the test
fixtures list, and because the split can land first on its own, as
[issue 10](#10-a-verdict-per-action--patch), with no change in behaviour.

### Why a credential that cannot read reads as anonymous

*Added during implementation of issue 3, with the project owner's sign-off.* The plan originally
refused a read without the read scope with a `403`, through a `CheckRead` verdict alongside the
others. No such verdict existed. Every read goes through `VisibleTo`, so adding one would have meant
a call at every read site, and it would have left open whether a listing is a `403` or an empty page.
It would also have broken [a credential never gets less than an anonymous caller
would](#how-the-actor-enforces-it): a caller refused `GET /api/v1/repositories/{id}` on a public
repository could get it by deleting their `Authorization` header.

Reading as anonymous settles all three. There is no read verdict to call, so no read site can forget
one. A listing is the public page. A credential can never see less than a stranger, and it sees more
only when it holds the read scope.

It is also what makes "every action needs `read`" enforce itself: the lookup every change starts from
cannot find the actor's own private repository, so the change is a `404`.

The same holds for `/users` and `/users/{userId}`, which are anonymous routes and so need no scope.
`/users/me` and the token routes have no anonymous view, so a read of them without the scope is
refused instead; see [users and tokens get access policies](#why-users-and-tokens-get-access-policies).

### Why a credential with none of our values keeps its user

*Added during implementation of issue 3, with the project owner's sign-off.* The plan originally said
an actor carrying an empty scope "is refused every operation". That conflicted with the rule that a
credential never gets less than an anonymous caller would: a token carrying only `openid profile
email` could not read a public repository that a caller with no header could.

The fix is not a special case. An empty scope permits nothing, so the verdicts refuse every change
and a missing read scope [reads as anonymous](#why-a-credential-that-cannot-read-reads-as-anonymous),
which together are exactly what a stranger may do. A token like that is an edge case anyway: a client
configured for this API has no reason not to ask for at least `pacman-manager:repositories:read`,
and a token without the `pacman-manager` audience is rejected before its scope is read.

*Rejected:* having `HttpContextActorAccessor` return `Actor.Anonymous` for an empty scope. That is
equivalent in what the caller may do, but it throws away *who* the caller is. Anything that refers to
the user, such as the logs today and rate limiting later, must still recognise them when they have
been granted nothing more. So the actor keeps its user, and only the permissions are absent. That
also makes a refused change a `403` rather than a `401`, since the caller is known.

### Why users and tokens get access policies

*Added during implementation of issue 3, with the project owner's sign-off.* Repositories and packages
each have an access policy, and their scope arms go there. Users and tokens had none, and
[`user-management.md`](user-management.md#there-is-no-iuseraccesspolicy) declined one deliberately,
on the grounds that "yourself only" left a policy nothing to decide.

The scope check is something to decide, and the policy is where it belongs. More will follow:
administrative access to users is expected later, and it needs a place for its check that is not the
top of a service method. So both entities get a policy of the same shape as the other two, and
every rule in it is a plain unit test.

*Rejected:* checking the scope at the top of each `UserManagementService` and `AccessTokenService`
method. It would have worked, but it would have put the next rule for users in the service too, and
left two entities whose rules are found somewhere other than a policy.

They are separate issues from issue 3, [11](#11-useraccesspolicy--minor) and
[12](#12-accesstokenaccesspolicy--minor), because the services they change are still being built,
and folding them in would hold enforcement on repositories behind work on users.

`/users` and `/users/{userId}` are anonymous routes, so reading them needs no scope. `/users/me` and
every token route have no anonymous view, so a read of them without its scope is a `403` rather than
a smaller result.

### Why the actor accessor reads the principal itself

*Added during implementation of issue 3, with the project owner's sign-off.* `HttpContextActorAccessor`
built the actor from `ICurrentUserService`, which returns a `User` and nothing else. The accessor
needs the `scope` claim too, and nothing else needs `ICurrentUserService`: the one other consumer,
`UserManagementService`, wants the caller, which is what an actor is. Passing the claim through
`ICurrentUserService` would have left two services describing the current caller, one of them
knowing only half. So the accessor reads the principal itself, and `ICurrentUserService` is removed,
as [issue 9](#9-the-actor-accessor-reads-the-principal-itself--patch).

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

Salting would also cost something structural, which is the stronger argument and the one most likely
to be overlooked. A per-row salt lives on the row, so the salt has to be read before the hash can be
computed, and the lookup stops being
[one query predicated on both halves](#why-verification-is-a-single-query) and becomes fetch-by-id
followed by an in-memory comparison. That is not an extra round trip — it is still one primary-key
lookup — but it gives up the property that an unknown identifier and a wrong secret are the same
outcome reached by the same path, handing back the token-id enumeration oracle that shape closes for
free. The compute is not the reason: a 32-byte secret and a 48-byte salted one both fit in a single
SHA-256 block, so the two cost the same tens of nanoseconds against a database round trip three
orders of magnitude larger.

If a stronger scheme is ever genuinely needed, **it is its own piece of work, and it will almost
certainly invalidate every existing token.** Only the hash is stored, so there is nothing to
re-derive a new one from: every user would have to mint replacements and edit every `pacman.conf`
holding one. **This is an accepted risk.** It is recorded here so that the migration is understood as
part of the price of changing the scheme, rather than discovered halfway through doing it.

A **global pepper** — an application-held secret mixed in as `SHA-256(pepper ‖ secret)` — is the one
variation that would not cost the query shape, since it is not per-row and is therefore still
computable before the lookup. It buys little against a 256-bit random value, but it is the option to
reach for first if a database-only compromise ever becomes the threat worth answering.

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

Logging an address nobody can rely on would be worse than logging none, which is why
[trusted forwarded-header sources](#7-trusted-forwarded-header-sources--minor) land before the
handler that makes a token usable, rather than as a follow-up.

### Why two services, not one

The work divides cleanly along whether an `Actor` exists yet, and the two halves become two services
because of it. Verification runs *before* an actor exists; `IAccessTokenService` is actor-scoped like
every other service.

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

### Why the service owns its models, filter and sort field

*Amended during implementation, with project-owner sign-off.* The plan as first accepted put the
three wire models, `AccessTokenFilter`, `AccessTokenSortField` and the duplicate-name
`ItemExistsException` in [issue 5](#5-accesstokenscontroller--minor), the controller. But issue 5
depends on [issue 2b](#2b-iaccesstokenservice--minor), and 2b's service is written in those types: its
listing takes the filter and the sort field and returns `AccessToken`, and minting returns
`CreatedAccessToken`. A service cannot compile against types scheduled for an issue that depends on
it, and the duplicate name can only be detected where the row is saved, which is the service's
minting method, not the controller.

They therefore land with the service in 2b. This is also how the rest of the API is arranged:
`RepositoryService` owns `Repository`, `RepositoryFilter` and `RepositorySortField`, and
`RepositoriesController` only routes to it. Issue 5 keeps the routes, the route template and the E2E
tests.

### Why verification is on `IUserService`

An earlier draft gave verification a service of its own, `IBasicAuthenticationService`, which turned a
Basic credential into a `ClaimsPrincipal`. It was dropped in favour of one method on `IUserService`.

`IUserService` is already the non-actor-aware service that answers "which user is this?" during
authentication: `ClaimsTransformer` calls `GetUserByExternalIdAsync` with an identity provider's
subject, before any actor exists. An access token is the same question asked with a different
credential, so `GetUserByAccessTokenAsync` sits beside it and inherits the same rule about what it may
assume. A second pre-auth service would have been a second place for that rule to be forgotten, and a
type whose whole surface was one method.

Returning a `User` rather than a `ClaimsPrincipal` keeps the split that already exists: the service
finds the user, and the authentication layer decides how a user is represented to the pipeline.
`ClaimsTransformer` already works that way, and the handler now does too.

This does not contradict [token management not being on `IUserService`](#why-token-management-is-not-on-iuserservice).
Verification is pre-auth and belongs with the other pre-auth lookups; minting, listing and deleting
are actor-scoped and do not.

### Why a token name has a normalized copy

Token names are unique per user, and a user who has `laptop` and then asks for `Laptop` almost
certainly means the same machine. Case-insensitive uniqueness is the rule; the question was how to
express it.

* **A case-insensitive index on `Name` itself** — `lower("Name")` as an expression index, or a
  `citext` column — cannot be declared with data annotations, and this solution configures its model
  with data annotations only; there is no `OnModelCreating` to put it in. `citext` is also
  Npgsql-specific, and `Microsoft.EntityFrameworkCore.InMemory` would not honour it, so the service
  tests would pass against a rule the database does not share.
* **Case-sensitive uniqueness** leaves two near-identical rows in a listing and no way to tell which is
  on which machine.

A stored `NormalizedName` is an ordinary column with an ordinary `[Index]`, behaves identically under
both providers, and serves `nameContains` too, so the listing's case-insensitive match needs no
lowering in the query. `Name` keeps what the user typed, which is what they want to see. The invariant
culture is required because the current culture's lowercasing differs between hosts — the Turkish
dotted and dotless `i` being the standard example — and a normalization that depends on the host is
not one.

### Why trusted proxies are configuration

`RemoteIpAddress` is only meaningful behind a reverse proxy if the proxy's forwarded headers are
believed, and only safe if nobody else's are. Three alternatives lost:

* **Reading `X-Forwarded-For` directly in the log line** would record whatever a caller cares to send,
  and would fix only this one log rather than every consumer of the client address.
* **Hardcoding the compose network's subnet** fits one deployment and is wrong for every other, and
  the compose subnet is not stable anyway.
* **Clearing `KnownProxies` and `KnownIPNetworks`** so that every source is trusted is the common
  shortcut, and it lets any client forge the address.

A configuration section of addresses and subnets is what every deployment can set correctly, and
accepting both forms in one list means an operator writes what they know — a fixed proxy address, or
the network a container proxy lives on — without learning which of two ASP.NET Core lists it belongs
in. Failing startup on an unparseable entry follows from the same concern: a trusted proxy silently
dropped reverts the log to the proxy's own address with nothing to say so.

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
