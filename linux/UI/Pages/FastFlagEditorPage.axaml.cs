using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using BloxCode.Core;

namespace BloxCode.Linux.UI.Pages;

/// <summary>One editable row. The page owns validation because duplicates need the whole list.</summary>
public sealed class FlagEntry(string name, string value, Action<FlagEntry> changed, Action selectionChanged) : INotifyPropertyChanged
{
    private string _name = name, _value = value;
    private bool _isChecked;

    public string Name
    {
        get => _name;
        set
        {
            value = (value ?? "").Trim();
            if (_name == value) { Raise(nameof(Name)); return; }
            _name = value;
            changed(this);
        }
    }

    public string Value
    {
        get => _value;
        set
        {
            value = FastFlagValidator.Normalize(_name, value ?? "");
            if (_value == value) { Raise(nameof(Value)); return; }
            _value = value;
            changed(this);
        }
    }

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value) return;
            _isChecked = value;
            Raise(nameof(IsChecked));
            // Selection is UI-only: no need to revalidate or save the whole list.
            selectionChanged();
        }
    }

    public FlagKind Kind => FastFlagValidator.KindOf(Name);
    public string KindText => FastFlagValidator.Describe(Kind);
    public bool IsBool => Kind == FlagKind.Bool && Value is "True" or "False";
    public bool IsPreset => FastFlagPresets.PresetFlags.Contains(Name);

    public bool BoolValue
    {
        get => Value == "True";
        set => Value = value ? "True" : "False";
    }

    public string? NameError { get; set; }
    public string? ValueError { get; set; }
    public string? Error => NameError is not null ? $"Not saved — {NameError}" : ValueError;
    public bool HasError => Error is not null;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise(string property) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));

    /// <summary>Refreshes everything derived from the name and value.</summary>
    public void RaiseAll()
    {
        foreach (string property in new[] { nameof(Name), nameof(Value), nameof(Kind), nameof(KindText), nameof(IsBool), nameof(BoolValue), nameof(Error), nameof(HasError) })
            Raise(property);
    }
}

public partial class FastFlagEditorPage : UserControl
{
    private const string AllowlistUrl = "https://devforum.roblox.com/t/allowlist-for-local-client-configuration-via-fast-flags/3966569";

    private readonly MainWindow _window;
    private readonly List<FlagEntry> _entries = new();
    private readonly ObservableCollection<FlagEntry> _visible = new();
    private readonly DispatcherTimer _toastTimer = new() { Interval = TimeSpan.FromSeconds(2.5) };
    private bool _bulk;

    public FastFlagEditorPage(MainWindow window)
    {
        _window = window;
        InitializeComponent();

        foreach (var (name, value) in Store.Settings.FastFlags.OrderBy(f => f.Key, StringComparer.OrdinalIgnoreCase))
            _entries.Add(NewEntry(name, value));

        FlagList.ItemsSource = _visible;
        EmptyMascot.IsAnimated = Store.Settings.AnimateMascot;
        _toastTimer.Tick += (_, _) => { Toast.Text = ""; _toastTimer.Stop(); };
        DetachedFromVisualTree += (_, _) => _toastTimer.Stop();

        Revalidate();
        ApplyFilter();
    }

    private bool Matches(FlagEntry entry)
    {
        if (entry.IsPreset && ShowPresets.IsChecked != true)
            return false;

        string query = (SearchBox.Text ?? "").Trim();
        return query.Length == 0
               || entry.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
               || entry.Value.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Rebuilds the visible rows (only on search / preset / add / delete, never mid-edit).</summary>
    private void ApplyFilter()
    {
        _visible.Clear();
        foreach (var entry in _entries.Where(Matches))
            _visible.Add(entry);
        RefreshChrome();
    }

    private FlagEntry NewEntry(string name, string value) => new(name, value, OnEntryChanged, () =>
    {
        if (!_bulk) RefreshChrome();
    });

    private void OnEntryChanged(FlagEntry entry)
    {
        if (_bulk) return;
        Revalidate();
        Save();
        RefreshChrome();
    }

    /// <summary>Recomputes every row's errors (duplicates depend on the whole list) and refreshes bindings.</summary>
    private void Revalidate()
    {
        // The first row with a name owns it; only later copies are flagged, so the original keeps saving.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in _entries)
        {
            entry.NameError = FastFlagValidator.ValidateName(entry.Name)
                              ?? (!seen.Add(entry.Name) ? "an entry for this flag name already exists." : null);
            if (entry.NameError is { Length: > 0 })
                entry.NameError = char.ToLowerInvariant(entry.NameError[0]) + entry.NameError[1..];
            entry.ValueError = entry.NameError is null ? FastFlagValidator.ValidateValue(entry.Name, entry.Value) : null;
            entry.RaiseAll();
        }
    }

    /// <summary>Rows with a bad or duplicate name stay on screen but out of Sober's config.</summary>
    private void Save()
    {
        var flags = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in _entries)
            if (entry.NameError is null)
                flags[entry.Name] = entry.Value;

        Store.Settings.FastFlags = flags;
        Store.SaveSettings();
    }

