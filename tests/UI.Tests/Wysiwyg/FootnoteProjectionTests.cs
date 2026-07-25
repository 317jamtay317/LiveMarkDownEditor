using System.Linq;
using System.Windows;
using System.Windows.Documents;
using Shouldly;
using UI.Wysiwyg;
using Xunit;

namespace UI.Tests.Wysiwyg;

/// <summary>
/// Tests for the projection and Capture of a Footnote: a Footnote Reference standing in the prose as a
/// superscript Footnote Number, and its Footnote Definition shown in the Footnote Section at the end of
/// the Visual Document but Captured at the position it was authored (INV-065).
/// </summary>
public sealed class FootnoteProjectionTests
{
    private static readonly MarkdownToFlowDocumentProjector Projector = new();
    private static readonly FlowDocumentToMarkdownCapturer Capturer = new();

    [Fact]
    public void Project_GivenFootnoteReference_ShowsASuperscriptFootnoteNumber_INV065()
    {
        StaThread.Run(() =>
        {
            var document = Projector.Project("A claim.[^a]\n\n[^a]: the note");

            var reference = Runs(document.Blocks.OfType<Paragraph>().First())
                .Single(run => run.Tag is FootnoteReferenceRole);

            reference.Text.ShouldBe("1");
            reference.BaselineAlignment.ShouldBe(BaselineAlignment.Superscript);
            ((FootnoteReferenceRole)reference.Tag).Label.ShouldBe("a");
        });
    }

    [Fact]
    public void Project_NumbersFootnotesByTheOrderTheirReferencesAppear_INV065()
    {
        StaThread.Run(() =>
        {
            // Authored the other way round: the definition for ^b comes first in the source.
            var document = Projector.Project("First.[^a] Second.[^b]\n\n[^b]: note b\n\n[^a]: note a");

            var numbers = Runs(document.Blocks.OfType<Paragraph>().First())
                .Where(run => run.Tag is FootnoteReferenceRole)
                .Select(run => (run.Text, ((FootnoteReferenceRole)run.Tag!).Label))
                .ToList();

            numbers.ShouldBe([("1", "a"), ("2", "b")]);
        });
    }

    [Fact]
    public void Project_GivenTwoReferencesSharingALabel_SharesTheirNumber_INV065()
    {
        StaThread.Run(() =>
        {
            var document = Projector.Project("Here[^a] and here[^a].\n\n[^a]: the note");

            Runs(document.Blocks.OfType<Paragraph>().First())
                .Where(run => run.Tag is FootnoteReferenceRole)
                .Select(run => run.Text)
                .ShouldBe(["1", "1"]);
        });
    }

    [Fact]
    public void Project_PutsEveryDefinitionInTheFootnoteSectionAtTheEnd_INV065()
    {
        StaThread.Run(() =>
        {
            var document = Projector.Project("A claim.[^a]\n\n[^a]: the note\n\nMore prose.");

            var section = document.Blocks.OfType<Section>()
                .Single(block => block.Tag is BlockSemantic.FootnoteSection);

            document.Blocks.LastBlock.ShouldBe(section);
            var definitions = section.Blocks.OfType<Section>().ToList();
            definitions.Count.ShouldBe(1);
            ((FootnoteDefinitionRole)definitions[0].Tag!).Label.ShouldBe("a");
            new TextRange(definitions[0].ContentStart, definitions[0].ContentEnd).Text.ShouldContain("the note");
        });
    }

    [Fact]
    public void Project_GivenNoFootnotes_ComposesNoFootnoteSection_INV065()
    {
        StaThread.Run(() =>
        {
            var document = Projector.Project("Just prose, and a [^missing] that matches nothing.");

            document.Blocks.OfType<Section>()
                .Any(block => block.Tag is BlockSemantic.FootnoteSection)
                .ShouldBeFalse();
        });
    }

    [Fact]
    public void Project_OmitsTheRenderedBackReference_INV065()
    {
        StaThread.Run(() =>
        {
            // Parsing a Footnote appends a back-link to the last block of its Definition. It belongs to
            // the Rendered Output, not to the document.
            var document = Projector.Project("A claim.[^a]\n\n[^a]: the note");

            var section = document.Blocks.OfType<Section>()
                .Single(block => block.Tag is BlockSemantic.FootnoteSection);

            new TextRange(section.ContentStart, section.ContentEnd).Text.ShouldNotContain("↩");
        });
    }

