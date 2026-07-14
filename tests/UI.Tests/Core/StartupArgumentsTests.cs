using Shouldly;
using UI.Core;
using Xunit;

namespace UI.Tests.Core;

/// <summary>
/// Tests for <see cref="StartupArguments"/>: extracting the Startup Document — the Markdown file
/// path handed to the editor by the command line or the shell's "Open with" (INV-020) — from the
/// process arguments, without being confused by host configuration switches.
/// </summary>
public sealed class StartupArgumentsTests
{
    [Fact]
    public void DocumentPath_WithNoArguments_ReturnsNull_INV020()
    {
        StartupArguments.DocumentPath([]).ShouldBeNull();
    }

    [Theory]
    [InlineData(@"C:\docs\note.md")]
    [InlineData(@"C:\docs\note.markdown")]
    [InlineData(@"C:\DOCS\NOTE.MD")]
    public void DocumentPath_WithMarkdownFileArgument_ReturnsIt_INV020(string path)
    {
        StartupArguments.DocumentPath([path]).ShouldBe(path);
    }

    [Fact]
    public void DocumentPath_IgnoresHostConfigurationSwitches_INV020()
    {
        // Generic-host switches ("--environment Development") must never be mistaken for a document.
        StartupArguments.DocumentPath(["--environment", "Development"]).ShouldBeNull();
    }

    [Fact]
    public void DocumentPath_SkipsSwitchesBeforeTheDocument_INV020()
    {
        StartupArguments.DocumentPath(["--environment", "Development", @"C:\docs\note.md"])
            .ShouldBe(@"C:\docs\note.md");
    }

    [Fact]
    public void DocumentPath_WithNonMarkdownArgument_ReturnsNull_INV020()
    {
        StartupArguments.DocumentPath([@"C:\docs\image.png"]).ShouldBeNull();
    }
}
