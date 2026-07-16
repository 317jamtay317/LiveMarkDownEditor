namespace UI.Core;

/// <summary>The text and destination the user gave the Link Prompt for a Link or an Image.</summary>
/// <param name="Text">The Link's text, or the Image's alt text.</param>
/// <param name="Url">The Link's destination URL, or the Image's source URL.</param>
public sealed record LinkDetails(string Text, string Url);

/// <summary>
/// Asks the user for a Link's text and destination URL, or an Image's alt text and source URL, so
/// Insert Link and Insert Image (INV-030) can run without the editor depending on WPF dialog types
/// (keeping them unit-testable). The Link Prompt is the one place a URL is typed: the Visual
/// Document never shows raw <c>[text](url)</c> syntax, so a destination has nowhere else to be
/// edited.
/// </summary>
public interface ILinkPrompt
{
    /// <summary>Asks for a Link's text and destination URL.</summary>
    /// <param name="proposedText">The selected text, offered as the Link's text. May be empty.</param>
    /// <returns>The user's answer, or <see langword="null"/> if the Link Prompt was dismissed.</returns>
    LinkDetails? AskForLink(string proposedText);

    /// <summary>Asks for an Image's alt text and source URL.</summary>
    /// <param name="proposedAlt">The selected text, offered as the Image's alt text. May be empty.</param>
    /// <returns>The user's answer, or <see langword="null"/> if the Link Prompt was dismissed.</returns>
    LinkDetails? AskForImage(string proposedAlt);
}
