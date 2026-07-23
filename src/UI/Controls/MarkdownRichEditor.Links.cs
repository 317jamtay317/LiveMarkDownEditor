using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using UI.Core;
using UI.Wysiwyg;

namespace UI.Controls;

// Following a Link and Printing the document — the two ways content leaves the editor. Ctrl+Clicking a
// Link opens a web address in the browser and a Markdown file in a new Tab (INV-038); Print re-projects
// the whole Markdown source so a Folded Section's hidden body prints too (INV-034). Both read the
// document and make no edit.
public sealed partial class MarkdownRichEditor
{
    /// <summary>
    /// Identifies the <see cref="DocumentPrinter"/> dependency property. Print sends the Visual
    /// Document through it; the composition root supplies the real printer, and a test supplies a
    /// fake. Left unset, Print does nothing (INV-034).
    /// </summary>
    public static readonly DependencyProperty DocumentPrinterProperty = DependencyProperty.Register(
        nameof(DocumentPrinter),
        typeof(IDocumentPrinter),
        typeof(MarkdownRichEditor),
        new PropertyMetadata(defaultValue: null));

    /// <summary>
    /// Identifies the <see cref="FollowLinkCommand"/> dependency property. Ctrl+Clicking a Link to a
    /// Markdown file invokes it with the file's absolute path so the shell opens it in a new Tab; a
    /// web Link is opened in the browser without it (INV-038).
    /// </summary>
    public static readonly DependencyProperty FollowLinkCommandProperty = DependencyProperty.Register(
        nameof(FollowLinkCommand),
        typeof(ICommand),
        typeof(MarkdownRichEditor),
        new PropertyMetadata(defaultValue: null));

    /// <summary>
    /// The printer Print sends the Visual Document to. Supplied by the composition root; when
    /// <see langword="null"/>, Print does nothing (INV-034).
    /// </summary>
    public IDocumentPrinter? DocumentPrinter
    {
        get => (IDocumentPrinter?)GetValue(DocumentPrinterProperty);
        set => SetValue(DocumentPrinterProperty, value);
    }

    /// <summary>
    /// The command invoked to open a followed Markdown Link in a new Tab, with the file's absolute
    /// path as its parameter. Supplied by the composition root; when <see langword="null"/>, a
    /// Markdown Link is not followed (a web Link still opens in the browser) (INV-038).
    /// </summary>
    public ICommand? FollowLinkCommand
    {
        get => (ICommand?)GetValue(FollowLinkCommandProperty);
        set => SetValue(FollowLinkCommandProperty, value);
    }

    /// <summary>
    /// Follows a Link's destination: a web address opens in the default browser, a Markdown file opens
    /// in a new Tab through <see cref="FollowLinkCommand"/>, and anything else is left alone. Following
    /// reads the document and is not an edit (INV-038).
    /// </summary>
    /// <param name="uri">The Link's destination, as its <c>NavigateUri</c>.</param>
    public void FollowLink(Uri uri)
    {
        var target = MarkdownLink.Classify(uri, BaseDirectory);
        switch (target.Kind)
        {
            case LinkKind.Web:
                LaunchBrowser(target.Value);
                break;
            case LinkKind.MarkdownFile when FollowLinkCommand?.CanExecute(target.Value) == true:
                FollowLinkCommand.Execute(target.Value);
                break;
        }
    }

    /// <summary>
    /// Prints the whole document. The Visual Document is re-projected from the current
    /// <see cref="Markdown"/> source rather than taken from the live editing surface, so a Folded
    /// Section's hidden Section Body prints too — Print means the whole document, never merely the
    /// visible part (INV-034, the fold rule of INV-032 reached from printing) — and the surface the
    /// user is editing is left undisturbed. Printing reads the document and writes no file the editor
    /// owns, so it is not an edit. Does nothing when no <see cref="DocumentPrinter"/> is set.
    /// </summary>
    public void PrintVisualDocument()
    {
        if (DocumentPrinter is null)
        {
            return;
        }

        var document = _projector.Project(Markdown, BaseDirectory);
        DocumentPrinter.Print(document, "LiveMarkDownEditor document");
    }

    private void OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        FollowLink(e.Uri);
        e.Handled = true;
    }

    // Opens a web address in the default browser. A platform boundary — the shell picks the browser.
    private static void LaunchBrowser(string url) =>
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
}
