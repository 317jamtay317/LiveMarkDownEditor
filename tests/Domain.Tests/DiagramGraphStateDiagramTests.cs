using Domain;
using Shouldly;
using Xunit;

namespace Domain.Tests;

/// <summary>
/// Tests for the State Diagram <see cref="DiagramKind"/> — its canonical Mermaid emission, its parse,
/// and the Terminal (Mermaid's <c>[*]</c> start/end marker) that carries no Node Id of its own
/// (INV-051/INV-052).
/// </summary>
public sealed class DiagramGraphStateDiagramTests
{
    // A start marker flowing into two states and out to an end marker — the shape a state machine
    // usually takes.
    private static DiagramGraph SampleMachine()
    {
        var graph = DiagramGraph.Empty(DiagramKind.StateDiagram, FlowDirection.TopDown)
            .AddNode(string.Empty, NodeShape.Terminal)
            .AddNode("Idle", NodeShape.Rounded)
            .AddNode("Running", NodeShape.Rounded)
            .AddNode(string.Empty, NodeShape.Terminal);

        var start = graph.Nodes[0].Id;
        var idle = graph.Nodes[1].Id;
        var running = graph.Nodes[2].Id;
        var stop = graph.Nodes[3].Id;

        return graph
            .Connect(start, idle, label: null, EdgeKind.Arrow)
            .Connect(idle, running, "start", EdgeKind.Arrow)
            .Connect(running, stop, "done", EdgeKind.Arrow);
    }

    [Fact]
    public void ToMermaidSource_ProducesACanonicalStateDiagram_INV051()
    {
        SampleMachine().ToMermaidSource().ShouldBe(
            "stateDiagram-v2\n" +
            "    direction TB\n" +
            "    n2 : Idle\n" +
            "    n3 : Running\n" +
            "    [*] --> n2\n" +
            "    n2 --> n3 : start\n" +
            "    n3 --> [*] : done");
    }

    [Fact]
    public void Parse_ReadsTheKindBackFromTheHeader_INV051()
    {
        DiagramGraph.Parse(SampleMachine().ToMermaidSource()).Kind.ShouldBe(DiagramKind.StateDiagram);
    }

    [Fact]
    public void Parse_ThenReEmit_IsAFixedPoint_INV051()
    {
        var source = SampleMachine().ToMermaidSource();

        DiagramGraph.Parse(source).ToMermaidSource().ShouldBe(source);
    }

    [Fact]
    public void RoundTrip_PreservesEveryStateAndTransition_INV051()
    {
        var parsed = DiagramGraph.Parse(SampleMachine().ToMermaidSource());

        parsed.Nodes.Count.ShouldBe(4);
        parsed.Nodes.Count(node => node.Shape == NodeShape.Terminal).ShouldBe(2);
        parsed.Nodes[0].Label.ShouldBe("Idle");
        parsed.Nodes[1].Label.ShouldBe("Running");
        parsed.Edges.Count.ShouldBe(3);
        parsed.Edges[1].Label.ShouldBe("start");
    }

    [Fact]
    public void RoundTrip_MintsAFreshNodeIdForATerminal_BecauseTheSourceNamesNone_INV051()
    {
        var original = SampleMachine();

        var parsed = DiagramGraph.Parse(original.ToMermaidSource());

        // The States keep their Node Ids verbatim; a Terminal, written [*], is renamed and moves to
        // the end — the source never named it, so there is nothing to recover.
        parsed.Nodes.Select(node => node.Id.Value).ShouldContain("n2");
        parsed.Nodes.Where(node => node.Shape == NodeShape.Terminal)
            .ShouldAllBe(node => node.Id.Value != "n1" && node.Id.Value != "n4");
        original.Nodes.Count(node => node.Shape == NodeShape.Terminal).ShouldBe(2);
    }

    [Fact]
    public void EachTerminalOccurrence_IsItsOwnDiagramNode_INV051()
    {
        var parsed = DiagramGraph.Parse(
            "stateDiagram-v2\n    direction TB\n    n1 : Only\n    [*] --> n1\n    n1 --> [*]");

        // Mermaid draws a start circle and an end circle; the graph says the same.
        parsed.Nodes.Count(node => node.Shape == NodeShape.Terminal).ShouldBe(2);
    }

