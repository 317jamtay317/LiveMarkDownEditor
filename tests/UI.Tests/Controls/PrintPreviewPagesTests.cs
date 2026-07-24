using System.Windows.Documents;
using Shouldly;
using UI.Controls;
using UI.Core;
using UI.Tests.Wysiwyg;
using Xunit;

namespace UI.Tests.Controls;

/// <summary>
/// Tests for <see cref="PrintPreviewPages"/>: paginating again — as happens the moment both the
/// Document and the Setup bindings land, one after the other — must re-host the paginator's cached
/// page visuals rather than crash on "already a child of another Visual" (INV-061).
/// </summary>
public sealed class PrintPreviewPagesTests
{
    [Fact]
    public void Paginate_Twice_ReHostsTheCachedPageVisuals_INV061()
    {
        StaThread.Run(() =>
        {
            var pages = new PrintPreviewPages
            {
                Document = new FlowDocument(new Paragraph(new Run("Body text"))),
            };

            // The second pagination: the paginator hands back the same cached page visuals, which
            // must be detached from the old hosts before they are hosted again.
            Should.NotThrow(() =>
                pages.Setup = new PageSetup(PageOrientation.Landscape, PrintMargins.For(MarginPreset.Narrow)));
        });
    }
}
