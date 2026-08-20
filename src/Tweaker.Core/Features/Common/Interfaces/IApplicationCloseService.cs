using Tweaker.Core.Features.Common.Models;

namespace Tweaker.Core.Features.Common.Interfaces;

public interface IApplicationCloseService
{
    Func<Task>? BeforeShutdown { get; set; }
    Task<OperationResult> CheckOperationsAndCloseAsync();
}
