using Shouldly;
using UI.Controls;
using Xunit;

namespace UI.Tests.Controls;

/// <summary>
/// Tests for <see cref="DocumentSheet"/>: the pure rule that fixes the Document Sheet's width and page
/// margins, so in Page View the Visual Document — tables included — is confined to a single page width
/// (INV-058).
/// </summary>
public sealed class DocumentSheetTests
{
    [Fact]
    public void Width_IsTheUsLetterWidth_INV058()
    {
        // US Letter is 8.5 inches wide; at 96 device-independent units per inch that is 816 units.
        DocumentSheet.Width.ShouldBe(816d);
    }

    [Fact]
    public void PagePadding_InsetsTheContentFromEverySheetEdge_INV058()
    {
        var padding = DocumentSheet.PagePadding;

        padding.Left.ShouldBeGreaterThan(0d);
        padding.Top.ShouldBeGreaterThan(0d);
        padding.Right.ShouldBeGreaterThan(0d);
        padding.Bottom.ShouldBeGreaterThan(0d);
    }
}
