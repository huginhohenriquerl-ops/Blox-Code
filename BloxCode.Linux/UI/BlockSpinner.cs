using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace BloxCode.Linux.UI;

/// <summary>Blox Code's working indicator: a block circling a square (▖ ▌ ▘ ▀ ▝ ▐ ▗ ▄).</summary>
public sealed class BlockSpinner : TextBlock
{
    private static readonly string[] Frames = { "▖", "▌", "▘", "▀", "▝", "▐", "▗", "▄" };
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(110) };
    private int _frame;

    public static readonly StyledProperty<bool> IsSpinningProperty =
        AvaloniaProperty.Register<BlockSpinner, bool>(nameof(IsSpinning), true);

    protected override Type StyleKeyOverride => typeof(TextBlock);

    public BlockSpinner()
    {
        Text = "■";
        _timer.Tick += (_, _) => Text = Frames[++_frame % Frames.Length];
    }

    public bool IsSpinning { get => GetValue(IsSpinningProperty); set => SetValue(IsSpinningProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSpinningProperty)
            Sync();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Sync();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer.Stop();
    }

    private void Sync()
    {
        if (IsSpinning && VisualRoot is not null)
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
