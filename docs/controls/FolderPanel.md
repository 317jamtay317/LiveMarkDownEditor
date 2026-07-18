# FolderPanel

The **Folder Panel**: the presentation-only panel along the left edge of the Workspace that presents an
open **Folder Workspace**'s **Folder Tree** — its Markdown Documents as a browsable tree of **Folder
Entries**. It is workspace-wide: unlike the [Navigation Panel](OutlinePanel.md) it is **not** tied to
the Active Session, so it stays visible even when every Tab is closed. It is hidden until the user opens
a Folder Workspace (`FolderWorkspaceViewModel.IsFolderPanelVisible`). It is presented as a tab of the
**Side Dock** — alongside the Navigation Panel, so the two navigation panels share one column rather
than each taking its own (INV-046).

- **Class:** `UI.Controls.FolderPanel` (derives from `System.Windows.Controls.TreeView`)
- **Default style:** `src/UI/Controls/FolderPanel.xaml` (merged in `App.xaml`)

Authored as a custom Control (a `TreeView` subclass plus a ResourceDictionary for its look), per the
project's Control exception to the zero-code-behind rule — the same pattern as the
[OutlinePanel](OutlinePanel.md). The panel is **view-only**: it reads the Folder Tree and raises
activation, and never mutates any document or the filesystem (INV-043).

## How it works

The panel binds to a Folder Workspace through its `Workspace` dependency property and raises activation
through its `ActivateCommand`:

- **Listing** — when `Workspace` changes, the panel sets its `ItemsSource` to `FolderWorkspace.Entries`
  (the top-level Folder Entries). A single `HierarchicalDataTemplate` renders each Folder Entry: its
  `Children` are the nested entries, and the node glyph (a folder or a Markdown file) is switched by a
  `DataTrigger` on the entry's `Kind` — the same technique the Difference Overlay uses for a Difference
  Line. A **File** entry has no children, so its row shows no Expand/Collapse chevron.
- **Expand / Collapse** — a **Folder** entry Expands and Collapses natively (its chevron is the
  `TreeViewItem`'s own), showing or hiding the nested entries beneath it. This is view-only and changes
  no document.
- **Activating** — because a `TreeView`'s `SelectedItem` is read-only (unlike the `ListBox` the
  OutlinePanel derives from), activation is driven from input rather than a selection change:
  `OnMouseDoubleClick` resolves the double-clicked row to its Folder Entry (via
  `ItemsControl.ContainerFromElement`) and, when it is a **File**, runs `ActivateCommand` with that
  entry; `OnKeyDown` does the same for **Enter** on the selected row. The Workspace routes the command
  to its `OpenPathAsync`, so activating a File opens it in a Tab — activating one already open just
  activates its existing Tab (INV-009). A **Folder** double-click is left to its native Expand/Collapse.

## Properties

| Property | Type | Description |
| --- | --- | --- |
| `Workspace` | `FolderWorkspace?` | The Folder Workspace whose Folder Tree this panel lists; its `Entries` are the tree's roots. |
| `ActivateCommand` | `ICommand?` | Run when a File is activated (double-click or Enter), with the File's `FolderEntry` as its parameter. |

## Usage

```xml
<controls:FolderPanel Workspace="{Binding Folder.Folder}"
                      ActivateCommand="{Binding Folder.ActivateEntryCommand}" />
```
