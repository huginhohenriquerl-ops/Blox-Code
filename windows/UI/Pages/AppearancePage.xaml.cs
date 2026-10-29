using System.Windows;
using System.Windows.Controls;
using BloxCode.Core;

namespace BloxCode.UI.Pages;

public partial class AppearancePage : UserControl
{
    private readonly MainWindow _window;
    private readonly bool _ready;

    public AppearancePage(MainWindow window)
    {
        _window = window;
        InitializeComponent();

        (Store.Settings.Theme switch
        {
            AppTheme.Light => ThemeLight,
            AppTheme.Dark => ThemeDark,
            _ => ThemeSystem,
        }).IsChecked = true;
        Animate.IsChecked = Store.Settings.AnimateMascot;
        Mascot.IsAnimated = Store.Settings.AnimateMascot;
        _ready = true;
    }

    private void OnThemeChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        Store.Settings.Theme = ThemeLight.IsChecked == true ? AppTheme.Light
            : ThemeDark.IsChecked == true ? AppTheme.Dark
            : AppTheme.System;
        ThemeManager.Apply(Store.Settings.Theme);
        Store.SaveSettings();
    }

    private void OnAnimateChanged(object sender, RoutedEventArgs e)
    {
        Store.Settings.AnimateMascot = Animate.IsChecked == true;
        Mascot.IsAnimated = Store.Settings.AnimateMascot;
        _window.RefreshMascots();
        Store.SaveSettings();
    }

    private void OnPreview(object sender, RoutedEventArgs e) => BootstrapperWindow.Preview(_window).ShowDialog();
}
