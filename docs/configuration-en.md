# MCP Server Configuration

> [Русская версия](configuration.md)

## Configuration Sources

The server supports **3 configuration sources** with priority (from highest to lowest):

1. **Environment Variables** - highest priority
2. **appsettings.json** - configuration file
3. **Default Values** - built into code

## Environment Variables

### SEQ_URL or SEQ_SERVER_URL
Seq server URL address.

**Default:** `http://localhost:8080`

```bash
export SEQ_URL="http://localhost:8080"
# or
export SEQ_SERVER_URL="http://seq.example.com"
```

### SEQ_API_KEY
API key for Seq access (if authentication is required).

**Default:** empty string (no authentication)

```bash
export SEQ_API_KEY="your-secret-api-key"
```

### PORT
Port on which the MCP server will run.

**Default:** `5555`

```bash
export PORT="5555"
```

## appsettings.json

Configuration file is located at `src/SeqMcp/appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "McpServer": {
    "Port": 5555
  },
  "Seq": {
    "Url": "http://localhost:8080",
    "ApiKey": ""
  }
}
```

### Editing Configuration

**To change MCP server port:**
```json
{
  "McpServer": {
    "Port": 7777  // Change to desired port
  }
}
```

**To change Seq URL:**
```json
{
  "Seq": {
    "Url": "http://seq.mycompany.com",
    "ApiKey": "your-api-key-here"  // Optional
  }
}
```

## Using with Claude Code

### HTTP type (recommended)

**Important:** When using HTTP/SSE in Claude Desktop config:

1. **Server must be started BEFOREHAND** as a separate process
2. **Environment variables are NOT passed** via `env` in Claude Desktop config
3. **Configuration is taken from:**
   - Server process environment variables
   - appsettings.json

**Step 1: Start MCP server**

```bash
# Windows (PowerShell)
cd M:\repos\seq-mcp-server\publish
.\SeqMcp.exe

# Linux/macOS
cd /path/to/seq-mcp-server/publish
./SeqMcp
```

Or with environment variables:

```bash
# Windows (PowerShell)
$env:SEQ_URL="http://localhost:8080"
$env:SEQ_API_KEY="your-key"
$env:PORT="5555"
.\SeqMcp.exe

# Linux/macOS
SEQ_URL="http://localhost:8080" SEQ_API_KEY="your-key" PORT="5555" ./SeqMcp
```

**Step 2: Configure Claude Desktop**

```json
{
  "mcpServers": {
    "seq": {
      "url": "http://localhost:5555/sse",
      "headers": {
        "X-Seq-Project-Scope": "MyProject",
        "X-Seq-Scope-Field": "Application"
      }
    }
  }
}
```

## For Development

### Using appsettings.Development.json

Create `src/SeqMcp/appsettings.Development.json` for development settings:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information"
    }
  },
  "McpServer": {
    "Port": 5556  // Different port for dev
  },
  "Seq": {
    "Url": "http://localhost:8080",  // Local Seq
    "ApiKey": ""
  }
}
```

Run in development mode:

```bash
cd src/SeqMcp
dotnet run --environment Development
```

## Configuration Verification

On startup, the server outputs the used configuration:

```
info: SeqMcp[0]
      Seq MCP Server starting...
info: SeqMcp[0]
      Server URL: http://localhost:5555
info: SeqMcp[0]
      Seq URL: http://localhost:8080
info: SeqMcp[0]
      Transport: HTTP/SSE
```

Verify that:
- ✅ `Server URL` matches the port in Claude Desktop config
- ✅ `Seq URL` points to your Seq server
- ✅ `Transport: HTTP/SSE` is active

## Troubleshooting

### Problem: "Failed to reconnect to seq"

**Cause:** Server is not running or port doesn't match.

**Solution:**
1. Start server: `./publish/SeqMcp.exe`
2. Check port in logs: `Server URL: http://localhost:5555`
3. Ensure Claude Desktop config has same port: `"url": "http://localhost:5555/sse"`

### Problem: Server cannot connect to Seq

**Cause:** Incorrect Seq URL or API key.

**Solution:**
1. Check Seq URL in logs: `Seq URL: http://localhost:8080`
2. Verify Seq accessibility: `curl http://localhost:8080/api`
3. If Seq requires authentication, add API key in `appsettings.json` or `SEQ_API_KEY` variable

### Problem: Port already in use

**Cause:** Another process is using port 5555.

**Solution:**

**Option 1: Change port in appsettings.json**
```json
{
  "McpServer": {
    "Port": 6666  // Different port
  }
}
```

**Option 2: Use environment variable**
```bash
PORT=6666 ./SeqMcp
```

Don't forget to update Claude Desktop config:
```json
{
  "url": "http://localhost:6666/sse"
}
```

---

**See also:**
- [Deployment Guide](deployment-en.md)
- [README-EN](../README-EN.md)
