using System.Reflection;

namespace StudentScoreTool;

public static class AppInfo
{
    public const string Name = "数据匹配助手";
    public const string Author = "Seul_SC_照晨";
    public const string GitHubUrl = "https://github.com/Seul-SC/DataMatchingAssistant";
    public const string GitHubIssuesUrl = "https://github.com/Seul-SC/DataMatchingAssistant/issues";
    public const string GiteeRepoUrl = "https://gitee.com/seul-sc/DataMatchingAssistant";
    /// <summary>
    /// 1.2.0 起软件内更新检查使用的 Gitee Releases 静态源。
    /// 请在 Gitee 上维护名为 latest 的 Release，并放入 releases.win.json 与对应 .nupkg。
    /// </summary>
    public const string UpdateFeedUrl = "https://gitee.com/seul-sc/DataMatchingAssistant/releases/download/latest";
    public const string Email = "1753534364@qq.com";
    public const string MailtoUrl = "mailto:1753534364@qq.com";
    public const string PackId = "DataMatchingAssistant";

    public static string Version
    {
        get
        {
            var version = Assembly.GetEntryAssembly()?.GetName().Version
                ?? Assembly.GetExecutingAssembly().GetName().Version
                ?? new Version(1, 2, 0, 0);
            return $"{version.Major}.{version.Minor}.{version.Build}";
        }
    }

    public static string VersionDisplay => $"v{Version}";
}
