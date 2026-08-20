using System.Threading.Tasks;
using Tweaker.Core.Features.Common.Models;

namespace Tweaker.Core.Features.Common.Interfaces;

public interface IConfigReviewOrchestrationService
{
    Task EnterReviewModeAsync(UnifiedConfigurationFile config, bool isWindowsDefaults = false);
    Task ApplyReviewedConfigAsync();
    Task CancelReviewModeAsync();
}
