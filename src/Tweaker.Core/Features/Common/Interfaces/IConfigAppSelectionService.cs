using System.Collections.Generic;
using System.Threading.Tasks;
using Tweaker.Core.Features.Common.Models;

namespace Tweaker.Core.Features.Common.Interfaces;

public interface IConfigAppSelectionService
{
    Task SelectWindowsAppsFromConfigAsync(ConfigSection windowsAppsSection);
    Task<(bool shouldContinue, bool saveScripts)> ConfirmWindowsAppsRemovalAsync();
    Task ClearWindowsAppsSelectionAsync();
    Task SelectExternalAppsFromConfigAsync(ConfigSection externalAppsSection);
    Task ProcessExternalAppsInstallationAsync(ConfigSection externalAppsSection);
    Task ProcessExternalAppsRemovalAsync(ConfigSection externalAppsSection);
    Task ProcessExternalAppsFromUserSelectionAsync(List<string> selectedAppIds);
}
