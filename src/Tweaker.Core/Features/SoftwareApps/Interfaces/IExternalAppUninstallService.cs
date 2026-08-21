using System;
using System.Threading;
using System.Threading.Tasks;
using Tweaker.Core.Features.Common.Models;
using Tweaker.Core.Features.SoftwareApps.Models;

namespace Tweaker.Core.Features.SoftwareApps.Interfaces;

public interface IExternalAppUninstallService
{
    Task<OperationResult<bool>> UninstallAsync(
        ItemDefinition item,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default);
}

public enum UninstallMethod
{
    None,
    WinGet,
    Chocolatey,
    Registry,
    FileSystem,
    AppX
}
