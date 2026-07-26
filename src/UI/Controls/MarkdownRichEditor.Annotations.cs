using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using UI.Find;
using UI.Spelling;

namespace UI.Controls;

// The editing annotations drawn over the surface: Code Shading behind code, the camelCase-aware spell
// checker's squiggles, and the right-click menu that offers a Misspelling's Spelling Suggestions and
// accepts a word into the User Dictionary (INV-040/INV-057). Each adorner is attached once the editor
// has an AdornerLayer and thereafter repaints itself.
public sealed partial class MarkdownRichEditor
{
    // The User Dictionary of accepted words, shared across sessions and persisted to per-user storage.
    private static readonly Lazy<IUserDictionary> SharedUserDictionary = new(CreateUserDictionary);

    // Created lazily the first time the dictionary is needed, and shared across sessions. The
    // operating system's speller, made aware of the User Dictionary so an accepted word is not a
    // Misspelling (INV-040).
    private static readonly Lazy<ISpellDictionary> SharedDictionary = new(() =>
        new UserAwareSpellDictionary(new WindowsSpellDictionary(), SharedUserDictionary.Value));

    private CodeShadingAdorner? _codeShadingAdorner;
    private SpellCheckAdorner? _spellCheckAdorner;

    private static IUserDictionary CreateUserDictionary() => new FileUserDictionary(System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LiveMarkDownEditor",
        "user-dictionary.txt"));

    // Refills the right-click menu on demand: when the pointer is over a Misspelling, its Spelling
    // Suggestions head the menu (choosing one replaces the word), followed by the usual clipboard
    // commands. Over correctly-spelled text it is just the clipboard commands.
    private void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        var clickPosition = e.CursorLeft < 0
            ? CaretPosition                                                  // opened from the keyboard
            : GetPositionFromPoint(Mouse.GetPosition(this), snapToText: true);
        var misspelling = _spellCheckAdorner?.MisspellingAt(clickPosition);

        EditorContextMenu.Fill(
            ContextMenu,
            misspelling?.Text,
            SharedDictionary.Value,
            commandTarget: this,
            onCorrect: replacement => CorrectMisspelling(misspelling, replacement),
            onAddToDictionary: AddToDictionary);
    }

    /// <summary>
    /// Swaps a Misspelling's span for the chosen Spelling Suggestion. The Suggestion inherits the
    /// Misspelling's formatting — correcting a word inside bold text leaves it bold — through the same
    /// <see cref="MatchReplacer"/> a Replace runs through, and the edit Captures back into the Markdown
    /// source like any other (INV-022).
    /// </summary>
    /// <param name="misspelling">
    /// The Misspelling's span. A <see langword="null"/> range, or one left over from a document that
    /// has since been replaced, corrects nothing rather than editing the wrong document.
    /// </param>
    /// <param name="suggestion">The chosen Spelling Suggestion, inserted verbatim.</param>
    internal void CorrectMisspelling(TextRange? misspelling, string suggestion)
    {
        if (misspelling is null || !misspelling.Start.IsInSameDocument(Document.ContentStart))
        {
            return;
        }

        // Replacing is a delete and an insert; group them so one Ctrl+Z undoes the whole correction.
        BeginChange();
        try
        {
            MatchReplacer.Replace(misspelling, suggestion);
        }
        finally
        {
            EndChange();
        }
    }

    // Accepts a Misspelling into the User Dictionary and re-checks, so it stops being marked (INV-040).
    private void AddToDictionary(string word)
    {
        SharedUserDictionary.Value.Add(word);
        _spellCheckAdorner?.Refresh();
    }

    // Attaches the Code Shading adorner once, when the editor first has an AdornerLayer. The adorner
    // then watches the editor for edits and repaints the shade behind code itself.
    private void AttachCodeShading()
    {
        if (_codeShadingAdorner is not null)
        {
            return;
        }

        var layer = AdornerLayer.GetAdornerLayer(this);
        if (layer is null)
        {
            return;
        }

        _codeShadingAdorner = new CodeShadingAdorner(this);
        layer.Add(_codeShadingAdorner);
    }

    // Attaches the spell-check adorner once, when the editor first has an AdornerLayer. The adorner
    // then watches the editor for edits and repaints its squiggles itself.
    private void AttachSpellCheck()
    {
        if (_spellCheckAdorner is not null)
        {
            return;
        }

        var layer = AdornerLayer.GetAdornerLayer(this);
        if (layer is null)
        {
            return;
        }

        _spellCheckAdorner = new SpellCheckAdorner(this, SharedDictionary.Value);
        layer.Add(_spellCheckAdorner);
    }
}
