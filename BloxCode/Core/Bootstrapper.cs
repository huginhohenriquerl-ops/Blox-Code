using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace BloxCode.Core;

public enum BootstrapPhase { Connecting, Checking, Downloading, Installing, Launching, Done }

public sealed record BootstrapProgress(BootstrapPhase Phase, string Status, double? Fraction = null, string? Detail = null);

/// <summary>
/// Keeps the Roblox player up to date and launches it. The flow mirrors what was recovered from
/// Bloxstrap.Bootstrapper: pick a mirror, resolve the channel's latest version, download packages
/// into an MD5-keyed cache, extract them through the package map, apply mods + FastFlags, start.
/// </summary>
public sealed partial class Bootstrapper(HttpClient http, LaunchSettings launch, IProgress<BootstrapProgress> progress)
{
    private const string PlayerExecutable = "RobloxPlayerBeta.exe";
    private const string WebView2ClientKey = @"SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}";
    private const string OriginalsFolder = ".bloxcode-originals";

    // A download that delivers nothing for this long is treated as dead and retried.
    private static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(30);

    private const string AppSettingsXml =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n" +
        "<Settings>\r\n" +
        "\t<ContentFolder>content</ContentFolder>\r\n" +
        "\t<BaseUrl>http://www.roblox.com</BaseUrl>\r\n" +
        "</Settings>\r\n";

    [GeneratedRegex("channel:([a-zA-Z0-9-_]+)", RegexOptions.IgnoreCase)]
    private static partial Regex ChannelArgRegex();

    public string? VersionGuid { get; private set; }

    public async Task RunAsync(CancellationToken ct)
    {
        const string ident = "Bootstrapper::Run";
        Logger.Info(ident, $"Starting (args: '{launch.RobloxArgs}')");

        Report(BootstrapPhase.Connecting, "Connecting to Roblox");
        await Deployment.InitializeConnectivity(http, ct);

        Report(BootstrapPhase.Checking, "Checking for updates");
        var (channel, info) = await ResolveVersion(ct);
        VersionGuid = info.VersionGuid;

        var state = Store.State;
        string versionDir = Path.Combine(Paths.Versions, info.VersionGuid);
        bool upToDate = state.VersionGuid == info.VersionGuid
                        && File.Exists(Path.Combine(versionDir, PlayerExecutable))
                        && !state.ForceReinstall && !launch.Force;

        if (!upToDate)
        {
            await InstallVersion(info.VersionGuid, versionDir, ct);
            state.VersionGuid = info.VersionGuid;
            state.Channel = channel;
            state.ForceReinstall = false;
            Store.SaveState();
        }

        Report(BootstrapPhase.Installing, "Applying your tweaks");
        ApplyModifications(versionDir);
        WriteFastFlags(versionDir);
        Store.SaveState();

        if (!upToDate && Store.Settings.CleanupOldVersions)
            CleanupOldVersions(info.VersionGuid);

        Logger.Info(ident, $"{info.VersionGuid} ready ({(upToDate ? "already up to date" : "freshly installed")})");

        if (launch.NoLaunch)
        {
            Report(BootstrapPhase.Done, "Roblox is up to date", 1);
            return;
        }

        Report(BootstrapPhase.Launching, "Starting Roblox", 1);
        StartRoblox(versionDir);
        Report(BootstrapPhase.Done, "Roblox is running", 1);
    }

    private async Task<(string Channel, ClientVersion Info)> ResolveVersion(CancellationToken ct)
    {
        string channel = launch.Channel ?? ChannelFromArgs() ?? Store.Settings.Channel;
        if (string.IsNullOrWhiteSpace(channel))
            channel = Deployment.DefaultChannel;
        channel = channel.ToLowerInvariant();

        try
        {
            return (channel, await Deployment.GetInfo(http, channel, ct));
        }
        catch (InvalidChannelException ex) when (channel != Deployment.DefaultChannel)
        {
            Logger.Warn("Bootstrapper::ResolveVersion", $"{ex.Message} - resetting channel to {Deployment.DefaultChannel}");
            return (Deployment.DefaultChannel, await Deployment.GetInfo(http, Deployment.DefaultChannel, ct));
        }
    }

