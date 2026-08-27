using System.Collections.ObjectModel;
using Tweaker.Core.Features.Common.Models;
using Tweaker.UI.Features.Optimize.ViewModels;

namespace Tweaker.UI.Features.Common.Interfaces;

/// <summary>
/// Service for loading setting ViewModels and refreshing their states.
/// </summary>
public interface ISettingsLoadingService
{
    /// <summary>
    /// Loads settings for a feature and creates ViewModels for each.
    /// </summary>
    Task<ObservableCollection<SettingItemViewModel>> LoadConfiguredSettingsAsync(
        string featureModuleId,
        string progressMessage,
        ISettingsFeatureViewModel? parentViewModel = null);

    /// <summary>
    /// Performs a lightweight refresh of setting states by re-reading from the system.
    /// Returns a dictionary of setting ID to current state.
    /// </summary>
    Task<Dictionary<string, SettingStateResult>> RefreshSettingStatesAsync(
        IEnumerable<SettingItemViewModel> settings);
}
