using System.Threading.Tasks;

namespace Tweaker.Core.Features.Common.Interfaces;

public interface IRemovalScriptUpdateService
{
    Task CheckAndUpdateScriptsAsync();
}
