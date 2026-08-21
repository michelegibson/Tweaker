using System;
using System.Threading;
using System.Threading.Tasks;
using Tweaker.Core.Features.Common.Models;

namespace Tweaker.Core.Features.SoftwareApps.Interfaces;

public interface IOptionalFeatureService
{
    Task<bool> EnableFeatureAsync(string featureName, string? displayName = null, IProgress<TaskProgressDetail>? progress = null, CancellationToken cancellationToken = default);
}
