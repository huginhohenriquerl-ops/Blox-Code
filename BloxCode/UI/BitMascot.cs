using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace BloxCode.UI;

public enum MascotMood { Idle, Walk, Work, Happy, Sad }

/// <summary>
/// Bit, Blox Code's mascot: a building block whose face is a terminal screen, with an antenna
/// spark that pulses like a signal. Drawn as pixel art on a 16 x 17 grid of square cells.
/// <list type="bullet">
/// <item>Idle — breathes, blinks, and the cursor mouth blinks like a prompt.</item>
/// <item>Walk — legs step in turn (used while downloading).</item>
/// <item>Work — arms type and the screen scrolls lines of code (used while unpacking).</item>
/// <item>Happy — hops with ^ ^ eyes. Sad — dim antenna, flat eyes, a small frown.</item>
/// </list>
/// </summary>
public sealed class BitMascot : FrameworkElement
{
    private const int Columns = 16;
    private const int Rows = 17;
    private const int Headroom = 3;   // spare cells above for the hop

    private static readonly Brush Glow = Frozen(Color.FromRgb(0xFF, 0xF1, 0xE6));
    private static readonly Brush Spark = Frozen(Color.FromRgb(0xFF, 0xC8, 0x5A));
    private static readonly Brush SparkDim = Frozen(Color.FromRgb(0x8A, 0x6A, 0x3A));

