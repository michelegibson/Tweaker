using Tweaker.Core.Features.Common.Constants;
using Tweaker.Core.Features.Common.Events;
using Tweaker.Core.Features.Common.Interfaces;
using Tweaker.UI.Features.Common.Interfaces;
using Tweaker.UI.Features.Customize.Interfaces;
using Tweaker.UI.Features.Optimize.ViewModels;
using ISettingsLoadingService = Tweaker.UI.Features.Common.Interfaces.ISettingsLoadingService;

namespace Tweaker.UI.Features.Customize.ViewModels;

public partial class StartMenuCustomizationsViewModel : BaseSettingsFeatureViewModel, ICustomizationFeatureViewModel
{
    public StartMenuCustomizationsViewModel(
        ISettingsLoadingService settingsLoadingService,
        ILogService logService,
        ILocalizationService localizationService,
        IDispatcherService dispatcherService,
        IEventBus eventBus,
        IApplicationModeService applicationModeService)
        : base(settingsLoadingService, logService, localizationService, dispatcherService, eventBus, applicationModeService)
    {
    }

    public override string ModuleId => FeatureIds.StartMenu;

    protected override string GetDisplayNameKey() => "Feature_StartMenu_Name";
}
