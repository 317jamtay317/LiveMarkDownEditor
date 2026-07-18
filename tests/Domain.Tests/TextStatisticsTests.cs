using Domain;
using Shouldly;
using Xunit;

namespace Domain.Tests;

/// <summary>
/// Tests for <see cref="TextStatistics.Compute"/> — the word count, character count, and reading time
/// shown in the Status Bar (INV-039): a pure, deterministic function of the text.
/// </summary>
public sealed class TextStatisticsTests
{
    [Fact]
    public void Compute_GivenEmptyText_IsAllZero()
    {
        TextStatistics.Compute(string.Empty).ShouldBe(DocumentStatistics.Empty);
    }

    [Fact]
    public void Compute_CountsWordsAndCharacters()
    {
        var statistics = TextStatistics.Compute("hello world");

        statistics.WordCount.ShouldBe(2);
        statistics.CharacterCount.ShouldBe(11);
    }

    [Theory]
    [InlineData("  hello   world  ", 2)] // runs of whitespace do not inflate the count
    [InlineData("one\ntwo\tthree", 3)] // newlines and tabs separate words
    [InlineData("   ", 0)] // whitespace alone is no words
    public void Compute_CountsWhitespaceSeparatedRuns(string text, int expectedWords)
    {
        TextStatistics.Compute(text).WordCount.ShouldBe(expectedWords);
    }

    [Fact]
    public void Compute_EstimatesReadingTimeAtTheReadingSpeed()
    {
        // 400 words at 200 wpm is two minutes.
        var text = string.Join(' ', Enumerable.Repeat("word", 400));

        TextStatistics.Compute(text).ReadingTime.ShouldBe(TimeSpan.FromMinutes(2));
    }

    [Fact]
    public void Compute_GivenTheSameTextTwice_YieldsEqualStatistics_INV039()
    {
        var text = "The quick brown fox jumps over the lazy dog.";

        TextStatistics.Compute(text).ShouldBe(TextStatistics.Compute(text));
    }

    [Fact]
    public void Compute_GivenNull_Throws()
    {
        Should.Throw<ArgumentNullException>(() => TextStatistics.Compute(null!));
    }
}
