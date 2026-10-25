using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace BloxCode.UI;

public enum ClawdMood { Idle, Walk, Work, Happy, Sad }

/// <summary>
/// Clawd, the Claude Code mascot, drawn as pixel art. The grid comes from the half-block banner
/// Claude Code prints on start-up:
/// <code>
///  ▐▛███▜▌
/// ▝▜█████▛▘
///   ▘▘ ▝▝
/// </code>
/// which unpacks to 16 columns x 5 rows of cells that are one unit wide and two units tall.
/// </summary>
public sealed class ClawdMascot : FrameworkElement
{
    private const int Columns = 16;
    private const double RowsTall = 10;   // 5 rows x 2 units
    private const double Headroom = 3;    // spare units above for jumping / raised arms

    private static readonly int[] Eyes = { 4, 11 };
    private static readonly int[] Legs = { 3, 5, 10, 12 };

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(130) };
    private int _frame;

    public ClawdMascot()
    {
        RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
        SnapsToDevicePixels = true;
        _timer.Tick += (_, _) => { _frame++; InvalidateVisual(); };
        Loaded += (_, _) => UpdateTimer();
        Unloaded += (_, _) => _timer.Stop();
    }

    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(
        nameof(Unit), typeof(double), typeof(ClawdMascot),
        new FrameworkPropertyMetadata(5.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Width of one pixel column, in DIPs.</summary>
    public double Unit
    {
        get => (double)GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    public static readonly DependencyProperty MoodProperty = DependencyProperty.Register(
        nameof(Mood), typeof(ClawdMood), typeof(ClawdMascot),
        new FrameworkPropertyMetadata(ClawdMood.Idle, FrameworkPropertyMetadataOptions.AffectsRender,
            (d, _) => ((ClawdMascot)d)._frame = 0));

    public ClawdMood Mood
    {
        get => (ClawdMood)GetValue(MoodProperty);
        set => SetValue(MoodProperty, value);
    }

    public static readonly DependencyProperty IsAnimatedProperty = DependencyProperty.Register(
        nameof(IsAnimated), typeof(bool), typeof(ClawdMascot),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((ClawdMascot)d).UpdateTimer()));

    public bool IsAnimated
    {
        get => (bool)GetValue(IsAnimatedProperty);
        set => SetValue(IsAnimatedProperty, value);
    }

    public static readonly DependencyProperty BodyProperty = DependencyProperty.Register(
        nameof(Body), typeof(Brush), typeof(ClawdMascot),
        new FrameworkPropertyMetadata(Freeze(new SolidColorBrush(Color.FromRgb(0xD7, 0x77, 0x57))), FrameworkPropertyMetadataOptions.AffectsRender));

    public Brush Body
    {
        get => (Brush)GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }

    public static readonly DependencyProperty EyeProperty = DependencyProperty.Register(
        nameof(Eye), typeof(Brush), typeof(ClawdMascot),
        new FrameworkPropertyMetadata(Freeze(new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x13))), FrameworkPropertyMetadataOptions.AffectsRender));

    public Brush Eye
    {
        get => (Brush)GetValue(EyeProperty);
        set => SetValue(EyeProperty, value);
    }

    /// <summary>Plays a short hop regardless of the current mood.</summary>
    public void Celebrate()
    {
        Mood = ClawdMood.Happy;
        _frame = 0;
        InvalidateVisual();
    }

    private void UpdateTimer()
    {
        if (IsAnimated && IsLoaded)
            _timer.Start();
        else
            _timer.Stop();
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(Columns * Unit, (RowsTall + Headroom) * Unit);

    protected override void OnRender(DrawingContext dc)
    {
        double u = Unit;
        int f = IsAnimated ? _frame : 0;
        var mood = Mood;

        // Vertical offset of the whole body, in units (negative = up).
        double bob = mood switch
        {
            ClawdMood.Idle => f / 6 % 2 == 0 ? 0 : 0.5,
            ClawdMood.Walk or ClawdMood.Work => f % 2 == 0 ? 0 : -0.5,
            ClawdMood.Happy => new[] { 0, -1.5, -2.5, -3, -2.5, -1.5, 0, 0 }[f % 8],
            _ => 0.5,
        };

        bool leftArmUp = mood switch
        {
            ClawdMood.Work => f % 2 == 0,
            ClawdMood.Happy => true,
            _ => false,
        };
        bool rightArmUp = mood switch
        {
            ClawdMood.Work => f % 2 == 1,
            ClawdMood.Happy => true,
            _ => false,
        };

        double top = (Headroom + bob) * u;
        Rect Cell(double col, double unitsFromTop, double w, double h) =>
            new(col * u, top + unitsFromTop * u, w * u, h * u);

        // Torso: columns 2..13, rows 0..3.
        dc.DrawRectangle(Body, null, Cell(2, 0, 12, 8));

        // Arms stick out of row 2; when raised they move up one row.
        dc.DrawRectangle(Body, null, Cell(0, leftArmUp ? 2 : 4, 2, 2));
        dc.DrawRectangle(Body, null, Cell(14, rightArmUp ? 2 : 4, 2, 2));

        // Legs hang below the torso. Walking lifts alternate pairs; the body bob keeps feet planted.
        for (int i = 0; i < Legs.Length; i++)
        {
            bool lifted = (mood is ClawdMood.Walk or ClawdMood.Work) && (i % 2 == f % 2);
            double length = lifted ? 1.5 : 2 - bob;
            if (mood == ClawdMood.Happy && bob < 0)
                length = 1.5;
            dc.DrawRectangle(Body, null, Cell(Legs[i], 8, 1, Math.Max(1, length)));
        }

        // Eyes punched into row 1. Blink every few seconds; look down when sad; squint when happy.
        bool blink = IsAnimated && mood != ClawdMood.Happy && f % 28 == 27;
        foreach (int col in Eyes)
        {
            Rect eye = mood switch
            {
                _ when blink => Cell(col, 3.4, 1, 0.6),
                ClawdMood.Sad => Cell(col, 3, 1, 1),
                ClawdMood.Happy => Cell(col, 2, 1, 1),
                _ => Cell(col, 2, 1, 2),
            };
            dc.DrawRectangle(Eye, null, eye);
        }
    }

    private static Brush Freeze(Brush b)
    {
        b.Freeze();
        return b;
    }
}
