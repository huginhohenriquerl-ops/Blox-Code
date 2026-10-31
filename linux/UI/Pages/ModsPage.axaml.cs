using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using BloxCode.Core;
using BloxCode.Linux.Runtime;

namespace BloxCode.Linux.UI.Pages;

public sealed record AssetRow(string Path, string Name, string Folder, string SizeText, bool IsModded);

public partial class ModsPage : UserControl
{
    private readonly MainWindow _window;
    private List<AssetEntry> _assets = new();
    private Func<AssetEntry, bool> _category = _ => true;

    // Roblox's package has tens of thousands of files: filter once typing pauses, not on every key.
    private readonly DispatcherTimer _searchDelay = new() { Interval = TimeSpan.FromMilliseconds(220) };

    public ModsPage(MainWindow window)
    {
        _window = window;
        InitializeComponent();
        FolderPath.Text = Paths.Modifications;
        _searchDelay.Tick += (_, _) =>
        {
            _searchDelay.Stop();
            ShowAssets();
        };
        DetachedFromVisualTree += (_, _) => _searchDelay.Stop();

        foreach (var (label, match) in ApkAssets.Categories)
        {
            var chip = new RadioButton { Content = label, GroupName = "assetCategory" + GetHashCode(), IsChecked = label == "All" };
            chip.Theme = Res.Theme("Segment");
            chip.IsCheckedChanged += (_, _) =>
            {
                if (chip.IsChecked != true) return;
                _category = match;
                ShowAssets();
            };
            CategoryChips.Children.Add(chip);
        }

        RefreshMods();
        AttachedToVisualTree += async (_, _) => await LoadAssetsAsync();
    }

    // ===== Roblox's files =====

    private async Task LoadAssetsAsync()
    {
        AssetStatus.Text = "Reading Roblox's files…";
        try
        {
            var (apk, assets) = await Task.Run(() =>
            {
                string? found = ApkAssets.FindApk();
                return (found, found is null ? new List<AssetEntry>() : ApkAssets.List(found));
            });
            _assets = assets;
            AssetStatus.Text = apk is null
                ? "Roblox's files show up here after Sober has run once (it downloads Roblox on first start)."
                : $"{assets.Count:N0} files in Roblox's package.";
        }
        catch (Exception ex)
        {
            Logger.Error("ModsPage::LoadAssets", ex);
            _assets = new();
            AssetStatus.Text = $"Couldn't read Roblox's package: {ex.Message}";
        }
        ShowAssets();
    }

    private void ShowAssets()
    {
        string query = (AssetSearch.Text ?? "").Trim();
        var modded = ModSync.ListMods().ToHashSet(StringComparer.Ordinal);
        string? selected = (AssetList.SelectedItem as AssetRow)?.Path;

        var rows = _assets
            .Where(a => _category(a) && (query.Length == 0 || a.Path.Contains(query, StringComparison.OrdinalIgnoreCase)))
            .Select(a => new AssetRow(a.Path, a.Name, a.Folder, FormatSize(a.Size), modded.Contains(a.Path)))
            .ToList();
        AssetList.ItemsSource = rows;
        AssetList.SelectedItem = rows.FirstOrDefault(r => r.Path == selected);
        UpdateSelection();
    }

    private static string FormatSize(long bytes) =>
        bytes >= 1048576 ? $"{bytes / 1048576.0:0.0} MB" : bytes >= 1024 ? $"{bytes / 1024.0:0} KB" : $"{bytes} B";

    private void UpdateSelection()
    {
        var row = AssetList.SelectedItem as AssetRow;
        ReplaceButton.IsEnabled = row is not null;
        RestoreButton.IsEnabled = row is { IsModded: true };
        SelectedText.Text = row?.Path ?? "Pick a file to replace";
    }

    private void OnAssetSelected(object? sender, SelectionChangedEventArgs e) => UpdateSelection();

    private void OnAssetSearch(object? sender, TextChangedEventArgs e)
    {
        _searchDelay.Stop();
        _searchDelay.Start();
    }

    private async void OnReloadAssets(object? sender, RoutedEventArgs e) => await LoadAssetsAsync();

