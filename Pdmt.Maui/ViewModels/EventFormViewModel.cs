using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pdmt.Maui.Models;
using Pdmt.Maui.Services;

namespace Pdmt.Maui.ViewModels;

public abstract partial class EventFormViewModel : ObservableObject
{
    private readonly TagService _tagService;
    private List<string> _allTagNames = [];

    protected EventFormViewModel(TagService tagService)
    {
        _tagService = tagService;
        UpdateIntensitySegments();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyPropertyChangedFor(nameof(SaveButtonBg))]
    [NotifyPropertyChangedFor(nameof(SaveButtonTextColor))]
    private string _title = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PositiveBtnBg))]
    [NotifyPropertyChangedFor(nameof(PositiveBtnBorder))]
    [NotifyPropertyChangedFor(nameof(PositiveBtnText))]
    [NotifyPropertyChangedFor(nameof(NegativeBtnBg))]
    [NotifyPropertyChangedFor(nameof(NegativeBtnBorder))]
    [NotifyPropertyChangedFor(nameof(NegativeBtnText))]
    [NotifyPropertyChangedFor(nameof(TagChipBg))]
    [NotifyPropertyChangedFor(nameof(TagChipBorder))]
    [NotifyPropertyChangedFor(nameof(TagChipText))]
    [NotifyPropertyChangedFor(nameof(IntensityLabelColor))]
    private bool _isPositive = true;

    [ObservableProperty]
    private int _intensity = 5;

    [ObservableProperty]
    private string? _description;

    [ObservableProperty]
    private bool _canInfluence;

    [ObservableProperty]
    private string _tagInput = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyPropertyChangedFor(nameof(SaveButtonBg))]
    [NotifyPropertyChangedFor(nameof(SaveButtonTextColor))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    public ObservableCollection<string> SelectedTags { get; } = [];
    public ObservableCollection<string> TagSuggestions { get; } = [];
    public bool HasSuggestions => TagSuggestions.Count > 0;

    public List<IntensitySegment> IntensitySegments { get; private set; } = [];

    // ── Type toggle colors ────────────────────────────────────────────────
    public Color PositiveBtnBg     => IsPositive ? Color.FromArgb("#dcfce7") : Color.FromArgb("#eeeeee");
    public Color PositiveBtnBorder => IsPositive ? Color.FromArgb("#86efac") : Colors.Transparent;
    public Color PositiveBtnText   => IsPositive ? Color.FromArgb("#16a34a") : Color.FromArgb("#888888");
    public Color NegativeBtnBg     => !IsPositive ? Color.FromArgb("#fee2e2") : Color.FromArgb("#eeeeee");
    public Color NegativeBtnBorder => !IsPositive ? Color.FromArgb("#fca5a5") : Colors.Transparent;
    public Color NegativeBtnText   => !IsPositive ? Color.FromArgb("#dc2626") : Color.FromArgb("#888888");

    // ── Save button ───────────────────────────────────────────────────────
    public bool CanSave              => !string.IsNullOrWhiteSpace(Title) && !IsBusy;
    public Color SaveButtonBg        => CanSave ? Color.FromArgb("#006a60") : Color.FromArgb("#eeeeee");
    public Color SaveButtonTextColor => CanSave ? Colors.White : Color.FromArgb("#888888");

    // ── Selected tag chip colors (reflect event type) ─────────────────────
    public Color TagChipBg     => IsPositive ? Color.FromArgb("#dcfce7") : Color.FromArgb("#fee2e2");
    public Color TagChipBorder => IsPositive ? Color.FromArgb("#86efac") : Color.FromArgb("#fca5a5");
    public Color TagChipText   => IsPositive ? Color.FromArgb("#16a34a") : Color.FromArgb("#dc2626");

    // ── Intensity value label color ───────────────────────────────────────
    public Color IntensityLabelColor => IsPositive ? Color.FromArgb("#16a34a") : Color.FromArgb("#dc2626");

    // ── Partial hooks ─────────────────────────────────────────────────────

    partial void OnIsPositiveChanged(bool value) => UpdateIntensitySegments();

    partial void OnIntensityChanged(int value) => UpdateIntensitySegments();

    partial void OnTagInputChanged(string value)
    {
        TagSuggestions.Clear();
        if (!string.IsNullOrWhiteSpace(value))
        {
            var matches = _allTagNames
                .Where(n => n.Contains(value, StringComparison.OrdinalIgnoreCase)
                            && !SelectedTags.Contains(n))
                .Take(6);
            foreach (var match in matches)
                TagSuggestions.Add(match);
        }
        OnPropertyChanged(nameof(HasSuggestions));
    }

    // ── Update helpers ────────────────────────────────────────────────────

    private void UpdateIntensitySegments()
    {
        var accent  = IsPositive ? Color.FromArgb("#22c55e") : Color.FromArgb("#ef4444");
        var inactive = Color.FromArgb("#EEEEEE");

        IntensitySegments = Enumerable.Range(0, 11)
            .Select(i => new IntensitySegment(
                i,
                i <= Intensity ? accent : inactive,
                i <= Intensity ? Math.Min(1.0, 0.4 + i * 0.06) : 0.4))
            .ToList();

        OnPropertyChanged(nameof(IntensitySegments));
    }

    // ── Commands ──────────────────────────────────────────────────────────

    protected async Task LoadTagsInternalAsync()
    {
        var tags = await _tagService.GetTagsAsync();
        _allTagNames = tags.Select(t => t.Name).ToList();
    }

    [RelayCommand]
    private void SetPositive() => IsPositive = true;

    [RelayCommand]
    private void SetNegative() => IsPositive = false;

    [RelayCommand]
    private void SetIntensity(int index) => Intensity = index;

    [RelayCommand]
    private void ToggleInfluence() => CanInfluence = !CanInfluence;

    [RelayCommand]
    private void SelectSuggestion(string name)
    {
        if (!SelectedTags.Contains(name))
            SelectedTags.Add(name);
        TagInput = "";
        TagSuggestions.Clear();
        OnPropertyChanged(nameof(HasSuggestions));
    }

    [RelayCommand]
    private void CommitTagInput()
    {
        var name = TagInput.Trim();
        if (!string.IsNullOrWhiteSpace(name) && !SelectedTags.Contains(name))
            SelectedTags.Add(name);
        TagInput = "";
        TagSuggestions.Clear();
        OnPropertyChanged(nameof(HasSuggestions));
    }

    [RelayCommand]
    private void RemoveTag(string name) => SelectedTags.Remove(name);

    [RelayCommand]
    private static async Task CancelAsync() => await Shell.Current.GoToAsync("..");
}
