using System.IO;
using Shouldly;
using UI.Wysiwyg;
using Xunit;

namespace UI.Tests.Wysiwyg;

/// <summary>
/// Tests for <see cref="MarkdownLink.Classify"/> — how a Ctrl+Click follow routes a Link (INV-038):
/// a web address to the browser, a relative Markdown file into a new Tab, anything else nowhere.
/// </summary>
public sealed class MarkdownLinkTests
{
    private const string BaseDirectory = @"C:\docs";

    [Theory]
    [InlineData("https://example.com/page")]
    [InlineData("http://example.com")]
    [InlineData("mailto:someone@example.com")]
    public void Classify_AWebAddress_IsAWebTarget(string url)
    {
        var target = MarkdownLink.Classify(new Uri(url), BaseDirectory);

        target.Kind.ShouldBe(LinkKind.Web);
        target.Value.ShouldBe(new Uri(url).AbsoluteUri);
    }

    [Fact]
    public void Classify_ARelativeMarkdownLink_ResolvesAgainstTheBaseDirectory()
    {
        var target = MarkdownLink.Classify(new Uri("notes.md", UriKind.Relative), BaseDirectory);

        target.Kind.ShouldBe(LinkKind.MarkdownFile);
        target.Value.ShouldBe(Path.Combine(BaseDirectory, "notes.md"));
    }

    [Fact]
    public void Classify_ARelativeMarkdownLink_WithAFragment_DropsTheFragment()
    {
        var target = MarkdownLink.Classify(new Uri("notes.md#section", UriKind.Relative), BaseDirectory);

        target.Kind.ShouldBe(LinkKind.MarkdownFile);
        target.Value.ShouldBe(Path.Combine(BaseDirectory, "notes.md"));
    }

    [Fact]
    public void Classify_ARelativeMarkdownLink_WithNoBaseDirectory_IsNone()
    {
        // An unsaved document has no folder to resolve a relative link against.
        var target = MarkdownLink.Classify(new Uri("notes.md", UriKind.Relative), baseDirectory: null);

        target.Kind.ShouldBe(LinkKind.None);
    }

    [Fact]
    public void Classify_ARelativeNonMarkdownLink_IsNone()
    {
        MarkdownLink.Classify(new Uri("photo.png", UriKind.Relative), BaseDirectory).Kind.ShouldBe(LinkKind.None);
    }

    [Fact]
    public void Classify_APureFragment_IsNone()
    {
        MarkdownLink.Classify(new Uri("#section", UriKind.Relative), BaseDirectory).Kind.ShouldBe(LinkKind.None);
    }
}
