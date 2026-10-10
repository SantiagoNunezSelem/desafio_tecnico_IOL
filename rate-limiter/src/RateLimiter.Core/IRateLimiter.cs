namespace RateLimiter.Core;

// Interfaz comun de los algoritmos
public interface IRateLimiter
{
    // Registra un request y responde si se acepta o se rechaza.
    RateLimitDecision TryConsume();
}
