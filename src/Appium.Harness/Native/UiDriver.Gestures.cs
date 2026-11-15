using OpenQA.Selenium.Appium;

namespace Appium.Harness;

// Scrolling and swiping. The geometry and the per-platform gesture scripts are in
// NativeGestures; this part is the public vocabulary over it.
public sealed partial class UiDriver
{
    private readonly NativeGestures _gestures;

    /// <summary>Scrolls down until the element is visible, then returns it.</summary>
    public AppiumElement ScrollTo(string automationId, int maxSwipes = 12)
    {
        SwipeUntilVisible(automationId, SwipeDirection.Up, maxSwipes);
        return Wait(automationId);
    }

    /// <summary>Scrolls up until the element is visible, then returns it.</summary>
    public AppiumElement ScrollUpTo(string automationId, int maxSwipes = 12)
    {
        SwipeUntilVisible(automationId, SwipeDirection.Down, maxSwipes);
        return Wait(automationId);
    }

    /// <summary>Scrolls down looking for the element; returns null rather than throwing.</summary>
    public AppiumElement? ScrollToOrNull(string automationId, int maxSwipes = 12)
    {
        SwipeUntilVisible(automationId, SwipeDirection.Up, maxSwipes);
        return WaitOrNull(automationId, TimeSpan.FromSeconds(2));
    }

    /// <summary>Scrolls up looking for the element; returns null rather than throwing.</summary>
    public AppiumElement? ScrollUpToOrNull(string automationId, int maxSwipes = 12)
    {
        SwipeUntilVisible(automationId, SwipeDirection.Down, maxSwipes);
        return WaitOrNull(automationId, TimeSpan.FromSeconds(2));
    }

    /// <summary>Finger moves up, revealing content below.</summary>
    public void SwipeUp(double atWidthFraction = 0.5, double distanceFraction = 0.6) =>
        Swipe(SwipeDirection.Up, atWidthFraction, distanceFraction);

    /// <summary>Finger moves down, revealing content above.</summary>
    public void SwipeDown(double atWidthFraction = 0.5, double distanceFraction = 0.6) =>
        Swipe(SwipeDirection.Down, atWidthFraction, distanceFraction);

    /// <summary>Finger moves left, revealing content to the right.</summary>
    public void SwipeLeft(double atHeightFraction = 0.5, double distanceFraction = 0.8) =>
        Swipe(SwipeDirection.Left, atHeightFraction, distanceFraction);

    /// <summary>Finger moves right, revealing content to the left.</summary>
    public void SwipeRight(double atHeightFraction = 0.5, double distanceFraction = 0.8) =>
        Swipe(SwipeDirection.Right, atHeightFraction, distanceFraction);

    /// <summary>
    /// One swipe, aimed by lane: <paramref name="alongFraction"/> is the cross-axis position
    /// (width fraction for a vertical swipe, height fraction for a horizontal one) and
    /// <paramref name="distanceFraction"/> is the travel along the swipe axis.
    /// </summary>
    public void Swipe(SwipeDirection direction, double alongFraction = 0.5, double distanceFraction = 0.6) =>
        _gestures.Swipe(direction, alongFraction, distanceFraction);

    /// <summary>One swipe anchored on an element — for gesturing a specific row or card.</summary>
    public void SwipeFrom(AppiumElement element, SwipeDirection direction, double distanceFraction = 0.5)
    {
        ArgumentNullException.ThrowIfNull(element);
        _gestures.SwipeFrom(element, direction, distanceFraction);
    }

    private void SwipeUntilVisible(string automationId, SwipeDirection direction, int maxSwipes)
    {
        for (var i = 0; i < maxSwipes && !Exists(automationId); i++)
        {
            Swipe(direction);
        }
    }
}
