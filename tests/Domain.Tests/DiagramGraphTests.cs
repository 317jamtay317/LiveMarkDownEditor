using System;
using Domain;
using Shouldly;
using Xunit;

namespace Domain.Tests;

/// <summary>
/// Tests for <see cref="DiagramGraph"/> — the structured node/arrow model of a Mermaid flowchart that
/// the Flowchart Builder edits. Pins its canonical Mermaid emission and parse round-trip (INV-051),
/// its validity guards (INV-052), and its immutable value-object operations.
/// </summary>
public sealed class DiagramGraphTests
{
    // A three-node decision flowchart, built through the aggregate's operations.
    private static DiagramGraph SampleDecision()
    {
        var graph = DiagramGraph.Empty(DiagramKind.Flowchart, FlowDirection.TopDown)
            .AddNode("Start", NodeShape.Stadium)
            .AddNode("Decide", NodeShape.Diamond)
            .AddNode("Ship", NodeShape.Rectangle);

        var start = graph.Nodes[0].Id;
        var decide = graph.Nodes[1].Id;
        var ship = graph.Nodes[2].Id;

        return graph
            .Connect(start, decide, label: null, EdgeKind.Arrow)
            .Connect(decide, ship, label: "yes", EdgeKind.Arrow);
    }

    [Fact]
    public void Empty_HasNoNodesOrEdges()
    {
        var graph = DiagramGraph.Empty(DiagramKind.Flowchart, FlowDirection.LeftRight);

        graph.Nodes.ShouldBeEmpty();
        graph.Edges.ShouldBeEmpty();
        graph.Direction.ShouldBe(FlowDirection.LeftRight);
        graph.Kind.ShouldBe(DiagramKind.Flowchart);
    }

    [Fact]
    public void AddNode_MintsAFreshUniqueId()
    {
        var graph = DiagramGraph.Empty(DiagramKind.Flowchart, FlowDirection.TopDown)
            .AddNode("A", NodeShape.Rectangle)
            .AddNode("B", NodeShape.Rectangle);

        graph.Nodes[0].Id.Value.ShouldBe("n1");
        graph.Nodes[1].Id.Value.ShouldBe("n2");
    }

    [Fact]
    public void ToMermaidSource_ProducesCanonicalFlowchart_INV051()
    {
        var source = SampleDecision().ToMermaidSource();

        source.ShouldBe(
            "flowchart TD\n" +
            "    n1([\"Start\"])\n" +
            "    n2{\"Decide\"}\n" +
            "    n3[\"Ship\"]\n" +
            "    n1 --> n2\n" +
            "    n2 -->|yes| n3");
    }

    [Fact]
    public void ToMermaidSource_ThenParse_YieldsAnEqualGraph_INV051()
    {
        var original = SampleDecision();

        var parsed = DiagramGraph.Parse(original.ToMermaidSource());

        parsed.Direction.ShouldBe(original.Direction);
        parsed.Nodes.ShouldBe(original.Nodes);
        parsed.Edges.ShouldBe(original.Edges);
    }

    [Fact]
    public void Parse_ThenReEmit_IsAFixedPoint_INV051()
    {
        var source = SampleDecision().ToMermaidSource();

        var reEmitted = DiagramGraph.Parse(source).ToMermaidSource();

        reEmitted.ShouldBe(source);
    }

    [Fact]
    public void RoundTrip_PreservesNodeLabelsWithSpacesAndPunctuation_INV051()
    {
        var graph = DiagramGraph.Empty(DiagramKind.Flowchart, FlowDirection.TopDown)
            .AddNode("Validate the user's input", NodeShape.Rectangle);

        var parsed = DiagramGraph.Parse(graph.ToMermaidSource());

        parsed.Nodes[0].Label.ShouldBe("Validate the user's input");
    }

    [Theory]
    [InlineData(FlowDirection.TopDown, "flowchart TD")]
    [InlineData(FlowDirection.LeftRight, "flowchart LR")]
    [InlineData(FlowDirection.BottomUp, "flowchart BT")]
    [InlineData(FlowDirection.RightLeft, "flowchart RL")]
    public void ToMermaidSource_EmitsTheDirectionToken_INV051(FlowDirection direction, string expectedHeader)
    {
        var source = DiagramGraph.Empty(DiagramKind.Flowchart, direction).ToMermaidSource();

        source.ShouldBe(expectedHeader);
    }

