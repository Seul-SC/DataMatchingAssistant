namespace StudentScoreTool.Tests;

public sealed class TextNormalizationTests
{
    [Theory]
    [InlineData("００１", "1")]
    [InlineData("0001", "1")]
    [InlineData("  001 ", "1")]
    [InlineData("0", "0")]
    [InlineData("000", "0")]
    [InlineData("A001", "A001")]
    [InlineData("00A01", "A01")]
    [InlineData("", "")]
    public void NormalizeIdStripsFullWidthSpacesAndLeadingZeros(string input, string expected)
    {
        Assert.Equal(expected, TextNormalization.NormalizeId(input));
    }

    [Fact]
    public void NormalizeIdTreatsFullWidthAndAsciiAsEqual()
    {
        Assert.Equal(TextNormalization.NormalizeId("２０２３００１"), TextNormalization.NormalizeId("2023001"));
        Assert.Equal(TextNormalization.NormalizeId("001"), TextNormalization.NormalizeId("1"));
    }

    [Theory]
    [InlineData("张伟（重修）", "张伟")]
    [InlineData("张伟(补考)", "张伟")]
    [InlineData("张 伟", "张伟")]
    [InlineData("马克·吐温", "马克·吐温")]
    [InlineData("马克•吐温", "马克·吐温")]
    [InlineData("張偉", "张伟")]
    [InlineData("陳麗", "陈丽")]
    public void NormalizeNameRemovesRemarksUnifiesDotsAndSimplifies(string input, string expected)
    {
        Assert.Equal(expected, TextNormalization.NormalizeName(input));
    }

    [Fact]
    public void DisplayTextKeepsRemarksAndTrimsSpaces()
    {
        // DisplayText 会把全角字符转成半角（便于解析成绩），括号也会变成半角。
        Assert.Equal("张伟(重修)", TextNormalization.DisplayText("  张伟（重修） "));
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    [InlineData(150, false)]
    [InlineData(151, true)]
    public void NumericRangeIsZeroToOneHundredFiftyInclusive(double value, bool expected)
    {
        Assert.Equal(expected, TextNormalization.IsNumericScoreOutOfRange(value));
    }
}
