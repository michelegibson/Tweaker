using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tweaker.Core.Features.Common.Enums;
using Tweaker.Core.Features.Common.Interfaces;
using Tweaker.Core.Features.Common.Models;
using Tweaker.UI.Features.SoftwareApps.ViewModels;

namespace Tweaker.UI.Features.SoftwareApps.Services;

/// <summary>
/// Bridges the WIM feature to the SoftwareApps feature by providing
/// selected Windows apps without exposing the concrete ViewModel.
/// </summary>
public class SelectedAppsProvider : ISelectedAppsProvider
{
    private readonly WindowsAppsViewModel _windowsAppsViewModel;

    public SelectedAppsProvider(WindowsAppsViewModel windowsAppsViewModel)
    {
        _windowsAppsViewModel = windowsAppsViewModel;
    }

    public async Task<IReadOnlyList<ConfigurationItem>> GetSelectedWindowsAppsAsync()
    {
        if (!_windowsAppsViewModel.IsInitialized)
            await _windowsAppsViewModel.LoadItemsAsync();

        return _windowsAppsViewModel.Items
            .Where(item => item.IsSelected)
            .Select(item =>
            {
                var configItem = new ConfigurationItem
                {
                    Id = item.Id,
                    Name = item.Name,
                    IsSelected = true,
                    InputType = InputType.Toggle
                };

                if (item.Definition.AppxPackageName?.Length > 0)
                {
                    configItem.AppxPackageName = item.Definition.AppxPackageName;
                }
                else if (!string.IsNullOrEmpty(item.Definition.CapabilityName))
                    configItem.CapabilityName = item.Definition.CapabilityName;
                else if (!string.IsNullOrEmpty(item.Definition.OptionalFeatureName))
                    configItem.OptionalFeatureName = item.Definition.OptionalFeatureName;

                return configItem;
            }).ToList();
    }
}
