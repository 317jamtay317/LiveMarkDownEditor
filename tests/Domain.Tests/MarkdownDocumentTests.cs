using Domain;
using Shouldly;
using Xunit;

namespace Domain.Tests;

/// <summary>
/// Tests for the <see cref="MarkdownDocument"/> aggregate — the canonical representation of the
/// content being edited.
/// </summary>
public sealed class MarkdownDocumentTests
{
    [Fact]
    public void Construct_GivenNullSource_ThrowsAndPreservesInvariant()
    {
        Should.Throw<ArgumentNullException>(() => new MarkdownDocument((string)null!));
    }

    [Fact]
    public void Construct_GivenEmptyString_IsAnEmptyDocument()
    {
        var document = new MarkdownDocument("");

        document.Source.Text.ShouldBe("");
    }

    [Fact]
    public void Construct_GivenSourceText_ExposesItThroughSource()
    {
        var document = new MarkdownDocument("# Title");

        document.Source.Text.ShouldBe("# Title");
    }

    [Fact]
    public void Equality_GivenSameSource_AreEqual()
    {
        new MarkdownDocument("hello").ShouldBe(new MarkdownDocument("hello"));
    }
}
