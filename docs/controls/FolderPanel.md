# FolderPanel

The **Folder Panel**: the presentation-only panel along the left edge of the Workspace that presents an
open **Folder Workspace**'s **Folder Tree** — every folder and its Markdown Documents as a browsable tree
of **Folder Entries**, laid out as VS Code's Explorer lays out a folder (INV-042), with what Git ignores
dimmed (INV-084). It is workspace-wide: unlike the [Navigation Panel](OutlinePanel.md) it presents no *document*, so
it stays visible even when every Tab is closed — though it does Follow the Active Session, highlighting
the File being edited (INV-083). It is hidden until the user opens
a Folder Workspace (`FolderWorkspaceViewModel.IsFolderPanelVisible`). It is presented as a tab of the
**Side Dock** — alongside the Navigation Panel, so the two navigation panels share one column rather
than each taking its own (INV-046).

- **Class:** `UI.Controls.FolderPanel` (derives from `System.Windows.Controls.TreeView`)
- **Default style:** `src/UI/Controls/FolderPanel.xaml` (merged in `App.xaml`)

Authored as a custom Control (a `TreeView` subclass plus a ResourceDictionary for its look), per the
project's Control exception to the zero-code-behind rule — the same pattern as the
[OutlinePanel](OutlinePanel.md). The panel is **view-only**: it reads the Folder Tree and raises
activation, deletion, renaming and folder creation, and never itself mutates any document or the
filesystem (INV-043). Delete File, Rename File, New File and New Folder are the four actions that change the
disk, and the panel only raises them as commands. The Folder Workspace asks the user to confirm and
does the deleting (INV-081), checks the New Name and does the renaming (INV-082), and checks the Folder
Name and creates the folder (INV-085).

## How it works

The panel binds to a Folder Workspace through its `Workspace` dependency property and raises activation
through its `ActivateCommand`:

- **Listing** — the panel lists its own **Folder Panel Rows** (`FolderPanelRow`), one per Folder Entry,
  rather than the Folder Entries themselves. Each row carries its `Entry`, its `Name` and `Kind`, its
  nested `Children` rows, and the panel-only `IsExpanded` state; the panel's `ItemsSource` is the
  top-level rows. A single `HierarchicalDataTemplate` renders each row: its `Children` are the nested
  rows, and the node glyph (a folder or a Markdown file) is switched by a `DataTrigger` on the row's
  `Kind` — the same technique the Difference Overlay uses for a Difference Line. A **File** row has no
  children, so it shows no Expand/Collapse chevron — and neither does an empty Folder, which the tree
  shows like any other (INV-042). A row's `ToString()` is its name, which is what UI Automation and
  screen readers announce. The commands and `SelectedEntry` still carry Folder Entries, never rows.
- **Dimming what Git ignores (INV-084)** — a row's `IsIgnored` mirrors its entry's, and a
  `MultiDataTrigger` in the template draws an Ignored row's name in `IgnoredTextBrush` — VS Code's
  `gitDecoration.ignoredResourceForeground`, `#8C8C8C` on the dark theme and `#8E8E90` on the light —
  unless the row is highlighted, where the highlight's own text color keeps it readable. Only the name
  is dimmed, not the glyph, as in VS Code. `FolderPanelRow.Sync` raises `IsIgnored` when a rebuild
  changes it, so editing a `.gitignore` dims or undims the same row. Dimming is only a look: an Ignored
  row browses, opens and is acted on like any other.
- **Keeping the user's place (INV-044)** — the Folder Tree is rebuilt whenever the disk changes and
  after a Delete File. Swapping in a whole new tree would make WPF re-create every row: every Folder
  would come back Collapsed, the highlight would vanish, and the virtualized list would lose its scroll
  position, because it forgets the heights of Expanded Folders above the view. So when `Workspace`
  changes to a rebuild of the **same root**, `FolderPanelRow.Sync` applies it to the existing rows as a
  change. A row whose entry has gone is removed, a new entry gets a new row in its place, and every
  other row is kept and updated in place. WPF sees only those additions and removals. A `Workspace`
  with a **different** `RootPath` builds fresh rows, all Collapsed.
