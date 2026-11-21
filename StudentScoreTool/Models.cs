namespace StudentScoreTool;

public enum SheetRole
{
    Source,
    Target
}

public sealed record SheetOption(string Name, int StudentCount, int ScoreColumnCount)
{
    public override string ToString() => Name;
}

public static class IssueCategories
{
    public const string NameMismatch = "学号相同但姓名不同";
    public const string BlankSource = "成绩表为空，已保留原成绩";
    public const string OutOfRange = "成绩超出 0～150";
    public const string MissingInTarget = "名单中没有该学生";
    public const string MissingInSource = "成绩表中没有该学生";
    public const string OverwriteSkipped = "已跳过覆盖";
    public const string FormulaSkipped = "公式未覆盖";
    public const string ColumnUnmapped = "成绩列未写入";
    public const string DuplicateNameUnresolved = "同名学生无法区分";
    public const string DuplicateIdUnresolved = "学号重复无法区分";

    public static readonly string[] DisplayOrder =
    [
        NameMismatch,
        OutOfRange,
        BlankSource,
        OverwriteSkipped,
        FormulaSkipped,
        ColumnUnmapped,
        MissingInTarget,
        MissingInSource,
        DuplicateNameUnresolved,
        DuplicateIdUnresolved
    ];
}

public sealed record ScoreIssue(
    string Category,
    string StudentName,
    string Message,
    string? StudentId = null,
    string? LeftLabel = null,
    string? LeftValue = null,
    string? RightLabel = null,
    string? RightValue = null)
{
    public override string ToString()
    {
        return string.IsNullOrWhiteSpace(StudentName)
            ? $"[{Category}] {Message}"
            : $"[{Category}] {StudentName}：{Message}";
    }
}

public sealed record OverwriteCandidate(
    string Key,
    string StudentName,
    string StudentId,
    string ScoreColumn,
    string CurrentValue,
    string IncomingValue);

public sealed record ProcessRequest(
    string SourcePath,
    string TargetPath,
    string? SourceSheetName,
    string? TargetSheetName);

public sealed record ProcessResult(
    string OutputPath,
    string? BackupPath,
    bool UsedCopy,
    int MatchedStudentCount,
    int WrittenScoreCount,
    int BlankScoreCount,
    IReadOnlyList<ScoreIssue> Issues,
    string? OutputNote = null,
    string SourceFileName = "",
    string TargetFileName = "")
{
    public bool HasIssues => Issues.Count > 0;
}

public sealed class ExcelProcessingException(string message) : Exception(message);
