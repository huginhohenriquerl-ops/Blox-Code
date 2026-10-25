using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using BloxCode.Core;
using BloxCode.UI;

namespace BloxCode;

public partial class App : Application
{
    public static readonly HttpClient Http = CreateHttpClient();

    public static string Version => typeof(App).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Start-up dialogs must not count as "the last window" and end the app early.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        Paths.EnsureCreated();
        Logger.Initialize();
        Logger.Info("App::OnStartup", $"Blox Code {Version} - args: {string.Join(' ', e.Args)}");

        DispatcherUnhandledException += OnUnhandledException;

        Store.Load();
        ThemeManager.Apply(Store.Settings.Theme);

        var launch = LaunchSettings.Parse(e.Args);

        if (launch.Upgrade)
        {
            // We are the new version, started from Temp\Updates: swap ourselves in and hand over.
            if (!Updater.ApplyUpgrade(launch.ForwardArgs))
                MessageDialog.Show(null, "Update didn't finish", $"Blox Code couldn't replace its installed copy. It will try again next time.\n\nLog: {Logger.FilePath}");
            Shutdown();
            return;
        }

        Updater.CleanupUpdatesFolder();

        if (await FindUpdateAsync(launch) is { } update)
        {
            BootstrapperWindow.ForUpdate(update, launch.ForwardArgs).Show();
            ShutdownMode = ShutdownMode.OnLastWindowClose;
            return;
        }

        switch (launch.Mode)
        {
            case LaunchMode.Player:
                if (Store.Settings.ConfirmLaunches && !launch.Quiet &&
                    !MessageDialog.Ask(null, "Launch Roblox?", "Something asked Blox Code to start Roblox.", "Launch", "Not now"))
                {
                    Shutdown();
                    return;
                }
                new BootstrapperWindow(launch).Show();
                break;

            case LaunchMode.Uninstall:
                if (launch.Quiet || MessageDialog.Ask(null, "Uninstall Blox Code?",
                        "This removes Blox Code, its shortcuts and the Roblox files it downloaded.", "Uninstall", "Cancel", danger: true))
                {
                    Installer.Uninstall();
                }
                Shutdown();
                break;

            default:
                new MainWindow().Show();
                break;
        }

        ShutdownMode = ShutdownMode.OnLastWindowClose;
    }

    /// <summary>
    /// Every start of the installed copy asks the release feed for something newer. The check is
    /// capped at a few seconds so a slow network never holds up a game launch.
    /// </summary>
    private static async Task<UpdateInfo?> FindUpdateAsync(LaunchSettings launch)
    {
        if (launch.NoUpdate || launch.Mode == LaunchMode.Uninstall || !Store.Settings.AutoUpdate
            || !Paths.IsRunningInstalled || !Updater.IsConfigured)
            return null;

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            return await Updater.CheckAsync(Http, cts.Token);
        }
        catch (Exception ex)
        {
            Logger.Warn("App::FindUpdate", $"Update check failed: {ex.Message}");
            return null;
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var http = new HttpClient(new SocketsHttpHandler { AutomaticDecompression = System.Net.DecompressionMethods.All })
        {
            Timeout = TimeSpan.FromMinutes(10),
        };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"BloxCode/{Version}");
        return http;
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Logger.Error("App::OnUnhandledException", e.Exception);
        e.Handled = true;
        MessageDialog.Show(null, "Something went wrong", e.Exception.Message + $"\n\nLog: {Logger.FilePath}");
    }
}
