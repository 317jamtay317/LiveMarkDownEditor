using System.Windows;
using Domain;
using UI.Core;
using UI.Wysiwyg;

namespace UI.Controls;

// Mermaid Diagrams: rendering each diagram's picture inline through the injected renderer (cached by
// source and theme — INV-047), tracking the Diagram the caret is within for the Preview Panel, and
// opening the Flowchart Builder to author or edit one. Rendering is view-only; writing a diagram back
// is an edit that Captures like any other (INV-003/INV-053).
public sealed partial class MarkdownRichEditor
{
    /// <summary>
    /// Identifies the <see cref="FlowchartBuilder"/> dependency property. Open Flowchart Builder asks
    /// through it for the diagram to insert; the composition root supplies the real builder, and a test
    /// supplies a stub. Left unset, opening the builder does nothing (INV-053).
    /// </summary>
    public static readonly DependencyProperty FlowchartBuilderProperty = DependencyProperty.Register(
        nameof(FlowchartBuilder),
        typeof(IFlowchartBuilder),
        typeof(MarkdownRichEditor),
        new PropertyMetadata(defaultValue: null));

    /// <summary>
    /// Identifies the <see cref="DiagramImageRenderer"/> dependency property. Each Mermaid Diagram's
    /// picture is rendered through it and shown inline; the composition root supplies the WebView2
    /// renderer. Left unset, a diagram shows its source-text fallback instead (INV-047).
    /// </summary>
    public static readonly DependencyProperty DiagramImageRendererProperty = DependencyProperty.Register(
        nameof(DiagramImageRenderer),
        typeof(IMermaidImageRenderer),
        typeof(MarkdownRichEditor),
        new PropertyMetadata(defaultValue: null, OnDiagramImageRendererChanged));

    /// <summary>
    /// Identifies the <see cref="IsDarkTheme"/> dependency property. Bound to the active theme, it makes
    /// each Mermaid Diagram render in the editor's light/dark palette and re-render when it flips (INV-047).
    /// </summary>
    public static readonly DependencyProperty IsDarkThemeProperty = DependencyProperty.Register(
        nameof(IsDarkTheme),
        typeof(bool),
        typeof(MarkdownRichEditor),
        new PropertyMetadata(defaultValue: false, OnIsDarkThemeChanged));

    private static readonly DependencyPropertyKey CurrentDiagramSourcePropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(CurrentDiagramSource),
        typeof(string),
        typeof(MarkdownRichEditor),
        new PropertyMetadata(defaultValue: null));

    /// <summary>Identifies the read-only <see cref="CurrentDiagramSource"/> dependency property.</summary>
    public static readonly DependencyProperty CurrentDiagramSourceProperty = CurrentDiagramSourcePropertyKey.DependencyProperty;

    private readonly MermaidRenderCoordinator _diagramRenderer = new();

    /// <summary>
    /// The Flowchart Builder that Open Flowchart Builder asks for a diagram to insert. Left
    /// <see langword="null"/>, opening the builder makes no edit (INV-053).
    /// </summary>
    public IFlowchartBuilder? FlowchartBuilder
    {
        get => (IFlowchartBuilder?)GetValue(FlowchartBuilderProperty);
        set => SetValue(FlowchartBuilderProperty, value);
    }

    /// <summary>
    /// The renderer that turns each Mermaid Diagram's source into the picture shown inline. Left
    /// <see langword="null"/>, a diagram shows its source-text fallback (INV-047).
    /// </summary>
    public IMermaidImageRenderer? DiagramImageRenderer
    {
        get => (IMermaidImageRenderer?)GetValue(DiagramImageRendererProperty);
        set => SetValue(DiagramImageRendererProperty, value);
    }

    /// <summary>
    /// Whether the active theme is dark, so each Mermaid Diagram's picture matches the editor's palette.
    /// Flipping it re-renders the diagrams in the new theme; it is view-only (INV-047).
    /// </summary>
    public bool IsDarkTheme
    {
        get => (bool)GetValue(IsDarkThemeProperty);
        set => SetValue(IsDarkThemeProperty, value);
    }

    /// <summary>
    /// The source of the Mermaid Diagram the caret is currently within, or <see langword="null"/>
    /// when the caret is not inside one. The Preview Panel binds its Diagram Preview here; it is
    /// recomputed as the caret moves and on every edit, and reading it is view-only (INV-047).
    /// </summary>
    public string? CurrentDiagramSource => (string?)GetValue(CurrentDiagramSourceProperty);

    /// <summary>
    /// Opens the Flowchart Builder on the Mermaid Diagram at the caret — or a new diagram when the
    /// caret is not within one — and writes the diagram it returns back into the document. Cancelling
    /// makes no edit, as does a <see langword="null"/> <see cref="FlowchartBuilder"/> (INV-053).
    /// </summary>
    public void OpenFlowchartBuilderAtCaret()
    {
        var source = FlowchartBuilder?.Build(MermaidDiagram.SourceAt(CaretPosition));
        if (source is not null)
        {
            InsertOrReplaceDiagramAtCaret(source);
        }
    }

    /// <summary>
    /// Writes <paramref name="mermaidSource"/> into the document as a Mermaid Diagram — replacing the
    /// Mermaid Diagram at the caret, or inserting a new Code Block at the caret — Capturing canonical
    /// Markdown like any other edit (INV-053/INV-018).
    /// </summary>
    /// <param name="mermaidSource">The Mermaid source to write.</param>
    public void InsertOrReplaceDiagramAtCaret(string mermaidSource)
    {
        DiagramBlockEditing.InsertOrReplaceDiagramAtCaret(this, mermaidSource);
        _diagramRenderer.RenderAll(Document, DiagramImageRenderer, IsDarkTheme);
    }

    private static void OnDiagramImageRendererChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // The renderer usually binds after the first projection; render the diagrams already shown.
        var editor = (MarkdownRichEditor)d;
        editor._diagramRenderer.RenderAll(editor.Document, e.NewValue as IMermaidImageRenderer, editor.IsDarkTheme);
    }

    private static void OnIsDarkThemeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // A theme flip re-renders each diagram in the new palette (served from cache after the first time).
        var editor = (MarkdownRichEditor)d;
        editor._diagramRenderer.RenderAll(editor.Document, editor.DiagramImageRenderer, editor.IsDarkTheme);
    }

    // Updates the Diagram Preview seam — the Mermaid Diagram the caret is within, or null when the
    // caret is not inside one. Pure and view-only: it only reads the caret's Code Block (INV-047).
    private void UpdateDiagramSource() =>
        SetValue(CurrentDiagramSourcePropertyKey, MermaidDiagram.SourceAt(CaretPosition));
}
