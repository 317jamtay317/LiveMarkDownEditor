using Domain;
using Shouldly;
using Xunit;

namespace Domain.Tests;

/// <summary>
/// Tests for <see cref="CodeToken"/> and <see cref="CodeTokenKind"/>, the Domain's vocabulary for
/// Syntax Highlighting. Covers INV-064: a Code Token names a run of a Code Block's code and the
/// Code Token Kind it is, and it can never name a run of nothing.
/// </summary>
public sealed class CodeTokenTests
{
    [Fact]
    public void CodeToken_GivenNullText_ThrowsAndPreservesInvariant_INV064()
    {
        Should.Throw<ArgumentNullException>(() => new CodeToken(null!, CodeTokenKind.Plain));
    }

    [Fact]
    public void CodeToken_GivenEmptyText_ThrowsAndPreservesInvariant_INV064()
    {
        // An empty Code Token colors nothing and would let a tokenizer pad its output without
        // changing the code, hiding the very drift INV-064's lossless rule exists to catch.
        Should.Throw<ArgumentException>(() => new CodeToken(string.Empty, CodeTokenKind.Plain));
    }

    [Fact]
    public void CodeToken_GivenUndefinedKind_ThrowsAndPreservesInvariant_INV064()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new CodeToken("x", (CodeTokenKind)42));
    }

    [Fact]
    public void CodeToken_CarriesItsTextAndKind_INV064()
    {
        var token = new CodeToken("public", CodeTokenKind.Keyword);

        token.Text.ShouldBe("public");
        token.Kind.ShouldBe(CodeTokenKind.Keyword);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("\n")]
    [InlineData("\t")]
    [InlineData("   \n\t")]
    public void CodeToken_GivenWhitespace_IsAllowed_INV064(string whitespace)
    {
        // Whitespace between two colored tokens is carried as a Plain Code Token — that is how the
        // concatenation of the tokens stays exactly the code that went in.
        new CodeToken(whitespace, CodeTokenKind.Plain).Text.ShouldBe(whitespace);
    }

    [Fact]
    public void CodeTokenKind_IsTheSevenKindsPlusPlain_INV064()
    {
        // The set is fixed and language-independent: it is what the palette colors, so a new
        // Highlighting Language maps onto these rather than introducing a color of its own.
        Enum.GetValues<CodeTokenKind>().ShouldBe(
        [
            CodeTokenKind.Plain,
            CodeTokenKind.Comment,
            CodeTokenKind.String,
            CodeTokenKind.Number,
            CodeTokenKind.Keyword,
            CodeTokenKind.Type,
            CodeTokenKind.Function,
            CodeTokenKind.Operator,
        ], ignoreOrder: true);
    }
}
