using System.IO;

namespace StudentScoreTool.Services;

/// <summary>
/// 识别旧版 Velopack 安装（1.1.0 / 1.2.0）。Velopack 的目录结构是：
/// &lt;安装根目录&gt;\Update.exe 和 &lt;安装根目录&gt;\current\DataMatchingAssistant.exe。
/// </summary>
public static class LegacyInstall
{
    /// <summary>
    /// 当前进程是不是从旧版 Velopack 安装里运行的过渡包。只有用 -p:VelopackBridge=true 编译的版本才会返回路径，
    /// 正式的 Inno 版本永远返回 null。
    /// </summary>
    public static string? CurrentBridgeRoot
    {
        get
        {
#if VELOPACK_BRIDGE
            return FindVelopackRoot(AppContext.BaseDirectory);
#else
            return null;
#endif
        }
    }

    /// <summary>如果 <paramref name="appDirectory"/> 是 Velopack 的 current 文件夹，返回安装根目录（不带结尾斜杠）。</summary>
    public static string? FindVelopackRoot(string? appDirectory)
    {
        if (string.IsNullOrWhiteSpace(appDirectory))
        {
            return null;
        }

        try
        {
            var current = new DirectoryInfo(Path.TrimEndingDirectorySeparator(appDirectory));
            if (!string.Equals(current.Name, "current", StringComparison.OrdinalIgnoreCase) || current.Parent is null)
            {
                return null;
            }

            var root = current.Parent.FullName;
            return File.Exists(Path.Combine(root, "Update.exe"))
                ? Path.TrimEndingDirectorySeparator(root)
                : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>传给 Inno 安装包的参数，安装包会据此删除旧版。路径末尾不能带反斜杠，否则会把引号转义掉。</summary>
    public static string BuildInstallerArguments(string? legacyRoot)
    {
        if (string.IsNullOrWhiteSpace(legacyRoot))
        {
            return "";
        }

        var trimmed = legacyRoot.TrimEnd('\\', '/');
        return $"/VELOPACKROOT=\"{trimmed}\"";
    }
}