    [Fact]
    public void Project_GivenAReferenceWithNoDefinition_ShowsTheLiteralText_INV065()
    {
        StaThread.Run(() =>
        {
            var document = Projector.Project("A claim.[^missing]");

            var paragraph = document.Blocks.OfType<Paragraph>().Single();

            new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text.ShouldBe("A claim.[^missing]");
            Runs(paragraph).Any(run => run.Tag is FootnoteReferenceRole).ShouldBeFalse();
        });
    }

    [Fact]
    public void Project_GivenAnUnreferencedDefinition_KeepsItUnnumbered_INV065()
    {
        StaThread.Run(() =>
        {
            // Markdown itself omits an unreferenced Definition from the Rendered Output. Dropping it
            // here would delete the author's prose the moment they removed a Reference.
            var document = Projector.Project("Prose.\n\n[^unused]: nobody cites this");

            var section = document.Blocks.OfType<Section>()
                .Single(block => block.Tag is BlockSemantic.FootnoteSection);

            var definition = section.Blocks.OfType<Section>().Single();
            ((FootnoteDefinitionRole)definition.Tag!).Label.ShouldBe("unused");
            var text = new TextRange(definition.ContentStart, definition.ContentEnd).Text;
            text.ShouldContain("nobody cites this");
            text.ShouldNotContain("1.");
        });
    }

    [Fact]
    public void Capture_WritesTheDefinitionWhereItWasAuthored_INV065()
    {
        StaThread.Run(() =>
        {
            const string Markdown = "A claim.[^a]\n\n[^a]: the note\n\nMore prose.";

            Capturer.Capture(Projector.Project(Markdown)).ShouldBe(Markdown);
        });
    }

    [Fact]
    public void Capture_GivenADefinitionAuthoredBeforeAnyBlock_WritesItFirst_INV065()
    {
        StaThread.Run(() =>
        {
            const string Markdown = "[^a]: the note\n\nA claim.[^a]";

            Capturer.Capture(Projector.Project(Markdown)).ShouldBe(Markdown);
        });
    }

    [Fact]
    public void Capture_GivenTheAnchorBlockDeleted_KeepsTheDefinitionAtTheEnd_INV065()
    {
        StaThread.Run(() =>
        {
            var document = Projector.Project("Middle prose.\n\n[^a]: the note\n\nA claim.[^a]");

            // The user deletes the block the Definition was authored after.
            var anchor = document.Blocks.OfType<Paragraph>()
                .First(paragraph => new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text
                    .Contains("Middle prose."));
            document.Blocks.Remove(anchor);

            Capturer.Capture(document).ShouldBe("A claim.[^a]\n\n[^a]: the note");
        });
    }

    [Fact]
    public void Capture_KeepsTheAuthorsLabel_RatherThanTheNumberShown_INV065()
    {
        StaThread.Run(() =>
        {
            const string Markdown = "A claim.[^alpha]\n\n[^alpha]: the note";

            Capturer.Capture(Projector.Project(Markdown)).ShouldBe(Markdown);
        });
    }

    [Fact]
    public void Capture_NeverWritesTheFootnoteSectionAsABlock_INV065()
    {
        StaThread.Run(() =>
        {
            var captured = Capturer.Capture(Projector.Project("A claim.[^a]\n\n[^a]: the note"));

            // The note is written once, as its Definition — not a second time as the Section's content.
            captured.Split("the note").Length.ShouldBe(2);
            captured.ShouldNotContain(">");
        });
    }

    [Fact]
    public void Capture_GivenAnEditedDefinition_WritesTheEdit_INV065()
    {
        StaThread.Run(() =>
        {
            var document = Projector.Project("A claim.[^a]\n\n[^a]: the note");
            var section = document.Blocks.OfType<Section>()
                .Single(block => block.Tag is BlockSemantic.FootnoteSection);
            var run = Runs(section.Blocks.OfType<Section>().Single().Blocks.OfType<Paragraph>().First())
                .Last(candidate => candidate.Text.Contains("the note"));

            run.Text = "the edited note";

            Capturer.Capture(document).ShouldBe("A claim.[^a]\n\n[^a]: the edited note");
        });
    }

    private static List<Run> Runs(Paragraph paragraph) => Flatten(paragraph.Inlines).ToList();

    private static IEnumerable<Run> Flatten(InlineCollection inlines)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case Run run:
                    yield return run;
                    break;
                case Span span:
                    foreach (var nested in Flatten(span.Inlines))
                    {
                        yield return nested;
                    }

                    break;
            }
        }
    }
}
