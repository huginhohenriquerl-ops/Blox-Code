namespace BloxCode.Core;

public static class Paths
{
    public static string LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    public static string Desktop => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    public static string StartMenu => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");

    public static string Base { get; } = Path.Combine(LocalAppData, "BloxCode");
    public static string Versions => Path.Combine(Base, "Versions");
    public static string Downloads => Path.Combine(Base, "Downloads");
    public static string Modifications => Path.Combine(Base, "Modifications");
    public static string Logs => Path.Combine(Base, "Logs");

    public static string SettingsFile => Path.Combine(Base, "Settings.json");
    public static string StateFile => Path.Combine(Base, "State.json");

    /// <summary>Where the installed copy of Blox Code lives.</summary>
    public static string Application => Path.Combine(Base, "BloxCode.exe");

    /// <summary>The executable that is currently running.</summary>
    public static string Process => Environment.ProcessPath ?? Application;

    public static bool IsRunningInstalled =>
        string.Equals(Path.GetFullPath(Process), Path.GetFullPath(Application), StringComparison.OrdinalIgnoreCase);

    public static void EnsureCreated()
    {
        foreach (var dir in new[] { Base, Versions, Downloads, Modifications, Logs })
            Directory.CreateDirectory(dir);
    }
}
