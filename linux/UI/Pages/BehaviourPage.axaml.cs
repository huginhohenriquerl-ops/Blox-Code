using Avalonia.Controls;
using Avalonia.Interactivity;
using BloxCode.Core;
using BloxCode.Linux.Platform;

namespace BloxCode.Linux.UI.Pages;

public partial class BehaviourPage : UserControl
{
    private readonly MainWindow _window;
    private bool _syncing = true;

    public BehaviourPage(MainWindow window)
    {
        _window = window;
        InitializeComponent();
        ConfirmLaunches.IsChecked = Store.Settings.ConfirmLaunches;
        AutoUpdate.IsChecked = Store.Settings.AutoUpdate;
        CheckNowButton.IsEnabled = Updater.IsConfigured;
        _syncing = false;
        RefreshInstall();
    }

    private void RefreshInstall()
    {
        bool installed = DesktopIntegration.IsInstalled;
        InstallText.Text = installed
            ? $"Installed at {Paths.Application}, with a menu entry and the roblox:// link handler."
            : DesktopIntegration.CanInstall
                ? "Not installed. Installing copies this AppImage to ~/.local/bin/bloxcode and adds the menu entry."
                : "This is a build folder, not the AppImage; install from BloxCode-x86_64.AppImage.";
        RegisterButton.IsEnabled = installed;
        ReinstallButton.Content = installed ? "Reinstall" : "Install";
        ReinstallButton.IsEnabled = DesktopIntegration.CanInstall;
        UninstallButton.IsEnabled = installed;
    }

    private void OnToggle(object? sender, RoutedEventArgs e)
    {
        if (_syncing)
            return;
        Store.Settings.ConfirmLaunches = ConfirmLaunches.IsChecked == true;
        Store.Settings.AutoUpdate = AutoUpdate.IsChecked == true;
        Store.SaveSettings();
    }

    private async void OnCheckNow(object? sender, RoutedEventArgs e)
    {
        CheckNowButton.IsEnabled = false;
        UpdateText.Text = "Checking…";
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var update = await Updater.CheckAsync(App.Http, cts.Token);
            if (update is null)
            {
                UpdateText.Text = $"You're on the latest version (v{Updater.Current}).";
            }
            else if (!Paths.IsRunningInstalled)
            {
                UpdateText.Text = $"v{update.Version} is out. Install Blox Code first; the installed copy updates itself.";
            }
            else if (await MessageDialog.AskAsync(_window, $"Update to v{update.Version}?", "Blox Code downloads the new AppImage, replaces itself and opens the menu again.", "Update", "Later"))
            {
                App.Show(LaunchWindow.ForUpdate(update, new[] { "-menu" }));
                _window.Close();
                return;
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("BehaviourPage::CheckNow", ex.Message);
            UpdateText.Text = $"Couldn't check: {ex.Message}";
        }
        CheckNowButton.IsEnabled = true;
    }

    private async void OnRegister(object? sender, RoutedEventArgs e)
    {
        try
        {
            await DesktopIntegration.RegisterSchemesAsync();
            await MessageDialog.ShowAsync(_window, "Done", "roblox:// links open Blox Code now.");
        }
        catch (Exception ex)
        {
            Logger.Error("BehaviourPage::Register", ex);
            await MessageDialog.ShowAsync(_window, "Couldn't finish", ex.Message);
        }
    }

    private async void OnReinstall(object? sender, RoutedEventArgs e)
    {
        try
        {
            await DesktopIntegration.InstallAsync(App.Icon);
            await MessageDialog.ShowAsync(_window, "Installed", "The menu entry, icon and link handler are in place.");
        }
        catch (Exception ex)
        {
            Logger.Error("BehaviourPage::Reinstall", ex);
            await MessageDialog.ShowAsync(_window, "Couldn't finish", ex.Message);
        }
        RefreshInstall();
        _window.RefreshFooter();
    }

    private async void OnUninstall(object? sender, RoutedEventArgs e)
    {
        if (!await MessageDialog.AskAsync(_window, "Uninstall Blox Code?",
                "This removes Blox Code, its menu entry, its settings and its mods from Sober. Sober itself, its settings and your Fast Flags in it stay.",
                "Uninstall", "Cancel", danger: true))
            return;

        try
        {
            await DesktopIntegration.UninstallAsync();
        }
        catch (Exception ex)
        {
            Logger.Error("BehaviourPage::Uninstall", ex);
        }
        await MessageDialog.ShowAsync(_window, "Uninstalled", "Blox Code is gone. Thanks for playing with Bit!");
        App.Exit();
    }

    private async void OnExportDiagnostics(object? sender, RoutedEventArgs e)
    {
        DiagnosticsText.Text = "Collecting…";
        try
        {
            string path = await Diagnostics.ExportAsync();
            DiagnosticsText.Text = $"Saved {path}";
            Shell.Open(Path.GetDirectoryName(path)!, _window);
        }
        catch (Exception ex)
        {
            Logger.Error("BehaviourPage::ExportDiagnostics", ex);
            DiagnosticsText.Text = $"Couldn't export: {ex.Message}";
        }
    }

    private void OnOpenLogs(object? sender, RoutedEventArgs e) => Shell.Open(Paths.Logs, _window);
}
