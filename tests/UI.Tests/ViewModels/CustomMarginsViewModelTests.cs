using Shouldly;
using UI.Core;
using UI.ViewModels;
using Xunit;

namespace UI.Tests.ViewModels;

/// <summary>
/// Tests for <see cref="CustomMarginsViewModel"/>: the Custom Margins Prompt's state — four margins
/// entered in inches, seeded with the current Print Margins, accepted only when every value is one a
/// PrintMargins may hold, and answering nothing when dismissed (INV-061).
/// </summary>
public sealed class CustomMarginsViewModelTests
{
    [Fact]
    public void Construct_SeedsTheBoxesWithTheCurrentMarginsInInches_INV061()
    {
        var viewModel = new CustomMarginsViewModel(PrintMargins.For(MarginPreset.Moderate));

        viewModel.Left.ShouldBe("0.75");
        viewModel.Top.ShouldBe("1");
        viewModel.Right.ShouldBe("0.75");
        viewModel.Bottom.ShouldBe("1");
    }

    [Fact]
    public void Accept_AnswersTheMarginsInDeviceUnits_INV061()
    {
        var viewModel = new CustomMarginsViewModel(PrintMargins.For(MarginPreset.Normal))
        {
            Left = "0.5",
            Top = "0.5",
            Right = "0.5",
            Bottom = "0.5",
        };

        viewModel.AcceptCommand.Execute(null);

        viewModel.Answer.ShouldBe(PrintMargins.For(MarginPreset.Narrow));
    }

    [Fact]
    public void Cancel_AnswersNothing_INV061()
    {
        var viewModel = new CustomMarginsViewModel(PrintMargins.For(MarginPreset.Normal));

        viewModel.CancelCommand.Execute(null);

        viewModel.Answer.ShouldBeNull();
    }

    [Theory]
    [InlineData("not a number")]
    [InlineData("")]
    [InlineData("-1")]
    public void CanAccept_IsFalse_WhenAValueIsNotAMargin_INV061(string bad)
    {
        var viewModel = new CustomMarginsViewModel(PrintMargins.For(MarginPreset.Normal))
        {
            Left = bad,
        };

        viewModel.CanAccept.ShouldBeFalse();
    }

    [Fact]
    public void CanAccept_IsFalse_WhenTheMarginsLeaveNoWritableArea_INV061()
    {
        // 4.25 + 4.25 inches swallows the whole 8.5-inch side of the Page: nowhere left to write.
        var viewModel = new CustomMarginsViewModel(PrintMargins.For(MarginPreset.Normal))
        {
            Left = "4.25",
            Right = "4.25",
        };

        viewModel.CanAccept.ShouldBeFalse();
    }

    [Fact]
    public void CanAccept_IsTrue_ForTheSeededMargins_INV061()
    {
        var viewModel = new CustomMarginsViewModel(PrintMargins.For(MarginPreset.Wide));

        viewModel.CanAccept.ShouldBeTrue();
    }
}
