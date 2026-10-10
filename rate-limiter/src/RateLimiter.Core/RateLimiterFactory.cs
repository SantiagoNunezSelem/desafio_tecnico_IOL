namespace RateLimiter.Core;

// Crea el Limiter que indica la configuracion
public static class RateLimiterFactory
{
    public static IRateLimiter Create(RateLimiterOptions options, TimeProvider timeProvider)
    {
        if (options.Window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.Window, "Window must be positive.");
        }

        switch (options.Algorithm)
        {
            case RateLimitAlgorithm.TokenBucket:
                // Se estandarizan los valores para ambos algoritmos. Se parte conceptualmente de la regla del Sliding Window Counter
                // para obtener los parametros del Token Bucket. La capacidad del bucket es el Limit y el RefillRatePerSecond es Limit / Window seconds.
                double refillRatePerSecond = options.Limit / options.Window.TotalSeconds;
                return new TokenBucket(new TokenBucketOptions(options.Limit, refillRatePerSecond), timeProvider);

            case RateLimitAlgorithm.SlidingWindowCounter:
                return new SlidingWindowCounter(new SlidingWindowCounterOptions(options.Limit, options.Window), timeProvider);

            default:
                throw new ArgumentOutOfRangeException(nameof(options), options.Algorithm, "Unknown algorithm.");
        }
    }
}
