namespace StudentScoreTool.Tests;

public sealed class ResultMessagesTests
{
    [Fact]
    public void PlaceholderMessagesAreReadyToBeReplaced()
    {
        Assert.NotEmpty(ResultMessages.SuccessMessages);
        Assert.NotEmpty(ResultMessages.IssueIntros);
        Assert.Equal(ResultMessages.SuccessMessages.Length, ResultMessages.SuccessMessages.Distinct().Count());
        Assert.Equal(ResultMessages.IssueIntros.Length, ResultMessages.IssueIntros.Distinct().Count());
        Assert.All(ResultMessages.SuccessMessages, message => Assert.False(string.IsNullOrWhiteSpace(message)));
        Assert.All(ResultMessages.IssueIntros, message => Assert.False(string.IsNullOrWhiteSpace(message)));
        Assert.Equal("处理完成。", ResultMessages.PickSuccess());
        Assert.Equal("处理完成，下面是需要留意的问题。", ResultMessages.PickIssueIntro());
    }

    [Fact]
    public void StickerCatalogIsEmptyUntilFinalArtIsAdded()
    {
        Assert.Empty(StickerCatalog.Happy);
        Assert.Empty(StickerCatalog.Sad);
        Assert.Null(StickerCatalog.PickHappy());
        Assert.Null(StickerCatalog.PickSad());
    }
}
