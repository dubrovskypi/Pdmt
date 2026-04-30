namespace Pdmt.Maui.ViewModels;

public class MonthDayCellViewModel
{
    private readonly double _maxAbsScore;

    public MonthDayCellViewModel(DateTime date, int posCount, int negCount, double dayScore, double maxAbsScore, bool isPlaceholder = false)
    {
        Date = date;
        PosCount = posCount;
        NegCount = negCount;
        DayScore = dayScore;
        IsPlaceholder = isPlaceholder;
        _maxAbsScore = maxAbsScore;
    }

    public DateTime Date { get; }
    public int PosCount { get; }
    public int NegCount { get; }
    public double DayScore { get; }
    public bool IsPlaceholder { get; }

    public bool HasEvents => !IsPlaceholder && (PosCount + NegCount) > 0;
    public bool HasPositive => !IsPlaceholder && PosCount > 0;
    public bool HasNegative => !IsPlaceholder && NegCount > 0;
    public string DayNumber => IsPlaceholder ? "" : Date.Day.ToString();
    public bool IsToday => !IsPlaceholder && Date.Date == DateTime.Today;

    public Color CellBackground => IsPlaceholder
        ? Colors.Transparent
        : DayScore > 1
            ? (Color)Application.Current!.Resources["PositiveBg"]
            : DayScore < -1
                ? (Color)Application.Current!.Resources["NegativeBg"]
                : (Color)Application.Current!.Resources["Card"];

    public Color CellBorder => IsToday
        ? (Color)Application.Current!.Resources["Primary"]
        : IsPlaceholder
            ? Colors.Transparent
            : (Color)Application.Current!.Resources["Border"];

    public double IntensityBarWidth => _maxAbsScore > 0 && HasEvents
        ? Math.Min(1.0, Math.Abs(DayScore) / _maxAbsScore) * 40
        : 0;

    public Color IntensityBarColor => DayScore >= 0
        ? (Color)Application.Current!.Resources["PositiveBar"]
        : (Color)Application.Current!.Resources["NegativeBar"];
}
