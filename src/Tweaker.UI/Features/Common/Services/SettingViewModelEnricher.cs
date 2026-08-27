using Tweaker.Core.Features.Common.Interfaces;
using Tweaker.Core.Features.Common.Models;
using Tweaker.UI.Features.Common.Interfaces;
using Tweaker.UI.Features.Optimize.ViewModels;

namespace Tweaker.UI.Features.Common.Services;

/// <summary>
/// Enriches a SettingItemViewModel with hardware detection, cross-group info, and review diff state.
/// </summary>
public class SettingViewModelEnricher : ISettingViewModelEnricher
{
    private readonly IHardwareDetectionService _hardwareDetectionService;
    private readonly ISettingLocalizationService _settingLocalizationService;
    private readonly ISettingReviewDiffApplier _reviewDiffApplier;

    public SettingViewModelEnricher(
        IHardwareDetectionService hardwareDetectionService,
        ISettingLocalizationService settingLocalizationService,
        ISettingReviewDiffApplier reviewDiffApplier)
    {
        _hardwareDetectionService = hardwareDetectionService;
        _settingLocalizationService = settingLocalizationService;
        _reviewDiffApplier = reviewDiffApplier;
    }

    /// <inheritdoc />
    public async Task DetectBatteryAsync(SettingItemViewModel viewModel)
    {
        viewModel.HasBattery = await _hardwareDetectionService.HasBatteryAsync();
    }

    /// <inheritdoc />
    public void SetCrossGroupInfoMessage(SettingItemViewModel viewModel, SettingDefinition setting)
    {
        viewModel.CrossGroupInfoMessage = _settingLocalizationService.BuildCrossGroupInfoMessage(setting);
    }

    /// <inheritdoc />
    public void ApplyReviewDiff(SettingItemViewModel viewModel, SettingStateResult currentState)
    {
        _reviewDiffApplier.ApplyReviewDiffToViewModel(viewModel, currentState);
    }
}
