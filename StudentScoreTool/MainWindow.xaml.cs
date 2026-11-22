using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
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
    private ProcessResult? _lastResult;
    private string? _lastIssueIntro;
    private IReadOnlyList<Uri> _lastIssueStickers = [];
    private UpdateCheckResult? _pendingUpdate;
    /// <summary>过渡包从旧版 Velopack 安装里运行时的安装根目录；正式 Inno 版本为 null。</summary>
    private readonly string? _legacyVelopackRoot = LegacyInstall.CurrentBridgeRoot;
    private readonly DispatcherTimer _themeMenuCloseTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(220)
    };

    public MainWindow()
    {
        InitializeComponent();
        VersionTextBlock.Text = AppInfo.VersionDisplay;
        _themeMenuCloseTimer.Tick += ThemeMenuCloseTimer_Tick;
        Deactivated += (_, _) => CloseThemeMenu();
        BuildThemeMenu();
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

    private void BuildThemeMenu()
    {
        ThemeMenuItems.Children.Clear();
        foreach (var theme in AppThemeNames.DisplayOrder)
        {
            var palette = ThemePalette.For(theme);
            var swatch = new Border
            {
                Width = 18,
                Height = 18,
                CornerRadius = new CornerRadius(9),
                BorderThickness = new Thickness(1),
                BorderBrush = (Brush)Application.Current.Resources["BorderBrush"],
                Background = new SolidColorBrush(ThemeManager.ParseColor(palette.Swatch)),
                VerticalAlignment = VerticalAlignment.Center
            };

            var name = new TextBlock
            {
                Text = AppThemeNames.GetDisplayName(theme),
                Margin = new Thickness(10, 0, 0, 0),
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)Application.Current.Resources["TextPrimaryBrush"]
            };

            var content = new StackPanel { Orientation = Orientation.Horizontal };
            content.Children.Add(swatch);
            content.Children.Add(name);

            var item = new Button
            {
                Tag = theme.ToString(),
                Content = content,
                Style = (Style)FindResource("ThemeMenuItemButton"),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                ToolTip = AppThemeNames.GetDisplayName(theme)
            };
            AutomationProperties.SetName(item, AppThemeNames.GetDisplayName(theme));
            item.Click += ThemeSwatch_Click;
            item.PreviewKeyDown += ThemeMenuItem_PreviewKeyDown;
            ThemeMenuItems.Children.Add(item);
        }
    }

    private void ThemeMenu_MouseEnter(object sender, MouseEventArgs e)
    {
        _themeMenuCloseTimer.Stop();
        ThemePopup.IsOpen = true;
    }

    private void ThemeMenu_MouseLeave(object sender, MouseEventArgs e)
    {
        _themeMenuCloseTimer.Stop();
        _themeMenuCloseTimer.Start();
    }

    private void ThemeMenuCloseTimer_Tick(object? sender, EventArgs e)
    {
        _themeMenuCloseTimer.Stop();
        if (ThemeMenuHost.IsMouseOver || ThemePopupBorder.IsMouseOver || ThemeMenuButton.IsKeyboardFocusWithin)
        {
            return;
        }

        if (ThemeMenuItems.Children.OfType<Button>().Any(item => item.IsKeyboardFocusWithin))
        {
            return;
        }

        ThemePopup.IsOpen = false;
    }

    private void ThemeMenuButton_Click(object sender, RoutedEventArgs e)
    {
        ThemePopup.IsOpen = !ThemePopup.IsOpen;
        if (ThemePopup.IsOpen)
        {
            ThemeMenuItems.Children.OfType<Button>().FirstOrDefault()?.Focus();
        }
    }

    private void ThemeMenuButton_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Down or Key.Enter or Key.Space)
        {
            ThemePopup.IsOpen = true;
            ThemeMenuItems.Children.OfType<Button>().FirstOrDefault()?.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CloseThemeMenu();
            ThemeMenuButton.Focus();
            e.Handled = true;
        }
    }

    private void ThemeMenuItem_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CloseThemeMenu();
            ThemeMenuButton.Focus();
            e.Handled = true;
        }
    }

    private void CloseThemeMenu()
    {
        _themeMenuCloseTimer.Stop();
        ThemePopup.IsOpen = false;
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
        CloseThemeMenu();
    }

    private void RefreshThemeSwatches()
    {
        foreach (var item in ThemeMenuItems.Children.OfType<Button>())
        {
            var selected = AppThemeNames.TryParse(item.Tag as string, out var theme)
                && theme == ThemeManager.Current;
            item.Background = selected
                ? (Brush)Application.Current.Resources["AccentSoftBrush"]
                : Brushes.Transparent;
            if (item.Content is StackPanel panel)
            {
                foreach (var child in panel.Children)
                {
                    if (child is Border ring)
                    {
                        ring.BorderBrush = selected
                            ? (Brush)Application.Current.Resources["AccentBrush"]
                            : (Brush)Application.Current.Resources["BorderBrush"];
                        ring.BorderThickness = new Thickness(selected ? 2 : 1);
                    }
                    else if (child is TextBlock label)
                    {
                        label.Foreground = (Brush)Application.Current.Resources["TextPrimaryBrush"];
                    }
                }
            }
        }
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

        var isMigration = _legacyVelopackRoot is not null;
        _isCheckingUpdate = true;
        var previousContent = CheckUpdateButton.Content;
        if (showWindow)
        {
            CheckUpdateButton.Content = "正在检查…";
            CheckUpdateButton.IsEnabled = false;
        }

        try
        {
            // 旧版过渡包：即使版本号相同，也要引导改装 Inno 版本。
            var result = await _updateService.CheckForUpdatesAsync(includeSameVersion: isMigration);
            _pendingUpdate = result.Availability == UpdateAvailability.Available ? result : null;
            UpdateBadge.Visibility = _pendingUpdate is not null || isMigration
                ? Visibility.Visible
                : Visibility.Collapsed;

            // 启动时只亮红点、不弹窗；唯一例外是旧版用户第一次打开过渡包时，自动说明一次新的安装方式。
            var autoPrompt = !showWindow
                && isMigration
                && _pendingUpdate is not null
                && !_settings.MigrationPromptShown;
            if (!showWindow && !autoPrompt)
            {
                return;
            }

            if (autoPrompt)
            {
                _settings.MigrationPromptShown = true;
                TrySaveSettings();
            }

            if (result.Availability == UpdateAvailability.Available && result.Manifest is not null)
            {
                var dialog = new UpdateWindow(result, _updateService, _legacyVelopackRoot) { Owner = this };
                dialog.ShowDialog();
                if (dialog.StartedUpdate)
                {
                    _isApplyingUpdate = true;
                    TrySaveSettings();
                    Application.Current.Shutdown();
                }
                return;
            }

            var icon = result.Availability == UpdateAvailability.Error
                ? MessageBoxImage.Warning
                : MessageBoxImage.Information;
            var message = result.Message;
            if (isMigration && result.Availability == UpdateAvailability.UpToDate)
            {
                message = $"暂时还拿不到新的安装包，请稍后再试，或到 Gitee 发布页手动下载：{AppInfo.GiteeReleasesUrl}";
            }

            MessageBox.Show(this, message, "检查更新", MessageBoxButton.OK, icon);
        }
        finally
        {
            _isCheckingUpdate = false;
            CheckUpdateButton.Content = previousContent;
            CheckUpdateButton.IsEnabled = true;
        }
    }

    private void TrySaveSettings()
    {
        try
        {
            _settings.Save();
        }
        catch
        {
            // 设置保存失败不影响更新。
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
            Title = role == SheetRole.Source ? "选择有成绩的表格" : "选择需要填写成绩的名单",
            Filter = SpreadsheetLoader.OpenFileFilter,
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
        if (_isBusy || GetDroppedTablePath(e) is null)
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
        e.Effects = !_isBusy && GetDroppedTablePath(e) is not null
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

        var path = GetDroppedTablePath(e);
        if (path is not null)
        {
            TryApplyFile(role, path);
        }
    }

    private static string? GetDroppedTablePath(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)
            || e.Data.GetData(DataFormats.FileDrop) is not string[] files
            || files.Length != 1)
        {
            return null;
        }

        var path = files[0];
        return File.Exists(path) && SpreadsheetLoader.IsSupportedPath(path)
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
        ClearLastIssues();
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
                ConfirmOverwrites,
                progress);

            _lastOutputPath = result.OutputPath;
            _lastResult = result;
            OpenOutputFolderButton.Visibility = Visibility.Visible;
            StatusTextBlock.Text = BuildStatusText(result);
            RefreshIssuesButton();
            SetBusy(false);

            if (result.HasIssues)
            {
                ShowIssuesWindow();
            }
            else
            {
                var success = new SuccessWindow(result) { Owner = this };
                success.ShowDialog();
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

    private async Task<IReadOnlyList<string>> ConfirmOverwrites(IReadOnlyList<OverwriteCandidate> candidates)
    {
        return await Dispatcher.InvokeAsync(() =>
        {
            var dialog = new OverwriteWindow(candidates) { Owner = this };
            return dialog.ShowDialog() == true
                ? dialog.SelectedKeys
                : Array.Empty<string>();
        });
    }

    private void ViewIssues_Click(object sender, RoutedEventArgs e) => ShowIssuesWindow();

    private void ShowIssuesWindow()
    {
        if (_lastResult is null || !_lastResult.HasIssues)
        {
            return;
        }

        _lastIssueIntro ??= ResultMessages.PickIssueIntro();
        if (_lastIssueStickers.Count == 0)
        {
            _lastIssueStickers = StickerCatalog.PickSad();
        }

        var dialog = new IssuesWindow(_lastResult, _lastIssueIntro, _lastIssueStickers) { Owner = this };
        dialog.ShowDialog();
    }

    private void ClearLastIssues()
    {
        _lastResult = null;
        _lastIssueIntro = null;
        _lastIssueStickers = [];
        RefreshIssuesButton();
    }

    private void RefreshIssuesButton()
    {
        var count = _lastResult?.Issues.Count ?? 0;
        if (count > 0)
        {
            ViewIssuesButton.Content = $"查看刚才的问题（{count} 项）";
            ViewIssuesButton.Visibility = Visibility.Visible;
        }
        else
        {
            ViewIssuesButton.Content = "查看刚才的问题";
            ViewIssuesButton.Visibility = Visibility.Collapsed;
        }
    }

    private static string BuildStatusText(ProcessResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.OutputNote))
        {
            return result.OutputNote;
        }

        if (result.WrittenScoreCount == 0)
        {
            return result.HasIssues
                ? "处理结束。没有写入新成绩，问题已列出。"
                : "处理结束，没有可写入的成绩。";
        }

        return result.HasIssues
            ? $"处理完成，已写入 {result.WrittenScoreCount} 项成绩，另有 {result.Issues.Count} 处需要留意。"
            : "处理完成。";
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
