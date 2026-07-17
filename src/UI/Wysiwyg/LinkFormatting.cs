using System.Windows.Controls;
using System.Windows.Documents;
using UI.Core;

namespace UI.Wysiwyg;

/// <summary>
/// The Insert Link and Insert Image Formatting Actions, and the one shared definition of what a Link
/// and an Image look like in the Visual Document. The Projector composes each through the same
/// <see cref="ApplyLink"/> / <see cref="ApplyImage"/> seams, so Capture treats a user-inserted Link
/// and a loaded one uniformly (INV-018). Each asks through the <see cref="ILinkPrompt"/> port and
/// edits only on a usable answer (INV-030).
/// </summary>
internal static class LinkFormatting
{
    /// <summary>
    /// Tags <paramref name="hyperlink"/> as a Link to <paramref name="url"/>, exactly as the
    /// Projector does. The <see cref="LinkRole"/> is what Capture keys on to re-emit
    /// <c>[text](url)</c>; the <c>NavigateUri</c> is what makes it a real link on screen.
    /// </summary>
    /// <param name="hyperlink">The hyperlink carrying the Link's text.</param>
    /// <param name="url">The Link's destination URL.</param>
    /// <param name="title">The optional link title, or <see langword="null"/>.</param>
    internal static void ApplyLink(Hyperlink hyperlink, string url, string? title)
    {
        hyperlink.Tag = new LinkRole(url, title);
        if (Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var uri))
        {
            hyperlink.NavigateUri = uri;
        }
    }

    /// <summary>
    /// The Insert Link Formatting Action: asks <paramref name="prompt"/> for the Link's text and
    /// destination URL — seeded with the selection — and turns the selection into that Link. No edit
    /// is made when the Link Prompt is dismissed or gives no URL (INV-030).
    /// </summary>
    /// <param name="editor">The editor whose selection is being linked.</param>
    /// <param name="prompt">The Link Prompt to ask, or <see langword="null"/> to make no edit.</param>
    internal static void InsertLink(RichTextBox editor, ILinkPrompt? prompt)
    {
        if (Ask(editor, prompt, image: false) is not { } details)
        {
            return;
        }

        editor.BeginChange();
        try
        {
            // A Link with no text at all would be invisible in the Visual Document, which shows a
            // Link by its text alone — so it falls back to its own URL.
            var text = details.Text.Length > 0 ? details.Text : details.Url;
            var start = ReplaceSelection(editor);

            var hyperlink = new Hyperlink(new Run(text), start);
            ApplyLink(hyperlink, details.Url, title: null);
            editor.Selection.Select(hyperlink.ContentEnd, hyperlink.ContentEnd);
        }
        finally
        {
            editor.EndChange();
        }
    }

    /// <summary>
    /// The Insert Image Formatting Action: asks <paramref name="prompt"/> for the Image's alt text
    /// and source URL — seeded with the selection — and inserts that Image. No edit is made when the
    /// Link Prompt is dismissed or gives no URL (INV-030).
    /// </summary>
    /// <param name="editor">The editor the Image is inserted into.</param>
    /// <param name="prompt">The Link Prompt to ask, or <see langword="null"/> to make no edit.</param>
    /// <param name="baseDirectory">The Base Directory a relative Image Source resolves against, or
    /// <see langword="null"/> when the Editor Session has no file yet (INV-031).</param>
    internal static void InsertImage(RichTextBox editor, ILinkPrompt? prompt, string? baseDirectory)
    {
        if (Ask(editor, prompt, image: true) is not { } details)
        {
            return;
        }

        editor.BeginChange();
        try
        {
            var alt = details.Text.Length > 0 ? details.Text : details.Url;
            var start = ReplaceSelection(editor);

            // Composed through the same seam the Projector uses, so the Image the user just inserted
            // is identical to the one a reload would project (INV-018/031). It is placed by way of an
            // anchor because an Image's picture is an InlineUIContainer, which — unlike a Run — cannot
            // be constructed at a TextPointer.
            var anchor = new Run(string.Empty, start);
            var image = ImageFormatting.CreateImage(details.Url, alt, title: null, baseDirectory);
            anchor.SiblingInlines?.InsertAfter(anchor, image);
            anchor.SiblingInlines?.Remove(anchor);
            editor.Selection.Select(image.ContentEnd, image.ContentEnd);
        }
        finally
        {
            editor.EndChange();
        }
    }

    // Asks the Link Prompt, seeded with the selection, and keeps only a usable answer: a dismissed
    // prompt or a blank URL must leave the document untouched (INV-030).
    private static LinkDetails? Ask(RichTextBox editor, ILinkPrompt? prompt, bool image)
    {
        if (prompt is null)
        {
            return null;
        }

        var proposed = editor.Selection.Text;
        var details = image ? prompt.AskForImage(proposed) : prompt.AskForLink(proposed);

        return details is not null && !string.IsNullOrWhiteSpace(details.Url) ? details : null;
    }

    // Clears the selected text and returns the position the new inline goes at, so the Link replaces
    // what it was made from rather than sitting beside it.
    private static TextPointer ReplaceSelection(RichTextBox editor)
    {
        if (!editor.Selection.IsEmpty)
        {
            editor.Selection.Text = string.Empty;
        }

        return editor.Selection.Start;
    }
}
