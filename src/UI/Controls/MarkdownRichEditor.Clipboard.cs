using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media.Imaging;
using Domain;
using UI.Wysiwyg;

namespace UI.Controls;

// The clipboard behaviours: a Copy carries an HTML flavor and a Copy as Markdown carries the source
// text, so a selection pastes formatted into web editors and plain into source tools (INV-035); and
// Smart Paste turns a pasted URL into a Link, a pasted image into a file beside the document, and
// pasted HTML into formatted Markdown (INV-041).
public sealed partial class MarkdownRichEditor
{
    /// <summary>
    /// Identifies the <see cref="Renderer"/> dependency property. A Copy renders the selection to
    /// HTML through it for the clipboard's HTML flavor; the composition root supplies the real
    /// renderer. Left unset, Copy adds no HTML flavor (the built-in rich text is unaffected) (INV-035).
    /// </summary>
    public static readonly DependencyProperty RendererProperty = DependencyProperty.Register(
        nameof(Renderer),
        typeof(IMarkdownRenderer),
        typeof(MarkdownRichEditor),
        new PropertyMetadata(defaultValue: null));

    /// <summary>
    /// The renderer a Copy uses to render the selection to HTML for the clipboard's HTML flavor.
    /// Supplied by the composition root; when <see langword="null"/>, Copy adds no HTML flavor and the
    /// built-in rich text (RTF) is unaffected (INV-035).
    /// </summary>
    public IMarkdownRenderer? Renderer
    {
        get => (IMarkdownRenderer?)GetValue(RendererProperty);
        set => SetValue(RendererProperty, value);
    }

    /// <summary>
    /// Captures the Markdown source of the blocks the current selection spans. A partial selection
    /// captures the whole blocks it touches (whole-block granularity); an empty selection captures
    /// nothing. It reads the document and is not an edit (INV-035).
    /// </summary>
    /// <returns>The canonical Markdown source of the selected blocks; the empty string for no selection.</returns>
    public string CaptureSelection()
    {
        if (Selection.IsEmpty)
        {
            return string.Empty;
        }

        var start = Selection.Start;
        var end = Selection.End;
        var selectedBlocks = Document.Blocks.Where(block => Overlaps(block, start, end)).ToList();
        return _capturer.Capture(selectedBlocks);
    }

    /// <summary>
    /// Renders the current selection to the CF_HTML clipboard flavor, or <see langword="null"/> when
    /// there is no selection or no <see cref="Renderer"/>. A Copy adds this so a selection pastes
    /// formatted into web editors (INV-035).
    /// </summary>
    /// <returns>The CF_HTML string, or <see langword="null"/> when no HTML flavor should be added.</returns>
    public string? SelectionAsCfHtml()
    {
        if (Renderer is null)
        {
            return null;
        }

        var markdown = CaptureSelection();
        if (string.IsNullOrEmpty(markdown))
        {
            return null;
        }

        var html = Renderer.Render(new MarkdownDocument(markdown)).Html;
        return CfHtml.Wrap(html);
    }

    /// <summary>
    /// Applies Smart Paste to the given clipboard data: a URL pasted over a selection becomes a Link,
    /// an image is written beside the Watched File and inserted as an Image, and HTML converts to
    /// Markdown and inserts as formatted content. Returns whether it handled the paste (INV-041).
    /// </summary>
    /// <param name="source">The clipboard data being pasted.</param>
    /// <returns><see langword="true"/> if Smart Paste handled the paste; otherwise <see langword="false"/>.</returns>
    public bool SmartPaste(IDataObject source)
    {
        ArgumentNullException.ThrowIfNull(source);

        // A URL pasted over a selection turns the selection into a Link.
        if (!Selection.IsEmpty
            && source.GetDataPresent(DataFormats.UnicodeText)
            && source.GetData(DataFormats.UnicodeText) is string text
            && IsWebUrl(text))
        {
            LinkFormatting.WrapSelectionAsLink(this, text.Trim());
            return true;
        }

        // An image on the clipboard is written beside the Watched File and inserted as an Image.
        if (source.GetDataPresent(DataFormats.Bitmap) && source.GetData(DataFormats.Bitmap) is BitmapSource image)
        {
            return TryPasteImage(image);
        }

        // HTML converts to Markdown and inserts as formatted content.
        if (source.GetDataPresent(DataFormats.Html) && source.GetData(DataFormats.Html) is string cfHtml)
        {
            var markdown = HtmlToMarkdown.Convert(CfHtml.ExtractFragment(cfHtml));
            if (markdown.Length > 0)
            {
                InsertProjectedMarkdown(markdown);
                return true;
            }
        }

        return false;
    }

