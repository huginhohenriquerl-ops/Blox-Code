namespace BloxCode.Core;

/// <summary>Friendly toggles backed by real FastFlags (the same flags Bloxstrap exposes).</summary>
public static class FastFlagPresets
{
    public const string ExclusiveFullscreen = "FFlagHandleAltEnterFullscreenManually";
    public const string DisableDpiScale = "DFFlagDisableDPIScale";
    public const string Msaa = "FIntDebugForceMSAASamples";
    public const string TextureOverrideEnabled = "DFFlagTextureQualityOverrideEnabled";
    public const string TextureQuality = "DFIntTextureQualityOverride";

    /// <summary>Flags owned by the toggles above; the editor hides them unless asked, like Bloxstrap.</summary>
    public static readonly HashSet<string> PresetFlags = new(StringComparer.Ordinal)
    {
        ExclusiveFullscreen, DisableDpiScale, Msaa, TextureOverrideEnabled, TextureQuality,
    };

    public static readonly (string Label, string? Value)[] MsaaModes =
        { ("Auto", null), ("1x", "1"), ("2x", "2"), ("4x", "4") };

    public static readonly (string Label, string? Value)[] TextureLevels =
        { ("Auto", null), ("Lowest", "0"), ("Low", "1"), ("Medium", "2"), ("High", "3") };

    private static Dictionary<string, string> Flags => Store.Settings.FastFlags;

    public static string? Get(string flag) => Flags.TryGetValue(flag, out var v) ? v : null;

    public static void Set(string flag, string? value)
    {
        if (value is null)
            Flags.Remove(flag);
        else
            Flags[flag] = value;
    }

    public static bool UseExclusiveFullscreen
    {
        get => Get(ExclusiveFullscreen)?.Equals("False", StringComparison.OrdinalIgnoreCase) == true;
        set => Set(ExclusiveFullscreen, value ? "False" : null);
    }

    public static bool DisableScaling
    {
        get => Get(DisableDpiScale)?.Equals("True", StringComparison.OrdinalIgnoreCase) == true;
        set => Set(DisableDpiScale, value ? "True" : null);
    }

    public static string? MsaaSamples
    {
        get => Get(Msaa);
        set => Set(Msaa, value);
    }

    public static string? TextureLevel
    {
        get => Get(TextureQuality);
        set
        {
            Set(TextureQuality, value);
            Set(TextureOverrideEnabled, value is null ? null : "True");
        }
    }
}
