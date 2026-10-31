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
        List<string> mods;
        try
        {
            Directory.CreateDirectory(Paths.Modifications);
            // A folder that can't be read (permissions, a file in use) is skipped, not fatal to the page.
            var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
            mods = Directory.EnumerateFiles(Paths.Modifications, "*", options)
                .Select(f => Path.GetRelativePath(Paths.Modifications, f))
                .Where(r => !r.Equals("README.txt", StringComparison.OrdinalIgnoreCase))
                .OrderBy(r => r)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.Warn("ModsPage::Refresh", ex.Message);
            mods = new();
        }

        ModList.ItemsSource = mods;
        CountText.Text = mods.Count == 1 ? "1 file" : $"{mods.Count} files";
        EmptyText.Visibility = mods.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnRefresh(object sender, RoutedEventArgs e) => Refresh();

    private void OnOpenFolder(object sender, RoutedEventArgs e) =>
        Shell.Open(Paths.Modifications, Window.GetWindow(this));
}
