using Velopack;
using Velopack.Sources;

namespace StudentScoreTool.Services;

public enum UpdateAvailability
{
    UpToDate,
    Available,
    NotInstalled,
    Error
}

public sealed class UpdateCheckResult
{
    public required UpdateAvailability Availability { get; init; }

    public required string Message { get; init; }

    public string? AvailableVersion { get; init; }

    public string? ReleaseNotes { get; init; }

    public UpdateInfo? Update { get; init; }

    public UpdateManager? Manager { get; init; }
}

public sealed class UpdateService
{
    public const string UpdateFeedUrl = AppInfo.UpdateFeedUrl;

    public UpdateManager CreateManager()
    {
        var source = new SimpleWebSource(UpdateFeedUrl);
        return new UpdateManager(source);
    }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        UpdateManager? manager = null;
        try
        {
            manager = CreateManager();
            if (!manager.IsInstalled)
            {
                return new UpdateCheckResult
                {
                    Availability = UpdateAvailability.NotInstalled,
                    Message = UpdateMessages.NotInstalled,
                    Manager = manager
                };
            }

            var update = await manager.CheckForUpdatesAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (update is null)
            {
                return new UpdateCheckResult
                {
                    Availability = UpdateAvailability.UpToDate,
                    Message = UpdateMessages.UpToDate(AppInfo.Version),
                    Manager = manager
                };
            }

            var newVersion = update.TargetFullRelease.Version?.ToString() ?? "新版本";
            var notes = update.TargetFullRelease.NotesMarkdown;
            if (string.IsNullOrWhiteSpace(notes))
            {
                notes = UpdateMessages.NoReleaseNotes;
            }

            return new UpdateCheckResult
            {
                Availability = UpdateAvailability.Available,
                Message = UpdateMessages.UpdateAvailable(newVersion, AppInfo.Version),
                AvailableVersion = newVersion,
                ReleaseNotes = notes,
                Update = update,
                Manager = manager
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new UpdateCheckResult
            {
                Availability = UpdateAvailability.Error,
                Message = UpdateMessages.CheckFailed(ex.Message),
                Manager = manager
            };
        }
    }

    public async Task DownloadAndApplyAsync(
        UpdateManager manager,
        UpdateInfo update,
        Action<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await manager.DownloadUpdatesAsync(update, progress, cancellationToken).ConfigureAwait(false);
        manager.ApplyUpdatesAndRestart(update);
    }
}
