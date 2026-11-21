namespace StudentScoreTool.Services;

public static class UpdateMessages
{
    public static string UpToDate(string version) =>
        $"当前已是最新版本（{version}）。";

    public static string UpdateAvailable(string newVersion, string currentVersion) =>
        $"发现新版本 {newVersion}（当前为 {currentVersion}）。";

    public const string NotInstalled =
        "当前不是通过安装包运行的版本，无法在线检查或安装更新。请先从 GitHub 下载并运行安装程序。";

    public const string AlreadyChecking = "正在检查更新，请稍候。";

    public const string AlreadyUpdating = "正在下载并安装更新，请稍候。";

    public const string ConfirmWhileBusy = "正在处理表格，更新将会关闭软件。请等待当前任务完成后再更新。";

    public const string NoReleaseNotes = "此版本没有提供更新说明。";

    public static string CheckFailed(string detail) =>
        string.IsNullOrWhiteSpace(detail)
            ? "检查更新失败，请稍后重试。"
            : $"检查更新失败。\n{detail}";

    public static string DownloadFailed(string detail) =>
        string.IsNullOrWhiteSpace(detail)
            ? "下载或安装更新失败，请稍后重试。"
            : $"下载或安装更新失败。\n{detail}";
}
