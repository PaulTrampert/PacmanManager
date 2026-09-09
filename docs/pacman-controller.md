# Pacman Controller

Status: **design**. This document is the source the implementation issues are cut from. It follows
the conventions established in [`authorization-plan.md`](authorization-plan.md) and
[`packages-api.md`](packages-api.md); where it departs from them, it says so.

It closes the first item under [`packages-api.md`'s deferred work](packages-api.md#deferred-work):
the API can publish packages, but nothing can install them, because a `pacman` client configured
with `Server = …` needs the `.db.tar.gz` and every package file resolvable under one base URL by the
basename `repo-add` recorded. There is no such URL today.

## Goals

* A `pacman` client configured with a single `Server = …` line can `pacman -Sy` and
  `pacman -S <package>` from a hosted repository.
* A **public** repository works for an unauthenticated client, with no credentials configured at
  all.
* A **private** repository works for its owner, authenticated with HTTP Basic credentials that can
  be written into a `pacman.conf`, and is indistinguishable from a repository that does not exist to
  everybody else.
* Nothing reachable under the pacman route root can modify anything, whatever credentials it is
  given.

## Non-goals for this work

Recorded here so the issues stay bounded; each has a follow-up in
[Deferred work](#deferred-work).

* Repository and package **signing** (`.db.sig`, `.pkg.tar.zst.sig`). Consumers must configure
  `SigLevel = Optional TrustAll`; see [Signature levels](#signature-levels).
* A browsable **directory index** at the repository root. `pacman` never asks for one.
* **Delta packages** (`.delta`), which pacman removed support for in 5.0 and nothing generates.
* An **arbitrary permission model**. Basic auth grants a user exactly the read access they already
  have, and never more.
* **Rate limiting** and download quotas.

---

## The route root

```
/repos/{ownerName}/{repoName}/{architecture}/{fileName}
```

Four things about this shape are deliberate.

**It is outside `/api`, and it is not versioned.** Every other route in this application is
`/api/v{version}/…` via `VersionByNamespaceConvention`. This one is not, because the URL is not ours
to version: it goes into a user's `pacman.conf`, on machines we do not administer, and it has to keep
working unchanged for as long as those machines exist. There is also nothing to version — the
response bodies are the wire formats `repo-add` and `makepkg` define, not models we control. A
future incompatible layout would be a different root, not a `v2` of this one.

**The path uses names, not ids**, so that a `pacman.conf` is legible and so that the URL survives
a `DATA_DIR` rebuild. The mapping from `(ownerName, repoName, architecture)` to a repository id, and
from a requested file name to a stored file, happens in the service layer; see
[Resolution](#resolution).

**The triple is exactly the database's unique index over repositories**, once the owner name is
resolved to an owner id. `PacmanRepository` is unique on `(OwnerId, Name, Architecture)`, so this
path matches at most one repository and needs no tie-breaking rule — the same reasoning that made
`GetRepositoryByNameAsync` take a whole `RepositoryKey` rather than a bare name.

**It lines up with pacman's own variable substitution.** pacman defines `$repo` as the section name
and `$arch` as the first configured architecture, so the last two segments are exactly what a client
writes as `$repo/$arch`:

```ini
[myrepo]
Server = https://packages.example.com/repos/paul/$repo/$arch
SigLevel = Optional TrustAll
```

That is not cosmetic. Because `$repo` *is* the section name, a client using this form can never ask
for a database whose name disagrees with the path — which is the property the
[file name rules](#what-file-names-are-served) rely on.

### Requiring the section name to match the repository name

`pacman` requests its database as `{Server}/{sectionName}.db`. The path segment is `{repoName}`.
These are independent strings, and this design **requires them to be equal**: a request for
`/repos/paul/myrepo/x86_64/other.db` is a `404`, not a redirect and not a served database.

The alternative — serving the repository's database under whatever `*.db` name was asked for —
would let a user name the section anything and still have it work, at the cost of two names for one
resource and a `pacman.conf` whose section name is silently meaningless. Since the `$repo` form above
makes the two equal by construction, the constraint costs a correctly configured client nothing and
turns a typo into an immediate, obvious failure instead of a repository that quietly works under the
wrong name.

---

## Usernames

`/repos/{ownerName}/…` needs a stable, unique, URL-safe, user-facing name on `User`, and
[`authorization-plan.md`](authorization-plan.md#looking-a-repository-up-by-name) already records
that this is the thing standing between the API and a pacman-consumable URL. `User` today has
`DisplayName` — neither unique nor constrained — and `Email`, which is unique but is personal data
that must not appear in a URL, in an anonymous listing, or in a web server access log.

So `User` gains a `Username`.

| Column | Type | Notes |
| :--- | :--- | :--- |
| `Username` | `string` | Required, unique, stored **already lowercased**. Matched by `RegularExpressions.Username`. |

### It is stored lowercased, and that is a schema decision

Two users called `Paul` and `paul` must not both exist, and `/repos/Paul/…` and `/repos/paul/…` must
resolve to the same person. That is a case-insensitive unique constraint, which in Postgres is
normally a unique index over `lower(username)`.

**That index cannot be expressed here.** `AGENTS.md` records that entity configuration in this
solution is by data annotations and there is no `OnModelCreating`, and a data annotation cannot
declare a functional index. Rather than open an `OnModelCreating` for one column — which would put
the configuration for a single entity somewhere no other entity's configuration lives — the value is
normalised on the way in and the index is a plain `[Index(nameof(Username), IsUnique = true)]`.

Every write path lowercases with `ToLowerInvariant` before storing, and every lookup lowercases the
input before comparing. `ToLowerInvariant`, not `ToLower`: the current culture is not something a
user name's identity may depend on, and the Turkish dotless-ı mapping would otherwise make `PAULI`
and `pauli` different users on a machine with a `tr-TR` locale and the same user everywhere else.
Restricting the character set to ASCII (below) makes this a non-issue in practice; doing it correctly
anyway costs nothing.

### Shape

```
\A[a-z0-9]([-_]?[a-z0-9])*\z
```

ASCII alphanumerics with single `-` or `_` separators, never leading or trailing, 2–39 characters.
The expression belongs in `RegularExpressions` alongside `RepositoryName`, anchored with `\A`/`\z`
for the reason already documented there: `$` also matches before a trailing newline, and this value
is used to build a URL and to look up a row.

No dots, because a name is followed in the path by segments that carry meaningful dots
(`myrepo.db.tar.gz`), and keeping the owner segment dot-free means nothing in the path is ambiguous
about where the file name starts. No uppercase, because the column already stores lowercase and
accepting uppercase input that silently changes would be a second spelling of one name.

**Reserved names.** `me`, `admin`, `api`, `repos`, `swagger` and `health` are refused. `me` is the
magic identifier on the users route below; the rest are route roots this application already serves
or plausibly will. A name that would collide with a route is much cheaper to refuse at registration
than to reclaim afterwards.

### Backfill and generation

`Username` is required, so the migration that adds it has to populate the rows already there, and
`ClaimsTransformer` has to supply one for every user it creates from now on.

The candidate is, in order: the `preferred_username` claim, then the local part of the email, then
`user`. It is lowercased, non-matching characters are replaced with `-`, runs of separators are
collapsed, leading and trailing separators are trimmed, and it is truncated to 39 characters. If the
result is empty, too short, reserved, or already taken, a numeric suffix is appended until it is
free.

The de-duplication is a loop against a unique index, so it races: two users registering at the same
instant can both find `paul` free. `EnsureUserLinkedAsync` already runs inside a transaction, so the
loop retries on the `DbUpdateException` from the unique violation rather than trusting its own
pre-check.

`preferred_username` is a candidate and not the answer: it is unique only within one identity
provider, and this application already models more than one authority in
`ExternalProviderUserMapping`. Two Keycloak realms can each have a `paul`, and only the first gets
the name.

### Renaming is a breaking change for that user's clients, and names are not recycled

`PATCH /api/v1/users/me` can change a username. **Every `pacman.conf` naming the old one stops
working**, with a `404` on the database — which is the correct and honest outcome, but it must be
documented at the endpoint and ideally warned about in whatever UI eventually calls it.

More seriously, a released name is a hazard. If Alice renames `alice` → `alice-b` and Bob then
registers `alice`, every client still configured for `https://…/repos/alice/myrepo/x86_64` is now
pointed at Bob's repositories. Nothing is disclosed — Bob's private repositories stay invisible, and
Alice's Basic credentials fail against them, because [the Basic username must match the token's
owner](#the-username-in-the-header-must-match-the-tokens-owner) — but a client that silently starts
tracking a stranger's repository is not an acceptable failure mode.

**A released username is therefore retained, not freed.** The simplest form that works is to keep
the old value in a `PacmanRetiredUsername` table (`Username` unique, `UserId`, `RetiredAt`) and
refuse registration of any name present in either table. It costs one row per rename and removes the
hazard entirely. Reclaiming a retired name is an administrative act, not a self-service one, and is
[deferred](#deferred-work).

---

## Authentication

Two schemes coexist. `Bearer` (the existing JWT) stays the default and is what the management API
under `/api/v1` uses. `Basic` is added for `pacman`, which has no way to obtain or refresh an OAuth
token and can only send credentials it was configured with.

### Read-only is a property of the actor, not of the route

The requirement is that Basic auth can never write. The obvious implementation is to accept the
`Basic` scheme only on the `/repos` routes, which are all reads. That is true today and stops being
true the first time somebody adds a write route and reaches for the scheme list without thinking
about it.

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

### Tokens

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

Unique index on `(UserId, Name)`.

#### Format

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

#### Hashing

`SHA-256` over the secret bytes, unsalted, compared with
`CryptographicOperations.FixedTimeEquals`.

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

#### The token is shown once

`POST /api/v1/users/me/tokens` returns the full string in its `201` response body and it is never
retrievable again, because only the hash is kept. Every other route that mentions a token returns
its id, name and dates only.

#### Revocation is a delete

`DELETE /api/v1/users/me/tokens/{tokenId}` removes the row. The absence of the row is then the only
state there is, which is one fewer thing for the verification path to check and no possibility of a
"revoked" token that some code path forgets to filter out. An audit trail of revocations would be
worth having and is [deferred](#deferred-work); it is not worth complicating the hot path for now.

#### Recording use

`LastUsedAt` exists so a user can tell which of their tokens is still in service. Writing it on every
request would mean **one `UPDATE` per package downloaded**, which is a write amplification the
feature does not justify.

It is therefore written only when the stored value is more than a configurable resolution (default
one hour) old. That reduces it to at most one write per token per hour while keeping the value
accurate enough for the question it answers. The write is best-effort: a failure to record use is
logged and never fails the request that was otherwise authorized.

### The Basic handler

An `AuthenticationHandler<BasicAuthenticationSchemeOptions>` that decodes
`Authorization: Basic base64(username:token)`, parses the token, loads the row by id, checks expiry,
compares the hash, and issues a principal carrying the existing
`AuthnConstants.AppUserIdClaimType` claim plus a marker claim identifying the credential as
read-only.

Reusing `app_user_id` is what makes the rest of the pipeline unchanged: `CurrentUserService` already
resolves that claim to a `User`, and `HttpContextActorAccessor` already turns a `User` into an
`Actor`. The only change there is that the accessor produces `Actor.ReadOnlyFor(user)` when the
marker claim is present.

`ClaimsTransformer` must be a no-op for this scheme. It short-circuits when `app_user_id` is already
present, which it is, so no change is needed — but there is a test to write that pins it, because a
Basic-authenticated request must never provision a user or a link.

#### The username in the header must match the token's owner

A token identifies its owner on its own, so the username field is redundant for authentication. It
is nonetheless **required to equal the token owner's `Username`**, case-insensitively; a mismatch is
a `401`.

The reason is the rename hazard above. After Alice renames herself, her stale `pacman.conf` still
carries `alice` in both the URL and the credentials. Enforcing the match means she gets one clean,
immediate `401` telling her the credentials are wrong. Ignoring the field would mean the credentials
keep working while the URL points at whoever holds `alice` now — a client half-working against a
stranger's repository, which is a much worse thing to debug than a failed login.

#### Present-but-invalid credentials are a `401`, and this is easy to get wrong

The `/repos` routes are `[AllowAnonymous]`, because a public repository has to serve an
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
user's `pacman.conf` — either as `Server = https://user:token@host/…`, which libcurl turns into a
Basic header, or via a curl `--netrc` style arrangement. That is the cost of the only mechanism
`pacman` supports, and it is the reason tokens are per-user, individually revocable and optionally
expiring rather than the account password.

The `Authorization` header must never be logged. `UseSerilogRequestLogging` does not log headers by
default, so this is a constraint on future changes rather than a change to make now; the issue adds a
test asserting that a token does not appear in the request log.

---

## Resolution

The `/repos` routes are served by a new `IPacmanRepoService`. It resolves a path to a file and
**owns no database access of its own**, which is the design decision worth stating first, because it
is the question a reader asks immediately.

[`authorization-plan.md`](authorization-plan.md#why-this-is-hard-to-get-wrong) and
[`packages-api.md`](packages-api.md#where-it-is-enforced) both rest on there being exactly one method
in the solution that touches `DbContext.PacmanRepositories` and exactly one that touches
`DbContext.PacmanPackages`, each asserted by an enforcement test. A third service reaching into
either `DbSet` would be a second copy of the visibility rule to keep in step — precisely what that
structure exists to prevent.

So resolution composes the existing chokepoints:

1. **Owner name → owner id** via a new `IUserService.GetUserByUsernameAsync`. Usernames are public,
   so this needs no authorization of its own; an unknown name simply yields no repository.
2. **Repository** via `IRepositoryService.GetRepositoryByNameAsync(new RepositoryKey(ownerId,
   repoName, architecture))`, which already applies `VisibleTo`. A private repository belonging to
   somebody else is absent here, so it is a `404` without any code in the new service deciding that.
3. **File** via `IRepositoryService` for a database file, or `IPackageService` for a package file.

Every authorization outcome in this feature therefore falls out of methods that were already
enforcing it, and `IPacmanRepoService` contains no rule of its own to get wrong.

### Case sensitivity, precisely

Only the **owner name** is case-insensitive, because it is stored lowercased and lowercased on the
way in. The **repository name** and **architecture** are matched exactly as stored, which is what
`GetRepositoryByNameAsync` already does and what the database's unique index already enforces. A
request for `/repos/paul/MyRepo/x86_64/…` against a repository named `myrepo` is a `404`.

This asymmetry is deliberate and must be documented at the route, because it is exactly the kind of
thing that looks like a bug. A username is an identity, and an identity that differs by case is a
phishing vector. A repository name is a label inside one user's namespace, it is already
case-sensitive everywhere else in this API, and pacman's `$repo` substitution reproduces the section
name verbatim — so a client using the documented form is always exact by construction.

### What file names are served

Let `{repo}` be the repository's name and `{id}` its id. The sync directory is
`{DATA_DIR}/libalpm/sync`.

| Requested `{fileName}` | Served from | Purpose |
| :--- | :--- | :--- |
| `{repo}.db` | `{id}.db.tar.gz` | The sync database. `pacman -Sy`. |
| `{repo}.db.tar.gz` | `{id}.db.tar.gz` | The same bytes under its real name. |
| `{repo}.files` | `{id}.files.tar.gz` | The files database. `pacman -Fy`. |
| `{repo}.files.tar.gz` | `{id}.files.tar.gz` | The same bytes under its real name. |
| A package basename | `{DATA_DIR}/repositories/{id}/{fileName}` | The package file, by the basename `repo-add` recorded. |
| Anything else | — | `404`. |

Everything is `application/octet-stream`.

**`repo-add` writes a files database, and nothing in this solution currently knows that.** Verified
against pacman `7.1.0.r9.g54d9411-2`: `create_db` and `rotate_db` in `/usr/bin/repo-add` both loop
over `for repo in "db" "files"`, so a single `repo-add` invocation produces `{id}.db.tar.gz`,
`{id}.files.tar.gz`, the symlinks `{id}.db` and `{id}.files` pointing at them, and `.old` backups of
each. `RepositoryService.GetRepositoryFileName` knows only about `.db.tar.gz`, which has two
consequences this work has to deal with — the files database has to be servable, and repository
deletion currently orphans everything except the one file it knows about. See
[issue 3](#3-account-for-the-whole-repo-add-file-set--patch).

**The symlinks are not used.** `{repo}.db` is served by reading `{id}.db.tar.gz` directly. The
symlinks are an artefact of `repo-add`'s own rotation and depending on them would mean following a
symlink out of a directory the application writes to, for no benefit — the bytes are identical.

**A package file name is resolved by lookup, never by parsing.** The client asks for exactly the
basename recorded as `%FILENAME%` in the database, so the service matches `PacmanPackage.FileName`
within the resolved repository. It does not decompose the name into `(name, version, architecture)`
and it does not build a path from the request: `IPackagePathResolver.GetPackageFilePath` composes the
path from the repository id and the **stored** basename, and already throws on anything that is not a
plain basename.

This needs an index. `PacmanPackage` is unique on `(RepositoryId, Name)`, but `FileName` is not
indexed at all, and this lookup runs once per package installed. Add
`[Index(nameof(RepositoryId), nameof(FileName))]`.

**A signature request is a `404`, not a `500`.** With `SigLevel` configured as documented pacman does
not ask for `.sig` files, but a client configured otherwise will, and it must get a clean answer.

**Path traversal.** `{fileName}` is a single route segment, so a literal `/` cannot appear in it, but
`%2F`, `..` and encoded control characters can be attempted. The name is validated as a plain
basename before it reaches any file system call, and the lookup-not-parse rule above means an
unrecognised name has no row and therefore no path. Both defences are tested.

---

## HTTP behaviour

`pacman` is an HTTP client with specific expectations, and meeting them is the difference between a
repository that works and one that works *well*. All of this applies to every `/repos` response.

**Conditional GET.** libalpm sets libcurl's `CURLOPT_TIMECOND` from the local database file's
modification time, so every `pacman -Sy` sends `If-Modified-Since`. Without a `Last-Modified`
response header and a `304` on a match, **every `pacman -Sy` re-downloads every database in full**.
Serving it via the `File(stream, contentType, fileName, lastModified, entityTag,
enableRangeProcessing)` overload gets this from the framework: `FileResultExecutorBase` evaluates the
preconditions and returns `304` itself.

Supply an `ETag` as well, cheaply derived from the file's length and modification ticks, which also
answers `If-None-Match`. Note that HTTP dates have one-second resolution and ASP.NET rounds
`Last-Modified` down to a whole second; a test that publishes and immediately re-reads inside the
same second will see a `304` where it expected a `200` unless it accounts for that.

**Range requests.** libalpm downloads a package to `{name}.part` and resumes an interrupted transfer
with `CURLOPT_RESUME_FROM_LARGE`, which sends `Range: bytes=N-` and expects `206 Partial Content`.
`enableRangeProcessing: true` on the same overload provides it, and works because `IFileSystem.OpenRead`
returns a seekable `FileStream`.

**`Content-Length`.** Follows from a seekable stream, and pacman's `CheckSpace` and progress
reporting both want it. Nothing may serve these responses chunked.

**`HEAD`.** Routed to the `GET` action by ASP.NET automatically. It must not stream a body, which the
framework also handles.

**Cache-Control, and this one is a security requirement.** A private repository's database and
packages are served on the strength of an `Authorization` header, so any intermediate cache that
stored them would serve them to the next caller without one:

* Private repository: `Cache-Control: private, no-store`.
* Public repository: `Cache-Control: public, max-age=…` is safe and desirable.

**`Vary: Authorization`** on every `/repos` response, for the same reason: the same URL can produce a
`404` for an anonymous caller and a `200` for an authenticated one.

**No `Content-Disposition`.** pacman names the file from the URL it requested. Setting a download
name is harmless but pointless here, and omitting it keeps the response minimal.

### Signature levels

Arch's stock `/etc/pacman.conf` sets `SigLevel = Required DatabaseOptional` globally — verified on
this repository's own reference machine. Because this work does not sign anything, **a client that
does not override `SigLevel` for the repository section will refuse every package**, with an error
about a missing signature rather than anything pointing at configuration.

That makes the `pacman.conf` snippet in [The route root](#the-route-root) not an example but a
requirement, and `SigLevel = Optional TrustAll` has to be documented everywhere the URL is. Signing
is the fix and is [deferred](#deferred-work); until then, the setting is the feature's single largest
usability trap and the reason [issue 12](#12-document-consuming-a-hosted-repository--patch) exists.

---

## Users API

`/api/v1/users`, versioned by namespace and plural like every other resource.

| Method | Path | Auth | Success | Failure |
| :--- | :--- | :--- | :--- | :--- |
| `GET` | `/api/v1/users` | Anonymous | `200` page of users | — |
| `GET` | `/api/v1/users/{userId}` | Anonymous | `200` user | `404` |
| `GET` | `/api/v1/users/me` | Required | `200` current user | `401` |
| `PATCH` | `/api/v1/users/me` | Required | `200` updated | `400`, `401`, `409` |
| `GET` | `/api/v1/users/me/tokens` | Required | `200` page of tokens | `401` |
| `POST` | `/api/v1/users/me/tokens` | Required | `201` token, **secret included once** | `400`, `401`, `409` |
| `DELETE` | `/api/v1/users/me/tokens/{tokenId}` | Required | `204` | `401`, `404` |

### `me` is the only way to reach the write routes

`GET /api/v1/users/{userId}` accepts a real id, and `me` is a literal alias for it — a literal
segment beats the `{userId:guid}` constraint in route matching, so the two do not conflict.

The write routes and every token route accept **`me` and nothing else**. There is deliberately no
`PATCH /api/v1/users/{userId}` that checks whether `{userId}` is the caller, because a route
incapable of naming another user cannot be got wrong: there is no check to forget, no id-confusion
bug to write, and nothing for a future administrative feature to accidentally widen. When one user
genuinely needs to act on another, that is an administrative capability with its own route and its
own permission, not a relaxation of this one.

The asymmetry with the read route is intentional. Reading a user is a public act; changing one is
not.

### Models

`PublicUserInfo` gains `username`. It is already what `Repository.Owner` projects to, so a repository
response starts carrying the name its pacman URL is built from — which is the value a caller
actually needs after listing repositories.

```jsonc
// PublicUserInfo — anonymous read routes and every embedded owner
{ "id": "0197…", "username": "paul", "displayName": "Paul" }
```

A separate `CurrentUser` model backs `/users/me` and adds `email`. **The anonymous routes must not
expose email**, which is why this is a second model rather than a nullable field on the first: a
field that is only sometimes populated is a field that will one day be populated by accident.

```jsonc
// CurrentUser — /api/v1/users/me only
{ "id": "0197…", "username": "paul", "displayName": "Paul", "email": "paul@example.com" }
```

```jsonc
// AccessToken — the secret appears only in the 201 from POST
{ "id": "0199…", "name": "laptop", "createdAt": "…", "expiresAt": null, "lastUsedAt": "…",
  "token": "pmt_0199…_kJ8…" }
```

**A note on the naming convention.** [`packages-api.md`](packages-api.md#entity) records that
entities take a `Pacman` prefix and the wire model keeps the bare name. `User` is the exception: the
entity shipped unprefixed, and renaming it to `PacmanUser` would touch the whole solution for
consistency alone — a user is not a pacman-domain concept the way a repository or a package is. So
the wire models are `PublicUserInfo` and `CurrentUser` rather than `User`, and `PacmanAccessToken`
does take the prefix because its wire model `AccessToken` would otherwise collide.

### Listing

`PaginationParams`, a `UserFilter` and `SortOptions<UserSortField>`, as every listing does.

| Parameter | Applied by | Meaning |
| :--- | :--- | :--- |
| `usernameContains` | `StringContainsQuery` | Substring of the username. |
| `displayNameContains` | `StringContainsQuery` | Substring of the display name. |

`UserSortField`: `Username`, `Created`. `Username` is first, so an unsorted listing is alphabetical,
and it carries `[DefaultSortDirection(SortDirection.Ascending)]`.

Users have no visibility rules — every user is visible to everyone, which is what makes the listing
anonymous. The reason that is acceptable is that the listing exposes nothing but a name a user chose
to be addressed by in a URL. Email is not projected, and no query parameter reaches it.

There is no `emailContains`, deliberately. It would turn the anonymous listing into an oracle for
"does this person have an account here", answerable one guess at a time.

### Changing a username

A collision on the unique index is a **`409`**, raised as `ItemExistsException`. That exception has
existed unused since the original authorization work — [`authorization-plan.md`](authorization-plan.md#known-gaps)
records it as a known gap — and this is its first real use. Renaming into a taken name is the case
most likely to happen, so it gets the honest status code rather than the `500` a bare
`DbUpdateException` would produce.

The same applies to a name that is reserved or [retired](#renaming-is-a-breaking-change-for-that-users-clients-and-names-are-not-recycled).

---

## Implementation plan

One issue per heading. Dependencies are noted; anything without a dependency can start immediately.
The ordering is a stack: 1 → 2 → 3 are independent groundwork, 4 → 6 build authentication, 7 → 9
build the users API, and 10 → 12 build the pacman routes on top.

### 1. Generic `ApplySort` — `PATCH`

`RepositorySortExtensions` and `PackageSortExtensions` are the same twenty lines with two type
parameters substituted: a dictionary from sort field to key selector, a `TryGetValue` falling back to
`default`, a direction ternary, and a `ThenBy` on the id. The users listing would be a third copy.

Replace them with one generic `ApplySort<TEntity, TSortField>` taking the key-selector dictionary and
the id selector, leaving each listing with only the table that is genuinely its own.

*Acceptance:* the existing `RepositorySortExtensions` and `PackageSortExtensions` tests pass
unchanged against the generic implementation, including the unknown-`sortBy` fallback and the
stable-paging tie-break.

*Depends on:* nothing. Should land before 8.

### 2. `Username` on `User` — `MINOR`

The column, `RegularExpressions.Username`, the reserved-name list, the length constants in
`UserValidationConstants`, the `PacmanRetiredUsername` table, the normalisation helper, generation in
`ClaimsTransformer`/`UserService`, `IUserService.GetUserByUsernameAsync`, `PublicUserInfo.Username`,
and a migration named `AddColumn_User_Username` that backfills existing rows.

*Acceptance:* unit tests for the normalisation and generation rules — a `preferred_username` with
characters outside the set, a missing claim falling back to the email local part, a collision taking
a numeric suffix, a reserved name being refused, and the retry on a concurrent unique violation.
`dotnet ef migrations list` shows the migration; applying it against the compose Postgres leaves
every pre-existing user with a distinct, valid username. `ClaimsTransformerTests` covers a new user
getting a generated name.

*Depends on:* nothing.

### 3. Account for the whole `repo-add` file set — `PATCH`

`RepositoryService` knows only `{id}.db.tar.gz`. `repo-add` also writes `{id}.files.tar.gz`, the
`{id}.db` and `{id}.files` symlinks, and `.old` backups of each. Give the file set one owner — the
natural home is `RepositoryDatabase`, which already exists so that the database file name and the
tools' working directory are defined once — and make repository deletion remove all of it.

*Acceptance:* unit tests over the file set the type reports. An E2E test creates a repository,
publishes a package, deletes the repository, and asserts the sync directory holds none of the six
files afterwards.

*Depends on:* nothing.

### 4. `PacmanAccessToken` entity and migration — `MINOR`

The entity as tabulated, `AccessTokenValidationConstants`, `PacmanAccessTokens` on
`PacmanManagerDbContext`, and a migration named `AddTable_PacmanAccessTokens`.

*Acceptance:* `dotnet ef migrations list` shows it; applying it against the compose Postgres
succeeds; the unique index on `(UserId, Name)` exists and the FK cascades from `User`.

*Depends on:* 2.

### 5. `IAccessTokenService` — `MINOR`

Minting (format, `RandomNumberGenerator`, SHA-256, the once-only return), parsing, verification
(primary key lookup, expiry, `FixedTimeEquals`), listing, deletion, and the coarse `LastUsedAt`
update with its configurable resolution.

*Acceptance:* unit tests that a minted token verifies; that a token differing in one character does
not; that a malformed, truncated or wrongly-prefixed string is rejected without throwing; that an
unknown token id is rejected; that an expired token is rejected; that the stored hash is not the
token; that `LastUsedAt` is written when stale and skipped when fresh; and that a failure to write it
does not fail verification.

*Depends on:* 4.

### 6. Read-only actors and Basic authentication — `MINOR`

`Actor.IsReadOnly` and `Actor.ReadOnlyFor`; the read-only arm in `RepositoryAccessPolicy.CheckWrite`,
`CheckCreate` and `PackageAccessPolicy.CheckPublish`; the marker claim; `HttpContextActorAccessor`
producing a read-only actor from it; the `Basic` scheme, its handler, the username-must-match rule,
and the present-but-invalid-is-a-`401` behaviour.

*Acceptance:* `RepositoryAccessPolicyTests` and `PackageAccessPolicyTests` gain a read-only row for
every write verdict, including a read-only owner of a private repository getting `Forbidden` rather
than `NotFound`, and assert that `VisibleTo` is unchanged for a read-only actor. Handler unit tests
cover a valid header, a malformed one, a wrong password, a mismatched username, an expired token and
an absent header. An E2E test asserts that a Basic-authenticated `POST` to `/api/v1/repositories` is
a `403` — the test that proves the restriction is a property of the credential rather than of the
route. A further test asserts a token does not appear in the request log, and one asserts a
Basic-authenticated request creates no `ExternalProviderUserMapping`.

*Depends on:* 5.

### 7. `UsersController` read routes — `MINOR`

`GET /api/v1/users`, `GET /api/v1/users/{userId}`, `GET /api/v1/users/me`, the `CurrentUser` model,
and the service methods behind them.

*Acceptance:* E2E tests for each route; `me` returns the authenticated user and `401` without
authentication; the anonymous routes never include an email, asserted against the raw response body
rather than a deserialised model so that an added property cannot slip past.

*Depends on:* 2.

### 8. `UserFilter`, `UserSortField` and the listing — `MINOR`

The filter, the sort field enum with its default direction, and the paged listing.

*Acceptance:* service unit tests for each filter and sort field, and for the unsorted default being
alphabetical by username.

*Depends on:* 1, 7.

### 9. `UsersController` write routes — `MINOR`

`PATCH /api/v1/users/me` for username and display name, with the reserved, retired and collision
cases; the token routes; `ItemExistsException` mapped to `409` by a new arm on the exception handler.

*Acceptance:* E2E tests: a rename takes effect and the old name stops resolving; renaming into a
taken, reserved or retired name is a `409`; creating a token returns the secret exactly once and
listing tokens never returns it; deleting a token makes it stop authenticating; a token belonging to
another user is a `404` to delete, not a `403`. A handler test asserts `ItemExistsException`
produces `409`.

*Depends on:* 2, 6, 7.

### 10. `IPacmanRepoService` — `MINOR`

Path resolution as described: owner name → owner id, the `RepositoryKey` lookup through
`IRepositoryService`, file-kind classification, `IPackageService.GetPackageContentByFileNameAsync`,
the `(RepositoryId, FileName)` index, and the richer return type carrying the stream, length and
modification time that the conditional-GET and range handling need.

*Acceptance:* unit tests for every row of the file-name table, including an unrecognised name, a
`.sig` request, a database name that disagrees with the repository name, and traversal attempts
(`..`, `%2F`, an encoded control character). Tests that a private repository resolves for its owner
and not for anyone else, and that the owner segment is case-insensitive while the repository name
and architecture are not. An enforcement test asserts the service names neither `DbContext`
`DbSet` — the property the whole design rests on.

*Depends on:* 2, 3.

### 11. `PacmanController` — `MINOR`

The routes under `/repos/{ownerName}/{repoName}/{architecture}/{fileName}`, thin as ever, plus the
HTTP behaviour: `Last-Modified`, `ETag`, `304`, `Range`/`206`, `Content-Length`, `Cache-Control`
by visibility, `Vary: Authorization`, and `application/octet-stream`.

*Acceptance:* E2E tests fetching the database, the files database and a package from a public
repository anonymously; the same against a private repository with a token, and `404` without one;
a mistyped token producing `401` rather than `404`; a conditional request returning `304`; a
`Range` request returning `206` with the right bytes; and a private repository's response carrying
`Cache-Control: private, no-store` while a public one does not.

*Depends on:* 6, 10.

### 12. Document consuming a hosted repository — `PATCH`

A `docs/consuming-a-repository.md` with the `pacman.conf` snippet, the `$repo`/`$arch` form, the
`SigLevel = Optional TrustAll` requirement and why it is needed, how to mint a token, how to
configure credentials, and what happens when a username changes. Linked from `README.md` and from
this document.

*Depends on:* 11.

### 13. End-to-end: a real `pacman` client installs a package — `MINOR`

The test that proves the feature. A Testcontainers `archlinux/archlinux` container, given a
`pacman.conf` pointing at the API on the test network, runs `pacman -Sy` and then
`pacman -S <fixture package>` and asserts the package is installed.

Run it against a public repository anonymously and against a private one with Basic credentials.
Assert that a second `pacman -Sy` with no intervening publish reports the database as up to date,
which is the only test that actually proves the conditional-GET path end to end.

The fixture package under `test-fixtures/packages` is what gets published; locate it with
`PacmanManager.TestUtils.PackageFixtures` rather than composing the path again.

*Acceptance:* as described. This is the acceptance criterion for the milestone as a whole — every
other test in this plan asserts a part, and this one asserts that the parts add up to something
`pacman` can use.

*Depends on:* 11.

---

## Deferred work

Worth filing as issues, but explicitly out of scope for the work above.

* **Repository and package signing.** A per-repository GPG key, `repo-add --sign`, storing and
  serving `.db.sig` and `.pkg.tar.zst.sig`, and publishing the public key so clients can move from
  `SigLevel = Optional TrustAll` to `Required`. Until this exists the repositories are only as
  trustworthy as the TLS connection serving them, and the `TrustAll` setting is the biggest
  usability wart in the feature.
* **A directory index** at `/repos/{owner}/{repo}/{arch}/`. pacman never needs one, but it is the
  first thing a human opens the URL expecting to see.
* **Reclaiming a retired username**, as an administrative operation with the client-breakage
  consequences made explicit.
* **An audit trail for tokens** — revocations, and the address a token was last used from — which
  would change revocation from a delete to a soft delete.
* **Rate limiting and download quotas.** Nothing bounds how often an anonymous caller can pull a
  public repository, and nothing bounds the bandwidth one token can consume.
* **Serving package files through a reverse proxy.** Streaming hundreds of megabytes through Kestrel
  works but occupies a request thread's worth of resources for the duration; `X-Accel-Redirect` or
  `X-Sendfile` would hand it to the proxy. Only worth doing once there is a deployment with a proxy
  in front.
* **`Usage` and mirrorlists.** Nothing generates a `pacman.conf` fragment or a mirrorlist for a user,
  which is the obvious convenience once more than one host serves the same repositories.
