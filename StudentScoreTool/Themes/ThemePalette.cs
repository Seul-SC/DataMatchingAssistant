namespace StudentScoreTool.Themes;

public sealed record ThemePalette(
    string WindowBackground,
    string TopBarBackground,
    string CardBackground,
    string CardAltBackground,
    string InputBackground,
    string HoverBackground,
    string Border,
    string TextPrimary,
    string TextSecondary,
    string TextMuted,
    string Accent,
    string AccentHover,
    string AccentForeground,
    string AccentSoft,
    string Danger,
    string Success,
    string Attention,
    string AttentionHover,
    string AttentionForeground,
    string Swatch)
{
    public static ThemePalette For(AppTheme theme) => theme switch
    {
        AppTheme.Dark => Dark,
        AppTheme.MintLight => MintLight,
        AppTheme.Accompany => Accompany,
        AppTheme.Ripple => Ripple,
        _ => Light
    };

    // 浅色：接近 Windows 11 的干净工具风，白卡片 + 中性灰背景 + 蓝色主按钮。
    public static ThemePalette Light { get; } = new(
        WindowBackground: "#F3F4F6",
        TopBarBackground: "#FFFFFF",
        CardBackground: "#FFFFFF",
        CardAltBackground: "#F7F8FA",
        InputBackground: "#FFFFFF",
        HoverBackground: "#EEF2F7",
        Border: "#E2E5EB",
        TextPrimary: "#1C2330",
        TextSecondary: "#4B5565",
        TextMuted: "#5B6573",
        Accent: "#2563EB",
        AccentHover: "#1D4ED8",
        AccentForeground: "#FFFFFF",
        AccentSoft: "#EDF3FF",
        Danger: "#DC2626",
        Success: "#108548",
        Attention: "#B45309",
        AttentionHover: "#92400E",
        AttentionForeground: "#FFFFFF",
        Swatch: "#F3F4F6");

    // 深色：深灰表面分层，主按钮用亮蓝底配深色文字，保证对比度。
    public static ThemePalette Dark { get; } = new(
        WindowBackground: "#1B1D21",
        TopBarBackground: "#22252A",
        CardBackground: "#25272C",
        CardAltBackground: "#2C2F36",
        InputBackground: "#2C2F36",
        HoverBackground: "#32363E",
        Border: "#3A3E46",
        TextPrimary: "#F3F5F7",
        TextSecondary: "#C5CAD3",
        TextMuted: "#9AA3B0",
        Accent: "#4C8DFF",
        AccentHover: "#7EB0FF",
        AccentForeground: "#0B1220",
        AccentSoft: "#243F73",
        Danger: "#F87171",
        Success: "#4ADE80",
        Attention: "#FBBF24",
        AttentionHover: "#FCD34D",
        AttentionForeground: "#1A1304",
        Swatch: "#1B1D21");

    // 薄荷绿：#98FF98 只作为点缀色，背景用淡薄荷中性色，主按钮用深绿配白字。
    public static ThemePalette MintLight { get; } = new(
        WindowBackground: "#EAF8EA",
        TopBarBackground: "#F7FFF7",
        CardBackground: "#FFFFFF",
        CardAltBackground: "#F3FBF3",
        InputBackground: "#FFFFFF",
        HoverBackground: "#E3F6E3",
        Border: "#B9E6B9",
        TextPrimary: "#123126",
        TextSecondary: "#2A4A38",
        TextMuted: "#3F5F4C",
        Accent: "#1B6B45",
        AccentHover: "#155738",
        AccentForeground: "#FFFFFF",
        AccentSoft: "#CFF8CF",
        Danger: "#C62828",
        Success: "#157A45",
        Attention: "#C2410C",
        AttentionHover: "#9A3412",
        AttentionForeground: "#FFFFFF",
        Swatch: "#98FF98");

    // 同行：色值取自同步待办 1.2.0 的 accompany 主题，只模仿颜色。
    // 主色 #194253，悬停态为略提亮的同色（仍与白字对比 ≥ 4.5:1）。
    // AccentSoft / HoverBackground 为主色约 16% / 14% 透明度叠在浅色表面上的实色。
    public static ThemePalette Accompany { get; } = new(
        WindowBackground: "#F8FDFF",
        TopBarBackground: "#EDF9FC",
        CardBackground: "#FFFFFF",
        CardAltBackground: "#E5FAFF",
        InputBackground: "#FFFFFF",
        HoverBackground: "#D9E3E7",
        Border: "#D5EEF4",
        TextPrimary: "#00232C",
        TextSecondary: "#334F56",
        TextMuted: "#405A61",
        Accent: "#194253",
        AccentHover: "#355968",
        AccentForeground: "#FFFFFF",
        AccentSoft: "#DAE1E3",
        Danger: "#C62828",
        Success: "#157A45",
        Attention: "#C2410C",
        AttentionHover: "#9A3412",
        AttentionForeground: "#FFFFFF",
        Swatch: "#194253");

    // 涟漪：色值取自同步待办 1.2.0 的 ripple 主题，只模仿颜色。显示名必须是「涟漪」。
    public static ThemePalette Ripple { get; } = new(
        WindowBackground: "#F5F9FF",
        TopBarBackground: "#CBDDFE",
        CardBackground: "#FFFFFF",
        CardAltBackground: "#E6EFFF",
        InputBackground: "#FFFFFF",
        HoverBackground: "#DBE4F3",
        Border: "#DCE8FF",
        TextPrimary: "#001F4F",
        TextSecondary: "#334C72",
        TextMuted: "#40577B",
        Accent: "#3A66AC",
        AccentHover: "#4A72B3",
        AccentForeground: "#FFFFFF",
        AccentSoft: "#DFE7F2",
        Danger: "#C62828",
        Success: "#157A45",
        Attention: "#C2410C",
        AttentionHover: "#9A3412",
        AttentionForeground: "#FFFFFF",
        Swatch: "#3A66AC");

    public static IReadOnlyDictionary<AppTheme, ThemePalette> All { get; } =
        new Dictionary<AppTheme, ThemePalette>
        {
            [AppTheme.Light] = Light,
            [AppTheme.MintLight] = MintLight,
            [AppTheme.Accompany] = Accompany,
            [AppTheme.Ripple] = Ripple,
            [AppTheme.Dark] = Dark
        };
}
