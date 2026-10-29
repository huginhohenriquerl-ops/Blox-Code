using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using BloxCode.Core;

namespace BloxCode.Linux.Runtime;

/// <summary>Sober's config.json exists but can't be used; Blox Code then leaves it alone.</summary>
public sealed class SoberConfigException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Reads and writes Sober's config.json. Invalid JSON stops Sober from opening, so every write is
/// validated and swapped in atomically, only the keys Blox Code owns are touched (unknown keys
/// survive), and the original file is backed up before the first change.
/// </summary>
public static class SoberConfig
{
    private static readonly object Sync = new();

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly JsonDocumentOptions ReadOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    public static string BackupFile => Path.Combine(Paths.Base, "sober-config.original.json");

    /// <summary>Sober's documented settings and defaults, used when it hasn't written its own file yet.</summary>
    public static JsonObject Defaults() => new()
    {
        ["allow_gamepad_permission"] = false,
        ["close_on_leave"] = true,
        ["discord_rpc_enabled"] = false,
        ["discord_rpc_show_join_button"] = false,
        ["enable_gamemode"] = true,
        ["enable_hidpi"] = false,
        ["enable_mobile_home_screen"] = false,
        ["fflags"] = new JsonObject(),
        ["graphics_optimization_mode"] = "balanced",
        ["server_location_indicator_enabled"] = false,
        ["touch_mode"] = "off",
        ["use_console_experience"] = false,
        ["use_libsecret"] = false,
        ["use_opengl"] = false,
    };

