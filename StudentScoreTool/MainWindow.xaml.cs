using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using StudentScoreTool.Services;
using StudentScoreTool.Themes;

namespace StudentScoreTool;

public partial class MainWindow : Window
{
    private readonly ExcelProcessor _processor = new();
    private readonly UpdateService _updateService = new();
    private readonly AppSettings _settings = AppSettings.Load();
    private bool _isBusy;
    private bool _isCheckingUpdate;
    private bool _isApplyingUpdate;
    private int _sourceDragCount;
    private int _targetDragCount;
    private string? _sourcePath;
    private string? _targetPath;
    private string? _lastOutputPath;
    private UpdateCheckResult? _pendingUpdate;

    public MainWindow()
    {
        InitializeComponent();
        VersionTextBlock.Text = AppInfo.VersionDisplay;
        RefreshThemeSwatches();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        RestoreRememberedFiles();
        await CheckUpdatesInBackgroundAsync();
    }

    private void RestoreRememberedFiles()
    {
        if (File.Exists(_settings.LastSourcePath))
        {
            TryApplyFile(SheetRole.Source, _settings.LastSourcePath!);
        }

        if (File.Exists(_settings.LastTargetPath))
        {
            TryApplyFile(SheetRole.Target, _settings.LastTargetPath!);
        }
    }

