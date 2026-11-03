using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BloxCode.Core;
using Microsoft.Win32;

namespace BloxCode.UI.Pages;

/// <summary>
/// Pick-a-file mods (cursor, sounds, game font) on top, the raw Modifications folder below. The
/// slots write into the same folder, so everything still goes through the bootstrapper's mod step.
/// </summary>
public partial class ModsPage : UserControl
{
    private readonly MainWindow _window;

    public ModsPage(MainWindow window)
    {
        _window = window;
        InitializeComponent();
        FolderPath.Text = Paths.Modifications;
        Refresh();
    }

    private void Refresh()
    {
        BuildSlots();

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
        CountText.Text = mods.Count switch
        {
            0 => "No files yet.",
            1 => "1 file, including the ones picked above:",
            _ => $"{mods.Count} files, including the ones picked above:",
        };
    }

    // ===== Slots =====

    private void BuildSlots()
    {
        Groups.Children.Clear();
        foreach (var group in ModCatalog.Groups)
        {
            var rows = new StackPanel();
            rows.Children.Add(new TextBlock { Text = group.Key, Margin = new Thickness(0, 0, 0, 6) }.Styled("Text.H2"));
            bool first = true;
            foreach (var slot in group)
            {
                if (!first)
                    rows.Children.Add(new Border().Styled("Divider"));
                rows.Children.Add(SlotRow(slot));
                first = false;
            }
            Groups.Children.Add(new Border { Child = rows }.Styled("Card"));
        }
    }

    private FrameworkElement SlotRow(ModSlot slot)
    {
        string? current = ModCatalog.CurrentFile(slot);

        var choose = new Button { Content = current is null ? "Choose file…" : "Change…", Margin = new Thickness(6, 0, 0, 0) }.Styled("Button.Secondary");
        choose.Click += (_, _) => Pick(slot);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        if (current is not null)
        {
            var reset = new Button { Content = "Reset" }.Styled("Button.Ghost");
            reset.Click += (_, _) => Run(() => ModCatalog.Reset(slot));
            buttons.Children.Add(reset);
        }
        buttons.Children.Add(choose);
        DockPanel.SetDock(buttons, Dock.Right);

        var status = new TextBlock { Margin = new Thickness(0, 3, 0, 0), FontSize = 12 };
        if (current is null)
        {
            status.Text = "Roblox default";
            status.SetResourceReference(TextBlock.ForegroundProperty, "Brush.TextMuted");
        }
        else
        {
            var info = new FileInfo(current);
            status.Text = $"Custom · {Size(info.Length)} · changed {info.LastWriteTime:d MMM}";
            status.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Accent");
        }

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
        text.Children.Add(new TextBlock { Text = slot.Title, FontWeight = FontWeights.SemiBold });
        text.Children.Add(new TextBlock { Text = slot.Description }.Styled("Text.Muted"));
        text.Children.Add(status);

        var preview = Preview(slot, current);
        DockPanel.SetDock(preview, Dock.Left);

        var row = new DockPanel { Margin = new Thickness(0, 4, 0, 4), Background = Brushes.Transparent, AllowDrop = true, ToolTip = $"{slot.FileTypes} · drop a file here" };
        row.Children.Add(buttons);
        row.Children.Add(preview);
        row.Children.Add(text);

        // Drag a file from Explorer straight onto the row.
        row.DragOver += (_, e) =>
        {
            e.Effects = DroppedFile(e) is { } f && ModCatalog.Accepts(slot, f) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        };
        row.Drop += (_, e) =>
        {
            if (DroppedFile(e) is { } file)
                Run(() => ModCatalog.Apply(slot, file));
        };
        return row;
    }

