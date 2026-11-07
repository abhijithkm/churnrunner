using Velopack;
using Velopack.Sources;

namespace ChurnRunner;

/// <summary>
/// Updates for copies installed with the installer: looks for a newer GitHub release,
/// downloads it in the background, and installs it when the app restarts. A copy run
/// straight from the zip or publish folder is not installed and is never updated here.
/// </summary>
internal sealed class AppUpdater
{
    public const string RepoUrl = "https://github.com/abhijithkm/churnrunner";

    private readonly UpdateManager _manager = new(new GithubSource(RepoUrl, null, false));
    private UpdateInfo? _ready;

    public bool IsInstalled => _manager.IsInstalled;

    /// <returns>The version that is downloaded and ready to install, or null when already up to date.</returns>
    public async Task<string?> CheckAndDownloadAsync()
    {
        if (!_manager.IsInstalled) return null;

        var info = await _manager.CheckForUpdatesAsync();
        if (info is null) return null;

        await _manager.DownloadUpdatesAsync(info);
        _ready = info;
        return info.TargetFullRelease.Version.ToString();
    }

    public bool HasUpdateReady => _ready is not null;

    /// <summary>Closes the app, installs the downloaded version and starts it again.</summary>
    public void ApplyAndRestart()
    {
        if (_ready is not null) _manager.ApplyUpdatesAndRestart(_ready.TargetFullRelease);
    }
}
