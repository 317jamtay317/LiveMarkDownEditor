using Markdig;
using Markdig.Extensions.EmphasisExtras;

namespace Infrastructure.Markdown;

/// <summary>
/// Builds the single, shared Markdig pipeline configured for GitHub Flavored Markdown (GFM):
/// CommonMark plus pipe/grid tables, task lists, strikethrough, autolinks, footnotes, and
/// definition lists.
/// </summary>
/// <remarks>
/// Both the HTML render path and the Visual Document projection MUST parse with the identical
/// pipeline so that what the user edits and what is exported agree on the same GFM feature set.
/// </remarks>
public static class GfmPipeline
{
    /// <summary>Creates a fresh GFM-configured <see cref="MarkdownPipeline"/>.</summary>
    /// <returns>A pipeline enabling the GFM extension set on top of CommonMark.</returns>
    public static MarkdownPipeline Create()
    {
        var builder = new MarkdownPipelineBuilder()
            .UsePipeTables()
            .UseGridTables()
            .UseTaskLists()
            .UseEmphasisExtras(EmphasisExtraOptions.Strikethrough)
            .UseAutoLinks()
            // A Footnote (INV-065) and a Definition List (INV-066) are part of the supported set, so
            // the projection and the Rendered Output must both read them as the constructs they are.
            .UseFootnotes()
            .UseDefinitionLists();

        // Registered last, and deliberately so: it replaces the parser the line above installs, which
        // Markdig does not add to the builder until that extension's own setup has run (INV-066).
        builder.Extensions.AddIfNotAlready(new LenientDefinitionListExtension());

        return builder.Build();
    }
}
