using Domain;
using Shouldly;
using Xunit;

namespace Domain.Tests;

/// <summary>
/// Tests for <see cref="VideoSource"/>, the single decision of what a Video Source is (INV-069). A
/// Video is written with the Image's own syntax, so the Media Source is the only thing that <em>can</em>
/// tell the two apart — and it is decided from the source text alone, never from the file's contents,
/// so the answer is the same before the file has been read as after (INV-003).
/// </summary>
public sealed class VideoSourceTests
{
    [Theory]
    [InlineData("clip.mp4")]
    [InlineData("clip.webm")]
    [InlineData("clip.mov")]
    [InlineData("clip.m4v")]
    [InlineData("clip.mkv")]
    [InlineData("clip.avi")]
    [InlineData("clip.ogv")]
    [InlineData("clip.wmv")]
    public void IsVideo_GivenAVideoFileExtension_IsTrue_INV069(string url) =>
        VideoSource.IsVideo(url).ShouldBeTrue();

    [Theory]
    [InlineData("cat.png")]
    [InlineData("cat.jpg")]
    [InlineData("cat.svg")]
    [InlineData("notes.txt")]
    [InlineData("no-extension")]
    [InlineData("archive.mp4.txt")]
    [InlineData("")]
    [InlineData(null)]
    public void IsVideo_GivenAnythingElse_IsFalse_INV069(string? url) =>
        VideoSource.IsVideo(url).ShouldBeFalse();

    /// <summary>An author writes `.MP4` as readily as `.mp4`; a file system does not care, so nor does this.</summary>
    [Theory]
    [InlineData("HOLIDAY.MP4")]
    [InlineData("media/Holiday.Mov")]
    public void IsVideo_IgnoresCase_INV069(string url) =>
        VideoSource.IsVideo(url).ShouldBeTrue();

    /// <summary>
    /// A remote Media Source carries a query or a fragment as often as not, and neither is part of the
    /// name of the file being fetched.
    /// </summary>
    [Theory]
    [InlineData("https://example.com/media/demo.mp4?t=30")]
    [InlineData("https://example.com/media/demo.mp4#start")]
    [InlineData("demo.mp4?v=2")]
    public void IsVideo_LooksPastAQueryOrFragment_INV069(string url) =>
        VideoSource.IsVideo(url).ShouldBeTrue();

    /// <summary>
    /// The extension has to belong to the file, not to something earlier in the path: a query string
    /// mentioning a video does not make the resource one.
    /// </summary>
    [Theory]
    [InlineData("https://example.com/watch?file=demo.mp4")]
    [InlineData("https://example.com/mp4/thumbnail.png")]
    public void IsVideo_GivenAVideoNameElsewhereInTheUrl_IsFalse_INV069(string url) =>
        VideoSource.IsVideo(url).ShouldBeFalse();

    [Fact]
    public void IsVideo_IgnoresSurroundingWhitespace_INV069() =>
        VideoSource.IsVideo("  clip.mp4  ").ShouldBeTrue();

    [Fact]
    public void Extensions_AreTheRecognisedVideoKinds_INV069()
    {
        // The set is stated rather than guessed at, so what counts as a Video is reviewable — and each
        // extension is written with its dot, the form the check compares.
        VideoSource.Extensions.ShouldAllBe(extension => extension.StartsWith('.'));
        VideoSource.Extensions.ShouldContain(".mp4");
    }
}
