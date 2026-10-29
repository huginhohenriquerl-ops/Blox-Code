using System.Diagnostics;
using System.Text.RegularExpressions;
using BloxCode.Core;
using BloxCode.Linux.Platform;

namespace BloxCode.Linux.Runtime;

public enum SoberState { NoFlatpak, NotInstalled, Installed }

public sealed record SoberStatus(SoberState State, string? Version = null, string? Installation = null);

/// <summary>Sober refused to start, with the end of its output for the log and the error message.</summary>
public sealed class SoberLaunchException(string message, string? output = null) : Exception(message)
{
    public string? Output { get; } = output;
}

/// <summary>
/// Sober (org.vinegarhq.Sober, from Flathub) runs the Android build of Roblox natively. It owns
/// downloading and updating Roblox; Blox Code finds it, installs it on request, and starts it.
/// </summary>
public static partial class Sober
{
    public const string AppId = "org.vinegarhq.Sober";
    public const string InstallCommand = "flatpak install --user flathub " + AppId;

    public static string AppDir => Path.Combine(Paths.Home, ".var", "app", AppId);
    public static string ConfigFile => Path.Combine(AppDir, "config", "sober", "config.json");
    public static string DataDir => Path.Combine(AppDir, "data", "sober");
    public static string OverlayDir => Path.Combine(DataDir, "asset_overlay");
    public static string PackagesDir => Path.Combine(DataDir, "packages");

    /// <summary>Sober's own launcher entry, which uninstalling hands roblox:// links back to.</summary>
    public const string DesktopId = AppId + ".desktop";

    public static string SoberLog => Path.Combine(Paths.Logs, "sober.log");

    private static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(20);

    /// <summary>
    /// The flatpak program, plus any leading arguments. BLOXCODE_FLATPAK overrides it for tests
    /// ("program" or "program|first-arg", e.g. a fake written as a node script).
    /// </summary>
    public static (string File, string[] Prefix)? Flatpak
    {
        get
        {
            if (Environment.GetEnvironmentVariable("BLOXCODE_FLATPAK") is { Length: > 0 } custom)
            {
                string[] parts = custom.Split('|');
                return (parts[0], parts[1..]);
            }
            return Proc.Which("flatpak") is { } path ? (path, Array.Empty<string>()) : null;
        }
    }

    public static Task<ProcResult> FlatpakAsync(TimeSpan timeout, CancellationToken ct, Action<string>? onLine, params string[] args)
    {
        var (file, prefix) = Flatpak ?? throw new FileNotFoundException("Flatpak isn't installed.");
        return Proc.RunAsync(file, prefix.Concat(args), timeout, ct, onLine);
    }

    public static async Task<SoberStatus> DetectAsync(CancellationToken ct = default)
    {
        if (Flatpak is null)
            return new SoberStatus(SoberState.NoFlatpak);

        ProcResult info;
        try
        {
            info = await FlatpakAsync(QueryTimeout, ct, null, "info", AppId);
        }
        catch (FileNotFoundException)
        {
            return new SoberStatus(SoberState.NoFlatpak);
        }

        if (!info.Ok)
            return new SoberStatus(SoberState.NotInstalled);

        var fields = ParseInfo(info.Output);
        return new SoberStatus(SoberState.Installed, fields.GetValueOrDefault("Version"), fields.GetValueOrDefault("Installation"));
    }

