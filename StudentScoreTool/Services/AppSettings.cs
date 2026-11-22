using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using StudentScoreTool.Themes;

namespace StudentScoreTool.Services;

public sealed class AppSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new AppThemeJsonConverter() }
    };

    public AppTheme Theme { get; set; } = AppTheme.Light;

    public string? LastSourcePath { get; set; }

    public string? LastTargetPath { get; set; }

    /// <summary>
    /// 过渡包（给旧版 Velopack 用户）是否已经自动弹过一次「改用新安装方式」的提示。
    /// </summary>
    public bool MigrationPromptShown { get; set; }

    /// <summary>
    /// 1.3.0 起设置保存在 %AppData%\DataMatchingAssistant\settings.json，与安装目录无关，
    /// 卸载旧版或换安装目录都不会丢失。
    /// </summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        AppInfo.PackId,
        "settings.json");

    /// <summary>
    /// 1.1.0 / 1.2.0 的设置位置。按当前用户安装的 Velopack 版本也装在这个文件夹里，
    /// 卸载旧版时整个文件夹会被删除，所以启动时要先把设置搬到 <see cref="DefaultPath"/>。
    /// </summary>
    public static string LegacyPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppInfo.PackId,
        "settings.json");

    public static AppSettings Load(string? path = null)
    {
        if (path is null)
        {
            path = DefaultPath;
            MigrateLegacyFile(LegacyPath, path);
        }

        try
        {
            if (!File.Exists(path))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(path);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            return settings ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    /// <summary>新位置还没有设置文件、旧位置有时，把旧文件复制过去。失败时静默忽略。</summary>
    public static bool MigrateLegacyFile(string legacyPath, string newPath)
    {
        try
        {
            if (File.Exists(newPath) || !File.Exists(legacyPath))
            {
                return false;
            }

            var directory = Path.GetDirectoryName(newPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.Copy(legacyPath, newPath, overwrite: false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Save(string? path = null)
    {
        path ??= DefaultPath;
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
    }
}

/// <summary>
/// 读取主题枚举时，把已移除的「深薄荷」(MintDeep) 以及未知值安全回退为浅色，避免整份设置文件失败。
/// </summary>
public sealed class AppThemeJsonConverter : JsonConverter<AppTheme>
{
    public override AppTheme Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number
            && reader.TryGetInt32(out var numeric)
            && Enum.IsDefined((AppTheme)numeric))
        {
            return (AppTheme)numeric;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            return AppThemeNames.ParseOrFallback(reader.GetString());
        }

        return AppTheme.Light;
    }

    public override void Write(Utf8JsonWriter writer, AppTheme value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
