using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using BloxCode.Core;
using BloxCode.Linux.Platform;

namespace BloxCode.Linux.UI.Pages;

public partial class AboutPage : UserControl
{
    public AboutPage()
    {
        InitializeComponent();
        Mascot.IsAnimated = Store.Settings.AnimateMascot;
        VersionText.Text = $"v{Updater.Current} · {Distro.Current.Name}";
    }

    private async void OnPoke(object? sender, PointerPressedEventArgs e)
    {
        // One hop, then back to idle, like the Windows About page.
        Mascot.Celebrate();
        await Task.Delay(1050);
        Mascot.Mood = MascotMood.Idle;
    }

    private Window? Owner => TopLevel.GetTopLevel(this) as Window;

    private string RepoUrl => $"https://github.com/{Updater.Repository ?? "huginhohenriquerl-ops/blox-code"}";

    private void OnRepo(object? sender, RoutedEventArgs e) => Shell.Open(RepoUrl, Owner);

    private void OnChangelog(object? sender, RoutedEventArgs e) => Shell.Open($"{RepoUrl}/blob/main/CHANGELOG.md", Owner);

    private void OnSober(object? sender, RoutedEventArgs e) => Shell.Open("https://sober.vinegarhq.org/", Owner);
}
