using System.Text;
using Infrastructure.Markdown;
using Infrastructure.Pdf;
using MigraDoc.DocumentObjectModel;
using Shouldly;
using Xunit;

namespace Infrastructure.Tests;

/// <summary>
/// Tests that a printout and an Export as PDF carry a Footnote (INV-065) and a Definition List
/// (INV-066) the way the Rendered Output does: a superscript Footnote Number in the prose with the
/// notes gathered behind a rule at the end, and each Definition Term flush with its Definition
/// Descriptions indented beneath it.
/// </summary>
public sealed class MarkdownPdfComposerFootnoteTests
{
    private static Document Compose(string markdown)
    {
        var ast = Markdig.Markdown.Parse(markdown, GfmPipeline.Create());
        return new MarkdownPdfComposer(diagrams: null).Compose(ast);
    }

    [Fact]
    public void Compose_GivenAFootnote_WritesTheNumberAndTheNote_INV065()
    {
        var document = Compose("A claim.[^a]\n\n[^a]: the note");

        var text = SectionText(document);
        text.ShouldContain("A claim.1");
        text.ShouldContain("the note");
    }

    [Fact]
    public void Compose_WritesTheFootnoteReferenceAsASuperscript_INV065()
    {
        var document = Compose("A claim.[^a]\n\n[^a]: the note");

        Superscripts(document).ShouldContain("1");
    }

    [Fact]
    public void Compose_NumbersTheNotesInReferenceOrder_INV065()
    {
        // Authored the other way round: the definition for ^b comes first in the source.
        var document = Compose("First.[^a] Second.[^b]\n\n[^b]: note b\n\n[^a]: note a");

        var text = SectionText(document);
        text.IndexOf("note a", StringComparison.Ordinal)
            .ShouldBeLessThan(text.IndexOf("note b", StringComparison.Ordinal));
        text.ShouldContain("1. note a");
        text.ShouldContain("2. note b");
    }

    [Fact]
    public void Compose_SetsTheNotesOffBehindARule_INV065()
    {
        var document = Compose("A claim.[^a]\n\n[^a]: the note");

        // The rule the Rendered Output draws above its notes section.
        document.LastSection.Elements.OfType<Paragraph>()
            .Any(paragraph => paragraph.Format.Borders.Bottom.Width.Point > 0)
            .ShouldBeTrue();
    }

    [Fact]
    public void Compose_OmitsTheRenderedBackReference_INV065()
    {
        var document = Compose("A claim.[^a]\n\n[^a]: the note");

        SectionText(document).ShouldNotContain("↩");
    }

    [Fact]
    public void Compose_GivenNoFootnotes_WritesNoNotesSection_INV065()
    {
        var document = Compose("Just prose.");

        document.LastSection.Elements.OfType<Paragraph>()
            .Any(paragraph => paragraph.Format.Borders.Bottom.Width.Point > 0)
            .ShouldBeFalse();
    }

    [Fact]
    public void Compose_GivenADefinitionList_IndentsTheDescriptionUnderItsTerm_INV066()
    {
        var document = Compose("Markdown\n:   A markup language.");

        var paragraphs = document.LastSection.Elements.OfType<Paragraph>().ToList();
        var term = paragraphs.Single(paragraph => TextOf(paragraph).Contains("Markdown"));
        var description = paragraphs.Single(paragraph => TextOf(paragraph).Contains("A markup language."));

        description.Format.LeftIndent.Point.ShouldBeGreaterThan(term.Format.LeftIndent.Point);
    }

    [Fact]
    public void Compose_GivenADefinitionListWithTwoTerms_WritesBothFlush_INV066()
    {
        var document = Compose("Term one\nTerm two\n:   Shared description.");

        var paragraphs = document.LastSection.Elements.OfType<Paragraph>().ToList();
        var terms = paragraphs
            .Where(paragraph => TextOf(paragraph).StartsWith("Term ", StringComparison.Ordinal))
            .ToList();

        terms.Count.ShouldBe(2);
        terms.ShouldAllBe(paragraph => paragraph.Format.LeftIndent.Point == 0);
    }

    private static List<string> Superscripts(Document document)
    {
        var found = new List<string>();
        foreach (var paragraph in document.LastSection.Elements.OfType<Paragraph>())
        {
            CollectSuperscripts(paragraph.Elements, superscript: false, found);
        }

        return found;
    }

    private static void CollectSuperscripts(ParagraphElements elements, bool superscript, List<string> found)
    {
        foreach (var element in elements)
        {
            switch (element)
            {
                case Text run when superscript:
                    found.Add(run.Content);
                    break;
                case FormattedText formatted:
                    CollectSuperscripts(
                        formatted.Elements,
                        superscript || formatted.Font.Superscript,
                        found);
                    break;
            }
        }
    }

    private static string SectionText(Document document)
    {
        var text = new StringBuilder();
        foreach (var paragraph in document.LastSection.Elements.OfType<Paragraph>())
        {
            text.Append(TextOf(paragraph));
        }

        return text.ToString();
    }

    private static string TextOf(Paragraph paragraph)
    {
        var text = new StringBuilder();
        CollectText(paragraph.Elements, text);
        return text.ToString();
    }

    private static void CollectText(ParagraphElements elements, StringBuilder text)
    {
        foreach (var element in elements)
        {
            switch (element)
            {
                case Text run:
                    text.Append(run.Content);
                    break;
                case FormattedText formatted:
                    CollectText(formatted.Elements, text);
                    break;
            }
        }
    }
}
