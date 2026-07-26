using System.Windows.Automation;
using System.Windows.Documents;
using UI.Controls;

namespace UI.Wysiwyg;

/// <summary>
/// The one shared definition of what a Video looks like in the Visual Document: a Video Player showing
/// the video its Media Source names, or its alt text when that video cannot be played (INV-069). The
/// Projector composes a loaded Video through <see cref="CreateVideo"/> and the Insert Video Formatting
/// Action composes a user-inserted one through the same seam, so Capture treats the two alike (INV-018).
/// </summary>
/// <remarks>
/// Either presentation carries the same <see cref="VideoRole"/> — the Media Source and alt text the Video
/// was built with, <em>not</em> the absolute path a relative source resolved to. That role is what
/// Capture keys on, so a Video re-emits the source its author wrote and the Markdown Document stays
/// portable (INV-031/069).
/// </remarks>
internal static class VideoFormatting
{
    /// <summary>
    /// Composes a Video: a Video Player for the video <paramref name="url"/> names, or a fallback showing
    /// <paramref name="alt"/> when it names nothing that can be played (INV-069).
    /// </summary>
    /// <param name="url">The Media Source, absolute or relative to <paramref name="baseDirectory"/>.</param>
    /// <param name="alt">The Video's alt text.</param>
    /// <param name="title">The optional title, or <see langword="null"/>.</param>
    /// <param name="baseDirectory">The Base Directory a relative Media Source resolves against, or
    /// <see langword="null"/> when the Editor Session has no file and so no folder to resolve against.</param>
    /// <returns>The inline to show, carrying the Video's <see cref="VideoRole"/> either way.</returns>
    internal static Inline CreateVideo(string url, string alt, string? title, string? baseDirectory)
    {
        var role = new VideoRole(url, alt, title);
        if (MediaSource.Resolve(url, baseDirectory) is not { } source)
        {
            return AltText(role);
        }

        var player = new VideoPlayerView { Source = source };

        // The alt text is what a screen reader is given for a video it cannot describe — the same words
        // a sighted reader sees when the video will not play (INV-031/069).
        AutomationProperties.SetName(player, alt);

        // The player is hosted in a ContentControl rather than being the container's Child directly, so a
        // Video that turns out to be unplayable can swap its alt text in *below* the Visual Document:
        // the document's own element tree is what an edit is made of, and replacing
        // InlineUIContainer.Child would raise TextChanged, Capture, and mark the Editor Session unsaved.
        // A video that would not play is not an edit the user made (INV-069).
        var host = new System.Windows.Controls.ContentControl { Content = player };
        player.PlaybackFailed += (_, _) => host.Content = AltTextBlock(alt);

        return new InlineUIContainer(host) { Tag = role };
    }

    /// <summary>
    /// Every Video Player the Visual Document shows, in document order. Reading them is view-only — it is
    /// how the editor finds the player a click landed on, since a <c>RichTextBox</c> lays a text layer
    /// over the elements embedded in its document (INV-069).
    /// </summary>
    /// <param name="document">The Visual Document to walk, or <see langword="null"/>.</param>
    /// <returns>The Video Players shown, in document order.</returns>
    internal static IReadOnlyList<VideoPlayerView> PlayersIn(FlowDocument? document)
    {
        if (document is null)
        {
            return [];
        }

        var players = new List<VideoPlayerView>();
        for (var pointer = document.ContentStart;
             pointer is not null;
             pointer = pointer.GetNextContextPosition(LogicalDirection.Forward))
        {
            if (pointer.Parent is InlineUIContainer
                {
                    Tag: VideoRole,
                    Child: System.Windows.Controls.ContentControl { Content: VideoPlayerView player },
                }
                && !players.Contains(player))
            {
                players.Add(player);
            }
        }

        return players;
    }

    // The alt-text presentation of a Video: what it shows when its video cannot be played (INV-069).
    private static Run AltText(VideoRole role) => new(role.Alt) { Tag = role };

    // The alt text as a UI element, for swapping into a container whose video turned out unplayable.
    private static System.Windows.Controls.TextBlock AltTextBlock(string alt) => new() { Text = alt };
}
