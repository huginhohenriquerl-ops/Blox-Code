using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using BloxCode.Core;

namespace BloxCode.Linux.Platform;

public sealed record ProcResult(int ExitCode, string Output, string Error)
{
    public bool Ok => ExitCode == 0;

    /// <summary>The most useful line to show when the command failed.</summary>
    public string Problem => (Error.Trim().Length > 0 ? Error : Output).Trim().Split('\n').LastOrDefault(l => l.Trim().Length > 0)?.Trim() ?? $"exit code {ExitCode}";
}

/// <summary>Runs helper programs (flatpak, xdg-mime, …) with a timeout and an untranslated locale.</summary>
public static class Proc
{
    /// <summary>The full path of a program on PATH, or null.</summary>
    public static string? Which(string name)
    {
        if (Path.IsPathRooted(name))
            return File.Exists(name) ? name : null;

        foreach (string dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                string candidate = Path.Combine(dir, name);
                if (File.Exists(candidate))
                    return candidate;
                if (OperatingSystem.IsWindows())
                    foreach (string ext in new[] { ".exe", ".cmd", ".bat" })
                        if (File.Exists(candidate + ext))
                            return candidate + ext;
            }
            catch (ArgumentException)
            {
                // A malformed PATH entry; skip it.
            }
        }
        return null;
    }

    public static ProcessStartInfo StartInfo(string file, IEnumerable<string> args)
    {
        var psi = new ProcessStartInfo(file)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (string arg in args)
            psi.ArgumentList.Add(arg);
        // We parse the output, so it has to be in English whatever the desktop language is.
        psi.Environment["LC_ALL"] = "C";
        psi.Environment["LANG"] = "C";
        return psi;
    }

    /// <summary>
    /// Runs a program to completion. Throws <see cref="FileNotFoundException"/> when it can't be
    /// started, <see cref="TimeoutException"/> when it overruns (the process is killed), and
    /// <see cref="OperationCanceledException"/> when cancelled (also killed).
    /// </summary>
    public static async Task<ProcResult> RunAsync(string file, IEnumerable<string> args, TimeSpan timeout,
        CancellationToken ct = default, Action<string>? onLine = null)
    {
        var psi = StartInfo(file, args);
        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var output = new StringBuilder();
        var error = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            lock (output) output.AppendLine(e.Data);
            onLine?.Invoke(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            lock (error) error.AppendLine(e.Data);
            onLine?.Invoke(e.Data);
        };

        try
        {
            if (!process.Start())
                throw new FileNotFoundException($"Couldn't start {file}.", file);
        }
        catch (Win32Exception ex)
        {
            throw new FileNotFoundException($"Couldn't start {file}: {ex.Message}", file, ex);
        }

        try { process.StandardInput.Close(); } catch { }
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(limit.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            if (ct.IsCancellationRequested)
                throw;
            throw new TimeoutException($"{Path.GetFileName(file)} didn't finish within {timeout.TotalSeconds:0} seconds.");
        }

        string stdout, stderr;
        lock (output) stdout = output.ToString();
        lock (error) stderr = error.ToString();
        return new ProcResult(process.ExitCode, stdout, stderr);
    }

    /// <summary>Like <see cref="RunAsync"/>, but a missing program or a timeout comes back as null (logged).</summary>
    public static async Task<ProcResult?> TryRunAsync(string file, IEnumerable<string> args, TimeSpan timeout, CancellationToken ct = default)
    {
        try
        {
            return await RunAsync(file, args, timeout, ct);
        }
        catch (Exception ex) when (ex is FileNotFoundException or TimeoutException or InvalidOperationException)
        {
            Logger.Warn("Proc::TryRun", $"{file} {string.Join(' ', args)}: {ex.Message}");
            return null;
        }
    }

    /// <summary>Starts a program that should outlive us (a file manager, Sober's settings). No pipes: it inherits nothing to break.</summary>
    public static bool Detach(string file, params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo(file) { UseShellExecute = false };
            foreach (string arg in args)
                psi.ArgumentList.Add(arg);
            using var process = Process.Start(psi);
            return process is not null;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            Logger.Warn("Proc::Detach", $"{file}: {ex.Message}");
            return false;
        }
    }
}
