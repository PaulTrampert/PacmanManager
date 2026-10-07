# PacmanManager

A self-hosted host for private [pacman](https://wiki.archlinux.org/title/Pacman) repositories.
Authenticated users create repositories, publish packages into them, and choose whether each one is
public or private; any Arch Linux machine can then install from them with a single `Server` line.

## Using a hosted repository

[Consuming a hosted repository](docs/consuming-a-repository.md) covers configuring `pacman` to install
from one: the `pacman.conf` section, the `SigLevel` it requires, minting an access token for a private
repository, and what each error means.

## Developing

[`AGENTS.md`](AGENTS.md) describes the solution's layout, how to build, test and run it, and the
conventions it follows; [`CONTRIBUTING.md`](CONTRIBUTING.md) has the contribution rules. Design
documents are under [`docs/`](docs).
