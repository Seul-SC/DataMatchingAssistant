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

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppInfo.PackId,
        "settings.json");

    public static AppSettings Load(string? path = null)
    {
        path ??= DefaultPath;
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
