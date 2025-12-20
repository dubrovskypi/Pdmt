namespace Pdmt.Api.Domain
{
    public class FailedLoginAttempt
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = null!;
        public string IpAddress { get; set; } = null!;
        public DateTime OccurredAtUtc { get; set; }
        public string Reason { get; set; } = null!;
    }
}
