using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using ExcelDataReader;

namespace StudentScoreTool;

public static class SpreadsheetLoader
{
    public static readonly string[] SupportedExtensions = [".xlsx", ".xls", ".csv"];

    public const string OpenFileFilter =
        "表格文件 (*.xlsx;*.xls;*.csv)|*.xlsx;*.xls;*.csv|Excel 工作簿 (*.xlsx)|*.xlsx|Excel 97-2003 (*.xls)|*.xls|CSV 文件 (*.csv)|*.csv|所有文件 (*.*)|*.*";

    static SpreadsheetLoader()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static bool IsSupportedPath(string path)
    {
        return SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
    }

    public static XLWorkbook OpenAsWorkbook(string path)
    {
        var extension = Path.GetExtension(path);
        if (extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return OpenXlsx(path);
        }

        if (extension.Equals(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return OpenCsv(path);
        }

        if (extension.Equals(".xls", StringComparison.OrdinalIgnoreCase))
        {
            return OpenXlsOrFallback(path);
        }

        throw new ExcelProcessingException($"不支持的文件格式：{extension}。请使用 .xlsx、.xls 或 .csv。");
    }

    private static XLWorkbook OpenXlsx(string path)
    {
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var memory = new MemoryStream();
        input.CopyTo(memory);
        memory.Position = 0;
        return new XLWorkbook(memory);
    }

    private static XLWorkbook OpenXlsOrFallback(string path)
    {
        var bytes = ReadAllBytesShared(path);
        if (bytes.Length >= 2 && bytes[0] == 0x50 && bytes[1] == 0x4B)
        {
            try
            {
                using var memory = new MemoryStream(bytes, writable: false);
                return new XLWorkbook(memory);
            }
            catch (Exception ex)
            {
                throw new ExcelProcessingException($"无法读取看起来像 .xlsx 的文件：{ex.Message}");
            }
        }

        if (LooksLikeBinaryExcel(bytes))
        {
            try
            {
                return OpenWithExcelDataReader(bytes);
            }
            catch (Exception ex)
            {
                throw new ExcelProcessingException($"无法读取 .xls 文件：{ex.Message}");
            }
        }

        var text = DecodeText(bytes);
        if (LooksLikeHtml(text))
        {
            return OpenHtmlTables(text, SuggestSheetName(path));
        }

        return OpenDelimitedText(text, SuggestSheetName(path));
    }

    private static XLWorkbook OpenCsv(string path)
    {
        var text = DecodeText(ReadAllBytesShared(path));
        return OpenDelimitedText(text, SuggestSheetName(path));
    }

    private static XLWorkbook OpenWithExcelDataReader(byte[] bytes)
    {
        using var memory = new MemoryStream(bytes, writable: false);
        using var reader = ExcelReaderFactory.CreateReader(memory);
        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration
            {
                UseHeaderRow = false
            }
        });

