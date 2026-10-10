namespace RateLimiter.Core;

public class TokenBucket : IRateLimiter
{
    private readonly int _capacity;                     // Cantidad maxima de tokens del bucket, es decir, la mayor rafaga permitida
    private readonly double _refillRatePerSecond;       // Tokens que se recuperan por segundo
    private readonly TimeProvider _timeProvider;
    private readonly object _lock = new();

    private double _tokens;

    private long _lastRefillTimestamp;

    public TokenBucket(int capacity, double refillRatePerSecond, TimeProvider timeProvider)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be positive.");
        }

        if (refillRatePerSecond <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(refillRatePerSecond), refillRatePerSecond, "Refill rate must be positive.");
        }

        _capacity = capacity;
        _refillRatePerSecond = refillRatePerSecond;
        _timeProvider = timeProvider;
        _tokens = capacity;
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
            double available = Math.Min(_capacity, _tokens + elapsedSeconds * _refillRatePerSecond);

            if (available < 1)
            {
                // No cargamos nuevos tokens ya que no hay sufucientes para abastecer la llamada actual.
                // Esto tambien evita errores por redondeo en los tokens cuando las request se realizan en cortos periodos de tiempo.
                TimeSpan retryAfter = TimeSpan.FromSeconds((1 - available) / _refillRatePerSecond);
                return new RateLimitDecision(false, _capacity, 0, retryAfter);
            }

            _tokens = available - 1; // Request consume token
            _lastRefillTimestamp = now;
            return new RateLimitDecision(true, _capacity, (int)_tokens, TimeSpan.Zero);
        }
    }
}
