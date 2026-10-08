namespace RateLimiter.Core;

/// <summary>
/// A single token bucket: it starts full, each allowed request takes one token, and tokens
/// come back at a steady rate up to the capacity. Safe to call from several threads.
/// </summary>
/// <remarks>
/// There is no background timer adding tokens. The bucket remembers when it was last
/// updated and, on each request, adds the tokens earned since then. The result is the
/// same as a periodic refill, but an idle bucket costs nothing.
/// </remarks>
/// 

// Santi: es sealed para que no pueda ser utilizado como clase base (herencia)
public sealed class TokenBucket
{
    private readonly TokenBucketOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly object _lock = new();

    // Fractional, because a request can arrive when only part of a token has been earned.
    private double _tokens;

    // A reading of the monotonic clock (not a date), so system clock adjustments cannot
    // make time appear to go backwards or jump forwards.
    private long _lastRefillTimestamp;

    public TokenBucket(TokenBucketOptions options, TimeProvider timeProvider)
    {
        _options = options;
        _timeProvider = timeProvider;
        _tokens = options.Capacity;
        _lastRefillTimestamp = timeProvider.GetTimestamp();
    }

    /// <summary>
    /// Takes one token if there is one. A rejected request takes nothing, so retrying
    /// early does not push back the moment the next request will be allowed.
    /// </summary>
    public RateLimitDecision TryConsume()
    {
        // Refill, check and take must happen as one step. Otherwise two threads could both
        // see the last token and both take it.
        lock (_lock) // Santi: cuando se setea _lock para que no entren dos al mismo tiempo?
        {
            long now = _timeProvider.GetTimestamp();
            double elapsedSeconds = _timeProvider.GetElapsedTime(_lastRefillTimestamp, now).TotalSeconds;
            double available = Math.Min(_options.Capacity, _tokens + elapsedSeconds * _options.RefillRatePerSecond);

            if (available < 1)
            {
                // The state is left untouched on purpose. Saving the partial refill here would
                // add up many small fractions across rejected requests, and the rounding error
                // of each addition could leave the bucket just short of a whole token.
                TimeSpan retryAfter = TimeSpan.FromSeconds((1 - available) / _options.RefillRatePerSecond);
                return new RateLimitDecision(false, _options.Capacity, 0, retryAfter);
            }

            _tokens = available - 1;
            _lastRefillTimestamp = now;
            return new RateLimitDecision(true, _options.Capacity, (int)_tokens, TimeSpan.Zero);
        }
    }
}
