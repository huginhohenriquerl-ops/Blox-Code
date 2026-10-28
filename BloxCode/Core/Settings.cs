using System.Text.Json;
using System.Text.Json.Serialization;

namespace BloxCode.Core;

public enum AppTheme { System, Light, Dark }

public sealed class Settings
{
    public AppTheme Theme { get; set; } = AppTheme.System;
    public string Channel { get; set; } = Deployment.DefaultChannel;
    public bool AnimateMascot { get; set; } = true;
    public bool CleanupOldVersions { get; set; } = true;
    public bool ConfirmLaunches { get; set; }
    public bool AutoUpdate { get; set; } = true;
    public Dictionary<string, string> FastFlags { get; set; } = new();
}

public sealed class State
{
    public string? VersionGuid { get; set; }
    public string? Channel { get; set; }
    public Dictionary<string, string> PackageHashes { get; set; } = new();
    /// <summary>Relative paths (inside the version folder) currently overwritten by a mod.</summary>
    public List<string> ModManifest { get; set; } = new();
    public bool ForceReinstall { get; set; }
    /// <summary>Set by the upgrade step so the next menu can say "updated to vX" once.</summary>
    public string? JustUpdatedTo { get; set; }
}

public static class Store
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    // The bootstrapper saves state from a worker thread while the UI may save settings.
    private static readonly object Sync = new();

    public static Settings Settings { get; private set; } = new();
    public static State State { get; private set; } = new();

    public static void Load()
    {
        Settings = Read<Settings>(Paths.SettingsFile) ?? new Settings();
        State = Read<State>(Paths.StateFile) ?? new State();

        // Hand-edited or older files can carry explicit nulls; never let them reach the rest of the app.
        Settings.FastFlags ??= new();
        Settings.Channel = string.IsNullOrWhiteSpace(Settings.Channel) ? Deployment.DefaultChannel : Settings.Channel.Trim();
        State.PackageHashes ??= new();
        State.ModManifest ??= new();
    }

    public static void SaveSettings() => Write(Paths.SettingsFile, Settings);
    public static void SaveState() => Write(Paths.StateFile, State);

    private static T? Read<T>(string path) where T : class
    {
        if (!File.Exists(path))
            return null;
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options);
        }
        catch (Exception ex)
        {
            // Keep the broken file for the user instead of silently overwriting it with defaults.
            Logger.Error("Store::Read", ex);
            try { File.Copy(path, path + ".corrupt", overwrite: true); } catch { }
            return null;
        }
    }

    private static void Write<T>(string path, T value)
    {
        lock (Sync)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                // Write a sibling file and swap it in, so a crash mid-write can't leave half a JSON file.
                string temp = path + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(value, Options));
                File.Move(temp, path, overwrite: true);
            }
            catch (Exception ex)
            {
                Logger.Error("Store::Write", ex);
            }
        }
    }
}
