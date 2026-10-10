using Microsoft.Extensions.Time.Testing;
using RateLimiter.Core;

namespace RateLimiter.Tests;

public class RateLimiterRegistryTests
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly FakeTimeProvider _time = new();

    private RateLimiterRegistry CreateRegistry(
        int limit, RateLimitAlgorithm algorithm = RateLimitAlgorithm.TokenBucket) =>
        new RateLimiterRegistry(new RateLimiterOptions { Algorithm = algorithm, Limit = limit, Window = Window }, _time);

    [Theory]
    [InlineData(RateLimitAlgorithm.TokenBucket)]
    [InlineData(RateLimitAlgorithm.SlidingWindowCounter)]
    public void SameClient_IsLimitedAcrossRequests(RateLimitAlgorithm algorithm)
    {
        RateLimiterRegistry registry = CreateRegistry(limit: 2, algorithm);

        Assert.True(registry.TryConsume("client-a").IsAllowed);
        Assert.True(registry.TryConsume("client-a").IsAllowed);
        Assert.False(registry.TryConsume("client-a").IsAllowed);
    }

    [Theory]
    [InlineData(RateLimitAlgorithm.TokenBucket)]
    [InlineData(RateLimitAlgorithm.SlidingWindowCounter)]
    public void OneClientReachingItsLimit_DoesNotAffectAnotherClient(RateLimitAlgorithm algorithm)
    {
        RateLimiterRegistry registry = CreateRegistry(limit: 1, algorithm);
        registry.TryConsume("client-a");

        Assert.False(registry.TryConsume("client-a").IsAllowed);
        Assert.True(registry.TryConsume("client-b").IsAllowed);
    }

    [Fact]
    public void InactiveClient_IsRemoved()
    {
        RateLimiterRegistry registry = CreateRegistry(limit: 5);
        registry.TryConsume("client-a");

        // La limpieza se ejecuta con el request de otro cliente, pasadas dos ventanas.
        _time.Advance(Window * 2);
        registry.TryConsume("client-b");

        Assert.Equal(1, registry.Count);
    }

    [Fact]
    public void ActiveClient_IsNotRemoved()
    {
        RateLimiterRegistry registry = CreateRegistry(limit: 5);
        registry.TryConsume("client-a");

        _time.Advance(Window);
        registry.TryConsume("client-a");
        _time.Advance(Window);
        registry.TryConsume("client-b");

        Assert.Equal(2, registry.Count);
    }

    [Fact]
    public void ClientThatIsStillLimited_KeepsItsStateAfterACleanup()
    {
        RateLimiterRegistry registry = CreateRegistry(limit: 1, RateLimitAlgorithm.SlidingWindowCounter);

        // client-a usa su unico request justo antes de que se ejecute una limpieza.
        _time.Advance(Window * 2 - TimeSpan.FromSeconds(1));
        registry.TryConsume("client-a");
        _time.Advance(TimeSpan.FromSeconds(1));
        registry.TryConsume("client-b");

        Assert.False(registry.TryConsume("client-a").IsAllowed);
    }

    [Fact]
    public void InvalidRule_IsRejectedWhenTheRegistryIsCreated()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateRegistry(limit: 0));
    }

    [Fact]
    public void NullClientKey_IsRejected()
    {
        RateLimiterRegistry registry = CreateRegistry(limit: 1);

        Assert.Throws<ArgumentNullException>(() => registry.TryConsume(null!));
    }

    [Fact]
    public void ConcurrentRequestsFromManyClients_GiveEachClientExactlyItsLimit()
    {
        const int clients = 50;
        const int requestsPerClient = 200;
        const int limit = 10;
        RateLimiterRegistry registry = CreateRegistry(limit);

        // Cada request es un numero, el resto de dividirlo por la cantidad de clientes indica
        // a que cliente pertenece. Asi los requests de todos los clientes llegan mezclados.
        var allowedByClient = Enumerable.Range(0, clients * requestsPerClient)
            .AsParallel()
            .Select(request => request % clients)
            .Where(client => registry.TryConsume($"client-{client}").IsAllowed)
            .GroupBy(client => client)
            .ToDictionary(group => group.Key, group => group.Count());

        // Se creo un solo limitador por cliente y ninguno acepto de mas ni de menos.
        Assert.Equal(clients, registry.Count);
        Assert.Equal(clients, allowedByClient.Count);
        Assert.All(allowedByClient.Values, allowed => Assert.Equal(limit, allowed));
    }
}
