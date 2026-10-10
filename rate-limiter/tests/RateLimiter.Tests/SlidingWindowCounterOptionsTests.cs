using RateLimiter.Core;

namespace RateLimiter.Tests;

public class SlidingWindowCounterOptionsTests
{
    [Fact]
    public void ValidValues_AreExposedUnchanged()
    {
        var options = new SlidingWindowCounterOptions(limit: 10, TimeSpan.FromMinutes(1));

        Assert.Equal(10, options.Limit);
        Assert.Equal(TimeSpan.FromMinutes(1), options.Window);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveLimit_IsRejected(int limit)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SlidingWindowCounterOptions(limit, TimeSpan.FromMinutes(1)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveWindow_IsRejected(int windowSeconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SlidingWindowCounterOptions(limit: 1, TimeSpan.FromSeconds(windowSeconds)));
    }
}
