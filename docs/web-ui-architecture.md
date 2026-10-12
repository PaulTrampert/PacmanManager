# Web UI Architecture

Status: **design**. This document fixes the architecture of the web front end, and is the source the
**Web UI: Walking Skeleton** issues are cut from. The later Web UI milestones — Repositories,
Packages, Account and Access Tokens, and Polish — build their screens on what it sets out, without
re-deciding it.

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

Three issues already sit in **Web UI: Walking Skeleton** — #183, #184 and #185, each finishing an
upstream SpaHost or ApiProxy fix on this side. They are not re-specified here; the
[implementation plan](#implementation-plan) says which of its issues each one waits for.

## Goals

* A contributor can run the web app against a real RepoHost and Keycloak, and run its unit,
  component and end-to-end tests, each with one documented command.
* The browser never holds an access token. Signing in, refreshing tokens and attaching them to API
  calls are the web host's job.
* The TypeScript API client cannot drift silently from RepoHost: a *breaking* change to the API
  fails CI until the committed OpenAPI document is regenerated, and the regenerated document then
  fails the web app's typecheck wherever the app's use of the API broke. Additive changes — a new
  route, a new optional field — do not touch the web build.
* A visitor who has not signed in can browse whatever the API shows anonymous callers; a signed-in
  user acts as themselves, and is sent to sign in again when their session dies.
* The feature milestones add screens by following the patterns here — a route, a query, a mutation,
  a story, a Playwright test — without an architectural decision of their own.

## Non-goals for this work

Recorded here so the issues stay bounded; each has a follow-up in
[Deferred work](#deferred-work).

* **Server-side rendering.** The app is a static bundle; SpaHost serves `index.html` for every
  route it does not otherwise handle.
* **Dark mode, responsive layout and an accessibility audit.** These are the **Web UI: Polish**
  milestone's subject. The patterns here do not preclude them.
* **Internationalisation.** Strings are English literals.
* **Running more than one web host replica**, which needs SpaHost's Redis-backed data protection.
* **Publishing the web image.** Like the other two images it is built in CI and never pushed.
* **Visual regression testing.**

---

## Repository layout

The web app lives in **`pacmanmanager-web/`** at the repository root, beside the .NET projects, and
its `package.json` `name` is `pacmanmanager-web` too. It is an npm project, **not** a member of
`PacmanManager.sln`.

```text
pacmanmanager-web/
├── .nvmrc                 # `lts/*`, read by CI and by nvm
├── package.json           # engines.node floor
├── package-lock.json      # committed; installs use `npm ci`
├── Dockerfile             # build context is this directory
├── .dockerignore          # node_modules, dist, storybook-static, test output
├── openapi/
│   └── v1.json            # RepoHost's OpenAPI document — committed, never hand-edited
├── .storybook/
├── e2e/                   # Playwright tests, fixtures and helpers
├── playwright.config.ts
├── vite.config.ts         # Vite, Vitest (both projects) and the dev proxy
└── src/
    ├── main.tsx           # providers and the router
    ├── theme.ts           # the MUI theme
    ├── routes/            # TanStack Router file-based routes
    ├── api/               # the client, its middleware, ApiError; schema.gen.ts is generated
    ├── session/           # the session query, useSession, requireUser
    ├── queries/<resource>/ # query-options factories and mutations, one folder per resource
    ├── components/<Name>/ # Name.tsx, Name.stories.tsx, Name.test.tsx, index.ts
    ├── mocks/             # Mock Service Worker (MSW) handlers, shared by Vitest and Storybook
    └── testing/           # render helpers and fixture factories
```

* Generated files end in **`.gen.ts`** and are gitignored: `src/routeTree.gen.ts` (TanStack Router)
  and `src/api/schema.gen.ts` (the API types). Nothing generated is committed except
  `openapi/v1.json`.
* A `generate` npm script produces both, and the `dev`, `build`, `typecheck`, `lint`, `test` and
  `storybook` scripts run it first through their `pre` scripts. A fresh clone therefore needs only
  `npm ci` before any of them.
* Tests and stories sit next to the component they cover. Playwright tests are the exception and
  live under `e2e/`.

**Why:**

* [the app lives beside the solution, not in it](#why-the-app-lives-beside-the-solution-not-in-it)

### Toolchain

| Concern | Choice |
| :--- | :--- |
| Runtime | Node **LTS** (`lts/*` in `.nvmrc`, `engines.node` a floor at the current LTS major); npm as shipped with it |
| Language | TypeScript, `strict`, `moduleResolution: bundler`, `noEmit` |
| Build and dev server | Vite, with `@vitejs/plugin-react` and `@tanstack/router-plugin` |
| UI | React 19, MUI (`@mui/material`, `@mui/icons-material`), Emotion |
| Routing | TanStack Router, file-based, `autoCodeSplitting: true` |
| Server state | TanStack Query |
| API types and client | `openapi-typescript` (types) and `openapi-fetch` (client) |
| API mocking | MSW, shared by Vitest and Storybook through `msw-storybook-addon` |
| Unit tests | Vitest, jsdom, Testing Library |
| Component tests | Storybook 10, stories run as tests by `@storybook/addon-vitest` in Chromium |
| End-to-end tests | `@playwright/test` |
| Lint and format | ESLint flat config (`typescript-eslint`, `react-hooks`, `@tanstack/eslint-plugin-query`, `@tanstack/eslint-plugin-router`, `eslint-plugin-storybook`) and Prettier |

* Every dependency is pinned by `package-lock.json`. Dependabot gains an `npm` ecosystem entry for
  `/pacmanmanager-web`, grouped like the others, and the existing `docker` entry covers the new
  Dockerfile.
* `.github/actions/setup-toolchain` and `.claude/hooks/session-start.sh` both install the Node LTS, so
  the Claude workflows and cloud sessions can build and test the web app.

**Why:**

* [MUI is the component library](#why-mui)
* [the client is `openapi-typescript` and `openapi-fetch`](#why-openapi-typescript-and-openapi-fetch)
* [APIs are mocked at the network with MSW](#why-msw-rather-than-swapping-the-client-module)
* [routes are file-based](#why-routes-are-file-based)

---

## Hosting

The production artefact is an image built **`FROM paultrampert/spahost:0.8.60`** (or later), with
the Vite build output copied to `/app/wwwroot/`. SpaHost is the only thing the browser talks to.

```text
browser ──► SpaHost (pacmanmanager.web)
              ├── /, /repositories/…   static files, then index.html as the fallback
              ├── /login, /logout      sign-in and sign-out (OIDC against Keycloak)
              ├── /userinfo            the signed-in user's claims, or 401
              ├── /antiforgery         sets the XSRF-TOKEN cookie
              └── /api/pacmanmanager/… ──► RepoHost /api/…, with the user's access token attached
```

### The compose service

`compose.yaml` gains **`pacmanmanager.web`**, built from `pacmanmanager-web/Dockerfile` with
`pacmanmanager-web` as its context, published on host port **8081**. Its configuration is
environment variables on the service:

| Variable | Value |
| :--- | :--- |
| `AuthConfig__OidcConfig__Authority` | `http://auth:8080/realms/localdev` |
| `AuthConfig__OidcConfig__ClientId` | `pacman-manager-web` |
| `AuthConfig__OidcConfig__ClientSecret` | the client's secret from `keycloak/localdev.json` |
| `AuthConfig__OidcConfig__Scopes__0…` | `openid`, `offline_access`, `profile`, `email`, `pacman-manager`, `pacman-manager:*:*` |
| `ApiProxy__PacmanManager__BaseUrl` | `http://pacmanmanager.repohost:8080/api/` |
| `ApiProxy__PacmanManager__AuthType` | `PTrampert.SpaHost.Authentication.OidcBearerAuthHandler, PTrampert.SpaHost` |

* The proxied API is named **`PacmanManager`**, so RepoHost's `/api/v1/…` is reached at
  **`/api/pacmanmanager/v1/…`**. Every API call the app makes uses that prefix and no other.
* The `pacman-manager-web` client in `keycloak/localdev.json` lists `http://localhost:8081/*` and
  `http://localhost:3000/*` among its redirect and post-logout redirect URIs, and its web origins.
* `docker compose up -d --build` **must** produce a working stack with no other setup: the
  `pacmanmanager.repohost` service carries the environment it needs (`Auth__Authority` and a
  connection string), and `pacmanmanager.web` depends on it and on `auth`.
* `ResponseHeaders` and `MaxRequestBodySize` on the proxied API are #185's and #184's to add.

**Why:**

* [the browser never holds a token](#why-the-browser-never-holds-a-token)
* [the web host is on 8081](#why-the-web-host-is-on-8081)

### The development server

`npm run dev` serves the app from Vite on **port 3000** with hot reload, and proxies
`/api`, `/login`, `/logout`, `/userinfo`, `/antiforgery`, `/signin-oidc` and
`/signout-callback-oidc` to the compose stack's web host on `http://localhost:8081`, with
`changeOrigin: false`. A `predev` script (after `generate`) brings the stack up with
`docker compose -f ../compose.yaml up -d --build`. The app's code is the
same in both modes; it never names a host.

---

## The API client

### The OpenAPI document

* `pacmanmanager-web/openapi/v1.json` is RepoHost's `v1` document, written by the Swashbuckle CLI
  (`Swashbuckle.AspNetCore.Cli`, at the same version as `Swashbuckle.AspNetCore`), which is added to
  `.config/dotnet-tools.json` alongside `dotnet-ef`.
* It is regenerated by one documented command, run from the repository root after building
  RepoHost:

  ```bash
  ASPNETCORE_ENVIRONMENT=Development dotnet swagger tofile \
    --output pacmanmanager-web/openapi/v1.json \
    PacmanManager.RepoHost/bin/Debug/net10.0/PacmanManager.RepoHost.dll v1
  ```

* The environment **must** be `Development`, in the command and in the check below. The document
  embeds `Swagger:OpenIdConnectUrl`, which only `appsettings.Development.json` sets, so any other
  environment produces a different document or none.
* `Program.cs` lets `HostAbortedException` propagate without logging it as fatal. The CLI stops the
  host with that exception straight after `builder.Build()`, so nothing after `Build()` — the pacman
  config, the `DATA_DIR` directories — runs during generation, and no database is needed.
* The committed document is the **contract the web app is built against**, not a mirror of every
  commit. An `OpenApiDocumentTests` fixture in `PacmanManager.RepoHost.Test` generates the document
  the same way into a temporary file, compares it with the committed one, and fails only on a
  **breaking** difference: a removed route or method, a removed or retyped property, a property or
  parameter that became required, a removed enum value, a removed response status. Additions —
  routes, optional properties, enum values, response statuses — pass without regenerating. The
  failure message lists the breaking differences and gives the regeneration command. The comparison
  is a small `OpenApiCompatibility` class with its own unit tests, one per rule.
* A pull request that breaks the contract regenerates the document in the same change, so the web
  app's typecheck fails until the app is adapted; that is the signal, and it is deliberate. A pull
  request that only adds to the API need not touch the web app, which adopts the additions by
  regenerating the document when it wants them.

**Why:**

* [the spec is committed and checked rather than generated in the image](#why-the-spec-is-committed-and-checked)

### Types and the client

* `openapi-typescript` turns `openapi/v1.json` into `src/api/schema.gen.ts`. The web app imports
  request and response types from there, re-exported under friendly names from `src/api`; it
  **never** declares a hand-written type for anything the document describes.
* `src/api/client.ts` exports the one `openapi-fetch` client, created with
  `baseUrl: "/api/pacmanmanager"` and `credentials: "same-origin"`. Nothing else calls `fetch`
  against `/api`.
* Its middleware does exactly two things:

  | Request | Middleware |
  | :--- | :--- |
  | Any method but `GET`, `HEAD` and `OPTIONS` | `GET /antiforgery` first, then send the `XSRF-TOKEN` cookie's value in the `X-XSRF-TOKEN` header |
  | Any response with status `401` | Send the browser to `/login?redirectUri=<current path and query>` — added by #183 |

* Query functions and mutations unwrap the client's result: data is returned, and an error response
  is thrown as an **`ApiError`** carrying the status and the parsed `ProblemDetails` (or
  `ValidationProblemDetails`) body. Components never see an `openapi-fetch` result object.

**Why:**

* [the antiforgery token is fetched before every mutation](#why-the-antiforgery-token-is-fetched-before-every-mutation)

---

## Session and anonymous visitors

The app renders for everyone. What a page shows depends on the **session**, which is either
`anonymous` or `signedIn` with the caller's `CurrentUser`.

| Step | Request | Outcome |
| :--- | :--- | :--- |
| 1 | `GET /userinfo` | `401` → the session is **anonymous**; stop. `200` → continue. |
| 2 | `GET /api/pacmanmanager/v1/users/me` | `200` → **signedIn** with that `CurrentUser`. `401` → the client's 401 rule sends the user to sign in. |

* The session is a TanStack Query query (`session/sessionQuery`), resolved in the **root route's
  `beforeLoad`** and put on the router context. **No route renders until it has resolved**, so a page
  never shows the anonymous view and then flips to the signed-in one.
* A `401` from `/userinfo` is **not** a reason to sign in, and the session query calls `/userinfo`
  with plain `fetch`, outside the API client and its 401 rule. Only a `401` from `/api/…` sends the
  user to `/login`.
* Step 2 is also first-login registration: RepoHost provisions the user record when it first sees
  their token, so a new user's first page load creates them, and the UI has nothing else to do.
* `requireUser` is the `beforeLoad` guard for routes that need a signed-in user (creating a
  repository, account pages). For an anonymous session it does a **full page navigation** to
  `/login?redirectUri=<the requested path>` — `/login` is a SpaHost route, not a router route.
* The app bar shows **Sign in** (a link to `/login?redirectUri=<current path>`) or the user's display
  name with **Sign out**. Sign out fetches `/antiforgery`, then submits a form `POST` to `/logout`
  carrying the token in an `antiforgeryToken` field. SpaHost ends the cookie session and the
  Keycloak session and returns the browser to `/`.
* Controls for actions the session cannot perform are not rendered: an anonymous visitor sees no
  create, edit, upload or delete control. Whether a signed-in user may act on a given repository is
  decided by the data the API returns, never re-derived in the UI.
* A resource the API answers `404` for is shown as not found, whatever the reason the API had,
  matching RepoHost's rule that a hidden repository is indistinguishable from a missing one.

**Why:**

* [`/userinfo` decides anonymous, and only `/api` 401s sign you in](#why-userinfo-decides-anonymous)
* [the session resolves before the first render](#why-the-session-resolves-before-the-first-render)

---

## Routing and data

* Routes are files under `src/routes`; `__root.tsx` is created with
  `createRootRouteWithContext<{ queryClient: QueryClient; session: Session }>()`.
* The router is created with `defaultPreload: "intent"` and `defaultPreloadStaleTime: 0`, so
  TanStack Query, not the router, decides freshness.
* Each resource has a folder under `src/queries/<resource>/` holding **query-options factories**
  (`repositoryListQuery(params)`, `repositoryQuery(name)`) and **mutation hooks**. Query keys start
  with the resource name, and a factory's key includes every parameter its request sends.
* Route loaders call `queryClient.ensureQueryData(...)` with the same factory the component passes
  to `useSuspenseQuery`. A component never builds a query key or calls the client inline.
* A mutation invalidates the list queries of its resource and updates or removes the affected
  detail query, on success only.
* List pages keep their pagination, filter and sort state in the URL's search params, validated by
  the route's `validateSearch`, with names matching the API's (`offset`, `pageSize`, `sortBy`,
  `direction`, and the filter's own parameter names).
* `QueryClient` defaults:

  | Setting | Value |
  | :--- | :--- |
  | Query `retry` | never for an `ApiError` with a `4xx` status; otherwise up to 2 retries |
  | Query `staleTime` | 30 seconds |
  | Query `refetchOnWindowFocus` | `true` |
  | Mutation `retry` | never |

**Why:**

* [routes are file-based](#why-routes-are-file-based)
* [a `4xx` is never retried](#why-a-4xx-is-never-retried)

---

## Error handling

| Failure | Shown as |
| :--- | :--- |
| `401` from `/api/…` | The client's 401 rule sends the browser to sign in — nothing renders |
| `404` from a route's loader | The route's not-found component, through `notFound()` |
| `403` | A "you don't have permission to do that" message in place of the page or form |
| `400` with `ValidationProblemDetails` from a mutation | Each `errors` entry on its form field; entries with no matching field in a `ProblemDetailsAlert` above the form |
| Any other `ApiError`, or a network failure, from a loader | The root route's error component, with a **Try again** action that resets the query and reloads the route |
| Any other `ApiError` from a mutation | A `ProblemDetailsAlert` with the response's `title` and `detail` |
| An unknown path | The root route's not-found component |

* There is exactly one root error component and one root not-found component. A route overrides
  them only where it can say something more specific.
* An unexpected exception in rendering is caught by the root error component; the app never shows a
  blank page.

---

## Theme and layout

* `src/theme.ts` builds the one MUI theme with `createTheme({ cssVariables: true, ... })` and a
  `light` colour scheme. Polish adds the `dark` scheme alongside it.
* `__root.tsx` renders the app shell: an MUI `AppBar` (product name linking to `/`, the session
  control) above a `Container` holding the `Outlet`.
* Every interactive element has an accessible name. Playwright and Testing Library select by role
  and label, so a control without one cannot be tested.

**Why:**

* [MUI is the component library](#why-mui)

---

## Testing

| Level | Covers | Tool | Runs against | Command |
| :--- | :--- | :--- | :--- | :--- |
| Unit | Functions, hooks, the client middleware, the session logic | Vitest + Testing Library, jsdom | MSW handlers | `npm test` |
| Component | Every component in `src/components`: a story per meaningful state, with a `play` function for each interaction | Storybook + `@storybook/addon-vitest`, Chromium | MSW handlers | `npm test` |
| End to end | User journeys across SpaHost, Keycloak and RepoHost | `@playwright/test` | The compose stack | `npm run e2e` |

* Vitest has two projects in `vite.config.ts`, `unit` and `storybook`; `npm test` runs both once and
  exits, and `npm run test:watch` watches.
* MSW handlers in `src/mocks` are typed from `schema.gen.ts`, so a handler whose shape no longer
  matches the API fails the typecheck. Vitest starts the server in its setup file and fails any test
  that makes a request no handler matches.

### End-to-end tests

* The stack is **started outside Playwright**, by a `pree2e` npm script that runs
  `docker compose -f ../compose.yaml up -d --build`, so `npm run e2e` is the one command locally and
  in CI. `up -d` is idempotent, so a stack already running is left alone. Playwright's global setup only waits until
  `http://localhost:8081/` and Keycloak's discovery document both answer, and fails with a message
  naming the compose command if they do not within its timeout.
* `baseURL` is `http://localhost:8081`. Tests run against the built image, never the dev server.
* Tests sign in through the real Keycloak login form as the realm's `test` and `other` users, using a
  `signIn(page, user)` helper in `e2e/fixtures`. Sign-in state may be saved per user with
  `storageState` and reused across tests.
* `e2e/keycloak.ts` holds the Keycloak admin REST calls tests need (signing a user's sessions out,
  for #183), authenticating as the bootstrap admin.
* **Tests share one stack and one database**, which persists between local runs. Every test creates
  what it needs under a unique name (a random suffix) and never asserts on data it did not create;
  no test depends on another's side effects or on the order tests run in.
* Locators use roles, labels and visible text. `data-testid` is a last resort.
* Retries are `0` locally and `1` in CI. Traces are kept on the first retry, and the HTML report and
  traces are uploaded as a CI artifact when the job fails.

**Why:**

* [end-to-end tests are TypeScript Playwright against compose](#why-end-to-end-tests-are-typescript-playwright-against-compose)
* [the stack is started outside Playwright](#why-the-stack-is-started-outside-playwright)
* [APIs are mocked at the network with MSW](#why-msw-rather-than-swapping-the-client-module)
* [stories run as tests under Vitest](#why-stories-run-as-tests-under-vitest)

---

## Continuous integration

`.github/workflows/ci.yml` gains two jobs, and its `docker` matrix gains a third image.

| Job | Steps |
| :--- | :--- |
| `web` | `actions/setup-node` from `.nvmrc` with the npm cache; `npm ci`; `npx playwright install --with-deps chromium`; `npm run format:check`; `npm run lint`; `npm run typecheck`; `npm test`; `npm run build`; `npm run build-storybook` |
| `e2e` | Node as above; `npm ci`; `npx playwright install --with-deps chromium`; `npm run e2e` (its `pree2e` starts the stack); on failure, `docker compose logs` and the Playwright report uploaded as an artifact; `docker compose down -v` always |
| `docker` (matrix entry `web`) | Builds `pacmanmanager-web/Dockerfile` with `pacmanmanager-web` as the context, `push: false` |

* Both new jobs run in `pacmanmanager-web` as their working directory, except the compose commands,
  which run from the repository root.
* The OpenAPI compatibility check needs no job of its own: it is a test in `PacmanManager.RepoHost.Test`,
  which the `test` job already runs. That job runs `dotnet tool restore` before testing.
* `AGENTS.md` documents the web app's commands next to `dotnet build` and `dotnet test`, adds
  `pacmanmanager-web` to the project layout table, and describes the new jobs.

---

## Implementation plan

One issue per heading, all in **Web UI: Walking Skeleton**. Dependencies are noted; anything
without a dependency can start immediately.

| Issue | Depends on |
| :--- | :--- |
| 1. OpenAPI document | nothing |
| 2. Scaffold | nothing |
| 3. Image and compose service | 2 |
| 4. API client | 1, 2 |
| 5. Playwright harness | 3 |
| 6. Session | 4, 5 |
| 7. Error handling | 4, 5 |

### 1. Generate and commit RepoHost's OpenAPI document — PATCH

Adds `Swashbuckle.AspNetCore.Cli` to the tool manifest, the `HostAbortedException` pass-through in
`Program.cs`, the committed `pacmanmanager-web/openapi/v1.json`, the `OpenApiDocumentTests` compatibility
check with `OpenApiCompatibility`, and `dotnet tool restore` in the `test` job. `AGENTS.md` gains the regeneration command.

*Constraints:* generation runs with `ASPNETCORE_ENVIRONMENT=Development`, in the command and in the
test — see [The OpenAPI document](#the-openapi-document).

*Acceptance:* the compatibility test passes on the committed file; adding a route or an optional
property still passes; removing a property or making one required fails with a message naming the
breaking difference and the regeneration command; running the command makes it pass again.
`OpenApiCompatibility` has a unit test per rule. Generation succeeds with no Postgres reachable and no `DATA_DIR`, and logs nothing at
`Fatal`.

*Depends on:* nothing.

**Why:**

* [the spec is committed and checked](#why-the-spec-is-committed-and-checked)

### 2. Scaffold `pacmanmanager-web` — MINOR

The npm project with the [toolchain](#toolchain) and [layout](#repository-layout) above: Vite,
TypeScript, React, MUI theme, TanStack Router with a root route rendering the app shell and an index
page, the `QueryClient` with its defaults, ESLint and Prettier, Vitest's two projects, Storybook with
MSW and one story for the app shell, the `generate` script (routes only until 4), the dev server
proxy, the `web` CI job, Node in `setup-toolchain` and `session-start.sh`, the Dependabot `npm`
entry, and the `AGENTS.md` updates.

*Acceptance:* the `web` job runs green on the PR. A unit test renders the index route inside the
router and finds its heading by role; the app shell's story runs as a test. `npm ci && npm run build`
works from a fresh clone with no generated file present.

*Depends on:* nothing.

**Why:**

* [the app lives beside the solution, not in it](#why-the-app-lives-beside-the-solution-not-in-it)
* [stories run as tests under Vitest](#why-stories-run-as-tests-under-vitest)

### 3. Web image and compose service — MINOR

`pacmanmanager-web/Dockerfile` and `.dockerignore`, the `pacmanmanager.web` compose service with the
[configuration above](#the-compose-service), the redirect URIs on `pacman-manager-web`, the
`pacmanmanager.repohost` environment that makes `docker compose up` work unaided, and the `web` entry
in the CI `docker` matrix.

*Constraints:* the image builds on `paultrampert/spahost:0.8.60` or later.

*Acceptance:* the `docker` job builds the image. After `docker compose up -d --build` on a clean
checkout, `http://localhost:8081/` serves the app, a deep link such as `/repositories` serves
`index.html`, and `/login` reaches the Keycloak sign-in page. Recorded in the PR description, since
5 is what automates it.

*Depends on:* 2.

**Why:**

* [the browser never holds a token](#why-the-browser-never-holds-a-token)
* [the web host is on 8081](#why-the-web-host-is-on-8081)

### 4. Generated API client with antiforgery — MINOR

`openapi-typescript` added to `generate`, `src/api` with the client, the antiforgery middleware,
`ApiError` and the unwrap helpers, and typed MSW handler helpers in `src/mocks`.

*Constraints:* the 401 rule is **not** part of this issue; it is #183's. Nothing outside `src/api`
calls `fetch` against `/api`.

*Acceptance:* unit tests, against MSW, assert that a `POST`, `PUT`, `PATCH` and `DELETE` each fetch
`/antiforgery` first and send the cookie's value in `X-XSRF-TOKEN`; that a `GET` does neither; and
that a `ProblemDetails` response becomes an `ApiError` with its status, `title` and, for a validation
response, its `errors`. Editing `openapi/v1.json` to rename a property used by the app fails
`npm run typecheck`.

*Depends on:* 1, 2.

**Why:**

* [the client is `openapi-typescript` and `openapi-fetch`](#why-openapi-typescript-and-openapi-fetch)
* [the antiforgery token is fetched before every mutation](#why-the-antiforgery-token-is-fetched-before-every-mutation)

### 5. Playwright harness — MINOR

`@playwright/test`, `playwright.config.ts`, the readiness global setup, the `signIn` fixture,
`e2e/keycloak.ts`, the `e2e` npm script and CI job, and one test: an anonymous visitor loads `/` and
sees the app shell.

*Constraints:* the stack is started outside Playwright — see
[End-to-end tests](#end-to-end-tests).

*Acceptance:* the `e2e` job runs green on the PR. With the stack down, `npm run e2e` fails within
the readiness timeout with a message naming `docker compose up -d --build`. A deliberately failing
test, pushed and then reverted on the PR, uploads the report and compose logs.

*Depends on:* 3.

**Why:**

* [end-to-end tests are TypeScript Playwright against compose](#why-end-to-end-tests-are-typescript-playwright-against-compose)
* [the stack is started outside Playwright](#why-the-stack-is-started-outside-playwright)

### 6. Session: sign-in, sign-out and first-login registration — MINOR

`src/session` with the session query, `useSession` and `requireUser`; the root route's `beforeLoad`;
the app bar's session control; sign-out by form `POST`.

*Constraints:* `/userinfo` is fetched outside the API client — see
[Session and anonymous visitors](#session-and-anonymous-visitors).

*Acceptance:* unit tests cover both session outcomes and `requireUser`'s redirect target, including
the query string. Stories cover the session control signed out and signed in. Playwright: an
anonymous visitor sees **Sign in** and no display name; the `test` user signs in from a deep link,
lands back on that link and sees their display name; signing out returns to `/` showing **Sign in**,
and a reload stays signed out.

*Depends on:* 4, 5.

**Why:**

* [`/userinfo` decides anonymous](#why-userinfo-decides-anonymous)
* [the session resolves before the first render](#why-the-session-resolves-before-the-first-render)

### 7. Error handling — MINOR

The root error and not-found components, `ProblemDetailsAlert`, the forbidden message, and a
`useFormErrors` helper mapping `ValidationProblemDetails.errors` to form fields.

*Acceptance:* stories for each component and state. Unit tests: a loader throwing an `ApiError` with
`500` renders the error component and **Try again** refetches; a `404` renders not-found; a query
failing with `404` is requested once and one failing with `503` three times. Playwright: an unknown
path shows the not-found page.

*Depends on:* 4, 5.

**Why:**

* [a `4xx` is never retried](#why-a-4xx-is-never-retried)

### Existing issues

| Issue | Waits for |
| :--- | :--- |
| #183 — sign in again when the session can no longer be refreshed | 4, 6 |
| #184 — uploads over 30 MB through the web host | 3, 5 |
| #185 — stream package downloads through the web host | 3, 5 |

#184's and #185's E2E tests drive the proxy with Playwright's `request` context, signed in as a realm
user (#184's upload sending the antiforgery header too), so neither waits for an upload or download
screen.

---

## Deferred work

Worth filing as issues, but explicitly out of scope for the work above.

* **Dark mode, responsive layout and an accessibility pass** — the **Web UI: Polish** milestone. The
  theme already uses CSS variables, so a `dark` scheme is additive.
* **Multiple web host replicas** — set SpaHost's `RedisConfig__UseForDataProtection` and add Redis
  to `compose.yaml`. Additive.
* **Internationalisation** — would mean extracting every literal; nothing here is in the way, but
  nothing helps either.
* **Server-side rendering** — would replace SpaHost's static hosting, and is unlikely to be worth it
  for an authenticated management UI.
* **Visual regression testing** — Storybook's stories are the natural input, through a hosted
  service or Playwright screenshots. Additive.
* **Publishing images** — a release pipeline for all three images, when the project has releases.

---

## Appendix

The argument for each decision in the plan, including what was considered and rejected. Nothing
here adds a requirement — the plan above is the specification, and it is the plan, once merged,
that is settled. These entries are the supporting reasoning: if implementation suggests a different
choice, they are what to argue with, in an issue, rather than something to quietly depart from.
A deviation that is agreed to updates the plan, this Appendix and the dependent issues together, as
the **How to read it** paragraph at the top sets out.

### Why the browser never holds a token

The stack was settled before this document: the app is hosted in PTrampert.SpaHost, which is a
backend for frontend. It signs the user in as a confidential client, keeps the tokens server-side
behind an HttpOnly cookie, refreshes them, and attaches them to proxied API calls. A token stolen by
script in the page does not exist to be stolen.

Rejected: a public client using PKCE in the browser, as Swagger UI does. It needs no web host at
all, but puts access and refresh tokens within reach of any script that runs in the page, and every
page in the app would carry its own token-refresh logic. SpaHost already exists, is maintained
alongside this project, and has had the fixes this app needs (#183, #184, #185) made upstream.

### Why the app lives beside the solution, not in it

`pacmanmanager-web/` is named the way front-end packages are: lower-case and hyphenated, as an npm
package name must be and as Budgy's `budgy-web` is. The directory, the `name` in `package.json` and
the image all read the same. The difference in case from the .NET projects is deliberate: it marks
the one directory that is an npm project rather than a .NET one.

It sits outside the `.sln`, as `budgy-web` does: it is an npm project, and no .NET build step needs
it. Its Docker build context is its own directory, so the image never sees the .NET sources, and the
root `.dockerignore` problem `AGENTS.md` describes for the E2E images does not arise.

Rejected:

* **`PacmanManager.Web/`**, which matches the .NET projects' naming but not the idiom of the
  ecosystem the app is written in, and suggests a .NET project that is not there.
* **A `web/` or `src/web/` directory**, which would be the only top-level directory not named for
  the product.
* **An `.esproj` in the solution**, which makes `dotnet build` depend on Node for no benefit.

### Why the spec is committed and checked

The TypeScript client is generated from RepoHost's OpenAPI document, so something has to produce that
document at build time. Committing it, with a test that it is still compatible with the code, gives
the cheapest web build and makes *breaking* drift impossible to merge: a breaking change fails
RepoHost's tests until the document is regenerated, and the regenerated document then fails the web
app's typecheck wherever the app's use of the API broke. Additive changes cannot break the app, so
they do not force a regeneration and API pull requests do not bleed into the web build unless they
must. The diff to `openapi/v1.json` is also a readable record of how the contract changed. Only the
document is committed; the client is generated from it during the web build and gitignored, since
generated code is not committed here.

Rejected:

* **Failing on any difference.** This design's first version. Every API pull request, however small,
  would have to regenerate the document and could break the web build, coupling the two more tightly
  than their contract requires.
* **Generating the document, then the client, then building API and web independently.** Nothing is
  committed, but the web build then needs the .NET toolchain (pacman tooling, libalpm, a JRE) and a
  RepoHost build — `npm run dev` and the Dockerfile included — and every API change reaches the web
  build unchecked.
* **Committing both the document and the client.** The client is wholly derived from the document,
  so committing it adds review noise and nothing a reviewer could not already see in the document.

* **Generating the document inside the web image.** The web Dockerfile would need a .NET SDK stage
  that builds RepoHost — with pacman tooling, libalpm and a JRE for ANTLR — only to emit a JSON file,
  and `npm run dev` would need .NET installed too.
* **Budgy's approach**: regenerating by hand from a running RepoHost and committing the generated
  client. Nothing notices when the client falls behind the API.
* **`Microsoft.Extensions.ApiDescription.Server`**, which generates the document as part of
  `dotnet build`. It is built for `Microsoft.AspNetCore.OpenApi`; RepoHost uses Swashbuckle, whose
  documentation describes only its CLI, and changing document generators would change the document
  itself and the Swagger UI wiring for no gain here.

The document is generated in `Development` because `ConfigureSwaggerGenOptions` embeds
`Swagger:OpenIdConnectUrl`, which only the development settings define. Pinning the environment in
both the command and the test is what makes the comparison deterministic.

### Why `openapi-typescript` and `openapi-fetch`

`openapi-typescript` emits types and nothing else, and `openapi-fetch` is a small client typed by
them, with a middleware hook that fits the antiforgery and 401 rules exactly. Nothing generated runs
at runtime, so the generated file can be gitignored and regenerated on every build without a review
burden.

Rejected:

* **`swagger-typescript-api`**, which Budgy uses. It generates a class with a method per operation,
  so a large volume of runtime code is generated, and operation names come from the generator
  rather than the API's paths.
* **`@hey-api/openapi-ts`** and **orval**, which can also generate TanStack Query hooks. That moves
  query keys and invalidation into generated code, where this design wants them in hand-written
  factories that a mutation can name.

### Why the antiforgery token is fetched before every mutation

SpaHost validates antiforgery on every authenticated non-`GET` request, and issues the token through
`GET /antiforgery` as a cookie. Fetching it before each mutation, as Budgy does, is one extra
same-origin request on an action a human just clicked, and is never stale: a token is tied to the
cookie session, and caching it would need invalidation whenever that session changes — sign-in,
sign-out, or SpaHost ending a session it could not refresh.

### Why `/userinfo` decides anonymous

SpaHost answers `/userinfo` with `401` for a visitor without a cookie session, and with the user's
claims otherwise; it answers a proxied `/api` call with `401` only when a signed-in user's token can
no longer be refreshed (#183). The two `401`s therefore mean different things — "you never signed in"
and "your session died" — and only the second should send the user to sign in.

Rejected:

* **Budgy's rule, where any `401` redirects to sign-in.** Budgy has nothing to show an anonymous
  visitor. PacmanManager does: public repositories and their packages are readable anonymously, and
  the UI would hide what the API offers.
* **Probing `/api/…/users/me` alone**, treating its `401` as anonymous. RepoHost also answers `401`
  for a request that arrives with no token, and SpaHost signs a dead session out and proxies later
  requests anonymously. A user whose session died would be read as anonymous on the next page load
  and quietly shown the public view — exactly the failure #183 exists to prevent.

### Why the session resolves before the first render

Rendering straight away as anonymous and correcting once the session arrives would flash the wrong
app bar on every load. Worse, a signed-in user's private repository would briefly be fetched without
knowing who they are, and the loader could commit to a not-found page before the session arrived.
Two small same-origin requests in the root `beforeLoad` cost less than handling that everywhere.

### Why MUI

Budgy uses MUI, so its components, theming and testing patterns are already familiar. It is complete
enough that the feature milestones need no second library, its components carry
accessible roles and labels that the Testing Library and Playwright locators rely on, and its
CSS-variable colour schemes make Polish's dark mode a theme change rather than a restyle.

Rejected: **Mantine**, comparable in scope but new to the project's owner; **shadcn/ui on Radix and
Tailwind**, which copies component source into the repository to be owned and maintained here, and
brings a second styling system.

### Why MSW rather than swapping the client module

Budgy swaps its API module for a hand-written mock under Storybook through a package `imports`
condition. The real client and its middleware then never run in a story, and the mock has to track
the client's surface by hand. MSW intercepts at the network instead: stories and unit tests exercise
the real client, the real antiforgery middleware and the real error unwrapping, and handlers are typed
from the same schema the client is, so a stale handler fails the typecheck.

### Why routes are file-based

TanStack Router's file-based routing generates the route tree and its types from `src/routes`, with
automatic code splitting, and is what Budgy uses. A route is one file named for its path, which is
the easiest structure for the feature milestones to extend without touching a central route table.
Code-based routing was rejected for that central table.

### Why a `4xx` is never retried

A `4xx` is the server's considered answer — not found, forbidden, invalid — and asking again gets the
same answer three times slower. TanStack Query's default of three retries would make every not-found
page take seconds to appear. Network failures and `5xx` responses can be transient, and keep two
retries. Mutations never retry, because a retried `POST` can do its work twice.

### Why end-to-end tests are TypeScript Playwright against compose

The end-to-end tests live with the app they test, in its language, and use Playwright's own runner,
with its fixtures, trace viewer, UI mode and code generator. They run against the same compose stack
a developer uses day to day, so `compose.yaml` stays working because CI depends on it.

Rejected: **a .NET test project using Microsoft.Playwright and the existing Testcontainers
fixtures.** It would reuse the Keycloak, Postgres and RepoHost container code and run inside the
existing `test` job, but the web app's tests would live in a different language and project from the
web app, and would forgo Playwright's own test runner — its fixtures, projects and UI mode — which
the .NET bindings do not have. The
cost of the choice made is a separate CI job and a few Keycloak admin calls written again in
TypeScript.

### Why the stack is started outside Playwright

Playwright's `webServer` option manages one long-running process. A compose stack is several
containers plus a migrations container that runs and exits, and bringing it up in `globalSetup` would
couple Playwright to compose's lifecycle and hide build failures inside a test run. Keeping
`docker compose up -d --build` as an explicit step, run by the `pree2e` npm script before Playwright
starts, means CI shows a broken image build as a broken
build step, a developer can leave the stack running between test runs, and global setup only has to
wait for readiness.

### Why stories run as tests under Vitest

`@storybook/addon-vitest` turns each story, and its `play` function, into a Vitest test running in a
real browser. Component tests then run under the same runner and the same `npm test` as unit tests,
with no Storybook server to start. Rejected: the older `@storybook/test-runner`, which needs a built
or running Storybook and its own CI step.

### Why the web host is on 8081

Keycloak already has 8080 and RepoHost 8082 on the host. 8081 is the free port between them and
matches Budgy's web host, so the two projects' stacks look alike. Port 3000 is reserved for the Vite
dev server, which is why it appears in the Keycloak client's redirect URIs.
