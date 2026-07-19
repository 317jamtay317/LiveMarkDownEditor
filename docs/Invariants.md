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
- **Tested by:** `MarkdownSourceTests.Construct_GivenNullText_ThrowsAndPreservesInvariant`,
  `MarkdownDocumentTests.Construct_GivenNullSource_ThrowsAndPreservesInvariant`.

### INV-002 — Rendering is deterministic
- **Statement:** Rendering the same Markdown Document source text always produces the same Rendered
  Output.
- **Enforced by:** The `IMarkdownRenderer` port contract; the Markdig adapter is a pure function
  of the source text over a fixed pipeline with no per-render state.
- **Tested by:** `MarkdigMarkdownRendererTests.Render_GivenSameSourceTwice_ProducesIdenticalOutput_INV002`.

### INV-003 — Projecting is deterministic
- **Statement:** Projecting the same Markdown Document source text against the same Base Directory
  always produces the same Visual Document. Project has no hidden state.
- **Enforced by:** Pure Project operation driven solely by its two inputs — the source text and the
  Base Directory (`MarkdownToFlowDocumentProjector`).
- **Tested by:** `WysiwygRoundTripTests.RoundTrip_IsIdempotent_INV005` (a deterministic Project is a
  precondition of the stable Round-Trip). _(Dedicated projection-determinism test to be added as
  more constructs land.)_
- **Note:** The Base Directory is the second input because an Image's source may be a relative path,
  which names a file only in relation to the Markdown Document that references it — `![](cat.png)`
  is a different picture in a different folder. It is an explicit *input*, not hidden state: the
  same text and the same Base Directory always project alike. Projection stays synchronous and pure
  regardless of what an Image's source resolves to; a remote Image's pixels arrive afterwards,
  through WPF's own decoding, and change no part of the Visual Document's structure.

### INV-004 — A Round-Trip preserves semantic content
- **Statement:** For any supported Markdown Document, Capturing its Projected Visual Document yields
  source text that is semantically equal to the original — it renders to the same Rendered Output.
  Formatting is expressed through the Visual Document, never as literal Markdown syntax the user can
  see. (Fidelity is guaranteed only for the currently supported set of Markdown constructs; support
  grows one tested construct at a time.)
- **Enforced by:** Project and Capture being inverse transformations over the supported constructs.
- **Tested by:** `WysiwygRoundTripTests.RoundTrip_PreservesSemantics_INV004` (verified against the
  `MarkdigMarkdownRenderer` HTML oracle). Currently supported: headings, paragraphs, bold, italic,
  strikethrough, inline code, Unordered and Ordered Lists (with nesting), links, autolinks, images,
  block quotes, fenced and indented code blocks, thematic breaks (horizontal rules), GFM tables
  (with column alignment), task-list items, and hard line breaks.

### INV-005 — Capture is idempotent over Round-Trips
- **Statement:** Once a Markdown Document has been Round-Tripped, Round-Tripping the result again
  produces identical source text. Capture converges — repeated Round-Trips never keep mutating the
  document (no whitespace drift, no escaping churn).
- **Enforced by:** Capture emitting normalised, canonical Markdown (`FlowDocumentToMarkdownCapturer`).
- **Tested by:** `WysiwygRoundTripTests.RoundTrip_IsIdempotent_INV005`.

### INV-006 — A Conflict never silently discards either side
- **Statement:** When an External Change to the Watched File **that changes content** (INV-026) is
  detected while the Editor Session has unsaved edits, a Conflict is raised and surfaced to the user
  for a decision. Neither the on-disk contents nor the unsaved edits are overwritten without an
  explicit choice.
- **Enforced by:** `ExternalChangeReconciler.Reconcile` and the Editor Session's
  `HandleExternalChangeAsync`, which raise a Conflict (rather than applying disk contents) when
  unsaved edits exist, and never overwrite without an explicit Keep/Reload choice.
- **Tested by:** `ExternalChangeReconcilerTests.Reconcile_WithUnsavedEdits_RaisesConflict_INV006`,
  `EditorSessionViewModelTests.ExternalChange_WithUnsavedEdits_RaisesConflict_AndKeepsEdits_INV006`.

### INV-007 — With no unsaved edits, an External Change reloads live
- **Statement:** When the Watched File changes in content (INV-026) and the Editor Session has **no**
  unsaved edits, the Markdown Document (and therefore the Visual Document) is updated to the new
  on-disk contents without prompting. This is the "live update by any user, including AI" behaviour.
- **Enforced by:** External-change reconciliation applying disk contents directly when the session
  is clean.
- **Tested by:** `ExternalChangeReconcilerTests.Reconcile_WithNoUnsavedEdits_ReloadsFromDisk_INV007`,
  `EditorSessionViewModelTests.ExternalChange_WhenSessionClean_ReloadsLive_INV007`.

### INV-008 — The Active Session tracks the open Editor Sessions
- **Statement:** A Workspace has exactly one Active Session while it holds any open Editor Sessions,
  and the Active Session is always one of those open Sessions. It opens with a single empty Editor
  Session. The Workspace **may become empty**: closing the last Tab leaves it with no open Editor
  Session and a `null` Active Session (it does **not** re-seed a fresh Tab), and the editing area
  shows the Empty-Workspace Placeholder. Opening or creating a document adds a Tab and makes it the
  Active Session again.
- **Enforced by:** `WorkspaceViewModel` — it creates an initial Editor Session in its constructor,
  keeps `ActiveSession` referencing an open Session (moving it to a neighbour when the Active Tab is
  closed), and sets `ActiveSession` to `null` when the last Tab is closed rather than re-seeding.
- **Tested by:** `WorkspaceViewModelTests.Constructor_StartsWithOneEmptyActiveSession_INV008`,
  `WorkspaceViewModelTests.Close_LastTab_LeavesWorkspaceEmpty_INV008`.

### INV-009 — A Watched File is open in at most one Editor Session
- **Statement:** The same Watched File is never open in two Tabs at once. Opening a file that is
  already open in the Workspace activates its existing Editor Session rather than creating a
  duplicate Tab.
- **Enforced by:** `WorkspaceViewModel.Open`, which matches the chosen path against the open Editor
  Sessions' Watched Files and activates the match instead of loading a second copy.
- **Tested by:** `WorkspaceViewModelTests.Open_WhenFileAlreadyOpen_ActivatesExistingTab_INV009`.

### INV-010 — Closing an Editor Session never silently discards unsaved edits
- **Statement:** When an Editor Session with unsaved edits is closed, the user is asked to Save,
  Discard, or Cancel. Cancel aborts the close and keeps the Tab; Save persists before closing;
  Discard closes without saving. Unsaved edits are never dropped without an explicit choice. (This
  is the close-time counterpart of INV-006.)
- **Enforced by:** `WorkspaceViewModel.CloseSession` via the `IUnsavedEditsPrompt` port.
- **Tested by:** `WorkspaceViewModelTests.Close_WithUnsavedEdits_Save_Persists_INV010`,
  `WorkspaceViewModelTests.Close_WithUnsavedEdits_Discard_Closes_INV010`,
  `WorkspaceViewModelTests.Close_WithUnsavedEdits_Cancel_KeepsTab_INV010`.

### INV-011 — Folding is a view-only operation
- **Statement:** Folding a Section hides its Section Body in the Visual Document but never changes
  the Markdown Document. Capturing the Visual Document yields identical Markdown source text whether
  or not any Section is Folded — a Fold hides a Section Body without removing it from the document's
  content. Only a Section Heading can be Folded; a non-heading block has no Section to Fold. Collapse
  All and Expand All are Folds applied across every Section and are equally view-only; the Editor
  Gutter (Line Numbers and Fold Toggles) is presentation-only and likewise never changes the
  Markdown Document.
- **Enforced by:** `MarkdownRichEditor` folding, which retains each Folded Section Body and Captures
  the full logical block sequence (visible blocks with Folded bodies spliced back in), so Capture is
  unaffected by fold state — including `CollapseAllFolds` / `ExpandAllFolds`. Section boundaries are
  computed by the pure `SectionMap`. The `EditorGutter` only reads the editor's blocks and fold state;
  it never mutates the document.
- **Tested by:** `MarkdownRichEditorTests.Fold_DoesNotChangeCapturedMarkdown_INV011`,
  `MarkdownRichEditorTests.CollapseAllFolds_DoesNotChangeCapturedMarkdown_INV011`,
  `SectionMapTests.FindBody_*`.

### INV-012 — The Outline and Navigation are view-only
- **Statement:** Building the Outline, showing or hiding the Navigation Panel, and Navigating to a
  Section Heading never change the Markdown Document. The Outline lists exactly the Active Session's
  Section Headings in document order — every one of them, including headings inside a Folded Section
  Body — and Navigating selects and scrolls to a Section Heading (Unfolding its enclosing Section
  first if needed) without altering the source text. Collapsing or Expanding an Outline Entry only
  hides or shows nested Outline Entries within the Navigation Panel — it changes neither the Markdown
  Document nor any Fold state in the Visual Document. Capturing the Visual Document yields identical
  Markdown source text before and after any Outline, Navigation, or Collapse/Expand action. (This is
  the Outline/Navigation counterpart of INV-011.)
- **Enforced by:** `MarkdownRichEditor.Outline` reading the full logical block sequence (so Folded
  headings are still listed) and `MarkdownRichEditor.Navigate` only Unfolding, selecting, and
  scrolling — never mutating the logical document. The `OutlinePanel` and its visibility toggle
  (`SideDockViewModel.IsNavigationTabVisible`, which docks the Navigation Panel as a tab — INV-046)
  only read the editor's Outline and drive Navigate;
  Collapse/Expand is computed by the pure `OutlineView` and applied to panel-only view state, never
  touching the editor.
