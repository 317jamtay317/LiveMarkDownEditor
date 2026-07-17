using System.Windows;
using System.Windows.Media;
using Shouldly;
using UI.Tests.Wysiwyg;
using Xunit;

namespace UI.Tests.Themes;

/// <summary>
/// Tests for the Command Icons: the vector glyphs the Command Bar shows in place of words, one for
/// each of its actions. A Command Icon is presentation-only — it names an action, it never performs
/// one — but XAML resolves it by string at run time, so a missing key or malformed path data draws
/// nothing and says nothing at compile time. These tests hold every icon the Command Bar asks for
/// to being present and drawable.
/// </summary>
public sealed class CommandIconTests
{
    /// <summary>Every Command Icon key the Command Bar references, by the action it names.</summary>
    public static TheoryData<string> CommandIconKeys() =>
    [
        "Icon.New",
        "Icon.Open",
        "Icon.Save",
        "Icon.ExportHtml",
        "Icon.ExportPdf",
        "Icon.Print",
        "Icon.SetHeadingLevel",
        "Icon.ToggleBold",
        "Icon.ToggleItalic",
        "Icon.ToggleStrikethrough",
        "Icon.ToggleCode",
        "Icon.ToggleUnorderedList",
        "Icon.ToggleOrderedList",
        "Icon.ToggleTaskList",
        "Icon.InsertLink",
        "Icon.InsertImage",
        "Icon.ToggleBlockQuote",
        "Icon.Table",
        "Icon.CollapseAllFolds",
        "Icon.ExpandAllFolds",
        "Icon.ToggleNavigationPanel",
        "Icon.ToggleSourcePanel",
        "Icon.ThemeDark",
        "Icon.ThemeLight",
    ];

    [Theory]
    [MemberData(nameof(CommandIconKeys))]
    public void CommandIcon_ForEveryCommandBarAction_IsADrawableGeometry(string key)
    {
        StaThread.Run(() =>
        {
            var icons = LoadCommandIcons();

            icons.Contains(key).ShouldBeTrue($"The Command Bar asks for '{key}'.");

            var geometry = icons[key].ShouldBeAssignableTo<Geometry>();

            geometry.ShouldNotBeNull();
            geometry.IsEmpty().ShouldBeFalse($"'{key}' must draw something.");
        });
    }

    /// <summary>
    /// Every Command Icon is authored on the same 24×24 grid, so the Command Bar can size them all
    /// alike. An icon drawn off that grid would render at a different weight beside its neighbours.
    /// </summary>
    [Theory]
    [MemberData(nameof(CommandIconKeys))]
    public void CommandIcon_ForEveryCommandBarAction_IsDrawnOnTheSharedGrid(string key)
    {
        StaThread.Run(() =>
        {
            var geometry = (Geometry)LoadCommandIcons()[key]!;

            var bounds = geometry.Bounds;

            bounds.Left.ShouldBeGreaterThanOrEqualTo(0);
            bounds.Top.ShouldBeGreaterThanOrEqualTo(0);
            bounds.Right.ShouldBeLessThanOrEqualTo(24);
            bounds.Bottom.ShouldBeLessThanOrEqualTo(24);
        });
    }

    /// <summary>
    /// Loads the compiled Command Icon dictionary — the same resource the running app merges, so a
    /// key that resolves here resolves there. Touching <see cref="System.Windows.Application"/>
    /// first is what registers the <c>pack:</c> URI scheme: the test host has no
    /// <see cref="System.Windows.Application"/> instance to have done it, and without it the pack
    /// URI below will not even parse.
    /// </summary>
    private static ResourceDictionary LoadCommandIcons()
    {
        _ = System.Windows.Application.ResourceAssembly;

        return new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/UI;component/Themes/Icons.xaml", UriKind.Absolute),
        };
    }
}
