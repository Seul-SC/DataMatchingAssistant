using System.IO;
using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace StudentScoreTool;

public sealed class ExcelProcessor
{
    private static readonly string[] NameHeaders = ["姓名", "学生姓名", "学生名字", "name"];
    private static readonly string[] IgnoredHeaders =
    [
        "提交时间", "状态", "院系", "专业", "班级", "序号", "平均", "四舍五入",
        "排名", "总分", "总成绩", "任课", "课程", "导出时间", "备注"
    ];

    public IReadOnlyList<SheetOption> InspectSheets(string path, SheetRole role)
    {
        ValidatePath(path);
        using var workbook = OpenWorkbook(path);
        var options = new List<(SheetOption Option, int Score)>();

        foreach (var worksheet in workbook.Worksheets)
        {
            var layout = AnalyzeLayout(worksheet);
            if (layout is null || layout.Students.Count == 0)
            {
                continue;
            }

            var scoreCount = GetScoreColumns(worksheet, layout, role, null).Count;
            var option = new SheetOption(worksheet.Name, layout.Students.Count, scoreCount);
            options.Add((option, scoreCount * 1000 + layout.Students.Count));
        }

        if (options.Count == 0)
        {
            throw new ExcelProcessingException("没有找到包含姓名列和学生数据的工作表。");
        }

        return options
            .OrderByDescending(item => item.Score)
            .Select(item => item.Option)
            .ToList();
    }

    public Task<ProcessResult> ProcessAsync(
        ProcessRequest request,
        bool createBackup,
        Func<IReadOnlyList<ScoreIssue>, Task<bool>> confirmIssues,
        IProgress<string>? progress = null)
    {
        return Task.Run(async () =>
        {
            ValidatePath(request.SourcePath);
            ValidatePath(request.TargetPath);
            progress?.Report("正在识别表格结构…");

            using var sourceBook = OpenWorkbook(request.SourcePath);
            using var targetBook = OpenWorkbook(request.TargetPath);
            var sourceSheet = SelectSheet(sourceBook, SheetRole.Source, request.SourceSheetName);
            var targetSheet = SelectSheet(targetBook, SheetRole.Target, request.TargetSheetName);
            var sourceLayout = AnalyzeLayout(sourceSheet)
                ?? throw new ExcelProcessingException("来源工作表中找不到姓名列。");
            var targetLayout = AnalyzeLayout(targetSheet)
                ?? throw new ExcelProcessingException("名单工作表中找不到姓名列。");

            var sourceScores = GetScoreColumns(sourceSheet, sourceLayout, SheetRole.Source, null);
            if (sourceScores.Count == 0)
            {
                throw new ExcelProcessingException("来源工作表中找不到成绩列。");
            }

            var targetScores = GetScoreColumns(targetSheet, targetLayout, SheetRole.Target, sourceScores);
            if (targetScores.Count == 0)
            {
                throw new ExcelProcessingException("名单工作表中找不到可填写的成绩列。");
            }

            var issues = new List<ScoreIssue>();
            var mappings = MapScoreColumns(sourceScores, targetScores, issues);
            if (mappings.Count == 0)
            {
                throw new ExcelProcessingException("来源成绩列与名单成绩列无法对应。");
            }

            progress?.Report("正在匹配学生…");
            var matches = MatchStudents(sourceLayout.Students, targetLayout.Students, issues);
            var writes = new List<WritePlan>();
            var blankScoreCount = 0;

            foreach (var match in matches.OrderBy(item => item.Target.Row))
            {
                foreach (var mapping in mappings)
                {
                    var sourceCell = sourceSheet.Cell(match.Source.Row, mapping.Source.Column);
                    if (sourceCell.IsEmpty())
                    {
                        blankScoreCount++;
                        continue;
                    }

                    var targetCell = targetSheet.Cell(match.Target.Row, mapping.Target.Column);
                    if (targetCell.HasFormula)
                    {
                        issues.Add(new ScoreIssue(
                            "公式未覆盖",
                            match.Target.Name,
                            $"{targetCell.Address} 是公式单元格，已跳过。"));
                        continue;
                    }

                    writes.Add(new WritePlan(
                        match.Target.Row,
                        mapping.Target.Column,
                        GetCellValue(sourceCell)));
                }
            }

            if (issues.Count > 0 && !await confirmIssues(issues))
            {
                throw new OperationCanceledException();
            }

            if (writes.Count == 0)
            {
                return new ProcessResult(
                    request.TargetPath,
                    null,
                    false,
                    matches.Count,
                    0,
                    blankScoreCount,
                    issues);
            }

            progress?.Report("正在写入并校验成绩…");
            foreach (var write in writes)
            {
                targetSheet.Cell(write.Row, write.Column).Value = write.Value;
            }

            return SaveValidatedWorkbook(
                targetBook,
                targetSheet.Name,
                request.TargetPath,
                writes,
                matches.Count,
                blankScoreCount,
                issues,
                createBackup);
        });
    }

