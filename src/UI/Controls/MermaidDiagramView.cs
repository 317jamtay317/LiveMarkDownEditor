using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace UI.Controls;

/// <summary>
/// The inline picture of a Mermaid Diagram in the Visual Document: it shows the diagram's rendered
/// image once the editor's render coordinator supplies it (<see cref="Rendered"/>), and until then — or
/// if the diagram cannot be rendered — falls back to showing its source text, so a diagram never leaves
/// a hole (INV-047). Double-clicking it opens the Flowchart Builder (handled by the editor).
/// </summary>
/// <remarks>
/// Authored as a custom Control that builds its content in code, per the project's Control exception to
/// the zero-code-behind rule — the same pattern as <see cref="MermaidPreview"/>. It holds no diagram
/// state that Capture reads; the source is carried on its host <c>BlockUIContainer</c>'s
/// <c>MermaidDiagramRole</c>, so the picture is purely presentation (INV-047).
/// </remarks>
public sealed class MermaidDiagramView : ContentControl
{
    /// <summary>Identifies the <see cref="Source"/> dependency property — the diagram's Mermaid source.</summary>
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source),
        typeof(string),
        typeof(MermaidDiagramView),
        new PropertyMetadata(defaultValue: null, OnVisualInputChanged));

    /// <summary>Identifies the <see cref="Rendered"/> dependency property — the rendered picture, or null.</summary>
    public static readonly DependencyProperty RenderedProperty = DependencyProperty.Register(
        nameof(Rendered),
        typeof(ImageSource),
        typeof(MermaidDiagramView),
        new PropertyMetadata(defaultValue: null, OnVisualInputChanged));

    private static readonly FontFamily MonospaceFont = new("Consolas, Cascadia Mono, Courier New");

    /// <summary>Creates the inline diagram picture, showing its source until the rendered image arrives.</summary>
    public MermaidDiagramView()
    {
        HorizontalAlignment = HorizontalAlignment.Left;
        Cursor = Cursors.Hand; // double-click to edit in the Flowchart Builder
        Focusable = false;
        Rebuild();
    }

    /// <summary>The Mermaid Diagram source this picture renders (and the builder edits).</summary>
    public string? Source
    {
        get => (string?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>The rendered picture, supplied by the editor's coordinator; null shows the source fallback.</summary>
    public ImageSource? Rendered
    {
        get => (ImageSource?)GetValue(RenderedProperty);
        set => SetValue(RenderedProperty, value);
    }

    private static void OnVisualInputChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((MermaidDiagramView)d).Rebuild();

    private void Rebuild() => Content = Rendered is not null ? BuildImage(Rendered) : BuildFallback(Source ?? string.Empty);

    private static UIElement BuildImage(ImageSource image) => new Image
    {
        Source = image,
        Stretch = Stretch.None,
        HorizontalAlignment = HorizontalAlignment.Left,
        SnapsToDevicePixels = true,
    };

    // Until the picture renders (or if it cannot), show the diagram's source in a code-like box, so the
    // diagram is never an empty hole (the Image fallback of INV-031, reached for a diagram — INV-047).
    private static UIElement BuildFallback(string source)
    {
        var text = new TextBlock
        {
            Text = source,
            FontFamily = MonospaceFont,
            FontSize = 12,
        };
        text.SetResourceReference(TextBlock.ForegroundProperty, "MutedTextBrush");

        var border = new Border
        {
            Child = text,
            Padding = new Thickness(10),
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        border.SetResourceReference(Border.BackgroundProperty, "SurfaceBrush");
        border.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        return border;
    }
}
