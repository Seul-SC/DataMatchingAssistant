using System.Diagnostics;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace StudentScoreTool;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        VersionTextBlock.Text = $"版本 {AppInfo.VersionDisplay}";
        TryLoadQr("tips-wechat.png", WeChatQrImage, WeChatQrPlaceholder);
        TryLoadQr("tips-alipay.png", AlipayQrImage, AlipayQrPlaceholder);
    }

    private static void TryLoadQr(string fileName, System.Windows.Controls.Image image, UIElement placeholder)
    {
        try
        {
            var uri = new Uri($"pack://application:,,,/Assets/{fileName}", UriKind.Absolute);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = uri;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            image.Source = bitmap;
            image.Visibility = Visibility.Visible;
            placeholder.Visibility = Visibility.Collapsed;
        }
        catch
        {
            image.Source = null;
            image.Visibility = Visibility.Collapsed;
            placeholder.Visibility = Visibility.Visible;
        }
    }

    private void GitHubLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = e.Uri.AbsoluteUri,
            UseShellExecute = true
        });
        e.Handled = true;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
