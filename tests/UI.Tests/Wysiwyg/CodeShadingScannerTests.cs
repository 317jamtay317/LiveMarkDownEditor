using System.Linq;
using System.Windows.Documents;
using Shouldly;
using UI.Wysiwyg;
using Xunit;

namespace UI.Tests.Wysiwyg;

/// <summary>
/// Tests for <see cref="CodeShadingScanner"/>: it finds the Code Regions of a Visual Document — one
/// per Code Block and one per Code Span — so the Code Shading overlay knows what to shade. It never
/// changes the document (INV-017).
/// </summary>
public sealed class CodeShadingScannerTests
{
    private static readonly MarkdownToFlowDocumentProjector Projector = new();

    private static string TextOf(CodeRegion region) => new TextRange(region.Start, region.End).Text;

    [Fact]
    public void Scan_FindsCodeBlock_AsABlockRegion()
    {
        StaThread.Run(() =>
        {
            var document = Projector.Project("```\nvar x = 1;\n```");

            var regions = CodeShadingScanner.Scan(document);

            regions.Count.ShouldBe(1);
            regions[0].IsBlock.ShouldBeTrue();
            TextOf(regions[0]).ShouldContain("var x = 1;");
        });
    }

    [Fact]
    public void Scan_FindsInlineCodeSpan_AsASpanRegion()
    {
        StaThread.Run(() =>
        {
            var document = Projector.Project("Call `Compute()` now.");

            var regions = CodeShadingScanner.Scan(document);

            regions.Count.ShouldBe(1);
            regions[0].IsBlock.ShouldBeFalse();
            TextOf(regions[0]).ShouldBe("Compute()");
        });
    }

    [Fact]
    public void Scan_IgnoresProse_YieldingNoRegions()
    {
        StaThread.Run(() =>
        {
            var document = Projector.Project("# Heading\n\nJust ordinary **prose** here.");

            CodeShadingScanner.Scan(document).ShouldBeEmpty();
        });
    }

    [Fact]
    public void Scan_FindsCodeSpansNestedInListsAndEmphasis()
    {
        StaThread.Run(() =>
        {
            // A Code Span inside a list item, and one inside bold — both must be found, so the
            // scanner has to descend through block containers and inline spans alike.
            var document = Projector.Project("- item with `alpha`\n- **bold `beta`**");

            var regions = CodeShadingScanner.Scan(document);

            regions.Count.ShouldBe(2);
            regions.All(region => !region.IsBlock).ShouldBeTrue();
            regions.Select(TextOf).ShouldBe(["alpha", "beta"]);
        });
    }

    [Fact]
    public void Scan_DoesNotTreatACodeBlocksOwnLines_AsSeparateSpans()
    {
        StaThread.Run(() =>
        {
            // A multi-line Code Block is one block Region, not one Region per line: the scanner must
            // not descend into a Code Block's inline Runs looking for spans.
            var document = Projector.Project("```\nline one\nline two\nline three\n```");

            var regions = CodeShadingScanner.Scan(document);

            regions.Count.ShouldBe(1);
            regions[0].IsBlock.ShouldBeTrue();
        });
    }
}
