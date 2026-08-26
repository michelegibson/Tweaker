using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Tweaker.Core.Features.Common.Constants;
using Tweaker.Core.Features.Common.Enums;
using Tweaker.Core.Features.Common.Events;
using Tweaker.Core.Features.Common.Interfaces;
using Tweaker.Core.Features.Common.Models;
using Tweaker.Core.Features.Common.Native;
using Tweaker.Core.Features.Common.Utils;
using Tweaker.Core.Features.Optimize.Interfaces;
using Tweaker.Core.Features.Optimize.Models;
using Tweaker.Infrastructure.Features.Common.Services;

namespace Tweaker.Infrastructure.Features.Optimize.Services;

public class PowerService(
    ILogService logService,
    IPowerSettingsQueryService powerSettingsQueryService,
    ICompatibleSettingsRegistry compatibleSettingsRegistry,
    IEventBus eventBus,
    IPowerPlanComboBoxService powerPlanComboBoxService,
    IProcessExecutor processExecutor,
    IFileSystemService fileSystemService,
    IPowerSchemeOperations powerSchemeOperations,
    IConfigImportState configImportState) : IPowerService, ISpecialSettingHandler
{
    private volatile IEnumerable<SettingDefinition>? _cachedSettings;
    private readonly object _cacheLock = new object();

    private enum ModunePlanRepairOutcome
    {
        Ready,
        LegacyPreserved,
        Removed,
        Failed
    }

    /// <summary>
    /// Attempts to apply a special (non-registry) setting. For power-plan-selection,
    /// delegates to plan import/activation logic.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the setting was handled and applied successfully;
    /// <see langword="false"/> if the setting is not a special setting, the value type
    /// is unsupported, or the operation failed.
    /// Never throws for expected business failures; errors are logged internally.
    /// </returns>
    public async Task<bool> TryApplySpecialSettingAsync(SettingDefinition setting, object value, bool additionalContext = false, ISettingApplicationService? settingApplicationService = null)
    {
        if (setting.Id == SettingIds.PowerPlanSelection)
        {
            logService.Log(LogLevel.Info, "[PowerService] Applying power-plan-selection");

            if (value is Dictionary<string, object> planDict)
            {
                var guid = planDict["Guid"].ToString()!;
                var name = planDict["Name"].ToString()!;

                logService.Log(LogLevel.Info, $"[PowerService] Config import: applying power plan {name} ({guid})");
                return await ApplyPowerPlanByGuidAsync(setting, guid, name, settingApplicationService).ConfigureAwait(false);
            }

            if (value is int index)
            {
                logService.Log(LogLevel.Info, $"[PowerService] UI selection: applying power plan at index {index}");

                var resolution = await powerPlanComboBoxService.ResolvePowerPlanByIndexAsync(index).ConfigureAwait(false);
                if (!resolution.Success)
                {
                    logService.Log(LogLevel.Error, $"[PowerService] Failed to resolve power plan index: {resolution.ErrorMessage}");
                    return false;
                }

                return await ApplyPowerPlanSelectionAsync(setting, resolution.Guid, index, resolution.DisplayName, settingApplicationService).ConfigureAwait(false);
            }

            logService.Log(LogLevel.Error, $"[PowerService] Invalid power plan value type: {value?.GetType().Name}");
            return false;
        }

        return false;
    }

    public async Task<Dictionary<string, Dictionary<string, object?>>> DiscoverSpecialSettingsAsync(IEnumerable<SettingDefinition> settings)
    {
        var results = new Dictionary<string, Dictionary<string, object?>>();

        var powerPlanSetting = settings.FirstOrDefault(s => s.Id == SettingIds.PowerPlanSelection);
        if (powerPlanSetting != null)
        {
            // Migrate the legacy branded plan or repair a corrupt canonical entry before ComboBox setup.
            await CleanupCorruptTweakerPlanAsync().ConfigureAwait(false);

            var activePlan = await GetActivePowerPlanAsync().ConfigureAwait(false);
            var rawValues = new Dictionary<string, object?>
            {
                ["ActivePowerPlan"] = activePlan?.Name,
                ["ActivePowerPlanGuid"] = activePlan?.Guid
            };
            results[SettingIds.PowerPlanSelection] = rawValues;
        }

        return results;
    }

    /// <summary>
    /// Migrates or repairs the branded power plan with the canonical GUID. The legacy
    /// Tweaker name is renamed in place and is never deleted if that rename fails.
    /// Unknown/corrupt entries are deleted only after an in-place repair fails.
    /// </summary>
    private async Task CleanupCorruptTweakerPlanAsync()
    {
        try
        {
            var systemPlans = await powerSettingsQueryService.GetAvailablePowerPlansAsync().ConfigureAwait(false);
            var matchingPlan = systemPlans.FirstOrDefault(p =>
                PowerPlanDefinitions.IsModunePowerPlanGuid(p.Guid));

            if (matchingPlan != null)
            {
                RepairOrRemoveInvalidModunePlan(matchingPlan);
            }
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"[PowerService] Error during Modune plan migration/repair: {ex.Message}");
        }
    }

    private ModunePlanRepairOutcome RepairOrRemoveInvalidModunePlan(PowerPlan plan)
    {
        if (!PowerPlanDefinitions.IsModunePowerPlanGuid(plan.Guid) ||
            string.Equals(plan.Name?.Trim(), PowerPlanDefinitions.ModunePowerPlanName, StringComparison.OrdinalIgnoreCase))
        {
            return ModunePlanRepairOutcome.Ready;
        }

        var schemeGuid = Guid.Parse(PowerPlanDefinitions.ModunePowerPlanGuid);
        var isLegacyAlias = string.Equals(
            plan.Name?.Trim(),
            PowerPlanDefinitions.LegacyTweakerPowerPlanName,
            StringComparison.OrdinalIgnoreCase);

        var repairResult = powerSchemeOperations.WriteFriendlyName(
            schemeGuid,
            PowerPlanDefinitions.ModunePowerPlanName);

        if (repairResult == PowerProf.ERROR_SUCCESS)
        {
            logService.Log(
                LogLevel.Info,
                isLegacyAlias
                    ? $"[PowerService] Renamed legacy '{PowerPlanDefinitions.LegacyTweakerPowerPlanName}' to '{PowerPlanDefinitions.ModunePowerPlanName}' in place"
                    : $"[PowerService] Repaired branded power plan name '{plan.Name}' as '{PowerPlanDefinitions.ModunePowerPlanName}'");
            powerSettingsQueryService.InvalidateCache();
            return ModunePlanRepairOutcome.Ready;
        }

        if (isLegacyAlias)
        {
            logService.Log(
                LogLevel.Warning,
                $"[PowerService] Could not rename legacy power plan (error 0x{repairResult:X8}); preserving it without deletion");
            return ModunePlanRepairOutcome.LegacyPreserved;
        }

        logService.Log(
            LogLevel.Warning,
            $"[PowerService] Failed to repair corrupt Modune plan '{plan.Name}' (error 0x{repairResult:X8}); attempting safe cleanup");

        if (plan.IsActive)
        {
            var balancedGuid = Guid.Parse("381b4222-f694-41f0-9685-ff5bb260df2e");
            var activateResult = powerSchemeOperations.SetActiveScheme(balancedGuid);
            if (activateResult != PowerProf.ERROR_SUCCESS)
            {
                logService.Log(
                    LogLevel.Warning,
                    $"[PowerService] Could not switch to Balanced (error 0x{activateResult:X8}); corrupt plan will not be deleted");
                return ModunePlanRepairOutcome.Failed;
            }

            logService.Log(LogLevel.Info, "[PowerService] Switched to Balanced before deleting corrupt Modune plan");
        }

        var deleteResult = powerSchemeOperations.DeleteScheme(schemeGuid);
        if (deleteResult == PowerProf.ERROR_SUCCESS)
        {
            logService.Log(LogLevel.Info, "[PowerService] Successfully deleted corrupt Modune plan after repair failed");
            powerSettingsQueryService.InvalidateCache();
            return ModunePlanRepairOutcome.Removed;
        }

        logService.Log(LogLevel.Warning, $"[PowerService] Failed to delete corrupt Modune plan: error 0x{deleteResult:X8}");
        return ModunePlanRepairOutcome.Failed;
    }

    /// <summary>
    /// Returns the cached power settings, loading them on first call. Used internally
    /// by ApplyRecommendedSettingsToPlanAsync when populating a freshly-imported plan.
    /// </summary>
    /// <returns>
    /// The filtered settings for the power feature, or an empty enumerable
    /// if loading fails (failure is logged, never thrown).
    /// </returns>
    private Task<IEnumerable<SettingDefinition>> GetSettingsAsync()
    {
        if (_cachedSettings != null)
            return Task.FromResult(_cachedSettings);

        lock (_cacheLock)
        {
            if (_cachedSettings != null)
                return Task.FromResult(_cachedSettings);

            try
            {
                logService.Log(LogLevel.Info, "Loading Power settings");
                _cachedSettings = compatibleSettingsRegistry.GetFilteredSettings(FeatureIds.Power);
                return Task.FromResult(_cachedSettings);
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Error, $"Error loading Power settings: {ex.Message}");
                return Task.FromResult(Enumerable.Empty<SettingDefinition>());
            }
        }
    }

    private void InvalidateCache()
    {
        lock (_cacheLock)
        {
            _cachedSettings = null;
        }
    }


    /// <summary>
    /// Gets the currently active power plan.
    /// </summary>
    /// <returns>
    /// The active <see cref="PowerPlan"/>, or <see langword="null"/> if the
    /// query fails (failure is logged as a warning, never thrown).
    /// </returns>
    public async Task<PowerPlan?> GetActivePowerPlanAsync()
    {
        try
        {
            return await powerSettingsQueryService.GetActivePowerPlanAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"Error getting active power plan: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Gets all power plans available on the system.
    /// </summary>
    /// <returns>
    /// A list of power plan objects, or an empty enumerable if the query
    /// fails (failure is logged as a warning, never thrown).
    /// </returns>
    public async Task<IEnumerable<object>> GetAvailablePowerPlansAsync()
    {
        try
        {
            var powerPlans = await powerSettingsQueryService.GetAvailablePowerPlansAsync().ConfigureAwait(false);
            return powerPlans.Cast<object>().ToList();
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"Error getting available power plans: {ex.Message}");
            return Enumerable.Empty<object>();
        }
    }


    private async Task<bool> SetActivePowerPlanAsync(string powerPlanGuid)
    {
        try
        {
            var currentActivePlan = await powerSettingsQueryService.GetActivePowerPlanAsync().ConfigureAwait(false);
            if (currentActivePlan != null && string.Equals(currentActivePlan.Guid, powerPlanGuid, StringComparison.OrdinalIgnoreCase))
            {
                logService.Log(LogLevel.Info, $"Power plan {powerPlanGuid} is already active, skipping application");
                return true;
            }

            var schemeGuid = Guid.Parse(powerPlanGuid);
            var result = powerSchemeOperations.SetActiveScheme(schemeGuid);

            if (result == PowerProf.ERROR_SUCCESS)
            {
                powerSettingsQueryService.InvalidateCache();
                return true;
            }

            logService.Log(LogLevel.Warning, $"PowerSetActiveScheme failed with code {result}");
            return false;
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"Error setting active power plan: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Deletes a power plan by its GUID.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the plan was deleted;
    /// <see langword="false"/> if the plan is active, deletion failed, or an
    /// error occurred (all failures are logged, never thrown).
    /// </returns>
    public async Task<bool> DeletePowerPlanAsync(string powerPlanGuid)
    {
        try
        {
            logService.Log(LogLevel.Info, $"Attempting to delete power plan: {powerPlanGuid}");

            var activePlan = await GetActivePowerPlanAsync().ConfigureAwait(false);
            if (activePlan != null && string.Equals(activePlan.Guid, powerPlanGuid, StringComparison.OrdinalIgnoreCase))
            {
                logService.Log(LogLevel.Warning, "Cannot delete active power plan");
                return false;
            }

            var schemeGuid = Guid.Parse(powerPlanGuid);
            var result = powerSchemeOperations.DeleteScheme(schemeGuid);

            if (result == PowerProf.ERROR_SUCCESS)
            {
                powerSettingsQueryService.InvalidateCache();
                logService.Log(LogLevel.Info, $"Successfully deleted power plan: {powerPlanGuid}");
                return true;
            }
            else
            {
                logService.Log(LogLevel.Error, $"Failed to delete power plan: {powerPlanGuid}. Error code: {result}");
                return false;
            }
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"Error deleting power plan: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Applies a power plan selected via the UI combo box.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the plan was activated successfully;
    /// <see langword="false"/> if the plan could not be imported, found, or activated.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="powerPlanGuid"/> is null or empty (programmer error).
    /// </exception>
    private async Task<bool> ApplyPowerPlanSelectionAsync(SettingDefinition setting, string powerPlanGuid, int planIndex, string planName, ISettingApplicationService? settingApplicationService)
    {
        logService.Log(LogLevel.Info, $"[PowerService] Applying power plan: {planName} ({powerPlanGuid})");

        if (string.IsNullOrEmpty(powerPlanGuid))
        {
            throw new ArgumentException("Power plan GUID cannot be null or empty");
        }

        var previousPlan = await GetActivePowerPlanAsync().ConfigureAwait(false);

        var systemPlans = await powerSettingsQueryService.GetAvailablePowerPlansAsync().ConfigureAwait(false);
        var existingSystemPlan = systemPlans.FirstOrDefault(p => string.Equals(p.Guid, powerPlanGuid, StringComparison.OrdinalIgnoreCase));
        var planExists = existingSystemPlan != null;

        // Migrate a legacy name in place. A corrupt entry is removed only if repair fails.
        if (planExists && IsTweakerPowerPlan(powerPlanGuid) &&
            !string.Equals(existingSystemPlan!.Name?.Trim(), PowerPlanDefinitions.ModunePowerPlanName, StringComparison.OrdinalIgnoreCase))
        {
            var repairOutcome = RepairOrRemoveInvalidModunePlan(existingSystemPlan);
            if (repairOutcome == ModunePlanRepairOutcome.Removed)
            {
                planExists = false;
            }
            else if (repairOutcome == ModunePlanRepairOutcome.Failed)
            {
                return false;
            }
        }

        bool success = false;

        if (!planExists)
        {
            var predefinedPlan = PowerPlanDefinitions.BuiltInPowerPlans
                .FirstOrDefault(p => string.Equals(p.Guid, powerPlanGuid, StringComparison.OrdinalIgnoreCase));

            if (predefinedPlan != null)
            {
                logService.Log(LogLevel.Info, $"[PowerService] Plan '{predefinedPlan.Name}' not found, attempting import");
                var importResult = await ImportPowerPlanAsync(predefinedPlan).ConfigureAwait(false);

                if (importResult.Success)
                {
                    logService.Log(LogLevel.Info, $"[PowerService] Successfully imported '{predefinedPlan.Name}', activating");
                    await Task.Delay(200).ConfigureAwait(false);

                    var importedSchemeGuid = Guid.Parse(importResult.ImportedGuid);
                    var activateResult = powerSchemeOperations.SetActiveScheme(importedSchemeGuid);
                    success = activateResult == PowerProf.ERROR_SUCCESS;

                    if (success)
                    {
                        powerSettingsQueryService.InvalidateCache();
                        InvalidateCache();
                        logService.Log(LogLevel.Info, $"[PowerService] Successfully activated imported plan");
                    }
                    else
                    {
                        logService.Log(LogLevel.Warning, $"[PowerService] First activation failed, retrying...");
                        await Task.Delay(500).ConfigureAwait(false);
                        activateResult = powerSchemeOperations.SetActiveScheme(importedSchemeGuid);
                        success = activateResult == PowerProf.ERROR_SUCCESS;

                        if (success)
                        {
                            powerSettingsQueryService.InvalidateCache();
                            InvalidateCache();
                            logService.Log(LogLevel.Info, $"[PowerService] Successfully activated on retry");
                        }
                        else
                        {
                            logService.Log(LogLevel.Error, $"[PowerService] Failed to activate after import. Error code: {activateResult}");
                        }
                    }

                    powerPlanGuid = importResult.ImportedGuid;
                }
                else
                {
                    logService.Log(LogLevel.Error, $"[PowerService] Failed to import plan: {importResult.ErrorMessage}");
                    return false;
                }
            }
            else
            {
                logService.Log(LogLevel.Error, $"[PowerService] Unknown power plan GUID: {powerPlanGuid}");
                return false;
            }
        }
        else
        {
            success = await SetActivePowerPlanAsync(powerPlanGuid).ConfigureAwait(false);
        }

        if (success)
        {
            logService.Log(LogLevel.Info, $"[PowerService] Publishing PowerPlanChangedEvent");

            eventBus.Publish(new PowerPlanChangedEvent
            {
                PreviousPlanGuid = previousPlan?.Guid ?? string.Empty,
                NewPlanGuid = powerPlanGuid,
                NewPlanName = planName,
                NewPlanIndex = planIndex
            });

            if (IsTweakerPowerPlan(powerPlanGuid))
            {
                if (configImportState.IsActive && configImportState.ImportSuppliesPowerValues)
                {
                    logService.Log(LogLevel.Info,
                        "[PowerService] Skipping recommended power re-apply: active config import supplies individual power values");
                }
                else
                {
                    await ApplyTweakerRecommendedSettingsAsync(settingApplicationService).ConfigureAwait(false);
                }
            }

            logService.Log(LogLevel.Info, $"[PowerService] Successfully applied power plan");
        }

        return success;
    }

    /// <summary>
    /// Applies a power plan identified by its GUID (used during config import).
    /// Creates the plan by duplicating Balanced if not found as a predefined or existing plan.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the plan was activated successfully;
    /// <see langword="false"/> if the plan could not be imported, created, or activated.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="powerPlanGuid"/> is null or empty (programmer error).
    /// </exception>
    private async Task<bool> ApplyPowerPlanByGuidAsync(SettingDefinition setting, string powerPlanGuid, string planName, ISettingApplicationService? settingApplicationService)
    {
        if (PowerPlanDefinitions.IsModunePowerPlanGuid(powerPlanGuid))
        {
            planName = PowerPlanDefinitions.ModunePowerPlanName;
        }

        logService.Log(LogLevel.Info, $"[PowerService] Applying power plan by GUID: {planName} ({powerPlanGuid})");

        if (string.IsNullOrEmpty(powerPlanGuid))
        {
            throw new ArgumentException("Power plan GUID cannot be null or empty");
        }

        var previousPlan = await GetActivePowerPlanAsync().ConfigureAwait(false);
        var systemPlans = await powerSettingsQueryService.GetAvailablePowerPlansAsync().ConfigureAwait(false);
        var existingSystemPlan = systemPlans.FirstOrDefault(p =>
            string.Equals(p.Guid, powerPlanGuid, StringComparison.OrdinalIgnoreCase));
        var planExists = existingSystemPlan != null;

        if (planExists && PowerPlanDefinitions.IsModunePowerPlanGuid(powerPlanGuid) &&
            !string.Equals(existingSystemPlan!.Name?.Trim(), PowerPlanDefinitions.ModunePowerPlanName, StringComparison.OrdinalIgnoreCase))
        {
            var repairOutcome = RepairOrRemoveInvalidModunePlan(existingSystemPlan);
            if (repairOutcome == ModunePlanRepairOutcome.Removed)
            {
                planExists = false;
            }
            else if (repairOutcome == ModunePlanRepairOutcome.Failed)
            {
                return false;
            }
        }

        bool success = false;

        if (!planExists)
        {
            logService.Log(LogLevel.Warning, $"[PowerService] Plan '{planName}' ({powerPlanGuid}) not found on system");

            var predefinedPlan = PowerPlanDefinitions.BuiltInPowerPlans
                .FirstOrDefault(p => string.Equals(p.Guid, powerPlanGuid, StringComparison.OrdinalIgnoreCase));

            if (predefinedPlan != null)
            {
                logService.Log(LogLevel.Info, $"[PowerService] Importing predefined plan '{predefinedPlan.Name}'");
                var importResult = await ImportPowerPlanAsync(predefinedPlan).ConfigureAwait(false);

                if (importResult.Success)
                {
                    logService.Log(LogLevel.Info, "[PowerService] Successfully imported, now activating");
                    await Task.Delay(200).ConfigureAwait(false);

                    success = await SetActivePowerPlanAsync(importResult.ImportedGuid).ConfigureAwait(false);
                    powerPlanGuid = importResult.ImportedGuid;
                }
                else
                {
                    logService.Log(LogLevel.Error, $"[PowerService] Failed to import plan: {importResult.ErrorMessage}");
                    return false;
                }
            }
            else
            {
                logService.Log(LogLevel.Info, $"[PowerService] Custom power plan '{planName}' - creating by duplicating Balanced");

                // Clean up any ghost/corrupt plan entry that may block duplication with this GUID
                var targetGuid = Guid.Parse(powerPlanGuid);
                var cleanupResult = powerSchemeOperations.DeleteScheme(targetGuid);
                if (cleanupResult == PowerProf.ERROR_SUCCESS)
                {
                    logService.Log(LogLevel.Info, $"[PowerService] Cleaned up ghost plan entry with GUID {powerPlanGuid}");
                }

                // Use powercfg for specific-GUID duplication (P/Invoke doesn't support destination GUID)
                var (dupSuccess, dupOutput) = await RunPowercfgAsync($"/duplicatescheme 381b4222-f694-41f0-9685-ff5bb260df2e {powerPlanGuid}").ConfigureAwait(false);

                if (dupSuccess)
                {
                    // Parse the actual GUID — powercfg may assign a different one
                    var actualGuid = ParseGuidFromPowercfgOutput(dupOutput) ?? powerPlanGuid;
                    if (!string.Equals(actualGuid, powerPlanGuid, StringComparison.OrdinalIgnoreCase))
                    {
                        logService.Log(LogLevel.Warning, $"[PowerService] powercfg assigned GUID {actualGuid} instead of requested {powerPlanGuid}");
                    }

                    SetPowerPlanName(Guid.Parse(actualGuid), planName);

                    powerSettingsQueryService.InvalidateCache();
                    logService.Log(LogLevel.Info, $"[PowerService] Successfully created custom plan '{planName}' with GUID {actualGuid}");

                    powerPlanGuid = actualGuid;
                    success = await SetActivePowerPlanAsync(powerPlanGuid).ConfigureAwait(false);
                }
                else
                {
                    logService.Log(LogLevel.Error, $"[PowerService] Failed to create custom plan '{planName}' with GUID {powerPlanGuid}");
                    return false;
                }
            }
        }
        else
        {
            success = await SetActivePowerPlanAsync(powerPlanGuid).ConfigureAwait(false);
        }

        if (success)
        {
            var options = await powerPlanComboBoxService.GetPowerPlanOptionsAsync().ConfigureAwait(false);
            var planIndex = options.FindIndex(o =>
                string.Equals(o.SystemPlan?.Guid, powerPlanGuid, StringComparison.OrdinalIgnoreCase));

            eventBus.Publish(new PowerPlanChangedEvent
            {
                PreviousPlanGuid = previousPlan?.Guid ?? string.Empty,
                NewPlanGuid = powerPlanGuid,
                NewPlanName = planName,
                NewPlanIndex = planIndex >= 0 ? planIndex : 0
            });

            if (IsTweakerPowerPlan(powerPlanGuid))
            {
                if (configImportState.IsActive && configImportState.ImportSuppliesPowerValues)
                {
                    logService.Log(LogLevel.Info,
                        "[PowerService] Skipping recommended power re-apply: active config import supplies individual power values");
                }
                else
                {
                    await ApplyTweakerRecommendedSettingsAsync(settingApplicationService).ConfigureAwait(false);
                }
            }

            logService.Log(LogLevel.Info, $"[PowerService] Successfully applied power plan '{planName}'");
        }

        return success;
    }

    /// <summary>
    /// Imports a predefined power plan onto the system. Uses duplication for built-in
    /// plans and falls back to backup/restore when duplication fails.
    /// </summary>
    /// <returns>
    /// A <see cref="PowerPlanImportResult"/> indicating success or failure with an
    /// error message. Never throws; all exceptions are caught and returned as a
    /// failed result.
    /// </returns>
    public async Task<PowerPlanImportResult> ImportPowerPlanAsync(PredefinedPowerPlan predefinedPlan)
    {
        try
        {
            // The branded plan is identified by its stable GUID, not by a display name.
            // This keeps imports from legacy configurations working after the rebrand.
            if (PowerPlanDefinitions.IsModunePowerPlanGuid(predefinedPlan.Guid))
            {
                var canonicalPlan = predefinedPlan with
                {
                    Name = PowerPlanDefinitions.ModunePowerPlanName,
                    Guid = PowerPlanDefinitions.ModunePowerPlanGuid
                };
                return await CreateTweakerPowerPlanAsync(canonicalPlan).ConfigureAwait(false);
            }

            if (predefinedPlan.Name == "Ultimate Performance")
            {
                var systemPlans = await powerSettingsQueryService.GetAvailablePowerPlansAsync().ConfigureAwait(false);
                var existingPlan = systemPlans.FirstOrDefault(p => Common.Utilities.PowerPlanHelper.IsUltimatePerformancePlan(p.Name));

                if (existingPlan != null)
                {
                    logService.Log(LogLevel.Info, $"Ultimate Performance plan already exists with GUID: {existingPlan.Guid}");
                    return new PowerPlanImportResult(true, existingPlan.Guid);
                }

                var sourceGuid = Guid.Parse(predefinedPlan.Guid);
                var dupResult = powerSchemeOperations.DuplicateScheme(sourceGuid, out var newGuid);

                if (dupResult == PowerProf.ERROR_SUCCESS)
                {
                    powerSettingsQueryService.InvalidateCache();

                    var actualGuid = newGuid.ToString("D");

                    if (!string.IsNullOrEmpty(actualGuid))
                    {
                        SetPowerPlanNameAndDescription(newGuid, predefinedPlan.Name, predefinedPlan.Description);
                        return new PowerPlanImportResult(true, actualGuid);
                    }
                }

                return new PowerPlanImportResult(false, "", "Ultimate Performance creation failed");
            }
            else
            {
                var systemPlans = await powerSettingsQueryService.GetAvailablePowerPlansAsync().ConfigureAwait(false);
                var existingPlan = systemPlans.FirstOrDefault(p =>
                    string.Equals(p.Guid, predefinedPlan.Guid, StringComparison.OrdinalIgnoreCase));

                if (existingPlan != null)
                {
                    logService.Log(LogLevel.Info, $"Power plan '{predefinedPlan.Name}' already exists with GUID: {existingPlan.Guid}");
                    return new PowerPlanImportResult(true, existingPlan.Guid);
                }

                logService.Log(LogLevel.Info, $"Attempting to duplicate power plan '{predefinedPlan.Name}' using GUID {predefinedPlan.Guid}");
                var srcGuid = Guid.Parse(predefinedPlan.Guid);
                var duplicateResult = powerSchemeOperations.DuplicateScheme(srcGuid, out var dupNewGuid);

                if (duplicateResult == PowerProf.ERROR_SUCCESS)
                {
                    powerSettingsQueryService.InvalidateCache();

                    var actualGuid = dupNewGuid.ToString("D");

                    if (!string.IsNullOrEmpty(actualGuid))
                    {
                        logService.Log(LogLevel.Info, $"Successfully duplicated power plan '{predefinedPlan.Name}' with GUID: {actualGuid}");
                        return new PowerPlanImportResult(true, actualGuid);
                    }
                }

                logService.Log(LogLevel.Warning, $"Duplicate scheme failed for '{predefinedPlan.Name}', falling back to backup/restore method");
                return await SimpleBackupRestore(predefinedPlan).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            return new PowerPlanImportResult(false, "", ex.Message);
        }
    }

    private async Task<PowerPlanImportResult> SimpleBackupRestore(PredefinedPowerPlan targetPlan)
    {
        var backupDir = Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Tweaker\Backup\PowerPlans");

        try
        {
            await BackupCustomPlansAsync(backupDir).ConfigureAwait(false);

            var restoreResult = PowerProf.PowerRestoreDefaultPowerSchemes();
            if (restoreResult != PowerProf.ERROR_SUCCESS)
                return new PowerPlanImportResult(false, "", "Failed to restore default schemes");

            await Task.Delay(1000).ConfigureAwait(false);
            await RestoreCustomPlansAsync(backupDir).ConfigureAwait(false);

            powerSettingsQueryService.InvalidateCache();

            if (fileSystemService.DirectoryExists(backupDir))
            {
                fileSystemService.DeleteDirectory(backupDir, true);
            }

            var plans = await powerSettingsQueryService.GetAvailablePowerPlansAsync().ConfigureAwait(false);
            var targetGuid = plans.FirstOrDefault(p =>
                string.Equals(Common.Utilities.PowerPlanHelper.CleanPlanName(p.Name), targetPlan.Name, StringComparison.OrdinalIgnoreCase))?.Guid;

            return !string.IsNullOrEmpty(targetGuid)
                ? new PowerPlanImportResult(true, targetGuid)
                : new PowerPlanImportResult(false, "", "Target plan not found after restore");
        }
        catch (Exception ex)
        {
            return new PowerPlanImportResult(false, "", ex.Message);
        }
    }



    private async Task BackupCustomPlansAsync(string backupFolder)
    {
        fileSystemService.CreateDirectory(backupFolder);

        var allPlans = await powerSettingsQueryService.GetAvailablePowerPlansAsync().ConfigureAwait(false);
        var customPlans = IdentifyCustomPlans(allPlans);

        foreach (var plan in customPlans)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var filename = $"{SanitizeFilename(plan.Name)}_{timestamp}.pow";
            var filepath = fileSystemService.CombinePath(backupFolder, filename);

            // PowerExportPowerScheme is not a reliable P/Invoke export, use powercfg
            await RunPowercfgAsync($"/export \"{filepath}\" {plan.Guid}").ConfigureAwait(false);
        }
    }

    private async Task RestoreCustomPlansAsync(string backupFolder)
    {
        if (!fileSystemService.DirectoryExists(backupFolder)) return;

        var backupFiles = fileSystemService.GetFiles(backupFolder, "*.pow");
        foreach (var file in backupFiles)
        {
            var importResult = PowerProf.PowerImportPowerScheme(IntPtr.Zero, file, out var importedPtr);
            if (importResult == PowerProf.ERROR_SUCCESS)
            {
                PowerProf.LocalFree(importedPtr);
            }
            await Task.Delay(200).ConfigureAwait(false);
        }
    }

    private List<PowerPlan> IdentifyCustomPlans(List<PowerPlan> allPlans)
    {
        var builtInGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "a1841308-3541-4fab-bc81-f71556f20b4a",
            "381b4222-f694-41f0-9685-ff5bb260df2e",
            "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"
        };

        var builtInNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Power Saver", "Balanced", "High Performance"
        };

        return allPlans.Where(plan =>
            !builtInGuids.Contains(plan.Guid) ||
            !builtInNames.Contains(Common.Utilities.PowerPlanHelper.CleanPlanName(plan.Name))
        ).ToList();
    }

    private string SanitizeFilename(string filename)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", filename.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
    }

    private async Task<PowerPlanImportResult> CreateTweakerPowerPlanAsync(PredefinedPowerPlan predefinedPlan)
    {
        var ultimatePerformancePlan = PowerPlanDefinitions.BuiltInPowerPlans
            .FirstOrDefault(p => p.Name == "Ultimate Performance");

        if (ultimatePerformancePlan == null)
        {
            return new PowerPlanImportResult(false, "", "Ultimate Performance plan not found");
        }

        try
        {
            var systemPlans = await powerSettingsQueryService.GetAvailablePowerPlansAsync().ConfigureAwait(false);
            var existingPlan = systemPlans.FirstOrDefault(p =>
                string.Equals(p.Guid, predefinedPlan.Guid, StringComparison.OrdinalIgnoreCase));

            if (existingPlan != null)
            {
                var repairOutcome = RepairOrRemoveInvalidModunePlan(existingPlan);
                if (repairOutcome is ModunePlanRepairOutcome.Ready or ModunePlanRepairOutcome.LegacyPreserved)
                {
                    logService.Log(LogLevel.Info, $"{PowerPlanDefinitions.ModunePowerPlanName} already exists with GUID: {existingPlan.Guid}");
                    return new PowerPlanImportResult(true, existingPlan.Guid);
                }

                if (repairOutcome == ModunePlanRepairOutcome.Failed)
                {
                    return new PowerPlanImportResult(false, "", "Existing branded power plan could not be repaired safely");
                }
            }

            // An invisible ghost can still block duplication even though enumeration
            // returned no entry. Try the same in-place repair first; delete only when
            // that repair cannot reach a usable scheme.
            if (existingPlan == null)
            {
                var moduneGuid = Guid.Parse(PowerPlanDefinitions.ModunePowerPlanGuid);
                var orphanRepairResult = powerSchemeOperations.WriteFriendlyName(
                    moduneGuid,
                    PowerPlanDefinitions.ModunePowerPlanName);

                if (orphanRepairResult == PowerProf.ERROR_SUCCESS)
                {
                    powerSchemeOperations.WriteDescription(moduneGuid, predefinedPlan.Description);
                    await ApplyRecommendedSettingsToPlanAsync(PowerPlanDefinitions.ModunePowerPlanGuid).ConfigureAwait(false);
                    powerSettingsQueryService.InvalidateCache();
                    logService.Log(LogLevel.Info, "[PowerService] Repaired non-enumerated Modune power plan in place");
                    return new PowerPlanImportResult(true, PowerPlanDefinitions.ModunePowerPlanGuid);
                }

                var activePlan = await GetActivePowerPlanAsync().ConfigureAwait(false);
                if (activePlan != null && PowerPlanDefinitions.IsModunePowerPlanGuid(activePlan.Guid))
                {
                    var balancedGuid = Guid.Parse("381b4222-f694-41f0-9685-ff5bb260df2e");
                    var activateResult = powerSchemeOperations.SetActiveScheme(balancedGuid);
                    if (activateResult != PowerProf.ERROR_SUCCESS)
                    {
                        logService.Log(LogLevel.Warning,
                            $"[PowerService] Could not switch to Balanced (error 0x{activateResult:X8}); ghost plan will not be deleted");
                        return new PowerPlanImportResult(false, "", "Could not safely deactivate existing branded power plan");
                    }
                }

                var cleanupResult = powerSchemeOperations.DeleteScheme(moduneGuid);
                if (cleanupResult == PowerProf.ERROR_SUCCESS)
                {
                    logService.Log(LogLevel.Info, "[PowerService] Cleaned up ghost Modune power plan entry after repair failed");
                    powerSettingsQueryService.InvalidateCache();
                }
            }

            logService.Log(LogLevel.Info, $"Creating {PowerPlanDefinitions.ModunePowerPlanName} from Ultimate Performance");

            // Use powercfg for specific-GUID duplication (P/Invoke doesn't support destination GUID)
            var (dupSuccess, dupOutput) = await RunPowercfgAsync($"/duplicatescheme {ultimatePerformancePlan.Guid} {PowerPlanDefinitions.ModunePowerPlanGuid}").ConfigureAwait(false);

            if (!dupSuccess)
            {
                logService.Log(LogLevel.Error, $"Failed to duplicate plan for {PowerPlanDefinitions.ModunePowerPlanName}");
                return new PowerPlanImportResult(false, "", "Failed to create plan");
            }

            // Parse the actual GUID from powercfg output — it may differ from the requested one
            var actualGuid = ParseGuidFromPowercfgOutput(dupOutput) ?? PowerPlanDefinitions.ModunePowerPlanGuid;
            if (!string.Equals(actualGuid, PowerPlanDefinitions.ModunePowerPlanGuid, StringComparison.OrdinalIgnoreCase))
            {
                logService.Log(LogLevel.Warning, $"[PowerService] powercfg assigned GUID {actualGuid} instead of requested {PowerPlanDefinitions.ModunePowerPlanGuid}");
            }

            SetPowerPlanNameAndDescription(Guid.Parse(actualGuid), PowerPlanDefinitions.ModunePowerPlanName, predefinedPlan.Description);

            await ApplyRecommendedSettingsToPlanAsync(actualGuid).ConfigureAwait(false);

            powerSettingsQueryService.InvalidateCache();

            logService.Log(LogLevel.Info, $"Successfully created {PowerPlanDefinitions.ModunePowerPlanName}: {actualGuid}");
            return new PowerPlanImportResult(true, actualGuid);
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"Error creating {PowerPlanDefinitions.ModunePowerPlanName}: {ex.Message}");
            return new PowerPlanImportResult(false, "", ex.Message);
        }
    }

    private async Task ApplyRecommendedSettingsToPlanAsync(string planGuid)
    {
        logService.Log(LogLevel.Info, $"Applying recommended settings to plan: {planGuid}");

        try
        {
            var allSettings = await GetSettingsAsync().ConfigureAwait(false);
            int appliedCount = 0;

            foreach (var setting in allSettings)
            {
                try
                {
                    var powerCfgWithRecommended = setting.PowerCfgSettings?.FirstOrDefault(ps =>
                        ps.RecommendedValueAC.HasValue || ps.RecommendedValueDC.HasValue);

                    if (powerCfgWithRecommended != null)
                    {
                        var acValue = powerCfgWithRecommended.RecommendedValueAC ?? powerCfgWithRecommended.RecommendedValueDC ?? 0;
                        var dcValue = powerCfgWithRecommended.RecommendedValueDC ?? powerCfgWithRecommended.RecommendedValueAC ?? 0;

                        logService.Log(LogLevel.Debug, $"Applying {setting.Id} - AC: {acValue}, DC: {dcValue}");

                        var planSchemeGuid = Guid.Parse(planGuid);
                        var subgroupGuid = Guid.Parse(powerCfgWithRecommended.SubgroupGuid);
                        var settGuid = Guid.Parse(powerCfgWithRecommended.SettingGuid);

                        PowerProf.PowerWriteACValueIndex(IntPtr.Zero, ref planSchemeGuid, ref subgroupGuid, ref settGuid, (uint)acValue);
                        PowerProf.PowerWriteDCValueIndex(IntPtr.Zero, ref planSchemeGuid, ref subgroupGuid, ref settGuid, (uint)dcValue);

                        appliedCount++;
                        continue;
                    }

                    if (setting.InputType == InputType.Selection &&
                        setting.Recommendation?.RecommendedOptionAC != null &&
                        setting.PowerCfgSettings?.Any() == true)
                    {
                        var recommendedOptionAC = setting.Recommendation.RecommendedOptionAC;
                        var recommendedOptionDC = setting.Recommendation.RecommendedOptionDC ?? recommendedOptionAC;

                        var options = setting.ComboBox?.Options;

                        if (options != null)
                        {
                            var indexAC = -1;
                            var indexDC = -1;
                            for (int oi = 0; oi < options.Count; oi++)
                            {
                                if (indexAC < 0 && string.Equals(options[oi].DisplayName, recommendedOptionAC, StringComparison.Ordinal))
                                    indexAC = oi;
                                if (indexDC < 0 && string.Equals(options[oi].DisplayName, recommendedOptionDC, StringComparison.Ordinal))
                                    indexDC = oi;
                            }

                            if (options.Any(o => o.ValueMappings != null))
                            {
                                int? acValue = null, dcValue = null;

                                if (indexAC >= 0 && options[indexAC].ValueMappings is { } valueDictAC &&
                                    valueDictAC.TryGetValue("PowerCfgValue", out var powerCfgValueAC) && powerCfgValueAC != null)
                                    acValue = Convert.ToInt32(powerCfgValueAC);

                                if (indexDC >= 0 && options[indexDC].ValueMappings is { } valueDictDC &&
                                    valueDictDC.TryGetValue("PowerCfgValue", out var powerCfgValueDC) && powerCfgValueDC != null)
                                    dcValue = Convert.ToInt32(powerCfgValueDC);

                                if (acValue.HasValue && dcValue.HasValue)
                                {
                                    var powerCfgSetting = setting.PowerCfgSettings[0];

                                    logService.Log(LogLevel.Debug, $"Applying {setting.Id} - AC: {recommendedOptionAC} ({acValue}), DC: {recommendedOptionDC} ({dcValue})");

                                    var recPlanGuid = Guid.Parse(planGuid);
                                    var recSubGuid = Guid.Parse(powerCfgSetting.SubgroupGuid);
                                    var recSettGuid = Guid.Parse(powerCfgSetting.SettingGuid);

                                    PowerProf.PowerWriteACValueIndex(IntPtr.Zero, ref recPlanGuid, ref recSubGuid, ref recSettGuid, (uint)acValue.Value);
                                    PowerProf.PowerWriteDCValueIndex(IntPtr.Zero, ref recPlanGuid, ref recSubGuid, ref recSettGuid, (uint)dcValue.Value);

                                    appliedCount++;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    logService.Log(LogLevel.Warning, $"Failed to apply recommended setting '{setting.Id}': {ex.Message}");
                }
            }

            logService.Log(LogLevel.Info, $"Applied {appliedCount} PowerCfg settings to {PowerPlanDefinitions.ModunePowerPlanName}");
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"Error applying recommended settings: {ex.Message}");
        }
    }

    private static bool IsTweakerPowerPlan(string guid) =>
        IsTweakerPowerPlan(guid, null);

    private static bool IsTweakerPowerPlan(string guid, string? name) =>
        PowerPlanDefinitions.IsModunePowerPlanGuid(guid) ||
        PowerPlanDefinitions.IsModunePowerPlanName(name);

    private async Task ApplyTweakerRecommendedSettingsAsync(ISettingApplicationService? settingApplicationService)
    {
        try
        {
            if (settingApplicationService == null)
                throw new InvalidOperationException("settingApplicationService is required for applying recommended settings");
            logService.Log(LogLevel.Info, $"[PowerService] Applying recommended settings for {PowerPlanDefinitions.ModunePowerPlanName}");
            await settingApplicationService.ApplyRecommendedSettingsForFeatureAsync(SettingIds.PowerPlanSelection).ConfigureAwait(false);
            logService.Log(LogLevel.Info, $"[PowerService] Successfully applied recommended settings for {PowerPlanDefinitions.ModunePowerPlanName}");
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"[PowerService] Failed to apply recommended settings: {ex.Message}");
        }
    }

    private void SetPowerPlanName(Guid schemeGuid, string name)
    {
        powerSchemeOperations.WriteFriendlyName(schemeGuid, name);
    }

    private void SetPowerPlanNameAndDescription(Guid schemeGuid, string name, string description)
    {
        powerSchemeOperations.WriteFriendlyName(schemeGuid, name);

        if (!string.IsNullOrEmpty(description))
        {
            powerSchemeOperations.WriteDescription(schemeGuid, description);
        }
    }

    /// <summary>
    /// Parses a power scheme GUID from powercfg output.
    /// Expected format: "Power Scheme GUID: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx  (Name)"
    /// </summary>
    private static string? ParseGuidFromPowercfgOutput(string output)
    {
        if (string.IsNullOrWhiteSpace(output)) return null;

        var match = Regex.Match(output, @"([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})");
        return match.Success ? match.Groups[1].Value : null;
    }

    private async Task<(bool Success, string Output)> RunPowercfgAsync(string arguments, bool useCmd = false)
    {
        try
        {
            string fileName;
            string args;

            if (useCmd)
            {
                fileName = "cmd.exe";
                args = $"/c {arguments}";
            }
            else
            {
                fileName = "powercfg";
                args = arguments;
            }

            var result = await processExecutor.ExecuteAsync(fileName, args).ConfigureAwait(false);
            return (result.Succeeded, result.StandardOutput.TrimEnd());
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"powercfg {arguments} failed: {ex.Message}");
            return (false, string.Empty);
        }
    }

}
