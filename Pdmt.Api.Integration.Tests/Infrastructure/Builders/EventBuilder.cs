using Pdmt.Api.Domain;

namespace Pdmt.Api.Integration.Tests.Infrastructure.Builders;

public sealed class EventBuilder
{
    private Guid _userId = TestAuthHandler.TestUserId;
    private EventType _type = EventType.Positive;
    private int _intensity = 5;
    private DateTimeOffset _timestamp = DateTimeOffset.UtcNow;
    private string _title = "Test Event";
    private string? _description;
    private string? _context;
    private bool _canInfluence;

    public EventBuilder WithUserId(Guid userId) { _userId = userId; return this; }
    public EventBuilder WithType(EventType type) { _type = type; return this; }
    public EventBuilder WithIntensity(int intensity) { _intensity = intensity; return this; }
    public EventBuilder WithTimestamp(DateTimeOffset ts) { _timestamp = ts; return this; }
    public EventBuilder WithTitle(string title) { _title = title; return this; }
    public EventBuilder WithDescription(string description) { _description = description; return this; }
    public EventBuilder WithContext(string context) { _context = context; return this; }
    public EventBuilder WithCanInfluence(bool canInfluence) { _canInfluence = canInfluence; return this; }

    public Event Build() => new()
    {
        Id = Guid.NewGuid(),
        UserId = _userId,
        Type = _type,
        Intensity = _intensity,
        Timestamp = _timestamp,
        Title = _title,
        Description = _description,
        Context = _context,
        CanInfluence = _canInfluence
    };
}
