using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Interactivity;
using BloxCode.Core;
using BloxCode.Linux.Runtime;

namespace BloxCode.Linux.UI.Pages;

/// <summary>Sober's own options with plain explanations, written into its config.json.</summary>
public partial class SoberPage : UserControl
{
    private static readonly (string Label, string Value)[] GraphicsModes =
        { ("Quality", "quality"), ("Balanced", "balanced"), ("Performance", "performance") };

    private readonly MainWindow _window;
    private readonly List<(CheckBox Box, bool Default)> _toggles;
    private bool _syncing;

    public SoberPage(MainWindow window)
    {
        _window = window;
        InitializeComponent();
        ConfigPath.Text = Sober.ConfigFile;

        var defaults = SoberConfig.Defaults();
        _toggles = new[] { UseOpenGl, HiDpi, GameMode, CloseOnLeave, ServerLocation, Discord, DiscordJoin, Gamepad }
            .Select(box => (box, SoberConfig.GetBool(defaults, (string)box.Tag!, false)))
            .ToList();

        foreach (var (box, _) in _toggles)
            box.IsCheckedChanged += (_, _) =>
            {
                if (!_syncing)
                    Save(root => root[(string)box.Tag!] = box.IsChecked == true);
            };

        foreach (var (label, value) in GraphicsModes)
        {
            var segment = new RadioButton { Content = label, GroupName = "graphics" + GetHashCode(), Tag = value };
            segment.Theme = Res.Theme("Segment");
            segment.IsCheckedChanged += (_, _) =>
            {
                if (!_syncing && segment.IsChecked == true)
                    Save(root => root["graphics_optimization_mode"] = value);
            };
            GraphicsSegments.Children.Add(segment);
        }

        Load();
    }

    private void Load()
    {
        JsonObject root;
        try
        {
            root = SoberConfig.Load();
            ErrorCard.IsVisible = false;
            Options.IsEnabled = true;
        }
        catch (SoberConfigException ex)
        {
            ErrorText.Text = ex.Message;
            ErrorCard.IsVisible = true;
            Options.IsEnabled = false;
            root = SoberConfig.Defaults();
        }

        _syncing = true;
        foreach (var (box, fallback) in _toggles)
            box.IsChecked = SoberConfig.GetBool(root, (string)box.Tag!, fallback);

        string mode = SoberConfig.GetString(root, "graphics_optimization_mode", "balanced");
        foreach (var segment in GraphicsSegments.Children.OfType<RadioButton>())
            segment.IsChecked = (string?)segment.Tag == mode;
        _syncing = false;

        RestoreButton.IsEnabled = SoberConfig.HasBackup;
        RestoreText.Text = SoberConfig.HasBackup
            ? "Blox Code saved Sober's settings before its first change. Restoring puts that file back; your Fast Flags stay in Blox Code and are written again on the next launch."
            : "Blox Code hasn't changed Sober's settings yet, so there is no backup. Resetting deletes config.json so Sober recreates its defaults.";
    }

    private void Save(Action<JsonObject> edit)
    {
        try
        {
            SoberConfig.Update(edit);
            SavedText.Text = Sober.IsRunning ? "Saved. Sober is open: restart it to see the change." : "Saved.";
        }
        catch (Exception ex)
        {
            Logger.Error("SoberPage::Save", ex);
            SavedText.Text = "";
            ErrorText.Text = ex.Message;
            ErrorCard.IsVisible = true;
        }
    }

    private async void OnRestore(object? sender, RoutedEventArgs e)
    {
        if (!await MessageDialog.AskAsync(_window, "Restore Sober's settings?", "Sober's config.json goes back to how it was before Blox Code changed it.", "Restore", "Cancel"))
            return;
        await Guard(SoberConfig.RestoreBackup);
        Load();
    }

    private async void OnReset(object? sender, RoutedEventArgs e)
    {
        if (!await MessageDialog.AskAsync(_window, "Reset Sober to defaults?",
                "Sober's config.json is deleted (Blox Code keeps a copy) and Sober recreates its defaults. Your Fast Flags stay in Blox Code and are written again on the next launch.",
                "Reset", "Cancel", danger: true))
            return;
        await Guard(SoberConfig.Reset);
        Load();
    }

    private async void OnClearOverlay(object? sender, RoutedEventArgs e)
    {
        if (!await MessageDialog.AskAsync(_window, "Take the mods out of Sober?",
                "Roblox's original files come back. Your mods stay in Blox Code's Modifications folder and are applied again on the next launch through Blox Code — remove them on the Mods page to keep them out.",
                "Take them out", "Cancel", danger: true))
            return;
        await Guard(() => ModSync.RemoveAllFromSober());
        SavedText.Text = "Blox Code's mods are out of Sober.";
    }

    private async Task Guard(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Logger.Error("SoberPage", ex);
            await MessageDialog.ShowAsync(_window, "Couldn't finish", ex.Message);
        }
    }

    private async void OnOpenSoberSettings(object? sender, RoutedEventArgs e)
    {
        if (!Sober.OpenOwnSettings())
            await MessageDialog.ShowAsync(_window, "Couldn't open Sober", "Sober or Flatpak isn't installed. The Home page shows how to install it.");
    }
}
