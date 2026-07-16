# Roadmap

Candidate features for LiveMarkDownEditor, kept as a checklist so work can be picked up and crossed
off. This is a **backlog, not a commitment** — nothing here is designed until it earns a term in
[UbiquitousLanguage.md](UbiquitousLanguage.md) and a rule in [Invariants.md](Invariants.md).

> Status: living document. When an item is picked up, follow the usual order — define the terms,
> add the invariant, write the failing test, then implement. When an item ships, delete its line
> from this file; the docs and tests become its record.

Items are grouped by kind and ordered roughly by value for effort within each group. Some reference
work that is still in review, noted inline.

## Closes a gap the docs or code already point at

- [ ] **Find and Replace.** INV-016 currently states outright that Find "offers no replace". Finding
      and stepping through Matches already works; replacing is a real edit that Captures back into
      the Markdown Document, so it reuses machinery rather than adding any. Retire that clause of
      INV-016 as part of the change.
- [ ] **Delete Row / Delete Column, and column alignment.** The Table Formatting Actions grow a
      Table but cannot shrink one, and per-column alignment already survives a Round-Trip without
      being reachable from the command bar. Must keep the Table rectangular. *(Builds on the Table
      Formatting Actions, in review.)*
- [ ] **Requery conflict-bar commands when a Conflict is raised.** `RelayCommand.CanExecuteChanged`
      delegates to `CommandManager.RequerySuggested`, which only fires on user input — so a Conflict
      raised by the file watcher leaves its buttons rendered disabled until the user's next mouse
      move. Harmless in practice, wrong on paper.
- [ ] **Decide how canonical-Markdown churn is shown in a Conflict Difference.** Capture emits
      canonical Markdown (INV-005), so once the Visual Document is edited its blank lines can differ
      from the Watched File's, and those lines show as differences. It is truthful — that is what a
      save would write — but noisier than a plain text comparison. Needs a product decision before
      any code. *(Builds on the Conflict Difference, in review.)*

## Rounds out the Formatting Actions

- [ ] **Heading level picker.** Headings are the backbone of Sections, the Outline, and Folding, and
      are the one structural construct with no command-bar action.
- [ ] **List toggles** — Unordered, Ordered, and Task Marker.
- [ ] **Insert Link (Ctrl+K) and Insert Image.**
- [ ] **Block Quote and strikethrough actions.**

## Quality of life

- [ ] **Export the Rendered Output as HTML.** Render already produces it for interoperability;
      nothing in the UI reaches it.
- [ ] **Print / export as PDF.** A Visual Document is a `FlowDocument`, which WPF prints natively.
- [ ] **Copy as rich text**, so a selection pastes formatted into Word or Outlook.
- [ ] **Restore the Workspace at startup, and a recent-files list.** Reopen the previous session's
      Tabs, offer an MRU list, and register a Windows Jump List. *(Pairs with the Startup Document
      and file-association work, in review.)*
- [ ] **Smart paste.** A URL pasted over a selection becomes a Link; an image on the clipboard is
      written beside the Watched File and inserted as an Image; HTML converts to Markdown.
- [ ] **Status bar** — word and character count, reading time, caret line and column, and the
      Current Section.
- [ ] **Add to Dictionary.** A user dictionary the Dictionary consults, so a Misspelling can be
      accepted permanently. The Misspelling context menu already exists for Spelling Suggestions.
- [ ] **Ctrl+Click to follow a Link** — a URL to the browser, a relative `.md` Link into a new Tab.

## Bigger swings

- [ ] **Highlight what changed on a live reload.** The clearest expression of what makes this editor
      different: INV-007 already reloads the Visual Document when the Watched File changes under a
      clean Editor Session, but the change lands invisibly. Briefly highlighting the changed Sections
      would make "another user or an AI just edited your file" something the user can *see*.
      *(The Conflict Difference, in review, supplies the comparison this needs.)*
- [ ] **Syntax highlighting inside a Code Block.** The language tag already survives a Round-Trip.
      Colouring is view-only, so it fits the read-only overlay pattern Code Shading established.
- [ ] **Folder Workspace.** A file tree for opening a directory of Markdown Documents, turning the
      editor into a lightweight knowledge base. The largest item here — it needs real domain work
      before any UI.
- [ ] **Footnotes and definition lists.** The notable Markdig-supported constructs still missing
      from INV-004's supported set. Each lands one tested construct at a time.
