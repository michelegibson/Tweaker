using System.Threading.Tasks;
using Tweaker.Core.Features.Common.Models;

namespace Tweaker.Core.Features.Common.Interfaces;

public interface IPowerCfgApplier
{
    Task<OperationResult> ApplyPowerCfgSettingsAsync(SettingDefinition setting, bool enable, object? value);
}
