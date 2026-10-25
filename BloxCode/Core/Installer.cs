using System.Diagnostics;
using Microsoft.Win32;

namespace BloxCode.Core;

/// <summary>
/// Installs Blox Code per-user (no admin): copies itself to %LocalAppData%\BloxCode, registers the
/// roblox / roblox-player protocols, an Apps &amp; Features entry and shortcuts. Same registry layout
/// that Bloxstrap.Installer was observed to use.
/// </summary>
public static class Installer
{
    private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\BloxCode";
    private static readonly string[] Protocols = { "roblox", "roblox-player" };

    public static string DesktopShortcut => Path.Combine(Paths.Desktop, "Blox Code.lnk");
    public static string StartMenuShortcut => Path.Combine(Paths.StartMenu, "Blox Code.lnk");
    public static string PlayShortcut => Path.Combine(Paths.StartMenu, "Play Roblox (Blox Code).lnk");

    public static bool IsInstalled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(UninstallKey);
            return key is not null && File.Exists(Paths.Application);
        }
    }

    /// <summary>The command currently registered for roblox-player:, if any.</summary>
    public static string? CurrentProtocolHandler()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\roblox-player\shell\open\command");
        return key?.GetValue("") as string;
    }

    public static bool IsDefaultLauncher =>
        CurrentProtocolHandler()?.Contains(Paths.Application, StringComparison.OrdinalIgnoreCase) == true;

    public static void Install(bool desktopShortcut)
    {
        const string ident = "Installer::Install";
        Paths.EnsureCreated();

        if (!Paths.IsRunningInstalled)
            File.Copy(Paths.Process, Paths.Application, overwrite: true);

        RegisterProtocols();

        using (var key = Registry.CurrentUser.CreateSubKey(UninstallKey))
        {
            string version = typeof(Installer).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
            key.SetValue("DisplayName", "Blox Code");
            key.SetValue("DisplayIcon", $"{Paths.Application},0");
            key.SetValue("DisplayVersion", version);
            key.SetValue("Publisher", "Blox Code");
            key.SetValue("InstallLocation", Paths.Base);
            key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
            key.SetValue("UninstallString", $"\"{Paths.Application}\" -uninstall");
            key.SetValue("QuietUninstallString", $"\"{Paths.Application}\" -uninstall -quiet");
            key.SetValue("ModifyPath", $"\"{Paths.Application}\" -settings");
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        }

        ShellLink.Create(StartMenuShortcut, Paths.Application, "", "Blox Code settings");
        ShellLink.Create(PlayShortcut, Paths.Application, "-player", "Play Roblox with Blox Code");
        if (desktopShortcut)
            ShellLink.Create(DesktopShortcut, Paths.Application, "", "Blox Code");

        File.WriteAllText(Path.Combine(Paths.Modifications, "README.txt"),
            "Files placed here are copied over the Roblox installation every launch.\r\n" +
            "Mirror Roblox's folder layout, e.g. content\\sounds\\ouch.ogg.\r\n" +
            "Delete a file to restore the original.\r\n");

        Logger.Info(ident, "Installation finished");
    }

    /// <summary>After a self-update: keep Apps &amp; Features showing the right version.</summary>
    public static void RefreshAfterUpgrade()
    {
        using var key = Registry.CurrentUser.OpenSubKey(UninstallKey, writable: true);
        key?.SetValue("DisplayVersion", Updater.Current.ToString());
    }

    public static void RegisterProtocols()
    {
        foreach (string protocol in Protocols)
        {
            using var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{protocol}");
            key.SetValue("", $"URL: {protocol} Protocol");
            key.SetValue("URL Protocol", "");

            using (var icon = key.CreateSubKey("DefaultIcon"))
                icon.SetValue("", $"{Paths.Application},0");
            using (var command = key.CreateSubKey(@"shell\open\command"))
                command.SetValue("", $"\"{Paths.Application}\" -player \"%1\"");
        }
    }

    public static void Uninstall()
    {
        const string ident = "Installer::Uninstall";

        // Only drop the protocol keys if they still point at us; another launcher may have taken over.
        if (IsDefaultLauncher)
            foreach (string protocol in Protocols)
                Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{protocol}", throwOnMissingSubKey: false);

        Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, throwOnMissingSubKey: false);

        foreach (string lnk in new[] { DesktopShortcut, StartMenuShortcut, PlayShortcut })
        {
            try { File.Delete(lnk); } catch { }
        }

        Logger.Info(ident, "Removing install folder after exit");

        // The running exe can't delete itself, so let cmd wait for us to exit first.
        Process.Start(new ProcessStartInfo("cmd.exe", $"/c timeout 5 && rmdir /S /Q \"{Paths.Base}\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
        });
    }
}
