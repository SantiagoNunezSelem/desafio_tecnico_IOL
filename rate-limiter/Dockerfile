# Etapa 1: compila la API. Usa la imagen con el SDK de .NET, que es pesada.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source
COPY src/ src/
RUN dotnet publish src/RateLimiter.Api -c Release -o /app

# Etapa 2: la imagen final. Solo tiene el runtime de ASP.NET y la API ya compilada,
# sin el SDK ni el codigo fuente.
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .

# Puerto en el que escucha la API dentro del contenedor (el valor por defecto de .NET 8).
EXPOSE 8080
ENTRYPOINT ["dotnet", "RateLimiter.Api.dll"]
