using Microsoft.Extensions.Time.Testing;
using RateLimiter.Core;

namespace RateLimiter.Tests;

public class TokenBucketTests
{
    // Para los test se usa un reloj falso. El mismo no avanza hasta que nosotros lo indiquemos.
    // Hace que no tengamos que esperar x segundos para no demorar los tests.
    private readonly FakeTimeProvider _time = new();

    private TokenBucket CreateBucket(int capacity, double refillRatePerSecond) =>
        new(new TokenBucketOptions(capacity, refillRatePerSecond), _time);

    private void Drain(TokenBucket bucket)
    {
        while (bucket.TryConsume().IsAllowed)
        {
        }
    }

    [Fact]
    public void NewBucket_AllowsBurstUpToCapacity_ThenRejects()
    {
        TokenBucket bucket = CreateBucket(capacity: 4, refillRatePerSecond: 2);

        for (int i = 0; i < 4; i++)
        {
            Assert.True(bucket.TryConsume().IsAllowed);
        }

        Assert.False(bucket.TryConsume().IsAllowed);
    }

    [Fact]
    public void AllowedDecision_ReportsLimitAndRemainingTokens()
    {
        TokenBucket bucket = CreateBucket(capacity: 3, refillRatePerSecond: 1);

        Assert.Equal(new RateLimitDecision(true, 3, 2, TimeSpan.Zero), bucket.TryConsume());
        Assert.Equal(new RateLimitDecision(true, 3, 1, TimeSpan.Zero), bucket.TryConsume());
        Assert.Equal(new RateLimitDecision(true, 3, 0, TimeSpan.Zero), bucket.TryConsume());
    }

    [Fact]
    public void TokensAreRefilledAtTheConfiguredRate()
    {
        TokenBucket bucket = CreateBucket(capacity: 4, refillRatePerSecond: 2);
        Drain(bucket);

        _time.Advance(TimeSpan.FromSeconds(1));

        Assert.True(bucket.TryConsume().IsAllowed);
        Assert.True(bucket.TryConsume().IsAllowed);
        Assert.False(bucket.TryConsume().IsAllowed);
    }

    [Fact]
    public void PartOfAToken_IsNotEnoughToAllowARequest()
    {
        TokenBucket bucket = CreateBucket(capacity: 1, refillRatePerSecond: 1);
        Drain(bucket);

        _time.Advance(TimeSpan.FromMilliseconds(999));

        Assert.False(bucket.TryConsume().IsAllowed);
    }

    [Fact]
    public void RefillNeverExceedsCapacity_EvenAfterALongIdlePeriod()
    {
        TokenBucket bucket = CreateBucket(capacity: 2, refillRatePerSecond: 10);
        Drain(bucket);

        _time.Advance(TimeSpan.FromHours(1));

        Assert.True(bucket.TryConsume().IsAllowed);
        Assert.True(bucket.TryConsume().IsAllowed);
        Assert.False(bucket.TryConsume().IsAllowed);
    }

    [Fact]
    public void RejectedDecision_ReportsHowLongUntilTheNextToken()
    {
        TokenBucket bucket = CreateBucket(capacity: 1, refillRatePerSecond: 2);
        Drain(bucket);

        _time.Advance(TimeSpan.FromMilliseconds(250));

        RateLimitDecision decision = bucket.TryConsume();

        Assert.Equal(new RateLimitDecision(false, 1, 0, TimeSpan.FromMilliseconds(250)), decision);
    }

    [Fact]
    public void WaitingForRetryAfter_MakesTheNextRequestSucceed()
    {
        TokenBucket bucket = CreateBucket(capacity: 1, refillRatePerSecond: 2);
        Drain(bucket);
        TimeSpan retryAfter = bucket.TryConsume().RetryAfter;

        _time.Advance(retryAfter);

        Assert.True(bucket.TryConsume().IsAllowed);
    }

    [Fact]
    public void RejectedRequests_DoNotDelayTheNextToken()
    {
        TokenBucket bucket = CreateBucket(capacity: 1, refillRatePerSecond: 1);
        Drain(bucket);

        for (int i = 0; i < 9; i++)
        {
            _time.Advance(TimeSpan.FromMilliseconds(100));
            Assert.False(bucket.TryConsume().IsAllowed);
        }

        _time.Advance(TimeSpan.FromMilliseconds(100));

        Assert.True(bucket.TryConsume().IsAllowed);
    }

    [Fact]
    public void ConcurrentRequests_NeverGetMoreTokensThanTheCapacity()
    {
        const int capacity = 100;
        TokenBucket bucket = CreateBucket(capacity, refillRatePerSecond: 1);
        int allowed = 0; // Cantidad de request validas

        // Dejamos el reloj congelado asi trabajamos con los 100 tokens

        allowed = Enumerable.Range(0, 10_000) // Genera 10.000 elementos (uno por request)
            .AsParallel()
            .Count(request => bucket.TryConsume().IsAllowed); // Cuenta los request exitosos

        // Se valida si la concurrencia no genero inconsistencias en la cantidad de tokens disponibles.
        Assert.Equal(capacity, allowed);
    }
}
