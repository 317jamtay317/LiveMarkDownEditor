using Domain;
using Shouldly;
using Xunit;

namespace Domain.Tests;

/// <summary>
/// Tests for the Entity Relationship Diagram <see cref="DiagramKind"/> — its canonical Mermaid
/// emission, its parse, and the cardinalities its Edge Set adds (INV-051/INV-052).
/// </summary>
public sealed class DiagramGraphEntityRelationshipTests
{
    // Two entities joined by a one-to-many relationship — the shape an ER diagram usually takes.
    private static DiagramGraph SampleSchema()
    {
        var graph = DiagramGraph.Empty(DiagramKind.EntityRelationshipDiagram, FlowDirection.TopDown)
            .AddNode("Customer", NodeShape.Rectangle)
            .AddNode("Order", NodeShape.Rectangle);

        return graph.Connect(graph.Nodes[0].Id, graph.Nodes[1].Id, "places", EdgeKind.OneToMany);
    }

    [Fact]
    public void ToMermaidSource_ProducesACanonicalErDiagram_INV051()
    {
        SampleSchema().ToMermaidSource().ShouldBe(
            "erDiagram\n" +
            "    n1[\"Customer\"]\n" +
            "    n2[\"Order\"]\n" +
            "    n1 ||--o{ n2 : \"places\"");
    }

    [Fact]
    public void ToMermaidSource_ThenParse_YieldsAnEqualGraph_INV051()
    {
        var original = SampleSchema();

        var parsed = DiagramGraph.Parse(original.ToMermaidSource());

        parsed.Kind.ShouldBe(DiagramKind.EntityRelationshipDiagram);
        parsed.Nodes.ShouldBe(original.Nodes);
        parsed.Edges.ShouldBe(original.Edges);
    }

    [Fact]
    public void Parse_ThenReEmit_IsAFixedPoint_INV051()
    {
        var source = SampleSchema().ToMermaidSource();

        DiagramGraph.Parse(source).ToMermaidSource().ShouldBe(source);
    }

    [Theory]
    [InlineData(EdgeKind.OneToOne, "||--||")]
    [InlineData(EdgeKind.OneToMany, "||--o{")]
    [InlineData(EdgeKind.ManyToOne, "}o--||")]
    [InlineData(EdgeKind.ManyToMany, "}o--o{")]
    public void EveryCardinality_RoundTrips_INV051(EdgeKind kind, string expectedOperator)
    {
        var graph = DiagramGraph.Empty(DiagramKind.EntityRelationshipDiagram, FlowDirection.TopDown)
            .AddNode("A", NodeShape.Rectangle)
            .AddNode("B", NodeShape.Rectangle);
        graph = graph.Connect(graph.Nodes[0].Id, graph.Nodes[1].Id, label: null, kind);

        graph.ToMermaidSource().ShouldContain($"n1 {expectedOperator} n2");
        DiagramGraph.Parse(graph.ToMermaidSource()).Edges[0].Kind.ShouldBe(kind);
    }

    [Fact]
    public void ARelationshipWithNoLabel_StillCarriesTheEmptyLabelMermaidDemands_INV051()
    {
        var graph = DiagramGraph.Empty(DiagramKind.EntityRelationshipDiagram, FlowDirection.TopDown)
            .AddNode("A", NodeShape.Rectangle)
            .AddNode("B", NodeShape.Rectangle);
        graph = graph.Connect(graph.Nodes[0].Id, graph.Nodes[1].Id, label: null, EdgeKind.OneToMany);

        graph.ToMermaidSource().ShouldEndWith("n1 ||--o{ n2 : \"\"");
        DiagramGraph.Parse(graph.ToMermaidSource()).Edges[0].Label.ShouldBeNull();
    }

    [Fact]
    public void AnEntityWithNoLabel_IsDeclaredBare_BecauseMermaidRejectsAnEmptyOne_INV051()
    {
        var graph = DiagramGraph.Empty(DiagramKind.EntityRelationshipDiagram, FlowDirection.TopDown)
            .AddNode(string.Empty, NodeShape.Rectangle);

        graph.ToMermaidSource().ShouldBe("erDiagram\n    n1");
        DiagramGraph.Parse(graph.ToMermaidSource()).Nodes[0].Label.ShouldBe(string.Empty);
    }

