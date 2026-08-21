using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tweaker.Core.Features.Common.Models;
using Tweaker.Core.Features.SoftwareApps.Enums;
using Tweaker.Core.Features.SoftwareApps.Models;

namespace Tweaker.Core.Features.SoftwareApps.Interfaces;

public interface IBloatRemovalService
{
    Task<RemovalOutcome> ExecuteDedicatedScriptAsync(ItemDefinition app,
        IProgress<TaskProgressDetail>? progress = null, CancellationToken ct = default);

    Task<RemovalOutcome> ExecuteBloatRemovalAsync(List<ItemDefinition> apps,
        IProgress<TaskProgressDetail>? progress = null, CancellationToken ct = default);

    Task PersistRemovalScriptsAsync(List<ItemDefinition> allApps);
    Task CleanupAllRemovalArtifactsAsync();

    Task<bool> RemoveItemsFromScriptAsync(List<ItemDefinition> itemsToRemove);
}