    [Fact]
    public void AnUnconnectedTerminal_IsDeclaredOnItsOwnLine_SoItSurvivesARoundTrip_INV051()
    {
        var graph = DiagramGraph.Empty(DiagramKind.StateDiagram, FlowDirection.TopDown)
            .AddNode(string.Empty, NodeShape.Terminal);

        graph.ToMermaidSource().ShouldBe("stateDiagram-v2\n    direction TB\n    [*]");
        DiagramGraph.Parse(graph.ToMermaidSource()).Nodes.Count.ShouldBe(1);
    }

    [Fact]
    public void AStateWithNoLabel_IsDeclaredBare_INV051()
    {
        var graph = DiagramGraph.Empty(DiagramKind.StateDiagram, FlowDirection.TopDown)
            .AddNode(string.Empty, NodeShape.Rounded);

        graph.ToMermaidSource().ShouldBe("stateDiagram-v2\n    direction TB\n    n1");
        DiagramGraph.Parse(graph.ToMermaidSource()).Nodes[0].Label.ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData(FlowDirection.TopDown, "TB")]
    [InlineData(FlowDirection.LeftRight, "LR")]
    [InlineData(FlowDirection.BottomUp, "BT")]
    [InlineData(FlowDirection.RightLeft, "RL")]
    public void TheFlowDirection_IsADirectionStatement_NotAHeaderToken_INV051(
        FlowDirection direction, string token)
    {
        var graph = DiagramGraph.Empty(DiagramKind.StateDiagram, direction);

        graph.ToMermaidSource().ShouldBe($"stateDiagram-v2\n    direction {token}");
        DiagramGraph.Parse(graph.ToMermaidSource()).Direction.ShouldBe(direction);
    }

    [Fact]
    public void RoundTrip_PreservesALabelHoldingAColon_INV051()
    {
        var graph = DiagramGraph.Empty(DiagramKind.StateDiagram, FlowDirection.TopDown)
            .AddNode("Waiting: on input", NodeShape.Rounded)
            .AddNode("Done", NodeShape.Rounded);
        graph = graph.Connect(graph.Nodes[0].Id, graph.Nodes[1].Id, "finished: ok", EdgeKind.Arrow);

        var parsed = DiagramGraph.Parse(graph.ToMermaidSource());

        // A colon separates a state from its label, so one inside a label is written as its entity code.
        graph.ToMermaidSource().ShouldNotContain(": on input");
        parsed.Nodes[0].Label.ShouldBe("Waiting: on input");
        parsed.Edges[0].Label.ShouldBe("finished: ok");
    }

    [Fact]
    public void Parse_ReadsAHandAuthoredStateDiagram()
    {
        var parsed = DiagramGraph.Parse(
            "stateDiagram-v2\n" +
            "  direction LR\n" +
            "  [*] --> Still\n" +
            "  Still --> Moving : push\n" +
            "  Moving --> [*]");

        parsed.Kind.ShouldBe(DiagramKind.StateDiagram);
        parsed.Direction.ShouldBe(FlowDirection.LeftRight);
        parsed.Nodes.Select(node => node.Label).ShouldContain(string.Empty);
        parsed.Nodes.Count(node => node.Shape == NodeShape.Rounded).ShouldBe(2);
        parsed.Edges.Count.ShouldBe(3);
        parsed.Edges[1].Label.ShouldBe("push");
    }

    [Fact]
    public void Parse_AcceptsTheV1StateDiagramHeader()
    {
        DiagramGraph.Parse("stateDiagram\n    a --> b").Kind.ShouldBe(DiagramKind.StateDiagram);
    }

    [Fact]
    public void Create_WithAShapeTheKindDoesNotAllow_Throws_INV052()
    {
        Should.Throw<ArgumentException>(() => DiagramGraph.Create(
            DiagramKind.StateDiagram,
            FlowDirection.TopDown,
            [new DiagramNode(new NodeId("a"), "A", NodeShape.Diamond)],
            []));
    }

    [Fact]
    public void Create_WithAnEdgeKindTheKindDoesNotAllow_Throws_INV052()
    {
        Should.Throw<ArgumentException>(() => DiagramGraph.Create(
            DiagramKind.StateDiagram,
            FlowDirection.TopDown,
            [new DiagramNode(new NodeId("a"), "A", NodeShape.Rounded), new DiagramNode(new NodeId("b"), "B", NodeShape.Rounded)],
            [new DiagramEdge(new NodeId("a"), new NodeId("b"), null, EdgeKind.Thick)]));
    }
}
