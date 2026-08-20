using System.Threading.Tasks;

namespace Tweaker.Core.Features.Common.Interfaces;

public interface IHardwareDetectionService
{
    Task<bool> HasBatteryAsync();
    Task<bool> HasLidAsync();
    Task<bool> SupportsBrightnessControlAsync();
    Task<bool> SupportsHybridSleepAsync();
}
