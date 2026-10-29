using Avalonia.Controls;
using Avalonia.Interactivity;
using BloxCode.Core;

namespace BloxCode.Linux.UI.Pages;

public partial class AppearancePage : UserControl
{
    private readonly MainWindow _window;
    private bool _syncing = true;

    public AppearancePage(MainWindow window)
    {
        _window = window;
        InitializeComponent();
        ThemeSystem.IsChecked = Store.Settings.Theme == AppTheme.System;
        ThemeLight.IsChecked = Store.Settings.Theme == AppTheme.Light;
        ThemeDark.IsChecked = Store.Settings.Theme == AppTheme.Dark;
        AnimateMascot.IsChecked = Store.Settings.AnimateMascot;
        Mascot.IsAnimated = Store.Settings.AnimateMascot;
        _syncing = false;
    }

    private void OnThemeChanged(object? sender, RoutedEventArgs e)
    {
        if (_syncing || sender is not RadioButton { IsChecked: true } picked)
            return;
        Store.Settings.Theme = picked == ThemeLight ? AppTheme.Light : picked == ThemeDark ? AppTheme.Dark : AppTheme.System;
        App.ApplyTheme(Store.Settings.Theme);
        Store.SaveSettings();
    }

    private void OnAnimateChanged(object? sender, RoutedEventArgs e)
    {
        if (_syncing)
            return;
        Store.Settings.AnimateMascot = AnimateMascot.IsChecked == true;
        Mascot.IsAnimated = Store.Settings.AnimateMascot;
        _window.RefreshMascots();
        Store.SaveSettings();
    }

    private async void OnPreview(object? sender, RoutedEventArgs e) => await LaunchWindow.Preview().ShowDialog(_window);
}
