# Deployment Guide

> [Русская версия](deployment.md)

## MCP Server Startup Methods

### 1. 🚀 Recommended: Published executable (Fast)

**Advantages:**
- ⚡ Instant startup without compilation
- 🎯 Optimized production build
- 💾 All dependencies included
- ✅ Perfect for Claude Desktop

**Commands:**
```bash
# Publish once
dotnet publish src/SeqMcp/SeqMcp.csproj -c Release -o ./publish

# Run instantly
./publish/SeqMcp.exe  # Windows
./publish/SeqMcp      # Linux/macOS
```

**Claude Desktop configuration (HTTP/SSE):**
```json
{
  "mcpServers": {
    "seq": {
      "url": "http://localhost:5555/sse",
      "headers": {
        "X-Seq-Project-Scope": "MyProject"
      }
    }
  }
}
```

**Note:** Server must be running BEFOREHAND. SEQ_URL and SEQ_API_KEY are passed via ENV when starting the server.

---

### 2. 🔧 For development: dotnet run (Slow)

**Advantages:**
- 🛠️ Convenient during active development
- 🔄 Automatic rebuild on changes

**Disadvantages:**
- ⏱️ Slow startup (compilation every time)
- ❌ Not recommended for Claude Desktop

**Commands:**
```bash
cd src/SeqMcp
dotnet run
```

---

### 3. ⚙️ Compromise: dotnet run --no-build (Medium)

**Advantages:**
- Doesn't compile on every run
- Uses last build

**Disadvantages:**
- Requires manual build before running
- Still slower than published executable

**Commands:**
```bash
# Build first
dotnet build src/SeqMcp/SeqMcp.csproj -c Release

# Then run
dotnet run --no-build --project src/SeqMcp/SeqMcp.csproj
```

---

## Startup Time Comparison

| Method | Startup Time | Recommendation |
|--------|-------------|----------------|
| **Published .exe** | ~0.5-1 sec | ✅ **Best for production** |
| **dotnet run --no-build** | ~2-3 sec | ⚠️ Acceptable |
| **dotnet run** | ~5-10 sec | ❌ Development only |

---

## Updating Published Version

After code changes:

```bash
# 1. Run tests
dotnet test

# 2. Republish
dotnet publish src/SeqMcp/SeqMcp.csproj -c Release -o ./publish

# 3. Restart Claude Desktop (if using)
```

---

## Docker (✅ Implemented)

Docker containerization is fully implemented:

```bash
# Run with Docker
docker run -d \
  --name seq-mcp \
  -p 5555:5555 \
  -e SEQ_URL=http://your-seq-server:8080 \
  -e SEQ_API_KEY=your-api-key-if-needed \
  ghcr.io/weselow/seq-mcp-server:latest
```

**Or with Docker Compose:**

```bash
docker-compose up -d
```

See [README-EN.md - Docker section](../README-EN.md#-docker-deployment)

---

## Recommendations

### For Claude Desktop users:
1. ✅ Use published executable
2. ✅ Specify full absolute path
3. ✅ Configure environment variables (SEQ_URL, SEQ_API_KEY)

### For developers:
1. 🛠️ Use `dotnet run` during active development
2. ✅ Switch to published .exe for final testing
3. ✅ Always run tests before publishing

### For production:
1. ✅ Only published executable
2. ✅ Configure monitoring and logging
3. ✅ Use systemd/supervisor for autostart (Linux)
4. ✅ Use Windows Service (Windows)
