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

- [x] **Delete Row / Delete Column, and column alignment.** The Table Formatting Actions grow a
  Table but cannot shrink one, and per-column alignment already survives a Round-Trip without
  being reachable from the command bar. Must keep the Table rectangular. *(Builds on the Table*
  *Formatting Actions, in review.)*
- [x] **Split **`MarkdownRichEditor`** up.** At ~995 lines it is roughly double the 500-line hard limit in
  `CLAUDE.md`. It is really five features sharing a class — Project/Capture sync, Folding, the
  Outline, Find/Replace, and the adorner wiring. Find's scan has already moved out to
  `UI.Find.MatchScanner`, and `CodeFormatting` / `TableEditing` show the shape: a helper the
  control delegates to. Folding and the Outline are the two big ones left.
- [x] **Requery conflict-bar commands when a Conflict is raised.** `RelayCommand.CanExecuteChanged`
  delegated to `CommandManager.RequerySuggested`, which only fires on user input — so a Conflict
  raised by the file watcher left its buttons rendered disabled until the user's next mouse
  move. Harmless in practice, wrong on paper. `RelayCommand` now owns its `CanExecuteChanged` and
  the Editor Session requeries the three commands whenever `HasConflict` changes.
- [x] **Decide how canonical-Markdown churn is shown in a Conflict Difference.** Capture emits
  canonical Markdown (INV-005), so once the Visual Document is edited its blank lines can differ
  from the Watched File's, and those lines show as differences. It is truthful — that is what a
  save would write — but noisier than a plain text comparison. **Decided: compare Canonical
  Markdown on both sides** — each side is Round-Tripped before it is compared (INV-025), so only
  differences of content are shown. The Conflict Difference is now a comparison of meaning rather
  than of bytes: a line shown as Unchanged may still differ on disk, and it no longer predicts a
  save's byte-level output. That trade is accepted, and recorded in INV-025.
- [x] **Decide whether a restyle-only External Change should raise a Conflict at all.** Falls out of
  INV-025. A Conflict is raised by comparing raw text, but the Conflict Difference now compares
  Canonical Markdown — so another writer merely restyling the Watched File (setext headings to ATX,
  say) raises a Conflict whose Difference shows every line Unchanged. Truthful (the bytes really did
  change) but it asks the user to resolve a Conflict over nothing they can see. **Decided: suppress
  it** — the self-write guard now compares Canonical Markdown rather than raw text, so an External
  Change that changes no content raises no Conflict and triggers no live reload (INV-026, with
  INV-006/007 amended to govern a change *of content*). It also turned out to be reached far more
  often than "restyle-only" suggested: Capture rewrites the whole document canonically, so one
  keystroke in a file authored in another style is enough. A clean session now also stops
  re-projecting for a no-op change, which had been discarding fold state and caret position.

## Quality of life

- [x] **Export the Rendered Output as HTML.** Render already produces it for interoperability;
  nothing in the UI reaches it.
- [x] **Print / export as PDF.** A Visual Document is a `FlowDocument`, which WPF prints natively.
- [x] **Copy as rich text**, so a selection pastes formatted into Word or Outlook.
- [ ] **Restore the Workspace at startup, and a recent-files list.** Reopen the previous session's
  Tabs, offer an MRU list, and register a Windows Jump List. *(Pairs with the Startup Document*
  *and file-association work, in review.)*
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