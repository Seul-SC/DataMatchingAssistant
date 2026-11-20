using StudentScoreTool.Services;
using StudentScoreTool.Themes;

namespace StudentScoreTool.Tests;

public sealed class UpdateMessagesTests
{
    [Fact]
    public void MessagesStayInSimplifiedChineseAndAvoidForbiddenTerms()
    {
        var texts = new[]
        {
            UpdateMessages.UpToDate("1.1.0"),
            UpdateMessages.UpdateAvailable("1.2.0", "1.1.0"),
            UpdateMessages.NotInstalled,
            UpdateMessages.CheckFailed("网络超时"),
            UpdateMessages.DownloadFailed("校验失败"),
            UpdateMessages.NoReleaseNotes
        };

        foreach (var text in texts)
        {
            Assert.False(string.IsNullOrWhiteSpace(text));
            Assert.DoesNotContain("捐款", text);
            Assert.DoesNotContain("募捐", text);
            Assert.DoesNotContain("公益", text);
        }

        Assert.Contains("1.1.0", UpdateMessages.UpToDate("1.1.0"), StringComparison.Ordinal);
        Assert.Contains("1.2.0", UpdateMessages.UpdateAvailable("1.2.0", "1.1.0"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("MintLight", true)]
    [InlineData("mintlight", true)]
    [InlineData("DeepMint", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ThemeParserAcceptsKnownNames(string? value, bool expected)
    {
        var parsed = AppThemeNames.TryParse(value, out var theme);
        Assert.Equal(expected, parsed);
        if (expected)
        {
            Assert.Equal(AppTheme.MintLight, theme);
        }
        else
        {
            Assert.Equal(AppTheme.Light, theme);
        }
    }

    [Fact]
    public void AppInfoConstantsMatchPublicProjectIdentity()
    {
        Assert.Equal("数据匹配助手", AppInfo.Name);
        Assert.Equal("Seul_SC_照晨", AppInfo.Author);
        Assert.Equal("https://github.com/Seul-SC/DataMatchingAssistant", AppInfo.GitHubUrl);
        Assert.Equal("DataMatchingAssistant", AppInfo.PackId);
    }
}
