using RateLimiter.Core;

namespace RateLimiter.Tests;

public class TokenBucketOptionsTests
{
    [Fact]
    public void ValidValues_AreExposedUnchanged()
    {
        var options = new TokenBucketOptions(capacity: 10, refillRatePerSecond: 0.5);

        Assert.Equal(10, options.Capacity);
        Assert.Equal(0.5, options.RefillRatePerSecond);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveCapacity_IsRejected(int capacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TokenBucketOptions(capacity, refillRatePerSecond: 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveRefillRate_IsRejected(double refillRatePerSecond)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TokenBucketOptions(capacity: 1, refillRatePerSecond));
    }
}