    [Theory]
    [InlineData(NodeShape.Rectangle, "n1[\"A\"]")]
    [InlineData(NodeShape.Rounded, "n1(\"A\")")]
    [InlineData(NodeShape.Stadium, "n1([\"A\"])")]
    [InlineData(NodeShape.Diamond, "n1{\"A\"}")]
    [InlineData(NodeShape.Circle, "n1((\"A\"))")]
    public void EveryNodeShape_RoundTrips_INV051(NodeShape shape, string expectedDeclaration)
    {
        var graph = DiagramGraph.Empty(DiagramKind.Flowchart, FlowDirection.TopDown).AddNode("A", shape);

        graph.ToMermaidSource().ShouldBe($"flowchart TD\n    {expectedDeclaration}");
        DiagramGraph.Parse(graph.ToMermaidSource()).Nodes[0].Shape.ShouldBe(shape);
    }

    [Theory]
    [InlineData(EdgeKind.Arrow, "-->")]
    [InlineData(EdgeKind.Dotted, "-.->")]
    [InlineData(EdgeKind.Thick, "==>")]
    [InlineData(EdgeKind.Open, "---")]
    public void EveryEdgeKind_RoundTrips_INV051(EdgeKind kind, string expectedOperator)
    {
        var graph = DiagramGraph.Empty(DiagramKind.Flowchart, FlowDirection.TopDown)
            .AddNode("A", NodeShape.Rectangle)
            .AddNode("B", NodeShape.Rectangle);
        graph = graph.Connect(graph.Nodes[0].Id, graph.Nodes[1].Id, label: null, kind);

        graph.ToMermaidSource().ShouldContain($"n1 {expectedOperator} n2");
        DiagramGraph.Parse(graph.ToMermaidSource()).Edges[0].Kind.ShouldBe(kind);
    }

    [Fact]
    public void Parse_ReadsAHandAuthoredFlowchart_WithGraphKeyword_ShapesAndLabels()
    {
        var parsed = DiagramGraph.Parse(
            "graph LR\n" +
            "  A[Start] --> B{Decide}\n" +
            "  B -->|no| A\n" +
            "  B ==> C((Done))");

        parsed.Direction.ShouldBe(FlowDirection.LeftRight);
        parsed.Nodes.Count.ShouldBe(3);
        parsed.Nodes[0].ShouldBe(new DiagramNode(new NodeId("A"), "Start", NodeShape.Rectangle));
        parsed.Nodes[1].ShouldBe(new DiagramNode(new NodeId("B"), "Decide", NodeShape.Diamond));
        parsed.Nodes[2].ShouldBe(new DiagramNode(new NodeId("C"), "Done", NodeShape.Circle));
        parsed.Edges[0].ShouldBe(new DiagramEdge(new NodeId("A"), new NodeId("B"), null, EdgeKind.Arrow));
        parsed.Edges[1].ShouldBe(new DiagramEdge(new NodeId("B"), new NodeId("A"), "no", EdgeKind.Arrow));
        parsed.Edges[2].ShouldBe(new DiagramEdge(new NodeId("B"), new NodeId("C"), null, EdgeKind.Thick));
    }

    [Fact]
    public void Parse_DefaultsDirectionToTopDown_WhenTheHeaderOmitsIt()
    {
        DiagramGraph.Parse("flowchart\n    A[\"A\"]").Direction.ShouldBe(FlowDirection.TopDown);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("sequenceDiagram\n    Alice->>Bob: Hi")]
    [InlineData("Just some prose, not a diagram.")]
    public void TryParse_GivenSomethingThatIsNotAFlowchart_ReturnsFalse(string? source)
    {
        DiagramGraph.TryParse(source, out _).ShouldBeFalse();
    }

    [Fact]
    public void Parse_GivenANonFlowchart_Throws()
    {
        Should.Throw<FormatException>(() => DiagramGraph.Parse("pie title Pets"));
    }

