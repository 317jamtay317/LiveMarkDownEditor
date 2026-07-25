using Domain;
using Infrastructure.Markdown;
using Markdig.Extensions.DefinitionLists;
using Markdig.Extensions.Footnotes;
using Markdig.Syntax;
using MdTable = Markdig.Extensions.Tables.Table;

namespace Infrastructure.Pdf.Blocks;

/// <summary>
/// The one place that knows which Block Writer writes which Markdown block. A Block Writer holds no
/// state of its own, so one instance of each serves every block of its kind; only a Mermaid Diagram
/// gets a writer per block, because each carries its own rendered image.
/// </summary>
/// <param name="diagrams">
/// The rendered Mermaid Diagram images, keyed by the diagram's source, so a `mermaid` Code Block
/// with an image is written as the picture and one without falls back to its source text (INV-050).
/// Defaults to none.
/// </param>
/// <param name="syntaxHighlighter">
/// Tokenizes each Code Block's code so the PDF carries the same Syntax Highlighting the Visual
/// Document shows (INV-064). <see langword="null"/> writes every Code Block uncolored.
/// </param>
internal sealed class BlockFactory(
    IReadOnlyDictionary<string, PreparedDiagram>? diagrams = null,
    ISyntaxHighlighter? syntaxHighlighter = null) : IBlockFactory
{
    private readonly IReadOnlyDictionary<string, PreparedDiagram> _diagrams =
        diagrams ?? new Dictionary<string, PreparedDiagram>();

    private readonly IBlockWriter _heading = new HeadingWriter();
    private readonly IBlockWriter _paragraph = new ParagraphWriter();
    private readonly IBlockWriter _table = new TableWriter();
    private readonly IBlockWriter _list = new ListWriter();
    private readonly IBlockWriter _blockQuote = new BlockQuoteWriter();
    private readonly IBlockWriter _code = new CodeBlockWriter(syntaxHighlighter);
    private readonly IBlockWriter _thematicBreak = new ThematicBreakWriter();
    private readonly IBlockWriter _footnoteSection = new FootnoteSectionWriter();
    private readonly IBlockWriter _definitionList = new DefinitionListWriter();
    private readonly IBlockWriter _nested = new NestedBlocksWriter();
    private readonly IBlockWriter _skipped = new SkippedBlockWriter();

    /// <inheritdoc />
    public IBlockWriter Create(Block block) => block switch
    {
        HeadingBlock => _heading,
        ParagraphBlock => _paragraph,
        MdTable => _table,
        ListBlock => _list,
        QuoteBlock => _blockQuote,
        CodeBlock code when RenderedDiagramOf(code) is { } diagram => new MermaidDiagramWriter(diagram),
        CodeBlock => _code,
        ThematicBreakBlock => _thematicBreak,
        FootnoteGroup => _footnoteSection,
        DefinitionList => _definitionList,
        ContainerBlock => _nested,
        LeafBlock { Inline: not null } => _paragraph,
        _ => _skipped,
    };

    // The rendered image for a Mermaid Diagram, if this Code Block is one and an image was produced
    // for it (INV-050).
    private PreparedDiagram? RenderedDiagramOf(CodeBlock code) =>
        code is FencedCodeBlock fenced
        && MermaidBlocks.IsMermaid(fenced.Info)
        && _diagrams.TryGetValue(MermaidBlocks.SourceOf(code), out var diagram)
            ? diagram
            : null;
}
