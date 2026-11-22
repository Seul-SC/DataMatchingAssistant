using System.IO;
using System.Net.Http;
using System.Security.Authentication;

namespace StudentScoreTool.Services;

public static class UpdateMessages
{
    public static string UpToDate(string version) =>
        $"当前已是最新版本（{version}）。";

    public static string UpdateAvailable(string newVersion, string currentVersion) =>
        $"发现新版本 {newVersion}（当前为 {currentVersion}）。";

    public const string AlreadyChecking = "正在检查更新，请稍候。";

    public const string AlreadyUpdating = "正在下载安装包，请稍候。";

    public const string ConfirmWhileBusy = "正在处理表格，更新将会关闭软件。请等待当前任务完成后再更新。";

    public const string NoReleaseNotes = "此版本没有提供更新说明。";

    public const string InvalidManifest = "服务器上的版本信息格式不正确，请稍后再试，或到 Gitee 发布页手动下载。";

    public const string ChecksumMismatch = "下载的安装包校验没有通过，可能没下载完整。请重试，或到 Gitee 发布页手动下载。";

    public const string SizeMismatch = "下载的安装包大小不对，可能没下载完整。请重试。";

    public const string MigrationTitle = "新版改用新的安装方式";

    public const string MigrationSummary = "新版改用新的安装方式，可自选安装目录。";

    public const string MigrationDetails =
        "从 1.3.0 起，数据匹配助手改用新的安装程序：\n"
        + "· 安装时可以点「浏览」，装到任意文件夹（比如 D 盘）；\n"
        + "· 以后检查更新直接从 Gitee 下载，国内更快。\n\n"
        + "点「下载并安装」后，软件会下载新安装包并打开安装向导，同时关闭当前窗口。"
        + "安装完成后会自动删除旧版本，你的主题和最近打开的文件都会保留。";

    public static string CheckFailed(string detail) =>
        string.IsNullOrWhiteSpace(detail)
            ? "检查更新失败，请稍后重试。"
            : $"检查更新失败。\n{detail}";

    public static string DownloadFailed(string detail) =>
        string.IsNullOrWhiteSpace(detail)
            ? "下载安装包失败，请稍后重试。"
            : $"下载安装包失败。\n{detail}";

    public static string LaunchFailed(string detail) =>
        $"安装包已经下载好，但没能打开安装向导。\n{detail}";

    /// <summary>把网络、文件等异常转换成给用户看的中文说明。</summary>
    public static string Describe(Exception ex) => ex switch
    {
        TaskCanceledException or TimeoutException => "连接超时，请检查网络后重试。",
        HttpRequestException { StatusCode: { } code } when (int)code == 404 =>
            "服务器上暂时找不到新版本的文件，请稍后再试。",
        HttpRequestException { StatusCode: { } code } =>
            $"服务器暂时无法访问（{(int)code}），请稍后再试。",
        HttpRequestException { InnerException: AuthenticationException } =>
            "安全连接失败，请检查电脑时间和网络设置。",
        HttpRequestException => "网络连接失败，请检查网络后重试。",
        FormatException or InvalidDataException => ex.Message,
        UnauthorizedAccessException => "没有权限写入临时文件夹，请检查安全软件设置后重试。",
        IOException => $"保存安装包时出错：{ex.Message}",
        _ => ex.Message
    };
}