    [Fact]
    public void Create_WithDuplicateNodeIds_Throws_INV052()
    {
        var duplicate = new NodeId("A");

        Should.Throw<ArgumentException>(() => DiagramGraph.Create(
            DiagramKind.Flowchart,
            FlowDirection.TopDown,
            [new DiagramNode(duplicate, "one", NodeShape.Rectangle), new DiagramNode(duplicate, "two", NodeShape.Rectangle)],
            []));
    }

    [Fact]
    public void Create_WithAnEdgeToAnUndeclaredNode_Throws_INV052()
    {
        Should.Throw<ArgumentException>(() => DiagramGraph.Create(
            DiagramKind.Flowchart,
            FlowDirection.TopDown,
            [new DiagramNode(new NodeId("A"), "A", NodeShape.Rectangle)],
            [new DiagramEdge(new NodeId("A"), new NodeId("ghost"), null, EdgeKind.Arrow)]));
    }

    [Fact]
    public void Connect_ToAnUndeclaredNode_Throws_INV052()
    {
        var graph = DiagramGraph.Empty(DiagramKind.Flowchart, FlowDirection.TopDown).AddNode("A", NodeShape.Rectangle);

        Should.Throw<ArgumentException>(() => graph.Connect(graph.Nodes[0].Id, new NodeId("ghost"), null, EdgeKind.Arrow));
    }

    [Fact]
    public void RemoveNode_AlsoRemovesItsIncidentEdges_INV052()
    {
        var graph = SampleDecision();
        var decide = graph.Nodes[1].Id; // the middle node, on both edges

        var pruned = graph.RemoveNode(decide);

        pruned.Nodes.ShouldNotContain(node => node.Id == decide);
        pruned.Edges.ShouldBeEmpty(); // both edges touched Decide
    }

    [Fact]
    public void RemoveEdge_RemovesOnlyThatEdge()
    {
        var graph = SampleDecision();
        var first = graph.Edges[0];

        var pruned = graph.RemoveEdge(first);

        pruned.Edges.ShouldNotContain(first);
        pruned.Edges.Count.ShouldBe(1);
    }

    [Fact]
    public void RenameNode_ChangesOnlyThatNodesLabel()
    {
        var graph = SampleDecision();
        var ship = graph.Nodes[2].Id;

        var renamed = graph.RenameNode(ship, "Deploy");

        renamed.Nodes[2].Label.ShouldBe("Deploy");
        renamed.Nodes[0].Label.ShouldBe("Start"); // unchanged
    }

    [Fact]
    public void SetNodeShape_ChangesOnlyThatNodesShape()
    {
        var graph = SampleDecision();
        var start = graph.Nodes[0].Id;

        graph.SetNodeShape(start, NodeShape.Circle).Nodes[0].Shape.ShouldBe(NodeShape.Circle);
    }

    [Fact]
    public void WithDirection_ChangesTheDirection()
    {
        DiagramGraph.Empty(DiagramKind.Flowchart, FlowDirection.TopDown)
            .WithDirection(FlowDirection.LeftRight)
            .Direction.ShouldBe(FlowDirection.LeftRight);
    }

    [Fact]
    public void Operations_DoNotMutateTheOriginal_INV052()
    {
        var original = DiagramGraph.Empty(DiagramKind.Flowchart, FlowDirection.TopDown).AddNode("A", NodeShape.Rectangle);

        original.AddNode("B", NodeShape.Rectangle);

        original.Nodes.Count.ShouldBe(1); // the second AddNode returned a new graph
    }

    [Fact]
    public void Connect_WithABlankLabel_StoresNoEdgeLabel()
    {
        var graph = DiagramGraph.Empty(DiagramKind.Flowchart, FlowDirection.TopDown)
            .AddNode("A", NodeShape.Rectangle)
            .AddNode("B", NodeShape.Rectangle);

        graph = graph.Connect(graph.Nodes[0].Id, graph.Nodes[1].Id, "   ", EdgeKind.Arrow);

        graph.Edges[0].Label.ShouldBeNull();
        graph.ToMermaidSource().ShouldEndWith("n1 --> n2");
    }
}
