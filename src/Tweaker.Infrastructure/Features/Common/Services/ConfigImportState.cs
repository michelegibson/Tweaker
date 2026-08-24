using Tweaker.Core.Features.Common.Interfaces;

namespace Tweaker.Infrastructure.Features.Common.Services;

public class ConfigImportState : IConfigImportState
{
    public bool IsActive { get; set; }
    public string? SourceName { get; set; }
    public bool ImportSuppliesPowerValues { get; set; }
}
