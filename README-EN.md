# Seq MCP Server

Model Context Protocol (MCP) server for Seq - enabling LLM applications to interact with Seq structured logging platform.

## ✨ Features

- **3 MCP Tools**: Event search, signals list, SQL queries
- **5 MCP Resources**: Quick access to latest events (seq://)
- **8 MCP Prompts**: Ready-made templates for log analysis (in Russian)
- **HTTP Transport**: Server-Sent Events (SSE) per MCP 2025-03-26 specification
- **Seq Integration**: Native integration with Seq.Api 2025.2.2
- **Token Optimization**: Concise descriptions for LLM context economy (~70% token savings)
- **Russian Language**: All descriptions and prompts in Russian for convenience of Russian users

## 🏗️ Architecture

- **Language**: C# / .NET 9 (ASP.NET Core)
- **Protocol**: MCP 2025-03-26 (Streamable HTTP/SSE)
- **Testing**: xUnit with 94.4% method coverage, 41.9% line coverage
- **Design**: Clean Architecture with strict TDD approach
- **DI**: Microsoft.Extensions.DependencyInjection
- **Logging**: ILogger with structured logging

## Project Structure

```
seq-mcp-server/
├── src/
│   └── SeqMcp/
│       ├── Configuration/      # Server configuration
│       ├── Services/           # Seq API client wrapper
│       ├── Tools/              # MCP tools
│       ├── Resources/          # MCP resources
│       ├── Prompts/            # MCP prompts
│       ├── Models/             # Data models
│       └── Program.cs          # Entry point
├── tests/
│   └── SeqMcp.Tests/           # Unit & integration tests
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
export PORT="3001"                                # Default: 3001
```

**Note:** You can use either `SEQ_URL` or `SEQ_SERVER_URL` - they are interchangeable. `SEQ_URL` takes priority.

## 🚀 Quick Start

### Prerequisites

- .NET 9 SDK
- Seq server running (local or remote)

### Build

```bash
dotnet build
```

### Run MCP Server

**For development:**
```bash
cd src/SeqMcp
dotnet run
```

**For production (publish):**
```bash
# Publish self-contained application
dotnet publish src/SeqMcp/SeqMcp.csproj -c Release -o ./publish

# Run published application
./publish/SeqMcp
```

Server will start on `http://localhost:3001`

### Test

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Development

This project follows strict TDD practices. See `docs/standards/` for:

- `GLOBAL-implementation-standard.md` - Core development principles
- `tdd-standard.md` - TDD process and rules

### Key Principles

1. **RED → GREEN → REFACTOR** cycle
2. Tests FIRST, code second
3. Never modify tests to fix compilation errors
4. Functions < 30 lines, complexity < 10

## 🛠️ MCP Tools

The server exposes 3 tools for interacting with Seq:

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

## 🔌 Using with Claude Desktop

### Step 1: Publish the project

```bash
dotnet publish src/SeqMcp/SeqMcp.csproj -c Release -o ./publish
```

### Step 2: Configure Claude Desktop

Add to your Claude Desktop config (`claude_desktop_config.json`):

**Windows:**
```json
{
  "mcpServers": {
    "seq": {
      "command": "M:\\repos\\seq-mcp-server\\publish\\SeqMcp.exe",
      "env": {
        "SEQ_URL": "http://localhost:8080",
        "SEQ_API_KEY": "your-api-key-if-needed"
      }
    }
  }
}
```

**Linux/macOS:**
```json
{
  "mcpServers": {
    "seq": {
      "command": "/path/to/seq-mcp-server/publish/SeqMcp",
      "env": {
        "SEQ_URL": "http://localhost:8080",
        "SEQ_API_KEY": "your-api-key-if-needed"
      }
    }
  }
}
```

**Alternative (development, slower):**
```json
{
  "mcpServers": {
    "seq": {
      "command": "dotnet",
      "args": ["run", "--no-build", "--project", "path/to/seq-mcp-server/src/SeqMcp/SeqMcp.csproj"],
      "env": {
        "SEQ_URL": "http://localhost:8080",
        "SEQ_API_KEY": "your-api-key-if-needed"
      }
    }
  }
}
```

## 📋 TODO / Roadmap

- [x] ~~Complete Seq.Api integration~~
- [x] ~~Add `seq_list_signals` tool~~
- [x] ~~Add `seq_execute_sql` tool~~
- [x] ~~MCP protocol implementation~~
- [x] ~~HTTP/SSE transport~~
- [x] ~~Error handling with logging~~
- [x] ~~MCP Resources (seq://events, seq://signals)~~
- [x] ~~MCP Prompts (common query templates)~~
- [ ] Docker containerization
- [ ] Integration tests with live Seq server
- [ ] CI/CD pipeline

## 📦 Dependencies

- **ModelContextProtocol.AspNetCore** 0.4.0-preview.2 - Official MCP SDK for ASP.NET Core
- **Seq.Api** 2025.2.2 - Official Seq HTTP API client
- **Microsoft.Extensions.Logging** 9.0.9 - Structured logging
- **xUnit** - Testing framework
- **FluentAssertions** - Fluent test assertions
- **Moq** - Mocking framework

## 🧪 Testing

### Run Tests

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test class
dotnet test --filter "FullyQualifiedName~SeqToolsTests"
```

### Test Statistics

- **Total Tests**: 19 unit tests + 5 integration tests (requires live Seq server)
- **Method Coverage**: 94.4%
- **Line Coverage**: 41.9%
- **Test Success Rate**: 100%

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

**Status**: ✅ **Production Ready** - Full MCP server with 3 tools, 5 resources, 8 prompts, HTTP transport, error handling, and comprehensive testing
