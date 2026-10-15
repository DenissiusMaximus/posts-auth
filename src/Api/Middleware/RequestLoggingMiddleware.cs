using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace auth.Api.Middleware;

public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var request = context.Request;
        var method = request.Method;
        var path = request.Path.Value ?? "/";
        var queryString = request.QueryString.HasValue ? request.QueryString.Value : string.Empty;
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        var clientIp = GetClientIp(context);

        logger.LogInformation("--> HTTP {Method} {Path}{Query} from {ClientIp} [TraceId: {TraceId}]",
            method, path, queryString, clientIp, traceId);

        try
        {
            await next(context);

            stopwatch.Stop();
            var statusCode = context.Response.StatusCode;
            var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

            var logLevel = statusCode switch
            {
                >= 500 => LogLevel.Error,
                >= 400 => LogLevel.Warning,
                _ => LogLevel.Information
            };

            logger.Log(logLevel,
                "<-- HTTP {Method} {Path}{Query} responded {StatusCode} in {ElapsedMs:0.00} ms [TraceId: {TraceId}]",
                method, path, queryString, statusCode, elapsedMs, traceId);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            stopwatch.Stop();
            var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

            logger.LogWarning(
                "<-- HTTP {Method} {Path}{Query} aborted by client after {ElapsedMs:0.00} ms [TraceId: {TraceId}]",
                method, path, queryString, elapsedMs, traceId);

            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

            logger.LogError(
                ex,
                "<-- HTTP {Method} {Path}{Query} failed with exception after {ElapsedMs:0.00} ms [TraceId: {TraceId}]",
                method, path, queryString, elapsedMs, traceId);

            throw;
        }
    }

    private static string GetClientIp(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor) &&
            !string.IsNullOrWhiteSpace(forwardedFor))
        {
            return forwardedFor.ToString().Split(',')[0].Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}

public static class RequestLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RequestLoggingMiddleware>();
    }
}

