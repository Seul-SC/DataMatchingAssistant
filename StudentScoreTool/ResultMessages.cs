namespace StudentScoreTool;

public static class ResultMessages
{
    // 占位文案。弹窗提示语尚未接入定稿，待审核后再替换，不要把这里当成最终文案。
    public static readonly string[] SuccessMessages =
    [
        "处理完成。"
    ];

    public static readonly string[] IssueIntros =
    [
        "处理完成，下面是需要留意的问题。"
    ];

    public static string PickSuccess() => Pick(SuccessMessages);

    public static string PickIssueIntro() => Pick(IssueIntros);

    private static string Pick(IReadOnlyList<string> messages)
    {
        return messages[Random.Shared.Next(messages.Count)];
    }
}
