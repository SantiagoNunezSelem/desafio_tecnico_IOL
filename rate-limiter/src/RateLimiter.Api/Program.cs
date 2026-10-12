using RateLimiter.Api;
using RateLimiter.Core;

// La clase Program es publica para que los tests puedan levantar la API en memoria
public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        RateLimiterOptions options = builder.Configuration.GetSection("RateLimiter").Get<RateLimiterOptions>() ?? new();

        builder.Services.AddSingleton(new RateLimiterRegistry(options, TimeProvider.System));

        var app = builder.Build();

        app.UseMiddleware<RateLimitMiddleware>();

        app.MapGet("/ping", Ping);

        app.Run();
    }

    private static string Ping()
    {
        return "pong";
    }
}