    private async void OnReplace(object? sender, RoutedEventArgs e)
    {
        if (AssetList.SelectedItem is not AssetRow row)
            return;

        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null || !storage.CanOpen)
        {
            await MessageDialog.ShowAsync(_window, "No file picker here",
                $"Copy your file to this path instead (same name), then come back:\n\n{Path.Combine(Paths.Modifications, row.Path)}");
            return;
        }

        try
        {
            string extension = Path.GetExtension(row.Path);
            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = $"Replace {row.Name}",
                AllowMultiple = false,
                FileTypeFilter = extension.Length > 0
                    ? new[] { new FilePickerFileType($"{extension} files") { Patterns = new[] { "*" + extension } }, FilePickerFileTypes.All }
                    : new[] { FilePickerFileTypes.All },
            });
            if (files.Count == 0)
                return;

            string target = Path.Combine(Paths.Modifications, row.Path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await using (var source = await files[0].OpenReadAsync())
            await using (var output = File.Create(target + ".bloxcode-tmp"))
                await source.CopyToAsync(output);
            File.Move(target + ".bloxcode-tmp", target, overwrite: true);

            if (!string.Equals(Path.GetExtension(files[0].Name), extension, StringComparison.OrdinalIgnoreCase))
                Logger.Warn("ModsPage::Replace", $"{files[0].Name} replaces {row.Path} with a different file type");
            Logger.Info("ModsPage::Replace", $"{files[0].Name} -> {row.Path}");
        }
        catch (Exception ex)
        {
            Logger.Error("ModsPage::Replace", ex);
            await MessageDialog.ShowAsync(_window, "Couldn't add that mod", ex.Message);
            return;
        }

        ApplyToSober();
    }

    private async void OnRestoreSelected(object? sender, RoutedEventArgs e)
    {
        if (AssetList.SelectedItem is AssetRow { IsModded: true } row)
            await RemoveModAsync(row.Path);
    }

    // ===== Applied mods =====

    private void RefreshMods()
    {
        var mods = ModSync.ListMods();
        ModList.ItemsSource = mods;
        EmptyText.IsVisible = mods.Count == 0;
        RemoveAllButton.IsEnabled = mods.Count > 0;
        CountText.Text = mods.Count switch
        {
            0 => "Your mods",
            1 => "1 mod",
            _ => $"{mods.Count} mods",
        };
    }

    /// <summary>Pushes the Modifications folder into Sober now, so a plain Sober start has the mods too.</summary>
    private void ApplyToSober()
    {
        try
        {
            var result = ModSync.Sync();
            SyncText.Text = result.Failed.Count > 0
                ? $"{result.Failed.Count} file(s) couldn't be copied into Sober (is Roblox open?). Blox Code retries on the next launch."
                : "Applied to Sober. Restart Roblox if it's open.";
        }
        catch (Exception ex)
        {
            Logger.Error("ModsPage::ApplyToSober", ex);
            SyncText.Text = $"Couldn't apply to Sober yet: {ex.Message}";
        }
        RefreshMods();
        ShowAssets();
    }

    private async Task RemoveModAsync(string rel)
    {
        try
        {
            string file = Path.Combine(Paths.Modifications, rel.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(file))
                File.Delete(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await MessageDialog.ShowAsync(_window, "Couldn't remove that mod", ex.Message);
            return;
        }
        ApplyToSober();
    }

    private async void OnRemoveMod(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string rel })
            await RemoveModAsync(rel);
    }

    private async void OnRemoveAll(object? sender, RoutedEventArgs e)
    {
        var mods = ModSync.ListMods();
        if (mods.Count == 0 || !await MessageDialog.AskAsync(_window, $"Remove all {mods.Count} mods?",
                "Your files are deleted from the Modifications folder and Roblox's originals come back.", "Remove all", "Cancel", danger: true))
            return;

        foreach (string rel in mods)
        {
            try { File.Delete(Path.Combine(Paths.Modifications, rel.Replace('/', Path.DirectorySeparatorChar))); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Logger.Warn("ModsPage::RemoveAll", $"{rel}: {ex.Message}"); }
        }
        ApplyToSober();
    }

    private void OnOpenFolder(object? sender, RoutedEventArgs e) => Shell.Open(Paths.Modifications, _window);
}
