using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
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
            onCorrect: replacement => ReplaceMisspelling(misspelling, replacement),
            onAddToDictionary: AddToDictionary);
    }

    // Accepts a Misspelling into the User Dictionary and re-checks, so it stops being marked (INV-040).
    private void AddToDictionary(string word)
    {
        SharedUserDictionary.Value.Add(word);
        _spellCheckAdorner?.Refresh();
    }

    // Swaps a Misspelling's span for the chosen Spelling Suggestion. Editing the Visual Document
    // Captures back into the Markdown source, so the correction flows through like any other edit.
    private void ReplaceMisspelling(TextRange? misspelling, string replacement)
    {
        if (misspelling is { Start.HasValidLayout: true, End.HasValidLayout: true })
        {
            misspelling.Text = replacement;
        }
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
