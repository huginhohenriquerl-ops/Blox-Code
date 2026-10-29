using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace BloxCode.Linux.UI;

/// <summary>
/// A shell command the user runs themselves, with a copy button. Blox Code never runs sudo: when
/// something needs it (installing Flatpak), it shows the right command for the distro instead.
/// </summary>
public static class CommandRow
{
    public static Control Create(string command)
    {
        var copy = new Button { Content = "Copy", Padding = new Thickness(10, 4), FontSize = 12.5 };
        copy.Click += async (_, _) =>
            copy.Content = await Shell.CopyAsync(TopLevel.GetTopLevel(copy) as Window, command) ? "Copied" : "Copy failed";

        var text = new SelectableTextBlock
        {
            Text = command,
            FontFamily = Res.Mono,
            FontSize = 12.5,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var row = new DockPanel();
        DockPanel.SetDock(copy, Dock.Right);
        row.Children.Add(copy);
        row.Children.Add(text);
        return new Border { Classes = { "command" }, Child = row };
    }
}
