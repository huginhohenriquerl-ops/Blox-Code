using System.Windows;
using System.Windows.Controls;
using BloxCode.Core;

namespace BloxCode.UI.Pages;

public partial class FastFlagsPage : UserControl
{
    private readonly MainWindow _window;
    private bool _syncing;

    public FastFlagsPage(MainWindow window)
    {
        _window = window;
        InitializeComponent();
        BuildSegments(MsaaSegments, "msaa", FastFlagPresets.MsaaModes, v => FastFlagPresets.MsaaSamples = v);
        BuildSegments(TextureSegments, "texture", FastFlagPresets.TextureLevels, v => FastFlagPresets.TextureLevel = v);
        Sync();
    }

    private void BuildSegments(Panel host, string group, (string Label, string? Value)[] options, Action<string?> apply)
    {
        foreach (var (label, value) in options)
        {
            var segment = new RadioButton { Content = label, GroupName = group + GetHashCode(), Tag = value };
            segment.SetResourceReference(StyleProperty, "Segment");
            segment.Checked += (_, _) =>
            {
                if (_syncing) return;
                apply(value);
                Store.SaveSettings();
                Sync();
            };
            host.Children.Add(segment);
        }
    }

    /// <summary>Reflects the settings dictionary in the preset controls and the editor summary.</summary>
    private void Sync()
    {
        _syncing = true;
        ExclusiveFullscreen.IsChecked = FastFlagPresets.UseExclusiveFullscreen;
        DisableScaling.IsChecked = FastFlagPresets.DisableScaling;
        SelectSegment(MsaaSegments, FastFlagPresets.MsaaSamples);
        SelectSegment(TextureSegments, FastFlagPresets.TextureLevel);
        _syncing = false;

        int custom = Store.Settings.FastFlags.Keys.Count(k => !FastFlagPresets.PresetFlags.Contains(k));
        EditorSummary.Text = custom switch
        {
            0 => "Add your own flags one by one or paste a JSON config.",
            1 => "1 custom flag set · add, edit, import or export",
            _ => $"{custom} custom flags set · add, edit, import or export",
        };
    }

    private static void SelectSegment(Panel host, string? value)
    {
        var match = host.Children.OfType<RadioButton>().FirstOrDefault(r => (string?)r.Tag == value)
                    ?? host.Children.OfType<RadioButton>().First();
        match.IsChecked = true;
    }

    private void OnPresetChanged(object sender, RoutedEventArgs e)
    {
        FastFlagPresets.UseExclusiveFullscreen = ExclusiveFullscreen.IsChecked == true;
        FastFlagPresets.DisableScaling = DisableScaling.IsChecked == true;
        Store.SaveSettings();
        Sync();
    }

    private void OnOpenEditor(object sender, RoutedEventArgs e) => _window.ShowPage(new FastFlagEditorPage(_window));
}