- **Tested by:** `MarkdownRichEditorTests.Navigate_DoesNotChangeCapturedMarkdown_INV012`,
  `MarkdownRichEditorTests.Outline_ListsHeadingsInsideFoldedSections_INV012`,
  `OutlineViewTests.VisibleEntries_UnderCollapsedAncestor_AreHidden`.

### INV-013 — The Source Panel and the Visual Document share one Markdown Document
- **Statement:** The Source Panel and the Visual Document are two views of the *same* Markdown
  Document source text and never diverge. Editing the Source Panel edits the source directly, which
  Projects an updated Visual Document; editing the Visual Document Captures back into the source,
  which the Source Panel reflects. A change originating in one view is never echoed back to re-edit
  the other (no feedback loop), and after either edit both views represent identical Markdown source
  text.
- **Enforced by:** Both views binding two-way to the single canonical `EditorSessionViewModel.Markdown`
  source text, and the `MarkdownRichEditor` re-entrancy guard (`_isSynchronising` / `_lastCaptured`)
  that projects an incoming source change and captures an outgoing edit without the two directions
  echoing each other.
- **Tested by:** `MarkdownRichEditorTests.AssigningSource_ProjectsVisualDocument_AndVisualEdit_UpdatesSource_INV013`.

### INV-014 — Toggling the Source Panel is view-only
- **Statement:** Showing or hiding the Source Panel never changes the Markdown Document. The Source
  Panel is hidden until the user toggles it on, and toggling it alters neither the Active Session's
  source text nor any Fold, Outline, or Navigation state. (This is the Source Panel counterpart of
  INV-011/012.)
- **Enforced by:** `WorkspaceViewModel.IsSourcePanelVisible` and `ToggleSourcePanelCommand`, which
  drive only presentation state and never touch any Editor Session's Markdown.
- **Tested by:** `WorkspaceViewModelTests.Constructor_StartsWithSourcePanelHidden_INV014`,
  `WorkspaceViewModelTests.ToggleSourcePanel_TogglesVisibility_WithoutChangingDocument_INV014`.

### INV-015 — Scroll Sync is proportional and view-only
- **Statement:** While both the Visual Document and the Source Panel are shown, scrolling either view
  scrolls the other to the same **proportional** position — the same fraction of its scrollable
  height — in both directions. The mapping is proportional, not line-for-line: offset `0` maps to
  offset `0`, the maximum scroll of one maps to the maximum scroll of the other, and a view with no
  scrollable content (its content fits) leaves its partner unmoved rather than dividing by zero.
  Scroll Sync moves viewports only and never changes the Markdown Document.
- **Enforced by:** The pure `ProportionalScroll` calculation (a function of the source offset and the
  two scrollable heights, guarded against a zero scrollable height) and the `ScrollSync` behaviour,
  which wires the two views' scroll events to that calculation and only moves viewports (it never
  touches any Editor Session's Markdown).
- **Tested by:** `ProportionalScrollTests.*`.

### INV-016 — Find is view-only
- **Statement:** Finding a query, highlighting its Matches, moving the Current Match with Find Next
  / Find Previous, and opening or closing the Find Bar never change the Markdown Document. Find is a
  read-only overlay on the Visual Document: it computes Matches from a snapshot of the document's
  text and highlights, scrolls, and selects, but the Captured Markdown source text is identical
  before and after any Find. Replacing a Match (INV-022) is a separate operation and is not part of
  Find: locating a Match never edits, and only Replace and Replace All do.
- **Enforced by:** The pure `MatchFinder` (which computes ordered, non-overlapping Matches from a
  text snapshot with no reference to the document), the pure `MatchScanner` (which maps those
  Matches onto a Visual Document as ranges and only reads it), the `FindHighlightAdorner` (which
  only draws), and Find state (`FindQuery`, Current Match, Find Bar visibility) being
  presentation-only on the `MarkdownRichEditor` Control — none of it feeds back into Capture.
- **Tested by:** `MatchFinderTests.*`, `MatchScannerTests.*`,
  `MarkdownRichEditorTests.Find_DoesNotChangeCapturedMarkdown_INV016`.

### INV-017 — Code Shading is view-only
- **Statement:** Drawing Code Shading behind the Code Blocks and Code Spans of the Visual Document
  never changes the Markdown Document. Code Shading is a read-only overlay: it computes its Code
  Regions from the document and only paints behind them, so the Captured Markdown source text is
  identical before and after Code Shading is drawn or recoloured. Because the shading is an overlay
  rather than each code element's own background, recolouring it for a theme change repaints without
  re-formatting the Visual Document. (This is the Code-Shading counterpart of INV-016.)
- **Enforced by:** The pure `CodeShadingScanner` (which computes the ordered Code Regions from a
  Visual Document with no reference back to the source), the `CodeShadingAdorner` (which only draws),
  and the projector tagging code but assigning it no `Background` — the shade lives solely in the
  overlay. None of it feeds back into Capture.
- **Tested by:** `CodeShadingScannerTests.*`,
  `MarkdownToFlowDocumentProjectorTests.CodeElements_CarryNoBackground_SoShadingCannotReflow_INV017`,
  `MarkdownRichEditorTests.CodeShading_DoesNotChangeCapturedMarkdown_INV017`.

### INV-018 — A Formatting Action Captures to canonical Markdown
- **Statement:** Applying a Formatting Action (Toggle Strikethrough, Toggle Code, Set Heading Level,
  Insert Link, Insert Image, Toggle Block Quote, Insert Table, Add Row, Add Column,
  Toggle Unordered List, Toggle Ordered List, Toggle Task List) edits the Visual Document using the
  same tagged elements a Project produces, so the Captured source text is canonical Markdown:
  Round-Tripping it preserves its semantics (INV-004) and converges (INV-005). A Formatting Action
  never corrupts the document — after it runs, the Visual Document and the Markdown Document still
  describe the same content.
- **An emphasis delimiter hugs its text.** `**bold **` and `~~struck ~~` do not close in Markdown: a
  closing delimiter preceded by whitespace is not right-flanking, so it emits literal asterisks or
  tildes rather than emphasis. A user selecting a word by double-click or Ctrl+Shift+Right takes its
  trailing space with it, so Capture hoists whitespace surrounding an emphasised span **outside** the
  delimiters (`one ~~two~~ three`, never `one ~~two ~~three`). Whitespace alone carries no emphasis
  and is emitted bare. Without this the Markdown would say "literal tildes" where the Visual Document
  says "struck through" — the two would stop describing the same content.
- **Enforced by:** The Formatting Actions on `MarkdownRichEditor` composing the identical roles the
  Projector emits (`InlineSemantic.Code`, `InlineSemantic.Strikethrough`, `HeadingRole`,
  `BlockSemantic.Quote`, `LinkRole`, `ImageRole`, `CodeBlockRole`, `TableRole`, `TaskMarkerRole`)
  through the shared `CodeFormatting` / `HeadingFormatting` / `InlineFormatting` / `QuoteFormatting` /
  `LinkFormatting` / `TableEditing` / `ListFormatting` / `TaskMarkerEditing` helpers, so Capture
  treats user-applied formatting and loaded formatting uniformly. A List carries no role of its own —
  its kind rides on the WPF `List`'s own `MarkerStyle` — so the Projector composes a List through
  `ListFormatting.ApplyList` and a Task Marker through `TaskMarkerEditing.CreateMarker`, the same
  seams the Formatting Actions use (mirroring `CodeFormatting.ApplyCodeSpan` / `TableEditing.WrapCell`).
  The whitespace rule lives in the Capturer's `Emit`, so it holds for every emphasised span alike —
  including the bold and italic actions that predate it.
- **Tested by:** `MarkdownRichEditorToggleCodeTests.*_INV018`,
  `MarkdownRichEditorTableTests.*_INV018`, `MarkdownRichEditorListTests.*_INV018`,
  `MarkdownRichEditorHeadingTests.*_INV018`, `MarkdownRichEditorQuoteTests.*_INV018`,
  `MarkdownRichEditorStrikethroughTests.*_INV018` — in particular
  `ToggleStrikethrough_WithTrailingSpaceInSelection_KeepsTheSpaceOutsideTheDelimiters_INV018` and
  `ToggleBold_WithTrailingSpaceInSelection_KeepsTheSpaceOutsideTheDelimiters_INV018`.

### INV-019 — A Table stays rectangular
- **Statement:** Every row of a Table has exactly one cell per column, and the Table's per-column
  alignments list exactly one alignment per column. Insert Table creates such a Table; Add Row mints
  its new row at the Table's column count; Add Column extends every row — header and body — by one
  cell. No Table operation ever leaves a ragged Table.
- **Enforced by:** The `TableEditing` helper, which derives the new row's width from the Table's
  columns and inserts a cell into every row when adding a column, updating the `TableRole` alignments
  in the same operation.
- **Tested by:** `MarkdownRichEditorTableTests.AddRow_MatchesColumnCount_INV019`,
  `MarkdownRichEditorTableTests.AddColumn_ExtendsEveryRow_INV019`.

### INV-020 — A Startup Document opens in the one running Workspace
- **Statement:** Launching the editor with a Startup Document opens that file into the Workspace at
  startup. If the editor is already running, the Startup Document is forwarded to the running
  instance — whose Workspace opens it, or activates its existing Tab (INV-009) — and no second
  editor window appears (Single Instance).
