using StudentScoreTool;

namespace StudentScoreTool.Tests;

public sealed class ResultMessagesTests
{
    [Fact]
    public void ApprovedCopy_IsCompleteAndUnique()
    {
        Assert.Equal(35, ResultMessages.SuccessMessages.Length);
        Assert.Equal(20, ResultMessages.IssueIntros.Length);
        Assert.Equal(ResultMessages.SuccessMessages.Length, ResultMessages.SuccessMessages.Distinct().Count());
        Assert.Equal(ResultMessages.IssueIntros.Length, ResultMessages.IssueIntros.Distinct().Count());
        Assert.All(ResultMessages.SuccessMessages, message => Assert.False(string.IsNullOrWhiteSpace(message)));
        Assert.All(ResultMessages.IssueIntros, message => Assert.False(string.IsNullOrWhiteSpace(message)));
    }

    [Fact]
    public void Pick_ReturnsAConfiguredMessage()
    {
        Assert.Contains(ResultMessages.PickSuccess(), ResultMessages.SuccessMessages);
        Assert.Contains(ResultMessages.PickIssueIntro(), ResultMessages.IssueIntros);
    }
}
