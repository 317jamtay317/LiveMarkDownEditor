using Domain;
using Shouldly;
using Xunit;

namespace Domain.Tests;

/// <summary>
/// Tests for <see cref="MarkdownSource"/>, the value object that guarantees INV-001:
/// a Markdown Document's source text is never <see langword="null"/>.
/// </summary>
public sealed class MarkdownSourceTests
{
    [Fact]
    public void Construct_GivenNullText_ThrowsAndPreservesInvariant()
    {
        Should.Throw<ArgumentNullException>(() => new MarkdownSource(null!));
    }

    [Fact]
    public void Construct_GivenEmptyText_RepresentsAnEmptyDocument()
    {
        var source = new MarkdownSource("");

        source.Text.ShouldBe("");
    }

    [Fact]
    public void Construct_GivenText_PreservesTheText()
    {
        var source = new MarkdownSource("# Heading");

        source.Text.ShouldBe("# Heading");
    }

    [Fact]
    public void Equality_GivenSameText_AreEqual()
    {
        new MarkdownSource("same").ShouldBe(new MarkdownSource("same"));
    }

    [Fact]
    public void Empty_IsTheEmptyDocument()
    {
        MarkdownSource.Empty.Text.ShouldBe("");
    }
}
