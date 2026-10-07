# Consuming a hosted repository

How to point a machine's `pacman` at a repository hosted by PacmanManager, public or private.
This is the user-facing counterpart to [Pacman Controller](pacman-controller.md),
[Basic Auth](basic-auth.md) and [User Management](user-management.md); those documents say why
things are the way they are, and this one says what to type.

Throughout, `packages.example.com` stands for wherever your PacmanManager is deployed, and `myrepo`
for the repository's name.

## The `pacman.conf` section

Add one section per repository to `/etc/pacman.conf`, named exactly after the repository:

```ini
[myrepo]
SigLevel = Optional TrustAll
Server = https://packages.example.com/pacman/$repo/$arch
```

Then `pacman -Sy` to fetch its database, and `pacman -S <package>` to install from it as you would
from any other repository.

### Use `$repo` and `$arch` literally

Write `$repo` and `$arch` exactly as shown. They are not placeholders for you to fill in: `pacman`
expands them itself, on every request.

* **`$repo`** becomes the section name, `myrepo`. The section name must therefore be the
  repository's name, spelled exactly as it is stored — names are case-sensitive, so `[MyRepo]` does
  not find a repository called `myrepo`. Repository names are unique across the whole deployment, so
  two repositories can never compete for one section name.
* **`$arch`** becomes the first value of `Architecture` in `[options]` — with the stock `auto`, the
  machine's own architecture. Each repository publishes one database per architecture it supports,
  and packages built for `any` appear in every one of them. Today the only architecture a repository
  may support is `x86_64`.

Every URL the client fetches is under `/pacman/myrepo/x86_64/`: the database as `myrepo.db`, the
files database (for `pacman -F`) as `myrepo.files`, and each package by its file name.

Sections are searched in the order they appear. If a package of the same name exists in an official
repository too, the section listed first wins, so put this one above `[core]` only if you mean it to
take precedence.

### `SigLevel = Optional TrustAll` is required

**This line is not optional.** PacmanManager does not sign its databases or its packages yet, and
Arch's stock `/etc/pacman.conf` sets `SigLevel = Required DatabaseOptional` for every repository that
does not say otherwise. Without the override, `pacman` refuses every package from the repository with
an error about a missing signature — nothing in that error points at your configuration.

