# Design Document Template

Status: **template**. Copy everything below the first horizontal rule into `docs/<name>.md` and fill
it in. [`user-management.md`](user-management.md) is the reference implementation of the format —
read it if a section here is unclear.

## How to use this file

* **Everything below the first horizontal rule is the template**, starting at `# ‹Title›`. Copy it
  whole, keep the section order, and drop only the sections that genuinely do not apply.
* **`> **Guidance.**` blockquotes are addressed to you, the author.** They are not part of the
  document. Delete every one of them before committing.
* **`‹text in guillemets›` is a placeholder.** Replace it, guillemets included. A committed design
  document contains no `‹` and no `>` guidance blocks.

## What the format is for

The shape exists to serve three co-equal requirements. When in doubt about where something belongs,
decide by asking which of these it serves:

1. **A human can review the plan for correctness.** The body states *what* will be built, so a
   reviewer can judge whether it is right without wading through justification.
2. **The reasons are preserved.** They move to the Appendix; they are never deleted. Later readers —
   the author six months on, and agents — need to know why something is the way it is, and which
   alternatives were weighed and rejected before it got there.
3. **An agent can cut implementation stories from it.** Each `###` under the implementation plan is
   one issue, already carrying its acceptance criteria and dependencies.

## The two rules that matter most

**A load-bearing constraint stays in the body, phrased as required behaviour.** A fact without which
an implementation would be written wrong is not rationale, even when it is grammatically a "why".
Do not send it to the Appendix because it explains a mechanism — restate it as behaviour and keep it
in the body. In the worked example, rather than explaining that `PTrampert.SimplePatch` enforces
`[Required]` only when a property is present, the body states the outcome in a table: `{}` is a
`200` that changes nothing, `{"displayName": null}` is a `400`. Only the argument for choosing that
library moved to the Appendix.

**Why footers are bulleted lists, one link per bullet** — never a comma-separated run-on line. Every
bullet is a link to an Appendix entry. Use the bulleted form even when there is only one link:

    **Why:**

    * [`CurrentUser` is a separate model](#why-currentuser-is-a-separate-model)
    * [`User` keeps its bare name](#why-user-keeps-its-bare-name)

A footer goes at the end of every specification section and every implementation story that has an
Appendix entry bearing on it. A section with no footer is one whose reasoning was never written
down — because it was self-evident, or because the specification was right first time. It is not
less settled than a section with three links: what settles the plan is the document landing on
`main`, not the presence of an argument for it.

## Restructuring an existing document into this format

**Preserve the exact heading text of any section that another document links to by anchor.** Other
design docs link into each other as `[…](packages-api.md#listing)`; renaming the heading breaks the
link silently. Grep the `docs/` directory for `<filename>#` before you rename anything, and if a
heading must change, fix every inbound link in the same commit.

---

# ‹Title›

Status: **‹design | accepted | implemented | superseded by `‹doc›.md`›**. ‹One sentence on what this
document is for — e.g. "This document is the source the implementation issues are cut from."›

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

> **Guidance.** Keep those two paragraphs and the deviation list close to verbatim; they are the
> contract between the body and the Appendix. If this document is one of a set, follow them with a
> short list of the siblings and a sentence on what depends on what — a reader's first question is
> whether they can start.

## Goals

> **Guidance.** Each bullet is an outcome a reader could check the finished work against, not a
> task list. **At least one** — a design document with no clear goal is not a design document.
> Six is a soft ceiling rather than a limit: needing more than six is usually a sign the work
> wants splitting across more than one design document.

* ‹outcome›

## Non-goals for this work

> **Guidance.** What the issues deliberately exclude, so they stay bounded. Each non-goal that is
> worth doing eventually gets a matching entry in [Deferred work](#deferred-work);
> cross-reference it.

Recorded here so the issues stay bounded; each has a follow-up in
[Deferred work](#deferred-work).

* **‹Non-goal›.** ‹One or two sentences.›

**Why:**

* [‹appendix entry›](#‹anchor›)

---

## ‹Specification section›

> **Guidance.** One `##` (or `###`) section per area of the design: routes, models, listing,
> a behaviour, a schema change, a migration. Name them for what they are, not "Design" or "Details".
>
> These sections are requirement-dense. Prefer a table or a list of assertions to a paragraph; every
> statement should be checkable by a reviewer and implementable without interpretation. Bold the
> constraints that are easy to get wrong (**must**, **and nothing else**, **never**). A code or JSON
> block is the right way to pin an exact shape.
>
> Argument does not belong here. If you catch yourself writing "because", either the sentence is a
> load-bearing constraint — restate it as required behaviour — or it belongs in the Appendix with a
> link from the Why footer.

| ‹Column› | ‹Column› |
| :--- | :--- |
| ‹value› | ‹value› |

* ‹Assertion.›

**Why:**

* [‹appendix entry›](#‹anchor›)

---

## Implementation plan

One issue per heading. Dependencies are noted; anything without a dependency can start immediately.

> **Guidance.** This section is what an agent reads to file issues, so each heading must stand on
> its own. Number them, and end each heading with the change level the PR title will carry —
> `PATCH`, `MINOR` or `MAJOR`. Cut each one to the size of a single pull request and prefer
> independent issues to a chain; see **Issue and pull request size** in `AGENTS.md`.

### ‹N›. ‹What this issue builds› — ‹PATCH | MINOR | MAJOR›

‹A sentence or two naming the concrete artefacts: the routes, types, columns or files.›

*Constraints:* ‹Only where there is a real trap — a rule that would be silently violated by a
reasonable implementation. Point at the specification section that governs the issue rather than
restating it. Omit this line entirely when there is no trap.›

*Acceptance:* ‹The tests that must exist and what they assert. Be specific enough that a reviewer
can tell a passing implementation from a plausible one — "asserted against the raw response body
rather than a deserialised model" beats "tested".›

*Depends on:* ‹issue numbers, or `nothing`. Note anything that must land *before* something else.›

**Why:**

* [‹appendix entry›](#‹anchor›)

---

## Deferred work

Worth filing as issues, but explicitly out of scope for the work above.

> **Guidance.** The landing place for every non-goal that is worth doing eventually. Say what would
> make it possible, and whether it is additive — that is what tells a future reader they are not
> boxed in.

* **‹Deferred item›** — ‹what it would take, and what currently stands in the way.›

---

## Appendix

The argument for each decision in the plan, including what was considered and rejected. Nothing
here adds a requirement — the plan above is the specification, and it is the plan, once merged,
that is settled. These entries are the supporting reasoning: if implementation suggests a different
choice, they are what to argue with, in an issue, rather than something to quietly depart from.
A deviation that is agreed to updates the plan, this Appendix and the dependent issues together, as
the **How to read it** paragraph at the top sets out.

> **Guidance.** One `###` entry per decision, titled so the Why-footer link reads as a sentence
> — "Why `CurrentUser` is a separate model", "There is no `IUserAccessPolicy`". Every entry is the
> target of at least one Why footer; an entry nothing links to is either a missing footer or a
> decision that belongs in the body.
>
> **Record what was rejected, not only what was chosen.** The rejected alternative and the reason it
> lost are the part a future reader cannot reconstruct, and the part that stops the same debate
> being had again. An entry that only restates the decision is not worth its space.
>
> Do not put a requirement here. If an implementer would get the code wrong by not reading an entry,
> that fact belongs in the body as behaviour.

### ‹Why the thing is the way it is›

‹The argument. What was considered, what was chosen, and why the alternatives lost.›