    private static XLWorkbook OpenWorkbook(string path)
    {
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var memory = new MemoryStream();
        input.CopyTo(memory);
        memory.Position = 0;
        return new XLWorkbook(memory);
    }

    private static IXLWorksheet SelectSheet(IXLWorkbook workbook, SheetRole role, string? requestedName)
    {
        if (!string.IsNullOrWhiteSpace(requestedName))
        {
            return workbook.Worksheets.FirstOrDefault(sheet => sheet.Name == requestedName)
                ?? throw new ExcelProcessingException($"找不到工作表“{requestedName}”。");
        }

        var candidates = workbook.Worksheets
            .Select(sheet => new
            {
                Sheet = sheet,
                Layout = AnalyzeLayout(sheet)
            })
            .Where(item => item.Layout is not null && item.Layout.Students.Count > 0)
            .Select(item => new
            {
                item.Sheet,
                Score = GetScoreColumns(item.Sheet, item.Layout!, role, null).Count * 1000
                    + item.Layout!.Students.Count
            })
            .OrderByDescending(item => item.Score)
            .ToList();

        if (candidates.Count == 0)
        {
            throw new ExcelProcessingException("没有找到包含姓名列和学生数据的工作表。");
        }

        if (candidates.Count > 1 && candidates[0].Score == candidates[1].Score)
        {
            throw new ExcelProcessingException("有多个可能的工作表，请在界面上手动选择。");
        }

        return candidates[0].Sheet;
    }

    private static SheetLayout? AnalyzeLayout(IXLWorksheet sheet)
    {
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        var lastColumn = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        if (lastRow == 0 || lastColumn == 0)
        {
            return null;
        }

        IXLCell? nameHeader = null;
        foreach (var cell in sheet.Rows(1, Math.Min(20, lastRow)).CellsUsed())
        {
            if (IsNameHeader(cell.GetFormattedString()))
            {
                nameHeader = cell;
                break;
            }
        }

        if (nameHeader is null)
        {
            return null;
        }

        var headerRow = nameHeader.Address.RowNumber;
        var nameColumn = nameHeader.Address.ColumnNumber;
        var dataStartRow = 0;
        for (var row = headerRow + 1; row <= lastRow; row++)
        {
            var value = NormalizeText(sheet.Cell(row, nameColumn).GetFormattedString());
            if (value.Length > 0 && !IsNameHeader(value))
            {
                dataStartRow = row;
                break;
            }
        }

        if (dataStartRow == 0)
        {
            return null;
        }

        var idColumn = 0;
        var genderColumn = 0;
        for (var column = 1; column <= lastColumn; column++)
        {
            var headers = GetHeaderParts(sheet, headerRow, dataStartRow - 1, column);
            var text = string.Join(' ', headers);
            if (text.Contains("学号", StringComparison.OrdinalIgnoreCase)
                || Regex.IsMatch(text, @"(^|\s)(id|student id)(\s|$)", RegexOptions.IgnoreCase))
            {
                idColumn = column;
            }

            if (headers.Any(header => NormalizeHeader(header) is "性别" or "gender" or "sex"))
            {
                genderColumn = column;
            }
        }

        var students = new List<StudentRow>();
        for (var row = dataStartRow; row <= lastRow; row++)
        {
            var name = NormalizeName(sheet.Cell(row, nameColumn).GetFormattedString());
            if (name.Length == 0 || IsNameHeader(name) || name is "合计" or "总计" or "平均分")
            {
                continue;
            }

            var id = idColumn == 0
                ? string.Empty
                : NormalizeIdentifier(sheet.Cell(row, idColumn).GetFormattedString());
            var gender = genderColumn == 0
                ? string.Empty
                : NormalizeGender(sheet.Cell(row, genderColumn).GetFormattedString());
            students.Add(new StudentRow(row, name, id, gender));
        }

        return new SheetLayout(
            headerRow,
            dataStartRow,
            lastRow,
            lastColumn,
            nameColumn,
            idColumn,
            genderColumn,
            students);
    }

