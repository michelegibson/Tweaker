using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Tweaker.Core.Features.Common.Enums;
using Tweaker.Core.Features.Common.Models;
using Tweaker.Core.Features.Customize.Models;
using Tweaker.Core.Features.Optimize.Models;
using Xunit;

namespace Tweaker.UI.Tests.Services;

public class SettingCatalogValidatorTests
{
    // PowerCfg-backed Selection settings have per-mode Recommended/Default state via
    // PowerRecommendation (RecommendedOptionAC/DC) + PowerCfgSetting.RecommendedValueAC/DC /
    // DefaultValueAC/DC, not via ComboBoxOption.IsRecommended/IsDefault flags. A single flag can't
    // encode distinct AC/DC recommendations, so they're exempt from the single-flag validator rules.
    // The universal rules (Options set, no duplicate DisplayNames) still apply.
    private static bool IsPowerCfgBacked(SettingDefinition s) =>
        s.PowerCfgSettings is { Count: > 0 };

    public static IEnumerable<object[]> AllSettings() =>
        CollectAllSettings().Select(s => new object[] { s.Id, s });

    private static IEnumerable<SettingDefinition> CollectAllSettings() =>
        new[]
        {
            GamingAndPerformanceOptimizations.GetGamingAndPerformanceOptimizations().Settings,
            NotificationOptimizations.GetNotificationOptimizations().Settings,
            PowerOptimizations.GetPowerOptimizations().Settings,
            PrivacyAndSecurityOptimizations.GetPrivacyAndSecurityOptimizations().Settings,
            SoundOptimizations.GetSoundOptimizations().Settings,
            UpdateOptimizations.GetUpdateOptimizations().Settings,
            ExplorerCustomizations.GetExplorerCustomizations().Settings,
            StartMenuCustomizations.GetStartMenuCustomizations().Settings,
            TaskbarCustomizations.GetTaskbarCustomizations().Settings,
            WindowsThemeCustomizations.GetWindowsThemeCustomizations().Settings,
        }.SelectMany(l => l);

    [Theory]
    [MemberData(nameof(AllSettings))]
    public void Selection_ComboBoxMetadata_HasOptions(string id, SettingDefinition s)
    {
        if (s.InputType != InputType.Selection) return;
        // LoadDynamicOptions Selection settings (e.g. power-plan-selection) populate ComboBox
        // options at runtime from system state; no static ComboBoxMetadata.
        if (s.Recommendation is { LoadDynamicOptions: true }) return;
        s.ComboBox.Should().NotBeNull($"{id} is Selection and must have ComboBoxMetadata");
        s.ComboBox!.Options.Should().NotBeNull($"{id} ComboBoxMetadata.Options must be set (migrated)");
        s.ComboBox.Options.Should().NotBeEmpty($"{id} ComboBoxMetadata.Options must not be empty");
    }

    [Theory]
    [MemberData(nameof(AllSettings))]
    public void Selection_HasAtLeastOneDefault(string id, SettingDefinition s)
    {
        if (s.InputType != InputType.Selection || s.ComboBox?.Options is null) return;
        // PowerCfg-backed Selection: Default state lives on PowerCfgSetting.DefaultValueAC/DC per power mode.
        if (IsPowerCfgBacked(s)) return;
        var defaults = s.ComboBox.Options.Count(o => o.IsDefault);
        // Subjective settings whose Windows factory default varies by locale (measurement-system,
        // currency-decimal, etc.) flag MULTIPLE options as IsDefault — each is a default in some
        // locale. Non-subjective settings still expect exactly one.
        if (s.IsSubjectivePreference)
            defaults.Should().BeGreaterThanOrEqualTo(1, $"{id} (subjective) must have at least one option with IsDefault = true");
        else
            defaults.Should().Be(1, $"{id} must have exactly one option with IsDefault = true");
    }

    [Theory]
    [MemberData(nameof(AllSettings))]
    public void Selection_AtMostOneRecommended(string id, SettingDefinition s)
    {
        if (s.InputType != InputType.Selection || s.ComboBox?.Options is null) return;
        // PowerCfg-backed Selection: Recommended state lives on PowerRecommendation (AC/DC) +
        // PowerCfgSetting.RecommendedValueAC/DC. AC and DC can recommend different options.
        if (IsPowerCfgBacked(s)) return;
        var recommended = s.ComboBox.Options.Count(o => o.IsRecommended);
        recommended.Should().BeLessThanOrEqualTo(1, $"{id} must have at most one option with IsRecommended = true");
    }

    // NOTE: Under the multi-badge model, IsSubjectivePreference + IsRecommended can coexist.
    // The Preference pill says "this is a matter of taste"; the Recommended pill says "but Tweaker
    // suggests this option." Both pills display independently. A subjective setting MAY carry a
    // Tweaker recommendation — there is no constraint preventing it.
    // IsSubjectivePreference applies to any InputType (Toggle, CheckBox, Selection, NumericRange, ...),
    // not just Selection. For Toggles/CheckBoxes it signals "user choice — Tweaker makes no
    // recommendation," which is why Toggle_HasRecommendation below exempts subjective settings.

    [Theory]
    [MemberData(nameof(AllSettings))]
    public void Selection_NoDuplicateDisplayNames(string id, SettingDefinition s)
    {
        if (s.InputType != InputType.Selection || s.ComboBox?.Options is null) return;
        var names = s.ComboBox.Options.Select(o => o.DisplayName).ToList();
        names.Should().OnlyHaveUniqueItems($"{id} ComboBox options must have unique DisplayNames");
    }

