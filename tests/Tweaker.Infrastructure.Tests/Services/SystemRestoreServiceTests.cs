using FluentAssertions;
using Moq;
using Tweaker.Core.Features.Common.Enums;
using Tweaker.Core.Features.Common.Interfaces;
using Tweaker.Infrastructure.Features.Common.Services;
using Xunit;

namespace Tweaker.Infrastructure.Tests.Services;

public class SystemRestoreServiceTests
{
    private readonly Mock<ILogService> _log = new();

    [Fact]
    public void IsEnabledForC_DoesNotThrow_OnAnyEnvironment()
    {
        // Smoke: ensures the method short-circuits to false rather than propagating exceptions.
        // Full behavioural tests require an integration environment with a real C: volume.
        var svc = new SystemRestoreService(_log.Object);
        var act = () => svc.IsEnabledForC();
        act.Should().NotThrow();
    }
}
