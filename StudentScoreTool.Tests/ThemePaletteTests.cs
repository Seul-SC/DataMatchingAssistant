using StudentScoreTool.Themes;

namespace StudentScoreTool.Tests;

public sealed class ThemePaletteTests
{
    [Fact]
    public void AllFourThemesAreDefined()
    {
        Assert.Equal(4, ThemePalette.All.Count);
        Assert.Contains(AppTheme.Light, ThemePalette.All.Keys);
        Assert.Contains(AppTheme.Dark, ThemePalette.All.Keys);
        Assert.Contains(AppTheme.MintLight, ThemePalette.All.Keys);
        Assert.Contains(AppTheme.MintDeep, ThemePalette.All.Keys);
    }

    [Theory]
    [InlineData(AppTheme.Light, "浅色")]
    [InlineData(AppTheme.Dark, "深色")]
    [InlineData(AppTheme.MintLight, "薄荷绿")]
    [InlineData(AppTheme.MintDeep, "深薄荷")]
    public void DisplayNamesAreInSimplifiedChinese(AppTheme theme, string expected)
    {
        Assert.Equal(expected, AppThemeNames.GetDisplayName(theme));
    }

    [Fact]
    public void MintThemesUseRequestedAccentSwatches()
    {
        Assert.Equal("#98FF98", ThemePalette.MintLight.Swatch, ignoreCase: true);
        Assert.Equal("#3EB489", ThemePalette.MintDeep.Swatch, ignoreCase: true);
    }

    [Theory]
    [InlineData(AppTheme.Light)]
    [InlineData(AppTheme.Dark)]
    [InlineData(AppTheme.MintLight)]
    [InlineData(AppTheme.MintDeep)]
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
    [InlineData(AppTheme.Dark)]
    [InlineData(AppTheme.MintLight)]
    [InlineData(AppTheme.MintDeep)]
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
    [InlineData(AppTheme.Dark)]
    [InlineData(AppTheme.MintLight)]
    [InlineData(AppTheme.MintDeep)]
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
}
