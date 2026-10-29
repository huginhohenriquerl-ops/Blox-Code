using Avalonia;
using BloxCode.Core;
using BloxCode.Linux.Platform;

namespace BloxCode.Linux;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // Headless checks for CI and for the remote test PC: no display needed.
        if (args.Contains("--self-test") && !args.Contains("--ui"))
            return SelfTest.Run();

        try
        {
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, Avalonia.Controls.ShutdownMode.OnExplicitShutdown);
        }
        catch (Exception ex)
        {
            // Usually no display, or a missing system library; there is no window to show it in.
            Logger.Error("Program::Main", ex);
            Console.Error.WriteLine($"Blox Code couldn't start: {ex.Message}");
            if (Logger.FilePath is { } log)
                Console.Error.WriteLine($"Log: {log}");
            return 1;
        }
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .With(new X11PlatformOptions { WmClass = DesktopIntegration.WmClass })
        .LogToTrace();
}
