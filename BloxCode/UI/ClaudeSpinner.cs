using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace BloxCode.UI;

/// <summary>The twinkling asterisk Claude Code shows while it is working (· ✢ ✳ ✶ ✻ ✽).</summary>
public sealed class ClaudeSpinner : TextBlock
{
    private static readonly string[] Frames = { "·", "✢", "✳", "✶", "✻", "✽", "✻", "✶", "✳", "✢" };
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private int _frame;

    public ClaudeSpinner()
    {
        Text = Frames[4];
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI Symbol");
        _timer.Tick += (_, _) => Text = Frames[++_frame % Frames.Length];
        Loaded += (_, _) => Sync();
        Unloaded += (_, _) => _timer.Stop();
    }

    public static readonly DependencyProperty IsSpinningProperty = DependencyProperty.Register(
        nameof(IsSpinning), typeof(bool), typeof(ClaudeSpinner),
        new PropertyMetadata(true, (d, _) => ((ClaudeSpinner)d).Sync()));

    public bool IsSpinning
    {
        get => (bool)GetValue(IsSpinningProperty);
        set => SetValue(IsSpinningProperty, value);
    }

    private void Sync()
    {
        if (IsSpinning && IsLoaded)
        {
            _timer.Start();
        }
        else
        {
            _timer.Stop();
            Text = "✻";
        }
    }
}
