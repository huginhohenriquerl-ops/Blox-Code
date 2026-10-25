using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using BloxCode.Core;
using Microsoft.Win32;

namespace BloxCode.UI.Pages;

public partial class AddFlagDialog : Window
{
    private static readonly string[] QuickPrefixes = { "FFlag", "DFFlag", "FInt", "DFInt", "FString", "DFString" };

    private readonly ISet<string> _existing;
    private Dictionary<string, string>? _parsed;

    /// <summary>Flags to add or overwrite, filled when the dialog returns true.</summary>
    public Dictionary<string, string> Result { get; } = new();

    public AddFlagDialog(Window owner, ISet<string> existing)
    {
        _existing = existing;
        InitializeComponent();
        ThemeManager.ApplyChrome(this);
        Owner = owner;

        foreach (string prefix in QuickPrefixes)
        {
            var chip = new Button { Content = prefix, FontFamily = (System.Windows.Media.FontFamily)FindResource("Font.Mono"), FontSize = 12, Padding = new Thickness(9, 3, 9, 3), Margin = new Thickness(0, 0, 6, 0) };
            chip.SetResourceReference(StyleProperty, "Button.Chip");
            chip.Click += (_, _) =>
            {
                // Swap an existing prefix rather than stacking them.
                string rest = NameBox.Text;
                string? current = QuickPrefixes.Concat(new[] { "SFFlag", "FLog", "DFLog" })
                    .OrderByDescending(p => p.Length).FirstOrDefault(p => rest.StartsWith(p, StringComparison.Ordinal));
                if (current is not null) rest = rest[current.Length..];
                NameBox.Text = prefix + rest;
                NameBox.Focus();
                NameBox.CaretIndex = NameBox.Text.Length;
            };
            PrefixChips.Children.Add(chip);
        }

        Loaded += (_, _) => NameBox.Focus();
        UpdateSingle();
    }

    private void OnTabChanged(object sender, RoutedEventArgs e)
    {
        if (ImportPanel is null) return;
        bool import = ImportTab.IsChecked == true;
        SinglePanel.Visibility = import ? Visibility.Collapsed : Visibility.Visible;
        ImportPanel.Visibility = import ? Visibility.Visible : Visibility.Collapsed;
        if (import) { JsonBox.Focus(); UpdateImport(); }
        else { NameBox.Focus(); UpdateSingle(); }
    }

    // ===== Add single =====

    private void OnSingleChanged(object sender, RoutedEventArgs e) => UpdateSingle();

    private string SingleValue => BoolValueHost.Visibility == Visibility.Visible
        ? (ValueTrue.IsChecked == true ? "True" : "False")
        : ValueBox.Text.Trim();

    private void UpdateSingle()
    {
        if (ConfirmButton is null || ValueBox is null) return;

        string name = NameBox.Text.Trim();
        var kind = FastFlagValidator.KindOf(name);
        bool filtered = name.EndsWith("_PlaceFilter", StringComparison.Ordinal) || name.EndsWith("_DataCenterFilter", StringComparison.Ordinal);

        // Booleans get a True/False switch; numbers, text and filters a box.
        bool boolValue = kind == FlagKind.Bool && !filtered;
        BoolValueHost.Visibility = boolValue ? Visibility.Visible : Visibility.Collapsed;
        ValueBox.Visibility = boolValue ? Visibility.Collapsed : Visibility.Visible;
        ValueBox.Tag = filtered ? "True;1818" : kind switch
        {
            FlagKind.Int or FlagKind.Log => "a whole number, e.g. 144",
            FlagKind.String => "text",
            _ => "value",
        };

        string? nameError = name.Length == 0 ? null : FastFlagValidator.ValidateName(name);
        if (nameError is null && _existing.Contains(name))
            nameError = "An entry for this flag name already exists.";

        SetHint(NameHint, nameError, name.Length == 0
            ? "Pick a prefix above or type the full name."
            : $"{FastFlagValidator.Describe(kind)} flag{(filtered ? " with a place filter" : "")}.");

        string value = SingleValue;
        string? valueError = name.Length == 0 || nameError is not null || (value.Length == 0 && !boolValue)
            ? null
            : FastFlagValidator.ValidateValue(name, value);
        SetHint(ValueHint, valueError, "");

        ConfirmButton.Content = "Add flag";
        ConfirmButton.IsEnabled = name.Length > 0 && nameError is null && valueError is null && (boolValue || value.Length > 0);
    }

    // ===== Import JSON =====

    private void OnJsonChanged(object sender, TextChangedEventArgs e) => UpdateImport();

    private void UpdateImport()
    {
        if (ConfirmButton is null) return;
        _parsed = null;
        string text = JsonBox.Text.Trim();

        if (text.Length == 0)
        {
            SetHint(JsonHint, null, "A ClientAppSettings.json, a Bloxstrap export or just a few \"Name\": value lines.");
        }
        else
        {
            try
            {
                _parsed = FastFlagValidator.ParseJson(text);
                int bad = _parsed.Keys.Count(k => FastFlagValidator.ValidateName(k) is not null);
                int conflicts = _parsed.Keys.Count(_existing.Contains);
                var parts = new List<string> { _parsed.Count == 1 ? "1 flag found" : $"{_parsed.Count} flags found" };
                if (conflicts > 0) parts.Add($"{conflicts} already set");
                if (bad > 0) parts.Add($"{bad} with an invalid name will be skipped");
                SetHint(JsonHint, null, string.Join(" · ", parts) + ".");
            }
            catch (JsonException ex)
            {
                SetHint(JsonHint, $"That isn't valid JSON yet — {ex.Message.Split('.')[0].ToLowerInvariant()}.", "");
            }
        }

        int valid = _parsed?.Keys.Count(k => FastFlagValidator.ValidateName(k) is null) ?? 0;
        ConfirmButton.Content = valid > 0 ? $"Import {valid}" : "Import";
        ConfirmButton.IsEnabled = valid > 0;
    }

    private void OnLoadFile(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog { Filter = "JSON files|*.json|All files|*.*", Title = "Import Fast Flags" };
        if (picker.ShowDialog(this) == true)
            JsonBox.Text = File.ReadAllText(picker.FileName);
    }

    // ===== Shared =====

    private static void SetHint(TextBlock hint, string? error, string normal)
    {
        hint.Text = error ?? normal;
        hint.SetResourceReference(TextBlock.ForegroundProperty, error is null ? "Brush.TextMuted" : "Brush.Danger");
        hint.Visibility = hint.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        if (!ConfirmButton.IsEnabled)
            return;

        if (SingleTab.IsChecked == true)
        {
            string name = NameBox.Text.Trim();
            Result[name] = FastFlagValidator.Normalize(name, SingleValue);
            DialogResult = true;
            return;
        }

        var incoming = _parsed!.Where(kv => FastFlagValidator.ValidateName(kv.Key) is null).ToDictionary(kv => kv.Key, kv => kv.Value);
        var conflicts = incoming.Keys.Where(_existing.Contains).ToList();

        if (conflicts.Count > 0)
        {
            string names = string.Join(", ", conflicts.Take(5)) + (conflicts.Count > 5 ? $" and {conflicts.Count - 5} more" : "");
            bool overwrite = MessageDialog.Ask(this, "Overwrite existing flags?",
                $"{conflicts.Count} of these flags already have a value: {names}. Use the imported values?",
                "Overwrite", "Keep mine");
            if (!overwrite)
                foreach (string name in conflicts)
                    incoming.Remove(name);
        }

        if (incoming.Count == 0)
        {
            DialogResult = false;
            return;
        }

        foreach (var (name, value) in incoming)
            Result[name] = value;
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
