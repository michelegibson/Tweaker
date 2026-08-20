using Tweaker.Core.Features.Common.Models;

namespace Tweaker.Core.Features.Common.Interfaces;

public interface IHardwareCompatibilityFilter
{
    Task<IEnumerable<SettingDefinition>> FilterSettingsByHardwareAsync(IEnumerable<SettingDefinition> settings);
}