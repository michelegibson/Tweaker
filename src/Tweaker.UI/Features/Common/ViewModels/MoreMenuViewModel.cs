using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tweaker.Core.Features.Common.Constants;
using Tweaker.Core.Features.Common.Enums;
using Tweaker.Core.Features.Common.Interfaces;

namespace Tweaker.UI.Features.Common.ViewModels;

/// <summary>
/// ViewModel for the More menu flyout, providing localized strings and commands.
/// </summary>
public partial class MoreMenuViewModel : ObservableObject, IDisposable
{
    private bool _disposed;
    private readonly ILocalizationService _localizationService;
    private readonly IVersionService _versionService;
    private readonly ILogService _logService;
    private readonly IApplicationCloseService _applicationCloseService;
    private readonly IFileSystemService _fileSystemService;
    private readonly IExplorerWindowManager _explorerWindowManager;
    private readonly IChangeHistoryService _changeHistoryService;
    private readonly IProcessExecutor _processExecutor;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    public partial string VersionInfo { get; set; }

    public MoreMenuViewModel(
        ILocalizationService localizationService,
        IVersionService versionService,
        ILogService logService,
        IApplicationCloseService applicationCloseService,
        IFileSystemService fileSystemService,
        IExplorerWindowManager explorerWindowManager,
        IChangeHistoryService changeHistoryService,
        IProcessExecutor processExecutor,
        IDialogService dialogService)
    {
        _localizationService = localizationService;
        _versionService = versionService;
        _logService = logService;
        _applicationCloseService = applicationCloseService;
        _fileSystemService = fileSystemService;
        _explorerWindowManager = explorerWindowManager;
        _changeHistoryService = changeHistoryService;
        _processExecutor = processExecutor;
        _dialogService = dialogService;
        VersionInfo = "Modune";

        // Subscribe to language changes
        _localizationService.LanguageChanged += OnLanguageChanged;

        InitializeVersionInfo();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _localizationService.LanguageChanged -= OnLanguageChanged;
    }

    /// <summary>
    /// Handles language changes to update localized strings.
    /// </summary>
    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(MenuDocumentation));
        OnPropertyChanged(nameof(MenuReportBug));
        OnPropertyChanged(nameof(MenuCheckForUpdates));
        OnPropertyChanged(nameof(MenuTweakerLogs));
        OnPropertyChanged(nameof(MenuChangeHistory));
        OnPropertyChanged(nameof(MenuTweakerScripts));
        OnPropertyChanged(nameof(MenuCloseTweaker));
    }

    private void InitializeVersionInfo()
    {
        try
        {
            var versionInfo = _versionService.GetCurrentVersion();
            VersionInfo = $"Modune {versionInfo.Version}";
        }
        catch (Exception ex)
        {
            _logService.LogDebug($"[MoreMenuViewModel] Failed to get version info: {ex.Message}");
            VersionInfo = "Modune";
        }
    }

    #region Localized Strings

    public string MenuDocumentation =>
        _localizationService.GetString("Tooltip_Documentation") ?? "Documentation";

    public string MenuReportBug =>
        _localizationService.GetString("Tooltip_ReportBug") ?? "Report a Bug";

    public string MenuCheckForUpdates =>
        _localizationService.GetString("Menu_CheckForUpdates") ?? "Check for Updates";

    public string MenuTweakerLogs =>
        _localizationService.GetString("Menu_TweakerLogs") ?? "Modune Logs";

    public string MenuChangeHistory =>
        _localizationService.GetString("Menu_ChangeHistory") ?? "Change History";

    public string MenuTweakerScripts =>
        _localizationService.GetString("Menu_TweakerScripts") ?? "Modune Scripts";

    public string MenuCloseTweaker =>
        _localizationService.GetString("Menu_CloseTweaker") ?? "Close Modune";

    #endregion

    #region Commands

    [RelayCommand]
    private async Task OpenDocsAsync()
    {
        try
        {
            await Windows.System.Launcher.LaunchUriAsync(
                new Uri("https://tweaker.net/docs/index.html"));
        }
        catch (Exception ex)
        {
            _logService.LogError($"Failed to open documentation page: {ex.Message}", ex);
        }
    }

    [RelayCommand]
    private async Task ReportBugAsync()
    {
        try
        {
            var issueUrl = Environment.GetEnvironmentVariable("MODUNE_ISSUES_URL");
            if (Uri.TryCreate(issueUrl, UriKind.Absolute, out var uri))
                await Windows.System.Launcher.LaunchUriAsync(uri);
        }
        catch (Exception ex)
        {
            _logService.LogError($"Failed to open bug report page: {ex.Message}", ex);
        }
    }

    [RelayCommand]
    private async Task OpenLogsAsync()
    {
        try
        {
            string logsFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Tweaker",
                "Logs");

            if (!_fileSystemService.DirectoryExists(logsFolder))
            {
                _fileSystemService.CreateDirectory(logsFolder);
            }

            await _explorerWindowManager.OpenFolderAsync(logsFolder);
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error opening logs folder: {ex.Message}", ex);
        }
    }

    [RelayCommand]
    private async Task OpenChangeHistoryAsync()
    {
        try
        {
            var path = _changeHistoryService.GetFilePath();
            await _processExecutor.ShellExecuteAsync(path);
        }
        catch (Exception ex)
        {
            _logService.LogError($"Failed to open change history file: {ex.Message}", ex);
        }
    }

    [RelayCommand]
    private async Task OpenScriptsAsync()
    {
        try
        {
            string scriptsFolder = ScriptPaths.ScriptsDirectory;

            if (!_fileSystemService.DirectoryExists(scriptsFolder))
            {
                _fileSystemService.CreateDirectory(scriptsFolder);
            }

            await _explorerWindowManager.OpenFolderAsync(scriptsFolder);
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error opening scripts folder: {ex.Message}", ex);
        }
    }

    [RelayCommand]
    private async Task CloseApplicationAsync()
    {
        try
        {
            _logService.LogInformation("User requested application close from More menu");
            await _applicationCloseService.CheckOperationsAndCloseAsync();
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error closing application: {ex.Message}", ex);
        }
    }

    #endregion
}
