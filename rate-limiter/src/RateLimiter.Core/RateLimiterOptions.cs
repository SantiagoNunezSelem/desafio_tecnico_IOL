namespace RateLimiter.Core;

public enum RateLimitAlgorithm
{
    TokenBucket,
    SlidingWindowCounter
}

// Las propiedades tienen set para poder cargarlas desde un archivo de configuracion.
public sealed class RateLimiterOptions
{
    public RateLimitAlgorithm Algorithm { get; set; }

    public int Limit { get; set; }

    public TimeSpan Window { get; set; }
}
