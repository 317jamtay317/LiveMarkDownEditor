# Invariants

The rules that must **always** hold true in the LiveMarkDownEditor domain. These are
**authoritative**: each invariant must be enforced inside the domain model and covered by at least
one xUnit + Shouldly test.

Use the terms defined in [UbiquitousLanguage.md](UbiquitousLanguage.md).

> Status: living document. Add invariants as the domain grows. Add the rule here first, then write
> a failing test, then implement.

## Format

Each invariant has a stable identifier (`INV-###`), a statement, and a note on how it is enforced
and tested.

## Invariants

### INV-001 — A Markdown Document always has non-null source text
- **Statement:** A Markdown Document's source text is never `null`. An empty document is represented
  by an empty string, not `null`.
- **Enforced by:** Constructor guard on the Markdown Document.
- **Tested by:** _(add test reference)_

### INV-002 — Rendering is deterministic
- **Statement:** Rendering the same Markdown Document source text always produces the same Rendered
  Output.
- **Enforced by:** Pure Render operation with no hidden state.
- **Tested by:** _(add test reference)_

<!--
Add new invariants above using the next INV-### number. Never reuse a retired number.
Every invariant MUST have at least one corresponding test before it is considered done.
-->
