namespace RateLimiter.Core;

public sealed class SlidingWindowCounterOptions
{
    // limit: cantidad maxima de requests permitidos dentro de una ventana
    // window: duracion de la ventana
    public SlidingWindowCounterOptions(int limit, TimeSpan window)
    {
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "Limit must be positive.");
        }

        if (window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(window), window, "Window must be positive.");
        }

        Limit = limit;
        Window = window;
    }

    public int Limit { get; }

    public TimeSpan Window { get; }
}
