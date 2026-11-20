using System.Globalization;

namespace StudentScoreTool.Themes;

public static class ColorContrast
{
    public static double Ratio(string foregroundHex, string backgroundHex)
    {
        var first = RelativeLuminance(foregroundHex);
        var second = RelativeLuminance(backgroundHex);
        var lighter = Math.Max(first, second);
        var darker = Math.Min(first, second);
        return (lighter + 0.05) / (darker + 0.05);
    }

    public static double RelativeLuminance(string hex)
    {
        var (r, g, b) = ParseHex(hex);
        return (0.2126 * Linearize(r)) + (0.7152 * Linearize(g)) + (0.0722 * Linearize(b));
    }

    public static (byte R, byte G, byte B) ParseHex(string hex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hex);
        var value = hex.Trim();
        if (value.StartsWith('#'))
        {
            value = value[1..];
        }

        if (value.Length == 3)
        {
            value = string.Concat(value[0], value[0], value[1], value[1], value[2], value[2]);
        }

        if (value.Length != 6)
        {
            throw new ArgumentException($"无效的颜色值：{hex}", nameof(hex));
        }

        return (
            byte.Parse(value[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(value[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(value[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }

    private static double Linearize(byte component)
    {
        var channel = component / 255.0;
        return channel <= 0.04045
            ? channel / 12.92
            : Math.Pow((channel + 0.055) / 1.055, 2.4);
    }
}
