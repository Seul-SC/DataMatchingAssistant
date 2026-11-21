using System.Windows;

namespace StudentScoreTool;

public partial class IssuesWindow : Window
{
    public IssuesWindow(ProcessResult result, string intro, IReadOnlyList<Uri> stickers)
    {
        InitializeComponent();
        Title = $"需要留意的问题（{result.Issues.Count} 项）";
        TitleTextBlock.Text = $"一共 {result.Issues.Count} 处需要留意";
        IntroTextBlock.Text = intro;
        FilesTextBlock.Text = BuildFilesSummary(result);
        SuccessWindow.FillStickerPanel(StickerPanel, stickers, 108);
        GroupItemsControl.ItemsSource = BuildGroups(result.Issues);
    }

    private static string BuildFilesSummary(ProcessResult result)
    {
        var source = string.IsNullOrWhiteSpace(result.SourceFileName) ? "成绩表" : result.SourceFileName;
        var target = string.IsNullOrWhiteSpace(result.TargetFileName) ? "名单表" : result.TargetFileName;
        var summary = $"成绩表「{source}」  ·  名单「{target}」";
        if (result.WrittenScoreCount > 0)
        {
            summary += $"。已写入 {result.WrittenScoreCount} 项成绩。";
        }

        if (!string.IsNullOrWhiteSpace(result.OutputNote))
        {
            summary += $" {result.OutputNote}";
        }

        return summary;
    }


    private static List<IssueGroupView> BuildGroups(IReadOnlyList<ScoreIssue> issues)
    {
        var order = IssueCategories.DisplayOrder
            .Select((name, index) => (name, index))
            .ToDictionary(item => item.name, item => item.index);

        return issues
            .GroupBy(issue => issue.Category)
            .OrderBy(group => order.TryGetValue(group.Key, out var index) ? index : 100)
            .Select(group => new IssueGroupView(
                group.Key,
                group.Select(IssueItemView.FromIssue).ToList()))
            .ToList();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}

public sealed class IssueGroupView
{
    public IssueGroupView(string title, IReadOnlyList<IssueItemView> items)
    {
        Title = title;
        Items = items;
        CountText = $"{items.Count} 项";
    }

    public string Title { get; }
    public string CountText { get; }
    public IReadOnlyList<IssueItemView> Items { get; }
}

public sealed class IssueItemView
{
    public required string Headline { get; init; }
    public required string Message { get; init; }
    public string? LeftLabel { get; init; }
    public string? LeftValue { get; init; }
    public string? RightLabel { get; init; }
    public string? RightValue { get; init; }
    public Visibility ComparisonVisibility { get; init; }

    public static IssueItemView FromIssue(ScoreIssue issue)
    {
        var headline = string.IsNullOrWhiteSpace(issue.StudentName)
            ? issue.Category
            : string.IsNullOrWhiteSpace(issue.StudentId)
                ? issue.StudentName
                : $"{issue.StudentName}（学号 {issue.StudentId}）";

        var hasComparison = !string.IsNullOrWhiteSpace(issue.LeftValue)
            || !string.IsNullOrWhiteSpace(issue.RightValue);

        return new IssueItemView
        {
            Headline = headline,
            Message = issue.Message,
            LeftLabel = issue.LeftLabel,
            LeftValue = issue.LeftValue,
            RightLabel = issue.RightLabel,
            RightValue = issue.RightValue,
            ComparisonVisibility = hasComparison ? Visibility.Visible : Visibility.Collapsed
        };
    }
}