- **Enforced by:** `StartupArguments` (parsing the Startup Document from the command line),
  `WorkspaceViewModel.OpenPathAsync` (the same dedupe-and-load path the file picker uses), and the
  `SingleInstanceGuard` (a named mutex plus named pipe that forwards the path to the first instance),
  wired together in the composition root (`Program`).
- **Tested by:** `StartupArgumentsTests.*`,
  `WorkspaceViewModelTests.OpenPath_WhenFileAlreadyOpen_ActivatesExistingTab_INV009_INV020`,
  `SingleInstanceGuardTests.*_INV020`.

### INV-021 — Viewing the Conflict Difference is view-only and deterministic
- **Statement:** Computing a Conflict Difference is a pure, deterministic function of the two sides —
  the same session text and disk text always yield the same Difference Lines — and every line of both
  sides is accounted for: the Unchanged and Session Only lines are exactly the session's lines in
  order, and the Unchanged and Disk Only lines are exactly the disk's lines in order. Showing or
  hiding the Conflict Difference (View Difference) never changes the Markdown Document, the Watched
  File, or the Conflict itself: both sides are retained, unchanged, until the user resolves the
  Conflict with an explicit choice (INV-006), and resolving it hides the difference.
- **Enforced by:** The pure static `ConflictDifference.Compute` (Domain — no I/O, no state), and the
  Editor Session's View Difference state (`ViewDifferenceCommand`, `IsDifferenceVisible`,
  `DifferenceLines`) driving only presentation, with the conflicting disk text retained until
  `ClearConflict`.
- **Tested by:** `ConflictDifferenceTests.Compute_GivenSameInputsTwice_YieldsIdenticalLines_INV021`,
  `ConflictDifferenceTests.Compute_AccountsForEveryLineOfBothSides_INV021`,
  `EditorSessionViewModelTests.ViewDifference_ShowsDifference_WithoutChangingMarkdownOrConflict_INV021`.

