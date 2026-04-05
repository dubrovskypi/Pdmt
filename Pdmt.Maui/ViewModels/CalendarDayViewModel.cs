using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Pdmt.Maui.Models;

namespace Pdmt.Maui.ViewModels;

public partial class CalendarDayViewModel : ObservableObject
{
    private const double MaxHalfWidth = 60;

    private readonly CalendarDayDetailsDto _dto;
    private readonly double _maxIntensitySum;

    public CalendarDayViewModel(CalendarDayDetailsDto dto, double maxIntensitySum)
    {
        _dto = dto;
        _maxIntensitySum = maxIntensitySum;
    }

    public DateTimeOffset Date => _dto.Date;
    public int PosCount => _dto.PosCount;
    public int NegCount => _dto.NegCount;
    public bool HasEvents => _dto.PosCount + _dto.NegCount > 0;
    public string DayAbbrev => _dto.Date.ToString("ddd").ToUpperInvariant();
    public string DayNumber => _dto.Date.Day.ToString();
    public string PosCountText => _dto.PosCount > 0 ? _dto.PosCount.ToString() : "";
    public string NegCountText => _dto.NegCount > 0 ? _dto.NegCount.ToString() : "";
    public double PosBarWidth => _maxIntensitySum > 0 ? _dto.PositiveIntensitySum / _maxIntensitySum * MaxHalfWidth : 0;
    public double NegBarWidth => _maxIntensitySum > 0 ? _dto.NegativeIntensitySum / _maxIntensitySum * MaxHalfWidth : 0;
    public string ScoreAbsolute => Math.Abs(_dto.DayScore).ToString("0.1", System.Globalization.CultureInfo.InvariantCulture);
    public string ScoreLabel => _dto.DayScore > 1 ? "pos" : _dto.DayScore < -1 ? "neg" : "even";
    public Color DotColor => _dto.DayScore > 1
        ? Color.FromArgb("#22c55e")
        : _dto.DayScore < -1
            ? Color.FromArgb("#ef4444")
            : Color.FromArgb("#f59e0b");

    public IReadOnlyList<TagCountDto> TopPositiveTags => _dto.TopPositiveTags;
    public IReadOnlyList<TagCountDto> TopNegativeTags => _dto.TopNegativeTags;
    public bool HasPosTags => _dto.TopPositiveTags.Count > 0;
    public bool HasNegTags => _dto.TopNegativeTags.Count > 0;

    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty] private bool _isExpandedLoading;
    [ObservableProperty] private bool _isExpandedEmpty;

    public ObservableCollection<CalendarEventViewModel> ExpandedEvents { get; } = [];
}
