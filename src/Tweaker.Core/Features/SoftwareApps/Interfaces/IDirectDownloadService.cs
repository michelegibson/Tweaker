using System;
using System.Threading;
using System.Threading.Tasks;
using Tweaker.Core.Features.Common.Models;
using Tweaker.Core.Features.SoftwareApps.Models;

namespace Tweaker.Core.Features.SoftwareApps.Interfaces;

public interface IDirectDownloadService
{
    Task<bool> DownloadAndInstallAsync(
        ItemDefinition item,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default);
}
