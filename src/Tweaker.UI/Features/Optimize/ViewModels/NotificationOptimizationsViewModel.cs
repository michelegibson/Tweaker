using Tweaker.Core.Features.Common.Constants;
using Tweaker.Core.Features.Common.Events;
using Tweaker.Core.Features.Common.Interfaces;
using Tweaker.UI.Features.Common.Interfaces;
using Tweaker.UI.Features.Optimize.Interfaces;
namespace Tweaker.UI.Features.Optimize.ViewModels;

public partial class NotificationOptimizationsViewModel : BaseSettingsFeatureViewModel, IOptimizationFeatureViewModel
{
    public override string ModuleId => FeatureIds.Notifications;

    protected override string GetDisplayNameKey() => "Feature_Notifications_Name";

    public NotificationOptimizationsViewModel(
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
