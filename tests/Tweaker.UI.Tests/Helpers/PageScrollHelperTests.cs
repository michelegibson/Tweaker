using FluentAssertions;
using Windows.System;
using Tweaker.UI.Features.Common.Helpers;
using Xunit;

namespace Tweaker.UI.Tests.Helpers;

/// <summary>
/// Covers the pure, UI-independent parts of <see cref="PageScrollHelper"/>.
/// The guard logic (<c>ShouldSkipForFocusedElement</c>) walks the visual tree and
/// cannot be exercised without a live XAML island, so it's verified by code
/// inspection rather than here.
/// </summary>
public class PageScrollHelperTests
{
    [Theory]
    [InlineData(VirtualKey.PageUp)]
    [InlineData(VirtualKey.PageDown)]
    [InlineData(VirtualKey.Home)]
    [InlineData(VirtualKey.End)]
    public void IsPagingKey_ForInterceptedKeys_ReturnsTrue(VirtualKey key)
    {
        PageScrollHelper.IsPagingKey(key).Should().BeTrue();
    }

    [Theory]
    [InlineData(VirtualKey.Up)]
    [InlineData(VirtualKey.Down)]
    [InlineData(VirtualKey.Left)]
    [InlineData(VirtualKey.Right)]
    [InlineData(VirtualKey.Tab)]
    [InlineData(VirtualKey.Enter)]
    [InlineData(VirtualKey.Space)]
    [InlineData(VirtualKey.Escape)]
    [InlineData(VirtualKey.A)]
    public void IsPagingKey_ForOtherKeys_ReturnsFalse(VirtualKey key)
    {
        PageScrollHelper.IsPagingKey(key).Should().BeFalse();
    }
}
