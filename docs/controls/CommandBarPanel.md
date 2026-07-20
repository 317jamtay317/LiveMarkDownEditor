# CommandBarPanel

The layout panel behind the [Command Bar](../UbiquitousLanguage.md). It lays its items out in one
horizontal row and, when they would run past the available width, collapses whole groups of actions to
their single dropdown — lowest `CollapseOrder` first — so nothing is ever pushed off-screen (INV-054).
While the row is wide enough, every group shows its individual [Command Icons](../UbiquitousLanguage.md)
instead.

- **Class:** `UI.Controls.CommandBarPanel` (derives from `System.Windows.Controls.Panel`)
- **Used by:** the Command Bar in [MainWindow.xaml](../../src/UI/MainWindow.xaml), in place of the
  `DockPanel` it once used.

Authored as a custom layout panel — a `Panel` subclass with measure/arrange logic, the panel counterpart
of a custom Control. It holds no ViewModel and reads no document: collapsing is presentation-only, and
the same command runs whether it is reached as an icon or a dropdown entry (INV-054).

## How it works

A **collapsible group** is a pair (or more) of children that share an `OverflowGroup` name:

- the **expanded form** — the group's individual buttons (there may be several; their widths add), and
- the **collapsed form** — a single dropdown, marked `IsOverflow="True"`.

On measure, the panel measures every child at its natural width (regardless of which form is currently
shown, so the decision always has each form's true width), then collapses groups by ascending
`CollapseOrder` until the active items fit the available width — less the width reserved for any
right-docked item. A child with no `OverflowGroup` is always shown. Hiding is done with opacity (and
hit-testing), not by collapsing layout, so a group that no longer fits disappears cleanly yet can
reappear the instant the window grows.

Because the panel re-measures whenever its width changes, the collapse set is a pure function of the
available width: the same width always yields the same collapsed groups.

## Attached properties

| Property | Type | Description |
| --- | --- | --- |
| `OverflowGroup` | `string?` | Names the group a child belongs to. Children sharing a name are one group; `null` (the default) means the child is always shown. |
| `IsOverflow` | `bool` | `True` on the group's collapsed form (its dropdown); `False` (default) on the expanded form. |
| `CollapseOrder` | `int` | The group's collapse order — lower collapses first. Set the same value on both forms of a group. |

The panel also reads the standard `DockPanel.Dock` attached property: a child with `Dock="Right"` is
laid out against the right edge (the theme toggle), and its width is reserved before any group collapses.

## Usage

```xml
<controls:CommandBarPanel Margin="8,6">
    <!-- always-shown items … -->

    <!-- a group's expanded form: individual icons -->
    <Button controls:CommandBarPanel.OverflowGroup="Insert" controls:CommandBarPanel.CollapseOrder="1" … />
    <Button controls:CommandBarPanel.OverflowGroup="Insert" controls:CommandBarPanel.CollapseOrder="1" … />

    <!-- the same group's collapsed form: one dropdown -->
    <Menu controls:CommandBarPanel.OverflowGroup="Insert" controls:CommandBarPanel.IsOverflow="True"
          controls:CommandBarPanel.CollapseOrder="1"> … </Menu>

    <Button DockPanel.Dock="Right" … />  <!-- pinned to the right edge -->
</controls:CommandBarPanel>
```
