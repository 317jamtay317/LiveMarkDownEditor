using System.Text;
using Shouldly;
using UI.Wysiwyg;
using Xunit;

namespace UI.Tests.Wysiwyg;

/// <summary>
/// Tests for <see cref="CfHtml"/>: the CF_HTML clipboard wrapper whose byte offsets a web editor
/// reads to find the fragment. A miscomputed offset pastes garbage or nothing, so these pin the
/// offsets against the actual UTF-8 bytes (INV-035).
/// </summary>
public sealed class CfHtmlTests
{
    [Fact]
    public void Wrap_GivenNull_Throws()
    {
        Should.Throw<ArgumentNullException>(() => CfHtml.Wrap(null!));
    }

    [Fact]
    public void Wrap_BeginsWithTheCfHtmlVersionHeader()
    {
        CfHtml.Wrap("<p>hi</p>").ShouldStartWith("Version:0.9");
    }

    [Fact]
    public void Wrap_CarriesTheFragmentBetweenTheFragmentMarkers()
    {
        CfHtml.Wrap("<p>hi</p>").ShouldContain("<!--StartFragment--><p>hi</p><!--EndFragment-->");
    }

    [Theory]
    [InlineData("<p>plain ascii</p>")]
    [InlineData("<p>café — déjà vu</p>")] // multi-byte, so byte offsets differ from char offsets
    public void Wrap_OffsetsIndexTheRightBytes(string fragment)
    {
        var cf = CfHtml.Wrap(fragment);
        var bytes = Encoding.UTF8.GetBytes(cf);

        var startHtml = ReadOffset(cf, "StartHTML:");
        var endHtml = ReadOffset(cf, "EndHTML:");
        var startFragment = ReadOffset(cf, "StartFragment:");
        var endFragment = ReadOffset(cf, "EndFragment:");

        Encoding.UTF8.GetString(bytes, startHtml, 6).ShouldBe("<html>");
        Encoding.UTF8.GetString(bytes, startFragment, endFragment - startFragment).ShouldBe(fragment);
        endHtml.ShouldBe(bytes.Length);
    }

    private static int ReadOffset(string cf, string label)
    {
        var start = cf.IndexOf(label, StringComparison.Ordinal) + label.Length;
        return int.Parse(cf.Substring(start, 10));
    }
}
