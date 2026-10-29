using System.IO.Compression;
using System.Text;
using BloxCode.Core;
using BloxCode.Linux.Runtime;

namespace BloxCode.Linux.Platform;

/// <summary>
/// "Export diagnostics": one zip with versions, the Sober setup and recent logs, to send when
/// something goes wrong on a machine nobody else can look at. Holds no cookies or Roblox account data.
/// </summary>
public static class Diagnostics
{
    public static async Task<string> ReportAsync(CancellationToken ct = default)
    {
        var report = new StringBuilder();
        void Line(string key, string? value) => report.Append(key).Append(": ").AppendLine(value ?? "(none)");

        Line("Blox Code", $"{Updater.Current} ({(Paths.IsAppImage ? "AppImage" : "not an AppImage")}, {(DesktopIntegration.IsInstalled ? "installed" : "not installed")})");
        Line("Running from", Paths.Process);
        Line("Distro", $"{Distro.Current.Name} (id {Distro.Current.Id}, like {string.Join(' ', Distro.Current.IdLike)}, family {Distro.Current.Family})");
        Line("Session", Distro.Session);
        Line("Kernel", ReadFirstLine("/proc/version"));
        Line("Link handler", await SafeAsync(DesktopIntegration.CurrentHandlerAsync()));

        if (Sober.Flatpak is { } flatpak)
        {
            Line("Flatpak", (await Proc.TryRunAsync(flatpak.File, flatpak.Prefix.Append("--version"), TimeSpan.FromSeconds(10), ct))?.Output.Trim());
            var status = await Sober.DetectAsync(ct);
            Line("Sober", $"{status.State} {status.Version} {status.Installation}".Trim());
            Line("Remotes", (await Proc.TryRunAsync(flatpak.File, flatpak.Prefix.Concat(new[] { "remotes", "--columns=name,options" }), TimeSpan.FromSeconds(10), ct))?.Output.Trim().Replace('\n', ';'));
            Line("NVIDIA GL extensions", string.Join(", ", await Sober.NvidiaGlExtensionsAsync(ct)));
        }
        else
        {
            Line("Flatpak", "not found on PATH");
        }

        Line("NVIDIA driver", Sober.NvidiaDriver());
        Line("NVIDIA check", await Sober.CheckNvidiaAsync(ct) ?? "ok");
        Line("Sober running", Sober.IsRunning.ToString());
        Line("Fast Flags", Store.Settings.FastFlags.Count.ToString());
        Line("Mods", $"{ModSync.ListMods().Count} in Modifications, {Store.State.ModManifest.Count} in Sober's overlay");
        Line("Roblox APK", ApkAssets.FindApk());
        return report.ToString();
    }

    /// <summary>Writes the zip into the home folder and returns its path.</summary>
    public static async Task<string> ExportAsync(CancellationToken ct = default)
    {
        string path = Path.Combine(Paths.Home, $"BloxCode-diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
        string report = await ReportAsync(ct);

        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry("report.txt");
            await using (var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false)))
                await writer.WriteAsync(report);

            AddFile(zip, Paths.SettingsFile, "bloxcode/Settings.json");
            AddFile(zip, Paths.StateFile, "bloxcode/State.json");
            AddFile(zip, Sober.ConfigFile, "sober/config.json");
            AddFile(zip, Sober.SoberLog, "logs/sober.log");
            AddFile(zip, "/etc/os-release", "system/os-release");

            if (Directory.Exists(Paths.Logs))
            {
                foreach (string log in Directory.EnumerateFiles(Paths.Logs, "*.log")
                             .Where(f => !f.EndsWith("sober.log", StringComparison.Ordinal))
                             .OrderByDescending(File.GetLastWriteTimeUtc).Take(10))
                    AddFile(zip, log, "logs/" + Path.GetFileName(log));
            }
        }

        Logger.Info("Diagnostics::Export", path);
        return path;
    }

    private static void AddFile(ZipArchive zip, string path, string name)
    {
        try
        {
            if (!File.Exists(path))
                return;
            // Logs are still open for writing; share the file instead of failing on it.
            using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var target = zip.CreateEntry(name).Open();
            source.CopyTo(target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.Warn("Diagnostics::AddFile", $"{path}: {ex.Message}");
        }
    }

    private static string? ReadFirstLine(string path)
    {
        try { return File.Exists(path) ? File.ReadLines(path).FirstOrDefault()?.Trim() : null; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    private static async Task<string?> SafeAsync(Task<string?> task)
    {
        try { return await task; }
        catch (Exception ex) { return $"error: {ex.Message}"; }
    }
}
