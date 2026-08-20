using Tweaker.Core.Features.Common.Models;

namespace Tweaker.Core.Features.Common.Interfaces;

public interface IPowerSettingsValidationService
{
    Task<IEnumerable<SettingDefinition>> FilterSettingsByExistenceAsync(IEnumerable<SettingDefinition> settings);
}