    // Code lines the screen scrolls through while working: (x, width).
    private static readonly (int X, int W)[] CodeLines = { (4, 5), (6, 3), (5, 6), (4, 4), (6, 2) };

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(130) };
    private int _frame;
    private Brush _top = Brushes.Transparent, _shade = Brushes.Transparent;

    public BitMascot()
    {
        RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
        SnapsToDevicePixels = true;
        DeriveShades();
        _timer.Tick += (_, _) => { _frame++; InvalidateVisual(); };
        Loaded += (_, _) => UpdateTimer();
        Unloaded += (_, _) => _timer.Stop();
    }

    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(
        nameof(Unit), typeof(double), typeof(BitMascot),
        new FrameworkPropertyMetadata(4.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Size of one pixel cell, in DIPs.</summary>
    public double Unit
    {
        get => (double)GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    public static readonly DependencyProperty MoodProperty = DependencyProperty.Register(
        nameof(Mood), typeof(MascotMood), typeof(BitMascot),
        new FrameworkPropertyMetadata(MascotMood.Idle, FrameworkPropertyMetadataOptions.AffectsRender,
            (d, _) => ((BitMascot)d)._frame = 0));

    public MascotMood Mood
    {
        get => (MascotMood)GetValue(MoodProperty);
        set => SetValue(MoodProperty, value);
    }

    public static readonly DependencyProperty IsAnimatedProperty = DependencyProperty.Register(
        nameof(IsAnimated), typeof(bool), typeof(BitMascot),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((BitMascot)d).UpdateTimer()));

    public bool IsAnimated
    {
        get => (bool)GetValue(IsAnimatedProperty);
        set => SetValue(IsAnimatedProperty, value);
    }

    public static readonly DependencyProperty BodyProperty = DependencyProperty.Register(
        nameof(Body), typeof(Brush), typeof(BitMascot),
        new FrameworkPropertyMetadata(Frozen(Color.FromRgb(0xD9, 0x77, 0x57)), FrameworkPropertyMetadataOptions.AffectsRender,
            (d, _) => ((BitMascot)d).DeriveShades()));

    /// <summary>Block colour; the lit top and shaded sides are derived from it.</summary>
    public Brush Body
    {
        get => (Brush)GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }

    public static readonly DependencyProperty EyeProperty = DependencyProperty.Register(
        nameof(Eye), typeof(Brush), typeof(BitMascot),
        new FrameworkPropertyMetadata(Frozen(Color.FromRgb(0x1A, 0x19, 0x18)), FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Screen colour (the face).</summary>
    public Brush Eye
    {
        get => (Brush)GetValue(EyeProperty);
        set => SetValue(EyeProperty, value);
    }

    /// <summary>Plays the hop regardless of the current mood.</summary>
    public void Celebrate()
    {
        Mood = MascotMood.Happy;
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

    private void DeriveShades()
    {
        var c = Body is SolidColorBrush solid ? solid.Color : Color.FromRgb(0xD9, 0x77, 0x57);
        _top = Frozen(Mix(c, Colors.White, 0.28));
        _shade = Frozen(Mix(c, Colors.Black, 0.2));
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(Columns * Unit, (Rows + Headroom) * Unit);

    protected override void OnRender(DrawingContext dc)
    {
        double u = Unit;
        int f = IsAnimated ? _frame : 0;
        var mood = Mood;

        int bob = mood switch
        {
            MascotMood.Happy => new[] { 0, -2, -3, -2, 0, 0 }[f % 6],
            MascotMood.Walk or MascotMood.Work => f % 2 == 1 ? -1 : 0,
            MascotMood.Idle => f / 6 % 2 == 1 ? 1 : 0,
            _ => 0,
        };
        int y0 = Headroom + 3 + bob;   // top row of the block

        void R(int x, int y, int w, int h, Brush b) => dc.DrawRectangle(b, null, new Rect(x * u, y * u, w * u, h * u));

        // Antenna with a spark that pulses.
        bool sparkOn = mood switch
        {
            MascotMood.Sad => false,
            MascotMood.Happy => true,
            _ => !IsAnimated || f % 4 < 2,
        };
        R(7, y0 - 3, 2, 2, sparkOn ? Spark : SparkDim);
        R(7, y0 - 1, 2, 1, _shade);

        // The block: rounded corners, lit top, shaded right side and bottom.
        R(2, y0, 12, 1, _top);
        R(1, y0 + 1, 14, 1, _top);
        R(1, y0 + 2, 13, 9, Body);
        R(14, y0 + 2, 1, 9, _shade);
        R(2, y0 + 11, 12, 1, _shade);

        // Arms: little nubs, raised in turn while typing, both up when happy.
        bool leftUp = mood == MascotMood.Happy || (mood == MascotMood.Work && f % 2 == 0);
        bool rightUp = mood == MascotMood.Happy || (mood == MascotMood.Work && f % 2 == 1);
        R(0, y0 + (leftUp ? 4 : 6), 1, 2, _shade);
        R(15, y0 + (rightUp ? 4 : 6), 1, 2, _shade);

        // Screen face.
        R(4, y0 + 3, 8, 1, Eye);
        R(3, y0 + 4, 10, 4, Eye);
        R(4, y0 + 8, 8, 1, Eye);

        if (mood == MascotMood.Work)
        {
            for (int i = 0; i < 3; i++)
            {
                var (x, w) = CodeLines[(i + f) % CodeLines.Length];
                R(x, y0 + 4 + i * 2, w, 1, Glow);
            }
        }
        else
        {
            bool blink = IsAnimated && mood != MascotMood.Happy && f % 14 == 13;
            foreach (int ex in new[] { 5, 9 })
            {
                if (mood == MascotMood.Happy)
                {
                    R(ex, y0 + 5, 1, 1, Glow);
                    R(ex + 1, y0 + 4, 1, 1, Glow);
                }
                else if (mood == MascotMood.Sad)
                    R(ex, y0 + 4, 2, 1, Glow);
                else if (blink)
                    R(ex, y0 + 5, 2, 1, Glow);
                else
                    R(ex, y0 + 4, 2, 2, Glow);
            }

            // The mouth is a cursor that blinks like a prompt; a small frown when sad.
            if (mood == MascotMood.Sad)
            {
                R(7, y0 + 6, 2, 1, Glow);
                R(6, y0 + 7, 1, 1, Glow);
                R(9, y0 + 7, 1, 1, Glow);
            }
            else if (mood == MascotMood.Happy || !IsAnimated || f % 4 < 2)
            {
                R(7, y0 + 7, 2, 1, Glow);
            }
        }

        // Legs: feet stay planted while bobbing, tucked in mid-hop, lifted in turn when walking.
        bool liftLeft = mood == MascotMood.Walk && f % 2 == 0;
        bool liftRight = mood == MascotMood.Walk && f % 2 == 1;
        int legLength = mood == MascotMood.Happy && bob < 0 ? 1 : 2 - bob;
        R(3, y0 + 12, 2, liftLeft ? 1 : legLength, _shade);
        R(11, y0 + 12, 2, liftRight ? 1 : legLength, _shade);
    }

    private static Color Mix(Color a, Color b, double t) => Color.FromRgb(
        (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
