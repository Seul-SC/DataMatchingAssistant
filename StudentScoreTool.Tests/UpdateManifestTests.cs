using System.Security.Cryptography;
using StudentScoreTool.Services;

namespace StudentScoreTool.Tests;

public sealed class UpdateManifestTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"DmaUpdate-{Guid.NewGuid():N}");

    public UpdateManifestTests()
    {
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public void ParseReadsAllFields()
    {
        var manifest = UpdateManifest.Parse("""
            {
              "version": "1.3.0",
              "notes": "改用 Inno 安装包",
              "url": "https://gitee.com/seul-sc/DataMatchingAssistant/releases/download/v1.3.0/DataMatchingAssistant-1.3.0-Setup.exe",
              "fallbackUrl": "https://github.com/Seul-SC/DataMatchingAssistant/releases/download/v1.3.0/DataMatchingAssistant-1.3.0-Setup.exe",
              "sha256": "ABCDEF",
              "size": 1234
            }
            """);

        Assert.Equal("1.3.0", manifest.Version);
        Assert.Equal("改用 Inno 安装包", manifest.Notes);
        Assert.EndsWith("Setup.exe", manifest.Url);
        Assert.NotNull(manifest.FallbackUrl);
        Assert.Equal("ABCDEF", manifest.Sha256);
        Assert.Equal(1234, manifest.Size);
    }

    [Fact]
    public void ParseAcceptsBomAndMissingOptionalFields()
    {
        var manifest = UpdateManifest.Parse("\uFEFF{\"version\":\"v1.4\",\"url\":\"https://example.com/a.exe\",\"sha256\":\"\",\"size\":0}");

        Assert.Null(manifest.Sha256);
        Assert.Null(manifest.Size);
        Assert.Null(manifest.Notes);
        Assert.True(UpdateManifest.IsNewer(manifest.Version, "1.3.0"));
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("{\"version\":\"abc\",\"url\":\"https://example.com/a.exe\"}")]
    [InlineData("{\"version\":\"1.3.0\",\"url\":\"ftp://example.com/a.exe\"}")]
    [InlineData("{\"version\":\"1.3.0\"}")]
    public void ParseRejectsInvalidManifests(string json)
    {
        Assert.Throws<FormatException>(() => UpdateManifest.Parse(json));
    }

    [Theory]
    [InlineData("1.3.0", "1.2.0", false, true)]
    [InlineData("1.3.0", "1.3.0", false, false)]
    [InlineData("1.3.0", "1.3.0", true, true)]
    [InlineData("1.2.9", "1.3.0", true, false)]
    [InlineData("v1.10.0", "1.9.0", false, true)]
    [InlineData("1.3.1-beta", "1.3.0", false, true)]
    [InlineData("garbage", "1.3.0", true, false)]
    public void IsNewerComparesVersions(string remote, string current, bool allowSame, bool expected)
    {
        Assert.Equal(expected, UpdateManifest.IsNewer(remote, current, allowSame));
    }

    [Fact]
    public void VerifyInstallerChecksSizeAndHash()
    {
        var path = Path.Combine(_directory, "setup.exe");
        var bytes = "fake installer"u8.ToArray();
        File.WriteAllBytes(path, bytes);
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        UpdateService.VerifyInstaller(path, new UpdateManifest { Version = "1.3.0", Url = "https://e/x", Sha256 = hash, Size = bytes.Length });
        Assert.Throws<InvalidDataException>(() =>
            UpdateService.VerifyInstaller(path, new UpdateManifest { Version = "1.3.0", Url = "https://e/x", Sha256 = new string('0', 64) }));
        Assert.Throws<InvalidDataException>(() =>
            UpdateService.VerifyInstaller(path, new UpdateManifest { Version = "1.3.0", Url = "https://e/x", Size = 1 }));
    }

    [Fact]
    public void InstallerFileNameUsesVersion()
    {
        Assert.Equal("DataMatchingAssistant-1.3.0-Setup.exe",
            UpdateService.GetInstallerFileName(new UpdateManifest { Version = "v1.3", Url = "https://e/x" }));
    }

    [Fact]
    public void FindVelopackRootRecognisesCurrentFolderNextToUpdateExe()
    {
        var root = Path.Combine(_directory, "DataMatchingAssistant");
        var current = Path.Combine(root, "current");
        Directory.CreateDirectory(current);

        Assert.Null(LegacyInstall.FindVelopackRoot(current + Path.DirectorySeparatorChar));
        File.WriteAllText(Path.Combine(root, "Update.exe"), "");
        Assert.Equal(root, LegacyInstall.FindVelopackRoot(current + Path.DirectorySeparatorChar));
        Assert.Null(LegacyInstall.FindVelopackRoot(root));
        Assert.Null(LegacyInstall.FindVelopackRoot(null));
    }

    [Fact]
    public void InstallerArgumentsNeverEndWithBackslashBeforeQuote()
    {
        Assert.Equal("", LegacyInstall.BuildInstallerArguments(null));
        Assert.Equal("/VELOPACKROOT=\"C:\\Users\\a\\AppData\\Local\\DataMatchingAssistant\"",
            LegacyInstall.BuildInstallerArguments("C:\\Users\\a\\AppData\\Local\\DataMatchingAssistant\\"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }
}