    private void RefreshChrome()
    {
        var visible = _visible.ToList();
        int selected = visible.Count(e => e.IsChecked);
        int hiddenPresets = ShowPresets.IsChecked == true ? 0 : _entries.Count(e => e.IsPreset);
        int problems = _entries.Count(e => e.HasError);

        DeleteButton.IsEnabled = selected > 0;
        DeleteText.Text = selected > 0 ? $"Delete {selected}" : "Delete selected";
        SelectAll.IsChecked = visible.Count > 0 && selected == visible.Count ? true : selected == 0 ? false : null;

        bool searching = (SearchBox.Text ?? "").Trim().Length > 0;
        EmptyState.IsVisible = visible.Count == 0;
        EmptyTitle.Text = searching ? "Nothing matches your search" : "No custom flags yet";
        EmptyText.Text = searching ? "Try part of the name or the value." : "Add one by name, or paste a whole JSON config.";
        EmptyButton.IsVisible = !searching;

        var parts = new List<string> { _entries.Count == 1 ? "1 flag" : $"{_entries.Count} flags" };
        if (hiddenPresets > 0) parts.Add($"{hiddenPresets} preset hidden");
        if (problems > 0) parts.Add(problems == 1 ? "1 needs attention" : $"{problems} need attention");
        Summary.Text = string.Join(" · ", parts);
    }

    private void ShowToast(string text)
    {
        Toast.Text = text;
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    private async void OnAddNew(object? sender, RoutedEventArgs e)
    {
        var dialog = new AddFlagDialog(_entries.Select(x => x.Name).ToHashSet(StringComparer.Ordinal));
        if (!await dialog.ShowDialog<bool>(_window) || dialog.Result.Count == 0)
            return;

        _bulk = true;
        FlagEntry? last = null;
        foreach (var (name, value) in dialog.Result)
        {
            var existing = _entries.FirstOrDefault(x => x.Name == name);
            if (existing is not null)
            {
                existing.Value = value;
                last = existing;
            }
            else
            {
                last = NewEntry(name, value);
                _entries.Add(last);
            }
        }
        _bulk = false;

        // Make sure what was just added is visible.
        if (last is not null && !Matches(last))
        {
            SearchBox.Text = "";
            if (last.IsPreset) ShowPresets.IsChecked = true;
        }
        Revalidate();
        Save();
        ApplyFilter();
        if (last is not null && FlagList.ContainerFromItem(last) is { } row)
            row.BringIntoView();

        ShowToast(dialog.Result.Count == 1 ? "Flag added." : $"{dialog.Result.Count} flags imported.");
    }

    private async void OnDeleteSelected(object? sender, RoutedEventArgs e)
    {
        var doomed = _visible.Where(x => x.IsChecked).ToList();
        if (doomed.Count == 0)
            return;
        if (doomed.Count > 1 && !await MessageDialog.AskAsync(_window, $"Delete {doomed.Count} flags?",
                string.Join(", ", doomed.Take(6).Select(x => x.Name)) + (doomed.Count > 6 ? "…" : ""), "Delete", "Cancel", danger: true))
            return;

        foreach (var entry in doomed)
            _entries.Remove(entry);
        Revalidate();
        Save();
        ApplyFilter();
        ShowToast(doomed.Count == 1 ? "Flag deleted." : $"{doomed.Count} flags deleted.");
    }

    private void OnDeleteRow(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: FlagEntry entry })
        {
            _entries.Remove(entry);
            _visible.Remove(entry);
            Revalidate();
            Save();
            RefreshChrome();
        }
    }

    private void OnSelectAll(object? sender, RoutedEventArgs e)
    {
        // Clicking an indeterminate box selects everything, like most file managers.
        bool select = _visible.Any(x => !x.IsChecked);
        _bulk = true;
        foreach (var entry in _visible)
            entry.IsChecked = select;
        _bulk = false;
        RefreshChrome();
    }

    private async void OnExport(object? sender, RoutedEventArgs e)
    {
        string json = JsonSerializer.Serialize(Store.Settings.FastFlags, new JsonSerializerOptions { WriteIndented = true });
        ShowToast(await Shell.CopyAsync(_window, json) ? "Copied to clipboard." : "The clipboard isn't available — try again.");
    }

    private void OnSearchChanged(object? sender, TextChangedEventArgs e)
    {
        if (FlagList is not null)
            ApplyFilter();
    }

    private void OnShowPresetsChanged(object? sender, RoutedEventArgs e) => ApplyFilter();

    private void OnBack(object? sender, RoutedEventArgs e) => _window.ShowPage(new FastFlagsPage(_window));

    private void OnAllowlistLink(object? sender, RoutedEventArgs e) => Shell.Open(AllowlistUrl, _window);
}
