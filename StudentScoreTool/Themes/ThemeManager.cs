using System.Windows;
using System.Windows.Media;

namespace StudentScoreTool.Themes;

public static class ThemeManager
{
    public static AppTheme Current { get; private set; } = AppTheme.Light;

    public static void Apply(AppTheme theme)
    {
        var palette = ThemePalette.For(theme);
        var resources = Application.Current?.Resources;
        if (resources is null)
        {
            return;
        }

        SetBrush(resources, "WindowBackgroundBrush", palette.WindowBackground);
        SetBrush(resources, "TopBarBackgroundBrush", palette.TopBarBackground);
        SetBrush(resources, "CardBackgroundBrush", palette.CardBackground);
        SetBrush(resources, "CardAltBackgroundBrush", palette.CardAltBackground);
        SetBrush(resources, "InputBackgroundBrush", palette.InputBackground);
        SetBrush(resources, "HoverBackgroundBrush", palette.HoverBackground);
        SetBrush(resources, "BorderBrush", palette.Border);
        SetBrush(resources, "TextPrimaryBrush", palette.TextPrimary);
        SetBrush(resources, "TextSecondaryBrush", palette.TextSecondary);
        SetBrush(resources, "TextMutedBrush", palette.TextMuted);
        SetBrush(resources, "AccentBrush", palette.Accent);
        SetBrush(resources, "AccentHoverBrush", palette.AccentHover);
        SetBrush(resources, "AccentForegroundBrush", palette.AccentForeground);
        SetBrush(resources, "AccentSoftBrush", palette.AccentSoft);
        SetBrush(resources, "DangerBrush", palette.Danger);
        SetBrush(resources, "SuccessBrush", palette.Success);
        SetBrush(resources, "AttentionBrush", palette.Attention);
        SetBrush(resources, "AttentionHoverBrush", palette.AttentionHover);
        SetBrush(resources, "AttentionForegroundBrush", palette.AttentionForeground);
        Current = theme;
    }

    public static Color ParseColor(string hex)
    {
        var (r, g, b) = ColorContrast.ParseHex(hex);
        return Color.FromRgb(r, g, b);
    }

    private static void SetBrush(ResourceDictionary resources, string key, string hex)
    {
        var brush = new SolidColorBrush(ParseColor(hex));
        brush.Freeze();
        resources[key] = brush;
    }
}
