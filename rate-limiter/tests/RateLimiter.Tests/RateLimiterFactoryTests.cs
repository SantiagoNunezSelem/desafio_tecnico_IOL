using Microsoft.Extensions.Time.Testing;
using RateLimiter.Core;

namespace RateLimiter.Tests;

public class RateLimiterFactoryTests
{
    private readonly FakeTimeProvider _time = new();

    private IRateLimiter Create(RateLimitAlgorithm algorithm, int limit, TimeSpan window) =>
        RateLimiterFactory.Create(
            new RateLimiterOptions { Algorithm = algorithm, Limit = limit, Window = window }, _time);

    [Fact]
    public void Create_ReturnsTheConfiguredAlgorithm()
    {
        TimeSpan window = TimeSpan.FromMinutes(1);

        Assert.IsType<TokenBucket>(Create(RateLimitAlgorithm.TokenBucket, 5, window));
        Assert.IsType<SlidingWindowCounter>(Create(RateLimitAlgorithm.SlidingWindowCounter, 5, window));
    }

    [Fact]
    public void TokenBucket_GetsLimitAsCapacity_AndRefillsItOverOneWindow()
    {
        // 4 requests cada 2 segundos equivale a un bucket de 4 tokens que recarga 2 por segundo.
        IRateLimiter limiter = Create(RateLimitAlgorithm.TokenBucket, limit: 4, TimeSpan.FromSeconds(2));

        for (int i = 0; i < 4; i++)
        {
            Assert.True(limiter.TryConsume().IsAllowed);
        }
        Assert.False(limiter.TryConsume().IsAllowed);

        _time.Advance(TimeSpan.FromMilliseconds(500));

        Assert.True(limiter.TryConsume().IsAllowed);
        Assert.False(limiter.TryConsume().IsAllowed);
    }

    [Theory]
    [InlineData(RateLimitAlgorithm.TokenBucket)]
    [InlineData(RateLimitAlgorithm.SlidingWindowCounter)]
    public void InvalidRule_IsRejected(RateLimitAlgorithm algorithm)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(algorithm, limit: 0, TimeSpan.FromMinutes(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(algorithm, limit: 5, TimeSpan.Zero));
    }

    [Fact]
    public void UnknownAlgorithm_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Create((RateLimitAlgorithm)99, limit: 5, TimeSpan.FromMinutes(1)));
    }
}
