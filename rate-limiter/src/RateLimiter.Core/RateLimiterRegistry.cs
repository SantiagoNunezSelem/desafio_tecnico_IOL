namespace RateLimiter.Core;

// Mantiene un limitador independiente por cliente almacendado en "entries".
// Los limitadores se crean la primera vez que llega un request de ese cliente y se eliminan
// cuando el cliente deja de enviar requests, para que la memoria no crezca sin limite
public sealed class RateLimiterRegistry
{
    private readonly RateLimiterOptions _options;
    private readonly TimeProvider _timeProvider;

    // No se usa un ConcurrentDictionary. Buscar el rate limiter de un cliente y marcarlo como usado tienen que ser un solo paso,
    // y eso lo da un lock, no una coleccion concurrente. Asi la limpieza no elimina un rate limiter que un request esta por usar
    private readonly Dictionary<string, Entry> _entries = new();
    private readonly object _lock = new();

    // Tiempo sin requests a partir del cual un limitador se elimina.
    private readonly TimeSpan _inactivityLimit;

    private long _lastCleanupTimestamp;

    public RateLimiterRegistry(RateLimiterOptions options, TimeProvider timeProvider)
    {
        options.Validate();

        _options = options;
        _timeProvider = timeProvider;
        _inactivityLimit = options.Window * 2;
        _lastCleanupTimestamp = timeProvider.GetTimestamp();
    }

    // Cantidad de clientes con un limitador en memoria
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _entries.Count;
            }
        }
    }

    // Registra un request del cliente y responde si se acepta o se rechaza.
    public RateLimitDecision TryConsume(string clientKey)
    {
        ArgumentNullException.ThrowIfNull(clientKey);

        IRateLimiter limiter;

        // Este lock protege solo el diccionario. Buscar el limitador y marcarlo como usado es un
        // solo paso, asi la limpieza nunca elimina un limitador que se esta por usar
        lock (_lock)
        {
            long now = _timeProvider.GetTimestamp();
            RemoveInactiveEntries(now);
            limiter = GetOrCreateLimiter(clientKey, now);
        }

        // Se consume fuera del lock: cada limitador tiene el suyo, por lo que los requests de
        // clientes distintos no se esperan entre si
        return limiter.TryConsume();
    }

    private IRateLimiter GetOrCreateLimiter(string clientKey, long now)
    {
        if (!_entries.TryGetValue(clientKey, out Entry? entry))
        {
            entry = new Entry(RateLimiterFactory.Create(_options, _timeProvider));
            _entries.Add(clientKey, entry);
        }

        entry.LastUsedTimestamp = now;
        return entry.Limiter;
    }

    // Elimina los limitadores de los clientes inactivos. No con  timer, la limpieza se hace
    // durante un request, y como mucho una vez cada _inactivityLimit. Este metodo esta dentro del lock.
    private void RemoveInactiveEntries(long now)
    {
        if (_timeProvider.GetElapsedTime(_lastCleanupTimestamp, now) < _inactivityLimit)
        {
            return;
        }

        List<string> inactiveKeys = _entries
            .Where(pair => _timeProvider.GetElapsedTime(pair.Value.LastUsedTimestamp, now) >= _inactivityLimit)
            .Select(pair => pair.Key)
            .ToList();

        foreach (string key in inactiveKeys)
        {
            _entries.Remove(key);
        }

        _lastCleanupTimestamp = now;
    }

    // Un Rate Limiter junto con el momento de su ultimo request
    private sealed class Entry
    {
        public Entry(IRateLimiter limiter)
        {
            Limiter = limiter;
        }

        public IRateLimiter Limiter { get; }

        public long LastUsedTimestamp { get; set; }
    }
}
