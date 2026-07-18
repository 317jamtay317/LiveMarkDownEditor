using System.IO;
using Shouldly;
using UI.Controls;
using UI.Tests.TestDoubles;
using UI.Tests.Wysiwyg;
using Xunit;

namespace UI.Tests.Controls;

/// <summary>
/// Tests for Ctrl+Click follow on the <see cref="MarkdownRichEditor"/> (INV-038): a Markdown Link is
/// opened in a new Tab through the follow command, and following is not an edit. (The web-address
/// branch launches the browser — a platform boundary — so it is covered by
/// <see cref="Wysiwyg.MarkdownLinkTests"/> at the classification level, not by launching a browser here.)
/// </summary>
public sealed class MarkdownRichEditorFollowLinkTests
{
    private const string BaseDirectory = @"C:\docs";

    [Fact]
    public void FollowLink_ToAMarkdownFile_InvokesTheFollowCommandWithItsAbsolutePath_INV038()
    {
        StaThread.Run(() =>
        {
            var follow = new RecordingCommand();
            var editor = new MarkdownRichEditor
            {
                Markdown = "see [notes](notes.md)",
                BaseDirectory = BaseDirectory,
                FollowLinkCommand = follow,
            };

            editor.FollowLink(new Uri("notes.md", UriKind.Relative));

            follow.ExecuteCount.ShouldBe(1);
            follow.LastParameter.ShouldBe(Path.Combine(BaseDirectory, "notes.md"));
        });
    }

    [Fact]
    public void FollowLink_ToAMarkdownFile_IsNotAnEdit_INV038()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor
            {
                Markdown = "see [notes](notes.md)",
                BaseDirectory = BaseDirectory,
                FollowLinkCommand = new RecordingCommand(),
            };

            editor.FollowLink(new Uri("notes.md", UriKind.Relative));

            editor.Markdown.ShouldBe("see [notes](notes.md)");
        });
    }

    [Fact]
    public void FollowLink_ToANonMarkdownTarget_DoesNothing_INV038()
    {
        StaThread.Run(() =>
        {
            var follow = new RecordingCommand();
            var editor = new MarkdownRichEditor
            {
                Markdown = "see [photo](photo.png)",
                BaseDirectory = BaseDirectory,
                FollowLinkCommand = follow,
            };

            editor.FollowLink(new Uri("photo.png", UriKind.Relative));

            follow.ExecuteCount.ShouldBe(0);
        });
    }
}
