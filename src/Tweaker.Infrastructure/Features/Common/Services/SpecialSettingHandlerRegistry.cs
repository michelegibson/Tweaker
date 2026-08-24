// File: src/Tweaker.Infrastructure/Features/Common/Services/SpecialSettingHandlerRegistry.cs
using System.Collections.Generic;
using Tweaker.Core.Features.Common.Interfaces;

namespace Tweaker.Infrastructure.Features.Common.Services;

public sealed class SpecialSettingHandlerRegistry(IReadOnlyDictionary<string, ISpecialSettingHandler> handlers)
    : ISpecialSettingHandlerRegistry
{
    public ISpecialSettingHandler? TryGet(string settingId)
        => handlers.TryGetValue(settingId, out var h) ? h : null;
}
