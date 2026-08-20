using System.Threading.Tasks;
using Tweaker.Core.Features.Common.Models;

namespace Tweaker.Core.Features.Common.Interfaces;

public interface IScriptMigrationService
{
    Task<ScriptMigrationResult> MigrateFromOldPathsAsync();
}