        return DataSetToWorkbook(dataSet);
    }

    private static XLWorkbook DataSetToWorkbook(DataSet dataSet)
    {
        var workbook = new XLWorkbook();
        var added = 0;
        foreach (DataTable table in dataSet.Tables)
        {
            if (table.Rows.Count == 0 && table.Columns.Count == 0)
            {
                continue;
            }

            var sheet = workbook.AddWorksheet(UniqueSheetName(workbook, table.TableName));
            for (var row = 0; row < table.Rows.Count; row++)
            {
                for (var column = 0; column < table.Columns.Count; column++)
                {
                    SetConvertedCell(sheet.Cell(row + 1, column + 1), table.Rows[row][column]);
                }
            }

            added++;
        }

        if (added == 0)
        {
            workbook.Dispose();
            throw new ExcelProcessingException("文件里没有可读的工作表。");
        }

        return workbook;
    }

    private static XLWorkbook OpenHtmlTables(string text, string fallbackName)
    {
        var tables = ExtractHtmlTables(text);
        if (tables.Count == 0)
        {
            throw new ExcelProcessingException("这个 .xls 文件看起来是网页表格，但没有找到可用的表格。");
        }

        var workbook = new XLWorkbook();
        for (var index = 0; index < tables.Count; index++)
        {
            var name = tables.Count == 1 ? fallbackName : $"{fallbackName}{index + 1}";
            var sheet = workbook.AddWorksheet(UniqueSheetName(workbook, name));
            var rows = tables[index];
            for (var row = 0; row < rows.Count; row++)
            {
                for (var column = 0; column < rows[row].Count; column++)
                {
                    SetConvertedCell(sheet.Cell(row + 1, column + 1), rows[row][column]);
                }
            }
        }

        return workbook;
    }

    private static XLWorkbook OpenDelimitedText(string text, string sheetName)
    {
        var rows = ParseDelimitedText(text);
        if (rows.Count == 0)
        {
            throw new ExcelProcessingException("这个表格文件是空的。");
        }

        var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet(UniqueSheetName(workbook, sheetName));
        for (var row = 0; row < rows.Count; row++)
        {
            for (var column = 0; column < rows[row].Count; column++)
            {
                SetConvertedCell(sheet.Cell(row + 1, column + 1), rows[row][column]);
            }
        }

        return workbook;
    }

    private static void SetConvertedCell(IXLCell cell, object? value)
    {
        if (value is null or DBNull)
        {
            return;
        }

        switch (value)
        {
            case double number:
                cell.Value = number;
                return;
            case float number:
                cell.Value = number;
                return;
            case decimal number:
                cell.Value = number;
                return;
            case int number:
                cell.Value = number;
                return;
            case long number:
                cell.Value = number;
                return;
            case short number:
                cell.Value = number;
                return;
            case byte number:
                cell.Value = number;
                return;
            case bool flag:
                cell.Value = flag;
                return;
            case DateTime date:
                cell.Value = date;
                return;
            default:
                SetTextOrNumber(cell, Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
                return;
        }
    }

    private static void SetTextOrNumber(IXLCell cell, string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return;
        }

        if (LooksLikeLeadingZeroId(trimmed))
        {
            cell.Style.NumberFormat.SetFormat("@");
            cell.Value = trimmed;
            return;
        }

        if (TextNormalization.TryParseScoreNumber(trimmed, out var number)
            && !trimmed.Contains('/')
            && !trimmed.Contains('-'))
        {
            cell.Value = number;
            return;
        }

        cell.Value = trimmed;
    }

    private static bool LooksLikeLeadingZeroId(string text)
    {
        if (text.Length <= 1 || !text.StartsWith('0'))
        {
            return false;
        }

        return text.All(char.IsDigit);
    }

    internal static List<IReadOnlyList<string>> ParseDelimitedText(string text)
    {
        text = text.TrimStart('\uFEFF');
        var lines = SplitLines(text);
        if (lines.Count == 0)
        {
            return [];
        }

        var delimiter = DetectDelimiter(lines);
        var rows = new List<IReadOnlyList<string>>();
        foreach (var line in lines)
        {
            if (line.Length == 0 && rows.Count == 0)
            {
                continue;
            }

            rows.Add(ParseDelimitedLine(line, delimiter));
        }

        while (rows.Count > 0 && rows[^1].All(string.IsNullOrWhiteSpace))
        {
            rows.RemoveAt(rows.Count - 1);
        }

        return rows;
    }

    private static List<string> SplitLines(string text)
    {
        var lines = new List<string>();
        var builder = new StringBuilder();
        var inQuotes = false;
        for (var index = 0; index < text.Length; index++)
        {
            var ch = text[index];
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                builder.Append(ch);
                continue;
            }

            if (!inQuotes && ch is '\r' or '\n')
            {
                if (ch == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                {
                    index++;
                }

                lines.Add(builder.ToString());
                builder.Clear();
                continue;
            }

            builder.Append(ch);
        }

        if (builder.Length > 0)
        {
            lines.Add(builder.ToString());
        }

        return lines;
    }

    private static char DetectDelimiter(IReadOnlyList<string> lines)
    {
        var candidates = new[] { ',', '\t', ';', '，' };
        var sample = lines.Take(8).ToList();
        var best = ',';
        var bestScore = -1;
        foreach (var delimiter in candidates)
        {
            var counts = sample
                .Select(line => ParseDelimitedLine(line, delimiter).Count)
                .Where(count => count > 1)
                .ToList();
            if (counts.Count == 0)
            {
                continue;
            }

            var score = counts.Count * 100 + counts.Max() - (counts.Max() - counts.Min());
            if (score > bestScore)
            {
                bestScore = score;
                best = delimiter;
            }
        }

        return best;
    }

    private static List<string> ParseDelimitedLine(string line, char delimiter)
    {
        var fields = new List<string>();
        var builder = new StringBuilder();
        var inQuotes = false;
        for (var index = 0; index < line.Length; index++)
        {
            var ch = line[index];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (index + 1 < line.Length && line[index + 1] == '"')
                    {
                        builder.Append('"');
                        index++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    builder.Append(ch);
                }

                continue;
            }

            if (ch == '"')
            {
                inQuotes = true;
                continue;
            }

            if (ch == delimiter)
            {
                fields.Add(builder.ToString());
                builder.Clear();
                continue;
            }

            builder.Append(ch);
        }

        fields.Add(builder.ToString());
        return fields;
    }

    private static List<List<List<string>>> ExtractHtmlTables(string html)
    {
        var tables = new List<List<List<string>>>();
        var tableMatches = Regex.Matches(
            html,
            @"<table\b[^>]*>(.*?)</table>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        foreach (Match tableMatch in tableMatches)
        {
            var rows = new List<List<string>>();
            var rowMatches = Regex.Matches(
                tableMatch.Groups[1].Value,
                @"<tr\b[^>]*>(.*?)</tr>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            foreach (Match rowMatch in rowMatches)
            {
                var cells = Regex.Matches(
                    rowMatch.Groups[1].Value,
                    @"<(td|th)\b[^>]*>(.*?)</\1>",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (cells.Count == 0)
                {
                    continue;
                }

                rows.Add(cells.Cast<Match>().Select(cell => DecodeHtmlText(cell.Groups[2].Value)).ToList());
            }

            if (rows.Count > 0)
            {
                tables.Add(rows);
            }
        }

        return tables;
    }

    private static string DecodeHtmlText(string value)
    {
        var text = Regex.Replace(value, @"<(br|BR)\s*/?>", " ", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<[^>]+>", string.Empty);
        text = System.Net.WebUtility.HtmlDecode(text);
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    private static bool LooksLikeBinaryExcel(byte[] bytes)
    {
        return bytes.Length >= 8
            && bytes[0] == 0xD0
            && bytes[1] == 0xCF
            && bytes[2] == 0x11
            && bytes[3] == 0xE0;
    }

    private static bool LooksLikeHtml(string text)
    {
        var head = text.TrimStart().ToLowerInvariant();
        return head.StartsWith("<html")
            || head.StartsWith("<!doctype")
            || head.StartsWith("<table")
            || head.Contains("<table");
    }

    private static byte[] ReadAllBytesShared(string path)
    {
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var memory = new MemoryStream();
        input.CopyTo(memory);
        return memory.ToArray();
    }

    private static string DecodeText(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }

        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        try
        {
            return utf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.GetEncoding("GB18030").GetString(bytes);
        }
    }

    private static string SuggestSheetName(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return string.IsNullOrWhiteSpace(name) ? "Sheet1" : name;
    }

    private static string UniqueSheetName(XLWorkbook workbook, string? rawName)
    {
        var name = SanitizeSheetName(rawName);
        var candidate = name;
        var index = 2;
        while (workbook.Worksheets.Any(sheet => sheet.Name == candidate))
        {
            var suffix = index.ToString(CultureInfo.InvariantCulture);
            var trimmed = name.Length + suffix.Length > 31
                ? name[..Math.Max(1, 31 - suffix.Length)]
                : name;
            candidate = trimmed + suffix;
            index++;
        }

        return candidate;
    }

    private static string SanitizeSheetName(string? rawName)
    {
        var name = string.IsNullOrWhiteSpace(rawName) ? "Sheet1" : rawName.Trim();
        foreach (var ch in new[] { ':', '\\', '/', '?', '*', '[', ']' })
        {
            name = name.Replace(ch, '_');
        }

        if (name.Length > 31)
        {
            name = name[..31];
        }

        return name.Length == 0 ? "Sheet1" : name;
    }
}
