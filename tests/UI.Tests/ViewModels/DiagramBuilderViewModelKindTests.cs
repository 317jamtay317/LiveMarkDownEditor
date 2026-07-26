using Domain;
using Shouldly;
using UI.ViewModels;
using Xunit;

namespace UI.Tests.ViewModels;

/// <summary>
/// Tests for the Diagram Kind the <see cref="DiagramBuilderViewModel"/> authors (INV-070): it opens on
/// the kind the Mermaid Diagram already is, offers each kind's own Shape Set and Edge Set, and keeps
/// the diagram when the kind changes — still writing nothing until Insert (INV-053).
/// </summary>
public sealed class DiagramBuilderViewModelKindTests
{
    [Fact]
    public void New_FromNull_StartsAsAFlowchart_INV070()
    {
        var builder = new DiagramBuilderViewModel(existingSource: null);

        builder.Kind.ShouldBe(DiagramKind.Flowchart);
        builder.Kinds.ShouldBe(Enum.GetValues<DiagramKind>());
    }

    [Theory]
    [InlineData("flowchart TD\n    a[\"A\"]", DiagramKind.Flowchart)]
    [InlineData("stateDiagram-v2\n    a : A", DiagramKind.StateDiagram)]
    [InlineData("classDiagram\n    class a", DiagramKind.ClassDiagram)]
    [InlineData("erDiagram\n    a[\"A\"]", DiagramKind.EntityRelationshipDiagram)]
    public void New_FromExistingSource_OpensOnTheKindThatSourceAlreadyIs_INV070(string source, DiagramKind expected)
    {
        new DiagramBuilderViewModel(source).Kind.ShouldBe(expected);
    }

    [Fact]
    public void New_FromADiagramKindItCannotAuthor_StartsAnEmptyFlowchart_INV053()
    {
        // A sequence diagram is not a node/arrow graph — the builder starts empty rather than guessing.
        var builder = new DiagramBuilderViewModel("sequenceDiagram\n    Alice->>Bob: Hi");

        builder.Kind.ShouldBe(DiagramKind.Flowchart);
        builder.Nodes.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(DiagramKind.Flowchart)]
    [InlineData(DiagramKind.StateDiagram)]
    [InlineData(DiagramKind.ClassDiagram)]
    [InlineData(DiagramKind.EntityRelationshipDiagram)]
    public void ThePickers_OfferExactlyTheKindsOwnShapeSetAndEdgeSet_INV070(DiagramKind kind)
    {
        var builder = new DiagramBuilderViewModel(existingSource: null) { Kind = kind };

        builder.NodeShapes.ShouldBe(kind.ShapeSet());
        builder.EdgeKinds.ShouldBe(kind.EdgeSet());
    }

    [Fact]
    public void ChangingTheKind_KeepsEveryNodeAndEdge_INV070()
    {
        var builder = new DiagramBuilderViewModel(existingSource: null);
        builder.AddNodeCommand.Execute(null);
        builder.AddNodeCommand.Execute(null);
        builder.Connect(builder.Nodes[0], builder.Nodes[1]);
        builder.Nodes[0].Label = "First";

        builder.Kind = DiagramKind.ClassDiagram;

        builder.Nodes.Count.ShouldBe(2);
        builder.Edges.Count.ShouldBe(1);
        builder.Nodes[0].Label.ShouldBe("First");
        builder.MermaidSource.ShouldStartWith("classDiagram");
    }

    [Fact]
    public void ChangingTheKind_CoercesAShapeOrEdgeKindTheNewKindDoesNotAllow_INV070()
    {
        var builder = new DiagramBuilderViewModel(existingSource: null);
        builder.AddNodeCommand.Execute(null);
        builder.AddNodeCommand.Execute(null);
        builder.SelectNode(builder.Nodes[0]);
        builder.SelectedNodeShape = NodeShape.Diamond;
        var edge = builder.Connect(builder.Nodes[0], builder.Nodes[1]);
        builder.SelectEdge(edge);
        builder.SelectedEdgeKind = EdgeKind.Thick;

        builder.Kind = DiagramKind.StateDiagram;

        builder.Nodes[0].Shape.ShouldBe(NodeShape.Rounded);
        builder.Edges[0].Kind.ShouldBe(EdgeKind.Arrow);
    }

    [Fact]
    public void TheDirectionPicker_IsOffForAKindWhoseMermaidCarriesNoDirection_INV070()
    {
        var builder = new DiagramBuilderViewModel(existingSource: null) { Direction = FlowDirection.LeftRight };

        builder.HasFlowDirection.ShouldBeTrue();

        builder.Kind = DiagramKind.EntityRelationshipDiagram;
        builder.HasFlowDirection.ShouldBeFalse();
        builder.MermaidSource.ShouldNotContain("direction");

        // The choice is kept, not thrown away — it returns when the kind does.
        builder.Kind = DiagramKind.Flowchart;
        builder.Direction.ShouldBe(FlowDirection.LeftRight);
        builder.MermaidSource.ShouldStartWith("flowchart LR");
    }

