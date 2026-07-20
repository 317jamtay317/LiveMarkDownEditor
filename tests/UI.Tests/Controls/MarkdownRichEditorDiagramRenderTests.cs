using System.Linq;
using System.Windows.Documents;
using Shouldly;
using UI.Controls;
using UI.Tests.TestDoubles;
using UI.Tests.Wysiwyg;
using Xunit;

namespace UI.Tests.Controls;

/// <summary>
/// Tests that the <see cref="MarkdownRichEditor"/> renders each inline Mermaid Diagram through the
/// <c>IMermaidImageRenderer</c> port in the editor's current theme, re-renders when the theme changes,
/// and does not re-render a diagram in a theme it has already been rendered in (INV-047).
/// </summary>
public sealed class MarkdownRichEditorDiagramRenderTests
{
    private const string Doc = "```mermaid\ngraph TD\n  A-->B\n```";
    private const string Source = "graph TD\n  A-->B";

    [Fact]
    public void EachDiagram_IsRenderedInTheCurrentTheme_INV047()
    {
        StaThread.Run(() =>
        {
            var renderer = new FakeMermaidImageRenderer();
            var editor = new MarkdownRichEditor { IsDarkTheme = true, DiagramImageRenderer = renderer };

            editor.Markdown = Doc;

            renderer.Calls.ShouldContain((Source, true));
        });
    }

    [Fact]
    public void TogglingTheTheme_ReRendersEachDiagram_INV047()
    {
        StaThread.Run(() =>
        {
            var renderer = new FakeMermaidImageRenderer();
            var editor = new MarkdownRichEditor { IsDarkTheme = false, DiagramImageRenderer = renderer };
            editor.Markdown = Doc;
            renderer.Calls.Clear();

            editor.IsDarkTheme = true;

            renderer.Calls.ShouldContain((Source, true));
        });
    }

    [Fact]
    public void ADiagram_IsNotReRendered_InAThemeItHasAlreadyBeenRenderedIn_INV047()
    {
        StaThread.Run(() =>
        {
            var renderer = new FakeMermaidImageRenderer();
            var editor = new MarkdownRichEditor { IsDarkTheme = false, DiagramImageRenderer = renderer };
            editor.Markdown = Doc;      // renders (Source, dark: false)
            editor.IsDarkTheme = true;  // renders (Source, dark: true)
            renderer.Calls.Clear();

            editor.IsDarkTheme = false; // already rendered light — served from cache

            renderer.Calls.ShouldBeEmpty();
        });
    }

    [Fact]
    public void ARenderedDiagram_ShowsItsPicture_NotTheSourceFallback_INV047()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { DiagramImageRenderer = new FakeMermaidImageRenderer() };

            editor.Markdown = Doc;

            var view = editor.Document.Blocks.OfType<BlockUIContainer>()
                .Select(block => block.Child).OfType<MermaidDiagramView>().Single();
            view.Rendered.ShouldNotBeNull();
        });
    }
}
