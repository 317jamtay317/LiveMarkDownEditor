using Domain;
using Shouldly;
using Xunit;

namespace Domain.Tests;

/// <summary>
/// Tests for the Class Diagram <see cref="DiagramKind"/> — its canonical Mermaid emission, its parse,
/// and the class relationships its Edge Set adds (INV-051/INV-052).
/// </summary>
public sealed class DiagramGraphClassDiagramTests
{
    // A base class, a subclass, and a collaborator — the shapes a class diagram usually takes.
    private static DiagramGraph SampleModel()
    {
        var graph = DiagramGraph.Empty(DiagramKind.ClassDiagram, FlowDirection.TopDown)
            .AddNode("Animal", NodeShape.Rectangle)
            .AddNode("Duck", NodeShape.Rectangle)
            .AddNode("Pond", NodeShape.Rectangle);

        var animal = graph.Nodes[0].Id;
        var duck = graph.Nodes[1].Id;
        var pond = graph.Nodes[2].Id;

        return graph
            .Connect(animal, duck, label: null, EdgeKind.Inheritance)
            .Connect(pond, duck, "holds", EdgeKind.Composition);
    }

    [Fact]
    public void ToMermaidSource_ProducesACanonicalClassDiagram_INV051()
    {
        SampleModel().ToMermaidSource().ShouldBe(
            "classDiagram\n" +
            "    direction TB\n" +
            "    class n1[\"Animal\"]\n" +
            "    class n2[\"Duck\"]\n" +
            "    class n3[\"Pond\"]\n" +
            "    n1 <|-- n2\n" +
            "    n3 *-- n2 : holds");
    }

    [Fact]
    public void ToMermaidSource_ThenParse_YieldsAnEqualGraph_INV051()
    {
        var original = SampleModel();

        var parsed = DiagramGraph.Parse(original.ToMermaidSource());

        parsed.Kind.ShouldBe(DiagramKind.ClassDiagram);
        parsed.Direction.ShouldBe(original.Direction);
        parsed.Nodes.ShouldBe(original.Nodes);
        parsed.Edges.ShouldBe(original.Edges);
    }

    [Fact]
    public void Parse_ThenReEmit_IsAFixedPoint_INV051()
    {
        var source = SampleModel().ToMermaidSource();

        DiagramGraph.Parse(source).ToMermaidSource().ShouldBe(source);
    }

    [Theory]
    [InlineData(EdgeKind.Arrow, "-->")]
    [InlineData(EdgeKind.Inheritance, "<|--")]
    [InlineData(EdgeKind.Composition, "*--")]
    [InlineData(EdgeKind.Aggregation, "o--")]
    [InlineData(EdgeKind.Dependency, "..>")]
    [InlineData(EdgeKind.Open, "--")]
    public void EveryClassRelationship_RoundTrips_INV051(EdgeKind kind, string expectedOperator)
    {
        var graph = DiagramGraph.Empty(DiagramKind.ClassDiagram, FlowDirection.TopDown)
            .AddNode("A", NodeShape.Rectangle)
            .AddNode("B", NodeShape.Rectangle);
        graph = graph.Connect(graph.Nodes[0].Id, graph.Nodes[1].Id, label: null, kind);

        graph.ToMermaidSource().ShouldContain($"n1 {expectedOperator} n2");
        DiagramGraph.Parse(graph.ToMermaidSource()).Edges[0].Kind.ShouldBe(kind);
    }

    [Fact]
    public void AClassWithNoLabel_IsDeclaredBare_BecauseMermaidRejectsAnEmptyOne_INV051()
    {
        var graph = DiagramGraph.Empty(DiagramKind.ClassDiagram, FlowDirection.TopDown)
            .AddNode(string.Empty, NodeShape.Rectangle);

        graph.ToMermaidSource().ShouldBe("classDiagram\n    direction TB\n    class n1");
        DiagramGraph.Parse(graph.ToMermaidSource()).Nodes[0].Label.ShouldBe(string.Empty);
    }

    [Fact]
    public void AnEmptyClassDiagram_StillCarriesItsDirection_SoMermaidCanParseIt_INV051()
    {
        DiagramGraph.Empty(DiagramKind.ClassDiagram, FlowDirection.LeftRight).ToMermaidSource()
            .ShouldBe("classDiagram\n    direction LR");
    }

    [Fact]
    public void RoundTrip_PreservesLabelsHoldingQuotesColonsAndSemicolons_INV051()
    {
        var graph = DiagramGraph.Empty(DiagramKind.ClassDiagram, FlowDirection.TopDown)
            .AddNode("The \"main\" one", NodeShape.Rectangle)
            .AddNode("B", NodeShape.Rectangle);
        graph = graph.Connect(graph.Nodes[0].Id, graph.Nodes[1].Id, "owns: many; sometimes", EdgeKind.Arrow);

        var parsed = DiagramGraph.Parse(graph.ToMermaidSource());

        parsed.Nodes[0].Label.ShouldBe("The \"main\" one");
        parsed.Edges[0].Label.ShouldBe("owns: many; sometimes");
    }

    [Fact]
    public void Parse_ReadsAHandAuthoredClassDiagram()
    {
        var parsed = DiagramGraph.Parse(
            "classDiagram\n" +
            "  direction LR\n" +
            "  class Animal\n" +
            "  Animal <|-- Duck\n" +
            "  Duck ..> Pond : visits");

        parsed.Direction.ShouldBe(FlowDirection.LeftRight);
        parsed.Nodes.Select(node => node.Id.Value).ShouldBe(["Animal", "Duck", "Pond"]);
        parsed.Nodes[0].Label.ShouldBe(string.Empty);
        parsed.Edges[0].Kind.ShouldBe(EdgeKind.Inheritance);
        parsed.Edges[1].Kind.ShouldBe(EdgeKind.Dependency);
        parsed.Edges[1].Label.ShouldBe("visits");
    }
}
