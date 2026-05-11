using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pdmt.Maui.Models;
using Pdmt.Maui.Services;
using System.Text;
using System.Text.Json;

namespace Pdmt.Maui.ViewModels;

public partial class AccountViewModel(
    AuthService authService,
    ITokenService tokenService,
    EventService eventService,
    TagService tagService) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Initial))]
    private string _email = string.Empty;

    [ObservableProperty] private string? _memberSinceLabel;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _positiveCount;
    [ObservableProperty] private int _negativeCount;
    [ObservableProperty] private int _tagCount;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;

    public string Initial =>
        string.IsNullOrWhiteSpace(Email) ? "?" : Email[..1].ToUpperInvariant();

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            Email = ExtractEmailFromJwt(await tokenService.GetAccessTokenAsync()) ?? string.Empty;

            var eventsTask = eventService.GetEventsAsync();
            var tagsTask = tagService.GetTagsAsync();
            await Task.WhenAll(eventsTask, tagsTask);

            var events = eventsTask.Result;
            TotalCount = events.Count;
            MemberSinceLabel = events.Count > 0
                ? $"Member since {events.Min(e => e.Timestamp):MMM yyyy}"
                : null;
            PositiveCount = events.Count(e => e.Type == EventType.Positive);
            NegativeCount = events.Count(e => e.Type == EventType.Negative);
            TagCount = tagsTask.Result.Count;
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsLoading = false; }
    }

    private static string? ExtractEmailFromJwt(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var parts = token.Split('.');
        if (parts.Length < 2) return null;
        var payload = parts[1].Replace('-', '+').Replace('_', '/');
        switch (payload.Length % 4) { case 2: payload += "=="; break; case 3: payload += "="; break; }
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("email", out var e) ? e.GetString() : null;
        }
        catch { return null; }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        try { await authService.LogoutAsync(); }
        catch { /* AuthService clears tokens in finally — navigate regardless */ }
        await Shell.Current.GoToAsync("//login");
    }
}
