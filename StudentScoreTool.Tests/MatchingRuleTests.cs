using System.Text;
using ClosedXML.Excel;

namespace StudentScoreTool.Tests;

public sealed class MatchingRuleTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"StudentScoreToolMatchTests-{Guid.NewGuid():N}");

    public MatchingRuleTests()
    {
        Directory.CreateDirectory(_directory);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    [Fact]
    public async Task MatchesByStudentIdFirstEvenWhenNamesDiffer()
    {
        var sourcePath = Path.Combine(_directory, "成绩表.xlsx");
        var targetPath = Path.Combine(_directory, "班级名单.xlsx");
        CreateSimple(sourcePath, "成绩", ["姓名", "学号", "成绩"], sheet =>
        {
            Add(sheet, 2, "张伟", "001", 91);
            Add(sheet, 3, "李四", "002", 88);
        });
        CreateSimple(targetPath, "名单", ["姓名", "学号", "成绩"], sheet =>
        {
            Add(sheet, 2, "张韦", "001", "");
            Add(sheet, 3, "李四", "002", "");
        });

        var result = await Process(sourcePath, targetPath);

        using var book = new XLWorkbook(targetPath);
        var sheet = book.Worksheet("名单");
        Assert.Equal(91, sheet.Cell("C2").GetDouble());
        Assert.Equal(88, sheet.Cell("C3").GetDouble());
        Assert.Equal(2, result.WrittenScoreCount);
        var mismatch = Assert.Single(result.Issues, issue => issue.Category == IssueCategories.NameMismatch);
        Assert.Contains("张伟", mismatch.Message, StringComparison.Ordinal);
        Assert.Contains("张韦", mismatch.Message, StringComparison.Ordinal);
        Assert.Equal("成绩表.xlsx", result.SourceFileName);
        Assert.Equal("班级名单.xlsx", result.TargetFileName);
    }

    [Fact]
    public async Task FallsBackToNameWhenEitherSideLacksStudentId()
    {
        var sourcePath = Path.Combine(_directory, "无学号来源.xlsx");
        var targetPath = Path.Combine(_directory, "无学号名单.xlsx");
        CreateSimple(sourcePath, "成绩", ["姓名", "学号", "成绩"], sheet =>
        {
            Add(sheet, 2, "张伟", "", 90);
            Add(sheet, 3, "李四", "002", 80);
        });
        CreateSimple(targetPath, "名单", ["姓名", "学号", "成绩"], sheet =>
        {
            Add(sheet, 2, "张伟", "900", "");
            Add(sheet, 3, "李四", "", "");
        });

        var result = await Process(sourcePath, targetPath);

        using var book = new XLWorkbook(targetPath);
        var sheet = book.Worksheet("名单");
        Assert.Equal(90, sheet.Cell("C2").GetDouble());
        Assert.Equal(80, sheet.Cell("C3").GetDouble());
        Assert.Equal(2, result.WrittenScoreCount);
        Assert.DoesNotContain(result.Issues, issue => issue.Category == IssueCategories.NameMismatch);
    }

    [Fact]
    public async Task NormalizesIdsAndNamesBeforeMatching()
    {
        var sourcePath = Path.Combine(_directory, "规范来源.xlsx");
        var targetPath = Path.Combine(_directory, "规范名单.xlsx");
        CreateSimple(sourcePath, "成绩", ["姓名", "学号", "成绩"], sheet =>
        {
            Add(sheet, 2, "張偉（重修）", "００１", 95);
            Add(sheet, 3, "马克•吐温", "0002", 86);
        });
        CreateSimple(targetPath, "名单", ["姓名", "学号", "成绩"], sheet =>
        {
            Add(sheet, 2, "张伟", "1", "");
            Add(sheet, 3, "马克·吐温", "2", "");
        });

        var result = await Process(sourcePath, targetPath);

        using var book = new XLWorkbook(targetPath);
        var sheet = book.Worksheet("名单");
        Assert.Equal(95, sheet.Cell("C2").GetDouble());
        Assert.Equal(86, sheet.Cell("C3").GetDouble());
        Assert.Equal(2, result.WrittenScoreCount);
    }

    [Fact]
    public async Task ListsBlankSourceScoresAndKeepsOriginalValue()
    {
        var sourcePath = Path.Combine(_directory, "空分来源.xlsx");
        var targetPath = Path.Combine(_directory, "空分名单.xlsx");
        CreateSimple(sourcePath, "成绩", ["姓名", "学号", "成绩"], sheet =>
        {
            Add(sheet, 2, "张三", "001", "");
        });
        CreateSimple(targetPath, "名单", ["姓名", "学号", "成绩"], sheet =>
        {
            Add(sheet, 2, "张三", "001", 88);
        });

        var result = await Process(sourcePath, targetPath);

        using var book = new XLWorkbook(targetPath);
        Assert.Equal(88, book.Worksheet("名单").Cell("C2").GetDouble());
        Assert.Equal(0, result.WrittenScoreCount);
        Assert.Equal(1, result.BlankScoreCount);
        var issue = Assert.Single(result.Issues);
        Assert.Equal(IssueCategories.BlankSource, issue.Category);
        Assert.Equal("张三", issue.StudentName);
        Assert.Contains("88", issue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WritesOutOfRangeNumbersAndListsThem()
    {
        var sourcePath = Path.Combine(_directory, "超范围来源.xlsx");
        var targetPath = Path.Combine(_directory, "超范围名单.xlsx");
        CreateSimple(sourcePath, "成绩", ["姓名", "成绩"], sheet =>
        {
            Add(sheet, 2, "甲", 151);
            Add(sheet, 3, "乙", -1);
            Add(sheet, 4, "丙", 0);
            Add(sheet, 5, "丁", 150);
        });
        CreateSimple(targetPath, "名单", ["姓名", "成绩"], sheet =>
        {
            Add(sheet, 2, "甲", "");
            Add(sheet, 3, "乙", "");
            Add(sheet, 4, "丙", "");
            Add(sheet, 5, "丁", "");
        });

        var result = await Process(sourcePath, targetPath);

        using var book = new XLWorkbook(targetPath);
        var sheet = book.Worksheet("名单");
        Assert.Equal(151, sheet.Cell("B2").GetDouble());
        Assert.Equal(-1, sheet.Cell("B3").GetDouble());
        Assert.Equal(0, sheet.Cell("B4").GetDouble());
        Assert.Equal(150, sheet.Cell("B5").GetDouble());
        Assert.Equal(4, result.WrittenScoreCount);
        Assert.Equal(2, result.Issues.Count);
        Assert.All(result.Issues, issue => Assert.Equal(IssueCategories.OutOfRange, issue.Category));
        Assert.Contains(result.Issues, issue => issue.StudentName == "甲");
        Assert.Contains(result.Issues, issue => issue.StudentName == "乙");
    }

    [Fact]
    public async Task WritesKnownAndUnknownTextGradesAsIs()
    {
        var sourcePath = Path.Combine(_directory, "文字来源.xlsx");
        var targetPath = Path.Combine(_directory, "文字名单.xlsx");
        CreateSimple(sourcePath, "成绩", ["姓名", "成绩"], sheet =>
        {
            Add(sheet, 2, "甲", "合格");
            Add(sheet, 3, "乙", "作弊");
            Add(sheet, 4, "丙", "暂缓登录");
        });
        CreateSimple(targetPath, "名单", ["姓名", "成绩"], sheet =>
        {
            Add(sheet, 2, "甲", "");
            Add(sheet, 3, "乙", "");
            Add(sheet, 4, "丙", "");
        });

        var result = await Process(sourcePath, targetPath);

        using var book = new XLWorkbook(targetPath);
        var sheet = book.Worksheet("名单");
        Assert.Equal("合格", sheet.Cell("B2").GetString());
        Assert.Equal("作弊", sheet.Cell("B3").GetString());
        Assert.Equal("暂缓登录", sheet.Cell("B4").GetString());
        Assert.Equal(3, result.WrittenScoreCount);
        Assert.DoesNotContain(result.Issues, issue => issue.Category == IssueCategories.OutOfRange);
    }

    [Fact]
    public async Task ProcessesCsvAndSavesXlsxWithoutChangingOriginal()
    {
        var sourcePath = Path.Combine(_directory, "成绩表.csv");
        var targetPath = Path.Combine(_directory, "名单.csv");
        await File.WriteAllTextAsync(sourcePath, "姓名,学号,成绩\n张三,001,90\n李四,002,80\n", Encoding.UTF8);
        var gbk = Encoding.GetEncoding("GB18030");
        await File.WriteAllBytesAsync(targetPath, gbk.GetBytes("姓名,学号,成绩\n张三,001,\n李四,002,\n"));
        var originalTarget = await File.ReadAllBytesAsync(targetPath);

        var result = await Process(sourcePath, targetPath, "成绩表", "名单");

        Assert.True(result.UsedCopy);
        Assert.EndsWith(".xlsx", result.OutputPath, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("已填写", Path.GetFileName(result.OutputPath), StringComparison.Ordinal);
        Assert.Equal(originalTarget, await File.ReadAllBytesAsync(targetPath));
        Assert.False(string.IsNullOrWhiteSpace(result.OutputNote));
        using var book = new XLWorkbook(result.OutputPath);
        var sheet = book.Worksheet(1);
        Assert.Equal(90, sheet.Cell("C2").GetDouble());
        Assert.Equal(80, sheet.Cell("C3").GetDouble());
    }

    [Fact]
    public async Task ConfirmedOverwriteWritesNewScore()
    {
        var sourcePath = Path.Combine(_directory, "覆盖来源.xlsx");
        var targetPath = Path.Combine(_directory, "覆盖名单.xlsx");
        CreateSimple(sourcePath, "成绩", ["姓名", "学号", "成绩"], sheet => Add(sheet, 2, "张三", "001", 90));
        CreateSimple(targetPath, "名单", ["姓名", "学号", "成绩"], sheet => Add(sheet, 2, "张三", "001", 70));

        IReadOnlyList<OverwriteCandidate>? seen = null;
        var result = await Process(sourcePath, targetPath, confirm: candidates =>
        {
            seen = candidates;
            return Task.FromResult<IReadOnlyList<string>>(candidates.Select(item => item.Key).ToList());
        });

        Assert.NotNull(seen);
        Assert.Single(seen);
        Assert.Equal("70", seen[0].CurrentValue);
        Assert.Equal("90", seen[0].IncomingValue);
        using var book = new XLWorkbook(targetPath);
        Assert.Equal(90, book.Worksheet("名单").Cell("C2").GetDouble());
        Assert.Equal(1, result.WrittenScoreCount);
        Assert.DoesNotContain(result.Issues, issue => issue.Category == IssueCategories.OverwriteSkipped);
    }

    private Task<ProcessResult> Process(
        string sourcePath,
        string targetPath,
        string? sourceSheet = null,
        string? targetSheet = null,
        Func<IReadOnlyList<OverwriteCandidate>, Task<IReadOnlyList<string>>>? confirm = null)
    {
        sourceSheet ??= "成绩";
        targetSheet ??= "名单";
        var processor = new ExcelProcessor();
        return processor.ProcessAsync(
            new ProcessRequest(sourcePath, targetPath, sourceSheet, targetSheet),
            false,
            confirm ?? (candidates => Task.FromResult<IReadOnlyList<string>>(
                candidates.Select(item => item.Key).ToList())));
    }

    private static void CreateSimple(string path, string sheetName, string[] headers, Action<IXLWorksheet> fill)
    {
        using var book = new XLWorkbook();
        var sheet = book.AddWorksheet(sheetName);
        for (var index = 0; index < headers.Length; index++)
        {
            sheet.Cell(1, index + 1).Value = headers[index];
        }

        fill(sheet);
        book.SaveAs(path);
    }

    private static void Add(IXLWorksheet sheet, int row, params object[] values)
    {
        for (var index = 0; index < values.Length; index++)
        {
            sheet.Cell(row, index + 1).Value = XLCellValue.FromObject(values[index]);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }
}
