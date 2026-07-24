using System.Collections.Generic;
using System.Linq;
using System.Windows.Documents;
using Domain;
using Shouldly;
using UI.Wysiwyg;
using Xunit;

namespace UI.Tests.Wysiwyg;

/// <summary>
/// Tests for <see cref="ChangeHighlightScanner"/>, which resolves the Changed Regions of a Reload
/// Difference to the Blocks of the reloaded Visual Document that carry the Change Highlight. Covers
/// INV-060: exactly the changed Blocks are marked, a Removed seam marks the Block that closed over
/// it, and scanning changes nothing.
/// </summary>
public sealed class ChangeHighlightScannerTests
{
    private static readonly MarkdownToFlowDocumentProjector Projector = new();

    /// <summary>Projects <paramref name="after"/> and scans it against the reload from <paramref name="before"/>.</summary>
    private static IReadOnlyList<ChangeHighlightTarget> Scan(string before, string after)
    {
        var document = Projector.Project(after);
        var regions = ReloadDifference.Compute(new MarkdownSource(before), new MarkdownSource(after));
        return ChangeHighlightScanner.Scan(document, regions);
    }

    /// <summary>A targeted Block's text, so tests can name the Block they expect by what it says.</summary>
    private static string TextOf(ChangeHighlightTarget target) => TextOf(target.Block);

    private static string TextOf(Block block) => new TextRange(block.ContentStart, block.ContentEnd).Text;

    [Fact]
    public void Scan_GivenNoChangedRegions_TargetsNothing_INV060()
    {
        StaThread.Run(() =>
        {
            Scan("alpha\n\nbravo", "alpha\n\nbravo").ShouldBeEmpty();
        });
    }

    [Fact]
    public void Scan_GivenAChangedLine_TargetsOnlyThatBlock_INV060()
    {
        StaThread.Run(() =>
        {
            var targets = Scan("alpha\n\nbravo\n\ncharlie", "alpha\n\nBRAVO\n\ncharlie");

            var target = targets.ShouldHaveSingleItem();
            target.Kind.ShouldBe(ChangeHighlightTargetKind.Changed);
            TextOf(target).ShouldContain("BRAVO");
        });
    }

    [Fact]
    public void Scan_GivenAChangedLineInsideAMultiLineBlock_TargetsTheWholeBlock_INV060()
    {
        StaThread.Run(() =>
        {
            var targets = Scan(
                "intro\n\n```\none\ntwo\n```",
                "intro\n\n```\none\nTWO\n```");

            var target = targets.ShouldHaveSingleItem();
            target.Kind.ShouldBe(ChangeHighlightTargetKind.Changed);
            TextOf(target).ShouldContain("TWO");
        });
    }

    [Fact]
    public void Scan_GivenAnAddedBlock_TargetsIt_INV060()
    {
        StaThread.Run(() =>
        {
            var targets = Scan("alpha\n\ncharlie", "alpha\n\nbravo\n\ncharlie");

            var target = targets.ShouldHaveSingleItem();
            target.Kind.ShouldBe(ChangeHighlightTargetKind.Changed);
            TextOf(target).ShouldContain("bravo");
        });
    }

    [Fact]
    public void Scan_GivenADeletedBlock_MarksTheSeamAboveTheBlockThatClosedOverIt_INV060()
    {
        StaThread.Run(() =>
        {
            var targets = Scan("alpha\n\nbravo\n\ncharlie", "alpha\n\ncharlie");

            var target = targets.ShouldHaveSingleItem();
            target.Kind.ShouldBe(ChangeHighlightTargetKind.RemovedAbove);
            TextOf(target).ShouldContain("charlie");
        });
    }

    [Fact]
    public void Scan_GivenABlockDeletedFromTheEnd_MarksTheSeamBelowTheLastBlock_INV060()
    {
        StaThread.Run(() =>
        {
            var targets = Scan("alpha\n\nbravo", "alpha");

            var target = targets.ShouldHaveSingleItem();
            target.Kind.ShouldBe(ChangeHighlightTargetKind.RemovedBelow);
            TextOf(target).ShouldContain("alpha");
        });
    }

    [Fact]
    public void Scan_GivenSeveralChanges_TargetsThemInDocumentOrderWithoutRepeating_INV060()
    {
        StaThread.Run(() =>
        {
            var targets = Scan(
                "alpha\n\nbravo\n\ncharlie\n\ndelta",
                "ALPHA\n\nbravo\n\ncharlie\n\nDELTA");

            targets.Count.ShouldBe(2);
            targets.ShouldAllBe(target => target.Kind == ChangeHighlightTargetKind.Changed);
            TextOf(targets[0]).ShouldContain("ALPHA");
            TextOf(targets[1]).ShouldContain("DELTA");
        });
    }

    [Fact]
    public void Scan_GivenAnEmptyDocument_TargetsNothing_INV060()
    {
        StaThread.Run(() =>
        {
            var document = Projector.Project(string.Empty);
            var regions = ReloadDifference.Compute(new MarkdownSource("alpha"), MarkdownSource.Empty);

            ChangeHighlightScanner.Scan(document, regions).ShouldBeEmpty();
        });
    }

    [Fact]
    public void Scan_GivenARegionPastTheLastBlock_TargetsNothingRatherThanFailing_INV060()
    {
        StaThread.Run(() =>
        {
            var document = Projector.Project("alpha");

            var targets = ChangeHighlightScanner.Scan(
                document,
                [new ChangedRegion(ChangedRegionKind.Changed, 40, 3)]);

            targets.ShouldBeEmpty();
        });
    }

    [Fact]
    public void Scan_LeavesTheVisualDocumentUnchanged_INV060()
    {
        StaThread.Run(() =>
        {
            const string After = "alpha\n\nBRAVO";
            var document = Projector.Project(After);
            var before = string.Concat(document.Blocks.Select(TextOf));

            ChangeHighlightScanner.Scan(
                document,
                ReloadDifference.Compute(new MarkdownSource("alpha\n\nbravo"), new MarkdownSource(After)));

            string.Concat(document.Blocks.Select(TextOf)).ShouldBe(before);
            document.Blocks.Count.ShouldBe(2);
        });
    }
}