### INV-022 — Replace is a real edit that Captures to canonical Markdown
- **Statement:** Replace swaps the Current Match for the Replacement; Replace All swaps every Match
  for it. Both are real edits (the counterpart of Find's INV-016 view-only guarantee), and both edit
  the Visual Document, so the result Captures as canonical Markdown: Round-Tripping it preserves its
  semantics (INV-004) and converges (INV-005). Three rules bound the edit:
  - **The Replacement is inserted verbatim** — exactly as typed, never adapting its case to the Match
    it replaces (a Match is found case-insensitively, but a Replacement is not rewritten to suit it).
    It inherits the Match's formatting when the Match *has* one: replacing a word inside bold text
    leaves it bold. A Match spanning a formatting boundary has no single formatting to inherit, so
    its Replacement is plain. An empty Replacement deletes the Match.
  - **Replace All spans the whole Markdown Document.** Because Find searches only the visible Visual
    Document, Replace All Unfolds every Folded Section first (INV-011, view-only) and re-finds, so an
    occurrence hidden inside a Folded Section Body is replaced rather than silently left behind.
  - **Replace All terminates.** It replaces exactly the Matches present when it is invoked, so a
    Replacement that contains the query does not re-match and cannot cascade.
- **Enforced by:** `MatchReplacer` (which swaps a Match's span for the Replacement through the same
  `TextRange.Text` mechanism a Spelling Suggestion uses — WPF carries the surrounding formatting into
  the new text, and flattens where the span had no single formatting), and `MarkdownRichEditor`'s
  `ReplaceCurrentMatch` / `ReplaceAllMatches`, which Unfold before Replacing All and iterate a
  snapshot of the Match ranges taken before the first edit. The edit then flows through the same
  `OnTextChanged` → Capture path as any other edit, which is what makes the Captured source
  canonical. Replace All's availability (`CanReplaceAll`) turns on the query alone and never on the
  Match count: Find cannot see into a Folded Section, so gating the command on the count would
  disable it in precisely the case this invariant exists to cover.
- **Tested by:** `MarkdownRichEditorReplaceTests.*_INV022`, in particular
  `ReplaceAll_CapturesCanonicalMarkdown_ThatRoundTrips_INV022` (anti-corruption),
  `ReplaceAll_UnfoldsFoldedSections_SoNoMatchIsMissed_INV022`, and
  `ReplaceAll_WhenReplacementContainsQuery_ReplacesOnlyTheOriginalMatches_INV022` (termination).

### INV-023 — A List Toggle preserves its List Items' content
- **Statement:** Toggle Unordered List, Toggle Ordered List, and Toggle Task List change a List's
  *kind* or its Task Markers — never the content of its List Items. Four rules bound them:
  - **Content survives every toggle.** Turning paragraphs into a List, a List back into paragraphs,
    or an Unordered List into an Ordered one preserves each item's text, its inline formatting, and
    the order of the items. One paragraph becomes one List Item, and one List Item becomes one
    paragraph.
  - **The two List kinds toggle between each other, never off through each other.** Toggle Ordered
    List applied to an Unordered List makes it Ordered (and vice versa); only the *same* kind's
    toggle turns a List back into paragraphs. So a List is never silently destroyed by reaching for
    the other kind.
  - **A Task Marker exists only on a List Item.** Applied outside a List, Toggle Task List makes the
    selected paragraphs an Unordered List first and then marks them — it never refuses the action.
    Turning a Task List back into paragraphs removes its Task Markers with it: no Task Marker can
    outlive the List Item that carries it.
  - **Toggle Task List is all-or-nothing over the selection.** It removes the Task Markers only when
    every selected List Item already carries one; otherwise it gives an unchecked Task Marker to
    those that lack one, so a partly-marked selection converges on marked rather than flip-flopping.
  - **A Task Marker's checkbox is its List Item's marker.** An Unordered List whose every item is a
    task item shows no bullet — a bullet beside a checkbox is one marker too many. It regains its
    bullets the moment any item is not a task item, because WPF gives a List one marker for all of
    its items and the unmarked items would otherwise be left with no marker at all. An Ordered Task
    List keeps its numbers. This is presentation only: it never changes the Captured source text.
  - **A Task List continues across a paragraph break.** Breaking the line in a task item gives the
    new List Item its own unchecked Task Marker, the way a bullet or a number carries to the next
    item — the new item is a task item, so the List does not regain its bullets. A Task Marker's
    checked state never carries: the new item always starts unchecked.
  - **A Task Marker's Run never swallows the item's text.** The marker is ordinary editable text and
    the caret legitimately sits inside its Run — a new task item's marker is its only inline, so WPF
    normalises the caret into it and the first thing typed lands there. Capture emits the marker from
    its role, and must emit whatever that Run holds beyond the checkbox glyph as the item's text.
    Otherwise a label the user can see on screen would never reach the Markdown Document.
- **Enforced by:** The `ListFormatting` helper, which composes its List through the same
  `ApplyList` seam the Projector uses (INV-018) and **moves each item's existing paragraph** into the
  List (and back out again) rather than re-creating it from text — so inline formatting cannot be
  flattened by a toggle. `ListFormatting.RefreshTaskMarkerStyle` is the one place the bullet rule
  lives, applied by the Projector and by every List Formatting Action alike; Capture tells an Ordered
  List from an Unordered one by `MarkerStyle == Decimal`, and `None` is not `Decimal`, so dropping the
  bullet cannot change the captured marker. `ListFormatting.TryContinueTaskList` leaves the paragraph
  break itself to WPF's `EditingCommands.EnterParagraphBreak` — which carries inline formatting across
  the break correctly — and `MarkContinuedTaskItem` then supplies the one thing WPF cannot know about,
  the new item's Task Marker, in the same `BeginChange` unit so one undo takes the whole new item back.
- **Tested by:** `MarkdownRichEditorListTests.*_INV023`. The paragraph break runs through a WPF editing
  command that needs a focused editor and so does nothing in a headless test; the tests cover the rule
  that marks the item the break creates, and Enter itself is verified by driving the real app.

### INV-024 — Toggle Task Marker flips only that Task Marker's state
- **Statement:** Clicking a Task Marker's checkbox toggles it between unchecked (`[ ]`) and checked
  (`[x]`) and changes nothing else: its List Item's content, its List's kind and numbering, and every
  other List Item are left exactly as they were. It is a real edit — the Captured source text differs
  in precisely that one marker — and it Captures as canonical Markdown, so Round-Tripping the result
  preserves its semantics (INV-004) and converges (INV-005). Toggling is reachable **only** by
  clicking a Task Marker itself: a click anywhere else in the Visual Document places the caret as it
  always has and never toggles a marker. Because a Task Marker's checked state is carried by its role
  rather than by the glyph the user sees, a toggle updates both together — the glyph and the role can
  never disagree about whether the task is checked.
- **Enforced by:** `TaskMarkerEditing`, which resolves the clicked position to a `TaskMarkerRole`-tagged
  `Run` (returning without an edit when the click is not on one) and replaces both that `Run`'s `Tag`
  and its glyph in a single `BeginChange`/`EndChange` unit, and `MarkdownRichEditor.OnPreviewMouseLeftButtonDown`,
  which only forwards the click and lets it fall through to normal caret placement otherwise.
- **Tested by:** `MarkdownRichEditorTaskMarkerTests.*_INV024`.

### INV-025 — A Conflict Difference compares Canonical Markdown
- **Statement:** The two sides a Conflict Difference compares are the **Canonical Markdown** of the
  Editor Session's unsaved source text and the Canonical Markdown of the Watched File's conflicting
  on-disk contents — each side Round-Tripped before it is compared. A difference of Markdown syntax
  style alone therefore never appears as a Difference Line: because Capture emits Canonical Markdown
  (INV-005), an edited Editor Session holds canonical source text, and a Watched File authored in
  another style would otherwise differ on every restyled line even where the two sides say the same
  thing. Every Difference Line shown is a difference of content, so View Difference answers the
  question a Conflict actually poses (INV-006) — what did the other writer change, and what would I
  lose — rather than burying it in formatting churn.
- **Consequence (accepted):** The Conflict Difference is a comparison of meaning, not of bytes. A line
  shown as Unchanged may still differ from the Watched File's own text, and resolving the Conflict
  with Keep My Edits then saving rewrites those lines to Canonical Markdown. The Conflict Difference
  does not claim to predict a save's byte-level output.
- **Enforced by:** `EditorSessionViewModel.RefreshDifference`, which Round-Trips both sides through
  its `IMarkdownRoundTrip` port before calling `ConflictDifference.Compute`; the `FlowDocumentRoundTrip`
  adapter realises the port as a Project immediately followed by a Capture. `Compute` itself stays pure
  and unaware — it compares whatever two sides it is given (INV-021).
- **Tested by:** `EditorSessionViewModelTests.ViewDifference_StillShowsARealChange_BesideChurn_INV025`,
  `FlowDocumentRoundTripTests.*_INV025`.
- **Note:** A Conflict Difference showing *every* line Unchanged is unreachable — INV-026 ignores a
  bytes-only External Change rather than raising a Conflict over it. Churn is therefore only ever
  seen beside a real change, which is what the test above pins.

### INV-026 — An External Change that changes no content is ignored
- **Statement:** An External Change whose new on-disk contents share the Editor Session's Canonical
  Markdown changes bytes but not content, and is acted on by neither INV-006 nor INV-007: no Conflict
  is raised, and no live reload occurs. This covers the Editor Session's own save (where the disk
  contents come to equal the session's source text outright) and any other writer restyling the
  Watched File — setext headings rewritten as ATX, `_` emphasis as `*`, blank-line spacing changed.
  It follows from INV-025: because a Conflict Difference compares Canonical Markdown, a Conflict
  raised by a bytes-only change would show every line Unchanged, demanding the user resolve a
  difference they cannot see. The rule is reached often, not rarely: Capture rewrites the *whole*
  Markdown Document as Canonical Markdown, so one keystroke in a Watched File authored in another
  style leaves an Editor Session that agrees with the file in content while differing from it in
  bytes on every restyled line.
- **Consequence (accepted):** The externally written bytes are not adopted — the Editor Session keeps
  its own source text, and a later save rewrites the Watched File as Canonical Markdown (INV-005).
  No content is discarded, so INV-006 holds: what is dropped is a styling of the same content, which
  the Editor Session would overwrite on its next save regardless.
- **Enforced by:** `EditorSessionViewModel.HandleExternalChangeAsync`, which returns before
  reconciling when the two sides share Canonical Markdown, Round-Tripping each through its
  `IMarkdownRoundTrip` port.
- **Tested by:** `EditorSessionViewModelTests.ExternalChange_ThatMatchesSession_IsIgnored_INV026`,
  `EditorSessionViewModelTests.ExternalChange_ThatOnlyRestylesTheWatchedFile_WhenSessionClean_IsIgnored_INV026`,
  `EditorSessionViewModelTests.ExternalChange_ThatOnlyRestylesTheWatchedFile_WithUnsavedEdits_RaisesNoConflict_INV026`.

### INV-027 — Set Heading Level changes a block's level, never its content
- **Statement:** Set Heading Level changes only the Heading Level of the block at the caret. Four rules
  bound it:
  - **Content survives.** Making a paragraph a Heading, changing a Heading's level, and turning a
    Heading back into a paragraph all preserve the block's text, its inline formatting, and its
    position in the document. One block goes in and the same block, relevelled, comes out.
  - **It sets, it does not toggle.** Choosing the level a Heading already has leaves it a Heading of
    that level, so the action is idempotent. Only **Paragraph** clears a Heading — a level is a value
    the user picks, so reaching for a level can never silently destroy the Heading.
  - **A Heading Level is always 1–6.** No other level is reachable: the Heading Level Picker offers
    exactly six, and Set Heading Level refuses any level outside 1–6 (Paragraph aside) rather than
    writing one. `#` repeats once per level, and a seventh `#` is not a heading in Markdown at all.
  - **A Heading is sized, never weighted.** The Heading a Set Heading Level produces is styled by the
    same seam the Projector uses, so it is distinguished by size alone — a bold weight would make
    Capture read the Heading's text as inline-bold and emit `# **text**` (INV-018).
- **Enforced by:** The `HeadingFormatting` helper, which **relevels the caret's existing paragraph in
  place** — setting or clearing its `HeadingRole` and restyling it — rather than re-creating it from
  text, so inline formatting cannot be flattened by a change of level. `HeadingFormatting.ApplyHeading`
  is the one place a Heading's styling lives, applied by the Projector and by Set Heading Level alike
  (mirroring `ListFormatting.ApplyList`), and `HeadingFormatting.SetLevel` ignoring a level outside
  the Paragraph-or-1–6 range.
- **Tested by:** `MarkdownRichEditorHeadingTests.*_INV027`, in particular
  `SetHeadingLevel_PreservesInlineFormatting_INV027`,
  `SetHeadingLevel_ToTheSameLevel_LeavesItAHeading_INV027`, and
  `SetHeadingLevel_GivenLevelOutsideOneToSix_LeavesTheDocumentUnchanged_INV027`.

### INV-028 — Toggle Block Quote quotes whole blocks, and preserves them
- **Statement:** Toggle Block Quote turns the blocks the selection touches into a Block Quote, and
  turns a Block Quote's blocks back into plain blocks. Three rules bound it:
  - **It quotes whole blocks, never part of one.** A Block Quote is captured as a `> ` prefix on
    every line, so quoting half a paragraph cannot be expressed in Markdown. A selection that starts
    or ends mid-block quotes that whole block, and a caret alone quotes the block it sits in.
  - **Content survives both directions.** Quoting blocks and unquoting them preserves each block's
    text, its inline formatting, its kind (a Heading stays a Heading, a List stays a List), and the
    order of the blocks. The blocks are **moved** into the Block Quote and back out again, never
    re-created from their text.
  - **Unquoting restores the blocks at top level.** A Block Quote turned off leaves its blocks in the
    document in their original order, in its place — not merged into a neighbour, and not dropped.
- **Enforced by:** The `QuoteFormatting` helper, which moves the selected top-level blocks into a
  `Section` composed through the same `ApplyQuote` seam the Projector uses (INV-018) — so a loaded
  Block Quote and a user-made one are identical to Capture — and moves them back out on the reverse
  toggle, in both cases relocating the existing blocks rather than rebuilding them.
- **Tested by:** `MarkdownRichEditorQuoteTests.*_INV028`, in particular
  `ToggleBlockQuote_WithPartialSelection_QuotesTheWholeBlock_INV028`,
  `ToggleBlockQuote_PreservesInlineFormatting_INV028`, and
  `ToggleBlockQuote_OnAQuote_RestoresItsBlocksAtTopLevel_INV028`.

### INV-029 — Toggle Strikethrough is symmetric over where the Strikethrough came from
- **Statement:** Toggle Strikethrough strikes the selection through, or restores struck-through prose
  to plain text. It removes a Strikethrough the Projector loaded exactly as readily as one a previous
  toggle applied: the two are the same thing to the user, so the action cannot be able to undo only
  its own work. Its content survives both directions — striking text through and restoring it
  preserves the text and its other inline formatting (bold stays bold).
- **Rationale:** Unlike bold and italic — which ride on `FontWeight` / `FontStyle`, inherited
  properties an inner Run can override — a Strikethrough rides on `TextDecorations`, which Capture
  reads by walking a Run's **ancestors**. So clearing the decoration on the selected Run alone would
  leave an enclosing struck Span still striking it, and the text would still Capture as `~~text~~`
  while looking plain. The Strikethrough must be removed where it lives.
- **Enforced by:** The `StrikethroughFormatting` helper, which finds the struck Spans the selection
  touches — by the same `InlineSemantic.Strikethrough` role and `TextDecorations` that Capture reads
  — and clears them, and which otherwise wraps the selection in a Span composed through the same
  `ApplyStrikethrough` seam the Projector uses (INV-018), mirroring `CodeFormatting`'s treatment of a
  Code Span.
- **Tested by:** `MarkdownRichEditorStrikethroughTests.*_INV029`, in particular
  `ToggleStrikethrough_OnALoadedStrikethrough_RemovesIt_INV029` and
  `ToggleStrikethrough_PreservesOtherInlineFormatting_INV029`.

### INV-030 — Insert Link and Insert Image edit only on a complete answer
- **Statement:** Insert Link and Insert Image ask for their text and URL through the Link Prompt, and
  edit the Visual Document only when the user gives a usable answer. Four rules bound them:
  - **Dismissing the Link Prompt makes no edit.** Cancelling leaves the Markdown Document, the
    selection, and the Visual Document exactly as they were — asking a question is not an edit.
  - **A Link is nothing without a destination.** An empty URL inserts no Link and no Image: the
    Visual Document shows a Link by its text alone, so a Link with no destination would be
    indistinguishable from prose the user could never repair from the Visual Document.
  - **The selection seeds the text, and is replaced by the result.** The selected text is offered as
    the proposed Link text (or Image alt text), so the common case — select a word, press Ctrl+K,
    paste a URL — needs no retyping. At a caret with no selection, the Link's text is whatever the
    user gives; a Link with neither selection nor text falls back to its URL, so it is never invisible.
  - **An empty answer for the text alone is not fatal.** Only the URL is required.
- **Enforced by:** The `LinkFormatting` helper, which returns before touching the document when the
  `ILinkPrompt` port yields no answer or a blank URL, and otherwise composes a `Hyperlink` carrying a
  `LinkRole` (or an Image carrying an `ImageRole`) through the same `ApplyLink` seam and
  `ImageFormatting.CreateImage` seam the Projector uses, so Capture treats a user-inserted Link and a
  loaded one uniformly (INV-018/031).
  The port keeps the Link Prompt's WPF dialog out of the editor, so the rules above are testable
  headlessly against a stub.
- **Tested by:** `MarkdownRichEditorLinkTests.*_INV030`, in particular
  `InsertLink_WhenThePromptIsDismissed_MakesNoEdit_INV030`,
  `InsertLink_WithAnEmptyUrl_MakesNoEdit_INV030`, and
  `InsertLink_SeedsThePromptWithTheSelection_INV030`.

### INV-031 — An Image shows its picture, or its alt text
- **Statement:** An Image is shown in the Visual Document as the picture its Image Source names. Four
  rules bound it:
  - **A relative Image Source resolves against the Base Directory.** `![](cat.png)` names the file
    beside the Markdown Document, which is the form Markdown authors write most.
  - **An Image that cannot be shown falls back to its alt text.** A missing file, an unreachable
    address, a source that is not an image, or a relative source with no Base Directory to resolve
    against (an unsaved Editor Session) — every one of them shows the alt text instead. A picture
    that failed to load must never leave a hole where the author's words were: the alt text *is* the
    fallback, which is what it is for.
  - **An Image Captures as `![alt](url)` either way.** Whether its picture is shown or its alt text
    is, an Image re-emits the Image Source and alt text it was built with — never the resolved
    absolute path, so a relative Image Source stays relative and the Markdown Document remains
    portable (INV-004/INV-018).
  - **A failed load is not an edit.** An Image whose picture never arrives leaves the Markdown
    Document untouched: showing is not editing, and a broken link is the author's to fix.
- **Enforced by:** The `ApplyImage` seam the Projector and Insert Image share, which carries the
  `ImageRole` (the original Image Source and alt text) that Capture keys on regardless of which of
  the two presentations is shown (INV-018).
- **Tested by:** `MarkdownRichEditorImageTests.*_INV031`.

### INV-032 — Export as HTML writes the Rendered Output, and edits nothing
- **Statement:** Export as HTML writes the Active Session's Rendered Output to the file the user
  chooses. Five rules bound it:
  - **Exporting is not an edit.** It never changes the Markdown Document, the Watched File, or the
    Editor Session's unsaved-edits state. An export is a *read* of the document that happens to write
    a different file: the Watched File is the only file an Editor Session ever writes to (INV-006),
    and exporting must not quietly join it. A document with unsaved edits still has them afterwards.
  - **Cancelling the save dialog writes nothing.** Asking the user where to put a file is not an
    export — no file is created, and nothing changes (the Link Prompt rule of INV-030, applied to a
    save dialog).
  - **It exports the document as it stands, unsaved edits and all.** The Rendered Output is rendered
    from the Editor Session's current source text, never re-read from the Watched File, so an export
    can never quietly write a stale document while the user looks at a newer one.
  - **Fold state cannot reach it.** Render is a function of the source text alone (INV-002) and
    Folding never changes the source text (INV-011), so a Folded Section's Section Body exports
    exactly as an Unfolded one does. "Export" means the whole Markdown Document, never merely the
    visible part of it — the Replace All rule of INV-022, reached from the other direction.
  - **Both Export Shapes carry the same Rendered Output.** A Standalone Page is an HTML Fragment plus
    a fixed wrapper: the two never differ in the content they carry, only in what surrounds it. So
    the choice of Export Shape is a choice of packaging and can never be a choice of document.
- **Enforced by:** The pure static `HtmlExport.Compose` (Domain — no I/O, no state), which wraps a
  Rendered Output for the chosen Export Shape and is the one place the Standalone Page's wrapper
  lives; `ExportViewModel.ExportHtmlAsync`, which returns before writing when the `IFilePicker` port
  yields no target, renders the Editor Session's own `Markdown` through `IMarkdownRenderer`, and
  writes through the `IHtmlExportStore` port — never through `IDocumentStore`, so an export has no
  route to the Watched File at all.
- **Tested by:** `HtmlExportTests.*_INV032`, `ExportViewModelTests.*_INV032` — in particular
  `ExportHtml_WhenTheSaveDialogIsCancelled_WritesNothing_INV032`,
  `ExportHtml_WithUnsavedEdits_ExportsTheSessionsText_AndLeavesThemUnsaved_INV032`, and
  `Compose_StandalonePage_CarriesTheSameRenderedOutputAsTheFragment_INV032`.

### INV-033 — Export as PDF writes the document, and edits nothing
- **Statement:** Export as PDF writes the Active Session's Markdown Document to the PDF file the user
  chooses. Four rules bound it, the same discipline as Export as HTML (INV-032):
  - **Exporting is not an edit.** It never changes the Markdown Document, the Watched File, or the
    Editor Session's unsaved-edits state. An export is a *read* of the document that happens to write
    a different file: the Watched File is the only file an Editor Session ever writes to (INV-006),
    and exporting must not quietly join it.
  - **Cancelling the save dialog writes nothing.** Asking the user where to put a file is not an
    export — no file is created, and nothing changes.
  - **It exports the document as it stands, unsaved edits and all.** The PDF is produced from the
    Editor Session's current source text, never re-read from the Watched File, so an export can never
    quietly write a stale document while the user looks at a newer one.
  - **Fold state cannot reach it.** The export is re-laid-out from the whole source text (INV-011:
    Folding never changes it), so a Folded Section's Section Body exports exactly as an Unfolded one
    does. "Export" means the whole Markdown Document, never merely the visible part.
- **Consequence (accepted):** Because a PDF cannot embed the Visual Document, an Export as PDF is
  **re-laid-out** from the Markdown rather than captured from the on-screen document, so it need not
  match the Visual Document line for line. (Print, INV-034, is the full-fidelity path.)
- **Enforced by:** `ExportViewModel.ExportPdfAsync`, which returns before writing when the
  `IFilePicker` port yields no path, exports the Editor Session's own `Markdown` through the
  `IPdfExporter` port, and writes through the `IPdfExportStore` port — never through `IDocumentStore`,
  so an export has no route to the Watched File at all. The `MigraDocPdfExporter` adapter re-lays-out
  the Markdown (parsed through the shared `GfmPipeline`) into a PDF.
- **Tested by:** `ExportViewModelTests.*_INV033` — in particular
  `ExportPdf_WhenTheSaveDialogIsCancelled_WritesNothing_INV033`,
  `ExportPdf_WithUnsavedEdits_ExportsTheSessionsText_INV033`, and
  `ExportPdf_NeverWritesTheWatchedFile_INV033`; and `MigraDocPdfExporterTests.*` for the re-layout.

### INV-034 — Printing is not an edit
- **Statement:** Print sends the Active Session's Visual Document to a printer and changes nothing: not
  the Markdown Document, the Watched File, or the Editor Session's unsaved-edits state. Two rules bound
  it:
  - **Printing is not an edit.** It reads the document and produces no file the editor owns — the
    printout (and any PDF made through the print dialog's "Microsoft Print to PDF") leaves every
    document exactly as it was.
  - **It prints the whole document.** The document printed is re-projected from the current Markdown
    source, not taken from the live editing surface, so a Folded Section's hidden Section Body prints
    too (INV-011: Folding never changes the source) and the surface the user is editing is left
    undisturbed. Print means the whole document, never merely the visible part — the fold rule of
    INV-032/INV-033 reached from printing.
- **Enforced by:** `MarkdownRichEditor.PrintVisualDocument`, which re-projects `Markdown` through the
  same `MarkdownToFlowDocumentProjector` the editing surface uses (yielding a self-contained
  `FlowDocument` that references none of the live document's state) and hands it to the
  `IDocumentPrinter` port — doing nothing when no printer is set. The port keeps the WPF print dialog
  out of the editor, so the rules above are testable headlessly against a fake.
- **Tested by:** `MarkdownRichEditorPrintTests.*_INV034` — in particular
  `Print_PrintsTheWholeDocument_IncludingFoldedSections_INV034` and
  `Print_DoesNotChangeTheMarkdownDocument_INV034`.

### INV-035 — Copying is not an edit, and carries the selection in rich flavors
- **Statement:** Copy places the current selection on the clipboard. Four rules bound it:
  - **Copying is not an edit.** It reads the document and never changes the Markdown Document, the
    Visual Document, or the Watched File. (Cut is a separate, real edit; the flavors Copy adds do not
    make Copy one.)
  - **A normal Copy carries rich text.** The selection is placed as RTF — the editing surface's own
    serialization, which Word and Outlook paste formatted — and as HTML, so it also pastes formatted
    into web editors that read the clipboard's HTML flavor rather than its RTF.
  - **Copy as Markdown carries the selection's Markdown source.** A separate command places the
    canonical Markdown of the selection on the clipboard, for a Markdown-aware target.
  - **The HTML and Markdown flavors are Captured from the blocks the selection spans.** They are
    produced by the same Capture (and Render) seams the editor already uses, so a copied Link,
    Heading, or List is the same as a saved one (INV-018). A partial selection copies the whole blocks
    it touches (whole-block granularity), and a selection cannot include what a Fold has hidden — the
    counterpart of Print's whole-document rule (INV-034), reached from the other direction.
- **Enforced by:** `MarkdownRichEditor.CaptureSelection` (the top-level Blocks the selection overlaps,
  Captured through the same `FlowDocumentToMarkdownCapturer` a save uses), `SelectionAsCfHtml` (which
  Renders that Markdown through the `IMarkdownRenderer` port and wraps it with the pure `CfHtml`), the
  `DataObject.Copying` handler that adds the HTML flavor to a Copy, and `CopyAsMarkdown`. RTF is the
  `RichTextBox`'s own selection serialization, left untouched.
- **Tested by:** `CfHtmlTests.*` (the clipboard wrapper's byte offsets) and
  `MarkdownRichEditorCopyTests.*_INV035` — in particular
  `CaptureSelection_WithAPartialSelection_CapturesTheWholeBlock_INV035`,
  `SelectionAsCfHtml_RendersTheSelectionToTheHtmlFlavor_INV035`, and
  `Copy_SerialisesTheSelectionAsRichText_INV035`.

### INV-036 — Recent Files are distinct, newest-first, and capped
- **Statement:** The Recent Files list holds Watched File paths newest first, with no duplicates
  (compared case-insensitively) and no blank entries, and never more than a fixed capacity — adding
  beyond it drops the oldest. Adding a path already present promotes it to the front rather than
  duplicating it. It is a value object: adding returns a new list and never mutates the original.
- **Enforced by:** The immutable `RecentFiles` value object (Domain), whose `Add` rebuilds the list
  with the new path at the front, the prior copy of it removed, trimmed to `Capacity`.
- **Tested by:** `RecentFilesTests.*` — in particular
  `Add_AnExistingPath_MovesItToTheFront_WithoutDuplicating`,
  `Add_TrimsToCapacity_DroppingTheOldest`, and `Add_DoesNotMutateTheOriginal`.

### INV-037 — The Workspace restores its saved documents, and never persists unsaved ones
- **Statement:** The Workspace persists and restores across runs. Four rules bound it:
  - **Only saved documents are persisted.** The Workspace State records the open Tabs' Watched File
    paths and the Recent Files — never a Tab that has no Watched File, and never any unsaved edits.
    Restoring reopens the documents, not a snapshot of unsaved work.
  - **Restore reopens the saved Tabs by path, skipping any that have gone.** A path that no longer
    loads is simply not reopened; it never blocks the rest of the restore or the app from starting.
  - **An empty or unreadable state restores nothing, leaving the one empty Tab.** A first run, or a
    corrupt state file, starts with the single empty Tab (INV-008) rather than failing.
  - **Recent Files track opens and saves.** Opening or saving a Watched File records it in the Recent
    Files (INV-036), which are persisted, shown in the Open Recent menu, and mirrored to the Windows
    Jump List. Restoring is not itself an open, so it loads the persisted Recent Files without
    reordering them.
- **Enforced by:** `WorkspaceViewModel.RestoreAsync` / `PersistStateAsync` (which record only Tabs
  with a Watched File and reopen through the same `OpenPathAsync`, tolerating a load that fails), the
  Application `WorkspaceState` snapshot and `IWorkspaceStateStore` port, and the
  `JsonWorkspaceStateStore` adapter that treats a missing or corrupt file as `WorkspaceState.Empty`.
  `Program` restores at startup, persists on exit, and mirrors the Recent Files to the `IJumpList`.
- **Tested by:** `WorkspaceViewModelTests.*_INV037` (restore reopens saved Tabs, skips files that
  have gone, keeps the empty Tab when there is nothing to restore, and tracks Recent Files) and
  `JsonWorkspaceStateStoreTests.*` (round-trip and corruption tolerance).

### INV-038 — Following a Link is not an edit
- **Statement:** Ctrl+Clicking a Link follows its destination and changes nothing: not the Markdown
  Document, the Visual Document, or the Watched File. Where it leads is bounded:
  - **A web address opens in the default browser** (http, https, or mailto).
  - **A relative Markdown file opens in a new Tab**, its path resolved against the Base Directory
    (INV-031's resolution rule, reached from following rather than showing). An unsaved document has
    no Base Directory, so a relative Link resolves to nothing and is left alone.
  - **Anything else is left alone** — a relative link to a non-Markdown file, or a bare fragment,
    opens nothing rather than guessing.
- **Enforced by:** The pure `MarkdownLink.Classify`, which resolves a Link's `NavigateUri` to a web
  target, a Markdown-file target, or nothing; `MarkdownRichEditor.FollowLink`, which opens a web
  target in the browser and routes a Markdown-file target to the `FollowLinkCommand`; and
  `WorkspaceViewModel.FollowLinkCommand`, which opens that file in a Tab through the same
  `OpenPathAsync` as any other open, tolerating a file that is not there. The Link carries a real
  `NavigateUri` (INV-030), so WPF raises the follow on Ctrl+Click.
- **Tested by:** `MarkdownLinkTests.*` (classification) and
  `MarkdownRichEditorFollowLinkTests.*_INV038` (a Markdown Link opens a Tab; following is not an edit;
  a non-Markdown target does nothing).

### INV-039 — The Status Bar reflects the document, and never changes it
- **Statement:** The Status Bar shows the Active Session's Document Statistics — word count, character
  count, and estimated reading time — together with the caret's line and column and the Current
  Section. It is presentation-only: computing or showing it never changes the Markdown Document, the
  Visual Document, or the Watched File (the view-only guarantee of the Outline, INV-012, applied to the
  Status Bar). The Document Statistics are a deterministic function of the text — the same text always
  yields the same counts and reading time.
- **Enforced by:** The pure static `TextStatistics.Compute` (Domain), and `MarkdownRichEditor.Status`
  (a `DocumentStatus` the editor recomputes from the visible document text on every change and from the
  caret on every selection change) — a read of the document that makes no edit.
- **Tested by:** `TextStatisticsTests.*` (the counts, the reading time, and determinism) and
  `MarkdownRichEditorStatusTests.*_INV039` (the Status reflects and follows the document).

### INV-040 — An accepted word is never a Misspelling, permanently
- **Statement:** A word the user has accepted through Add to Dictionary is never marked a Misspelling,
  whatever the operating system's speller thinks, and the acceptance is permanent: the User Dictionary
  is persisted, so the word stays accepted across runs. Accepting a word is not an edit — it changes
  what counts as a Misspelling, never the Markdown Document — and Spell Check re-checks so the word's
  squiggle clears at once. Words are compared case-insensitively.
- **Enforced by:** The `UserAwareSpellDictionary` decorator, which reports a word not misspelled when
  the `IUserDictionary` contains it and otherwise defers to the inner speller; the `FileUserDictionary`
  adapter, which loads the accepted words from a per-user file and appends each newly accepted one; and
  `MarkdownRichEditor.AddToDictionary`, which accepts the Misspelling's word and calls the spell-check
  adorner's `Refresh` (a re-check, not an edit). Spelling Suggestions still come from the inner speller
  unchanged.
- **Tested by:** `UserAwareSpellDictionaryTests.*_INV040` (an accepted word is not a Misspelling; an
  unaccepted one is judged by the speller) and `FileUserDictionaryTests.*` (accepted words are held,
  case-insensitive, and persisted across instances).

### INV-041 — Smart Paste adapts to the clipboard, and only handles what it recognises
- **Statement:** A paste into the editing surface adapts to the clipboard's content. Three cases are
  handled specially, and anything else pastes as it normally would:
  - **A URL pasted over a selection becomes a Link.** The selected text is kept as the Link's text and
    the pasted URL as its destination — the common "select a phrase, paste a URL" gesture — composed
    through the same seam Insert Link uses (INV-030), so it Captures as `[text](url)` (INV-018). Over
    no selection a URL pastes as ordinary text.
  - **An image on the clipboard is written beside the Watched File and inserted as an Image.** The
    picture is saved as a file in the Base Directory and referenced by its relative name, so the
    Markdown Document stays portable (INV-031) and Captures as `![alt](file)`. An unsaved document has
    no folder to write beside, so a pasted image is dropped rather than inserted un-representably.
  - **HTML is converted to Markdown and pasted as formatted content.** HTML copied from a web page is
    converted to Markdown and projected into the Visual Document, so it lands formatted the same as any
    projected Markdown rather than as raw markup or plain text.
- **Enforced by:** `MarkdownRichEditor.SmartPaste` (hooked through `DataObject.Pasting`), which
  classifies the clipboard data and either handles it — through `LinkFormatting.WrapSelectionAsLink`,
  an image written and inserted through `LinkFormatting.InsertImageSource`, or `HtmlToMarkdown.Convert`
  (over `CfHtml.ExtractFragment`) projected in at the selection — or reports it unhandled so the
  default paste proceeds.
- **Tested by:** `MarkdownRichEditorSmartPasteTests.*_INV041` (a URL over a selection becomes a Link;
  HTML pastes as Markdown; plain text is left to the default paste), `HtmlToMarkdownTests.*`, and
  `CfHtmlTests.ExtractFragment_*`.

### INV-042 — A Folder Workspace is the pruned, ordered, Markdown-only tree of its root
- **Statement:** A Folder Workspace presents its root folder as a Folder Tree that is a pure,
  deterministic function of the root and the set of file paths beneath it (the Folder-Workspace
  counterpart of INV-002/INV-003). Four rules bound it:
  - **Markdown files only.** A file becomes a **File** exactly when its name ends in `.md` or
    `.markdown` (compared case-insensitively); every other file is omitted.
  - **Empty branches are pruned.** A **Folder** appears only if at least one Markdown Document exists
    beneath it, directly or transitively. A folder — or a chain of folders — containing no Markdown is
    not shown; a deep chain leading to a single Markdown file is kept whole.
  - **Deterministic ordering.** Within every folder, and at the root, the child **Folder**s precede
    the **File**s, and each group is ordered case-insensitively by name. Shuffling the input paths
    yields an identical Folder Tree.
  - **Portable, canonical paths.** Each Folder Entry carries its root-relative path, and
    `AbsolutePathOf` resolves one to a canonical absolute path — so a file opened from the Folder Tree
    is the same path string as the same file opened through the picker, which is what lets INV-009
    dedupe them (it compares absolute paths case-insensitively).
- **Enforced by:** The pure `FolderWorkspace.From` (Domain — no I/O), which builds the nested
  `FolderEntry` tree from `/`-separated relative paths, prunes Markdown-empty branches, and orders
  folders-before-files / case-insensitively; the shared `MarkdownFile.IsMarkdown` rule; and
  `FolderWorkspace.AbsolutePathOf`. Enumerating the files on disk is Infrastructure's job
  (`FileSystemMarkdownFolderReader`), so the Domain stays pure.
- **Tested by:** `FolderWorkspaceTests.*_INV042` (extension filter, transitive pruning, folders-before-
  files ordering, determinism under shuffling, and the `AbsolutePathOf` path round-trip).

### INV-043 — Browsing a Folder Workspace is view-only; activating a File opens, never edits
- **Statement:** Opening a Folder Workspace, showing or hiding the Folder Panel, and Expanding or
  Collapsing a Folder never change any Markdown Document, any Editor Session, or the filesystem — the
  Folder-Workspace counterpart of INV-012/INV-014, extended to the filesystem because a Folder
  Workspace, unlike the Outline, is a view onto disk. Activating a **File** opens its Markdown Document
  in a Tab through the same path the file picker uses, so a file already open in the Workspace
  activates its existing Tab rather than duplicating it (INV-009), and opening it is not an edit.
  Activating a **Folder** does nothing but its own Expand/Collapse.
- **Enforced by:** `FolderWorkspaceViewModel` — its `OpenFolderCommand`, `ToggleFolderPanelCommand`,
  and `IsFolderPanelVisible` drive only presentation state, and its `ActivateEntryCommand` resolves a
  File to its absolute path and routes it to `WorkspaceViewModel.OpenPathAsync` (the same
  dedupe-and-load path the picker uses, tolerating a file that has gone) — and the `FolderPanel`
  Control, which reads the Folder Tree and raises activation without mutating any document.
- **Tested by:** `FolderWorkspaceViewModelTests.*_INV043` (opening a folder builds the tree; activating
  a File opens it through the callback with its canonical path; toggling the panel and browsing the
  tree change no document).

### INV-044 — A Folder Workspace tracks its root live
- **Statement:** While a Folder Workspace is open, a Markdown Document added, removed, or renamed
  anywhere under its root updates the Folder Tree to match — re-enumerated and rebuilt by the same
  deterministic projection (INV-042) — without changing any Markdown Document or Editor Session. It is
  the Folder-Workspace counterpart of INV-007's live reload, but view-only: the tree follows the disk,
  nothing is edited. A burst of filesystem events (an editor or tool often emits several for one
  change) is debounced into a single rebuild.
- **Enforced by:** `FolderWorkspaceViewModel` subscribing to `IFolderWatcher.Changed`, marshalling to
  the UI thread through `IUiDispatcher`, and re-running its reader-and-rebuild (its `RefreshCommand`);
  the `FileSystemFolderWatcher` adapter — a recursive `FileSystemWatcher` with a debounce, mirroring
  `FileSystemDocumentWatcher`.
- **Tested by:** `FolderWorkspaceViewModelTests.*_INV044` (a `Changed` from a fake watcher re-reads the
  folder and the Folder Tree reflects the new file set, changing no document).

### INV-045 — The open Folder Workspace is restored across runs
- **Statement:** The open Folder Workspace's root path is persisted in the Workspace State and reopened
  at startup, alongside the open Tabs and Recent Files (INV-037). Three rules bound it:
  - **Only the root is persisted, and the tree is re-enumerated.** The Folder Tree is never persisted —
    the disk is the source of truth — so Restore re-reads the root to rebuild the current tree.
  - **A root that has gone is skipped.** A persisted root folder that no longer exists, or cannot be
    read, leaves no Folder Workspace open, exactly as a vanished Tab is skipped (INV-037); it never
    blocks startup.
  - **No Folder Workspace persists nothing.** With no Folder Workspace open the Workspace State records
    no root, and an absent root restores none.
- **Enforced by:** `WorkspaceState` carrying the open Folder Workspace's root path;
  `WorkspaceViewModel.PersistStateAsync` / `RestoreAsync` recording and reopening it through
  `FolderWorkspaceViewModel`, tolerating a root that fails to read; the same `IWorkspaceStateStore` /
  `JsonWorkspaceStateStore` as INV-037 (a missing field loads as no folder).
- **Tested by:** `WorkspaceViewModelTests.*_INV045` and `FolderWorkspaceViewModelTests.*_INV045` (a
  persisted root reopens on Restore; a root that has gone is skipped) and `JsonWorkspaceStateStoreTests.*`
  (the root round-trips; old state without the field loads as no folder).

### INV-046 — The Side Dock shows exactly the navigation panels toggled on, one at a time
- **Statement:** The Side Dock hosts the Folder Panel and the Navigation Panel as tabs. It is shown
  while at least one of the two is toggled on and hidden when neither is; when shown it presents
  exactly one — the **Selected** tab — at a time. Toggling a panel on shows its tab and selects it;
  toggling the Selected panel off selects the other panel if it is still shown, and otherwise leaves
  the Side Dock hidden. Docking a panel, selecting a tab, and showing or hiding the Side Dock are
  presentation-only — none changes any Markdown Document, any Editor Session, or any Fold, Outline, or
  Folder-Tree state (the Side-Dock counterpart of INV-012/INV-014/INV-043).
- **Enforced by:** `SideDockViewModel`, which owns the Navigation Panel's tab visibility
  (`IsNavigationTabVisible` and `ToggleNavigationPanelCommand`) and the `SelectedTab`, observes the
  Folder Panel's visibility on the `FolderWorkspaceViewModel` it coordinates with (so opening a Folder
  Workspace shows and selects its tab), and derives `IsVisible` as "at least one tab is shown". It
  holds no document and drives only presentation state, so coordinating the two panels edits nothing.
- **Tested by:** `SideDockViewModelTests.*_INV046` (toggling a panel on shows and selects its tab;
  toggling the Selected panel off falls back to the other or hides the dock; the dock is hidden when
  neither panel is on; and coordinating the tabs opens or edits no document).

### INV-047 — The Diagram Preview is view-only and shows the Mermaid Diagram at the caret
- **Statement:** Identifying which Mermaid Diagram the caret is within, and rendering its Diagram
  Preview, never changes the Markdown Document — the Diagram-Preview counterpart of Find (INV-016)
  and Code Shading (INV-017). Three rules bound it:
  - **A Mermaid Diagram is identified by its language alone.** A Code Block whose info string is
    `mermaid` (compared case-insensitively) is a Mermaid Diagram; any other Code Block is not.
    Identifying it and reading its source is a pure function of the Visual Document and the caret.
  - **The Diagram Preview follows the caret.** The caret inside a Mermaid Diagram has that diagram as
    its Diagram Preview; a caret anywhere else — in another Code Block, or in prose — has none. Moving
    the caret between diagrams changes which one is previewed.
  - **Rendering is not an edit.** The Diagram Preview is rendered from a read of the diagram's source;
    Capturing the Visual Document yields identical Markdown source text before and after any Diagram
    Preview is rendered or re-rendered. A Mermaid Diagram's source itself round-trips as the fenced
    Code Block it is (INV-004).
- **Enforced by:** The pure `MermaidDiagram.SourceAt` (which reads the caret's Code Block and returns
  its source when the language is `mermaid`, otherwise `null` — it mutates nothing), the editor's
  read-only `CurrentDiagramSource` (recomputed as the caret moves and on every edit), and the
  `MermaidPreview` control, which only renders — none of it feeds back into Capture.
- **Tested by:** `MermaidDiagramTests.*_INV047` (a caret in a mermaid block yields its source; a caret
  in another Code Block or in prose yields none; the language match is case-insensitive) and
  `MarkdownRichEditorMermaidTests.Preview_DoesNotChangeCapturedMarkdown_INV047`.

### INV-048 — Toggling the Preview Panel is view-only
- **Statement:** Showing or hiding the Preview Panel never changes the Markdown Document. The Preview
  Panel is hidden until the user toggles it on, and toggling it alters neither the Active Session's
  source text nor any Fold, Outline, Navigation, or Source-Panel state. (This is the Preview-Panel
  counterpart of the Source Panel's INV-014.)
- **Enforced by:** `WorkspaceViewModel.IsPreviewPanelVisible` and `TogglePreviewPanelCommand`, which
  drive only presentation state and never touch any Editor Session's Markdown.
- **Tested by:** `WorkspaceViewModelTests.Constructor_StartsWithPreviewPanelHidden_INV048`,
  `WorkspaceViewModelTests.TogglePreviewPanel_TogglesVisibility_WithoutChangingDocument_INV048`.

### INV-049 — Export as HTML renders Mermaid Diagrams
- **Statement:** A Standalone Page renders its Mermaid Diagrams. Three rules bound it, in addition to
  Export as HTML's own discipline (INV-032):
  - **Render stays pure.** The Rendered Output is unchanged — Markdig still emits a Mermaid Diagram as
    a `mermaid`-classed fenced code block (INV-002). The diagram is rendered client-side, in the page,
    never inside Render.
  - **A Standalone Page carries the Mermaid renderer; an HTML Fragment does not.** When the Rendered
    Output contains a Mermaid Diagram, the Standalone Page's fixed wrapper embeds the Mermaid script —
    bundled and inlined, so it needs no network — which renders those blocks in a browser. The
    Fragment carries the same Rendered Output alone, so both Export Shapes still carry identical
    Rendered Output and differ only in the wrapper (INV-032). A Rendered Output with no Mermaid Diagram
    embeds no script.
  - **Exporting is still not an edit** (INV-032).
- **Enforced by:** `HtmlExport.Compose`, which appends the supplied Mermaid script to the Standalone
  Page's wrapper only when the Rendered Output contains a Mermaid Diagram, leaving `output.Html`
  untouched; `ExportViewModel.ExportHtmlAsync`, which supplies the bundled script through the
  `IMermaidScriptSource` port.
- **Tested by:** `HtmlExportTests.*_INV049` (a Standalone Page with a mermaid block embeds the script
  and still contains the Fragment verbatim; a Fragment never does; a page without a mermaid block
  embeds no script) and `ExportViewModelTests.*_INV049`.

### INV-050 — Export as PDF renders Mermaid Diagrams as images
- **Statement:** An Export as PDF renders each Mermaid Diagram as an image placed where the diagram's
  Code Block was, the rest of the document re-laid-out as before (INV-033). Three rules bound it:
  - **A rendered diagram replaces its code.** Where a Mermaid Diagram is rendered to an image, the PDF
    shows the picture, not the diagram's source text. A Mermaid Diagram that cannot be rendered falls
    back to its source text as an ordinary Code Block — a diagram that failed to render must never
    leave a hole (the Image fallback rule of INV-031, reached from export).
  - **Re-laid-out, not captured.** As with all of INV-033, the PDF is composed afresh from the
    Markdown, so a diagram image need not match the on-screen Diagram Preview pixel for pixel.
  - **Exporting is still not an edit, cancelling writes nothing, and fold state cannot reach it**
    (INV-033).
- **Enforced by:** `ExportViewModel.ExportPdfAsync`, which exports the session's own Markdown through
  `IPdfExporter.ExportAsync`; the `MigraDocPdfExporter`, which renders each Mermaid Diagram through
  the `IMermaidImageRenderer` port and hands the images to the `MarkdownPdfComposer`; and the
  composer, which draws a provided diagram image for a `mermaid` Code Block and otherwise writes the
  code text. The WebView2-backed renderer realises the port, and a diagram it cannot render is passed
  as no image, so the composer falls back.
- **Tested by:** `MarkdownPdfComposerTests.*_INV050` (a mermaid Code Block with a provided image
  composes an image, not code text; without an image it composes the code text) and
  `MigraDocPdfExporterTests.*_INV050` (a Mermaid Diagram rendered through a fake renderer still
  produces a valid PDF, as does one the renderer cannot render).

### INV-051 — A Diagram Graph round-trips through its Mermaid source, canonically
- **Statement:** Emitting a Diagram Graph as Mermaid source and parsing that source back yields an
  equal Diagram Graph, and emission is **canonical** — parsing then re-emitting a builder-produced
  graph never keeps changing the source. Every part is preserved, in order: the Flow Direction, each
  Diagram Node's Node Id, Node Label, and Node Shape, and each Diagram Edge's From, To, Edge Label, and
  Edge Kind. It is the Diagram-Graph counterpart of a Round-Trip preserving semantics and converging
  (INV-004/INV-005). Three rules bound it:
  - **Structure round-trips; layout does not.** A Diagram Graph models nodes and edges, not where they
    sit. Mermaid computes layout, so node positions are neither emitted nor parsed — they are the
    Flowchart Builder's view state alone (INV-053). Two graphs that differ only in on-canvas position
    are the same Diagram Graph.
  - **A Node Label survives verbatim.** A label is emitted quoted, so spaces and punctuation round-trip;
    a label is never confused with the Node Id, which is emitted bare.
  - **Emission is deterministic.** The same Diagram Graph always emits the same source — nodes in
    declaration order, then edges in order — so re-emitting a parsed graph is a fixed point.
- **Enforced by:** The pure `DiagramGraph.ToMermaidSource` (canonical emit — header, then one
  declaration per Diagram Node, then one line per Diagram Edge) and the pure static
  `DiagramGraph.Parse` / `TryParse` (Domain — no I/O, no state), which are inverse over the forms emit
  produces.
- **Tested by:** `DiagramGraphTests.*_INV051` — in particular
  `ToMermaidSource_ThenParse_YieldsAnEqualGraph_INV051`,
  `Parse_ThenReEmit_IsAFixedPoint_INV051`, and
  `RoundTrip_PreservesNodeLabelsWithSpaces_INV051`.

### INV-052 — A Diagram Graph is always valid
- **Statement:** A Diagram Graph can never be constructed in an invalid state. Three rules always hold:
  - **Node Ids are unique and non-empty.** No two Diagram Nodes share a Node Id, and no Node Id is
    blank — an edge could not otherwise say which node it means.
  - **Every Diagram Edge references declared Diagram Nodes.** An edge whose From or To names a node the
    graph does not declare cannot exist; there are no dangling edges.
  - **Removing a Diagram Node removes its incident Diagram Edges.** Deleting a node cascades to every
    edge that touches it, so the second rule is preserved rather than violated by a deletion.
  It is the Diagram-Graph counterpart of a Table staying rectangular (INV-019).
- **Enforced by:** The `DiagramGraph` constructor guard (rejecting duplicate/blank Node Ids and edges
  to undeclared nodes) and its operations returning new, re-validated graphs — `Connect` refusing an
  endpoint the graph does not declare, and `RemoveNode` dropping the node together with its incident
  edges. `DiagramGraph` is an immutable value object, so an operation never mutates an existing graph
  (the `RecentFiles` pattern).
- **Tested by:** `DiagramGraphTests.*_INV052` — in particular
  `Create_WithDuplicateNodeIds_Throws_INV052`,
  `Connect_ToAnUndeclaredNode_Throws_INV052`, and
  `RemoveNode_AlsoRemovesItsIncidentEdges_INV052`.

### INV-053 — The Flowchart Builder is view-only until Insert, which writes canonical Mermaid
- **Statement:** The Flowchart Builder authors a Diagram Graph over the Mermaid Diagram at the caret,
  and touches the Markdown Document only when the user commits. Four rules bound it, the discipline of
  Insert Link (INV-030) and a Formatting Action (INV-018) applied to a whole diagram:
  - **Opening reads, it does not write.** Opening the builder parses the Mermaid Diagram at the caret
    into a Diagram Graph (or starts empty when the caret is not within a parseable flowchart). Reading
    the diagram is not an edit.
  - **Editing in the builder changes no document.** Adding, moving, renaming, reshaping, connecting,
    and deleting on the canvas change only the builder's Diagram Graph and its view state — never the
    Markdown Document — until the user Inserts.
  - **Insert writes canonical Mermaid.** Insert writes the Diagram Graph's Mermaid source as a
    `mermaid` Code Block — **replacing** the Mermaid Diagram the builder was opened on, or **inserting**
    a new Code Block at the caret when it was not opened on one — and the edit Captures canonical
    Markdown, so Round-Tripping the result preserves its semantics (INV-004) and converges (INV-005), as
    any Formatting Action does (INV-018).
  - **Cancel writes nothing.** Dismissing the builder leaves the Markdown Document, the caret, and the
    Visual Document exactly as they were — the Link Prompt's dismissal rule (INV-030) for the whole
    dialog.
- **Enforced by:** The `IFlowchartBuilder` port (which keeps the WPF builder window out of the editor,
  so these rules are testable headlessly against a stub) yielding the Mermaid source to write or
  `null` on Cancel; the `FlowchartBuilderViewModel`, which drives the Diagram Graph and emits its
  source without touching any Markdown Document; and `MarkdownRichEditor.OpenFlowchartBuilderAtCaret`,
  which returns before editing when the port yields `null`, and otherwise calls
  `DiagramBlockEditing.InsertOrReplaceDiagramAtCaret` — composing a `mermaid` Code Block through the
  same `CodeFormatting.ApplyCodeBlock` seam the Projector and Toggle Code use (INV-018), inside one
  `BeginChange` unit, so the edit flows through the ordinary Capture path.
- **Tested by:** `FlowchartBuilderViewModelTests.*_INV053` (editing the builder yields no source until
  Insert; Cancel yields `null`; the emitted source is the Diagram Graph's own `ToMermaidSource`) and
  `MarkdownRichEditorFlowchartTests.*_INV053` — in particular
  `Insert_WhenOpenedOnADiagram_ReplacesThatDiagram_AndCapturesCanonicalMarkdown_INV053`,
  `Insert_WithNoDiagramAtCaret_InsertsANewBlock_INV053`, and
  `Cancel_MakesNoEdit_INV053`.

<!--
Add new invariants above using the next INV-### number. Never reuse a retired number.
Every invariant MUST have at least one corresponding test before it is considered done.
-->
