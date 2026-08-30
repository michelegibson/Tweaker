using System.Text;
using FluentAssertions;
using Microsoft.Win32;
using Moq;
using Tweaker.Core.Features.Common.Constants;
using Tweaker.Core.Features.Common.Enums;
using Tweaker.Core.Features.Common.Interfaces;
using Tweaker.Core.Features.Common.Models;
using Tweaker.Core.Features.Optimize.Interfaces;
using Tweaker.Core.Features.Optimize.Models;
using Tweaker.Infrastructure.Features.AdvancedTools.Services;
using Xunit;

namespace Tweaker.Infrastructure.Tests.AdvancedTools;

public class AutounattendScriptBuilderTests
{
    private readonly Mock<IPowerSettingsQueryService> _powerSettingsQueryService = new();
    private readonly Mock<IHardwareDetectionService> _hardwareDetectionService = new();
    private readonly Mock<ILogService> _logService = new();
    private readonly Mock<IComboBoxResolver> _comboBoxResolver = new();
    private readonly Mock<IPowerShellRunner> _powerShellRunner = new();
    private readonly AutounattendScriptBuilder _sut;

    public AutounattendScriptBuilderTests()
    {
        // Default setup for power settings query (always needed since BuildTweakermentsScriptAsync calls it)
        _powerSettingsQueryService.Setup(s => s.GetActivePowerPlanAsync())
            .ReturnsAsync(new PowerPlan { Guid = "balanced-guid", Name = "Balanced" });
        _powerSettingsQueryService.Setup(s => s.GetAllPowerSettingsACDCAsync(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, (int? acValue, int? dcValue)>());
        _hardwareDetectionService.Setup(s => s.HasBatteryAsync()).ReturnsAsync(false);

        // Syntax validation succeeds by default
        _powerShellRunner.Setup(s => s.ValidateScriptSyntaxAsync(It.IsAny<string>(), default))
            .Returns(Task.CompletedTask);

        _sut = new AutounattendScriptBuilder(
            _powerSettingsQueryService.Object,
            _hardwareDetectionService.Object,
            _logService.Object,
            _comboBoxResolver.Object,
            _powerShellRunner.Object);
    }

    // ---------------------------------------------------------------
    // BuildTweakermentsScriptAsync - Empty config
    // ---------------------------------------------------------------

    [Fact]
    public async Task BuildTweakermentsScriptAsync_EmptyConfig_ProducesValidScript()
    {
        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IEnumerable<SettingDefinition>>();

        var result = await _sut.BuildTweakermentsScriptAsync(config, allSettings);

        result.Should().NotBeNullOrEmpty();
    }

    // ---------------------------------------------------------------
    // BuildTweakermentsScriptAsync - Contains header
    // ---------------------------------------------------------------

    [Fact]
    public async Task BuildTweakermentsScriptAsync_ContainsHeader()
    {
        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IEnumerable<SettingDefinition>>();

        var result = await _sut.BuildTweakermentsScriptAsync(config, allSettings);

        result.Should().Contain(".SYNOPSIS");
        result.Should().Contain("param(");
    }

    // ---------------------------------------------------------------
    // BuildTweakermentsScriptAsync - Contains logging setup
    // ---------------------------------------------------------------

    [Fact]
    public async Task BuildTweakermentsScriptAsync_ContainsLoggingSetup()
    {
        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IEnumerable<SettingDefinition>>();

        var result = await _sut.BuildTweakermentsScriptAsync(config, allSettings);

        result.Should().Contain("function Write-Log");
        result.Should().Contain("$LogPath");
    }

    // ---------------------------------------------------------------
    // BuildTweakermentsScriptAsync - Contains helper functions
    // ---------------------------------------------------------------

    [Fact]
    public async Task BuildTweakermentsScriptAsync_ContainsHelperFunctions()
    {
        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IEnumerable<SettingDefinition>>();

        var result = await _sut.BuildTweakermentsScriptAsync(config, allSettings);

        result.Should().Contain("function Set-RegistryValue");
        result.Should().Contain("function Start-ProcessAsUser");
    }

    // ---------------------------------------------------------------
    // BuildTweakermentsScriptAsync - Contains if (-not $UserCustomizations) block
    // ---------------------------------------------------------------

    [Fact]
    public async Task BuildTweakermentsScriptAsync_ContainsSystemBlock()
    {
        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IEnumerable<SettingDefinition>>();

        var result = await _sut.BuildTweakermentsScriptAsync(config, allSettings);

        result.Should().Contain("if (-not $UserCustomizations)");
    }

    // ---------------------------------------------------------------
    // BuildTweakermentsScriptAsync - Contains if ($UserCustomizations) block
    // ---------------------------------------------------------------

    [Fact]
    public async Task BuildTweakermentsScriptAsync_ContainsUserBlock()
    {
        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IEnumerable<SettingDefinition>>();

        var result = await _sut.BuildTweakermentsScriptAsync(config, allSettings);

        result.Should().Contain("if ($UserCustomizations)");
    }

    // ---------------------------------------------------------------
    // BuildTweakermentsScriptAsync - Contains completion block
    // ---------------------------------------------------------------

    [Fact]
    public async Task BuildTweakermentsScriptAsync_ContainsCompletionBlock()
    {
        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IEnumerable<SettingDefinition>>();

        var result = await _sut.BuildTweakermentsScriptAsync(config, allSettings);

        result.Should().Contain("Script Completed");
    }

    // ---------------------------------------------------------------
    // BuildTweakermentsScriptAsync - Contains custom script placeholders
    // ---------------------------------------------------------------

    [Fact]
    public async Task BuildTweakermentsScriptAsync_ContainsCustomScriptPlaceholders()
    {
        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IEnumerable<SettingDefinition>>();

        var result = await _sut.BuildTweakermentsScriptAsync(config, allSettings);

        result.Should().Contain("SYSTEM WIDE");
        result.Should().Contain("USER SPECIFIC");
        result.Should().Contain("# Start here");
        result.Should().Contain("# End here");
    }

    // ---------------------------------------------------------------
    // BuildTweakermentsScriptAsync - Contains scripts directory setup
    // ---------------------------------------------------------------

    [Fact]
    public async Task BuildTweakermentsScriptAsync_ContainsScriptsDirectorySetup()
    {
        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IEnumerable<SettingDefinition>>();

        var result = await _sut.BuildTweakermentsScriptAsync(config, allSettings);

        result.Should().Contain("$scriptsDir");
    }

    // ---------------------------------------------------------------
    // BuildTweakermentsScriptAsync - Contains Tweaker installer
    // ---------------------------------------------------------------

    [Fact]
    public async Task BuildTweakermentsScriptAsync_ContainsTweakerInstaller()
    {
        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IEnumerable<SettingDefinition>>();

        var result = await _sut.BuildTweakermentsScriptAsync(config, allSettings);

        result.Should().Contain("Install Modune.lnk");
    }

    // ---------------------------------------------------------------
    // BuildTweakermentsScriptAsync - Contains Clean Start Menu
    // ---------------------------------------------------------------

    [Fact]
    public async Task BuildTweakermentsScriptAsync_ContainsCleanStartMenu()
    {
        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IEnumerable<SettingDefinition>>();

        var result = await _sut.BuildTweakermentsScriptAsync(config, allSettings);

        result.Should().Contain("START MENU LAYOUT");
    }

    // ---------------------------------------------------------------
    // BuildTweakermentsScriptAsync - Contains UserCustomizations scheduled task
    // ---------------------------------------------------------------

    [Fact]
    public async Task BuildTweakermentsScriptAsync_ContainsUserCustomizationsTask()
    {
        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IEnumerable<SettingDefinition>>();

        var result = await _sut.BuildTweakermentsScriptAsync(config, allSettings);

        result.Should().Contain("TweakerUserCustomizations");
    }

    // ---------------------------------------------------------------
    // BuildTweakermentsScriptAsync - Contains user detection bridge
    // ---------------------------------------------------------------

    [Fact]
    public async Task BuildTweakermentsScriptAsync_ContainsUserDetectionBridge()
    {
        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IEnumerable<SettingDefinition>>();

        var result = await _sut.BuildTweakermentsScriptAsync(config, allSettings);

        result.Should().Contain("$runningAsSystem");
        result.Should().Contain("S-1-5-18");
        result.Should().Contain("UserCustomizationsApplied");
    }

    // ---------------------------------------------------------------
    // BuildTweakermentsScriptAsync - Validates script syntax
    // ---------------------------------------------------------------

    [Fact]
    public async Task BuildTweakermentsScriptAsync_CallsValidateScriptSyntax()
    {
        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IEnumerable<SettingDefinition>>();

        await _sut.BuildTweakermentsScriptAsync(config, allSettings);

        _powerShellRunner.Verify(r => r.ValidateScriptSyntaxAsync(
            It.IsAny<string>(), default), Times.Once);
    }

    // ---------------------------------------------------------------
    // BuildTweakermentsScriptAsync - Syntax validation failure throws
    // ---------------------------------------------------------------

    [Fact]
    public async Task BuildTweakermentsScriptAsync_SyntaxValidationFails_Throws()
    {
        _powerShellRunner.Setup(s => s.ValidateScriptSyntaxAsync(It.IsAny<string>(), default))
            .ThrowsAsync(new InvalidOperationException("Syntax error at line 42"));

        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IEnumerable<SettingDefinition>>();

        var act = () => _sut.BuildTweakermentsScriptAsync(config, allSettings);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Syntax error*");
    }

    // ---------------------------------------------------------------
    // BuildTweakermentsScriptAsync - With WindowsApps items
    // ---------------------------------------------------------------

    [Fact]
    public async Task BuildTweakermentsScriptAsync_WithWindowsApps_EmitsAppRemoval()
    {
        var config = new UnifiedConfigurationFile
        {
            WindowsApps = new ConfigSection
            {
                Items = new List<ConfigurationItem>
                {
                    new ConfigurationItem
                    {
                        Id = "windows-app-cortana",
                        AppxPackageName = new[] { "Microsoft.549981C3F5F10" }
                    }
                }
            }
        };
        var allSettings = new Dictionary<string, IEnumerable<SettingDefinition>>();

        var result = await _sut.BuildTweakermentsScriptAsync(config, allSettings);

        result.Should().Contain("WINDOWS APPS REMOVAL");
        result.Should().Contain("BloatRemoval");
    }

    // ---------------------------------------------------------------
    // BuildTweakermentsScriptAsync - With Optimize features (HKLM)
    // ---------------------------------------------------------------

    [Fact]
    public async Task BuildTweakermentsScriptAsync_WithOptimizeFeatures_EmitsHklmRegistryEntries()
    {
        var settingDef = new SettingDefinition
        {
            Id = "test-optimize-setting",
            Name = "Optimize Setting",
            Description = "Test optimize",
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = "HKEY_LOCAL_MACHINE\\SOFTWARE\\Test",
                    ValueName = "OptVal",
                    ValueType = RegistryValueKind.DWord,
                    EnabledValue = [1],
                    DisabledValue = [0],
                    RecommendedValue = null,
                    DefaultValue = null
                }
            }
        };

