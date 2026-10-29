using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;

namespace BloxCode.Linux.UI;

/// <summary>
/// Blox Code draws its own title bar on every desktop (GNOME, KDE, Cinnamon…), like on Windows.
/// With system decorations off, moving and resizing go through the window manager here.
/// </summary>
public static class Chrome
{
    /// <summary>Dragging the element moves the window; double-clicking it toggles maximized.</summary>
    public static void MakeDragArea(Window window, Control area, bool maximizeOnDoubleClick = true)
    {
        area.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(area).Properties.IsLeftButtonPressed || e.Source is Button || IsInsideButton(e.Source))
                return;
            if (e.ClickCount == 2 && maximizeOnDoubleClick && window.CanResize)
            {
                window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                return;
            }
            window.BeginMoveDrag(e);
        };
    }

    private static bool IsInsideButton(object? source)
    {
        for (var visual = source as Visual; visual is not null; visual = visual.GetVisualParent())
            if (visual is Button or TextBox or CheckBox or RadioButton)
                return true;
        return false;
    }

    private static Visual? GetVisualParent(this Visual visual) => Avalonia.VisualTree.VisualExtensions.GetVisualParent(visual);

    /// <summary>Adds invisible grips along the edges of a root panel that resize the window.</summary>
    public static void AddResizeGrips(Window window, Panel root, double thickness = 6)
    {
        void Grip(WindowEdge edge, HorizontalAlignment h, VerticalAlignment v, StandardCursorType cursor, double? width, double? height)
        {
            var grip = new Border
            {
                Background = Avalonia.Media.Brushes.Transparent,
                HorizontalAlignment = h,
                VerticalAlignment = v,
                Cursor = new Cursor(cursor),
                ZIndex = 100,
            };
            if (width is { } w) grip.Width = w;
            if (height is { } ht) grip.Height = ht;
            grip.PointerPressed += (_, e) =>
            {
                if (window.WindowState == WindowState.Normal && e.GetCurrentPoint(grip).Properties.IsLeftButtonPressed)
                    window.BeginResizeDrag(edge, e);
            };
            window.GetObservable(Window.WindowStateProperty).Subscribe(new Observer(state => grip.IsVisible = state == WindowState.Normal));
            root.Children.Add(grip);
        }

        double c = thickness * 2;
        Grip(WindowEdge.West, HorizontalAlignment.Left, VerticalAlignment.Stretch, StandardCursorType.LeftSide, thickness, null);
        Grip(WindowEdge.East, HorizontalAlignment.Right, VerticalAlignment.Stretch, StandardCursorType.RightSide, thickness, null);
        Grip(WindowEdge.North, HorizontalAlignment.Stretch, VerticalAlignment.Top, StandardCursorType.TopSide, null, thickness);
        Grip(WindowEdge.South, HorizontalAlignment.Stretch, VerticalAlignment.Bottom, StandardCursorType.BottomSide, null, thickness);
        Grip(WindowEdge.NorthWest, HorizontalAlignment.Left, VerticalAlignment.Top, StandardCursorType.TopLeftCorner, c, c);
        Grip(WindowEdge.NorthEast, HorizontalAlignment.Right, VerticalAlignment.Top, StandardCursorType.TopRightCorner, c, c);
        Grip(WindowEdge.SouthWest, HorizontalAlignment.Left, VerticalAlignment.Bottom, StandardCursorType.BottomLeftCorner, c, c);
        Grip(WindowEdge.SouthEast, HorizontalAlignment.Right, VerticalAlignment.Bottom, StandardCursorType.BottomRightCorner, c, c);
    }

    private sealed class Observer(Action<WindowState> next) : IObserver<WindowState>
    {
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(WindowState value) => next(value);
    }
}
