using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace UI.Controls;

/// <summary>
/// Where the editing surface's text column ends, in the surface's own coordinates. A full-width
/// block shade — Code Shading (INV-017) and the Change Highlight (INV-060) — stops there.
/// </summary>
/// <remarks>
/// Both shades already start on the column: a block's first character sits one block-padding inside
/// its left edge, so subtracting that padding lands exactly on the column start. The right edge has
/// no such landmark, and taking the control's full width instead overhangs the column by whatever
/// padding the surface carries. That is barely visible in the ordinary view (11px), and glaring in
/// Page View, where the control's padding is the Page's Margins — a one-inch margin left the shade
/// running to the paper's edge on the right while sitting an inch clear of it on the left.
/// <para>
/// The column is derived from the hosting <see cref="ScrollViewer"/>'s viewport rather than from the
/// control's width, because the viewport is already net of both the control's padding and the
/// vertical scrollbar — so a shade never runs underneath the scrollbar either.
/// </para>
/// </remarks>
internal static class EditorTextColumn
{
    /// <summary>The right edge of <paramref name="editor"/>'s text column.</summary>
    /// <param name="editor">The editing surface to measure.</param>
    /// <returns>The column's right edge, in <paramref name="editor"/>'s coordinates.</returns>
    internal static double RightEdge(RichTextBox editor) =>
        RightEdge(
            editor.ActualWidth,
            editor.Padding,
            editor.Document?.PagePadding ?? default,
            ViewportWidthOf(editor));

    /// <summary>
    /// The right edge of a text column with the given metrics — the arithmetic on its own, so it can
    /// be checked without a rendered control.
    /// </summary>
    /// <param name="actualWidth">The surface's rendered width.</param>
    /// <param name="padding">The surface's padding. In Page View this is the Page's Margins.</param>
    /// <param name="pagePadding">
    /// The document's page padding, which insets the text within the viewport. <c>Auto</c> (NaN) counts as none.
    /// </param>
    /// <param name="viewportWidth">
    /// The hosting scroll viewport's width — already net of the padding and of any vertical
    /// scrollbar. <see langword="null"/> before the template is applied, which falls back to the
    /// surface's own width and padding.
    /// </param>
    /// <returns>The column's right edge, never left of where the column starts.</returns>
    internal static double RightEdge(double actualWidth, Thickness padding, Thickness pagePadding, double? viewportWidth)
    {
        var inset = double.IsNaN(pagePadding.Right) ? 0 : pagePadding.Right;
        var left = padding.Left + (double.IsNaN(pagePadding.Left) ? 0 : pagePadding.Left);

        var right = viewportWidth is { } viewport
            ? padding.Left + viewport - inset
            : actualWidth - padding.Right - inset;

        return Math.Max(right, left);
    }

    // The ScrollViewer the control's template hosts the document in. Walked rather than looked up by
    // name so this holds for any RichTextBox template, and re-walked per call because the template
    // is applied after construction.
    private static double? ViewportWidthOf(DependencyObject node)
    {
        if (node is ScrollViewer viewer)
        {
            return viewer.ViewportWidth;
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
        {
            if (ViewportWidthOf(VisualTreeHelper.GetChild(node, i)) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
