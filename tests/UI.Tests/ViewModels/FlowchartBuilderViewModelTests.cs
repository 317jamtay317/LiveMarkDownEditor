using Domain;
using Shouldly;
using UI.ViewModels;
using Xunit;

namespace UI.Tests.ViewModels;

/// <summary>
/// Tests for <see cref="FlowchartBuilderViewModel"/> — the Flowchart Builder's state and behaviour.
/// Covers INV-053: editing the builder yields no <see cref="FlowchartBuilderViewModel.Result"/> until
/// Insert, Cancel yields <see langword="null"/>, and the emitted source is the Diagram Graph's own
/// canonical Mermaid; plus the add/connect/reshape/delete behaviour the canvas drives.
/// </summary>
public sealed class FlowchartBuilderViewModelTests
{
    [Fact]
    public void New_FromNull_StartsEmpty()
    {
        var builder = new FlowchartBuilderViewModel(existingSource: null);

        builder.Nodes.ShouldBeEmpty();
        builder.Edges.ShouldBeEmpty();
        builder.Direction.ShouldBe(FlowDirection.TopDown);
    }

    [Fact]
    public void New_FromExistingSource_ParsesTheDiagramIn()
    {
        var builder = new FlowchartBuilderViewModel(
            "flowchart LR\n    A[\"Start\"]\n    B{\"Decide\"}\n    A --> B");

        builder.Direction.ShouldBe(FlowDirection.LeftRight);
        builder.Nodes.Select(n => n.Id.Value).ShouldBe(["A", "B"]);
        builder.Nodes[0].Label.ShouldBe("Start");
        builder.Nodes[1].Shape.ShouldBe(NodeShape.Diamond);
        builder.Edges.Count.ShouldBe(1);
    }

    [Fact]
    public void SeededNodes_AreLaidOutSoTheyDoNotStack()
    {
        var builder = new FlowchartBuilderViewModel(
            "flowchart TD\n    A[\"A\"]\n    B[\"B\"]\n    A --> B");

        (builder.Nodes[0].X == builder.Nodes[1].X && builder.Nodes[0].Y == builder.Nodes[1].Y).ShouldBeFalse();
    }

    [Fact]
    public void AddNode_AddsANodeAndSelectsIt()
    {
        var builder = new FlowchartBuilderViewModel(existingSource: null);

        builder.AddNodeCommand.Execute(null);

        builder.Nodes.Count.ShouldBe(1);
        builder.SelectedNode.ShouldBe(builder.Nodes[0]);
        builder.MermaidSource.ShouldContain("n1");
    }

    [Fact]
    public void Connect_AddsAnEdgeBetweenTwoNodes()
    {
        var builder = new FlowchartBuilderViewModel(existingSource: null);
        builder.AddNodeCommand.Execute(null);
        builder.AddNodeCommand.Execute(null);

        builder.Connect(builder.Nodes[0], builder.Nodes[1]);

        builder.Edges.Count.ShouldBe(1);
        builder.MermaidSource.ShouldContain("n1 --> n2");
    }

    [Fact]
    public void MermaidSource_MatchesTheDomainEmit_INV051()
    {
        var expected = DiagramGraph.Empty(DiagramKind.Flowchart, FlowDirection.TopDown)
            .AddNode("Node", NodeShape.Rectangle)
            .AddNode("Node", NodeShape.Rectangle);
        var withEdge = expected.Connect(expected.Nodes[0].Id, expected.Nodes[1].Id, label: null, EdgeKind.Arrow);

        var builder = new FlowchartBuilderViewModel(existingSource: null);
        builder.AddNodeCommand.Execute(null);
        builder.AddNodeCommand.Execute(null);
        builder.Connect(builder.Nodes[0], builder.Nodes[1]);

        builder.MermaidSource.ShouldBe(withEdge.ToMermaidSource());
    }

    [Fact]
    public void RenamingANode_ReEmitsTheSource()
    {
        var builder = new FlowchartBuilderViewModel(existingSource: null);
        builder.AddNodeCommand.Execute(null);

        builder.Nodes[0].Label = "Start";

        builder.MermaidSource.ShouldContain("\"Start\"");
    }

