using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Appium.Harness.Maui;

/// <summary>
/// Publishes chosen view properties — a colour, a bound view-model value — into the platform
/// accessibility tree so an Appium test can assert on them.
///
/// <para>A native accessibility tree carries text, ids and a few state flags; it does <b>not</b>
/// carry a MAUI <c>Color</c> or anything from a view model. Without this the only honest way to
/// test "the XP label is blue" is to compare pixels. With it:</para>
///
/// <code>
/// &lt;Label AutomationId="profile-total-xp" Text="{Binding TotalXp}" TextColor="#7EB4F0"
///        probe:TestProbe.Properties="TextColor"
///        probe:TestProbe.Value="{Binding TotalXp}" /&gt;
/// </code>
///
/// <code>
/// Ui.Expect("profile-total-xp").HasTextColor("#7EB4F0").HasProperty("Value", 120);
/// </code>
///
/// <para><b>Off unless enabled.</b> Nothing is published until the app calls
/// <see cref="TestProbeAppBuilderExtensions.UseTestProbes"/>, which it should do only in a test
/// build (inside its own <c>#if DEBUG</c> or test-build constant). A compile-time gate in this
/// package could not do that job: a package is compiled once, in Release, long before the app
/// decides what kind of build it is. In a shipping build the attached properties still exist, so
/// the XAML compiles, and do nothing.</para>
///
/// <para><b>Where it lands.</b> Android: the view's <c>contentDescription</c>. iOS: the
/// <c>accessibilityValue</c>. Appium.Harness reads the same field back. The value is a
/// <c>[[probe:Name=value;…]]</c> payload, written by the same source file the harness parses it
/// with, so the two sides cannot drift. On Android a <c>SemanticProperties.Description</c> is kept
/// in front of it; on iOS the description lives in the label and is left alone.</para>
///
/// <para><b>Limits.</b> Do not put a probe on a control whose accessibility <i>value</i> a test
/// already reads on iOS — a <c>Switch</c> or <c>Entry</c> — since that is the field it uses there.</para>
/// </summary>
public static class TestProbe
{
    private static readonly ConditionalWeakTable<VisualElement, Publisher> Publishers = new();

    /// <summary>Comma-separated names of public properties of the view to publish, e.g. <c>"TextColor,BackgroundColor"</c>.</summary>
    public static readonly BindableProperty PropertiesProperty = BindableProperty.CreateAttached(
        "Properties", typeof(string), typeof(TestProbe), defaultValue: null, propertyChanged: OnInputChanged);

    /// <summary>A free-form value — typically bound to a view-model property — published as <c>Value</c>.</summary>
    public static readonly BindableProperty ValueProperty = BindableProperty.CreateAttached(
        "Value", typeof(object), typeof(TestProbe), defaultValue: null, propertyChanged: OnInputChanged);

    /// <summary>
    /// Whether probes publish. Set by <see cref="TestProbeAppBuilderExtensions.UseTestProbes"/>;
    /// false in any build that does not call it, which is what keeps a shipping build silent.
    /// </summary>
    public static bool IsEnabled { get; internal set; }

    /// <summary>Gets <see cref="PropertiesProperty"/>.</summary>
    public static string? GetProperties(BindableObject view) => (string?)view.GetValue(PropertiesProperty);

    /// <summary>Sets <see cref="PropertiesProperty"/>.</summary>
    public static void SetProperties(BindableObject view, string? value) => view.SetValue(PropertiesProperty, value);

    /// <summary>Gets <see cref="ValueProperty"/>.</summary>
    public static object? GetValue(BindableObject view) => view.GetValue(ValueProperty);

    /// <summary>Sets <see cref="ValueProperty"/>.</summary>
    public static void SetValue(BindableObject view, object? value) => view.SetValue(ValueProperty, value);

    private static void OnInputChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        // Checked per change rather than once: a view created before UseTestProbes ran is simply
        // never tracked, which is the safe direction to be wrong in.
        if (IsEnabled && bindable is VisualElement view)
        {
            Publishers.GetValue(view, v => new Publisher(v)).Publish();
        }
    }

    internal static bool IsTracked(VisualElement view) => Publishers.TryGetValue(view, out _);

    /// <summary>The payload <paramref name="view"/> publishes, or null when it publishes nothing.</summary>
    internal static string? PayloadFor(VisualElement view)
    {
        var values = new List<KeyValuePair<string, string?>>();

        foreach (var name in Names(view))
        {
            if (name == ProbeProperties.Value)
            {
                continue; // the attached Value, below, owns this name
            }

            var property = view.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (property is null)
            {
                Debug.WriteLine($"[TestProbe] {view.GetType().Name} has no public property '{name}'");
                continue;
            }

            values.Add(new(name, Format(property.GetValue(view))));
        }

        if (GetValue(view) is { } bound)
        {
            values.Add(new(ProbeProperties.Value, Format(bound)));
        }

        return values.Count == 0 ? null : ProbePayload.Format(values);
    }

    /// <summary>Colours as <c>#AARRGGBB</c> (what <c>HarnessColor</c> reads); everything else culture-invariant.</summary>
    internal static string? Format(object? value) => value switch
    {
        null => null,
        Color color => ToArgbHex(color),
        SolidColorBrush brush => brush.Color is { } c ? ToArgbHex(c) : null,
        Brush => null, // gradients have no single colour to publish
        bool flag => flag ? "true" : "false",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString(),
    };

    private static string[] Names(VisualElement view) =>
        (GetProperties(view) ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string ToArgbHex(Color color)
    {
        static int Byte(float channel) => (int)Math.Round(channel * 255f, MidpointRounding.AwayFromZero);
        return $"#{Byte(color.Alpha):X2}{Byte(color.Red):X2}{Byte(color.Green):X2}{Byte(color.Blue):X2}";
    }

    /// <summary>Keeps one view's published payload current.</summary>
    private sealed class Publisher
    {
        private readonly VisualElement _view;

        public Publisher(VisualElement view)
        {
            _view = view;
            view.PropertyChanged += (_, e) =>
            {
                // "Description" is SemanticProperties.Description, which Android folds into the same
                // field as the payload (see Apply), so a change to it has to republish.
                if (e.PropertyName == "Description"
                    || Names(view).Contains(e.PropertyName ?? string.Empty, StringComparer.Ordinal))
                {
                    Publish();
                }
            };

            // The platform view may not exist yet when the properties are set, and a recycled
            // view gets a new one — publish again whenever it changes.
            view.HandlerChanged += (_, _) => Publish();
        }

        public void Publish() => Apply(_view, PayloadFor(_view));
    }

    private static void Apply(VisualElement view, string? payload)
    {
#if ANDROID
        // contentDescription is also where SemanticProperties.Description lands on Android, so keep
        // a real description in front of the payload rather than replacing it.
        var description = SemanticProperties.GetDescription(view);
        var text = string.IsNullOrEmpty(payload)
            ? description
            : string.IsNullOrEmpty(description) ? payload : $"{description} {payload}";

        if (view.Handler?.PlatformView is Android.Views.View native)
        {
            native.ContentDescription = text;
        }
#elif IOS
        // accessibilityValue only. The description goes to accessibilityLabel on iOS and is left
        // there: mixing it into the value would make the harness's text read (payload stripped,
        // label as the fallback) return the description instead of the rendered text.
        if (view.Handler?.PlatformView is UIKit.UIView native)
        {
            native.AccessibilityValue = string.IsNullOrEmpty(payload) ? null : payload;
        }
#else
        // No platform view to write to (the plain net10.0 build the unit tests use).
        _ = view;
        _ = payload;
#endif
    }
}
