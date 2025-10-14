# Seq MCP Server

Model Context Protocol (MCP) server for Seq - enabling LLM applications to interact with Seq structured logging platform.

> [Русская версия](README.md)

## CI/CD Status

![CI](https://github.com/weselow/seq-mcp-server/workflows/CI/badge.svg)
![Docker Build](https://github.com/weselow/seq-mcp-server/workflows/Docker%20Build%20and%20Push/badge.svg)
![Security](https://github.com/weselow/seq-mcp-server/workflows/Security%20and%20Code%20Quality/badge.svg)

## ✨ Features

- **7 MCP Tools**: Event search, signal management, SQL queries, applications list
- **9 MCP Resources**: Quick access to latest events (seq://)
- **8 MCP Prompts**: Ready-made templates for log analysis (in Russian)
- **HTTP Transport**: Server-Sent Events (SSE) per MCP 2025-03-26 specification
- **Seq Integration**: Native integration with Seq.Api 2025.2.2
- **Scope Filtering**: Automatic filtering by project via HTTP headers/ENV
- **Production-Ready HttpClient**: Optimized Singleton with connection pooling
- **Health Check Endpoint**: Server and Seq connection monitoring
- **Token Optimization**: Concise descriptions for LLM context economy (~70% token savings)
- **Russian Language**: All descriptions and prompts in Russian for convenience of Russian users

## 🏗️ Architecture

- **Language**: C# / .NET 9 (ASP.NET Core)
- **Protocol**: MCP 2025-03-26 (Streamable HTTP/SSE)
- **Testing**: xUnit with 94.4% method coverage, 41.9% line coverage
- **Design**: Clean Architecture with strict TDD approach
- **DI**: Microsoft.Extensions.DependencyInjection
- **Logging**: ILogger with structured logging

## 🚀 Quick Start

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
# Build
dotnet build

# Publish
dotnet publish src/SeqMcp/SeqMcp.csproj -c Release -o ./publish
```

**Step 2: Start MCP Server**

Start the server in a separate terminal:

**Windows (PowerShell):**
```powershell
# Minimal configuration (required parameters only)
$env:SEQ_URL="http://localhost:5341"              # REQUIRED: Seq server URL
$env:SEQ_API_KEY="your-api-key"                   # OPTIONAL: API key (if Seq requires authentication)
.\publish\SeqMcp.exe
```

**Linux/macOS:**
```bash
# Minimal configuration (required parameters only)
export SEQ_URL="http://localhost:5341"            # REQUIRED: Seq server URL
export SEQ_API_KEY="your-api-key"                 # OPTIONAL: API key (if Seq requires authentication)
./publish/SeqMcp
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
│   └── SeqMcp/
│       ├── Configuration/      # Configuration (SeqServerConfig, SeqRequestContext)
│       ├── Services/           # Seq API client wrapper (SeqApiClient)
│       ├── Tools/              # MCP tools (SeqTools)
│       ├── Resources/          # MCP resources (SeqResources)
│       ├── Prompts/            # MCP prompts (SeqPrompts)
│       ├── Models/             # Data models (DTO)
│       └── Program.cs          # Entry point and DI configuration
├── tests/
│   └── SeqMcp.Tests/           # Unit and integration tests
└── docs/
    └── standards/              # Development standards
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
curl -H "X-Seq-Project-Scope: MyProject" \
     -H "X-Seq-Scope-Field: Application" \
     http://localhost:5555/mcp/v1
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

1. **Docker (recommended)** - run container and specify `"url": "http://localhost:5555/sse"` in config
2. **Without Docker** - publish project and specify `"url"` with server address in config

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

- **Unit tests**: 35 tests (always run)
- **Integration tests**: 13 tests (require Seq server, Skip by default)
- **Total**: 48 tests
- **Success rate**: 100% (35/35 unit tests passed)
- **Coverage**: Scope filtering (7), Health Check (8), Signal Management (9 integration)

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
