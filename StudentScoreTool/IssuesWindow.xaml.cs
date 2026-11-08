using System.Windows;

namespace StudentScoreTool;

public partial class IssuesWindow : Window
{
    public IssuesWindow(IReadOnlyList<ScoreIssue> issues)
    {
        InitializeComponent();
        IssueListBox.ItemsSource = issues;
        Title = $"确认异常记录（{issues.Count} 项）";
    }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
