using Shouldly;
using UI.Find;
using UI.Tests.Wysiwyg;
using UI.Wysiwyg;
using Xunit;

namespace UI.Tests.Find;

/// <summary>
/// Tests for <see cref="MatchScanner"/>: it maps the Matches <see cref="MatchFinder"/> computes onto
/// a Visual Document, yielding one range per Match in document order. It only reads the document, so
/// finding never changes the Markdown Document (INV-016).
/// </summary>
public sealed class MatchScannerTests
{
    private static readonly MarkdownToFlowDocumentProjector Projector = new();

    [Fact]
    public void Scan_FindsEveryMatch_InDocumentOrder()
    {
        StaThread.Run(() =>
        {
            var document = Projector.Project("alpha beta alpha");

            var matches = MatchScanner.Scan(document, "alpha");

            matches.Count.ShouldBe(2);
            matches.ShouldAllBe(match => match.Text == "alpha");
            matches[0].Start.CompareTo(matches[1].Start).ShouldBe(-1);
        });
    }

    [Fact]
    public void Scan_EmptyQuery_FindsNothing()
    {
        StaThread.Run(() =>
        {
            var document = Projector.Project("alpha beta");

            MatchScanner.Scan(document, string.Empty).ShouldBeEmpty();
        });
    }

    [Fact]
    public void Scan_MatchesCaseInsensitively()
    {
        StaThread.Run(() =>
        {
            var document = Projector.Project("Alpha and ALPHA");

            MatchScanner.Scan(document, "alpha").Count.ShouldBe(2);
        });
    }

    [Fact]
    public void Scan_MatchSpanningInlineFormatting_YieldsOneContiguousRange()
    {
        StaThread.Run(() =>
        {
            // "bo" is bold and "ld" is not, so the Match straddles two runs.
            var document = Projector.Project("**bo**ld text");

            var matches = MatchScanner.Scan(document, "bold");

            matches.Count.ShouldBe(1);
            matches[0].Text.ShouldBe("bold");
        });
    }

    [Fact]
    public void Scan_MatchNeverBridgesTwoBlocks()
    {
        StaThread.Run(() =>
        {
            // "alpha" ends one paragraph and "beta" begins the next; the two are separated in the
            // snapshot, so a query spanning them matches nothing.
            var document = Projector.Project("alpha\n\nbeta");

            MatchScanner.Scan(document, "alpha beta").ShouldBeEmpty();
            MatchScanner.Scan(document, "alpha").Count.ShouldBe(1);
        });
    }
}
