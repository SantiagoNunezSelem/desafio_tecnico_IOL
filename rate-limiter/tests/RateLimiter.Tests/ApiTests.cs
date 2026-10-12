using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RateLimiter.Core;

namespace RateLimiter.Tests;

// Levantan la API completa en memoria y se envian requests HTTP
public class ApiTests
{
    [Fact]
    public async Task Api_StartsWithTheRuleFromAppSettings()
    {
        using var factory = new WebApplicationFactory<Program>();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/ping");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("pong", await response.Content.ReadAsStringAsync());
        Assert.Equal("5", response.Headers.GetValues("X-Ratelimit-Limit").Single());
        Assert.Equal("4", response.Headers.GetValues("X-Ratelimit-Remaining").Single());
    }

    [Theory]
    [InlineData(RateLimitAlgorithm.TokenBucket)]
    [InlineData(RateLimitAlgorithm.SlidingWindowCounter)]
    public async Task RequestsOverTheLimit_Get429(RateLimitAlgorithm algorithm)
    {
        var options = new RateLimiterOptions { Algorithm = algorithm, Limit = 2, Window = TimeSpan.FromMinutes(1) };
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddSingleton(new RateLimiterRegistry(options, TimeProvider.System))));
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage first = await client.GetAsync("/ping");
        HttpResponseMessage second = await client.GetAsync("/ping");
        HttpResponseMessage third = await client.GetAsync("/ping");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
        Assert.True(third.Headers.Contains("X-Ratelimit-Retry-After"));
        Assert.StartsWith("Too many requests.", await third.Content.ReadAsStringAsync());
    }
}
