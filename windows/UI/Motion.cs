using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using BloxCode.Core;

namespace BloxCode.UI;

/// <summary>
/// The interface's entrance animations: a page rises and fades in, and its top-level blocks follow
/// one after another. Off when the user turned animations off in Appearance, or Windows' own
/// "Animation effects" setting is off.
/// </summary>
public static class Motion
{
    private static readonly IEasingFunction Ease = new CubicEase { EasingMode = EasingMode.EaseOut };

    public static bool Enabled => Store.Settings.Animations && SystemParameters.ClientAreaAnimation;

    /// <summary>Animates a page that was just put on screen.</summary>
    public static void EnterPage(FrameworkElement page)
    {
        if (!Enabled)
            return;
        Rise(page, delayMs: 0, distance: 10);
        if (Blocks(page) is { } blocks)
        {
            int i = 0;
            foreach (var block in blocks.Children.OfType<FrameworkElement>().Take(8))
                Rise(block, delayMs: 50 + 35 * i++, distance: 8);
        }
    }

    /// <summary>The panel holding a page's cards: the first panel inside its scroll viewer.</summary>
    private static Panel? Blocks(FrameworkElement page)
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

    /// <summary>Fades and slides one element in. Key frames hold it hidden through the delay.</summary>
    private static void Rise(FrameworkElement element, int delayMs, double distance)
    {
        var total = TimeSpan.FromMilliseconds(delayMs + 240);
        var delay = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(delayMs));

        var fade = new DoubleAnimationUsingKeyFrames { Duration = total, FillBehavior = FillBehavior.Stop };
        fade.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        fade.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, delay));
        fade.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(total), Ease));
        element.BeginAnimation(UIElement.OpacityProperty, fade);

        // Only elements without a transform of their own get the slide.
        if (element.RenderTransform is not null && element.RenderTransform != Transform.Identity)
            return;
        var shift = new TranslateTransform();
        element.RenderTransform = shift;
        var slide = new DoubleAnimationUsingKeyFrames { Duration = total };
        slide.KeyFrames.Add(new DiscreteDoubleKeyFrame(distance, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        slide.KeyFrames.Add(new DiscreteDoubleKeyFrame(distance, delay));
        slide.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(total), Ease));
        slide.Completed += (_, _) => element.RenderTransform = Transform.Identity;
        shift.BeginAnimation(TranslateTransform.YProperty, slide);
    }
}
