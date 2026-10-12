# Web UI: Repositories

Status: **design**. This document is the source the **Web UI: Repositories** issues are cut from. It
builds its screens on [Web UI Architecture](../web-ui-architecture/web-ui-architecture.md) and does
not re-decide anything that document fixes.

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

It depends on the **Web UI: Walking Skeleton** milestone being complete: the generated client, the
session, `requireUser`, error handling and the app shell are all used here as they stand.

## Goals

* Anyone, signed in or not, can list the repositories they can see, and narrow and order that list
  by every filter and sort the API supports.
* Anyone can open a repository they can see, and copy a `pacman.conf` section that points a client
  at it through RepoHost's public address.
* A signed-in user can create a repository, choosing whether it is public or private.
* A user can rename a repository, change its visibility, or delete it, wherever the API says they
  may — and is told what each of those does to the clients already using it before it happens.
* The UI never decides for itself whether a caller may change a repository; it shows the controls
  the API's answer permits.

## Non-goals for this work

Recorded here so the issues stay bounded; each has a follow-up in
[Deferred work](#deferred-work).

* **Packages.** A repository's packages are not listed, uploaded or deleted here; that is the
  **Web UI: Packages** milestone.
* **Credentials in the `pacman.conf` snippet.** A private repository's snippet says that the client
  must authenticate, but carries no credentials and offers no way to make any.
* **Checking a name's availability before submitting.** A taken name is reported when the create or
  update is refused, not as it is typed.
* **Choosing architectures.** Every repository supports every supported architecture, as the API
  defaults it today; the UI shows the value but never sends a different one.
* **API changes beyond those [listed below](#api-changes).** In particular there is no server-side
  confirmation of a rename, a visibility change or a delete — those confirmations are the UI's
  alone.

**Why:**

* [confirmations live in the UI only](#why-confirmations-live-in-the-ui-only)

---

## API changes

RepoHost gains four small things. Each is additive, so none of them breaks the committed OpenAPI
document, but each one regenerates it so the web app can use what it adds.

### `Repository.permissions`

Every `Repository` RepoHost returns — the listing, `GET` by id, and the responses to `POST` and
`PUT` — carries what the caller may do to it:

```jsonc
{
  "id": "0197…",
  "name": "myrepo",
  // …existing fields…
  "permissions": { "update": true, "delete": true }
}
```

| Field | `true` exactly when |
| :--- | :--- |
| `update` | `RepositoryAccessPolicy.CheckUpdate(repository, actor)` is `Allowed` |
| `delete` | `RepositoryAccessPolicy.CheckDelete(repository, actor)` is `Allowed` |

* Both fields are **always present**, `false` included. An anonymous caller gets `false` for both.
* The values come from the **same policy calls** `UpdateRepositoryAsync` and `DeleteRepositoryAsync`
  enforce with, so ownership and the credential's scope both count: a token without
  `pacman-manager:repositories:update` sees `update: false` on its own repositories.
* They are computed per response for the actor making the request, and **never stored**.
* The policy decides from the repository's owner id and visibility; computing `permissions` must not
  add a query per repository to a listing.

**Why:**

* [permissions come from the API](#why-permissions-come-from-the-api)
* [`update` and `delete` are separate](#why-update-and-delete-are-separate)

### `Repository.serverUrl` and the `PublicUrl` setting

RepoHost gains a top-level `PublicUrl` setting: the absolute URL a `pacman` client reaches RepoHost
at, such as `https://packages.example.com/`.

| `PublicUrl` | Startup | `serverUrl` |
| :--- | :--- | :--- |
| absent or empty | starts | `{request root}pacman/$repo/$arch` |
| an absolute `http` or `https` URL | starts | `{PublicUrl}pacman/$repo/$arch` |
| anything else | **fails**, through options validation, naming the value | — |

* The **request root** is the current request's `Scheme`, `Host` and `PathBase`, read **after**
  `UseForwardedHeaders` — so a trusted proxy's `X-Forwarded-Proto` sets the scheme, but the host is
  always the one the request was addressed to.
* A configured `PublicUrl` always wins over the request root.
* Whichever root is used, it is normalised to end in exactly one `/` before `pacman/` is appended, so
  `https://example.com/pm` and `https://example.com/pm/` both give `https://example.com/pm/pacman/$repo/$arch`.
* `serverUrl` is therefore **always present**.
* `$repo` and `$arch` are **literal** in the value, exactly as `pacman.conf` writes them; they are
  not replaced with the repository's name or an architecture.
* `serverUrl` is built **each time a repository is returned**. It is never stored, so changing
  `PublicUrl` changes every repository's `serverUrl` on the next request.
* `appsettings.json` carries `"PublicUrl": ""`, so the key is discoverable. `compose.yaml` sets
  `PublicUrl: "http://localhost:8082/"` on `pacmanmanager.repohost`.
* With `PublicUrl` set, `serverUrl` is the same for every caller, signed in or not. Without it, it
  depends on how the request reached RepoHost: a request proxied by SpaHost carries SpaHost's
  upstream address (`http://pacmanmanager.repohost:8080/` in compose), so a deployment the web UI is
  used from must set `PublicUrl` for the snippet to be usable.

**Why:**

* [`PublicUrl` is configured, with the request as a fallback](#why-publicurl-is-configured-with-the-request-as-a-fallback)
* [`serverUrl` is a field on `Repository`](#why-serverurl-is-a-field-on-repository)

### `GET /api/v1/Architectures`

| Method | Path | Auth | Success |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/v1/Architectures` | Anonymous | `200` array of architectures |

```jsonc
[
  { "value": "x86_64", "displayValue": "64-bit Intel/AMD (x86_64)" }
]
```

* The response is a **plain JSON array**, not a `PaginatedResponse`, and the route takes no
  pagination, filter or sort parameters.
* It lists exactly `PacmanRepositoryValidationConstants.SupportedArchitectures`, in that order — the
  same list `[SupportedArchitecture]` validates against, so the two cannot disagree.
* `displayValue` is `<friendly name> (<value>)`. The friendly names live beside
  `SupportedArchitectures` in `PacmanManager.Entities`, and a supported architecture without one is a
  test failure.

**Why:**

* [architectures are a plain array](#why-architectures-are-a-plain-array)

### `409` is declared on create and update

`POST /api/v1/Repositories` and `PUT /api/v1/Repositories/{id}` already answer `409` when the name is
taken. Both gain `[ProducesResponseType(StatusCodes.Status409Conflict)]`, so the OpenAPI document and
the client's generated types know the response exists.

---

## Navigation

The walking skeleton's shell — an `AppBar` above a `Container` — is **unchanged**. There is no
navigation menu: no drawer, and no links in the app bar.

* The product name in the app bar links to `/`, which [redirects](#routes) to the repository list.
* Every page **below** the list shows MUI `Breadcrumbs` above its heading, starting from
  **Repositories**, which links to `/repositories`. The last crumb is the current page and is not a
  link.

  | Page | Breadcrumbs |
  | :--- | :--- |
  | `/repositories` | none |
  | `/repositories/new` | Repositories / New repository |
  | `/repositories/$repositoryId` | Repositories / *the repository's name* |
  | `/repositories/$repositoryId/edit` | Repositories / *the repository's name* / Edit |

* A crumb naming a repository shows its current name, from `repositoryQuery(id)`.

**Why:**

* [there is no navigation menu yet](#why-there-is-no-navigation-menu-yet)

---

## Routes

| Path | Who | Shows |
| :--- | :--- | :--- |
| `/` | anyone | redirects to `/repositories` |
| `/repositories` | anyone | the [repository list](#the-repository-list) |
| `/repositories/new` | signed in (`requireUser`) | the [create form](#creating-a-repository) |
| `/repositories/$repositoryId` | anyone who can see it | the [repository page](#the-repository-page) |
| `/repositories/$repositoryId/edit` | signed in (`requireUser`) | the [edit form](#editing-a-repository) |

* A repository is addressed by **id**, so renaming it never changes its URL.
* Pages below the list carry [breadcrumbs](#navigation) back to it.
* `src/queries/repositories/` holds `repositoryListQuery(search)`, `repositoryQuery(id)` and the
  create, update and delete mutation hooks; `src/queries/architectures/` holds
  `architecturesQuery()`; `src/queries/users/` holds `userListQuery(search)` and `userQuery(id)`.
  Keys and invalidation follow [Routing and data](../web-ui-architecture/web-ui-architecture.md#routing-and-data).
* An edit page for a repository whose `permissions.update` is `false` shows the
  [`403` message](../web-ui-architecture/web-ui-architecture.md#error-handling) in place of the form.

**Why:**

* [repositories are addressed by id](#why-repositories-are-addressed-by-id)
* [`/` redirects to the list](#why--redirects-to-the-list)

---

## The repository list

`/repositories` shows one page of `GET /v1/Repositories`. Its search params are the API's, as the
architecture document requires:

| Search param | Sent as | Default |
| :--- | :--- | :--- |
| `nameContains` | the same | absent |
| `isPublic` | the same | see [Visibility](#filters) |
| `ownerId` | the same | absent |
| `architecture` | the same | absent |
| `sortBy` | the same | absent (`Name`) |
| `direction` | the same | absent (the field's default) |
| `offset` | the same | `0` |
| `pageSize` | the same | `50` |

* The list is a table with the columns **Name** (linking to the repository's page), **Owner** (the
  display name), **Visibility** (a Public or Private chip), **Architectures** (the
  `displayValue`s), **Created** and **Updated**.
* Changing a filter, the sort or the page size resets `offset` to `0`.
* The name search updates the URL **300 ms after typing stops**, not on every keystroke.
* A signed-in user sees a **New repository** button linking to `/repositories/new`. An anonymous
  visitor does not.

### Filters

Every filter follows one rule: **a filter with one possible value or none is shown disabled, set to
"All"**. The one exception is visibility for an anonymous visitor.

| Filter | Options | Disabled when | Shown while disabled |
| :--- | :--- | :--- | :--- |
| Name | free text | never | — |
| Visibility | All, Public, Private | the visitor is anonymous | **Public** |
| Owner | typeahead over users, the signed-in user first | `GET /v1/Users?pageSize=1` reports `total` ≤ 1 | **All** |
| Architecture | All, then each of `GET /v1/Architectures` by `displayValue` | that array has ≤ 1 entry | **All** |

* **Visibility.** Signed in, it defaults to **All** (`isPublic` absent). Anonymous, it is disabled on
  **Public** and sends **no** `isPublic`: the API already shows an anonymous caller public
  repositories only.
* **Owner.** An autocomplete that queries `GET /v1/Users?displayNameContains=<text>&pageSize=20`,
  300 ms after typing stops, and sets `ownerId` to the chosen user's id. There is no separate
  "mine" control.
* For a signed-in user, the autocomplete's **first option is always themselves**, labelled with
  their display name and "(you)" and followed by a divider, built from `session.user` rather than
  the API's answer:
  * with no text typed, it is shown above the API's first page of users;
  * with text typed, it is shown only when their display name contains the text, case-insensitively;
  * the signed-in user is removed from the API's results, so they never appear twice.
* Selecting it sets `ownerId` to `session.user.id`, and the field then shows the "(you)" label.
* When the page loads with an `ownerId`, the autocomplete shows that user's display name, fetched
  with `GET /v1/Users/{id}`. If that is a `404`, `ownerId` is removed from the search params.
* A disabled filter never sends a value. Its state is a consequence of the data, so the list still
  answers exactly as an unfiltered one would.

**Why:**

* [single-option filters are disabled, not hidden](#why-single-option-filters-are-disabled-not-hidden)

### Sorting and paging

* The **Name**, **Created** and **Updated** column headers sort the list, as MUI's
  `TableSortLabel`. Clicking the active header reverses `direction`; clicking another sets `sortBy`
  to it and removes `direction`, so the field's default applies. There is no other sort control.
* With no `sortBy` in the URL, **Name** shows as the active header; with no `direction`, the arrow
  shows that field's default direction.
* Paging uses the response's `total` and `offset`, with page sizes 25, 50 and 100.

**Why:**

* [the list sorts from its column headers](#why-the-list-sorts-from-its-column-headers)

### Empty states

An `EmptyState` component in `src/components` — a heading, a message and an optional action — is
used for every empty list:

| Situation | Heading | Action |
| :--- | :--- | :--- |
| Signed in, no filters applied, no repositories | No repositories yet | **Create a repository**, to `/repositories/new` |
| Any filter applied, no results | No repositories match | **Clear filters**, removing every filter search param |
| Anonymous, no filters applied, no repositories | No public repositories | none |

* "No filters applied" means `nameContains`, `isPublic`, `ownerId` and `architecture` are all
  absent. Disabled filters never count as applied.
* The component knows nothing about repositories; the list route chooses the text and the action.

---

## The repository page

`/repositories/$repositoryId` loads `repositoryQuery(id)`. A `404` is the route's not-found
component, whatever the reason the API had.

The page is a compact header that the **Web UI: Packages** milestone's package list will sit
beneath. Top to bottom:

1. the [breadcrumbs](#navigation);
2. a title row: the name as the page's `h1` with a Public or Private chip beside it, and on the
   right
   * an outlined **Edit** button linking to `/repositories/$repositoryId/edit`, only when
     `permissions.update` is `true`;
   * an outlined, error-coloured **Delete** button opening the
     [delete dialog](#deleting-a-repository), only when `permissions.delete` is `true`;
3. one row of label-over-value details: **Owner** (display name), **Architectures** (the
   `displayValue`s), **Created** and **Updated**;
4. the [`pacman.conf` section](#the-pacmanconf-section), collapsed.

**Why:**

* [the repository page is a header for its packages](#why-the-repository-page-is-a-header-for-its-packages)

### The `pacman.conf` section

An MUI `Accordion` titled **Use this repository**, with the secondary text "The pacman.conf section
for this repository". It is **collapsed** each time the page loads; its state is not remembered.
Expanded, it shows an instruction to add the section to `/etc/pacman.conf` and run `pacman -Sy`, a
**Copy** button, and a read-only code block holding exactly:

```ini
[<name>]
SigLevel = Optional TrustAll
Server = <serverUrl>
```

* `<name>` is the repository's name as stored, and `<serverUrl>` the field's value verbatim.
* **Copy** copies the block's text to the clipboard, then reads **Copied** for a few seconds.
* For a **private** repository, a note below the block says that `pacman` must authenticate to read
  it, and that the `Server` line needs credentials. The note does not generate or link to any.

**Why:**

* [`serverUrl` is a field on `Repository`](#why-serverurl-is-a-field-on-repository)

---

## Creating a repository

`/repositories/new` is a form with:

| Field | Control | Default | Sent as |
| :--- | :--- | :--- | :--- |
| Name | text | empty | `name` |
| Visibility | Public / Private choice | **Private** | `isPublic` |
| Architectures | the `displayValue`s from `GET /v1/Architectures`, **disabled** | all of them | not sent |

* On success it navigates to the new repository's page.
* **Cancel** returns to `/repositories`.

### Name validation

The name is checked in the browser before submitting, and the form does not submit while it fails.
**The rules are not written in the web app.** They are read from `WriteRepositoryRequest.name` in the
committed OpenAPI document, which RepoHost generates from the attributes it validates with:

| Rule | From the document | Message |
| :--- | :--- | :--- |
| required | `required`, and `minLength: 1` | Name is required. |
| maximum length | `maxLength` | Name must be 255 characters or fewer. |
| pattern | `pattern`, translated | Use letters, digits and `@ _ + - .`; `-` and `.` cannot start or end the name, or appear twice in a row. |

* A change to `[Required]`, `[MinLength]`, `[MaxLength]` or `[RegularExpression]` on
  `WriteRepositoryRequest` reaches the form by regenerating `openapi/v1.json`, with **no web change**.
  The length in the maximum-length message is the document's value, not a literal.
* The pattern's message is the one piece written in the web app, since the document carries no
  message for a pattern.
* A `409` from the API is shown **on the name field** as "A repository with this name already
  exists." It is never shown as a `ProblemDetailsAlert`.
* Any other error follows [Error handling](../web-ui-architecture/web-ui-architecture.md#error-handling).

**Why:**

* [validation rules come from the OpenAPI document](#why-validation-rules-come-from-the-openapi-document)

### Validation rules from the OpenAPI document

`npm run generate` gains a step, `scripts/generate-validation.ts`, that writes the gitignored
`src/api/validation.gen.ts` from `openapi/v1.json`. It is general, not specific to repositories:

* For **every** request-body schema, it emits each property's `required`, `minLength`, `maxLength`
  and `pattern`, keyed by schema and property name, typed so that a schema or property that does not
  exist is a type error.
* Each `pattern` is translated from .NET to JavaScript syntax:

  | .NET | JavaScript |
  | :--- | :--- |
  | `\A` | `^` |
  | `\z` | `$` |
  | anything else | unchanged |

* The translated pattern is compiled with `new RegExp(pattern, "u")` **during generation**. A
  pattern containing a .NET-only construct — `\Z`, `\G`, an inline option group such as `(?i)`, an
  atomic group `(?>…)`, a conditional `(?(…)…)`, a possessive quantifier, or a `\p{Is…}` block name —
  or one that does not compile, **fails generation**, naming the schema, the property and the
  pattern. It is never silently dropped or loosened.
* A small helper in `src/api` turns a property's rules into a field validator producing messages of
  the kind shown in [Name validation](#name-validation), so a form names its schema, property and
  field label, and writes only the pattern's message.
* The translator has unit tests covering each row of the table and each rejected construct, and a
  test asserts that the generated `WriteRepositoryRequest.name` pattern accepts and rejects the same
  names as RepoHost's `RegularExpressionsTests`.

**Why:**

* [validation rules come from the OpenAPI document](#why-validation-rules-come-from-the-openapi-document)

---

## Editing a repository

`/repositories/$repositoryId/edit` is the create form's fields, filled with the repository's current
values, and uses the same [name validation](#name-validation) and `409` handling.

* It sends `PUT /v1/Repositories/{id}` with the edited `name` and `isPublic` and the repository's
  **existing** `supportedArchitectures`, unchanged.
* **Save** is disabled until the name or the visibility differs from the stored value.
* On success it navigates to the repository's page. **Cancel** returns there without saving.

### Confirming consequential changes

Saving a change that affects clients already using the repository first opens a confirmation dialog
listing each consequence that applies:

| Change | Consequence shown |
| :--- | :--- |
| the name changed | Every `pacman.conf` using this repository must change its section name to the new name, or `pacman` will stop finding it. |
| Private → Public | Anyone, signed in or not, will be able to read every package in this repository. |
| Public → Private | Clients that read this repository without credentials will lose access. |

* The dialog's **Save** sends the request; its **Cancel** returns to the form with the edits intact.
* The API is not told whether the user confirmed. It performs the update as it does today.

**Why:**

* [confirmations live in the UI only](#why-confirmations-live-in-the-ui-only)

---

## Deleting a repository

**Delete** on the repository page opens a dialog that:

* says the repository and every package in it will be permanently deleted, and that clients using it
  will fail;
* asks the user to type the repository's name, and keeps its **Delete** button disabled until the
  typed text equals the name **exactly**, case included;
* on success, navigates to `/repositories`; on failure, shows the error in the dialog and stays open.

The API is not told about the confirmation, and deletes as it does today.

**Why:**

* [confirmations live in the UI only](#why-confirmations-live-in-the-ui-only)

---

## Implementation plan

Not yet written. The issues are cut once the wireframes on the Penpot **Repositories** page are
agreed, since the components they show decide how the UI work divides.

---

## Deferred work

Worth filing as issues, but explicitly out of scope for the work above.

* **A navigation scheme** — a drawer, app-bar links or something else, chosen when a second
  section of the app exists. Breadcrumbs stay useful under any of them, so it is additive.
* **Packages on the repository page** — the **Web UI: Packages** milestone. The repository page is
  where a package list goes; nothing here is in its way.
* **Credentials in the `pacman.conf` snippet** — the **Web UI: Account and Access Tokens**
  milestone. Once a user can create a token in the UI, a private repository's snippet can offer to
  fill in the `Server` line's credentials. Additive: the snippet's note is where it goes.
* **Checking name availability as it is typed** — would need an API route answering whether a name
  is taken, and a decision about whether that may leak the existence of a private repository.
* **Trusting `X-Forwarded-Host`** — [#225](https://github.com/PaulTrampert/PacmanManager/issues/225).
  Would let RepoHost behind a proxy build a correct `serverUrl` without `PublicUrl`. It changes
  which forwarded headers [Basic Auth](../basic-auth/basic-auth.md#trusted-forwarded-header-sources)
  trusts, so it is designed there. Additive: a configured `PublicUrl` still wins.
* **Choosing architectures** — once RepoHost supports more than one, the create and edit forms'
  disabled architecture control becomes enabled. The control and `GET /v1/Architectures` already
  exist, so this is additive.

---

## Appendix

The argument for each decision in the plan, including what was considered and rejected. Nothing
here adds a requirement — the plan above is the specification, and it is the plan, once merged,
that is settled. These entries are the supporting reasoning: if implementation suggests a different
choice, they are what to argue with, in an issue, rather than something to quietly depart from.
A deviation that is agreed to updates the plan, this Appendix and the dependent issues together, as
the **How to read it** paragraph at the top sets out.

### Why permissions come from the API

[Session and anonymous visitors](../web-ui-architecture/web-ui-architecture.md#session-and-anonymous-visitors)
requires that whether a signed-in user may act on a repository is "decided by the data the API
returns, never re-derived in the UI". Comparing `owner.id` with `session.user.id` would be re-deriving
it, and would also be wrong: RepoHost's rule is ownership **and** the credential's scope, and a
session whose token lacks `pacman-manager:repositories:delete` would be shown a Delete button that the API refuses.
Taking the field from the same `RepositoryAccessPolicy` calls the writes enforce with means the UI and
the API cannot disagree, and a future rule — an administrator, a shared repository — reaches the UI
without a UI change.

### Why `update` and `delete` are separate

A single `canModify` was considered. Scopes grant update and delete independently, so one boolean
would have to be either "both" (hiding Edit from a token that may update) or "either" (showing Delete
to a token that may not). An object also leaves room for later actions, such as publishing packages,
without another top-level field.

### Why `PublicUrl` is configured, with the request as a fallback

RepoHost cannot reliably learn its public address from the request that asks for it. The web UI's
requests arrive through SpaHost's proxy, addressed to `http://pacmanmanager.repohost:8080`; RepoHost
trusts only `X-Forwarded-For` and `X-Forwarded-Proto` ([Basic Auth](../basic-auth/basic-auth.md#trusted-forwarded-header-sources)),
not `X-Forwarded-Host`; and a deployment may put the `/pacman` routes behind a different host or path
from the API entirely. So a configured `PublicUrl` is the authoritative answer, and always wins.
Configuring it on SpaHost instead was rejected because the address belongs to RepoHost, which every
client — not only the web UI — may want to learn it from.

Without it, the request's own root is used rather than omitting the field or failing startup. A
caller that reaches RepoHost directly — the CLI, Swagger, a deployment with no proxy in front — gets a
correct URL with no configuration at all, an existing deployment keeps working after the upgrade, and
the UI has a single shape to render instead of a "not configured" state. The cost is that a web UI
behind SpaHost shows SpaHost's upstream address until `PublicUrl` is set, which is why the setting
exists and why compose sets it. Trusting `X-Forwarded-Host` to close that gap was left out of this work
([#225](https://github.com/PaulTrampert/PacmanManager/issues/225)):
widening which forwarded headers are believed is a change to [Basic Auth](../basic-auth/basic-auth.md)'s
design, not this one's.

### Why `serverUrl` is a field on `Repository`

A deployment-wide endpoint returning `PublicUrl`, with the UI appending `pacman/$repo/$arch`, was
considered. It would put knowledge of RepoHost's `/pacman` route in the web app, which would then
break silently if the route moved. Building the whole URL in RepoHost keeps the route and the URL that
reaches it in one place.

It is built when the repository is returned, never stored at create time, so that changing
`PublicUrl` — moving the server, adding TLS — fixes every repository at once instead of leaving each
with the address it was created under.

### Why architectures are a plain array

Every other listing takes `PaginationParams`, a filter and `SortOptions`, and returns a
`PaginatedResponse`. The supported architectures are a small, static, server-defined list: paging,
filtering or sorting it is ceremony a client would have to undo to fill a single dropdown. The
exception is limited to lists of that kind; anything that grows with data stays paginated.

The endpoint exists at all, rather than the web app hard-coding `x86_64`, so that adding an
architecture to RepoHost enables the filter and the form control with no web change.

### Why single-option filters are disabled, not hidden

Hiding a filter that cannot narrow anything was the alternative. Showing it disabled keeps the layout
stable as a deployment grows — a second user, a second architecture — and tells the visitor that the
dimension exists. "All" rather than the lone value is shown because selecting the value would send a
filter, and a filter that changes nothing still makes the list look filtered and the empty state say
"No repositories match".

Anonymous visibility is the exception because "All" would be untrue: an anonymous visitor can only
ever see public repositories, and saying so explains why their list is shorter than a colleague's.

### Why confirmations live in the UI only

A rename, a visibility change and a delete are all things the owner is entitled to do, and the API
already does them. Requiring confirmation in the API — a typed name in the `DELETE` body, say — would
protect CLI callers too, but it would change three routes and break the contract for a protection
whose real audience is a person clicking in a browser. It can be added later without changing the UI.

### Why there is no navigation menu yet

Two schemes were drawn and set aside: a **Repositories** link in the app bar, and a permanent drawer
on the left. This milestone has one section — the repository list and the pages beneath it — so a
menu would hold a single item that duplicates the product name's link, while committing every later
milestone to a scheme chosen before anyone knew what those sections were. Breadcrumbs give the only
navigation these pages need, back up the hierarchy, without that commitment. The scheme is chosen
when a second section arrives; see [Deferred work](#deferred-work).

### Why the list sorts from its column headers

A separate sort dropdown with a direction toggle, beside the filters, was the first plan. Sortable
column headers are the convention for a data table, put the sort beside the data it orders, and keep
the filter bar for filters. The cost is a **Created** column, which the list had not otherwise
shown, so that it can be sorted on.

### Why the repository page is a header for its packages

The page's long-term job is managing packages, which arrive in the next milestone; most visits will
be for that, not for configuring `pacman`. The details are therefore a single row rather than a card
or a description list, and the `pacman.conf` section — about 240 px expanded — is collapsed so it
never pushes the package list down. Showing it expanded was the first wireframe; a dialog opened from
a button beside Edit and Delete was also considered, and rejected because an accordion keeps the
snippet in the page, where it is found by reading rather than by guessing at a button. The collapsed
state is not remembered: a visitor who needs the snippet needs it once per machine, and remembering
it would make the page's height depend on history.

### Why repositories are addressed by id

[Routing and data](../web-ui-architecture/web-ui-architecture.md#routing-and-data) illustrates the
detail query as `repositoryQuery(name)`. The API has `GET /v1/Repositories/{id}` and no exact lookup
by name, and a name in the URL would change on every rename, breaking bookmarks and links. The id is
stable, needs no API change, and the name is on the page anyway.

### Why `/` redirects to the list

The walking skeleton's home page is a placeholder. Repositories are what every visitor, anonymous or
not, comes to see, and the product name in the app bar links to `/`. A dashboard was considered and
left until there is something to put on it.

### Why validation rules come from the OpenAPI document

Mirroring the rules by hand in the web app — a constant holding the pattern and the length, with
the same test cases on both sides — was the first plan. It catches drift only when someone remembers
to update both, and the project owner preferred one source of truth. RepoHost's validation
attributes already reach the OpenAPI document, which the web app is already generated from, so
reading them there adds one generation step and no new contract.

The pattern needs translating because Swashbuckle copies the `[RegularExpression]` verbatim, in .NET
syntax: `\A` and `\z` are not anchors in JavaScript, where `\A` is an error under the `u` flag. Only
the anchors are translated because they are the only .NET-only syntax the API uses; every other
construct either means the same in both or is refused at generation. Refusing is the point: a
pattern quietly mistranslated would accept names the API rejects, or reject names it accepts, and
nobody would notice until a user did.

The pattern's message stays in the web app because the OpenAPI document has nowhere to carry one.
Emitting it as a vendor extension from a Swashbuckle schema filter was considered and left until a
second pattern needs a message.
