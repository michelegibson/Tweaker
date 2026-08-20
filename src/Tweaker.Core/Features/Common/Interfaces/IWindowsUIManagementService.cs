using System.Threading.Tasks;
using Tweaker.Core.Features.Common.Enums;
using Tweaker.Core.Features.Common.Models;

namespace Tweaker.Core.Features.Common.Interfaces;

public interface IWindowsUIManagementService
{
    bool IsProcessRunning(string processName);
    void KillProcess(string processName);
    Task<OperationResult> RefreshWindowsGUI(bool killExplorer = true);
    void BroadcastRegionalSettingChange();
}
