using System.Text.Json;
using System.Text.Json.Serialization;

namespace StudentScoreTool.Services;

/// <summary>
/// Gitee 上 update/version.json 的内容。
/// 必填：version、url；选填：notes、sha256、size、fallbackUrl。
/// </summary>
public sealed class UpdateManifest
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    /// <summary>Inno 安装包的下载地址（Gitee Release 附件）。</summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    /// <summary>可选的备用下载地址，例如 GitHub Release 上同一个安装包。</summary>
    [JsonPropertyName("fallbackUrl")]
    public string? FallbackUrl { get; set; }

    [JsonPropertyName("sha256")]
    public string? Sha256 { get; set; }

    [JsonPropertyName("size")]
    public long? Size { get; set; }

    /// <summary>解析并校验 version.json。格式不对时抛出 <see cref="FormatException"/>。</summary>
    public static UpdateManifest Parse(string json)
    {
        UpdateManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<UpdateManifest>(json.TrimStart('\uFEFF'), JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new FormatException(UpdateMessages.InvalidManifest, ex);
        }

        if (manifest is null
            || !TryParseVersion(manifest.Version, out _)
            || !IsHttpUrl(manifest.Url))
        {
            throw new FormatException(UpdateMessages.InvalidManifest);
        }

        if (!string.IsNullOrWhiteSpace(manifest.FallbackUrl) && !IsHttpUrl(manifest.FallbackUrl))
        {
            manifest.FallbackUrl = null;
        }

        if (manifest.Size is <= 0)
        {
            manifest.Size = null;
        }

        manifest.Sha256 = string.IsNullOrWhiteSpace(manifest.Sha256) ? null : manifest.Sha256.Trim();
        return manifest;
    }

    /// <summary>把 "1.3.0"、"v1.3.0"、"1.3" 等转换成三段版本号。</summary>
    public static bool TryParseVersion(string? text, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        if (trimmed.StartsWith('v') || trimmed.StartsWith('V'))
        {
            trimmed = trimmed[1..];
        }

        var dash = trimmed.IndexOfAny(['-', '+']);
        if (dash >= 0)
        {
            trimmed = trimmed[..dash];
        }

        if (!System.Version.TryParse(trimmed.Contains('.') ? trimmed : trimmed + ".0", out var parsed))
        {
            return false;
        }

        version = new Version(parsed.Major, parsed.Minor, Math.Max(parsed.Build, 0));
        return true;
    }

    /// <summary>远端版本是否比当前版本新；<paramref name="allowSame"/> 为 true 时相同版本也算。</summary>
    public static bool IsNewer(string remote, string current, bool allowSame = false)
    {
        if (!TryParseVersion(remote, out var remoteVersion) || !TryParseVersion(current, out var currentVersion))
        {
            return false;
        }

        var compare = remoteVersion.CompareTo(currentVersion);
        return compare > 0 || (allowSame && compare == 0);
    }

    private static bool IsHttpUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
}
