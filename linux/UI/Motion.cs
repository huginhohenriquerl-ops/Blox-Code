using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using BloxCode.Core;

namespace BloxCode.Linux.UI;

/// <summary>
/// The interface's entrance animations: a page rises and fades in, and its top-level blocks follow
/// one after another. Off when the user turned animations off in Appearance.
/// </summary>
public static class Motion
{
    private static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(240);

    public static bool Enabled => Store.Settings.Animations;

    /// <summary>Animates a page that was just put on screen.</summary>
    public static void EnterPage(Control page)
    {
        if (!Enabled)
            return;

        var moving = new List<(Control Control, TimeSpan Delay)> { (page, TimeSpan.Zero) };
        if (Blocks(page) is { } blocks)
            moving.AddRange(blocks.Children.Take(8).Select((c, i) => (c, TimeSpan.FromMilliseconds(50 + 35 * i))));

        // Controls with transitions or transforms of their own are left alone.
        moving.RemoveAll(m => m.Control.Transitions is { Count: > 0 } || (m.Control.RenderTransform is not null && m.Control != page));

        foreach (var (control, _) in moving)
        {
            control.Opacity = 0;
            control.RenderTransform = TransformOperations.Parse($"translateY({(control == page ? 10 : 8)}px)");
        }

        // Once on screen, give them transitions and let them move to where they belong.
        Dispatcher.UIThread.Post(() =>
        {
            foreach (var (control, delay) in moving)
            {
                control.Transitions = new Transitions
                {
                    new DoubleTransition { Property = Visual.OpacityProperty, Duration = Duration, Delay = delay, Easing = new CubicEaseOut() },
                    new TransformOperationsTransition { Property = Visual.RenderTransformProperty, Duration = Duration, Delay = delay, Easing = new CubicEaseOut() },
                };
                control.Opacity = 1;
                control.RenderTransform = TransformOperations.Parse("translateY(0px)");
            }

            // Then hand the controls back untouched, so later changes don't animate by surprise.
            DispatcherTimer.RunOnce(() =>
            {
                foreach (var (control, _) in moving)
                {
                    control.Transitions = null;
                    control.RenderTransform = null;
                }
            }, TimeSpan.FromMilliseconds(700));
        }, DispatcherPriority.Loaded);
    }

    /// <summary>The panel holding a page's cards: the first panel inside its scroll viewer.</summary>
    private static Panel? Blocks(Control page)
    {
        object? content = page is UserControl { Content: ScrollViewer scroll } ? scroll.Content : (page as UserControl)?.Content;
        for (int depth = 0; depth < 3 && content is not null; depth++)
        {
            if (content is Panel { Children.Count: > 1 } panel and not Grid)
                return panel;
            content = content switch
            {
                Grid grid => grid.Children.OfType<StackPanel>().FirstOrDefault(p => p.Children.Count > 1),
                Decorator decorator => decorator.Child,
                _ => null,
            };
        }
        return content as Panel;
    }
}
