using Tweaker.Core.Features.Common.Constants;
using Tweaker.Core.Features.Common.Events;
using Tweaker.Core.Features.Common.Interfaces;
using Tweaker.UI.Features.Common.Interfaces;
using Tweaker.UI.Features.Optimize.Interfaces;
namespace Tweaker.UI.Features.Optimize.ViewModels;

public partial class SoundOptimizationsViewModel : BaseSettingsFeatureViewModel, IOptimizationFeatureViewModel
{
    public override string ModuleId => FeatureIds.Sound;

    protected override string GetDisplayNameKey() => "Feature_Sound_Name";

    public SoundOptimizationsViewModel(
        ISettingsLoadingService settingsLoadingService,
        ILogService logService,
        ILocalizationService localizationService,
        IDispatcherService dispatcherService,
        IEventBus eventBus,
        IApplicationModeService applicationModeService)
        : base(settingsLoadingService, logService, localizationService, dispatcherService, eventBus, applicationModeService)
    {
    }
}
