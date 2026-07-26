using System.IO;

namespace UI.Wysiwyg;

/// <summary>
/// The one place a Media Source — an Image Source or a Video Source — is turned into something that can
/// actually be shown: the absolute URI it names, or nothing at all. An Image and a Video resolve through
/// here alike, which is what makes their rules identical (INV-031/069).
/// </summary>
internal static class MediaSource
{
    /// <summary>
    /// The absolute URI <paramref name="url"/> names, or <see langword="null"/> when it names nothing
    /// reachable — a missing file, an unusable path, or a relative source with no Base Directory to
    /// resolve against. Either way the caller falls back to the alt text (INV-031).
    /// </summary>
    /// <param name="url">The Media Source, absolute or relative to <paramref name="baseDirectory"/>.</param>
    /// <param name="baseDirectory">The Base Directory a relative Media Source resolves against, or
    /// <see langword="null"/> when the Editor Session has no file and so no folder to resolve against.</param>
    /// <returns>The URI to show, or <see langword="null"/>.</returns>
    internal static Uri? Resolve(string url, string? baseDirectory)
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
                // A source that is not a usable path names no file — the alt text stands in.
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
}
