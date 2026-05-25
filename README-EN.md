# Seq MCP Server

Model Context Protocol (MCP) server for Seq - enabling LLM applications to interact with Seq structured logging platform.

> [Русская версия](README.md)

## CI/CD Status

![CI](https://github.com/weselow/seq-mcp-server/workflows/CI/badge.svg)
![Docker Build](https://github.com/weselow/seq-mcp-server/workflows/Docker%20Build%20and%20Push/badge.svg)
![Security](https://github.com/weselow/seq-mcp-server/workflows/Security%20and%20Code%20Quality/badge.svg)

## ✨ Features

- **Two run modes**: HTTP/SSE server (Docker) for teams and stdio CLI (single-file exe) for individual use with MCP clients
- **7 MCP Tools**: Event search, signal management, SQL queries, applications list
- **9 MCP Resources**: Quick access to latest events (seq://)
- **8 MCP Prompts**: Ready-made templates for log analysis (in Russian)
- **HTTP Transport**: Server-Sent Events (SSE) per MCP 2025-03-26 specification
- **Multi-tenancy** (HTTP): one server serving multiple Seq targets via `X-Seq-Url`/`X-Seq-ApiKey` headers (off by default, SSRF protection when enabled)
- **Seq Integration**: Native integration with Seq.Api 2025.2.2
- **Scope Filtering**: Automatic filtering by project via HTTP headers/ENV
- **Health Check Endpoint**: Server and Seq connection monitoring
- **Token Optimization**: Concise descriptions for LLM context economy (~70% token savings)
- **Russian Language**: All descriptions and prompts in Russian for convenience of Russian users

## 🏗️ Architecture

- **Language**: C# / .NET 9
- **Protocol**: MCP 2025-03-26 (HTTP/SSE for Docker, stdio JSON-RPC for exe)
- **Project layout**:
  - `src/SeqMcp.Core` — shared library (Models, Services, Tools, Resources, Prompts)
  - `src/SeqMcp.Http` — ASP.NET Core web app (Docker target)
  - `src/SeqMcp.Stdio` — single-file CLI exe for local use
- **DI**: single `ISeqConnectionFactory` (Singleton) with per-tenant `HttpClient`+`SeqConnection`, LRU cache, lease/refcount for safe eviction
- **Testing**: xUnit, ~180 unit tests + integration via `Process.Start` for stdio
- **Logging**: ILogger with structured logging (in stdio — strictly to stderr; stdout is reserved for JSON-RPC)

## 🚀 Quick Start

Two ways to run the server:

- **Docker (HTTP/SSE server)** — recommended for teams, remote clients, shared deployments. See below.
- **Stdio exe** — single-file binary; the MCP client launches the process itself. The API key never leaves the user's machine. See the [🧷 Stdio mode](#-stdio-mode-local-exe) section.

### Running in Docker (recommended)

The easiest way - use the ready-made Docker image from GitHub Container Registry:

```bash
# 1. Start Seq MCP Server
docker run -d \
  --name seq-mcp \
  -p 5555:5555 \
  -e SEQ_URL=http://your-seq-server:5341 \
  -e SEQ_API_KEY=your-api-key-if-needed \
  ghcr.io/weselow/seq-mcp-server:latest

# 2. Check that it started
curl http://localhost:5555/health
```

**Required parameters:**
- `SEQ_URL` - your Seq server address (e.g., `http://localhost:5341`)

**Optional parameters:**
- `SEQ_API_KEY` - Seq API key (if authentication is required)
- `SEQ_PROJECT_SCOPE` - project name for log filtering (e.g., `"MyWebApp"`)
- `SEQ_SCOPE_FIELD` - field in logs for filtering (default `"Application"`)
- `PORT` - MCP server port (default `5555`)

**Why do you need filtering (Scope Filtering)?**

If several projects write to your Seq (WebApp, BackgroundService, API), then without filtering the LLM will receive logs from ALL projects, which:
- Wastes tokens on unnecessary logs
- Slows down finding the right information
- Overloads LLM context

With filtering by `Application = 'MyWebApp'` you will only get logs from your project, saving tokens and improving analysis accuracy.

### Claude Desktop Configuration

After starting the MCP server (Docker or locally), add it to Claude Desktop configuration:

> **HTTP-mode MCP endpoints** (pick what your client supports):
> - `GET http://localhost:5555/sse` — Legacy SSE transport, understood by Claude Desktop and other classic MCP clients. Used in the examples below.
> - `POST http://localhost:5555/` — Streamable HTTP transport (MCP spec 2025-03-26) for newer clients.

**Minimal configuration:**
```json
{
  "mcpServers": {
    "seq": {
      "url": "http://localhost:5555/sse"    // REQUIRED: MCP server URL
    }
  }
}
```

**Configuration with project filtering (recommended):**

**Windows** (`%APPDATA%\Claude\claude_desktop_config.json`):
```json
{
  "mcpServers": {
    "seq": {
      "url": "http://localhost:5555/sse",                     // REQUIRED: MCP server URL
      "headers": {
        "X-Seq-Project-Scope": "MyWebApp",                    // OPTIONAL: project filter (saves tokens!)
        "X-Seq-Scope-Field": "Application"                    // OPTIONAL: filtering field (default "Application")
      }
    }
  }
}
```

**Linux/macOS** (`~/.config/Claude/claude_desktop_config.json`):
```json
{
  "mcpServers": {
    "seq": {
      "url": "http://localhost:5555/sse",                     // REQUIRED: MCP server URL
      "headers": {
        "X-Seq-Project-Scope": "MyWebApp",                    // OPTIONAL: project filter (saves tokens!)
        "X-Seq-Scope-Field": "Application"                    // OPTIONAL: filtering field (default "Application")
      }
    }
  }
}
```

**Important:**
- Our server uses **HTTP/SSE transport**, so `url` is used, not `command`
- **Filtering via headers** (`X-Seq-Project-Scope`) allows getting only logs from the needed project - this saves LLM tokens and speeds up analysis
- After changing configuration, restart Claude Desktop
- Headers are passed in each HTTP request to the MCP server

**Note:** To run the full stack (Seq + MCP server) use Docker Compose - see [🐳 Docker](#-docker) section.

---

### Alternative: Running without Docker

**Requirements:**
- .NET 9 SDK
- Running Seq server

**Step 1: Build and Publish**
```bash
# Build the whole solution
dotnet build

# Publish the HTTP server
dotnet publish src/SeqMcp.Http/SeqMcp.Http.csproj -c Release -o ./publish
```

**Step 2: Start MCP Server**

Start the server in a separate terminal:

**Windows (PowerShell):**
```powershell
# Minimal configuration (required parameters only)
$env:SEQ_URL="http://localhost:5341"              # REQUIRED: Seq server URL
$env:SEQ_API_KEY="your-api-key"                   # OPTIONAL: API key (if Seq requires authentication)
dotnet .\publish\SeqMcp.Http.dll
```

**Linux/macOS:**
```bash
# Minimal configuration (required parameters only)
export SEQ_URL="http://localhost:5341"            # REQUIRED: Seq server URL
export SEQ_API_KEY="your-api-key"                 # OPTIONAL: API key (if Seq requires authentication)
dotnet ./publish/SeqMcp.Http.dll
```

**Additional environment variables (optional):**
```bash
export PORT="5555"                                # MCP server port (default 5555)
export SEQ_PROJECT_SCOPE="MyProject"              # Project filter (better to set via headers in Claude Desktop)
export SEQ_SCOPE_FIELD="Application"              # Filtering field (better to set via headers)
```

**Step 3: Claude Desktop Configuration**

After starting the server, add it to Claude Desktop configuration. **It is recommended to use filtering via headers** (see "Claude Desktop Configuration" section above):

```json
{
  "mcpServers": {
    "seq": {
      "url": "http://localhost:5555/sse",
      "headers": {
        "X-Seq-Project-Scope": "MyWebApp",                    // OPTIONAL: project filter - configure for your application
        "X-Seq-Scope-Field": "Application"                    // OPTIONAL: filtering field (default "Application")
      }
    }
  }
}
```

**Advantages of filtering via headers:**
- No need to restart MCP server when changing filter
- Can quickly switch between projects by changing only Claude Desktop config
- Filtering works at HTTP request level

**Note:** The server must be started BEFORE starting Claude Desktop. If you restart the server, also restart Claude Desktop to reconnect.

## Project Structure

```
seq-mcp-server/
├── src/
│   ├── SeqMcp.Core/                # Shared library
│   │   ├── Configuration/          # SeqOptions, SeqRequestContext, SeqOptionsLoader
│   │   ├── Hosting/                # DI extensions for MCP primitives
│   │   ├── Services/               # SeqApiClient, SeqConnectionFactory, HealthCheckService
│   │   ├── Tools/                  # MCP tools (SeqTools)
│   │   ├── Resources/              # MCP resources (SeqResources)
│   │   ├── Prompts/                # MCP prompts (SeqPrompts)
│   │   └── Models/                 # Data models (DTO)
│   ├── SeqMcp.Http/                # ASP.NET Core web app (Docker target)
│   │   ├── Middleware/             # SeqHeadersMiddleware, RequestLoggingMiddleware
│   │   ├── Program.cs              # HTTP server entry point, DI, /health
│   │   └── appsettings.json
│   └── SeqMcp.Stdio/               # Single-file CLI exe (stdio JSON-RPC)
│       ├── Program.cs              # Stdio server entry point
│       └── SeqMcp.Stdio.csproj
├── tests/
│   ├── SeqMcp.Tests/                       # Unit tests for Core and Http
│   └── SeqMcp.Stdio.IntegrationTests/      # Stdio integration tests via Process.Start
└── docs/
    └── standards/                          # Development standards
```

## ⚙️ Configuration

### Environment Variables

```bash
# Seq server connection (both variants supported)
export SEQ_URL="http://localhost:8080"           # Default: http://localhost:8080
export SEQ_SERVER_URL="http://localhost:8080"    # Alternative name (for compatibility)
export SEQ_API_KEY="your-api-key"                # Optional, for authenticated Seq instances

# MCP server port
export PORT="5555"                                # Default: 5555

# Project filtering (optional)
export SEQ_PROJECT_SCOPE="MyProject"             # Optional: filter by project name
export SEQ_SCOPE_FIELD="Application"             # Default: "Application"

# Multi-tenancy via HTTP headers (optional, see "Multi-tenancy" section)
export SEQ_ALLOW_URL_OVERRIDE="true"             # Default: false. Allows X-Seq-Url header from request
export SEQ_BLOCK_PRIVATE_HOSTS="true"            # Default: false. Recommended for public deployments
```

**Note:** You can use either `SEQ_URL` or `SEQ_SERVER_URL` - they are interchangeable. `SEQ_URL` takes priority.

### Project Filtering (Scope Filtering)

The server supports automatic event filtering by project/application. This is useful when multiple projects log to one Seq server.

**Configuration priority** (from highest to lowest):
1. **HTTP headers** (for HTTP MCP transport)
2. **Environment variables** (`SEQ_PROJECT_SCOPE`, `SEQ_SCOPE_FIELD`)
3. **appsettings.json** (`Seq:ProjectScope`, `Seq:ScopeField`)
4. **No filtering** (if nothing is specified)

**Usage examples:**

**Via HTTP headers:**
```bash
# MCP endpoint: POST / (Streamable HTTP) or GET /sse (Legacy SSE)
curl -H "X-Seq-Project-Scope: MyProject" \
     -H "X-Seq-Scope-Field: Application" \
     -H "Accept: text/event-stream" \
     http://localhost:5555/sse
```

**Via environment variables:**
```bash
export SEQ_PROJECT_SCOPE="MyProject"
export SEQ_SCOPE_FIELD="Application"
dotnet run
```

**Via appsettings.json:**
```json
{
  "Seq": {
    "Url": "http://localhost:8080",
    "ApiKey": "your-api-key",
    "ProjectScope": "MyProject",
    "ScopeField": "Application"
  }
}
```

When filtering is enabled, all requests automatically add the condition:
```
Application = 'MyProject'
```

If the user adds their own filter:
```
Level = 'Error'
```

The final filter will be:
```
(Application = 'MyProject') and (Level = 'Error')
```

### Multi-tenancy (HTTP mode)

By default the HTTP server talks to a single Seq instance defined in configuration (`SEQ_URL` / `SEQ_API_KEY`). To let one MCP server serve multiple tenants with different Seq instances, an optional header-based override mode is available:

| Header                | Purpose                                        | Requires flag                         |
|-----------------------|------------------------------------------------|---------------------------------------|
| `X-Seq-Url`           | Seq URL for the current request                | `SEQ_ALLOW_URL_OVERRIDE=true`         |
| `X-Seq-ApiKey`        | Seq API key for the current request            | always accepted                       |
| `X-Seq-Project-Scope` | Project scope filter (see above)               | always accepted                       |
| `X-Seq-Scope-Field`   | Scope filter field (see above)                 | always accepted                       |

**Enabling override mode:**

```bash
export SEQ_ALLOW_URL_OVERRIDE=true
# optional — paranoid mode (see below)
export SEQ_BLOCK_PRIVATE_HOSTS=true
```

**Example HTTP request:**

```bash
# POST to root / is the Streamable HTTP transport (MCP 2025-03-26)
curl -X POST http://localhost:5555/ \
  -H "X-Seq-Url: https://tenant-a.seq.example.com" \
  -H "X-Seq-ApiKey: per-tenant-api-key" \
  -H "Content-Type: application/json" \
  -H "Accept: application/json, text/event-stream" \
  --data '{...MCP request...}'
```

If `SEQ_ALLOW_URL_OVERRIDE` is unset or `false`, the `X-Seq-Url` header is silently ignored and a one-time warning is logged (without the header value). `X-Seq-ApiKey` is always accepted and never logged.

**SSRF protection.** When the URL comes from a header, a TCP-connect-level filter is enabled for the outgoing connection:

- loopback (`127.0.0.0/8`, `::1`) — always blocked;
- link-local (`169.254.0.0/16` including AWS IMDS `169.254.169.254`, `fe80::/10`) — always blocked;
- RFC1918 (`10/8`, `172.16/12`, `192.168/16`) — blocked only when `SEQ_BLOCK_PRIVATE_HOSTS=true`.

DNS resolution happens on every connection, which closes DNS-rebinding: even if a domain initially resolves to a public IP and then to loopback, the second connect still validates the resolved IP.

URL validation at middleware level (before the connection factory):

- scheme must be `http` or `https`;
- no credentials in the URL (`user:pass@host`);
- no fragment (`#...`);
- no null bytes or control chars (CR/LF — anti-injection);
- invalid URL → `400 Bad Request` (the header value is not echoed in the response body).

**Multi-tenancy deployment requirements:**

- Do **not** expose the MCP HTTP server publicly without authentication. Use a reverse proxy (Nginx, Caddy, Traefik) with TLS and client authentication (mTLS, OAuth2, API gateway).
- Enable **rate limiting** on the `/mcp` endpoint at the reverse proxy — a client carrying `X-Seq-Url` can try to scan the internal network.
- For public deployments enable `SEQ_BLOCK_PRIVATE_HOSTS=true` so RFC1918 addresses (internal network) are also blocked.
- `X-Seq-ApiKey` must never be logged: an API key is a password-equivalent secret.

### Health Check Endpoint

The server provides a `/health` endpoint for monitoring status and Seq server availability.

**URL**: `GET http://localhost:5555/health`

**Response on success (200 OK):**
```json
{
  "status": "healthy",
  "version": "1.0.0.0",
  "uptimeSeconds": 3600,
  "seqConnection": {
    "isHealthy": true,
    "message": "Connected to Seq server",
    "responseTimeMs": 45
  },
  "metrics": {
    "total_requests": 150,
    "uptime_seconds": 3600,
    "seq_response_time_ms": 45
  }
}
```

**Response on problems (503 Service Unavailable):**
```json
{
  "status": "unhealthy",
  "version": "1.0.0.0",
  "uptimeSeconds": 3600,
  "seqConnection": {
    "isHealthy": false,
    "message": "Connection failed: Connection refused",
    "responseTimeMs": 1000
  },
  "metrics": {
    "total_requests": 150,
    "uptime_seconds": 3600,
    "seq_response_time_ms": 1000
  }
}
```

**Metrics:**
- `total_requests` - total number of health check requests since startup
- `uptime_seconds` - server uptime in seconds
- `seq_response_time_ms` - Seq server response time in milliseconds

**Usage for monitoring:**
```bash
# Availability check
curl http://localhost:5555/health

# Kubernetes liveness probe
livenessProbe:
  httpGet:
    path: /health
    port: 5555
  initialDelaySeconds: 10
  periodSeconds: 30

# Prometheus monitoring
http://localhost:5555/health
```

## 🐳 Docker

### Quick Start with Docker Compose (recommended)

The easiest way to run Seq MCP Server together with Seq server:

```bash
# 1. Create .env file (optional)
cp .env.example .env
# Edit .env if needed

# 2. Start both services
docker-compose up -d

# 3. Check logs
docker-compose logs -f seq-mcp

# 4. Check health
curl http://localhost:5555/health

# 5. Open Seq UI
http://localhost:8080
```

**What's included:**
- `seq` - Seq log server (port 8080 UI, 5341 ingestion)
- `seq-mcp` - Seq MCP Server (port 5555)
- Health checks for both services
- Automatic dependency (MCP waits for Seq readiness)
- Persistent volume for Seq data

**Management:**
```bash
# Stop
docker-compose down

# Stop and remove data
docker-compose down -v

# Restart
docker-compose restart seq-mcp

# View status
docker-compose ps
```

### Building Docker Image

```bash
# Build image
docker build -t seq-mcp-server:latest .

# Run container
docker run -d \
  --name seq-mcp \
  -p 5555:5555 \
  -e SEQ_URL=http://your-seq-server:80 \
  -e SEQ_API_KEY=your-api-key \
  seq-mcp-server:latest

# Check logs
docker logs -f seq-mcp

# Check health
docker exec seq-mcp curl http://localhost:5555/health
```

### Docker Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `SEQ_URL` | Seq server URL | `http://localhost:8080` |
| `SEQ_API_KEY` | Seq API key (optional) | - |
| `SEQ_PROJECT_SCOPE` | Scope for filtering | - |
| `SEQ_SCOPE_FIELD` | Field for scope filtering | `Application` |
| `PORT` | MCP server port | `5555` |
| `ASPNETCORE_ENVIRONMENT` | ASP.NET Core environment | `Production` |

### Docker in Production

**Docker Compose file for production:**

```yaml
version: '3.8'

services:
  seq-mcp:
    image: your-registry/seq-mcp-server:latest
    container_name: seq-mcp-prod
    ports:
      - "5555:5555"
    environment:
      - SEQ_URL=http://your-seq-server:80
      - SEQ_API_KEY=${SEQ_API_KEY}
      - SEQ_PROJECT_SCOPE=ProductionApp
    restart: always
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:5555/health"]
      interval: 30s
      timeout: 10s
      retries: 3
    deploy:
      resources:
        limits:
          cpus: '0.5'
          memory: 512M
        reservations:
          cpus: '0.25'
          memory: 256M
```

**Kubernetes Deployment:**

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: seq-mcp-server
spec:
  replicas: 2
  selector:
    matchLabels:
      app: seq-mcp
  template:
    metadata:
      labels:
        app: seq-mcp
    spec:
      containers:
      - name: seq-mcp
        image: your-registry/seq-mcp-server:latest
        ports:
        - containerPort: 5555
        env:
        - name: SEQ_URL
          value: "http://seq-service:80"
        - name: SEQ_API_KEY
          valueFrom:
            secretKeyRef:
              name: seq-credentials
              key: api-key
        livenessProbe:
          httpGet:
            path: /health
            port: 5555
          initialDelaySeconds: 10
          periodSeconds: 30
        readinessProbe:
          httpGet:
            path: /health
            port: 5555
          initialDelaySeconds: 5
          periodSeconds: 10
        resources:
          limits:
            cpu: 500m
            memory: 512Mi
          requests:
            cpu: 250m
            memory: 256Mi
```

### Tests

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

## 🧷 Stdio mode (local exe)

In addition to the HTTP/SSE server, the project can be built as a standalone exe with stdio transport — the MCP client launches the process itself and communicates over stdin/stdout. Convenient for local use with Claude Desktop, Cline and other MCP clients: one process = one Seq, the API key never leaves the user's machine.

### Build

```bash
# Windows
dotnet publish src/SeqMcp.Stdio/SeqMcp.Stdio.csproj -c Release -r win-x64 -p:PublishSingleFile=true

# Linux
dotnet publish src/SeqMcp.Stdio/SeqMcp.Stdio.csproj -c Release -r linux-x64 -p:PublishSingleFile=true

# macOS
dotnet publish src/SeqMcp.Stdio/SeqMcp.Stdio.csproj -c Release -r osx-x64 -p:PublishSingleFile=true
```

The result is a single self-contained exe in `src/SeqMcp.Stdio/bin/Release/net9.0/<rid>/publish/`.

### MCP client configuration

Example for Claude Desktop (`claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "seq": {
      "command": "/path/to/SeqMcp.Stdio.exe",
      "env": {
        "SEQ_URL": "http://localhost:5341",
        "SEQ_API_KEY": "your-api-key-if-needed"
      }
    }
  }
}
```

Stdio server logs go to stderr (stdout is reserved for JSON-RPC), so they show up in the MCP client logs and don't corrupt the protocol.

## Development

The project follows strict TDD practices. See `docs/standards/`:

- `GLOBAL-implementation-standard.md` - Core development principles
- `tdd-standard.md` - TDD process and rules

### Key Principles

1. **RED → GREEN → REFACTOR** cycle
2. Tests FIRST, code second
3. Never modify tests to fix compilation errors
4. Functions < 30 lines, complexity < 10

## 🛠️ MCP Tools

The server provides 7 tools for working with Seq:

### 1. seq_search_events

Search and retrieve Seq log events with optional filtering.

**Parameters:**
- `filter` (string, optional): Seq query filter (e.g., `"Level = 'Error'"`, `"@Exception is not null"`). Default: "" (all events)
- `limit` (integer, optional): Maximum events to return. Default: 100

**Returns:** JSON with structured log events including:
- Event ID
- Timestamp
- Log level (Information, Warning, Error, etc.)
- Rendered message
- Exception details (if present)

**Example:**
```json
{
  "Events": [...],
  "TotalCount": 42
}
```

### 2. seq_list_signals

List all saved Seq signals (alerts/saved searches).

**Parameters:** None

**Returns:** JSON with signals including:
- Signal ID
- Title
- Description
- Filter query

**Example:**
```json
{
  "Signals": [...],
  "TotalCount": 5
}
```

### 3. seq_execute_sql

Execute SQL query against Seq log data.

**Parameters:**
- `query` (string, required): SQL query using Seq SQL syntax (e.g., `"select count(*) from stream where Level = 'Error'"`)

**Returns:** JSON with query results:
- Original query
- Result data (JSON string)
- Row count

**Example:**
```json
{
  "Query": "select count(*) from stream",
  "Result": "{...}",
  "RowCount": 1
}
```

### 4. seq_create_signal

Create a new signal/alert in Seq.

**Parameters:**
- `title` (string, required): Signal title
- `description` (string, optional): Signal description
- `filter` (string, optional): Seq filter for signal
- `isProtected` (boolean, optional): Protected signal (default false)

**Returns:** JSON with creation result:
- Created signal ID
- Title
- Success message

**Example:**
```json
{
  "SignalId": "signal-12345",
  "Title": "High Error Rate",
  "Message": "Signal 'High Error Rate' created successfully"
}
```

### 5. seq_update_signal

Update an existing signal.

**Parameters:**
- `signalId` (string, required): Signal ID to update
- `title` (string, optional): New title
- `description` (string, optional): New description
- `filter` (string, optional): New filter

**Returns:** JSON with update result:
- Signal ID
- Success message

**Example:**
```json
{
  "SignalId": "signal-12345",
  "Message": "Signal 'signal-12345' updated successfully"
}
```

### 6. seq_delete_signal

Delete a signal by ID.

**Parameters:**
- `signalId` (string, required): Signal ID to delete

**Returns:** JSON with deletion result:
- Signal ID
- Success message

**Example:**
```json
{
  "SignalId": "signal-12345",
  "Message": "Signal 'signal-12345' deleted successfully"
}
```

### 7. seq_get_apps

Get a list of applications logging to Seq.

**Parameters:**
- `limit` (integer, optional): Maximum number of applications (default 50)

**Returns:** JSON with applications list:
- List of applications with names and event counts
- Total count

**Example:**
```json
{
  "Applications": [
    {
      "Name": "WebApp",
      "EventCount": 15420
    },
    {
      "Name": "BackgroundService",
      "EventCount": 8932
    }
  ],
  "TotalCount": 2
}
```

## 📦 MCP Resources

Resources provide quick access to data via `seq://` URI scheme:

### 1. seq://events/latest
Latest 50 events from Seq (all levels)

### 2. seq://events/errors
Latest 50 errors (Error + Fatal levels)

### 3. seq://events/warnings
Latest 50 warnings (Warning level)

### 4. seq://events/exceptions
Events with exceptions (latest 50)

### 5. seq://signals
All saved Seq signals

### 6. seq://events/last-hour
Events from the last hour (all levels, up to 100)

### 7. seq://events/today
Events from today (all levels, up to 200)

### 8. seq://performance/slow
Slow operations with Elapsed > 1000ms (latest 50)

### 9. seq://stats/summary
Event statistics for the last hour by levels (SQL aggregation)

## 💡 MCP Prompts (Templates)

Ready-made prompts for typical log analysis tasks (in Russian):

### 1. seq_analyze_errors
**Parameter**: `period` (1h, 24h, 7d)

Error analysis for period with top-5, patterns and recommendations

### 2. seq_top_exceptions
**Parameter**: `count` (default: 10)

Top exceptions with grouping and analysis

### 3. seq_activity_summary
**Parameter**: `period` (1h, 24h, 7d)

Activity summary by logging levels

### 4. seq_check_signals
Check all active signals

### 5. seq_performance_check
**Parameter**: `period` (1h, 24h)

Performance analysis and issues

### 6. seq_trace_request
**Parameter**: `requestId` (required)

Request tracing by RequestId/CorrelationId

### 7. seq_security_audit
**Parameter**: `period` (1h, 24h, 7d)

Security events audit (auth, unauthorized, etc.)

### 8. seq_daily_report
Daily logs status report

## 🔌 Integration with Claude Desktop

Claude Desktop configuration is described in the [🚀 Quick Start](#-quick-start) section above.

**Two connection methods:**

1. **Docker / HTTP server** — run container or `dotnet ./publish/SeqMcp.Http.dll` and specify `"url": "http://localhost:5555/sse"` in config
2. **Stdio exe** — publish `SeqMcp.Stdio` as a single-file exe and specify `"command": "/path/to/SeqMcp.Stdio.exe"` in config (see [🧷 Stdio mode](#-stdio-mode-local-exe))

See detailed configuration examples for Windows/Linux/macOS in the "Quick Start" section.

## 📋 TODO / Roadmap

- [x] ~~Complete Seq.Api integration~~
- [x] ~~Add `seq_list_signals` tool~~
- [x] ~~Add `seq_execute_sql` tool~~
- [x] ~~MCP protocol implementation~~
- [x] ~~HTTP/SSE transport~~
- [x] ~~Error handling with logging~~
- [x] ~~MCP Resources (seq://events, seq://signals)~~
- [x] ~~MCP Prompts (query templates)~~
- [x] ~~Scope filtering (project filtering)~~
- [x] ~~Production-ready HttpClient with connection pooling~~
- [x] ~~Health Check endpoint~~
- [x] ~~Docker containerization (Dockerfile, docker-compose, .dockerignore)~~
- [x] ~~Additional MCP Resources (last-hour, today, slow, stats)~~
- [x] ~~Additional MCP Tools (create_signal, update_signal, delete_signal, get_apps)~~
- [x] ~~Integration tests with live Seq server (13 integration tests)~~
- [x] ~~CI/CD pipeline (GitHub Actions) - 3 workflows: CI, Docker, Security~~

**CI/CD Pipeline**: See [docs/CICD.md](docs/CICD.md) for complete documentation

## 📦 Dependencies

- **ModelContextProtocol.AspNetCore** 0.4.0-preview.2 - Official MCP SDK for ASP.NET Core
- **Seq.Api** 2025.2.2 - Official Seq HTTP API client
- **Microsoft.Extensions.Logging** 9.0.9 - Structured logging
- **xUnit** - Testing framework
- **FluentAssertions** - Fluent test assertions
- **Moq** - Mocking framework

## 🧪 Testing

### Running Tests

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test class
dotnet test --filter "FullyQualifiedName~SeqToolsTests"
```

### Test Statistics

- **Unit tests** (`SeqMcp.Tests`): ~180 tests (always run; includes `Skip`-marked integration tests that require a live Seq)
- **Stdio integration tests** (`SeqMcp.Stdio.IntegrationTests`): 4 tests that spin up a real stdio process via `Process.Start` and verify the JSON-RPC handshake
- **Coverage**: Scope filtering, Health Check, Signal Management, multi-tenancy (URL/ApiKey override), SSRF filter, stdio handshake

**Running integration tests:**
```bash
# 1. Start Seq via Docker
docker run -d --name seq-test -e ACCEPT_EULA=Y -p 5341:80 datalust/seq

# 2. Run tests (integration tests will remain Skip)
dotnet test

# 3. To run integration tests remove Skip attribute from tests
```

Detailed documentation: [docs/INTEGRATION_TESTS.md](docs/INTEGRATION_TESTS.md)

## 🤝 Contributing

1. Follow TDD standards in `docs/standards/`
2. Always write tests FIRST (RED → GREEN → REFACTOR)
3. Maintain >60% method coverage
4. All tests must pass before PR
5. Use conventional commits

## 📄 License

MIT

## 🔗 Links

- [Model Context Protocol Specification](https://spec.modelcontextprotocol.io/)
- [Seq Documentation](https://docs.datalust.co/docs)
- [ModelContextProtocol C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)

---

**Status**: ✅ **Production Ready** - Full-featured MCP server with 7 tools, 9 resources, 8 prompts, HTTP transport, error handling, and comprehensive testing
