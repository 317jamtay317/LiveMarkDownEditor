using Markdig.Extensions.DefinitionLists;
using Markdig.Parsers;
using Markdig.Syntax;

namespace Infrastructure.Markdown;

/// <summary>
/// A <see cref="DefinitionListParser"/> that reads a Definition Description from its marker followed
/// by **at least one** space (`: text`) rather than Markdig's stricter three (`:   text`), per INV-066.
/// </summary>
/// <remarks>
/// <para>
/// Markdig measures the marker and the whitespace after it as a single <c>delta</c> and demands
/// <c>delta &gt;= 4</c> — the marker plus three spaces — which rejects the `: text` form that PHP
/// Markdown Extra accepts and that authors actually write. This parser lowers that threshold to
/// <c>delta &gt;= 2</c>, the marker plus one space, and changes nothing else.
/// </para>
/// <para>
/// Requiring that one space is what keeps the leniency safe: a marker followed immediately by a
/// non-space is still ordinary paragraph text, so a table's <c>:---:</c> alignment row, a <c>~~~</c>
/// code fence, and a <c>:shortcode:</c> can never be mistaken for a Definition.
/// </para>
/// <para>
/// Reading leniently does not change what is written back — Capture always emits the canonical
/// marker and three spaces (INV-066), so a leniently-authored Definition List normalises on its
/// first Round-Trip.
/// </para>
/// <para>
/// The block-building body below is derived from Markdig's <see cref="DefinitionListParser"/>
/// (Copyright (c) Alexandre Mutel, BSD-Clause 2 licence) because the threshold it changes sits in
/// the middle of that logic and the type exposes no hook for it.
/// </para>
/// </remarks>
public sealed class LenientDefinitionListParser : DefinitionListParser
{
    /// <summary>The marker plus one space — the least this parser accepts as a Definition Description.</summary>
    private const int MinimumMarkerWidth = 2;

    /// <summary>The marker plus three spaces — the canonical width, past which the rest is content indent.</summary>
    private const int CanonicalMarkerWidth = 4;

    /// <summary>Attempts to open a Definition List at the parser's current position.</summary>
    /// <param name="processor">The block processor positioned at a candidate marker.</param>
    /// <returns>
    /// <see cref="BlockState.Continue"/> when a Definition Item was opened, otherwise
    /// <see cref="BlockState.None"/>.
    /// </returns>
    public override BlockState TryOpen(BlockProcessor processor)
    {
        var paragraphBlock = processor.LastBlock as ParagraphBlock;
        if (processor.IsCodeIndent || paragraphBlock is null || paragraphBlock.LastLine - processor.LineIndex > 1)
        {
            return BlockState.None;
        }

        var column = processor.ColumnBeforeIndent;
        if (!TryConsumeMarker(processor, column))
        {
            return BlockState.None;
        }

        var previousParent = paragraphBlock.Parent!;
        var currentDefinitionList = GetCurrentDefinitionList(paragraphBlock, previousParent);

        processor.Discard(paragraphBlock);

        // If the paragraph was not among the opened blocks, take it off its parent by hand.
        paragraphBlock.Parent?.Remove(paragraphBlock);

        if (currentDefinitionList is null)
        {
            currentDefinitionList = new DefinitionList(this)
            {
                Span = new SourceSpan(paragraphBlock.Span.Start, processor.Line.End),
                Column = paragraphBlock.Column,
                Line = paragraphBlock.Line,
            };
            previousParent.Add(currentDefinitionList);
        }

        var definitionItem = new DefinitionItem(this)
        {
            Line = processor.LineIndex,
            Column = column,
            Span = new SourceSpan(paragraphBlock.Span.Start, processor.Line.End),
            OpeningCharacter = processor.CurrentChar,
        };

        for (var i = 0; i < paragraphBlock.Lines.Count; i++)
        {
            var line = paragraphBlock.Lines.Lines[i];
            var term = new DefinitionTerm(this)
            {
                Column = paragraphBlock.Column,
                Line = line.Line,
                Span = new SourceSpan(paragraphBlock.Span.Start, paragraphBlock.Span.End),
                IsOpen = false,
            };
            term.AppendLine(ref line.Slice, line.Column, line.Line, line.Position, processor.TrackTrivia);
            definitionItem.Add(term);
        }

        currentDefinitionList.Add(definitionItem);
        processor.Open(definitionItem);
        currentDefinitionList.UpdateSpanEnd(processor.Line.End);

        return BlockState.Continue;
    }

