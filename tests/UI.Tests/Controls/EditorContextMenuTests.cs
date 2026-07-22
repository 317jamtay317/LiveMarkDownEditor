using System.Windows;
using System.Windows.Controls;
using Shouldly;
using UI.Controls;
using UI.Tests.TestDoubles;
using UI.Tests.Wysiwyg;
using Xunit;

namespace UI.Tests.Controls;

/// <summary>
/// Tests for the editing surface's right-click menu: right-clicking a Misspelling offers its Spelling
/// Suggestions and Add to Dictionary above the clipboard commands, and right-clicking correctly-spelled
/// text offers the clipboard commands alone.
/// </summary>
public sealed class EditorContextMenuTests
{
    private static IReadOnlyList<string> HeadersOf(ContextMenu menu) =>
        menu.Items.OfType<MenuItem>().Select(item => (string)item.Header).ToArray();

    private static void Click(ContextMenu menu, string header) =>
        menu.Items.OfType<MenuItem>()
            .First(item => (string)item.Header == header)
            .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

    [Fact]
    public void Fill_OverAMisspelling_OffersItsSpellingSuggestionsFirst()
    {
        StaThread.Run(() =>
        {
            var dictionary = new StubSpellDictionary("wrods") { Suggestions = ["words", "word's"] };
            var menu = new ContextMenu();

            EditorContextMenu.Fill(menu, "wrods", dictionary, new RichTextBox(), _ => { }, _ => { });

            HeadersOf(menu).Take(2).ShouldBe(["words", "word's"]);
        });
    }

    [Fact]
    public void Fill_OverAMisspelling_OffersAddToDictionary_INV040()
    {
        StaThread.Run(() =>
        {
            var dictionary = new StubSpellDictionary("wrods") { Suggestions = ["words"] };
            var menu = new ContextMenu();

            EditorContextMenu.Fill(menu, "wrods", dictionary, new RichTextBox(), _ => { }, _ => { });

            HeadersOf(menu).ShouldContain(EditorContextMenu.AddToDictionaryHeader);
        });
    }

    [Fact]
    public void Fill_OverAMisspellingWithNoSuggestions_SaysSo()
    {
        StaThread.Run(() =>
        {
            var dictionary = new StubSpellDictionary("qqzz");
            var menu = new ContextMenu();

            EditorContextMenu.Fill(menu, "qqzz", dictionary, new RichTextBox(), _ => { }, _ => { });

            HeadersOf(menu).ShouldContain(EditorContextMenu.NoSuggestionsHeader);
            HeadersOf(menu).ShouldContain(EditorContextMenu.AddToDictionaryHeader);
        });
    }

    [Fact]
    public void Fill_ChoosingASpellingSuggestion_CorrectsTheMisspelling()
    {
        StaThread.Run(() =>
        {
            var dictionary = new StubSpellDictionary("wrods") { Suggestions = ["words"] };
            var menu = new ContextMenu();
            var corrected = string.Empty;

            EditorContextMenu.Fill(
                menu, "wrods", dictionary, new RichTextBox(), replacement => corrected = replacement, _ => { });
            Click(menu, "words");

            corrected.ShouldBe("words");
        });
    }

    [Fact]
    public void Fill_ChoosingAddToDictionary_AcceptsTheWord_INV040()
    {
        StaThread.Run(() =>
        {
            var dictionary = new StubSpellDictionary("wrods");
            var menu = new ContextMenu();
            var accepted = string.Empty;

            EditorContextMenu.Fill(
                menu, "wrods", dictionary, new RichTextBox(), _ => { }, word => accepted = word);
            Click(menu, EditorContextMenu.AddToDictionaryHeader);

            accepted.ShouldBe("wrods");
        });
    }

    [Fact]
    public void Fill_AwayFromAMisspelling_OffersTheClipboardCommandsAlone()
    {
        StaThread.Run(() =>
        {
            var menu = new ContextMenu();

            EditorContextMenu.Fill(
                menu, misspelling: null, new StubSpellDictionary(), new RichTextBox(), _ => { }, _ => { });

            HeadersOf(menu).ShouldBe(["Cut", "Copy", "Copy as Markdown", "Paste"]);
        });
    }

    [Fact]
    public void Fill_ReplacesWhatTheMenuHeldBefore()
    {
        StaThread.Run(() =>
        {
            // The menu instance is created once and refilled on every opening — a stale Suggestion
            // from the previous right-click must not linger.
            var dictionary = new StubSpellDictionary("wrods") { Suggestions = ["words"] };
            var menu = new ContextMenu();
            var editor = new RichTextBox();

            EditorContextMenu.Fill(menu, "wrods", dictionary, editor, _ => { }, _ => { });
            EditorContextMenu.Fill(menu, misspelling: null, dictionary, editor, _ => { }, _ => { });

            HeadersOf(menu).ShouldNotContain("words");
        });
    }

    [Fact]
    public void Editor_OwnsAContextMenuFromConstruction_SoWpfDoesNotPreemptIt()
    {
        StaThread.Run(() =>
        {
            // WPF's own text-editor context menu is a class handler that runs before any instance
            // handler; seeing no menu of the editor's own it opens its own and marks the event
            // handled, so the editor's ContextMenuOpening handler never runs and the Spelling
            // Suggestions are never shown. Owning a menu from construction is what prevents that.
            var editor = new MarkdownRichEditor();

            editor.ContextMenu.ShouldNotBeNull();
        });
    }
}