    private static List<ScoreColumn> GetScoreColumns(
        IXLWorksheet sheet,
        SheetLayout layout,
        SheetRole role,
        IReadOnlyList<ScoreColumn>? sourceScores)
    {
        var columns = new List<ScoreColumn>();
        var sourceNames = sourceScores?
            .Select(column => NormalizeScoreGroup(column.Group))
            .Where(name => name.Length > 0)
            .ToHashSet() ?? [];

        for (var column = 1; column <= layout.LastColumn; column++)
        {
            if (column == layout.NameColumn || column == layout.IdColumn || column == layout.GenderColumn)
            {
                continue;
            }

            var parts = GetHeaderParts(sheet, layout.HeaderRow, layout.DataStartRow - 1, column);
            if (parts.Count == 0)
            {
                continue;
            }

            var group = parts[0];
            var fullHeader = string.Join(' ', parts);
            if (IgnoredHeaders.Any(header => fullHeader.Contains(header, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var hasScoreWord = fullHeader.Contains("成绩", StringComparison.OrdinalIgnoreCase)
                || fullHeader.Contains("分数", StringComparison.OrdinalIgnoreCase)
                || fullHeader.Contains("得分", StringComparison.OrdinalIgnoreCase);
            var isOrdinal = IsOrdinalHeader(group);
            var matchesSourceName = role == SheetRole.Target
                && sourceNames.Contains(NormalizeScoreGroup(group));

            if (hasScoreWord || isOrdinal || matchesSourceName)
            {
                columns.Add(new ScoreColumn(column, group, fullHeader));
            }
        }

        if (columns.Count == 0 && role == SheetRole.Source)
        {
            for (var column = 1; column <= layout.LastColumn; column++)
            {
                if (column == layout.NameColumn || column == layout.IdColumn || column == layout.GenderColumn)
                {
                    continue;
                }

                var parts = GetHeaderParts(sheet, layout.HeaderRow, layout.DataStartRow - 1, column);
                var fullHeader = string.Join(' ', parts);
                if (parts.Count == 0
                    || IgnoredHeaders.Any(header => fullHeader.Contains(header, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                if (LooksLikeScoreData(sheet, layout, column))
                {
                    columns.Add(new ScoreColumn(column, parts[0], fullHeader));
                }
            }
        }

        return columns;
    }

    private static bool LooksLikeScoreData(IXLWorksheet sheet, SheetLayout layout, int column)
    {
        var values = layout.Students
            .Select(student => sheet.Cell(student.Row, column))
            .Where(cell => !cell.IsEmpty())
            .ToList();
        if (values.Count == 0)
        {
            return false;
        }

        var numericCount = values.Count(cell =>
            cell.DataType == XLDataType.Number
            || double.TryParse(cell.GetFormattedString(), out _));
        if (numericCount >= Math.Max(1, (int)Math.Ceiling(values.Count * 0.7)))
        {
            return true;
        }

        var textGrades = new[] { "优", "良", "中", "差", "及格", "不及格", "缺考", "免修", "缓考", "通过", "不通过" };
        return values.All(cell => textGrades.Contains(NormalizeText(cell.GetFormattedString())));
    }

    private static List<ScoreMapping> MapScoreColumns(
        IReadOnlyList<ScoreColumn> sourceScores,
        IReadOnlyList<ScoreColumn> targetScores,
        List<ScoreIssue> issues)
    {
        var mappings = new List<ScoreMapping>();
        var usedTargets = new HashSet<int>();

        for (var index = 0; index < sourceScores.Count; index++)
        {
            var source = sourceScores[index];
            var key = NormalizeScoreGroup(source.Group);
            var namedTargets = targetScores
                .Where(target => !usedTargets.Contains(target.Column))
                .Where(target => key.Length > 0 && NormalizeScoreGroup(target.Group) == key)
                .ToList();

            ScoreColumn? target = null;
            if (namedTargets.Count == 1)
            {
                target = namedTargets[0];
            }
            else if (index < targetScores.Count
                && !usedTargets.Contains(targetScores[index].Column)
                && CanMapByPosition(source, targetScores[index]))
            {
                target = targetScores[index];
            }

            if (target is null)
            {
                issues.Add(new ScoreIssue(
                    "成绩列未写入",
                    string.Empty,
                    $"名单表没有与“{source.FullHeader}”对应的已有成绩列，未新增列。"));
                continue;
            }

            usedTargets.Add(target.Column);
            mappings.Add(new ScoreMapping(source, target));
        }

        return mappings;
    }

    private static bool CanMapByPosition(ScoreColumn source, ScoreColumn target)
    {
        var sourceName = NormalizeScoreGroup(source.Group);
        var targetName = NormalizeScoreGroup(target.Group);
        if (sourceName.Length == 0 && targetName.Length == 0)
        {
            return true;
        }

        if (!IsOrdinalHeader(target.Group))
        {
            return false;
        }

        return IsOrdinalHeader(source.Group) || sourceName.Length == 0;
    }

    private static List<StudentMatch> MatchStudents(
        IReadOnlyList<StudentRow> sourceStudents,
        IReadOnlyList<StudentRow> targetStudents,
        List<ScoreIssue> issues)
    {
        var matches = new List<StudentMatch>();
        var sourceGroups = sourceStudents.GroupBy(student => student.Name).ToList();
        var targetNames = targetStudents.Select(student => student.Name).ToHashSet();

        foreach (var sourceGroup in sourceGroups)
        {
            var targets = targetStudents.Where(student => student.Name == sourceGroup.Key).ToList();
            var sources = sourceGroup.ToList();
            if (targets.Count == 0)
            {
                foreach (var source in sources)
                {
                    issues.Add(new ScoreIssue("名单中没有该学生", source.Name, "未添加学生行，该生的成绩未写入。"));
                }
                continue;
            }

            if (sources.Count == 1 && targets.Count == 1)
            {
                var source = sources[0];
                var target = targets[0];
                if (HasIdentifierConflict(source, target))
                {
                    issues.Add(new ScoreIssue(
                        "学号冲突",
                        target.Name,
                        $"唯一姓名匹配，但学号不一致（成绩表：{source.Id}；名单表：{target.Id}），未写入。"));
                }
                else
                {
                    matches.Add(new StudentMatch(source, target));
                }
                continue;
            }

            var remainingSources = new List<StudentRow>(sources);
            var remainingTargets = new List<StudentRow>(targets);

            foreach (var target in targets.Where(student => student.Id.Length > 0))
            {
                var idMatches = remainingSources.Where(source => source.Id == target.Id).ToList();
                if (idMatches.Count == 1)
                {
                    matches.Add(new StudentMatch(idMatches[0], target));
                    remainingSources.Remove(idMatches[0]);
                    remainingTargets.Remove(target);
                }
            }

            foreach (var genderGroup in remainingTargets
                .Where(student => student.Gender.Length > 0)
                .GroupBy(student => student.Gender)
                .ToList())
            {
                var genderSources = remainingSources
                    .Where(source => source.Gender == genderGroup.Key)
                    .ToList();
                if (genderGroup.Count() == 1 && genderSources.Count == 1)
                {
                    var target = genderGroup.First();
                    matches.Add(new StudentMatch(genderSources[0], target));
                    remainingSources.Remove(genderSources[0]);
                    remainingTargets.Remove(target);
                }
            }

            foreach (var target in remainingTargets)
            {
                issues.Add(new ScoreIssue(
                    "同名学生无法区分",
                    target.Name,
                    "学号和性别均不能唯一确定对应学生，未写入。"));
            }

            foreach (var source in remainingSources)
            {
                if (!targetNames.Contains(source.Name))
                {
                    issues.Add(new ScoreIssue("名单中没有该学生", source.Name, "该生的成绩未写入。"));
                }
            }
        }

        foreach (var target in targetStudents.Where(target => !sourceGroups.Any(group => group.Key == target.Name)))
        {
            issues.Add(new ScoreIssue("成绩表中没有该学生", target.Name, "目标成绩保持原样。"));
        }

        return matches;
    }

    private static ProcessResult SaveValidatedWorkbook(
        IXLWorkbook workbook,
        string targetSheetName,
        string targetPath,
        IReadOnlyList<WritePlan> writes,
        int matchedStudentCount,
        int blankScoreCount,
        IReadOnlyList<ScoreIssue> issues,
        bool createBackup)
    {
        var directory = Path.GetDirectoryName(targetPath)
            ?? throw new ExcelProcessingException("无法确定名单文件所在目录。");
        var tempPath = Path.Combine(directory, $".score-tool-{Guid.NewGuid():N}.xlsx");
        string? backupPath = null;

        try
        {
            workbook.SaveAs(tempPath);
            ValidateSavedWorkbook(tempPath, targetSheetName, writes);

            if (createBackup)
            {
                var backupDirectory = Path.Combine(directory, "备份");
                Directory.CreateDirectory(backupDirectory);
                var fileName = Path.GetFileNameWithoutExtension(targetPath);
                backupPath = Path.Combine(
                    backupDirectory,
                    $"{fileName}-{DateTime.Now:yyyyMMdd-HHmmss-fff}.xlsx");
                File.Copy(targetPath, backupPath);
            }

            try
            {
                File.Replace(tempPath, targetPath, null);
                return new ProcessResult(
                    targetPath,
                    backupPath,
                    false,
                    matchedStudentCount,
                    writes.Count,
                    blankScoreCount,
                    issues);
            }
            catch (IOException)
            {
                return SaveCopyInstead(tempPath, targetPath, backupPath, matchedStudentCount, writes.Count, blankScoreCount, issues);
            }
            catch (UnauthorizedAccessException)
            {
                return SaveCopyInstead(tempPath, targetPath, backupPath, matchedStudentCount, writes.Count, blankScoreCount, issues);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static ProcessResult SaveCopyInstead(
        string tempPath,
        string targetPath,
        string? backupPath,
        int matchedStudentCount,
        int writtenScoreCount,
        int blankScoreCount,
        IReadOnlyList<ScoreIssue> issues)
    {
        var directory = Path.GetDirectoryName(targetPath)!;
        var name = Path.GetFileNameWithoutExtension(targetPath);
        var extension = Path.GetExtension(targetPath);
        var copyPath = Path.Combine(directory, $"{name}-已填写{extension}");
        if (File.Exists(copyPath))
        {
            copyPath = Path.Combine(directory, $"{name}-已填写-{DateTime.Now:yyyyMMdd-HHmmss}{extension}");
        }

        File.Move(tempPath, copyPath);
        return new ProcessResult(
            copyPath,
            backupPath,
            true,
            matchedStudentCount,
            writtenScoreCount,
            blankScoreCount,
            issues);
    }

    private static void ValidateSavedWorkbook(
        string path,
        string sheetName,
        IReadOnlyList<WritePlan> writes)
    {
        using var workbook = OpenWorkbook(path);
        var sheet = workbook.Worksheets.First(sheet => sheet.Name == sheetName);
        foreach (var write in writes)
        {
            var actual = sheet.Cell(write.Row, write.Column).Value;
            if (!actual.Equals(write.Value))
            {
                throw new ExcelProcessingException(
                    $"写入后校验失败：{sheet.Cell(write.Row, write.Column).Address}。原文件未被替换。");
            }
        }
    }

    private static XLCellValue GetCellValue(IXLCell cell)
    {
        return cell.HasFormula ? cell.CachedValue : cell.Value;
    }

    private static List<string> GetHeaderParts(IXLWorksheet sheet, int firstRow, int lastRow, int column)
    {
        var result = new List<string>();
        for (var row = firstRow; row <= lastRow; row++)
        {
            var text = GetMergedHeaderText(sheet.Cell(row, column));
            if (text.Length > 0 && !result.Contains(text))
            {
                result.Add(text);
            }
        }
        return result;
    }

    private static string GetMergedHeaderText(IXLCell cell)
    {
        var text = NormalizeText(cell.GetFormattedString());
        if (text.Length > 0 || !cell.IsMerged())
        {
            return text;
        }
        return NormalizeText(cell.MergedRange().FirstCell().GetFormattedString());
    }

    private static bool IsNameHeader(string value)
    {
        var normalized = NormalizeHeader(value);
        return NameHeaders.Contains(normalized)
            || (normalized.EndsWith("姓名", StringComparison.Ordinal) && !normalized.Contains("家长"));
    }

    private static bool IsOrdinalHeader(string value)
    {
        var normalized = NormalizeScoreGroup(value);
        return normalized.Length > 0 && int.TryParse(normalized, out var number) && number > 0 && number <= 100;
    }

    private static string NormalizeScoreGroup(string value)
    {
        var text = NormalizeHeader(value);
        text = Regex.Replace(text, @"[（(][^）)]*[）)]", string.Empty);
        text = text.Replace("成绩", string.Empty)
            .Replace("分数", string.Empty)
            .Replace("得分", string.Empty)
            .Replace("作业", string.Empty)
            .Replace("考试", string.Empty)
            .Replace("考核", string.Empty)
            .Replace("第", string.Empty)
            .Replace("次", string.Empty);
        text = Regex.Replace(text, @"^(\d+)[.、]$", "$1");

        return text switch
        {
            "一" => "1",
            "二" or "两" => "2",
            "三" => "3",
            "四" => "4",
            "五" => "5",
            "六" => "6",
            "七" => "7",
            "八" => "8",
            "九" => "9",
            "十" => "10",
            _ => text
        };
    }

    private static string NormalizeHeader(string value)
    {
        return NormalizeText(value).TrimEnd('：', ':').ToLowerInvariant();
    }

    private static string NormalizeText(string? value)
    {
        return Regex.Replace(value?.Replace('\u00A0', ' ').Trim() ?? string.Empty, @"\s+", " ");
    }

    private static string NormalizeName(string value)
    {
        return NormalizeText(value).Replace(" ", string.Empty);
    }

    private static string NormalizeIdentifier(string value)
    {
        return NormalizeText(value).Replace(" ", string.Empty).ToUpperInvariant();
    }

    private static string NormalizeGender(string value)
    {
        var gender = NormalizeText(value);
        return gender switch
        {
            "男性" => "男",
            "女性" => "女",
            _ => gender
        };
    }

    private static bool HasIdentifierConflict(StudentRow source, StudentRow target)
    {
        return source.Id.Length > 0 && target.Id.Length > 0 && source.Id != target.Id;
    }

    private static void ValidatePath(string path)
    {
        if (!File.Exists(path))
        {
            throw new ExcelProcessingException($"文件不存在：{path}");
        }

        if (!string.Equals(Path.GetExtension(path), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            throw new ExcelProcessingException("目前只支持 .xlsx 文件。");
        }
    }

    private sealed record StudentRow(int Row, string Name, string Id, string Gender);
    private sealed record ScoreColumn(int Column, string Group, string FullHeader);
    private sealed record ScoreMapping(ScoreColumn Source, ScoreColumn Target);
    private sealed record StudentMatch(StudentRow Source, StudentRow Target);
    private sealed record WritePlan(int Row, int Column, XLCellValue Value);

    private sealed record SheetLayout(
        int HeaderRow,
        int DataStartRow,
        int LastRow,
        int LastColumn,
        int NameColumn,
        int IdColumn,
        int GenderColumn,
        List<StudentRow> Students);
}
