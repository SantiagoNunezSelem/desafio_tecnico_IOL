namespace RateLimiter.Core;

public sealed class TokenBucketOptions
{
    // El refillRatePerSecond se aplica solo cuando hay suficientes tokens para ejecutar un request
    public TokenBucketOptions(int capacity, double refillRatePerSecond)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be positive.");
        }

        if (refillRatePerSecond <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(refillRatePerSecond), refillRatePerSecond, "Refill rate must be a positive");
        }

        Capacity = capacity;
        RefillRatePerSecond = refillRatePerSecond;
    }

    public int Capacity { get; }

    public double RefillRatePerSecond { get; }
}
