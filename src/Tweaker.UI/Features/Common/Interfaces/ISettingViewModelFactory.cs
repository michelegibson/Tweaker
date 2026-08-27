using Tweaker.Core.Features.Common.Models;
using Tweaker.UI.Features.Optimize.ViewModels;

namespace Tweaker.UI.Features.Common.Interfaces;

/// <summary>
/// Creates fully-configured SettingItemViewModel instances from setting definitions.
/// </summary>
public interface ISettingViewModelFactory
{
    /// <summary>
    /// Creates a fully-configured SettingItemViewModel for the given setting definition and current state.
    /// </summary>
    Task<SettingItemViewModel> CreateAsync(
        SettingDefinition setting,
        SettingStateResult currentState,
        ISettingsFeatureViewModel? parentViewModel);
}
