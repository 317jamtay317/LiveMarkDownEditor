using Domain;
using Infrastructure.Markdown;
using Shouldly;
using Xunit;

namespace Infrastructure.Tests;

/// <summary>
/// Tests that the Rendered Output carries a Code Block's Syntax Highlighting as token markup, and
/// that coloring it never changes the code the block carries (INV-064, INV-032).
/// </summary>
public sealed class MarkdigMarkdownRendererHighlightingTests
{
    private static readonly MarkdigMarkdownRenderer Renderer = new(new ColorCodeSyntaxHighlighter());
    private static readonly MarkdigMarkdownRenderer Plain = new();

    private static string RenderOf(string markdown) => Renderer.Render(new MarkdownDocument(markdown)).Html;

    [Fact]
    public void Render_ColorsACodeBlockAsTokenSpans_INV064()
    {
        var html = RenderOf("```csharp\nconst int N = 42;\n```");

        html.ShouldContain("<span class=\"tok-keyword\">const</span>");
        html.ShouldContain("<span class=\"tok-number\">42</span>");
    }

    [Fact]
    public void Render_KeepsTheLanguageClass_SoAPageStillKnowsWhatTheBlockIs_INV064()
    {
        RenderOf("```csharp\nconst int N = 42;\n```").ShouldContain("class=\"language-csharp\"");
    }

    [Fact]
    public void Render_CarriesTheSameCodeAsAnUncoloredRender_INV064()
    {
        // The coloring adds markup around the code; it must never change the code itself.
        const string markdown = "```csharp\n// note\nconst int N = 42;\nvar s = \"hi\";\n```";

        CodeTextOf(RenderOf(markdown)).ShouldBe(CodeTextOf(Plain.Render(new MarkdownDocument(markdown)).Html));
    }

    [Fact]
    public void Render_EscapesCodeThatLooksLikeMarkup_INV064()
    {
        // The code is HTML-escaped exactly as before; the token spans are the only markup added.
        var html = RenderOf("```csharp\nvar x = a < b && c > d;\n```");

        html.ShouldContain("&lt;");
        html.ShouldContain("&gt;");
        html.ShouldContain("&amp;&amp;");
        html.ShouldNotContain("<b ");
    }

    [Fact]
    public void Render_GivenNoHighlightingLanguage_IsUnchangedFromAnUncoloredRender_INV064()
    {
        const string markdown = "```\npublic class Foo { }\n```";

        RenderOf(markdown).ShouldBe(Plain.Render(new MarkdownDocument(markdown)).Html);
    }

    [Fact]
    public void Render_GivenAnUnknownLanguage_IsUnchangedFromAnUncoloredRender_INV064()
    {
        const string markdown = "```bash\nls -la | grep x\n```";

        RenderOf(markdown).ShouldBe(Plain.Render(new MarkdownDocument(markdown)).Html);
    }

    [Fact]
    public void Render_LeavesAMermaidDiagramUntouched_SoThePageCanStillRenderIt_INV064()
    {
        // The Standalone Page's bootstrap finds each diagram by its language-mermaid class and reads
        // its textContent (INV-049); token spans inside it would feed Mermaid markup, not source.
        const string markdown = "```mermaid\ngraph TD;\n  A-->B;\n```";

        var html = RenderOf(markdown);

        html.ShouldBe(Plain.Render(new MarkdownDocument(markdown)).Html);
        html.ShouldContain("language-mermaid");
        html.ShouldNotContain("tok-");
    }

    [Fact]
    public void Render_LeavesAnInlineCodeSpanUncolored_INV064()
    {
        // Syntax Highlighting is the Code Block's alone: a Code Span carries no language.
        RenderOf("Call `const` now.").ShouldNotContain("tok-");
    }

    [Fact]
    public void Render_IsStillDeterministic_INV002()
    {
        const string markdown = "```csharp\nconst int N = 42;\n```";

        RenderOf(markdown).ShouldBe(RenderOf(markdown));
    }

    [Fact]
    public void Render_ColorsEveryCodeBlockInTheDocument_INV064()
    {
        var html = RenderOf("```csharp\nconst int A = 1;\n```\n\nProse.\n\n```json\n{\"k\": 1}\n```");

        html.ShouldContain("<span class=\"tok-keyword\">const</span>");
        html.ShouldContain("<span class=\"tok-function\">&quot;k&quot;</span>");
    }

    // The text a browser would show inside the code element, with the token markup stripped and the
    // HTML entities resolved — what the reader actually copies out of the page.
    private static string CodeTextOf(string html)
    {
        var start = html.IndexOf("<code", StringComparison.Ordinal);
        var end = html.IndexOf("</code>", StringComparison.Ordinal);
        var inner = html[(html.IndexOf('>', start) + 1)..end];

        return System.Net.WebUtility.HtmlDecode(
            System.Text.RegularExpressions.Regex.Replace(inner, "<[^>]+>", string.Empty));
    }
}
