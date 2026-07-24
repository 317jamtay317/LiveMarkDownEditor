# PrintPreviewPages

The **Print Preview**'s pages: the given document paginated **for real** — by WPF's own
`DocumentPaginator`, the same pagination Print hands to the printer — into a vertical stack of
**Pages** at the **Page Setup**'s oriented size and **Print Margins** (INV-061). Unlike the
[Document Sheet](DocumentSheetBackdrop.md), whose **Page Breaks** only mark boundaries without
repaginating, each page here holds exactly the content the printed page would.

- **Class:** `UI.Controls.PrintPreviewPages` (derives from `System.Windows.FrameworkElement`)
- **Hosted by:** `PrintPreviewWindow`, on a scrolling canvas beside its Print action, with its state in
  `PrintPreviewViewModel`
- **Shown through:** the `IPrintPreview` port (`WindowPrintPreview`), which the
  [MarkdownRichEditor](MarkdownRichEditor.md)'s `PrintPreview` routed command (Ctrl+Shift+P) reaches
  after re-projecting the whole Markdown source — so a Folded Section's hidden body is previewed too
  (INV-034)

## How it works

- **True pagination.** The document is given the Page Setup's oriented `PageWidth`/`PageHeight` and its
  Print Margins as `PagePadding`, then laid out by its own `DocumentPaginator` — `ComputePageCount`
  followed by `GetPage` per page. This is the very layout `PrintDialog.PrintDocument` prints, so the
  preview cannot disagree with the printer.
- **One visual per Page.** Each page is hosted as a paper rectangle (white, with a gray edge — paper is
  paper whatever the theme, because the preview shows the printout, not the editor) with the
  paginator's page visual over it, stacked vertically with a band of canvas between pages.
- **Re-paginates on change.** Setting `Document` or `Setup` clears the stack and paginates afresh.
- **Presentation-only.** The document handed over is self-contained — freshly projected, never the live
  editing surface — so previewing changes nothing (INV-061).

## Properties

| Property | Type | Description |
| --- | --- | --- |
| `Document` | `FlowDocument` | The self-contained document to paginate and preview. |
| `Setup` | `PageSetup` | The Page Setup the pages are laid out under. Left unset, the default (Portrait, Normal margins) applies (INV-061). |

## Usage

```xml
<ScrollViewer VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Auto"
              Background="{DynamicResource EditorCanvasBrush}">
    <controls:PrintPreviewPages Document="{Binding Document}" Setup="{Binding Setup}"
                                Margin="28" HorizontalAlignment="Center" />
</ScrollViewer>
```

The window it lives in is opened through the port, never constructed directly:

```csharp
// MarkdownRichEditor, on the PrintPreview routed command:
var document = _projector.Project(Markdown, BaseDirectory);
PrintPreview.Show(document, PageSetup ?? PageSetup.Default, "LiveMarkDownEditor document");
```