    [Fact]
    public void RoundTrip_PreservesLabelsHoldingQuotes_INV051()
    {
        var graph = DiagramGraph.Empty(DiagramKind.EntityRelationshipDiagram, FlowDirection.TopDown)
            .AddNode("The \"main\" table", NodeShape.Rectangle)
            .AddNode("B", NodeShape.Rectangle);
        graph = graph.Connect(graph.Nodes[0].Id, graph.Nodes[1].Id, "says \"hi\"", EdgeKind.ManyToMany);

        var parsed = DiagramGraph.Parse(graph.ToMermaidSource());

        parsed.Nodes[0].Label.ShouldBe("The \"main\" table");
        parsed.Edges[0].Label.ShouldBe("says \"hi\"");
    }

    [Fact]
    public void Parse_ReadsAHandAuthoredErDiagram_AndMapsItsCardinalitiesOntoTheEdgeSet()
    {
        var parsed = DiagramGraph.Parse(
            "erDiagram\n" +
            "  CUSTOMER ||--o{ ORDER : places\n" +
            "  ORDER ||--|{ LINE_ITEM : contains\n" +
            "  CUSTOMER }|..|{ DELIVERY_ADDRESS : uses");

        parsed.Kind.ShouldBe(DiagramKind.EntityRelationshipDiagram);
        parsed.Nodes.Select(node => node.Id.Value).ShouldBe(["CUSTOMER", "ORDER", "LINE_ITEM", "DELIVERY_ADDRESS"]);
        parsed.Edges[0].Kind.ShouldBe(EdgeKind.OneToMany);
        parsed.Edges[1].Kind.ShouldBe(EdgeKind.OneToMany); // one-or-more reads as many
        parsed.Edges[2].Kind.ShouldBe(EdgeKind.ManyToMany);
        parsed.Edges[0].Label.ShouldBe("places");
    }

    [Fact]
    public void AnErDiagram_CarriesNoFlowDirection_BecauseMermaidReadsOneAsMoreEntities_INV051()
    {
        // `direction LR` inside an erDiagram renders as two extra entity boxes named "direction" and
        // "LR" — Mermaid lays an ER diagram out itself. So the kind carries none, and asking for one
        // is quietly Top-Down rather than corrupting the picture.
        var graph = DiagramGraph.Empty(DiagramKind.EntityRelationshipDiagram, FlowDirection.LeftRight);

        DiagramKind.EntityRelationshipDiagram.CarriesFlowDirection().ShouldBeFalse();
        graph.Direction.ShouldBe(FlowDirection.TopDown);
        graph.ToMermaidSource().ShouldBe("erDiagram");
        graph.ToMermaidSource().ShouldNotContain("direction");
    }

    [Fact]
    public void Parse_IgnoresAHandAuthoredDirectionStatement_RatherThanDeclaringItAnEntity_INV051()
    {
        var parsed = DiagramGraph.Parse("erDiagram\n    direction LR\n    n1[\"A\"]\n    n2[\"B\"]\n    n1 ||--o{ n2 : \"x\"");

        parsed.Nodes.Select(node => node.Id.Value).ShouldBe(["n1", "n2"]);
        parsed.Direction.ShouldBe(FlowDirection.TopDown);
    }

    [Fact]
    public void EveryOtherKind_DoesCarryAFlowDirection_INV051()
    {
        DiagramKind.Flowchart.CarriesFlowDirection().ShouldBeTrue();
        DiagramKind.StateDiagram.CarriesFlowDirection().ShouldBeTrue();
        DiagramKind.ClassDiagram.CarriesFlowDirection().ShouldBeTrue();
    }

    [Fact]
    public void Parse_ReadsAnEntityLabelFromItsAlias()
    {
        var parsed = DiagramGraph.Parse("erDiagram\n    c[\"Customer account\"]\n    o[\"Order\"]\n    c ||--o{ o : \"\"");

        parsed.Nodes[0].Label.ShouldBe("Customer account");
        parsed.Edges[0].Label.ShouldBeNull();
    }
}
