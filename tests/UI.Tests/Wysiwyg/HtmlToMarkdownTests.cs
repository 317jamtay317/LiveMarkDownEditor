using Shouldly;
using UI.Wysiwyg;
using Xunit;

namespace UI.Tests.Wysiwyg;

/// <summary>
/// Tests for <see cref="HtmlToMarkdown.Convert"/> — the HTML-to-Markdown conversion Smart Paste uses
/// so web HTML pastes as clean Markdown (INV-041).
/// </summary>
public sealed class HtmlToMarkdownTests
{
    [Fact]
    public void Convert_AHeading_ProducesAMarkdownHeading()
    {
        HtmlToMarkdown.Convert("<h1>Hello</h1>").ShouldBe("# Hello");
    }

    [Fact]
    public void Convert_BoldText_ProducesMarkdownEmphasis()
    {
        HtmlToMarkdown.Convert("<p>a <strong>bold</strong> word</p>").ShouldContain("**bold**");
    }

    [Fact]
    public void Convert_ALink_ProducesAMarkdownLink()
    {
        HtmlToMarkdown.Convert("<a href=\"https://example.com\">site</a>")
            .ShouldBe("[site](https://example.com)");
    }

    [Fact]
    public void Convert_GivenNull_Throws()
    {
        Should.Throw<ArgumentNullException>(() => HtmlToMarkdown.Convert(null!));
    }
}
