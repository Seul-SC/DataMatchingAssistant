using StudentScoreTool.Themes;

namespace StudentScoreTool.Tests;

public sealed class ThemePaletteTests
{
    [Fact]
    public void FiveThemesAreDefinedInDisplayOrder()
    {
        Assert.Equal(5, ThemePalette.All.Count);
        Assert.Equal(
            new[] { AppTheme.Light, AppTheme.MintLight, AppTheme.Accompany, AppTheme.Ripple, AppTheme.Dark },
            AppThemeNames.DisplayOrder);
        Assert.False(Enum.IsDefined(typeof(AppTheme), "MintDeep"));
        foreach (var theme in AppThemeNames.DisplayOrder)
        {
            Assert.Contains(theme, ThemePalette.All.Keys);
            Assert.Same(ThemePalette.For(theme), ThemePalette.All[theme]);
        }
    }

    [Theory]
    [InlineData(AppTheme.Light, "浅色")]
    [InlineData(AppTheme.MintLight, "薄荷绿")]
    [InlineData(AppTheme.Accompany, "同行")]
    [InlineData(AppTheme.Ripple, "涟漪")]
    [InlineData(AppTheme.Dark, "深色")]
    public void DisplayNamesAreInSimplifiedChinese(AppTheme theme, string expected)
    {
        Assert.Equal(expected, AppThemeNames.GetDisplayName(theme));
    }

    [Fact]
    public void RippleDisplayNameUsesExactRippleCharacters()
    {
        Assert.Equal("\u6D9F\u6F2A", AppThemeNames.GetDisplayName(AppTheme.Ripple));
        Assert.Equal(2, AppThemeNames.GetDisplayName(AppTheme.Ripple).Length);
    }

    [Fact]
    public void RequestedThemeSwatchesMatchSourceColors()
    {
        Assert.Equal("#98FF98", ThemePalette.MintLight.Swatch, ignoreCase: true);
        Assert.Equal("#194253", ThemePalette.Accompany.Swatch, ignoreCase: true);
        Assert.Equal("#3A66AC", ThemePalette.Ripple.Swatch, ignoreCase: true);
        Assert.Equal("#194253", ThemePalette.Accompany.Accent, ignoreCase: true);
        Assert.Equal("#F8FDFF", ThemePalette.Accompany.WindowBackground, ignoreCase: true);
        Assert.Equal("#EDF9FC", ThemePalette.Accompany.TopBarBackground, ignoreCase: true);
        Assert.Equal("#FFFFFF", ThemePalette.Accompany.CardBackground, ignoreCase: true);
        Assert.Equal("#D5EEF4", ThemePalette.Accompany.Border, ignoreCase: true);
        Assert.Equal("#00232C", ThemePalette.Accompany.TextPrimary, ignoreCase: true);
        Assert.Equal("#FFFFFF", ThemePalette.Accompany.AccentForeground, ignoreCase: true);
        Assert.Equal("#3A66AC", ThemePalette.Ripple.Accent, ignoreCase: true);
        Assert.Equal("#F5F9FF", ThemePalette.Ripple.WindowBackground, ignoreCase: true);
        Assert.Equal("#CBDDFE", ThemePalette.Ripple.TopBarBackground, ignoreCase: true);
        Assert.Equal("#FFFFFF", ThemePalette.Ripple.CardBackground, ignoreCase: true);
        Assert.Equal("#DCE8FF", ThemePalette.Ripple.Border, ignoreCase: true);
        Assert.Equal("#001F4F", ThemePalette.Ripple.TextPrimary, ignoreCase: true);
        Assert.Equal("#FFFFFF", ThemePalette.Ripple.AccentForeground, ignoreCase: true);
    }

    [Theory]
    [InlineData(AppTheme.Light)]
    [InlineData(AppTheme.MintLight)]
    [InlineData(AppTheme.Accompany)]
    [InlineData(AppTheme.Ripple)]
    [InlineData(AppTheme.Dark)]
    public void PrimaryAndMutedTextMeetContrastOnSurfaces(AppTheme theme)
    {
        var palette = ThemePalette.For(theme);
        var surfaces = new[]
        {
            palette.WindowBackground,
            palette.TopBarBackground,
            palette.CardBackground,
            palette.CardAltBackground,
            palette.InputBackground,
            palette.HoverBackground
        };
        var texts = new[] { palette.TextPrimary, palette.TextSecondary, palette.TextMuted };

        foreach (var text in texts)
        {
            foreach (var surface in surfaces)
            {
                Assert.True(
                    ColorContrast.Ratio(text, surface) >= 4.5,
                    $"{theme} 文本 {text} 在背景 {surface} 上的对比度不足。");
            }
        }
    }

    [Theory]
    [InlineData(AppTheme.Light)]
    [InlineData(AppTheme.MintLight)]
    [InlineData(AppTheme.Accompany)]
    [InlineData(AppTheme.Ripple)]
    [InlineData(AppTheme.Dark)]
    public void AccentButtonsMeetContrast(AppTheme theme)
    {
        var palette = ThemePalette.For(theme);
        Assert.True(
            ColorContrast.Ratio(palette.AccentForeground, palette.Accent) >= 4.5,
            $"{theme} 主按钮对比度不足。");
        Assert.True(
            ColorContrast.Ratio(palette.AccentForeground, palette.AccentHover) >= 4.5,
            $"{theme} 主按钮悬停态对比度不足。");
        Assert.True(
            ColorContrast.Ratio(palette.TextPrimary, palette.AccentSoft) >= 4.5,
            $"{theme} 选中高亮上的文字对比度不足。");
    }

    [Theory]
    [InlineData(AppTheme.Light)]
    [InlineData(AppTheme.MintLight)]
    [InlineData(AppTheme.Accompany)]
    [InlineData(AppTheme.Ripple)]
    [InlineData(AppTheme.Dark)]
    public void AttentionButtonsMeetContrast(AppTheme theme)
    {
        var palette = ThemePalette.For(theme);
        Assert.True(
            ColorContrast.Ratio(palette.AttentionForeground, palette.Attention) >= 4.5,
            $"{theme} 问题按钮对比度不足。");
        Assert.True(
            ColorContrast.Ratio(palette.AttentionForeground, palette.AttentionHover) >= 4.5,
            $"{theme} 问题按钮悬停态对比度不足。");
    }

    [Fact]
    public void ColorContrastMatchesKnownValue()
    {
        var ratio = ColorContrast.Ratio("#FFFFFF", "#2563EB");
        Assert.InRange(ratio, 5.1, 5.3);
    }

    [Fact]
    public void UnknownOrRemovedThemesFallBackToLight()
    {
        Assert.False(AppThemeNames.TryParse("MintDeep", out var parsed));
        Assert.Equal(AppTheme.Light, parsed);
        Assert.Equal(AppTheme.Light, AppThemeNames.ParseOrFallback("MintDeep"));
        Assert.Equal(AppTheme.Light, AppThemeNames.ParseOrFallback("深薄荷"));
        Assert.Equal(ThemePalette.Light, ThemePalette.For((AppTheme)99));
    }
}