    /// <summary>A 44 px tile: the cursor image itself, "Aa" in the chosen font, or a note for sounds.</summary>
    private static FrameworkElement Preview(ModSlot slot, string? current)
    {
        var tile = new Border { Width = 44, Height = 44, CornerRadius = new CornerRadius(10), Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center };
        tile.SetResourceReference(Border.BackgroundProperty, current is null ? "Brush.SurfaceAlt" : "Brush.AccentSoft");

        UIElement content = slot.Kind switch
        {
            ModKind.Image when LoadImage(current ?? RobloxOriginal(slot)) is { } image =>
                new Image { Source = image, Width = 32, Height = 32, Stretch = Stretch.Uniform },
            ModKind.Font => FontSample(current),
            ModKind.Sound => new TextBlock { Text = "", FontSize = 18 }.Styled("Text.Icon"),
            _ => new TextBlock { Text = "", FontSize = 18 }.Styled("Text.Icon"),
        };
        if (content is FrameworkElement element)
        {
            element.HorizontalAlignment = HorizontalAlignment.Center;
            element.VerticalAlignment = VerticalAlignment.Center;
        }
        if (content is TextBlock glyph)
            glyph.SetResourceReference(TextBlock.ForegroundProperty, current is null ? "Brush.TextMuted" : "Brush.Accent");
        tile.Child = content;
        return tile;
    }

    /// <summary>Roblox's own file for the slot, from the installed version (its backup once a mod replaced it).</summary>
    private static string? RobloxOriginal(ModSlot slot)
    {
        if (Store.State.VersionGuid is not { } guid)
            return null;
        string rel = slot.Targets[0].Replace('/', '\\');
        string version = Path.Combine(Paths.Versions, guid);
        string backup = Path.Combine(version, ".bloxcode-originals", rel);
        string file = Path.Combine(version, rel);
        return File.Exists(backup) ? backup : File.Exists(file) ? file : null;
    }

    private static BitmapImage? LoadImage(string? path)
    {
        if (path is null || !File.Exists(path))
            return null;
        try
        {
            // OnLoad reads the whole file now, so the mod can still be replaced or deleted.
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            image.UriSource = new Uri(path);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or InvalidOperationException)
        {
            return null;
        }
    }

    private static TextBlock FontSample(string? font)
    {
        var sample = new TextBlock { Text = "Aa", FontSize = 18, FontWeight = FontWeights.SemiBold };
        if (font is null)
            return sample;
        try
        {
            // WPF keeps font files open, so it reads a throwaway copy rather than the mod itself.
            string dir = Path.Combine(Paths.Cache, "font-preview", DateTime.UtcNow.Ticks.ToString());
            Directory.CreateDirectory(dir);
            string copy = Path.Combine(dir, "preview" + Path.GetExtension(font));
            File.Copy(font, copy);
            var face = new GlyphTypeface(new Uri(copy));
            string family = face.FamilyNames.Values.FirstOrDefault() ?? "";
            sample.FontFamily = new FontFamily(new Uri(dir + "\\"), "./#" + family);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FileFormatException or ArgumentException)
        {
            Logger.Warn("ModsPage::FontSample", ex.Message);
        }
        return sample;
    }

    private static string? DroppedFile(DragEventArgs e) =>
        e.Data.GetData(DataFormats.FileDrop) is string[] { Length: 1 } files && File.Exists(files[0]) ? files[0] : null;

    private void Pick(ModSlot slot)
    {
        string patterns = string.Join(";", slot.Extensions.Select(x => "*" + x));
        var picker = new OpenFileDialog { Title = $"Choose a file for {slot.Title}", Filter = $"{slot.FileTypes}|{patterns}" };
        if (picker.ShowDialog(_window) == true)
            Run(() => ModCatalog.Apply(slot, picker.FileName));
    }

    private void Run(Action change)
    {
        try
        {
            change();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Logger.Warn("ModsPage::Run", ex.Message);
            MessageDialog.Show(_window, "Couldn't change that mod", ex.Message);
        }
        Refresh();
    }

    private static string Size(long bytes) => bytes < 1024 * 1024 ? $"{Math.Max(1, bytes / 1024)} KB" : $"{bytes / (1024.0 * 1024):0.#} MB";

    private void OnRefresh(object sender, RoutedEventArgs e) => Refresh();

    private void OnOpenFolder(object sender, RoutedEventArgs e) =>
        Shell.Open(Paths.Modifications, Window.GetWindow(this));
}

internal static class StyleExtensions
{
    /// <summary>Applies a style from the app's resources to a control built in code.</summary>
    public static T Styled<T>(this T element, string key) where T : FrameworkElement
    {
        element.SetResourceReference(FrameworkElement.StyleProperty, key);
        return element;
    }
}
