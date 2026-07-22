using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UI.Spelling;

namespace UI.Controls;

/// <summary>
/// Fills the editing surface's right-click menu: a Misspelling's Spelling Suggestions and Add to
/// Dictionary when the pointer is over one, then the clipboard commands. Over correctly-spelled text
/// it is the clipboard commands alone.
/// </summary>
/// <remarks>
/// The menu's <see cref="ContextMenu"/> is created once and refilled here on every opening, never
/// replaced. WPF's own text-editor context menu is a class handler on <see cref="RichTextBox"/> that
/// runs <em>before</em> any instance handler: seeing no menu of the editor's own it builds and opens
/// its own, marking the event handled — which would leave the editor's
/// <see cref="FrameworkElement.ContextMenuOpening"/> handler unreached, and the Spelling Suggestions
/// forever unseen. Owning a <see cref="ContextMenu"/> from construction is what makes WPF stand aside.
/// </remarks>
public static class EditorContextMenu
{
    /// <summary>The entry shown in place of Spelling Suggestions when the Dictionary offers none.</summary>
    public const string NoSuggestionsHeader = "No suggestions";

    /// <summary>The entry that accepts a Misspelling into the User Dictionary (INV-040).</summary>
    public const string AddToDictionaryHeader = "Add to Dictionary";

    /// <summary>
    /// Refills <paramref name="menu"/> for a right-click, replacing whatever it held before.
    /// </summary>
    /// <param name="menu">The editor's own context menu, refilled in place.</param>
    /// <param name="misspelling">
    /// The Misspelling under the pointer, or <see langword="null"/> when the pointer is not over one —
    /// in which case the menu is the clipboard commands alone.
    /// </param>
    /// <param name="dictionary">The Dictionary asked for the Misspelling's Spelling Suggestions.</param>
    /// <param name="commandTarget">The editor the clipboard and correction commands act on.</param>
    /// <param name="onCorrect">Called with the chosen Spelling Suggestion when one is picked.</param>
    /// <param name="onAddToDictionary">Called with the Misspelling when Add to Dictionary is chosen.</param>
    /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
    public static void Fill(
        ContextMenu menu,
        string? misspelling,
        ISpellDictionary dictionary,
        IInputElement commandTarget,
        Action<string> onCorrect,
        Action<string> onAddToDictionary)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(dictionary);
        ArgumentNullException.ThrowIfNull(commandTarget);
        ArgumentNullException.ThrowIfNull(onCorrect);
        ArgumentNullException.ThrowIfNull(onAddToDictionary);

        menu.Items.Clear();
        if (misspelling is not null)
        {
            AddSpellingEntries(menu, misspelling, dictionary, onCorrect, onAddToDictionary);
            menu.Items.Add(new Separator());
        }

        AddClipboardEntries(menu, commandTarget);
    }

    private static void AddSpellingEntries(
        ContextMenu menu,
        string misspelling,
        ISpellDictionary dictionary,
        Action<string> onCorrect,
        Action<string> onAddToDictionary)
    {
        var suggestions = SpellingSuggestions.For(misspelling, dictionary);
        if (suggestions.Count == 0)
        {
            menu.Items.Add(new MenuItem { Header = NoSuggestionsHeader, IsEnabled = false });
        }

        foreach (var suggestion in suggestions)
        {
            // The Suggestions are set in semi-bold because they are the reason the menu was opened;
            // the commands below are the everyday entries the user already knows.
            var replacement = suggestion;
            var item = new MenuItem { Header = suggestion, FontWeight = FontWeights.SemiBold };
            item.Click += (_, _) => onCorrect(replacement);
            menu.Items.Add(item);
        }

        var word = misspelling;
        var addToDictionary = new MenuItem { Header = AddToDictionaryHeader };
        addToDictionary.Click += (_, _) => onAddToDictionary(word);
        menu.Items.Add(addToDictionary);
    }

    private static void AddClipboardEntries(ContextMenu menu, IInputElement commandTarget)
    {
        menu.Items.Add(new MenuItem
        {
            Header = "Cut", Command = ApplicationCommands.Cut, CommandTarget = commandTarget,
        });
        menu.Items.Add(new MenuItem
        {
            Header = "Copy", Command = ApplicationCommands.Copy, CommandTarget = commandTarget,
        });
        menu.Items.Add(new MenuItem
        {
            Header = "Copy as Markdown",
            Command = MarkdownEditingCommands.CopyAsMarkdown,
            CommandTarget = commandTarget,
        });
        menu.Items.Add(new MenuItem
        {
            Header = "Paste", Command = ApplicationCommands.Paste, CommandTarget = commandTarget,
        });
    }
}
