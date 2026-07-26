using System.IO;
using System.Windows.Documents;
using Shouldly;
using UI.Controls;
using UI.Core;
using UI.Tests.TestDoubles;
using UI.Tests.Wysiwyg;
using Xunit;

namespace UI.Tests.Controls;

/// <summary>
/// Tests for how a Video is shown, played, and Captured (INV-069). A Video is written with the Image's
/// own syntax — <c>![alt](clip.mp4)</c> — and told apart from an Image by its Media Source alone; it is
/// shown as a Video Player that plays it in place, resolves and falls back exactly as an Image does
/// (INV-031), and re-emits the source it was authored with whether it is playing or not (INV-018).
/// </summary>
public sealed class MarkdownRichEditorVideoTests : IDisposable
{
    [Fact]
    public void Video_WithAnAbsoluteVideoSource_ShowsAPlayer_INV069()
    {
        StaThread.Run(() =>
        {
            var file = WriteFile("absolute.mp4");

            var editor = new MarkdownRichEditor { Markdown = $"![a clip]({file})" };

            PlayersIn(editor).Count.ShouldBe(1, "An absolute Media Source names the video outright.");
        });
    }

    [Fact]
    public void Video_WithARelativeVideoSource_ResolvesItAgainstTheBaseDirectory_INV069()
    {
        StaThread.Run(() =>
        {
            WriteFile("relative.mp4");

            var editor = new MarkdownRichEditor
            {
                BaseDirectory = _directory,
                Markdown = "![a clip](relative.mp4)",
            };

            // The identical rule an Image Source follows: the file sits beside the document (INV-031).
            PlayersIn(editor).Count.ShouldBe(1);
        });
    }

    /// <summary>
    /// The Media Source decides which construct it is, and nothing else could: the syntax is the same.
    /// </summary>
    [Fact]
    public void Image_WhoseSourceIsNotAVideo_IsStillAnImage_INV069()
    {
        StaThread.Run(() =>
        {
            WriteFile("cat.png");

            var editor = new MarkdownRichEditor
            {
                BaseDirectory = _directory,
                Markdown = "![a cat](cat.png)",
            };

            PlayersIn(editor).ShouldBeEmpty();
        });
    }

    [Fact]
    public void Video_WithARelativeVideoSource_AndNoBaseDirectory_FallsBackToAltText_INV069()
    {
        StaThread.Run(() =>
        {
            // An unsaved Editor Session has no folder for "beside this document" to mean (INV-031).
            var editor = new MarkdownRichEditor { Markdown = "![a clip](relative.mp4)" };

            PlayersIn(editor).ShouldBeEmpty();
            TextIn(editor).ShouldBe("a clip");
        });
    }

    [Fact]
    public void Video_WhoseVideoSourceIsMissing_FallsBackToAltText_INV069()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor
            {
                BaseDirectory = _directory,
                Markdown = "![a clip](no-such-file.mp4)",
            };