- **Expand / Collapse** — a **Folder** row Expands and Collapses natively (its chevron is the
  `TreeViewItem`'s own), showing or hiding the nested rows beneath it. This is view-only and changes no
  document. Each `TreeViewItem`'s `IsExpanded` is bound two ways to its row's `IsExpanded`, at every
  depth: every container is the panel's own private `TreeViewItem` subclass, which binds the containers
  nested under it the way the panel binds its top-level ones. It is a binding rather than a set value
  because WPF's virtualization clears a plain `IsExpanded` on every container it prepares (so a
  recycled container cannot carry another entry's state) but leaves a binding alone.
- **Activating** — because a `TreeView`'s `SelectedItem` is read-only (unlike the `ListBox` the
  OutlinePanel derives from), activation is driven from input rather than a selection change:
  `OnMouseDoubleClick` resolves the double-clicked row to its Folder Entry and, when it is a **File**,
  runs `ActivateCommand` with that entry; `OnKeyDown` does the same for **Enter** on the selected row.
  The Workspace routes the command to its `OpenPathAsync`, so activating a File opens it in a Tab —
  activating one already open just activates its existing Tab (INV-009). A **Folder** double-click is
  left to its native Expand/Collapse.
  - The click lands on a visual *inside* the row, so the row is found by walking up the visual tree to
    the nearest `TreeViewItem`. `ItemsControl.ContainerFromElement` cannot be used for this: a nested
    row belongs to its parent row, not to the `TreeView`, so asking the panel for the container of a
    nested click returns the top-level ancestor row instead — which, being a **Folder**, activates
    nothing. That was the cause of Files inside a Folder refusing to open (INV-043).
- **Highlighting** — `OnSelectedItemChanged` republishes the highlighted row as the **Selected Folder
  Entry** through the `SelectedEntry` dependency property. Highlighting is browsing, not activating: it
  opens nothing and edits nothing (INV-043). What it is for is naming the **Save Folder** — the folder
  a new Markdown Document is offered to be saved into (INV-080). A rebuild keeps the highlighted row
  highlighted while its entry still exists, and republishes `SelectedEntry` as the rebuilt entry.
  - `SelectedEntry` is a writable dependency property rather than a read-only one because a read-only
    dependency property cannot carry a binding at all, and the Workspace binds this one `TwoWay`. The
    `TreeView`'s own `SelectedItem` cannot be used directly for the same reason: it is read-only.
- **Following the Active Session (INV-083)** — the other direction of that binding. The Workspace
  pushes the File it is editing into `SelectedEntry`, and the panel **reveals** it: `PathTo` finds the
  chain of rows down to it, every Folder on the way is Expanded, the row is brought into view if the
  virtualized list had not realized it, and it is highlighted. Pushing `null` — an unsaved Tab, or a
  document outside the open root — leaves no row highlighted, which is what `Deselect` is for: a
  `TreeView` has no "select nothing" of its own, so the highlighted row clears itself. The reveal is
  deferred to the next layout (`DispatcherPriority.Loaded`), because a Folder Workspace opened and its
  Tabs Restored in one pass pushes a File before its rows exist. Following never runs
  `ActivateCommand`: it highlights, it does not open (INV-043).
  - A push that only echoes the row the user just highlighted reveals nothing, so clicking a row never
    scrolls the panel out from under the click. The reveal reads `SelectedEntry` rather than the pushed
    value, so several pushes in one pass settle on the last of them.
  - The reveal shares its row-finding with Rename File's own highlight — both live in the
    `FolderPanel.Rows.cs` partial, so "find the row for this relative path, opening and scrolling to
    it" is written once.
- **Deleting** — Delete File (INV-081) is reached two ways. Both act on the **Selected Folder Entry**,
  and only when it is a **File**:
  - **The Delete key.** `OnKeyDown` runs `DeleteCommand` with the selected File, the same way Enter
    runs `ActivateCommand`. Neither key does anything on a Folder.
  - **The context menu.** The panel's default style gives it one `ContextMenu`, holding **New
    folder** and, over a File, **Rename file** and **Delete file**. The menu belongs to the panel, not
    to each row. Before it opens, `OnContextMenuOpening` finds the right-clicked row by the same
    visual-tree walk a double-click uses, and selects it, so each entry acts on exactly the entry the
    user pointed at and the highlight shows which one. Over empty space it selects nothing, so New
    Folder goes in the root. The file entries (and the separator above them) are shown only while
    `IsFileSelected` is true, so a Folder offers New File and New Folder alone. The Delete entry binds
    `PlacementTarget.DeleteCommand` with `PlacementTarget.SelectedEntry` as its parameter: a
    `ContextMenu` is its own visual tree, so it can reach the panel only through `PlacementTarget`.
  - The panel only raises the command. Asking whether the user is sure, closing the File's Tab, and
    sending the file to the Recycle Bin are the Folder Workspace's job
    (`FolderWorkspaceViewModel.DeleteEntryCommand`).
