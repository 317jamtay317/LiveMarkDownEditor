# PageView

**Page View**: the presentation mode that lays the [MarkdownRichEditor](MarkdownRichEditor.md)'s Visual
Document out on a fixed-width **Document Sheet** floating on a scrolling canvas — the way a word
processor shows a page — so every element, **tables included**, is confined to one page width instead
of stretching to the pane. Page View is **on by default**; turned off, the editor fills the pane as a
plain editing surface. It is presentation-only: turning it on or off never changes the Markdown
Document or the result of a Capture (INV-058).

- **Class:** `UI.Controls.PageView` (a `static` attached behaviour)
- **Rule:** `UI.Controls.DocumentSheet` — the Sheet's fixed `Width` (816, the US Letter width at 96 dpi)
  and its page `PagePadding`
- **Canvas colour:** `EditorCanvasBrush` (in `Palette.Light.xaml` / `Palette.Dark.xaml`)

Authored as an attached behaviour — the sanctioned home for view-interaction logic outside a ViewModel
— so the page-view concern lives here rather than swelling `MarkdownRichEditor`. The editor gets only a
tiny, page-agnostic seam: a `RevealRectOverride` the behaviour sets so find-match and heading jumps
scroll the canvas instead of the editor's own (now-disabled) scroll.

## How it works

A `RichTextBox` does not virtualise — it lays its whole `FlowDocument` out at once — and a projected
GFM table is a WPF `Table` with no fixed width, so on an unbounded surface a table spans the entire
pane while prose wraps, and every widen (above all **maximising**) reflows the whole document. Page
View fixes this at the root: it lays the document out on a Sheet of one fixed width, so content width
no longer tracks the pane.

The behaviour is attached to the surface `Grid` that holds the [Editor Gutter](EditorGutter.md) and the
editor, inside an outer `ScrollViewer` (the canvas). On enter it:

- **stops the editor scrolling itself** (`VerticalScrollBarVisibility = Disabled`) so it grows to its
  content's full height and the whole Sheet moves as one piece when the canvas scrolls;
- **fixes the editor to the Sheet** — `Width = DocumentSheet.Width`, page `Padding`, a 1px edge — and
  its Grid column to `Auto`, so `[gutter | Sheet]` hugs its content;
- **centres the pair on the canvas** natively — the canvas does not scroll horizontally, so it measures
  the surface at the viewport width and `HorizontalAlignment=Center` centres it with equal gray margins
  on both sides, reliably and free of any layout-timing recomputation;
- **paints the canvas** with `EditorCanvasBrush` (darker than the Sheet, so the page edge reads);
- **follows the caret** by scrolling the canvas to keep it in view as the user types or navigates
  (the editor's own caret-into-view is disabled with its scroll);
- **re-points [Scroll Sync](../Invariants.md#inv-015)** so the Source Panel syncs to the canvas rather
  than the editor, which no longer scrolls itself.

On exit it restores every one of these to the plain full-pane surface, and the editor scrolls itself
again exactly as before.

## Attached properties

Set these on the surface `Grid`:

| Property | Type | Description |
| --- | --- | --- |
| `IsEnabled` | `bool` | Whether the surface is in Page View. Bind to `WorkspaceViewModel.IsPageViewEnabled` (on by default). |
| `Editor` | `MarkdownRichEditor` | The editing surface laid out as a Document Sheet. |
| `Canvas` | `ScrollViewer` | The outer scroller the Sheet floats on and that follows the caret. |
| `Source` | `TextBoxBase` | The Source Panel to keep Scroll-Synced with the page. |

## Usage

```xml
<ScrollViewer x:Name="EditorScroller" Focusable="False"
              VerticalScrollBarVisibility="Disabled" HorizontalScrollBarVisibility="Disabled">
    <Grid controls:PageView.IsEnabled="{Binding IsPageViewEnabled}"
          controls:PageView.Editor="{Binding ElementName=Editor}"
          controls:PageView.Canvas="{Binding ElementName=EditorScroller}"
          controls:PageView.Source="{Binding ElementName=SourcePanel}">
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="Auto" />
            <ColumnDefinition Width="*" />
        </Grid.ColumnDefinitions>
        <controls:EditorGutter Grid.Column="0" Editor="{Binding ElementName=Editor}" />
        <controls:MarkdownRichEditor x:Name="Editor" Grid.Column="1"
                                     Markdown="{Binding ActiveSession.Markdown, UpdateSourceTrigger=PropertyChanged}" />
    </Grid>
</ScrollViewer>
```
