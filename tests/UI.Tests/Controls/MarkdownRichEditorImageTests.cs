using System.IO;
using System.Windows.Documents;
using Shouldly;
using UI.Controls;
using UI.Tests.Wysiwyg;
using Xunit;

namespace UI.Tests.Controls;

/// <summary>
/// Tests for how an Image is shown in the Visual Document: as the picture its Image Source names, or
/// as its alt text when that picture cannot be shown (INV-031). A relative Image Source resolves
/// against the Base Directory, and an Image Captures back to <c>![alt](url)</c> whichever of the two
/// it is showing (INV-018).
/// </summary>
public sealed class MarkdownRichEditorImageTests : IDisposable
{
    [Fact]
    public void Image_WithAnAbsoluteImageSource_ShowsThePicture_INV031()
    {
        StaThread.Run(() =>
        {
            var file = WritePng("absolute.png");

            var editor = new MarkdownRichEditor { Markdown = $"![a cat]({file})" };

            PicturesIn(editor).Count.ShouldBe(1, "An absolute Image Source names the picture outright.");
        });
    }

    /// <summary>
    /// The form Markdown authors write most: the picture sits beside the document. It names a file
    /// only in relation to the Base Directory, which is why Project takes one (INV-003).
    /// </summary>
    [Fact]
    public void Image_WithARelativeImageSource_ResolvesItAgainstTheBaseDirectory_INV031()
    {
        StaThread.Run(() =>
        {
            WritePng("relative.png");

            var editor = new MarkdownRichEditor
            {
                BaseDirectory = _directory,
                Markdown = "![a cat](relative.png)",
            };

            PicturesIn(editor).Count.ShouldBe(1, "The picture sits beside the document.");
        });
    }

    /// <summary>
    /// Percent-encoding is how a space is written in a Markdown URL — a bare space does not parse as
    /// one at all — so `my%20photo.png` is the form an author is given for a file whose name has a
    /// space in it, and it names the same file on disk as `my photo.png`.
    /// </summary>
    [Fact]
    public void Image_WithAPercentEncodedRelativeImageSource_ResolvesTheDecodedFile_INV031()
    {
        StaThread.Run(() =>
        {
            WritePng("my photo.png");

            var editor = new MarkdownRichEditor
            {
                BaseDirectory = _directory,
                Markdown = "![a cat](my%20photo.png)",
            };

            PicturesIn(editor).Count.ShouldBe(1, "%20 names the space in 'my photo.png'.");
        });
    }

    /// <summary>A file whose name genuinely contains a percent sign is still found as written.</summary>
    [Fact]
    public void Image_WithAPercentInTheFileName_StillResolvesTheLiteralFile_INV031()
    {
        StaThread.Run(() =>
        {
            WritePng("100%25.png");

            var editor = new MarkdownRichEditor
            {
                BaseDirectory = _directory,
                Markdown = "![a cat](100%25.png)",
            };

            PicturesIn(editor).Count.ShouldBe(1);
        });
    }

    [Fact]
    public void Image_WithARelativeImageSource_AndNoBaseDirectory_FallsBackToAltText_INV031()
    {
        StaThread.Run(() =>
        {
            // An unsaved Editor Session has no file, so "beside this document" names no folder yet.
            var editor = new MarkdownRichEditor { Markdown = "![a cat](relative.png)" };

            PicturesIn(editor).ShouldBeEmpty();
            TextIn(editor).ShouldBe("a cat");
        });
    }

    [Fact]
    public void Image_WhoseImageSourceIsMissing_FallsBackToAltText_INV031()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor
            {
                BaseDirectory = _directory,
                Markdown = "![a cat](no-such-file.png)",
            };

            PicturesIn(editor).ShouldBeEmpty();
            TextIn(editor).ShouldBe("a cat");
        });
    }

    [Fact]
    public void Image_WhoseImageSourceIsNotAnImage_FallsBackToAltText_INV031()
    {
        StaThread.Run(() =>
        {
            File.WriteAllText(Path.Combine(_directory, "notes.txt"), "not a picture");

            var editor = new MarkdownRichEditor
            {
                BaseDirectory = _directory,
                Markdown = "![a cat](notes.txt)",
            };

            PicturesIn(editor).ShouldBeEmpty();
            TextIn(editor).ShouldBe("a cat");
        });
    }

    [Fact]
    public void Image_ShowingItsPicture_StillCapturesTheImageSourceItWasBuiltWith_INV018()
    {
        StaThread.Run(() =>
        {
            WritePng("shown.png");

            var editor = new MarkdownRichEditor
            {
                BaseDirectory = _directory,
                Markdown = "![a cat](shown.png)",
            };

            // The relative Image Source, not the absolute path it resolved to: the Markdown Document
            // has to stay portable (INV-031).
            editor.Markdown.ShouldBe("![a cat](shown.png)");
        });
    }

    [Fact]
    public void Image_FallenBackToAltText_StillCapturesAsAnImage_INV018()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor
            {
                BaseDirectory = _directory,
                Markdown = "![a cat](no-such-file.png)",
            };

            // A picture that would not load is not an edit: the source text is the author's.
            editor.Markdown.ShouldBe("![a cat](no-such-file.png)");
        });
    }

    [Fact]
    public void Image_ShowingItsPicture_RoundTrips_INV005()
    {
        StaThread.Run(() =>
        {
            WritePng("trip.png");

            var editor = new MarkdownRichEditor
            {
                BaseDirectory = _directory,
                Markdown = "text ![a cat](trip.png) more",
            };

            var captured = editor.Markdown;
            var reloaded = new MarkdownRichEditor { BaseDirectory = _directory, Markdown = captured };

            reloaded.Markdown.ShouldBe(captured);
        });
    }

    /// <summary>
    /// Every Image element the Visual Document shows, in document order. The picture is looked for
    /// inside the container's host element, which is what lets a failed download swap in the alt text
    /// without that swap counting as an edit (INV-031).
    /// </summary>
    private static List<System.Windows.Controls.Image> PicturesIn(MarkdownRichEditor editor)
    {
        var pictures = new List<System.Windows.Controls.Image>();
        for (var pointer = editor.Document.ContentStart;
             pointer is not null;
             pointer = pointer.GetNextContextPosition(System.Windows.Documents.LogicalDirection.Forward))
        {
            if (pointer.Parent is InlineUIContainer
                {
                    Child: System.Windows.Controls.ContentControl
                    {
                        Content: System.Windows.Controls.Image picture,
                    },
                }
                && !pictures.Contains(picture))
            {
                pictures.Add(picture);
            }
        }

        return pictures;
    }

    private static string TextIn(MarkdownRichEditor editor) =>
        new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text.Trim();

    // A real, decodable 1x1 PNG. A file of arbitrary bytes with a .png name would fail to decode,
    // which is the fallback case rather than the success case these tests need.
    private string WritePng(string name)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllBytes(path, Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="));
        return path;
    }

    private readonly string _directory =
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "md-image-tests", Guid.NewGuid().ToString("N")))
            .FullName;

    /// <summary>Removes the temporary folder this test's Image Sources were written to.</summary>
    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // A picture still held open by WPF's decoder is not this test's problem to solve.
        }
    }
}
