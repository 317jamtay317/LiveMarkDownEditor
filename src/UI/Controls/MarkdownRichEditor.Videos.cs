using System.Windows;
using UI.Wysiwyg;

namespace UI.Controls;

// Videos: routing a click to the Video Player it landed on, so a Video can be played, paused, and
// scrubbed where it stands (INV-069). All of it is view-only — a Video Player holds nothing Capture
// reads, so watching a document never changes it.
public sealed partial class MarkdownRichEditor
{
    /// <summary>
    /// Hands a click at <paramref name="point"/> — in this editor's coordinates — to the Video Player it
    /// landed on, if any, which starts the Video (INV-069). It reaches an unstarted player only: once a
    /// Video is running its surface is a window of its own and takes its clicks directly, which is what
    /// gives the reader the real playback controls rather than a picture of them.
    /// </summary>
    /// <param name="point">Where the click landed, relative to this editor.</param>
    /// <returns><see langword="true"/> when a Video Player took the click; otherwise
    /// <see langword="false"/>, and the click goes on to place the caret as it always has.</returns>
    /// <remarks>
    /// The player is found by its rendered bounds rather than by the text hit-test: a
    /// <c>RichTextBox</c> lays a text layer over the elements embedded in its document, so
    /// <c>GetPositionFromPoint</c> answers about the text beside a Video Player rather than about the
    /// player itself. A document holds few Videos, so asking each one whether the click is inside it is
    /// both exact and cheap.
    /// </remarks>
    private bool HandleVideoClick(Point point)
    {
        foreach (var player in VideoFormatting.PlayersIn(Document))
        {
            if (!player.IsDescendantOf(this) || player.RenderSize.Width <= 0)
            {
                continue;
            }

            var local = TranslatePoint(point, player);
            if (new Rect(player.RenderSize).Contains(local))
            {
                return player.HandleClickAt(local);
            }
        }

        return false;
    }
}
