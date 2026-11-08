using ClosedXML.Excel;

namespace StudentScoreTool.Tests;

public sealed class ExcelProcessorTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"StudentScoreToolTests-{Guid.NewGuid():N}");

    public ExcelProcessorTests()
    {
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public async Task WritesThreeAssignmentsAndPreservesBlankFormulaAndStyle()
    {
        var sourcePath = Path.Combine(_directory, "来源.xlsx");
        var targetPath = Path.Combine(_directory, "名单.xlsx");
        CreateAssignmentSource(sourcePath);
        CreateAssignmentTarget(targetPath);

        var result = await Process(sourcePath, targetPath, "作业统计", "作业", true);

        using var workbook = new XLWorkbook(targetPath);
        var sheet = workbook.Worksheet("作业");
        Assert.Equal(100, sheet.Cell("C3").GetDouble());
        Assert.Equal(85, sheet.Cell("D3").GetDouble());
        Assert.Equal(92, sheet.Cell("E3").GetDouble());
        Assert.Equal(90, sheet.Cell("C4").GetDouble());
        Assert.Equal(66, sheet.Cell("D4").GetDouble());
        Assert.Equal(88, sheet.Cell("E4").GetDouble());
        Assert.Equal(80, sheet.Cell("C5").GetDouble());
        Assert.Equal(70, sheet.Cell("D5").GetDouble());
        Assert.Equal(77, sheet.Cell("E5").GetDouble());
        Assert.True(sheet.Cell("G3").HasFormula);
        Assert.Equal(XLColor.Yellow, sheet.Cell("C3").Style.Fill.BackgroundColor);
        Assert.Equal("已保留", workbook.Worksheet("平时考勤").Cell("A1").GetString());
        Assert.Equal(3, result.MatchedStudentCount);
        Assert.Equal(7, result.WrittenScoreCount);
        Assert.Equal(2, result.BlankScoreCount);
        Assert.Empty(result.Issues);
        Assert.False(result.UsedCopy);
        Assert.NotNull(result.BackupPath);
        Assert.True(File.Exists(result.BackupPath));

        using var backup = new XLWorkbook(result.BackupPath!);
        Assert.True(backup.Worksheet("作业").Cell("C3").IsEmpty());
    }

    [Fact]
    public async Task ResolvesDuplicateNamesByIdThenGenderAndRejectsIdConflict()
    {
        var sourcePath = Path.Combine(_directory, "重名来源.xlsx");
        var targetPath = Path.Combine(_directory, "重名名单.xlsx");
        using (var book = new XLWorkbook())
        {
            var sheet = book.AddWorksheet("成绩");
            sheet.Cell("A1").Value = "姓名";
            sheet.Cell("B1").Value = "学号";
            sheet.Cell("C1").Value = "性别";
            sheet.Cell("D1").Value = "语文成绩";
            AddRow(sheet, 2, "张三", "001", "男", 90);
            AddRow(sheet, 3, "张三", "002", "女", 80);
            AddRow(sheet, 4, "李四", "", "男", 70);
            AddRow(sheet, 5, "李四", "", "女", 60);
            AddRow(sheet, 6, "王五", "100", "", 50);
            book.SaveAs(sourcePath);
        }

        using (var book = new XLWorkbook())
        {
            var sheet = book.AddWorksheet("名单");
            sheet.Cell("A1").Value = "姓名";
            sheet.Cell("B1").Value = "学号";
            sheet.Cell("C1").Value = "性别";
            sheet.Cell("D1").Value = "语文";
            AddRow(sheet, 2, "张三", "002", "女", "");
            AddRow(sheet, 3, "张三", "001", "男", "");
            AddRow(sheet, 4, "李四", "", "女", "");
            AddRow(sheet, 5, "李四", "", "男", "");
            AddRow(sheet, 6, "王五", "999", "", 55);
            book.SaveAs(targetPath);
        }

        var confirmed = false;
        var result = await Process(sourcePath, targetPath, "成绩", "名单", false, issues =>
        {
            confirmed = true;
            Assert.Single(issues);
            Assert.Equal("学号冲突", issues[0].Category);
            return Task.FromResult(true);
        });

        using var resultBook = new XLWorkbook(targetPath);
        var resultSheet = resultBook.Worksheet("名单");
        Assert.Equal(80, resultSheet.Cell("D2").GetDouble());
        Assert.Equal(90, resultSheet.Cell("D3").GetDouble());
        Assert.Equal(60, resultSheet.Cell("D4").GetDouble());
        Assert.Equal(70, resultSheet.Cell("D5").GetDouble());
        Assert.Equal(55, resultSheet.Cell("D6").GetDouble());
        Assert.True(confirmed);
        Assert.Equal(4, result.WrittenScoreCount);
    }

    [Fact]
    public async Task DoesNotAddScoreColumnsOrOverwriteTargetFormulas()
    {
        var sourcePath = Path.Combine(_directory, "多列来源.xlsx");
        var targetPath = Path.Combine(_directory, "少列名单.xlsx");
        CreateSimpleWorkbook(sourcePath, "来源", ["姓名", "第一次成绩", "第二次成绩", "第三次成绩"], sheet =>
        {
            AddRow(sheet, 2, "张三", 90, 80, 70);
        });
        CreateSimpleWorkbook(targetPath, "名单", ["姓名", "1", "2"], sheet =>
        {
            sheet.Cell("A2").Value = "张三";
            sheet.Cell("B2").FormulaA1 = "1+1";
        });

        var result = await Process(sourcePath, targetPath, "来源", "名单", false);

        using var book = new XLWorkbook(targetPath);
        var sheet = book.Worksheet("名单");
        Assert.True(sheet.Cell("B2").HasFormula);
        Assert.Equal(80, sheet.Cell("C2").GetDouble());
        Assert.True(sheet.Cell("D1").IsEmpty());
        Assert.Equal(1, result.WrittenScoreCount);
        Assert.Equal(2, result.Issues.Count);
        Assert.Contains(result.Issues, issue => issue.Category == "成绩列未写入");
        Assert.Contains(result.Issues, issue => issue.Category == "公式未覆盖");
    }

    [Fact]
    public async Task CopiesTextGradesAndZeroWithoutChangingBlankTargets()
    {
        var sourcePath = Path.Combine(_directory, "文字来源.xlsx");
        var targetPath = Path.Combine(_directory, "文字名单.xlsx");
        CreateSimpleWorkbook(sourcePath, "来源", ["姓名", "实验成绩"], sheet =>
        {
            AddRow(sheet, 2, "甲", "优");
            AddRow(sheet, 3, "乙", "缺考");
            AddRow(sheet, 4, "丙", 0);
            AddRow(sheet, 5, "丁", "");
        });
        CreateSimpleWorkbook(targetPath, "名单", ["姓名", "实验"], sheet =>
        {
            AddRow(sheet, 2, "甲", "");
            AddRow(sheet, 3, "乙", "");
            AddRow(sheet, 4, "丙", 10);
            AddRow(sheet, 5, "丁", 20);
        });

        var result = await Process(sourcePath, targetPath, "来源", "名单", false);

        using var book = new XLWorkbook(targetPath);
        var sheet = book.Worksheet("名单");
        Assert.Equal("优", sheet.Cell("B2").GetString());
        Assert.Equal("缺考", sheet.Cell("B3").GetString());
        Assert.Equal(0, sheet.Cell("B4").GetDouble());
        Assert.Equal(20, sheet.Cell("B5").GetDouble());
        Assert.Equal(3, result.WrittenScoreCount);
        Assert.Equal(1, result.BlankScoreCount);
    }

    [Fact]
    public async Task DoesNotWriteMathIntoChineseColumnByPosition()
    {
        var sourcePath = Path.Combine(_directory, "科目来源.xlsx");
        var targetPath = Path.Combine(_directory, "科目名单.xlsx");
        CreateSimpleWorkbook(sourcePath, "来源", ["姓名", "数学"], sheet => AddRow(sheet, 2, "张三", 90));
        CreateSimpleWorkbook(targetPath, "名单", ["姓名", "语文"], sheet => AddRow(sheet, 2, "张三", 10));

        await Assert.ThrowsAsync<ExcelProcessingException>(() =>
            Process(sourcePath, targetPath, "来源", "名单", false));

        using var book = new XLWorkbook(targetPath);
        Assert.Equal(10, book.Worksheet("名单").Cell("B2").GetDouble());
    }

    [Fact]
    public async Task CancelingIssuesLeavesTargetUntouchedAndCreatesNoBackup()
    {
        var sourcePath = Path.Combine(_directory, "取消来源.xlsx");
        var targetPath = Path.Combine(_directory, "取消名单.xlsx");
        CreateSimpleWorkbook(sourcePath, "来源", ["姓名", "学号", "成绩"], sheet =>
        {
            AddRow(sheet, 2, "张三", "001", 90);
        });
        CreateSimpleWorkbook(targetPath, "名单", ["姓名", "学号", "成绩"], sheet =>
        {
            AddRow(sheet, 2, "张三", "002", 10);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Process(sourcePath, targetPath, "来源", "名单", true, _ => Task.FromResult(false)));

        using var book = new XLWorkbook(targetPath);
        Assert.Equal(10, book.Worksheet("名单").Cell("C2").GetDouble());
        Assert.False(Directory.Exists(Path.Combine(_directory, "备份")));
    }

    [Fact]
    public async Task SavesCopyWhenTargetFileIsLocked()
    {
        var sourcePath = Path.Combine(_directory, "锁定来源.xlsx");
        var targetPath = Path.Combine(_directory, "锁定名单.xlsx");
        CreateSimpleWorkbook(sourcePath, "来源", ["姓名", "成绩"], sheet => AddRow(sheet, 2, "张三", 90));
        CreateSimpleWorkbook(targetPath, "名单", ["姓名", "成绩"], sheet => AddRow(sheet, 2, "张三", 10));

        ProcessResult result;
        using (new FileStream(targetPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
        {
            result = await Process(sourcePath, targetPath, "来源", "名单", true);
        }

        Assert.True(result.UsedCopy);
        Assert.NotEqual(targetPath, result.OutputPath);
        Assert.True(File.Exists(result.OutputPath));
        using var original = new XLWorkbook(targetPath);
        Assert.Equal(10, original.Worksheet("名单").Cell("B2").GetDouble());
        using var copy = new XLWorkbook(result.OutputPath);
        Assert.Equal(90, copy.Worksheet("名单").Cell("B2").GetDouble());
    }

    private Task<ProcessResult> Process(
        string sourcePath,
        string targetPath,
        string sourceSheet,
        string targetSheet,
        bool backup,
        Func<IReadOnlyList<ScoreIssue>, Task<bool>>? confirm = null)
    {
        var processor = new ExcelProcessor();
        return processor.ProcessAsync(
            new ProcessRequest(sourcePath, targetPath, sourceSheet, targetSheet),
            backup,
            confirm ?? (_ => Task.FromResult(true)));
    }

    private static void CreateAssignmentSource(string path)
    {
        using var book = new XLWorkbook();
        var sheet = book.AddWorksheet("作业统计");
        sheet.Cell("A1").Value = "作业统计";
        sheet.Cell("A2").Value = "课程：模拟课程";
        sheet.Cell("A3").Value = "学生姓名";
        sheet.Cell("B3").Value = "学号/工号";
        sheet.Cell("C3").Value = "院系";
        sheet.Cell("D3").Value = "专业";
        sheet.Cell("E3").Value = "班级";
        SetAssignmentHeaders(sheet, 6, "第一次作业（第一章）");
        SetAssignmentHeaders(sheet, 9, "第二次作业（第二章）");
        SetAssignmentHeaders(sheet, 12, "第三次作业（第四章）");
        sheet.Cell("A5").Value = "张一";
        sheet.Cell("B5").Value = "001";
        sheet.Cell("F5").Value = 100;
        sheet.Cell("I5").Value = 85;
        sheet.Cell("L5").Value = 92;
        sheet.Cell("A6").Value = "李二";
        sheet.Cell("B6").Value = "002";
        sheet.Cell("F6").Value = 90;
        sheet.Cell("L6").Value = 88;
        sheet.Cell("A7").Value = "王三";
        sheet.Cell("F7").Value = 80;
        sheet.Cell("I7").Value = 70;
        book.SaveAs(path);
    }

    private static void CreateAssignmentTarget(string path)
    {
        using var book = new XLWorkbook();
        book.AddWorksheet("平时考勤").Cell("A1").Value = "已保留";
        var sheet = book.AddWorksheet("作业");
        sheet.Cell("A2").Value = "序号";
        sheet.Cell("B2").Value = "姓名";
        sheet.Cell("C2").Value = "1";
        sheet.Cell("D2").Value = "2";
        sheet.Cell("E2").Value = "3";
        sheet.Cell("F2").Value = "4";
        sheet.Cell("G2").Value = "平均";
        var names = new[] { "张一", "李二", "王三" };
        for (var index = 0; index < names.Length; index++)
        {
            var row = index + 3;
            sheet.Cell(row, 1).Value = index + 1;
            sheet.Cell(row, 2).Value = names[index];
        }
        sheet.Cell("D4").Value = 66;
        sheet.Cell("E5").Value = 77;
        sheet.Cell("G3").FormulaA1 = "AVERAGE(C3:E3)";
        sheet.Cell("C3").Style.Fill.BackgroundColor = XLColor.Yellow;
        book.SaveAs(path);
    }

    private static void SetAssignmentHeaders(IXLWorksheet sheet, int column, string title)
    {
        sheet.Range(3, column, 3, column + 2).Merge().Value = title;
        sheet.Cell(4, column).Value = "成绩";
        sheet.Cell(4, column + 1).Value = "提交时间";
        sheet.Cell(4, column + 2).Value = "状态";
    }

    private static void CreateSimpleWorkbook(
        string path,
        string sheetName,
        string[] headers,
        Action<IXLWorksheet> fill)
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

    private static void AddRow(IXLWorksheet sheet, int row, params object[] values)
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
