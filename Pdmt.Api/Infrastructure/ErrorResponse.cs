namespace Pdmt.Api.Infrastructure
{
    public class ErrorResponse
    {
        public string Message { get; set; } = null!;
        public string? Details { get; set; }
        public string? CorrelationId { get; set; }
    }
}
