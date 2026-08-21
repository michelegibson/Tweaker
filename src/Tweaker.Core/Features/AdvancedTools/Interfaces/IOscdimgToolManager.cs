using System;
using System.Threading;
using System.Threading.Tasks;
using Tweaker.Core.Features.Common.Models;

namespace Tweaker.Core.Features.AdvancedTools.Interfaces;

public interface IOscdimgToolManager
{
    string GetOscdimgPath();

    Task<bool> IsOscdimgAvailableAsync();

    Task<bool> EnsureOscdimgAvailableAsync(
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default);
}
