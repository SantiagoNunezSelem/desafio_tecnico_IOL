namespace RateLimiter.Core;

/// <summary>
/// The two parameters of the token bucket algorithm. Invalid values are rejected here,
/// once, so a <see cref="TokenBucket"/> never has to deal with them at request time.
/// </summary>
public sealed class TokenBucketOptions
{
    /// <param name="capacity">Maximum number of tokens the bucket holds, i.e. the largest burst allowed.</param>
    /// <param name="refillRatePerSecond">Tokens added per second, i.e. the sustained request rate allowed.</param>
    public TokenBucketOptions(int capacity, double refillRatePerSecond)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be positive.");
        }

        // Written as "not (finite and positive)" so that NaN is rejected too: NaN <= 0 is false.
        if (!(double.IsFinite(refillRatePerSecond) && refillRatePerSecond > 0))
        {
            throw new ArgumentOutOfRangeException(
                nameof(refillRatePerSecond), refillRatePerSecond, "Refill rate must be a positive, finite number.");
        }

        Capacity = capacity;
        RefillRatePerSecond = refillRatePerSecond;
    }

    public int Capacity { get; }

    public double RefillRatePerSecond { get; }
}
