using System.Windows;
using System.Windows.Input;

namespace BloxCode.UI;

public partial class MessageDialog : Window
{
    private MessageDialog(Window? owner, string title, string message, string primary, string? secondary, bool danger)
    {
        InitializeComponent();
        ThemeManager.ApplyChrome(this);

        if (owner is { IsVisible: true })
        {
            Owner = owner;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }

        TitleText.Text = title;
        MessageText.Text = message;
        PrimaryButton.Content = primary;
        Mascot.Mood = danger ? ClawdMood.Sad : ClawdMood.Idle;
        if (danger)
        {
            PrimaryButton.SetResourceReference(BackgroundProperty, "Brush.Danger");
            PrimaryButton.SetResourceReference(TagProperty, "Brush.Danger");
        }

        if (secondary is null)
            SecondaryButton.Visibility = Visibility.Collapsed;
        else
            SecondaryButton.Content = secondary;
    }

    public static void Show(Window? owner, string title, string message) =>
        new MessageDialog(owner, title, message, "OK", null, false).ShowDialog();

    public static bool Ask(Window? owner, string title, string message, string yes, string no, bool danger = false) =>
        new MessageDialog(owner, title, message, yes, no, danger).ShowDialog() == true;

    private void OnPrimary(object sender, RoutedEventArgs e) => DialogResult = true;
    private void OnSecondary(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }
}
