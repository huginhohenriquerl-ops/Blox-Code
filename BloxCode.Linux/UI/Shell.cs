using System.Diagnostics;
using Avalonia.Controls;
using BloxCode.Core;
using BloxCode.Linux.Platform;

namespace BloxCode.Linux.UI;

/// <summary>
/// Opening folders/links and the clipboard can fail in ordinary situations (no xdg-open, no file
/// manager, no clipboard owner). These helpers turn that into a message, not a crash.
/// </summary>
public static class Shell
{
    public static async void Open(string target, Window? owner = null)
    {
        try
        {
            bool isUrl = target.StartsWith("http", StringComparison.OrdinalIgnoreCase);
            if (!isUrl && !File.Exists(target))
                Directory.CreateDirectory(target);

            bool opened;
            if (OperatingSystem.IsLinux())
            {
                opened = Proc.Which("xdg-open") is { } xdgOpen && Proc.Detach(xdgOpen, target);
                if (!opened && owner is not null && TopLevel.GetTopLevel(owner)?.Launcher is { } launcher)
                    opened = await launcher.LaunchUriAsync(isUrl ? new Uri(target) : new Uri(Path.GetFullPath(target)));
            }
            else
            {
                using var process = Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
                opened = true;
            }

            if (!opened)
                await MessageDialog.ShowAsync(owner, "Couldn't open that", $"Nothing on this system opens it. Here's the path:\n\n{target}");
        }
        catch (Exception ex)
        {
            Logger.Warn("Shell::Open", $"{target}: {ex.Message}");
            await MessageDialog.ShowAsync(owner, "Couldn't open that", $"{target}\n\n{ex.Message}");
        }
    }

    /// <summary>Copies text; false when no clipboard is available (or it refused).</summary>
    public static async Task<bool> CopyAsync(Window? owner, string text)
    {
        try
        {
            if (owner is not null && TopLevel.GetTopLevel(owner)?.Clipboard is { } clipboard)
            {
                await clipboard.SetTextAsync(text);
                return true;
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("Shell::Copy", ex.Message);
        }
        return false;
    }
}
