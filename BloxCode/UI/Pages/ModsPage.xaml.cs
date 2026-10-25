using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using BloxCode.Core;

namespace BloxCode.UI.Pages;

public partial class ModsPage : UserControl
{
    public ModsPage()
    {
        InitializeComponent();
        FolderPath.Text = Paths.Modifications;
        Refresh();
    }

    private void Refresh()
    {
        Directory.CreateDirectory(Paths.Modifications);
        var mods = Directory.EnumerateFiles(Paths.Modifications, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(Paths.Modifications, f))
            .Where(r => !r.Equals("README.txt", StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r)
            .ToList();

        ModList.ItemsSource = mods;
        CountText.Text = mods.Count == 1 ? "1 file" : $"{mods.Count} files";
        EmptyText.Visibility = mods.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnRefresh(object sender, RoutedEventArgs e) => Refresh();

    private void OnOpenFolder(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(Paths.Modifications) { UseShellExecute = true });
}
