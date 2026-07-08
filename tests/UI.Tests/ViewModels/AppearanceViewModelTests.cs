using Shouldly;
using UI.Core;
using UI.Tests.TestDoubles;
using UI.ViewModels;
using Xunit;

namespace UI.Tests.ViewModels;

/// <summary>
/// Tests for <see cref="AppearanceViewModel"/> — the shell's light/dark theme toggle, backed by the
/// <see cref="IThemeService"/>.
/// </summary>
public sealed class AppearanceViewModelTests
{
    private readonly FakeThemeService _theme = new();

    [Fact]
    public void IsDarkTheme_ReflectsThemeServiceCurrent()
    {
        var appearance = new AppearanceViewModel(_theme);

        appearance.IsDarkTheme.ShouldBeFalse();
    }

    [Fact]
    public void ToggleThemeCommand_TogglesTheme_AndRaisesIsDarkTheme()
    {
        var appearance = new AppearanceViewModel(_theme);
        var raised = false;
        appearance.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppearanceViewModel.IsDarkTheme))
            {
                raised = true;
            }
        };

        appearance.ToggleThemeCommand.Execute(null);

        _theme.Current.ShouldBe(AppTheme.Dark);
        appearance.IsDarkTheme.ShouldBeTrue();
        raised.ShouldBeTrue();
    }

    [Fact]
    public void ToggleThemeCommand_Twice_ReturnsToLight()
    {
        var appearance = new AppearanceViewModel(_theme);

        appearance.ToggleThemeCommand.Execute(null);
        appearance.ToggleThemeCommand.Execute(null);

        _theme.Current.ShouldBe(AppTheme.Light);
        appearance.IsDarkTheme.ShouldBeFalse();
    }
}