    /// <summary>
    /// Every Toggle/CheckBox SettingDefinition that has registry-backed state must declare a
    /// Tweaker recommendation — either via the toggle-level <see cref="SettingDefinition.RecommendedToggleState"/>
    /// flag or via at least one <see cref="RegistrySetting.RecommendedValue"/>. Catches future
    /// drift where someone adds a toggle without filling in the recommendation.
    /// Settings backed only by ScheduledTask / PowerCfg / NativePowerApi / PowerShellScripts /
    /// RegContents are exempt (they carry their recommendation on those models, not here).
    /// Settings flagged <see cref="SettingDefinition.IsSubjectivePreference"/> are also exempt — the
    /// flag explicitly means "user choice, no Tweaker recommendation."
    /// </summary>
    [Theory]
    [MemberData(nameof(AllSettings))]
    public void Toggle_HasRecommendation(string id, SettingDefinition s)
    {
        if (s.InputType != InputType.Toggle && s.InputType != InputType.CheckBox) return;
        if (s.RegistrySettings is null || s.RegistrySettings.Count == 0) return;
        if (s.IsSubjectivePreference) return;

        bool hasToggleLevelFlag = s.RecommendedToggleState.HasValue;
        bool hasPerKeyValue = s.RegistrySettings.Any(r => r.RecommendedValue != null);

        (hasToggleLevelFlag || hasPerKeyValue).Should().BeTrue(
            $"{id} is a Toggle/CheckBox and must declare a Tweaker recommendation — set " +
            $"SettingDefinition.RecommendedToggleState or at least one RegistrySetting.RecommendedValue");
    }

    [Theory]
    [MemberData(nameof(AllSettings))]
    public void Selection_RegistrySettings_RecommendedAndDefaultValue_MustBeNull(string id, SettingDefinition s)
    {
        if (s.InputType != InputType.Selection || s.RegistrySettings is null) return;
        // PowerCfg-backed Selection: their state source is PowerCfgSetting, not RegistrySetting.
        // Guard is vacuously true today (they have no top-level RegistrySettings) but kept for clarity.
        if (IsPowerCfgBacked(s)) return;
        foreach (var reg in s.RegistrySettings)
        {
            reg.RecommendedValue.Should().BeNull(
                $"{id} is Selection - {reg.ValueName ?? "(key-level)"} RecommendedValue must be null (resolved via ComboBoxOption.ValueMappings)");
            reg.DefaultValue.Should().BeNull(
                $"{id} is Selection - {reg.ValueName ?? "(key-level)"} DefaultValue must be null (resolved via ComboBoxOption.ValueMappings)");
        }
    }

    // Autounattend emits each RegContentSetting into exactly one pass (SYSTEM or user) based on its
    // section headers. A single block mixing hives would be silently truncated under the hive
    // filter, so authors must split into separate RegContentSetting entries per hive.
    private static readonly Regex s_hkcuHeader = new(
        @"^\s*\[HKEY_CURRENT_USER\\",
        RegexOptions.Multiline | RegexOptions.IgnoreCase);
    private static readonly Regex s_systemHiveHeader = new(
        @"^\s*\[(HKEY_LOCAL_MACHINE|HKEY_CLASSES_ROOT|HKEY_USERS|HKEY_CURRENT_CONFIG)\\",
        RegexOptions.Multiline | RegexOptions.IgnoreCase);

    [Theory]
    [MemberData(nameof(AllSettings))]
    public void RegContents_DoNotMixHives(string id, SettingDefinition s)
    {
        if (s.RegContents is null) return;
        foreach (var rc in s.RegContents)
        {
            foreach (var content in new[] { rc.EnabledContent, rc.DisabledContent })
            {
                if (string.IsNullOrEmpty(content)) continue;
                bool hasHkcu = s_hkcuHeader.IsMatch(content);
                bool hasSystem = s_systemHiveHeader.IsMatch(content);
                (hasHkcu && hasSystem).Should().BeFalse(
                    $"{id} RegContentSetting mixes HKEY_CURRENT_USER and system-hive section headers " +
                    $"in a single block. Split into one RegContentSetting per hive so each can be " +
                    $"routed to the correct autounattend pass.");
            }
        }
    }

    // PowerShellScripts with RunContext.System cannot touch HKCU (runs as SYSTEM in specialize pass
    // where HKCU resolves to SYSTEM's empty profile hive). Catches future drift where someone adds
    // an HKCU-touching script without marking it RunContext.User.
    [Theory]
    [MemberData(nameof(AllSettings))]
    public void PowerShellScripts_HkcuReferences_MustDeclareUserRunContext(string id, SettingDefinition s)
    {
        if (s.PowerShellScripts is null) return;
        foreach (var ps in s.PowerShellScripts)
        {
            foreach (var script in new[] { ps.EnabledScript, ps.DisabledScript, ps.Script })
            {
                if (string.IsNullOrEmpty(script)) continue;
                bool touchesHkcu =
                    script.Contains("HKCU:", System.StringComparison.OrdinalIgnoreCase)
                    || script.Contains("HKEY_CURRENT_USER", System.StringComparison.OrdinalIgnoreCase);
                if (touchesHkcu)
                {
                    ps.RunContext.Should().Be(RunContext.User,
                        $"{id} PowerShellScript references HKCU but is marked RunContext.System. " +
                        $"It would run as SYSTEM in specialize pass where HKCU is not the user's hive.");
                }
            }
        }
    }
}
