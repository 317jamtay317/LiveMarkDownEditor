using System.IO;
using System.Windows.Automation;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfImage = System.Windows.Controls.Image;

namespace UI.Wysiwyg;

/// <summary>
/// The one shared definition of what an Image looks like in the Visual Document: the picture its
/// Image Source names, or its alt text when that picture cannot be shown (INV-031). The Projector
/// composes a loaded Image through <see cref="CreateImage"/> and the Insert Image Formatting Action
/// composes a user-inserted one through the same seam, so Capture treats the two alike (INV-018).
/// </summary>
/// <remarks>
/// Either presentation carries the same <see cref="ImageRole"/> — the Image Source and alt text the
/// Image was built with, <em>not</em> the absolute path a relative source resolved to. That role is
/// what Capture keys on, so an Image re-emits the source its author wrote and the Markdown Document
/// stays portable (INV-031).
/// </remarks>
internal static class ImageFormatting
{
    // A picture wider than this is scaled down to fit rather than shown at its natural size: a photo
    // straight off a camera is thousands of pixels wide and would otherwise push the whole line off
    // the page. Uniform scaling keeps its aspect ratio.
    private const double MaximumWidth = 700d;

    /// <summary>
    /// Composes an Image: the picture <paramref name="url"/> names, or a fallback showing
    /// <paramref name="alt"/> when it cannot be shown (INV-031).
    /// </summary>
    /// <param name="url">The Image Source, absolute or relative to <paramref name="baseDirectory"/>.</param>
    /// <param name="alt">The Image's alt text.</param>
    /// <param name="title">The optional image title, or <see langword="null"/>.</param>
    /// <param name="baseDirectory">The Base Directory a relative Image Source resolves against, or
    /// <see langword="null"/> when the Editor Session has no file and so no folder to resolve against.</param>
    /// <returns>The inline to show, carrying the Image's <see cref="ImageRole"/> either way.</returns>
    internal static Inline CreateImage(string url, string alt, string? title, string? baseDirectory)
    {
        var role = new ImageRole(url, alt, title);
        if (Resolve(url, baseDirectory) is not { } source || Decode(source) is not { } bitmap)
        {
            return AltText(role);
        }

        var picture = new WpfImage
        {
            Source = bitmap,
            Stretch = Stretch.Uniform,
            // DownOnly is what keeps a small picture its own size. Uniform alone scales in both
            // directions, so a 240px-wide picture would be blown up to fill the line — and a
            // thumbnail rendered a page wide is a worse lie than one rendered too small.
            StretchDirection = System.Windows.Controls.StretchDirection.DownOnly,
            MaxWidth = MaximumWidth,
        };

        // The alt text is what a screen reader is given for a picture it cannot describe — the same
        // words a sighted user sees when the picture fails to load (INV-031).
        AutomationProperties.SetName(picture, alt);

        // The picture is hosted in a ContentControl rather than being the container's Child directly,
        // so a failed download can swap the alt text in *below* the Visual Document: the document's
        // own element tree is what an edit is made of, and replacing InlineUIContainer.Child would
        // raise TextChanged, Capture, and mark the Editor Session unsaved. A picture that never
        // arrived is not an edit the user made (INV-031).
        var host = new System.Windows.Controls.ContentControl { Content = picture };
        var container = new InlineUIContainer(host) { Tag = role };

        // A remote picture's bytes arrive after this method returns, so its failure cannot be seen
        // above: an unreachable address falls back to the alt text here instead, when it is known.
        if (bitmap.IsDownloading)
        {
            bitmap.DownloadFailed += (_, _) => host.Content = AltTextBlock(alt);
        }

        return container;
    }

    /// <summary>The alt-text presentation of <paramref name="role"/>: what an Image shows when its
    /// picture cannot be (INV-031).</summary>
    /// <param name="role">The Image's source and alt text, carried through for Capture.</param>
    private static Run AltText(ImageRole role) => new(role.Alt) { Tag = role };

    // The alt text as a UI element, for swapping into a container whose remote picture never arrived.
    private static System.Windows.Controls.TextBlock AltTextBlock(string alt) => new() { Text = alt };

    // The absolute URI an Image Source names, or null when it names nothing reachable. A relative
    // source is meaningless without a Base Directory, which is why an unsaved Editor Session's
    // relative Images fall back to their alt text (INV-031).
    private static Uri? Resolve(string url, string? baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute))
        {
            // A file:// URI still has to name a file that exists; a remote one is the network's to
            // judge, asynchronously.
            return !absolute.IsFile || File.Exists(absolute.LocalPath) ? absolute : null;
        }

        if (baseDirectory is null)
        {
            return null;
        }

        // Decoded first, literal second. A space cannot be written bare in a Markdown URL — it does
        // not parse as one — so `my%20photo.png` is the form an author is handed for "my photo.png",
        // and percent-decoding is what every other Markdown tool does with it. The literal name is
        // still tried afterwards, so a file whose name genuinely contains a percent sign is found as
        // written rather than mangled by the decoding.
        foreach (var candidate in new[] { Decode(url), url })
        {
            if (candidate is null)
            {
                continue;
            }

            try
            {
                var path = Path.GetFullPath(Path.Combine(baseDirectory, candidate));
                if (File.Exists(path))
                {
                    return new Uri(path);
                }
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException
                                                  or PathTooLongException or IOException)
            {
                // A source that is not a usable path names no picture — the alt text stands in.
            }
        }

        return null;
    }

    // The percent-decoded form of url, or null when it decodes to nothing new (or is not decodable).
    private static string? Decode(string url)
    {
        try
        {
            var decoded = Uri.UnescapeDataString(url);
            return decoded == url ? null : decoded;
        }
        catch (Exception exception) when (exception is UriFormatException or ArgumentException)
        {
            return null;
        }
    }

    // Decodes the picture at uri, or returns null when it is not one. A local file decodes here and
    // now, so a source that is not an image is caught before it is shown; a remote one only starts
    // downloading, and reports its failure through BitmapImage.DownloadFailed.
    private static BitmapImage? Decode(Uri uri)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = uri;

            // OnLoad decodes immediately and releases the file, so the Image Source is not left
            // locked on disk — the user must stay free to edit or replace the picture they linked.
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            return bitmap;
        }
        catch (Exception exception) when (exception is NotSupportedException or FileFormatException
                                              or IOException or UriFormatException or ArgumentException
                                              or InvalidOperationException)
        {
            return null;
        }
    }
}
