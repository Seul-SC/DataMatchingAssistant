using System.Globalization;
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
    private static readonly string[] TextGrades =
    [
        "优", "良", "中", "差", "及格", "不及格", "缺考", "免修", "缓考", "通过", "不通过",
        "合格", "不合格", "作弊", "旷考", "补考"
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
        Func<IReadOnlyList<OverwriteCandidate>, Task<IReadOnlyList<string>>> confirmOverwrites,
        IProgress<string>? progress = null)
    {
        return Task.Run(async () =>
        {
            ValidatePath(request.SourcePath);
            ValidatePath(request.TargetPath);
            var sourceFileName = Path.GetFileName(request.SourcePath);
            var targetFileName = Path.GetFileName(request.TargetPath);
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
            var pendingOverwrites = new List<PendingOverwrite>();
            var columnsWithAnyGrade = mappings
                .Where(mapping => sourceLayout.Students.Any(student =>
                    !IsEffectivelyEmpty(sourceSheet.Cell(student.Row, mapping.Source.Column))))
                .Select(mapping => mapping.Source.Column)
                .ToHashSet();

            foreach (var match in matches.OrderBy(item => item.Target.Row))
            {
                foreach (var mapping in mappings)
                {
                    var sourceCell = sourceSheet.Cell(match.Source.Row, mapping.Source.Column);
                    var columnTitle = mapping.Target.FullHeader;
                    if (IsEffectivelyEmpty(sourceCell))
                    {
                        // 成绩表空格一律保持名单原样，不填入。
                        // 仅当同一作业既有成绩又有空格时，才在问题窗列出；整列都空则不列为问题。
                        blankScoreCount++;
                        if (!columnsWithAnyGrade.Contains(mapping.Source.Column))
                        {
                            continue;
                        }

                        var targetCellForBlank = targetSheet.Cell(match.Target.Row, mapping.Target.Column);
                        var original = FormatScore(GetCellValue(targetCellForBlank), targetCellForBlank);
                        issues.Add(new ScoreIssue(
                            IssueCategories.BlankSource,
                            match.Target.DisplayName,
                            $"「{columnTitle}」名单原成绩：{original}；成绩表该格为空，已保持原样。",
                            GetDisplayId(match),
                            "名单原成绩",
                            original,
                            "成绩表",
                            "空"));
                        continue;
                    }

                    var targetCell = targetSheet.Cell(match.Target.Row, mapping.Target.Column);
                    if (targetCell.HasFormula)
                    {
                        issues.Add(new ScoreIssue(
                            IssueCategories.FormulaSkipped,
                            match.Target.DisplayName,
                            $"{targetCell.Address} 是公式单元格，已跳过。",
                            GetDisplayId(match)));
                        continue;
                    }

                    var incoming = GetCellValue(sourceCell);
                    var outOfRangeIssue = TryCreateOutOfRangeIssue(match, columnTitle, incoming);
                    if (IsEffectivelyEmpty(targetCell) || ScoresEqual(GetCellValue(targetCell), incoming))
                    {
                        writes.Add(new WritePlan(match.Target.Row, mapping.Target.Column, incoming));
                        if (outOfRangeIssue is not null)
                        {
                            issues.Add(outOfRangeIssue);
                        }

                        continue;
                    }

                    var currentText = FormatScore(GetCellValue(targetCell), targetCell);
                    var incomingText = FormatScore(incoming, sourceCell);
                    var key = $"{match.Target.Row}:{mapping.Target.Column}";
                    pendingOverwrites.Add(new PendingOverwrite(
                        new OverwriteCandidate(
                            key,
                            match.Target.DisplayName,
                            GetDisplayId(match),
                            columnTitle,
                            currentText,
                            incomingText),
                        new WritePlan(match.Target.Row, mapping.Target.Column, incoming),
                        outOfRangeIssue));
                }
            }

            if (pendingOverwrites.Count > 0)
            {
                progress?.Report("发现已有成绩与新成绩不同，等待确认…");
                var approved = new HashSet<string>(
                    await confirmOverwrites(pendingOverwrites.Select(item => item.Candidate).ToList()),
                    StringComparer.Ordinal);
                foreach (var item in pendingOverwrites)
                {
                    if (approved.Contains(item.Candidate.Key))
                    {
                        writes.Add(item.Write);
                        if (item.OutOfRangeIssue is not null)
                        {
                            issues.Add(item.OutOfRangeIssue);
                        }
                    }
                    else
                    {
                        issues.Add(new ScoreIssue(
                            IssueCategories.OverwriteSkipped,
                            item.Candidate.StudentName,
                            $"「{item.Candidate.ScoreColumn}」原成绩 {item.Candidate.CurrentValue}，新成绩 {item.Candidate.IncomingValue}，已按你的选择保留原成绩。",
                            item.Candidate.StudentId,
                            "原成绩",
                            item.Candidate.CurrentValue,
                            "新成绩",
                            item.Candidate.IncomingValue));
                    }
                }
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
                    issues,
                    null,
                    sourceFileName,
                    targetFileName);
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
                createBackup,
                sourceFileName,
                targetFileName);
        });
    }

    private static XLWorkbook OpenWorkbook(string path) => SpreadsheetLoader.OpenAsWorkbook(path);

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
            var rawName = sheet.Cell(row, nameColumn).GetFormattedString();
            var displayName = TextNormalization.DisplayText(rawName);
            var name = TextNormalization.NormalizeName(rawName);
            if (name.Length == 0 || IsNameHeader(rawName) || name is "合计" or "总计" or "平均分")
            {
                continue;
            }

            var rawId = idColumn == 0 ? string.Empty : sheet.Cell(row, idColumn).GetFormattedString();
            var gender = genderColumn == 0
                ? string.Empty
                : NormalizeGender(sheet.Cell(row, genderColumn).GetFormattedString());
            students.Add(new StudentRow(
                row,
                displayName,
                name,
                TextNormalization.DisplayText(rawId),
                TextNormalization.NormalizeId(rawId),
                gender));
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
            .Where(cell => !IsEffectivelyEmpty(cell))
            .ToList();
        if (values.Count == 0)
        {
            return false;
        }

        var numericCount = values.Count(cell =>
            cell.DataType == XLDataType.Number
            || TextNormalization.TryParseScoreNumber(cell.GetFormattedString(), out _));
        if (numericCount >= Math.Max(1, (int)Math.Ceiling(values.Count * 0.7)))
        {
            return true;
        }

        return values.All(cell => LooksLikeTextGrade(NormalizeText(cell.GetFormattedString())));
    }

    private static bool LooksLikeTextGrade(string text)
    {
        if (TextGrades.Contains(text))
        {
            return true;
        }

        return text.Length is > 0 and <= 10
            && !text.Contains('：')
            && !text.Contains(':');
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
                    IssueCategories.ColumnUnmapped,
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
        var usedSources = new HashSet<StudentRow>();
        var usedTargets = new HashSet<StudentRow>();

        var targetsById = targetStudents
            .Where(student => student.HasId)
            .GroupBy(student => student.NormalizedId)
            .ToDictionary(group => group.Key, group => group.ToList());

        foreach (var sourceGroup in sourceStudents.Where(student => student.HasId).GroupBy(student => student.NormalizedId))
        {
            if (!targetsById.TryGetValue(sourceGroup.Key, out var idTargets))
            {
                continue;
            }

            var idSources = sourceGroup.ToList();
            if (idSources.Count != 1 || idTargets.Count != 1)
            {
                foreach (var student in idSources.Concat(idTargets))
                {
                    issues.Add(new ScoreIssue(
                        IssueCategories.DuplicateIdUnresolved,
                        student.DisplayName,
                        $"学号 {PreferDisplayId(idSources[0], idTargets[0])} 在成绩表或名单里不唯一，未按学号匹配。",
                        PreferDisplayId(idSources[0], idTargets[0])));
                }

                foreach (var student in idSources)
                {
                    usedSources.Add(student);
                }

                foreach (var student in idTargets)
                {
                    usedTargets.Add(student);
                }

                continue;
            }

            var source = idSources[0];
            var target = idTargets[0];
            matches.Add(new StudentMatch(source, target));
            usedSources.Add(source);
            usedTargets.Add(target);
            if (source.NormalizedName != target.NormalizedName)
            {
                var idText = PreferDisplayId(source, target);
                issues.Add(new ScoreIssue(
                    IssueCategories.NameMismatch,
                    target.DisplayName,
                    $"学号 {idText}：成绩表「{source.DisplayName}」 vs 名单「{target.DisplayName}」。已按学号写入成绩。",
                    idText,
                    "成绩表",
                    source.DisplayName,
                    "名单",
                    target.DisplayName));
            }
        }

        var remainingSources = sourceStudents.Where(student => !usedSources.Contains(student)).ToList();
        var remainingTargets = targetStudents.Where(student => !usedTargets.Contains(student)).ToList();

        foreach (var sourceGroup in remainingSources.GroupBy(student => student.NormalizedName))
        {
            var sources = sourceGroup.ToList();
            var targets = remainingTargets
                .Where(student => student.NormalizedName == sourceGroup.Key && !usedTargets.Contains(student))
                .ToList();
            if (targets.Count == 0)
            {
                continue;
            }

            if (sources.Count == 1 && targets.Count == 1 && CanNameMatch(sources[0], targets[0]))
            {
                matches.Add(new StudentMatch(sources[0], targets[0]));
                usedSources.Add(sources[0]);
                usedTargets.Add(targets[0]);
                continue;
            }

            var leftoverSources = new List<StudentRow>(sources.Where(student => !usedSources.Contains(student)));
            var leftoverTargets = new List<StudentRow>(targets.Where(student => !usedTargets.Contains(student)));

            foreach (var genderGroup in leftoverTargets
                .Where(student => student.Gender.Length > 0)
                .GroupBy(student => student.Gender)
                .ToList())
            {
                var genderTargets = genderGroup.ToList();
                var genderSources = leftoverSources
                    .Where(student => student.Gender == genderGroup.Key && genderTargets.Any(target => CanNameMatch(student, target)))
                    .ToList();
                if (genderTargets.Count == 1
                    && genderSources.Count == 1
                    && CanNameMatch(genderSources[0], genderTargets[0]))
                {
                    matches.Add(new StudentMatch(genderSources[0], genderTargets[0]));
                    usedSources.Add(genderSources[0]);
                    usedTargets.Add(genderTargets[0]);
                    leftoverSources.Remove(genderSources[0]);
                    leftoverTargets.Remove(genderTargets[0]);
                }
            }

            leftoverSources = leftoverSources.Where(student => !usedSources.Contains(student)).ToList();
            leftoverTargets = leftoverTargets.Where(student => !usedTargets.Contains(student)).ToList();
            if (leftoverSources.Count == 1
                && leftoverTargets.Count == 1
                && CanNameMatch(leftoverSources[0], leftoverTargets[0]))
            {
                matches.Add(new StudentMatch(leftoverSources[0], leftoverTargets[0]));
                usedSources.Add(leftoverSources[0]);
                usedTargets.Add(leftoverTargets[0]);
                leftoverSources.Clear();
                leftoverTargets.Clear();
            }

            if (leftoverSources.Count > 0 && leftoverTargets.Count > 0
                && leftoverSources.Any(source => leftoverTargets.Any(target => CanNameMatch(source, target))))
            {
                foreach (var target in leftoverTargets)
                {
                    issues.Add(new ScoreIssue(
                        IssueCategories.DuplicateNameUnresolved,
                        target.DisplayName,
                        "学号和性别均不能唯一确定对应学生，未写入。",
                        target.DisplayId));
                    usedTargets.Add(target);
                }

                foreach (var source in leftoverSources)
                {
                    usedSources.Add(source);
                }
            }
        }

        foreach (var source in sourceStudents.Where(student => !usedSources.Contains(student)))
        {
            issues.Add(new ScoreIssue(
                IssueCategories.MissingInTarget,
                source.DisplayName,
                "未添加学生行，该生的成绩未写入。",
                source.DisplayId));
        }

        foreach (var target in targetStudents.Where(student => !usedTargets.Contains(student)))
        {
            issues.Add(new ScoreIssue(
                IssueCategories.MissingInSource,
                target.DisplayName,
                "目标成绩保持原样。",
                target.DisplayId));
        }

        return matches;
    }

    private static bool CanNameMatch(StudentRow source, StudentRow target)
    {
        return !(source.HasId && target.HasId);
    }

    private static ProcessResult SaveValidatedWorkbook(
        IXLWorkbook workbook,
        string targetSheetName,
        string targetPath,
        IReadOnlyList<WritePlan> writes,
        int matchedStudentCount,
        int blankScoreCount,
        IReadOnlyList<ScoreIssue> issues,
        bool createBackup,
        string sourceFileName,
        string targetFileName)
    {
        var directory = Path.GetDirectoryName(targetPath)
            ?? throw new ExcelProcessingException("无法确定名单文件所在目录。");
        var originalExtension = Path.GetExtension(targetPath);
        var isXlsx = originalExtension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase);
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
                    $"{fileName}-{DateTime.Now:yyyyMMdd-HHmmss-fff}{originalExtension}");
                File.Copy(targetPath, backupPath);
            }

            if (!isXlsx)
            {
                var outputPath = CreateSiblingXlsxPath(targetPath);
                File.Move(tempPath, outputPath);
                var note = $"原名单是 {originalExtension.ToLowerInvariant()}，为避免改坏原文件，结果已另存为「{Path.GetFileName(outputPath)}」。原文件未改动。";
                return new ProcessResult(
                    outputPath,
                    backupPath,
                    true,
                    matchedStudentCount,
                    writes.Count,
                    blankScoreCount,
                    issues,
                    note,
                    sourceFileName,
                    targetFileName);
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
                    issues,
                    null,
                    sourceFileName,
                    targetFileName);
            }
            catch (IOException)
            {
                return SaveCopyInstead(tempPath, targetPath, backupPath, matchedStudentCount, writes.Count, blankScoreCount, issues, sourceFileName, targetFileName);
            }
            catch (UnauthorizedAccessException)
            {
                return SaveCopyInstead(tempPath, targetPath, backupPath, matchedStudentCount, writes.Count, blankScoreCount, issues, sourceFileName, targetFileName);
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

    private static string CreateSiblingXlsxPath(string targetPath)
    {
        var directory = Path.GetDirectoryName(targetPath)!;
        var name = Path.GetFileNameWithoutExtension(targetPath);
        var copyPath = Path.Combine(directory, $"{name}-已填写.xlsx");
        if (File.Exists(copyPath))
        {
            copyPath = Path.Combine(directory, $"{name}-已填写-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx");
        }

        return copyPath;
    }

    private static ProcessResult SaveCopyInstead(
        string tempPath,
        string targetPath,
        string? backupPath,
        int matchedStudentCount,
        int writtenScoreCount,
        int blankScoreCount,
        IReadOnlyList<ScoreIssue> issues,
        string sourceFileName,
        string targetFileName)
    {
        var directory = Path.GetDirectoryName(targetPath)!;
        var name = Path.GetFileNameWithoutExtension(targetPath);
        var copyPath = Path.Combine(directory, $"{name}-已填写.xlsx");
        if (File.Exists(copyPath))
        {
            copyPath = Path.Combine(directory, $"{name}-已填写-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx");
        }

        File.Move(tempPath, copyPath);
        var note = $"原文件正被占用，结果已保存为「{Path.GetFileName(copyPath)}」。原名单未改动。";
        return new ProcessResult(
            copyPath,
            backupPath,
            true,
            matchedStudentCount,
            writtenScoreCount,
            blankScoreCount,
            issues,
            note,
            sourceFileName,
            targetFileName);
    }

    private static void ValidateSavedWorkbook(
        string path,
        string sheetName,
        IReadOnlyList<WritePlan> writes)
    {
        using var workbook = OpenWorkbook(path);
        var sheet = workbook.Worksheets.First(item => item.Name == sheetName);
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

    private static bool IsEffectivelyEmpty(IXLCell cell)
    {
        if (cell.IsEmpty())
        {
            return true;
        }

        return string.IsNullOrWhiteSpace(cell.GetFormattedString());
    }

    private static bool ScoresEqual(XLCellValue left, XLCellValue right)
    {
        if (TryGetNumber(left, out var leftNumber) && TryGetNumber(right, out var rightNumber))
        {
            return Math.Abs(leftNumber - rightNumber) < 0.000000001;
        }

        return string.Equals(FormatScore(left), FormatScore(right), StringComparison.Ordinal);
    }

    private static bool TryGetNumber(XLCellValue value, out double number)
    {
        if (value.IsNumber)
        {
            number = value.GetNumber();
            return true;
        }

        if (value.IsText)
        {
            return TextNormalization.TryParseScoreNumber(value.GetText(), out number);
        }

        number = 0;
        return false;
    }

    private static string FormatScore(XLCellValue value, IXLCell? cell = null)
    {
        if (value.IsBlank || (cell is not null && IsEffectivelyEmpty(cell)))
        {
            return "空";
        }

        if (value.IsNumber)
        {
            var number = value.GetNumber();
            if (Math.Abs(number - Math.Round(number)) < 0.000000001)
            {
                return Math.Round(number).ToString("0", CultureInfo.InvariantCulture);
            }

            return number.ToString(CultureInfo.InvariantCulture);
        }

        var text = cell is null ? value.ToString() : cell.GetFormattedString();
        text = TextNormalization.DisplayText(text);
        return text.Length == 0 ? "空" : text;
    }

    private static ScoreIssue? TryCreateOutOfRangeIssue(StudentMatch match, string columnTitle, XLCellValue incoming)
    {
        if (!TryGetNumber(incoming, out var number) || !TextNormalization.IsNumericScoreOutOfRange(number))
        {
            return null;
        }

        var display = FormatScore(incoming);
        return new ScoreIssue(
            IssueCategories.OutOfRange,
            match.Target.DisplayName,
            $"「{columnTitle}」成绩 {display}，超出 0～150，已按成绩表写入。",
            GetDisplayId(match),
            "成绩",
            display,
            "正常范围",
            "0～150");
    }

    private static string GetDisplayId(StudentMatch match) => PreferDisplayId(match.Source, match.Target);

    private static string PreferDisplayId(StudentRow first, StudentRow second)
    {
        if (first.DisplayId.Length >= second.DisplayId.Length && first.DisplayId.Length > 0)
        {
            return first.DisplayId;
        }

        if (second.DisplayId.Length > 0)
        {
            return second.DisplayId;
        }

        return first.NormalizedId.Length > 0 ? first.NormalizedId : second.NormalizedId;
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
        return Regex.Replace(TextNormalization.ToHalfWidth(value).Replace('\u00A0', ' ').Trim(), @"\s+", " ");
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

    private static void ValidatePath(string path)
    {
        if (!File.Exists(path))
        {
            throw new ExcelProcessingException($"文件不存在：{path}");
        }

        if (!SpreadsheetLoader.IsSupportedPath(path))
        {
            throw new ExcelProcessingException("目前支持 .xlsx、.xls 和 .csv 文件。");
        }
    }

    private sealed record StudentRow(
        int Row,
        string DisplayName,
        string NormalizedName,
        string DisplayId,
        string NormalizedId,
        string Gender)
    {
        public bool HasId => NormalizedId.Length > 0;
    }

    private sealed record ScoreColumn(int Column, string Group, string FullHeader);
    private sealed record ScoreMapping(ScoreColumn Source, ScoreColumn Target);
    private sealed record StudentMatch(StudentRow Source, StudentRow Target);
    private sealed record WritePlan(int Row, int Column, XLCellValue Value);
    private sealed record PendingOverwrite(OverwriteCandidate Candidate, WritePlan Write, ScoreIssue? OutOfRangeIssue);

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
