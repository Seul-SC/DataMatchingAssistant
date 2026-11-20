using System.ComponentModel;
using System.Windows;
using StudentScoreTool.Services;

namespace StudentScoreTool;

public partial class UpdateWindow : Window
{
    private readonly UpdateCheckResult _result;
    private readonly UpdateService _updateService;
    private bool _isUpdating;

    public bool StartedUpdate { get; private set; }

    public UpdateWindow(UpdateCheckResult result, UpdateService updateService)
    {
        InitializeComponent();
        _result = result;
        _updateService = updateService;
        SummaryTextBlock.Text = result.Message;
        NotesTextBlock.Text = string.IsNullOrWhiteSpace(result.ReleaseNotes)
            ? UpdateMessages.NoReleaseNotes
            : result.ReleaseNotes;
    }

    private async void Update_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdating || _result.Manager is null || _result.Update is null)
        {
            return;
        }

        _isUpdating = true;
        StartedUpdate = true;
        LaterButton.IsEnabled = false;
        UpdateButton.IsEnabled = false;
        UpdateButton.Content = "正在更新…";
        ProgressTextBlock.Visibility = Visibility.Visible;
        DownloadProgressBar.Visibility = Visibility.Visible;
        ProgressTextBlock.Text = "正在下载更新包…";

        try
        {
            await _updateService.DownloadAndApplyAsync(
                _result.Manager,
                _result.Update,
                progress => Dispatcher.Invoke(() =>
                {
                    DownloadProgressBar.Value = progress;
                    ProgressTextBlock.Text = $"正在下载更新包… {progress}%";
                }));
        }
        catch (Exception ex)
        {
            _isUpdating = false;
            StartedUpdate = false;
            LaterButton.IsEnabled = true;
            UpdateButton.IsEnabled = true;
            UpdateButton.Content = "立即更新";
            MessageBox.Show(this, UpdateMessages.DownloadFailed(ex.Message), "更新失败",
                MessageBoxButton.OK, MessageBoxImage.Warning);
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
