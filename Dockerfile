# Seq MCP Server - Multi-stage Dockerfile
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files
COPY ["src/SeqMcp.Core/SeqMcp.Core.csproj", "src/SeqMcp.Core/"]
COPY ["src/SeqMcp.Http/SeqMcp.Http.csproj", "src/SeqMcp.Http/"]
COPY ["tests/SeqMcp.Tests/SeqMcp.Tests.csproj", "tests/SeqMcp.Tests/"]

# Restore dependencies
RUN dotnet restore "src/SeqMcp.Http/SeqMcp.Http.csproj"

# Copy source code
COPY . .

# Build and publish
WORKDIR "/src/src/SeqMcp.Http"
RUN dotnet build "SeqMcp.Http.csproj" -c Release -o /app/build
RUN dotnet publish "SeqMcp.Http.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Install curl for the container HEALTHCHECK (not included in the aspnet runtime image)
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Create non-root user for security
RUN groupadd -r seqmcp && useradd -r -g seqmcp seqmcp

# Copy published app from build stage
COPY --from=build /app/publish .

# Set ownership
RUN chown -R seqmcp:seqmcp /app

# Switch to non-root user
USER seqmcp

# Environment variables (can be overridden)
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:5555 \
    SEQ_URL=http://localhost:8080 \
    PORT=5555

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:5555/health || exit 1

# Expose port
EXPOSE 5555

# Entry point
ENTRYPOINT ["dotnet", "SeqMcp.Http.dll"]
