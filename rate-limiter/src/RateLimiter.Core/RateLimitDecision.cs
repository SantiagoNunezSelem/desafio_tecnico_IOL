namespace RateLimiter.Core;

/// <summary>
/// El header de respuesta que retorna un rate limiter al consultar si la request fue aceptada o rechazada.
/// </summary>

// Se declara struct por ser un bloque de datos relacionados.
// Crear instancias por cada respuesta seria ineficiente. Deberia utilizar memoria el Garbaje Collector.
public readonly record struct RateLimitDecision(bool IsAllowed, int Limit, int Remaining, TimeSpan RetryAfter);
