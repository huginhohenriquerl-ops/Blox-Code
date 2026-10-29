using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using BloxCode.Core;

namespace BloxCode.Linux.UI.Pages;

public partial class AddFlagDialog : Window
{
    private static readonly string[] QuickPrefixes = { "FFlag", "DFFlag", "FInt", "DFInt", "FString", "DFString" };

    private readonly ISet<string> _existing;
    private Dictionary<string, string>? _parsed;

    /// <summary>Flags to add or overwrite, filled when the dialog returns true.</summary>
    public Dictionary<string, string> Result { get; } = new();

    public AddFlagDialog() : this(new HashSet<string>()) { }

    public AddFlagDialog(ISet<string> existing)
    {
        _existing = existing;
        InitializeComponent();
        Chrome.MakeDragArea(this, TitleBar, maximizeOnDoubleClick: false);

        foreach (string prefix in QuickPrefixes)
        {
            var chip = new Button
            {
                Content = prefix,
                FontFamily = Res.Mono,
                FontSize = 12,
                Padding = new Thickness(9, 3),
                Margin = new Thickness(0, 0, 6, 0),
                Classes = { "chip" },
            };
            chip.Click += (_, _) =>
            {
                // Swap an existing prefix rather than stacking them.
                string rest = NameBox.Text ?? "";
                string? current = QuickPrefixes.Concat(new[] { "SFFlag", "FLog", "DFLog" })
                    .OrderByDescending(p => p.Length).FirstOrDefault(p => rest.StartsWith(p, StringComparison.Ordinal));
                if (current is not null) rest = rest[current.Length..];
                NameBox.Text = prefix + rest;
                NameBox.Focus();
                NameBox.CaretIndex = NameBox.Text.Length;
            };
            PrefixChips.Children.Add(chip);
        }

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                Close(false);
            else if (e.Key == Key.Enter && SingleTab.IsChecked == true)
                OnConfirm(this, new RoutedEventArgs());
        };
        Opened += (_, _) => NameBox.Focus();
        UpdateSingle();
    }

    private void OnTabChanged(object? sender, RoutedEventArgs e)
    {
        if (ImportPanel is null || SinglePanel is null) return;
        bool import = ImportTab.IsChecked == true;
        SinglePanel.IsVisible = !import;
        ImportPanel.IsVisible = import;
        if (import) { JsonBox.Focus(); UpdateImport(); }
        else { NameBox.Focus(); UpdateSingle(); }
    }

    // ===== Add single =====

    private void OnNameChanged(object? sender, TextChangedEventArgs e) => UpdateSingle();

    private void OnValueToggled(object? sender, RoutedEventArgs e) => UpdateSingle();

    private string SingleValue => BoolValueHost.IsVisible
        ? (ValueTrue.IsChecked == true ? "True" : "False")
        : (ValueBox.Text ?? "").Trim();

    private void UpdateSingle()
    {
        if (ConfirmButton is null || ValueBox is null || NameHint is null) return;

        string name = (NameBox.Text ?? "").Trim();
        var kind = FastFlagValidator.KindOf(name);
        bool filtered = name.EndsWith("_PlaceFilter", StringComparison.Ordinal) || name.EndsWith("_DataCenterFilter", StringComparison.Ordinal);

        // Booleans get a True/False switch; numbers, text and filters a box.
        bool boolValue = kind == FlagKind.Bool && !filtered;
        BoolValueHost.IsVisible = boolValue;
        ValueBox.IsVisible = !boolValue;
        ValueBox.Watermark = filtered ? "True;1818" : kind switch
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

    private void OnJsonChanged(object? sender, TextChangedEventArgs e) => UpdateImport();

    /// <summary>Accepts flag JSON, and also a whole Sober config.json (its "fflags" object).</summary>
    private static Dictionary<string, string> Parse(string text)
    {
        try
        {
            if (JsonNode.Parse(text, documentOptions: new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip })
                is JsonObject { } root && root["fflags"] is JsonObject fflags)
                text = fflags.ToJsonString();
        }
        catch (JsonException)
        {
            // Not a complete document; ParseJson below is more forgiving (missing braces).
        }
        return FastFlagValidator.ParseJson(text);
    }

    private void UpdateImport()
    {
        if (ConfirmButton is null || JsonHint is null) return;
        _parsed = null;
        string text = (JsonBox.Text ?? "").Trim();

        if (text.Length == 0)
        {
            SetHint(JsonHint, null, "A Sober config.json, a ClientAppSettings.json, a Bloxstrap export or just a few \"Name\": value lines.");
        }
        else
        {
            try
            {
                _parsed = Parse(text);
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

    private async void OnLoadFile(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (!StorageProvider.CanOpen)
            {
                SetHint(JsonHint, "This desktop has no file picker; paste the JSON instead.", "");
                return;
            }
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import Fast Flags",
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType("JSON files") { Patterns = new[] { "*.json" } }, FilePickerFileTypes.All },
            });
            if (files.Count == 0)
                return;

            var file = files[0];
            // A real flag config is a few KB; refuse something huge that would freeze the editor.
            var props = await file.GetBasicPropertiesAsync();
            if (props.Size > 2 * 1024 * 1024)
            {
                SetHint(JsonHint, "That file is too big to be a Fast Flags config (over 2 MB).", "");
                return;
            }
            await using var stream = await file.OpenReadAsync();
            using var reader = new StreamReader(stream);
            JsonBox.Text = await reader.ReadToEndAsync();
        }
        catch (Exception ex)
        {
            Logger.Warn("AddFlagDialog::LoadFile", ex.Message);
            SetHint(JsonHint, $"Couldn't read that file: {ex.Message}", "");
        }
    }

    // ===== Shared =====

    private static void SetHint(TextBlock hint, string? error, string normal)
    {
        hint.Text = error ?? normal;
        hint.Classes.Set("error", error is not null);
        hint.IsVisible = hint.Text.Length > 0;
    }

    private async void OnConfirm(object? sender, RoutedEventArgs e)
    {
        if (!ConfirmButton.IsEnabled)
            return;

        if (SingleTab.IsChecked == true)
        {
            string name = (NameBox.Text ?? "").Trim();
            Result[name] = FastFlagValidator.Normalize(name, SingleValue);
            Close(true);
            return;
        }

        var incoming = _parsed!.Where(kv => FastFlagValidator.ValidateName(kv.Key) is null).ToDictionary(kv => kv.Key, kv => kv.Value);
        var conflicts = incoming.Keys.Where(_existing.Contains).ToList();

        if (conflicts.Count > 0)
        {
            string names = string.Join(", ", conflicts.Take(5)) + (conflicts.Count > 5 ? $" and {conflicts.Count - 5} more" : "");
            bool overwrite = await MessageDialog.AskAsync(this, "Overwrite existing flags?",
                $"{conflicts.Count} of these flags already have a value: {names}. Use the imported values?",
                "Overwrite", "Keep mine");
            if (!overwrite)
                foreach (string name in conflicts)
                    incoming.Remove(name);
        }

        if (incoming.Count == 0)
        {
            Close(false);
            return;
        }

        foreach (var (name, value) in incoming)
            Result[name] = value;
        Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
