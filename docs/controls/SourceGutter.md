# SourceGutter

The **Source Gutter**: the presentation-only margin strip drawn to the left of the
[Source Panel](../UbiquitousLanguage.md)'s `TextBox`. It shows a **Line Number** for each visible
source line of the Markdown Document, mirroring the Source Panel's own line layout and scroll
position.

- **Class:** `UI.Controls.SourceGutter` (derives from `System.Windows.Controls.Canvas`)
- **Default style:** `src/UI/Controls/SourceGutter.xaml` (merged in `App.xaml`)

Authored as a custom Control (class plus a ResourceDictionary for its look), per the project's
Control exception to the zero-code-behind rule. The gutter is **view-only**: it reads the Source
Panel's line layout and mirrors it, and never mutates the document — so it cannot change the
Markdown source (INV-014). It is the Source Panel counterpart of the
[EditorGutter](EditorGutter.md), which serves the Visual Document.

## How it works

The gutter positions its numbers from `TextBox.GetRectFromCharacterIndex`, which reports each line's
position relative to the Source Panel's viewport. It re-lays out whenever the Source Panel's text,
size, or scroll offset changes (via `TextChanged`, `SizeChanged`, and the bubbled
`ScrollViewer.ScrollChangedEvent`), so the numbers stay aligned as the user types and scrolls.

The Source Panel is set to `TextWrapping="NoWrap"`, so each source line occupies exactly one rendered
row. A Line Number here is therefore the 1-based ordinal of a source line. Only the lines currently
within the viewport (`GetFirstVisibleLineIndex` … `GetLastVisibleLineIndex`) are walked and drawn.

## Properties

| Property | Type | Description |
| --- | --- | --- |
| `Source` | `TextBox?` | The Source Panel TextBox whose source lines this gutter mirrors. Setting it wires the refresh subscriptions. |
| `LineNumberBrush` | `Brush` | Brush for Line Numbers. Themed to `MutedTextBrush`. |

## Usage

```xml
<Grid>
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="Auto" />
        <ColumnDefinition Width="*" />
    </Grid.ColumnDefinitions>
    <controls:SourceGutter Grid.Column="0" Source="{Binding ElementName=SourcePanel}" />
    <TextBox Grid.Column="1" x:Name="SourcePanel" TextWrapping="NoWrap"
             Text="{Binding ActiveSession.Markdown, UpdateSourceTrigger=PropertyChanged, Delay=120}" />
</Grid>
```