- **Renaming** — Rename File (INV-082) edits the **Selected Folder Entry**'s name in place, and only
  when it is a **File**. The rename logic lives in the `FolderPanel.Rename.cs` partial.
  - **Starting.** The panel's own routed command, `FolderPanel.EditFileName`, carries the **F2** key
    gesture and is bound in the panel's constructor, so F2 on a selected File starts editing. The
    context menu's **Rename file** entry routes the same command to the panel (its `CommandTarget` is
    the menu's `PlacementTarget`), after `OnContextMenuOpening` has selected the right-clicked File.
    The command can run only on a File, only when a `RenameCommand` is bound, and only when no name is
    already being edited.
  - **Editing.** Starting sets the row's `NewName` to its current name and its `IsRenaming` to true.
    The row template's `DataTrigger` on `IsRenaming` swaps the name for a `TextBox` bound two ways to
    `NewName`. Once the next layout has shown it, the panel focuses the editor and selects the name
    without its extension, as Windows does, so typing replaces just the name.
  - **Committing and cancelling.** While a name is being edited, `OnKeyDown` gives Enter and Escape to
    the edit and every other key to the name, so Enter never opens the File and Delete never deletes
    it. Enter commits and Escape abandons, and both put keyboard focus back on the row.
    `OnLostKeyboardFocus` commits when focus leaves the editor, as moving away from a row does in every
    Windows file tree, unless focus has moved into the editor's own context menu (to paste, say). A
    double-click inside the editor selects a word; it does not open the File. The panel's own context
    menu does not open over a name being edited.
  - **Raising the command.** Committing runs `RenameCommand` with a `RenameFileRequest`: the File and
    the New Name exactly as typed. A name the Folder Tree's rule says is unchanged raises nothing.
  - **Keeping the user's place.** At commit, the panel asks the Folder Tree's rule
    (`FolderWorkspace.Rename`) where the File will be, and remembers that relative path. The first
    rebuild of the same root afterwards highlights the renamed File's row once layout has given it
    one, bringing it into view if the virtualized list had not realized it, and focuses it when the
    user committed with Enter. Only that first rebuild looks, so a refused rename never highlights a
    file of that name that appears later for some other reason.
  - The panel only raises the command. Tidying and checking the New Name, explaining a refusal,
    renaming the file, and moving the File's Tab and Recent Files entry to the new path are the Folder
    Workspace's job (`FolderWorkspaceViewModel.RenameEntryCommand`).
- **Creating files and folders** — New File (INV-086) and New Folder (INV-085) name a new entry in
  place, in the **Save Folder**: inside a Selected Folder, beside a Selected File, or at the root when
  nothing is selected — VS Code's Explorer **New File** and **New Folder**. The logic lives in the
  `FolderPanel.NewEntry.cs` partial.
  - **Starting.** The panel's own routed commands, `FolderPanel.CreateFile` and
    `FolderPanel.CreateFolder`, are bound in the panel's constructor. The Folder header's **New file**
    and **New folder** buttons route them to the panel by `CommandTarget`, and the context menu's
    entries route them through `PlacementTarget`. Each can run only with a Folder Workspace shown, its
    `NewFileCommand` or `NewFolderCommand` bound, and no name already being edited. Because the header
    buttons sit outside the panel, where WPF asks a routed command again only after input, the panel
    calls `CommandManager.InvalidateRequerySuggested` when its `Workspace` changes and when an edit
    starts or ends — otherwise a Folder Workspace Restored at startup leaves the buttons greyed out.
  - **From New Document.** Ctrl+N with a Folder Entry selected (INV-087) sets the Folder Workspace's
    `NewFileRequest`, which the panel binds two ways as its `NewFileRequest` property. When a request
    arrives the panel hands the property back as `null` and, once layout has run (the panel may just
    have been shown), starts New File as `CreateFile` does.
  - **Editing.** Starting asks the Folder Tree's rule (`FolderWorkspace.SaveFolderEntryFor`) for the
    Save Folder, Expands it, and inserts a **New Entry row** (`FolderPanelRow.ForNewEntry`,
    `IsNewEntry`, with the kind being made) where VS Code puts it: at the top of the Save Folder for a
    Folder, and at the top of its files, below its Folders, for a File. The row is already
    `IsRenaming` with an empty `NewName` — the same in-place name editor Rename File uses, labelled
    "New file name" or "New folder name" for screen readers. Once layout has shown it, the panel brings
    it into view and focuses it.
  - **A rebuild while typing.** `FolderPanelRow.Sync` syncs a level's rows around a New Entry row,
    never moving it, so a live refresh (INV-044) never takes away the name being typed.
  - **Committing and cancelling.** Enter, Escape and focus leaving the editor behave as they do for
    Rename File. Ending the edit always removes the New Entry row. Escape, and an Entry Name that is
    blank once tidied, create nothing and say nothing, as in VS Code. Anything else runs
    `NewFileCommand` or `NewFolderCommand` with a `NewEntryRequest`: the Selected Folder Entry the
    action began with, and the Entry Name exactly as typed. A new Folder is highlighted by the first
    rebuild afterwards, as a renamed File is; a new File opens in a Tab, whose Following highlights it
    (INV-083), and the Workspace moves keyboard focus into the editor.
  - The panel only raises the commands. Checking the Entry Name, explaining a refusal, creating the
    entry and opening a new File are the Folder Workspace's job
    (`FolderWorkspaceViewModel.NewFileCommand` and `NewFolderCommand`).

