using Microsoft.UI.Xaml;
using Tweaker.Core.Features.Common.Constants;
using Tweaker.Core.Features.Common.Events;
using Tweaker.Core.Features.Common.Events.Settings;
using Tweaker.Core.Features.Common.Interfaces;
using Tweaker.UI.Features.Common.Interfaces;
using Windows.UI.ViewManagement;


namespace Tweaker.UI.Features.Common.Services;

/// <summary>
/// Service for managing application themes in WinUI 3.
/// </summary>
public class ThemeService : IThemeService
{
    private readonly IUserPreferencesService _userPreferences;
    private readonly IWindowsRegistryService _registryService;
    private readonly IInteractiveUserService _interactiveUserService;
    private readonly IMainWindowProvider _mainWindowProvider;
    private readonly UISettings _uiSettings;
    private TweakerTheme _currentTheme = TweakerTheme.System;

    /// <inheritdoc />
    public TweakerTheme CurrentTheme => _currentTheme;

    /// <inheritdoc />
    public event EventHandler<TweakerTheme>? ThemeChanged;

    public ThemeService(
        IUserPreferencesService userPreferences,
        IWindowsRegistryService registryService,
        IInteractiveUserService interactiveUserService,
        IEventBus eventBus,
        IMainWindowProvider mainWindowProvider)
    {
        _userPreferences = userPreferences;
        _registryService = registryService;
        _interactiveUserService = interactiveUserService;
        _mainWindowProvider = mainWindowProvider;
        _uiSettings = new UISettings();

        // Listen for Windows theme changes to update System theme followers
        _uiSettings.ColorValuesChanged += OnWindowsThemeChanged;

        // Under OTS, UISettings.ColorValuesChanged tracks the admin's theme.
        // Listen for the theme setting being applied so we can update the window.
        if (_interactiveUserService.IsOtsElevation)
        {
            eventBus.Subscribe<SettingAppliedEvent>(OnSettingApplied);
        }
    }

    /// <inheritdoc />
    public void SetTheme(TweakerTheme theme)
    {
        _currentTheme = theme;
        ApplyTheme(theme);

        // Save preference asynchronously (fire and forget since UI has already updated)
        _ = SaveThemePreferenceAsync(theme);

        ThemeChanged?.Invoke(this, theme);
    }

    /// <inheritdoc />
    public void LoadSavedTheme()
    {
        // Load theme preference synchronously to avoid async/await deadlock on UI thread
        _currentTheme = LoadThemePreferenceSync();
        ApplyTheme(_currentTheme);
    }

    private TweakerTheme LoadThemePreferenceSync()
    {
        try
        {
            // Use synchronous method to get preference to avoid deadlock
            var themeString = _userPreferences.GetPreference<string>("Theme", string.Empty);

            if (string.IsNullOrEmpty(themeString))
            {
                return TweakerTheme.System; // Default to following Windows
            }

            if (Enum.TryParse<TweakerTheme>(themeString, out var theme))
            {
                return theme;
            }
        }
        catch
        {
            // Fall through to default
        }

        return TweakerTheme.System;
    }

    /// <inheritdoc />
    public ElementTheme GetEffectiveTheme()
    {
        return _currentTheme switch
        {
            TweakerTheme.System => IsWindowsDarkTheme() ? ElementTheme.Dark : ElementTheme.Light,
            TweakerTheme.LightNative => ElementTheme.Light,
            TweakerTheme.DarkNative => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
    }

    private void ApplyTheme(TweakerTheme theme)
    {
        if (_mainWindowProvider.MainWindow?.Content is not FrameworkElement rootElement)
            return;

        switch (theme)
        {
            case TweakerTheme.System:
                // Under OTS, ElementTheme.Default follows the admin's theme.
                // Explicitly set based on the interactive user's registry instead.
                if (_interactiveUserService.IsOtsElevation)
                    rootElement.RequestedTheme = IsWindowsDarkTheme() ? ElementTheme.Dark : ElementTheme.Light;
                else
                    rootElement.RequestedTheme = ElementTheme.Default;
                break;

            case TweakerTheme.LightNative:
                rootElement.RequestedTheme = ElementTheme.Light;
                break;

            case TweakerTheme.DarkNative:
                rootElement.RequestedTheme = ElementTheme.Dark;
                break;
        }
    }

    private async Task SaveThemePreferenceAsync(TweakerTheme theme)
    {
        try
        {
            await _userPreferences.SetPreferenceAsync("Theme", theme.ToString());
        }
        catch
        {
            // Silently ignore save failures - theme is already applied in memory
        }
    }

    private async Task<TweakerTheme> LoadThemePreferenceAsync()
    {
        try
        {
            var themeString = await _userPreferences.GetPreferenceAsync<string>("Theme", string.Empty);

            if (string.IsNullOrEmpty(themeString))
            {
                return TweakerTheme.System; // Default to following Windows
            }

            if (Enum.TryParse<TweakerTheme>(themeString, out var theme))
            {
                return theme;
            }
        }
        catch
        {
            // Fall through to default
        }

        return TweakerTheme.System;
    }

    private bool IsWindowsDarkTheme()
    {
        if (_interactiveUserService.IsOtsElevation)
        {
            // Under OTS elevation, UISettings reflects the admin's theme.
            // Read from the interactive user's registry hive instead.
            var value = _registryService.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme");
            if (value is int intVal)
                return intVal == 0;
        }

        var foreground = _uiSettings.GetColorValue(UIColorType.Foreground);
        return foreground.R > 128 && foreground.G > 128 && foreground.B > 128;
    }

    private void OnSettingApplied(SettingAppliedEvent evt)
    {
        if (evt.SettingId != SettingIds.ThemeModeWindows || _currentTheme != TweakerTheme.System)
            return;

        _mainWindowProvider.MainWindow?.DispatcherQueue.TryEnqueue(() =>
        {
            ApplyTheme(TweakerTheme.System);
            ThemeChanged?.Invoke(this, TweakerTheme.System);
        });
    }

    private void OnWindowsThemeChanged(UISettings sender, object args)
    {
        // Only react if we're following system theme
        if (_currentTheme == TweakerTheme.System)
        {
            // Must dispatch to UI thread
            _mainWindowProvider.MainWindow?.DispatcherQueue.TryEnqueue(() =>
            {
                // Under OTS, re-apply explicitly since ElementTheme.Default tracks the admin
                if (_interactiveUserService.IsOtsElevation)
                    ApplyTheme(TweakerTheme.System);

                ThemeChanged?.Invoke(this, TweakerTheme.System);
            });
        }
    }
}
