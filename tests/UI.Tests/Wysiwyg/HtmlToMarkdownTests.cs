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
    public void Convert_PreformattedHtml_ProducesACodeBlock_INV041()
    {
        HtmlToMarkdown.Convert("<pre>if (x) {\n    doThing();\n}</pre>")
            .ShouldBe("```\nif (x) {\n    doThing();\n}\n```");
    }

    [Fact]
    public void Convert_CodeCopiedFromACodeEditor_KeepsItsIndentation_INV041()
    {
        // What a code editor puts on the clipboard: a `white-space: pre` wrapper holding one element
        // per line. HTML's own whitespace rules would collapse that indentation away, but the wrapper
        // says the whitespace is the content — so it pastes as a Code Block with its indents intact.
        var html =
            "<div style=\"color: #cccccc;background-color: #1f1f1f;white-space: pre;\">" +
            "<div><span style=\"color: #c586c0;\">if</span><span> (x) {</span></div>" +
            "<div><span>    doThing();</span></div>" +
            "<div><span>}</span></div>" +
            "</div>";

        HtmlToMarkdown.Convert(html).ShouldBe("```\nif (x) {\n    doThing();\n}\n```");
    }

    [Fact]
    public void Convert_PreformattedHtmlHoldingOneElementPerLine_KeepsTheLinesApart_INV041()
    {
        HtmlToMarkdown.Convert("<pre><div>if (x) {</div><div>    doThing();</div><div>}</div></pre>")
            .ShouldBe("```\nif (x) {\n    doThing();\n}\n```");
    }

    [Fact]
    public void Convert_PreformattedHtmlHoldingNonBreakingSpaces_ReadsThemAsIndentation_INV041()
    {
        HtmlToMarkdown.Convert("<pre><div>if (x) {</div><div>&nbsp;&nbsp;&nbsp;&nbsp;doThing();</div></pre>")
            .ShouldBe("```\nif (x) {\n    doThing();\n```");
    }

    [Fact]
    public void Convert_PreformattedHtmlHoldingMarkupCharacters_KeepsThemAsText_INV041()
    {
        // Code is full of characters HTML spells with entities; they are code, not markup.
        HtmlToMarkdown.Convert("<pre><div>if (a &lt; b &amp;&amp; c &gt; d)</div></pre>")
            .ShouldBe("```\nif (a < b && c > d)\n```");
    }

    [Fact]
    public void Convert_ProseInATextEditorsHtml_IsStillProse_INV041()
    {
        // Only whitespace-preserving HTML is code; ordinary formatted HTML converts as it always has.
        HtmlToMarkdown.Convert("<div><p>a <strong>bold</strong> word</p></div>")
            .ShouldBe("a **bold** word");
    }

    [Fact]
    public void Convert_GivenNull_Throws()
    {
        Should.Throw<ArgumentNullException>(() => HtmlToMarkdown.Convert(null!));
    }
}
