using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace BloxCode.Linux.UI;

/// <summary>
/// A line icon from Controls.axaml (20 x 20 geometry) stroked in the inherited text colour, so it
/// follows buttons, hover states and the theme. Linux has no Segoe icon font to borrow.
/// </summary>
public sealed class IconView : Control
{
    public static readonly StyledProperty<Geometry?> DataProperty =
        AvaloniaProperty.Register<IconView, Geometry?>(nameof(Data));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<IconView>();

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<IconView, double>(nameof(StrokeThickness), 1.6);

    public static readonly StyledProperty<bool> FilledProperty =
        AvaloniaProperty.Register<IconView, bool>(nameof(Filled));

    static IconView()
    {
        AffectsRender<IconView>(DataProperty, ForegroundProperty, StrokeThicknessProperty, FilledProperty);
    }

    public IconView()
    {
        Width = 16;
        Height = 16;
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
    }

    public Geometry? Data { get => GetValue(DataProperty); set => SetValue(DataProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public double StrokeThickness { get => GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }
    public bool Filled { get => GetValue(FilledProperty); set => SetValue(FilledProperty, value); }

    public override void Render(DrawingContext context)
    {
        if (Data is null || Foreground is null || Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        double scale = Math.Min(Bounds.Width, Bounds.Height) / 20.0;
        var offset = new Point((Bounds.Width - 20 * scale) / 2, (Bounds.Height - 20 * scale) / 2);
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offset.X, offset.Y)))
        {
            var pen = new Pen(Foreground, StrokeThickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            context.DrawGeometry(Filled ? Foreground : null, pen, Data);
        }
    }
}
