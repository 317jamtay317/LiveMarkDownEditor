# CommandTip

The [Command Tip](../UbiquitousLanguage.md): the themed tooltip the [Command Bar](../UbiquitousLanguage.md)
— and the editor's other command buttons — shows on hover. In place of a bare one-line hint it names the
action and explains it: the action's **Name** as a heading, a short line saying what the action *does*,
and, when the action has one, its key gesture as a small chip.

- **Class:** `UI.Controls.CommandTip` (derives from `System.Windows.Controls.ToolTip`)
- **Default style:** [`Controls/CommandTip.xaml`](../../src/UI/Controls/CommandTip.xaml), merged in
  [App.xaml](../../src/UI/App.xaml)
- **Used by:** every command button, dropdown header, and dropdown entry in
  [MainWindow.xaml](../../src/UI/MainWindow.xaml)

Authored as a custom Control — a `ToolTip` subclass plus a ResourceDictionary for its look — per the
project's Control exception to the zero-code-behind rule. It is presentation-only: a Command Tip names
and explains an action, it never performs one.

## Why a custom control

WPF's stock `ToolTip` is a pale, system-drawn popup that ignores the palette; in the dark theme it reads
as a glaring near-white box — the same bug the stock `TextBox` and `ComboBox` carry, which
[`Themes/Controls.xaml`](../../src/UI/Themes/Controls.xaml) fixes with implicit styles. Deriving from
`ToolTip` lets a Command Tip do two things at once:

- **carry structured content** — a heading, a detail line, and an optional gesture, rather than one flat
  string; and
- **paint itself from the active palette** — every colour in its template is a `DynamicResource` lookup,
  so a theme swap recolours it live.

Because a `CommandTip` *is* a `ToolTip`, it is assigned straight to an element's `ToolTip` and shown with
its own template — no stock tooltip is wrapped around it. The plain-string tooltips that remain (the
Editor Gutter's Fold Toggle, the Flowchart Builder's connector handle) are themed by the sibling implicit
`ToolTip` style in `Themes/Controls.xaml`.

## Properties

| Property | Type | Description |
| --- | --- | --- |
| `Heading` | `string` | The action's Name — what it is (e.g. `"Bold"`). Shown as the tip's heading. Defaults to empty. |
| `Detail` | `string` | A short, plain-language line on what the action does. Shown beneath the heading; **collapses when empty**. Defaults to empty. |
| `Gesture` | `string` | The action's key gesture (e.g. `"Ctrl+B"`), shown as a chip beside the heading. **Collapses when empty**, for actions with no gesture. Defaults to empty. |

All three default to the empty string (never `null`), so the template's "collapse when empty" triggers
fire on a plain string comparison and an unfilled tip shows nothing where a missing piece would be.

## Usage

Set one as the element's tooltip and fill in the three properties:

```xml
<Button Style="{StaticResource IconFormatButton}"
        Command="EditingCommands.ToggleBold" CommandTarget="{Binding ElementName=Editor}"
        AutomationProperties.Name="Bold">
    <Button.ToolTip>
        <controls:CommandTip Heading="Bold"
                             Detail="Make the selected text bold, or turn bold text back to normal."
                             Gesture="Ctrl+B" />
    </Button.ToolTip>
    <Path Style="{StaticResource CommandIcon}" Data="{StaticResource Icon.ToggleBold}" />
</Button>
```

`Heading` mirrors the button's `AutomationProperties.Name` (what a screen reader reads), so the action is
named the same way to a sighted user hovering and to an assistive-technology user.

## Showing while disabled

By default WPF hides a control's tooltip once the control is disabled — but a greyed-out command is
exactly when the user most wants to know what it is and why it is unavailable. So the shared command
styles in [`Themes/Controls.xaml`](../../src/UI/Themes/Controls.xaml) — `CommandButton` (the base for every
command button), `CommandMenuItem` (a dropdown header), and `CommandMenuEntry` (a dropdown entry) — set:

```xml
<Setter Property="ToolTipService.ShowOnDisabled" Value="True" />
```

`ShowOnDisabled` belongs on the hovered element, not on the tooltip, so it lives in these styles rather than
in `CommandTip`. Every command button, dropdown, and dropdown entry derives from one of them, so all of
them show their Command Tip whether enabled or not.

## Tests

- [`CommandTipTests`](../../tests/UI.Tests/Controls/CommandTipTests.cs) — the control is a `ToolTip`, and
  its three properties round-trip and default to empty.
- [`CommandTipStyleTests`](../../tests/UI.Tests/Themes/CommandTipStyleTests.cs) — the `CommandTip` default
  style and the implicit `ToolTip` style are defined, target the right types, and take every colour from
  the palette (no literal a theme swap could not recolour); and the shared command styles set
  `ToolTipService.ShowOnDisabled` so a disabled command still shows its tip.
