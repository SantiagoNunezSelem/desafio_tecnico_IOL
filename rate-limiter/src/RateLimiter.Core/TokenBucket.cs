namespace RateLimiter.Core;

public class TokenBucket : IRateLimiter
{
    private readonly TokenBucketOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly object _lock = new();

    private double _tokens;

    private long _lastRefillTimestamp;

    public TokenBucket(TokenBucketOptions options, TimeProvider timeProvider)
    {
        _options = options;
        _timeProvider = timeProvider;
        _tokens = options.Capacity;
        _lastRefillTimestamp = timeProvider.GetTimestamp();
    }

    // Metodo para consumir tokens del bucket en caso de que haya disponibles.
    public RateLimitDecision TryConsume()
    {
        // Se bloquea la lectura y escritura para evitar race conditions.
        lock (_lock) // Bloquea cuando el input parameter (key) es el mismo, de lo contario permite ejecutar el proceso concurrentemente.
        {
            long now = _timeProvider.GetTimestamp();
            double elapsedSeconds = _timeProvider.GetElapsedTime(_lastRefillTimestamp, now).TotalSeconds;
            double available = Math.Min(_options.Capacity, _tokens + elapsedSeconds * _options.RefillRatePerSecond);

            if (available < 1)
            {
                // No cargamos nuevos tokens ya que no hay sufucientes para abastecer la llamada actual.
                // Esto tambien evita errores por redondeo en los tokens cuando las request se realizan en cortos periodos de tiempo.
                TimeSpan retryAfter = TimeSpan.FromSeconds((1 - available) / _options.RefillRatePerSecond);
                return new RateLimitDecision(false, _options.Capacity, 0, retryAfter);
            }

            _tokens = available - 1; // Request consume token
            _lastRefillTimestamp = now;
            return new RateLimitDecision(true, _options.Capacity, (int)_tokens, TimeSpan.Zero);
        }
    }
}