    /// <summary>Attempts to continue the open Definition Item, or to start the next one.</summary>
    /// <param name="processor">The block processor positioned at the start of the line's content.</param>
    /// <param name="block">The open <see cref="DefinitionItem"/>.</param>
    /// <returns>The state describing whether the Item continues, breaks, or ends the list.</returns>
    public override BlockState TryContinue(BlockProcessor processor, Block block)
    {
        var definitionItem = (DefinitionItem)block;
        if (processor.IsCodeIndent)
        {
            processor.GoToCodeIndent();
            return BlockState.Continue;
        }

        var list = (DefinitionList)definitionItem.Parent!;
        var lastBlankLine = definitionItem.LastChild as BlankLineBlock;

        // A second marker starts the next Definition Item of the same list.
        if (Array.IndexOf(OpeningCharacters!, processor.CurrentChar) >= 0)
        {
            var startPosition = processor.Start;
            var column = processor.ColumnBeforeIndent;

            if (!TryConsumeMarker(processor, column))
            {
                return EndList(definitionItem, list, lastBlankLine, BlockState.None);
            }

            processor.Close(definitionItem);
            var nextDefinitionItem = new DefinitionItem(this)
            {
                Span = new SourceSpan(startPosition, processor.Line.End),
                Line = processor.LineIndex,
                Column = processor.Column,
                OpeningCharacter = processor.CurrentChar,
            };
            list.Add(nextDefinitionItem);
            processor.Open(nextDefinitionItem);

            return BlockState.Continue;
        }

        var isBreakable = definitionItem.LastChild?.IsBreakable ?? true;
        if (processor.IsBlankLine)
        {
            if (lastBlankLine is null && isBreakable)
            {
                definitionItem.Add(new BlankLineBlock());
            }

            return isBreakable ? BlockState.ContinueDiscard : BlockState.Continue;
        }

        if (lastBlankLine is null && definitionItem.LastChild is ParagraphBlock)
        {
            return BlockState.Continue;
        }

        return EndList(definitionItem, list, lastBlankLine, BlockState.Break);
    }

    /// <summary>
    /// Consumes a Definition Description marker and the whitespace after it, leaving the processor on
    /// the Description's first content column.
    /// </summary>
    /// <param name="processor">The block processor positioned at the marker.</param>
    /// <param name="column">The column the marker starts at, restored when this is not a marker.</param>
    /// <returns><see langword="true"/> when a marker and at least one space were consumed.</returns>
    private static bool TryConsumeMarker(BlockProcessor processor, int column)
    {
        processor.NextChar();
        processor.ParseIndent();
        var delta = processor.Column - column;

        if (delta < MinimumMarkerWidth)
        {
            processor.GoToColumn(column);
            return false;
        }

        // Anything past the canonical width is the Description's own content indent, not the marker.
        if (delta > CanonicalMarkerWidth)
        {
            processor.GoToColumn(column + CanonicalMarkerWidth);
        }

        return true;
    }

    private static BlockState EndList(
        DefinitionItem definitionItem,
        DefinitionList list,
        BlankLineBlock? lastBlankLine,
        BlockState state)
    {
        // Drop the trailing blank line before breaking, so it never becomes part of the Item.
        if (lastBlankLine is not null)
        {
            definitionItem.RemoveAt(definitionItem.Count - 1);
        }

        list.Span.End = list.LastChild!.Span.End;
        return state;
    }

    private static DefinitionList? GetCurrentDefinitionList(
        ParagraphBlock paragraphBlock,
        ContainerBlock previousParent)
    {
        var index = previousParent.IndexOf(paragraphBlock) - 1;
        if (index < 0)
        {
            return null;
        }

        switch (previousParent[index])
        {
            case DefinitionList definitionList:
                return definitionList;

            case BlankLineBlock:
                if (index > 0 && previousParent[index - 1] is DefinitionList precedingList)
                {
                    previousParent.RemoveAt(index);
                    return precedingList;
                }

                break;
        }

        return null;
    }
}
