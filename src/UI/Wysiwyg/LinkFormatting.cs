using System.Windows.Controls;
using System.Windows.Documents;
using UI.Core;

namespace UI.Wysiwyg;

/// <summary>
/// The Insert Link, Insert Image, and Insert Video Formatting Actions, and the one shared definition of
/// what a Link looks like in the Visual Document. Each composes through the same seam the Projector
/// does — <see cref="ApplyLink"/> here, <see cref="ImageFormatting"/> and <see cref="VideoFormatting"/>
/// for the other two — so Capture treats a user-inserted Link, Image, or Video and a loaded one
/// uniformly (INV-018). Each asks through the <see cref="ILinkPrompt"/> port and edits only on a usable
/// answer (INV-030).
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
        if (Ask(editor, prompt, LinkPromptKind.Link) is not { } details)
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
        if (Ask(editor, prompt, LinkPromptKind.Image) is not { } details)
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

    /// <summary>
    /// The Insert Video Formatting Action: asks <paramref name="prompt"/> for the Video's alt text and
    /// Media Source — seeded with the selection — and inserts that Video, paused. No edit is made when
    /// the Link Prompt is dismissed or gives no URL (INV-030/069).
    /// </summary>
    /// <param name="editor">The editor the Video is inserted into.</param>
    /// <param name="prompt">The Link Prompt to ask, or <see langword="null"/> to make no edit.</param>
    /// <param name="baseDirectory">The Base Directory a relative Media Source resolves against, or
    /// <see langword="null"/> when the Editor Session has no file yet (INV-031).</param>
    internal static void InsertVideo(RichTextBox editor, ILinkPrompt? prompt, string? baseDirectory)
    {
        if (Ask(editor, prompt, LinkPromptKind.Video) is not { } details)
        {
            return;
        }

        editor.BeginChange();
        try
        {
            var alt = details.Text.Length > 0 ? details.Text : details.Url;
            var start = ReplaceSelection(editor);

            // Composed through the same seam the Projector uses, so the Video the user just inserted is
            // identical to the one a reload would project (INV-018/069). It is placed by way of an anchor
            // because a Video Player is an InlineUIContainer, which — unlike a Run — cannot be
            // constructed at a TextPointer.
            var anchor = new Run(string.Empty, start);
            var video = VideoFormatting.CreateVideo(details.Url, alt, title: null, baseDirectory);
            anchor.SiblingInlines?.InsertAfter(anchor, video);
            anchor.SiblingInlines?.Remove(anchor);
            editor.Selection.Select(video.ContentEnd, video.ContentEnd);
        }
        finally
        {
            editor.EndChange();
        }
    }

    /// <summary>
    /// Turns the current selection into a Link to <paramref name="url"/>, keeping the selected text as
    /// the Link's text. Used by Smart Paste when a URL is pasted over a selection (INV-041). Does
    /// nothing when the selection is empty.
    /// </summary>
    /// <param name="editor">The editor whose selection is being linked.</param>
    /// <param name="url">The destination URL, already known (no prompt).</param>
    internal static void WrapSelectionAsLink(RichTextBox editor, string url)
    {
        NarrowSelectionToItsCore(editor);
        var text = VisualDocumentTraversal.TextIn(editor.Selection);
        if (text.Length == 0)
        {
            return;
        }

        editor.BeginChange();
        try
        {
            var start = ReplaceSelection(editor);
            var hyperlink = new Hyperlink(new Run(text), start);
            ApplyLink(hyperlink, url, title: null);
            editor.Selection.Select(hyperlink.ContentEnd, hyperlink.ContentEnd);
        }
        finally
        {
            editor.EndChange();
        }
    }

    /// <summary>
    /// Inserts an Image with the given source and alt text at the selection, through the same seam the
    /// Projector uses (INV-018/031). Used by Smart Paste when an image is pasted (INV-041).
    /// </summary>
    /// <param name="editor">The editor the Image is inserted into.</param>
    /// <param name="source">The Image Source (a path or URL), captured as <c>![alt](source)</c>.</param>
    /// <param name="alt">The Image's alt text.</param>
    /// <param name="baseDirectory">The Base Directory a relative source resolves against (INV-031).</param>
    internal static void InsertImageSource(RichTextBox editor, string source, string alt, string? baseDirectory)
    {
        editor.BeginChange();
        try
        {
            var start = ReplaceSelection(editor);
            var anchor = new Run(string.Empty, start);
            var image = ImageFormatting.CreateImage(source, alt, title: null, baseDirectory);
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
    // prompt or a blank URL must leave the document untouched (INV-030). The selection is narrowed to
    // its non-whitespace core first, so the space a double-click swept up neither shows in the Link
    // Prompt as a stray character nor ends up inside the Link's brackets (INV-018).
    private static LinkDetails? Ask(RichTextBox editor, ILinkPrompt? prompt, LinkPromptKind kind)
    {
        if (prompt is null)
        {
            return null;
        }

        NarrowSelectionToItsCore(editor);
        var proposed = VisualDocumentTraversal.TextIn(editor.Selection);
        var details = kind switch
        {
            LinkPromptKind.Image => prompt.AskForImage(proposed),
            LinkPromptKind.Video => prompt.AskForVideo(proposed),
            _ => prompt.AskForLink(proposed),
        };

        return details is not null && !string.IsNullOrWhiteSpace(details.Url) ? details : null;
    }

    // Trims the whitespace a double-click swept into the selection back out of it, leaving the caret
    // where it was when there is nothing but whitespace to select.
    private static void NarrowSelectionToItsCore(RichTextBox editor)
    {
        if (editor.Selection.IsEmpty)
        {
            return;
        }

        var core = VisualDocumentTraversal.WithoutSurroundingWhitespace(editor.Selection);
        editor.Selection.Select(core.Start, core.End);
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
