using System.Windows;
using System.Windows.Controls;
using UI.Wysiwyg;

namespace UI.Controls;

/// <summary>
/// The single-pane WYSIWYG editing surface: a <see cref="RichTextBox"/> whose content is a Visual
/// Document projected from Markdown. Formatting is shown as formatting — the user never sees raw
/// Markdown syntax — while the canonical Markdown source is exposed through <see cref="Markdown"/>.
/// </summary>
/// <remarks>
/// This is a custom Control (the only place interaction logic lives outside a ViewModel). It keeps
/// <see cref="Markdown"/> and its <see cref="RichTextBox.Document"/> in sync: assigning
/// <see cref="Markdown"/> Projects it into the Visual Document; editing the Visual Document Captures
/// it back into <see cref="Markdown"/>. A re-entrancy guard prevents the two directions from
/// echoing each other.
/// </remarks>
public sealed class MarkdownRichEditor : RichTextBox
{
    /// <summary>
    /// Identifies the <see cref="Markdown"/> dependency property. Binds two-way by default so the
    /// canonical Markdown source flows to and from the bound Editor Session.
    /// </summary>
    public static readonly DependencyProperty MarkdownProperty = DependencyProperty.Register(
        nameof(Markdown),
        typeof(string),
        typeof(MarkdownRichEditor),
        new FrameworkPropertyMetadata(
            string.Empty,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnMarkdownChanged));

    private readonly MarkdownToFlowDocumentProjector _projector = new();
    private readonly FlowDocumentToMarkdownCapturer _capturer = new();
    private bool _isSynchronising;
    private string _lastCaptured = string.Empty;

    /// <summary>The canonical Markdown source text shown, and edited, as a Visual Document.</summary>
    public string Markdown
    {
        get => (string)GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    private static void OnMarkdownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var editor = (MarkdownRichEditor)d;
        if (editor._isSynchronising)
        {
            return;
        }

        var markdown = (string?)e.NewValue ?? string.Empty;
        if (markdown == editor._lastCaptured)
        {
            // This change is the echo of our own Capture; the Visual Document already reflects it.
            return;
        }

        editor.ProjectFromMarkdown(markdown);
    }

    private void ProjectFromMarkdown(string markdown)
    {
        _isSynchronising = true;
        try
        {
            Document = _projector.Project(markdown);
        }
        finally
        {
            _isSynchronising = false;
        }
    }

    /// <inheritdoc />
    protected override void OnTextChanged(TextChangedEventArgs e)
    {
        base.OnTextChanged(e);
        if (_isSynchronising)
        {
            return;
        }

        _isSynchronising = true;
        try
        {
            _lastCaptured = _capturer.Capture(Document);
            SetCurrentValue(MarkdownProperty, _lastCaptured);
        }
        finally
        {
            _isSynchronising = false;
        }
    }
}