    // Adds the HTML flavor to a Copy (or Cut / drag) so the selection pastes formatted into web
    // editors; the RichTextBox's own RTF and text flavors are left untouched.
    private void OnCopying(object sender, DataObjectCopyingEventArgs e)
    {
        var cfHtml = SelectionAsCfHtml();
        if (cfHtml is not null)
        {
            e.DataObject.SetData(DataFormats.Html, cfHtml);
        }
    }

    // Copies the selection's Markdown source to the clipboard for Copy as Markdown.
    private void CopySelectionAsMarkdown()
    {
        var markdown = CaptureSelection();
        if (!string.IsNullOrEmpty(markdown))
        {
            Clipboard.SetText(markdown);
        }
    }

    // Whether a top-level Block's content range overlaps the selection [start, end].
    private static bool Overlaps(Block block, TextPointer start, TextPointer end) =>
        block.ContentStart.CompareTo(end) < 0 && block.ContentEnd.CompareTo(start) > 0;

    // Smart Paste (INV-041): inspects the clipboard data and, when it recognises a special case,
    // performs the paste itself and reports it handled so the default paste is cancelled.
    private void OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (SmartPaste(e.SourceDataObject ?? e.DataObject))
        {
            e.CancelCommand();
        }
    }

    // Writes a pasted image beside the Watched File and inserts it as an Image. An unsaved document has
    // no folder to write beside, so the image is dropped rather than pasted un-representably.
    private bool TryPasteImage(BitmapSource image)
    {
        if (string.IsNullOrEmpty(BaseDirectory))
        {
            return true;
        }

        try
        {
            var fileName = $"pasted-{Guid.NewGuid():N}.png";
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using (var stream = File.Create(Path.Combine(BaseDirectory, fileName)))
            {
                encoder.Save(stream);
            }

            LinkFormatting.InsertImageSource(this, fileName, "pasted image", BaseDirectory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A failed write drops the image rather than corrupting the document.
        }

        return true;
    }

    // Projects the given Markdown to a document fragment and inserts it at the selection, so pasted
    // HTML lands as formatted content the same as any projected Markdown. The projected blocks are
    // MOVED into the document (not serialised), so their roles survive and Capture re-emits the
    // Markdown — a XAML round-trip would drop the role Tags and paste plain text.
    private void InsertProjectedMarkdown(string markdown)
    {
        var fragment = _projector.Project(markdown, BaseDirectory);
        var fragmentBlocks = fragment.Blocks.ToList();
        if (fragmentBlocks.Count == 0)
        {
            return;
        }

        BeginChange();
        try
        {
            if (!Selection.IsEmpty)
            {
                Selection.Text = string.Empty;
            }

            var caretParagraph = Selection.Start.Paragraph;
            var topLevelCaret = caretParagraph is not null && ReferenceEquals(caretParagraph.Parent, Document);
            var insertAfter = topLevelCaret ? caretParagraph : Document.Blocks.LastBlock;

            foreach (var block in fragmentBlocks)
            {
                fragment.Blocks.Remove(block);
                if (insertAfter is null)
                {
                    Document.Blocks.Add(block);
                }
                else
                {
                    Document.Blocks.InsertAfter(insertAfter, block);
                }

                insertAfter = block;
            }

            // A caret in an empty placeholder paragraph leaves it behind; drop it so the paste does not
            // sit beneath a stray blank line.
            if (topLevelCaret && caretParagraph!.Inlines.Count == 0)
            {
                Document.Blocks.Remove(caretParagraph);
            }

            if (insertAfter is not null)
            {
                Selection.Select(insertAfter.ContentEnd, insertAfter.ContentEnd);
            }
        }
        finally
        {
            EndChange();
        }
    }

    private static bool IsWebUrl(string text)
    {
        var trimmed = text.Trim();
        return trimmed.Length > 0
            && !trimmed.Any(char.IsWhiteSpace)
            && Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
