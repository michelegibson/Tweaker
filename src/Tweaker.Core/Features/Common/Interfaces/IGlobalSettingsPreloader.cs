using System.Threading.Tasks;

namespace Tweaker.Core.Features.Common.Interfaces;

public interface IGlobalSettingsPreloader
{
    Task PreloadAllSettingsAsync();
    bool IsPreloaded { get; }
}
