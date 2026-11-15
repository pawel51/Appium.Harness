namespace Appium.Harness.Maui;

/// <summary>Turns <see cref="TestProbe"/> on for the app.</summary>
public static class TestProbeAppBuilderExtensions
{
    /// <summary>
    /// Enables <see cref="TestProbe"/>, process-wide. Call it from <c>CreateMauiApp</c>, before any
    /// page is created, and <b>only in a test build</b>:
    ///
    /// <code>
    /// #if DEBUG
    ///     builder.UseTestProbes();
    /// #endif
    /// </code>
    ///
    /// <para>A published payload exposes colours and view-model values to anything that reads the
    /// accessibility tree, including assistive technology; a shipping build must not call this.</para>
    /// </summary>
    public static MauiAppBuilder UseTestProbes(this MauiAppBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        TestProbe.IsEnabled = true;
        return builder;
    }
}
