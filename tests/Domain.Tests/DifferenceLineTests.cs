using Domain;
using Shouldly;
using Xunit;

namespace Domain.Tests;

/// <summary>
/// Tests for <see cref="DifferenceLine"/>, one line of a Conflict Difference (INV-021).
/// </summary>
public sealed class DifferenceLineTests
{
    [Fact]
    public void Construct_GivenNullText_ThrowsAndPreservesInvariant()
    {
        Should.Throw<ArgumentNullException>(() => new DifferenceLine(DifferenceLineKind.Unchanged, null!));
    }

    [Fact]
    public void Construct_GivenKindAndText_PreservesBoth()
    {
        var line = new DifferenceLine(DifferenceLineKind.SessionOnly, "# Mine");

        line.Kind.ShouldBe(DifferenceLineKind.SessionOnly);
        line.Text.ShouldBe("# Mine");
    }

    [Fact]
    public void Construct_GivenEmptyText_RepresentsABlankLine()
    {
        new DifferenceLine(DifferenceLineKind.Unchanged, "").Text.ShouldBe("");
    }

    [Fact]
    public void Equality_GivenSameKindAndText_AreEqual()
    {
        new DifferenceLine(DifferenceLineKind.DiskOnly, "same")
            .ShouldBe(new DifferenceLine(DifferenceLineKind.DiskOnly, "same"));
    }

    [Fact]
    public void Equality_GivenSameTextButDifferentKind_AreNotEqual()
    {
        new DifferenceLine(DifferenceLineKind.SessionOnly, "same")
            .ShouldNotBe(new DifferenceLine(DifferenceLineKind.DiskOnly, "same"));
    }
}
