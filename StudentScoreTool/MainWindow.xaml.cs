using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace StudentScoreTool;

public partial class MainWindow : Window
{
    private readonly ExcelProcessor _processor = new();
    private bool _isBusy;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void SelectSource_Click(object sender, RoutedEventArgs e)
    {
        SelectFile(SheetRole.Source);
    }

    private void SelectTarget_Click(object sender, RoutedEventArgs e)
    {
        SelectFile(SheetRole.Target);
    }

    private void SelectFile(SheetRole role)
    {
        var dialog = new OpenFileDialog
        {
            Title = role == SheetRole.Source ? "选择有成绩的 Excel" : "选择需要填写成绩的名单 Excel",
            Filter = "Excel 工作簿 (*.xlsx)|*.xlsx",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var pathTextBox = role == SheetRole.Source ? SourcePathTextBox : TargetPathTextBox;
        var sheetComboBox = role == SheetRole.Source ? SourceSheetComboBox : TargetSheetComboBox;
        var hintTextBlock = role == SheetRole.Source ? SourceSheetHint : TargetSheetHint;

        try
        {
            var sheets = _processor.InspectSheets(dialog.FileName, role);
            pathTextBox.Text = dialog.FileName;
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
        }
        catch (Exception ex)
        {
            pathTextBox.Clear();
            sheetComboBox.ItemsSource = null;
            hintTextBlock.Text = string.Empty;
            MessageBox.Show(this, ex.Message, "无法读取文件", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        UpdateStartState();
    }

    private void SheetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateStartState();
    }

    private void UpdateStartState()
    {
        StartButton.IsEnabled = !_isBusy
            && !string.IsNullOrWhiteSpace(SourcePathTextBox.Text)
            && !string.IsNullOrWhiteSpace(TargetPathTextBox.Text)
            && SourceSheetComboBox.SelectedItem is SheetOption
            && TargetSheetComboBox.SelectedItem is SheetOption;
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (SourceSheetComboBox.SelectedItem is not SheetOption sourceSheet
            || TargetSheetComboBox.SelectedItem is not SheetOption targetSheet)
        {
            return;
        }

        if (string.Equals(SourcePathTextBox.Text, TargetPathTextBox.Text, StringComparison.OrdinalIgnoreCase)
            && sourceSheet.Name == targetSheet.Name)
        {
            MessageBox.Show(this, "来源和名单不能使用同一个工作表。", "请重新选择",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SetBusy(true);
        try
        {
            var request = new ProcessRequest(
                SourcePathTextBox.Text,
                TargetPathTextBox.Text,
                sourceSheet.Name,
                targetSheet.Name);
            var progress = new Progress<string>(message => StatusTextBlock.Text = message);
            var result = await _processor.ProcessAsync(
                request,
                BackupCheckBox.IsChecked == true,
                ConfirmIssues,
                progress);

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
