using System.Windows.Controls;
using Shouldly;
using UI.Controls;
using UI.Tests.Wysiwyg;
using Xunit;

namespace UI.Tests.Controls;

/// <summary>
/// Tests for the <see cref="CommandTip"/> control: the themed tooltip the Command Bar — and the
/// editor's other command buttons — shows on hover, naming an action and explaining what it does.
/// These hold the control to being a real <see cref="ToolTip"/> that carries the three pieces a
/// Command Tip shows: its Heading (what the action is), its Detail (what it does), and its optional
/// Gesture (the key gesture).
/// </summary>
public sealed class CommandTipTests
{
    /// <summary>
    /// A Command Tip <em>is</em> a ToolTip, so it can be assigned straight to an element's
    /// <see cref="System.Windows.FrameworkElement.ToolTip"/> and shown with its own themed template —
    /// with no stock, system-drawn tooltip wrapped around it.
    /// </summary>
    [Fact]
    public void CommandTip_IsAToolTip()
    {
        StaThread.Run(() => new CommandTip().ShouldBeAssignableTo<ToolTip>());
    }

    /// <summary>Heading, Detail, and Gesture round-trip the text a call site sets on them.</summary>
    [Fact]
    public void HeadingDetailAndGesture_RoundTripTheirText()
    {
        StaThread.Run(() =>
        {
            var tip = new CommandTip
            {
                Heading = "Bold",
                Detail = "Make the selected text bold, or turn bold text back to normal.",
                Gesture = "Ctrl+B",
            };

            tip.Heading.ShouldBe("Bold");
            tip.Detail.ShouldBe("Make the selected text bold, or turn bold text back to normal.");
            tip.Gesture.ShouldBe("Ctrl+B");
        });
    }

    /// <summary>
    /// Every property defaults to empty rather than <c>null</c>, so the template's "collapse when
    /// empty" triggers (the gesture chip, the detail line) fire on a plain string comparison — and an
    /// unfilled Command Tip shows nothing where the missing piece would be, never the word "null".
    /// </summary>
    [Fact]
    public void Properties_DefaultToEmpty_NotNull()
    {
        StaThread.Run(() =>
        {
            var tip = new CommandTip();

            tip.Heading.ShouldBe(string.Empty);
            tip.Detail.ShouldBe(string.Empty);
            tip.Gesture.ShouldBe(string.Empty);
        });
    }
}
