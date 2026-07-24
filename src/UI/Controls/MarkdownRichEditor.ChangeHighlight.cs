using System.Windows;
using System.Windows.Documents;
using Domain;

namespace UI.Controls;

/// <summary>
/// The Change Highlight half of the editor: it takes the Changed Regions of a live reload and shades
/// what they touched, briefly, through a read-only overlay (INV-060).
/// </summary>
public partial class MarkdownRichEditor
{
    /// <summary>Identifies the <see cref="ChangedRegions"/> dependency property.</summary>
    public static readonly DependencyProperty ChangedRegionsProperty = DependencyProperty.Register(
        nameof(ChangedRegions),
        typeof(IReadOnlyList<ChangedRegion>),
        typeof(MarkdownRichEditor),
        new PropertyMetadata(null, OnChangedRegionsChanged));

    private ChangeHighlightAdorner? _changeHighlightAdorner;

    /// <summary>
    /// The Changed Regions of the live reload just applied, numbered within the Markdown now shown.
    /// Setting them shows the Change Highlight; setting an empty set takes it down. It is bound from
    /// the Editor Session, which publishes the regions after replacing the source text — so the
    /// document they are resolved against is always the one they describe (INV-060).
    /// </summary>
    public IReadOnlyList<ChangedRegion>? ChangedRegions
    {
        get => (IReadOnlyList<ChangedRegion>?)GetValue(ChangedRegionsProperty);
        set => SetValue(ChangedRegionsProperty, value);
    }

    private static void OnChangedRegionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((MarkdownRichEditor)d).ShowChangeHighlight();

    // Attaches the Change Highlight adorner once the editor has an AdornerLayer. It is attached first
    // of the editor's overlays, so its shade sits beneath the Code Shading, the spelling squiggles,
    // and the Find highlights rather than washing them out.
    private void AttachChangeHighlight()
    {
        if (_changeHighlightAdorner is not null)
        {
            return;
        }

        var layer = AdornerLayer.GetAdornerLayer(this);
        if (layer is null)
        {
            return;
        }

        _changeHighlightAdorner = new ChangeHighlightAdorner(this);
        layer.Add(_changeHighlightAdorner);
    }

    private void ShowChangeHighlight()
    {
        // Regions can arrive before the editor is in the visual tree (a reload into a Tab that is not
        // the Active Session); attaching on demand means the highlight is simply skipped rather than
        // shown out of context when that Tab is later selected.
        AttachChangeHighlight();
        _changeHighlightAdorner?.Show(ChangedRegions);
    }
}
