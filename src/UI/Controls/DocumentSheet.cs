using System.Windows;

namespace UI.Controls;

/// <summary>
/// The pure rule for the Document Sheet — the fixed-width page the Visual Document is laid out on in
/// Page View. Fixing the width to one page confines every element it holds, tables included, to that
/// width instead of letting it stretch to the pane (INV-058).
/// </summary>
public static class DocumentSheet
{
    /// <summary>
    /// The Document Sheet's width, in device-independent units: the US Letter width of 8.5 inches at
    /// 96 units per inch (816). It is fixed, so the Visual Document's layout width does not track the
    /// editing surface — which is what confines a table to the page and stops a widen from reflowing
    /// the document (INV-058).
    /// </summary>
    public const double Width = 816d;

    /// <summary>
    /// The Document Sheet's page margins: the inset from the Sheet's edges to its content, the way a
    /// word processor leaves a margin around the page. Presentation-only (INV-058).
    /// </summary>
    public static Thickness PagePadding { get; } = new(left: 72d, top: 64d, right: 72d, bottom: 64d);
}
