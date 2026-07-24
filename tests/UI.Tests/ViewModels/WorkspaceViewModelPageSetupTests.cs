using Application;
using Shouldly;
using UI.Core;
using UI.Tests.TestDoubles;
using UI.ViewModels;
using Xunit;

namespace UI.Tests.ViewModels;

/// <summary>
/// Tests that the Workspace holds the one editor-wide Page Setup: restored from its store at
/// construction, changed by the orientation and margin commands, asked for custom values through the
/// Custom Margins Prompt, persisted on every change, and never a change to any Markdown Document
/// (INV-061).
/// </summary>
public sealed class WorkspaceViewModelPageSetupTests
{
    private readonly FakeDocumentStore _store = new();
    private readonly StubFilePicker _picker = new();
    private readonly StubUnsavedEditsPrompt _prompt = new();
    private readonly InlineUiDispatcher _dispatcher = new();
    private readonly FakeThemeService _theme = new();
    private readonly FakeMarkdownRoundTrip _roundTrip = new();
    private readonly FakeWorkspaceStateStore _stateStore = new();
    private readonly StubFolderPicker _folderPicker = new();
    private readonly FakeMarkdownFolderReader _folderReader = new();
    private readonly FakeFolderWatcher _folderWatcher = new();
    private readonly FakePageSetupStore _pageSetupStore = new();
    private readonly List<FakeDocumentWatcher> _watchers = [];

    private WorkspaceViewModel CreateWorkspace(ICustomMarginsPrompt? customMarginsPrompt = null)
    {
        EditorSessionFactory factory = () =>
        {
            var watcher = new FakeDocumentWatcher();
            _watchers.Add(watcher);
            return new EditorSessionViewModel(_store, watcher, _dispatcher, _roundTrip);
        };
        var folder = new FolderWorkspaceViewModel(_folderPicker, _folderReader, _folderWatcher, _dispatcher);
        return new WorkspaceViewModel(
            factory,
            _picker,
            _prompt,
            new StubLinkPrompt(answer: null),
            new FakeDocumentPrinter(),
            new StubMarkdownRenderer(),
            new StubFlowchartBuilder(result: null),
            new FakeMermaidImageRenderer(),
            new AppearanceViewModel(_theme),
            new ExportViewModel(
                _picker,
                new StubMarkdownRenderer(),
                new FakeHtmlExportStore(),
                new FakePdfExporter(),
                new FakePdfExportStore(),
                new FakeMermaidScriptSource()),
            folder,
            new SideDockViewModel(folder),
            _stateStore,
            _pageSetupStore,
            customMarginsPrompt ?? new StubCustomMarginsPrompt(answer: null),
            new FakePrintPreview());
    }

    [Fact]
    public void PageSetup_OnAFreshStore_IsTheDefault_INV061()
    {
        var workspace = CreateWorkspace();

        workspace.PageSetup.ShouldBe(PageSetup.Default);
        workspace.MarginPreset.ShouldBe(MarginPreset.Normal);
    }

    [Fact]
    public void Construct_RestoresThePersistedPageSetup_INV061()
    {
        var persisted = new PageSetup(PageOrientation.Landscape, PrintMargins.For(MarginPreset.Narrow));
        _pageSetupStore.Stored = persisted;

        var workspace = CreateWorkspace();

        workspace.PageSetup.ShouldBe(persisted);
        workspace.MarginPreset.ShouldBe(MarginPreset.Narrow);
    }

    [Fact]
    public void SetPageOrientation_TurnsThePage_AndPersists_INV061()
    {
        var workspace = CreateWorkspace();

        workspace.SetPageOrientationCommand.Execute(PageOrientation.Landscape);

        workspace.PageSetup.Orientation.ShouldBe(PageOrientation.Landscape);
        _pageSetupStore.Saved.ShouldContain(workspace.PageSetup);
    }

    [Fact]
    public void SetPageOrientation_KeepsTheMargins_INV061()
    {
        var workspace = CreateWorkspace();
        workspace.SetMarginPresetCommand.Execute(MarginPreset.Wide);

        workspace.SetPageOrientationCommand.Execute(PageOrientation.Landscape);

        workspace.PageSetup.Margins.ShouldBe(PrintMargins.For(MarginPreset.Wide));
    }

    [Fact]
    public void SetPageOrientation_LeavesTheActiveSessionsMarkdownUnchanged_INV061()
    {
        var workspace = CreateWorkspace();
        workspace.ActiveSession!.Markdown = "# Title\n\nSome prose.";
        var before = workspace.ActiveSession.Markdown;

        workspace.SetPageOrientationCommand.Execute(PageOrientation.Landscape);

        workspace.ActiveSession.Markdown.ShouldBe(before);
    }

    [Fact]
    public void SetMarginPreset_SetsThePresetsMargins_AndPersists_INV061()
    {
        var workspace = CreateWorkspace();

        workspace.SetMarginPresetCommand.Execute(MarginPreset.Narrow);

        workspace.PageSetup.Margins.ShouldBe(PrintMargins.For(MarginPreset.Narrow));
        workspace.MarginPreset.ShouldBe(MarginPreset.Narrow);
        _pageSetupStore.Saved.ShouldContain(workspace.PageSetup);
    }

    [Fact]
    public void SetMarginPreset_GivenCustom_ChangesNothing_INV061()
    {
        // Custom has no fixed margins to set — the Custom Margins Prompt is the way to custom values.
        var workspace = CreateWorkspace();
        var before = workspace.PageSetup;

        workspace.SetMarginPresetCommand.Execute(MarginPreset.Custom);

        workspace.PageSetup.ShouldBe(before);
        _pageSetupStore.Saved.ShouldBeEmpty();
    }

    [Fact]
    public void EditCustomMargins_WhenThePromptIsDismissed_ChangesNothing_INV061()
    {
        var workspace = CreateWorkspace(new StubCustomMarginsPrompt(answer: null));
        var before = workspace.PageSetup;

        workspace.EditCustomMarginsCommand.Execute(null);

        workspace.PageSetup.ShouldBe(before);
        _pageSetupStore.Saved.ShouldBeEmpty();
    }

    [Fact]
    public void EditCustomMargins_SeedsThePromptWithTheCurrentMargins_INV061()
    {
        var prompt = new StubCustomMarginsPrompt(answer: null);
        var workspace = CreateWorkspace(prompt);

        workspace.EditCustomMarginsCommand.Execute(null);

        prompt.LastSeeded.ShouldBe(workspace.PageSetup.Margins);
    }

    [Fact]
    public void EditCustomMargins_AppliesTheAnswer_AndPersists_INV061()
    {
        var custom = new PrintMargins(left: 30d, top: 40d, right: 30d, bottom: 40d);
        var workspace = CreateWorkspace(new StubCustomMarginsPrompt(custom));

        workspace.EditCustomMarginsCommand.Execute(null);

        workspace.PageSetup.Margins.ShouldBe(custom);
        workspace.MarginPreset.ShouldBe(MarginPreset.Custom);
        _pageSetupStore.Saved.ShouldContain(workspace.PageSetup);
    }
}