    /// <summary>"Key: value" lines from `flatpak info` (run with LC_ALL=C, so the keys are English).</summary>
    public static Dictionary<string, string> ParseInfo(string output)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string line in output.Split('\n'))
        {
            int colon = line.IndexOf(':');
            if (colon > 0 && colon < line.Length - 1)
                fields.TryAdd(line[..colon].Trim(), line[(colon + 1)..].Trim());
        }
        return fields;
    }

    /// <summary>
    /// Installs Sober for the current user from Flathub (no sudo): adds the user Flathub remote if
    /// needed, then installs, reporting flatpak's percentages as progress.
    /// </summary>
    public static async Task InstallAsync(IProgress<BootstrapProgress> progress, CancellationToken ct)
    {
        const string ident = "Sober::Install";
        progress.Report(new BootstrapProgress(BootstrapPhase.Connecting, "Adding Flathub"));
        var remote = await FlatpakAsync(TimeSpan.FromMinutes(2), ct, null,
            "remote-add", "--user", "--if-not-exists", "flathub", "https://dl.flathub.org/repo/flathub.flatpakrepo");
        if (!remote.Ok)
            throw new InvalidOperationException($"Couldn't add Flathub: {remote.Problem}");

        progress.Report(new BootstrapProgress(BootstrapPhase.Downloading, "Installing Sober from Flathub"));
        double? lastFraction = null;
        var install = await FlatpakAsync(TimeSpan.FromMinutes(45), ct, line =>
        {
            if (line.Trim().Length == 0)
                return;
            Logger.Info(ident, line);
            if (ParseProgress(line) is { } p)
            {
                lastFraction = p.Fraction ?? lastFraction;
                progress.Report(p with { Fraction = lastFraction });
            }
            else
            {
                // Without a terminal flatpak may print plain lines; show them so it's visibly working.
                string text = line.Trim();
                progress.Report(new BootstrapProgress(BootstrapPhase.Downloading, "Installing Sober from Flathub", lastFraction,
                    text.Length > 70 ? text[..70] + "…" : text));
            }
        }, "install", "--user", "--noninteractive", "-y", "flathub", AppId);

        if (!install.Ok)
            throw new InvalidOperationException($"flatpak couldn't install Sober: {install.Problem}");
        Logger.Info(ident, "Sober installed");
    }

    [GeneratedRegex(@"(\d{1,3})%")]
    private static partial Regex Percent();

    [GeneratedRegex(@"^\s*Installing\s+(\d+)/(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex Step();

    /// <summary>Reads one line of `flatpak install` output ("Installing 2/3… 45%  2.1 MB/s").</summary>
    public static BootstrapProgress? ParseProgress(string line)
    {
        line = line.Replace('\r', ' ');
        var percent = Percent().Matches(line).LastOrDefault();
        var step = Step().Match(line);
        if (percent is null && !step.Success)
            return null;

        double? fraction = null;
        if (percent is not null && int.TryParse(percent.Groups[1].Value, out int pct))
        {
            pct = Math.Clamp(pct, 0, 100);
            fraction = pct / 100.0;
            // Spread several refs (runtime, GL extension, Sober) over one bar.
            if (step.Success && int.TryParse(step.Groups[1].Value, out int n) && int.TryParse(step.Groups[2].Value, out int total) && total > 0 && n >= 1 && n <= total)
                fraction = (n - 1 + pct / 100.0) / total;
        }

        string detail = step.Success ? $"step {step.Groups[1].Value} of {step.Groups[2].Value}" : "";
        return new BootstrapProgress(BootstrapPhase.Downloading, "Installing Sober from Flathub", fraction, detail);
    }

    public static bool IsRunning
    {
        get
        {
            try
            {
                var found = Process.GetProcessesByName("sober");
                foreach (var p in found)
                    p.Dispose();
                return found.Length > 0;
            }
            catch (Exception ex) when (ex is InvalidOperationException or PlatformNotSupportedException or System.ComponentModel.Win32Exception)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Starts Sober with an optional roblox:// link. Its output goes to Logs/sober.log (never to a
    /// pipe of ours that would break when Blox Code exits), and the call returns once Sober is
    /// clearly up, or throws if it quits straight away.
    /// </summary>
    public static async Task LaunchAsync(string robloxArgs, CancellationToken ct)
    {
        const string ident = "Sober::Launch";
        var (file, prefix) = Flatpak ?? throw new SoberLaunchException("Flatpak isn't installed.");

        robloxArgs = robloxArgs.Trim();
        if (robloxArgs.StartsWith('-'))
            throw new SoberLaunchException("That doesn't look like a Roblox link.");

        var command = new List<string>(prefix) { "run", AppId };
        if (robloxArgs.Length > 0)
            command.Add(robloxArgs);

        Directory.CreateDirectory(Paths.Logs);
        ProcessStartInfo psi;
        if (!OperatingSystem.IsWindows() && Proc.Which("sh") is { } sh)
        {
            // sh opens the log for Sober; the arguments travel as positional parameters, so nothing is re-parsed.
            psi = new ProcessStartInfo(sh) { UseShellExecute = false };
            foreach (string arg in new[] { "-c", "log=$1; shift; exec \"$@\" >\"$log\" 2>&1 </dev/null", "bloxcode-sober", SoberLog, file })
                psi.ArgumentList.Add(arg);
        }
        else
        {
            psi = new ProcessStartInfo(file) { UseShellExecute = false };
        }
        foreach (string arg in command)
            psi.ArgumentList.Add(arg);

        bool wasRunning = IsRunning;
        Logger.Info(ident, $"Starting {file} {string.Join(' ', command)}{(wasRunning ? " (Sober is already running)" : "")}");
        using var process = Process.Start(psi) ?? throw new SoberLaunchException("Couldn't start flatpak.");

        // Up when it's still running after a few seconds, or once a sober process shows up.
        var started = Stopwatch.StartNew();
        while (started.Elapsed < TimeSpan.FromSeconds(8))
        {
            if (process.HasExited)
            {
                if (process.ExitCode == 0)
                {
                    Logger.Info(ident, "flatpak handed the launch over and exited");
                    return;
                }
                string tail = ReadLogTail();
                Logger.Warn(ident, $"Sober exited with code {process.ExitCode}:\n{tail}");
                throw new SoberLaunchException(wasRunning
                    ? $"Sober didn't take the link (exit code {process.ExitCode}). It was already open: close it and try again."
                    : $"Sober closed right away (exit code {process.ExitCode}).", tail);
            }
            if (started.Elapsed > TimeSpan.FromSeconds(2) && IsRunning)
                break;
            await Task.Delay(250, ct);
        }
        Logger.Info(ident, $"Sober is running (pid {process.Id})");
    }

    /// <summary>The last lines Sober printed, for error messages and diagnostics.</summary>
    public static string ReadLogTail(int lines = 12)
    {
        try
        {
            if (!File.Exists(SoberLog))
                return "";
            using var stream = new FileStream(SoberLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            var all = reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            return string.Join('\n', all.TakeLast(lines)).Trim();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return "";
        }
    }

    /// <summary>Opens Sober's own settings window.</summary>
    public static bool OpenOwnSettings()
    {
        if (Flatpak is not { } flatpak)
            return false;
        return Proc.Detach(flatpak.File, flatpak.Prefix.Concat(new[] { "run", AppId, "config" }).ToArray());
    }

    // ===== NVIDIA: the Flatpak GL extension has to match the host driver exactly =====

    [GeneratedRegex(@"Kernel Module(?:\s+for\s+\S+)?\s+(\d+\.\d+(?:\.\d+)?)")]
    private static partial Regex NvidiaVersion();

    /// <summary>The proprietary NVIDIA driver version ("580.95.05"), or null without one.</summary>
    public static string? NvidiaDriver(string? procText = null)
    {
        try
        {
            procText ??= File.Exists("/proc/driver/nvidia/version") ? File.ReadAllText("/proc/driver/nvidia/version") : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
        return procText is not null && NvidiaVersion().Match(procText) is { Success: true } m ? m.Groups[1].Value : null;
    }

    /// <summary>The org.freedesktop.Platform.GL.nvidia-* runtimes flatpak has, as dashed versions ("580-95-05").</summary>
    public static async Task<List<string>> NvidiaGlExtensionsAsync(CancellationToken ct = default)
    {
        if (Flatpak is null)
            return new();
        try
        {
            var list = await FlatpakAsync(QueryTimeout, ct, null, "list", "--runtime", "--columns=application");
            return ParseGlExtensions(list.Output);
        }
        catch (Exception ex) when (ex is FileNotFoundException or TimeoutException)
        {
            return new();
        }
    }

    public static List<string> ParseGlExtensions(string output) => output.Split('\n')
        .Select(l => l.Trim())
        .Where(l => l.StartsWith("org.freedesktop.Platform.GL.nvidia-", StringComparison.Ordinal))
        .Select(l => l["org.freedesktop.Platform.GL.nvidia-".Length..])
        .Distinct()
        .ToList();

    /// <summary>
    /// A warning when the NVIDIA driver has no matching Flatpak GL extension, the most common reason
    /// Sober runs without acceleration or won't open on NVIDIA. Null when fine or not NVIDIA.
    /// </summary>
    public static async Task<string?> CheckNvidiaAsync(CancellationToken ct = default)
    {
        if (NvidiaDriver() is not { } driver)
            return null;
        var extensions = await NvidiaGlExtensionsAsync(ct);
        string wanted = driver.Replace('.', '-');
        if (extensions.Contains(wanted))
            return null;
        Logger.Warn("Sober::CheckNvidia", $"Driver {driver}, GL extensions: {string.Join(", ", extensions)}");
        return extensions.Count == 0
            ? $"NVIDIA driver {driver} is installed, but Flatpak has no NVIDIA GL extension for it."
            : $"NVIDIA driver {driver} doesn't match Flatpak's GL extension ({string.Join(", ", extensions.Select(e => e.Replace('-', '.')))}).";
    }
}
