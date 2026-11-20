using System.Reflection;

namespace StudentScoreTool;

public static class AppInfo
{
    public const string Name = "数据匹配助手";
    public const string Author = "Seul_SC_照晨";
    public const string GitHubUrl = "https://github.com/Seul-SC/DataMatchingAssistant";
    public const string PackId = "DataMatchingAssistant";

    public static string Version
    {
        get
        {
            var version = Assembly.GetEntryAssembly()?.GetName().Version
                ?? Assembly.GetExecutingAssembly().GetName().Version
                ?? new Version(1, 1, 0, 0);
            return $"{version.Major}.{version.Minor}.{version.Build}";
        }
    }

    public static string VersionDisplay => $"v{Version}";
}
