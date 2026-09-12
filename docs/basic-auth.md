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

That last requirement turns out to be a special case of a general one — a credential carrying a
statement of what it may be used for — so this document specifies that too, as ordinary OAuth 2.0
scopes, and how a `Bearer` token carries them. That half is not needed by `pacman`, and is
deliberately last in the plan; it is here because it is the same mechanism, and describing it
anywhere else would mean two statements of one rule.

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
  instance. This is a property of where they live rather than a shortcut; see
  [Why instances are not in here](#why-instances-are-not-in-here).
* **Tokens that can write.** Not a limitation to be lifted later by relaxing this design — a
  writing credential is a different thing, with a different threat model, and would need its own.
* **Basic auth as a general alternative to `Bearer`.** `Bearer` remains what the management API is
  driven with; `Basic` exists for clients that cannot speak OAuth.
* **An audit trail.** Revocation is a delete, and nothing records where a token was used from.
* **Rate limiting.** Nothing bounds how much one token can consume.

---

## Authority is a property of the actor, not of the route

The requirement is that Basic auth can never write. The obvious implementation is to accept the
`Basic` scheme only on the [pacman routes](pacman-controller.md), which are all reads. That is true
today and stops being true the first time somebody adds a write route and reaches for the scheme
list without thinking about it.

So the restriction goes where this codebase already puts its authorization invariants — into the
`Actor`, which is
[the single thing every service authorizes against](authorization-plan.md#the-pieces). The
credential decides what its bearer may do; the route never does, and no service ever asks which
scheme a request arrived under.

### The `scope` claim

Both schemes produce a principal carrying the same two things: the existing
`AuthnConstants.AppUserIdClaimType` claim, which says **who**, and the standard OAuth 2.0 **`scope`**
claim, which says **what this credential may be used for**.

`scope` is where the ecosystem already puts this, so that is where it goes. Per
[RFC 6749 §3.3](https://www.rfc-editor.org/rfc/rfc6749#section-3.3) it is one space-delimited,
case-sensitive list of opaque strings, and every value of ours has this shape:

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

**The audience prefix is not decoration.** `pacman-manager` is this API's audience — `appsettings.json`
sets it and an `oidc-audience-mapper` on the realm's `pacman-manager` client scope emits it — so a
scope value names the API it belongs to and then the permission within it. That is the same shape
Azure uses (`api://<id>/Files.Read`) and, in spirit, Google's scope URIs. It matters because `scope`
is a namespace shared by every client in a realm: unprefixed values like `users:read` or
`tokens:write` are exactly the names another application would also want, and `aud` and `scope`
should not be able to disagree about which API is under discussion.

The entity is the **plural** resource name, so a scope value reads as the route it governs:
`pacman-manager:repositories:read` is `GET /api/v1/repositories`, and there is no second vocabulary
to learn or keep in step.

Three rules make the claim safe to reason about, and all three are requirements rather than
observations:

* **Scopes are purely additive: absent means denied.** A credential may do what its scopes name and
  nothing else, so a token carrying none of our values can do nothing at all. This is the ordinary
  reading of `scope` and the conservative direction — a permission that has to be granted explicitly
  cannot be granted by an oversight, and a bug in the parser fails closed. It has one consequence
  that has to be planned for rather than discovered; see
  [Every existing token becomes powerless](#every-existing-token-becomes-powerless).
* **A scope never grants, it only narrows.** What it names is a *ceiling*, not a permission: every
  rule in [`authorization-plan.md`](authorization-plan.md#authorization-rules) still runs afterwards,
  unchanged, so `pacman-manager:repositories:write` on a repository somebody else owns is still a
  refusal. An identity provider can hand out any scope it likes; it cannot hand out access. This is
  the same intersection rule AWS session policies and GitHub App installation tokens use, and it is
  what makes trusting a provider's `scope` claim safe at all.
* **A value that is not ours is ignored**, never fatal and never permissive. `scope` legitimately
  carries `openid`, `profile`, `email`, `roles` and the bare `pacman-manager` audience value, and
  none of those grant anything here. A malformed value of ours is dropped the same way and logged,
  so a provider that mangles one cannot escalate through it — it can only take a permission away.

### Every existing token becomes powerless

Deny-by-default is the safer rule and it is not free. Every `Bearer` token the realm issues today
carries `openid profile email roles pacman-manager` and nothing that matches the grammar above, so
on the day this lands **every one of them can do nothing** — the web client and the Swagger UI
included. There is no version of additive scopes where that is not true; the only question is
whether it is planned for.

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

### `Basic` produces a read-only scope

A `PacmanAccessToken` authenticates its owner and is issued exactly one scope value,
`pacman-manager:*:read`. That is the whole mechanism by which "a token cannot write" is true: not a
check on the scheme, not a check on the route, but a credential that arrives already unable to name
a write.

The handler mints this itself rather than reading it from anywhere. Nothing in the Basic path talks
to the identity provider, so the scope is ours to assert, and asserting exactly one value is what
makes the guarantee checkable in one place.

### Scopes on a `Bearer` token

The same values, issued by the identity provider rather than by us, let a caller hold a token that
can do less than they can — the OAuth client on a build machine that may publish packages and read
repositories, and nothing else. Nothing in this application mints that token and nothing validates
the scopes beyond parsing them: they are the provider's statement about its own token, and the three
rules above bound what such a statement can mean.

In Keycloak each value is a client scope. The broad ones ride on the existing `pacman-manager`
client scope, which is **default** on the interactive clients so they keep working; a narrow
credential is a client configured with a narrower set instead. Either way this half needs no
application code beyond the parser, which is the property worth demonstrating.

### How the actor enforces it

`Actor` carries the parsed scope, and read-only becomes a question asked of it rather than a
separate flag:

```csharp
public ActorScope Scope { get; }

public bool IsReadOnly => !Scope.PermitsAnyWrite;

public static Actor For(User user, ActorScope scope) => new(user, isSystem: false, scope);
```

**Every verdict method asks the scope first**, before the ownership tests, and returns
`RepositoryAccess.Forbidden` when it does not permit that operation on that entity — `CheckRead` as
much as `CheckWrite`, `CheckCreate` and `PackageAccessPolicy.CheckPublish`. Reads are in scope
because absence now denies: a credential without `repositories:read` may not read repositories, and
that has to be enforced somewhere rather than assumed. A credential therefore cannot exceed its
scope through any route, present or future, including one that forgets to think about schemes at all
— which is the same structural argument that put enforcement in the service layer instead of an
action filter in the first place.

Two exemptions, both by construction rather than by rule:

* **An actor with no credential behind it is unrestricted.** `Actor.System` and `FixedActorAccessor`
  take `ActorScope.Unrestricted` explicitly, so background jobs and command-line tools are
  unaffected. Deny-by-default is a statement about what a *token* carries, and they carry none.
* **A credential never gets less than an anonymous caller would.** An `[AllowAnonymous]` route that
  consults no actor — the public repository listing, the pacman routes for a public repository —
  serves a narrowly scoped credential exactly as it serves a stranger. Anything else would mean a
  user could see more by deleting their `Authorization` header, which is absurd on its face and
  would teach people to do it.

Two details the parser has to get right, both already half-solved by the existing code. `scope`
arrives as **one space-delimited string**, not as repeated claims, so it is split before it is
parsed. And `Program.cs` already calls `JsonWebTokenHandler.DefaultInboundClaimTypeMap.Clear()`, so
the claim reaches the principal as `scope` rather than remapped to a WS-Federation-era URI — a trap
that is closed, and worth leaving closed.

`Forbidden` (`403`) rather than `Unauthenticated` (`401`) is the right verdict, including on a
private repository the actor owns: the caller is authenticated, re-authenticating will not help, and
they already know the repository exists. Nothing leaks.

### `VisibleTo` does not change at all

This falls out of [the grammar naming no instances](#why-instances-are-not-in-here), and it is the
best argument for that grammar. A scope can say "may not read repositories", which `CheckRead`
answers with a `403`, but it cannot say "may read only *these* repositories" — so there is no
predicate to intersect, and `VisibleTo` and `RepositoryService.VisibleAsync` are untouched.

The distinction is between a credential being **refused** an operation and a credential being shown
a **smaller world**. This design only ever does the first. A credential that may read repositories at
all sees exactly what its user sees, their own private repositories included: **a scope restricts
what may be done, not what may be known.** Getting that wrong would silently hide a user's own
private repositories from their own `pacman` client, and the surest way to avoid it is to have no
code that could cause it.

### Why not a claim read by each service, or a scheme check, or a filter

Three alternatives were considered.

A **claim read at each call site** — every service pulling `scope` off the principal and
interpreting it — puts the rule in every service instead of in one place, which is what
`RepositoryAccessPolicy` exists to prevent. The claim is the transport; the `Actor` is the one place
that understands it, parsed once at the edge. A **scheme check** (`if (scheme == "Basic")`) is the
route-shaped version of the same mistake and stops being true the first time a second read-only
credential exists. An **authorization filter** on the write routes only protects HTTP callers, which
is the reason
[`authorization-plan.md`](authorization-plan.md#architecture-strategy-enforcement-in-the-service-layer)
rejected a filter for the repository rules. Checking the scope in the **`IActorAccessor`** cannot
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

### Every use is logged

`LastUsedAt` answers "is this token still in service". It cannot answer "where was it used from",
and that is the question somebody asks when a token may have leaked. A full audit trail is
[deferred](#deferred-work); **a log line is not**, and it costs nothing to write now.

Every successful verification logs, at minimum:

* **The token id** — the `Guid` half of the presented credential. It is
  [not secret](#format), which is what makes it safe to log and useful to correlate.
* **The client IP address** the request arrived from.

The timestamp comes free from the logging configuration, and the owning user is derivable from the
token id, so those are not repeated into the message. Failures are logged the same way, with the
token id when the string parsed far enough to yield one.

**The secret is never logged, in any form** — not the token string, not the header, not a prefix of
it. That constraint already exists for [the `Authorization` header](#transport); this section is the
place it would most plausibly be broken, because a log line about a credential is exactly where
somebody reaches for the credential.

One deployment detail the implementing issue has to handle rather than assume: `Program.cs` does not
configure forwarded headers, so behind the reverse proxy that
[terminates TLS](#transport) `RemoteIpAddress` is the proxy's address and the log is worthless for
this purpose. Either `UseForwardedHeaders` is configured — with a known-proxy list, since an
unrestricted one lets a caller forge the value — or the header is read explicitly and logged as what
it is. Logging an address nobody can rely on would be worse than logging none.

### Two services own this, not one

The work divides cleanly along whether an `Actor` exists yet, and the two halves become two services
because of it.

**`IBasicAuthenticationService` is not actor-scoped** and has one job: turn a set of Basic
credentials into a `ClaimsPrincipal`, or into nothing. Parsing the token, the primary-key lookup, the
expiry check, the `FixedTimeEquals` hash comparison, the claims it issues, the coarse `LastUsedAt`
write and [the use log](#every-use-is-logged) all live here. It cannot take `IActorAccessor`, and the
reason is structural rather than awkward: the accessor depends on `ICurrentUserService`, which
depends on the authenticated principal, which is the thing this service produces. It runs *before* an
actor exists.

**`IAccessTokenService` is actor-scoped** like every other service, and manages tokens on behalf of
the caller: minting (format, `RandomNumberGenerator`, SHA-256, the once-only return), the
[listing](#the-listing-follows-the-standard-shape), and deletion. Every method takes the actor's own
tokens as its subject, so a caller can never name another user's.

The split is the point. A single service holding both would have a method that deliberately bypasses
the actor sitting next to methods that depend on it, which is exactly the shape somebody later copies
by accident; two types, with two jobs and two rules about what they may assume, cannot be confused
for one another.

**Why not `IUserService`.** Token management is arguably user management, and `IUserService` already
exists — but it is not actor-scoped and cannot become so, because `ClaimsTransformer` calls it during
authentication, before there is an actor to scope to. Putting actor-scoped methods on it would
recreate the mixed-authority problem this split exists to avoid. `IAccessTokenService` is therefore
its own service, injected into `AccessTokensController` alongside nothing else, and `IUserService` is
left exactly as it is.

**Which service owns the `DbSet`.** `PacmanAccessTokens` is touched by both — the authentication
service for the pre-auth lookup by id, the token service for everything a user does to their own
tokens — and the enforcement test names both and asserts that nothing else does. That is a deliberate
departure from the one-method rule
[`authorization-plan.md`](authorization-plan.md#why-this-is-hard-to-get-wrong) holds for
`PacmanRepositories`, and it is safe for a reason worth writing down: the pre-auth query has no
visibility rule to duplicate. It is a lookup by primary key whose result authenticates somebody
rather than being shown to them.

---

## Scheme selection, and the handler

### The `Authorization` prefix picks the handler

Registering a second scheme is not enough on its own, and this is the part of the design most likely
to be got wrong. `Program.cs` registers
`AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer()`, and the authentication
middleware runs **only the default scheme**. Nor can the pacman routes opt in with
`[Authorize(AuthenticationSchemes = "Basic")]`: those routes are `[AllowAnonymous]`, which is
precisely the metadata that suppresses that attribute. A `Basic` header would reach the `Bearer`
handler, which returns `NoResult`, the request would proceed as anonymous, and the Basic handler
would never run at all.

So the default becomes a **policy scheme that forwards on the header prefix**, which is the one
mechanism that runs before any endpoint metadata is consulted:

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
Nothing about the existing management API changes, and no route names a scheme.

### The handler

An `AuthenticationHandler<BasicAuthenticationSchemeOptions>` whose whole body delegates to
[`IBasicAuthenticationService`](#2a-ibasicauthenticationservice--minor): it
decodes `Authorization: Basic base64(username:token)`, and the service parses the token, loads the
row by id, checks expiry and compares the hash. The principal that comes back carries the existing
`AuthnConstants.AppUserIdClaimType` claim plus the
[`pacman-manager:*:read` scope](#basic-produces-a-read-only-scope).

Reusing `app_user_id` is what makes the rest of the pipeline unchanged: `CurrentUserService` already
resolves that claim to a `User`, and `HttpContextActorAccessor` already turns a `User` into an
`Actor`. The only change there is that the accessor reads the `scope` claim off the principal —
without caring which scheme produced it — and builds the actor with the resulting
[scope](#the-scope-claim).

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
allows.** Only the *absence* of credentials falls through to anonymous.

[The forwarding scheme](#the-authorization-prefix-picks-the-handler) gets the handler invoked, which
is half of it. The other half is that a failed authentication result on an `[AllowAnonymous]`
endpoint is discarded before anything challenges, so the handler's own `HandleChallengeAsync` is
never reached. A short piece of middleware immediately after `UseAuthentication()` closes it:

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

It is deliberately incurious: it asks only whether a Basic header was offered and not accepted, so
it cannot develop an opinion about which routes take the scheme, and it sits before authorization so
an `[AllowAnonymous]` endpoint cannot swallow it. The implementing issue owns the test that pins it,
because nothing else in the pipeline will catch a regression here.

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

These routes require **`Bearer`**, which follows from the header prefix alone: a request carrying a
`Basic` credential is authenticated by that scheme, and the `pacman-manager:*:read` scope it arrives
with cannot name a write. A token therefore cannot be used to mint another token without any rule
saying so — minting is a write.

`me` is the only subject, for the reason
[User Management](user-management.md#me-is-the-only-way-to-reach-the-write-route) gives — a route
incapable of naming another user has no authorization check to forget. Note that this document does
not otherwise depend on that one; the reasoning is shared, the code is not.

### The listing follows the standard shape

`GET /api/v1/users/me/tokens` takes `PaginationParams`, an `AccessTokenFilter` and
`SortOptions<AccessTokenSortField>`, exactly as [every other listing](packages-api.md#listing) does.

| Parameter | Applied by | Meaning |
| :--- | :--- | :--- |
| `nameContains` | `StringContainsQuery` | Substring of the token's name, matched case-insensitively by lowering both sides as the package search does. |

There is no filter on expiry. Sorting by `ExpiresAt` puts the tokens nearest to lapsing together,
which is the question a user actually asks, and a filter for it would be a second way to ask the same
thing over a handful of rows.

`AccessTokenSortField`: `CreatedAt`, `Name`, `ExpiresAt`, `LastUsedAt`. `CreatedAt` is first and
therefore the default, carrying `[DefaultSortDirection(SortDirection.Descending)]` so that the token
a user just minted is at the top of the page they land on.

The filter is ANDed onto a query already restricted to the calling user's own tokens, which is the
[same relationship](authorization-plan.md#authorization-rules) every other filter has to visibility:
it can only ever remove rows.

One user's token count is bounded by their patience, so paging here is arguably overkill. It is the
shape anyway, for two reasons: a listing that ships without pagination cannot grow it later without
breaking every caller, and a reader of this API should not have to learn which listings are special.

`409` on `POST` is a duplicate token name for that user, raised as `ItemExistsException` and mapped
by a new arm on `AuthorizationExceptionHandler`, without which it would fall through to a `500`
exactly as `PackageForbiddenException` would have. That exception has existed unused since the
original authorization work — [`authorization-plan.md`](authorization-plan.md#known-gaps) records it
as a known gap — and this is its first use, unless
[Pacman Controller](pacman-controller.md#1a-itemexistsexception--409--patch) — which needs the same
arm for a repository name collision — has already landed. Whichever of the two goes first adds it,
and the second finds it already there.

### Models

```jsonc
// CreateAccessTokenRequest — the POST body
{ "name": "laptop", "expiresAt": null }
```

`name` is required and validated against `AccessTokenValidationConstants.NameMaxLength`; a name the
user already has is the `409` above. `expiresAt` is optional and, when present, **must be in the
future** — a past date is a `400`, not a token that is dead on arrival. There is no maximum: a
credential that lives in a `pacman.conf` on a machine somebody administers by hand is one whose
lifetime the owner is better placed to judge than we are, and
[expiry notifications](#deferred-work) are the thing that would make a cap humane.

**An omitted or null `expiresAt` means the token never expires**, which is expected to be the common
case by some distance — these are configuration-file credentials, and revocation is
[a delete](#revocation-is-a-delete) rather than a date. Stating the default explicitly is worth the
line precisely because the safer-looking reading (omitted means "some sensible default lifetime") is
the wrong one.

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
string, the primary-key lookup, the expiry check, the `FixedTimeEquals` comparison, the principal and
its claims, the coarse `LastUsedAt` write, and [the use log](#every-use-is-logged). Not actor-scoped,
and nothing here takes `IActorAccessor`.

Verification needs a token to verify, so minting the format lives here too — as an internal detail
this service owns, which [issue 2b](#2b-iaccesstokenservice--minor) then exposes to a user.

*Acceptance:* unit tests that a minted token verifies; that a token differing in one character does
not; that a malformed, truncated or wrongly-prefixed string is rejected without throwing; that an
unknown token id is rejected; that an expired token is rejected; that the stored hash is not the
token and the token is not recoverable from the row; and that a minted secret is Base64Url and
therefore URL-safe.

`LastUsedAt` is written when stale and skipped when fresh, a failure to write it does not fail
verification, and its resolution is a configuration key with an entry in `appsettings.json` and a
default of one hour, tested at both a stale and a fresh value.

The log is asserted, not assumed: a successful verification writes the token id and the client
address, a failed one writes the token id where the string yielded one, and **no test-visible log
line contains the secret** — asserted against a captured log rather than by reading the code.

*Depends on:* 1.

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

### 3. Scoped actors — `MINOR`

`ActorScope` and its parser (split the space-delimited `scope` claim, keep the values matching
`<audience>:<entity>:<action>`, `*` accepted for entity and action, everything else ignored, and an
**empty** scope when none of the values are ours); `Actor.Scope` with `IsReadOnly` derived from it;
`ActorScope.Unrestricted` passed explicitly by `Actor.System` and `FixedActorAccessor`; and the
scope arm in every verdict method — `CheckRead` as well as `CheckWrite`, `CheckCreate` and
`PackageAccessPolicy.CheckPublish` — checked before the ownership tests.

`VisibleTo` is deliberately **not** touched; see [why](#visibleto-does-not-change-at-all).

**This issue cannot land before [issue 6](#6-scopes-on-a-bearer-token--minor)**, which is what makes
the realm emit the scopes. Turning enforcement on against a realm that emits none makes every token
powerless — see
[Every existing token becomes powerless](#every-existing-token-becomes-powerless).

Separate from issue 4 because it is a pure policy change with no HTTP in it, testable entirely by
`RepositoryAccessPolicyTests` and `PackageAccessPolicyTests`, and reviewable on its own — which is
the property that makes it worth reviewing carefully, since it is the guarantee the whole document
rests on.

*Acceptance:* parser unit tests for a wildcard entity, a wildcard action, both, an unknown entity, an
unknown action, a missing prefix, a wrong prefix, and a value with the right prefix but too few
segments. Then the four that matter most, because they are where a mistake is silent:

* A claim of `openid profile email roles pacman-manager` — none of it ours — parses to an **empty**
  scope, and an actor carrying it is refused every operation. This is the test that pins the
  direction of the default, and it is the one that would have to be rewritten if anybody ever
  reversed it by accident.
* A claim mixing ours with somebody else's (`openid pacman-manager:repositories:read`) keeps the one
  and ignores the rest.
* An actor built by `FixedActorAccessor` or `Actor.System` is unrestricted, so tools and background
  work are untouched by the default.
* `VisibleTo` is unchanged for every scope, asserted directly, since a scope that could narrow it
  would hide a user's own private repositories from their own client.

Both policy test fixtures gain rows for a `pacman-manager:*:read` scope on every verdict — including
a read-only owner of a private repository getting `Forbidden` rather than `NotFound` on a write, and
that same owner still *reading* it — plus a scope that permits an operation still losing to the
ownership rules underneath, which is the rule that a scope narrows and never widens.

*Depends on:* 6.

### 4. The `Basic` scheme and its handler — `MINOR`

The [selector policy scheme](#the-authorization-prefix-picks-the-handler) and the constants naming
it, the `Basic` scheme and its options, the handler over `IBasicAuthenticationService`, the scope
claim it issues, `HttpContextActorAccessor` building an `Actor` from whatever scope claim the
principal carries, and the
[present-but-invalid-is-a-`401` middleware](#present-but-invalid-credentials-are-a-401-and-this-is-easy-to-get-wrong).

Swagger is untouched: `Basic` gets no security definition and the OAuth flow stays exactly as it is.
That is a decision, not a backlog item. Swagger is how somebody learns to drive this API, and the
answer it should give is `Bearer` — `Basic` exists for `pacman`, which cannot speak OAuth, and
advertising it beside the OAuth flow would present it as an equivalent way in. It is not one, and
nothing should suggest it is.

*Acceptance:* handler unit tests cover a valid header, a missing header, a malformed one, a
non-Basic scheme, a wrong secret, an unknown token id and an expired token, and assert every failure
produces the same undifferentiated `401`. A test asserts the username field is ignored: the same
token authenticates with `token:`, with the owner's display name, and with an arbitrary string.

A test asserts the selector routes by prefix: a `Bearer` request is still handled by the JWT scheme
untouched, an absent header still falls through to anonymous, and an unrecognised scheme is treated
as `Bearer` rather than as `Basic`.

An E2E test asserts that a Basic-authenticated `POST` to `/api/v1/repositories` is a `403` — the test
that proves the restriction is a property of the credential rather than of the route, and the one
that would catch someone later removing the scope arm. A further test asserts a token does not
appear in the request log, and one asserts a Basic-authenticated request creates no
`ExternalProviderUserMapping`.

The `[AllowAnonymous]`-plus-`401` behaviour needs an anonymous endpoint to test against, and the
pacman routes do not exist yet. Add a test-only anonymous endpoint, or defer that single assertion to
[Pacman Controller](pacman-controller.md#4-pacmancontroller--minor) — the implementing issue picks,
but it must not go untested in both.

*Depends on:* 2a, 3.

### 5. `AccessTokensController` — `MINOR`

The three routes, the three wire models
([`CreateAccessTokenRequest`, `AccessToken` and `CreatedAccessToken`](#models)), the
[`AccessTokenFilter` and `AccessTokenSortField`](#the-listing-follows-the-standard-shape), the
`ControllerConstants` template, and the `ItemExistsException` → `409` arm if it is not already
there.

*Acceptance:* E2E tests: creating a token returns the secret exactly once, and listing tokens never
returns it, asserted against the raw response body; the returned token then authenticates a request;
deleting it makes it stop authenticating; a second token with the same name is a `409`; a past
`expiresAt` is a `400` and an omitted one produces a token with no expiry; another user's token is a
`404` to delete; the listing pages, filters and sorts, and never shows another user's token; every
route is a `401` unauthenticated; and a Basic-authenticated caller cannot mint a token. A handler
test asserts `ItemExistsException` produces `409`.

*Depends on:* 2b, 4.

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
are added there too — which is also the cheapest way to exercise them by hand.

No application code at all, which is the property worth demonstrating: the parser from issue 3 is the
only thing that reads these.

*Acceptance:* a token from the updated realm carries the expected values, asserted by decoding it in
a test rather than by inspection. Then, once issue 3 lands, E2E tests with a token carrying
`pacman-manager:*:read` (writes are `403`), one carrying `pacman-manager:packages:publish`
(publishing succeeds, creating a repository is `403`), one carrying `pacman-manager:repositories:*`
(repository writes succeed, publishing is `403`), and one carrying none of ours (every operation is
a `403`).

*Depends on:* nothing in this document, but it touches `keycloak/localdev.json` and therefore the
`auth` service in `compose.yaml` rather than only C#.

---

## Deferred work

Worth filing as issues, but explicitly out of scope for the work above.

* **A queryable audit trail for tokens.** [Use is logged](#every-use-is-logged), which answers the
  question that matters after a suspected leak, and that is the whole interim measure. What is
  deferred is history a *user* can read — every use rather than the last, and revocations, which
  would change revocation from a delete to a soft delete.
* **Minting a narrowed `PacmanAccessToken`.** Only the Basic half is missing. A narrowed **`Bearer`**
  credential already works and needs nothing from us: register a client that requests a smaller set
  of scopes, which is what client scopes are for and what every OIDC provider does. What has no
  equivalent is a user asking for a `PacmanAccessToken` carrying anything other than
  `pacman-manager:*:read`, because this application mints those itself — a scope field on the create
  request, and a concept in the UI to go with it.
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
