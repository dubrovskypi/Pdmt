using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Pdmt.Maui.ViewModels.Cards;

public abstract partial class InsightCardViewModel : ObservableObject
{
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    public bool HasError => ErrorMessage is not null;

    public abstract Task LoadAsync(DateTimeOffset from, DateTimeOffset to, bool showLoading = true, CancellationToken ct = default);

    // Ensure PropertyChanged always fires on the UI thread.
    // Card LoadAsync methods run HTTP calls concurrently via Task.WhenAll,
    // and their continuations may land on thread-pool threads even when started
    // from the main thread — so we dispatch explicitly to keep MAUI bindings stable.
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        if (MainThread.IsMainThread)
            base.OnPropertyChanged(e);
        else
            MainThread.BeginInvokeOnMainThread(() => base.OnPropertyChanged(e));
    }
}
