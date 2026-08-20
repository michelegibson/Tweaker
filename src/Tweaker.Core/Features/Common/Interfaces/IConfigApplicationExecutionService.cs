using System.Collections.Generic;
using System.Threading.Tasks;
using Tweaker.Core.Features.Common.Models;

namespace Tweaker.Core.Features.Common.Interfaces;

public interface IConfigApplicationExecutionService
{
    Task ExecuteConfigImportAsync(UnifiedConfigurationFile config, ImportOptions options);
    Task ApplyConfigurationWithOptionsAsync(
        UnifiedConfigurationFile config,
        List<string> selectedSections,
        ImportOptions options);
}
