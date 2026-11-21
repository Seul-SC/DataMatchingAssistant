using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace StudentScoreTool;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        VersionTextBlock.Text = $"版本 {AppInfo.VersionDisplay}";
    }

    private void Email_Click(object sender, RoutedEventArgs e)
    {
        OpenUrl(AppInfo.MailtoUrl);
    }

    private void Issues_Click(object sender, RoutedEventArgs e)
    {
        OpenUrl(AppInfo.GitHubIssuesUrl);
    }

    private void ExternalLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        OpenUrl(e.Uri.AbsoluteUri);
        e.Handled = true;
    }

    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
