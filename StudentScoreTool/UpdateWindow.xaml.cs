using System.ComponentModel;
using System.Windows;
using StudentScoreTool.Services;

namespace StudentScoreTool;

public partial class UpdateWindow : Window
{
    private readonly UpdateCheckResult _result;
    private readonly UpdateService _updateService;
    private readonly string? _legacyRoot;
    private readonly string _updateButtonText;
    private bool _isUpdating;

    /// <summary>安装向导已经启动，调用方应退出软件。</summary>
    public bool StartedUpdate { get; private set; }

    /// <param name="legacyRoot">旧版 Velopack 安装根目录。不为空时显示「改用新安装方式」的说明，并让安装包删除旧版。</param>
    public UpdateWindow(UpdateCheckResult result, UpdateService updateService, string? legacyRoot = null)
    {
        InitializeComponent();
        _result = result;
        _updateService = updateService;
        _legacyRoot = legacyRoot;

        var notes = string.IsNullOrWhiteSpace(result.ReleaseNotes)
            ? UpdateMessages.NoReleaseNotes
            : result.ReleaseNotes;

        if (legacyRoot is null)
        {
            SummaryTextBlock.Text = result.Message;
            NotesTextBlock.Text = notes;
            _updateButtonText = "更新";
        }
        else
        {
            Title = UpdateMessages.MigrationTitle;
            HeaderTextBlock.Text = UpdateMessages.MigrationTitle;
            SummaryTextBlock.Text = UpdateMessages.MigrationSummary;
            NotesTextBlock.Text = $"{UpdateMessages.MigrationDetails}\n\n—— {result.AvailableVersion} 更新说明 ——\n{notes}";
            _updateButtonText = "下载并安装";
        }

        UpdateButton.Content = _updateButtonText;
    }

    private async void Update_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdating || _result.Manifest is null)
        {
            return;
        }

        _isUpdating = true;
        LaterButton.IsEnabled = false;
        UpdateButton.IsEnabled = false;
        UpdateButton.Content = "正在下载…";
        ProgressTextBlock.Visibility = Visibility.Visible;
        DownloadProgressBar.Visibility = Visibility.Visible;
        DownloadProgressBar.Value = 0;
        ProgressTextBlock.Text = "正在下载安装包…";

        try
        {
            var progress = new Progress<int>(percent =>
            {
                DownloadProgressBar.Value = percent;
                ProgressTextBlock.Text = $"正在下载安装包… {percent}%";
            });

            var installer = await _updateService.DownloadInstallerAsync(_result.Manifest, progress);
            ProgressTextBlock.Text = "下载完成，正在打开安装向导…";
            UpdateService.LaunchInstaller(installer, LegacyInstall.BuildInstallerArguments(_legacyRoot));

            StartedUpdate = true;
            _isUpdating = false;
            DialogResult = true;
        }
        catch (Exception ex)
        {
            _isUpdating = false;
            StartedUpdate = false;
            LaterButton.IsEnabled = true;
            UpdateButton.IsEnabled = true;
            UpdateButton.Content = _updateButtonText;
            ProgressTextBlock.Visibility = Visibility.Collapsed;
            DownloadProgressBar.Visibility = Visibility.Collapsed;
            var detail = ex is InvalidOperationException { InnerException: Win32Exception }
                ? ex.Message
                : UpdateMessages.DownloadFailed(UpdateMessages.Describe(ex))
                  + $"\n\n也可以到 Gitee 发布页手动下载：{AppInfo.GiteeReleasesUrl}";
            MessageBox.Show(this, detail, "更新失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Later_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_isUpdating)
        {
            e.Cancel = true;
        }
    }
}
