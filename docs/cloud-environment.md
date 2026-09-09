# Working on PacmanManager from a claude.ai cloud environment

[Claude Code on the web](https://claude.ai/code) runs each session on an Ubuntu 24.04 VM with the
common toolchains preinstalled. This repository needs three things that VM does not ship: the
**.NET 10 SDK**, **pacman's own tooling** (`repo-add`, `repo-remove` and `libalpm`, which
`LibAlpmSharp` P/Invokes), and a **running Docker daemon** for the Testcontainers end to end tests.

`.claude/hooks/session-start.sh` installs all of it, and `.claude/settings.json` registers it as a
`SessionStart` hook, so a session started against this branch is ready without any configuration.
What the environment still decides is **network access**, and the default level is not wide enough
for the end to end tests. This document covers both halves.

## 1. Create the environment

1. Open [claude.ai/code](https://claude.ai/code) and select the cloud icon showing the current
   environment's name, in the row above the message box.
2. Select **Add cloud environment**.
3. Name it something like `PacmanManager`.
4. Fill in **Network access** and **Setup script** as below, then **Create environment**.

## 2. Network access

Pick **Custom**, check **Also include default list of common package managers**, and add these
domains, one per line:

```text
production.cloudfront.docker.com
quay.io
*.quay.io
geo.mirror.pkgbuild.com
*.pkgbuild.com
archlinux.org
*.archlinux.org
```

Why each one is needed:

| Domain | Needed for | What fails without it |
| :--- | :--- | :--- |
| `production.cloudfront.docker.com` | Docker Hub image **layers**. The Trusted list allows `registry-1.docker.io` and the older `production.cloudflare.docker.com`, but Docker Hub now serves blobs from CloudFront. | Every `docker pull` and every `FROM` resolves its manifest and then fails with `403 Forbidden` on the blob — `postgres:18` and `archlinux/archlinux` both. |
| `quay.io`, `*.quay.io` | `quay.io/keycloak/keycloak`, started by `KeycloakContainer`. | The Keycloak container never starts. |
| `geo.mirror.pkgbuild.com`, `*.pkgbuild.com`, `archlinux.org`, `*.archlinux.org` | The `pacman -Syu` inside `PacmanManager.RepoHost/Dockerfile`, and `PacmanManager.AurClient`'s live AUR tests, which call `aur.archlinux.org`. | The RepoHost image build fails, and six `AurClient` tests fail. |

**Trusted** on its own is enough for `dotnet build`, `dotnet test` on the unit fixtures, and NuGet
restore: `archive.ubuntu.com` and `api.nuget.org` are already on the default list. Choosing **Full**
instead of **Custom** also works and needs no list.

> `dot.net` is on the Trusted list but `builds.dotnet.microsoft.com`, where `dotnet-install.sh`
> actually downloads from, is not. That is why the setup script installs the SDK from Ubuntu's own
> archive rather than with the install script.

## 3. Setup script

The setup script runs once per environment, before Claude Code launches, and Anthropic snapshots the
filesystem afterwards, so later sessions start with everything already on disk. Putting the
toolchain here rather than leaving it to the hook is what keeps session startup fast.

Paste this into the **Setup script** field:

```bash
#!/bin/bash
set -euo pipefail
export DEBIAN_FRONTEND=noninteractive

apt-get update -qq || true
apt-get install -y -qq --no-install-recommends \
    dotnet-sdk-10.0 \
    pacman-package-manager \
    libalpm-dev \
    libarchive-tools \
    zstd \
    default-jre-headless

# Warm the image cache so the end to end tests do not pull on the clock. Both are best effort:
# they need the network allowlist above, and the session must still start whether or not they work.
nohup dockerd >/var/log/dockerd.log 2>&1 &
for _ in $(seq 1 30); do docker info >/dev/null 2>&1 && break; sleep 1; done
docker pull postgres:18 || true
docker pull quay.io/keycloak/keycloak || true
docker pull archlinux/archlinux || true
docker pull mcr.microsoft.com/dotnet/sdk:10.0 || true
```

Every package has a reason:

| Package | Why |
| :--- | :--- |
| `dotnet-sdk-10.0` | `net10.0` is the target framework of every project. Ubuntu 24.04 ships the SDK in `noble-updates`, so no third party apt feed is needed. |
| `pacman-package-manager` | `repo-add` and `repo-remove`, which `RepoHost` shells out to. |
| `libalpm-dev` | The `libalpm.so` symlink that `NativeMethods`' `DllImport("libalpm.so")` resolves. It pulls in `libalpm13t64`, the library itself. |
| `libarchive-tools`, `zstd` | `bsdtar` and `zstd`, which `test-fixtures/packages/build-fixture.sh` uses. |
| `default-jre-headless` | `Antlr4BuildTasks` runs the ANTLR jar to generate the `pacman.conf` parser, so the build needs a JRE. |

The setup script must exit zero and finish inside about five minutes, which this one does; the
`|| true` on the pulls is what keeps a blocked registry from failing the whole session.

## 4. What the SessionStart hook adds

`.claude/hooks/session-start.sh` runs after Claude Code launches, on every session. It is a no-op
outside a cloud session (`CLAUDE_CODE_REMOTE`), and in one it:

* installs any of the packages above that are missing, so the repository works even in an
  environment with no setup script;
* **starts `dockerd`**, which the setup script cannot do for you — the environment cache is a
  filesystem snapshot, so pulled images survive but running processes do not;
* runs `dotnet tool restore` (for `dotnet-ef`) and `dotnet restore`.

## 5. What works, and what does not

With the environment above:

| | Status |
| :--- | :--- |
| `dotnet build` (the repository's typecheck and lint) | Works |
| `LibAlpmSharp.Test` | Works — the fixture packages load through the real `libalpm`, and the local-database fixtures seed their own |
| `PacmanManager.CliTools.Test` | Works |
| `PacmanManager.RepoHost.Test` unit fixtures, `repo-add`/`repo-remove` included | Works |
| `PacmanManager.AurClient.Test` | Works only with the `archlinux.org` domains allowed; those six tests call the live AUR |
| `PacmanManager.RepoHost.Test` end to end fixtures | Works only with the Docker and Arch domains allowed |

**`LibAlpmSharp.Test` needs no filter here.** The fixtures that want installed packages seed a
local database under a temporary root (`PacmanManager.TestUtils.LocalPackageDatabase`) and point
`LibAlpm.Initialize(root, dbPath)` at it, so they read known fixture metadata rather than whatever
the host has installed, and they pass against Ubuntu's `libalpm 13.0.2` exactly as they do against
Arch's:

```bash
dotnet test LibAlpmSharp.Test
```

The one case that still depends on the host is
`LibAlpmTests.Initialize_WithDefaultPaths_CreatesInstance`, whose subject is the `/` and
`/var/lib/pacman` defaults themselves. It warns and stops where those paths are not a pacman
database the current user may open, which on the cloud image depends on whether the session is root.

## Resource limits

Cloud sessions get roughly 4 vCPUs, 16 GB of RAM and 30 GB of disk, which the full
`dotnet test` run, Postgres, Keycloak and two image builds fit inside.
