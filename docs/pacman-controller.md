# Pacman Controller
This document describes the functionality required to actually expose pacman repositories while respecting repository permissions.

## High Level Requirements
* Expose pacman repositories at `/repos/{ownerName}/{repoName}/{architecture}/`.
  * Important note: path uses *names* not ids. This is to keep the path user-friendly.
* All paths under the root should respect what `pacman` expects to find for that repository. This means any name->id mapping, filename mapping, etc needs to happen within the routes.
  * Follow the existing design pattern of keeping controllers thin and putting this logic in the Service layer.
* All routes for this path root are *READ ONLY*
* HTTP Basic Auth must be supported to allow for config in a client's `pacman.conf`.
  * Basic auth must only ever grant *READ ONLY* permissions to a user. The read access follows repository access rules (anon can read all public, authenticated can read public + their own private).
  * I think the best way to support this for now would be to keep a table of a user's basic auth tokens. Tokens should be stored one-way hashed.
  * We will need a UserController at `/api/v1/users` that allows a user to generate a new token, revoke a token, and change their name. It will also need a list and read route. For the read route, let's have a special `me` id that returns the authenticated user. Read routes may be anonymous, write may only be called on the current user.
