namespace RateLimiter.Core;

// Algoritmo "Sliding window counter".
// El tiempo se divide en ventanas fijas y se guardan solo dos contadores: los requests de la
// ventana actual y los de la ventana anterior. Con ellos se estima cuantos requests hubo en la
// ultima ventana "deslizante" (la que termina en este instante):
//
//     estimado = actual + anterior * (fraccion de la ventana anterior que cae en la deslizante)
//
// Ejemplo: limite 7 por minuto, 5 requests en el minuto anterior, 3 en el actual y
// ya paso el 30% del minuto actual. Estimado = 3 + 5 * 0.7 = 6.5
public class SlidingWindowCounter : IRateLimiter
{
    private readonly SlidingWindowCounterOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly object _lock = new();

    // Momento en que se creo el limitador. Permite contar las ventanas y el numero de ventana actual
    private readonly long _startTimestamp;

    private long _currentWindow;
    private int _currentCount;      // Requests ventana actual
    private int _previousCount;     // Requests ventana anterior

    public SlidingWindowCounter(SlidingWindowCounterOptions options, TimeProvider timeProvider)
    {
        _options = options;
        _timeProvider = timeProvider;
        _startTimestamp = timeProvider.GetTimestamp();
    }

    public RateLimitDecision TryConsume()
    {
        // Se bloquea la lectura y escritura de los contadores para evitar race conditions
        lock (_lock)
        {
            // Tiempo transcurrido desde que se creo el limitador (ahora - _startTimestamp)
            TimeSpan elapsed = _timeProvider.GetElapsedTime(_startTimestamp);

            // Numero de ventana actual: cuantas ventanas completas pasaron desde el inicio
            long currentWindow = elapsed.Ticks / _options.Window.Ticks;
            MoveToWindow(currentWindow);

            // Fraccion de la ventana actual que ya transcurrio. 0 al comenzar y tiende a 1 al terminar
            double elapsedCurrentFraction = (double)(elapsed.Ticks % _options.Window.Ticks) / _options.Window.Ticks;
            // Fraccion de la ventana anterior que todavia esta dentro de la ventana deslizante
            double previousWindowFraction = 1 - elapsedCurrentFraction;

            double estimated = _currentCount + _previousCount * previousWindowFraction;

            // Se rechaza si, contando este request, el estimado de la ventana deslizante supera el limite
            if (estimated + 1 > _options.Limit)
            {
                return new RateLimitDecision(false, _options.Limit, 0, GetRetryAfter(elapsedCurrentFraction));
            }

            _currentCount++;
            int remaining = (int)(_options.Limit - estimated - 1);
            return new RateLimitDecision(true, _options.Limit, remaining, TimeSpan.Zero);
        }
    }

    // Actualiza los contadores cuando el tiempo avanzo a una ventana nueva
    private void MoveToWindow(long window)
    {
        if (window == _currentWindow)
        {
            return;
        }

        // Si se avanzo exactamente una ventana, la actual pasa a ser la anterior
        // Si se avanzo mas de una, lo contado ya quedo fuera de la ventana deslizante
        bool isNextWindow = window == _currentWindow + 1;
        _previousCount = isNextWindow ? _currentCount : 0;
        _currentCount = 0;
        _currentWindow = window;
    }

    // Calcula cuanto falta para que el estimado baje lo suficiente y entre un request mas.
    private TimeSpan GetRetryAfter(double elapsedFraction)
    {
        // La ventana actual ya esta llena: hay que esperar a que termine. En la siguiente, estos
        // requests pasan a ser "la anterior" y se libera un lugar cada (ventana / limite).
        if (_currentCount == _options.Limit)
        {
            return _options.Window * (1 - elapsedFraction) + _options.Window / _options.Limit;
        }

        // Lo que sobra viene de la ventana anterior, que pesa cada vez menos. Se despeja de la
        // formula del estimado en que momento de la ventana actual queda libre un lugar.
        int freeSlots = _options.Limit - 1 - _currentCount;
        double neededFraction = 1 - (double)freeSlots / _previousCount;
        return _options.Window * (neededFraction - elapsedFraction);
    }
}