    /// <summary>The current file, or the defaults when Sober hasn't created it. Throws when it can't be parsed.</summary>
    public static JsonObject Load(string? path = null)
    {
        path ??= Sober.ConfigFile;
        lock (Sync)
        {
            if (!File.Exists(path))
                return Defaults();

            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new SoberConfigException($"Couldn't read Sober's settings: {ex.Message}", ex);
            }

            if (text.Trim().Length == 0)
                return Defaults();

            try
            {
                return JsonNode.Parse(text, documentOptions: ReadOptions) as JsonObject
                       ?? throw new SoberConfigException("Sober's config.json isn't a JSON object.");
            }
            catch (JsonException ex)
            {
                string where = ex.LineNumber is { } line ? $" (line {line + 1})" : "";
                throw new SoberConfigException($"Sober's config.json isn't valid JSON{where}, so Sober won't open either. Fix it, or reset it on the Sober page.", ex);
            }
        }
    }

    /// <summary>Writes the file atomically after checking the text parses back; backs up the original once.</summary>
    public static void Save(JsonObject root, string? path = null)
    {
        path ??= Sober.ConfigFile;
        lock (Sync)
        {
            string text = root.ToJsonString(WriteOptions) + "\n";
            using (JsonDocument.Parse(text)) { }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (path == Sober.ConfigFile && File.Exists(path) && !File.Exists(BackupFile))
            {
                Directory.CreateDirectory(Paths.Base);
                File.Copy(path, BackupFile);
                Logger.Info("SoberConfig::Save", $"Backed up Sober's original settings to {BackupFile}");
            }

            string temp = path + ".bloxcode-tmp";
            File.WriteAllText(temp, text);
            File.Move(temp, path, overwrite: true);
        }
    }

    /// <summary>Applies a change to the file (load, edit, save), skipping the write when nothing changed.</summary>
    public static bool Update(Action<JsonObject> edit, string? path = null)
    {
        lock (Sync)
        {
            var root = Load(path);
            var before = root.DeepClone();
            edit(root);
            if (JsonNode.DeepEquals(before, root) && File.Exists(path ?? Sober.ConfigFile))
                return false;
            Save(root, path);
            return true;
        }
    }

    // ===== Fast Flags =====

    /// <summary>
    /// The JSON value for a flag. Blox Code stores every value as text (like ClientAppSettings.json
    /// on Windows); Sober's file uses real JSON types, so booleans and whole numbers are converted.
    /// </summary>
    public static JsonNode ToJson(string name, string value)
    {
        var kind = FastFlagValidator.KindOf(name);
        if (kind == FlagKind.String)
            return JsonValue.Create(value);
        if (value.Equals("true", StringComparison.OrdinalIgnoreCase))
            return JsonValue.Create(true);
        if (value.Equals("false", StringComparison.OrdinalIgnoreCase))
            return JsonValue.Create(false);
        if (kind is FlagKind.Int or FlagKind.Log && long.TryParse(value, out long number))
            return JsonValue.Create(number);
        return JsonValue.Create(value);
    }

    public static string FromJson(string name, JsonNode? node) => node?.GetValueKind() switch
    {
        JsonValueKind.String => FastFlagValidator.Normalize(name, node.GetValue<string>()),
        JsonValueKind.True => "True",
        JsonValueKind.False => "False",
        null or JsonValueKind.Null => "",
        _ => node.ToJsonString(),
    };

    public static Dictionary<string, string> ReadFlags(JsonObject root)
    {
        var flags = new Dictionary<string, string>(StringComparer.Ordinal);
        if (root["fflags"] is JsonObject fflags)
            foreach (var (name, value) in fflags)
                flags[name] = FromJson(name, value);
        return flags;
    }

    /// <summary>
    /// Writes Blox Code's Fast Flags into Sober's "fflags". The first time, flags the user had
    /// already put in Sober are imported into Blox Code instead of being dropped.
    /// Returns true when the file changed.
    /// </summary>
    public static bool ApplyFastFlags(string? path = null)
    {
        const string ident = "SoberConfig::ApplyFastFlags";
        bool imported = false;

        bool changed = Update(root =>
        {
            if (!Store.State.SoberFlagsImported)
            {
                foreach (var (name, value) in ReadFlags(root))
                {
                    if (FastFlagValidator.ValidateName(name) is null && Store.Settings.FastFlags.TryAdd(name, value))
                        imported = true;
                }
                Store.State.SoberFlagsImported = true;
            }

            var fflags = new JsonObject();
            foreach (var (name, value) in Store.Settings.FastFlags.OrderBy(f => f.Key, StringComparer.Ordinal))
                fflags[name] = ToJson(name, value);
            root["fflags"] = fflags;
        }, path);

        Store.SaveState();
        if (imported)
        {
            Logger.Info(ident, "Imported Sober's existing fflags");
            Store.SaveSettings();
        }
        if (changed)
            Logger.Info(ident, $"Wrote {Store.Settings.FastFlags.Count} flags to Sober");
        return changed;
    }

    // ===== Sober's own options =====

    public static bool GetBool(JsonObject root, string key, bool fallback) =>
        root[key]?.GetValueKind() switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => fallback,
        };

    public static string GetString(JsonObject root, string key, string fallback) =>
        root[key]?.GetValueKind() == JsonValueKind.String ? root[key]!.GetValue<string>() : fallback;

    // ===== Restore =====

    public static bool HasBackup => File.Exists(BackupFile);

    /// <summary>Puts back the file Sober had before Blox Code first changed it.</summary>
    public static void RestoreBackup()
    {
        lock (Sync)
        {
            string text = File.ReadAllText(BackupFile);
            Directory.CreateDirectory(Path.GetDirectoryName(Sober.ConfigFile)!);
            string temp = Sober.ConfigFile + ".bloxcode-tmp";
            File.WriteAllText(temp, text);
            File.Move(temp, Sober.ConfigFile, overwrite: true);
        }
        ForgetSync();
    }

    /// <summary>Deletes config.json (kept aside first) so Sober recreates its defaults on the next start.</summary>
    public static void Reset()
    {
        lock (Sync)
        {
            if (File.Exists(Sober.ConfigFile))
            {
                Directory.CreateDirectory(Paths.Base);
                File.Copy(Sober.ConfigFile, Path.Combine(Paths.Base, "sober-config.before-reset.json"), overwrite: true);
                File.Delete(Sober.ConfigFile);
            }
        }
        ForgetSync();
    }

    /// <summary>After a restore or reset, the flags in Sober's file are the user's again: import them next time.</summary>
    private static void ForgetSync()
    {
        Store.State.SoberFlagsImported = false;
        Store.SaveState();
    }
}
