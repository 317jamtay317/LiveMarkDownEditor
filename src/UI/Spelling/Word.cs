namespace UI.Spelling;

/// <summary>
/// A single word (or sub-word of a camelCase identifier) found in a piece of text, together with its
/// position, so a spell checker can flag exactly that span.
/// </summary>
/// <param name="Text">The word's text.</param>
/// <param name="Start">The zero-based index of the word's first character within the source text.</param>
/// <param name="Length">The number of characters in the word.</param>
public readonly record struct Word(string Text, int Start, int Length);
