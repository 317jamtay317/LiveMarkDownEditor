namespace Domain;

/// <summary>
/// Port for tokenizing a Code Block's code into the <see cref="CodeToken"/>s that Syntax
/// Highlighting colors (INV-064). The Domain owns this contract; an adapter realises it — the
/// shipped adapter drives ColorCode's grammars — so the Domain stays free of any one tokenizer.
/// </summary>
/// <remarks>
/// Every consumer of this port — the Visual Document, an Export as HTML, an Export as PDF, and a
/// printout — colors from the same tokens, so none of them can drift from the others.
/// </remarks>
public interface ISyntaxHighlighter
{
    /// <summary>
    /// Tokenizes <paramref name="code"/> as <paramref name="language"/>.
    /// </summary>
    /// <param name="code">The Code Block's code, exactly as the document holds it.</param>
    /// <param name="language">
    /// The Code Block's fence info string. <see langword="null"/>, empty, or a language no grammar
    /// claims means the Code Block has no Highlighting Language.
    /// </param>
    /// <returns>
    /// The Code Tokens in document order, whose <see cref="CodeToken.Text"/> concatenates back to
    /// exactly <paramref name="code"/>. A Code Block with no Highlighting Language — and code that
    /// is empty — yields no tokens at all, which shows as plain code rather than a guess.
    /// </returns>
    IReadOnlyList<CodeToken> Highlight(string code, string? language);
}