            // A Video that cannot be played must never leave a hole where the author's words were.
            PlayersIn(editor).ShouldBeEmpty();
            TextIn(editor).ShouldBe("a clip");
        });
    }

    [Fact]
    public void Video_ShowingItsPlayer_CapturesTheVideoSourceItWasBuiltWith_INV069()
    {
        StaThread.Run(() =>
        {
            WriteFile("shown.mp4");

            var editor = new MarkdownRichEditor
            {
                BaseDirectory = _directory,
                Markdown = "![a clip](shown.mp4)",
            };

            // The relative Media Source, not the absolute path it resolved to: the document stays
            // portable (INV-031).
            editor.Markdown.ShouldBe("![a clip](shown.mp4)");
        });
    }

    [Fact]
    public void Video_FallenBackToAltText_StillCapturesAsAVideo_INV069()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor
            {
                BaseDirectory = _directory,
                Markdown = "![a clip](no-such-file.mp4)",
            };

            editor.Markdown.ShouldBe("![a clip](no-such-file.mp4)");
        });
    }

    [Fact]
    public void Video_ShowingItsPlayer_RoundTrips_INV005()
    {
        StaThread.Run(() =>
        {
            WriteFile("trip.mp4");

            var editor = new MarkdownRichEditor
            {
                BaseDirectory = _directory,
                Markdown = "text ![a clip](trip.mp4) more",
            };

            var captured = editor.Markdown;
            var reloaded = new MarkdownRichEditor { BaseDirectory = _directory, Markdown = captured };

            reloaded.Markdown.ShouldBe(captured);
        });
    }

    /// <summary>
    /// A document that plays at its reader is a document that has taken something from them: a freshly
    /// projected Video is paused (INV-069).
    /// </summary>
    [Fact]
    public void Video_IsPausedUntilItIsAskedToPlay_INV069()
    {
        StaThread.Run(() =>
        {
            WriteFile("quiet.mp4");

            var editor = new MarkdownRichEditor
            {
                BaseDirectory = _directory,
                Markdown = "![a clip](quiet.mp4)",
            };

            PlayersIn(editor).Single().IsPlaying.ShouldBeFalse();
        });
    }

    [Fact]
    public void Video_PlayedAndPaused_ChangesNothingInTheMarkdown_INV069()
    {
        StaThread.Run(() =>
        {
            WriteFile("watched.mp4");

            var editor = new MarkdownRichEditor
            {
                BaseDirectory = _directory,
                Markdown = "![a clip](watched.mp4)",
            };
            var player = PlayersIn(editor).Single();

            player.TogglePlay();
            player.IsPlaying.ShouldBeTrue();
            editor.Markdown.ShouldBe("![a clip](watched.mp4)", "Watching a document is not editing it.");

            player.TogglePlay();
            player.IsPlaying.ShouldBeFalse();
            editor.Markdown.ShouldBe("![a clip](watched.mp4)");
        });
    }

    [Fact]
    public void Video_Scrubbed_ChangesNothingInTheMarkdown_INV069()
    {
        StaThread.Run(() =>
        {
            WriteFile("scrubbed.mp4");

            var editor = new MarkdownRichEditor
            {
                BaseDirectory = _directory,
                Markdown = "![a clip](scrubbed.mp4)",
            };

            PlayersIn(editor).Single().SeekTo(0.5);

            editor.Markdown.ShouldBe("![a clip](scrubbed.mp4)");
        });
    }

    /// <summary>
    /// Two Videos talking over each other is never what a reader asked for, and they cannot be listening
    /// to both — so starting one pauses the other (INV-069).
    /// </summary>
    [Fact]
    public void Video_Started_PausesEveryOtherVideo_INV069()
    {
        StaThread.Run(() =>
        {
            WriteFile("first.mp4");
            WriteFile("second.mp4");

            var editor = new MarkdownRichEditor
            {
                BaseDirectory = _directory,
                Markdown = "![first](first.mp4)\n\n![second](second.mp4)",
            };
            var players = PlayersIn(editor);
            players.Count.ShouldBe(2);

            players[0].TogglePlay();
            players[1].TogglePlay();

            players[0].IsPlaying.ShouldBeFalse("The second Video took the first one's place.");
            players[1].IsPlaying.ShouldBeTrue();
        });
    }

    /// <summary>
    /// Only *starting* takes another Video's place. Pausing one leaves the rest exactly as they were —
    /// nothing has replaced them (INV-069).
    /// </summary>
    [Fact]
    public void Video_Paused_LeavesEveryOtherVideoAsItWas_INV069()
    {
        StaThread.Run(() =>
        {
            WriteFile("one.mp4");
            WriteFile("two.mp4");

            var editor = new MarkdownRichEditor
            {
                BaseDirectory = _directory,
                Markdown = "![one](one.mp4)\n\n![two](two.mp4)",
            };
            var players = PlayersIn(editor);

            players[1].TogglePlay();
            players[0].Pause();

            players[1].IsPlaying.ShouldBeTrue();
        });
    }

    [Fact]
    public void Video_Started_ChangesNoOtherVideosMarkdown_INV069()
    {
        StaThread.Run(() =>
        {
            WriteFile("a.mp4");
            WriteFile("b.mp4");
            const string markdown = "![a](a.mp4)\n\n![b](b.mp4)";

            var editor = new MarkdownRichEditor { BaseDirectory = _directory, Markdown = markdown };
            var players = PlayersIn(editor);

            players[0].TogglePlay();
            players[1].TogglePlay();

            // Taking a Video's place is still only playing, and playing is not an edit.
            editor.Markdown.ShouldBe(markdown);
        });
    }

    [Fact]
    public void InsertVideo_AtCaret_WritesTheImageSyntaxAVideoIsWrittenWith_INV069()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor
            {
                BaseDirectory = _directory,
                Markdown = "prose",
                LinkPrompt = new StubLinkPrompt(new LinkDetails("a clip", "clip.mp4")),
            };
            VisualDocumentText.PlaceCaretAfter(editor, "prose");

            MarkdownEditingCommands.InsertVideo.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("prose![a clip](clip.mp4)");
        });
    }

    [Fact]
    public void InsertVideo_WithTheLinkPromptDismissed_MakesNoEdit_INV030()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor
            {
                Markdown = "prose",
                LinkPrompt = new StubLinkPrompt(answer: null),
            };
            VisualDocumentText.PlaceCaretAfter(editor, "prose");

            MarkdownEditingCommands.InsertVideo.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("prose");
        });
    }

    /// <summary>
    /// Every Video Player the Visual Document shows, in document order. The player is looked for inside
    /// the container's host element, which is what lets an unplayable Video swap in its alt text below
    /// the document — without that swap counting as an edit (INV-031/069).
    /// </summary>
    private static List<VideoPlayerView> PlayersIn(MarkdownRichEditor editor)
    {
        var players = new List<VideoPlayerView>();
        for (var pointer = editor.Document.ContentStart;
             pointer is not null;
             pointer = pointer.GetNextContextPosition(LogicalDirection.Forward))
        {
            if (pointer.Parent is InlineUIContainer
                {
                    Child: System.Windows.Controls.ContentControl { Content: VideoPlayerView player },
                }
                && !players.Contains(player))
            {
                players.Add(player);
            }
        }

        return players;
    }

    private static string TextIn(MarkdownRichEditor editor) =>
        new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text.Trim();

    // A file that exists under the given name. Resolving a Media Source asks the file system whether
    // the file is there, not whether it decodes — an unplayable video reports itself when it is played,
    // which is the Image's DownloadFailed case reached for a Video (INV-069).
    private string WriteFile(string name)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllBytes(path, [0, 0, 0, 0]);
        return path;
    }

    private readonly string _directory =
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "md-video-tests", Guid.NewGuid().ToString("N")))
            .FullName;

    /// <summary>Removes the temporary folder this test's Media Sources were written to.</summary>
    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // A file still held open by the media stack is not this test's problem to solve.
        }
    }
}
