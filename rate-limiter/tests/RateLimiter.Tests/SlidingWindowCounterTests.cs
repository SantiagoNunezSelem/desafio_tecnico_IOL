using Microsoft.Extensions.Time.Testing;
using RateLimiter.Core;

namespace RateLimiter.Tests;

public class SlidingWindowCounterTests
{
    private readonly FakeTimeProvider _time = new();

    private SlidingWindowCounter CreateLimiter(int limit, TimeSpan window) =>
        new SlidingWindowCounter(limit, window, _time);

    private void Consume(SlidingWindowCounter limiter, int requests)
    {
        for (int i = 0; i < requests; i++)
        {
            Assert.True(limiter.TryConsume().IsAllowed);
        }
    }

    [Fact]
    public void NewLimiter_AllowsRequestsUpToTheLimit_ThenRejects()
    {
        SlidingWindowCounter limiter = CreateLimiter(limit: 3, TimeSpan.FromMinutes(1));

        Consume(limiter, 3);

        Assert.False(limiter.TryConsume().IsAllowed);
    }

    [Fact]
    public void AllowedDecision_ReportsLimitAndRemainingRequests()
    {
        SlidingWindowCounter limiter = CreateLimiter(limit: 3, TimeSpan.FromMinutes(1));

        Assert.Equal(new RateLimitDecision(true, 3, 2, TimeSpan.Zero), limiter.TryConsume());
        Assert.Equal(new RateLimitDecision(true, 3, 1, TimeSpan.Zero), limiter.TryConsume());
        Assert.Equal(new RateLimitDecision(true, 3, 0, TimeSpan.Zero), limiter.TryConsume());
    }

    [Fact]
    public void PreviousWindow_CountsInProportionToItsOverlap()
    {
        // Numeros del ejemplo del libro: limite 7 por minuto y 5 requests en el minuto anterior.
        SlidingWindowCounter limiter = CreateLimiter(limit: 7, TimeSpan.FromMinutes(1));
        Consume(limiter, 5);

        // Al 30% del minuto siguiente, el anterior pesa 70%. estimated = 0 + 5 * 0.7 = 3.5 requests.
        _time.Advance(TimeSpan.FromSeconds(60 + 18));

        // Quedan 3.5 lugares libres, por lo tanto entran 3 requests y el cuarto se rechaza.
        Consume(limiter, 3);
        Assert.False(limiter.TryConsume().IsAllowed);
    }

    [Fact]
    public void BurstAcrossTheWindowBoundary_IsRejected()
    {
        // Valida que el Sliding Window Counter no tenga el problema de extremos que tiene el Fixed Widnow Counter.
        SlidingWindowCounter limiter = CreateLimiter(limit: 5, TimeSpan.FromMinutes(1));
        _time.Advance(TimeSpan.FromSeconds(30));
        Consume(limiter, 5);

        _time.Advance(TimeSpan.FromSeconds(30));

        // estimated = 0 + 5 * 1
        Assert.False(limiter.TryConsume().IsAllowed);
    }

    [Fact]
    public void RequestsOlderThanTwoWindows_AreForgotten()
    {
        SlidingWindowCounter limiter = CreateLimiter(limit: 2, TimeSpan.FromMinutes(1));
        Consume(limiter, 2);

        _time.Advance(TimeSpan.FromMinutes(2));

        Consume(limiter, 2);
        Assert.False(limiter.TryConsume().IsAllowed);
    }

    [Fact]
    public void RejectedDecision_WhenCurrentWindowIsFull_ReportsRetryAfter()
    {
        SlidingWindowCounter limiter = CreateLimiter(limit: 2, TimeSpan.FromMinutes(1));
        Consume(limiter, 2);

        RateLimitDecision decision = limiter.TryConsume();

        // 1 minuto hasta que termina la ventana (ya con su limite) + 30 segundos para que se libere un lugar (ventana/limite).
        Assert.Equal(new RateLimitDecision(false, 2, 0, TimeSpan.FromSeconds(90)), decision);
    }

    [Fact]
    public void RejectedDecision_WhenPreviousWindowStillCounts_ReportsRetryAfter()
    {
        SlidingWindowCounter limiter = CreateLimiter(limit: 4, TimeSpan.FromMinutes(1));
        Consume(limiter, 4);

        // Al 25% de la ventana siguiente la anterior pesa 4 * 0.75 = 3: entra un solo request.
        _time.Advance(TimeSpan.FromSeconds(60 + 15));
        Consume(limiter, 1);

        RateLimitDecision decision = limiter.TryConsume();

        // Al 50% la anterior pesa 2, y con el request ya aceptado queda un lugar libre.
        Assert.Equal(new RateLimitDecision(false, 4, 0, TimeSpan.FromSeconds(15)), decision);
    }

    [Theory]
    [InlineData(2, 0)]
    [InlineData(4, 75)]
    public void WaitingForRetryAfter_MakesTheNextRequestSucceed(int limit, int secondsToAdvance)
    {
        SlidingWindowCounter limiter = CreateLimiter(limit, TimeSpan.FromMinutes(1));
        Consume(limiter, limit);
        _time.Advance(TimeSpan.FromSeconds(secondsToAdvance));
        while (limiter.TryConsume().IsAllowed)
        {
        }

        TimeSpan retryAfter = limiter.TryConsume().RetryAfter;
        _time.Advance(retryAfter);

        Assert.True(limiter.TryConsume().IsAllowed);
    }

    [Fact]
    public void ConcurrentRequests_NeverExceedTheLimit()
    {
        const int limit = 100;
        SlidingWindowCounter limiter = CreateLimiter(limit, TimeSpan.FromMinutes(1));

        // El reloj queda congelado, asi todos los requests caen en la misma ventana.
        int allowed = Enumerable.Range(0, 10_000)
            .AsParallel()
            .Count(request => limiter.TryConsume().IsAllowed);

        Assert.Equal(limit, allowed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveLimit_IsRejected(int limit)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateLimiter(limit, TimeSpan.FromMinutes(1)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveWindow_IsRejected(int windowSeconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateLimiter(limit: 1, TimeSpan.FromSeconds(windowSeconds)));
    }
}