    private string? ChannelFromArgs()
    {
        var match = ChannelArgRegex().Match(launch.RobloxArgs);
        return match.Success && match.Groups[1].Value.Length > 0 ? match.Groups[1].Value : null;
    }

    private async Task InstallVersion(string versionGuid, string versionDir, CancellationToken ct)
    {
        const string ident = "Bootstrapper::InstallVersion";
        Report(BootstrapPhase.Checking, "Reading package manifest");

        string manifestText;
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            manifestText = await http.GetStringAsync(Deployment.GetLocation($"/{versionGuid}-rbxPkgManifest.txt"), timeout.Token);
        }
        var packages = PackageManifest.Parse(manifestText)
            .Where(p => PackageManifest.PlayerDirectoryMap.ContainsKey(p.Name))
            .ToList();
        if (!packages.Any(p => p.Name == "RobloxApp.zip"))
            throw new InvalidDataException($"The package manifest for {versionGuid} has no RobloxApp.zip");
        Logger.Info(ident, $"{packages.Count} packages for {versionGuid}");

        if (Directory.Exists(versionDir))
        {
            // Wiping a folder Roblox is running from fails halfway and leaves a broken install.
            if (IsRobloxRunningFrom(versionDir))
                throw new IOException("Roblox is still open from this version. Close Roblox and try again.");
            Directory.Delete(versionDir, recursive: true);
        }
        Directory.CreateDirectory(versionDir);

        long total = Math.Max(1, packages.Sum(p => p.PackedSize)), done = 0;

        foreach (var package in packages)
        {
            ct.ThrowIfCancellationRequested();
            long start = done;
            await DownloadPackage(versionGuid, package, bytes =>
            {
                Report(BootstrapPhase.Downloading, $"Downloading {package.Name}",
                    (double)(start + bytes) / total, $"{Mb(start + bytes)} / {Mb(total)}");
            }, ct);
            done += package.PackedSize;

            Report(BootstrapPhase.Installing, $"Unpacking {package.Name}", (double)done / total, $"{Mb(done)} / {Mb(total)}");
            await Task.Run(() => ExtractPackage(package, versionDir), ct);
        }

        File.WriteAllText(Path.Combine(versionDir, "AppSettings.xml"), AppSettingsXml);
        Store.State.PackageHashes = packages.ToDictionary(p => p.Name, p => p.Signature);
        Store.State.ModManifest.Clear();

