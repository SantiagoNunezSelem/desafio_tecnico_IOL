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

    public void Validate()
    {
        if (!Enum.IsDefined(Algorithm))
        {
            throw new ArgumentOutOfRangeException(nameof(Algorithm), Algorithm, "Unknown algorithm.");
        }

        if (Limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Limit), Limit, "Limit must be positive.");
        }

        if (Window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(Window), Window, "Window must be positive.");
        }
    }
}
