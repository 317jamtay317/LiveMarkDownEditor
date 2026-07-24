using Shouldly;
using UI.ViewModels;
using Xunit;

namespace UI.Tests.ViewModels;

/// <summary>
/// Tests for <see cref="CompactLayout"/> — the pure rule that decides, from the available width and the
/// side panels the user has toggled on, which panels stay visible. Below the width an arrangement needs
/// it collapses them one at a time — Preview, then Source, then the Side Dock — so the Visual Document
/// always keeps its minimum width, and it is a deterministic function of its inputs (INV-059).
/// </summary>
public sealed class CompactLayoutTests
{
    private static readonly PanelIntent All = new(Dock: true, Source: true, Preview: true);

    [Fact]
    public void Resolve_WhenEverythingFits_ShowsEveryToggledPanel_INV059()
    {
        // 240 editor + 260 dock + 420 + 420 = 1340 needed; 1400 has room for all.
        var visible = CompactLayout.Resolve(availableWidth: 1400, All);

        visible.ShouldBe(new PanelVisibility(Dock: true, Source: true, Preview: true));
    }

    [Fact]
    public void Resolve_WhenTooNarrow_CollapsesThePreviewFirst_INV059()
    {
        // 1340 does not fit 1000; dropping only the Preview (920) does.
        var visible = CompactLayout.Resolve(availableWidth: 1000, All);

        visible.ShouldBe(new PanelVisibility(Dock: true, Source: true, Preview: false));
    }

    [Fact]
    public void Resolve_WhenNarrower_CollapsesTheSourceNext_INV059()
    {
        // 1340 → drop Preview (920) → still over 700 → drop Source (500), which fits.
        var visible = CompactLayout.Resolve(availableWidth: 700, All);

        visible.ShouldBe(new PanelVisibility(Dock: true, Source: false, Preview: false));
    }

    [Fact]
    public void Resolve_WhenNarrowest_CollapsesEveryPanelToTheEditorAlone_INV059()
    {
        // Even the Side Dock must go; only the editor (240) is left.
        var visible = CompactLayout.Resolve(availableWidth: 400, All);

        visible.ShouldBe(new PanelVisibility(Dock: false, Source: false, Preview: false));
    }

    [Fact]
    public void Resolve_CollapsesThePreviewBeforeTheSource_EvenWithoutTheDock_INV059()
    {
        var intent = new PanelIntent(Dock: false, Source: true, Preview: true);

        // 240 + 420 + 420 = 1080 does not fit 800; dropping the Preview (660) does — Source survives.
        var visible = CompactLayout.Resolve(availableWidth: 800, intent);

        visible.ShouldBe(new PanelVisibility(Dock: false, Source: true, Preview: false));
    }

    [Fact]
    public void Resolve_WhenWidthIsUnmeasured_LeavesEveryPanelAtItsIntent_INV059()
    {
        // A width of zero means "not yet measured": the layout is left exactly as the user toggled it.
        var visible = CompactLayout.Resolve(availableWidth: 0, All);

        visible.ShouldBe(new PanelVisibility(Dock: true, Source: true, Preview: true));
    }

    [Fact]
    public void Resolve_IsDeterministic_SameInputsYieldTheSameLayout_INV059()
    {
        CompactLayout.Resolve(700, All).ShouldBe(CompactLayout.Resolve(700, All));
    }

    [Theory]
    [InlineData(1340)]
    [InlineData(1000)]
    [InlineData(920)]
    [InlineData(700)]
    [InlineData(500)]
    [InlineData(400)]
    [InlineData(300)]
    [InlineData(241)]
    public void Resolve_AlwaysLeavesTheEditorItsMinimumWidth_INV059(double availableWidth)
    {
        var visible = CompactLayout.Resolve(availableWidth, All);

        var shown = (visible.Dock ? CompactLayout.SideDockWidth : 0)
            + (visible.Source ? CompactLayout.SidePanelWidth : 0)
            + (visible.Preview ? CompactLayout.SidePanelWidth : 0);

        // The shown panels plus the editor's minimum never exceed the width available to them.
        (shown + CompactLayout.EditorMinWidth).ShouldBeLessThanOrEqualTo(availableWidth);
    }
}
