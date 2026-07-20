using System.Windows;
using System.Windows.Controls;
using Shouldly;
using UI.Controls;
using UI.Tests.Wysiwyg;
using Xunit;

namespace UI.Tests.Themes;

/// <summary>
/// Tests for the Command Tip's themed look, and for the themed chrome behind every other tooltip.
/// WPF's stock ToolTip is a pale, system-drawn popup that ignores the palette — in the dark theme it
/// reads as a glaring near-white box, the same bug the stock TextBox and ComboBox carry (see
/// <c>Themes/Controls.xaml</c>). These tests hold both the <see cref="CommandTip"/> default style and
/// the implicit <see cref="ToolTip"/> style to taking every colour from the palette, so a theme swap
/// recolours a tooltip with everything else.
/// </summary>
/// <remarks>
/// Like <see cref="DialogButtonTests"/>, these load the compiled resource dictionaries directly rather
/// than constructing a live tooltip: resolving a tooltip's template means a running
/// <see cref="System.Windows.Application"/> with the palette merged, which the test host has no STA
/// Application to provide. That a call site actually shows a themed Command Tip is verified by driving
/// the real app.
/// </remarks>
public sealed class CommandTipStyleTests
{
    /// <summary>The Command Tip's default style is defined, and it styles a <see cref="CommandTip"/>.</summary>
    [Fact]
    public void CommandTipStyle_IsDefined_AndTargetsACommandTip()
    {
        StaThread.Run(() =>
        {
            var dictionary = LoadDictionary("Controls/CommandTip.xaml");

            dictionary.Contains(typeof(CommandTip)).ShouldBeTrue("A Command Tip needs its default style.");

            var style = dictionary[typeof(CommandTip)].ShouldBeOfType<Style>();

            style.TargetType.ShouldBe(typeof(CommandTip));
        });
    }

    /// <summary>
    /// The implicit ToolTip style is defined, so a bare string tooltip (the gutter's Fold Toggle, the
    /// Flowchart Builder's connector) is drawn from the palette rather than in stock white chrome.
    /// </summary>
    [Fact]
    public void ToolTipStyle_IsDefined_AndTargetsAToolTip()
    {
        StaThread.Run(() =>
        {
            var dictionary = LoadDictionary("Themes/Controls.xaml");

            dictionary.Contains(typeof(ToolTip)).ShouldBeTrue("A bare tooltip needs themed chrome.");

            var style = dictionary[typeof(ToolTip)].ShouldBeOfType<Style>();

            style.TargetType.ShouldBe(typeof(ToolTip));
        });
    }

    /// <summary>
    /// Every colour the Command Tip paints itself with is a DynamicResource lookup into the active
    /// palette; a literal would strand it in the theme it was authored against on the next swap.
    /// </summary>
    [Fact]
    public void CommandTipStyle_TakesItsColours_FromThePalette()
    {
        StaThread.Run(() => AssertColoursComeFromThePalette(
            LoadDictionary("Controls/CommandTip.xaml"), typeof(CommandTip)));
    }

    /// <summary>The implicit ToolTip chrome likewise takes every colour it sets from the palette.</summary>
    [Fact]
    public void ToolTipStyle_TakesItsColours_FromThePalette()
    {
        StaThread.Run(() => AssertColoursComeFromThePalette(
            LoadDictionary("Themes/Controls.xaml"), typeof(ToolTip)));
    }

    /// <summary>
    /// The shared command styles — every command button, dropdown header, and dropdown entry derives
    /// from one of these — carry every colour they set as a palette lookup, so a theme swap recolours a
    /// dropdown and its list with everything else.
    /// </summary>
    public static TheoryData<string> CommandStyleKeys() =>
    [
        "CommandButton",
        "CommandMenuItem",
        "CommandMenuEntry",
    ];

    /// <summary>
    /// The styles that carry a Command Tip set <c>ToolTipService.ShowOnDisabled</c>, so a disabled
    /// command (Bold with no selection, Add Row outside a table) still explains itself on hover — the
    /// moment a tip is most useful, because the user is asking why the action is unavailable.
    /// </summary>
    [Theory]
    [MemberData(nameof(CommandStyleKeys))]
    public void CommandStyle_ShowsItsTooltip_EvenWhenDisabled(string key)
    {
        StaThread.Run(() =>
        {
            var style = (Style)LoadDictionary("Themes/Controls.xaml")[key]!;

            var setter = style.Setters
                .OfType<Setter>()
                .SingleOrDefault(candidate => candidate.Property == ToolTipService.ShowOnDisabledProperty);

            setter.ShouldNotBeNull(
                $"'{key}' must set ToolTipService.ShowOnDisabled so a disabled command still shows its Command Tip.");
            setter.Value.ShouldBe(true);
        });
    }

    private static void AssertColoursComeFromThePalette(ResourceDictionary dictionary, Type key)
    {
        var style = (Style)dictionary[key]!;

        var colourSetters = style.Setters
            .OfType<Setter>()
            .Where(setter => setter.Property == Control.ForegroundProperty
                             || setter.Property == Control.BackgroundProperty
                             || setter.Property == Control.BorderBrushProperty)
            .ToList();

        colourSetters.ShouldNotBeEmpty($"'{key.Name}' must colour itself.");
        foreach (var setter in colourSetters)
        {
            setter.Value.ShouldBeOfType<DynamicResourceExtension>(
                $"'{key.Name}' sets {setter.Property.Name} to a literal, which no theme swap can recolour.");
        }
    }

    /// <summary>
    /// Loads a compiled resource dictionary from the UI assembly — the same resource the running app
    /// merges, so a key that resolves here resolves there. Touching
    /// <see cref="System.Windows.Application"/> first registers the <c>pack:</c> URI scheme, as in
    /// <see cref="CommandIconTests"/>.
    /// </summary>
    private static ResourceDictionary LoadDictionary(string componentPath)
    {
        _ = System.Windows.Application.ResourceAssembly;

        return new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/UI;component/{componentPath}", UriKind.Absolute),
        };
    }
}
