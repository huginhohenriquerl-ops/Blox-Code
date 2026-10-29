using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using BloxCode.Core;

namespace BloxCode.Linux.UI;

public partial class MessageDialog : Window
{
    private bool _result;

    public MessageDialog()
    {
        InitializeComponent();
    }

    private MessageDialog(string title, string message, string primary, string? secondary, bool danger, string[] commands) : this()
    {
        TitleText.Text = title;
        MessageText.Text = message;
        PrimaryButton.Content = primary;
        Mascot.Mood = danger ? MascotMood.Sad : MascotMood.Idle;
        Mascot.IsAnimated = Store.Settings.AnimateMascot;
        if (danger)
        {
            PrimaryButton.Classes.Remove("primary");
            PrimaryButton.Classes.Add("danger-fill");
        }

        if (secondary is null)
            SecondaryButton.IsVisible = false;
        else
            SecondaryButton.Content = secondary;

        foreach (string command in commands)
            Commands.Children.Add(UI.CommandRow.Create(command));

        Chrome.MakeDragArea(this, Body, maximizeOnDoubleClick: false);
        KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Escape)
                OnSecondary(this, new RoutedEventArgs());
        };
    }

    public static Task ShowAsync(Window? owner, string title, string message, params string[] commands) =>
        RunAsync(owner, new MessageDialog(title, message, "OK", null, false, commands));

    public static Task<bool> AskAsync(Window? owner, string title, string message, string yes, string no, bool danger = false) =>
        RunAsync(owner, new MessageDialog(title, message, yes, no, danger, Array.Empty<string>()));

    private static async Task<bool> RunAsync(Window? owner, MessageDialog dialog)
    {
        try
        {
            if (owner is { IsVisible: true })
            {
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                await dialog.ShowDialog(owner);
            }
            else
            {
                // Before any window exists (start-up errors, link confirmations) there is no owner.
                var closed = new TaskCompletionSource();
                dialog.Closed += (_, _) => closed.TrySetResult();
                dialog.Show();
                dialog.Activate();
                await closed.Task;
            }
        }
        catch (Exception ex)
        {
            Logger.Error("MessageDialog::Run", ex);
        }
        return dialog._result;
    }

    private void OnPrimary(object? sender, RoutedEventArgs e)
    {
        _result = true;
        Close();
    }

    private void OnSecondary(object? sender, RoutedEventArgs e)
    {
        _result = false;
        Close();
    }
}