    [Fact]
    public void ReshapingTheSelectedNode_ReEmitsTheSource()
    {
        var builder = new FlowchartBuilderViewModel(existingSource: null);
        builder.AddNodeCommand.Execute(null);
        builder.SelectNode(builder.Nodes[0]);

        builder.SelectedNodeShape = NodeShape.Diamond;

        builder.Nodes[0].Shape.ShouldBe(NodeShape.Diamond);
        builder.MermaidSource.ShouldContain("{\"Node\"}");
    }

    [Fact]
    public void ChangingDirection_ReEmitsTheSource()
    {
        var builder = new FlowchartBuilderViewModel(existingSource: null);

        builder.Direction = FlowDirection.LeftRight;

        builder.MermaidSource.ShouldStartWith("flowchart LR");
    }

    [Fact]
    public void DeletingANode_AlsoRemovesItsIncidentEdges()
    {
        var builder = new FlowchartBuilderViewModel(existingSource: null);
        builder.AddNodeCommand.Execute(null);
        builder.AddNodeCommand.Execute(null);
        builder.Connect(builder.Nodes[0], builder.Nodes[1]);
        builder.SelectNode(builder.Nodes[0]);

        builder.DeleteSelectedCommand.Execute(null);

        builder.Nodes.Count.ShouldBe(1);
        builder.Edges.ShouldBeEmpty();
    }

    [Fact]
    public void DeleteSelected_IsDisabled_WithNoSelection()
    {
        var builder = new FlowchartBuilderViewModel(existingSource: null);

        builder.DeleteSelectedCommand.CanExecute(null).ShouldBeFalse();

        builder.AddNodeCommand.Execute(null); // AddNode selects the new node
        builder.DeleteSelectedCommand.CanExecute(null).ShouldBeTrue();
    }

    [Fact]
    public void MovingANode_DoesNotChangeTheSource_INV051()
    {
        var builder = new FlowchartBuilderViewModel(existingSource: null);
        builder.AddNodeCommand.Execute(null);
        var before = builder.MermaidSource;

        builder.MoveNode(builder.Nodes[0], 500, 500);

        builder.MermaidSource.ShouldBe(before);
    }

    [Fact]
    public void Editing_ProducesNoResult_UntilInsert_INV053()
    {
        var builder = new FlowchartBuilderViewModel(existingSource: null);
        builder.AddNodeCommand.Execute(null);
        builder.Nodes[0].Label = "Start";

        builder.Result.ShouldBeNull(); // no commit yet — no edit escapes the builder
        builder.DialogResult.ShouldBeNull();
    }

    [Fact]
    public void Insert_YieldsTheMermaidSource_INV053()
    {
        var builder = new FlowchartBuilderViewModel(existingSource: null);
        builder.AddNodeCommand.Execute(null);
        builder.Nodes[0].Label = "Start";

        builder.InsertCommand.Execute(null);

        builder.DialogResult.ShouldBe(true);
        builder.Result.ShouldBe(builder.MermaidSource);
        builder.Result.ShouldContain("\"Start\"");
    }

    [Fact]
    public void Cancel_YieldsNull_INV053()
    {
        var builder = new FlowchartBuilderViewModel(existingSource: null);
        builder.AddNodeCommand.Execute(null);

        builder.CancelCommand.Execute(null);

        builder.DialogResult.ShouldBe(false);
        builder.Result.ShouldBeNull();
    }

    [Fact]
    public void Insert_AfterEditing_RoundTripsThroughTheDomain_INV051()
    {
        var builder = new FlowchartBuilderViewModel(existingSource: null);
        builder.AddNodeCommand.Execute(null);
        builder.Nodes[0].Label = "Start";
        builder.SelectNode(builder.Nodes[0]);
        builder.SelectedNodeShape = NodeShape.Stadium;
        builder.InsertCommand.Execute(null);

        var reparsed = DiagramGraph.Parse(builder.Result!);

        reparsed.Nodes[0].Label.ShouldBe("Start");
        reparsed.Nodes[0].Shape.ShouldBe(NodeShape.Stadium);
    }
}
