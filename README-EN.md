# Seq MCP Server

MCP server for [Seq](https://datalust.co/seq) — connects Claude and other LLM clients to structured logs so they can be searched, aggregated, and analysed via MCP tools.

![CI](https://github.com/weselow/seq-mcp-server/workflows/CI/badge.svg)
![Docker Build](https://github.com/weselow/seq-mcp-server/workflows/Docker%20Build%20and%20Push/badge.svg)
![Security](https://github.com/weselow/seq-mcp-server/workflows/Security%20and%20Code%20Quality/badge.svg)

> [Русская версия](README.md)

## When you need this

- Connect Claude Desktop to your Seq and analyse logs without copy-paste — **scenario 1 (stdio exe)**.
- Run a shared MCP server for a team or a remote client — **scenario 2 (Docker HTTP/SSE)**.
- Serve multiple customers with different Seq instances from a single server — **scenario 3 (multi-tenant HTTP)**.
- Try it locally without a separate Seq — **local sandbox (Docker Compose)**.

## Quick Start

### Scenario 1 — Claude Desktop + local Seq (stdio)

Single user, one Seq on the machine or LAN. The API key stays on the user's machine; no networking required.

1. Download the binary for your OS from the [Releases](https://github.com/weselow/seq-mcp-server/releases/latest) page:
   - `seq-mcp-stdio-win-x64.exe`
   - `seq-mcp-stdio-linux-x64`
   - `seq-mcp-stdio-osx-x64`

   Linux/macOS: `chmod +x seq-mcp-stdio-*`.

2. Add to `claude_desktop_config.json`:

   ```json
   {
     "mcpServers": {
       "seq": {
         "command": "/path/to/seq-mcp-stdio-...",
         "env": {
           "SEQ_URL": "http://localhost:5341",
           "SEQ_API_KEY": "your-api-key-if-needed"
         }
       }
     }
   }
   ```

3. Restart Claude Desktop.

Stdio-process logs go to stderr — Claude Desktop captures them in its own logs while stdout stays clean for JSON-RPC.

### Scenario 2 — Docker HTTP/SSE for a team

One server, many clients connecting by URL. Fits a team sharing one Seq.

```bash
docker run -d --name seq-mcp -p 5555:5555 \
  -e SEQ_URL=http://your-seq:5341 \
  -e SEQ_API_KEY=your-api-key \
  ghcr.io/weselow/seq-mcp-server:latest

curl http://localhost:5555/health
```

Claude Desktop config:

```json
{
  "mcpServers": {
    "seq": {
      "url": "http://localhost:5555/sse",
      "headers": {
        "X-Seq-Project-Scope": "MyWebApp"
      }
    }
  }
}
```

`url` is required. `headers` are optional and filter logs by project (see [Scope filtering](#scope-filtering)).

The server exposes two transports:

- `GET /sse` — Legacy SSE (Claude Desktop, classic MCP clients).
- `POST /` — Streamable HTTP per [MCP 2025-03-26](https://spec.modelcontextprotocol.io/) (newer clients).

### Scenario 3 — Multi-tenant HTTP

One MCP server fronts several Seq instances — the client passes the target URL in `X-Seq-Url` and the key in `X-Seq-ApiKey`. Fits SaaS, aggregators, internal platforms.

**Off by default** — without the flag, `X-Seq-Url` is silently ignored:

```bash
docker run -d --name seq-mcp -p 5555:5555 \
  -e SEQ_ALLOW_URL_OVERRIDE=true \
  -e SEQ_BLOCK_PRIVATE_HOSTS=true \
  ghcr.io/weselow/seq-mcp-server:latest
```

Example request (Streamable HTTP):

```bash
curl -X POST http://localhost:5555/ \
  -H "X-Seq-Url: https://tenant-a.seq.example.com" \
  -H "X-Seq-ApiKey: per-tenant-api-key" \
  -H "Content-Type: application/json" \
  -H "Accept: application/json, text/event-stream" \
  --data '{...MCP request...}'
```

Read [Multi-tenancy security](#multi-tenancy-security) before exposing this to the internet — without an authenticating reverse proxy and rate-limit, multi-tenant MCP must not be public.

### Local sandbox (Seq + MCP in Docker Compose)

To try the MCP server without a separate Seq:

```bash
docker-compose up -d
curl http://localhost:5555/health   # MCP
open http://localhost:8080          # Seq UI
```

Compose brings up Seq (UI on `8080`, ingestion on `5341`) and the MCP server (`5555`) on the same network. Seq data lives in a persistent volume. Tear down with data — `docker-compose down -v`.

## Configuration

### Environment variables

| Variable | Purpose | Default |
|---|---|---|
| `SEQ_URL` | Seq server URL | `http://localhost:8080` |
| `SEQ_API_KEY` | Seq API key | — |
| `SEQ_PROJECT_SCOPE` | Project name for scope filtering | — |
| `SEQ_SCOPE_FIELD` | Field used to filter the project | `Application` |
| `SEQ_ALLOW_URL_OVERRIDE` | Allow `X-Seq-Url` from requests (multi-tenant) | `false` |
| `SEQ_BLOCK_PRIVATE_HOSTS` | Block RFC1918 in outgoing connections | `false` |
| `PORT` | HTTP server port | `5555` |

The `SEQ_URL` default targets the bundled `docker-compose.yml`, where Seq exposes its UI on `8080`. For a standalone Seq on a developer machine, use `SEQ_URL=http://localhost:5341` (the standard ingestion port).

`SEQ_SERVER_URL` is accepted as a synonym of `SEQ_URL` for backward compatibility.

### Configuration sources

Each field reads from its source by its own rule — this is deliberate, for compatibility:

| Field | Winner | Rationale |
|---|---|---|
| `Url`, `ApiKey` | env > appsettings | Env overrides file — typical for containers |
| `ProjectScope`, `ScopeField` | appsettings > env | The file pins per-image project config |
| HTTP headers (`X-Seq-*`) | always above env and appsettings | Per-request override |

### Scope filtering

When multiple projects ship logs to the same Seq, the server can auto-append `<scope-field> = '<project>'` to every query. Saves LLM tokens and speeds analysis.

Sources, in priority order:

1. HTTP headers `X-Seq-Project-Scope` + `X-Seq-Scope-Field`.
2. `SEQ_PROJECT_SCOPE` + `SEQ_SCOPE_FIELD` (env).
3. `Seq:ProjectScope` + `Seq:ScopeField` (appsettings.json).

A user filter is `and`-joined:

```
user:   Level = 'Error'
final:  (Application = 'MyWebApp') and (Level = 'Error')
```

## Multi-tenancy security

Applies to **scenario 3**.

### What the server enforces

- Without `SEQ_ALLOW_URL_OVERRIDE=true`, `X-Seq-Url` is silently ignored and one warning is logged (without echoing the value).
- **URL validation** at the middleware level: scheme `http`/`https`, no credentials, no fragment, no NUL or CR/LF. Invalid URL → `400 Bad Request`; the header value is not echoed in the response.
- **Outgoing TCP connect filter** when the URL comes from a header:
  - loopback (`127.0.0.0/8`, `::1`) — always blocked;
  - link-local (`169.254.0.0/16`, including AWS IMDS `169.254.169.254`, `fe80::/10`) — always blocked;
  - RFC1918 (`10/8`, `172.16/12`, `192.168/16`) — blocked when `SEQ_BLOCK_PRIVATE_HOSTS=true`.
- **DNS resolution on every connect** closes DNS-rebinding: even if the domain first resolves to a public IP and later to loopback, the next connect re-checks the resolved address.
- `X-Seq-ApiKey` is never logged — it is a password-equivalent secret.

### What the operator must do before going public

- Put a reverse proxy (Nginx, Caddy, Traefik) in front, with TLS and client authentication (mTLS, OAuth2, API gateway). Without auth, multi-tenant MCP must not be exposed publicly.
- Enable rate-limit on `/` (Streamable HTTP) and `/sse` at the proxy — a client carrying `X-Seq-Url` can otherwise probe the internal network.
- Set `SEQ_BLOCK_PRIVATE_HOSTS=true` — otherwise RFC1918 addresses (internal VPC) remain reachable.

## What the MCP server exposes

### Tools (7)

| Name | Purpose | Key parameters |
|---|---|---|
| `seq_search_events` | Search events with a Seq filter | `filter`, `limit` |
| `seq_list_signals` | List saved signals | — |
| `seq_execute_sql` | SQL query against log data | `query` |
| `seq_create_signal` | Create signal/alert | `title`, `description`, `filter`, `isProtected` |
| `seq_update_signal` | Update a signal | `signalId`, `title?`, `description?`, `filter?` |
| `seq_delete_signal` | Delete a signal | `signalId` |
| `seq_get_apps` | Applications writing to Seq with event counts | `limit` |

All tools return structured JSON. Tool descriptions and parameter docs are in Russian (LLM-friendly for the project's primary audience).

### Resources (9)

URI scheme `seq://`:

- `events/latest` — latest 50 events, all levels.
- `events/errors` — latest 50 errors (`Error` + `Fatal`).
- `events/warnings` — latest 50 warnings.
- `events/exceptions` — latest 50 events with exceptions.
- `events/last-hour` — events from the last hour, up to 100.
- `events/today` — events from today, up to 200.
- `performance/slow` — operations with `Elapsed > 1000ms`, last 50.
- `signals` — all saved signals.
- `stats/summary` — last-hour event aggregation by level (SQL).

### Prompts (8)

Templates for routine analysis (in Russian):

| Name | Parameter | What it does |
|---|---|---|
| `seq_analyze_errors` | `period` (1h/24h/7d) | Top-5 errors, patterns, recommendations |
| `seq_top_exceptions` | `count` (10) | Exception grouping |
| `seq_activity_summary` | `period` | Summary by log levels |
| `seq_check_signals` | — | Check active signals |
| `seq_performance_check` | `period` | Slow operations, issues |
| `seq_trace_request` | `requestId` | Trace by RequestId/CorrelationId |
| `seq_security_audit` | `period` | Audit of auth/unauthorized events |
| `seq_daily_report` | — | Daily report |

## Health Check

`GET http://localhost:5555/health` — MCP server status and Seq availability.

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

When Seq is unreachable, the response is `503 Service Unavailable` with the same shape but `status: "unhealthy"` and details in `seqConnection.message`. Suitable for Kubernetes liveness/readiness probes and Prometheus scrape.

## Production deployment

### docker-compose

```yaml
services:
  seq-mcp:
    image: ghcr.io/weselow/seq-mcp-server:latest
    ports:
      - "5555:5555"
    environment:
      - SEQ_URL=http://your-seq:5341
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
        limits: { cpus: '0.5', memory: 512M }
        reservations: { cpus: '0.25', memory: 256M }
```

### Kubernetes

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: seq-mcp-server
spec:
  replicas: 2
  selector: { matchLabels: { app: seq-mcp } }
  template:
    metadata: { labels: { app: seq-mcp } }
    spec:
      containers:
        - name: seq-mcp
          image: ghcr.io/weselow/seq-mcp-server:latest
          ports: [{ containerPort: 5555 }]
          env:
            - { name: SEQ_URL, value: "http://seq-service:5341" }
            - name: SEQ_API_KEY
              valueFrom:
                secretKeyRef: { name: seq-credentials, key: api-key }
          livenessProbe:
            httpGet: { path: /health, port: 5555 }
            initialDelaySeconds: 10
            periodSeconds: 30
          readinessProbe:
            httpGet: { path: /health, port: 5555 }
            initialDelaySeconds: 5
            periodSeconds: 10
          resources:
            limits: { cpu: 500m, memory: 512Mi }
            requests: { cpu: 250m, memory: 256Mi }
```

## Architecture

- **Language / runtime**: C# / .NET 9.
- **MCP**: 2025-03-26, HTTP/SSE and stdio JSON-RPC.
- **Projects**:
  - `src/SeqMcp.Core` — shared library (models, services, tools, resources, prompts, options).
  - `src/SeqMcp.Http` — ASP.NET Core web app, Docker target.
  - `src/SeqMcp.Stdio` — single-file CLI exe, local stdio transport.
- **DI**: a single `ISeqConnectionFactory` (Singleton) with per-tenant `HttpClient` + `SeqConnection` pairs, LRU cache, lease/refcount for safe eviction.
- **HTTP client**: tuned `SocketsHttpHandler` — pooled connections, gzip, no redirects, no cookies.
- **Stdio logging**: strictly to stderr; stdout is reserved for JSON-RPC.

```
src/
├── SeqMcp.Core/
│   ├── Configuration/   — SeqOptions, SeqRequestContext, SeqOptionsLoader
│   ├── Hosting/         — DI extensions for MCP primitives
│   ├── Services/        — SeqApiClient, SeqConnectionFactory, HealthCheckService
│   ├── Tools/           — SeqTools
│   ├── Resources/       — SeqResources
│   ├── Prompts/         — SeqPrompts
│   └── Models/          — DTO
├── SeqMcp.Http/
│   ├── Middleware/      — SeqHeadersMiddleware, RequestLoggingMiddleware
│   └── Program.cs       — DI, MCP transport, /health
└── SeqMcp.Stdio/
    └── Program.cs       — stdio server entry point

tests/
├── SeqMcp.Tests/                    — unit tests for Core and Http (xUnit + FluentAssertions + Moq)
└── SeqMcp.Stdio.IntegrationTests/   — stdio via Process.Start, JSON-RPC handshake
```

## Building from source

### Requirements

- .NET 9 SDK.
- Optional: Docker (for the image), a running Seq (for integration tests).

### Stdio exe

```bash
dotnet publish src/SeqMcp.Stdio/SeqMcp.Stdio.csproj \
  -c Release -r <rid> -p:PublishSingleFile=true -p:SelfContained=true
# <rid>: win-x64 | linux-x64 | osx-x64
```

Output — a single-file binary under `src/SeqMcp.Stdio/bin/Release/net9.0/<rid>/publish/`.

### HTTP server

```bash
dotnet publish src/SeqMcp.Http/SeqMcp.Http.csproj -c Release -o ./publish
SEQ_URL=http://localhost:5341 dotnet ./publish/SeqMcp.Http.dll
```

### Docker image

```bash
docker build -t seq-mcp-server:local .
docker run -d -p 5555:5555 -e SEQ_URL=http://your-seq:5341 seq-mcp-server:local
```

### Tests

```bash
dotnet test                                                    # all 172 tests
dotnet test --collect:"XPlat Code Coverage"                    # with coverage
dotnet test --filter "FullyQualifiedName~SeqToolsTests"        # one class
```

172 tests = 169 unit + 3 stdio integration. Some tests are `Skip`-marked and need a live Seq on `http://localhost:5341`. Details — [docs/INTEGRATION_TESTS-EN.md](docs/INTEGRATION_TESTS-EN.md).

## Development

The project follows strict TDD:

- [docs/standards/GLOBAL-implementation-standard.md](docs/standards/GLOBAL-implementation-standard.md) — overall principles.
- [docs/standards/tdd-standard.md](docs/standards/tdd-standard.md) — RED → GREEN → REFACTOR cycle.

Rules:

1. Tests come first. Never amend the test to make code compile — change the code to fit the test.
2. Functions < 30 lines, cyclomatic complexity < 10, classes < 200 lines.
3. Method coverage > 60%.
4. Conventional commits.

CI/CD: three workflows — CI (build/test/lint), Docker (build/push to GHCR), Security (CodeQL + dependency check). Docs: [docs/CICD.md](docs/CICD.md).

## Dependencies

- `ModelContextProtocol` / `ModelContextProtocol.AspNetCore` 0.4.0-preview.2 — MCP SDK.
- `Seq.Api` 2025.2.2 — Seq client for signals and SQL.
- `Microsoft.Extensions.Hosting` / `Logging` 9.0.9.
- xUnit 2.9, FluentAssertions 8, Moq 4 — tests.

## License

MIT.

## Links

- [Model Context Protocol Specification](https://spec.modelcontextprotocol.io/)
- [Seq Documentation](https://docs.datalust.co/docs)
- [ModelContextProtocol C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)
