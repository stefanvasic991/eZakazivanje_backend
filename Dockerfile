FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["eZakazivanje.Api/eZakazivanje.Api.csproj", "eZakazivanje.Api/"]
COPY ["eZakazivanje.DataService/eZakazivanje.DataService.csproj", "eZakazivanje.DataService/"]
COPY ["eZakazivanje.Entity/eZakazivanje.Entity.csproj", "eZakazivanje.Entity/"]
RUN dotnet restore "eZakazivanje.Api/eZakazivanje.Api.csproj"

# Copy everything else and build
COPY . .
RUN dotnet build "eZakazivanje.Api/eZakazivanje.Api.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "eZakazivanje.Api/eZakazivanje.Api.csproj" -c Release -o /app/publish

# Final stage/image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Install curl for healthcheck
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copy published files
COPY --from=publish /app/publish .

# Create directories for uploads, config, and logs
RUN mkdir -p /app/wwwroot/uploads /app/config /app/logs

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:80
ENV DOTNET_USE_POLLING_FILE_WATCHER=1
ENV DOTNET_RUNNING_IN_CONTAINER=true

# Create non-root user
RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

# Define volumes
VOLUME ["/app/wwwroot/uploads", "/app/config"]

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:80/health || exit 1

EXPOSE 80
ENTRYPOINT ["dotnet", "eZakazivanje.Api.dll"]