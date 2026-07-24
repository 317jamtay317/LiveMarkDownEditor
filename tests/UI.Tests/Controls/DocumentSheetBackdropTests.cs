using Shouldly;
using UI.Controls;
using UI.Tests.Wysiwyg;
using Xunit;

namespace UI.Tests.Controls;

/// <summary>
/// Tests for <see cref="DocumentSheetBackdrop"/>: its Page Break rules fall on the Page boundaries of
/// the page height the Page Setup's orientation yields (INV-058, INV-061).
/// </summary>
public sealed class DocumentSheetBackdropTests
{
    [Fact]
    public void PageHeight_DefaultsToTheUprightUsLetterPage_INV061()
    {
        StaThread.Run(() =>
        {
            var backdrop = new DocumentSheetBackdrop();

            backdrop.PageHeight.ShouldBe(DocumentSheet.PageHeight);
        });
    }

    [Fact]
    public void PageHeight_TakesTheLandscapePageHeight_INV061()
    {
        StaThread.Run(() =>
        {
            var backdrop = new DocumentSheetBackdrop { PageHeight = 816d };

            backdrop.PageHeight.ShouldBe(816d);
        });
    }
}
