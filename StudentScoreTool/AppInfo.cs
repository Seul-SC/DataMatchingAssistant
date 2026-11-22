using System.Reflection;

namespace StudentScoreTool;

public static class AppInfo
{
    public const string Name = "数据匹配助手";
    public const string Author = "Seul_SC_照晨";
    public const string GitHubUrl = "https://github.com/Seul-SC/DataMatchingAssistant";
    public const string GitHubIssuesUrl = "https://github.com/Seul-SC/DataMatchingAssistant/issues";
    public const string GiteeRepoUrl = "https://gitee.com/seul-sc/DataMatchingAssistant";
    public const string GiteeReleasesUrl = "https://gitee.com/seul-sc/DataMatchingAssistant/releases";

    /// <summary>
    /// 1.3.0 起软件检查更新时读取的版本信息文件（Gitee 仓库 main 分支里的 update/version.json）。
    /// 发版时先把安装包上传到 Gitee Release，再更新这个文件，用户才会看到新版本。
    /// </summary>
    public const string UpdateManifestUrl = "https://gitee.com/seul-sc/DataMatchingAssistant/raw/main/update/version.json";

    /// <summary>Gitee 读取失败时的备用地址（GitHub 仓库里的同一个文件）。</summary>
    public const string UpdateManifestFallbackUrl = "https://raw.githubusercontent.com/Seul-SC/DataMatchingAssistant/main/update/version.json";

    public const string Email = "1753534364@qq.com";
    public const string MailtoUrl = "mailto:1753534364@qq.com";

    /// <summary>旧版 Velopack 安装使用的应用 ID，也是设置文件夹和安装包文件名的前缀。</summary>
    public const string PackId = "DataMatchingAssistant";

    public static string Version
    {
        get
        {
            var version = Assembly.GetEntryAssembly()?.GetName().Version
                ?? Assembly.GetExecutingAssembly().GetName().Version
                ?? new Version(1, 3, 0, 0);
            return $"{version.Major}.{version.Minor}.{version.Build}";
        }
    }

    public static string VersionDisplay => $"v{Version}";
}
