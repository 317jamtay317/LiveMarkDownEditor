using UI.Core;

namespace UI.Controls;

/// <summary>
/// The live document status shown in the Status Bar: word and character counts and reading time
/// (recomputed as the document changes), and the caret's line and column and the Current Section
/// (updated as the caret moves). Presentation-only — reading it never changes the document (INV-039).
/// </summary>
public sealed class DocumentStatus : ObservableObject
{
    private int _wordCount;
    private int _characterCount;
    private TimeSpan _readingTime;
    private int _caretLine = 1;
    private int _caretColumn = 1;
    private string _currentSection = string.Empty;

    /// <summary>The document's word count.</summary>
    public int WordCount
    {
        get => _wordCount;
        set => Set(ref _wordCount, value);
    }

    /// <summary>The document's character count, whitespace included.</summary>
    public int CharacterCount
    {
        get => _characterCount;
        set => Set(ref _characterCount, value);
    }

    /// <summary>The estimated time to read the document's prose.</summary>
    public TimeSpan ReadingTime
    {
        get => _readingTime;
        set
        {
            if (Set(ref _readingTime, value))
            {
                Raise(nameof(ReadingTimeText));
            }
        }
    }

    /// <summary>The reading time rendered for the Status Bar, e.g. "3 min read".</summary>
    public string ReadingTimeText => _readingTime switch
    {
        { Ticks: 0 } => "0 min read",
        _ when _readingTime < TimeSpan.FromMinutes(1) => "< 1 min read",
        _ => $"{(int)Math.Ceiling(_readingTime.TotalMinutes)} min read",
    };

    /// <summary>The caret's 1-based line in the Visual Document.</summary>
    public int CaretLine
    {
        get => _caretLine;
        set => Set(ref _caretLine, value);
    }

    /// <summary>The caret's 1-based column on its line.</summary>
    public int CaretColumn
    {
        get => _caretColumn;
        set => Set(ref _caretColumn, value);
    }

    /// <summary>The Current Section's heading text, or the empty string when the caret precedes any heading.</summary>
    public string CurrentSection
    {
        get => _currentSection;
        set => Set(ref _currentSection, value);
    }
}
