using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace StudentScoreTool;

public partial class SuccessWindow : Window
{
    public SuccessWindow(ProcessResult result)
    {
        InitializeComponent();
        MessageTextBlock.Text = ResultMessages.PickSuccess();
        ApplySticker(StickerCatalog.PickHappy());
        SummaryTextBlock.Text = BuildSummary(result);
        if (!string.IsNullOrWhiteSpace(result.OutputNote))
        {
            NoteTextBlock.Text = result.OutputNote;
            NoteTextBlock.Visibility = Visibility.Visible;
        }
    }

    private void ApplySticker(Uri? uri)
    {
        if (uri is null)
        {
            StickerBorder.Visibility = Visibility.Collapsed;
            return;
        }

        StickerImage.Source = LoadSticker(uri);
    }

    private static string BuildSummary(ProcessResult result)
    {
        var lines = new List<string>
        {
            $"已匹配 {result.MatchedStudentCount} 人，写入 {result.WrittenScoreCount} 项成绩。"
        };

        if (!string.IsNullOrWhiteSpace(result.OutputPath))
        {
            lines.Add($"结果文件：{Path.GetFileName(result.OutputPath)}");
        }

        if (!string.IsNullOrWhiteSpace(result.BackupPath))
        {
            lines.Add($"已备份原名单：{Path.GetFileName(result.BackupPath)}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    internal static BitmapImage LoadSticker(Uri uri)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = uri;
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
