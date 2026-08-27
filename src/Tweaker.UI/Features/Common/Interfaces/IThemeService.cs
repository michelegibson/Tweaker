using Microsoft.UI.Xaml;

namespace Tweaker.UI.Features.Common.Interfaces;

/// <summary>
/// Defines the available theme options for Tweaker.
/// </summary>
public enum TweakerTheme
{
    /// <summary>Follow Windows system theme setting.</summary>
    System,
    /// <summary>Pure WinUI 3 light mode with Windows accent color.</summary>
    LightNative,
    /// <summary>Pure WinUI 3 dark mode with Windows accent color.</summary>
    DarkNative
}

/// <summary>
/// Service for managing application themes.
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// Gets the currently applied theme.
    /// </summary>
    TweakerTheme CurrentTheme { get; }

    /// <summary>
    /// Raised when the theme changes.
    /// </summary>
    event EventHandler<TweakerTheme>? ThemeChanged;

    /// <summary>
    /// Sets and applies the specified theme.
    /// </summary>
    /// <param name="theme">The theme to apply.</param>
    void SetTheme(TweakerTheme theme);

    /// <summary>
    /// Loads the saved theme preference and applies it.
    /// </summary>
    void LoadSavedTheme();

    /// <summary>
    /// Gets the actual effective theme (Light or Dark) accounting for System theme following Windows.
    /// </summary>
    ElementTheme GetEffectiveTheme();
}
