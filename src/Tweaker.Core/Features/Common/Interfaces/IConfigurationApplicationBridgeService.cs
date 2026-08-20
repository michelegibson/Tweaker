using System;
using System.Threading.Tasks;
using Tweaker.Core.Features.Common.Models;

namespace Tweaker.Core.Features.Common.Interfaces;

public interface IConfigurationApplicationBridgeService
{
    Task<bool> ApplyConfigurationSectionAsync(
        ConfigSection section,
        string sectionName,
        Func<string, object?, SettingDefinition, Task<(bool confirmed, bool checkboxResult)>>? confirmationHandler = null);
}
