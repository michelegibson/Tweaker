// File: src/Tweaker.Infrastructure/Features/Common/Services/SpecialDiscoveryRegistry.cs
using System.Collections.Generic;
using Tweaker.Core.Features.Common.Interfaces;

namespace Tweaker.Infrastructure.Features.Common.Services;

public sealed class SpecialDiscoveryRegistry(IReadOnlyList<ISpecialSettingHandler> handlers)
    : ISpecialDiscoveryRegistry
{
    public IEnumerable<ISpecialSettingHandler> All => handlers;
}
