using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace StudentScoreTool.Services;

public enum UpdateAvailability
{
    UpToDate,
    Available,
    Error
}

public sealed class UpdateCheckResult
{
    public required UpdateAvailability Availability { get; init; }

    public required string Message { get; init; }

    public string? AvailableVersion { get; init; }

    public string? ReleaseNotes { get; init; }

    public UpdateManifest? Manifest { get; init; }
}

/// <summary>
/// 1.3.0 起的更新方式：读取 Gitee 上的 version.json，发现新版本后下载 Inno 安装包到临时文件夹，
/// 校验后启动完整安装向导，再由调用方退出软件。安装包会沿用上次选择的安装目录。
/// </summary>
public sealed class UpdateService
{
    private static readonly TimeSpan ManifestTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan DownloadStallTimeout = TimeSpan.FromSeconds(60);

    private static readonly Lazy<HttpClient> SharedClient = new(() =>
    {
        var client = new HttpClient(new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10,
            ConnectTimeout = TimeSpan.FromSeconds(15)
        })
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(AppInfo.PackId, AppInfo.Version));
        return client;
    });

    private readonly IReadOnlyList<string> _manifestUrls;

    public UpdateService()
        : this([AppInfo.UpdateManifestUrl, AppInfo.UpdateManifestFallbackUrl])
    {
    }

    public UpdateService(IReadOnlyList<string> manifestUrls)
    {
        _manifestUrls = manifestUrls;
    }

    private static HttpClient Client => SharedClient.Value;

    /// <summary>
    /// 检查更新。<paramref name="includeSameVersion"/> 用于旧版过渡包：版本号相同也要引导安装 Inno 版本。
    /// </summary>
    public async Task<UpdateCheckResult> CheckForUpdatesAsync(
        bool includeSameVersion = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var manifest = await FetchManifestAsync(cancellationToken).ConfigureAwait(false);
            var current = AppInfo.Version;
            if (!UpdateManifest.IsNewer(manifest.Version, current, includeSameVersion))
            {
                return new UpdateCheckResult
                {
                    Availability = UpdateAvailability.UpToDate,
                    Message = UpdateMessages.UpToDate(current),
                    Manifest = manifest
                };
            }

            var notes = string.IsNullOrWhiteSpace(manifest.Notes)
                ? UpdateMessages.NoReleaseNotes
                : manifest.Notes.Trim();

            return new UpdateCheckResult
            {
                Availability = UpdateAvailability.Available,
                Message = UpdateMessages.UpdateAvailable(manifest.Version, current),
                AvailableVersion = manifest.Version,
                ReleaseNotes = notes,
                Manifest = manifest
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return new UpdateCheckResult
            {
                Availability = UpdateAvailability.Error,
                Message = UpdateMessages.CheckFailed(UpdateMessages.Describe(ex))
            };
        }
    }

    private async Task<UpdateManifest> FetchManifestAsync(CancellationToken cancellationToken)
    {
        Exception? lastError = null;
        foreach (var url in _manifestUrls.Where(u => !string.IsNullOrWhiteSpace(u)))
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(ManifestTimeout);
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
                using var response = await Client.SendAsync(request, timeout.Token).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
                return UpdateManifest.Parse(json);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                lastError = ex;
            }
        }

        throw lastError ?? new InvalidOperationException(UpdateMessages.InvalidManifest);
    }

    /// <summary>下载安装包到 %TEMP%\DataMatchingAssistant\updates，并按 version.json 校验大小和 SHA-256。</summary>
    public async Task<string> DownloadInstallerAsync(
        UpdateManifest manifest,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var directory = Path.Combine(Path.GetTempPath(), AppInfo.PackId, "updates");
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, GetInstallerFileName(manifest));

        var urls = new List<string> { manifest.Url };
        if (!string.IsNullOrWhiteSpace(manifest.FallbackUrl)
            && !string.Equals(manifest.FallbackUrl, manifest.Url, StringComparison.OrdinalIgnoreCase))
        {
            urls.Add(manifest.FallbackUrl);
        }

        Exception? lastError = null;
        foreach (var url in urls)
        {
            try
            {
                await DownloadFileAsync(url, target, manifest.Size, progress, cancellationToken).ConfigureAwait(false);
                VerifyInstaller(target, manifest);
                return target;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                lastError = ex;
                TryDelete(target);
            }
        }

        throw lastError ?? new InvalidOperationException(UpdateMessages.DownloadFailed(""));
    }

    /// <summary>以普通方式（显示完整安装向导）启动安装包，不等待它结束。</summary>
    public static void LaunchInstaller(string installerPath, string? arguments = null)
    {
        var startInfo = new ProcessStartInfo(installerPath)
        {
            UseShellExecute = true,
            Arguments = arguments ?? "",
            WorkingDirectory = Path.GetDirectoryName(installerPath) ?? Path.GetTempPath()
        };

        try
        {
            using var _ = Process.Start(startInfo);
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException(UpdateMessages.LaunchFailed(ex.Message), ex);
        }
    }

    public static string GetInstallerFileName(UpdateManifest manifest)
    {
        var version = UpdateManifest.TryParseVersion(manifest.Version, out var parsed)
            ? parsed.ToString(3)
            : "latest";
        return $"{AppInfo.PackId}-{version}-Setup.exe";
    }

    /// <summary>校验安装包；不通过时抛出 <see cref="InvalidDataException"/>。</summary>
    public static void VerifyInstaller(string path, UpdateManifest manifest)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length == 0)
        {
            throw new InvalidDataException(UpdateMessages.SizeMismatch);
        }

        if (manifest.Size is { } expectedSize && info.Length != expectedSize)
        {
            throw new InvalidDataException(UpdateMessages.SizeMismatch);
        }

        if (manifest.Sha256 is { } expectedHash)
        {
            using var stream = File.OpenRead(path);
            var actual = Convert.ToHexString(SHA256.HashData(stream));
            if (!string.Equals(actual, expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(UpdateMessages.ChecksumMismatch);
            }
        }
    }

    private static async Task DownloadFileAsync(
        string url,
        string target,
        long? expectedSize,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        var partial = target + ".partial";
        TryDelete(partial);

        using var stall = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        stall.CancelAfter(DownloadStallTimeout);
        using var response = await Client
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, stall.Token)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? expectedSize ?? 0;
        await using (var source = await response.Content.ReadAsStreamAsync(stall.Token).ConfigureAwait(false))
        await using (var destination = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            var buffer = new byte[81920];
            long received = 0;
            var lastPercent = -1;
            while (true)
            {
                stall.CancelAfter(DownloadStallTimeout);
                var read = await source.ReadAsync(buffer, stall.Token).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                await destination.WriteAsync(buffer.AsMemory(0, read), stall.Token).ConfigureAwait(false);
                received += read;
                if (total > 0)
                {
                    var percent = (int)Math.Clamp(received * 100 / total, 0, 100);
                    if (percent != lastPercent)
                    {
                        lastPercent = percent;
                        progress?.Report(percent);
                    }
                }
            }
        }

        TryDelete(target);
        File.Move(partial, target);
        progress?.Report(100);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // 临时文件删不掉不影响更新。
        }
    }
}
