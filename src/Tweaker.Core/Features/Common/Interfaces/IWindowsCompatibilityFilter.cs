using System.Collections.Generic;
using Tweaker.Core.Features.Common.Models;

namespace Tweaker.Core.Features.Common.Interfaces;

public interface IWindowsCompatibilityFilter
{
    IEnumerable<SettingDefinition> FilterSettingsByWindowsVersion(
        IEnumerable<SettingDefinition> settings
    );

    IEnumerable<SettingDefinition> FilterSettingsByWindowsVersion(
        IEnumerable<SettingDefinition> settings,
        bool applyFilter
    );
}
