using Markdig;
using Markdig.Extensions.EmphasisExtras;

namespace Infrastructure.Markdown;

/// <summary>
/// Builds the single, shared Markdig pipeline configured for GitHub Flavored Markdown (GFM):
/// CommonMark plus pipe/grid tables, task lists, strikethrough, and autolinks.
/// </summary>
/// <remarks>
/// Both the HTML render path and the Visual Document projection MUST parse with the identical
/// pipeline so that what the user edits and what is exported agree on the same GFM feature set.
/// </remarks>
public static class GfmPipeline
{
    /// <summary>Creates a fresh GFM-configured <see cref="MarkdownPipeline"/>.</summary>
    /// <returns>A pipeline enabling the GFM extension set on top of CommonMark.</returns>
    public static MarkdownPipeline Create() =>
        new MarkdownPipelineBuilder()
            .UsePipeTables()
            .UseGridTables()
            .UseTaskLists()
            .UseEmphasisExtras(EmphasisExtraOptions.Strikethrough)
            .UseAutoLinks()
            .Build();
}
