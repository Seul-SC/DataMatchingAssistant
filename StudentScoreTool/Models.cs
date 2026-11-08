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

public sealed record ScoreIssue(string Category, string StudentName, string Message)
{
    public override string ToString()
    {
        return string.IsNullOrWhiteSpace(StudentName)
            ? $"[{Category}] {Message}"
            : $"[{Category}] {StudentName}：{Message}";
    }
}

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
    IReadOnlyList<ScoreIssue> Issues);

public sealed class ExcelProcessingException(string message) : Exception(message);
