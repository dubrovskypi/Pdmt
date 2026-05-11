using Pdmt.Api.Infrastructure;
using Pdmt.Api.Infrastructure.Exceptions;

namespace Pdmt.Api.Middleware
{
    public class ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IWebHostEnvironment env)
    {
        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await next(context);
            }
            catch (RateLimitExceededException ex)
            {
                await HandleException(context, ex, 429);
            }
            catch (NotFoundException ex)
            {
                await HandleException(context, ex, 404);
            }
            catch (UnauthorizedAccessException ex)
            {
                await HandleException(context, ex, 401);
            }
            catch (ValidationException ex)
            {
                await HandleException(context, ex, 400);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception");
                await HandleException(context, ex, 500, isUnhandled: true);
            }
        }

        private async Task HandleException(
            HttpContext context,
            Exception ex,
            int statusCode,
            bool isUnhandled = false)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = statusCode;

            var correlationId = context.Items["CorrelationId"]?.ToString();
            var isDev = env.IsDevelopment();

            var response = new ErrorResponse
            {
                Message = isUnhandled && !isDev ? "Internal server error" : ex.Message,
                Details = !isUnhandled && isDev ? ex.StackTrace : null,
                CorrelationId = correlationId
            };

            await context.Response.WriteAsJsonAsync(response);
        }
    }
}