    [Fact]
    public void ChangingTheKind_ReEmitsTheSource_INV051()
    {
        var builder = new DiagramBuilderViewModel(existingSource: null);
        builder.AddNodeCommand.Execute(null);

        builder.Kind = DiagramKind.EntityRelationshipDiagram;

        builder.MermaidSource.ShouldStartWith("erDiagram");
    }

    [Fact]
    public void ChangingTheKind_WritesNothing_UntilInsert_INV053()
    {
        var builder = new DiagramBuilderViewModel("flowchart TD\n    a[\"A\"]");

        builder.Kind = DiagramKind.StateDiagram;

        builder.Result.ShouldBeNull();
        builder.DialogResult.ShouldBeNull();
    }

    [Theory]
    [InlineData(DiagramKind.Flowchart, NodeShape.Rectangle)]
    [InlineData(DiagramKind.StateDiagram, NodeShape.Rounded)]
    [InlineData(DiagramKind.ClassDiagram, NodeShape.Rectangle)]
    [InlineData(DiagramKind.EntityRelationshipDiagram, NodeShape.Rectangle)]
    public void AddNode_ShapesTheNewNodeAsTheKindsDefault_INV070(DiagramKind kind, NodeShape expected)
    {
        var builder = new DiagramBuilderViewModel(existingSource: null) { Kind = kind };

        builder.AddNodeCommand.Execute(null);

        builder.Nodes[0].Shape.ShouldBe(expected);
    }

    [Theory]
    [InlineData(DiagramKind.Flowchart, "Node")]
    [InlineData(DiagramKind.StateDiagram, "State")]
    [InlineData(DiagramKind.ClassDiagram, "Class")]
    [InlineData(DiagramKind.EntityRelationshipDiagram, "Entity")]
    public void AddNode_NamesTheNewNodeAfterWhatTheKindCallsOne(DiagramKind kind, string expected)
    {
        var builder = new DiagramBuilderViewModel(existingSource: null) { Kind = kind };

        builder.AddNodeCommand.Execute(null);

        builder.Nodes[0].Label.ShouldBe(expected);
    }

    [Theory]
    [InlineData(DiagramKind.Flowchart, EdgeKind.Arrow)]
    [InlineData(DiagramKind.StateDiagram, EdgeKind.Arrow)]
    [InlineData(DiagramKind.ClassDiagram, EdgeKind.Arrow)]
    [InlineData(DiagramKind.EntityRelationshipDiagram, EdgeKind.OneToMany)]
    public void Connect_DrawsTheNewEdgeAsTheKindsDefault_INV070(DiagramKind kind, EdgeKind expected)
    {
        var builder = new DiagramBuilderViewModel(existingSource: null) { Kind = kind };
        builder.AddNodeCommand.Execute(null);
        builder.AddNodeCommand.Execute(null);

        builder.Connect(builder.Nodes[0], builder.Nodes[1]).Kind.ShouldBe(expected);
    }

    [Fact]
    public void AStateDiagram_AuthorsATerminalThroughTheShapePicker_INV070()
    {
        var builder = new DiagramBuilderViewModel(existingSource: null) { Kind = DiagramKind.StateDiagram };
        builder.AddNodeCommand.Execute(null);
        builder.AddNodeCommand.Execute(null);
        builder.SelectNode(builder.Nodes[0]);

        builder.SelectedNodeShape = NodeShape.Terminal;
        builder.Connect(builder.Nodes[0], builder.Nodes[1]);

        builder.MermaidSource.ShouldContain("[*] --> n2");
    }

    [Fact]
    public void Insert_AfterChangingTheKind_YieldsThatKindsCanonicalMermaid_INV053()
    {
        var builder = new DiagramBuilderViewModel(existingSource: null) { Kind = DiagramKind.EntityRelationshipDiagram };
        builder.AddNodeCommand.Execute(null);
        builder.Nodes[0].Label = "Customer";

        builder.InsertCommand.Execute(null);

        DiagramGraph.Parse(builder.Result!).Kind.ShouldBe(DiagramKind.EntityRelationshipDiagram);
        builder.Result!.ShouldContain("\"Customer\"");
    }

    [Fact]
    public void EveryKind_EmitsSourceThatParsesBackAsItself_INV051()
    {
        foreach (var kind in Enum.GetValues<DiagramKind>())
        {
            var builder = new DiagramBuilderViewModel(existingSource: null) { Kind = kind };
            builder.AddNodeCommand.Execute(null);
            builder.AddNodeCommand.Execute(null);
            builder.Connect(builder.Nodes[0], builder.Nodes[1]);

            DiagramGraph.TryParse(builder.MermaidSource, out var graph).ShouldBeTrue();
            graph.Kind.ShouldBe(kind);
            graph.ToMermaidSource().ShouldBe(builder.MermaidSource);
        }
    }
}
