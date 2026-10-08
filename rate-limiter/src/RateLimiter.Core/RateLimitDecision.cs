namespace RateLimiter.Core;

/// <summary>
/// The outcome of asking a rate limiter whether one request may proceed. It carries
/// everything a caller needs to inform the client (the X-Ratelimit-* headers).
/// </summary>
/// <param name="IsAllowed">Whether the request may proceed.</param>
/// <param name="Limit">The bucket capacity.</param>
/// <param name="Remaining">Whole tokens left after this decision.</param>
/// <param name="RetryAfter">How long to wait until a request can succeed; zero when allowed.</param>
/// 

// Santi: porque es un struct? En que se diferencia de una clase? Porque no hacerlo una interfaz de dato? o una clase con atributos?
public readonly record struct RateLimitDecision(bool IsAllowed, int Limit, int Remaining, TimeSpan RetryAfter);
