using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using RateLimiter.Api;
using RateLimiter.Core;

namespace RateLimiter.Tests;

// Prueban el middleware sin levantar la API
public class RateLimitMiddlewareTests
{
    private readonly FakeTimeProvider _time = new();

    // Cuenta cuantos requests dejo pasar el middleware hacia el endpoint.
    private int _requestsThatReachedTheEndpoint;

    private RateLimitMiddleware CreateMiddleware(int limit, TimeSpan window)
    {
        var options = new RateLimiterOptions { Algorithm = RateLimitAlgorithm.TokenBucket, Limit = limit, Window = window };
        var registry = new RateLimiterRegistry(options, _time);

        // En lugar de un endpoint real solo incrementa el contador _requestsThatReachedTheEndpoint
        RequestDelegate next = context =>
        {
            _requestsThatReachedTheEndpoint++;
            return Task.CompletedTask;
        };

        return new RateLimitMiddleware(next, registry, NullLogger<RateLimitMiddleware>.Instance);
    }

    private static async Task<HttpResponse> SendRequest(RateLimitMiddleware middleware, string ip = "10.0.0.1")
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(ip);

        await middleware.InvokeAsync(context);

        return context.Response;
    }

    [Fact]
    public async Task AllowedRequest_ReachesTheEndpoint_AndReportsTheLimit()
    {
        RateLimitMiddleware middleware = CreateMiddleware(limit: 3, TimeSpan.FromMinutes(1));

        HttpResponse response = await SendRequest(middleware);

        Assert.Equal(1, _requestsThatReachedTheEndpoint);
        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal("3", response.Headers["X-Ratelimit-Limit"]);
        Assert.Equal("2", response.Headers["X-Ratelimit-Remaining"]);
        Assert.False(response.Headers.ContainsKey("X-Ratelimit-Retry-After"));
    }

    [Fact]
    public async Task RequestOverTheLimit_Gets429_AndDoesNotReachTheEndpoint()
    {
        RateLimitMiddleware middleware = CreateMiddleware(limit: 1, TimeSpan.FromSeconds(30));
        
        await SendRequest(middleware);

        HttpResponse response = await SendRequest(middleware);

        Assert.Equal(1, _requestsThatReachedTheEndpoint);
        Assert.Equal(StatusCodes.Status429TooManyRequests, response.StatusCode);
        Assert.Equal("1", response.Headers["X-Ratelimit-Limit"]);
        Assert.Equal("0", response.Headers["X-Ratelimit-Remaining"]);
        Assert.Equal("30", response.Headers["X-Ratelimit-Retry-After"]);
        Assert.Equal("30", response.Headers.RetryAfter);
    }

    [Fact]
    public async Task RetryAfter_IsRoundedUpToWholeSeconds()
    {
        RateLimitMiddleware middleware = CreateMiddleware(limit: 1, TimeSpan.FromMilliseconds(1200));
        await SendRequest(middleware);

        HttpResponse response = await SendRequest(middleware);

        Assert.Equal("2", response.Headers["X-Ratelimit-Retry-After"]);
    }

    [Fact]
    public async Task WaitingForRetryAfter_MakesTheNextRequestSucceed()
    {
        RateLimitMiddleware middleware = CreateMiddleware(limit: 1, TimeSpan.FromSeconds(30));
        await SendRequest(middleware);
        HttpResponse rejected = await SendRequest(middleware);

        int retryAfterSeconds = int.Parse(rejected.Headers["X-Ratelimit-Retry-After"].ToString());
        _time.Advance(TimeSpan.FromSeconds(retryAfterSeconds));
        HttpResponse response = await SendRequest(middleware);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
    }

    [Fact]
    public async Task EachIpAddress_HasItsOwnLimit()
    {
        RateLimitMiddleware middleware = CreateMiddleware(limit: 1, TimeSpan.FromMinutes(1));
        await SendRequest(middleware, ip: "10.0.0.1");

        HttpResponse sameIp = await SendRequest(middleware, ip: "10.0.0.1");
        HttpResponse otherIp = await SendRequest(middleware, ip: "10.0.0.2");

        Assert.Equal(StatusCodes.Status429TooManyRequests, sameIp.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, otherIp.StatusCode);
    }

    [Fact]
    public async Task RequestsWithoutIpAddress_ShareOneLimit()
    {
        RateLimitMiddleware middleware = CreateMiddleware(limit: 1, TimeSpan.FromMinutes(1));
        var first = new DefaultHttpContext();
        var second = new DefaultHttpContext();

        await middleware.InvokeAsync(first);
        await middleware.InvokeAsync(second);

        Assert.Equal(StatusCodes.Status200OK, first.Response.StatusCode);
        Assert.Equal(StatusCodes.Status429TooManyRequests, second.Response.StatusCode);
    }
}
