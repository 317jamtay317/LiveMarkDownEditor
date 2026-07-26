using Domain;
using Shouldly;
using Xunit;

namespace Domain.Tests;

/// <summary>
/// Tests for <see cref="DiagramKinds"/> — the Shape Set and Edge Set each Diagram Kind allows, and the
/// defaults and coercion the Diagram Builder uses when the kind changes (INV-052/INV-070).
/// </summary>
public sealed class DiagramKindsTests
{
    [Fact]
    public void Flowchart_AllowsEveryFlowchartShapeAndEdgeKind_INV052()
    {
        DiagramKind.Flowchart.ShapeSet().ShouldBe(
            [NodeShape.Rectangle, NodeShape.Rounded, NodeShape.Stadium, NodeShape.Diamond, NodeShape.Circle]);
        DiagramKind.Flowchart.EdgeSet().ShouldBe(
            [EdgeKind.Arrow, EdgeKind.Dotted, EdgeKind.Thick, EdgeKind.Open]);
    }

    [Fact]
    public void StateDiagram_AllowsAStateAndATerminal_JoinedByTransitions_INV052()
    {
        DiagramKind.StateDiagram.ShapeSet().ShouldBe([NodeShape.Rounded, NodeShape.Terminal]);
        DiagramKind.StateDiagram.EdgeSet().ShouldBe([EdgeKind.Arrow]);
    }

    [Fact]
    public void ClassDiagram_AllowsOneShapeAndTheClassRelationships_INV052()
    {
        DiagramKind.ClassDiagram.ShapeSet().ShouldBe([NodeShape.Rectangle]);
        DiagramKind.ClassDiagram.EdgeSet().ShouldBe(
        [
            EdgeKind.Arrow, EdgeKind.Inheritance, EdgeKind.Composition, EdgeKind.Aggregation,
            EdgeKind.Dependency, EdgeKind.Open,
        ]);
    }

    [Fact]
    public void EntityRelationshipDiagram_AllowsOneShapeAndTheCardinalities_INV052()
    {
        DiagramKind.EntityRelationshipDiagram.ShapeSet().ShouldBe([NodeShape.Rectangle]);
        DiagramKind.EntityRelationshipDiagram.EdgeSet().ShouldBe(
            [EdgeKind.OneToMany, EdgeKind.OneToOne, EdgeKind.ManyToOne, EdgeKind.ManyToMany]);
    }

    [Theory]
    [InlineData(DiagramKind.Flowchart, NodeShape.Rectangle, EdgeKind.Arrow)]
    [InlineData(DiagramKind.StateDiagram, NodeShape.Rounded, EdgeKind.Arrow)]
    [InlineData(DiagramKind.ClassDiagram, NodeShape.Rectangle, EdgeKind.Arrow)]
    [InlineData(DiagramKind.EntityRelationshipDiagram, NodeShape.Rectangle, EdgeKind.OneToMany)]
    public void EveryKind_DefaultsToTheFirstOfItsSets(DiagramKind kind, NodeShape shape, EdgeKind edgeKind)
    {
        kind.DefaultShape().ShouldBe(shape);
        kind.DefaultEdgeKind().ShouldBe(edgeKind);
    }

    [Fact]
    public void Allows_AnswersFromTheKindsOwnSets_INV052()
    {
        DiagramKind.Flowchart.Allows(NodeShape.Diamond).ShouldBeTrue();
        DiagramKind.Flowchart.Allows(NodeShape.Terminal).ShouldBeFalse();
        DiagramKind.StateDiagram.Allows(NodeShape.Terminal).ShouldBeTrue();
        DiagramKind.StateDiagram.Allows(EdgeKind.Dotted).ShouldBeFalse();
        DiagramKind.ClassDiagram.Allows(EdgeKind.Inheritance).ShouldBeTrue();
        DiagramKind.EntityRelationshipDiagram.Allows(EdgeKind.Arrow).ShouldBeFalse();
    }

    [Fact]
    public void Coerce_KeepsAnAllowedShapeAndFallsBackToTheDefault_INV070()
    {
        DiagramKind.StateDiagram.Coerce(NodeShape.Terminal).ShouldBe(NodeShape.Terminal);
        DiagramKind.StateDiagram.Coerce(NodeShape.Diamond).ShouldBe(NodeShape.Rounded);
        DiagramKind.ClassDiagram.Coerce(EdgeKind.Inheritance).ShouldBe(EdgeKind.Inheritance);
        DiagramKind.ClassDiagram.Coerce(EdgeKind.Thick).ShouldBe(EdgeKind.Arrow);
    }

    [Fact]
    public void EveryKind_HasANonEmptyShapeSetAndEdgeSet()
    {
        foreach (var kind in Enum.GetValues<DiagramKind>())
        {
            kind.ShapeSet().ShouldNotBeEmpty();
            kind.EdgeSet().ShouldNotBeEmpty();
        }
    }
}
