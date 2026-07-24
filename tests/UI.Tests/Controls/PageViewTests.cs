using System.Windows;
using System.Windows.Controls;
using Shouldly;
using UI.Controls;
using UI.Core;
using UI.Tests.Wysiwyg;
using Xunit;

namespace UI.Tests.Controls;

/// <summary>
/// Tests that Page View lays the Document Sheet out at the Page Setup's oriented Page width and Print
/// Margins — the on-screen surface of the one editor-wide setup (INV-058, INV-061).
/// </summary>
public sealed class PageViewTests
{
    private sealed record Surface(Grid Grid, MarkdownRichEditor Editor);

    private static Surface BuildSurface(PageSetup? setup)
    {
        var editor = new MarkdownRichEditor();
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        Grid.SetColumn(editor, 1);
        grid.Children.Add(editor);

        if (setup is not null)
        {
            PageView.SetSetup(grid, setup);
        }

        PageView.SetEditor(grid, editor);
        PageView.SetIsEnabled(grid, true);
        return new Surface(grid, editor);
    }

    [Fact]
    public void EnterPageView_LaysTheSheetOutAtTheSetupsPageWidthAndMargins_INV061()
    {
        StaThread.Run(() =>
        {
            var setup = new PageSetup(PageOrientation.Landscape, PrintMargins.For(MarginPreset.Narrow));

            var surface = BuildSurface(setup);

            surface.Editor.Width.ShouldBe(1056d);
            surface.Editor.Padding.ShouldBe(new Thickness(48d));
        });
    }

    [Fact]
    public void EnterPageView_WithNoSetup_UsesTheDefault_INV061()
    {
        StaThread.Run(() =>
        {
            var surface = BuildSurface(setup: null);

            surface.Editor.Width.ShouldBe(816d);
            surface.Editor.Padding.ShouldBe(new Thickness(96d));
        });
    }

    [Fact]
    public void ChangingTheSetup_WhileInPageView_RelaysTheSheetOut_INV061()
    {
        StaThread.Run(() =>
        {
            var surface = BuildSurface(PageSetup.Default);

            PageView.SetSetup(
                surface.Grid,
                new PageSetup(PageOrientation.Landscape, PrintMargins.For(MarginPreset.Wide)));

            surface.Editor.Width.ShouldBe(1056d);
            surface.Editor.Padding.ShouldBe(new Thickness(192d, 96d, 192d, 96d));
        });
    }
}
