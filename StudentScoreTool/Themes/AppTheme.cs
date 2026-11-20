namespace StudentScoreTool.Themes;

public enum AppTheme
{
    Light = 0,
    Dark = 1,
    MintLight = 2,
    MintDeep = 3
}

public static class AppThemeNames
{
    public static string GetDisplayName(AppTheme theme) => theme switch
    {
        AppTheme.Light => "浅色",
        AppTheme.Dark => "深色",
        AppTheme.MintLight => "薄荷绿",
        AppTheme.MintDeep => "深薄荷",
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
}
