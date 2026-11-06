using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using BloxCode.Core;
using BloxCode.Linux.Platform;
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

        foreach (string tab in ModCatalog.Groups.Select(g => g.Key).Prepend(AllTab).Append(AdvancedTab))
        {
            var segment = new RadioButton { Content = tab, GroupName = "modTabs" + GetHashCode(), IsChecked = tab == _tab, Theme = Res.Theme("Segment") };
            segment.IsCheckedChanged += (_, _) =>
            {
                if (segment.IsChecked != true) return;
                _tab = tab;
                BuildSlots();
            };
            Tabs.Children.Add(segment);
        }

        RefreshMods();
        AttachedToVisualTree += async (_, _) => await LoadAssetsAsync();
    }

    // ===== Pick-a-file slots =====

    private const string AllTab = "All", AdvancedTab = "Advanced";
    private string _tab = AllTab;

    private void BuildSlots()
    {
        Groups.IsVisible = _tab != AdvancedTab;
        AssetsCard.IsVisible = AppliedCard.IsVisible = _tab is AllTab or AdvancedTab;
        ResetAllButton.IsVisible = ModCatalog.Slots.Any(s => ModCatalog.CurrentFile(s) is not null);

        Groups.Children.Clear();
        foreach (var group in ModCatalog.Groups.Where(g => _tab == AllTab || g.Key == _tab))
        {
            var rows = new StackPanel();
            rows.Children.Add(new TextBlock { Text = group.Key, Classes = { "h2" }, Margin = new Thickness(0, 0, 0, 6) });
            bool first = true;
            foreach (var slot in group)
            {
                if (!first)
                    rows.Children.Add(new Border { Classes = { "divider" } });
                rows.Children.Add(SlotRow(slot));
                first = false;
            }
            Groups.Children.Add(new Border { Classes = { "card" }, Child = rows });
        }
    }

    private Control SlotRow(ModSlot slot)
    {
        string? current = ModCatalog.CurrentFile(slot);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        if (slot.Kind == ModKind.Sound)
        {
            var play = new Button { Padding = new Thickness(10, 8), Content = new IconView { Data = Res.Get<Geometry>("Icon.Play"), Filled = true, Width = 12, Height = 12 } };
            ToolTip.SetTip(play, current is null ? "Play Roblox's sound" : "Play your sound");
            play.Click += async (_, _) => await PlaySoundAsync(slot, current);
            buttons.Children.Add(play);
        }
        if (current is not null)
        {
            var reset = new Button { Content = "Reset" };
            reset.Click += (_, _) => Change(() => ModCatalog.Reset(slot), slot);
            buttons.Children.Add(reset);
        }
        var choose = new Button { Content = current is null ? "Choose file…" : "Change…", Classes = { "secondary" } };
        choose.Click += async (_, _) => await PickAsync(slot);
        buttons.Children.Add(choose);
        DockPanel.SetDock(buttons, Dock.Right);

        // Sober runs the Android client: say so when its package doesn't have this file at all.
        bool missing = _assets.Count > 0 && !_assets.Any(a => a.Path == slot.Targets[0]);
        var status = new TextBlock { FontSize = 12, Margin = new Thickness(0, 3, 0, 0), TextWrapping = TextWrapping.Wrap };
        if (current is null)
        {
            status.Text = missing ? "Roblox default · not in Roblox's Android files, so this may not change anything" : "Roblox default";
            Res.BindBrush(status, TextBlock.ForegroundProperty, missing ? "Brush.Warning" : "Brush.TextMuted");
        }
        else
        {
            var info = new FileInfo(current);
            status.Text = $"Custom · {FormatSize(info.Length)} · changed {info.LastWriteTime:d MMM}";
            Res.BindBrush(status, TextBlock.ForegroundProperty, "Brush.Accent");
        }

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
        text.Children.Add(new TextBlock { Text = slot.Title, FontWeight = FontWeight.SemiBold });
        text.Children.Add(new TextBlock { Text = slot.Description, Classes = { "muted" }, TextWrapping = TextWrapping.Wrap });
        text.Children.Add(status);

        var preview = Preview(slot, current);
        DockPanel.SetDock(preview, Dock.Left);

        var row = new DockPanel { Margin = new Thickness(0, 4), Background = Brushes.Transparent };
        ToolTip.SetTip(row, $"{slot.FileTypes} · drop a file here");
        row.Children.Add(buttons);
        row.Children.Add(preview);
        row.Children.Add(text);

        // Drag a file from the file manager straight onto the row.
        DragDrop.SetAllowDrop(row, true);
        row.AddHandler(DragDrop.DragOverEvent, (_, e) =>
        {
            e.DragEffects = DroppedFile(e) is { } f && ModCatalog.Accepts(slot, f) ? DragDropEffects.Copy : DragDropEffects.None;
        });
        row.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            if (DroppedFile(e) is { } file)
                Change(() => ModCatalog.Apply(slot, file), slot);
        });
        return row;
    }

    /// <summary>A 44 px tile: the chosen cursor image, "Aa" for the font, a speaker for sounds.</summary>
    private static Control Preview(ModSlot slot, string? current)
    {
        var tile = new Border { Width = 44, Height = 44, CornerRadius = new CornerRadius(10), Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center };
        Res.BindBrush(tile, Border.BackgroundProperty, current is null ? "Brush.SurfaceAlt" : "Brush.AccentSoft");
        string tint = current is null ? "Brush.TextMuted" : "Brush.Accent";

        Control content;
        if (slot.Kind == ModKind.Image && current is not null && LoadBitmap(current) is { } bitmap)
        {
            content = new Image { Source = bitmap, Width = 32, Height = 32, Stretch = Stretch.Uniform };
        }
        else if (slot.Kind == ModKind.Font)
        {
            var sample = new TextBlock { Text = "Aa", FontSize = 18, FontWeight = FontWeight.SemiBold };
            Res.BindBrush(sample, TextBlock.ForegroundProperty, tint);
            content = sample;
        }
        else
        {
            var icon = new IconView { Data = Res.Get<Geometry>(slot.Kind == ModKind.Sound ? "Icon.Sound" : "Icon.File"), Width = 20, Height = 20 };
            Res.BindBrush(icon, IconView.ForegroundProperty, tint);
            content = icon;
        }
        content.HorizontalAlignment = HorizontalAlignment.Center;
        content.VerticalAlignment = VerticalAlignment.Center;
        tile.Child = content;
        return tile;
    }

    private static Bitmap? LoadBitmap(string path)
    {
        try
        {
            // Read into memory first so the mod file isn't held open.
            using var stream = new MemoryStream(File.ReadAllBytes(path));
            return new Bitmap(stream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// Plays the slot's sound (the custom one, or Roblox's own out of the APK) with whatever player
    /// the desktop has: PipeWire, PulseAudio or ffplay.
    /// </summary>
    private async Task PlaySoundAsync(ModSlot slot, string? current)
    {
        string? file = current;
        if (file is null)
        {
            if (ApkAssets.FindApk() is not { } apk)
            {
                await MessageDialog.ShowAsync(_window, "Nothing to play yet", "Roblox's own sounds show up here after Sober has run once.");
                return;
            }
            file = Path.Combine(Paths.Cache, "sound-preview", Path.GetFileName(slot.Targets[0]));
            try { await Task.Run(() => ApkAssets.Extract(apk, slot.Targets[0], file)); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                await MessageDialog.ShowAsync(_window, "Couldn't read Roblox's sound", ex.Message);
                return;
            }
        }

        bool started = Proc.Which("pw-play") is { } pw ? Proc.Detach(pw, file)
            : Proc.Which("paplay") is { } pa ? Proc.Detach(pa, file)
            : Proc.Which("ffplay") is { } ff && Proc.Detach(ff, "-nodisp", "-autoexit", "-loglevel", "quiet", file);
        if (!started)
            await MessageDialog.ShowAsync(_window, "No sound player found", "Blox Code plays previews with pw-play, paplay or ffplay; none of them is installed.");
    }

    private async void OnResetAll(object? sender, RoutedEventArgs e)
    {
        var swapped = ModCatalog.Slots.Where(s => ModCatalog.CurrentFile(s) is not null).ToList();
        if (swapped.Count == 0 || !await MessageDialog.AskAsync(_window, $"Reset {swapped.Count} mod(s)?",
                "Roblox's own cursor, sounds and font come back. Files you added in the advanced list stay.", "Reset all", "Cancel"))
            return;
        // Passing the font slot (when it's among them) makes Change refresh the font families too.
        Change(() => swapped.ForEach(s => ModCatalog.Reset(s)), swapped.FirstOrDefault(s => s.Kind == ModKind.Font) ?? swapped[0]);
    }

    private static string? DroppedFile(DragEventArgs e) =>
        e.DataTransfer.TryGetFiles() is [var item] && item.TryGetLocalPath() is { } path && File.Exists(path) ? path : null;

    private async Task PickAsync(ModSlot slot)
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null || !storage.CanOpen)
        {
            await MessageDialog.ShowAsync(_window, "No file picker here",
                $"Copy your file to this path instead, then come back:\n\n{Path.Combine(Paths.Modifications, slot.Targets[0])}");
            return;
        }

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = $"Choose a file for {slot.Title}",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType(slot.FileTypes) { Patterns = slot.Extensions.Select(x => "*" + x).ToArray() } },
        });
        if (files.Count == 0)
            return;

        // Portals can hand back a document without a local path: copy it out first.
        string? local = files[0].TryGetLocalPath();
        string? temp = null;
        if (local is null)
        {
            temp = Path.Combine(Paths.Cache, "picked" + Path.GetExtension(files[0].Name));
            Directory.CreateDirectory(Paths.Cache);
            await using (var source = await files[0].OpenReadAsync())
            await using (var output = File.Create(temp))
                await source.CopyToAsync(output);
            local = temp;
        }
        Change(() => ModCatalog.Apply(slot, local), slot);
        if (temp is not null)
            try { File.Delete(temp); } catch (IOException) { }
    }

    private async void Change(Action change, ModSlot slot)
    {
        try
        {
            change();
            if (slot.Kind == ModKind.Font)
                ApkAssets.RefreshCustomFont();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Logger.Warn("ModsPage::Change", ex.Message);
            await MessageDialog.ShowAsync(_window, "Couldn't change that mod", ex.Message);
        }
        ApplyToSober();
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
        BuildSlots();
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
        BuildSlots();
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