        var config = new UnifiedConfigurationFile
        {
            Optimize = new FeatureGroupSection
            {
                Features = new Dictionary<string, ConfigSection>
                {
                    {
                        "TestOptimize", new ConfigSection
                        {
                            Items = new List<ConfigurationItem>
                            {
                                new ConfigurationItem
                                {
                                    Id = "test-optimize-setting",
                                    IsSelected = true,
                                    InputType = InputType.Toggle
                                }
                            }
                        }
                    }
                }
            }
        };

        var allSettings = new Dictionary<string, IEnumerable<SettingDefinition>>
        {
            { "TestOptimize", new[] { settingDef } }
        };

        var result = await _sut.BuildTweakermentsScriptAsync(config, allSettings);

        result.Should().Contain("Set-RegistryValue");
        result.Should().Contain("OptVal");
    }

    // ---------------------------------------------------------------
    // BuildTweakermentsScriptAsync - With Customize features (HKCU)
    // ---------------------------------------------------------------

    [Fact]
    public async Task BuildTweakermentsScriptAsync_WithCustomizeFeatures_EmitsHkcuInUserBlock()
    {
        var settingDef = new SettingDefinition
        {
            Id = "test-customize-setting",
            Name = "Customize Setting",
            Description = "Test customize",
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = "HKEY_CURRENT_USER\\Software\\Test",
                    ValueName = "CustVal",
                    ValueType = RegistryValueKind.DWord,
                    EnabledValue = [1],
                    DisabledValue = [0],
                    RecommendedValue = null,
                    DefaultValue = null
                }
            }
        };

        var config = new UnifiedConfigurationFile
        {
            Customize = new FeatureGroupSection
            {
                Features = new Dictionary<string, ConfigSection>
                {
                    {
                        "TestCustomize", new ConfigSection
                        {
                            Items = new List<ConfigurationItem>
                            {
                                new ConfigurationItem
                                {
                                    Id = "test-customize-setting",
                                    IsSelected = true,
                                    InputType = InputType.Toggle
                                }
                            }
                        }
                    }
                }
            }
        };

        var allSettings = new Dictionary<string, IEnumerable<SettingDefinition>>
        {
            { "TestCustomize", new[] { settingDef } }
        };

        var result = await _sut.BuildTweakermentsScriptAsync(config, allSettings);

        // The HKCU entries should appear after "if ($UserCustomizations)"
        var userBlockIndex = result.IndexOf("if ($UserCustomizations)");
        var custValIndex = result.IndexOf("CustVal", userBlockIndex);
        custValIndex.Should().BeGreaterThan(userBlockIndex);
    }

    // ---------------------------------------------------------------
    // BuildTweakermentsScriptAsync - Logs success on valid syntax
    // ---------------------------------------------------------------

    [Fact]
    public async Task BuildTweakermentsScriptAsync_ValidSyntax_LogsSuccess()
    {
        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IEnumerable<SettingDefinition>>();

        await _sut.BuildTweakermentsScriptAsync(config, allSettings);

        _logService.Verify(l => l.Log(
            LogLevel.Info,
            It.Is<string>(s => s.Contains("passed PowerShell syntax validation")),
            null), Times.Once);
    }

    // ---------------------------------------------------------------
    // BuildTweakermentsScriptAsync - Logs error on failed syntax
    // ---------------------------------------------------------------

    [Fact]
    public async Task BuildTweakermentsScriptAsync_FailedSyntax_LogsError()
    {
        _powerShellRunner.Setup(s => s.ValidateScriptSyntaxAsync(It.IsAny<string>(), default))
            .ThrowsAsync(new InvalidOperationException("Bad syntax"));

        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IEnumerable<SettingDefinition>>();

        try { await _sut.BuildTweakermentsScriptAsync(config, allSettings); }
        catch { /* expected */ }

        _logService.Verify(l => l.Log(
            LogLevel.Error,
            It.Is<string>(s => s.Contains("failed PowerShell syntax validation")),
            null), Times.Once);
    }
}
