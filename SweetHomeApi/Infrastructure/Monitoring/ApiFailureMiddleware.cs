using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Persistance;
using Persistance.Monitoring;

namespace SweetHomeApi.Infrastructure.Monitoring;

internal sealed class ApiFailureMiddleware(
    RequestDelegate next,
    IServiceScopeFactory scopeFactory,
    ILogger<ApiFailureMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
        if (route is null)
        {
            await next(context);
            return;
        }

        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await RecordAsync(context, route, StatusCodes.Status500InternalServerError, exception.GetType().Name);
            throw;
        }

        if (context.Response.StatusCode is >= 500 and <= 599)
            await RecordAsync(context, route, context.Response.StatusCode, null);
    }

    private async Task RecordAsync(HttpContext context, string route, int statusCode, string? exceptionType)
    {
        var failure = new ApiFailure
        {
            OccurredAt = DateTime.UtcNow,
            StatusCode = statusCode,
            Method = context.Request.Method,
            Route = "/" + route.TrimStart('/'),
            ExceptionType = exceptionType,
            TraceId = context.TraceIdentifier
        };
        logger.LogWarning("API returned {StatusCode} for {Method} {Route}. Request ID: {TraceId}",
            failure.StatusCode, failure.Method, failure.Route, failure.TraceId);

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SweetHomeDbContext>();
            db.ApiFailures.Add(failure);
            await db.SaveChangesAsync(timeout.Token);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not record API failure {TraceId}", failure.TraceId);
        }
    }
}
