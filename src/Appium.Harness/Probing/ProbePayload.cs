// Dependency-free on purpose: an app under test can link-compile this one file
// (<Compile Include="…/ProbePayload.cs" Link="…" />) so the app that writes the payload and the
// harness that reads it share one implementation and cannot drift, without the app taking a
// dependency on Appium. Keep it to the BCL — no Selenium, no Appium.

using System.Text;

namespace Appium.Harness;

/// <summary>
/// The wire format of the test probe: a set of named properties folded into one string that an
/// accessibility tree can carry.
///
/// <para>A native accessibility tree exposes text, ids and a few state flags — never a MAUI
/// <c>Color</c> or a view-model property. The app publishes the ones a test cares about into a
/// field the tree <i>does</i> carry, in the form <c>[[probe:Name=value;Name2=value2]]</c>, and
/// the harness reads them back out. The marker lets the payload share a field with a real
/// accessibility label, and the escaping means a value can contain any character.</para>
/// </summary>
public static class ProbePayload
{
    private const string Open = "[[probe:";
    private const string Close = "]]";

    /// <summary>Folds properties into <c>[[probe:Name=value;...]]</c>. Null values are written empty.</summary>
    public static string Format(IEnumerable<KeyValuePair<string, string?>> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);

        var builder = new StringBuilder(Open);
        var first = true;
        foreach (var (name, value) in properties)
        {
            if (!first)
            {
                builder.Append(';');
            }

            first = false;
            Escape(builder, name);
            builder.Append('=');
            Escape(builder, value ?? string.Empty);
        }

        return builder.Append(Close).ToString();
    }

    /// <summary>
    /// Every property in the first payload found in <paramref name="carrier"/>, names compared
    /// case-insensitively. Empty when the field holds no payload or a malformed one — never throws.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Parse(string? carrier)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(carrier))
        {
            return result;
        }

        var start = carrier.IndexOf(Open, StringComparison.Ordinal);
        if (start < 0)
        {
            return result;
        }

        var name = new StringBuilder();
        var current = new StringBuilder();
        var haveName = false;
        var closed = false;

        for (var i = start + Open.Length; i < carrier.Length; i++)
        {
            var c = carrier[i];

            if (c == '\\' && i + 1 < carrier.Length)
            {
                current.Append(carrier[++i]);
            }
            else if (c == '=' && !haveName)
            {
                name.Append(current);
                current.Clear();
                haveName = true;
            }
            else if (c == ';' || (c == ']' && string.CompareOrdinal(carrier, i, Close, 0, Close.Length) == 0))
            {
                if (haveName && name.Length > 0)
                {
                    result[name.ToString()] = current.ToString();
                }

                name.Clear();
                current.Clear();
                haveName = false;

                if (c == ']')
                {
                    closed = true;
                    break;
                }
            }
            else
            {
                current.Append(c);
            }
        }

        // An unterminated payload is a truncated field, not data we can trust.
        return closed ? result : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Reads one property from the payload in <paramref name="carrier"/>.</summary>
    public static bool TryGet(string? carrier, string name, out string value)
    {
        var found = Parse(carrier).TryGetValue(name, out var raw);
        value = raw ?? string.Empty;
        return found;
    }

    /// <summary>
    /// <paramref name="carrier"/> with its payload (if any) removed and the ends trimmed. A field
    /// that carries no payload comes back untouched, so this is safe to apply to any text.
    /// </summary>
    public static string Strip(string? carrier)
    {
        if (string.IsNullOrEmpty(carrier))
        {
            return carrier ?? string.Empty;
        }

        var start = carrier.IndexOf(Open, StringComparison.Ordinal);
        if (start < 0)
        {
            return carrier;
        }

        // Find the real terminator the same way Parse does: skipping escaped characters.
        for (var i = start + Open.Length; i < carrier.Length; i++)
        {
            if (carrier[i] == '\\')
            {
                i++;
            }
            else if (carrier[i] == ']' && string.CompareOrdinal(carrier, i, Close, 0, Close.Length) == 0)
            {
                return (carrier[..start] + carrier[(i + Close.Length)..]).Trim();
            }
        }

        return carrier; // unterminated: leave it, as Parse does
    }

    private static void Escape(StringBuilder builder, string text)
    {
        foreach (var c in text)
        {
            if (c is '\\' or ';' or '=' or ']')
            {
                builder.Append('\\');
            }

            builder.Append(c);
        }
    }
}

/// <summary>
/// Property names with a defined meaning to both the app that publishes and the harness that reads.
/// They are MAUI's own property names, so <c>TestProbe.Properties="TextColor"</c> reads naturally.
/// </summary>
public static class ProbeProperties
{
    /// <summary>A view's text colour.</summary>
    public const string TextColor = "TextColor";

    /// <summary>A view's background colour.</summary>
    public const string BackgroundColor = "BackgroundColor";

    /// <summary>The free-form value bound through <c>TestProbe.Value</c> — any view-model data a test wants to assert on.</summary>
    public const string Value = "Value";
}
