using System;
using System.Threading;
using System.Threading.Tasks;
using Tweaker.Core.Features.Common.Models;

namespace Tweaker.Core.Features.SoftwareApps.Interfaces;

public interface ILegacyCapabilityService
{
    Task<bool> EnableCapabilityAsync(string capabilityName, string? displayName = null, IProgress<TaskProgressDetail>? progress = null, CancellationToken cancellationToken = default);
}
