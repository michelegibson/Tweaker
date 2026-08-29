using FluentAssertions;
using Microsoft.Win32;
using Tweaker.Core.Features.Common.Constants;
using Tweaker.Core.Features.Common.Enums;
using Tweaker.Core.Features.Customize.Models;
using Xunit;

namespace Tweaker.Core.Tests.Models;

public class TaskbarCustomizationsTests
{
    [Fact]
    public void TaskbarClean_DeclaresFavoritesBinaryRegistrySetting_AsAction()
    {
        var clean = TaskbarCustomizations.GetTaskbarCustomizations()
            .Settings.Single(s => s.Id == SettingIds.TaskbarClean);

        clean.InputType.Should().Be(InputType.Action);
        clean.RestartProcess.Should().Be("Explorer");
        clean.RequiresConfirmation.Should().BeTrue();

        var reg = clean.RegistrySettings.Should().ContainSingle().Subject;
        reg.KeyPath.Should().EndWith(@"Explorer\Taskband");
        reg.ValueName.Should().Be("Favorites");
        reg.ValueType.Should().Be(RegistryValueKind.Binary);
        reg.EnabledValue.Should().ContainSingle().Which.Should().BeOfType<byte[]>()
            .Which.Should().BeEmpty();
        reg.RecommendedValue.Should().BeNull();
        reg.DefaultValue.Should().BeNull();
    }
}