## Properties

| Property | Type | Description |
| --- | --- | --- |
| `Workspace` | `FolderWorkspace?` | The Folder Workspace whose Folder Tree this panel lists; its `Entries` are the tree's roots. Setting a rebuild of the same root updates the rows in place, keeping the user's place (INV-044). |
| `ActivateCommand` | `ICommand?` | Run when a File is activated (double-click or Enter), with the File's `FolderEntry` as its parameter. |
| `DeleteCommand` | `ICommand?` | Run for Delete File (the Delete key, or **Delete file** on a File's context menu), with the File's `FolderEntry` as its parameter. Never run for a Folder (INV-081). |
| `NewFileCommand` | `ICommand?` | Run when an Entry Name is committed for New File, with a `NewEntryRequest` naming the Selected Folder Entry New File began with and the Entry Name as typed. Never run for Escape or a blank name (INV-086). |
| `NewFolderCommand` | `ICommand?` | Run when an Entry Name is committed for New Folder, with a `NewEntryRequest` naming the Selected Folder Entry New Folder began with and the Entry Name as typed. Never run for Escape or a blank name (INV-085). |
| `NewFileRequest` | `object?` | A request from New Document to start New File (INV-087). Bound two ways by default: the panel takes each new request, hands the property back as `null`, and opens New File's name editor. |
| `IsFileSelected` | `bool` (read-only) | Whether the Selected Folder Entry is a File, so the context menu offers Rename file and Delete file (INV-081, INV-082). |
| `RenameCommand` | `ICommand?` | Run when a New Name is committed for Rename File (Enter, or focus leaving the name editor), with a `RenameFileRequest` naming the File and the New Name as typed. Never run for a Folder, for Escape, or for an unchanged name (INV-082). |
| `SelectedEntry` | `FolderEntry?` | The highlighted row, republished for the Workspace, which binds it `TwoWay`. Set from outside it is the File to Follow: the panel reveals and highlights that File's row, or highlights none when `null` (INV-083). |

## Commands

| Command | Gesture | Description |
| --- | --- | --- |
| `FolderPanel.CreateFile` | — | Opens New File's name editor at the top of the Save Folder's files. Raised by the Folder header's **New file** button and the context menu's **New file**; Ctrl+N reaches it through `NewFileRequest` (INV-086, INV-087). |
| `FolderPanel.CreateFolder` | — | Opens New Folder's name editor at the top of the Save Folder. Raised by the Folder header's **New folder** button and the context menu's **New folder** (INV-085). |
| `FolderPanel.EditFileName` | F2 | Edits the selected File's name in place: the start of Rename File. Also raised by **Rename file** on a File's context menu. Unavailable on a Folder (INV-082). |

## Usage

```xml
<controls:FolderPanel x:Name="FolderTree"
                      Workspace="{Binding Folder.Folder}"
                      ActivateCommand="{Binding Folder.ActivateEntryCommand}"
                      DeleteCommand="{Binding Folder.DeleteEntryCommand}"
                      RenameCommand="{Binding Folder.RenameEntryCommand}"
                      NewFileCommand="{Binding Folder.NewFileCommand}"
                      NewFolderCommand="{Binding Folder.NewFolderCommand}"
                      NewFileRequest="{Binding Folder.NewFileRequest, Mode=TwoWay}"
                      SelectedEntry="{Binding Folder.SelectedEntry, Mode=TwoWay}" />

<!-- New file / New folder buttons outside the panel route the panel's own commands to it. -->
<Button Command="{x:Static controls:FolderPanel.CreateFile}"
        CommandTarget="{Binding ElementName=FolderTree}" />
<Button Command="{x:Static controls:FolderPanel.CreateFolder}"
        CommandTarget="{Binding ElementName=FolderTree}" />
```
