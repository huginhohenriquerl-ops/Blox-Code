using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using BloxCode.Core;

namespace BloxCode.UI;

/// <summary>
/// Opening folders/links and touching the clipboard both fail in ordinary situations (no default
/// browser, another app holding the clipboard). These helpers turn that into a message, not a crash.
/// </summary>
public static class Shell
{
    public static void Open(string target, Window? owner = null)
    {
        try
        {
            if (!target.StartsWith("http", StringComparison.OrdinalIgnoreCase) && !File.Exists(target))
                Directory.CreateDirectory(target);
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException)
        {
            Logger.Warn("Shell::Open", $"{target}: {ex.Message}");
            MessageDialog.Show(owner, "Couldn't open that", $"{target}\n\n{ex.Message}");
        }
    }

    /// <summary>Copies text, retrying briefly while another app has the clipboard open.</summary>
    public static bool TryCopy(string text)
    {
        for (int attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                Clipboard.SetDataObject(text, copy: true);
                return true;
            }
            catch (COMException ex) when (attempt < 5)
            {
                Logger.Warn("Shell::TryCopy", $"Clipboard busy (attempt {attempt}): {ex.Message}");
                Thread.Sleep(60);
            }
            catch (COMException ex)
            {
                Logger.Warn("Shell::TryCopy", $"Clipboard unavailable: {ex.Message}");
            }
        }
        return false;
    }
}