    private void ThemeSwatch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element
            || !AppThemeNames.TryParse(element.Tag as string, out var theme))
        {
            return;
        }

        ThemeManager.Apply(theme);
        _settings.Theme = theme;
        _settings.Save();
        RefreshThemeSwatches();
    }

    private void RefreshThemeSwatches()
    {
        HighlightSwatch(ThemeLightRing, ThemeManager.Current == AppTheme.Light);
        HighlightSwatch(ThemeDarkRing, ThemeManager.Current == AppTheme.Dark);
        HighlightSwatch(ThemeMintLightRing, ThemeManager.Current == AppTheme.MintLight);
        HighlightSwatch(ThemeMintDeepRing, ThemeManager.Current == AppTheme.MintDeep);
    }

    private static void HighlightSwatch(Border ring, bool selected)
    {
        ring.BorderBrush = selected
            ? (Brush)Application.Current.Resources["AccentBrush"]
            : (Brush)Application.Current.Resources["BorderBrush"];
        ring.BorderThickness = new Thickness(selected ? 3 : 2);
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AboutWindow { Owner = this };
        dialog.ShowDialog();
    }

    private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_isApplyingUpdate)
        {
            MessageBox.Show(this, UpdateMessages.AlreadyUpdating, "检查更新",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_isBusy)
        {
            MessageBox.Show(this, UpdateMessages.ConfirmWhileBusy, "检查更新",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_isCheckingUpdate)
        {
            MessageBox.Show(this, UpdateMessages.AlreadyChecking, "检查更新",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        await CheckUpdatesAsync(showWindow: true);
    }

    private async Task CheckUpdatesInBackgroundAsync()
    {
        await CheckUpdatesAsync(showWindow: false);
    }

    private async Task CheckUpdatesAsync(bool showWindow)
    {
        if (_isCheckingUpdate)
        {
            return;
        }

        _isCheckingUpdate = true;
        var previousContent = CheckUpdateButton.Content;
        if (showWindow)
        {
            CheckUpdateButton.Content = "正在检查…";
            CheckUpdateButton.IsEnabled = false;
        }

        try
        {
            var result = await _updateService.CheckForUpdatesAsync();
            _pendingUpdate = result.Availability == UpdateAvailability.Available ? result : null;
            UpdateBadge.Visibility = _pendingUpdate is null ? Visibility.Collapsed : Visibility.Visible;

            if (!showWindow)
            {
                return;
            }

            if (result.Availability == UpdateAvailability.Available
                && result.Update is not null
                && result.Manager is not null)
            {
                var dialog = new UpdateWindow(result, _updateService) { Owner = this };
                dialog.ShowDialog();
                if (dialog.StartedUpdate)
                {
                    _isApplyingUpdate = true;
                }
                return;
            }

            var icon = result.Availability == UpdateAvailability.Error
                ? MessageBoxImage.Warning
                : MessageBoxImage.Information;
            MessageBox.Show(this, result.Message, "检查更新", MessageBoxButton.OK, icon);
        }
        finally
        {
            _isCheckingUpdate = false;
            CheckUpdateButton.Content = previousContent;
            CheckUpdateButton.IsEnabled = true;
        }
    }

    private void SelectSource_Click(object sender, RoutedEventArgs e) => SelectFile(SheetRole.Source);

    private void SelectTarget_Click(object sender, RoutedEventArgs e) => SelectFile(SheetRole.Target);

    private void SelectFile(SheetRole role)
    {
        if (_isBusy)
        {
            return;
        }

        var currentPath = role == SheetRole.Source ? _sourcePath : _targetPath;
        var dialog = new OpenFileDialog
        {
            Title = role == SheetRole.Source ? "选择有成绩的 Excel" : "选择需要填写成绩的名单 Excel",
            Filter = "Excel 工作簿 (*.xlsx)|*.xlsx",
            CheckFileExists = true,
            Multiselect = false
        };

        if (!string.IsNullOrWhiteSpace(currentPath))
        {
            var directory = Path.GetDirectoryName(currentPath);
            if (Directory.Exists(directory))
            {
                dialog.InitialDirectory = directory;
            }
        }

        if (dialog.ShowDialog(this) == true)
        {
            TryApplyFile(role, dialog.FileName);
        }
    }

    private void Source_DragEnter(object sender, DragEventArgs e) => HandleDragEnter(SheetRole.Source, e);

    private void Source_DragOver(object sender, DragEventArgs e) => HandleDragOver(e);

    private void Source_DragLeave(object sender, DragEventArgs e) => HandleDragLeave(SheetRole.Source, e, SourceDropBorder);

    private void Source_Drop(object sender, DragEventArgs e) => HandleDrop(SheetRole.Source, e);

    private void Target_DragEnter(object sender, DragEventArgs e) => HandleDragEnter(SheetRole.Target, e);

    private void Target_DragOver(object sender, DragEventArgs e) => HandleDragOver(e);

    private void Target_DragLeave(object sender, DragEventArgs e) => HandleDragLeave(SheetRole.Target, e, TargetDropBorder);

    private void Target_Drop(object sender, DragEventArgs e) => HandleDrop(SheetRole.Target, e);

    private void HandleDragEnter(SheetRole role, DragEventArgs e)
    {
        if (_isBusy || GetDroppedXlsxPath(e) is null)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.Copy;
        if (role == SheetRole.Source)
        {
            _sourceDragCount++;
            SetDropHighlight(SourceDropBorder, true);
        }
        else
        {
            _targetDragCount++;
            SetDropHighlight(TargetDropBorder, true);
        }

        e.Handled = true;
    }

    private void HandleDragOver(DragEventArgs e)
    {
        e.Effects = !_isBusy && GetDroppedXlsxPath(e) is not null
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void HandleDragLeave(SheetRole role, DragEventArgs e, Border border)
    {
        var position = e.GetPosition(border);
        var stillInside = position.X >= 0
            && position.Y >= 0
            && position.X <= border.ActualWidth
            && position.Y <= border.ActualHeight;
        if (stillInside)
        {
            return;
        }

        if (role == SheetRole.Source)
        {
            _sourceDragCount = 0;
            SetDropHighlight(SourceDropBorder, false);
        }
        else
        {
            _targetDragCount = 0;
            SetDropHighlight(TargetDropBorder, false);
        }
    }

    private void HandleDrop(SheetRole role, DragEventArgs e)
    {
        if (role == SheetRole.Source)
        {
            _sourceDragCount = 0;
            SetDropHighlight(SourceDropBorder, false);
        }
        else
        {
            _targetDragCount = 0;
            SetDropHighlight(TargetDropBorder, false);
        }

        e.Handled = true;
        if (_isBusy)
        {
            return;
        }

        var path = GetDroppedXlsxPath(e);
        if (path is not null)
        {
            TryApplyFile(role, path);
        }
    }

    private static string? GetDroppedXlsxPath(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)
            || e.Data.GetData(DataFormats.FileDrop) is not string[] files
            || files.Length != 1)
        {
            return null;
        }

        var path = files[0];
        return File.Exists(path) && path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
            ? path
            : null;
    }

    private void SetDropHighlight(Border border, bool highlighted)
    {
        border.BorderBrush = highlighted
            ? (Brush)Application.Current.Resources["AccentBrush"]
            : (Brush)Application.Current.Resources["BorderBrush"];
        border.BorderThickness = new Thickness(highlighted ? 2 : 1);
    }

    private void TryApplyFile(SheetRole role, string path)
    {
        var sheetComboBox = role == SheetRole.Source ? SourceSheetComboBox : TargetSheetComboBox;
        var hintTextBlock = role == SheetRole.Source ? SourceSheetHint : TargetSheetHint;

        try
        {
            var sheets = _processor.InspectSheets(path, role);
            SetSelectedFile(role, path);
            sheetComboBox.ItemsSource = sheets;
            sheetComboBox.DisplayMemberPath = nameof(SheetOption.Name);
            sheetComboBox.SelectedValuePath = nameof(SheetOption.Name);
            hintTextBlock.Text = $"{sheets.Count} 个可用工作表";

            var relatedSheets = new List<SheetOption>();
            if (role == SheetRole.Target && SourceSheetComboBox.SelectedItem is SheetOption sourceSheet)
            {
                relatedSheets = sheets.Where(sheet =>
                    sourceSheet.Name.StartsWith(sheet.Name, StringComparison.OrdinalIgnoreCase)
                    || sheet.Name.StartsWith(sourceSheet.Name, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            var hasUniqueBest = sheets.Count == 1 || relatedSheets.Count == 1;
            sheetComboBox.SelectedItem = hasUniqueBest
                ? relatedSheets.Count == 1 ? relatedSheets[0] : sheets[0]
                : null;
            StatusTextBlock.Text = hasUniqueBest
                ? "已自动识别工作表，可以开始处理。"
                : "有多个可能的工作表，请手动选择。";

            if (role == SheetRole.Source)
            {
                _settings.LastSourcePath = path;
            }
            else
            {
                _settings.LastTargetPath = path;
            }

            _settings.Save();
        }
        catch (Exception ex)
        {
            SetSelectedFile(role, null);
            sheetComboBox.ItemsSource = null;
            hintTextBlock.Text = string.Empty;
            MessageBox.Show(this, ex.Message, "无法读取文件", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        UpdateStartState();
    }

    private void SetSelectedFile(SheetRole role, string? path)
    {
        var isSource = role == SheetRole.Source;
        if (isSource)
        {
            _sourcePath = path;
        }
        else
        {
            _targetPath = path;
        }

        var placeholder = isSource ? SourcePlaceholderText : TargetPlaceholderText;
        var fileNameText = isSource ? SourceFileNameText : TargetFileNameText;
        var filePathText = isSource ? SourceFilePathText : TargetFilePathText;
        var hasFile = !string.IsNullOrWhiteSpace(path);

        placeholder.Visibility = hasFile ? Visibility.Collapsed : Visibility.Visible;
        fileNameText.Visibility = hasFile ? Visibility.Visible : Visibility.Collapsed;
        filePathText.Visibility = hasFile ? Visibility.Visible : Visibility.Collapsed;
        fileNameText.Text = hasFile ? Path.GetFileName(path) : string.Empty;
        filePathText.Text = path ?? string.Empty;
        fileNameText.ToolTip = path;
        filePathText.ToolTip = path;
    }

    private void SheetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateStartState();
    }

    private void UpdateStartState()
    {
        StartButton.IsEnabled = !_isBusy
            && !string.IsNullOrWhiteSpace(_sourcePath)
            && !string.IsNullOrWhiteSpace(_targetPath)
            && SourceSheetComboBox.SelectedItem is SheetOption
            && TargetSheetComboBox.SelectedItem is SheetOption;
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (SourceSheetComboBox.SelectedItem is not SheetOption sourceSheet
            || TargetSheetComboBox.SelectedItem is not SheetOption targetSheet
            || string.IsNullOrWhiteSpace(_sourcePath)
            || string.IsNullOrWhiteSpace(_targetPath))
        {
            return;
        }

        if (string.Equals(_sourcePath, _targetPath, StringComparison.OrdinalIgnoreCase)
            && sourceSheet.Name == targetSheet.Name)
        {
            MessageBox.Show(this, "来源和名单不能使用同一个工作表。", "请重新选择",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SetBusy(true);
        OpenOutputFolderButton.Visibility = Visibility.Collapsed;
        try
        {
            var request = new ProcessRequest(
                _sourcePath,
                _targetPath,
                sourceSheet.Name,
                targetSheet.Name);
            var progress = new Progress<string>(message => StatusTextBlock.Text = message);
            var result = await _processor.ProcessAsync(
                request,
                BackupCheckBox.IsChecked == true,
                ConfirmIssues,
                progress);

            _lastOutputPath = result.OutputPath;
            OpenOutputFolderButton.Visibility = Visibility.Visible;
            StatusTextBlock.Text = result.UsedCopy
                ? $"原文件被占用，已保存副本：{Path.GetFileName(result.OutputPath)}"
                : "处理完成。";

            if (result.UsedCopy)
            {
                MessageBox.Show(this,
                    $"原文件正被占用，结果已保存为：\n{result.OutputPath}",
                    "处理完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else if (result.WrittenScoreCount == 0)
            {
                MessageBox.Show(this, "没有可写入的成绩，名单文件未修改。",
                    "处理结束", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else if (result.Issues.Count > 0)
            {
                MessageBox.Show(this,
                    $"处理完成，已写入 {result.WrittenScoreCount} 项成绩，跳过 {result.Issues.Count} 项异常。",
                    "处理完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(this, "处理完成。", "数据匹配助手",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (OperationCanceledException)
        {
            StatusTextBlock.Text = "已取消，名单文件未修改。";
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = "处理失败，名单文件未替换。";
            MessageBox.Show(this, ex.Message, "处理失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void OpenOutputFolder_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_lastOutputPath))
        {
            return;
        }

        try
        {
            if (File.Exists(_lastOutputPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{_lastOutputPath}\"",
                    UseShellExecute = true
                });
                return;
            }

            var directory = Path.GetDirectoryName(_lastOutputPath);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = directory,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "无法打开文件夹", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task<bool> ConfirmIssues(IReadOnlyList<ScoreIssue> issues)
    {
        return await Dispatcher.InvokeAsync(() =>
        {
            var dialog = new IssuesWindow(issues) { Owner = this };
            return dialog.ShowDialog() == true;
        });
    }

    private void SetBusy(bool isBusy)
    {
        _isBusy = isBusy;
        SourceBrowseButton.IsEnabled = !isBusy;
        TargetBrowseButton.IsEnabled = !isBusy;
        SourceSheetComboBox.IsEnabled = !isBusy;
        TargetSheetComboBox.IsEnabled = !isBusy;
        BackupCheckBox.IsEnabled = !isBusy;
        WorkingProgressBar.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
        StartButton.Content = isBusy ? "正在处理…" : "开始处理";
        UpdateStartState();
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isBusy)
        {
            e.Cancel = true;
            MessageBox.Show(this, "正在处理文件，请等待完成后再关闭。",
                "请稍候", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
