# PanelHeader

The [Panel Header](../UbiquitousLanguage.md): the strip across the top of a Dockable Panel carrying
the panel's title beside its **Pin Toggle** and its **Close Button** (INV-062). The Pin Toggle
unpins a Docked panel to Auto-Hidden — its tab joins its Auto-Hide Bar — or pins an Auto-Hidden one
back to Docked; the Close Button closes the panel, which its Command Bar View Menu toggle reopens.
Both buttons grey out where the command is unavailable, which is how the Document Pane rule
surfaces: the last Docked Document Pane can be neither closed nor unpinned (INV-063).

- **Class:** `UI.Controls.PanelHeader` — a `Control` with a default style in `PanelHeader.xaml`,
  merged by `App.xaml`.
- **Used by:** [`MainWindow.xaml`](../../src/UI/MainWindow.xaml) atop the Editor Pane, the Source
  Panel, and the Preview Panel (docked and in their Panel Flyouts alike). The Side Dock's tab strip
  hosts equivalent pin/close buttons directly, acting on the tab the dock presents.

## How it works

The header is stateless chrome: the host binds its title, its pin state, and the two commands, so
the same control serves every panel. The pin glyph turns with `IsPinned` — upright while pinned,
sideways while unpinned (the Visual Studio convention) — and both buttons pass `CommandParameter`
(the panel's `DockablePanel` value) to their commands, whose `CanExecute` drives the greyed-out
state. Every colour is a `DynamicResource` palette lookup, so the header follows the light/dark
theme.

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Title` | `string` | `""` | The panel's name, shown at the header's left (e.g. `SOURCE`). |
| `IsPinned` | `bool` | `true` | Turns the Pin Toggle's glyph: upright (pinned) or sideways (unpinned). |
| `PinCommand` | `ICommand?` | `null` | Run by the Pin Toggle — the Workspace's `TogglePinCommand`. |
| `CloseCommand` | `ICommand?` | `null` | Run by the Close Button — the Workspace's `ClosePanelCommand`. |
| `CommandParameter` | `object?` | `null` | Passed to both commands — the header's `DockablePanel`. |

## Events

None — the header raises nothing of its own; both buttons speak through their bound commands.

## Usage

```xml
<controls:PanelHeader DockPanel.Dock="Top" Title="SOURCE"
                      IsPinned="{Binding IsSourcePanelPinned}"
                      PinCommand="{Binding TogglePinCommand}"
                      CloseCommand="{Binding ClosePanelCommand}"
                      CommandParameter="{x:Static viewModels:DockablePanel.SourcePanel}" />
```

## Styling

The default style lives in `src/UI/Controls/PanelHeader.xaml`. It is self-contained: the header's
button chrome is the local `PanelHeaderButton` style (transparent, hover-highlighted, 35% opacity
when disabled), and the glyphs are the `Icon.Pin` / `Icon.PinOff` / `Icon.Close` geometries from
`Themes/Icons.xaml`.
