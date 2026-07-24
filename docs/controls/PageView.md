# PageView

**Page View**: the presentation mode that lays the [MarkdownRichEditor](MarkdownRichEditor.md)'s Visual
Document out on a **Document Sheet** of whole US Letter **Pages** floating on a scrolling canvas — the
way a word processor shows a page — so every element, **tables included**, is confined to one page width
instead of stretching to the pane, and the Sheet gains its next Page as soon as the content needs it.
The Page is turned and inset by the one editor-wide **Page Setup**: its **Page Orientation** (8.5 × 11
portrait, 11 × 8.5 landscape) and its **Print Margins** — the same setup the Print Preview and the
printout obey (INV-061). Page View is **on by default**; turned off, the editor fills the pane as a
plain editing surface. It is presentation-only: turning it on or off never changes the Markdown
Document or the result of a Capture (INV-058).

- **Class:** `UI.Controls.PageView` (a `static` attached behaviour)
- **Rule:** `UI.Controls.DocumentSheet` — the US Letter page's `Width` (816) and `PageHeight` (1056) at
  96 dpi, and the whole-Page arithmetic (`PageCount`, `HeightFor`, `TrailingSpaceFor`), each
  parameterized by the page height the Page Setup's orientation yields
- **Setup:** `UI.Core.PageSetup` — the orientation and margins the Sheet is laid out under (INV-061)
- **Sheet:** [DocumentSheetBackdrop](DocumentSheetBackdrop.md) — the paper and the Page Break rules,
  drawn behind the editor
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
- **fixes the editor to the Sheet** — `Width` at the Page Setup's oriented page width, its Print
  Margins as the page `Padding`, a 1px edge — and its Grid column to `Auto`, so `[gutter | Sheet]`
  hugs its content. Changing the `Setup` while in Page View re-lays the Sheet out (INV-061);
- **snaps the Sheet to whole Pages**: the filler `DocumentSheet.TrailingSpaceFor` asks for — at the
  Page Setup's oriented page height — is added to the Sheet's bottom page margin, so a short document
  still shows a full Page and the Sheet gains its next Page the moment the content outgrows the last.
  It re-snaps on the editor's
  `SizeChanged` — the one signal that covers every way the content's height can change (typing, a
  reload, an Unfold) — coalesced to one snap per dispatcher cycle, since setting the filler resizes the
  Sheet and lands straight back there. Because the filler rides on the page margin, the Sheet's own
  height stays the measure of the content: subtracting the filler back off recovers it;
- **hands the paper to the [DocumentSheetBackdrop](DocumentSheetBackdrop.md)** by making the editor's
  `Background` transparent, so the Sheet's fill and its Page Break rules are drawn *behind* the Visual
  Document and a break passes under the text instead of striking through it;
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
| `Setup` | `PageSetup` | The Page Setup the Sheet is laid out under — orientation and Print Margins. Bind to `WorkspaceViewModel.PageSetup`; left unset, the default (Portrait, Normal margins) applies (INV-061). |

## Usage

```xml
<ScrollViewer x:Name="EditorScroller" Focusable="False"
              VerticalScrollBarVisibility="Disabled" HorizontalScrollBarVisibility="Disabled">
    <Grid controls:PageView.IsEnabled="{Binding IsPageViewEnabled}"
          controls:PageView.Editor="{Binding ElementName=Editor}"
          controls:PageView.Canvas="{Binding ElementName=EditorScroller}"
          controls:PageView.Source="{Binding ElementName=SourcePanel}"
          controls:PageView.Setup="{Binding PageSetup}">
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="Auto" />
            <ColumnDefinition Width="*" />
        </Grid.ColumnDefinitions>
        <controls:EditorGutter Grid.Column="0" Editor="{Binding ElementName=Editor}" />
        <!-- Before the editor, so the Sheet is drawn behind it. -->
        <controls:DocumentSheetBackdrop Grid.Column="1"
                                        HorizontalAlignment="Left" VerticalAlignment="Top"
                                        Width="{Binding ActualWidth, ElementName=Editor}"
                                        Height="{Binding ActualHeight, ElementName=Editor}"
                                        Visibility="{Binding IsPageViewEnabled, Converter={StaticResource BooleanToVisibilityConverter}}" />
        <controls:MarkdownRichEditor x:Name="Editor" Grid.Column="1"
                                     Markdown="{Binding ActiveSession.Markdown, UpdateSourceTrigger=PropertyChanged}" />
    </Grid>
</ScrollViewer>
```
