using Application;
using Shouldly;
using UI.Core;
using UI.Tests.TestDoubles;
using UI.ViewModels;
using Xunit;

namespace UI.Tests.ViewModels;

/// <summary>
/// Tests that Page View — laying the Visual Document on a fixed-width Document Sheet — is a
/// presentation-only mode of the Workspace: on by default, toggled by its command, and never a change
/// to any Markdown Document (INV-058).
/// </summary>
public sealed class WorkspaceViewModelPageViewTests
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
    private readonly List<FakeDocumentWatcher> _watchers = [];

    private WorkspaceViewModel CreateWorkspace()
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
            new FakePageSetupStore(),
            new StubCustomMarginsPrompt(answer: null),
            new FakePrintPreview());
    }

    [Fact]
    public void IsPageViewEnabled_IsOnByDefault_INV058()
    {
        var workspace = CreateWorkspace();

        workspace.IsPageViewEnabled.ShouldBeTrue();
    }

    [Fact]
    public void TogglePageViewCommand_FlipsPageView_INV058()
    {
        var workspace = CreateWorkspace();

        workspace.TogglePageViewCommand.Execute(null);
        workspace.IsPageViewEnabled.ShouldBeFalse();

        workspace.TogglePageViewCommand.Execute(null);
        workspace.IsPageViewEnabled.ShouldBeTrue();
    }

    [Fact]
    public void TogglePageView_LeavesTheActiveSessionMarkdownUnchanged_INV058()
    {
        var workspace = CreateWorkspace();
        workspace.ActiveSession!.Markdown = "# Title\n\n| a | b |\n| - | - |\n| 1 | 2 |";
        var before = workspace.ActiveSession.Markdown;

        workspace.TogglePageViewCommand.Execute(null);

        workspace.ActiveSession.Markdown.ShouldBe(before);
    }
}
