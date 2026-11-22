using StudentScoreTool.Services;
using StudentScoreTool.Themes;

namespace StudentScoreTool.Tests;

public sealed class AppSettingsTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"DataMatchingAssistantSettings-{Guid.NewGuid():N}");

    public AppSettingsTests()
    {
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public void LoadReturnsDefaultsWhenFileIsMissing()
    {
        var path = Path.Combine(_directory, "missing.json");
        var settings = AppSettings.Load(path);

        Assert.Equal(AppTheme.Light, settings.Theme);
        Assert.Null(settings.LastSourcePath);
        Assert.Null(settings.LastTargetPath);
    }

    [Fact]
    public void SaveAndLoadRoundTripsThemeAndPaths()
    {
        var path = Path.Combine(_directory, "settings.json");
        var original = new AppSettings
        {
            Theme = AppTheme.Ripple,
            LastSourcePath = Path.Combine(_directory, "来源.xlsx"),
            LastTargetPath = Path.Combine(_directory, "名单.xlsx")
        };

        original.Save(path);
        var loaded = AppSettings.Load(path);

        Assert.Equal(AppTheme.Ripple, loaded.Theme);
        Assert.Equal(original.LastSourcePath, loaded.LastSourcePath);
        Assert.Equal(original.LastTargetPath, loaded.LastTargetPath);
    }

    [Fact]
    public void LoadMapsLegacyMintDeepToLightAndKeepsPaths()
    {
        var path = Path.Combine(_directory, "legacy.json");
        var source = Path.Combine(_directory, "来源.xlsx");
        var target = Path.Combine(_directory, "名单.xlsx");
        File.WriteAllText(path,
            "{\n  \"theme\": \"MintDeep\",\n  \"lastSourcePath\": "
            + System.Text.Json.JsonSerializer.Serialize(source)
            + ",\n  \"lastTargetPath\": "
            + System.Text.Json.JsonSerializer.Serialize(target)
            + "\n}");

        var loaded = AppSettings.Load(path);

        Assert.Equal(AppTheme.Light, loaded.Theme);
        Assert.Equal(source, loaded.LastSourcePath);
        Assert.Equal(target, loaded.LastTargetPath);
    }

    [Fact]
    public void LoadReturnsDefaultsWhenFileIsInvalid()
    {
        var path = Path.Combine(_directory, "broken.json");
        File.WriteAllText(path, "{ not-json");

        var settings = AppSettings.Load(path);

        Assert.Equal(AppTheme.Light, settings.Theme);
        Assert.Null(settings.LastSourcePath);
    }

    [Fact]
    public void DefaultPathLivesUnderLocalAppDataAndNotTheInstallFolder()
    {
        var expectedRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DataMatchingAssistant");

        Assert.True(AppSettings.DefaultPath.StartsWith(expectedRoot, StringComparison.OrdinalIgnoreCase));
        Assert.Equal("settings.json", Path.GetFileName(AppSettings.DefaultPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }
}
