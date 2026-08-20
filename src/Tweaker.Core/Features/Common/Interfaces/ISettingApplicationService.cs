using System.Threading.Tasks;
using Tweaker.Core.Features.Common.Models;

namespace Tweaker.Core.Features.Common.Interfaces;

public interface ISettingApplicationService
{
    Task<OperationResult> ApplySettingAsync(ApplySettingRequest request);
    Task ApplyRecommendedSettingsForFeatureAsync(string settingId);
}
