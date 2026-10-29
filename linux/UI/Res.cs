using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace BloxCode.Linux.UI;

/// <summary>
/// App-level resources for code that builds controls before they are in a window (constructors),
/// where a control's own FindResource can't see Application.Resources yet.
/// </summary>
public static class Res
{
    public static T Get<T>(string key) where T : class =>
        Application.Current is { } app && app.TryGetResource(key, app.ActualThemeVariant, out var value) && value is T typed
            ? typed
            : throw new KeyNotFoundException($"Missing resource {key}");

    public static ControlTheme Theme(string key) => Get<ControlTheme>(key);

    public static FontFamily Mono => Get<FontFamily>("Font.Mono");

    /// <summary>Binds a brush property to a theme resource, so it follows light/dark switches.</summary>
    public static void BindBrush(Control control, AvaloniaProperty<IBrush?> property, string key) =>
        control[!property] = control.GetResourceObservable(key).ToBinding();
}