`Optional TrustAll` tells `pacman` to accept an unsigned database and unsigned packages from this
repository. The consequence is that the repository is exactly as trustworthy as the HTTPS connection
that serves it, which is one more reason to [use HTTPS](#private-repositories). Signing is planned,
and once it exists this line can tighten to `Required`.

## Private repositories

A public repository needs nothing more than the section above, and no account at all. A private
repository is readable only by its owner, and the `Server` line has to carry credentials: an
**access token** minted for your account.

**Serve private repositories over HTTPS only.** The credentials travel in a header on every request,
and anybody who can read them off the wire can read the repository.

### Minting a token

A token is minted through the API, by a caller already signed in through the identity provider: in
the Swagger UI, `POST /api/v1/users/me/tokens` after authorizing, or with any HTTP client holding a
`Bearer` access token:

```bash
curl -X POST https://packages.example.com/api/v1/users/me/tokens \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"name": "laptop"}'
```

* **`name`** is a label for you, so a list of your tokens is legible — name it after the machine
  that will use it. Each of your tokens needs a different name, ignoring case.
* **`expiresAt`** is optional. Leave it out and the token never expires; give a time in the future
  and it stops working then.

The response carries two values you need:

```json
{
  "id": "0199…",
  "username": "pmt_0199…",
  "name": "laptop",
  "createdAt": "…",
  "secret": "pms_kJ8…"
}
```

**The `secret` is shown once, in this response, and never again.** Only a hash of it is kept, so it
cannot be looked up later. If you lose it, revoke the token and mint another.

A token can read every repository its owner can see, and **can never change anything**, through any
route. You can list your tokens with `GET /api/v1/users/me/tokens` and revoke one with
`DELETE /api/v1/users/me/tokens/{id}`; a revoked token stops working immediately.

### Putting the token in the `Server` line

The token's `username` goes in the URL's user name and its `secret` in the password, the usual
`user:password@` form:

```ini
[myrepo]
SigLevel = Optional TrustAll
Server = https://pmt_0199…:pms_kJ8…@packages.example.com/pacman/$repo/$arch
```

Both values are already URL-safe, so they go in exactly as returned, with nothing to escape.

`pacman` does not send these credentials with its first request. It sends them only when the server
answers with a `401` challenge, and the server challenges an anonymous request for any repository it
cannot show to an anonymous caller. That is expected, and invisible unless you are watching the
server's log.

The stock `/etc/pacman.conf` is readable by every user on the machine. If that matters, put the
section in a file of its own, readable only by root, and pull it in at the end of `pacman.conf`:

```ini
Include = /etc/pacman.d/myrepo.conf
```

## A private repository's name is not a secret

**Anybody can find out whether a repository name is in use, including when the repository is
private.** Repository names are unique across the whole deployment, so trying to create a repository
with a name that is taken has to fail, and that failure is visible to whoever tries.

What stays private is everything else: that the repository is yours, what is in it, its packages,
and who owns it. A private repository never appears in anybody else's listing, cannot be fetched by
anybody else, and the failed create says only that the name is taken.

If the name itself must not be guessable — because it names a customer or an unannounced project,
say — choose a name nobody would guess.

## When it does not work

`pacman` reports a failed download as `The requested URL returned error: <status>`, and the status
code is the only part of the server's answer it prints. A failed database download fails the whole
sync, with `error: failed to synchronize all databases`, so one broken repository stops
`pacman -Syu` for every repository on the machine until it is fixed or its section is removed.

| What `pacman` prints | What it means |
| :--- | :--- |
| `The requested URL returned error: 401` | The `Server` line has **no credentials, or wrong ones**, for a repository that is private **or does not exist**. Check that the token is in the URL, that it has not expired or been revoked, and that the username and secret belong together. Then check the section name for a typo. |
| `The requested URL returned error: 404` with working credentials | The token is valid, but its owner cannot see a repository of that name: it is somebody else's private repository, or no repository has that name — check the spelling and case. |
| `The requested URL returned error: 404` from a public repository, or a private one you can see | The repository exists, but not the file: usually an architecture the repository does not support. |
| An error about a missing or required signature | The section has no `SigLevel = Optional TrustAll`. |

A `401` for a repository that does not exist is deliberate. If the server answered `404` instead, an
anonymous request would reveal which names belong to private repositories. The cost is that a typo in
the repository name of an anonymous `Server` line reports `401` rather than `404`, which looks like a
credentials problem when it is a spelling problem.

`pacman` may also report a `404` for `myrepo.db.sig` or for a package's `.sig` during an otherwise
successful sync. That is `pacman` looking for signatures that do not exist yet, and is harmless with
`SigLevel = Optional TrustAll`.

## When a repository is renamed

A repository's name is in the URL of every machine configured for it, and **a rename breaks every
one of them at once.**

* **The old URL stops working immediately.** There is no redirect: the old name behaves like any
  name nobody holds, so a client sees a `401` with no credentials and a `404` with them. Since that
  fails the database download, **the whole `pacman -Syu` fails** on every machine still configured
  for the old name.
* **The old name is free for anybody to claim** the moment the rename happens. If somebody else
  creates a repository with it, machines still configured for the old name sync from *their*
  repository instead — silently, if theirs is public, since a public repository answers with or
  without credentials.
* **`pacman` will not tell anybody.** Nothing in its output says the repository was renamed.

So **if you rename a repository, you have to tell its users**, and each of them has to change the
section name in their `pacman.conf` to the new name. The `Server` line can stay as it is, since
`$repo` follows the section name. Then `pacman -Sy` fetches the database under its new name.

Removing an architecture from a repository breaks clients on that architecture the same way: their
next sync is a `404`.
