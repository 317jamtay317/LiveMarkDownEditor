namespace UI.Core;

/// <summary>The visual theme applied to the application's chrome and editing surface.</summary>
public enum AppTheme
{
    /// <summary>A light theme: dark text on light surfaces.</summary>
    Light,

    /// <summary>A dark theme: light text on dark surfaces.</summary>
    Dark,
}

/// <summary>
/// Applies and toggles the application's visual <see cref="AppTheme"/>. Abstracted so ViewModels can
/// drive appearance without depending on WPF resource types, keeping them unit-testable.
/// </summary>
public interface IThemeService
{
    /// <summary>The currently applied theme.</summary>
    AppTheme Current { get; }

    /// <summary>Raised after the applied theme changes.</summary>
    event EventHandler? ThemeChanged;

    /// <summary>Applies the given theme, replacing the current one.</summary>
    /// <param name="theme">The theme to apply.</param>
    void Apply(AppTheme theme);

    /// <summary>Switches between <see cref="AppTheme.Light"/> and <see cref="AppTheme.Dark"/>.</summary>
    void Toggle();
}
