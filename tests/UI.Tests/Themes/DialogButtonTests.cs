using System.Windows;
using System.Windows.Controls;
using Shouldly;
using UI.Tests.Wysiwyg;
using Xunit;

namespace UI.Tests.Themes;

/// <summary>
/// Tests for the Dialog Button styles: the look of the buttons that accept or dismiss the Link
/// Prompt. WPF's stock Button chrome is a fixed grey gradient that ignores the palette entirely, so
/// a button left unstyled reads as a foreign control sitting on a themed surface — and looks broken
/// outright in the dark palette. These tests hold both styles to taking every colour from the
/// palette, so a theme swap recolours them with everything else.
/// </summary>
/// <remarks>
/// That the Link Prompt <em>asks</em> for these styles is not covered here. Reaching its buttons
/// means constructing the Window, and the <c>StaticResource</c> lookups in its XAML resolve against
/// <see cref="System.Windows.Application"/>.<c>Resources</c> — which the running app merges in
/// App.xaml but the test host has no Application to provide. Hosting one would mean a process-wide
/// <c>Application.Current</c> pinned to a single STA thread, which is not how the rest of these
/// tests run.
/// </remarks>
public sealed class DialogButtonTests
{
    /// <summary>The Dialog Button styles, by the role each one plays in a dialog.</summary>
    public static TheoryData<string> DialogButtonStyleKeys() =>
    [
        "DialogButton",
        "PrimaryDialogButton",
    ];

    [Theory]
    [MemberData(nameof(DialogButtonStyleKeys))]
    public void DialogButtonStyle_IsDefined_AndTargetsAButton(string key)
    {
        StaThread.Run(() =>
        {
            var controls = LoadControlStyles();

            controls.Contains(key).ShouldBeTrue($"A dialog asks for '{key}'.");

            var style = controls[key].ShouldBeOfType<Style>();

            style.TargetType.ShouldBe(typeof(Button));
        });
    }

    /// <summary>
    /// Every colour a Dialog Button paints itself with is a DynamicResource lookup into the active
    /// palette. A literal colour here would survive a theme swap and strand the button in the
    /// palette it was authored against — the very bug these styles exist to fix.
    /// </summary>
    [Theory]
    [MemberData(nameof(DialogButtonStyleKeys))]
    public void DialogButtonStyle_TakesItsColours_FromThePalette(string key)
    {
        StaThread.Run(() =>
        {
            var style = (Style)LoadControlStyles()[key]!;

            var colourSetters = style.Setters
                .OfType<Setter>()
                .Where(setter => setter.Property == Control.ForegroundProperty
                                 || setter.Property == Control.BackgroundProperty
                                 || setter.Property == Control.BorderBrushProperty)
                .ToList();

            colourSetters.ShouldNotBeEmpty($"'{key}' must colour itself.");
            foreach (var setter in colourSetters)
            {
                setter.Value.ShouldBeOfType<DynamicResourceExtension>(
                    $"'{key}' sets {setter.Property.Name} to a literal, which no theme swap can recolour.");
            }
        });
    }

    /// <summary>
    /// Loads the compiled control style dictionary — the same resource the running app merges, so a
    /// key that resolves here resolves there. Touching <see cref="System.Windows.Application"/>
    /// first is what registers the <c>pack:</c> URI scheme, as in <see cref="CommandIconTests"/>.
    /// </summary>
    private static ResourceDictionary LoadControlStyles()
    {
        _ = System.Windows.Application.ResourceAssembly;

        return new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/UI;component/Themes/Controls.xaml", UriKind.Absolute),
        };
    }
}
