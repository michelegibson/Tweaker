using System;
using FluentAssertions;
using Moq;
using Tweaker.Core.Features.Common.Constants;
using Tweaker.Core.Features.Common.Enums;
using Tweaker.Core.Features.Common.Events;
using Tweaker.Core.Features.Common.Interfaces;
using Tweaker.Core.Features.Common.Models;
using Tweaker.Core.Features.Common.Native;
using Tweaker.Core.Features.Optimize.Models;
using Tweaker.Infrastructure.Features.Common.Services;
using Tweaker.Infrastructure.Features.Optimize.Services;
using Xunit;

namespace Tweaker.Infrastructure.Tests.Services;

public class PowerServiceTests
{
    private readonly Mock<ILogService> _logService;
    private readonly Mock<IPowerSettingsQueryService> _powerSettingsQueryService;
    private readonly Mock<ICompatibleSettingsRegistry> _compatibleSettingsRegistry;
    private readonly Mock<IEventBus> _eventBus;
    private readonly Mock<IPowerPlanComboBoxService> _powerPlanComboBoxService;
    private readonly Mock<IProcessExecutor> _processExecutor;
    private readonly Mock<IFileSystemService> _fileSystemService;
    private readonly Mock<IPowerSchemeOperations> _powerSchemeOperations;
    private readonly ConfigImportState _configImportState;
    private readonly PowerService _sut;

    public PowerServiceTests()
    {
        _logService = new Mock<ILogService>();
        _powerSettingsQueryService = new Mock<IPowerSettingsQueryService>();
        _compatibleSettingsRegistry = new Mock<ICompatibleSettingsRegistry>();
        _eventBus = new Mock<IEventBus>();
        _powerPlanComboBoxService = new Mock<IPowerPlanComboBoxService>();
        _processExecutor = new Mock<IProcessExecutor>();
        _fileSystemService = new Mock<IFileSystemService>();
        _powerSchemeOperations = new Mock<IPowerSchemeOperations>();
        _configImportState = new ConfigImportState();

        _sut = new PowerService(
            _logService.Object,
            _powerSettingsQueryService.Object,
            _compatibleSettingsRegistry.Object,
            _eventBus.Object,
            _powerPlanComboBoxService.Object,
            _processExecutor.Object,
            _fileSystemService.Object,
            _powerSchemeOperations.Object,
            _configImportState);
    }

    private static SettingDefinition MakeSetting(string id, string? name = null, string? description = null) =>
        new()
        {
            Id = id,
            Name = name ?? id,
            Description = description ?? $"Description for {id}",
        };

    [Fact]
    public async Task GetActivePowerPlanAsync_DelegatesToQueryService()
    {
        // Arrange
        var expectedPlan = new Tweaker.Core.Features.Optimize.Models.PowerPlan
        {
            Name = "Balanced",
            Guid = "381b4222-f694-41f0-9685-ff5bb260df2e"
        };

        _powerSettingsQueryService
            .Setup(s => s.GetActivePowerPlanAsync())
            .ReturnsAsync(expectedPlan);

        // Act
        var result = await _sut.GetActivePowerPlanAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Balanced");
        result.Guid.Should().Be("381b4222-f694-41f0-9685-ff5bb260df2e");
    }

    [Fact]
    public async Task GetActivePowerPlanAsync_WhenQueryServiceThrows_ReturnsNull()
    {
        // Arrange
        _powerSettingsQueryService
            .Setup(s => s.GetActivePowerPlanAsync())
            .ThrowsAsync(new Exception("Query failed"));

        // Act
        var result = await _sut.GetActivePowerPlanAsync();

        // Assert
        result.Should().BeNull();
        _logService.Verify(
            l => l.Log(LogLevel.Warning, It.Is<string>(s => s.Contains("Error getting active power plan"))),
            Times.Once);
    }

