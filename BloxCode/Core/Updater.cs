using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace BloxCode.Core;

public sealed record UpdateInfo(Version Version, string Tag, string DownloadUrl, long Size, string? Sha256, string? NotesUrl);

/// <summary>
/// Keeps Blox Code itself current, following the flow recovered from Bloxstrap: ask GitHub for the
/// latest release, download its exe into Temp\Updates, start it with -upgrade, and let that new
/// process copy itself over the installed exe once the old one has exited.
/// </summary>
public static class Updater
{
    private const string MutexName = "BloxCode-AutoUpdater";

    /// <summary>GitHub "owner/repo" baked in at build time (see UpdateRepository in BloxCode.csproj).</summary>
    public static string? Repository { get; } = typeof(Updater).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(a => a.Key == "UpdateRepository")?.Value is { Length: > 0 } repo ? repo : null;

    /// <summary>The release feed; BLOXCODE_UPDATE_FEED overrides it for testing against a local server.</summary>
    public static string? FeedUrl =>
        Environment.GetEnvironmentVariable("BLOXCODE_UPDATE_FEED") is { Length: > 0 } feed ? feed
        : Repository is not null ? $"https://api.github.com/repos/{Repository}/releases/latest"
        : null;

    public static bool IsConfigured => FeedUrl is not null;

    public static Version Current { get; } = Trim(typeof(Updater).Assembly.GetName().Version ?? new Version(1, 0, 0));

    public static string UpdatesFolder => Path.Combine(Path.GetTempPath(), "BloxCode", "Updates");

    /// <summary>Returns the newer release, or null when already current.</summary>
    public static async Task<UpdateInfo?> CheckAsync(HttpClient http, CancellationToken ct)
    {
        const string ident = "Updater::Check";
        if (FeedUrl is not { } feed)
            return null;

        using var request = new HttpRequestMessage(HttpMethod.Get, feed);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;
        string tag = root.GetProperty("tag_name").GetString() ?? "";

        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var latest))
        {
            Logger.Warn(ident, $"Ignoring release with unparseable tag '{tag}'");
            return null;
        }
        latest = Trim(latest);
        if (latest <= Current)
        {
            Logger.Info(ident, $"Up to date ({Current}, latest {latest})");
            return null;
        }

        var assets = root.GetProperty("assets").EnumerateArray().ToList();
        var asset = assets.FirstOrDefault(a => a.GetProperty("name").GetString()?.Equals("BloxCode.exe", StringComparison.OrdinalIgnoreCase) == true);
        if (asset.ValueKind == JsonValueKind.Undefined)
            asset = assets.FirstOrDefault(a => a.GetProperty("name").GetString()?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true);
        if (asset.ValueKind == JsonValueKind.Undefined)
        {
            Logger.Warn(ident, $"Release {tag} has no .exe asset");
            return null;
        }

        // GitHub publishes "sha256:<hex>" digests for release assets; verify when present.
        string? sha256 = asset.TryGetProperty("digest", out var digest) && digest.GetString() is { } d && d.StartsWith("sha256:")
            ? d["sha256:".Length..].ToLowerInvariant()
            : null;

        var info = new UpdateInfo(latest, tag,
            asset.GetProperty("browser_download_url").GetString()!,
            asset.TryGetProperty("size", out var size) ? size.GetInt64() : 0,
            sha256,
            root.TryGetProperty("html_url", out var html) ? html.GetString() : null);

        Logger.Info(ident, $"Update available: {Current} -> {latest}");
        return info;
    }

    public static async Task<string> DownloadAsync(HttpClient http, UpdateInfo info, IProgress<(long Done, long Total)> progress, CancellationToken ct)
    {
        Directory.CreateDirectory(UpdatesFolder);
        string path = Path.Combine(UpdatesFolder, $"BloxCode-{info.Version}.exe");

        using (var response = await http.GetAsync(info.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            response.EnsureSuccessStatusCode();
            long total = response.Content.Headers.ContentLength ?? info.Size;
            await using var source = await response.Content.ReadAsStreamAsync(ct);
            await using var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16, true);

            var buffer = new byte[1 << 16];
            long read = 0, reported = 0;
            int n;
            while ((n = await source.ReadAsync(buffer, ct)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, n), ct);
                read += n;
                if (read - reported >= 1 << 18)
                {
                    progress.Report((read, total));
                    reported = read;
                }
            }
            progress.Report((read, total));
        }

        if (info.Sha256 is { } expected)
        {
            await using var stream = File.OpenRead(path);
            string actual = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, ct));
            if (actual != expected)
            {
                File.Delete(path);
                throw new InvalidDataException($"The downloaded update failed its checksum (expected {expected}, got {actual}).");
            }
        }

        Logger.Info("Updater::Download", $"Downloaded {info.Tag} to {path}");
        return path;
    }

    /// <summary>Starts the downloaded exe in upgrade mode, forwarding the original launch arguments.</summary>
    public static void LaunchUpgrade(string exe, IEnumerable<string> forwardArgs)
    {
        var psi = new ProcessStartInfo(exe) { UseShellExecute = false };
        psi.ArgumentList.Add("-upgrade");
        foreach (string arg in forwardArgs)
            psi.ArgumentList.Add(arg);
        Process.Start(psi);
    }

    /// <summary>
    /// Runs inside the freshly downloaded exe: replace the installed copy, refresh the install
    /// metadata, then hand over to the installed exe. Returns false if the copy couldn't be made.
    /// </summary>
    public static bool ApplyUpgrade(IEnumerable<string> forwardArgs)
    {
        const string ident = "Updater::ApplyUpgrade";
        using var mutex = new Mutex(false, MutexName);
        bool owned = false;

        try
        {
            try { owned = mutex.WaitOne(TimeSpan.FromSeconds(10)); }
            catch (AbandonedMutexException) { owned = true; }
            if (!owned)
            {
                Logger.Warn(ident, "Failed to update! (Could not obtain the updater mutex)");
                return false;
            }

            // The old process may still be closing; wait for it to let go of the file.
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    File.Copy(Paths.Process, Paths.Application, overwrite: true);
                    break;
                }
                catch (IOException) when (attempt < 10)
                {
                    Logger.Info(ident, "Waiting for write permissions to update");
                    Thread.Sleep(500);
                }
            }

            Installer.RefreshAfterUpgrade();
            Store.State.JustUpdatedTo = Current.ToString();
            Store.SaveState();
            Logger.Info(ident, $"Upgraded installed copy to {Current}");
        }
        catch (Exception ex)
        {
            Logger.Error(ident, ex);
            return false;
        }
        finally
        {
            if (owned)
                mutex.ReleaseMutex();
        }

        var psi = new ProcessStartInfo(Paths.Application) { UseShellExecute = false, WorkingDirectory = Paths.Base };
        psi.ArgumentList.Add("-noupdate");
        foreach (string arg in forwardArgs)
            psi.ArgumentList.Add(arg);
        Process.Start(psi);
        return true;
    }

    /// <summary>Deletes leftover downloads once we're no longer running from that folder.</summary>
    public static void CleanupUpdatesFolder()
    {
        if (!Directory.Exists(UpdatesFolder) || Paths.Process.StartsWith(UpdatesFolder, StringComparison.OrdinalIgnoreCase))
            return;
        foreach (string file in Directory.EnumerateFiles(UpdatesFolder))
        {
            try { File.Delete(file); } catch { }
        }
    }

    private static Version Trim(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));
}
