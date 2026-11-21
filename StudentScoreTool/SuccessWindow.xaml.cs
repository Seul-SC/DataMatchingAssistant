using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace StudentScoreTool;

public partial class SuccessWindow : Window
{
    public SuccessWindow(ProcessResult result)
    {
        InitializeComponent();
        MessageTextBlock.Text = ResultMessages.PickSuccess();
        ApplyStickers(StickerCatalog.PickHappy());
        SummaryTextBlock.Text = BuildSummary(result);
        if (!string.IsNullOrWhiteSpace(result.OutputNote))
        {
            NoteTextBlock.Text = result.OutputNote;
            NoteTextBlock.Visibility = Visibility.Visible;
        }
    }

    private void ApplyStickers(IReadOnlyList<Uri> uris)
    {
        FillStickerPanel(StickerPanel, uris, 120);
    }

    internal static void FillStickerPanel(StackPanel panel, IReadOnlyList<Uri> uris, double size)
    {
        panel.Children.Clear();
        if (uris.Count == 0)
        {
            panel.Visibility = Visibility.Collapsed;
            return;
        }

        panel.Visibility = Visibility.Visible;
        foreach (var uri in uris)
        {
            var image = new Image
            {
                Source = LoadSticker(uri),
                Width = size,
                Height = size,
                Margin = new Thickness(0, 0, 12, 0),
                Stretch = Stretch.Uniform
            };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            panel.Children.Add(image);
        }
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
