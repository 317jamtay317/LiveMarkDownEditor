using Shouldly;
using UI.Wysiwyg;
using Xunit;

namespace UI.Tests.Wysiwyg;

/// <summary>
/// Tests for <see cref="FlowDocumentRoundTrip"/> — the adapter that realises the Round-Trip port as
/// a Project immediately followed by a Capture, so an Editor Session can obtain the Canonical
/// Markdown of either side of a Conflict (INV-025).
/// </summary>
public sealed class FlowDocumentRoundTripTests
{
    [Theory]
    [InlineData("Title\n=====", "# Title")]
    [InlineData("Sub\n---", "## Sub")]
    [InlineData("Hello _there_", "Hello *there*")]
    [InlineData("Hello __there__", "Hello **there**")]
    [InlineData("* One\n* Two", "- One\n- Two")]
    public void RoundTrip_NormalisesSyntaxStyle_ToCanonicalMarkdown_INV025(string authored, string canonical)
    {
        StaThread.Run(() => new FlowDocumentRoundTrip().RoundTrip(authored).ShouldBe(canonical));
    }

    [Fact]
    public void RoundTrip_LeavesCanonicalMarkdownUnchanged_INV025()
    {
        const string canonical = "# Title\n\nHello *there*\n\n- One\n- Two";

        StaThread.Run(() => new FlowDocumentRoundTrip().RoundTrip(canonical).ShouldBe(canonical));
    }

    [Fact]
    public void RoundTrip_GivenEmptySourceText_IsEmpty_INV025()
    {
        StaThread.Run(() => new FlowDocumentRoundTrip().RoundTrip("").ShouldBe(""));
    }
}
