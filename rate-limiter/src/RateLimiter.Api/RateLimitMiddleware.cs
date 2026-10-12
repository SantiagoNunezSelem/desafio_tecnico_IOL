using System.Diagnostics.Metrics;
using RateLimiter.Core;

namespace RateLimiter.Api;

// Se ejecuta en cada request antes de que llegue a los endpoints. Consulta al registro y, segun
// la respuesta, deja pasar el request o lo corta con un HTTP 429.
public sealed class RateLimitMiddleware
{
    private static readonly Meter Meter = new("RateLimiter");
    private static readonly Counter<long> AllowedRequests = Meter.CreateCounter<long>("rate_limiter.requests.allowed");
    private static readonly Counter<long> RejectedRequests = Meter.CreateCounter<long>("rate_limiter.requests.rejected");

    private readonly RequestDelegate _next;     // El siguiente paso del pipeline (otro middleware o el endpoint)
    private readonly RateLimiterRegistry _registry;
    private readonly ILogger<RateLimitMiddleware> _logger;

    public RateLimitMiddleware(RequestDelegate next, RateLimiterRegistry registry, ILogger<RateLimitMiddleware> logger)
    {
        _next = next;
        _registry = registry;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        string clientKey = GetClientKey(context);
        RateLimitDecision decision = _registry.TryConsume(clientKey);

        // Estos headers se informan siempre, asi el cliente sabe cuanto le queda antes de ser rechazado.
        context.Response.Headers["X-Ratelimit-Limit"] = decision.Limit.ToString();
        context.Response.Headers["X-Ratelimit-Remaining"] = decision.Remaining.ToString();

        if (decision.IsAllowed)
        {
            AllowedRequests.Add(1);
            await _next(context);
            return;
        }

        RejectedRequests.Add(1);

        string retryAfterSeconds = Math.Ceiling(decision.RetryAfter.TotalSeconds).ToString();

        _logger.LogInformation("Request rejected for client {ClientKey}. Retry after {RetryAfter} seconds.", clientKey, retryAfterSeconds);

        // No se llama a _next: el request no llega al endpoint.
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers["X-Ratelimit-Retry-After"] = retryAfterSeconds;
        context.Response.Headers.RetryAfter = retryAfterSeconds;    // Header estandar de HTTP para el mismo dato
        await context.Response.WriteAsync($"Too many requests. Retry after {retryAfterSeconds} seconds.");
    }

    // Identifica al cliente por su direccion IP
    private static string GetClientKey(HttpContext context)
    {
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
