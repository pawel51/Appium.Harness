using System.Globalization;

namespace Appium.Harness;

/// <summary>
/// A colour normalised so that what the app reports and what a test expects can be compared:
/// the native probe publishes <c>#AARRGGBB</c> hex, a webview reports CSS
/// <c>rgb()</c>/<c>rgba()</c>, and a test author types whichever is handy.
///
/// <para>Accepted input: <c>#RGB</c>, <c>#RRGGBB</c>, <c>#AARRGGBB</c> (the .NET MAUI / Android
/// order — <b>not</b> CSS's <c>#RRGGBBAA</c>), <c>rgb(r, g, b)</c>, <c>rgba(r, g, b, a)</c>
/// (comma or CSS4 space/slash syntax; channels as numbers or percentages; alpha as 0–1 or a
/// percentage) and <c>transparent</c>.</para>
/// </summary>
public readonly record struct HarnessColor(byte A, byte R, byte G, byte B)
{
    /// <summary>Parses a colour, throwing <see cref="FormatException"/> when it is not one.</summary>
    public static HarnessColor Parse(string value) =>
        TryParse(value, out var color)
            ? color
            : throw new FormatException(
                $"'{value}' is not a colour this harness understands. " +
                "Use #RGB, #RRGGBB, #AARRGGBB, rgb(r, g, b), rgba(r, g, b, a) or transparent.");

    /// <summary>Parses a colour, returning false instead of throwing.</summary>
    public static bool TryParse(string? value, out HarnessColor color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();

        if (text.StartsWith('#'))
        {
            return TryParseHex(text[1..], out color);
        }

        if (text.Equals("transparent", StringComparison.OrdinalIgnoreCase))
        {
            color = new HarnessColor(0, 0, 0, 0);
            return true;
        }

        return TryParseFunctional(text, out color);
    }

    /// <summary>Lets a test pass a colour string wherever a <see cref="HarnessColor"/> is expected.</summary>
    public static implicit operator HarnessColor(string value) => Parse(value);

    /// <summary>
    /// Whether every channel, alpha included, is within <paramref name="tolerance"/> of
    /// <paramref name="other"/>. A little tolerance absorbs the rounding between a MAUI float
    /// colour and the 8-bit value a platform reports; use 0 when the exact value is the point.
    /// </summary>
    public bool IsCloseTo(HarnessColor other, int tolerance = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tolerance);
        return Math.Abs(A - other.A) <= tolerance
            && Math.Abs(R - other.R) <= tolerance
            && Math.Abs(G - other.G) <= tolerance
            && Math.Abs(B - other.B) <= tolerance;
    }

    /// <summary>The colour as <c>#AARRGGBB</c>.</summary>
    public string ToHex() => $"#{A:X2}{R:X2}{G:X2}{B:X2}";

    /// <inheritdoc />
    public override string ToString() => ToHex();

    private static bool TryParseHex(string digits, out HarnessColor color)
    {
        color = default;
        if (!digits.All(Uri.IsHexDigit))
        {
            return false;
        }

        switch (digits.Length)
        {
            case 3:
                color = new HarnessColor(255, Nibble(digits[0]), Nibble(digits[1]), Nibble(digits[2]));
                return true;
            case 6:
                color = new HarnessColor(255, Byte(digits, 0), Byte(digits, 2), Byte(digits, 4));
                return true;
            case 8:
                color = new HarnessColor(Byte(digits, 0), Byte(digits, 2), Byte(digits, 4), Byte(digits, 6));
                return true;
            default:
                return false;
        }

        static byte Nibble(char c)
        {
            var n = Convert.ToByte(c.ToString(), 16);
            return (byte)(n * 17); // 0xF -> 0xFF
        }

        static byte Byte(string s, int start) =>
            byte.Parse(s.AsSpan(start, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    private static bool TryParseFunctional(string text, out HarnessColor color)
    {
        color = default;

        var open = text.IndexOf('(');
        if (open < 0 || !text.EndsWith(')'))
        {
            return false;
        }

        var name = text[..open].Trim().ToLowerInvariant();
        if (name is not ("rgb" or "rgba"))
        {
            return false;
        }

        var parts = text[(open + 1)..^1]
            .Split([',', ' ', '/', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is not (3 or 4))
        {
            return false;
        }

        if (!TryChannel(parts[0], out var r) || !TryChannel(parts[1], out var g) || !TryChannel(parts[2], out var b))
        {
            return false;
        }

        byte alpha = 255;
        if (parts.Length == 4 && !TryAlpha(parts[3], out alpha))
        {
            return false;
        }

        color = new HarnessColor(alpha, r, g, b);
        return true;
    }

    private static bool TryChannel(string token, out byte channel)
    {
        channel = 0;
        if (!TryNumber(token, out var value, out var percent))
        {
            return false;
        }

        var scaled = percent ? value * 255 / 100 : value;
        channel = ClampToByte(scaled);
        return true;
    }

    private static bool TryAlpha(string token, out byte alpha)
    {
        alpha = 0;
        if (!TryNumber(token, out var value, out var percent))
        {
            return false;
        }

        alpha = ClampToByte((percent ? value / 100 : value) * 255);
        return true;
    }

    private static bool TryNumber(string token, out double value, out bool percent)
    {
        percent = token.EndsWith('%');
        return double.TryParse(
            percent ? token[..^1] : token,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value) && double.IsFinite(value);
    }

    private static byte ClampToByte(double value) =>
        (byte)Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), 0, 255);
}
