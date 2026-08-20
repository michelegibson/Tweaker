using Tweaker.Core.Features.Common.Models;

namespace Tweaker.Core.Features.Common.Interfaces;

public interface IConfigMigrationService
{
    void MigrateConfig(UnifiedConfigurationFile config);
}
