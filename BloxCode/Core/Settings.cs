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

    public static Settings Settings { get; private set; } = new();
    public static State State { get; private set; } = new();

    public static void Load()
    {
        Settings = Read<Settings>(Paths.SettingsFile) ?? new Settings();
        State = Read<State>(Paths.StateFile) ?? new State();
    }

    public static void SaveSettings() => Write(Paths.SettingsFile, Settings);
    public static void SaveState() => Write(Paths.StateFile, State);

    private static T? Read<T>(string path) where T : class
    {
        try
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options);
        }
        catch (Exception ex)
        {
            Logger.Error("Store::Read", ex);
        }
        return null;
    }

    private static void Write<T>(string path, T value)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(value, Options));
        }
        catch (Exception ex)
        {
            Logger.Error("Store::Write", ex);
        }
    }
}
