# LiveMarkDownEditor

A live Markdown editor. This document tells Claude how to work in this repository.

## Source of Truth

Two documents are the **authoritative source of truth** for this project. When code and these
documents disagree, the documents win — fix the code, or update the documents *first* and then
the code.

- **[docs/UbiquitousLanguage.md](docs/UbiquitousLanguage.md)** — the shared vocabulary of the
  domain. Every type, method, namespace, and test name MUST use these terms exactly. Do not
  introduce a synonym for a term that already exists here. If you need a new concept, add it to
  this document first.
- **[docs/Invariants.md](docs/Invariants.md)** — the rules that must always hold true in the
  domain. Every invariant MUST be enforced in the domain model (guard clauses, value objects,
  aggregate roots) and MUST be covered by at least one test. If you discover a new rule, add it
  here first, then write a failing test, then implement.

Before starting any domain work, read both documents. When you finish a change that touches the
domain, verify both documents are still accurate and update them in the same change.

## Core Practices (non-negotiable)

### Domain-Driven Design (DDD)
- Model the domain first. Use the ubiquitous language from `docs/UbiquitousLanguage.md` everywhere.
- Encapsulate invariants inside the domain model — prefer value objects and aggregates that cannot
  be constructed in an invalid state. No anemic domain models.
- Keep the domain free of infrastructure and UI concerns. Dependencies point inward toward the domain.

### Test-Driven Development (TDD)
- **Red → Green → Refactor.** Always write a failing test before writing implementation code.
- Every invariant in `docs/Invariants.md` has at least one corresponding test.
- Do not write production code that is not driven by a failing test.

### Testing Stack
- **xUnit** is the test framework. Use `[Fact]` for single cases and `[Theory]` / `[InlineData]`
  for parameterized cases.
- **Shouldly** is the assertion library. Use `ShouldBe`, `ShouldThrow`, `ShouldContain`, etc.
  Do NOT use `Assert.*` — always assert with Shouldly.
- Name tests with the ubiquitous language. Prefer `MethodOrBehavior_Scenario_ExpectedOutcome`.

```csharp
[Fact]
public void Render_GivenEmptyDocument_ProducesEmptyHtml()
{
    var document = new MarkdownDocument("");

    var html = document.Render();

    html.ShouldBe("");
}
```

## Documentation
- All public and protected members MUST be documented with XML doc comments (`/// <summary>`).
- Private members are exempt.

## Conventions
- Target the domain in its own project, isolated from UI/infrastructure (Clean Architecture).
- Classes are sealed by default; use primary constructors where they fit.
- Keep classes small — aim under 200 lines, hard limit 500.

## Workflow
- Work on a feature branch, not `main`/`master`. Commit regularly.
- Only commit or push when the user asks.
