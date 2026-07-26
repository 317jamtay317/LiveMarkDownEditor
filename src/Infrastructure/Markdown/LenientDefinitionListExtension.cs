using Markdig;
using Markdig.Extensions.DefinitionLists;
using Markdig.Renderers;

namespace Infrastructure.Markdown;

/// <summary>
/// Swaps Markdig's <see cref="DefinitionListParser"/> for the <see cref="LenientDefinitionListParser"/>,
/// so a Definition Description is read from its marker plus one space (INV-066).
/// </summary>
/// <remarks>
/// This must be registered <em>after</em> <c>UseDefinitionLists()</c>: Markdig runs an extension's
/// setup at build time, so the parser this replaces does not exist on the builder until the
/// definition-list extension itself has run.
/// </remarks>
public sealed class LenientDefinitionListExtension : IMarkdownExtension
{
    /// <summary>Replaces the strict definition-list block parser with the lenient one.</summary>
    /// <param name="pipeline">The pipeline builder being configured.</param>
    public void Setup(MarkdownPipelineBuilder pipeline) =>
        pipeline.BlockParsers.Replace<DefinitionListParser>(new LenientDefinitionListParser());

    /// <summary>No renderer changes: leniency is a parsing rule, and the Rendered Output is unchanged.</summary>
    /// <param name="pipeline">The built pipeline.</param>
    /// <param name="renderer">The renderer being configured.</param>
    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
    }
}
