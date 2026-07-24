using Shouldly;
using UI.Core;
using Xunit;

namespace UI.Tests.Core;

/// <summary>
/// Tests for <see cref="PageSetup"/>, <see cref="PrintMargins"/>, and <see cref="MarginPreset"/>: the
/// one editor-wide Page Setup — a Page Orientation together with Print Margins — that shapes the
/// Document Sheet, the Print Preview, and the printout alike (INV-061).
/// </summary>
public sealed class PageSetupTests
{
    [Fact]
    public void Portrait_PageIsUsLetterUpright_INV061()
    {
        var setup = new PageSetup(PageOrientation.Portrait, PrintMargins.For(MarginPreset.Normal));

        setup.PageWidth.ShouldBe(816d);
        setup.PageHeight.ShouldBe(1056d);
    }

    [Fact]
    public void Landscape_PageIsUsLetterTurned_INV061()
    {
        var setup = new PageSetup(PageOrientation.Landscape, PrintMargins.For(MarginPreset.Normal));

        setup.PageWidth.ShouldBe(1056d);
        setup.PageHeight.ShouldBe(816d);
    }

    [Fact]
    public void Default_IsPortraitWithNormalMargins_INV061()
    {
        PageSetup.Default.Orientation.ShouldBe(PageOrientation.Portrait);
        PageSetup.Default.Margins.ShouldBe(PrintMargins.For(MarginPreset.Normal));
    }

    [Theory]
    [InlineData(MarginPreset.Normal, 96d, 96d, 96d, 96d)]
    [InlineData(MarginPreset.Narrow, 48d, 48d, 48d, 48d)]
    [InlineData(MarginPreset.Moderate, 72d, 96d, 72d, 96d)]
    [InlineData(MarginPreset.Wide, 192d, 96d, 192d, 96d)]
    public void For_GivenANamedPreset_YieldsItsMargins_INV061(
        MarginPreset preset, double left, double top, double right, double bottom)
    {
        var margins = PrintMargins.For(preset);

        margins.Left.ShouldBe(left);
        margins.Top.ShouldBe(top);
        margins.Right.ShouldBe(right);
        margins.Bottom.ShouldBe(bottom);
    }

    [Fact]
    public void For_GivenCustom_Throws_BecauseCustomHasNoFixedMargins_INV061()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => PrintMargins.For(MarginPreset.Custom));
    }

    [Theory]
    [InlineData(MarginPreset.Normal)]
    [InlineData(MarginPreset.Narrow)]
    [InlineData(MarginPreset.Moderate)]
    [InlineData(MarginPreset.Wide)]
    public void PresetOf_RecognisesEachNamedPresetsMargins_INV061(MarginPreset preset)
    {
        PrintMargins.PresetOf(PrintMargins.For(preset)).ShouldBe(preset);
    }

    [Fact]
    public void PresetOf_GivenMarginsMatchingNoPreset_IsCustom_INV061()
    {
        var custom = new PrintMargins(left: 30d, top: 40d, right: 30d, bottom: 40d);

        PrintMargins.PresetOf(custom).ShouldBe(MarginPreset.Custom);
    }

    [Theory]
    [InlineData(-1d, 96d, 96d, 96d)]
    [InlineData(96d, -1d, 96d, 96d)]
    [InlineData(96d, 96d, -1d, 96d)]
    [InlineData(96d, 96d, 96d, -1d)]
    public void Construct_GivenANegativeMargin_ThrowsAndPreservesInvariant_INV061(
        double left, double top, double right, double bottom)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new PrintMargins(left, top, right, bottom));
    }

    [Fact]
    public void Construct_GivenMarginsThatLeaveNoWritableWidth_Throws_INV061()
    {
        // 408 + 408 swallows the whole 816-unit width of a portrait Page: nowhere left to write.
        Should.Throw<ArgumentOutOfRangeException>(
            () => new PrintMargins(left: 408d, top: 96d, right: 408d, bottom: 96d));
    }

    [Fact]
    public void Construct_GivenMarginsThatLeaveNoWritableHeightInLandscape_Throws_INV061()
    {
        // 408 + 408 swallows the whole 816-unit height of a landscape Page. The guard holds in either
        // orientation, so switching the Page Orientation can never invalidate the margins.
        Should.Throw<ArgumentOutOfRangeException>(
            () => new PrintMargins(left: 96d, top: 408d, right: 96d, bottom: 408d));
    }

    [Fact]
    public void ToThickness_CarriesTheMarginsToTheSheetsPadding_INV061()
    {
        var margins = new PrintMargins(left: 72d, top: 96d, right: 72d, bottom: 96d);

        var thickness = margins.ToThickness();

        thickness.Left.ShouldBe(72d);
        thickness.Top.ShouldBe(96d);
        thickness.Right.ShouldBe(72d);
        thickness.Bottom.ShouldBe(96d);
    }

    [Fact]
    public void PageSetups_WithTheSameOrientationAndMargins_AreEqual_INV061()
    {
        var one = new PageSetup(PageOrientation.Landscape, PrintMargins.For(MarginPreset.Narrow));
        var other = new PageSetup(PageOrientation.Landscape, PrintMargins.For(MarginPreset.Narrow));

        one.ShouldBe(other);
    }
}
