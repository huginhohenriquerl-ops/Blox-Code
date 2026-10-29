using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace BloxCode.UI;

/// <summary>Blox Code's working indicator: a block circling a square (▖ ▌ ▘ ▀ ▝ ▐ ▗ ▄).</summary>
public sealed class BlockSpinner : TextBlock
{
    private static readonly string[] Frames = { "▖", "▌", "▘", "▀", "▝", "▐", "▗", "▄" };
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(110) };
    private int _frame;

    public BlockSpinner()
    {
        Text = "■";
        FontFamily = new System.Windows.Media.FontFamily("Cascadia Mono, Consolas");
        _timer.Tick += (_, _) => Text = Frames[++_frame % Frames.Length];
        Loaded += (_, _) => Sync();
        Unloaded += (_, _) => _timer.Stop();
    }

    public static readonly DependencyProperty IsSpinningProperty = DependencyProperty.Register(
        nameof(IsSpinning), typeof(bool), typeof(BlockSpinner),
        new PropertyMetadata(true, (d, _) => ((BlockSpinner)d).Sync()));

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
            Text = "■";
        }
    }
}
