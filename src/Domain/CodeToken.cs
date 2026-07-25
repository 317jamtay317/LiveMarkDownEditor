namespace Domain;

/// <summary>
/// One consecutive run of a Code Block's code whose characters share a single
/// <see cref="CodeTokenKind"/> — <c>public</c>, <c>"a string"</c>, <c>// a comment</c>. It is what
/// Syntax Highlighting colors (INV-064).
/// </summary>
/// <remarks>
/// A Code Token always names a real run of characters: it can be neither <see langword="null"/> nor
/// empty. An empty token would color nothing while letting a tokenizer pad its output without
/// changing the code, which is precisely the drift INV-064's lossless rule exists to catch.
/// Whitespace, by contrast, is legitimate and common — the gaps between colored tokens are carried
/// as <see cref="CodeTokenKind.Plain"/> tokens, and that is how the tokens of a Code Block
/// concatenate back to exactly the code that went in.
/// </remarks>
public sealed record CodeToken
{
    /// <summary>Creates a Code Token over a run of code.</summary>
    /// <param name="text">The run of code the token covers. Never empty.</param>
    /// <param name="kind">The kind of code <paramref name="text"/> is.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="text"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="kind"/> is not a defined <see cref="CodeTokenKind"/>.
    /// </exception>
    public CodeToken(string text, CodeTokenKind kind)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0)
        {
            throw new ArgumentException("A Code Token covers a run of code, so it is never empty.", nameof(text));
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown Code Token Kind.");
        }

        Text = text;
        Kind = kind;
    }

    /// <summary>The run of code this token covers, exactly as it appears in the Code Block.</summary>
    public string Text { get; }

    /// <summary>The kind of code <see cref="Text"/> is, which the palette colors it by.</summary>
    public CodeTokenKind Kind { get; }
}