    [Fact]
    public async Task GetAvailablePowerPlansAsync_ReturnsPlansList()
    {
        // Arrange
        var plans = new List<Tweaker.Core.Features.Optimize.Models.PowerPlan>
        {
            new() { Name = "Balanced", Guid = "381b4222-f694-41f0-9685-ff5bb260df2e" },
            new() { Name = "High Performance", Guid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c" },
        };

        _powerSettingsQueryService
            .Setup(s => s.GetAvailablePowerPlansAsync())
            .ReturnsAsync(plans);

        // Act
        var result = await _sut.GetAvailablePowerPlansAsync();

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAvailablePowerPlansAsync_WhenQueryServiceThrows_ReturnsEmpty()
    {
        // Arrange
        _powerSettingsQueryService
            .Setup(s => s.GetAvailablePowerPlansAsync())
            .ThrowsAsync(new Exception("Query failed"));

        // Act
        var result = await _sut.GetAvailablePowerPlansAsync();

        // Assert
        result.Should().BeEmpty();
        _logService.Verify(
            l => l.Log(LogLevel.Warning, It.Is<string>(s => s.Contains("Error getting available power plans"))),
            Times.Once);
    }

    [Fact]
    public async Task TryApplySpecialSettingAsync_NonPowerPlanSetting_ReturnsFalse()
    {
        // Arrange
        var setting = MakeSetting("some-other-setting");

        // Act
        var result = await _sut.TryApplySpecialSettingAsync(setting, 0);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DiscoverSpecialSettingsAsync_WithPowerPlanSetting_ReturnsActivePlanInfo()
    {
        // Arrange
        var settings = new List<SettingDefinition>
        {
            MakeSetting("power-plan-selection"),
        };

        var activePlan = new Tweaker.Core.Features.Optimize.Models.PowerPlan
        {
            Name = "Balanced",
            Guid = "381b4222-f694-41f0-9685-ff5bb260df2e"
        };

        _powerSettingsQueryService
            .Setup(s => s.GetActivePowerPlanAsync())
            .ReturnsAsync(activePlan);

        // Act
        var result = await _sut.DiscoverSpecialSettingsAsync(settings);

        // Assert
        result.Should().ContainKey("power-plan-selection");
        result["power-plan-selection"]["ActivePowerPlan"].Should().Be("Balanced");
        result["power-plan-selection"]["ActivePowerPlanGuid"].Should().Be("381b4222-f694-41f0-9685-ff5bb260df2e");
    }

    [Fact]
    public async Task DiscoverSpecialSettingsAsync_WithoutPowerPlanSetting_ReturnsEmptyDictionary()
    {
        // Arrange
        var settings = new List<SettingDefinition>
        {
            MakeSetting("some-other-setting"),
        };

        // Act
        var result = await _sut.DiscoverSpecialSettingsAsync(settings);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task TryApplySpecialSettingAsync_CorruptModunePlan_WhenRepairFails_DeletesGhostAndAttemptsImport()
    {
        // Arrange — ghost plan exists with the stable branded GUID but a corrupt name
        var moduneGuid = PowerPlanDefinitions.ModunePowerPlanGuid;
        var setting = MakeSetting(SettingIds.PowerPlanSelection);

        var ghostPlan = new PowerPlan
        {
            Name = "Unknown Power Plan",
            Guid = moduneGuid,
            IsActive = false
        };

        _powerPlanComboBoxService
            .Setup(s => s.ResolvePowerPlanByIndexAsync(4))
            .ReturnsAsync(new PowerPlanResolutionResult
            {
                Success = true,
                Guid = moduneGuid,
                DisplayName = PowerPlanDefinitions.ModunePowerPlanName
            });

        _powerSettingsQueryService
            .Setup(s => s.GetActivePowerPlanAsync())
            .ReturnsAsync(new PowerPlan { Name = "Balanced", Guid = "381b4222-f694-41f0-9685-ff5bb260df2e" });

        _powerSettingsQueryService
            .Setup(s => s.GetAvailablePowerPlansAsync())
            .ReturnsAsync(new List<PowerPlan> { ghostPlan });

        _powerSchemeOperations
            .Setup(s => s.DeleteScheme(It.IsAny<Guid>()))
            .Returns(PowerProf.ERROR_SUCCESS);

        // powercfg /duplicatescheme — return output with the actual GUID assigned
        var assignedGuid = "159d8424-9c94-4b24-ada1-b427b29e9b2e";
        _processExecutor
            .Setup(p => p.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(new ProcessExecutionResult
            {
                ExitCode = 0,
                StandardOutput = $"Power Scheme GUID: {assignedGuid}  (Ultimate Performance)"
            });

        _powerSchemeOperations
            .Setup(s => s.SetActiveScheme(It.IsAny<Guid>()))
            .Returns(PowerProf.ERROR_SUCCESS);

        _powerSchemeOperations
            .Setup(s => s.WriteFriendlyName(It.IsAny<Guid>(), It.IsAny<string>()))
            .Returns(PowerProf.ERROR_SUCCESS);

        // In-place repair must fail before the corrupt entry is eligible for deletion.
        _powerSchemeOperations
            .Setup(s => s.WriteFriendlyName(
                Guid.Parse(moduneGuid),
                PowerPlanDefinitions.ModunePowerPlanName))
            .Returns(PowerProf.ERROR_ACCESS_DENIED);

        _powerSchemeOperations
            .Setup(s => s.WriteDescription(It.IsAny<Guid>(), It.IsAny<string>()))
            .Returns(PowerProf.ERROR_SUCCESS);

        // Act
        var result = await _sut.TryApplySpecialSettingAsync(setting, 4);

        // Assert — ghost plan should be deleted
        _powerSchemeOperations.Verify(
            s => s.DeleteScheme(Guid.Parse(moduneGuid)),
            Times.AtLeastOnce);

        // Should log that repair failed before cleanup
        _logService.Verify(
            l => l.Log(LogLevel.Warning, It.Is<string>(s => s.Contains("Failed to repair corrupt Modune plan"))),
            Times.AtLeastOnce);

        // Name should be set on the actual GUID powercfg assigned, not the requested one
        _powerSchemeOperations.Verify(
            s => s.WriteFriendlyName(Guid.Parse(assignedGuid), PowerPlanDefinitions.ModunePowerPlanName),
            Times.Once);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task TryApplySpecialSettingAsync_LegacyTweakerPlan_RenamesInPlaceAndDoesNotDelete()
    {
        // Arrange — a pre-rebrand plan exists with the stable branded GUID
        var moduneGuid = PowerPlanDefinitions.ModunePowerPlanGuid;
        var setting = MakeSetting(SettingIds.PowerPlanSelection);

        var validPlan = new PowerPlan
        {
            Name = "Tweaker Power Plan",
            Guid = moduneGuid,
            IsActive = false
        };

        _powerPlanComboBoxService
            .Setup(s => s.ResolvePowerPlanByIndexAsync(4))
            .ReturnsAsync(new PowerPlanResolutionResult
            {
                Success = true,
                Guid = moduneGuid,
                DisplayName = PowerPlanDefinitions.ModunePowerPlanName
            });

        _powerSettingsQueryService
            .Setup(s => s.GetActivePowerPlanAsync())
            .ReturnsAsync(new PowerPlan { Name = "Balanced", Guid = "381b4222-f694-41f0-9685-ff5bb260df2e" });

        _powerSettingsQueryService
            .Setup(s => s.GetAvailablePowerPlansAsync())
            .ReturnsAsync(new List<PowerPlan> { validPlan });

        _powerSchemeOperations
            .Setup(s => s.SetActiveScheme(It.IsAny<Guid>()))
            .Returns(PowerProf.ERROR_SUCCESS);

        _powerSchemeOperations
            .Setup(s => s.WriteFriendlyName(
                Guid.Parse(moduneGuid),
                PowerPlanDefinitions.ModunePowerPlanName))
            .Returns(PowerProf.ERROR_SUCCESS);

        // Act
        var result = await _sut.TryApplySpecialSettingAsync(setting, 4);

        // Assert — should NOT delete the valid plan
        _powerSchemeOperations.Verify(
            s => s.DeleteScheme(Guid.Parse(moduneGuid)),
            Times.Never);

        _powerSchemeOperations.Verify(
            s => s.WriteFriendlyName(
                Guid.Parse(moduneGuid),
                PowerPlanDefinitions.ModunePowerPlanName),
            Times.Once);

        // Should succeed by just activating the existing plan
        result.Should().BeTrue();
    }

    [Fact]
    public async Task TryApplySpecialSettingAsync_CorruptModunePlan_WhenRepairSucceeds_DoesNotDelete()
    {
        var moduneGuid = PowerPlanDefinitions.ModunePowerPlanGuid;
        var setting = MakeSetting(SettingIds.PowerPlanSelection);
        var corruptPlan = new PowerPlan
        {
            Name = "Unknown Power Plan",
            Guid = moduneGuid,
            IsActive = false
        };

        _powerPlanComboBoxService
            .Setup(s => s.ResolvePowerPlanByIndexAsync(4))
            .ReturnsAsync(new PowerPlanResolutionResult
            {
                Success = true,
                Guid = moduneGuid,
                DisplayName = PowerPlanDefinitions.ModunePowerPlanName
            });
        _powerSettingsQueryService
            .Setup(s => s.GetActivePowerPlanAsync())
            .ReturnsAsync(new PowerPlan { Name = "Balanced", Guid = "381b4222-f694-41f0-9685-ff5bb260df2e" });
        _powerSettingsQueryService
            .Setup(s => s.GetAvailablePowerPlansAsync())
            .ReturnsAsync(new List<PowerPlan> { corruptPlan });
        _powerSchemeOperations
            .Setup(s => s.WriteFriendlyName(Guid.Parse(moduneGuid), PowerPlanDefinitions.ModunePowerPlanName))
            .Returns(PowerProf.ERROR_SUCCESS);
        _powerSchemeOperations
            .Setup(s => s.SetActiveScheme(Guid.Parse(moduneGuid)))
            .Returns(PowerProf.ERROR_SUCCESS);

        var result = await _sut.TryApplySpecialSettingAsync(setting, 4);

        result.Should().BeTrue();
        _powerSchemeOperations.Verify(
            s => s.WriteFriendlyName(Guid.Parse(moduneGuid), PowerPlanDefinitions.ModunePowerPlanName),
            Times.Once);
        _powerSchemeOperations.Verify(s => s.DeleteScheme(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task ImportPowerPlanAsync_LegacyNameWithCanonicalGuid_UsesBrandedImportAndCanonicalName()
    {
        var moduneGuid = PowerPlanDefinitions.ModunePowerPlanGuid;
        var legacyDefinition = new PredefinedPowerPlan(
            PowerPlanDefinitions.LegacyTweakerPowerPlanName,
            "Legacy exported definition",
            "PowerPlan_TweakerPowerPlan_Name",
            moduneGuid);

        _powerSettingsQueryService
            .Setup(s => s.GetAvailablePowerPlansAsync())
            .ReturnsAsync(new List<PowerPlan>());
        _powerSchemeOperations
            .Setup(s => s.DeleteScheme(Guid.Parse(moduneGuid)))
            .Returns(PowerProf.ERROR_SUCCESS);
        _processExecutor
            .Setup(p => p.ExecuteAsync("powercfg", It.IsAny<string>(), default))
            .ReturnsAsync(new ProcessExecutionResult
            {
                ExitCode = 0,
                StandardOutput = $"Power Scheme GUID: {moduneGuid}  (Ultimate Performance)"
            });
        _powerSchemeOperations
            .SetupSequence(s => s.WriteFriendlyName(Guid.Parse(moduneGuid), PowerPlanDefinitions.ModunePowerPlanName))
            .Returns(PowerProf.ERROR_FILE_NOT_FOUND)
            .Returns(PowerProf.ERROR_SUCCESS);
        _powerSchemeOperations
            .Setup(s => s.WriteDescription(Guid.Parse(moduneGuid), It.IsAny<string>()))
            .Returns(PowerProf.ERROR_SUCCESS);
        _compatibleSettingsRegistry
            .Setup(s => s.GetFilteredSettings(FeatureIds.Power))
            .Returns(Array.Empty<SettingDefinition>());

        var result = await _sut.ImportPowerPlanAsync(legacyDefinition);

        result.Success.Should().BeTrue();
        result.ImportedGuid.Should().Be(moduneGuid);
        _processExecutor.Verify(
            p => p.ExecuteAsync(
                "powercfg",
                It.Is<string>(args => args.Contains(moduneGuid, StringComparison.OrdinalIgnoreCase)),
                default),
            Times.Once);
        _powerSchemeOperations.Verify(
            s => s.WriteFriendlyName(Guid.Parse(moduneGuid), PowerPlanDefinitions.ModunePowerPlanName),
            Times.Exactly(2));
    }

    [Fact]
    public async Task DiscoverSpecialSettingsAsync_CorruptModunePlanActive_RepairFailsThenDeletesAfterSwitchingToBalanced()
    {
        // Arrange — ghost branded plan is active with a wrong name
        var moduneGuid = PowerPlanDefinitions.ModunePowerPlanGuid;
        var balancedGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";
        var settings = new List<SettingDefinition> { MakeSetting(SettingIds.PowerPlanSelection) };

        var ghostPlan = new PowerPlan
        {
            Name = "Unknown Power Plan",
            Guid = moduneGuid,
            IsActive = true
        };

        var balancedPlan = new PowerPlan
        {
            Name = "Balanced",
            Guid = balancedGuid,
            IsActive = false
        };

        _powerSettingsQueryService
            .Setup(s => s.GetAvailablePowerPlansAsync())
            .ReturnsAsync(new List<PowerPlan> { balancedPlan, ghostPlan });

        // After cleanup, active plan is Balanced
        _powerSettingsQueryService
            .Setup(s => s.GetActivePowerPlanAsync())
            .ReturnsAsync(balancedPlan);

        _powerSchemeOperations
            .Setup(s => s.SetActiveScheme(Guid.Parse(balancedGuid)))
            .Returns(PowerProf.ERROR_SUCCESS);

        _powerSchemeOperations
            .Setup(s => s.WriteFriendlyName(
                Guid.Parse(moduneGuid),
                PowerPlanDefinitions.ModunePowerPlanName))
            .Returns(PowerProf.ERROR_ACCESS_DENIED);

        _powerSchemeOperations
            .Setup(s => s.DeleteScheme(Guid.Parse(moduneGuid)))
            .Returns(PowerProf.ERROR_SUCCESS);

        // Act
        var result = await _sut.DiscoverSpecialSettingsAsync(settings);

        // Assert — should switch to Balanced before deleting
        _powerSchemeOperations.Verify(
            s => s.SetActiveScheme(Guid.Parse(balancedGuid)),
            Times.Once);

        // Should delete the ghost
        _powerSchemeOperations.Verify(
            s => s.DeleteScheme(Guid.Parse(moduneGuid)),
            Times.Once);

        // Should invalidate cache
        _powerSettingsQueryService.Verify(
            s => s.InvalidateCache(),
            Times.AtLeastOnce);

        // Result should reflect Balanced as active
        result.Should().ContainKey(SettingIds.PowerPlanSelection);
        result[SettingIds.PowerPlanSelection]["ActivePowerPlan"].Should().Be("Balanced");
    }

    [Fact]
    public async Task DiscoverSpecialSettingsAsync_CorruptActivePlan_WhenBalancedActivationFails_DoesNotDelete()
    {
        var moduneGuid = PowerPlanDefinitions.ModunePowerPlanGuid;
        var balancedGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";
        var settings = new List<SettingDefinition> { MakeSetting(SettingIds.PowerPlanSelection) };
        var corruptPlan = new PowerPlan
        {
            Name = "Unknown Power Plan",
            Guid = moduneGuid,
            IsActive = true
        };

        _powerSettingsQueryService
            .Setup(s => s.GetAvailablePowerPlansAsync())
            .ReturnsAsync(new List<PowerPlan> { corruptPlan });
        _powerSettingsQueryService
            .Setup(s => s.GetActivePowerPlanAsync())
            .ReturnsAsync(corruptPlan);
        _powerSchemeOperations
            .Setup(s => s.WriteFriendlyName(Guid.Parse(moduneGuid), PowerPlanDefinitions.ModunePowerPlanName))
            .Returns(PowerProf.ERROR_ACCESS_DENIED);
        _powerSchemeOperations
            .Setup(s => s.SetActiveScheme(Guid.Parse(balancedGuid)))
            .Returns(PowerProf.ERROR_ACCESS_DENIED);

        await _sut.DiscoverSpecialSettingsAsync(settings);

        _powerSchemeOperations.Verify(s => s.DeleteScheme(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task DiscoverSpecialSettingsAsync_LegacyTweakerPlanActive_RenamesInPlaceAndDoesNotDelete()
    {
        // Arrange — a pre-rebrand plan is active
        var moduneGuid = PowerPlanDefinitions.ModunePowerPlanGuid;
        var settings = new List<SettingDefinition> { MakeSetting(SettingIds.PowerPlanSelection) };

        var validPlan = new PowerPlan
        {
            Name = "Tweaker Power Plan",
            Guid = moduneGuid,
            IsActive = true
        };

        _powerSettingsQueryService
            .Setup(s => s.GetAvailablePowerPlansAsync())
            .ReturnsAsync(new List<PowerPlan> { validPlan });

        _powerSettingsQueryService
            .Setup(s => s.GetActivePowerPlanAsync())
            .ReturnsAsync(new PowerPlan
            {
                Name = PowerPlanDefinitions.ModunePowerPlanName,
                Guid = moduneGuid,
                IsActive = true
            });

        _powerSchemeOperations
            .Setup(s => s.WriteFriendlyName(
                Guid.Parse(moduneGuid),
                PowerPlanDefinitions.ModunePowerPlanName))
            .Returns(PowerProf.ERROR_SUCCESS);

        // Act
        var result = await _sut.DiscoverSpecialSettingsAsync(settings);

        // Assert — should NOT delete valid plan
        _powerSchemeOperations.Verify(
            s => s.DeleteScheme(It.IsAny<Guid>()),
            Times.Never);

        _powerSchemeOperations.Verify(
            s => s.WriteFriendlyName(
                Guid.Parse(moduneGuid),
                PowerPlanDefinitions.ModunePowerPlanName),
            Times.Once);

        // Discovery after cache invalidation reports the canonical public name.
        result[SettingIds.PowerPlanSelection]["ActivePowerPlan"].Should().Be(PowerPlanDefinitions.ModunePowerPlanName);
        result[SettingIds.PowerPlanSelection]["ActivePowerPlanGuid"].Should().Be(moduneGuid);
    }

    // Sets up an existing-on-system Tweaker plan so a config-import dictionary apply reaches
    // the IsTweakerPowerPlan branch (which decides whether to re-apply recommended settings).
    private (SettingDefinition setting, object value, Mock<ISettingApplicationService> apply) ArrangeTweakerPlanImport()
    {
        var tweakerGuid = "57696e68-616e-6365-506f-776572000000";
        var setting = MakeSetting(SettingIds.PowerPlanSelection);

        var value = new Dictionary<string, object>
        {
            ["Guid"] = tweakerGuid,
            ["Name"] = "Tweaker Power Plan",
        };

        // Active plan differs from the target so SetActivePowerPlanAsync actually activates it.
        _powerSettingsQueryService
            .Setup(s => s.GetActivePowerPlanAsync())
            .ReturnsAsync(new PowerPlan { Name = "Balanced", Guid = "381b4222-f694-41f0-9685-ff5bb260df2e" });

        // Plan already exists on the system -> simple activation path.
        _powerSettingsQueryService
            .Setup(s => s.GetAvailablePowerPlansAsync())
            .ReturnsAsync(new List<PowerPlan>
            {
                new() { Name = "Tweaker Power Plan", Guid = tweakerGuid, IsActive = false }
            });

        _powerSchemeOperations
            .Setup(s => s.SetActiveScheme(It.IsAny<Guid>()))
            .Returns(PowerProf.ERROR_SUCCESS);

        _powerPlanComboBoxService
            .Setup(s => s.GetPowerPlanOptionsAsync())
            .ReturnsAsync(new List<PowerPlanComboBoxOption>());

        var apply = new Mock<ISettingApplicationService>();
        apply
            .Setup(a => a.ApplyRecommendedSettingsForFeatureAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        return (setting, value, apply);
    }

    [Fact]
    public async Task TryApplySpecialSettingAsync_ConfigImportSuppliesPowerValues_SkipsRecommendedReapply()
    {
        // Arrange — active config import that carries individual power values
        _configImportState.IsActive = true;
        _configImportState.ImportSuppliesPowerValues = true;
        var (setting, value, apply) = ArrangeTweakerPlanImport();

        // Act
        var result = await _sut.TryApplySpecialSettingAsync(setting, value, settingApplicationService: apply.Object);

        // Assert — the recommended re-apply must NOT fire (import is the source of truth)
        result.Should().BeTrue();
        apply.Verify(
            a => a.ApplyRecommendedSettingsForFeatureAsync(It.IsAny<string>()),
            Times.Never);
        _logService.Verify(
            l => l.Log(LogLevel.Info, It.Is<string>(s => s.Contains("Skipping recommended power re-apply"))),
            Times.Once);
        _powerSchemeOperations.Verify(
            s => s.WriteFriendlyName(
                Guid.Parse(PowerPlanDefinitions.ModunePowerPlanGuid),
                PowerPlanDefinitions.ModunePowerPlanName),
            Times.Once);
        _powerSchemeOperations.Verify(s => s.DeleteScheme(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task TryApplySpecialSettingAsync_NoActiveImport_AppliesRecommendedSettings()
    {
        // Arrange — manual UI / no active import: existing behavior must be preserved
        _configImportState.IsActive = false;
        _configImportState.ImportSuppliesPowerValues = false;
        var (setting, value, apply) = ArrangeTweakerPlanImport();

        // Act
        var result = await _sut.TryApplySpecialSettingAsync(setting, value, settingApplicationService: apply.Object);

        // Assert — recommended settings ARE re-applied
        result.Should().BeTrue();
        apply.Verify(
            a => a.ApplyRecommendedSettingsForFeatureAsync(SettingIds.PowerPlanSelection),
            Times.Once);
    }
}
