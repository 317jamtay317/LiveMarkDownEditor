using UI.Core;

namespace UI.Tests.TestDoubles;

/// <summary>
/// In-memory <see cref="IThemeService"/> for tests: tracks the current theme and raises
/// <see cref="ThemeChanged"/> on apply/toggle, without touching WPF resources.
/// </summary>
public sealed class FakeThemeService : IThemeService
{
    /// <inheritdoc />
    public AppTheme Current { get; private set; } = AppTheme.Light;

    /// <inheritdoc />
    public event EventHandler? ThemeChanged;

    /// <inheritdoc />
    public void Apply(AppTheme theme)
    {
        Current = theme;
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void Toggle() => Apply(Current == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark);
}