        await EnsureWebView2(versionDir, ct);
        CleanupDownloadCache(packages);
    }

    private async Task DownloadPackage(string versionGuid, Package package, Action<long> onBytes, CancellationToken ct)
    {
        const string ident = "Bootstrapper::DownloadPackage";

        if (File.Exists(package.DownloadPath) && Md5Of(package.DownloadPath) == package.Signature)
        {
            Logger.Info(ident, $"{package.Name} found in cache");
            onBytes(package.PackedSize);
            return;
        }

        // Bloxstrap keys its cache by the same MD5, so a verified copy from it saves the download.
        string bloxstrapCopy = Path.Combine(Paths.LocalAppData, "Bloxstrap", "Downloads", package.Signature);
        if (File.Exists(bloxstrapCopy) && Md5Of(bloxstrapCopy) == package.Signature)
        {
            File.Copy(bloxstrapCopy, package.DownloadPath, overwrite: true);
            Logger.Info(ident, $"{package.Name} reused from Bloxstrap's cache");
            onBytes(package.PackedSize);
            return;
        }

        string url = Deployment.GetLocation($"/{versionGuid}-{package.Name}");
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                // HttpClient.Timeout stops counting once headers arrive, so a body that stalls would
                // hang forever; this token is pushed forward on every chunk and fires on silence.
                using var stall = CancellationTokenSource.CreateLinkedTokenSource(ct);
                stall.CancelAfter(StallTimeout);

                using (var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, stall.Token))
                {
                    response.EnsureSuccessStatusCode();
                    await using var source = await response.Content.ReadAsStreamAsync(stall.Token);
                    await using var file = new FileStream(package.DownloadPath, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16, true);

                    var buffer = new byte[1 << 16];
                    long read = 0, reported = 0;
                    int n;
                    while ((n = await source.ReadAsync(buffer, stall.Token)) > 0)
                    {
                        stall.CancelAfter(StallTimeout);
                        await file.WriteAsync(buffer.AsMemory(0, n), ct);
                        read += n;
                        // Every chunk would flood the dispatcher; a quarter megabyte is smooth enough.
                        if (read - reported >= 1 << 18)
                        {
                            onBytes(read);
                            reported = read;
                        }
                    }
                    onBytes(read);
                }

                string hash = Md5Of(package.DownloadPath);
                if (hash != package.Signature)
                    throw new InvalidDataException($"Checksum mismatch for {package.Name}: expected {package.Signature}, got {hash}");

                Logger.Info(ident, $"Downloaded {package.Name}");
                return;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                TryDelete(package.DownloadPath);
                string reason = ex is OperationCanceledException ? $"no data for {StallTimeout.TotalSeconds:0}s" : ex.Message;
                if (attempt >= 3)
                    throw new HttpRequestException($"Couldn't download {package.Name} after 3 attempts ({reason})", ex);

                Logger.Warn(ident, $"Attempt {attempt} for {package.Name} failed: {reason}");
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2), ct);
            }
        }
    }

    private static void ExtractPackage(Package package, string versionDir)
    {
        // Trailing separator so "...\version-ab" can't be matched by a sibling like "...\version-abc".
        string root = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(Path.Combine(versionDir, PackageManifest.PlayerDirectoryMap[package.Name]))) + Path.DirectorySeparatorChar;
        using var zip = ZipFile.OpenRead(package.DownloadPath);

        foreach (var entry in zip.Entries)
        {
            if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
                continue;

            string dest = Path.GetFullPath(Path.Combine(root, entry.FullName.Replace('/', '\\')));
            if (!dest.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Refusing to extract '{entry.FullName}' outside the version folder");

            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            entry.ExtractToFile(dest, overwrite: true);
        }
    }

    private async Task EnsureWebView2(string versionDir, CancellationToken ct)
    {
        const string ident = "Bootstrapper::EnsureWebView2";
        if (IsWebView2Installed())
            return;

        string installer = Path.Combine(versionDir, "WebView2RuntimeInstaller", "MicrosoftEdgeWebview2Setup.exe");
        if (!File.Exists(installer))
        {
            Logger.Warn(ident, "WebView2 runtime missing and this Roblox version ships no installer");
            return;
        }

        Report(BootstrapPhase.Installing, "Installing WebView2 runtime");
        using var process = Process.Start(new ProcessStartInfo(installer, "/silent /install") { UseShellExecute = false });
        if (process is not null)
            await process.WaitForExitAsync(ct);
    }

    private static bool IsWebView2Installed()
    {
        static bool Has(RegistryKey hive, string key)
        {
            using var k = hive.OpenSubKey(key);
            return k?.GetValue("pv") is string pv && pv.Length > 0 && pv != "0.0.0.0";
        }

        return Has(Registry.LocalMachine, WebView2ClientKey.Replace(@"SOFTWARE\", @"SOFTWARE\WOW6432Node\"))
               || Has(Registry.LocalMachine, WebView2ClientKey)
               || Has(Registry.CurrentUser, WebView2ClientKey);
    }

    /// <summary>
    /// Copies everything in the Modifications folder over the version folder. Files that get
    /// overwritten are backed up so removing a mod restores the original.
    /// </summary>
    private static void ApplyModifications(string versionDir)
    {
        const string ident = "Bootstrapper::ApplyModifications";
        var state = Store.State;
        string originals = Path.Combine(versionDir, OriginalsFolder);

        var mods = Directory.Exists(Paths.Modifications)
            ? Directory.EnumerateFiles(Paths.Modifications, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(Paths.Modifications, f))
                .Where(r => !r.Equals("README.txt", StringComparison.OrdinalIgnoreCase))
                .ToList()
            : new List<string>();

        // A mod that can't be written (say, Roblox has the file open) is skipped and retried next
        // launch instead of failing the whole launch. The manifest only records what really happened.
        var applied = new List<string>();

        foreach (string rel in state.ModManifest.Except(mods, StringComparer.OrdinalIgnoreCase).ToList())
        {
            string target = Path.Combine(versionDir, rel), backup = Path.Combine(originals, rel);
            try
            {
                if (File.Exists(backup))
                {
                    File.Copy(backup, target, overwrite: true);
                    File.Delete(backup);
                }
                else if (File.Exists(target))
                {
                    File.Delete(target);
                }
                Logger.Info(ident, $"Removed mod {rel}");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Logger.Warn(ident, $"Couldn't remove mod {rel}, will retry: {ex.Message}");
                applied.Add(rel);
            }
        }

        foreach (string rel in mods)
        {
            string source = Path.Combine(Paths.Modifications, rel);
            string target = Path.Combine(versionDir, rel), backup = Path.Combine(originals, rel);
            try
            {
                if (File.Exists(target) && !File.Exists(backup) && !state.ModManifest.Contains(rel, StringComparer.OrdinalIgnoreCase))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                    File.Copy(target, backup);
                }

                if (!File.Exists(target) || new FileInfo(target).Length != new FileInfo(source).Length || Md5Of(target) != Md5Of(source))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Copy(source, target, overwrite: true);
                    Logger.Info(ident, $"Applied mod {rel}");
                }
                applied.Add(rel);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Logger.Warn(ident, $"Couldn't apply mod {rel}, will retry: {ex.Message}");
                // Keep it tracked if an earlier launch did apply it, so removal still restores the original.
                if (state.ModManifest.Contains(rel, StringComparer.OrdinalIgnoreCase))
                    applied.Add(rel);
            }
        }

        state.ModManifest = applied;
    }

    private static void WriteFastFlags(string versionDir)
    {
        string dir = Path.Combine(versionDir, "ClientSettings");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "ClientAppSettings.json"),
            JsonSerializer.Serialize(Store.Settings.FastFlags, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void StartRoblox(string versionDir)
    {
        var psi = new ProcessStartInfo(Path.Combine(versionDir, PlayerExecutable))
        {
            WorkingDirectory = versionDir,
            UseShellExecute = false,
            Arguments = launch.RobloxArgs,
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Roblox did not start");
        Logger.Info("Bootstrapper::StartRoblox", $"Started Roblox (PID {process.Id})");
    }

    private static bool IsRobloxRunningFrom(string versionDir)
    {
        string dir = Path.TrimEndingDirectorySeparator(Path.GetFullPath(versionDir)) + Path.DirectorySeparatorChar;
        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(PlayerExecutable)))
        {
            using (process)
            {
                try
                {
                    if (process.MainModule?.FileName?.StartsWith(dir, StringComparison.OrdinalIgnoreCase) == true)
                        return true;
                }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
                {
                    // Exited meanwhile, or not ours to inspect.
                }
            }
        }
        return false;
    }

    private static void CleanupOldVersions(string keep)
    {
        foreach (string dir in Directory.EnumerateDirectories(Paths.Versions))
        {
            if (Path.GetFileName(dir).Equals(keep, StringComparison.OrdinalIgnoreCase))
                continue;
            try
            {
                Directory.Delete(dir, recursive: true);
                Logger.Info("Bootstrapper::CleanupOldVersions", $"Deleted {dir}");
            }
            catch (Exception ex)
            {
                Logger.Warn("Bootstrapper::CleanupOldVersions", $"Failed to delete {dir}: {ex.Message}");
            }
        }
    }

    private static void CleanupDownloadCache(List<Package> keep)
    {
        var wanted = keep.Select(p => p.Signature).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (string file in Directory.EnumerateFiles(Paths.Downloads))
            if (!wanted.Contains(Path.GetFileName(file)))
                TryDelete(file);
    }

    private static string Md5Of(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(MD5.HashData(stream));
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { }
    }

    private static string Mb(long bytes) => $"{bytes / 1048576.0:0.0} MB";

    private void Report(BootstrapPhase phase, string status, double? fraction = null, string? detail = null) =>
        progress.Report(new BootstrapProgress(phase, status, fraction, detail));
}
