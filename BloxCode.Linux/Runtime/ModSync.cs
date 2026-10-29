using BloxCode.Core;

namespace BloxCode.Linux.Runtime;

public sealed record ModSyncResult(int Applied, int Removed, List<string> Failed);

/// <summary>
/// Mirrors Blox Code's Modifications folder into Sober's asset_overlay, which overrides Roblox's
/// built-in files (same layout as the APK's assets/, e.g. content/textures/…).
/// <list type="bullet">
/// <item>Files Blox Code placed are tracked in State.ModManifest and removed when the mod goes.</item>
/// <item>A file the user had put in the overlay by hand is moved aside before a mod replaces it,
/// and moved back when that mod is removed.</item>
/// <item>Everything else in the overlay is left alone.</item>
/// </list>
/// </summary>
public static class ModSync
{
    private static readonly object Gate = new();

    public static string BackupDir => Path.Combine(Paths.Base, "OverlayBackup");

    /// <summary>Files directly in Modifications that are notes, not mods.</summary>
    private static readonly HashSet<string> Ignored = new(StringComparer.OrdinalIgnoreCase) { "README.txt" };

    /// <summary>Mods as overlay-relative paths with forward slashes, sorted.</summary>
    public static List<string> ListMods(string? modsDir = null)
    {
        modsDir ??= Paths.Modifications;
        if (!Directory.Exists(modsDir))
            return new();

        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.Hidden | FileAttributes.System };
        return Directory.EnumerateFiles(modsDir, "*", options)
            .Select(f => Path.GetRelativePath(modsDir, f).Replace('\\', '/'))
            .Where(rel => !Ignored.Contains(rel) && IsSafe(rel) && !Path.GetFileName(rel).StartsWith('.'))
            .OrderBy(rel => rel, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>No rooted paths, no "..", nothing that could land outside the overlay.</summary>
    public static bool IsSafe(string rel) =>
        rel.Length > 0 && !Path.IsPathRooted(rel) && !rel.StartsWith('/') && !rel.Split('/', '\\').Any(part => part is ".." or "." or "");

    public static ModSyncResult Sync(string? modsDir = null, string? overlayDir = null, string? backupDir = null)
    {
        const string ident = "ModSync::Sync";
        modsDir ??= Paths.Modifications;
        overlayDir ??= Sober.OverlayDir;
        backupDir ??= BackupDir;

        lock (Gate)
        {
            var mods = ListMods(modsDir);
            var wanted = mods.ToHashSet(StringComparer.Ordinal);
            var previous = Store.State.ModManifest.Where(IsSafe).ToHashSet(StringComparer.Ordinal);
            var placed = new HashSet<string>(StringComparer.Ordinal);
            var failed = new List<string>();
            int applied = 0, removed = 0;

            // Mods that went away: delete our copy, put back whatever the user had there.
            foreach (string rel in previous.Where(r => !wanted.Contains(r)))
            {
                try
                {
                    string target = Combine(overlayDir, rel);
                    if (File.Exists(target))
                        File.Delete(target);
                    string backup = Combine(backupDir, rel);
                    if (File.Exists(backup))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                        File.Move(backup, target);
                    }
                    PruneEmpty(Path.GetDirectoryName(target)!, overlayDir);
                    PruneEmpty(Path.GetDirectoryName(backup)!, backupDir);
                    removed++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Keep tracking it so the next sync tries again.
                    Logger.Warn(ident, $"Couldn't remove {rel}: {ex.Message}");
                    placed.Add(rel);
                    failed.Add(rel);
                }
            }

            foreach (string rel in mods)
            {
                try
                {
                    string source = Combine(modsDir, rel);
                    string target = Combine(overlayDir, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);

                    // A file we didn't put there belongs to the user: set it aside first.
                    if (!previous.Contains(rel) && File.Exists(target))
                    {
                        string backup = Combine(backupDir, rel);
                        Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                        if (!File.Exists(backup))
                            File.Move(target, backup);
                    }

                    if (!IsSame(source, target))
                    {
                        string temp = target + ".bloxcode-tmp";
                        File.Copy(source, temp, overwrite: true);
                        File.SetLastWriteTimeUtc(temp, File.GetLastWriteTimeUtc(source));
                        File.Move(temp, target, overwrite: true);
                        applied++;
                    }
                    placed.Add(rel);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Logger.Warn(ident, $"Couldn't apply {rel}: {ex.Message}");
                    failed.Add(rel);
                    if (previous.Contains(rel))
                        placed.Add(rel);
                }
            }

            Store.State.ModManifest = placed.OrderBy(r => r, StringComparer.Ordinal).ToList();
            Store.SaveState();
            if (applied + removed + failed.Count > 0)
                Logger.Info(ident, $"{applied} applied, {removed} removed, {failed.Count} failed, {placed.Count} active");
            return new ModSyncResult(applied, removed, failed);
        }
    }

    /// <summary>Takes every Blox Code mod out of Sober (the Modifications folder itself is kept).</summary>
    public static ModSyncResult RemoveAllFromSober(string? overlayDir = null, string? backupDir = null) =>
        Sync(Path.Combine(Paths.Cache, "no-mods"), overlayDir, backupDir);

    private static bool IsSame(string source, string target)
    {
        if (!File.Exists(target))
            return false;
        var a = new FileInfo(source);
        var b = new FileInfo(target);
        return a.Length == b.Length && a.LastWriteTimeUtc == b.LastWriteTimeUtc;
    }

    private static string Combine(string root, string rel) =>
        Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>Removes now-empty folders up to (not including) the root.</summary>
    private static void PruneEmpty(string dir, string root)
    {
        string stop = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        for (var current = Path.GetFullPath(dir); current.Length > stop.Length && current.StartsWith(stop, Paths.PathComparison);
             current = Path.GetDirectoryName(current)!)
        {
            if (!Directory.Exists(current) || Directory.EnumerateFileSystemEntries(current).Any())
                return;
            Directory.Delete(current);
        }
    }
}
