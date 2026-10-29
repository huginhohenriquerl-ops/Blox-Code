namespace BloxCode.Linux.Platform;

public enum DistroFamily { Arch, Debian, Other }

/// <summary>
/// Which distro we're on, from /etc/os-release (ID and ID_LIKE). Blox Code never runs sudo itself;
/// it only shows the right command for the family, with a copy button.
/// </summary>
public sealed record Distro(string Id, string[] IdLike, string Name, string VersionId)
{
    public DistroFamily Family =>
        Is("arch") ? DistroFamily.Arch
        : Is("debian") || Is("ubuntu") ? DistroFamily.Debian
        : DistroFamily.Other;

    private bool Is(string id) => Id == id || IdLike.Contains(id);

    public string InstallFlatpakCommand => Family switch
    {
        DistroFamily.Arch => "sudo pacman -S --needed flatpak",
        DistroFamily.Debian => "sudo apt install flatpak",
        _ => "Install the \"flatpak\" package with your distro's package manager",
    };

    public const string AddFlathubCommand =
        "flatpak remote-add --if-not-exists --user flathub https://dl.flathub.org/repo/flathub.flatpakrepo";

    public static Distro Current { get; } = Load();

    public static Distro Load(string? text = null)
    {
        if (text is null)
        {
            foreach (string path in new[] { "/etc/os-release", "/usr/lib/os-release" })
            {
                try
                {
                    if (File.Exists(path))
                    {
                        text = File.ReadAllText(path);
                        break;
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }
        }

        var values = Parse(text ?? "");
        string id = values.GetValueOrDefault("ID", "linux").ToLowerInvariant();
        string[] like = values.GetValueOrDefault("ID_LIKE", "").ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string name = values.GetValueOrDefault("PRETTY_NAME", values.GetValueOrDefault("NAME", "Linux"));
        return new Distro(id, like, name, values.GetValueOrDefault("VERSION_ID", ""));
    }

    /// <summary>KEY=value lines, values optionally quoted with ' or " (shell-style escapes inside "").</summary>
    public static Dictionary<string, string> Parse(string text)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Trim();
            int eq = line.IndexOf('=');
            if (line.Length == 0 || line.StartsWith('#') || eq <= 0)
                continue;

            string key = line[..eq].Trim();
            string value = line[(eq + 1)..].Trim();
            if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
                value = value[1..^1].Replace("\\\"", "\"").Replace("\\$", "$").Replace("\\`", "`").Replace("\\\\", "\\");
            else if (value.Length >= 2 && value[0] == '\'' && value[^1] == '\'')
                value = value[1..^1];
            values[key] = value;
        }
        return values;
    }

    public static string Session =>
        $"{Environment.GetEnvironmentVariable("XDG_SESSION_TYPE") ?? "unknown"} · {Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? "unknown desktop"}";
}
