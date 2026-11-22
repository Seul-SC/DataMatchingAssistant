namespace StudentScoreTool.Themes;

public enum AppTheme
{
    Light = 0,
    Dark = 1,
    MintLight = 2,
    Accompany = 3,
    Ripple = 4
}

public static class AppThemeNames
{
    /// <summary>
    /// 界面中的展示顺序：浅色、薄荷绿、同行、涟漪，深色放在最后。
    /// </summary>
    public static readonly AppTheme[] DisplayOrder =
    [
        AppTheme.Light,
        AppTheme.MintLight,
        AppTheme.Accompany,
        AppTheme.Ripple,
        AppTheme.Dark
    ];

    public static string GetDisplayName(AppTheme theme) => theme switch
    {
        AppTheme.Light => "浅色",
        AppTheme.Dark => "深色",
        AppTheme.MintLight => "薄荷绿",
        AppTheme.Accompany => "同行",
        AppTheme.Ripple => "涟漪",
        _ => "浅色"
    };

    public static bool TryParse(string? value, out AppTheme theme)
    {
        if (Enum.TryParse(value, ignoreCase: true, out theme)
            && Enum.IsDefined(theme))
        {
            return true;
        }

        theme = AppTheme.Light;
        return false;
    }

    public static AppTheme ParseOrFallback(string? value)
    {
        if (IsLegacyMintDeep(value))
        {
            return AppTheme.Light;
        }

        return TryParse(value, out var theme) ? theme : AppTheme.Light;
    }

    public static bool IsLegacyMintDeep(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && string.Equals(value.Trim(), "MintDeep", StringComparison.OrdinalIgnoreCase);
    }
}
