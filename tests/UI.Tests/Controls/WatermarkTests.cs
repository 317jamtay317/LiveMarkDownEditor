using System.Windows.Controls;
using Shouldly;
using UI.Controls;
using UI.Tests.Wysiwyg;
using Xunit;

namespace UI.Tests.Controls;

/// <summary>
/// Tests for the <see cref="Watermark"/> attached property: the hint a text box shows while it is
/// empty, so an unfocused empty box (such as the Find Bar's Replacement box) still says what it is
/// for. Presentation-only — a Watermark is never part of the box's text.
/// </summary>
public sealed class WatermarkTests
{
    [Fact]
    public void GetText_WhenNotSet_IsEmpty()
    {
        StaThread.Run(() => Watermark.GetText(new TextBox()).ShouldBe(string.Empty));
    }

    [Fact]
    public void SetText_IsReadBackByGetText()
    {
        StaThread.Run(() =>
        {
            var box = new TextBox();

            Watermark.SetText(box, "Find");

            Watermark.GetText(box).ShouldBe("Find");
        });
    }

    [Fact]
    public void SetText_LeavesTheBoxesOwnTextAlone()
    {
        StaThread.Run(() =>
        {
            // A Watermark is a hint, not content: it must never leak into what the box actually holds.
            var box = new TextBox { Text = "alpha" };

            Watermark.SetText(box, "Find");

            box.Text.ShouldBe("alpha");
        });
    }
}
