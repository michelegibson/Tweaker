using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace Tweaker.UI.Features.Common.Helpers;

/// <summary>
/// Applies fast-scroll keyboard handling (PageUp/PageDown/Home/End) to WinUI 3
/// <see cref="ScrollView"/> hosts.
///
/// Background: all of our feature-detail pages host a <see cref="ListView"/> with
/// inner scrolling disabled inside an outer <see cref="ScrollView"/>. The ListView
/// consumes PageUp/PageDown for focus traversal, which emergently scrolls the outer
/// ScrollView all the way to the top/bottom. That's the "broken-feeling" behavior
/// issue #581 tracks.
///
/// This helper replaces that with viewport-sized paging on the outer ScrollView:
/// PageUp/PageDown scroll by ~one viewport; Home/End jump to the very top/bottom.
/// Focus does not move — matching mouse-wheel-chord / browser semantics, which is
/// what the issue requested.
///
/// Guards: we do NOT handle the key when the focused element is inside a control
/// that should own its own paging (open ComboBox popup, AutoSuggestBox with its
/// suggestion list open, multi-line TextBox, or an enabled nested
/// ScrollViewer/ScrollView other than the host we were asked to scroll). In those
/// cases we let the event bubble untouched.
/// </summary>
internal static class PageScrollHelper
{
    /// <summary>
    /// Fraction of the viewport a single PageUp/PageDown press scrolls.
    /// Kept small so content with only modest overflow doesn't jump straight to the end.
    /// </summary>
    private const double PageStepFraction = 0.15;

    /// <summary>
    /// Attaches fast-scroll handling to <paramref name="keyEventSource"/> for the
    /// given <paramref name="scrollView"/>.
    ///
    /// The inner ListView's built-in KeyDown reacts to PageUp/PageDown by moving
    /// focus to the first/last visible item, which raises
    /// <c>BringIntoViewRequested</c> and makes the outer ScrollView jump to the
    /// extreme. PreviewKeyDown tunnels root → target, so we can set
    /// <c>e.Handled = true</c> before the ListView's own handler runs. A bubbling
    /// KeyDown subscription stays as a belt-and-braces fallback.
    /// </summary>
    public static void Attach(UIElement keyEventSource, ScrollView scrollView)
    {
        if (keyEventSource == null || scrollView == null) return;

        keyEventSource.AddHandler(
            UIElement.PreviewKeyDownEvent,
            new KeyEventHandler((s, e) => HandleKey(scrollView, e)),
            handledEventsToo: true);

        keyEventSource.AddHandler(
            UIElement.KeyDownEvent,
            new KeyEventHandler((s, e) => HandleKey(scrollView, e)),
            handledEventsToo: true);
    }

    /// <summary>
    /// Inspects <paramref name="e"/> and scrolls <paramref name="scrollView"/> if
    /// the key is PageUp/PageDown/Home/End and no guard applies. Sets
    /// <c>e.Handled = true</c> only when it actually scrolled.
    /// </summary>
    public static void HandleKey(ScrollView scrollView, KeyRoutedEventArgs e)
    {
        if (scrollView == null || e == null) return;
        if (!IsPagingKey(e.Key)) return;

        if (ShouldSkipForFocusedElement(e.OriginalSource as DependencyObject, scrollView))
            return;

        if (scrollView.ScrollableHeight <= 0) return;

        var options = new ScrollingScrollOptions(ScrollingAnimationMode.Disabled);

        switch (e.Key)
        {
            case VirtualKey.PageUp:
                scrollView.ScrollBy(0, -scrollView.ViewportHeight * PageStepFraction, options);
                e.Handled = true;
                break;

            case VirtualKey.PageDown:
                scrollView.ScrollBy(0, scrollView.ViewportHeight * PageStepFraction, options);
                e.Handled = true;
                break;

            case VirtualKey.Home:
                scrollView.ScrollTo(scrollView.HorizontalOffset, 0, options);
                e.Handled = true;
                break;

            case VirtualKey.End:
                scrollView.ScrollTo(scrollView.HorizontalOffset, scrollView.ScrollableHeight, options);
                e.Handled = true;
                break;
        }
    }

    internal static bool IsPagingKey(VirtualKey key) =>
        key == VirtualKey.PageUp ||
        key == VirtualKey.PageDown ||
        key == VirtualKey.Home ||
        key == VirtualKey.End;

    /// <summary>
    /// Returns true if the focused element (or any ancestor up to, but not
    /// including, <paramref name="scrollViewHost"/>) is a control that should
    /// own its own paging behavior.
    ///
    /// A nested scroller only "owns the key" if it's actually scrollable — a
    /// ListView's internal template ScrollViewer with vertical scrolling disabled
    /// (the exact pattern SettingsListView uses, see the attached
    /// <c>ScrollViewer.VerticalScrollMode="Disabled"</c>) must NOT block us:
    /// otherwise every key press gets swallowed by that inert scroller and our
    /// outer ScrollView never sees the event.
    /// </summary>
    internal static bool ShouldSkipForFocusedElement(DependencyObject? focused, ScrollView scrollViewHost)
    {
        for (var current = focused; current != null; current = VisualTreeHelper.GetParent(current))
        {
            // Classic ScrollViewer — skip past it if vertical scrolling is disabled.
            if (current is ScrollViewer svr && svr.VerticalScrollMode != ScrollMode.Disabled)
                return true;

            // WinUI 3 ScrollView — same deal, and don't claim the host as "nested".
            if (current is ScrollView sv
                && !ReferenceEquals(sv, scrollViewHost)
                && sv.VerticalScrollMode != ScrollingScrollMode.Disabled)
                return true;

            if (current is ComboBox combo && combo.IsDropDownOpen) return true;

            if (current is AutoSuggestBox asb && asb.IsSuggestionListOpen) return true;

            if (current is TextBox tb && (tb.AcceptsReturn || tb.TextWrapping != TextWrapping.NoWrap))
                return true;
        }

        return false;
    }
}
