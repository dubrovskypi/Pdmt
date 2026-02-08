using Pdmt.Api.Infrastructure;
using Pdmt.Api.Infrastructure.Exceptions;

namespace Pdmt.Api.Middleware
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(
            RequestDelegate next,
            ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (RateLimitExceededException ex)
            {
                await HandleException(context, ex, 429);
            }
            catch (UnauthorizedAccessException ex)
            {
                await HandleException(context, ex, 401);
            }
            catch (InvalidOperationException ex)
            {
                await HandleException(context, ex, 400);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception");
                await HandleException(context, ex, 500, hideDetails: true);
            }
        }

        private async Task HandleException(
            HttpContext context,
            Exception ex,
            int statusCode,
            bool hideDetails = false)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = statusCode;

            var correlationId = context.Items["CorrelationId"]?.ToString();

            var response = new ErrorResponse
            {
                Message = ex.Message,
                Details = hideDetails ? null : ex.StackTrace,
                CorrelationId = correlationId
            };

            await context.Response.WriteAsJsonAsync(response);
        }
    }
}
