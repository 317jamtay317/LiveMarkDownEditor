# DocumentSheetBackdrop

The **DocumentSheetBackdrop** draws the **Document Sheet** itself in [Page View](PageView.md): the paper
the Visual Document is laid out on, and the **Page Break** rule where each 8.5 × 11 **Page** ends and the
next begins. It is the visible half of the Sheet; how tall the Sheet is — always a whole number of Pages
— is decided by the pure [`DocumentSheet`](../../src/UI/Controls/DocumentSheet.cs) rule and applied by
[PageView](PageView.md) (INV-058).

- **Class:** `UI.Controls.DocumentSheetBackdrop` (derives from `System.Windows.FrameworkElement`)
- **Placed by:** `MainWindow.xaml`, in the editing surface's Grid **before** the
  [MarkdownRichEditor](MarkdownRichEditor.md), so it is drawn behind it.

## Why behind the editor, not over it

A Page Break has to be a full-width rule across the Sheet — that is what makes a tall Sheet read as a
stack of pages rather than one long strip. Drawn as an adorner (the pattern the
[CodeShadingAdorner](CodeShadingAdorner.md) and the [ChangeHighlightAdorner](ChangeHighlightAdorner.md)
use) it would be drawn **over** the Visual Document, and a break that lands on a line of prose would
strike straight through it.

So the Sheet is drawn underneath instead: this element paints the paper and the rules, and Page View
makes the editor's own `Background` transparent so they show through. A Page Break then passes *under*
the text, the way a seam printed on paper would. Nothing else changes — the editor keeps its own border,
caret, selection, and every adorner it already carries.

Note what this does **not** do: content flows continuously across a Page Break. The rule marks where a
Page ends; it does not push the line that straddles it onto the next Page (INV-058).

## How it works

- **Sized to the editor, not the cell.** `Width` and `Height` are bound to the editor's `ActualWidth` /
  `ActualHeight`, with `HorizontalAlignment=Left` and `VerticalAlignment=Top`. The surface Grid can be
  stretched taller than the Sheet by the canvas it sits in, so stretching to the cell would paint paper
  below the Sheet's own bottom edge.
- **A rule per Page boundary.** `OnRender` fills its whole area with `SheetBrush`, then draws a one-unit
  rule at each multiple of `DocumentSheet.PageHeight` below the bottom edge — half-pixel offset so it
  lands on a device pixel instead of blurring across two. The Sheet's own bottom is a page edge already,
  so it gets no rule.
- **Recolour is free.** Both brushes are resource references (`EditorBackgroundBrush`, `PageBreakBrush`)
  declared `AffectsRender`, so a theme switch repaints the Sheet and never reflows the document.
- **Invisible outside Page View.** Its `Visibility` is bound to `WorkspaceViewModel.IsPageViewEnabled`;
  with Page View off the editor paints its own background again and this element is collapsed.
- **Never in the way.** `IsHitTestVisible` is false, so clicks, selection, and drag all land on the
  editor behind — or rather, in front of — it.

## Properties

| Property | Type | Description |
| --- | --- | --- |
| `SheetBrush` | `Brush` | The paper the Visual Document is laid out on. Defaults to the `EditorBackgroundBrush` of the active palette. |
| `PageBreakBrush` | `Brush` | The rule drawn where one Page ends and the next begins. Defaults to the `PageBreakBrush` of the active palette. |

## Usage

```xml
<!-- Before the editor in the same Grid cell, so it is drawn behind it. -->
<controls:DocumentSheetBackdrop Grid.Column="1"
                                HorizontalAlignment="Left" VerticalAlignment="Top"
                                Width="{Binding ActualWidth, ElementName=Editor}"
                                Height="{Binding ActualHeight, ElementName=Editor}"
                                Visibility="{Binding IsPageViewEnabled, Converter={StaticResource BooleanToVisibilityConverter}}" />
<controls:MarkdownRichEditor x:Name="Editor" Grid.Column="1" ... />
```
